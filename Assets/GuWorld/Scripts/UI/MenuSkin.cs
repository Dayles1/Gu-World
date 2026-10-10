using UnityEngine;

namespace GuWorld.UI
{
    /// <summary>
    /// The look of GU World's menus (drawn with Unity's immediate GUI): dark,
    /// see-through panels, quiet buttons, and a warm gold for what is chosen.
    /// Made once, on the first frame that draws.
    /// </summary>
    public class MenuSkin
    {
        static readonly Color Ink = new Color(0.07f, 0.075f, 0.085f, 0.86f);
        static readonly Color Button = new Color(1, 1, 1, 0.07f);
        static readonly Color ButtonHover = new Color(1, 1, 1, 0.14f);
        static readonly Color ButtonDown = new Color(1, 1, 1, 0.2f);
        public static readonly Color Gold = new Color(0.86f, 0.68f, 0.36f);
        static readonly Color GoldDim = new Color(0.86f, 0.68f, 0.36f, 0.85f);
        static readonly Color Danger = new Color(0.72f, 0.24f, 0.2f, 0.9f);
        static readonly Color Text = new Color(0.93f, 0.91f, 0.87f);
        static readonly Color Muted = new Color(0.93f, 0.91f, 0.87f, 0.6f);

        public readonly GUIStyle Panel, Title, Heading, Label, Small, Choice, Chosen, Primary, Warning, Hud;

        public MenuSkin()
        {
            Panel = new GUIStyle { normal = { background = Fill(Ink) }, padding = new RectOffset(28, 28, 24, 24) };
            Title = new GUIStyle { fontSize = 30, fontStyle = FontStyle.Bold, normal = { textColor = Gold }, margin = new RectOffset(0, 0, 0, 4) };
            Heading = new GUIStyle { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Muted }, margin = new RectOffset(0, 0, 14, 6) };
            Label = new GUIStyle { fontSize = 15, richText = true, wordWrap = true, normal = { textColor = Text }, margin = new RectOffset(0, 0, 3, 3) };
            Small = new GUIStyle(Label) { fontSize = 13, normal = { textColor = Muted } };
            Hud = new GUIStyle { fontSize = 15, richText = true, normal = { textColor = Text } };

            Choice = ButtonStyle(Button, ButtonHover, ButtonDown, Text);
            Chosen = ButtonStyle(GoldDim, Gold, Gold, new Color(0.1f, 0.08f, 0.05f));
            Chosen.fontStyle = FontStyle.Bold;
            Primary = ButtonStyle(GoldDim, Gold, Gold, new Color(0.1f, 0.08f, 0.05f));
            Primary.fontSize = 17;
            Primary.fontStyle = FontStyle.Bold;
            Primary.fixedHeight = 46;
            Warning = ButtonStyle(Danger, new Color(0.82f, 0.3f, 0.25f), new Color(0.9f, 0.35f, 0.3f), Text);
            Warning.fontStyle = FontStyle.Bold;
        }

        static GUIStyle ButtonStyle(Color normal, Color hover, Color down, Color text) => new GUIStyle
        {
            normal = { background = Fill(normal), textColor = text },
            hover = { background = Fill(hover), textColor = text },
            active = { background = Fill(down), textColor = text },
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fixedHeight = 36,
            margin = new RectOffset(3, 3, 3, 3),
            padding = new RectOffset(10, 10, 6, 6),
        };

        static Texture2D Fill(Color colour)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            texture.SetPixel(0, 0, colour);
            texture.Apply();

            return texture;
        }

        /// <summary>Text with a dark shadow, readable over the bright sky.</summary>
        public static void Shadowed(Rect area, string text, GUIStyle style)
        {
            var colour = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.75f);
            GUI.Label(new Rect(area.x + 1, area.y + 1, area.width, area.height), text, style);
            style.normal.textColor = colour;
            GUI.Label(area, text, style);
        }
    }
}
