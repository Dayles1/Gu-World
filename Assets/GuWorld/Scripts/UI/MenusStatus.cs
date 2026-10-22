using System;
using GuWorld.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// The status window (O), the way a "system" shows one: the hero's name, the body's level and title, health,
    /// stamina and the sea; then each trait of the body — its level, how far toward the next, what it gives now and
    /// what trains it.
    /// </summary>
    public partial class Menus
    {
        /// <summary>The system's own colours: a cold blue over the ink.</summary>
        static readonly Color SystemBlue = UiKit.Hex(0x6FB8FF);
        static readonly Color SystemDeep = UiKit.Hex(0x0B1A2B, 0.92f);

        void StatusPage()
        {
            var body = BookFrame("star", "Статус", Page.Status);
            var p = host.Physique;
            var gu = host.Cultivator;

            // The window, in the middle.
            var window = UiKit.Image("Window", body, SystemDeep, UiKit.Chamfer);
            window.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180, 820));
            UiKit.Frame(window.rectTransform, SystemBlue);
            var inside = UiKit.Rect("Inside", window.transform).Fill(28);
            inside.Stack<VerticalLayoutGroup>(10, new RectOffset(18, 18, 10, 10));

            UiKit.Label("[ СТАТУС ]", inside, 30, SystemBlue, TextAnchor.MiddleCenter, FontStyle.Bold).Size(46);
            UiKit.Image("Rule", inside, new Color(SystemBlue.r, SystemBlue.g, SystemBlue.b, 0.5f)).Size(2);

            var head = UiKit.Rect("Head", inside);
            head.Size(96);
            head.Stack<HorizontalLayoutGroup>(24, null, TextAnchor.MiddleLeft).childForceExpandWidth = true;
            var left = UiKit.Rect("Who", head);
            left.Stack<VerticalLayoutGroup>(2);
            UiKit.Label($"<b>Имя:</b> {host.Look.FullName}", left, 20, UiKit.Text).Size(30);
            UiKit.Label($"<b>Уровень тела:</b> {p.Overall}", left, 20, UiKit.Text).Size(30);
            UiKit.Label($"<b>Титул:</b> {p.Title}", left, 20, SystemBlue).Size(30);
            var right = UiKit.Rect("Realm", head);
            right.Stack<VerticalLayoutGroup>(2);
            var a = gu.Aperture;
            UiKit.Label($"<b>Ранг:</b> {a.Rank} · {GuWorld.Cultivation.Aperture.Name(a.Stage)}", right, 20, UiKit.Text).Size(30);
            UiKit.Label($"<b>Талант:</b> {a.TalentName}", right, 20, UiKit.Text).Size(30);
            UiKit.Label($"<b>Шанс прорыва:</b> {gu.BreakChance * 100:0.##}%", right, 20, UiKit.Text).Size(30);

            // Health, stamina, the sea: bars.
            Gauge(inside, "Здоровье", host.Health.Current, host.Health.Max, UiKit.Life);
            Gauge(inside, "Выносливость", p.Stamina, p.MaxStamina, UiKit.GoldLit);
            Gauge(inside, "Море эссенции", a.Essence, a.Cap, GuWorld.Cultivation.Aperture.RankColour(a.Rank));
            UiKit.Image("Rule", inside, new Color(SystemBlue.r, SystemBlue.g, SystemBlue.b, 0.5f)).Size(2);

            foreach (Trait trait in Enum.GetValues(typeof(Trait)))
            {
                TraitRow(inside, p, trait);
            }

            UiKit.Image("Rule", inside, new Color(SystemBlue.r, SystemBlue.g, SystemBlue.b, 0.5f)).Size(2);
            var clans = UiKit.Rect("Clans", inside);
            clans.Size(30);
            clans.Stack<HorizontalLayoutGroup>(30, null, TextAnchor.MiddleCenter).childForceExpandWidth = false;

            foreach (var clan in GuWorld.Creatures.Clans.Rivals)
            {
                var h = GuWorld.Creatures.Clans.Hostility(clan);
                var colour = h >= GuWorld.Creatures.Clans.War ? "#C2413A" : h >= GuWorld.Creatures.Clans.OnSight ? "#E0A13A" : "#A69A86";
                UiKit.Label($"<b>{GuWorld.Creatures.MasterKinds.ClanName(clan)}:</b> <color={colour}>{GuWorld.Creatures.Clans.Standing(clan)} · {h:0}</color>",
                    clans, 17, UiKit.Text, TextAnchor.MiddleCenter).Size(30, 340, 0);
            }

            UiKit.Label("Характеристики растут от дела: каждая следующая единица требует больше. Пустая выносливость — ни бега, ни переката, ни удара.",
                inside, 14, UiKit.Muted, TextAnchor.MiddleCenter).Size(26);
        }

        /// <summary>A bar with its name and numbers (the status window).</summary>
        static void Gauge(Transform parent, string name, float now, float most, Color colour)
        {
            var row = UiKit.Rect(name, parent);
            row.Size(30);
            var label = UiKit.Label(name, row, 17, UiKit.Text);
            label.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -200, 0);
            var back = UiKit.Image("Back", row, UiKit.InkLight, UiKit.Chamfer);
            back.rectTransform.Place(Vector2.zero, Vector2.one, 210, 7, 170, 7);
            var fill = UiKit.Image("Fill", back.transform, colour);
            fill.rectTransform.Place(Vector2.zero, new Vector2(most > 0 ? Mathf.Clamp01(now / most) : 0, 1), 2, 2, 2, 2);
            var numbers = UiKit.Label($"{Mathf.Ceil(now):0} / {most:0}", row, 17, UiKit.Text, TextAnchor.MiddleRight, FontStyle.Bold);
            numbers.rectTransform.Place(new Vector2(1, 0), Vector2.one, -160, 0, 0, 0);
        }

        /// <summary>A trait of the body: its short name and level, how far toward the next, what it gives, what trains it.</summary>
        static void TraitRow(Transform parent, Physique p, Trait trait)
        {
            var row = UiKit.Rect(trait.ToString(), parent);
            row.Size(60);
            var tag = UiKit.Label($"<b>{Physique.Short(trait)}</b>", row, 22, SystemBlue, TextAnchor.MiddleLeft);
            tag.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -70, 0);
            var level = UiKit.Label($"<b>{p.Level(trait)}</b>", row, 28, UiKit.Text, TextAnchor.MiddleLeft);
            level.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 72, 0, -140, 0);
            var name = UiKit.Label($"<b>{Physique.Name(trait)}</b>  <color=#A69A86>· {Physique.HowTrained(trait)}</color>", row, 16, UiKit.Text);
            name.rectTransform.Place(Vector2.zero, Vector2.one, 150, 30, 0, 2);
            var gives = UiKit.Label(p.Gives(trait), row, 15, UiKit.GoldLit);
            gives.rectTransform.Place(Vector2.zero, Vector2.one, 150, 4, 300, 30);
            var back = UiKit.Image("Back", row, UiKit.InkLight);
            back.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0.5f), -280, 8, 60, 0);
            var fill = UiKit.Image("Fill", back.transform, SystemBlue);
            fill.rectTransform.Place(Vector2.zero, new Vector2(p.Progress(trait), 1));
            var share = UiKit.Label($"{p.Progress(trait) * 100:0}%", row, 14, UiKit.Muted, TextAnchor.MiddleRight);
            share.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0.5f), -54, 0, 0, 0);
        }
    }
}
