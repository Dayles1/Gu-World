using System.Collections.Generic;
using GuWorld.Cultivation;
using GuWorld.Items;
using GuWorld.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// The HUD (the design's "HUD — ПК"): at the top left the hero — a seal with
    /// their rank inside a ring of essence, their name, health, the place and the
    /// hour, their stones; at the top right a diamond-shaped minimap with the
    /// north and where they stand, and the task under it; at the foot the action
    /// bar, keys 1–9; the log of what happened at the bottom left; the keys of
    /// the hero's pages at the bottom right; and over the bar what one can do here.
    /// </summary>
    public partial class Menus
    {
        /// <summary>Metres across the minimap.</summary>
        const float MinimapSpan = 260;
        const float MinimapSize = 236;
        const int LogLines = 7;
        const float LogFor = 14;

        RectTransform hud;
        Image essenceRing;
        Image capRing;
        Text rankMark;
        Text heroName;
        Text essenceLine;
        RectTransform lifeFill;
        Text lifeLine;
        Text placeLine;
        Text timeLine;
        Text stonesLine;
        RawImage minimap;
        RectTransform minimapArrow;
        Text coordsLine;
        readonly List<(RectTransform dot, Text label)> minimapMarks = new List<(RectTransform, Text)>();
        RectTransform questCard;
        Text questTitle;
        Text questGoal;
        Text questProgress;
        readonly Cell[] barCells = new Cell[Cultivator.BarSize];
        Text skinLine;
        Text promptLine;
        RectTransform promptBar;
        RectTransform promptFill;
        readonly List<(string text, float at)> log = new List<(string, float)>();
        Text[] logTexts;

        void BuildHud()
        {
            hud = UiKit.Rect("HUD", canvas.transform).Fill();

            // The hero: a ring of essence round a rank seal.
            var portrait = UiKit.Rect("Portrait", hud).At(new Vector2(0, 1), new Vector2(104, -112), new Vector2(150, 150));
            var back = UiKit.Image("Back", portrait, UiKit.Hex(0x0E0D0C, 0.85f), UiKit.Disc);
            back.rectTransform.Fill();
            capRing = UiKit.Image("Cap", portrait, new Color(UiKit.Essence.r, UiKit.Essence.g, UiKit.Essence.b, 0.22f), UiKit.Ring);
            Radial(capRing);
            essenceRing = UiKit.Image("Essence", portrait, UiKit.Essence, UiKit.Ring);
            Radial(essenceRing);
            var inner = UiKit.Image("Inner", portrait, UiKit.Hex(0x17130F), UiKit.Disc);
            inner.rectTransform.Fill(19);
            rankMark = UiKit.Glyph("一", inner.transform, 58, UiKit.Gold);
            rankMark.rectTransform.Fill();

            heroName = UiKit.Label("", hud, 24, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed();
            heroName.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 192, -74, -760, 44);
            essenceLine = UiKit.Label("", hud, 15, UiKit.EssenceLit, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed();
            essenceLine.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 192, -100, -760, 76);
            var life = UiKit.Image("Life", hud, UiKit.Hex(0x1A1110, 0.9f), UiKit.Chamfer);
            life.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 192, -124, -500, 108);
            lifeFill = UiKit.Image("Fill", life.transform, UiKit.Life).rectTransform;
            lifeFill.Place(Vector2.zero, Vector2.one, 2, 2, 2, 2);
            lifeLine = UiKit.Label("", hud, 15, UiKit.Text, TextAnchor.MiddleLeft).Shadowed();
            lifeLine.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 512, -126, -720, 106);
            placeLine = UiKit.Label("", hud, 16, UiKit.Text).Shadowed();
            placeLine.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 192, -156, -900, 134);
            timeLine = UiKit.Label("", hud, 15, UiKit.Muted).Shadowed();
            timeLine.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 192, -180, -900, 158);
            var coin = UiKit.Glyph("元", hud, 20, UiKit.Gold).Shadowed();
            coin.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 186, -210, -220, 184);
            stonesLine = UiKit.Label("", hud, 17, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed();
            stonesLine.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 218, -210, -600, 186);

            BuildMinimap();
            BuildQuest();
            BuildBar();
            BuildLog();

            promptLine = UiKit.Label("", hud, 22, UiKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            promptLine.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -500, 150, -500, -190);
            promptBar = UiKit.Image("Hold", hud, UiKit.Hex(0x0E0D0C, 0.8f), UiKit.Chamfer).rectTransform;
            promptBar.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -130, 136, -130, -144);
            promptFill = UiKit.Image("Fill", promptBar, UiKit.GoldLit).rectTransform;
            promptFill.Place(Vector2.zero, new Vector2(0, 1), 1, 1, 1, 1);
            promptBar.gameObject.SetActive(false);

            // The hero's pages, at the bottom right.
            var keys = UiKit.Rect("Keys", hud).Place(new Vector2(1, 0), new Vector2(1, 0), -560, 26, 28, -64);
            keys.Stack<HorizontalLayoutGroup>(8, null, TextAnchor.MiddleRight).childForceExpandWidth = false;

            foreach (var (key, name) in new[] { ("K", "Апертура"), ("I", "Сумка"), ("M", "Карта"), ("Esc", "Меню") })
            {
                var plate = UiKit.Panel(name, keys, UiKit.Hex(0x0E0D0C, 0.78f));
                plate.Size(38, key.Length > 1 ? 118 : 112, 0);
                var text = UiKit.Label($"<color=#F0D08A><b>{key}</b></color>  {name}", plate.transform, 15, UiKit.Text, TextAnchor.MiddleCenter);
                text.rectTransform.Fill();
            }
        }

        static void Radial(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.rectTransform.Fill();
        }

        void BuildMinimap()
        {
            var centre = new Vector2(-40 - MinimapSize * 0.5f, -40 - MinimapSize * 0.5f);
            var frame = UiKit.Image("Minimap Frame", hud, UiKit.Gold, UiKit.Diamond);
            frame.rectTransform.At(new Vector2(1, 1), centre, Vector2.one * (MinimapSize + 8));
            var under = UiKit.Image("Minimap Under", hud, UiKit.Hex(0x0E0D0C), UiKit.Diamond);
            under.rectTransform.At(new Vector2(1, 1), centre, Vector2.one * (MinimapSize + 3));
            var mask = UiKit.Image("Minimap", hud, Color.white, UiKit.Diamond);
            mask.rectTransform.At(new Vector2(1, 1), centre, Vector2.one * MinimapSize);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            minimap = UiKit.Rect("Map", mask.transform).gameObject.AddComponent<RawImage>();
            minimap.rectTransform.Fill();
            minimap.raycastTarget = false;

            for (var i = 0; i < 6; i++)
            {
                var dot = UiKit.Image("Mark", mask.transform, UiKit.GoldLit, UiKit.Disc).rectTransform;
                dot.sizeDelta = new Vector2(8, 8);
                var label = UiKit.Label("", mask.transform, 13, UiKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
                label.rectTransform.sizeDelta = new Vector2(160, 20);
                minimapMarks.Add((dot, label));
            }

            minimapArrow = UiKit.Image("Hero", mask.transform, UiKit.GoldLit, UiKit.Arrow).rectTransform;
            minimapArrow.sizeDelta = new Vector2(20, 20);
            var north = UiKit.Glyph("С", hud, 18, UiKit.GoldLit).Shadowed();
            north.font = UiKit.Font;
            north.fontStyle = FontStyle.Bold;
            north.rectTransform.At(new Vector2(1, 1), centre + new Vector2(0, MinimapSize * 0.5f + 14), new Vector2(30, 24));
            coordsLine = UiKit.Label("", hud, 14, UiKit.Muted, TextAnchor.MiddleCenter).Shadowed();
            coordsLine.rectTransform.At(new Vector2(1, 1), centre - new Vector2(0, MinimapSize * 0.5f + 16), new Vector2(200, 22));
        }

        void BuildQuest()
        {
            questCard = UiKit.Rect("Quest", hud).Place(new Vector2(1, 1), new Vector2(1, 1), -420, -480, 36, 330);
            var back = UiKit.Image("Back", questCard, UiKit.Hex(0x0E0D0C, 0.62f), UiKit.Chamfer);
            back.rectTransform.Fill();
            var bar = UiKit.Image("Bar", questCard, UiKit.Gold);
            bar.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 10, -3, 10);
            var column = UiKit.Rect("Column", questCard).Fill();
            column.Stack<VerticalLayoutGroup>(3, new RectOffset(18, 16, 12, 10));
            UiKit.Caps("Задание", column, 13).Size(20);
            questTitle = UiKit.Label("", column, 19, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed();
            questTitle.Size(28);
            questGoal = UiKit.Label("", column, 15, UiKit.Muted).Shadowed();
            questGoal.Size(40);
            questProgress = UiKit.Label("", column, 16, UiKit.GoldLit, TextAnchor.MiddleRight, FontStyle.Bold).Shadowed();
            questProgress.Size(22);
        }

        void BuildBar()
        {
            const float size = 66;
            const float gap = 8;
            var width = Cultivator.BarSize * size + (Cultivator.BarSize - 1) * gap;
            var bar = UiKit.Rect("Bar", hud).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -width / 2, 26, -width / 2, -(26 + size));

            for (var i = 0; i < Cultivator.BarSize; i++)
            {
                var cell = UiKit.Cell(bar, size);
                cell.Back.rectTransform.anchorMin = cell.Back.rectTransform.anchorMax = new Vector2(0, 0.5f);
                cell.Back.rectTransform.anchoredPosition = new Vector2(size / 2 + i * (size + gap), 0);
                cell.Key.text = $"{i + 1}";
                barCells[i] = cell;
            }

            skinLine = UiKit.Label("", hud, 15, UiKit.EssenceLit, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            skinLine.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -300, 100, -300, -124);
        }

        void BuildLog()
        {
            var box = UiKit.Rect("Log", hud).Place(Vector2.zero, Vector2.zero, 28, 110, -640, -330);
            box.Stack<VerticalLayoutGroup>(2, null, TextAnchor.LowerLeft);
            logTexts = new Text[LogLines];

            for (var i = 0; i < LogLines; i++)
            {
                logTexts[i] = UiKit.Label("", box, 16, UiKit.Text).Shadowed();
                logTexts[i].Size(26);
            }
        }

        /// <summary>Something the game tells the player: a line in the log ("Система: …").</summary>
        public void Notify(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            log.Add((text, Time.unscaledTime));

            if (log.Count > 30)
            {
                log.RemoveAt(0);
            }
        }

        /// <summary>The log's last lines (for checking).</summary>
        public IReadOnlyList<(string text, float at)> Log => log;

        /// <summary>What one can do here, at the foot of the screen (null: nothing); with a share held so far, a bar.</summary>
        public void SetPrompt(string text, float held = -1)
        {
            promptLine.text = text ?? "";
            var holding = text != null && held >= 0;
            promptBar.gameObject.SetActive(holding);

            if (holding)
            {
                promptFill.anchorMax = new Vector2(Mathf.Clamp01(held), 1);
            }
        }

        void UpdateHud()
        {
            if (!hud.gameObject.activeSelf || host == null)
            {
                return;
            }

            var gu = host.Cultivator;
            var a = gu.Aperture;
            var rank = Aperture.RankColour(a.Rank);
            essenceRing.color = rank;
            essenceRing.fillAmount = Mathf.Clamp01(a.Essence / 100);
            capRing.color = new Color(rank.r, rank.g, rank.b, 0.2f);
            capRing.fillAmount = Mathf.Clamp01(a.Cap / 100);
            rankMark.text = Aperture.RankGlyph(a.Rank);
            heroName.text = host.Look.FullName;
            essenceLine.text = UiKit.Spaced("Эссенция") + $"  {a.Essence:0}%";

            var health = host.Health;
            lifeFill.anchorMax = new Vector2(Mathf.Clamp01(health.Current / Health.Max), 1);
            lifeLine.text = $"{Mathf.CeilToInt(health.Current)} / {Health.Max:0}";
            placeLine.text = host.PlaceName;
            timeLine.text = host.TimeLine;
            stonesLine.text = $"{gu.Stones}";

            UpdateMinimap();
            UpdateQuest();
            UpdateBar(gu);
            UpdateLog();
        }

        void UpdateMinimap()
        {
            var map = host.Map;

            if (map == null)
            {
                minimap.enabled = false;

                return;
            }

            minimap.enabled = true;
            minimap.texture = map.Texture;
            var at = host.HeroAt;
            var centre = map.ToMap(at);
            var span = new Vector2(MinimapSpan / map.Area.width, MinimapSpan / map.Area.height);
            minimap.uvRect = new Rect(centre - span * 0.5f, span);
            minimapArrow.localEulerAngles = new Vector3(0, 0, -host.HeroFacing * Mathf.Rad2Deg);
            coordsLine.text = $"{at.x:0}, {at.z:0}";

            var shown = 0;

            foreach (var mark in map.Marks)
            {
                if (!mark.Major || shown >= minimapMarks.Count)
                {
                    continue;
                }

                var offset = (map.ToMap(mark.At) - centre) / span * MinimapSize;

                // Only inside the diamond.
                if (Mathf.Abs(offset.x) + Mathf.Abs(offset.y) > MinimapSize * 0.5f - 6)
                {
                    continue;
                }

                var (dot, label) = minimapMarks[shown++];
                dot.gameObject.SetActive(true);
                label.gameObject.SetActive(true);
                dot.anchoredPosition = offset;
                label.rectTransform.anchoredPosition = offset + new Vector2(0, 13);
                label.text = mark.Label;
            }

            for (var i = shown; i < minimapMarks.Count; i++)
            {
                minimapMarks[i].dot.gameObject.SetActive(false);
                minimapMarks[i].label.gameObject.SetActive(false);
            }
        }

        void UpdateQuest()
        {
            var quests = host.Quests;
            var current = quests.Current;

            if (current != null)
            {
                questCard.gameObject.SetActive(true);
                questTitle.text = current.Title;
                questGoal.text = quests.Ready ? $"Вернитесь к: {current.Giver}" : current.Goal;
                questProgress.text = quests.Ready ? "готово" : $"{quests.Progress} / {current.Target}";

                return;
            }

            var next = quests.NextFrom(Quests.Teacher);
            questCard.gameObject.SetActive(next != null);

            if (next != null)
            {
                questTitle.text = "Академия клана";
                questGoal.text = $"Наставник академии {Quests.Teacher} ждёт вас с поручением";
                questProgress.text = "";
            }
        }

        void UpdateBar(Cultivator gu)
        {
            for (var i = 0; i < barCells.Length; i++)
            {
                ShowEntry(barCells[i], gu, gu.Bar[i], true);
            }

            skinLine.text = gu.SkinOn ? UiKit.Spaced("Нефритовая кожа") : "";
        }

        /// <summary>Shows an action bar entry in a cell: a Gu (its rank rim, hunger, rest) or a thing (its count).</summary>
        static void ShowEntry(Cell cell, Cultivator gu, string entry, bool live)
        {
            cell.Count.text = "";
            cell.Corner.text = "";
            cell.Dot.enabled = false;
            cell.SetRest(0);
            cell.Mark.color = UiKit.Text;

            if (entry == null)
            {
                cell.Mark.text = "";
                cell.SetRim(UiKit.Hex(0x2A2622));

                return;
            }

            if (entry.StartsWith("gu:"))
            {
                var kind = GuBook.Find(entry.Substring(3));
                var owned = gu.First(kind?.Id);
                cell.Mark.text = kind?.Glyph ?? "?";
                var rim = Aperture.RankColour(kind?.Rank ?? 1);

                if (owned == null)
                {
                    cell.Mark.color = UiKit.Muted;
                    cell.SetRim(UiKit.Line);

                    return;
                }

                var hunger = gu.HungerOf(owned);
                cell.Dot.enabled = hunger >= Hunger.Hungry;
                cell.Dot.color = hunger >= Hunger.Starving ? UiKit.Danger : UiKit.Hunger;
                cell.Mark.color = hunger >= Hunger.Starving ? UiKit.Muted : kind.Skill == SkillKind.JadeSkin && gu.SkinOn ? UiKit.EssenceLit : UiKit.Text;
                cell.SetRim(kind.Skill == SkillKind.JadeSkin && gu.SkinOn ? UiKit.GoldLit : rim);

                if (live && kind.Cooldown > 0)
                {
                    cell.SetRest(gu.CooldownLeft(kind.Id) / kind.Cooldown);
                }

                if (kind.Skill == SkillKind.Moonblade && !gu.Aperture.CanSpend(kind.Cost))
                {
                    cell.Mark.color = UiKit.Danger;
                }

                return;
            }

            var item = ItemBook.Find(entry.Substring(5));
            var count = item != null ? gu.Bag.Count(item.Id) : 0;
            cell.Mark.text = item?.Glyph ?? "?";
            cell.Mark.color = count > 0 ? UiKit.Text : UiKit.Muted;
            cell.Count.text = count > 0 ? $"{count}" : "0";
            cell.SetRim(UiKit.GoldDeep);
        }

        void UpdateLog()
        {
            var now = Time.unscaledTime;
            var first = Mathf.Max(0, log.Count - LogLines);
            var shown = log.Count - first;

            for (var i = 0; i < LogLines; i++)
            {
                // The newest at the foot.
                var index = i - (LogLines - shown);

                if (index < 0)
                {
                    logTexts[i].text = "";

                    continue;
                }

                var (text, at) = log[first + index];
                var alpha = Mathf.Clamp01((LogFor - (now - at)) / 2);
                logTexts[i].text = $"<color=#C9A563{Mathf.RoundToInt(alpha * 255):X2}>Система:</color> {text}";
                logTexts[i].color = new Color(UiKit.Text.r, UiKit.Text.g, UiKit.Text.b, alpha);
            }
        }
    }
}
