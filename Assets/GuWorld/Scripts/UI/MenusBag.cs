using System.Collections.Generic;
using GuWorld.Cultivation;
using GuWorld.Items;
using GuWorld.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// The hero's pages share a frame (the design's inventory and aperture
    /// boards): a seal and a title, tabs between the bag (I), the aperture (K)
    /// and the map (M), the stones carried and a way out. This part holds the
    /// frame and the bag: forty cells with filters, the weight, and what the
    /// chosen thing is and what can be done with it. A thing or a Gu chosen on
    /// these pages goes onto the action bar with a key, 1–9.
    /// </summary>
    public partial class Menus
    {
        const float BagCell = 86;
        const float BagGap = 8;

        readonly List<Cell> pageCells = new List<Cell>();
        int bagChosen = -1;
        /// <summary>The bag's filter: -1 all, else an ItemCategory.</summary>
        int bagFilter = -1;

        /// <summary>The frame of the hero's pages; returns the body to fill.</summary>
        RectTransform BookFrame(string icon, string title, Page page)
        {
            var dim = UiKit.Image("Dim", pageRoot, UiKit.Hex(0x0B0A0A, 0.95f));
            dim.rectTransform.Fill();
            dim.raycastTarget = true;

            var top = UiKit.Rect("Top", pageRoot).Place(new Vector2(0, 1), Vector2.one, 48, -112, 48, 28);
            var seal = UiKit.SealMark(icon, top, 60);
            seal.rectTransform.At(new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(60, 60));
            var name = UiKit.Label(title, top, 34, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            name.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 82, 0, -420, 0);

            var tabs = UiKit.Rect("Tabs", top).Place(Vector2.zero, new Vector2(0, 1), 430, 12, -1100, 12);
            tabs.Stack<HorizontalLayoutGroup>(8, null, TextAnchor.MiddleLeft).childForceExpandWidth = true;

            foreach (var (label, key, target) in new[] { ("Сумка", "I", Page.Bag), ("Апертура", "K", Page.Aperture), ("Карта", "M", Page.Map), ("Тело", "B", Page.Body) })
            {
                var to = target;
                UiKit.Button($"{UiKit.Spaced(label)}   <color=#A69A86>{key}</color>", tabs, () => host.OpenPage(to), target == page ? ButtonKind.Tab : ButtonKind.Plain, 16).Size(52, -1, 1);
            }

            var close = UiKit.Button("Закрыть · Esc", top, host.Resume, ButtonKind.Plain, 16);
            ((RectTransform)close.transform).Place(new Vector2(1, 0), Vector2.one, -190, 12, 0, 12);
            var coin = UiKit.Icon("stone", top, 24, UiKit.Gold);
            coin.rectTransform.Place(new Vector2(1, 0), Vector2.one, -420, 0, 380, 0);
            var stones = UiKit.Label($"<b>{host.Cultivator.Stones}</b>  <color=#A69A86>камней</color>", top, 20, UiKit.Text);
            stones.rectTransform.Place(new Vector2(1, 0), Vector2.one, -376, 0, 206, 0);

            var rule = UiKit.Image("Rule", pageRoot, UiKit.Line);
            rule.rectTransform.Place(new Vector2(0, 1), Vector2.one, 48, -120, 48, 118);

            return UiKit.Rect("Body", pageRoot).Place(Vector2.zero, Vector2.one, 48, 36, 48, 140);
        }

        /// <summary>Keys on the aperture page: 1–4 put the chosen Gu on that key.</summary>
        void UpdatePages()
        {
            var keyboard = Keyboard.current;

            if (keyboard == null || (Current != Page.Bag && Current != Page.Aperture))
            {
                return;
            }

            var digits = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key };

            for (var i = 0; i < digits.Length; i++)
            {
                if (!digits[i].wasPressedThisFrame)
                {
                    continue;
                }

                var entry = ChosenEntry();

                if (entry != null)
                {
                    host.Cultivator.Bind(i, entry);
                    Notify($"На клавише {i + 1}: {EntryName(entry)}");
                    Show(Current);
                }
            }
        }

        /// <summary>What is chosen on the page, as an action bar entry (null: nothing that can go there).</summary>
        string ChosenEntry()
        {
            var gu = host.Cultivator;

            // Only Gu go on the keys (things are used from the bag; the stone has its own key).
            if (Current == Page.Bag)
            {
                return null;
            }

            var owned = gu.At(ringChosen);

            return owned != null && owned.Kind.Skill != SkillKind.None ? "gu:" + owned.kind : null;
        }

        static string EntryName(string entry) =>
            entry.StartsWith("gu:") ? GuBook.Find(entry.Substring(3))?.SkillName : ItemBook.Find(entry.Substring(5))?.Name;

        void BagPage()
        {
            var gu = host.Cultivator;
            var bag = gu.Bag;
            var body = BookFrame("bag", "Инвентарь", Page.Bag);

            if (bag[bagChosen] == null)
            {
                // Nothing chosen: the first thing in the bag.
                bagChosen = -1;

                for (var i = 0; i < Bag.Cells && bagChosen < 0; i++)
                {
                    if (bag[i] != null) bagChosen = i;
                }
            }

            // The hero, on the left.
            var left = UiKit.Panel("Hero", body);
            left.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -340, 0);
            var info = UiKit.Rect("Info", left.transform).Fill();
            info.Stack<VerticalLayoutGroup>(8, new RectOffset(26, 26, 24, 22));
            UiKit.Label(host.Look.FullName, info, 24, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Size(60);
            var a = gu.Aperture;
            UiKit.Caps($"Ранг {a.Rank} · {Aperture.Name(a.Stage)}", info, 14, Aperture.RankColour(a.Rank)).Size(22);
            UiKit.Image("Rule", info, UiKit.Line).Size(2);
            Stat(info, "Жизнь", $"{Mathf.CeilToInt(host.Health.Current)} / {Health.Max:0}");
            Stat(info, "Эссенция", $"{a.Essence:0} / {a.Cap:0}%");
            Stat(info, "Первобытные камни", $"{gu.Stones}");
            Stat(info, "Вес", $"{bag.Weight:0.0} / {Bag.Capacity:0}");
            UiKit.Image("Rule", info, UiKit.Line).Size(2);
            UiKit.Label("Камни поглощаются клавишей V (3 с). Гу ставятся на клавиши 1–4 в апертуре (K).", info, 16, UiKit.Muted).Size(64);
            UiKit.Label("Гу в сумке лежат в сосуде и не голодают — пока вы не очистите их в апертуру.", info, 16, UiKit.Muted).Size(64);

            // The cells, in the middle.
            var gridWidth = Bag.Across * BagCell + (Bag.Across - 1) * BagGap;
            var centre = UiKit.Rect("Centre", body).Place(Vector2.zero, new Vector2(0, 1), 380, 0, -(380 + gridWidth), 0);
            var filters = UiKit.Rect("Filters", centre).Place(new Vector2(0, 1), Vector2.one, 0, -48, 0, 0);
            filters.Stack<VerticalLayoutGroup>(0);
            UiKit.Choices(filters, new[] { "Все", "Гу", "Еда", "Травы", "Материалы" }, bagFilter + 1, i =>
            {
                bagFilter = i - 1;
                Show(Page.Bag);
            }, 16);

            var grid = UiKit.Rect("Grid", centre).Place(new Vector2(0, 1), Vector2.one, 0, -(64 + 5 * BagCell + 4 * BagGap), 0, 64);

            for (var i = 0; i < Bag.Cells; i++)
            {
                var index = i;
                var cell = UiKit.Cell(grid, BagCell, () =>
                {
                    bagChosen = host.Cultivator.Bag[index] != null ? index : -1;
                    Show(Page.Bag);
                });
                cell.Back.rectTransform.anchorMin = cell.Back.rectTransform.anchorMax = new Vector2(0, 1);
                cell.Back.rectTransform.anchoredPosition = new Vector2(BagCell / 2 + i % Bag.Across * (BagCell + BagGap), -(BagCell / 2 + i / Bag.Across * (BagCell + BagGap)));
                ShowStack(cell, bag[i], i == bagChosen, host.Now);
                pageCells.Add(cell);
            }

            var foot = UiKit.Rect("Foot", centre).Place(new Vector2(0, 1), Vector2.one, 0, -(64 + 5 * BagCell + 4 * BagGap + 70), 0, 64 + 5 * BagCell + 4 * BagGap + 16);
            foot.Stack<HorizontalLayoutGroup>(12, null, TextAnchor.MiddleLeft).childForceExpandWidth = false;
            UiKit.Label($"{UiKit.Spaced("Вес")}   <b>{bag.Weight:0.0} / {Bag.Capacity:0}</b>", foot, 17, UiKit.Muted).Size(54, 420, 1);
            UiKit.Button(UiKit.Spaced("Упорядочить"), foot, () =>
            {
                host.Cultivator.Bag.Sort();
                bagChosen = -1;
                Show(Page.Bag);
            }, ButtonKind.Plain, 16).Size(54, 260);

            // The chosen thing, on the right.
            var right = UiKit.Panel("Details", body);
            right.rectTransform.Place(new Vector2(1, 0), Vector2.one, -500, 0, 0, 0);
            var details = UiKit.Rect("Column", right.transform).Fill();
            details.Stack<VerticalLayoutGroup>(8, new RectOffset(28, 28, 26, 24));
            var chosen = bag[bagChosen];

            if (chosen == null)
            {
                UiKit.Label("Выберите вещь в сумке.", details, 20, UiKit.Muted).Size(40);

                return;
            }

            var kind = chosen.Kind;
            var head = UiKit.Rect("Head", details);
            head.Size(120);
            var big = UiKit.Cell(head, 112);
            big.Back.rectTransform.anchorMin = big.Back.rectTransform.anchorMax = new Vector2(0, 0.5f);
            big.Back.rectTransform.anchoredPosition = new Vector2(56, 0);
            ShowStack(big, chosen, false, host.Now);
            var title = UiKit.Rect("Title", head).Place(Vector2.zero, Vector2.one, 130, 0, 0, 0);
            title.Stack<VerticalLayoutGroup>(4, null, TextAnchor.MiddleLeft);
            UiKit.Label(kind.Name, title, 24, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Size(60);
            UiKit.Caps((kind.Rank > 0 ? $"Ранг {kind.Rank} · " : "") + kind.Kind, title, 13, kind.Rank > 0 ? Aperture.RankColour(kind.Rank) : UiKit.Muted).Size(22);

            UiKit.Label(kind.About, details, 17).Size(96);
            UiKit.Image("Rule", details, UiKit.Line).Size(2);
            Stat(details, "Количество", $"{chosen.count}");
            Stat(details, "Вес", $"{chosen.count * kind.Weight:0.##}");
            Stat(details, "Цена", kind.Price > 0 ? (kind.Bundle > 1 ? $"{kind.Bundle} шт. — {ItemBook.Stones(kind.Price)}" : ItemBook.Stones(kind.Price)) : "—");
            Stat(details, "Свежесть", chosen.spoils > 0 ? $"завянет через {Span(chosen.spoils - host.Now)}" : "не портится");

            if (kind.Use == ItemUse.Feed)
            {
                Stat(details, "Кто ест", kind.AnyGu ? "любой Гу — вместо одной порции" : FeedsWhom(kind.Id));
            }
            else if (kind.Use == ItemUse.Eat && kind.Heal > 0)
            {
                Stat(details, "Даёт", $"+{kind.Heal:0} здоровья");
            }

            var space = UiKit.Rect("Space", details);
            space.Size(-1).flexibleHeight = 1;
            var cellIndex = bagChosen;

            if (kind.Use != ItemUse.None)
            {
                UiKit.Button(UiKit.Spaced(ActionName(kind)), details, () =>
                {
                    // A stone is drawn in over three seconds in the world: back to it.
                    if (host.Cultivator.UseCell(cellIndex) && kind.Use == ItemUse.Absorb)
                    {
                        host.Resume();
                    }
                    else
                    {
                        Show(Page.Bag);
                    }
                }, ButtonKind.Gold, 17).Size(56);
            }

            var row = UiKit.Rect("Row", details);
            row.Stack<HorizontalLayoutGroup>(8).childForceExpandWidth = true;
            row.Size(50);
            UiKit.Button(UiKit.Spaced("Разделить"), row, () =>
            {
                host.Cultivator.Bag.Split(cellIndex);
                Show(Page.Bag);
            }, ButtonKind.Plain, 15).Size(50, -1, 1);
            UiKit.Button(UiKit.Spaced("Выбросить"), row, () =>
                Ask("Выбросить?", $"{kind.Name} ×{chosen.count} — выброшенное не вернуть.", "Выбросить", () =>
                {
                    host.Drop(cellIndex);
                    bagChosen = -1;
                    Show(Page.Bag);
                }, Page.Bag), ButtonKind.Danger, 15).Size(50, -1, 1);

        }

        static string ActionName(ItemKind kind) => kind.Use switch
        {
            ItemUse.Refine => $"Очистить в апертуру · {GuBook.Find(kind.Gu)?.RefineCost:0}%",
            ItemUse.Absorb => "Поглотить · V",
            _ when kind.Verb != null => kind.Verb,
            ItemUse.Eat => "Съесть",
            ItemUse.Feed => kind.AnyGu ? "Подкормить Гу" : "Накормить Гу",
            _ => "",
        };

        static string FeedsWhom(string food)
        {
            var who = new List<string>();

            foreach (var kind in GuBook.All)
            {
                if (kind.Food == food)
                {
                    who.Add(kind.Name);
                }
            }

            return who.Count > 0 ? string.Join(", ", who) : "—";
        }

        /// <summary>A pile in a cell: its icon, its count, its rank, whether it is about to spoil; chosen or filtered out.</summary>
        void ShowStack(Cell cell, ItemStack stack, bool chosen, double now)
        {
            var kind = stack?.Kind;
            cell.SetIcon(kind?.Icon);
            cell.Count.text = stack != null && stack.count > 1 ? $"{stack.count}" : "";
            cell.Corner.text = kind != null && kind.Rank > 0 ? Aperture.RankNumeral(kind.Rank) : "";
            cell.Corner.color = Aperture.RankColour(kind?.Rank ?? 1);
            cell.Dot.enabled = stack != null && stack.spoils > 0 && stack.spoils - now < 24 * 60;
            cell.Dot.color = UiKit.Hunger;
            cell.SetRim(chosen ? UiKit.GoldLit : kind == null ? UiKit.Hex(0x2A2622) : kind.Rank > 0 ? Aperture.RankColour(kind.Rank) : UiKit.GoldDeep);
            cell.Back.color = chosen ? UiKit.Hex(0x2A1E12) : UiKit.Hex(0x0E0D0C, 0.95f);
            var shown = kind == null || bagFilter < 0 || (int)kind.Category == bagFilter;
            var alpha = shown ? 1 : 0.25f;
            var tint = Icons.Tint(kind?.Icon);
            cell.Mark.color = new Color(tint.r, tint.g, tint.b, alpha);
            cell.Count.color = new Color(UiKit.Text.r, UiKit.Text.g, UiKit.Text.b, alpha);
        }

        static void Stat(Transform parent, string name, string value)
        {
            var row = UiKit.Rect(name, parent);
            row.Stack<HorizontalLayoutGroup>(8, null, TextAnchor.MiddleLeft).childForceExpandWidth = false;
            row.Size(28);
            UiKit.Label(name, row, 16, UiKit.Muted).Size(-1, 170, 0);
            UiKit.Label(value, row, 17, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Size(-1, -1, 1);
        }

        /// <summary>A stretch of game time in words: "3 дн 4 ч", "5 ч 20 мин", "12 мин".</summary>
        public static string Span(double minutes)
        {
            if (minutes <= 0)
            {
                return "сейчас";
            }

            var total = (long)System.Math.Ceiling(minutes);
            var days = total / (24 * 60);
            var hours = total / 60 % 24;
            var mins = total % 60;

            return days > 0 ? (hours > 0 ? $"{days} дн {hours} ч" : $"{days} дн") : hours > 0 ? (mins > 0 ? $"{hours} ч {mins} мин" : $"{hours} ч") : $"{mins} мин";
        }
    }
}
