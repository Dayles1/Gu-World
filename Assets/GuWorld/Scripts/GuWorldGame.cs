using System.Linq;
using GuWorld.Body;
using GuWorld.Combat;
using GuWorld.Cultivation;
using GuWorld.Engine;
using GuWorld.Items;
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
    /// hairstyle, hanfu; fifteen, black-haired, beardless). While playing, Esc pauses;
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
        /// <summary>The people of the hero's own village (the named ones and its crowd).</summary>
        Villagers villagers;
        /// <summary>Every village's people (the other clans' too).</summary>
        readonly System.Collections.Generic.List<Villagers> crowds = new System.Collections.Generic.List<Villagers>();
        Cultivator cultivator;
        Health health;
        readonly Quests quests = new Quests();
        readonly Trade trade = new Trade();
        /// <summary>The shop the hero is at (while its page is open).</summary>
        Shop shop;
        LocalMap map;
        /// <summary>The minimap's finer map round the hero, drawn again when they have walked far from its middle.</summary>
        LocalMap nearMap;
        const float NearSpan = 900;
        /// <summary>The bed being gathered, and how long E has been held on it.</summary>
        HerbBed gathering;
        float gatherHeld;
        Villager talkingTo;
        int asked;
        Menus menus;
        HitboxView hitboxes;
        Insects insects;
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
        /// <summary>`-bench N`: N frames timed before the picture, written beside it (`.txt`).</summary>
        int benchFrames;
        readonly System.Collections.Generic.List<float> frameTimes = new System.Collections.Generic.List<float>();
        readonly System.Collections.Generic.Dictionary<string, (Unity.Profiling.ProfilerRecorder recorder, double sum)> counters = new System.Collections.Generic.Dictionary<string, (Unity.Profiling.ProfilerRecorder, double)>();
        double gpuSum;
        int qualityBefore = -1;
        /// <summary>`-views "x,y,z,yaw,pitch,file;…"`: more pictures after the first, from these views (above the ground with `-ground`).</summary>
        readonly System.Collections.Generic.List<(Pose pose, float pitch, string file)> moreViews = new System.Collections.Generic.List<(Pose, float, string)>();
        int gpuCount;

        /// <summary>`-off a,b`: parts of the world left out of a timed run, to find what costs the frames.</summary>
        void LeaveOut(string list)
        {
            foreach (var part in list.Split(','))
            {
                switch (part.Trim())
                {
                    case "trees":
                        foreach (var t in Terrain.activeTerrains) t.treeDistance = 0;
                        Render.TreeInstancer.Distance = 0;
                        break;
                    case "grass":
                        foreach (var t in Terrain.activeTerrains) t.detailObjectDistance = 0;
                        break;
                    case "shadows":
                        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.shadows = LightShadows.None;
                        break;
                    case "msaa":
                        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp) urp.msaaSampleCount = 1;
                        break;
                    case "post":
                        foreach (var v in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None)) v.enabled = false;
                        break;
                    case "people":
                        foreach (var v in FindObjectsByType<Villagers>(FindObjectsSortMode.None)) v.gameObject.SetActive(false);
                        break;
                    default:
                        // A part of the mountain by its root's name: "Giant Trees", "Village gu_yue", "Hamlets", "Trails", "Water".
                        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                        {
                            if (go.name.Replace(" ", "").ToLowerInvariant().Contains(part.Trim().Replace(" ", "").ToLowerInvariant()))
                            {
                                go.SetActive(false);
                            }
                        }

                        break;
                }
            }
        }

        void StartCounters()
        {
            foreach (var (category, name) in new[] { (Unity.Profiling.ProfilerCategory.Internal, "Main Thread"), (Unity.Profiling.ProfilerCategory.Render, "Batches Count"), (Unity.Profiling.ProfilerCategory.Render, "SetPass Calls Count"), (Unity.Profiling.ProfilerCategory.Render, "Triangles Count"), (Unity.Profiling.ProfilerCategory.Render, "Shadow Casters Count"), (Unity.Profiling.ProfilerCategory.Render, "Visible Skinned Meshes Count") })
            {
                counters[name] = (Unity.Profiling.ProfilerRecorder.StartNew(category, name), 0);
            }
        }
        bool talkAt;
        string screenshotPage;
        bool castAt;
        /// <summary>`-cast 12`: how many frames before the picture the blade is thrown.</summary>
        int castFrames = 5;
        /// <summary>`-view x,y,z,yaw,pitch` (with -screenshot): the camera held there, looking that way (degrees).</summary>
        Pose? fixedView;
        /// <summary>`-ground` (with -view): its height is above the ground there.</summary>
        bool viewAboveGround;

        public bool CreatorFace { get; set; }
        public ThirdPersonCamera View => view;
        public Cultivator Cultivator => cultivator;
        public HumanBody.Parts Parts => picture.Parts;
        public Health Health => health;
        public Quests Quests => quests;
        public Trade Trade => trade;
        public Shop Shop => shop;
        public LocalMap Map => map;
        public LocalMap NearMap => nearMap;
        public Vector3 HeroAt => body.transform.position;
        public float HeroFacing => body.Facing;
        public double Now => clock.Minutes;
        public Look Look => look;
        public bool HasSave => saved;
        public string SaveSummary => $"{look.FullName} · Ранг {cultivator.Aperture.Rank} · Деревня Гу Юэ · День {clock.Day}, {clock.Label}";
        public string PlaceName => Location.id == QingMao.Id ? $"{Atlas.QingMao.Name} · {Where(HeroAt)}" : Location.title;

        /// <summary>Where on the mountain a point is: the settlement it is in or near, else the mountain's side.</summary>
        static string Where(Vector3 at)
        {
            var p = new Vector2(at.x, at.z);

            foreach (var place in QingMao.Settlements)
            {
                if (Vector2.Distance(p, place.Centre) < place.Radius * 1.35f)
                {
                    return place.Name.ToLowerInvariant();
                }
            }

            foreach (var place in QingMao.Settlements)
            {
                if (Vector2.Distance(p, place.Centre) < place.Radius * 3 + 300)
                {
                    return $"окрестности: {place.Name.ToLowerInvariant()}";
                }
            }

            if (Vector2.Distance(p, QingMao.Peak) < 900) return "вершина";
            if (Vector2.Distance(p, QingMao.ForePeak) < 1300) return "передняя гора";

            return p.y > 3500 ? "задняя гора" : "склоны горы";
        }
        public string TimeLine => $"{PhaseName(clock.Phase).ToUpperInvariant()} · ДЕНЬ {clock.Day} · {clock.Label} · {GuWorld.World.Weather.Name(weather.Kind)}";

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
                var benchAt = System.Array.IndexOf(args, "-bench");

                if (benchAt >= 0 && benchAt + 1 < args.Length)
                {
                    int.TryParse(args[benchAt + 1], out benchFrames);
                }
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

            // A village with a plan has its people: the hero's own clan's village first.
            VillageLayout layout = null;
            var layouts = FindObjectsByType<VillageLayout>(FindObjectsSortMode.None);

            foreach (var candidate in layouts)
            {
                if (layout == null || candidate.Settlement == "gu_yue")
                {
                    layout = candidate;
                }
            }

            foreach (var each in layouts)
            {
                // The hero's own village: its named people and a crowd; the other clans' villages, crowds of their own.
                var specs = new System.Collections.Generic.List<VillagerSpec>();

                if (each == layout)
                {
                    specs.AddRange(Roster.People());
                }

                var count = each.Settlement == "gu_yue" ? 80 : each.Settlement == null ? 0 : 34;
                specs.AddRange(Crowd.Of(each.Settlement ?? "gu_yue", count, (each.Settlement ?? "").GetHashCode()));
                var people = Villagers.Create(each, picture.Parts, specs);
                crowds.Add(people);

                if (each == layout)
                {
                    villagers = people;
                }
            }
            // `-quality N` (with a picture): the graphics level for this run, not saved.
            var qualityAt = System.Array.IndexOf(args, "-quality");

            if (screenshot != null && qualityAt >= 0 && qualityAt + 1 < args.Length && int.TryParse(args[qualityAt + 1], out var quality))
            {
                qualityBefore = GameSettings.Quality;
                GameSettings.Quality = quality;
            }

            // The terrains' trees drawn in bulk (before the graphics level is applied: it sets how far).
            if (Render.TreeInstancer.Current == null)
            {
                gameObject.AddComponent<Render.TreeInstancer>();
            }

            GameSettings.Apply(view);
            view.GetComponent<Camera>().farClipPlane = FarClip;
            sky.FogStart = FogStart;
            sky.FogEnd = FogEnd;

            cultivator.Load(restored.cultivation, clock.Minutes);
            health.Set(restored.health < 0 ? Health.Max : restored.health);
            quests.Load(restored.quests);
            menus = Menus.Create(this);
            cultivator.Said += menus.Notify;
            cultivator.Did += quests.Count;
            quests.Said += menus.Notify;
            health.Fainted += OnFainted;
            TrainingDummy.Struck += OnStruck;
            Wounds.Landed += OnWound;
            hitboxes = HitboxView.Create(view.transform);
            HitboxView.On = GameSettings.ShowHitboxes;
            insects = Insects.Create();
            TreeBranch.Creaking += OnCreak;
            TreeBranch.Snapped += OnSnap;
            PlaceDummies(layout);
            PlaceBeds(layout, restored.beds);
            DrawMap(layout);

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
                    viewAboveGround = Has(args, "-ground");
                }

                var viewsAt = System.Array.IndexOf(args, "-views");

                if (viewsAt >= 0 && viewsAt + 1 < args.Length)
                {
                    foreach (var one in args[viewsAt + 1].Split(';'))
                    {
                        var parts = one.Split(',');

                        if (parts.Length >= 6)
                        {
                            float F(int i) => float.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
                            moreViews.Add((new Pose { x = F(0), y = F(1), z = F(2), yaw = F(3) }, F(4), string.Join(",", parts, 5, parts.Length - 5)));
                        }
                    }

                    viewAboveGround |= Has(args, "-ground");
                }

                player.AutoWalk = Has(args, "-run") ? 2 : Has(args, "-walk") ? 1 : 0;
                talkAt = Has(args, "-talk");
                castAt = Has(args, "-cast");
                var castFramesAt = System.Array.IndexOf(args, "-cast");
                castFrames = castFramesAt >= 0 && castFramesAt + 1 < args.Length && int.TryParse(args[castFramesAt + 1], out var cf) ? cf : 5;
                HitboxView.On |= Has(args, "-hitboxes");
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

                // `-back`: the hero's making seen from behind.
                if (Has(args, "-back"))
                {
                    creatorAngle = Mathf.PI;
                }

                if (Has(args, "-title")) Enter(Mode.Title);
                else if (Has(args, "-create")) Enter(Mode.Create);
                else if (Has(args, "-pause")) { Enter(Mode.Playing); Enter(Mode.Paused); }
                else if (Has(args, "-settings"))
                {
                    Enter(Mode.Title);
                    var tab = System.Array.IndexOf(args, "-settings") + 1 < args.Length && int.TryParse(args[System.Array.IndexOf(args, "-settings") + 1], out var t) ? t : 0;
                    menus.ShowSettings(tab, Menus.Page.Title);
                }
                else if (Has(args, "-codex")) { Enter(Mode.Title); menus.Show(Menus.Page.Codex); }
                else Enter(Mode.Playing);

                // `-page bag|aperture|map|shop`: one of the hero's pages over the game (opened once the hero stands).
                var pageAt = System.Array.IndexOf(args, "-page");
                screenshotPage = pageAt >= 0 && pageAt + 1 < args.Length ? args[pageAt + 1] : null;

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
                robe = parts.Length > 3 ? parts[3] : "dark",
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
            health = hero.AddComponent<Health>();
            cultivator = hero.AddComponent<Cultivator>();
            cultivator.Camera = view;
            hero.SetActive(true);
            Wear(look);
        }

        /// <summary>Builds the hero's body for a look (beardless: the hero is fifteen).</summary>
        public void Wear(Look next)
        {
            look = next.With(beard: Beard.None);
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

        public void Resume()
        {
            // Leaving a shop ends the talk it was opened from.
            shop = null;

            if (talkingTo != null)
            {
                talkingTo.EndTalk();
                talkingTo = null;
            }

            Enter(Mode.Playing);
        }

        /// <summary>One of the hero's pages (bag, aperture, map): the world waits while it is open.</summary>
        public void OpenPage(Menus.Page page)
        {
            if (mode != Mode.Paused)
            {
                Enter(Mode.Paused);
            }

            menus.Show(page);
        }

        public Deal Buy(System.Collections.Generic.IDictionary<string, int> lots)
        {
            var deal = trade.Buy(shop, lots, cultivator.Bag, clock.Minutes, out var paid);

            if (deal != Deal.Done)
            {
                menus.Notify(Trade.Explain(deal));

                return deal;
            }

            menus.Notify($"Куплено за {ItemBook.Stones(paid)}");

            foreach (var pair in lots)
            {
                if (pair.Value > 0)
                {
                    quests.Count("buy:" + pair.Key, pair.Value * (ItemBook.Find(pair.Key)?.Bundle ?? 1));
                }
            }

            return deal;
        }

        public int Sell(int cell, int count) => trade.Sell(cultivator.Bag, cell, count, clock.Minutes);

        public void Drop(int cell)
        {
            var stack = cultivator.Bag[cell];

            if (stack != null)
            {
                menus.Notify($"Выброшено: {stack.Kind?.Name} ×{stack.count}");
                cultivator.Bag.TakeFrom(cell, stack.count);
            }
        }

        public void NewGame()
        {
            SaveGame.Delete();
            saved = false;
            clock = WorldClock.NewGame();
            var start = SaveGame.NewGame(Location).player;
            player.Spawn = new Vector3(start.x, start.y, start.z);
            player.SpawnYaw = start.yaw;
            player.Respawn();
            cultivator.Load(null, clock.Minutes);
            health.Set(Health.Max);
            quests.Load(null);

            foreach (var bed in HerbBed.All)
            {
                bed.ReadyAt = 0;
            }

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

        /// <summary>Straw dummies to try skills on: in the academy's yard, or (on the proving ground) before the spawn.</summary>
        void PlaceDummies(VillageLayout layout)
        {
            var straw = Lit("Straw", 0xc9b07a);
            var wood = Lit("Post", 0x5a4330);

            if (layout != null && layout.TryPlace("academy yard", out var yard))
            {
                var turn = Quaternion.Euler(0, yard.yaw, 0);

                for (var i = -1; i <= 1; i++)
                {
                    var at = yard.at + turn * new Vector3(i * 3.2f, 0, 7);
                    // Looked for from just above the yard: the academy's eaves reach out over it.
                    TrainingDummy.Make(OnGround(at, 3), yard.yaw + 180, straw, wood);
                }

                return;
            }

            if (UseChunks)
            {
                for (var i = -1; i <= 1; i++)
                {
                    TrainingDummy.Make(OnGround(new Vector3(i * 3.2f, 0, 2)), 0, straw, wood);
                }
            }
        }

        /// <summary>
        /// Moon orchid beds (lore: res_moon_orchid — the clan grows them by its spring): a
        /// little garden on dry, level ground near the village's spring, or (on the proving
        /// ground) beside the spawn. Each bed's regrowth comes from the save, by its order.
        /// </summary>
        void PlaceBeds(VillageLayout layout, double[] saved)
        {
            var blue = Lit("Orchid Blue", 0x8fb4ff);
            var pink = Lit("Orchid Pink", 0xf0a6c8);
            var leaf = Lit("Orchid Leaf", 0x3f6b3a);
            var heart = Lit("Orchid Heart", 0xf4f0d8);
            var spots = new System.Collections.Generic.List<Vector3>();

            if (layout != null && layout.TryPlace("spring", out var spring))
            {
                // Rings round the spring, from near to far: dry ground, not steep, beds apart.
                for (var radius = 9f; radius <= 24 && spots.Count < 6; radius += 2.5f)
                {
                    for (var angle = 0f; angle < 360 && spots.Count < 6; angle += 20)
                    {
                        var at = spring.at + Quaternion.Euler(0, angle, 0) * Vector3.forward * radius;
                        var p = new Vector2(at.x, at.z);

                        var plan = WorldPlan.Current;

                        if (plan != null && (WorldPlan.Distance(plan.Streams, p) < 4 || WorldPlan.Distance(plan.Trails, p) < 1.5f || WorldPlan.Distance(plan.Streets, p) < 1))
                        {
                            continue;
                        }

                        if (!UnityEngine.Physics.Raycast(at + Vector3.up * 60, Vector3.down, out var hit, 200, ~0, QueryTriggerInteraction.Ignore) ||
                            hit.normal.y < 0.94f || hit.collider.GetComponent<TerrainCollider>() == null)
                        {
                            continue;
                        }

                        if (spots.TrueForAll(s => Vector3.Distance(s, hit.point) > 2.6f))
                        {
                            spots.Add(hit.point);
                        }
                    }
                }
            }
            else if (UseChunks)
            {
                for (var i = 0; i < 6; i++)
                {
                    spots.Add(OnGround(new Vector3(6 + i % 3 * 2.6f, 0, 7 + i / 3 * 2.6f)));
                }
            }

            for (var i = 0; i < spots.Count; i++)
            {
                var bed = HerbBed.Make(spots[i], 41 + i * 7, blue, pink, leaf, heart);

                if (saved != null && i < saved.Length && double.IsFinite(saved[i]) && saved[i] <= clock.Minutes + bed.Regrow)
                {
                    bed.ReadyAt = saved[i];
                }
            }
        }

        /// <summary>The location's map for the HUD and the map page, with the village's places marked.</summary>
        void DrawMap(VillageLayout layout)
        {
            var plan = WorldPlan.Current;
            map = plan != null
                ? LocalMap.World(plan, new Rect(Location.bounds.min.x, Location.bounds.min.z, Location.bounds.size.x, Location.bounds.size.z))
                : LocalMap.Grid(Location.bounds);
            RedrawNearMap(true);

            if (layout != null)
            {
                foreach (var (place, label) in new[]
                {
                    ("square", "Площадь"), ("pavilion", "Павильон клана"), ("academy", "Академия"), ("shrine", "Святилище"),
                    ("stall 2", "Лавка Баошаня"), ("stall 0", "Лапшичная"),
                    ("gate north", "Северные ворота"), ("gate south", "Южные ворота"), ("gate east", "Восточные ворота"), ("gate west", "Западные ворота"),
                })
                {
                    if (layout.TryPlace(place, out var spot))
                    {
                        map.Marks.Add(new MapMark { Label = label, At = spot.at });
                    }
                }
            }

            if (HerbBed.All.Count > 0)
            {
                map.Marks.Add(new MapMark { Label = "Лунные орхидеи", At = HerbBed.All[0].transform.position });
            }
        }

        /// <summary>The minimap's map round the hero, again when they have walked a third of it away from its middle.</summary>
        void RedrawNearMap(bool now = false)
        {
            var plan = WorldPlan.Current;

            if (plan == null || body == null)
            {
                return;
            }

            var at = body.transform.position;

            if (!now && nearMap != null && Vector2.Distance(new Vector2(at.x, at.z), nearMap.Area.center) < NearSpan * 0.3f)
            {
                return;
            }

            if (nearMap != null)
            {
                Destroy(nearMap.Texture);
            }

            nearMap = LocalMap.Around(at, NearSpan, plan);

            // The village's places are worth a label at this scale.
            foreach (var mark in map.Marks)
            {
                nearMap.Marks.Add(new MapMark { Label = mark.Label, At = mark.At, Major = true });
            }
        }

        /// <summary>Fainted: the world goes dark, and the hero wakes in the village some hours later, weak.</summary>
        void OnFainted()
        {
            menus.Notify("Вы потеряли сознание");
            menus.FadeThrough(() =>
            {
                body.PlaceAt(player.Spawn);
                body.Facing = player.SpawnYaw;
                view.Yaw = player.SpawnYaw + Mathf.PI;
                clock.Skip(3);
                cultivator.Tick(clock.Minutes);
                health.Set(Health.Max * 0.3f);
                menus.Notify("Вы очнулись в деревне. Прошло три часа");
            });
        }

        /// <summary>A point dropped onto whatever is under it, looked for from `above` metres over it.</summary>
        static Vector3 OnGround(Vector3 at, float above = 100)
        {
            at.y = UnityEngine.Physics.Raycast(new Vector3(at.x, at.y + above, at.z), Vector3.down, out var hit, 400, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : at.y;

            return at;
        }

        void OnStruck(TrainingDummy dummy, BodyPart? part, float damage, bool broke)
        {
            quests.Count("hit_dummy");
            menus.Notify(broke ? "Чучело разрублено!" : $"{Wounds.Line("Чучело", part, damage, false)} (осталось {Mathf.Max(0, dummy.Health):0})");
        }

        void OnCreak(TreeBranch branch)
        {
            if (body != null && body.Branch == branch)
            {
                menus.Notify("Ветка трещит под ногами!");
                view.Shake(0.15f);
            }
            else if (Vector3.Distance(branch.Root, body.transform.position) < 40)
            {
                menus.Notify("Ветка трещит от удара");
            }
        }

        void OnSnap(TreeBranch branch, string why)
        {
            if (Vector3.Distance(branch.Root, body.transform.position) < 60)
            {
                menus.Notify($"Ветка обломилась — {why}");
                view.Shake(0.35f);
            }
        }

        /// <summary>A blow landed on someone (a villager, the hero): what it did, in the log.</summary>
        void OnWound(string who, BodyPart? part, float harm, bool down)
        {
            menus.Notify(Wounds.Line(who, part, harm, down));
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

            var choices = new System.Collections.Generic.List<(string, System.Action)>();
            var name = who.Spec.Name;
            var theirs = Trade.Of(name);

            if (theirs != null)
            {
                choices.Add(("Показать товар", () => OpenShop(theirs)));
            }

            if (quests.Ready && quests.Current.Giver == name)
            {
                choices.Add(("Я выполнил поручение", () => HandIn(who)));
            }
            else if (quests.NextFrom(name) is QuestKind next)
            {
                choices.Add(("Есть ли для меня поручение?", () => Offer(who, next)));
            }

            choices.Add(("Кто вы?", () => Say(Talk.About(who))));
            choices.Add(("Что нового?", () => Say(Talk.News(who, clock.Day))));
            choices.Add(("Расскажите о деревне", () => Say(Talk.Village(who, asked++))));
            choices.Add(("До свидания", () => menus.ShowDialogue(who.Spec.Name, who.Spec.Title, Talk.Farewell(who, look.Gender), new (string, System.Action)[] { ("Уйти", EndTalk) })));
            menus.ShowDialogue(who.Spec.Name, who.Spec.Title, text, choices);
        }

        /// <summary>A task offered: what it asks, and taking it or not.</summary>
        void Offer(Villager who, QuestKind quest)
        {
            menus.ShowDialogue(who.Spec.Name, who.Spec.Title, quest.Offer, new (string, System.Action)[]
            {
                ("Возьмусь", () =>
                {
                    quests.Take(quest);
                    Say("Ступай. И не трать эссенцию попусту.");
                }),
                ("В другой раз", () => Say("Как знаешь. Поручение подождёт.")),
            });
        }

        /// <summary>A task handed back: the giver's thanks and the reward in stones.</summary>
        void HandIn(Villager who)
        {
            var quest = quests.HandIn(who.Spec.Name);

            if (quest == null)
            {
                return;
            }

            var got = cultivator.Bag.Add(ItemBook.Stone, quest.RewardStones, clock.Minutes);
            menus.Notify($"Задание «{quest.Title}» сдано: получено {ItemBook.Stones(got)}");
            Say(quest.Thanks);
        }

        /// <summary>The shop of the one being talked to.</summary>
        void OpenShop(Shop theirs)
        {
            shop = theirs;
            Enter(Mode.Paused);
            menus.OpenShop();
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

                if (screenshotFrame == 100 && screenshotPage != null)
                {
                    switch (screenshotPage)
                    {
                        case "bag": OpenPage(Menus.Page.Bag); break;
                        case "aperture": OpenPage(Menus.Page.Aperture); break;
                        case "map": OpenPage(Menus.Page.Map); break;
                    case "body": OpenPage(Menus.Page.Body); break;
                        case "shop": OpenShop(Trade.Shops[0]); break;
                    }
                }

                // `-cast`: the first slot used just before the picture (the blade in flight).
                if (screenshotFrame == 150 + benchFrames - castFrames && castAt)
                {
                    cultivator.Use(0);
                }

                if (screenshotFrame == 140)
                {
                    var offAt = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-off");

                    if (offAt >= 0 && offAt + 1 < System.Environment.GetCommandLineArgs().Length)
                    {
                        LeaveOut(System.Environment.GetCommandLineArgs()[offAt + 1]);
                    }

                    if (benchFrames > 0)
                    {
                        StartCounters();
                    }
                }

                if (screenshotFrame > 150 && screenshotFrame <= 150 + benchFrames)
                {
                    frameTimes.Add(Time.unscaledDeltaTime * 1000);

                    foreach (var key in counters.Keys.ToList())
                    {
                        var (recorder, sum) = counters[key];
                        counters[key] = (recorder, sum + (recorder.Valid ? recorder.LastValue : 0));
                    }

                    FrameTimingManager.CaptureFrameTimings();
                    var timings = new FrameTiming[1];

                    if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                    {
                        gpuSum += timings[0].gpuFrameTime;
                        gpuCount++;
                    }
                }

                if (screenshotFrame == 150 + benchFrames)
                {
                    ScreenCapture.CaptureScreenshot(screenshot);

                    if (frameTimes.Count > 0)
                    {
                        frameTimes.Sort();
                        var average = frameTimes.Average();
                        var p95 = frameTimes[Mathf.Min(frameTimes.Count - 1, (int)(frameTimes.Count * 0.95f))];
                        System.IO.File.WriteAllText(screenshot + ".txt",
                            $"frames {frameTimes.Count}, average {average:0.0} ms ({1000 / average:0} fps), 95% under {p95:0.0} ms, worst {frameTimes[frameTimes.Count - 1]:0.0} ms, " +
                            $"quality {GameSettings.Quality}, memory {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024)} MB" + System.Environment.NewLine +
                            $"gpu {(gpuCount > 0 ? (gpuSum / gpuCount).ToString("0.0") + " ms" : "n/a")}" +
                            string.Concat(counters.Select(c => $", {c.Key} {(c.Value.recorder.Valid ? (c.Key == "Main Thread" ? (c.Value.sum / frameTimes.Count / 1e6).ToString("0.0") + " ms" : (c.Value.sum / frameTimes.Count).ToString("0")) : "n/a")}")) + System.Environment.NewLine);
                    }
                }
                else if (screenshotFrame > 150 + benchFrames && screenshotFrame <= 150 + benchFrames + 45 * moreViews.Count)
                {
                    // The other views, one every 45 frames: moved to, let settle, taken.
                    var since = screenshotFrame - 150 - benchFrames;
                    var k = (since - 1) / 45;

                    if (since % 45 == 1)
                    {
                        fixedView = moreViews[k].pose;
                        viewPitch = moreViews[k].pitch;
                        view.Following = false;
                    }
                    else if (since % 45 == 0)
                    {
                        ScreenCapture.CaptureScreenshot(moreViews[k].file);
                    }
                }
                else if (screenshotFrame == 160 + benchFrames + 45 * moreViews.Count)
                {
                    // The player's own graphics level back (a timed run's is not kept).
                    if (qualityBefore >= 0)
                    {
                        GameSettings.Quality = qualityBefore;
                        PlayerPrefs.Save();
                    }

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

                // The hitbox view: every collider near the camera as wire lines.
                if (keyboard.f4Key.wasPressedThisFrame)
                {
                    GameSettings.ShowHitboxes = !GameSettings.ShowHitboxes;
                    HitboxView.On = GameSettings.ShowHitboxes;
                    menus.Notify(HitboxView.On ? "Хитбоксы: показаны (F4 — скрыть)" : "Хитбоксы скрыты");
                }

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
            foreach (var crowd in crowds)
            {
                crowd.Tick(hourOfDay, Time.deltaTime, view.transform.position);
            }

            HerbBed.Now = clock.Minutes;

            if (mode == Mode.Playing && Time.frameCount % 30 == 0)
            {
                RedrawNearMap();
            }

            if (mode == Mode.Playing && screenshot == null)
            {
                Interact(keyboard);
            }

            // The Gu Master's keys: the action bar, a stone, the hero's pages.
            if (mode == Mode.Playing && keyboard != null && screenshot == null)
            {
                var digits = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key, keyboard.digit5Key, keyboard.digit6Key, keyboard.digit7Key, keyboard.digit8Key, keyboard.digit9Key };

                for (var i = 0; i < digits.Length; i++)
                {
                    if (digits[i].wasPressedThisFrame)
                    {
                        cultivator.Use(i);
                    }
                }

                if (keyboard.vKey.wasPressedThisFrame) cultivator.AbsorbStone();

                if (keyboard.kKey.wasPressedThisFrame) OpenPage(Menus.Page.Aperture);
                else if (keyboard.iKey.wasPressedThisFrame) OpenPage(Menus.Page.Bag);
                else if (keyboard.mKey.wasPressedThisFrame) OpenPage(Menus.Page.Map);
                else if (keyboard.bKey.wasPressedThisFrame) OpenPage(Menus.Page.Body);
            }
            else if (mode == Mode.Paused && keyboard != null && !wasPlaying && IsHeroPage(menus.Current))
            {
                // On the hero's pages the same keys go between them, and the page's own key closes it.
                var to = keyboard.kKey.wasPressedThisFrame ? Menus.Page.Aperture : keyboard.iKey.wasPressedThisFrame ? Menus.Page.Bag :
                    keyboard.mKey.wasPressedThisFrame ? Menus.Page.Map : keyboard.bKey.wasPressedThisFrame ? Menus.Page.Body : Menus.Page.None;

                if (to != Menus.Page.None)
                {
                    if (to == menus.Current) Resume();
                    else menus.Show(to);
                }
            }

            if (debug && keyboard != null && keyboard.gKey.wasPressedThisFrame)
            {
                cultivator.NextGrade();
            }

            if (debug && keyboard != null && keyboard.yKey.wasPressedThisFrame)
            {
                cultivator.Bag.Add(ItemBook.Stone, 50, clock.Minutes);
                menus.Notify("Отладка: +50 первобытных камней");
            }

            if (mode == Mode.Playing || mode == Mode.Talking)
            {
                var before = clock.Minutes;
                clock.Advance(Time.deltaTime);
                cultivator.Tick(clock.Minutes);
                health.Mend(clock.Minutes - before);
                sinceSave += Time.deltaTime;

                if (sinceSave >= AutosaveEvery)
                {
                    Save();
                }
            }

            // The main menu's own light and fair sky; the game's clock and weather everywhere else.
            var time = forcedTime ?? (mode == Mode.Title ? TitleTimeOfDay : clock.TimeOfDay);
            sky.Apply(time, weather);
            atmosphere?.Apply(sky, weather, time, view.transform);
            insects?.Apply(sky, weather, view.transform);

            if (Time.unscaledDeltaTime > 0)
            {
                fps = Mathf.Lerp(fps, 1 / Time.unscaledDeltaTime, 0.05f);
            }

            menus.SetFps(fps);
        }

        static bool IsHeroPage(Menus.Page page) => page == Menus.Page.Bag || page == Menus.Page.Aperture || page == Menus.Page.Map || page == Menus.Page.Body;

        /// <summary>
        /// What the hero can do where they stand: talk to the one before them (E), or gather
        /// a bed in flower beside them (E held for a moment) — or see that it is bare.
        /// </summary>
        void Interact(Keyboard keyboard)
        {
            var at = body.transform.position;
            var pressed = keyboard != null && keyboard.eKey.wasPressedThisFrame;

            // On a giant tree: climbing, stepping onto a branch, back onto the trunk, sitting.
            if (body.Tree != null)
            {
                var branch = body.Tree.BranchAt(body.TreeAngle, body.TreeHeight + 0.4f);
                menus.SetPrompt(branch != null ? "[E]  Перейти на ветку   ·   W/S — лезть   ·   Пробел — спрыгнуть" : "W/S — лезть   ·   A/D — вокруг ствола   ·   Пробел — спрыгнуть");

                if (pressed && branch != null)
                {
                    body.StepOntoBranch();
                }

                return;
            }

            if (body.Sitting)
            {
                menus.SetPrompt("Вы сидите   ·   [X] или шаг — встать");

                return;
            }

            var trunk = ClimbTree.Near(at, 0.9f, out _);

            if (trunk != null && body.Grounded)
            {
                menus.SetPrompt(body.Branch != null ? "[E]  Ухватиться за ствол   ·   [X] — сесть" : "[E]  Лезть на дерево");

                if (pressed)
                {
                    body.GrabTree(trunk);
                }

                return;
            }

            if (body.Branch != null)
            {
                var thin = body.Branch.ThicknessAt(body.Branch.Along(at)) < 0.42f;
                menus.SetPrompt(thin ? "Ветка тонкая — может не выдержать   ·   [X] — сесть" : "[X]  Сесть на ветку");

                return;
            }

            Villager before = null;

            foreach (var crowd in crowds)
            {
                before ??= crowd.Before(at, view.transform.forward);
            }

            if (before != null)
            {
                gathering = null;
                menus.SetPrompt($"[E]  Поговорить — {before.Spec.Name}");

                if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                {
                    StartTalk(before);
                }

                return;
            }

            HerbBed bed = null;

            foreach (var candidate in HerbBed.All)
            {
                var d = Vector3.Distance(candidate.transform.position, at);

                if (d < 2.3f && (bed == null || d < Vector3.Distance(bed.transform.position, at)))
                {
                    bed = candidate;
                }
            }

            if (bed == null)
            {
                gathering = null;
                menus.SetPrompt(null);

                return;
            }

            if (!bed.Ready)
            {
                gathering = null;
                menus.SetPrompt($"{bed.Label} — отцвела, вырастет через {Menus.Span(bed.ReadyAt - clock.Minutes)}");

                return;
            }

            if (bed != gathering || keyboard == null || !keyboard.eKey.isPressed)
            {
                gathering = bed;
                gatherHeld = 0;
            }

            if (keyboard != null && keyboard.eKey.isPressed)
            {
                gatherHeld += Time.deltaTime;
            }

            menus.SetPrompt($"[E]  Собрать · {bed.Label} · {HerbBed.GatherTime:0} с", gatherHeld > 0 ? gatherHeld / HerbBed.GatherTime : -1);

            if (gatherHeld >= HerbBed.GatherTime)
            {
                Gather(bed);
                gatherHeld = 0;
            }
        }

        /// <summary>Picks a bed: its petals into the bag (what fits), counted for the task.</summary>
        public void Gather(HerbBed bed)
        {
            if (cultivator.Bag.Room(bed.Item, clock.Minutes) <= 0)
            {
                menus.Notify("Сумка полна");

                return;
            }

            var picked = bed.Gather();
            var kept = cultivator.Bag.Add(bed.Item, picked, clock.Minutes);
            var kind = ItemBook.Find(bed.Item);
            menus.Notify($"Собрано: {kind?.Name.ToLowerInvariant()} ×{kept}");
            quests.Count("gather:" + bed.Item, kept);
        }

        float viewPitch;

        void LateUpdate()
        {
            if (fixedView.HasValue)
            {
                var v = fixedView.Value;
                var at = new Vector3(v.x, v.y, v.z);
                if (viewAboveGround)
                {
                    at.y += QingMao.GroundAt(at);
                }

                view.transform.SetPositionAndRotation(at, Quaternion.Euler(viewPitch, v.yaw, 0));

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
            TrainingDummy.Struck -= OnStruck;
            Wounds.Landed -= OnWound;
            TreeBranch.Creaking -= OnCreak;
            TreeBranch.Snapped -= OnSnap;
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
                cultivation = cultivator.Save(),
                health = health.Current,
                quests = quests.Save(),
                beds = HerbBed.All.ConvertAll(b => b.ReadyAt).ToArray(),
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
            GUILayout.BeginArea(new Rect(16, 250, 440, 230), skin.Panel);
            GUILayout.Label(
                $"FPS {fps:0}\n" +
                $"x {p.x:0.00}  y {p.y:0.00}  z {p.z:0.00}\n" +
                $"скорость {body.HorizontalSpeed:0.0} м/с · {body.Stance} · {(body.Grounded ? "на земле" : "в воздухе")}\n" +
                (world != null ? $"чанков загружено {world.LoadedCount} (по {Chunks.size} м)\n" : $"{Location.title}\n") +
                $"погода: {GuWorld.World.Weather.Name(weather.Kind)}{(forcedWeather.HasValue ? " (вручную)" : "")}\n" +
                $"эссенция {cultivator.Aperture.Essence:0.0}% / {cultivator.Aperture.Cap:0}% · класс {cultivator.Aperture.Grade}\n" +
                $"жизнь {health.Current:0} · камней {cultivator.Stones} · вес {cultivator.Bag.Weight:0.0}\n" +
                "T — +1 час · R — погода · G — класс таланта · Y — +50 камней",
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
