using System.Collections.Generic;
using GuWorld.Cultivation;
using GuWorld.Items;
using GuWorld.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// The aperture page (the design's "Персонаж и апертура"): the hero on the
    /// left; in the middle the sea of essence in a ring of eight cells — the Gu
    /// refined into it, the empty cells, the ones still closed to this rank; on
    /// the right the chosen cell (a Gu's skill, its hunger, feeding it; an empty
    /// cell's Gu to refine from the bag) and the feeding of all of them; under
    /// the ring, the action bar.
    /// </summary>
    public partial class Menus
    {
        const float RingRadius = 268;
        const float RingCell = 86;

        int ringChosen;

        void AperturePage()
        {
            var gu = host.Cultivator;
            var a = gu.Aperture;
            var body = BookFrame("ring", "Апертура", Page.Aperture);
            var rank = Aperture.RankColour(a.Rank);

            // The hero, on the left.
            var left = UiKit.Panel("Hero", body);
            left.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -340, 0);
            var info = UiKit.Rect("Info", left.transform).Fill();
            info.Stack<VerticalLayoutGroup>(8, new RectOffset(26, 26, 24, 22));
            UiKit.Label(host.Look.FullName, info, 24, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Size(60);
            UiKit.Caps("Клан Гу Юэ · гора Цинмао", info, 13, UiKit.Muted).Size(22);
            var sealRow = UiKit.Rect("Seal", info);
            sealRow.Size(92);
            var seal = UiKit.SealText(Aperture.RankNumeral(a.Rank), sealRow, 80);
            seal.rectTransform.At(new Vector2(0, 0.5f), new Vector2(40, 0), new Vector2(80, 80));
            var rankText = UiKit.Label($"<b>Ранг {a.Rank}</b>\n{Aperture.Name(a.Stage)}", sealRow, 19);
            rankText.rectTransform.Place(Vector2.zero, Vector2.one, 98, 0, 0, 0);
            UiKit.Image("Rule", info, UiKit.Line).Size(2);
            Stat(info, "Талант", $"класс {a.Grade}");
            Stat(info, "Жизнь", $"{Mathf.CeilToInt(host.Health.Current)} / {Health.Max:0}");
            Stat(info, "Восстановление", $"{a.RecoveryPerHour:0}% в час");
            Stat(info, "Первобытные камни", $"{gu.Stones}");
            Stat(info, "Гу в ячейках", $"{gu.Gu.Count} / {gu.Open}");
            UiKit.Image("Rule", info, UiKit.Line).Size(2);
            UiKit.Label("Талант задаёт, как высоко стоит море и как быстро оно наполняется. Камень вливает эссенцию сразу.", info, 15, UiKit.Muted).Size(80);

            // The sea and the ring, in the middle.
            var centre = UiKit.Rect("Ring", body).Place(new Vector2(0, 0), new Vector2(0, 1), 380, 110, -(380 + 680), 0);
            var middle = new Vector2(0, 30);
            var sea = UiKit.Rect("Sea", centre).At(new Vector2(0.5f, 0.5f), middle, new Vector2(380, 380));
            UiKit.Image("Back", sea, UiKit.Hex(0x0E0D0C), UiKit.Disc).rectTransform.Fill();
            var cap = UiKit.Image("Cap", sea, new Color(rank.r, rank.g, rank.b, 0.2f), UiKit.Ring);
            Radial(cap);
            cap.fillAmount = Mathf.Clamp01(a.Cap / 100);
            var fill = UiKit.Image("Essence", sea, rank, UiKit.Ring);
            Radial(fill);
            fill.fillAmount = Mathf.Clamp01(a.Essence / 100);
            var inner = UiKit.Image("Inner", sea, UiKit.Hex(0x121010), UiKit.Disc);
            inner.rectTransform.Fill(44);
            var words = UiKit.Rect("Words", inner.transform).Fill(20);
            words.Stack<VerticalLayoutGroup>(2, null, TextAnchor.MiddleCenter);
            UiKit.Label(UiKit.Spaced("Море эссенции"), words, 13, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold).Size(22);
            UiKit.Label($"{a.Essence:0}%", words, 64, UiKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold).Size(76);
            UiKit.Label($"{a.EssenceName} · ранг {a.Rank}", words, 16, UiKit.EssenceLit, TextAnchor.MiddleCenter).Size(24);
            UiKit.Label($"предел {a.Cap:0}% · +{a.RecoveryPerHour:0}% в час", words, 14, UiKit.Muted, TextAnchor.MiddleCenter).Size(22);

            for (var i = 0; i < Cultivator.Ring; i++)
            {
                var slot = i;
                var angle = (90 - i * 360f / Cultivator.Ring) * Mathf.Deg2Rad;
                var cell = UiKit.Cell(centre, RingCell, () =>
                {
                    ringChosen = slot;
                    Show(Page.Aperture);
                });
                cell.Back.rectTransform.anchorMin = cell.Back.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                cell.Back.rectTransform.anchoredPosition = middle + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * RingRadius;
                ShowSlot(cell, gu, slot, slot == ringChosen);
            }

            // The action bar, under the ring.
            var barRow = UiKit.Rect("Bar", body).Place(Vector2.zero, Vector2.zero, 380, 0, -(380 + 680), -100);
            UiKit.Caps("Клавиши Гу 1–4 · щелчок убирает", barRow, 12, UiKit.Muted).rectTransform.Place(new Vector2(0, 1), Vector2.one, 0, -20, 0, 0);

            for (var i = 0; i < Cultivator.BarSize; i++)
            {
                var key = i;
                var cell = UiKit.Cell(barRow, 64, () =>
                {
                    host.Cultivator.Unbind(key);
                    Show(Page.Aperture);
                });
                cell.Back.rectTransform.anchorMin = cell.Back.rectTransform.anchorMax = new Vector2(0, 0);
                cell.Back.rectTransform.anchoredPosition = new Vector2(32 + i * 74, 38);
                cell.Key.text = $"{i + 1}";
                ShowEntry(cell, gu, gu.Bar[i], false);
            }

            // The chosen cell, and the feeding, on the right.
            var right = UiKit.Panel("Chosen", body);
            right.rectTransform.Place(new Vector2(1, 0.42f), Vector2.one, -560, 8, 0, 0);
            var details = UiKit.Rect("Column", right.transform).Fill();
            details.Stack<VerticalLayoutGroup>(8, new RectOffset(28, 28, 24, 20));
            ChosenSlot(details, gu);

            var feeding = UiKit.Panel("Feeding", body);
            feeding.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0.42f), -560, 0, 0, 8);
            var list = UiKit.Rect("Column", feeding.transform).Fill();
            list.Stack<VerticalLayoutGroup>(6, new RectOffset(28, 28, 20, 20));
            UiKit.Caps("Кормление", list, 14).Size(24);

            if (gu.Gu.Count == 0)
            {
                UiKit.Label("В апертуре нет Гу.", list, 17, UiKit.Muted).Size(28);
            }

            var sorted = new List<OwnedGu>(gu.Gu);
            sorted.Sort((x, y) => gu.MealIn(x).CompareTo(gu.MealIn(y)));

            foreach (var owned in sorted)
            {
                FeedRow(list, gu, owned);
            }

            var space = UiKit.Rect("Space", list);
            space.Size(-1).flexibleHeight = 1;
            var buttons = UiKit.Rect("Buttons", list);
            buttons.Stack<HorizontalLayoutGroup>(8).childForceExpandWidth = true;
            buttons.Size(50);
            UiKit.Button(UiKit.Spaced("Накормить всех"), buttons, () =>
            {
                host.Cultivator.FeedAll();
                Show(Page.Aperture);
            }, ButtonKind.Plain, 15).Size(50, -1, 1);
            UiKit.Button(UiKit.Spaced("Поглотить камень"), buttons, () =>
            {
                // Three seconds in the world: back to it to draw the stone in.
                if (host.Cultivator.StartAbsorbing())
                {
                    host.Resume();
                }
                else
                {
                    Show(Page.Aperture);
                }
            }, ButtonKind.Plain, 15).Size(50, -1, 1);
        }

        /// <summary>A cell of the ring: its Gu (rank rim, hunger dot), empty, or closed to this rank.</summary>
        static void ShowSlot(Cell cell, Cultivator gu, int slot, bool chosen)
        {
            var owned = gu.At(slot);
            cell.Count.text = "";
            cell.Back.color = chosen ? UiKit.Hex(0x2A1E12) : UiKit.Hex(0x0E0D0C, 0.95f);

            cell.Badge.enabled = false;

            if (slot >= gu.Open)
            {
                cell.SetIcon("lock");
                cell.Mark.color = UiKit.Hex(0x4A4238);
                cell.Corner.text = "";
                cell.SetRim(chosen ? UiKit.GoldLit : UiKit.Hex(0x2A2622));

                return;
            }

            if (owned == null)
            {
                cell.SetIcon(null);
                cell.Corner.text = "";
                cell.SetRim(chosen ? UiKit.GoldLit : UiKit.LineLit);

                return;
            }

            var kind = owned.Kind;
            var hunger = gu.HungerOf(owned);
            cell.SetIcon(kind.Icon);
            cell.Mark.color = hunger >= Hunger.Starving ? UiKit.Muted : Icons.Tint(kind.Icon);
            cell.Corner.text = Aperture.RankNumeral(kind.Rank);
            cell.Corner.color = Aperture.RankColour(kind.Rank);
            // The vital Gu (its life is its master's): a heart.
            cell.Badge.enabled = owned.vital;
            cell.Dot.enabled = hunger >= Hunger.Hungry;
            cell.Dot.color = hunger >= Hunger.Starving ? UiKit.Danger : UiKit.Hunger;
            cell.SetRim(chosen ? UiKit.GoldLit : Aperture.RankColour(kind.Rank));
        }

        void ChosenSlot(Transform details, Cultivator gu)
        {
            var owned = gu.At(ringChosen);

            if (ringChosen >= gu.Open)
            {
                UiKit.Label("Закрыто", details, 26, UiKit.Muted, TextAnchor.MiddleLeft, FontStyle.Bold).Size(36);
                var need = ringChosen >= Cultivator.OpenSlots(2) ? 3 : 2;
                UiKit.Caps($"Нужен ранг {need}", details, 14, UiKit.Muted).Size(22);
                UiKit.Label("Ячейка откроется, когда апертура вырастет до этого ранга.", details, 17, UiKit.Muted).Size(50);

                return;
            }

            if (owned == null)
            {
                UiKit.Label("Пустая ячейка", details, 26, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Size(36);
                UiKit.Caps("Свободно", details, 14, UiKit.Muted).Size(22);
                var any = false;

                for (var cell = 0; cell < Bag.Cells; cell++)
                {
                    var stack = gu.Bag[cell];

                    if (stack?.Kind?.Use != ItemUse.Refine)
                    {
                        continue;
                    }

                    if (!any)
                    {
                        UiKit.Label("Очистить Гу из сумки — он займёт эту ячейку и станет вашим:", details, 17, UiKit.Muted).Size(48);
                        any = true;
                    }

                    var index = cell;
                    var kind = GuBook.Find(stack.Kind.Gu);
                    UiKit.Button($"{kind.Name} · {kind.RefineCost:0}% эссенции", details, () =>
                    {
                        host.Cultivator.Refine(index);
                        Show(Page.Aperture);
                    }, ButtonKind.Plain, 16).Size(48);
                }

                if (!any)
                {
                    UiKit.Label("В сумке нет Гу. Их продают в лавке на площади.", details, 17, UiKit.Muted).Size(48);
                }

                return;
            }

            var gk = owned.Kind;
            var hunger = gu.HungerOf(owned);
            var food = ItemBook.Find(gk.Food);
            UiKit.Label(gk.Name, details, 26, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Size(36);
            UiKit.Caps($"Ранг {gk.Rank} · {gk.Kind}" + (owned.vital ? " · жизненный Гу" : ""), details, 13, Aperture.RankColour(gk.Rank)).Size(22);
            UiKit.Label(ItemBook.Find(gk.Item)?.About ?? "", details, 16).Size(92);
            UiKit.Image("Rule", details, UiKit.Line).Size(2);
            Stat(details, "Навык", gk.SkillName);
            UiKit.Label(SkillLine(gk), details, 15, UiKit.Muted).Size(40);
            Stat(details, "Сытость", GuBook.Name(hunger));
            var due = gu.MealIn(owned);
            Stat(details, "Еда", due > 0 ? $"через {Span(due)}" : $"просрочена на {Span(-due)}");
            Stat(details, "Пища", $"{food?.Name.ToLowerInvariant()} ×{gk.Meal} · в сумке {gu.Bag.Count(gk.Food)}");
            var space = UiKit.Rect("Space", details);
            space.Size(-1).flexibleHeight = 1;
            var row = UiKit.Rect("Row", details);
            row.Stack<HorizontalLayoutGroup>(8).childForceExpandWidth = true;
            row.Size(52);
            var chosenGu = owned;
            UiKit.Button(UiKit.Spaced("Накормить"), row, () =>
            {
                host.Cultivator.Feed(chosenGu);
                Show(Page.Aperture);
            }, ButtonKind.Gold, 16).Size(52, -1, 1);
            UiKit.Label("1–4 — на клавишу", row, 15, UiKit.Muted, TextAnchor.MiddleCenter).Size(52, -1, 1);
        }

        /// <summary>A line of the feeding list: a dot for its hunger, the Gu, when it eats next.</summary>
        static void FeedRow(Transform parent, Cultivator gu, OwnedGu owned)
        {
            var kind = owned.Kind;
            var hunger = gu.HungerOf(owned);
            var row = UiKit.Rect(kind.Name, parent);
            row.Stack<HorizontalLayoutGroup>(10, null, TextAnchor.MiddleLeft).childForceExpandWidth = false;
            row.Size(30);
            var dot = UiKit.Image("Dot", row, hunger == Hunger.Fed ? UiKit.Essence : hunger == Hunger.Hungry ? UiKit.Hunger : UiKit.Danger, UiKit.Disc);
            dot.Size(12, 12, 0);
            UiKit.Label(kind.Name, row, 17, hunger == Hunger.Fed ? UiKit.Text : UiKit.Hex(0xEBC98A)).Size(-1, 260, 0);
            var due = gu.MealIn(owned);
            UiKit.Label(hunger == Hunger.Fed ? $"через {Span(due)}" : GuBook.Name(hunger), row, 16, UiKit.Muted, TextAnchor.MiddleRight).Size(-1, -1, 1);
        }
    }
}
