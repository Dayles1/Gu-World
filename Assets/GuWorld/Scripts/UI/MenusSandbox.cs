using GuWorld.Cultivation;
using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// The immortal's aperture's page (Tab): every Gu there is, as a grid of marks — a click
    /// puts the one clicked in the chosen cell at hand (the eight cells across the top, 1–8),
    /// the pointer over one tells what it is and does — and on the left the modes: immortal or
    /// mortal, an endless or a true sea, tiring and hungry Gu or not, the rank, the small
    /// realm, the talent, the hour, wolves called up or sent away. No ring, no refining: a Gu
    /// put at hand is ready to use.
    /// </summary>
    public partial class Menus
    {
        /// <summary>The grid's rank filter (0: all) and where it was scrolled to.</summary>
        int arsenalRank;
        float arsenalScroll;
        Text arsenalInfo;

        void SandboxPage()
        {
            var gu = host.Cultivator;
            var a = gu.Aperture;
            var body = BookFrame("ring", "Апертура бессмертного", Page.Sandbox);

            // ── The modes, on the left ──
            var left = UiKit.Panel("Modes", body);
            left.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -520, 0);
            var modes = UiKit.Rect("Column", left.transform).Fill();
            modes.Stack<VerticalLayoutGroup>(4, new RectOffset(22, 22, 18, 16));
            UiKit.Label("РЕЖИМЫ", modes, 20, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).Size(28);

            void Toggle(string name, bool on, string yes, string no, System.Action<bool> set)
            {
                Heading(modes, name);
                UiKit.Choices(modes, new[] { yes, no }, on ? 0 : 1, i =>
                {
                    set(i == 0);
                    Show(Page.Sandbox);
                }, 14);
            }

            Toggle("ГЕРОЙ", host.Health.Immortal, "Бессмертный", "Смертный", on => host.Health.Immortal = on);
            Toggle("ЭССЕНЦИЯ", gu.EndlessEssence, "Бесконечная", "Как у таланта", on => gu.EndlessEssence = on);
            Toggle("УСТАЛОСТЬ ГУ", !gu.Untiring, "Есть (может убить)", "Нет", on => gu.Untiring = !on);
            Toggle("ГОЛОД ГУ", !gu.Sated, "Есть", "Нет", on => gu.Sated = !on);

            Heading(modes, $"РАНГ · {a.Rank}, {Aperture.Name(a.Stage)}");
            UiKit.Choices(modes, new[] { "I", "II", "III", "IV", "V" }, a.Rank - 1, i =>
            {
                host.Cultivator.SetRealm(i + 1, host.Cultivator.Aperture.Stage);
                Show(Page.Sandbox);
            }, 14);
            UiKit.Choices(modes, new[] { "Начальная", "Средняя", "Верхняя", "Пик" }, (int)a.Stage, i =>
            {
                host.Cultivator.SetRealm(host.Cultivator.Aperture.Rank, (Stage)i);
                Show(Page.Sandbox);
            }, 13);

            Heading(modes, $"ТАЛАНТ · {a.TalentName}");
            float[] talents = { 30, 50, 70, 88, 99, 100 };
            UiKit.Choices(modes, new[] { "D 30", "C 50", "B 70", "A 88", "A 99", "100" }, System.Array.FindIndex(talents, t => Mathf.Abs(t - a.Talent) < 0.5f), i =>
            {
                host.Cultivator.SetTalent(talents[i]);
                host.Cultivator.Aperture.Essence = host.Cultivator.Aperture.Cap;
                Show(Page.Sandbox);
            }, 13);

            Heading(modes, "ВРЕМЯ");
            float[] hours = { 7, 13, 19, 23 };
            UiKit.Choices(modes, new[] { "Утро", "День", "Вечер", "Ночь" }, -1, i =>
            {
                host.SetHour(hours[i]);
                Show(Page.Sandbox);
            }, 13);

            Heading(modes, $"ВРАГИ · живых: {LivingWolves()}");
            var g = GuWorld.Creatures.WolfKinds.Grey;
            var l = GuWorld.Creatures.WolfKinds.Lightning;
            UiKit.Choices(modes, new[] { "Серые ×3 (1–3)", "Молниевые ×3 (4–8)", "Убрать всех" }, -1, i =>
            {
                if (i == 2)
                {
                    host.ClearWolves();
                    Notify("Волки убраны");
                }
                else
                {
                    Notify($"Волков пришло: {host.SpawnPack(i == 0 ? g : l, 3)}");
                }

                host.Resume();
            }, 12);
            // The beast kings, each with its pack.
            var kings = new[] { GuWorld.Creatures.WolfKinds.Bold, GuWorld.Creatures.WolfKinds.Frenzy, GuWorld.Creatures.WolfKinds.ThunderCrown };
            UiKit.Choices(modes, new[] { "Отважный (12)", "Неистовый (20)", "Громовая корона (35)" }, -1, i =>
            {
                Notify($"{kings[i].Name} идёт со стаей ({host.SpawnPack(kings[i], 1)})");
                host.Resume();
            }, 12);
            // Gu Masters of the clans: a Bai with ice, a Xiong with the bear's strength, a Gu Yue with moonblades — a pair of
            // youths of rank one, a master of rank two, an elder of rank three.
            var masters = GuWorld.Creatures.MasterKinds.All;
            string[] clans = { "bai", "xiong", "gu_yue" };
            UiKit.Choices(modes, new[] { "Бай: лёд", "Сюн: медведь", "Гу Юэ: луна" }, -1, i =>
            {
                Notify($"Гу-мастера пришли: {host.SpawnMasters(clans[i], 1, 2)}");
                host.Resume();
            }, 12);
            UiKit.Choices(modes, new[] { "Ранг 2", "Старейшина 3", "Отряд Бай+Сюн" }, -1, i =>
            {
                var came = i == 2
                    ? host.SpawnMasters("bai", 1, 1) + host.SpawnMasters("xiong", 2, 1)
                    : host.SpawnMasters(Random.value < 0.5f ? "bai" : "xiong", i == 0 ? 2 : 3, 1);
                Notify($"Гу-мастера пришли: {came}");
                host.Resume();
            }, 12);

            var space = UiKit.Rect("Space", modes);
            space.Size(-1).flexibleHeight = 1;
            var row = UiKit.Rect("Row", modes);
            row.Stack<HorizontalLayoutGroup>(8).childForceExpandWidth = true;
            row.Size(42);
            UiKit.Button("Вылечить · отдых Гу", row, () =>
            {
                host.Health.Set(GuWorld.Systems.Health.Max);
                host.Cultivator.RestAll();
                host.Cultivator.Aperture.Essence = host.Cultivator.Aperture.Cap;
                Notify("Здоровье, эссенция и Гу — в порядке");
                Show(Page.Sandbox);
            }, ButtonKind.Plain, 13).Size(42, -1, 1);
            UiKit.Button("На старт", row, () =>
            {
                host.BackToStart();
                host.Resume();
            }, ButtonKind.Plain, 13).Size(42, -1, 1);
            UiKit.Button("Покинуть апертуру", modes, () => FadeThrough(host.LeaveSandbox), ButtonKind.Danger, 14).Size(42);

            // ── All the Gu, on the right ──
            var right = UiKit.Panel("All Gu", body);
            right.rectTransform.Place(Vector2.zero, Vector2.one, 540, 0, 0, 0);
            var column = UiKit.Rect("Column", right.transform).Fill();
            column.Stack<VerticalLayoutGroup>(8, new RectOffset(24, 24, 18, 16));
            UiKit.Label($"ВСЕ ГУ · {GuBook.Count}", column, 20, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).Size(28);
            UiKit.Label("<b>1.</b> Выберите ячейку ниже (1–8).   <b>2.</b> Щёлкните по Гу в сетке — он встанет в неё.   " +
                        "<b>3.</b> Закройте (Tab или Esc) и жмите <b>ЛКМ</b> — Гу сработает туда, куда смотрите.", column, 16, UiKit.Text).Size(46);

            // The eight cells at hand: a click chooses where the next Gu goes.
            var hand = UiKit.Rect("Hand", column);
            hand.Stack<HorizontalLayoutGroup>(8, null, TextAnchor.MiddleLeft).childForceExpandWidth = false;
            hand.Size(84);

            for (var i = 0; i < GuWorldGame.SandboxCells; i++)
            {
                var slot = i;
                var kind = GuBook.Find(host.SandboxBar[i]);
                var chosen = i == host.SandboxSlot;
                var cell = UiKit.Cell(hand, 76, () =>
                {
                    host.SandboxSlot = slot;
                    Show(Page.Sandbox);
                });
                cell.Size(76, 76, 0);
                cell.Key.text = $"{i + 1}";
                cell.Back.color = chosen ? UiKit.Hex(0x3A2A1C, 0.97f) : UiKit.Hex(0x0E0D0C, 0.95f);
                cell.SetIcon(kind?.Icon);
                cell.Mark.color = Icons.Tint(kind?.Icon);
                cell.Corner.text = kind != null ? Aperture.RankNumeral(kind.Rank) : "";
                cell.Corner.color = Aperture.RankColour(kind?.Rank ?? 1);
                cell.SetRim(chosen ? UiKit.GoldLit : kind != null ? Aperture.RankColour(kind.Rank) : UiKit.Line);
                cell.Hovered = () => ShowArsenalInfo(kind, slot);
            }

            UiKit.Button("Очистить ячейку", hand, () =>
            {
                host.PutInSlot(host.SandboxSlot, null);
                Show(Page.Sandbox);
            }, ButtonKind.Plain, 13).Size(42, 160);

            // What the pointer is over (or the chosen cell's Gu).
            arsenalInfo = UiKit.Label("", column, 15, UiKit.Text);
            arsenalInfo.Size(48);
            ShowArsenalInfo(GuBook.Find(host.SandboxBar[host.SandboxSlot]), host.SandboxSlot);

            var filters = UiKit.Rect("Ranks", column);
            filters.Stack<HorizontalLayoutGroup>(6).childForceExpandWidth = true;
            filters.Size(38);

            for (var rank = 0; rank <= 5; rank++)
            {
                var r = rank;
                UiKit.Button(rank == 0 ? "Все" : $"Ранг {Aperture.RankNumeral(rank)}", filters, () =>
                {
                    arsenalRank = r;
                    arsenalScroll = 0;
                    Show(Page.Sandbox);
                }, rank == arsenalRank ? ButtonKind.Tab : ButtonKind.Plain, 14).Size(38, -1, 1);
            }

            var grid = GridScroll(column, arsenalScroll, y => arsenalScroll = y);

            foreach (var kind in GuBook.All)
            {
                if (arsenalRank != 0 && kind.Rank != arsenalRank)
                {
                    continue;
                }

                var k = kind;
                var item = UiKit.Rect(kind.Id, grid);
                item.Stack<VerticalLayoutGroup>(2, null, TextAnchor.UpperCenter);
                var cell = UiKit.Cell(item, 64, () =>
                {
                    host.PutInSlot(host.SandboxSlot, k);
                    Notify($"{k.Name} — в ячейке {host.SandboxSlot + 1}");
                    Show(Page.Sandbox);
                });
                cell.Size(64, 64, 0);
                cell.SetIcon(kind.Icon);
                cell.Mark.color = Icons.Tint(kind.Icon);
                cell.Corner.text = Aperture.RankNumeral(kind.Rank);
                cell.Corner.color = Aperture.RankColour(kind.Rank);
                var inHand = System.Array.IndexOf(host.SandboxBar, kind.Id) >= 0;
                cell.SetRim(inHand ? UiKit.GoldLit : Aperture.RankColour(kind.Rank) * 0.8f);
                cell.Hovered = () => ShowArsenalInfo(k, -1);
                var label = UiKit.Label(ShortName(kind.Name), item, 11, inHand ? UiKit.GoldLit : UiKit.Muted, TextAnchor.UpperCenter);
                label.Size(30);
            }
        }

        /// <summary>A Gu's name without the word "Гу" (the grid's labels are small).</summary>
        static string ShortName(string name) => name.StartsWith("Гу ") ? name.Substring(3) : name;

        void ShowArsenalInfo(GuKind kind, int slot)
        {
            if (arsenalInfo == null)
            {
                return;
            }

            if (kind == null)
            {
                arsenalInfo.text = slot >= 0 ? $"Ячейка {slot + 1} пуста — щёлкните по Гу в сетке, чтобы положить его сюда" : "";

                return;
            }

            var use = kind.OneUse ? "разовый: срабатывает по ЛКМ, здесь не кончается" : kind.Passive ? "пассивный: работает сам, пока в ячейке" :
                kind.Held ? "держится: ЛКМ — поднять, ещё раз — снять" : "ЛКМ — применить";
            arsenalInfo.text = $"<b>{kind.Name}</b> <color=#{ColorUtility.ToHtmlStringRGB(Aperture.RankColour(kind.Rank))}>ранг {kind.Rank}</color> · {kind.Kind} · {use}\n" +
                               $"<color=#A69A86>{kind.SkillName} — {SkillLine(kind)}</color>";
        }

        /// <summary>A scrolling grid (the Gu are far more than fit), scrolled `down` points from its top at first.</summary>
        static RectTransform GridScroll(Transform parent, float down, System.Action<float> scrolled)
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
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(104, 100);
            grid.spacing = new Vector2(6, 6);
            grid.childAlignment = TextAnchor.UpperLeft;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            content.anchoredPosition = new Vector2(0, down);
            scroll.onValueChanged.AddListener(_ => scrolled(content.anchoredPosition.y));

            return content;
        }

        static int LivingWolves()
        {
            return GuWorld.Creatures.Foes.Living();
        }
    }
}
