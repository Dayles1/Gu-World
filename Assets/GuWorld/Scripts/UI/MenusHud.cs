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
        /// <summary>The Gu's cards (keys 1–4; on the immortal's aperture the eight cells at hand, 1–8).</summary>
        GuSlot[] slots = new GuSlot[0];
        /// <summary>The Gu at hand (or the fists' blows), in words, over the bar.</summary>
        Text handLine;
        Cell stoneCell;
        /// <summary>The fists' card, the dots of their chain, and the special blows' cells (Q, R).</summary>
        GuSlot fistSlot;
        Image[] chainDots;
        Cell heavyCell;
        Cell spinCell;
        Text skinLine;
        /// <summary>The sea of essence over the bar: how full, and what the chosen Gu would take of it.</summary>
        RectTransform essenceFill;
        RectTransform essenceCost;
        Image essenceCostImage;
        Text essenceLabel;
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

            // The hero's pages, in a column at the bottom right (clear of the bar).
            var keys = UiKit.Rect("Keys", hud).Place(new Vector2(1, 0), new Vector2(1, 0), -160, 26, 28, -280);
            keys.Stack<VerticalLayoutGroup>(5, null, TextAnchor.LowerRight).childForceExpandHeight = false;

            var hints = host.Sandbox
                ? new[] { ("Tab", "Все Гу"), ("M", "Карта"), ("T", "Чат"), ("Esc", "Меню") }
                : new[] { ("K", "Апертура"), ("I", "Сумка"), ("M", "Карта"), ("B", "Тело"), ("T", "Чат"), ("Esc", "Меню") };

            foreach (var (key, name) in hints)
            {
                var plate = UiKit.Panel(name, keys, UiKit.Hex(0x0E0D0C, 0.78f));
                plate.Size(34, 132, 0);
                var text = UiKit.Label($"<color=#F0D08A><b>{key}</b></color>  {name}", plate.transform, 15, UiKit.Text, TextAnchor.MiddleCenter);
                text.rectTransform.Fill();
            }

            BuildGuide();
            BuildPlates();
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

        /// <summary>
        /// The bar at the foot of the screen: the fists' card (F) with their special blows beside it (Q, R) and the dots
        /// of their chain; the Gu's cards (1–4, or the immortal's aperture's eight); the stone (V); and over the cards
        /// the sea of essence as a bar, with what the chosen Gu would take of it marked.
        /// </summary>
        void BuildBar()
        {
            const float gap = 8;
            // The fists' card and the Gu's, set apart; the stone's after them.
            const float apart = 26;
            const float mini = 46;
            var keys = host.Sandbox ? GuWorldGame.SandboxCells : Cultivator.BarSize;
            var span = keys * GuSlot.Width + (keys - 1) * gap;
            var width = mini + 6 + GuSlot.Width + apart + span + (host.Sandbox ? 0 : apart + 70);
            var bar = UiKit.Rect("Bar", hud).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -width / 2, 22, -width / 2, -(22 + GuSlot.Height));
            var x = 0f;

            // Q and R, stacked.
            heavyCell = MiniCell(bar, "Q", "bolt", x + mini / 2, GuSlot.Height / 2 - mini / 2 - 3);
            spinCell = MiniCell(bar, "R", "wing", x + mini / 2, -GuSlot.Height / 2 + mini / 2 + 3);
            x += mini + 6;

            fistSlot = GuSlot.Build(bar, "F");
            Put(fistSlot.Root, x + GuSlot.Width / 2);
            chainDots = new Image[GuWorld.Combat.Melee.Chain.Length];

            for (var i = 0; i < chainDots.Length; i++)
            {
                chainDots[i] = UiKit.Image("Chain", fistSlot.Root, UiKit.Muted, UiKit.Disc);
                chainDots[i].rectTransform.At(new Vector2(0.5f, 0), new Vector2((i - (chainDots.Length - 1) * 0.5f) * 13, 9), new Vector2(7, 7));
            }

            x += GuSlot.Width + apart;
            var slotsFrom = x;
            slots = new GuSlot[keys];

            for (var i = 0; i < keys; i++)
            {
                slots[i] = GuSlot.Build(bar, $"{i + 1}");
                Put(slots[i].Root, x + GuSlot.Width / 2);
                x += GuSlot.Width + gap;
            }

            stoneCell = UiKit.Cell(bar, 70);
            stoneCell.gameObject.SetActive(!host.Sandbox);
            stoneCell.Back.rectTransform.anchorMin = stoneCell.Back.rectTransform.anchorMax = new Vector2(1, 0.5f);
            stoneCell.Back.rectTransform.anchoredPosition = new Vector2(-35, 0);
            stoneCell.Key.text = "V";
            stoneCell.SetIcon("stone");
            stoneCell.SetRim(UiKit.GoldDeep);

            // The sea of essence, over the Gu's cards.
            var sea = UiKit.Rect("Essence", bar).Place(new Vector2(0, 1), new Vector2(0, 1), slotsFrom, 8, -(slotsFrom + span), -20);
            UiKit.Image("Back", sea, UiKit.Hex(0x000000, 0.55f)).rectTransform.Fill();
            var fill = UiKit.Image("Fill", sea, UiKit.Essence);
            fill.rectTransform.Place(Vector2.zero, new Vector2(0, 1));
            essenceFill = fill.rectTransform;
            essenceCostImage = UiKit.Image("Cost", sea, new Color(1, 1, 1, 0.55f));
            essenceCost = essenceCostImage.rectTransform;
            essenceCost.Place(Vector2.zero, new Vector2(0, 1));
            essenceLabel = UiKit.Label("", sea, 12, UiKit.EssenceLit, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed();
            essenceLabel.rectTransform.Place(new Vector2(0, 1), Vector2.one, 0, 2, 0, -18);

            handLine = UiKit.Label("", hud, 16, UiKit.Text, TextAnchor.MiddleCenter).Shadowed();
            handLine.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -760, 174, -760, -226);
            skinLine = UiKit.Label("", hud, 15, UiKit.EssenceLit, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            skinLine.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -700, 228, -700, -252);
        }

        /// <summary>A card's middle `x` along the bar (from its left), in its height's middle.</summary>
        static void Put(RectTransform card, float x)
        {
            card.anchorMin = card.anchorMax = new Vector2(0, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = new Vector2(x, 0);
        }

        /// <summary>A small cell for a special blow: its key, and a shade while it rests.</summary>
        static Cell MiniCell(Transform bar, string key, string icon, float x, float y)
        {
            var cell = UiKit.Cell(bar, 46);
            cell.Back.rectTransform.anchorMin = cell.Back.rectTransform.anchorMax = new Vector2(0, 0.5f);
            cell.Back.rectTransform.anchoredPosition = new Vector2(x, y);
            cell.Key.text = key;
            cell.Key.color = UiKit.GoldLit;
            cell.SetIcon(icon);
            cell.SetRim(UiKit.GoldDeep);

            return cell;
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
            UpdatePlates();
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
            // The immortal's aperture (the proving ground) has no tasks.
            if (host.Sandbox)
            {
                questCard.gameObject.SetActive(false);

                return;
            }

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
            var fists = host.Fists;
            GuKind chosen = null;

            if (host.Sandbox)
            {
                chosen = UpdateHand(gu);
            }
            else
            {
                for (var i = 0; i < slots.Length; i++)
                {
                    var kind = gu.KindOn(i);
                    ShowSlot(slots[i], gu, kind, false);
                }

                handLine.text = "";
            }

            // The fists: lit while ready; their chain's dots (the next blow bright); the special blows resting.
            if (fists != null)
            {
                var ready = fists.Ready;
                var blow = fists.Current;
                fistSlot.Show(FistKind, 0, 0, -1, true, false, ready, blow.HasValue ? GuWorld.Combat.Melee.Name(blow.Value) : ready ? "Кулаки" : "Кулаки · F");

                for (var i = 0; i < chainDots.Length; i++)
                {
                    var lit = ready && (fists.SinceBlow < 0.6f || blow.HasValue) && i < fists.ChainStep;
                    chainDots[i].color = !ready ? UiKit.Hex(0x2A2622) : lit ? UiKit.GoldLit : i == fists.ChainStep ? UiKit.Gold : UiKit.Muted;
                }

                Special(heavyCell, fists, GuWorld.Combat.Blow.Heavy);
                Special(spinCell, fists, GuWorld.Combat.Blow.Spin);

                if (ready)
                {
                    handLine.text = $"<b>Кулаки</b>  ·  ЛКМ — серия: джеб → кросс → хук → пинок  ·  ПКМ — блок  ·  " +
                                    $"<b>Q</b> апперкот{Rest(fists, GuWorld.Combat.Blow.Heavy)}  ·  <b>R</b> разворот{Rest(fists, GuWorld.Combat.Blow.Spin)}  ·  F — убрать";
                }
            }

            // The sea: how full, and the chosen Gu's price marked at its end (red when the sea does not hold it).
            var a = gu.Aperture;
            var full = Mathf.Clamp01(a.Essence / 100);
            essenceFill.anchorMax = new Vector2(full, 1);
            var price = chosen != null && !chosen.Passive && !gu.EndlessEssence ? Mathf.Min(gu.CostOf(chosen), 100) / 100 : 0;
            essenceCost.anchorMin = new Vector2(Mathf.Max(0, full - price), 0);
            essenceCost.anchorMax = new Vector2(full, 1);
            essenceCostImage.color = price <= full ? new Color(1, 1, 1, 0.5f) : new Color(UiKit.Danger.r, UiKit.Danger.g, UiKit.Danger.b, 0.8f);
            essenceLabel.text = gu.EndlessEssence ? $"ЭССЕНЦИЯ {a.Essence:0}% · БЕСКОНЕЧНА" : $"ЭССЕНЦИЯ {a.Essence:0}% / {a.Cap:0}%" +
                                (chosen != null && !chosen.Passive ? $"   ·   {GuSlot.Short(chosen.Name)}: {gu.CostOf(chosen):0.#}%" : "");

            var stones = gu.Stones;
            stoneCell.Count.text = $"{stones}";
            stoneCell.Mark.color = stones > 0 ? (gu.Absorbing ? UiKit.EssenceLit : Icons.Tint("stone")) : UiKit.Muted;
            stoneCell.SetRim(gu.Absorbing ? UiKit.EssenceLit : UiKit.GoldDeep);
            stoneCell.SetRest(gu.Absorbing ? 1 - gu.AbsorbProgress : 0);
            skinLine.text = HeldLine(gu);

            // The crosshair only while playing (not in a conversation); the ring while a stone is drawn in.
            crosshair.gameObject.SetActive(Current == Page.None);
            absorbing.gameObject.SetActive(gu.Absorbing);

            if (gu.Absorbing)
            {
                absorbFill.fillAmount = gu.AbsorbProgress;
                absorbLine.text = $"Поглощение камня · {(1 - gu.AbsorbProgress) * Cultivator.AbsorbTime:0.0} с";
            }
        }

        /// <summary>The fists' card's look (a Gu-like kind only for its mark and its colour).</summary>
        static readonly GuKind FistKind = new GuKind { Id = "fists", Name = "Кулаки", Icon = "fist", Rank = 1, Skill = SkillKind.None };

        /// <summary>A special blow's cell: lit while the fists are ready, shaded while it rests.</summary>
        static void Special(Cell cell, GuWorld.Combat.Melee fists, GuWorld.Combat.Blow blow)
        {
            var rest = fists.RestLeft(blow) / GuWorld.Combat.Melee.RestOf(blow);
            cell.SetRest(fists.Ready ? rest : 0);
            cell.Mark.color = !fists.Ready ? UiKit.Hex(0x4A4238) : rest > 0 ? UiKit.Muted : UiKit.GoldLit;
            cell.SetRim(fists.Ready && rest <= 0 ? UiKit.Gold : UiKit.Hex(0x2A2622));
        }

        /// <summary>A Gu on a card, live: its rest, its tiredness, its price for this master now.</summary>
        static void ShowSlot(GuSlot slot, Cultivator gu, GuKind kind, bool chosen, string empty = "—")
        {
            if (kind == null)
            {
                slot.Empty(empty, chosen);

                return;
            }

            var owned = gu.First(kind.Id);
            var rest = owned != null && kind.Cooldown > 0 ? gu.CooldownLeft(kind.Id) / kind.Cooldown : 0;
            var held = owned != null && gu.IsHeld(kind.Id);
            var price = kind.Passive ? -1 : gu.EndlessEssence ? 0 : gu.CostOf(kind);
            var affordable = kind.Passive || held || gu.EndlessEssence || gu.Aperture.CanSpend(gu.CostOf(kind));
            slot.Show(kind, rest, owned?.tired ?? 0, price, affordable, held, chosen, GuSlot.Short(kind.Name));
        }

        /// <summary>
        /// The immortal's aperture's eight cells at hand, the chosen one raised — and over them, in words, what the chosen
        /// one is, does and costs. Returns the chosen Gu (null: an empty cell).
        /// </summary>
        GuKind UpdateHand(Cultivator gu)
        {
            var bar = host.SandboxBar;

            for (var i = 0; i < slots.Length; i++)
            {
                ShowSlot(slots[i], gu, GuBook.Find(bar[i]), i == host.SandboxSlot, "пусто · Tab");
            }

            var hand = GuBook.Find(bar[Mathf.Clamp(host.SandboxSlot, 0, bar.Length - 1)]);

            if (hand == null)
            {
                handLine.text = $"Ячейка {host.SandboxSlot + 1} пуста — <b>Tab</b>: выбрать Гу";
            }
            else
            {
                var owned = gu.First(hand.Id);
                var tired = owned?.tired ?? 0;
                var state = hand.Passive ? "работает сам" : owned != null && gu.IsHeld(hand.Id) ? "поднят — ЛКМ снять" :
                    tired >= 100 ? $"<color=#E0736B>устал: через силу — шанс гибели {Cultivator.StrainChance(tired) * 100:0}%</color>" :
                    "ЛКМ — применить";
                handLine.text = $"<b>{hand.Name}</b> <color=#{ColorUtility.ToHtmlStringRGB(Aperture.RankColour(hand.Rank))}>{Aperture.RankNumeral(hand.Rank)}</color>  ·  " +
                                $"{hand.SkillName}  ·  {state}\n<size=13><color=#A69A86>{SkillLine(hand)}</color></size>";
            }

            return hand;
        }

        /// <summary>What the hero's Gu keep up now: held Gu, timed effects, the beasts a sense hears.</summary>
        static string HeldLine(Cultivator gu)
        {
            var parts = new System.Collections.Generic.List<string>();

            foreach (var id in gu.Held)
            {
                var kind = GuBook.Find(id);

                if (kind != null)
                {
                    parts.Add(kind.SkillName);
                }
            }

            foreach (var (skill, name) in new[]
                     {
                         (SkillKind.Might, "сила"), (SkillKind.Haste, "скорость"), (SkillKind.Leap, "крылья"), (SkillKind.Flight, "полёт"),
                         (SkillKind.Berserk, "ярость"), (SkillKind.Ward, "завеса"),
                     })
            {
                var left = gu.BuffLeft(skill);

                if (left > 0)
                {
                    parts.Add($"{name} {left:0} с");
                }
            }

            var (count, nearest) = gu.Sensed();

            if (nearest >= 0)
            {
                parts.Add(count > 0 ? $"зверей рядом: {count}, ближний в {nearest:0} м" : "зверей не слышно");
            }

            return parts.Count > 0 ? UiKit.Spaced(string.Join(" · ", parts)) : "";
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
                var up = gu.IsHeld(kind.Id);
                cell.Dot.enabled = hunger >= Hunger.Hungry;
                cell.Dot.color = hunger >= Hunger.Starving ? UiKit.Danger : UiKit.Hunger;
                cell.Mark.color = hunger >= Hunger.Starving ? UiKit.Muted : up ? UiKit.EssenceLit : Icons.Tint(kind.Icon);
                // Spent (tired past its strength): the rim burns red — using it now is forcing it.
                cell.SetRim(owned.tired >= 100 ? UiKit.Danger : up ? UiKit.GoldLit : rim);

                // The shade over the cell: how tired it is (or, rested, its own short rest).
                if (live)
                {
                    var rest = kind.Cooldown > 0 ? gu.CooldownLeft(kind.Id) / kind.Cooldown : 0;
                    cell.SetRest(Mathf.Max(rest, Mathf.Clamp01(owned.tired / 100)));
                    cell.Count.text = owned.tired >= 1 ? $"{owned.tired:0}" : "";
                    cell.Count.color = owned.tired >= 100 ? UiKit.Danger : owned.tired >= 60 ? UiKit.Hunger : UiKit.Muted;
                }

                if (!up && !gu.Aperture.CanSpend(gu.CostOf(kind)))
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
