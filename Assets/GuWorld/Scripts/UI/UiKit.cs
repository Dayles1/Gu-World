using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// The parts GU World's menus are built from, in code (uGUI): a canvas
    /// scaled from 1920×1080, rounded panels, text, menu entries that light up
    /// and slide when pointed at, buttons, sliders and rows of choices. One
    /// look throughout — dark ink, warm gold, quiet greys.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.055f, 0.06f, 0.07f, 0.92f);
        public static readonly Color InkLight = new Color(1, 1, 1, 0.06f);
        public static readonly Color InkHover = new Color(1, 1, 1, 0.12f);
        public static readonly Color Gold = new Color(0.86f, 0.68f, 0.36f);
        public static readonly Color GoldDeep = new Color(0.62f, 0.45f, 0.2f);
        public static readonly Color Text = new Color(0.94f, 0.92f, 0.88f);
        public static readonly Color Muted = new Color(0.94f, 0.92f, 0.88f, 0.55f);
        public static readonly Color Danger = new Color(0.78f, 0.27f, 0.22f);
        public static readonly Color OnGold = new Color(0.1f, 0.075f, 0.04f);

        static Font font;
        static Font glyphs;
        static Sprite rounded;
        static Sprite softEdge;

        /// <summary>The text font (Unity's own).</summary>
        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>A system font with Chinese characters, for the emblem (null where there is none).</summary>
        public static Font Glyphs
        {
            get
            {
                if (glyphs == null)
                {
                    glyphs = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimSun", "Noto Sans CJK SC", "PingFang SC", "Arial Unicode MS" }, 64);
                }

                return glyphs;
            }
        }

        /// <summary>A rounded rectangle, sliced so it stretches to any size.</summary>
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(64, 14);

        /// <summary>White fading to clear from left to right, for darkening a side of the screen.</summary>
        public static Sprite SoftEdge => softEdge != null ? softEdge : softEdge = MakeFade();

        static Sprite MakeRounded(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    var dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    var alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
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

        /// <summary>Fills its parent (with an inset).</summary>
        public static RectTransform Fill(this RectTransform rect, float inset = 0) =>
            rect.Place(Vector2.zero, Vector2.one, inset, inset, inset, inset);

        public static Image Image(string name, Transform parent, Color colour, Sprite sprite = null)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = colour;
            image.sprite = sprite;
            image.type = sprite == Rounded ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = false;

            return image;
        }

        public static Image Panel(string name, Transform parent, Color? colour = null) => Image(name, parent, colour ?? Ink, Rounded);

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

        /// <summary>A soft dark shadow under text that sits on the world.</summary>
        public static T Shadowed<T>(this T graphic) where T : Graphic
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.7f);
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

        /// <summary>A big entry of the main menu: text that lights up gold and slides when pointed at.</summary>
        public static MenuEntry Entry(string text, string detail, Transform parent, Action click)
        {
            var rect = Rect(text, parent);
            var entry = rect.gameObject.AddComponent<MenuEntry>();
            entry.Build(text, detail, click);
            rect.Size(detail != null ? 74 : 60);

            return entry;
        }

        /// <summary>A button: plain, gold (the main action) or red (one that cannot be undone).</summary>
        public static Hover Button(string text, Transform parent, Action click, ButtonKind kind = ButtonKind.Plain, int size = 20)
        {
            var image = Image(text, parent, Color.white, Rounded);
            image.raycastTarget = true;
            var label = Label(text, image.transform, size, kind == ButtonKind.Plain ? Text : kind == ButtonKind.Gold ? OnGold : Text, TextAnchor.MiddleCenter, kind == ButtonKind.Plain ? FontStyle.Normal : FontStyle.Bold);
            label.rectTransform.Fill();
            var hover = image.gameObject.AddComponent<Hover>();
            var (rest, lit) = kind switch
            {
                ButtonKind.Gold => (Gold * new Color(1, 1, 1, 0.92f), Gold),
                ButtonKind.Danger => (Danger * new Color(1, 1, 1, 0.85f), Danger),
                _ => (InkLight, InkHover),
            };
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

        /// <summary>A row of choices, the chosen one gold; `pick` gets the index chosen.</summary>
        public static void Choices(Transform parent, IList<string> options, int chosen, Action<int> pick, int size = 18)
        {
            var row = Rect("Choices", parent);
            row.Stack<HorizontalLayoutGroup>(6, null, TextAnchor.MiddleLeft).childForceExpandWidth = true;
            row.Size(48, -1, 1);

            for (var i = 0; i < options.Count; i++)
            {
                var index = i;
                var button = Button(options[i], row, () => pick(index), i == chosen ? ButtonKind.Gold : ButtonKind.Plain, size);
                button.Size(46, -1, 1);
            }
        }

        /// <summary>A slider from min to max, its value shown beside it.</summary>
        public static Slider Slider(Transform parent, float min, float max, float value, Func<float, string> show, Action<float> change)
        {
            var holder = Rect("Slider", parent);
            holder.Size(48, -1, 1);
            var track = Image("Track", holder, InkLight, Rounded);
            track.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), 0, -5, 90, -5);
            var fillArea = Rect("Fill Area", holder).Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), 0, -5, 90, -5);
            var fill = Image("Fill", fillArea, Gold, Rounded);
            fill.rectTransform.Fill();
            var handleArea = Rect("Handle Area", holder).Place(new Vector2(0, 0), new Vector2(1, 1), 10, 0, 100, 0);
            var handle = Image("Handle", handleArea, Text, Rounded);
            handle.rectTransform.sizeDelta = new Vector2(22, 0);
            handle.rectTransform.anchorMin = new Vector2(0, 0.2f);
            handle.rectTransform.anchorMax = new Vector2(0, 0.8f);
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
    }

    public enum ButtonKind
    {
        Plain,
        Gold,
        Danger,
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

    /// <summary>An entry of the main menu: big text; pointed at, a gold bar grows beside it and the text slides right and turns gold.</summary>
    public class MenuEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        Text title;
        Text note;
        Image bar;
        Image glow;
        Action click;
        float lit;
        bool over;

        public void Build(string text, string detail, Action onClick)
        {
            click = onClick;
            var rect = (RectTransform)transform;
            glow = UiKit.Image("Glow", rect, new Color(1, 1, 1, 0), UiKit.SoftEdge);
            glow.rectTransform.Fill();
            glow.raycastTarget = true;
            bar = UiKit.Image("Bar", rect, UiKit.Gold);
            bar.rectTransform.Place(new Vector2(0, 0.15f), new Vector2(0, 0.85f), 0, 0, 0, 0);
            bar.rectTransform.sizeDelta = new Vector2(0, bar.rectTransform.sizeDelta.y);
            title = UiKit.Label(text.ToUpperInvariant(), rect, 34, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed();
            title.rectTransform.Place(new Vector2(0, detail != null ? 0.38f : 0), Vector2.one, 24, 0, 0, 0);

            if (detail != null)
            {
                note = UiKit.Label(detail, rect, 17, UiKit.Muted).Shadowed();
                note.rectTransform.Place(Vector2.zero, new Vector2(1, 0.42f), 26, 0, 0, 0);
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
            bar.rectTransform.sizeDelta = new Vector2(5 * ease, bar.rectTransform.sizeDelta.y);
            title.rectTransform.offsetMin = new Vector2(24 + 14 * ease, title.rectTransform.offsetMin.y);
            title.color = Color.Lerp(UiKit.Text, UiKit.Gold, ease);
            glow.color = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.16f * ease);
        }
    }
}
