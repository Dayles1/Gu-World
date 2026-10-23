using System.Linq;
using GuWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>
    /// The builder's page (F9; WorldBuilder does the work): no dimming — the world is what one looks at. On the left
    /// the catalog by section (a click takes a model up to put down, or to stretch for stairs, walls, paths); along
    /// the top the tools, the brush's size and strength, the models' scale, the grid, saving; along the bottom what
    /// the keys do and what is chosen.
    /// </summary>
    public partial class Menus
    {
        string builderSection = "Дома";

        static readonly string[][] BuilderSectionRows =
        {
            new[] { "Дома", "Клан", "Крепость" },
            new[] { "Лестницы и тропы", "Декор", "Деревья" },
            new[] { "Внутри", "Гу", "Прочее" },
        };

        static readonly string[] BuilderTools = { "Выбор", "Поставить", "Протянуть", "Поднять", "Опустить", "Выровнять", "Сгладить" };

        /// <summary>Copies the builder's word of what is going on into the page's line, every frame.</summary>
        class BuilderStatusFeed : MonoBehaviour
        {
            public Text Line;

            void Update()
            {
                if (WorldBuilder.Current != null && Line != null)
                {
                    Line.text = WorldBuilder.Current.Status;
                }
            }
        }

        void BuilderPage()
        {
            var builder = WorldBuilder.Current;

            if (builder == null || !builder.Ready)
            {
                Notify("Строитель работает на горе Цинмао, когда мир загружен");
                host.Resume();

                return;
            }

            builder.Begin();
            var catalog = BuildCatalog.Load();

            // The catalog, by section.
            var left = UiKit.Panel("Models", pageRoot);
            left.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 16, 92, -336, 16);
            var column = UiKit.Rect("Inside", left.transform).Fill();
            column.Stack<VerticalLayoutGroup>(6, new RectOffset(14, 14, 12, 12));
            UiKit.Label("СТРОИТЕЛЬ", column, 22, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).Size(30);

            foreach (var row in BuilderSectionRows)
            {
                UiKit.Choices(column, row, System.Array.IndexOf(row, builderSection), i =>
                {
                    builderSection = row[i];
                    Show(Page.Builder);
                }, 13);
            }

            var listHolder = UiKit.Rect("List", column);
            listHolder.Size(-1, -1, 1).flexibleHeight = 1;
            var scroll = listHolder.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            var viewport = UiKit.Rect("Viewport", listHolder).Fill();
            viewport.gameObject.AddComponent<RectMask2D>();
            UiKit.Image("Catch", viewport, new Color(0, 0, 0, 0.01f)).rectTransform.Fill();
            var content = UiKit.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            content.Stack<VerticalLayoutGroup>(4);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            foreach (var entry in catalog.Entries.Where(e => e.Section == builderSection))
            {
                var chosen = builder.Chosen == entry && builder.CurrentTool != WorldBuilder.Tool.Select;
                var label = entry.Stretch || entry.Repeat ? entry.Label + "  ↔" : entry.Label;
                UiKit.Button(label, content, () =>
                {
                    builder.Choose(entry.Stretch || entry.Repeat ? WorldBuilder.Tool.Stretch : WorldBuilder.Tool.Place, entry);
                    Show(Page.Builder);
                }, chosen ? ButtonKind.Tab : ButtonKind.Plain, 15).Size(34);
            }

            // The tools along the top.
            var top = UiKit.Panel("Tools", pageRoot);
            top.rectTransform.Place(new Vector2(0, 1), Vector2.one, 352, -186, 16, 16);
            var tools = UiKit.Rect("Inside", top.transform).Fill();
            tools.Stack<VerticalLayoutGroup>(6, new RectOffset(16, 16, 10, 10));
            UiKit.Choices(tools, BuilderTools, (int)builder.CurrentTool, i =>
            {
                builder.Choose((WorldBuilder.Tool)i);
                Show(Page.Builder);
            }, 15);

            var sliders = UiKit.Rect("Sliders", tools);
            sliders.Size(48);
            sliders.Stack<HorizontalLayoutGroup>(18, null, TextAnchor.MiddleLeft).childForceExpandWidth = true;
            UiKit.Label("Кисть", sliders, 15, UiKit.Muted).Size(-1, 52);
            UiKit.Slider(sliders, 1, 60, builder.Radius, v => $"{v:0} м", v => builder.Radius = v);
            UiKit.Label("Сила", sliders, 15, UiKit.Muted).Size(-1, 44);
            UiKit.Slider(sliders, 0.05f, 3, builder.Strength, v => $"{v:0.00}", v => builder.Strength = v);
            UiKit.Label("Размер", sliders, 15, UiKit.Muted).Size(-1, 60);
            UiKit.Slider(sliders, 0.2f, 4, builder.ScaleChoice, v => $"{v:0.00}", v => builder.ScaleChoice = v);

            var buttons = UiKit.Rect("Buttons", tools);
            buttons.Size(40);
            buttons.Stack<HorizontalLayoutGroup>(10, null, TextAnchor.MiddleLeft).childForceExpandWidth = false;
            UiKit.Button(builder.Snap ? "Сетка 1 м: вкл" : "Сетка 1 м: выкл", buttons, () =>
            {
                builder.Snap = !builder.Snap;
                Show(Page.Builder);
            }, builder.Snap ? ButtonKind.Tab : ButtonKind.Plain, 15).Size(36, 170);
            UiKit.Button("Сохранить", buttons, builder.Save, ButtonKind.Plain, 15).Size(36, 140);
            UiKit.Button("Герой сюда", buttons, () =>
            {
                var at = builder.GroundAhead();
                builder.End();
                host.Teleport(at);
                host.Resume();
            }, ButtonKind.Plain, 15).Size(36, 150);
            UiKit.Button("Вернуть построенное", buttons, () => Ask("Вернуть построенное?",
                "Все сдвинутые и убранные дома и вещи деревни встанут на свои места после перезапуска игры. То, что вы поставили сами, и земля останутся.",
                "Вернуть", () =>
                {
                    builder.ForgetChanges();
                    Show(Page.Builder);
                }, Page.Builder), ButtonKind.Plain, 15).Size(36, 230);
            UiKit.Button("Выйти · F9", buttons, () =>
            {
                builder.End();
                host.Resume();
            }, ButtonKind.Gold, 15).Size(36, 150);

            // What the keys do, and what is going on.
            var bottom = UiKit.Panel("Help", pageRoot);
            bottom.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), 16, 16, 16, -76);
            var help = UiKit.Rect("Inside", bottom.transform).Fill();
            help.Stack<VerticalLayoutGroup>(2, new RectOffset(16, 16, 6, 6));
            UiKit.Label("Камера: правая кнопка — смотреть · WASD — лететь · Space/C — вверх/вниз · Shift — быстрее · колесо — вперёд/назад · Esc — отменить / выйти",
                help, 14, UiKit.Muted).Size(24);
            var status = UiKit.Label(builder.Status, help, 16, UiKit.Text);
            status.Size(28);
            status.gameObject.AddComponent<BuilderStatusFeed>().Line = status;
        }
    }
}
