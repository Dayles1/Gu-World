using System.Collections.Generic;
using GuWorld.Cultivation;
using GuWorld.Items;
using GuWorld.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// The HUD (the design's "HUD — ПК"): at the top left the hero — their rank
    /// inside a ring of essence, their name, health, the place and the hour,
    /// their stones; at the top right a diamond-shaped minimap with the north and
    /// where they stand, and the task under it; at the foot the Gu's keys 1–4 and,
    /// apart, the stone's key V; the crosshair in the middle (where blows go), and
    /// under it, while a stone is drawn in, a ring filling for three seconds; the
    /// log of what happened at the bottom left; the keys of the hero's pages at
    /// the bottom right; and over the bar what one can do here.
    /// </summary>
    public partial class Menus
    {
        /// <summary>Metres across the minimap, at each of its zooms (+ and − while playing); the first three on the finer map round the hero.</summary>
        static readonly float[] MinimapSpans = { 130, 260, 400, 1500, 4000 };
        int minimapZoom = 1;
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
        RectTransform minimapPin;
        Text coordsLine;
        readonly List<(RectTransform dot, Text label)> minimapMarks = new List<(RectTransform, Text)>();
        /// <summary>Where the minimap's names already are this frame (a name that would cover another is left out, its dot kept).</summary>
        readonly List<Rect> minimapTaken = new List<Rect>();
        RectTransform questCard;
        Text questTitle;
        Text questGoal;
        Text questProgress;
        readonly Cell[] barCells = new Cell[Cultivator.BarSize];
        Cell stoneCell;
        Cell fistCell;
        Text fistLine;
        Text skinLine;
        RectTransform crosshair;
        RectTransform absorbing;
        Image absorbFill;
        Text absorbLine;
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
            rankMark = UiKit.Mark("I", inner.transform, 46, UiKit.Gold);
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
            var coin = UiKit.Icon("stone", hud, 22, UiKit.Gold).Shadowed();
            coin.rectTransform.At(new Vector2(0, 1), new Vector2(203, -198), new Vector2(22, 22));
            stonesLine = UiKit.Label("", hud, 17, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed();
            stonesLine.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 218, -210, -600, 186);

            BuildMinimap();
            BuildQuest();
            BuildBar();
            BuildLog();
            BuildLegend();
            BuildCrosshair();

            promptLine = UiKit.Label("", hud, 22, UiKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            promptLine.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -500, 150, -500, -190);
            promptBar = UiKit.Image("Hold", hud, UiKit.Hex(0x0E0D0C, 0.8f), UiKit.Chamfer).rectTransform;
            promptBar.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -130, 136, -130, -144);
            promptFill = UiKit.Image("Fill", promptBar, UiKit.GoldLit).rectTransform;
            promptFill.Place(Vector2.zero, new Vector2(0, 1), 1, 1, 1, 1);
            promptBar.gameObject.SetActive(false);

            // The hero's pages, at the bottom right.
            var keys = UiKit.Rect("Keys", hud).Place(new Vector2(1, 0), new Vector2(1, 0), -690, 26, 28, -64);
            keys.Stack<HorizontalLayoutGroup>(8, null, TextAnchor.MiddleRight).childForceExpandWidth = false;

            foreach (var (key, name) in new[] { ("K", "Апертура"), ("I", "Сумка"), ("M", "Карта"), ("B", "Тело"), ("T", "Чат"), ("Esc", "Меню") })
            {
                var plate = UiKit.Panel(name, keys, UiKit.Hex(0x0E0D0C, 0.78f));
                plate.Size(38, name.Length > 5 ? 116 : 92, 0);
                var text = UiKit.Label($"<color=#F0D08A><b>{key}</b></color>  {name}", plate.transform, 15, UiKit.Text, TextAnchor.MiddleCenter);
                text.rectTransform.Fill();
            }

            BuildGuide();
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

            // The map's mark (MenusMap): where it is, or at the edge toward it when it lies farther.
            minimapPin = UiKit.Icon("pin", mask.transform, 22, UiKit.Seal).rectTransform;
            minimapPin.pivot = new Vector2(0.5f, 0.04f);
            UiKit.Image("Hole", minimapPin, UiKit.OnGold, UiKit.Disc).rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 3), new Vector2(6, 6));
            // Where the task leads (MenusGuide): a gold diamond, at the edge toward it when farther.
            minimapQuest = UiKit.Image("Quest", mask.transform, UiKit.GoldLit, UiKit.Diamond).rectTransform;
            minimapQuest.sizeDelta = new Vector2(14, 14);
            UiKit.Image("Heart", minimapQuest, UiKit.Seal, UiKit.Diamond).rectTransform.Fill(4);
            minimapArrow = UiKit.Image("Hero", mask.transform, UiKit.GoldLit, UiKit.Arrow).rectTransform;
            minimapArrow.sizeDelta = new Vector2(20, 20);
            var north = UiKit.Mark("С", hud, 18, UiKit.GoldLit).Shadowed();
            north.rectTransform.At(new Vector2(1, 1), centre + new Vector2(0, MinimapSize * 0.5f + 14), new Vector2(30, 24));
            coordsLine = UiKit.Label("", hud, 14, UiKit.Muted, TextAnchor.MiddleCenter).Shadowed();
            coordsLine.rectTransform.At(new Vector2(1, 1), centre - new Vector2(0, MinimapSize * 0.5f + 16), new Vector2(300, 22));
        }

        void BuildQuest()
        {
            questCard = UiKit.Rect("Quest", hud).Place(new Vector2(1, 1), new Vector2(1, 1), -420, -504, 36, 330);
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
            // Who or what, how far and which way (MenusGuide); how far along, on the right of the same line.
            var line = UiKit.Rect("Where", column);
            line.Stack<HorizontalLayoutGroup>(8, null, TextAnchor.MiddleLeft).childForceExpandWidth = false;
            line.Size(22);
            UiKit.Image("Mark", line, UiKit.GoldLit, UiKit.Diamond).Size(12, 12, 0);
            questWhere = UiKit.Label("", line, 15, UiKit.Text).Shadowed();
            questWhere.Size(-1, -1, 1);
            questWhere.horizontalOverflow = HorizontalWrapMode.Overflow;
            questProgress = UiKit.Label("", line, 16, UiKit.GoldLit, TextAnchor.MiddleRight, FontStyle.Bold).Shadowed();
            questProgress.Size(-1, 70, 0);
        }

        void BuildBar()
        {
            const float size = 70;
            const float gap = 8;
            // The Gu's keys, then — set apart — the stone's.
            const float apart = 34;
            var width = (Cultivator.BarSize + 1) * size + (Cultivator.BarSize - 1) * gap + apart;
            var bar = UiKit.Rect("Bar", hud).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -width / 2, 26, -width / 2, -(26 + size));
            var caption = UiKit.Caps("Гу", bar, 12, UiKit.Muted);
            caption.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 0, 2, -200, -22);

            for (var i = 0; i < Cultivator.BarSize; i++)
            {
                var cell = UiKit.Cell(bar, size);
                cell.Back.rectTransform.anchorMin = cell.Back.rectTransform.anchorMax = new Vector2(0, 0.5f);
                cell.Back.rectTransform.anchoredPosition = new Vector2(size / 2 + i * (size + gap), 0);
                cell.Key.text = $"{i + 1}";
                barCells[i] = cell;
            }

            // The fists, set apart to the left of the Gu: F readies them.
            fistCell = UiKit.Cell(bar, size);
            fistCell.Back.rectTransform.anchorMin = fistCell.Back.rectTransform.anchorMax = new Vector2(0, 0.5f);
            fistCell.Back.rectTransform.anchoredPosition = new Vector2(-apart - size / 2, 0);
            fistCell.Key.text = "F";
            fistCell.SetIcon("fist");
            var fistCaption = UiKit.Caps("Кулаки", bar, 12, UiKit.Muted);
            fistCaption.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), -apart - size - 20, 2, apart - 20, -22);
            fistLine = UiKit.Label("", hud, 15, UiKit.GoldLit, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            fistLine.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -420, 128, -420, -150);

            stoneCell = UiKit.Cell(bar, size);
            stoneCell.Back.rectTransform.anchorMin = stoneCell.Back.rectTransform.anchorMax = new Vector2(1, 0.5f);
            stoneCell.Back.rectTransform.anchoredPosition = new Vector2(-size / 2, 0);
            stoneCell.Key.text = "V";
            stoneCell.SetIcon("stone");
            stoneCell.SetRim(UiKit.GoldDeep);
            var stoneCaption = UiKit.Caps("Камень", bar, 12, UiKit.Muted);
            stoneCaption.alignment = TextAnchor.MiddleRight;
            stoneCaption.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), -200, 2, 0, -22);

            skinLine = UiKit.Label("", hud, 15, UiKit.EssenceLit, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            skinLine.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -300, 100, -300, -124);
        }

        /// <summary>The crosshair, and the ring that fills while a stone is drawn in.</summary>
        void BuildCrosshair()
        {
            crosshair = UiKit.Rect("Crosshair", hud).At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            var ring = UiKit.Image("Ring", crosshair, new Color(UiKit.GoldLit.r, UiKit.GoldLit.g, UiKit.GoldLit.b, 0.75f), UiKit.Ring);
            ring.rectTransform.Fill();
            var dot = UiKit.Image("Dot", crosshair, UiKit.GoldLit, UiKit.Disc);
            dot.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5, 5));
            ring.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);

            absorbing = UiKit.Rect("Absorbing", hud).At(new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(96, 96));
            var back = UiKit.Image("Back", absorbing, UiKit.Hex(0x0E0D0C, 0.72f), UiKit.Disc);
            back.rectTransform.Fill();
            var track = UiKit.Image("Track", absorbing, new Color(UiKit.Essence.r, UiKit.Essence.g, UiKit.Essence.b, 0.25f), UiKit.Ring);
            track.rectTransform.Fill(4);
            absorbFill = UiKit.Image("Fill", absorbing, UiKit.EssenceLit, UiKit.Ring);
            Radial(absorbFill);
            absorbFill.rectTransform.Fill(4);
            var stone = UiKit.Icon("stone", absorbing, 38, UiKit.EssenceLit);
            stone.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38, 38));
            absorbLine = UiKit.Label("", absorbing, 17, UiKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            absorbLine.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -160, -40, -160, 8);
            absorbing.gameObject.SetActive(false);
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

        RectTransform legend;

        /// <summary>The hitbox view's key: what each colour is (shown while the view is on).</summary>
        void BuildLegend()
        {
            legend = UiKit.Panel("Hitbox Legend", hud, UiKit.Hex(0x0E0D0C, 0.82f)).rectTransform;
            legend.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), 24, -150, -284, -150);
            var list = UiKit.Rect("List", legend).Fill();
            list.Stack<VerticalLayoutGroup>(2, new RectOffset(14, 14, 10, 10));
            UiKit.Caps("Хитбоксы · F7", list, 13).Size(22);

            foreach (var (_, name, colour) in Render.HitboxView.Legend)
            {
                var row = UiKit.Rect(name, list);
                row.Size(22);
                var chip = UiKit.Image("Chip", row, colour);
                chip.rectTransform.At(new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(14, 4));
                UiKit.Label(name, row, 14, UiKit.Text).rectTransform.Place(Vector2.zero, Vector2.one, 24, 0, 0, 0);
            }

            UiKit.Label("Части тела — цвета страницы «Тело» (B)", list, 13, UiKit.Muted).Size(34);
            legend.gameObject.SetActive(false);
        }

        void UpdateHud()
        {
            if (legend != null && legend.gameObject.activeSelf != Render.HitboxView.Shown)
            {
                legend.gameObject.SetActive(Render.HitboxView.Shown);
            }

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
            rankMark.text = Aperture.RankNumeral(a.Rank);
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
            UpdateGuide();
            UpdateBar(gu);
            UpdateLog();
        }

        /// <summary>The minimap closer in (-1) or farther out (+1).</summary>
        public void ZoomMinimap(int step)
        {
            minimapZoom = Mathf.Clamp(minimapZoom + step, 0, MinimapSpans.Length - 1);
            Notify($"Миникарта: {(MinimapSpans[minimapZoom] >= 1000 ? $"{MinimapSpans[minimapZoom] / 1000:0.#} км" : $"{MinimapSpans[minimapZoom]:0} м")} в поперечнике");
        }

        void UpdateMinimap()
        {
            var minimapSpan = MinimapSpans[minimapZoom];
            var near = host.NearMap;
            var heroAt = new Vector2(host.HeroAt.x, host.HeroAt.z);
            // The finer map while all that is shown lies on it; farther out, the whole mountain's.
            var onNear = near != null && near.Area.Contains(heroAt - Vector2.one * minimapSpan / 2) && near.Area.Contains(heroAt + Vector2.one * minimapSpan / 2);
            var map = onNear ? near : host.Map ?? near;

            if (map == null)
            {
                minimap.enabled = false;

                return;
            }

            minimap.enabled = true;
            minimap.texture = map.Texture;
            var at = host.HeroAt;
            var centre = map.ToMap(at);
            var span = new Vector2(minimapSpan / map.Area.width, minimapSpan / map.Area.height);
            minimap.uvRect = new Rect(centre - span * 0.5f, span);
            minimapArrow.localEulerAngles = new Vector3(0, 0, -host.HeroFacing * Mathf.Rad2Deg);
            coordsLine.text = $"{at.x:0}, {at.z:0}";
            minimapPin.gameObject.SetActive(MapPin.HasValue);
            var quest = host.QuestTarget;
            minimapQuest.gameObject.SetActive(quest.HasValue);

            if (quest.HasValue)
            {
                var toQuest = (map.ToMap(quest.Value.at) - centre) / span * MinimapSize;
                var rim = MinimapSize * 0.5f - 9;
                var round = Mathf.Abs(toQuest.x) + Mathf.Abs(toQuest.y);
                minimapQuest.anchoredPosition = round > rim ? toQuest * (rim / round) : toQuest;
            }

            if (MapPin.HasValue)
            {
                var pin = MapPin.Value;
                var toPin = (map.ToMap(pin) - centre) / span * MinimapSize;
                var edge = MinimapSize * 0.5f - 12;
                var round = Mathf.Abs(toPin.x) + Mathf.Abs(toPin.y);
                minimapPin.anchoredPosition = round > edge ? toPin * (edge / round) : toPin;
                coordsLine.text += $"   ·   метка {Distance(new Vector2(pin.x - at.x, pin.z - at.z).magnitude)}";
            }

            var shown = 0;
            minimapTaken.Clear();

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
                dot.anchoredPosition = offset;
                var width = mark.Label.Length * label.fontSize * 0.56f;
                var box = new Rect(offset.x - width * 0.5f, offset.y + 4, width, label.fontSize + 4);
                var fits = !minimapTaken.Exists(r => r.Overlaps(box));
                label.gameObject.SetActive(fits);

                if (fits)
                {
                    minimapTaken.Add(box);
                    label.rectTransform.anchoredPosition = offset + new Vector2(0, 13);
                    label.text = mark.Label;
                }
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

            var next = quests.Next;
            questCard.gameObject.SetActive(next != null);

            if (next != null)
            {
                questTitle.text = "Новое поручение";
                questGoal.text = $"Вас ждёт {next.GiverTitle ?? ""} {next.Giver}".Replace("  ", " ");
                questProgress.text = "";
            }
        }

        void UpdateBar(Cultivator gu)
        {
            for (var i = 0; i < barCells.Length; i++)
            {
                ShowEntry(barCells[i], gu, gu.Bar[i], true);
            }

            // The fists: lit while ready, the special blows' letters in the corner (dim while they rest), what the keys do above.
            var fists = host.Fists;

            if (fists != null)
            {
                var resting = Mathf.Max(fists.RestLeft(GuWorld.Combat.Blow.Heavy) / GuWorld.Combat.Melee.RestOf(GuWorld.Combat.Blow.Heavy),
                    fists.RestLeft(GuWorld.Combat.Blow.Spin) / GuWorld.Combat.Melee.RestOf(GuWorld.Combat.Blow.Spin));
                fistCell.SetRim(fists.Ready ? UiKit.GoldLit : UiKit.GoldDeep);
                fistCell.Mark.color = fists.Ready ? UiKit.GoldLit : UiKit.Muted;
                fistCell.Corner.text = fists.Ready ? "Q R" : "";
                fistCell.Corner.color = resting > 0 ? UiKit.Muted : UiKit.GoldLit;
                fistCell.SetRest(fists.Ready ? resting : 0);
                fistLine.text = fists.Ready
                    ? $"ЛКМ — серия   ·   ПКМ — блок   ·   Q — мощный удар{Rest(fists, GuWorld.Combat.Blow.Heavy)}   ·   R — с разворота{Rest(fists, GuWorld.Combat.Blow.Spin)}"
                    : "";
            }

            var stones = gu.Stones;
            stoneCell.Count.text = $"{stones}";
            stoneCell.Mark.color = stones > 0 ? (gu.Absorbing ? UiKit.EssenceLit : Icons.Tint("stone")) : UiKit.Muted;
            stoneCell.SetRim(gu.Absorbing ? UiKit.EssenceLit : UiKit.GoldDeep);
            stoneCell.SetRest(gu.Absorbing ? 1 - gu.AbsorbProgress : 0);
            skinLine.text = gu.SkinOn ? UiKit.Spaced("Нефритовая кожа") : "";

            // The crosshair only while playing (not in a conversation); the ring while a stone is drawn in.
            crosshair.gameObject.SetActive(Current == Page.None);
            absorbing.gameObject.SetActive(gu.Absorbing);

            if (gu.Absorbing)
            {
                absorbFill.fillAmount = gu.AbsorbProgress;
                absorbLine.text = $"Поглощение камня · {(1 - gu.AbsorbProgress) * Cultivator.AbsorbTime:0.0} с";
            }
        }

        /// <summary>" (1.2 с)" while a special blow rests, else nothing.</summary>
        static string Rest(GuWorld.Combat.Melee fists, GuWorld.Combat.Blow blow)
        {
            var left = fists.RestLeft(blow);

            return left > 0 ? $" ({left:0.0} с)" : "";
        }

        /// <summary>Shows a Gu's key in a cell: the Gu's icon, its rank rim, hunger and rest.</summary>
        static void ShowEntry(Cell cell, Cultivator gu, string entry, bool live)
        {
            cell.Count.text = "";
            cell.Corner.text = "";
            cell.Dot.enabled = false;
            cell.SetRest(0);
            cell.Mark.color = UiKit.Text;

            if (entry == null || !entry.StartsWith("gu:"))
            {
                cell.SetIcon(null);
                cell.SetRim(UiKit.Hex(0x2A2622));

                return;
            }

            if (entry.StartsWith("gu:"))
            {
                var kind = GuBook.Find(entry.Substring(3));
                var owned = gu.First(kind?.Id);
                cell.SetIcon(kind?.Icon);
                cell.Corner.text = kind != null ? Aperture.RankNumeral(kind.Rank) : "";
                cell.Corner.color = Aperture.RankColour(kind?.Rank ?? 1);
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
                cell.Mark.color = hunger >= Hunger.Starving ? UiKit.Muted : kind.Skill == SkillKind.JadeSkin && gu.SkinOn ? UiKit.EssenceLit : Icons.Tint(kind.Icon);
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
                // The chat shows the log itself while it is open.
                var alpha = Current == Page.Chat ? 0 : Mathf.Clamp01((LogFor - (now - at)) / 2);
                var said = text.StartsWith("> ") || text.StartsWith("!") || text.StartsWith("<color");
                logTexts[i].text = said ? ChatLine(text) : $"<color=#C9A563{Mathf.RoundToInt(alpha * 255):X2}>Система:</color> {text}";
                logTexts[i].color = new Color(UiKit.Text.r, UiKit.Text.g, UiKit.Text.b, alpha);
            }
        }
    }
}
