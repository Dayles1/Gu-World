using System.Collections.Generic;
using GuWorld.Items;
using UnityEngine;

namespace GuWorld.Systems
{
    /// <summary>A thing a shop sells, and how many lots of it it has each day.</summary>
    public class Goods
    {
        public string Item;
        /// <summary>Lots a day (a lot is the item's Bundle: ten petals, one Gu).</summary>
        public int Stock;
        /// <summary>Its price here (0: the item's own); in merit at the clan's treasury.</summary>
        public int Price;
    }

    /// <summary>A shop: who keeps it, what it is called, what they say, what it sells.</summary>
    public class Shop
    {
        public string Id;
        public string Keeper;
        public string Title;
        public string Hello;
        public Goods[] Goods;
        /// <summary>Paid in merit with the clan (the treasury), not in stones.</summary>
        public bool ForMerit;
        /// <summary>Whether it buys things back (the treasury does not).</summary>
        public bool Buys = true;
        /// <summary>How many of its Gu it has out on a day (0: all of them); its prices go up and down a little from day to day.</summary>
        public int GuADay;
        public bool Haggles;
    }

    /// <summary>How a deal went.</summary>
    public enum Deal
    {
        Done,
        /// <summary>Nothing in the deal.</summary>
        Nothing,
        NoMoney,
        NoRoom,
        NoStock,
    }

    /// <summary>
    /// Buying and selling for primeval stones (the design's shop page). Prices
    /// are the items' own (ItemBook): what a shop asks for a lot, and half of
    /// it — rounded down — for what it buys back. Each shop's stock is full
    /// again every midnight. The village has two: Baoshan's Gu goods by the
    /// square (Gu, their food, a healing salve), and auntie Hua's noodles, buns
    /// and wine.
    /// </summary>
    public class Trade
    {
        public static readonly Shop[] Shops =
        {
            new Shop
            {
                Id = "baoshan",
                Keeper = "Гу Юэ Баошань",
                Title = "Лавка Баошаня · Гу и корм для Гу",
                Hello = "Корм для Гу, Гу для мастеров, мазь — для ран. Лепестки свежие, утренние — десяток за камень, как везде в деревне.",
                Goods = BaoshanGoods(),
                GuADay = 9,
                Haggles = true,
            },
            new Shop
            {
                Id = "hua",
                Keeper = "Тётушка Хуа",
                Title = "Лапшичная тётушки Хуа",
                Hello = "Садись, садись. Лапша горячая, булочки только с пара, вино — из свежего бамбука. Есть и острое байское. Мастерам Гу — за камни.",
                Goods = new[]
                {
                    new Goods { Item = ItemBook.Noodles, Stock = 20 },
                    new Goods { Item = ItemBook.MeatBuns, Stock = 15 },
                    new Goods { Item = ItemBook.BambooWine, Stock = 10 },
                    new Goods { Item = ItemBook.WhiteGrains, Stock = 4 },
                },
            },
            new Shop
            {
                Id = "treasury",
                Keeper = SideQuests.Steward,
                Title = "Сокровищница клана · за вклад",
                Hello = "Сокровищница открыта тем, кто служит клану. Камнями здесь не платят — только вкладом: поручениями, головами врагов, делом.",
                Goods = TreasuryGoods(),
                ForMerit = true,
                Buys = false,
            },
        };

        /// <summary>
        /// The treasury's goods, for merit: the Gu no shop sells and no beast carries (a Gu Master earns them from the
        /// clan) — dearer the higher their rank — and the relics that lift a small realm.
        /// </summary>
        static Goods[] TreasuryGoods()
        {
            var ids = new[]
            {
                "iron_bone", "ice_muscles", "eagle_wings", "water_trace", "breath_concealment", "chasing_wind", "wolf_sprint", "wolf_howl",
                "wolf_enslavement", "therapy_light", "shadow_follower", "onion_explosion", "frost_arrow", "hand_blade", "broadsword_light",
                "blue_bird_ice_coffin", "water_prison", "lava_explosion", "tornado", "fiery_snake", "immobilization", "ivory_armor",
                "white_silver_relic", "yellow_gold_relic", "essence_iron_bone", "fire_dragon", "water_curtain_skyflower",
            };
            var goods = new List<Goods>();

            foreach (var id in ids)
            {
                var gu = GuWorld.Cultivation.GuBook.Find(id);

                if (gu != null)
                {
                    goods.Add(new Goods { Item = gu.Item, Stock = 1, Price = 10 * (1 << Mathf.Clamp(gu.Rank - 1, 0, 4)) + (gu.OneUse ? 10 : 0) });
                }
            }

            return goods.ToArray();
        }

