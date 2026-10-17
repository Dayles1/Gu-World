using System;
using System.Collections.Generic;
using GuWorld.Body;
using GuWorld.Cultivation;
using GuWorld.Items;
using GuWorld.Systems;
using GuWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace GuWorld.UI
{
    /// <summary>What the menus ask of the game.</summary>
    public interface IMenuHost
    {
        /// <summary>A saved game to go on with, and a line about it ("Гу Юэ Лань · Ранг 1 · День 3 · 14:20").</summary>
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
        /// <summary>Goes to one of the hero's pages (the bag, the aperture, the map) — the world waits.</summary>
        void OpenPage(Menus.Page page);
        /// <summary>Puts the hero at a point at once (y NaN: on the ground there).</summary>
        void Teleport(Vector3 at);
        /// <summary>The chat's commands (null where there are none), and closing the chat.</summary>
        GuWorld.Systems.Commands Commands { get; }
        void CloseChat();

        /// <summary>The bodies the figures are made of (the body page shows the hero's).</summary>
        HumanBody.Parts Parts { get; }

        /// <summary>The hero as a Gu Master, their health, their tasks.</summary>
        Cultivator Cultivator { get; }
        /// <summary>The hero's own fists (their slot on the bar).</summary>
        GuWorld.Combat.Melee Fists { get; }
        Health Health { get; }
        Quests Quests { get; }
        /// <summary>The shop the hero is at (null: none), and a deal there.</summary>
        Shop Shop { get; }
        Trade Trade { get; }
        Deal Buy(IDictionary<string, int> lots);
        int Sell(int cell, int count);
        /// <summary>Throws a pile out of the bag.</summary>
        void Drop(int cell);

        /// <summary>The location's map, where the hero is on it and which way they face (radians, 0 north).</summary>
        LocalMap Map { get; }
        /// <summary>A finer map round the hero, for the minimap (null where there is none).</summary>
        LocalMap NearMap { get; }
        Vector3 HeroAt { get; }
        float HeroFacing { get; }
        /// <summary>The place and the hour, as the HUD shows them.</summary>
        string PlaceName { get; }
        string TimeLine { get; }
        /// <summary>The game minute now.</summary>
        double Now { get; }
    }

    /// <summary>
    /// GU World's menus (uGUI, built in code, in the design's look): the main
    /// menu over the living world, the hero's making, the pause, the settings,
    /// the Gu encyclopedia, confirmations and conversations — and, while
    /// playing, the HUD (MenusHud) and the hero's pages: the bag, the aperture,
    /// the map and a shop (MenusBag, MenusAperture, MenusMap, MenusShop).
    /// Changing between the big screens fades through black. Everything
    /// animates in real time, so it works while the world is paused.
    /// </summary>
    public partial class Menus : MonoBehaviour
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
            /// <summary>The aperture: the sea of essence, the ring of Gu, feeding them.</summary>
            Aperture,
            Bag,
            Map,
            Shop,
            /// <summary>The Gu encyclopedia (from the main menu).</summary>
            Codex,
            /// <summary>The body: where blows wound and how much.</summary>
            Body,
            /// <summary>The chat over the game (T, /): the world goes on.</summary>
            Chat,
        }

        enum Tab
        {
            Game,
            Controls,
            Graphics,
            Sound,
        }

        public const string Version = "0.2.0";

        IMenuHost host;
        Canvas canvas;
        RectTransform pageRoot;
        Text fpsLine;

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
        string codexChosen;

        public Page Current { get; private set; } = (Page)(-1);

        /// <summary>The width of the hero's making panel, in screen pixels (the camera is dragged right of it).</summary>
        public float CreatorPanelWidth => 600f * canvas.scaleFactor;

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

            BuildHud();
            pageRoot = UiKit.Rect("Page", canvas.transform).Fill();

            // The counter sits above everything, menus too.
            fpsLine = UiKit.Label("", canvas.transform, 15, new Color(0.62f, 0.9f, 0.62f), TextAnchor.UpperLeft, FontStyle.Bold).Shadowed();
            fpsLine.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 12, -30, -300, 8);

            fade = UiKit.Image("Fade", canvas.transform, Color.black);
            fade.rectTransform.Fill();
            fade.raycastTarget = false;
        }

        /// <summary>The frames-per-second counter's value (shown when the setting is on).</summary>
        public void SetFps(float fps)
        {
            fpsLine.text = GameSettings.ShowFps ? $"FPS {fps:0}" : "";
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

        bool hudHidden;

        /// <summary>The interface hidden (F1): only the world is drawn.</summary>
        public bool HudHidden
        {
            get => hudHidden;
            set
            {
                hudHidden = value;
                hud.gameObject.SetActive(!hudHidden && (Current == Page.None || Current == Page.Dialogue || Current == Page.Chat));
            }
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

            UpdateHud();
            UpdatePages();

            if (Current == Page.Chat)
            {
                UpdateChat();
            }

            if (Current == Page.Map)
            {
                UpdateMapPage();
            }
        }

        /// <summary>Shows a page (rebuilt from scratch: the pages are small).</summary>
        public void Show(Page page)
        {
            Current = page;
            hud.gameObject.SetActive(!hudHidden && (page == Page.None || page == Page.Dialogue || page == Page.Chat));

            if (page != Page.Body)
            {
                Render.BodyStudio.Close();
            }

            for (var i = pageRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(pageRoot.GetChild(i).gameObject);
            }

            pageCells.Clear();

            switch (page)
            {
                case Page.Title: Title(); break;
                case Page.Creator: Creator(); break;
                case Page.Pause: Pause(); break;
                case Page.Settings: Settings(); break;
                case Page.About: About(); break;
                case Page.Confirm: Confirm(); break;
                case Page.Dialogue: Dialogue(); break;
                case Page.Aperture: AperturePage(); break;
                case Page.Bag: BagPage(); break;
                case Page.Map: MapPage(); break;
                case Page.Chat: ChatPage(); break;
                case Page.Shop: ShopPage(); break;
                case Page.Codex: Codex(); break;
                case Page.Body: BodyPage(); break;
            }
        }

        /// <summary>Esc on a page: one step back. False when there is nowhere to go back to.</summary>
        public bool Back()
        {
            switch (Current)
            {
                case Page.Settings: Show(settingsBack); return true;
                case Page.About: Show(Page.Title); return true;
                case Page.Codex: Show(Page.Title); return true;
                case Page.Confirm: Show(confirmBack); return true;
                case Page.Creator: FadeThrough(host.BackToTitle); return true;
                case Page.Pause: host.Resume(); return true;
                case Page.Dialogue: host.EndTalk(); return true;
                case Page.Aperture:
                case Page.Bag:
                case Page.Map:
                case Page.Shop:
                case Page.Body:
                    host.Resume();
                    return true;
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
            var shade = UiKit.Image("Shade", pageRoot, new Color(0.02f, 0.016f, 0.016f, 0.9f), UiKit.SoftEdge);
            shade.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -1150, 0);
            var floor = UiKit.Image("Floor", pageRoot, new Color(0, 0, 0, 0.35f));
            floor.rectTransform.Place(Vector2.zero, new Vector2(1, 0), 0, 0, 0, -90);

            // The seal and the name.
            var seal = UiKit.SealMark("moon", pageRoot, 84);
            seal.rectTransform.anchorMin = seal.rectTransform.anchorMax = new Vector2(0, 1);
            seal.rectTransform.anchoredPosition = new Vector2(160, -150);
            var name = UiKit.Label("GU WORLD", pageRoot, 104, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Shadowed();
            name.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 222, -210, -1100, 90);
            var world = UiKit.Caps("Мир Гу · путь мастера", pageRoot, 22, UiKit.Gold);
            world.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 228, -258, -900, 214);
            var line = UiKit.Image("Line", pageRoot, UiKit.GoldDeep);
            line.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 118, -282, -700, 280);
            var mountain = UiKit.Caps("Гора Цинмао · Южная граница", pageRoot, 18, UiKit.Muted);
            mountain.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 118, -326, -900, 292);

            var menu = UiKit.Rect("Menu", pageRoot).Place(new Vector2(0, 0), new Vector2(0, 0), 96, 140, -660, -620);
            menu.Stack<VerticalLayoutGroup>(8, null, TextAnchor.LowerLeft);

            if (host.HasSave)
            {
                // The way back in: a card with the cinnabar button and who is waiting.
                var card = UiKit.Panel("Continue", menu, UiKit.Hex(0x121010, 0.92f));
                card.Size(118);
                var column = UiKit.Rect("Column", card.transform).Fill();
                column.Stack<VerticalLayoutGroup>(6, new RectOffset(20, 20, 16, 14));
                UiKit.Button(UiKit.Spaced("Продолжить"), column, () => FadeThrough(host.Continue), ButtonKind.Gold, 22).Size(54);
                UiKit.Label(host.SaveSummary, column, 17, UiKit.Muted).Size(26);
            }

            UiKit.Entry("Новый путь", null, menu, () =>
            {
                if (host.HasSave)
                {
                    Ask("Начать новый путь?", "Сохранённая игра будет удалена: вы заново создадите героя и начнёте с первого утра.",
                        "Начать заново", () => FadeThrough(host.NewGame), Page.Title);
                }
                else
                {
                    FadeThrough(host.NewGame);
                }
            }, "play");
            UiKit.Entry("Энциклопедия Гу", null, menu, () => Show(Page.Codex), "book");
            UiKit.Entry("Настройки", null, menu, () => OpenSettings(Page.Title), "gear");
            UiKit.Entry("Об игре", null, menu, () => Show(Page.About), "info");
            UiKit.Entry("Выход", null, menu, host.Quit, "exit");

            // The chronicle: what is new in this version.
            var chronicle = UiKit.Panel("Chronicle", pageRoot, UiKit.Hex(0x121010, 0.88f));
            chronicle.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), -560, 90, 48, -330);
            var notes = UiKit.Rect("Notes", chronicle.transform).Fill();
            notes.Stack<VerticalLayoutGroup>(6, new RectOffset(26, 26, 20, 18));
            UiKit.Caps("Хроника", notes, 15).Size(24);
            UiKit.Label("Гора Цинмао по атласу мира", notes, 21, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Size(30);
            UiKit.Label("Кольцо апертуры на восемь Гу, голод и кормление Гу, сумка, лавки, задания академии, " +
                        "грядки лунных орхидей, здоровье, карта горы и мира.", notes, 16, UiKit.Muted).Size(84);

            var credit = UiKit.Label("Модели персонажей — Quaternius (CC0)", pageRoot, 15, UiKit.Muted).Shadowed();
            credit.rectTransform.Place(Vector2.zero, Vector2.zero, 118, 28, -900, -60);
            var version = UiKit.Label($"v{Version}", pageRoot, 17, UiKit.Muted, TextAnchor.LowerRight).Shadowed();
            version.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), -300, 28, 48, -60);
        }

        // ── The hero's making ─────────────────────────────────────────

        void Creator()
        {
            var look = host.Look;
            var panel = UiKit.Panel("Panel", pageRoot, UiKit.Hex(0x0E0C0B, 0.97f));
            panel.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -600, 0);
            var column = UiKit.Rect("Column", panel.transform).Fill();
            column.Stack<VerticalLayoutGroup>(6, new RectOffset(52, 52, 34, 26));

            UiKit.Label("СОЗДАНИЕ ГЕРОЯ", column, 36, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).Size(46);
            UiKit.Label("Пятнадцать лет, клан Гу Юэ. Волосы чёрные, как у всех в клане.", column, 16, UiKit.Muted).Size(24);

            Heading(column, "ИМЯ");
            var nameRow = UiKit.Rect("Name", column);
            nameRow.Stack<HorizontalLayoutGroup>(8, null, TextAnchor.MiddleLeft).childForceExpandWidth = false;
            nameRow.Size(48);
            var field = UiKit.Field(nameRow, look.name, "Своё имя — после имени клана", 16, text =>
            {
                // The name is kept as typed; the body is not rebuilt for it.
                look.name = Look.CleanName(text);
            });
            field.Size(46, -1, 1);
            // A whole look by chance (the name and the gender kept).
            UiKit.Button("Случайно", nameRow, () =>
            {
                host.Wear(look.Random(new System.Random()));
                Show(Page.Creator);
            }, ButtonKind.Plain, 16).Size(46, 130);

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

            Heading(column, "ХАНЬФУ — НА СПИНЕ И ГРУДИ СЕРЕБРЯНЫЙ ПОЛУМЕСЯЦ КЛАНА");
            var robes = (Robe[])Enum.GetValues(typeof(Robe));
            Grid(column, robes, 3, r => Look.Name(r), r => r == look.RobeColour, r =>
            {
                host.Wear(look.With(robe: r));
                Show(Page.Creator);
            });

            Heading(column, "КОЖА");
            var skins = (Skin[])Enum.GetValues(typeof(Skin));
            Grid(column, skins, 3, s => Look.Name(s), s => s == look.SkinTone, s =>
            {
                host.Wear(look.With(skin: s));
                Show(Page.Creator);
            });

            Heading(column, "РОСТ");
            var statures = (Stature[])Enum.GetValues(typeof(Stature));
            Grid(column, statures, 3, s => Look.Name(s), s => s == look.Height, s =>
            {
                host.Wear(look.With(stature: s));
                Show(Page.Creator);
            });

            Heading(column, "ВИД — КОЛЕСО МЫШИ ПРИБЛИЖАЕТ");
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
            UiKit.Button(UiKit.Spaced("Начать путь"), buttons, () => FadeThrough(host.StartPlaying), ButtonKind.Gold, 21).Size(56, -1, 2);

            var hint = UiKit.Label("Потяните мышью, чтобы повернуть героя · колесо — ближе / дальше", pageRoot, 18, UiKit.Muted, TextAnchor.LowerCenter).Shadowed();
            hint.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), 600, 30, 0, -70);
        }

        static void Heading(Transform parent, string text) =>
            UiKit.Label(text, parent, 14, UiKit.Muted, TextAnchor.LowerLeft, FontStyle.Bold).Size(24);

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
            var shade = UiKit.Image("Shade", pageRoot, new Color(0.02f, 0.016f, 0.016f, 0.85f), UiKit.SoftEdge);
            shade.rectTransform.Place(Vector2.zero, new Vector2(0, 1), 0, 0, -1000, 0);

            var title = UiKit.Label("ПАУЗА", pageRoot, 68, UiKit.Text, TextAnchor.LowerLeft, FontStyle.Bold).Shadowed();
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 112, -240, -900, 120);
            var line = UiKit.Image("Line", pageRoot, UiKit.GoldDeep);
            line.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), 118, -256, -400, 254);

            var menu = UiKit.Rect("Menu", pageRoot).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), 90, -400, -640, -190);
            menu.Stack<VerticalLayoutGroup>(2, null, TextAnchor.UpperLeft);
            UiKit.Entry("Продолжить", null, menu, host.Resume, "play");
            UiKit.Entry("Апертура", null, menu, () => host.OpenPage(Page.Aperture), "ring");
            UiKit.Entry("Сумка", null, menu, () => host.OpenPage(Page.Bag), "bag");
            UiKit.Entry("Карта", null, menu, () => host.OpenPage(Page.Map), "map");
            UiKit.Entry("Тело", null, menu, () => host.OpenPage(Page.Body), "body");
            UiKit.Entry("Настройки", null, menu, () => OpenSettings(Page.Pause), "gear");
            UiKit.Entry("Главное меню", null, menu, () => FadeThrough(host.BackToTitle), "home");
            UiKit.Entry("Выйти из игры", null, menu, host.Quit, "exit");

            // The controls, on a card at the right.
            var card = UiKit.Panel("Controls", pageRoot);
            card.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), -640, -340, 100, -340);
            var list = UiKit.Rect("List", card.transform).Fill();
            list.Stack<VerticalLayoutGroup>(2, new RectOffset(34, 34, 26, 26));
            UiKit.Caps("Управление", list, 16).Size(32);
            Keys(list);
        }

        /// <summary>The keys and what they do, in one column or side by side in two (the settings' card is not tall enough for one).</summary>
        static void Keys(Transform parent, int columns = 1)
        {
            string[,] keys =
            {
                { "W A S D", "ходьба" },
                { "Shift", "бег" },
                { "Пробел", "прыжок · на уступ · ухватиться за стену" },
                { "C / Z", "присесть / ползти" },
                { "1 – 4", "Гу: держать — прицел, отпустить — удар" },
                { "F", "кулаки: ЛКМ серия · ПКМ блок · Q · R" },
                { "ЛКМ", "без кулаков — первый Гу в прицел" },
                { "V", "поглотить первобытный камень (3 с)" },
                { "E", "говорить · собрать (удерживать)" },
                { "K · I · M · B", "апертура · сумка · карта · тело" },
                { "X", "сесть · встать" },
                { "На стене", "W A S D — лезть · Пробел — оттолкнуться" },
                { "Мышь", "камера, колесо — ближе / дальше" },
                { "F5", "вид от первого лица / из-за спины" },
                { "T · /", "чат и команды" },
                { "Esc", "пауза" },
                { "F3 · F7", "отладка · режим хитбоксов" },
            };

            var into = new Transform[columns];

            if (columns > 1)
            {
                var sides = UiKit.Rect("Columns", parent);
                sides.Stack<HorizontalLayoutGroup>(28).childForceExpandWidth = true;

                for (var c = 0; c < columns; c++)
                {
                    var column = UiKit.Rect("Column", sides);
                    column.Stack<VerticalLayoutGroup>(2, null, TextAnchor.UpperLeft);
                    column.Size(-1, -1, 1);
                    into[c] = column;
                }
            }
            else
            {
                into[0] = parent;
            }

            var perColumn = Mathf.CeilToInt(keys.GetLength(0) / (float)columns);

            for (var i = 0; i < keys.GetLength(0); i++)
            {
                var row = UiKit.Rect(keys[i, 0], into[i / perColumn]);
                row.Stack<HorizontalLayoutGroup>(12, null, TextAnchor.MiddleLeft).childForceExpandWidth = false;
                row.Size(35);
                var key = UiKit.Panel("Key", row, UiKit.InkLight, UiKit.LineLit);
                key.Size(32, 140, 0);
                UiKit.Label(keys[i, 0], key.transform, 16, UiKit.GoldLit, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Fill();
                UiKit.Label(keys[i, 1], row, 17, UiKit.Text).Size(-1, -1, 1);
            }
        }

        // ── The settings ──────────────────────────────────────────────

        void Settings()
        {
            Backdrop();
            var card = UiKit.Panel("Settings", pageRoot);
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -560, -420, -560, -420);
            var column = UiKit.Rect("Column", card.transform).Fill();
            column.Stack<VerticalLayoutGroup>(12, new RectOffset(48, 48, 40, 36));

            UiKit.Label("НАСТРОЙКИ", column, 38, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).Size(54);
            UiKit.Choices(column, new[] { "Игра", "Управление", "Графика", "Звук" }, (int)tab, i =>
            {
                tab = (Tab)i;
                Show(Page.Settings);
            }, 19);

            var rule = UiKit.Image("Rule", column, UiKit.Line);
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
                    UiKit.Label("Клавиши", column, 15, UiKit.Muted, TextAnchor.LowerLeft, FontStyle.Bold).Size(30);
                    var keys = UiKit.Rect("Keys", column);
                    keys.Stack<VerticalLayoutGroup>(2);
                    Keys(keys, 2);
                    break;
                }
                case Tab.Graphics:
                {
                    var quality = UiKit.Row("Качество", column);
                    UiKit.Choices(quality, System.Array.ConvertAll(GameSettings.Levels, l => l.Name), GameSettings.Quality, i =>
                    {
                        GameSettings.Quality = i;
                        GameSettings.Apply(view);
                        Show(Page.Settings);
                    });
                    var level = GameSettings.Current;
                    UiKit.Label($"Тени до {level.Shadows:0} м · трава до {level.GrassDistance:0} м · деревья до {level.TreeDistance / 1000:0.#} км · люди до {level.PeopleSight:0} м · " +
                                $"масштаб {level.Scale * 100:0}% · MSAA ×{level.Msaa}", column, 15, UiKit.Muted).Size(26);
                    var boxes = UiKit.Row("Хитбоксы (F7)", column);
                    UiKit.Choices(boxes, new[] { "Скрыты", "Показаны" }, GameSettings.ShowHitboxes ? 1 : 0, i =>
                    {
                        GameSettings.ShowHitboxes = i == 1;
                        Render.HitboxView.On = i == 1;
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
                    var counter = UiKit.Row("Показывать FPS", column);
                    UiKit.Choices(counter, new[] { "Выкл", "Вкл" }, GameSettings.ShowFps ? 1 : 0, i =>
                    {
                        GameSettings.ShowFps = i == 1;
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
            var dim = UiKit.Image("Dim", pageRoot, new Color(0.02f, 0.016f, 0.016f, 0.7f));
            dim.rectTransform.Fill();
            dim.raycastTarget = true;
        }

        // ── About, the encyclopedia, confirmations ────────────────────

        void About()
        {
            Backdrop();
            var card = UiKit.Panel("About", pageRoot);
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -500, -340, -500, -340);
            var column = UiKit.Rect("Column", card.transform).Fill();
            column.Stack<VerticalLayoutGroup>(12, new RectOffset(48, 48, 40, 36));

            UiKit.Label("ОБ ИГРЕ", column, 38, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold).Size(54);
            UiKit.Label("GU World — игра о мире Гу, вдохновлённая романом «Reverend Insanity» (Гу Чжэнь Жэнь). " +
                        "Первая арка — гора Цинмао на Южной границе: деревня клана Гу Юэ, академия, первые Гу. " +
                        "Мир разложен по атласу «Мир Гу»; сведения о Гу — из лор-базы, слова — свои.", column, 19).Size(110);
            UiKit.Label($"Версия {Version}", column, 17, UiKit.Muted).Size(26);
            UiKit.Label("АВТОРЫ И ИСТОЧНИКИ", column, 15, UiKit.Muted, TextAnchor.LowerLeft, FontStyle.Bold).Size(32);
            UiKit.Label("Модели персонажей — Quaternius, Universal Base Characters (CC0).\nДвижок — Unity 6, Universal Render Pipeline.", column, 17).Size(56);

            var space = UiKit.Rect("Space", column);
            space.Size(-1).flexibleHeight = 1;
            var buttons = UiKit.Rect("Buttons", column);
            buttons.Stack<HorizontalLayoutGroup>(10, null, TextAnchor.MiddleRight).childForceExpandWidth = false;
            buttons.Size(52);
            UiKit.Button("Назад", buttons, () => Show(Page.Title)).Size(52, 220);
        }

        /// <summary>The Gu encyclopedia: the Gu the game knows, and what the lore says each eats and does.</summary>
        void Codex()
        {
            Backdrop();
            var card = UiKit.Panel("Codex", pageRoot);
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -720, -420, -720, -420);

            var header = UiKit.Rect("Header", card.transform).Place(new Vector2(0, 1), new Vector2(1, 1), 40, -110, 40, 30);
            var seal = UiKit.SealMark("book", header, 64);
            seal.rectTransform.At(new Vector2(0, 0.5f), new Vector2(32, 0), new Vector2(64, 64));
            var title = UiKit.Label("ЭНЦИКЛОПЕДИЯ ГУ", header, 34, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.rectTransform.Place(Vector2.zero, Vector2.one, 86, 0, 0, 0);

            var list = UiKit.Rect("List", card.transform).Place(Vector2.zero, new Vector2(0, 1), 40, 100, -380, 130);
            list.Stack<VerticalLayoutGroup>(8);
            GuKind chosen = null;

            foreach (var kind in GuBook.All)
            {
                chosen ??= kind;

                if (kind.Id == codexChosen)
                {
                    chosen = kind;
                }
            }

            foreach (var kind in GuBook.All)
            {
                var k = kind;
                UiKit.Button($"{kind.Name} · ранг {kind.Rank}", list, () =>
                {
                    codexChosen = k.Id;
                    Show(Page.Codex);
                }, kind == chosen ? ButtonKind.Tab : ButtonKind.Plain, 18).Size(52);
            }

            var details = UiKit.Rect("Details", card.transform).Place(Vector2.zero, Vector2.one, 420, 100, 40, 130);
            details.Stack<VerticalLayoutGroup>(10);

            if (chosen != null)
            {
                var item = ItemBook.Find(chosen.Item);
                var food = ItemBook.Find(chosen.Food);
                UiKit.Label(chosen.Name, details, 30, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Size(40);
                UiKit.Caps($"Ранг {chosen.Rank} · {chosen.Kind}", details, 15, Aperture.RankColour(chosen.Rank)).Size(24);
                UiKit.Label(item?.About ?? "", details, 19).Size(84);
                UiKit.Image("Rule", details, UiKit.Line).Size(2);
                UiKit.Label($"<b>Навык:</b> {chosen.SkillName} — {SkillLine(chosen)}", details, 18).Size(30);
                UiKit.Label($"<b>Пища:</b> {food?.Name.ToLowerInvariant()} ×{chosen.Meal} {MealLine(chosen.MealEvery)}", details, 18).Size(30);
                UiKit.Label($"<b>Очищение в апертуру:</b> {chosen.RefineCost:0}% эссенции разом", details, 18).Size(30);
                UiKit.Label($"<b>Цена в лавке:</b> {(item != null && item.Price > 0 ? ItemBook.Stones(item.Price) : "не продаётся")}", details, 18).Size(30);
                UiKit.Label($"Запись лор-базы: {chosen.LoreRef}", details, 14, UiKit.Muted).Size(22);
            }

            var buttons = UiKit.Rect("Buttons", card.transform).Place(new Vector2(1, 0), new Vector2(1, 0), -280, 36, 40, -88);
            buttons.Stack<HorizontalLayoutGroup>(10, null, TextAnchor.MiddleRight).childForceExpandWidth = true;
            UiKit.Button("Назад", buttons, () => Show(Page.Title)).Size(52);
        }

        /// <summary>A Gu's skill in a line: what it costs and does.</summary>
        static string SkillLine(GuKind kind) => kind.Skill switch
        {
            SkillKind.Moonblade => $"{kind.Cost:0}% эссенции за бросок, режет на {kind.Reach:0} м и гаснет через {kind.Range:0} м, отдых {kind.Cooldown:0.0} с",
            SkillKind.JadeSkin => $"поднять — {kind.Cost:0}% эссенции, держать — {kind.Upkeep * 60:0}% в час, удары тоже берут эссенцию",
            _ => "—",
        };

        /// <summary>How often a Gu eats, in words.</summary>
        static string MealLine(double minutes)
        {
            var hours = minutes / 60;

            return hours >= 24 ? $"раз в {Days((int)Math.Round(hours / 24))}" : hours == 12 ? "дважды в день" : $"каждые {hours:0} ч";
        }

        static string Days(int n) => n == 1 ? "день" : ItemBook.Count(n, "день", "дня", "дней");

        void Confirm()
        {
            Backdrop();
            var card = UiKit.Panel("Confirm", pageRoot);
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -400, -170, -400, -170);
            var column = UiKit.Rect("Column", card.transform).Fill();
            column.Stack<VerticalLayoutGroup>(14, new RectOffset(44, 44, 36, 32));

            UiKit.Label(confirmTitle, column, 32, UiKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Size(46);
            UiKit.Label(confirmText, column, 19, UiKit.Muted).Size(64);
            var space = UiKit.Rect("Space", column);
            space.Size(-1).flexibleHeight = 1;

            var buttons = UiKit.Rect("Buttons", column);
            buttons.Stack<HorizontalLayoutGroup>(10).childForceExpandWidth = true;
            buttons.Size(54);
            UiKit.Button("Отмена", buttons, () => Show(confirmBack)).Size(54, -1, 1);
            UiKit.Button(confirmButton, buttons, () => confirmAction?.Invoke(), ButtonKind.Danger).Size(54, -1, 1);
        }

        /// <summary>The conversation: a band across the foot of the screen, the questions on its right.</summary>
        void Dialogue()
        {
            var band = UiKit.Panel("Dialogue", pageRoot, UiKit.Hex(0x121010, 0.95f));
            band.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), 120, 40, 120, -340);
            var left = UiKit.Rect("Words", band.transform).Place(Vector2.zero, new Vector2(0.6f, 1));
            left.Stack<VerticalLayoutGroup>(6, new RectOffset(40, 24, 28, 24));
            UiKit.Label(speaker, left, 28, UiKit.GoldLit, TextAnchor.MiddleLeft, FontStyle.Bold).Size(36);
            UiKit.Caps(speakerTitle ?? "", left, 14, UiKit.Muted).Size(22);
            UiKit.Image("Rule", left, UiKit.Line).Size(2);
            UiKit.Label(spoken, left, 21).Size(-1).flexibleHeight = 1;

            var right = UiKit.Rect("Answers", band.transform).Place(new Vector2(0.6f, 0), Vector2.one);
            right.Stack<VerticalLayoutGroup>(7, new RectOffset(16, 34, 24, 22), TextAnchor.MiddleLeft);

            foreach (var (text, act) in answers)
            {
                UiKit.Button(text, right, act, ButtonKind.Plain, 18).Size(46);
            }
        }
    }
}
