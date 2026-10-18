using System.Collections.Generic;
using GuWorld.Cultivation;
using GuWorld.Items;
using GuWorld.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// A shop (the design's "Лавка торговца"): the keeper and what they say on
    /// the left, and under them the thing chosen in the list (a click on its
    /// row) — what it is and what it does; buying or selling in the middle — the
    /// goods by kind, how many are left, the price, and how many to take; the
    /// deal on the right — what is in it, its sum, and the stones left after it.
    /// </summary>
    public partial class Menus
    {
        bool shopSelling;
        int shopFilter = -1;
        readonly Dictionary<string, int> shopLots = new Dictionary<string, int>();
        /// <summary>What is being sold: a bag cell → (the thing's id, how many).</summary>
        readonly Dictionary<int, (string id, int count)> shopSales = new Dictionary<int, (string, int)>();
        /// <summary>The thing whose details show under the keeper (null: the first of the goods).</summary>
        string shopChosen;
        /// <summary>How far down the goods the list is scrolled (the page is built again on every click: it stays there).</summary>
        float shopScroll;

        /// <summary>Opens the shop the hero is at, with an empty deal.</summary>
        public void OpenShop()
        {
            shopSelling = false;
            shopFilter = -1;
            shopLots.Clear();
            shopSales.Clear();
            shopChosen = null;
            shopScroll = 0;
            Show(Page.Shop);
        }

        void ShopPage()
        {
            var shop = host.Shop;

            if (shop == null)
            {
                host.Resume();

                return;
            }

            var gu = host.Cultivator;
            var now = host.Now;
            Backdrop();

            // The head: the seal, the shop, buying or selling, the purse.
            var top = UiKit.Rect("Top", pageRoot).Place(new Vector2(0, 1), Vector2.one, 48, -112, 48, 28);
            var seal = UiKit.SealMark("coin", top, 60);
            seal.rectTransform.At(new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(60, 60));
            var title = UiKit.Label($"<b>Лавка</b>  <size=18><color=#A69A86>{shop.Keeper} · деревня Гу Юэ</color></size>", top, 32, UiKit.Text);
            title.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 82, 0, -760, 0);
            var modes = UiKit.Rect("Modes", top).Place(Vector2.zero, new Vector2(0, 1), 780, 12, -1180, 12);
            modes.Stack<HorizontalLayoutGroup>(8).childForceExpandWidth = true;
            UiKit.Button(UiKit.Spaced("Купить"), modes, () => { shopSelling = false; shopScroll = 0; Show(Page.Shop); }, shopSelling ? ButtonKind.Plain : ButtonKind.Tab, 16).Size(52, -1, 1);
            UiKit.Button(UiKit.Spaced("Продать"), modes, () => { shopSelling = true; shopScroll = 0; Show(Page.Shop); }, shopSelling ? ButtonKind.Tab : ButtonKind.Plain, 16).Size(52, -1, 1);
            var close = UiKit.Button("Закрыть · Esc", top, host.Resume, ButtonKind.Plain, 16);
            ((RectTransform)close.transform).Place(new Vector2(1, 0), Vector2.one, -190, 12, 0, 12);
            var coin = UiKit.Icon("stone", top, 24, UiKit.Gold);
            coin.rectTransform.Place(new Vector2(1, 0), Vector2.one, -420, 0, 380, 0);
            var purse = UiKit.Label($"<b>{gu.Stones}</b>  <color=#A69A86>камней</color>", top, 20, UiKit.Text);
            purse.rectTransform.Place(new Vector2(1, 0), Vector2.one, -376, 0, 206, 0);
            var body = UiKit.Rect("Body", pageRoot).Place(Vector2.zero, Vector2.one, 48, 36, 48, 140);

            // The keeper.
            var left = UiKit.Panel("Keeper", body);
            left.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -360, 0);
            var keeper = UiKit.Rect("Column", left.transform).Fill();
            keeper.Stack<VerticalLayoutGroup>(10, new RectOffset(26, 26, 24, 22));
            UiKit.Label(shop.Keeper, keeper, 24, UiKit.GoldLit, TextAnchor.MiddleLeft, FontStyle.Bold).Size(34);
            UiKit.Caps(shop.Title, keeper, 12, UiKit.Muted).Size(40);
            UiKit.Label($"«{shop.Hello}»", keeper, 18, UiKit.Text, TextAnchor.UpperLeft, FontStyle.Italic).Size(118);
            UiKit.Image("Rule", keeper, UiKit.Line).Size(2);
            Stat(keeper, "Отношение", "нейтральное");
            UiKit.Label("Лавка покупает вещи за половину своей цены. Товар пополняется каждое утро.", keeper, 15, UiKit.Muted).Size(44);
            UiKit.Image("Rule", keeper, UiKit.Line).Size(2);
            Goods(keeper, ItemBook.Find(shopChosen ?? (shop.Goods.Length > 0 ? shop.Goods[0].Item : null)));

            // The goods (or the bag, selling).
            var centre = UiKit.Panel("Goods", body);
            centre.rectTransform.Place(Vector2.zero, Vector2.one, 400, 0, 520, 0);
            var column = UiKit.Rect("Column", centre.transform).Fill();
            column.Stack<VerticalLayoutGroup>(8, new RectOffset(24, 24, 20, 20));
            UiKit.Choices(column, new[] { "Все", "Гу", "Корм и еда", "Материалы" }, shopFilter + 1, i =>
            {
                shopFilter = i - 1;
                shopScroll = 0;
                Show(Page.Shop);
            }, 15);
            var header = UiKit.Rect("Header", column);
            header.Stack<HorizontalLayoutGroup>(10, new RectOffset(70, 10, 0, 0), TextAnchor.MiddleLeft).childForceExpandWidth = false;
            header.Size(26);
            UiKit.Caps("Товар", header, 12, UiKit.Muted).Size(-1, -1, 1);
            UiKit.Caps(shopSelling ? "В сумке" : "В наличии", header, 12, UiKit.Muted).Size(-1, 120, 0);
            UiKit.Caps("Цена", header, 12, UiKit.Muted).Size(-1, 150, 0);
            UiKit.Label("", header, 12).Size(-1, 150, 0);
            var rows = Scroll(column, shopScroll, v => shopScroll = v);

            if (!shopSelling)
            {
                foreach (var goods in shop.Goods)
                {
                    var kind = ItemBook.Find(goods.Item);

                    if (kind != null && Shows(kind))
                    {
                        BuyRow(rows, shop, goods, kind, now);
                    }
                }
            }
            else
            {
                var any = false;

                for (var cell = 0; cell < Bag.Cells; cell++)
                {
                    var stack = gu.Bag[cell];
                    var kind = stack?.Kind;

                    if (kind != null && kind.Id != ItemBook.Stone && Trade.SellPrice(kind, kind.Bundle) > 0 && Shows(kind))
                    {
                        SellRow(rows, cell, stack, kind);
                        any = true;
                    }
                }

                if (!any)
                {
                    UiKit.Label("Продать нечего: лавка берёт только то, что сама продаёт по цене не меньше камня за лот.", rows, 16, UiKit.Muted).Size(50);
                }
            }

            // The deal.
            var right = UiKit.Panel("Deal", body);
            right.rectTransform.Place(new Vector2(1, 0), Vector2.one, -480, 0, 0, 0);
            var deal = UiKit.Rect("Column", right.transform).Fill();
            deal.Stack<VerticalLayoutGroup>(8, new RectOffset(26, 26, 22, 22));
            UiKit.Caps("Сделка", deal, 14).Size(24);
            var total = 0;
            var empty = true;

            if (!shopSelling)
            {
                foreach (var pair in shopLots)
                {
                    if (pair.Value <= 0)
                    {
                        continue;
                    }

                    var kind = ItemBook.Find(pair.Key);
                    empty = false;
                    DealLine(deal, $"{kind.Name} × {pair.Value * kind.Bundle}", kind.Price * pair.Value);
                }

                total = Trade.Price(shopLots);
            }
            else
            {
                foreach (var pair in shopSales)
                {
                    var kind = ItemBook.Find(pair.Value.id);
                    var got = Trade.SellPrice(kind, pair.Value.count);
                    empty = false;
                    DealLine(deal, $"{kind.Name} × {pair.Value.count}", got);
                    total += got;
                }
            }

            if (empty)
            {
                UiKit.Label("Нажмите «+» у товара, чтобы добавить его в сделку.", deal, 16, UiKit.Muted).Size(50);
            }

            var space = UiKit.Rect("Space", deal);
            space.Size(-1).flexibleHeight = 1;
            UiKit.Image("Rule", deal, UiKit.Line).Size(2);
            var left2 = shopSelling ? gu.Stones + total : gu.Stones - total;
            Stat(deal, shopSelling ? "Получите" : "Итого", ItemBook.Price(total));
            Stat(deal, "Останется", left2 >= 0 ? ItemBook.Price(left2) : $"<color=#C2413A>не хватает {ItemBook.Price(-left2)}</color>");
            UiKit.Button(UiKit.Spaced(shopSelling ? "Продать" : "Купить"), deal, () =>
            {
                if (shopSelling)
                {
                    SellAll();
                }
                else if (host.Buy(shopLots) == Deal.Done)
                {
                    shopLots.Clear();
                }

                Show(Page.Shop);
            }, ButtonKind.Gold, 18).Size(56);
        }

        bool Shows(ItemKind kind) => shopFilter switch
        {
            0 => kind.Category == ItemCategory.Gu,
            1 => kind.Category == ItemCategory.Food || kind.Category == ItemCategory.Herb || kind.Use == ItemUse.Feed,
            2 => kind.Category == ItemCategory.Material,
            _ => true,
        };

        /// <summary>The chosen thing: its cell, name and kind, a few lines about it, and what it gives.</summary>
        static void Goods(Transform parent, ItemKind kind)
        {
            if (kind == null)
            {
                return;
            }

            var head = UiKit.Rect("Head", parent);
            head.Size(76);
            var big = UiKit.Cell(head, 68);
            big.Back.rectTransform.anchorMin = big.Back.rectTransform.anchorMax = new Vector2(0, 0.5f);
            big.Back.rectTransform.anchoredPosition = new Vector2(34, 0);
            big.SetIcon(kind.Icon);
            big.Mark.color = Icons.Tint(kind.Icon);
            big.Corner.text = kind.Rank > 0 ? Aperture.RankNumeral(kind.Rank) : "";
            big.Corner.color = Aperture.RankColour(kind.Rank);
            big.SetRim(kind.Rank > 0 ? Aperture.RankColour(kind.Rank) : UiKit.GoldDeep);
            var title = UiKit.Rect("Title", head).Place(Vector2.zero, Vector2.one, 82, 0, 0, 0);
            title.Stack<VerticalLayoutGroup>(2, null, TextAnchor.MiddleLeft);
            UiKit.Label(kind.Name, title, 18, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Size(44);
            UiKit.Caps((kind.Rank > 0 ? $"Ранг {kind.Rank} · " : "") + kind.Kind, title, 11, kind.Rank > 0 ? Aperture.RankColour(kind.Rank) : UiKit.Muted).Size(18);
            UiKit.Label(kind.About, parent, 15).Size(104);
            var gives = kind.Use switch
            {
                ItemUse.Eat when kind.Heal > 0 => $"+{kind.Heal:0} здоровья",
                ItemUse.Feed when kind.AnyGu => "порция корма любому Гу",
                ItemUse.Feed => "корм для Гу",
                ItemUse.Refine => $"очистка за {GuBook.Find(kind.Gu)?.RefineCost:0}% эссенции (мастеру его ранга)",
                ItemUse.Activate => GuBook.Find(kind.Gu)?.SkillName,
                _ => null,
            };

            void Line(string name, string value)
            {
                var row = UiKit.Rect(name, parent);
                row.Stack<HorizontalLayoutGroup>(8, null, TextAnchor.MiddleLeft).childForceExpandWidth = false;
                row.Size(26);
                UiKit.Label(name, row, 15, UiKit.Muted).Size(-1, 92, 0);
                UiKit.Label(value, row, 15, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Size(-1, -1, 1);
            }

            if (gives != null)
            {
                Line("Даёт", gives);
            }

            Line("Хранится", kind.Keeps > 0 ? Span(kind.Keeps) : "не портится");
            Line("Вес", $"{kind.Weight:0.##}");
        }

        void BuyRow(Transform parent, Shop shop, Goods goods, ItemKind kind, double now)
        {
            var left = host.Trade.Left(shop, goods, now);
            shopLots.TryGetValue(kind.Id, out var lots);
            var row = Row(parent, kind, lots > 0, () =>
            {
                shopChosen = kind.Id;
                Show(Page.Shop);
            });
            UiKit.Label(left > 0 ? (kind.Bundle > 1 ? $"{left * kind.Bundle} шт." : $"{left}") : "нет", row, 16, left > 0 ? UiKit.Text : UiKit.Danger).Size(-1, 120, 0);
            UiKit.Label(kind.Bundle > 1 ? $"{ItemBook.Price(kind.Price)}\n<size=13><color=#A69A86>за {kind.Bundle} шт.</color></size>" : ItemBook.Price(kind.Price), row, 16, UiKit.Text).Size(-1, 150, 0);
            Counter(row, kind.Bundle > 1 ? $"{lots * kind.Bundle}" : $"{lots}", lots > 0, () =>
            {
                shopLots[kind.Id] = Mathf.Max(0, lots - 1);
                Show(Page.Shop);
            }, () =>
            {
                shopLots[kind.Id] = Mathf.Min(left, lots + 1);
                Show(Page.Shop);
            });
        }

        void SellRow(Transform parent, int cell, ItemStack stack, ItemKind kind)
        {
            shopSales.TryGetValue(cell, out var sale);
            var count = sale.id == stack.id ? sale.count : 0;
            var row = Row(parent, kind, count > 0, () =>
            {
                shopChosen = kind.Id;
                Show(Page.Shop);
            });
            UiKit.Label($"{stack.count}", row, 16, UiKit.Text).Size(-1, 120, 0);
            UiKit.Label($"{ItemBook.Price(Trade.SellPrice(kind, kind.Bundle))}" + (kind.Bundle > 1 ? $"\n<size=13><color=#A69A86>за {kind.Bundle} шт.</color></size>" : ""), row, 15, UiKit.Text).Size(-1, 150, 0);
            var step = kind.Bundle;
            Counter(row, $"{count}", count > 0, () =>
            {
                var next = Mathf.Max(0, count - step);

                if (next > 0) shopSales[cell] = (stack.id, next);
                else shopSales.Remove(cell);

                Show(Page.Shop);
            }, () =>
            {
                var next = Mathf.Min(stack.count / step * step, count + step);

                if (next > 0) shopSales[cell] = (stack.id, next);

                Show(Page.Shop);
            });
        }

        /// <summary>A row of the goods: the thing's cell, its name and what it is; a click on it shows it under the keeper.</summary>
        RectTransform Row(Transform parent, ItemKind kind, bool inDeal, System.Action choose)
        {
            var chosen = kind.Id == shopChosen;
            var resting = inDeal ? new Color(0.79f, 0.65f, 0.39f, 0.07f) : chosen ? new Color(1, 1, 1, 0.04f) : new Color(0, 0, 0, 0);
            var image = UiKit.Image(kind.Name, parent, resting, UiKit.Chamfer);
            image.raycastTarget = true;
            image.gameObject.AddComponent<Hover>().Set(image, resting, new Color(0.79f, 0.65f, 0.39f, 0.12f), choose);
            var row = image.rectTransform;
            row.Stack<HorizontalLayoutGroup>(10, new RectOffset(4, 6, 4, 4), TextAnchor.MiddleLeft).childForceExpandWidth = false;
            row.Size(64);
            var cell = UiKit.Cell(row, 56);
            cell.Size(56, 56, 0);
            cell.SetIcon(kind.Icon);
            cell.Mark.color = Icons.Tint(kind.Icon);
            cell.Corner.text = kind.Rank > 0 ? Aperture.RankNumeral(kind.Rank) : "";
            cell.Corner.color = Aperture.RankColour(kind.Rank);
            cell.SetRim(kind.Rank > 0 ? Aperture.RankColour(kind.Rank) : UiKit.GoldDeep);
            UiKit.Label($"<b>{kind.Name}</b>\n<size=13><color=#A69A86>{kind.Kind}</color></size>", row, 17).Size(-1, -1, 1);

            return row;
        }

        static void Counter(Transform row, string value, bool lit, System.Action minus, System.Action plus)
        {
            var box = UiKit.Rect("Counter", row);
            box.Stack<HorizontalLayoutGroup>(4, null, TextAnchor.MiddleCenter).childForceExpandWidth = false;
            box.Size(44, 150, 0);
            UiKit.Button("−", box, minus, ButtonKind.Plain, 20).Size(40, 40, 0);
            UiKit.Label(value, box, 17, lit ? UiKit.GoldLit : UiKit.Hex(0x6E655A), TextAnchor.MiddleCenter, FontStyle.Bold).Size(40, 54, 0);
            UiKit.Button("+", box, plus, ButtonKind.Plain, 20).Size(40, 40, 0);
        }

        static void DealLine(Transform parent, string what, int stones)
        {
            var row = UiKit.Rect(what, parent);
            row.Stack<HorizontalLayoutGroup>(8, null, TextAnchor.MiddleLeft).childForceExpandWidth = false;
            row.Size(28);
            UiKit.Label(what, row, 16).Size(-1, -1, 1);
            UiKit.Label(ItemBook.Price(stones), row, 16, UiKit.GoldLit, TextAnchor.MiddleRight).Size(-1, 120, 0);
        }

        /// <summary>Sells what is in the deal, pile by pile (each checked to be what it was).</summary>
        void SellAll()
        {
            var got = 0;

            foreach (var pair in new List<KeyValuePair<int, (string id, int count)>>(shopSales))
            {
                if (host.Cultivator.Bag[pair.Key]?.id == pair.Value.id)
                {
                    got += host.Sell(pair.Key, pair.Value.count);
                }
            }

            shopSales.Clear();

            if (got > 0)
            {
                Notify($"Продано за {ItemBook.Stones(got)}");
            }
        }

        /// <summary>
        /// A scrolling column (the goods can be more than fit): scrolled `down` points from its top at first, and telling
        /// `scrolled` where it is as it moves.
        /// </summary>
        static RectTransform Scroll(Transform parent, float down = 0, System.Action<float> scrolled = null)
        {
            var view = UiKit.Rect("View", parent);
            view.Size(-1).flexibleHeight = 1;
            view.gameObject.AddComponent<RectMask2D>();
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var content = UiKit.Rect("Content", view);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            content.Stack<VerticalLayoutGroup>(6);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            // Points from the top, not a share of the length: the list's length is only known once it is laid out.
            content.anchoredPosition = new Vector2(0, down);

            if (scrolled != null)
            {
                scroll.onValueChanged.AddListener(_ => scrolled(content.anchoredPosition.y));
            }

            return content;
        }
    }
}
