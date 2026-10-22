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
    }

    /// <summary>A shop: who keeps it, what it is called, what they say, what it sells.</summary>
    public class Shop
    {
        public string Id;
        public string Keeper;
        public string Title;
        public string Hello;
        public Goods[] Goods;
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
        };

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

        /// <summary>Lots of a thing a shop has left today.</summary>
        public int Left(Shop shop, Goods goods, double now)
        {
            Restock(now);
            sold.TryGetValue(shop.Id + "/" + goods.Item, out var n);

            return Mathf.Max(0, goods.Stock - n);
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

        /// <summary>Buys a basket — all of it or nothing: lots of each thing, paid from the bag's stones.</summary>
        public Deal Buy(Shop shop, IDictionary<string, int> lots, Bag bag, double now, out int paid)
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

            var total = Price(lots);

            if (bag.Count(ItemBook.Stone) < total)
            {
                return Deal.NoMoney;
            }

            bag.Take(ItemBook.Stone, total);
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
                    back += (pair.Value - lotsGot) * kind.Price;
                    sold.TryGetValue(shop.Id + "/" + pair.Key, out var n);
                    sold[shop.Id + "/" + pair.Key] = n + lotsGot;
                }
            }

            if (back > 0)
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
            Deal.NoMoney => "Не хватает первобытных камней",
            Deal.NoRoom => "В сумке нет места (или слишком тяжело)",
            Deal.NoStock => "Столько у торговца нет",
            Deal.Nothing => "Сделка пуста",
            _ => "Сделка заключена",
        };
    }
}
