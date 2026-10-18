using GuWorld.Cultivation;
using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// A Gu's card on the HUD's bar: its key at the top left and its rank at the top right; its mark on a medal
    /// ringed in the rank's colour, a shade sweeping round it while it rests; what one use costs, on the medal;
    /// its name under it; and at the foot a bar of how tired it is — green, amber, red, and pulsing red once
    /// using it means forcing it. A held Gu glows while it is up; the chosen card (the immortal's aperture) is
    /// raised and framed in gold.
    /// </summary>
    public class GuSlot
    {
        public const float Width = 86;
        public const float Height = 108;

        public RectTransform Root;
        Image glow, back, frame, ring, icon, sweep, tiredFill;
        RectTransform tiredBar;
        Text key, rank, name, cost;

        static readonly Color Rested = UiKit.Hex(0x6FBF73);
        static readonly Color Tiring = UiKit.Hex(0xE0A13A);
        static readonly Color Spent = UiKit.Hex(0xE0533F);

        public static GuSlot Build(Transform parent, string keyName)
        {
            var slot = new GuSlot();
            var root = UiKit.Rect("Gu Slot " + keyName, parent);
            root.sizeDelta = new Vector2(Width, Height);
            slot.Root = root;

            slot.glow = UiKit.Image("Glow", root, Color.clear, UiKit.SoftEdge);
            slot.glow.rectTransform.Fill(-14);
            slot.back = UiKit.Image("Back", root, UiKit.Hex(0x0E0D0C, 0.9f), UiKit.Chamfer);
            slot.back.rectTransform.Fill();
            slot.frame = UiKit.Frame(slot.back.transform, UiKit.Line);

            var medal = UiKit.Rect("Medal", root).At(new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(56, 56));
            UiKit.Image("Disc", medal, UiKit.Hex(0x1A1613), UiKit.Disc).rectTransform.Fill();
            slot.ring = UiKit.Image("Ring", medal, UiKit.Line, UiKit.Ring);
            slot.ring.rectTransform.Fill();
            slot.icon = UiKit.Icon(null, medal, 32, Color.white);
            slot.icon.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(32, 32));
            slot.sweep = UiKit.Image("Rest", medal, new Color(0, 0, 0, 0.66f), UiKit.Disc);
            slot.sweep.rectTransform.Fill();
            slot.sweep.type = Image.Type.Filled;
            slot.sweep.fillMethod = Image.FillMethod.Radial360;
            slot.sweep.fillOrigin = (int)Image.Origin360.Top;
            slot.sweep.fillClockwise = false;
            slot.sweep.fillAmount = 0;

            slot.key = UiKit.Label(keyName, root, 14, UiKit.Muted, TextAnchor.UpperLeft, FontStyle.Bold).Shadowed();
            slot.key.rectTransform.Fill(6);
            slot.rank = UiKit.Label("", root, 12, UiKit.Muted, TextAnchor.UpperRight, FontStyle.Bold).Shadowed();
            slot.rank.rectTransform.Fill(6);
            slot.cost = UiKit.Label("", medal, 12, UiKit.Text, TextAnchor.LowerRight, FontStyle.Bold).Shadowed();
            slot.cost.rectTransform.Place(Vector2.zero, Vector2.one, -14, -6, -16, 0);
            slot.name = UiKit.Label("", root, 11, UiKit.Text, TextAnchor.MiddleCenter).Shadowed();
            slot.name.rectTransform.Place(Vector2.zero, new Vector2(1, 0), 3, 12, 3, -30);
            slot.name.horizontalOverflow = HorizontalWrapMode.Wrap;
            slot.name.resizeTextForBestFit = true;
            slot.name.resizeTextMinSize = 9;
            slot.name.resizeTextMaxSize = 11;

            var bar = UiKit.Image("Tired", root, UiKit.Hex(0x000000, 0.55f));
            bar.rectTransform.Place(Vector2.zero, new Vector2(1, 0), 9, 6, 9, -10);
            slot.tiredBar = bar.rectTransform;
            slot.tiredFill = UiKit.Image("Fill", bar.transform, Rested);
            slot.tiredFill.rectTransform.Place(Vector2.zero, new Vector2(0, 1));

            return slot;
        }

        /// <summary>A card with nothing on it: its key, and a word.</summary>
        public void Empty(string word, bool chosen)
        {
            Raise(chosen);
            back.color = UiKit.Hex(0x0E0D0C, chosen ? 0.92f : 0.6f);
            frame.color = chosen ? UiKit.GoldLit : UiKit.Hex(0x2A2622);
            glow.color = Color.clear;
            ring.color = UiKit.Hex(0x2A2622);
            icon.enabled = false;
            sweep.fillAmount = 0;
            rank.text = "";
            cost.text = "";
            name.text = word;
            name.color = UiKit.Muted;
            tiredBar.gameObject.SetActive(false);
        }

        /// <summary>
        /// A Gu on the card: `rest` how far through its own short rest (0 ready), `tired` how tired (of 100), `price` what a
        /// use costs this master (below 0: none shown), `affordable` whether the sea holds it, `held` whether it is up,
        /// `chosen` the card picked (the immortal's aperture), `label` its name, shortened.
        /// </summary>
        public void Show(GuKind kind, float rest, float tired, float price, bool affordable, bool held, bool chosen, string label)
        {
            Raise(chosen);
            var rankColour = Aperture.RankColour(kind.Rank);
            back.color = chosen ? UiKit.Hex(0x2A1E14, 0.95f) : UiKit.Hex(0x0E0D0C, 0.9f);
            frame.color = chosen ? UiKit.GoldLit : held ? UiKit.EssenceLit : new Color(rankColour.r, rankColour.g, rankColour.b, 0.65f);
            ring.color = held ? UiKit.EssenceLit : rankColour;
            icon.enabled = true;
            icon.sprite = Icons.Get(kind.Icon);
            icon.color = affordable || held ? Icons.Tint(kind.Icon) : UiKit.Muted;
            sweep.fillAmount = Mathf.Clamp01(rest);
            rank.text = Aperture.RankNumeral(kind.Rank);
            rank.color = rankColour;
            cost.text = price >= 0 ? $"{price:0.#}%" : "";
            cost.color = affordable ? UiKit.Text : UiKit.Danger;
            name.text = held ? "● " + label : label;
            name.color = held ? UiKit.EssenceLit : UiKit.Text;

            // The pulse of a Gu held up, or forced past its strength.
            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6);
            glow.color = held ? new Color(UiKit.EssenceLit.r, UiKit.EssenceLit.g, UiKit.EssenceLit.b, 0.25f + 0.15f * pulse)
                : tired >= 100 ? new Color(Spent.r, Spent.g, Spent.b, 0.2f + 0.25f * pulse)
                : chosen ? new Color(UiKit.GoldLit.r, UiKit.GoldLit.g, UiKit.GoldLit.b, 0.22f) : Color.clear;

            tiredBar.gameObject.SetActive(!kind.Passive && !kind.OneUse);
            tiredFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(tired / 100), 1);
            tiredFill.color = tired >= 100 ? Color.Lerp(Spent, Color.white, 0.35f * pulse) : tired >= 80 ? Spent : tired >= 50 ? Tiring : Rested;
        }

        void Raise(bool chosen)
        {
            Root.localScale = Vector3.one * (chosen ? 1.08f : 1);
        }

        /// <summary>A Gu's name for its card: without the word "Гу", and short.</summary>
        public static string Short(string name)
        {
            var shortName = name.StartsWith("Гу ") ? name.Substring(3) : name;

            return shortName.Length > 22 ? shortName.Substring(0, 21) + "…" : shortName;
        }
    }
}
