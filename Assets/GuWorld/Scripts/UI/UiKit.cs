using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// The parts GU World's menus are built from, in code (uGUI), in the
    /// design's look (GuWorld-Design: "black lacquer, cinnabar, old gold"):
    /// near-black panels with cut corners and a thin bronze frame, warm parchment
    /// text, gold for what is chosen, cinnabar for the main action, the rank
    /// colours for essence and Gu. A canvas scaled from 1920×1080, panels, text,
    /// menu entries that light up when pointed at, buttons, sliders, rows of
    /// choices, item cells and the shapes the HUD needs (rings, discs, diamonds).
    /// </summary>
    public static class UiKit
    {
        // The design's tokens (README "Дизайн-токены интерфейса").
        public static readonly Color Ink = Hex(0x0B0A0A, 0.96f);
        public static readonly Color Panel1 = Hex(0x121010, 0.97f);
        public static readonly Color PanelTop = Hex(0x17130F, 0.97f);
        public static readonly Color Line = Hex(0x3A3128);
        public static readonly Color LineLit = Hex(0x4A3E2E);
        public static readonly Color Text = Hex(0xE6DCC8);
        public static readonly Color Muted = Hex(0xA69A86);
        public static readonly Color Gold = Hex(0xC9A563);
        public static readonly Color GoldLit = Hex(0xF0D08A);
        public static readonly Color GoldDeep = Hex(0x6A5A44);
        public static readonly Color Cinnabar = Hex(0x9C2A23);
        public static readonly Color CinnabarLit = Hex(0xB3342B);
        public static readonly Color Seal = Hex(0xA82E25);
        public static readonly Color Danger = Hex(0xC2413A);
        public static readonly Color Essence = Hex(0x4E9A86);
        public static readonly Color EssenceLit = Hex(0x8FD0BC);
        public static readonly Color Hunger = Hex(0xE0A13A);
        public static readonly Color Life = Hex(0xB8473E);
        /// <summary>A chosen tab or filter: dark brown under gold.</summary>
        public static readonly Color Chosen = Hex(0x3A2A1C);
        public static readonly Color InkLight = Hex(0x1A1613, 0.95f);
        public static readonly Color InkHover = Hex(0x241D17, 0.98f);
        public static readonly Color OnGold = Hex(0xF3E9D6);

        public static Color Hex(int rgb, float alpha = 1) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, alpha);

        static Font font;
        static Sprite rounded, softEdge, chamfer, chamferFrame, ring, disc, diamond, arrow;

        /// <summary>The text font (Unity's own).</summary>
        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");


        /// <summary>A rounded rectangle, sliced so it stretches to any size.</summary>
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeShape(64, 14, Shape.Round, 0);

        /// <summary>A rectangle with its corners cut off (the design's panels and cells), and its frame.</summary>
        public static Sprite Chamfer => chamfer != null ? chamfer : chamfer = MakeShape(64, 10, Shape.Cut, 0);
        public static Sprite ChamferFrame => chamferFrame != null ? chamferFrame : chamferFrame = MakeShape(64, 10, Shape.Cut, 1.6f);

        /// <summary>White fading to clear from left to right, for darkening a side of the screen.</summary>
        public static Sprite SoftEdge => softEdge != null ? softEdge : softEdge = MakeFade();

        /// <summary>A ring (for radial fills: the essence round the portrait, the sea), a disc, a diamond, an arrow.</summary>
        public static Sprite Ring => ring != null ? ring : ring = MakeRound(256, 0.78f);
        public static Sprite Disc => disc != null ? disc : disc = MakeRound(128, 0);
        public static Sprite Diamond => diamond != null ? diamond : diamond = MakeDiamond(128);
        public static Sprite Arrow => arrow != null ? arrow : arrow = MakeArrow(64);

        enum Shape
        {
            Round,
            Cut,
        }

        /// <summary>A sliced shape: rounded or corner-cut, filled or (line &gt; 0) just its outline.</summary>
        static Sprite MakeShape(int size, int corner, Shape shape, float line)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // Signed distance to the edge (positive inside).
                    var px = x + 0.5f;
                    var py = y + 0.5f;
                    var inside = Mathf.Min(Mathf.Min(px, size - px), Mathf.Min(py, size - py));
                    var cx = Mathf.Max(0, Mathf.Max(corner - px, px - (size - corner)));
                    var cy = Mathf.Max(0, Mathf.Max(corner - py, py - (size - corner)));
                    float d;

                    if (shape == Shape.Round)
                    {
                        d = cx > 0 && cy > 0 ? corner - Mathf.Sqrt(cx * cx + cy * cy) : inside;
                    }
                    else
                    {
                        // The cut: a 45° line across each corner.
                        d = Mathf.Min(inside, (Mathf.Min(px, size - px) + Mathf.Min(py, size - py) - corner) / 1.4142f);
                    }

                    var alpha = line > 0 ? Mathf.Clamp01(d + 0.5f) * Mathf.Clamp01(line + 0.5f - d) : Mathf.Clamp01(d + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            var border = corner + 3;

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        /// <summary>A disc, or (hole &gt; 0) a ring whose hole is that share of its radius.</summary>
        static Sprite MakeRound(int size, float hole)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            var r = size * 0.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    var alpha = Mathf.Clamp01(r - 0.5f - d + 0.5f);

                    if (hole > 0)
                    {
                        alpha *= Mathf.Clamp01(d - r * hole + 0.5f);
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeDiamond(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            var r = size * 0.5f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = r - (Mathf.Abs(x + 0.5f - r) + Mathf.Abs(y + 0.5f - r));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d / 1.4142f + 0.5f) * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>An arrowhead pointing up (the hero on the maps).</summary>
        static Sprite MakeArrow(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];
            Vector2 tip = new Vector2(size * 0.5f, size * 0.95f), left = new Vector2(size * 0.12f, size * 0.08f), notch = new Vector2(size * 0.5f, size * 0.3f), right = new Vector2(size * 0.88f, size * 0.08f);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var inside = InTriangle(p, tip, left, notch) || InTriangle(p, tip, notch, right);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(inside ? 255 : 0));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Side(Vector2 u, Vector2 v, Vector2 w) => (u.x - w.x) * (v.y - w.y) - (v.x - w.x) * (u.y - w.y);
            var d1 = Side(p, a, b);
            var d2 = Side(p, b, c);
            var d3 = Side(p, c, a);

            return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
        }

        static Sprite MakeFade()
        {
            const int width = 256;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };

            for (var x = 0; x < width; x++)
            {
                var t = x / (width - 1f);
                texture.SetPixel(x, 0, new Color(1, 1, 1, 1 - t * t * (3 - 2 * t)));
            }

            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f));
        }

        /// <summary>A screen-wide canvas (and the event system the pointer needs).</summary>
        public static Canvas Canvas(string name, int order)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                UnityEngine.Object.DontDestroyOnLoad(events);
            }

            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            return rect;
        }

        /// <summary>Anchors a rectangle: min and max anchors, then offsets from them (left, bottom, right, top).</summary>
        public static RectTransform Place(this RectTransform rect, Vector2 min, Vector2 max, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);

            return rect;
        }

        /// <summary>Puts a rectangle of a size with its centre at a point of its parent (from the parent's anchor point).</summary>
        public static RectTransform At(this RectTransform rect, Vector2 anchor, Vector2 centre, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = centre;
            rect.sizeDelta = size;

            return rect;
        }

        /// <summary>Fills its parent (with an inset).</summary>
        public static RectTransform Fill(this RectTransform rect, float inset = 0) =>
            rect.Place(Vector2.zero, Vector2.one, inset, inset, inset, inset);

        public static Image Image(string name, Transform parent, Color colour, Sprite sprite = null)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = colour;
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = false;

            return image;
        }

        /// <summary>A panel: dark, corners cut, a thin bronze frame (the frame ignores layouts).</summary>
        public static Image Panel(string name, Transform parent, Color? colour = null, Color? frame = null)
        {
            var panel = Image(name, parent, colour ?? Panel1, Chamfer);
            Frame(panel.transform, frame ?? Line);

            return panel;
        }

        /// <summary>The cut-corner outline over a shape.</summary>
        public static Image Frame(Transform parent, Color colour)
        {
            var frame = Image("Frame", parent, colour, ChamferFrame);
            frame.rectTransform.Fill();
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            return frame;
        }

        public static Text Label(string text, Transform parent, int size, Color? colour = null, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var label = Rect("Text", parent).gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = colour ?? Text;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.supportRichText = true;

            return label;
        }

        /// <summary>An icon (Icons), square, tinted.</summary>
        public static Image Icon(string name, Transform parent, float size, Color colour)
        {
            var icon = Image(name ?? "Icon", parent, colour, Icons.Get(name));
            icon.preserveAspect = true;
            icon.rectTransform.sizeDelta = new Vector2(size, size);
            icon.enabled = icon.sprite != null;

            return icon;
        }

        /// <summary>Text set as a mark (a rank's numeral in a seal): centred, bold.</summary>
        public static Text Mark(string text, Transform parent, int size, Color colour) =>
            Label(text, parent, size, colour, TextAnchor.MiddleCenter, FontStyle.Bold);

        /// <summary>A small labelled heading in spaced capitals ("ЗАДАНИЕ").</summary>
        public static Text Caps(string text, Transform parent, int size = 15, Color? colour = null) =>
            Label(Spaced(text), parent, size, colour ?? Gold, TextAnchor.MiddleLeft, FontStyle.Bold);

        /// <summary>Capitals with a thin space between letters, as the design sets its small headings.</summary>
        public static string Spaced(string text) => string.Join(" ", text.ToUpperInvariant().ToCharArray());

        /// <summary>A cinnabar seal: a square with an icon in it.</summary>
        public static Image SealMark(string icon, Transform parent, float size)
        {
            var seal = Image("Seal", parent, Seal, Chamfer);
            seal.rectTransform.sizeDelta = new Vector2(size, size);
            var mark = Icon(icon, seal.transform, size * 0.62f, OnGold);
            mark.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size * 0.62f);

            return seal;
        }

        /// <summary>A cinnabar seal with a numeral or a word in it (a rank).</summary>
        public static Image SealText(string text, Transform parent, float size)
        {
            var seal = Image("Seal", parent, Seal, Chamfer);
            seal.rectTransform.sizeDelta = new Vector2(size, size);
            var mark = Mark(text, seal.transform, Mathf.RoundToInt(size * (text.Length > 2 ? 0.36f : 0.48f)), OnGold);
            mark.rectTransform.Fill();

            return seal;
        }

        /// <summary>A soft dark shadow under text that sits on the world.</summary>
        public static T Shadowed<T>(this T graphic) where T : Graphic
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.75f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            return graphic;
        }

        /// <summary>Lays children out in a column (or a row), with spacing and padding.</summary>
        public static T Stack<T>(this RectTransform rect, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.UpperLeft)
            where T : HorizontalOrVerticalLayoutGroup
        {
            var group = rect.gameObject.AddComponent<T>();
            group.spacing = spacing;
            group.padding = padding ?? new RectOffset(0, 0, 0, 0);
            group.childAlignment = align;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;

            return group;
        }

        public static LayoutElement Size(this Component part, float height = -1, float width = -1, float flexible = -1)
        {
            var element = part.GetComponent<LayoutElement>() ?? part.gameObject.AddComponent<LayoutElement>();

            if (height >= 0) element.preferredHeight = element.minHeight = height;
            if (width >= 0) element.preferredWidth = element.minWidth = width;
            if (flexible >= 0) element.flexibleWidth = flexible;

            return element;
        }

        /// <summary>A big entry of the main menu: text that lights up gold when pointed at, an icon at its right.</summary>
        public static MenuEntry Entry(string text, string detail, Transform parent, Action click, string icon = null)
        {
            var rect = Rect(text, parent);
            var entry = rect.gameObject.AddComponent<MenuEntry>();
            entry.Build(text, detail, click, icon);
            rect.Size(detail != null ? 74 : 58);

            return entry;
        }

        /// <summary>
        /// A button, in the design's plates: plain (dark, framed), gold — the main action,
        /// in cinnabar — red (one that cannot be undone), or a chosen tab (gold on brown).
        /// </summary>
        public static Hover Button(string text, Transform parent, Action click, ButtonKind kind = ButtonKind.Plain, int size = 20)
        {
            var image = Image(text, parent, Color.white, Chamfer);
            image.raycastTarget = true;
            var (rest, lit, ink, frame) = kind switch
            {
                ButtonKind.Gold => (Cinnabar, CinnabarLit, OnGold, Gold),
                ButtonKind.Danger => (Hex(0x3A1714, 0.95f), Hex(0x5A211C), Hex(0xF2C9C2), Danger),
                ButtonKind.Tab => (Chosen, Hex(0x47331F), GoldLit, Gold),
                _ => (InkLight, InkHover, Text, Line),
            };
            Frame(image.transform, frame);
            var label = Label(text, image.transform, size, ink, TextAnchor.MiddleCenter, kind == ButtonKind.Plain ? FontStyle.Normal : FontStyle.Bold);
            label.rectTransform.Fill(4);
            var hover = image.gameObject.AddComponent<Hover>();
            hover.Set(image, rest, lit, click);
            image.Size(52);

            return hover;
        }

        /// <summary>A row: a name on the left, and whatever `fill` puts on the right.</summary>
        public static RectTransform Row(string name, Transform parent, float height = 56)
        {
            var row = Rect(name, parent);
            row.Stack<HorizontalLayoutGroup>(16, null, TextAnchor.MiddleLeft);
            row.Size(height);
            var label = Label(name, row, 20, Text);
            label.Size(-1, 300, 0);

            return row;
        }

        /// <summary>A row of choices, the chosen one gold on brown; `pick` gets the index chosen.</summary>
        public static void Choices(Transform parent, IList<string> options, int chosen, Action<int> pick, int size = 18)
        {
            var row = Rect("Choices", parent);
            row.Stack<HorizontalLayoutGroup>(6, null, TextAnchor.MiddleLeft).childForceExpandWidth = true;
            row.Size(48, -1, 1);

            for (var i = 0; i < options.Count; i++)
            {
                var index = i;
                var button = Button(options[i], row, () => pick(index), i == chosen ? ButtonKind.Tab : ButtonKind.Plain, size);
                button.Size(46, -1, 1);
            }
        }

        /// <summary>A slider from min to max, its value shown beside it.</summary>
        public static Slider Slider(Transform parent, float min, float max, float value, Func<float, string> show, Action<float> change)
        {
            var holder = Rect("Slider", parent);
            holder.Size(48, -1, 1);
            var track = Image("Track", holder, InkLight, Rounded);
            track.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), 0, -4, 90, -4);
            var fillArea = Rect("Fill Area", holder).Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), 0, -4, 90, -4);
            var fill = Image("Fill", fillArea, Gold, Rounded);
            fill.rectTransform.Fill();
            var handleArea = Rect("Handle Area", holder).Place(new Vector2(0, 0), new Vector2(1, 1), 10, 0, 100, 0);
            var handle = Image("Handle", handleArea, GoldLit, Chamfer);
            handle.rectTransform.sizeDelta = new Vector2(18, 0);
            handle.rectTransform.anchorMin = new Vector2(0, 0.22f);
            handle.rectTransform.anchorMax = new Vector2(0, 0.78f);
            handle.raycastTarget = true;
            var number = Label(show(value), holder, 20, Text, TextAnchor.MiddleRight);
            number.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 1), -80, 0, 0, 0);

            var slider = holder.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.onValueChanged.AddListener(v =>
            {
                number.text = show(v);
                change(v);
            });

            return slider;
        }

        /// <summary>A one-line text field (the hero's name), with a hint while empty.</summary>
        public static InputField Field(Transform parent, string value, string hint, int limit, Action<string> change)
        {
            var box = Image("Field", parent, InkLight, Chamfer);
            box.raycastTarget = true;
            Frame(box.transform, LineLit);
            box.Size(52, -1, 1);
            var text = Label("", box.transform, 22, Text);
            text.rectTransform.Fill(14);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var placeholder = Label(hint, box.transform, 20, new Color(Muted.r, Muted.g, Muted.b, 0.6f), TextAnchor.MiddleLeft, FontStyle.Italic);
            placeholder.rectTransform.Fill(14);

            var field = box.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = limit;
            field.lineType = InputField.LineType.SingleLine;
            field.targetGraphic = box;
            field.caretColor = GoldLit;
            field.selectionColor = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
            field.text = value ?? "";
            field.onValueChanged.AddListener(v => change(v));

            return field;
        }

        /// <summary>
        /// An item cell (the design's corner-cut cells): its rim in a rank's or kind's colour,
        /// an icon in the middle, a count at the foot. Clicking it calls `click`.
        /// </summary>
        public static Cell Cell(Transform parent, float size, Action click = null)
        {
            var back = Image("Cell", parent, Hex(0x0E0D0C, 0.95f), Chamfer);
            back.raycastTarget = click != null;
            back.rectTransform.sizeDelta = new Vector2(size, size);
            var cell = back.gameObject.AddComponent<Cell>();
            cell.Build(back, size, click);

            return cell;
        }
    }

    public enum ButtonKind
    {
        Plain,
        /// <summary>The main action (cinnabar).</summary>
        Gold,
        Danger,
        /// <summary>A chosen tab or filter.</summary>
        Tab,
    }

    /// <summary>A clickable image that eases between its resting and lit colours (in real time, so it works while paused).</summary>
    public class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        Image image;
        Color rest;
        Color lit;
        Action click;
        bool over;

        public void Set(Image target, Color resting, Color lighted, Action onClick)
        {
            image = target;
            rest = resting;
            lit = lighted;
            click = onClick;
            image.color = rest;
        }

        public void OnPointerEnter(PointerEventData _) => over = true;
        public void OnPointerExit(PointerEventData _) => over = false;

        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left)
            {
                click?.Invoke();
            }
        }

        void Update()
        {
            if (image != null)
            {
                image.color = Color.Lerp(image.color, over ? lit : rest, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
            }
        }
    }

    /// <summary>An item cell: rim, icon, count, a corner mark (rank), a badge (the vital Gu's heart), a dot (hunger), a shade (rest after use).</summary>
    public class Cell : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Image Back;
        public Image Rim;
        public Image Mark;
        public Text Count;
        public Text Corner;
        public Text Key;
        public Image Badge;
        public Image Dot;
        public Image Shade;
        Action click;
        bool over;
        Color rim;

        public void Build(Image back, float size, Action onClick)
        {
            Back = back;
            click = onClick;
            Rim = UiKit.Frame(back.transform, UiKit.Line);
            Shade = UiKit.Image("Rest", back.transform, new Color(0, 0, 0, 0.62f), UiKit.Chamfer);
            Shade.rectTransform.Place(Vector2.zero, new Vector2(1, 0));
            Mark = UiKit.Icon(null, back.transform, size * 0.56f, UiKit.Text);
            Mark.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size * 0.56f);
            Count = UiKit.Label("", back.transform, Mathf.RoundToInt(Mathf.Clamp(size * 0.24f, 13, 18)), UiKit.Text, TextAnchor.LowerRight, FontStyle.Bold).Shadowed();
            Count.rectTransform.Fill(5);
            Corner = UiKit.Label("", back.transform, Mathf.RoundToInt(Mathf.Clamp(size * 0.2f, 11, 16)), UiKit.Essence, TextAnchor.UpperRight, FontStyle.Bold);
            Corner.rectTransform.Fill(5);
            Badge = UiKit.Icon("heart", back.transform, Mathf.Clamp(size * 0.22f, 11, 18), UiKit.Danger);
            Badge.rectTransform.At(Vector2.zero, new Vector2(Mathf.Clamp(size * 0.22f, 11, 18) * 0.5f + 5, Mathf.Clamp(size * 0.22f, 11, 18) * 0.5f + 5), Vector2.one * Mathf.Clamp(size * 0.22f, 11, 18));
            Badge.enabled = false;
            Key = UiKit.Label("", back.transform, 14, UiKit.Muted, TextAnchor.UpperLeft, FontStyle.Bold);
            Key.rectTransform.Fill(5);
            Dot = UiKit.Image("Dot", back.transform, UiKit.Hunger, UiKit.Disc);
            Dot.rectTransform.At(new Vector2(1, 1), new Vector2(-9, -9), new Vector2(9, 9));
            Dot.enabled = false;
            SetRim(UiKit.Line);
        }

        public void SetRim(Color colour)
        {
            rim = colour;
            Rim.color = colour;
        }

        /// <summary>The icon in the middle (null: none).</summary>
        public void SetIcon(string name)
        {
            Mark.sprite = Icons.Get(name);
            Mark.enabled = Mark.sprite != null;
        }

        /// <summary>How far through its rest it still is (0 ready … 1 just used): a shade rising from the foot.</summary>
        public void SetRest(float share) => Shade.rectTransform.anchorMax = new Vector2(1, Mathf.Clamp01(share));

        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left)
            {
                click?.Invoke();
            }
        }

        public void OnPointerEnter(PointerEventData _) => over = click != null;

        public void OnPointerExit(PointerEventData _) => over = false;

        void Update()
        {
            if (Rim != null && click != null)
            {
                Rim.color = over ? UiKit.GoldLit : rim;
            }
        }
    }

    /// <summary>An entry of the main menu: big text; pointed at, a gold bar grows beside it and the text slides right and turns gold.</summary>
    public class MenuEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        Text title;
        Text note;
        Image mark;
        Image bar;
        Image glow;
        Action click;
        float lit;
        bool over;

        public void Build(string text, string detail, Action onClick, string icon)
        {
            click = onClick;
            var rect = (RectTransform)transform;
            glow = UiKit.Image("Glow", rect, new Color(1, 1, 1, 0), UiKit.SoftEdge);
            glow.rectTransform.Fill();
            glow.raycastTarget = true;
            bar = UiKit.Image("Bar", rect, UiKit.GoldLit);
            bar.rectTransform.Place(new Vector2(0, 0.2f), new Vector2(0, 0.8f), 0, 0, 0, 0);
            bar.rectTransform.sizeDelta = new Vector2(0, bar.rectTransform.sizeDelta.y);
            title = UiKit.Label(UiKit.Spaced(text), rect, 28, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed();
            title.rectTransform.Place(new Vector2(0, detail != null ? 0.38f : 0), Vector2.one, 24, 0, 60, 0);

            if (detail != null)
            {
                note = UiKit.Label(detail, rect, 17, UiKit.Muted).Shadowed();
                note.rectTransform.Place(Vector2.zero, new Vector2(1, 0.42f), 26, 0, 60, 0);
            }

            if (icon != null)
            {
                mark = UiKit.Icon(icon, rect, 30, UiKit.Hex(0x3A3024));
                mark.rectTransform.At(new Vector2(1, 0.5f), new Vector2(-26, 0), new Vector2(30, 30));
            }
        }

        public void OnPointerEnter(PointerEventData _) => over = true;
        public void OnPointerExit(PointerEventData _) => over = false;

        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left)
            {
                click?.Invoke();
            }
        }

        void Update()
        {
            lit = Mathf.MoveTowards(lit, over ? 1 : 0, Time.unscaledDeltaTime * 6);
            var ease = lit * lit * (3 - 2 * lit);
            bar.rectTransform.sizeDelta = new Vector2(4 * ease, bar.rectTransform.sizeDelta.y);
            title.rectTransform.offsetMin = new Vector2(24 + 14 * ease, title.rectTransform.offsetMin.y);
            title.color = Color.Lerp(UiKit.Text, UiKit.GoldLit, ease);
            glow.color = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.1f * ease);

            if (mark != null)
            {
                mark.color = Color.Lerp(UiKit.Hex(0x3A3024), UiKit.Gold, ease);
            }
        }
    }
}
