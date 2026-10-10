using GuWorld.Body;
using GuWorld.Engine;
using GuWorld.Physics;
using GuWorld.Render;
using GuWorld.State;
using GuWorld.Systems;
using GuWorld.UI;
using GuWorld.Village;
using GuWorld.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using Pose = GuWorld.State.Pose;

namespace GuWorld
{
    /// <summary>
    /// GU World put together (ported from client/game.ts and client/main.ts):
    /// loads the saved game — a new game on the first morning, a saved one
    /// from its saved time and place, repaired where it had to be — builds
    /// the hero, the camera, the light and the world around them, and runs
    /// the clock, the sky, autosaving and the menus (Menus).
    ///
    /// The game opens on the main menu over the living world: the hero stands
    /// in the evening light while the camera circles them. From there it goes
    /// on with the saved game, or starts a new one by making the hero (gender,
    /// hairstyle, beard; fifteen, black-haired). While playing, Esc pauses;
    /// pausing saves, and so do going back to the main menu and quitting.
    /// While paused nothing in the world moves (Time.timeScale is 0).
    ///
    /// Put it on an empty object in an empty scene: it makes everything else.
    /// </summary>
    public class GuWorldGame : MonoBehaviour, IMenuHost
    {
        /// <summary>Seconds of play between saves.</summary>
        const float AutosaveEvery = 30;
        /// <summary>The main menu's light: a golden evening (0.5 is noon).</summary>
        const float TitleTimeOfDay = 0.7f;

        enum Mode
        {
            /// <summary>The main menu: the hero stands, the camera circles, the clock waits.</summary>
            Title,
            /// <summary>Making the hero: the hero stands for the camera.</summary>
            Create,
            Playing,
            Paused,
            /// <summary>Talking to a villager: the world goes on, the cursor is free for the answers.</summary>
            Talking,
        }

        public LocationSettings Location = LocationSettings.TestGrounds;
        public ChunkSettings Chunks = ChunkSettings.Default;
        public Material GroundMaterial;
        public Material BlockMaterial;
        public Material StoneMaterial;
        public Material HeroMaterial;
        /// <summary>The bodies: human-male.glb and human-female.glb as glTFast imports them.</summary>
        public GameObject HeroModel;
        public GameObject HeroModelFemale;
        /// <summary>Opaque two-sided URP Lit (hair, clothes) and see-through URP Lit (stubble), copied per use.</summary>
        public Material ClothMaterial;
        public Material FaintMaterial;
        /// <summary>The proving ground loads its ground and things chunk by chunk; a built location (Qing Mao) has its own terrain.</summary>
        public bool UseChunks = true;
        /// <summary>The haze (m) and how far the camera sees.</summary>
        public float FogStart = 60;
        public float FogEnd = 260;
        public float FarClip = 900;

        WorldClock clock;
        PlayerController player;
        HeroMotor body;
        HeroView picture;
        ThirdPersonCamera view;
        ChunkManager world;
        DaySky sky;
        Atmosphere atmosphere;
        Villagers villagers;
        Villager talkingTo;
        int asked;
        Menus menus;
        /// <summary>The debug panel's weather (R cycles it); null: the world's own.</summary>
        GuWorld.World.Sky? forcedWeather;
        /// <summary>For pictures: a time of day held fixed (`-time 0.9`).</summary>
        float? forcedTime;
        Mode mode;
        Look look;
        bool saved;
        float sinceSave;
        bool debug;
        float fps;
        MenuSkin skin;
        float titleAngle;
        GuWorld.World.Weather weather = GuWorld.World.Weather.Fair;
        float creatorAngle;

        /// <summary>
        /// `-screenshot &lt;file&gt;` on the command line: shows a screen for a moment,
        /// saves a picture of it there and quits (for checking a build by eye). It plays
        /// unless `-title`, `-create` (with `-face` close up), `-pause` or `-settings &lt;tab&gt;`
        /// says otherwise; `-look gender,hair,beard` picks the look; `-walk` / `-run` move.
        /// Nothing is saved.
        /// </summary>
        string screenshot;
        int screenshotFrame;
        bool talkAt;
        /// <summary>`-view x,y,z,yaw,pitch` (with -screenshot): the camera held there, looking that way (degrees).</summary>
        Pose? fixedView;