        /// <summary>
        /// Baoshan's goods: the Gu's food, the salve, and every Gu of the catalogue that has a
        /// price — the rank-one ones one a day, the dearer rank-two ones now and then.
        /// </summary>
        static Goods[] BaoshanGoods()
        {
            var goods = new List<Goods>
            {
                new Goods { Item = ItemBook.Petal, Stock = 30 },
                new Goods { Item = ItemBook.JadeRock, Stock = 20 },
                new Goods { Item = ItemBook.IntimateGrass, Stock = 20 },
                new Goods { Item = ItemBook.RawMeat, Stock = 20 },
                new Goods { Item = ItemBook.BeastBone, Stock = 15 },
                new Goods { Item = ItemBook.IronSand, Stock = 20 },
                new Goods { Item = ItemBook.SpringWater, Stock = 20 },
                new Goods { Item = ItemBook.Charcoal, Stock = 20 },
                new Goods { Item = ItemBook.Salve, Stock = 5 },
            };

            foreach (var gu in GuWorld.Cultivation.GuBook.All)
            {
                if (gu.Price > 0)
                {
                    goods.Add(new Goods { Item = gu.Item, Stock = 1 });
                }
            }

            return goods.ToArray();
        }

        /// <summary>The shop a villager keeps (null: none).</summary>
        public static Shop Of(string keeper) => System.Array.Find(Shops, s => s.Keeper == keeper);

        readonly Dictionary<string, int> sold = new Dictionary<string, int>();
        long day = long.MinValue;

        /// <summary>What a shop pays for things of a kind (half its price, rounded down; 0: it does not buy them).</summary>
        public static int SellPrice(ItemKind kind, int count) =>
            kind == null || kind.Price <= 0 ? 0 : Mathf.FloorToInt(kind.Price * count / (float)kind.Bundle * 0.5f);

        /// <summary>Lots of a thing a shop has left today (none of a Gu it has not out today).</summary>
        public int Left(Shop shop, Goods goods, double now)
        {
            Restock(now);

            if (!OutToday(shop, goods, now))
            {
                return 0;
            }

            sold.TryGetValue(shop.Id + "/" + goods.Item, out var n);

            return Mathf.Max(0, goods.Stock - n);
        }

        static int DayOf(double now) => (int)System.Math.Floor(now / (24 * 60));

        /// <summary>Whether a shop has a thing out today: everything, but of its Gu (GuADay) only that many, another lot each day.</summary>
        public static bool OutToday(Shop shop, Goods goods, double now)
        {
            if (shop.GuADay <= 0 || ItemBook.Find(goods.Item)?.Category != ItemCategory.Gu)
            {
                return true;
            }

            var gu = new List<Goods>();

            foreach (var g in shop.Goods)
            {
                if (ItemBook.Find(g.Item)?.Category == ItemCategory.Gu)
                {
                    gu.Add(g);
                }
            }

            // The day's lot: the Gu shuffled by the day, the first so many of them.
            var random = new System.Random(DayOf(now) * 3571 + shop.Id.Length);

            for (var i = gu.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (gu[i], gu[j]) = (gu[j], gu[i]);
            }

            return gu.IndexOf(goods) < shop.GuADay;
        }

        /// <summary>
        /// What a lot costs here today: the goods' own price (or the item's), a little up or down from day to day where the
        /// keeper haggles (±15%), less the hero's discount with the clan; never under one.
        /// </summary>
        public static int PriceOf(Shop shop, string item, double now, float discount)
        {
            var goods = System.Array.Find(shop.Goods, g => g.Item == item);
            var price = goods != null && goods.Price > 0 ? goods.Price : ItemBook.Find(item)?.Price ?? 0;

            if (price <= 0)
            {
                return 0;
            }

            var scale = 1f;

            if (shop.Haggles && price >= 10)
            {
                var day = new System.Random(DayOf(now) * 7919 + item.GetHashCode());
                scale += ((float)day.NextDouble() - 0.5f) * 0.3f;
            }

            return Mathf.Max(1, Mathf.RoundToInt(price * scale * (1 - Mathf.Clamp(discount, 0, 0.5f))));
        }

        /// <summary>The price of a basket here today.</summary>
        public static int Price(Shop shop, IDictionary<string, int> lots, double now, float discount)
        {
            var total = 0;

            foreach (var pair in lots)
            {
                total += PriceOf(shop, pair.Key, now, discount) * Mathf.Max(0, pair.Value);
            }

            return total;
        }

        /// <summary>What was sold today, for the save (the day, and lots by "shop/item").</summary>
        public void Save(State.WorldSave into)
        {
            into.trade_day = day;
            into.sold_keys = new List<string>(sold.Keys).ToArray();
            into.sold_counts = new int[into.sold_keys.Length];

            for (var i = 0; i < into.sold_keys.Length; i++)
            {
                into.sold_counts[i] = sold[into.sold_keys[i]];
            }
        }

