using System;
using System.Collections.Generic;
using GuWorld.Combat;
using GuWorld.Render;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>Passes a drag over a picture on (the body page turns its figure by it).</summary>
    public class Dragger : MonoBehaviour, IDragHandler
    {
        public Action<Vector2> Dragged;

        public void OnDrag(PointerEventData data) => Dragged?.Invoke(data.delta);
    }

    /// <summary>
    /// The body page (B): the hero's figure with the body's parts in colour — where
    /// a blow is deadly and where it only hurts — and beside it every part with the
    /// share of health a blow there takes, the vital ones marked, and how the
    /// strength of the blow against the toughness of the body changes it all.
    /// </summary>
    public partial class Menus
    {
        BodyPart? bodyChosen;
        float bodyYaw;

        void BodyPage()
        {
            var body = BookFrame("body", "Тело", Page.Body);

            // The figure.
            var left = UiKit.Panel("Figure", body);
            left.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -600, 0);
            var frame = UiKit.Rect("Frame", left.transform).Place(new Vector2(0, 0), new Vector2(1, 1), 24, 92, 24, 24);
            var studio = BodyStudio.Open(host.Parts, host.Look);
            studio.Turn(bodyYaw);
            studio.Choose(bodyChosen);
            var picture = UiKit.Rect("Picture", frame).gameObject.AddComponent<RawImage>();
            picture.texture = studio.Texture;
            picture.rectTransform.Fill();
            picture.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            picture.GetComponent<AspectRatioFitter>().aspectRatio = 0.75f;
            picture.gameObject.AddComponent<Dragger>().Dragged = delta =>
            {
                bodyYaw -= delta.x * 0.5f;
                studio.Turn(bodyYaw);
            };
            UiKit.Frame(frame, UiKit.GoldDeep);

            var views = UiKit.Rect("Views", left.transform).Place(new Vector2(0, 0), new Vector2(1, 0), 24, 24, 24, -76);
            views.Stack<HorizontalLayoutGroup>(8).childForceExpandWidth = true;

            foreach (var (label, yaw) in new[] { ("Спереди", 0f), ("Сбоку", 90f), ("Сзади", 180f) })
            {
                var y = yaw;
                UiKit.Button(label, views, () =>
                {
                    bodyYaw = y;
                    studio.Turn(y);
                }, ButtonKind.Plain, 16).Size(48, -1, 1);
            }

            var hint = UiKit.Label("Потяните мышью, чтобы повернуть", frame, 14, UiKit.Muted, TextAnchor.LowerCenter).Shadowed();
            hint.rectTransform.Place(Vector2.zero, new Vector2(1, 0), 0, 8, 0, -30);

            // The parts.
            var middle = UiKit.Panel("Parts", body);
            middle.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 624, 0, -(624 + 760), 0);
            var list = UiKit.Rect("List", middle.transform).Fill();
            list.Stack<VerticalLayoutGroup>(3, new RectOffset(20, 20, 18, 16));
            UiKit.Caps("Части тела · доля здоровья за чистый удар равной силы", list, 13).Size(26);
            var seen = new HashSet<string>();

            foreach (var info in Wounds.Parts)
            {
                if (!seen.Add(info.Name))
                {
                    continue;
                }

                PartRow(list, info);
            }

            // How it is reckoned.
            var right = UiKit.Panel("Rules", body);
            right.rectTransform.Place(Vector2.zero, Vector2.one, 624 + 784, 0, 0, 0);
            var rules = UiKit.Rect("Rules", right.transform).Fill();
            rules.Stack<VerticalLayoutGroup>(8, new RectOffset(24, 24, 20, 18));

            if (bodyChosen.HasValue)
            {
                var info = Wounds.Of(bodyChosen.Value);
                UiKit.Label(info.Name, rules, 28, info.Colour, TextAnchor.MiddleLeft, FontStyle.Bold).Size(38);
                UiKit.Caps(info.Critical ? "Уязвимое место" : "Обычная рана", rules, 13, info.Critical ? UiKit.Danger : UiKit.Muted).Size(22);
                UiKit.Label($"{info.About}\nЧистый удар равной силы: {info.Min * 100:0}–{info.Max * 100:0}% здоровья.", rules, 17).Size(96);
                UiKit.Image("Rule", rules, UiKit.Line).Size(2);
            }

            UiKit.Label("Сила против крепости", rules, 22, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Size(32);
            UiKit.Label("Доли выше — для удара, равного крепости тела. Слабый удар по сильному телу теряет почти всё, " +
                        "а ниже трети его крепости не оставляет и царапины. Удар намного сильнее тела берёт больше своей доли.", rules, 16, UiKit.Muted).Size(110);

            foreach (var ratio in new[] { 0.25f, 0.5f, 0.75f, 1f, 2f, 4f })
            {
                var scale = Wounds.Scale(ratio, 1);
                var row = UiKit.Rect("Ratio", rules);
                row.Size(30);
                row.Stack<HorizontalLayoutGroup>(10, null, TextAnchor.MiddleLeft).childForceExpandWidth = false;
                UiKit.Label($"сила ×{ratio:0.##}", row, 16, UiKit.Text).Size(30, 96);
                var bar = UiKit.Rect("Bar", row);
                bar.Size(14, 140);
                UiKit.Image("Back", bar, UiKit.InkLight).rectTransform.Fill();
                var fill = UiKit.Image("Fill", bar, scale <= 0 ? UiKit.Muted : UiKit.Life);
                fill.rectTransform.Place(Vector2.zero, new Vector2(Mathf.Clamp01(scale / Wounds.MaxScale), 1));
                UiKit.Label(scale <= 0 ? "не пробивает" : $"×{scale:0.00}", row, 15, scale <= 0 ? UiKit.Muted : UiKit.GoldLit).Size(30, 120);
            }

            UiKit.Image("Rule", rules, UiKit.Line).Size(2);
            var power = GuWorld.Cultivation.GuBook.Moonlight.Power * (1 + 0.6f * (host.Cultivator.Aperture.Rank - 1));
            UiKit.Label($"<b>Ваша крепость:</b> {host.Health.Toughness:0}\n<b>Лунный клинок:</b> сила {power:0}, за пределом в {GuWorld.Cultivation.GuBook.Moonlight.Reach:0} м — вдвое слабее.\n" +
                        "Простые люди — около 45, ученики — 70, стражи-мастера Гу — 140, наставники — 220, старейшины — 300.", rules, 15).Size(110);
        }

        void PartRow(Transform parent, PartInfo info)
        {
            var part = info.Part;
            var chosen = bodyChosen.HasValue && Wounds.Of(bodyChosen.Value).Name == info.Name;
            var row = UiKit.Button("", parent, () =>
            {
                bodyChosen = chosen ? (BodyPart?)null : part;
                Show(Page.Body);
            }, chosen ? ButtonKind.Tab : ButtonKind.Plain, 16);
            row.Size(52);
            var inside = UiKit.Rect("Inside", row.transform).Fill();
            var chip = UiKit.Image("Colour", inside, info.Colour, UiKit.Disc);
            chip.rectTransform.At(new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(16, 16));
            chip.raycastTarget = false;
            var name = UiKit.Label($"<b>{info.Name}</b>" + (info.Critical ? "  <color=#C2413A>◆ уязвимо</color>" : ""), inside, 17, UiKit.Text);
            name.rectTransform.Place(Vector2.zero, Vector2.one, 42, 24, 150, 2);
            name.raycastTarget = false;
            var about = UiKit.Label(info.About, inside, 13, UiKit.Muted);
            about.rectTransform.Place(Vector2.zero, Vector2.one, 42, 3, 150, 26);
            about.raycastTarget = false;
            var share = UiKit.Label($"{info.Min * 100:0}–{info.Max * 100:0}%", inside, 19, info.Critical ? UiKit.Danger : UiKit.GoldLit, TextAnchor.MiddleRight, FontStyle.Bold);
            share.rectTransform.Place(new Vector2(1, 0), Vector2.one, -140, 0, 16, 0);
            share.raycastTarget = false;
        }
    }
}