        public bool CreatorFace { get; set; }
        public ThirdPersonCamera View => view;
        public Look Look => look;
        public bool HasSave => saved;
        public string SaveSummary => $"День {clock.Day} · {clock.Label} · {Location.title}";

        void Awake()
        {
            // Unity objects have their own null: `??` would not see a missing one.
            if (GroundMaterial == null) GroundMaterial = Lit("Ground", 0x8c9478);
            if (BlockMaterial == null) BlockMaterial = Lit("Block", 0xb9b4aa);
            if (StoneMaterial == null) StoneMaterial = Lit("Stone", 0x8f8c84);
            if (HeroMaterial == null) HeroMaterial = Lit("Hero", 0x4a6b8a);
            if (ClothMaterial == null) ClothMaterial = Lit("Cloth", 0xffffff);
            if (FaintMaterial == null) FaintMaterial = ClothMaterial;

            // The proving ground's grid, as on the web.
            if (UseChunks)
            {
                var grid = GroundGrid.Make();
                GroundMaterial.mainTexture = grid;

                if (GroundMaterial.HasProperty("_BaseMap"))
                {
                    GroundMaterial.SetTexture("_BaseMap", grid);
                }
            }

            var args = System.Environment.GetCommandLineArgs();
            var at = System.Array.IndexOf(args, "-screenshot");

            if (at >= 0 && at + 1 < args.Length)
            {
                screenshot = args[at + 1];
            }

            var restored = screenshot != null ? SaveGame.NewGame(Location) : SaveGame.Load(Location);
            clock = restored.clock;
            saved = restored.look.IsChosen;

            foreach (var problem in restored.problems)
            {
                Debug.LogWarning($"GU World: {problem}");
            }

            look = restored.look.IsChosen ? restored.look : Look.Default(Gender.Male);

            if (screenshot != null)
            {
                look = LookFromArgs(args) ?? look;
            }

            BuildLight();
            BuildCamera();
            BuildHero(restored.player);
            BuildWorld();

            // The ground under the hero must be there before they are put on it.
            world?.LoadAround(new Vector3(restored.player.x, 0, restored.player.z));

            // A village with a plan has its people.
            var layout = FindAnyObjectByType<VillageLayout>();

            if (layout != null)
            {
                villagers = Villagers.Create(layout, picture.Parts);
            }
            GameSettings.Apply(view);
            view.GetComponent<Camera>().farClipPlane = FarClip;
            sky.FogStart = FogStart;
            sky.FogEnd = FogEnd;

            menus = Menus.Create(this);

            if (restored.problems.Count > 0)
            {
                menus.Notify("Сохранение восстановлено");
            }

            if (screenshot != null)
            {
                var viewAt = System.Array.IndexOf(args, "-view");

                if (viewAt >= 0 && viewAt + 1 < args.Length)
                {
                    var v = System.Array.ConvertAll(args[viewAt + 1].Split(','), s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
                    fixedView = new Pose { x = v[0], y = v[1], z = v[2], yaw = v[3] };
                    viewPitch = v.Length > 4 ? v[4] : 15;
                }

                player.AutoWalk = Has(args, "-run") ? 2 : Has(args, "-walk") ? 1 : 0;
                talkAt = Has(args, "-talk");
                var timeAt = System.Array.IndexOf(args, "-time");

                if (timeAt >= 0 && timeAt + 1 < args.Length)
                {
                    forcedTime = float.Parse(args[timeAt + 1], System.Globalization.CultureInfo.InvariantCulture);
                }

                var weatherAt = System.Array.IndexOf(args, "-weather");

                if (weatherAt >= 0 && weatherAt + 1 < args.Length && System.Enum.TryParse<GuWorld.World.Sky>(args[weatherAt + 1], true, out var forced))
                {
                    forcedWeather = forced;
                }
                CreatorFace = Has(args, "-face");

                if (Has(args, "-title")) Enter(Mode.Title);
                else if (Has(args, "-create")) Enter(Mode.Create);
                else if (Has(args, "-pause")) { Enter(Mode.Playing); Enter(Mode.Paused); }
                else if (Has(args, "-settings"))
                {
                    Enter(Mode.Title);
                    var tab = System.Array.IndexOf(args, "-settings") + 1 < args.Length && int.TryParse(args[System.Array.IndexOf(args, "-settings") + 1], out var t) ? t : 0;
                    menus.ShowSettings(tab, Menus.Page.Title);
                }
                else Enter(Mode.Playing);

                // A held view: the hero's camera lets go of it.
                if (fixedView.HasValue)
                {
                    view.Following = false;
                }
            }
            else
            {
                Enter(Mode.Title);
                menus.FadeIn();
            }
        }

        static bool Has(string[] args, string flag) => System.Array.IndexOf(args, flag) >= 0;

        static Look LookFromArgs(string[] args)
        {
            var at = System.Array.IndexOf(args, "-look");

            if (at < 0 || at + 1 >= args.Length)
            {
                return null;
            }

            var parts = args[at + 1].Split(',');

            return new Look
            {
                gender = parts.Length > 0 ? parts[0] : "male",
                hair = parts.Length > 1 ? parts[1] : "",
                beard = parts.Length > 2 ? parts[2] : "none",
            };
        }

        void BuildLight()
        {
            var sun = FindAnyObjectByType<Light>();

            if (sun == null || sun.type != LightType.Directional)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.shadows = LightShadows.Soft;
            sky = gameObject.AddComponent<DaySky>();
            sky.Sun = sun;

            // Under a real sky the moon lights the night too (a second, shadowless light).
            if (RenderSettings.skybox != null)
            {
                var moon = new GameObject("Moon Light").AddComponent<Light>();
                moon.type = LightType.Directional;
                moon.shadows = LightShadows.None;
                moon.intensity = 0;
                sky.Moon = moon;
            }

            atmosphere = FindAnyObjectByType<Atmosphere>();
        }

        void BuildCamera()
        {
            var camera = Camera.main;

            if (camera == null)
            {
                camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
                camera.gameObject.AddComponent<AudioListener>();
            }

            view = camera.gameObject.GetComponent<ThirdPersonCamera>();

            if (view == null)
            {
                view = camera.gameObject.AddComponent<ThirdPersonCamera>();
            }

            sky.View = camera;
            // Bloom and tone mapping, where the location has a volume for them.
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        void BuildHero(Pose pose)
        {
            var hero = new GameObject("Hero");
            hero.SetActive(false);
            hero.AddComponent<CharacterController>();
            body = hero.AddComponent<HeroMotor>();
            body.Bounds = Location.bounds;
            player = hero.AddComponent<PlayerController>();
            player.Camera = view;
            player.Spawn = new Vector3(pose.x, pose.y, pose.z);
            player.SpawnYaw = pose.yaw;
            view.Ignore = hero.GetComponent<CharacterController>();
            picture = hero.AddComponent<HeroView>();
            picture.Parts = new HumanBody.Parts
            {
                male = HeroModel,
                female = HeroModelFemale,
                cloth = ClothMaterial,
                faint = FaintMaterial,
            };
            picture.Camera = view;
            hero.SetActive(true);
            Wear(look);
        }

        /// <summary>Builds the hero's body for a look.</summary>
        public void Wear(Look next)
        {
            look = next;
            picture.Wear(look);
            player.EyeScale = picture.Scale;
        }

        void BuildWorld()
        {
            if (!UseChunks)
            {
                return;
            }

            world = new GameObject("World").AddComponent<ChunkManager>();
            world.Settings = Chunks;
            world.Location = Location;
            world.Focus = player.transform;
            world.GroundMaterial = GroundMaterial;
            world.BlockMaterial = BlockMaterial;
            world.StoneMaterial = StoneMaterial;
        }

        /// <summary>Goes to a mode: the pause, the cursor, the camera and the menu page follow from it.</summary>
        void Enter(Mode next)
        {
            var was = mode;
            mode = next;

            var playing = next == Mode.Playing;
            Time.timeScale = next == Mode.Paused ? 0 : 1;
            view.Following = playing || next == Mode.Paused || next == Mode.Talking;

            if (playing && screenshot == null)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (!playing)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (playing && (was == Mode.Create || was == Mode.Title))
            {
                player.PlaceCamera();
            }

            menus.Show(next switch
            {
                Mode.Title => Menus.Page.Title,
                Mode.Create => Menus.Page.Creator,
                Mode.Paused => Menus.Page.Pause,
                // The dialogue page comes with its words (StartTalk).
                Mode.Talking => menus.Current,
                _ => Menus.Page.None,
            });

            if (next != Mode.Playing)
            {
                menus.SetPrompt(null);
            }
        }

        // ── What the menus ask ────────────────────────────────────────

        public void Continue() => Enter(Mode.Playing);

        /// <summary>Plays (the smoke test's way in too): from the main menu, the hero's making or the pause.</summary>
        public void Play()
        {
            var made = mode == Mode.Create;
            Enter(Mode.Playing);

            if (made)
            {
                Save();
            }
        }

        public void StartPlaying() => Play();

        public void Resume() => Enter(Mode.Playing);

        public void NewGame()
        {
            SaveGame.Delete();
            saved = false;
            clock = WorldClock.NewGame();
            var start = SaveGame.NewGame(Location).player;
            player.Spawn = new Vector3(start.x, start.y, start.z);
            player.SpawnYaw = start.yaw;
            player.Respawn();
            creatorAngle = 0;
            Wear(Look.Default(look.Gender));
            Enter(Mode.Create);
        }

        public void ResetGame() => menus.FadeThrough(NewGame);

        public void BackToTitle()
        {
            if (mode == Mode.Playing || mode == Mode.Paused)
            {
                Save();
            }

            Enter(Mode.Title);
        }

        /// <summary>Begins a conversation with a villager: they stop and turn; their greeting and the questions show.</summary>
        void StartTalk(Villager who)
        {
            talkingTo = who;
            who.BeginTalk(body.transform.position);
            Enter(Mode.Talking);
            var hour = Mathf.FloorToInt((float)clock.MinuteOfDay / 60);
            Say(Talk.Greeting(who, hour, weather.Kind, look.Gender));
        }

        void Say(string text)
        {
            var who = talkingTo;

            if (who == null)
            {
                return;
            }

            menus.ShowDialogue(who.Spec.Name, who.Spec.Title, text, new (string, System.Action)[]
            {
                ("Кто вы?", () => Say(Talk.About(who))),
                ("Что нового?", () => Say(Talk.News(who, clock.Day))),
                ("Расскажите о деревне", () => Say(Talk.Village(who, asked++))),
                ("До свидания", () => menus.ShowDialogue(who.Spec.Name, who.Spec.Title, Talk.Farewell(who, look.Gender), new (string, System.Action)[] { ("Уйти", EndTalk) })),
            });
        }

        public void EndTalk()
        {
            talkingTo?.EndTalk();
            talkingTo = null;

            if (mode == Mode.Talking)
            {
                Enter(Mode.Playing);
            }
        }

        public void Quit()
        {
            if (mode == Mode.Playing || mode == Mode.Paused)
            {
                Save();
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── Each frame ────────────────────────────────────────────────

        void Update()
        {
            if (screenshot != null)
            {
                screenshotFrame++;

                // `-talk`: the hero before the nearest villager, talking.
                if (screenshotFrame == 110 && talkAt && villagers != null)
                {
                    Villager nearest = null;

                    foreach (var person in villagers.People)
                    {
                        if (!person.Asleep && (nearest == null || Vector3.Distance(person.transform.position, body.transform.position) < Vector3.Distance(nearest.transform.position, body.transform.position)))
                        {
                            nearest = person;
                        }
                    }

                    if (nearest != null)
                    {
                        var front = nearest.transform.position + nearest.transform.forward * 1.8f;
                        body.PlaceAt(front);
                        body.Facing = Mathf.Atan2(-nearest.transform.forward.x, -nearest.transform.forward.z);
                        view.Yaw = body.Facing + Mathf.PI;
                        StartTalk(nearest);
                    }
                }

                if (screenshotFrame == 150)
                {
                    ScreenCapture.CaptureScreenshot(screenshot);
                }
                else if (screenshotFrame == 160)
                {
                    Application.Quit();
                }
            }

            var keyboard = Keyboard.current;
            var wasPlaying = mode == Mode.Playing;
            weather = forcedWeather.HasValue ? GuWorld.World.Weather.Of(forcedWeather.Value)
                : mode == Mode.Title ? GuWorld.World.Weather.Fair
                : GuWorld.World.Weather.At(clock.Minutes);

            // Esc while playing (PlayerController frees the cursor) pauses.
            if (mode == Mode.Playing && screenshot == null && Cursor.lockState != CursorLockMode.Locked)
            {
                Enter(Mode.Paused);
                Save();
            }

            if (keyboard != null)
            {
                if (keyboard.f3Key.wasPressedThisFrame) debug = !debug;

                // Debug: move the clock to see the light change, and go through the weathers.
                if (debug && keyboard.tKey.wasPressedThisFrame) clock.Skip(1);

                if (debug && keyboard.rKey.wasPressedThisFrame)
                {
                    forcedWeather = forcedWeather switch
                    {
                        null => GuWorld.World.Sky.Clear,
                        GuWorld.World.Sky.Clear => GuWorld.World.Sky.Cloudy,
                        GuWorld.World.Sky.Cloudy => GuWorld.World.Sky.Rain,
                        _ => null,
                    };
                }

                // Esc on a menu steps back (not the Esc that just paused).
                if (!wasPlaying && keyboard.escapeKey.wasPressedThisFrame)
                {
                    menus.Back();
                }
            }

            // The village lives by the clock (the main menu's by its evening).
            var hourOfDay = (forcedTime ?? (mode == Mode.Title ? TitleTimeOfDay : clock.TimeOfDay)) * 24;
            villagers?.Tick(hourOfDay, Time.deltaTime, view.transform.position);

            if (mode == Mode.Playing && villagers != null && screenshot == null)
            {
                var before = villagers.Before(body.transform.position, view.transform.forward);
                menus.SetPrompt(before != null ? $"[E]  Поговорить — {before.Spec.Name}" : null);

                if (before != null && keyboard != null && keyboard.eKey.wasPressedThisFrame)
                {
                    StartTalk(before);
                }
            }

            if (mode == Mode.Playing || mode == Mode.Talking)
            {
                clock.Advance(Time.deltaTime);
                sinceSave += Time.deltaTime;

                if (sinceSave >= AutosaveEvery)
                {
                    Save();
                }

                menus.SetHud($"<b>День {clock.Day}</b> · {clock.Label} · {PhaseName(clock.Phase)} · {GuWorld.World.Weather.Name(weather.Kind)}\n{Location.title}");
            }

            // The main menu's own light and fair sky; the game's clock and weather everywhere else.
            var time = forcedTime ?? (mode == Mode.Title ? TitleTimeOfDay : clock.TimeOfDay);
            sky.Apply(time, weather);
            atmosphere?.Apply(sky, weather, time, view.transform);

            if (Time.unscaledDeltaTime > 0)
            {
                fps = Mathf.Lerp(fps, 1 / Time.unscaledDeltaTime, 0.05f);
            }
        }

        float viewPitch;

        void LateUpdate()
        {
            if (fixedView.HasValue)
            {
                var v = fixedView.Value;
                view.transform.SetPositionAndRotation(new Vector3(v.x, v.y, v.z), Quaternion.Euler(viewPitch, v.yaw, 0));

                return;
            }

            if (mode == Mode.Title)
            {
                TitleCamera();
            }
            else if (mode == Mode.Create)
            {
                CreatorCamera();
            }
        }

        /// <summary>The main menu: the camera circles the hero slowly, low, the hero right of the middle.</summary>
        void TitleCamera()
        {
            titleAngle += Time.unscaledDeltaTime * 0.07f;
            var height = HeroView.HeightOf(look.Gender);
            var facing = body.Facing + 0.55f + 0.35f * Mathf.Sin(titleAngle);
            var direction = new Vector3(Mathf.Sin(facing), 0, Mathf.Cos(facing));
            var target = body.transform.position + Vector3.up * height * 0.62f;
            var aside = Vector3.Cross(Vector3.up, direction).normalized * 0.9f;
            var camera = view.transform;
            camera.position = target + direction * 3.4f + Vector3.up * 0.1f + aside;
            camera.rotation = Quaternion.LookRotation(target + aside - camera.position, Vector3.up);
        }

        /// <summary>Making the hero: the camera in front of them, turned round by dragging right of the panel.</summary>
        void CreatorCamera()
        {
            var mouse = Mouse.current;

            if (mouse != null && mouse.leftButton.isPressed && mouse.position.ReadValue().x > menus.CreatorPanelWidth)
            {
                creatorAngle -= mouse.delta.ReadValue().x * 0.008f;
            }

            var height = HeroView.HeightOf(look.Gender);
            var target = body.transform.position + Vector3.up * (CreatorFace ? height * 0.93f : height * 0.55f);
            var distance = CreatorFace ? 0.8f : 3.1f;
            var facing = body.Facing + creatorAngle;
            var direction = new Vector3(Mathf.Sin(facing), 0, Mathf.Cos(facing));
            // Seen a little from the right of the screen's middle, leaving room for the panel.
            var aside = Vector3.Cross(Vector3.up, direction).normalized * (CreatorFace ? 0.18f : 0.75f);
            var camera = view.transform;
            camera.position = target + direction * distance + Vector3.up * (CreatorFace ? 0.02f : 0.25f) + aside;
            camera.rotation = Quaternion.LookRotation(target + aside - camera.position, Vector3.up);
        }

        void OnDestroy()
        {
            // Leaving play mode in the editor must not leave the editor paused, or its render pipeline changed.
            Time.timeScale = 1;
            GameSettings.Restore();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && mode == Mode.Playing && screenshot == null)
            {
                Enter(Mode.Paused);
                Save();
            }
        }

        void OnApplicationQuit()
        {
            if (mode == Mode.Playing || mode == Mode.Paused)
            {
                Save();
            }
        }

        void Save()
        {
            // Nothing to save before the hero is made, or while checking by eye.
            if (clock == null || body == null || screenshot != null || mode == Mode.Create)
            {
                return;
            }

            sinceSave = 0;
            var ok = SaveGame.Write(new Save
            {
                location = Location.id,
                world_minutes = clock.Minutes,
                player = SaveGame.ToSaved(body.transform.position, body.Facing),
                look = look,
            });

            saved |= ok;
            menus.Notify(ok ? "Сохранено" : "Не удалось сохранить — попробуем снова");
        }

        static string PhaseName(DayPhase phase) => phase switch
        {
            DayPhase.Night => "ночь",
            DayPhase.Dawn => "рассвет",
            DayPhase.Morning => "утро",
            DayPhase.Day => "день",
            _ => "вечер",
        };

        void OnGUI()
        {
            if (!debug)
            {
                return;
            }

            skin ??= new MenuSkin();
            var p = body.transform.position;
            GUILayout.BeginArea(new Rect(16, 90, 380, 200), skin.Panel);
            GUILayout.Label(
                $"FPS {fps:0}\n" +
                $"x {p.x:0.00}  y {p.y:0.00}  z {p.z:0.00}\n" +
                $"скорость {body.HorizontalSpeed:0.0} м/с · {body.Stance} · {(body.Grounded ? "на земле" : "в воздухе")}\n" +
                (world != null ? $"чанков загружено {world.LoadedCount} (по {Chunks.size} м)\n" : $"{Location.title}\n") +
                $"погода: {GuWorld.World.Weather.Name(weather.Kind)}{(forcedWeather.HasValue ? " (вручную)" : "")}\n" +
                "T — +1 час · R — погода",
                skin.Small);
            GUILayout.EndArea();
        }

        /// <summary>A plain URP material, for when none was given.</summary>
        static Material Lit(string name, int rgb)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name };
            var color = new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);
            material.color = color;

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.15f);
            }

            return material;
        }
    }
}