        /// <summary>What was sold, from a save (null: the shops full).</summary>
        public void Load(State.WorldSave from)
        {
            sold.Clear();
            day = long.MinValue;

            if (from?.sold_keys == null || from.sold_counts == null)
            {
                return;
            }

            day = from.trade_day;

            for (var i = 0; i < from.sold_keys.Length && i < from.sold_counts.Length; i++)
            {
                if (from.sold_keys[i] != null && from.sold_counts[i] > 0)
                {
                    sold[from.sold_keys[i]] = from.sold_counts[i];
                }
            }
        }

        /// <summary>Each shop's stock is full again at midnight.</summary>
        void Restock(double now)
        {
            var today = (long)System.Math.Floor(now / (24 * 60));

            if (today != day)
            {
                day = today;
                sold.Clear();
            }
        }

        /// <summary>The price of a basket (lots of each thing).</summary>
        public static int Price(IDictionary<string, int> lots)
        {
            var total = 0;

            foreach (var pair in lots)
            {
                total += (ItemBook.Find(pair.Key)?.Price ?? 0) * Mathf.Max(0, pair.Value);
            }

            return total;
        }

        /// <summary>
        /// Buys a basket — all of it or nothing: lots of each thing, paid from the bag's stones (or, at the treasury, from the
        /// merit given, which comes back less what was spent), the discount taken off.
        /// </summary>
        public Deal Buy(Shop shop, IDictionary<string, int> lots, Bag bag, double now, out int paid, ref int merit, float discount = 0)
        {
            paid = 0;
            var any = false;

            foreach (var pair in lots)
            {
                if (pair.Value <= 0)
                {
                    continue;
                }

                any = true;
                var goods = System.Array.Find(shop.Goods, g => g.Item == pair.Key);

                if (goods == null || Left(shop, goods, now) < pair.Value)
                {
                    return Deal.NoStock;
                }

                var kind = ItemBook.Find(pair.Key);

                if (bag.Room(pair.Key, now) < pair.Value * kind.Bundle)
                {
                    return Deal.NoRoom;
                }
            }

            if (!any)
            {
                return Deal.Nothing;
            }

            var total = Price(shop, lots, now, discount);

            if ((shop.ForMerit ? merit : bag.Count(ItemBook.Stone)) < total)
            {
                return Deal.NoMoney;
            }

            if (shop.ForMerit)
            {
                merit -= total;
            }
            else
            {
                bag.Take(ItemBook.Stone, total);
            }

            var back = 0;

            foreach (var pair in lots)
            {
                if (pair.Value > 0)
                {
                    var kind = ItemBook.Find(pair.Key);
                    var wanted = pair.Value * kind.Bundle;
                    var got = bag.Add(pair.Key, wanted, now);
                    // Each thing was checked to fit, but not all of them together: what did not go in is not paid for.
                    var lotsGot = got / kind.Bundle;
                    back += (pair.Value - lotsGot) * PriceOf(shop, pair.Key, now, discount);
                    sold.TryGetValue(shop.Id + "/" + pair.Key, out var n);
                    sold[shop.Id + "/" + pair.Key] = n + lotsGot;
                }
            }

            if (back > 0 && shop.ForMerit)
            {
                merit += back;
            }
            else if (back > 0)
            {
                bag.Add(ItemBook.Stone, back, now);
            }

            paid = total - back;

            return Deal.Done;
        }

        /// <summary>Sells some of one cell's pile; returns the stones got (0: not bought).</summary>
        public int Sell(Bag bag, int cell, int count, double now)
        {
            var stack = bag[cell];
            var kind = stack?.Kind;
            count = Mathf.Min(count, stack?.count ?? 0);
            var price = SellPrice(kind, count);

            if (price <= 0 || kind.Id == ItemBook.Stone)
            {
                return 0;
            }

            // The stones must fit (selling the whole pile frees its cell for them).
            var room = bag.Room(ItemBook.Stone, now) + (count >= stack.count ? ItemBook.Find(ItemBook.Stone).Stack : 0);

            if (room < price)
            {
                return 0;
            }

            bag.TakeFrom(cell, count);
            bag.Add(ItemBook.Stone, price, now);

            return price;
        }

        public static string Explain(Deal deal) => deal switch
        {
            Deal.NoMoney => "Не хватает первобытных камней (или вклада)",
            Deal.NoRoom => "В сумке нет места (или слишком тяжело)",
            Deal.NoStock => "Столько у торговца нет",
            Deal.Nothing => "Сделка пуста",
            _ => "Сделка заключена",
        };
    }
}
