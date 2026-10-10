using System;
using System.Collections.Generic;
using GuWorld.Body;
using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>What the menus ask of the game.</summary>
    public interface IMenuHost
    {
        /// <summary>A saved game to go on with, and a line about it ("День 3 · 14:20 · Полигон").</summary>
        bool HasSave { get; }
        string SaveSummary { get; }
        Look Look { get; }
        bool CreatorFace { get; set; }
        Render.ThirdPersonCamera View { get; }

        void Continue();
        /// <summary>A new game: wipes the save and goes to making the hero.</summary>
        void NewGame();
        void Wear(Look look);
        /// <summary>The hero is made: play.</summary>
        void StartPlaying();
        void Resume();
        void BackToTitle();
        void ResetGame();
        void Quit();
        /// <summary>The conversation is over: back to playing.</summary>
        void EndTalk();
    }

    /// <summary>
    /// GU World's menus (uGUI, built in code): the main menu over the living
    /// world, the hero's making, the pause, the settings in tabs, the about
    /// page and confirmations — plus the HUD while playing. Changing between
    /// the big screens fades through black. Everything animates in real time,
    /// so it works while the world is paused.
    /// </summary>
    public class Menus : MonoBehaviour
    {
        public enum Page
        {
            /// <summary>Playing: only the HUD.</summary>
            None,
            Title,
            Creator,
            Pause,
            Settings,
            About,
            Confirm,
            /// <summary>Talking to someone: their words below, the questions beside them; the world goes on.</summary>
            Dialogue,
        }

        enum Tab
        {
            Game,
            Controls,
            Graphics,
            Sound,
        }

        public const string Version = "0.1.0";

        IMenuHost host;
        Canvas canvas;
        RectTransform pageRoot;
        RectTransform hud;
        Text hudLine;
        Text noticeLine;
        Text promptLine;
        float noticeUntil;

        // The conversation on screen.
        string speaker;
        string speakerTitle;
        string spoken;
        IList<(string text, Action act)> answers = new (string, Action)[0];
        Image fade;
        float fadeAlpha;
        float fadeTarget;
        Action afterFade;
        Tab tab;

        // What the confirmation says and does, and where "back" leads from the settings and the confirmation.
        string confirmTitle;
        string confirmText;
        string confirmButton;
        Action confirmAction;
        Page confirmBack;
        Page settingsBack;

        public Page Current { get; private set; } = (Page)(-1);

        /// <summary>The width of the hero's making panel, in screen pixels (the camera is dragged right of it).</summary>
        public float CreatorPanelWidth => 560f * canvas.scaleFactor;

        public static Menus Create(IMenuHost host)
        {
            var menus = new GameObject("Menus").AddComponent<Menus>();
            menus.host = host;
            menus.Build();

            return menus;
        }

        void Build()
        {
            canvas = UiKit.Canvas("Menu Canvas", 10);
            canvas.transform.SetParent(transform, false);

            hud = UiKit.Rect("HUD", canvas.transform).Fill();
            hudLine = UiKit.Label("", hud, 22).Shadowed();
            hudLine.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 28, -90, -900, 24);
            noticeLine = UiKit.Label("", hud, 22, UiKit.Text, TextAnchor.UpperRight).Shadowed();
            noticeLine.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), -620, -70, 28, 24);
            promptLine = UiKit.Label("", hud, 24, UiKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            promptLine.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), -500, 150, -500, -200);

            pageRoot = UiKit.Rect("Page", canvas.transform).Fill();

            fade = UiKit.Image("Fade", canvas.transform, Color.black);
            fade.rectTransform.Fill();
            fade.raycastTarget = false;
        }

        /// <summary>A line on the HUD (the time and place).</summary>
        public void SetHud(string text) => hudLine.text = text;

        /// <summary>A hint at the foot of the screen (what one can do here); null for none.</summary>
        public void SetPrompt(string text)
        {
            promptLine.text = text ?? "";
        }

        /// <summary>Shows a conversation: who speaks, what they say, and what one can answer.</summary>
        public void ShowDialogue(string name, string title, string text, IList<(string text, Action act)> choices)
        {
            speaker = name;
            speakerTitle = title;
            spoken = text;
            answers = choices;
            Show(Page.Dialogue);
        }

        public void Notify(string text)
        {
            noticeLine.text = text;
            noticeUntil = Time.unscaledTime + 2.2f;
        }

        /// <summary>Fades to black, does `then`, and fades back in.</summary>
        public void FadeThrough(Action then)
        {
            afterFade = then;
            fadeTarget = 1;
        }

        /// <summary>Starts covered in black and fades in (the game's opening).</summary>
        public void FadeIn()
        {
            fadeAlpha = 1;
            fadeTarget = 0;
        }

        void Update()
        {
            var step = Time.unscaledDeltaTime / 0.28f;
            fadeAlpha = Mathf.MoveTowards(fadeAlpha, fadeTarget, step);
            fade.color = new Color(0, 0, 0, fadeAlpha);
            fade.raycastTarget = fadeAlpha > 0.01f;

            if (fadeTarget >= 1 && fadeAlpha >= 1)
            {
                var then = afterFade;
                afterFade = null;
                fadeTarget = 0;
                then?.Invoke();
            }

            var noticeLeft = noticeUntil - Time.unscaledTime;
            noticeLine.color = new Color(UiKit.Text.r, UiKit.Text.g, UiKit.Text.b, Mathf.Clamp01(noticeLeft * 2));
        }

        /// <summary>Shows a page (rebuilt from scratch: the pages are small).</summary>
        public void Show(Page page)
        {
            Current = page;
            hud.gameObject.SetActive(page == Page.None);

            for (var i = pageRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(pageRoot.GetChild(i).gameObject);
            }

            switch (page)
            {
                case Page.Title: Title(); break;
                case Page.Creator: Creator(); break;
                case Page.Pause: Pause(); break;
                case Page.Settings: Settings(); break;
                case Page.About: About(); break;
                case Page.Confirm: Confirm(); break;
                case Page.Dialogue: Dialogue(); break;
            }
        }

        /// <summary>Esc on a page: one step back. False when there is nowhere to go back to.</summary>
        public bool Back()
        {
            switch (Current)
            {
                case Page.Settings: Show(settingsBack); return true;
                case Page.About: Show(Page.Title); return true;
                case Page.Confirm: Show(confirmBack); return true;
                case Page.Creator: FadeThrough(host.BackToTitle); return true;
                case Page.Pause: host.Resume(); return true;
                case Page.Dialogue: host.EndTalk(); return true;
                default: return false;
            }
        }

        public void Ask(string title, string text, string button, Action action, Page back)
        {
            confirmTitle = title;
            confirmText = text;
            confirmButton = button;
            confirmAction = action;
            confirmBack = back;
            Show(Page.Confirm);
        }

        /// <summary>Opens the settings on a tab (0 game, 1 controls, 2 graphics, 3 sound).</summary>
        public void ShowSettings(int onTab, Page back)
        {
            tab = (Tab)Mathf.Clamp(onTab, 0, 3);
            OpenSettings(back);
        }

        void OpenSettings(Page back)
        {
            settingsBack = back;
            Show(Page.Settings);
        }

        // ── The main menu ─────────────────────────────────────────────

        void Title()
        {
            // The world shows through; the left is darkened for the menu.
            var shade = UiKit.Image("Shade", pageRoot, new Color(0, 0, 0, 0.88f), UiKit.SoftEdge);
            shade.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -1150, 0);
            var floor = UiKit.Image("Floor", pageRoot, new Color(0, 0, 0, 0.35f));
            floor.rectTransform.Place(Vector2.zero, new Vector2(1, 0), 0, 0, 0, -90);

            // The emblem: 蛊, "Gu", large and faint behind the name.
            if (UiKit.Glyphs != null)
            {
                var emblem = UiKit.Label("蛊", pageRoot, 380, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.1f), TextAnchor.MiddleCenter);
                emblem.font = UiKit.Glyphs;
                emblem.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 40, -560, -620, 60);
            }

            var name = UiKit.Label("GU WORLD", pageRoot, 118, UiKit.Text, TextAnchor.LowerLeft, FontStyle.Bold).Shadowed();
            name.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 112, -300, -1100, 120);
            var line = UiKit.Image("Line", pageRoot, UiKit.Gold);
            line.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 118, -316, -340, 312);
            var motto = UiKit.Label("МИР ГУ  ·  РАННЯЯ ВЕРСИЯ", pageRoot, 22, UiKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold).Shadowed();
            motto.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 118, -370, -1100, 330);

            var menu = UiKit.Rect("Menu", pageRoot).Place(new Vector2(0, 0), new Vector2(0, 0), 90, 150, -620, -560);
            menu.Stack<VerticalLayoutGroup>(6, null, TextAnchor.LowerLeft);

            if (host.HasSave)
            {
                UiKit.Entry("Продолжить", host.SaveSummary, menu, () => FadeThrough(host.Continue));
            }

            UiKit.Entry("Новая игра", null, menu, () =>
            {
                if (host.HasSave)
                {
                    Ask("Начать заново?", "Сохранённая игра будет удалена: вы заново создадите героя и начнёте с первого утра.",
                        "Начать заново", () => FadeThrough(host.NewGame), Page.Title);
                }
                else
                {
                    FadeThrough(host.NewGame);
                }
            });
            UiKit.Entry("Настройки", null, menu, () => OpenSettings(Page.Title));
            UiKit.Entry("Об игре", null, menu, () => Show(Page.About));
            UiKit.Entry("Выход", null, menu, host.Quit);

            var credit = UiKit.Label("Модели персонажей — Quaternius (CC0)", pageRoot, 16, UiKit.Muted).Shadowed();
            credit.rectTransform.Place(Vector2.zero, Vector2.zero, 118, 28, -900, -60);
            var version = UiKit.Label($"v{Version}", pageRoot, 18, UiKit.Muted, TextAnchor.LowerRight).Shadowed();
            version.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), -300, 28, 36, -60);
        }

        // ── The hero's making ─────────────────────────────────────────

        void Creator()
        {
            var look = host.Look;
            var panel = UiKit.Panel("Panel", pageRoot, new Color(0.05f, 0.055f, 0.065f, 0.97f));
            panel.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -560, 0);
            panel.sprite = null;
            var column = UiKit.Rect("Column", panel.transform).Fill();
            column.Stack<VerticalLayoutGroup>(10, new RectOffset(52, 52, 56, 40));

            UiKit.Label("СОЗДАНИЕ ГЕРОЯ", column, 40, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).Size(52);
            UiKit.Label("Пятнадцать лет. Волосы чёрные, как у всех в клане.", column, 18, UiKit.Muted).Size(30);

            Heading(column, "ПОЛ");
            UiKit.Choices(column, new[] { "Юноша", "Девушка" }, look.Gender == Gender.Male ? 0 : 1, i =>
            {
                var gender = i == 0 ? Gender.Male : Gender.Female;

                if (gender != look.Gender)
                {
                    host.Wear(look.With(gender: gender, hair: Look.DefaultHair(gender)));
                    Show(Page.Creator);
                }
            });

            Heading(column, "ПРИЧЁСКА");
            var styles = (HairStyle[])Enum.GetValues(typeof(HairStyle));
            Grid(column, styles, 3, s => Look.Name(s), s => s == look.Hair, s =>
            {
                host.Wear(look.With(hair: s));
                Show(Page.Creator);
            });

            if (look.Gender == Gender.Male)
            {
                Heading(column, "БОРОДА");
                var beards = (Beard[])Enum.GetValues(typeof(Beard));
                Grid(column, beards, 3, b => Look.Name(b), b => b == look.BeardStyle, b =>
                {
                    host.Wear(look.With(beard: b));
                    Show(Page.Creator);
                });
            }

            Heading(column, "ОДЕЖДА");
            UiKit.Label("Рубаха с запахом, кушак, широкие штаны с обмотками и тканевые туфли — как носят в горной деревне.", column, 17, UiKit.Muted).Size(48);

            Heading(column, "ВИД");
            UiKit.Choices(column, new[] { "Весь рост", "Лицо" }, host.CreatorFace ? 1 : 0, i =>
            {
                host.CreatorFace = i == 1;
                Show(Page.Creator);
            });

            var space = UiKit.Rect("Space", column);
            space.Size(-1).flexibleHeight = 1;

            var buttons = UiKit.Rect("Buttons", column);
            buttons.Stack<HorizontalLayoutGroup>(10).childForceExpandWidth = true;
            buttons.Size(56);
            UiKit.Button("Назад", buttons, () => FadeThrough(host.BackToTitle)).Size(56, -1, 1);
            UiKit.Button("Начать игру", buttons, () => FadeThrough(host.StartPlaying), ButtonKind.Gold, 22).Size(56, -1, 2);

            var hint = UiKit.Label("Потяните мышью, чтобы повернуть героя", pageRoot, 18, UiKit.Muted, TextAnchor.LowerCenter).Shadowed();
            hint.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), 560, 30, 0, -70);
        }

        static void Heading(Transform parent, string text) =>
            UiKit.Label(text, parent, 16, UiKit.Muted, TextAnchor.LowerLeft, FontStyle.Bold).Size(34);

        static void Grid<T>(Transform parent, IList<T> items, int across, Func<T, string> name, Func<T, bool> chosen, Action<T> pick)
        {
            for (var i = 0; i < items.Count; i += across)
            {
                var row = new List<string>();
                var chosenIndex = -1;

                for (var k = i; k < Mathf.Min(i + across, items.Count); k++)
                {
                    if (chosen(items[k])) chosenIndex = k - i;
                    row.Add(name(items[k]));
                }

                var start = i;
                UiKit.Choices(parent, row, chosenIndex, index => pick(items[start + index]), 17);
            }
        }

        // ── The pause ─────────────────────────────────────────────────

        void Pause()
        {
            var dim = UiKit.Image("Dim", pageRoot, new Color(0, 0, 0, 0.45f));
            dim.rectTransform.Fill();
            var shade = UiKit.Image("Shade", pageRoot, new Color(0, 0, 0, 0.8f), UiKit.SoftEdge);
            shade.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -1000, 0);

            var title = UiKit.Label("ПАУЗА", pageRoot, 72, UiKit.Text, TextAnchor.LowerLeft, FontStyle.Bold).Shadowed();
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 112, -260, -900, 120);
            var line = UiKit.Image("Line", pageRoot, UiKit.Gold);
            line.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 118, -276, -300, 272);

            var menu = UiKit.Rect("Menu", pageRoot).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), 90, -260, -620, -140);
            menu.Stack<VerticalLayoutGroup>(6, null, TextAnchor.UpperLeft);
            UiKit.Entry("Продолжить", null, menu, host.Resume);
            UiKit.Entry("Настройки", null, menu, () => OpenSettings(Page.Pause));
            UiKit.Entry("Главное меню", null, menu, () => FadeThrough(host.BackToTitle));
            UiKit.Entry("Выйти из игры", null, menu, host.Quit);

            // The controls, on a card at the right.
            var card = UiKit.Panel("Controls", pageRoot);
            card.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), -620, -250, 110, -250);
            var list = UiKit.Rect("List", card.transform).Fill();
            list.Stack<VerticalLayoutGroup>(4, new RectOffset(36, 36, 30, 30));
            UiKit.Label("УПРАВЛЕНИЕ", list, 18, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).Size(36);
            Keys(list);
        }

        static void Keys(Transform parent)
        {
            string[,] keys =
            {
                { "W A S D", "ходьба" },
                { "Shift", "бег" },
                { "Пробел", "прыжок · залезть на уступ" },
                { "C", "присесть" },
                { "Z", "ползти" },
                { "Мышь", "камера" },
                { "Колесо", "ближе / дальше" },
                { "Esc", "пауза" },
                { "F3", "отладка" },
            };

            for (var i = 0; i < keys.GetLength(0); i++)
            {
                var row = UiKit.Rect(keys[i, 0], parent);
                row.Stack<HorizontalLayoutGroup>(12, null, TextAnchor.MiddleLeft).childForceExpandWidth = false;
                row.Size(40);
                var key = UiKit.Panel("Key", row, UiKit.InkLight);
                key.Size(34, 150, 0);
                UiKit.Label(keys[i, 0], key.transform, 17, UiKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Fill();
                UiKit.Label(keys[i, 1], row, 18, UiKit.Text).Size(-1, -1, 1);
            }
        }

        // ── The settings ──────────────────────────────────────────────

        void Settings()
        {
            Backdrop();
            var card = UiKit.Panel("Settings", pageRoot);
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -560, -400, -560, -400);
            var column = UiKit.Rect("Column", card.transform).Fill();
            column.Stack<VerticalLayoutGroup>(12, new RectOffset(48, 48, 40, 36));

            UiKit.Label("НАСТРОЙКИ", column, 40, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).Size(56);
            UiKit.Choices(column, new[] { "Игра", "Управление", "Графика", "Звук" }, (int)tab, i =>
            {
                tab = (Tab)i;
                Show(Page.Settings);
            }, 19);

            var rule = UiKit.Image("Rule", column, new Color(1, 1, 1, 0.08f));
            rule.Size(2);

            var view = host.View;

            switch (tab)
            {
                case Tab.Game:
                {
                    var row = UiKit.Row("Прогресс", column);
                    UiKit.Button("Сбросить игру", row, () =>
                        Ask("Сбросить игру?", "Сохранение будет удалено. Игра начнётся заново — с создания героя. Настройки останутся.",
                            "Да, сбросить", host.ResetGame, Page.Settings), ButtonKind.Danger).Size(48, -1, 1);
                    UiKit.Label("Игра сохраняется сама каждые 30 секунд, при паузе и при выходе.", column, 17, UiKit.Muted).Size(30);
                    UiKit.Label($"Файл сохранения: {State.SaveGame.FilePath}", column, 15, UiKit.Muted).Size(44);
                    break;
                }
                case Tab.Controls:
                {
                    var sensitivity = UiKit.Row("Чувствительность мыши", column);
                    UiKit.Slider(sensitivity, 0.25f, 3, GameSettings.Sensitivity, v => v.ToString("0.00"), v =>
                    {
                        GameSettings.Sensitivity = v;
                        GameSettings.Apply(view);
                    });
                    var invert = UiKit.Row("Инверсия по вертикали", column);
                    UiKit.Choices(invert, new[] { "Выкл", "Вкл" }, GameSettings.InvertY ? 1 : 0, i =>
                    {
                        GameSettings.InvertY = i == 1;
                        GameSettings.Apply(view);
                        Show(Page.Settings);
                    });
                    UiKit.Label("Клавиши", column, 16, UiKit.Muted, TextAnchor.LowerLeft, FontStyle.Bold).Size(34);
                    var keys = UiKit.Rect("Keys", column);
                    keys.Stack<VerticalLayoutGroup>(2);
                    Keys(keys);
                    break;
                }
                case Tab.Graphics:
                {
                    var quality = UiKit.Row("Качество", column);
                    UiKit.Choices(quality, new[] { "Низкое", "Среднее", "Высокое" }, GameSettings.Quality, i =>
                    {
                        GameSettings.Quality = i;
                        GameSettings.Apply(view);
                        Show(Page.Settings);
                    });
                    var window = UiKit.Row("Режим экрана", column);
                    UiKit.Choices(window, new[] { "В окне", "Во весь экран" }, GameSettings.Fullscreen ? 1 : 0, i =>
                    {
                        GameSettings.Fullscreen = i == 1;
                        GameSettings.Apply(view);
                        Show(Page.Settings);
                    });
                    var vsync = UiKit.Row("Вертикальная синхронизация", column);
                    UiKit.Choices(vsync, new[] { "Выкл", "Вкл" }, GameSettings.VSync ? 1 : 0, i =>
                    {
                        GameSettings.VSync = i == 1;
                        GameSettings.Apply(view);
                        Show(Page.Settings);
                    });
                    var fov = UiKit.Row("Поле зрения", column);
                    UiKit.Slider(fov, 55, 90, GameSettings.FieldOfView, v => $"{v:0}°", v =>
                    {
                        GameSettings.FieldOfView = v;
                        GameSettings.Apply(view);
                    });
                    break;
                }
                case Tab.Sound:
                {
                    var volume = UiKit.Row("Общая громкость", column);
                    UiKit.Slider(volume, 0, 1, GameSettings.Volume, v => $"{v * 100:0}%", v =>
                    {
                        GameSettings.Volume = v;
                        GameSettings.Apply(view);
                    });
                    UiKit.Label("Звуков в игре пока нет — громкость пригодится, когда они появятся.", column, 17, UiKit.Muted).Size(30);
                    break;
                }
            }

            var space = UiKit.Rect("Space", column);
            space.Size(-1).flexibleHeight = 1;
            var buttons = UiKit.Rect("Buttons", column);
            buttons.Stack<HorizontalLayoutGroup>(10, null, TextAnchor.MiddleRight).childForceExpandWidth = false;
            buttons.Size(52);
            UiKit.Button("Назад", buttons, () => Show(settingsBack)).Size(52, 220);
        }

        /// <summary>The world dimmed behind a card.</summary>
        void Backdrop()
        {
            var dim = UiKit.Image("Dim", pageRoot, new Color(0, 0, 0, 0.62f));
            dim.rectTransform.Fill();
            dim.raycastTarget = true;
        }

        // ── About, confirmations ──────────────────────────────────────

        void About()
        {
            Backdrop();
            var card = UiKit.Panel("About", pageRoot);
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -480, -330, -480, -330);
            var column = UiKit.Rect("Column", card.transform).Fill();
            column.Stack<VerticalLayoutGroup>(14, new RectOffset(48, 48, 40, 36));

            UiKit.Label("ОБ ИГРЕ", column, 40, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).Size(56);
            UiKit.Label("GU World — игра о мире Гу, вдохновлённая романом «Reverend Insanity» (Гу Чжэнь Жэнь). " +
                        "Это ранняя версия: полигон для проверки движка, герой и его облик.", column, 20).Size(84);
            UiKit.Label($"Версия {Version}", column, 18, UiKit.Muted).Size(28);
            UiKit.Label("АВТОРЫ И ИСТОЧНИКИ", column, 16, UiKit.Muted, TextAnchor.LowerLeft, FontStyle.Bold).Size(34);
            UiKit.Label("Модели персонажей — Quaternius, Universal Base Characters (CC0).\nДвижок — Unity 6, Universal Render Pipeline.", column, 18).Size(60);

            var space = UiKit.Rect("Space", column);
            space.Size(-1).flexibleHeight = 1;
            var buttons = UiKit.Rect("Buttons", column);
            buttons.Stack<HorizontalLayoutGroup>(10, null, TextAnchor.MiddleRight).childForceExpandWidth = false;
            buttons.Size(52);
            UiKit.Button("Назад", buttons, () => Show(Page.Title)).Size(52, 220);
        }

        /// <summary>The conversation: a band across the foot of the screen, the questions on its right.</summary>
        void Dialogue()
        {
            var band = UiKit.Panel("Dialogue", pageRoot);
            band.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), 120, 40, 120, -330);
            var left = UiKit.Rect("Words", band.transform).Place(Vector2.zero, new Vector2(0.62f, 1));
            left.Stack<VerticalLayoutGroup>(6, new RectOffset(40, 24, 30, 26));
            UiKit.Label(speaker, left, 30, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).Size(38);
            UiKit.Label(speakerTitle, left, 18, UiKit.Muted).Size(26);
            UiKit.Image("Rule", left, new Color(1, 1, 1, 0.08f)).Size(2);
            UiKit.Label(spoken, left, 23).Size(-1).flexibleHeight = 1;

            var right = UiKit.Rect("Answers", band.transform).Place(new Vector2(0.62f, 0), Vector2.one);
            right.Stack<VerticalLayoutGroup>(8, new RectOffset(16, 34, 30, 26), TextAnchor.MiddleLeft);

            foreach (var (text, act) in answers)
            {
                UiKit.Button(text, right, act, ButtonKind.Plain, 19).Size(50);
            }
        }

        void Confirm()
        {
            Backdrop();
            var card = UiKit.Panel("Confirm", pageRoot);
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -400, -170, -400, -170);
            var column = UiKit.Rect("Column", card.transform).Fill();
            column.Stack<VerticalLayoutGroup>(14, new RectOffset(44, 44, 36, 32));

            UiKit.Label(confirmTitle, column, 34, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Size(46);
            UiKit.Label(confirmText, column, 19, UiKit.Muted).Size(64);
            var space = UiKit.Rect("Space", column);
            space.Size(-1).flexibleHeight = 1;

            var buttons = UiKit.Rect("Buttons", column);
            buttons.Stack<HorizontalLayoutGroup>(10).childForceExpandWidth = true;
            buttons.Size(54);
            UiKit.Button("Отмена", buttons, () => Show(confirmBack)).Size(54, -1, 1);
            UiKit.Button(confirmButton, buttons, () => confirmAction?.Invoke(), ButtonKind.Danger).Size(54, -1, 1);
        }
    }
}
