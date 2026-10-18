using System.Collections.Generic;
using System.Linq;
using GuWorld.Body;
using GuWorld.Combat;
using GuWorld.Creatures;
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
    public class GuWorldGame : MonoBehaviour, IMenuHost, ICommandHost
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
            /// <summary>Typing in the chat (T, /): the world goes on, the hero stands.</summary>
            Chat,
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
        Melee melee;
        readonly Quests quests = new Quests();
        readonly Trade trade = new Trade();
        /// <summary>The shop the hero is at (while its page is open).</summary>
        Shop shop;
        LocalMap map;
        /// <summary>The minimap's finer map round the hero, drawn again when they have walked far from its middle.</summary>
        LocalMap nearMap;
        const float NearSpan = 900;
        /// <summary>The next minimap being drawn, a few rows a frame (all at once it stops the game for a fifth of a second).</summary>
        LocalMap.Painting nearPainting;
        const int NearRowsPerFrame = 16;
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
        /// <summary>The frames before the timed ones (the village filling in), ms; and the frame the last villager came.</summary>
        readonly System.Collections.Generic.List<float> startTimes = new System.Collections.Generic.List<float>();
        int lastMadeAt = -1;
        int peopleMade;
        readonly System.Collections.Generic.Dictionary<string, (Unity.Profiling.ProfilerRecorder recorder, double sum)> counters = new System.Collections.Generic.Dictionary<string, (Unity.Profiling.ProfilerRecorder, double)>();
        readonly System.Collections.Generic.Dictionary<string, Unity.Profiling.ProfilerRecorder> memory = new System.Collections.Generic.Dictionary<string, Unity.Profiling.ProfilerRecorder>();
        double gpuSum;
        double cpuSum;
        double renderSum;
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
                    case "terrain":
                        foreach (var t in Terrain.activeTerrains) t.drawHeightmap = false;
                        break;
                    case "sky":
                        RenderSettings.skybox = null;
                        Camera.main.clearFlags = CameraClearFlags.SolidColor;
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

        /// <summary>What the world holds for the timed run's report: renderers (all, seen) by the top object they are under, colliders.</summary>
        static string Census()
        {
            var all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var seen = new System.Collections.Generic.Dictionary<string, int>();
            var shown = 0;

            foreach (var renderer in all)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || !renderer.isVisible)
                {
                    continue;
                }

                shown++;
                var top = renderer.transform.root.name;
                top = top.Length > 28 ? top.Substring(0, 28) : top;
                // Squares and houses by kind, not each one.
                top = System.Text.RegularExpressions.Regex.Replace(top, @"[-\d\s(),]+$", "");
                seen[top] = (seen.TryGetValue(top, out var n) ? n : 0) + 1;
            }

            var colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
            var bodies = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;

            return $"renderers {all.Length}, seen {shown} (" + string.Join(", ", seen.OrderByDescending(p => p.Value).Take(14).Select(p => $"{p.Key} {p.Value}")) + ")" +
                   $"; colliders {colliders.Length} ({colliders.Count(c => c.enabled && c.gameObject.activeInHierarchy)} on), bodies {bodies}";
        }

        /// <summary>The first frames of a timed run, while the village fills in: how long, the worst, how many were slow.</summary>
        string StartReport()
        {
            if (startTimes.Count == 0)
            {
                return "start: n/a";
            }

            var worst = startTimes.Max();
            var slow = startTimes.Count(t => t > 50);

            return $"start: frames 2–{startTimes.Count + 1} took {startTimes.Sum() / 1000:0.0} s, worst {worst:0} ms, {slow} over 50 ms; " +
                   $"{peopleMade} people made, the last at frame {lastMadeAt}";
        }

        void StartCounters()
        {
            foreach (var (category, name) in new[] { (Unity.Profiling.ProfilerCategory.Internal, "Main Thread"), (Unity.Profiling.ProfilerCategory.Render, "Batches Count"), (Unity.Profiling.ProfilerCategory.Render, "SetPass Calls Count"), (Unity.Profiling.ProfilerCategory.Render, "Triangles Count"), (Unity.Profiling.ProfilerCategory.Render, "Shadow Casters Count"), (Unity.Profiling.ProfilerCategory.Render, "Visible Skinned Meshes Count") })
            {
                counters[name] = (Unity.Profiling.ProfilerRecorder.StartNew(category, name), 0);
            }

            // Memory as it stands at the end (the graphics' share is what the bodies' meshes and textures take).
            foreach (var name in new[] { "Gfx Used Memory", "Total Used Memory" })
            {
                memory[name] = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, name);
            }

            // `-profile file` (a development build): the timed frames written for the profiler (file.raw).
            var args = System.Environment.GetCommandLineArgs();
            var at = System.Array.IndexOf(args, "-profile");

            if (Debug.isDebugBuild && at >= 0 && at + 1 < args.Length)
            {
                UnityEngine.Profiling.Profiler.maxUsedMemory = 256 * 1024 * 1024;
                UnityEngine.Profiling.Profiler.logFile = args[at + 1];
                UnityEngine.Profiling.Profiler.enableBinaryLog = true;
                UnityEngine.Profiling.Profiler.enabled = true;
            }
        }
        bool talkAt;
        string screenshotPage;
        bool castAt;
        /// <summary>`-aim`: the first Gu's skill held, aimed (its path on the ground); `-absorb`: a stone being drawn in.</summary>
        bool aimShot;
        bool absorbShot;
        /// <summary>`-chat "/tp g"`: the chat open with that typed in (its suggestions shown).</summary>
        string chatShot;
        string doShot;
        string aimGu;
        int rollFrames;
        /// <summary>`-wolves 3`: a pack called up before the camera.</summary>
        int wolvesShot;
        /// <summary>`-cast 12`: how many frames before the picture the blade is thrown.</summary>
        int castFrames = 5;
        /// <summary>`-blow kick 16`: that blow thrown so many frames before the picture; `-guard`: the guard up.</summary>
        Blow? blowShot;
        int blowFrames;
        bool guardShot;
        /// <summary>`-climb x,z,yaw,seconds`: the hero put there facing that way (degrees), taking hold of the wall before them and climbing up it;
        /// `-climb near,seconds`: before the nearest house's wall, wherever the hero is (x NaN).</summary>
        Vector4? climbShot;
        /// <summary>`-fists`: the fists ready (their slot lit, the stance a fighter's).</summary>
        bool fistsShot;
        /// <summary>`-orbit angle,distance,height`: the camera held on the hero from that side (degrees from their front), that far, looking at that height.</summary>
        Vector3? orbitView;
        float orbitTurn;
        float pitchShot = float.NaN;
        /// <summary>`-pin x,z[,go]`: a mark put on the map (and gone to).</summary>
        Vector3? pinShot;
        bool pinGo;
        /// <summary>What a check run saw (the picture's name + ".log.txt"): the climb's steps, the blows that landed.</summary>
        System.Text.StringBuilder testLog;
        /// <summary>`-view x,y,z,yaw,pitch` (with -screenshot): the camera held there, looking that way (degrees).</summary>
        Pose? fixedView;
        /// <summary>`-ground` (with -view): its height is above the ground there.</summary>
        bool viewAboveGround;

        /// <summary>The making's camera: 0 the whole figure … 1 the face (eased toward where the wheel or the buttons put it).</summary>
        float creatorZoom;
        float creatorZoomTo;

        public bool CreatorFace
        {
            get => creatorZoomTo > 0.5f;
            set => creatorZoomTo = value ? 1 : 0;
        }
        /// <summary>The way (difficulty, talent) chosen for the game being begun; -1: not yet (the making asks first).</summary>
        public int ChosenPath { get; set; } = -1;
        public ThirdPersonCamera View => view;
        public Cultivator Cultivator => cultivator;
        public Melee Fists => melee;
        public HumanBody.Parts Parts => picture.Parts;
        public Health Health => health;
        public Quests Quests => quests;
        public Trade Trade => trade;
        public Shop Shop => shop;
        public LocalMap Map => map;
        public LocalMap NearMap => nearMap;

        /// <summary>Where the task in hand leads (worked out a few times a second: QuestTargets).</summary>
        public (Vector3 at, string what)? QuestTarget { get; private set; }
        float questTargetAt;

        /// <summary>
        /// Where the task in hand leads: back to its giver when it is done (or to the next task's giver when there is none);
        /// else to what it asks — the dummies, the nearest bed of the herb, the shop that sells the thing, the nearest
        /// wolves, the place, the person to talk to or to bring the thing to. Feeding a Gu leads nowhere (the aperture, K).
        /// </summary>
        (Vector3 at, string what)? FindQuestTarget()
        {
            var current = quests.Current;

            if (current == null)
            {
                return quests.Next is QuestKind next ? Person(next.Giver) : null;
            }

            if (quests.Ready)
            {
                return Person(current.Giver);
            }

            var task = current.Event ?? "";
            var hero = body.transform.position;

            if (task == "hit_dummy")
            {
                return dummiesAt is Vector3 dummies ? (dummies, "чучела во дворе академии") : null;
            }

            if (task.StartsWith("gather:"))
            {
                var item = task.Substring(7);
                HerbBed nearest = null;

                foreach (var bed in HerbBed.All)
                {
                    // A bed ready to pick first; the nearest of those.
                    if (bed != null && bed.Item == item && (nearest == null || (bed.Ready && !nearest.Ready) ||
                        (bed.Ready == nearest.Ready && (bed.transform.position - hero).sqrMagnitude < (nearest.transform.position - hero).sqrMagnitude)))
                    {
                        nearest = bed;
                    }
                }

                return nearest != null ? (nearest.transform.position, nearest.Ready ? "грядка" : "грядка (ещё не созрела)") : null;
            }

            if (task.StartsWith("buy:"))
            {
                var item = task.Substring(4);
                var shop = System.Array.Find(Trade.Shops, s => System.Array.Exists(s.Goods, g => g.Item == item));

                return shop != null ? Person(shop.Keeper) : null;
            }

            if (task.StartsWith("kill:"))
            {
                return wolves != null && wolves.Nearest(hero) is Vector3 wolf ? (wolf, "волки") : null;
            }

            if (task.StartsWith("visit:"))
            {
                return Places.TryGetValue(task.Substring(6), out var place) ? (place, current.Goal) : null;
            }

            if (task.StartsWith("talk:"))
            {
                return Person(task.Substring(5));
            }

            if (task.StartsWith("deliver:") && task.IndexOf('@') > 0)
            {
                return Person(task.Substring(task.IndexOf('@') + 1));
            }

            return null;
        }

        /// <summary>Where a villager of the hero's village is now, and their name (asleep, said so).</summary>
        (Vector3 at, string what)? Person(string name)
        {
            if (villagers == null)
            {
                return null;
            }

            foreach (var person in villagers.People)
            {
                if (person != null && person.Spec.Name == name)
                {
                    return (person.transform.position, person.Asleep ? $"{name} (спит)" : name);
                }
            }

            return null;
        }

        /// <summary>The advice on the keys, in order: each until the player has done it once (kept between games).</summary>
        static readonly (string key, string text)[] Advice =
        {
            ("talk", "Идите к золотому ромбу и нажмите E рядом с человеком — поговорить и взять поручение"),
            ("cast", "Зажмите 1 — прицел Гу, отпустите — лунный клинок полетит туда, куда смотрит прицел"),
            ("absorb", "V — поглотить первобытный камень: за 3 секунды эссенция в апертуре пополнится"),
            ("map", "M — карта: щёлкните по ней, чтобы поставить метку, — она появится и на миникарте"),
            ("fists", "F — кулаки: ЛКМ — серия ударов, ПКМ — блок, Q и R — особые удары"),
        };

        /// <summary>
        /// Advice on what is happening now, given before the advice on the keys: each until the player has done what it
        /// says — or, advice that only tells (`tells`), until it has been up a while.
        /// </summary>
        static readonly (string key, string text, bool tells)[] Moments =
        {
            ("dodge", "Враг охотится на вас! Alt — перекат: в его начале ничто вас не задевает", false),
            ("parry", "Поднимите блок (ПКМ) в самый миг удара — парирование: удар отбит, враг шатается", false),
            ("shielded", "Враг поднял щит — он забирает бо́льшую часть удара. Перекатом в сторону — и ждите, пока щит спадёт", true),
            ("foreign", "Чужая земля: Гу-мастера клана предупредили вас. Уходите — или готовьтесь к бою", true),
            ("hurt", "Вы тяжело ранены: отойдите от врагов — раны затягиваются со временем, лечащие Гу — быстрее", true),
            ("tired", "Гу устал (100+): применить его можно, но через силу — и он может погибнуть. Дайте ему отдохнуть", true),
            ("fusion", "Можно слить Гу в новый! U — слияние: все рецепты, что есть и чего не хватает", false),
            ("meditate", "X — сесть: сидя, вдали от врагов, вы медитируете — эссенция уходит в стенки апертуры, и ступень растёт", false),
            ("night", "Ночь: волки видят дальше, а люди — хуже. За стенами деревни ночью опаснее", true),
        };

        readonly Dictionary<string, float> momentShown = new Dictionary<string, float>();
        int momentFrame = -1;
        string moment;
        float idleFor;
        float fusionLookAt;
        bool fusionReady;

        /// <summary>Whether the moment for a piece of advice is now.</summary>
        bool IsMoment(string key)
        {
            var at = body.transform.position;

            switch (key)
            {
                case "dodge":
                    return Foes.Hunting(at, 14);
                case "parry":
                    return melee.Ready && Foes.Hunting(at, 3.5f);
                case "shielded":
                    foreach (var foe in Foes.Near(at, 20))
                    {
                        if (foe.Hunting && foe.Shielded)
                        {
                            return true;
                        }
                    }

                    return false;
                case "foreign":
                    return GuMaster.All.Exists(m => m != null && m.Warns && Vector3.Distance(m.transform.position, at) < 45);
                case "hurt":
                    return !health.Down && health.Current < Health.Max * 0.35f;
                case "tired":
                    return !Sandbox && cultivator.Gu.Exists(g => g.tired >= 100);
                case "fusion":
                    // Looked at once a second (every recipe against the bag).
                    if (Time.unscaledTime >= fusionLookAt)
                    {
                        fusionLookAt = Time.unscaledTime + 1;
                        fusionReady = !Sandbox && System.Array.Exists(GuBook.Recipes, r => cultivator.Lacks(r) == null);
                    }

                    return fusionReady;
                case "meditate":
                    return !Sandbox && idleFor > 8 && !body.Sitting && cultivator.Aperture.Essence > cultivator.Aperture.Cap * 0.4f && !Foes.Hunting(at, 40);
                case "night":
                    var hour = clock.MinuteOfDay / 60;

                    return !Sandbox && Location.id == QingMao.Id && (hour < 5 || hour > 20.5f) &&
                           !System.Array.Exists(QingMao.Settlements, s => Vector2.Distance(new Vector2(at.x, at.z), s.Centre) < s.Radius * 1.1f);
                default:
                    return false;
            }
        }

        /// <summary>The advice of the moment, if any (worked out once a frame; advice that only tells is learnt once up a while).</summary>
        string Moment()
        {
            if (momentFrame == Time.frameCount)
            {
                return moment;
            }

            momentFrame = Time.frameCount;
            moment = null;
            idleFor = body.HorizontalSpeed > 0.2f || body.Sitting ? 0 : idleFor + Time.unscaledDeltaTime;

            foreach (var (key, text, tells) in Moments)
            {
                if (Knows(key) || !IsMoment(key))
                {
                    continue;
                }

                moment = text;

                if (tells)
                {
                    momentShown[key] = (momentShown.TryGetValue(key, out var shown) ? shown : 0) + Time.unscaledDeltaTime;

                    if (momentShown[key] > 9)
                    {
                        Learnt(key);
                    }
                }

                break;
            }

            return moment;
        }

        public string Hint
        {
            get
            {
                // Not over the first moments, nor in a picture for checking.
                if (screenshot != null || Time.timeSinceLevelLoad < 4)
                {
                    return null;
                }

                if (Moment() is string now)
                {
                    return now;
                }

                foreach (var (key, text) in Advice)
                {
                    if (!Knows(key))
                    {
                        return text;
                    }
                }

                return null;
            }
        }

        static bool Knows(string key) => PlayerPrefs.GetInt("advice." + key, 0) == 1;

        /// <summary>The player has done what a piece of advice says: it is not given again.</summary>
        static void Learnt(string key)
        {
            if (!Knows(key))
            {
                PlayerPrefs.SetInt("advice." + key, 1);
            }
        }
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
                    return LowerFirst(place.Name);
                }
            }

            foreach (var place in QingMao.Settlements)
            {
                if (Vector2.Distance(p, place.Centre) < place.Radius * 3 + 300)
                {
                    return $"окрестности: {LowerFirst(place.Name)}";
                }
            }

            if (Vector2.Distance(p, QingMao.Peak) < 900) return "вершина";
            if (Vector2.Distance(p, QingMao.ForePeak) < 1300) return "передняя гора";

            return p.y > 3500 ? "задняя гора" : "склоны горы";
        }
        public string TimeLine => $"{PhaseName(clock.Phase).ToUpperInvariant()} · ДЕНЬ {clock.Day} · {clock.Label} · {GuWorld.World.Weather.Name(weather.Kind)}" +
                                  (Battle.Effects is string effects ? $" · <color=#8FD0BC>{effects}</color>" : "");

        void Awake()
        {
            // `-sandbox` started on the mountain: straight to the proving ground's scene (this one does nothing more).
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-sandbox") >= 0 && !SandboxNext &&
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != SandboxScene)
            {
                redirecting = true;
                SandboxNext = true;
                UnityEngine.SceneManagement.SceneManager.LoadScene(SandboxScene);

                return;
            }

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
            // The proving ground (from the main menu, or `-sandbox`): its own square kilometre, nothing saved.
            Sandbox = SandboxNext || Has(args, "-sandbox");
            SandboxNext = false;

            if (Sandbox)
            {
                Location = LocationSettings.Polygon;
            }

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

            // A timed run goes on behind other windows (Unity stops a game out of focus).
            if (screenshot != null)
            {
                Application.runInBackground = true;
            }

            var restored = screenshot != null || Sandbox ? SaveGame.NewGame(Location) : SaveGame.Load(Location);
            clock = restored.clock;
            saved = restored.look.IsChosen;

            foreach (var problem in restored.problems)
            {
                Debug.LogWarning($"GU World: {problem}");
            }

            look = restored.look.IsChosen ? restored.look : Sandbox && carriedLook != null ? carriedLook : Look.Default(Gender.Male);

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

            // `-vsync 0|1` (with a picture): waiting for the screen or not, for this run.
            var vsyncAt = System.Array.IndexOf(args, "-vsync");

            if (screenshot != null && vsyncAt >= 0 && vsyncAt + 1 < args.Length && int.TryParse(args[vsyncAt + 1], out var vsync))
            {
                QualitySettings.vSyncCount = vsync;
            }

            // `-grd 0|1` (with a picture): the GPU Resident Drawer off or on, for this run.
            var grdAt = System.Array.IndexOf(args, "-grd");

            if (screenshot != null && grdAt >= 0 && grdAt + 1 < args.Length && int.TryParse(args[grdAt + 1], out var grd) &&
                UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset pipeline)
            {
                // (Its type lives in an assembly the game's scripts do not reference.)
                var mode = pipeline.GetType().GetProperty("gpuResidentDrawerMode");
                mode?.SetValue(pipeline, System.Enum.ToObject(mode.PropertyType, grd == 0 ? 0 : 1));
            }

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
            Wounds.Struck += OnStrikeShown;
            hitboxes = HitboxView.Create(view.transform);
            HitboxView.On = GameSettings.ShowHitboxes;
            insects = Insects.Create();
            TreeBranch.Creaking += OnCreak;
            TreeBranch.Snapped += OnSnap;
            PlaceDummies(layout);
            PlaceBeds(layout, restored.beds);
            DrawMap(layout);

            // The wolves of the forest round the village (their dens outside its walls), and the packs a command calls up.
            wolves = WolfPacks.Create(body.transform, WolfDens());
            Wolf.Killed += OnWolfKilled;
            Wolf.Roared += OnWolfRoared;
            // The other clans' Gu Masters at their posts round their villages, and those called up.
            patrols = Patrols.Create(body.transform, picture.Parts, ClanLands());
            GuMaster.Killed += OnMasterKilled;
            GuMaster.Spoke += OnMasterSpoke;

            if (Sandbox)
            {
                StartSandbox();
            }

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
                aimShot = Has(args, "-aim") || Has(args, "-aimgu");
                var rollAt = System.Array.IndexOf(args, "-roll");
                rollFrames = rollAt >= 0 && rollAt + 1 < args.Length && int.TryParse(args[rollAt + 1], out var rf) ? rf : 0;
                var aimGuAt = System.Array.IndexOf(args, "-aimgu");
                aimGu = aimGuAt >= 0 && aimGuAt + 1 < args.Length ? args[aimGuAt + 1] : null;
                absorbShot = Has(args, "-absorb");
                var wolvesAt = System.Array.IndexOf(args, "-wolves");
                wolvesShot = wolvesAt >= 0 && wolvesAt + 1 < args.Length && int.TryParse(args[wolvesAt + 1], out var n) ? n : 0;
                var chatAt = System.Array.IndexOf(args, "-chat");
                chatShot = chatAt >= 0 && chatAt + 1 < args.Length ? args[chatAt + 1] : null;
                // `-do "/gu 1;/rank 2"`: chat commands run once the world stands (a picture of what they make).
                var doAt = System.Array.IndexOf(args, "-do");
                doShot = doAt >= 0 && doAt + 1 < args.Length ? args[doAt + 1] : null;
                var castFramesAt = System.Array.IndexOf(args, "-cast");
                castFrames = castFramesAt >= 0 && castFramesAt + 1 < args.Length && int.TryParse(args[castFramesAt + 1], out var cf) ? cf : 5;
                HitboxView.On = (HitboxView.On || Has(args, "-hitboxes")) && !Has(args, "-nohitboxes");
                var blowAt = System.Array.IndexOf(args, "-blow");

                if (blowAt >= 0 && blowAt + 1 < args.Length && System.Enum.TryParse<Blow>(args[blowAt + 1], true, out var blow))
                {
                    blowShot = blow;
                    blowFrames = blowAt + 2 < args.Length && int.TryParse(args[blowAt + 2], out var bf) ? bf : blow == Blow.Kick ? 16 : 9;
                }

                guardShot = Has(args, "-guard");
                var climbAt = System.Array.IndexOf(args, "-climb");

                if (climbAt >= 0 && climbAt + 1 < args.Length)
                {
                    var parts = args[climbAt + 1].Split(',');

                    if (parts[0] == "near")
                    {
                        climbShot = new Vector4(float.NaN, 0, 0, parts.Length > 1 ? float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 4);
                    }
                    else
                    {
                        var c = System.Array.ConvertAll(parts, s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
                        climbShot = new Vector4(c[0], c[1], c[2], c.Length > 3 ? c[3] : 3);
                    }
                }

                fistsShot = Has(args, "-fists");
                var orbitAt = System.Array.IndexOf(args, "-orbit");

                if (orbitAt >= 0 && orbitAt + 1 < args.Length)
                {
                    var o = System.Array.ConvertAll(args[orbitAt + 1].Split(','), t => float.Parse(t, System.Globalization.CultureInfo.InvariantCulture));
                    orbitView = new Vector3(o[0], o.Length > 1 ? o[1] : 3.2f, o.Length > 2 ? o[2] : 1.0f);
                    // A fourth number: the way the hero walks (-walk, -run), degrees from the way they start facing.
                    orbitTurn = o.Length > 3 ? o[3] * Mathf.Deg2Rad : 0;
                }

                // `-pitch` (radians, down positive): the hero's camera tipped so (the crosshair, the aim with it).
                var pitchAt = System.Array.IndexOf(args, "-pitch");
                pitchShot = pitchAt >= 0 && pitchAt + 1 < args.Length ? float.Parse(args[pitchAt + 1], System.Globalization.CultureInfo.InvariantCulture) : float.NaN;

                var pinAt = System.Array.IndexOf(args, "-pin");

                if (pinAt >= 0 && pinAt + 1 < args.Length)
                {
                    var parts = args[pinAt + 1].Split(',');
                    pinShot = new Vector3(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), 0, float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
                    pinGo = parts.Length > 2 && parts[2] == "go";
                }

                if (blowShot.HasValue || climbShot.HasValue)
                {
                    testLog = new System.Text.StringBuilder();
                    Wounds.Landed += (who, part, harm, down) => testLog.AppendLine($"frame {screenshotFrame}: {Wounds.Line(who, part, harm, down)}");
                }
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

                // `-fp`: seen from the hero's eyes.
                if (Has(args, "-fp"))
                {
                    SetFirstPerson(true);
                }
            }
            else
            {
                Enter(Sandbox ? Mode.Playing : Mode.Title);
                menus.FadeIn();
            }
        }

        static bool Has(string[] args, string flag) => System.Array.IndexOf(args, flag) >= 0;

        // ── The proving ground ────────────────────────────────────────

        /// <summary>Set by the main menu just before the proving ground's scene is loaded (its game reads it as it starts).</summary>
        static bool SandboxNext;
        /// <summary>This scene's game is only passing the start on to the proving ground's.</summary>
        bool redirecting;
        /// <summary>The hero's look, taken along to the proving ground.</summary>
        static Look carriedLook;
        const string SandboxScene = "Assets/GuWorld/Scenes/TestGrounds.unity";

        /// <summary>
        /// The proving ground, as an immortal's aperture (lore: sys_immortal_aperture,
        /// sys_blessed_lands_and_grotto_heavens): the blessed land of a fallen Gu Immortal, a
        /// small world of a square kilometre walled in by the aperture's edge, its essence so
        /// dense that primeval stones never run out. Its page (P) sets the modes (immortal, an
        /// endless sea, untiring and sated Gu) and holds every Gu there is; straw dummies stand
        /// at 5–40 m like a range, and wolves are called up at will. Nothing there is saved; a
        /// death only puts the hero back on their feet.
        /// </summary>
        public bool Sandbox { get; private set; }

        public void OpenSandbox()
        {
            SandboxNext = true;
            carriedLook = look;
            Time.timeScale = 1;
            UnityEngine.SceneManagement.SceneManager.LoadScene(SandboxScene);
        }

        /// <summary>Back from the proving ground to the mountain's main menu.</summary>
        public void LeaveSandbox()
        {
            Time.timeScale = 1;
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }

        void StartSandbox()
        {
            cultivator.Begin(-1, 99);
            // Everything to try at once: no death, a sea that never runs dry, Gu that never go hungry (they still tire).
            cultivator.AllCells = true;
            cultivator.EndlessEssence = true;
            cultivator.Sated = true;
            health.Immortal = true;
            player.WheelZoom = false;
            TopUpStones();

            for (var i = 0; i < SandboxCells; i++)
            {
                PutInSlot(i, GuBook.Find(SandboxStart[i]));
            }

            // A range: dummies along the way the hero looks, 5 to 40 metres off, and a few more to the side.
            var straw = Lit("Straw", 0xc9b07a);
            var wood = Lit("Post", 0x5a4330);
            var spawn = Location.spawn;
            var ahead = new Vector3(Mathf.Sin(Location.spawnYaw), 0, Mathf.Cos(Location.spawnYaw));
            var side = Vector3.Cross(Vector3.up, ahead);

            foreach (var metres in new[] { 5f, 10f, 15f, 20f, 30f, 40f })
            {
                var at = spawn + ahead * metres - side * 7;
                TrainingDummy.Make(OnGround(at), Location.spawnYaw * Mathf.Rad2Deg + 180, straw, wood);
            }

            for (var i = 0; i < 4; i++)
            {
                var at = spawn + ahead * 8 + side * (6 + i * 2.5f);
                TrainingDummy.Make(OnGround(at), Location.spawnYaw * Mathf.Rad2Deg + 180, straw, wood);
            }

            ApertureLook();
            menus.Banner("Благословенная земля", "1–8 — выбрать Гу · ЛКМ — применить · Tab — все Гу");
            menus.Notify("1–8 — выбрать Гу (его прицел виден сразу) · ЛКМ — применить · Tab — все Гу");
            menus.Notify("Манекены слева — дальномер 5–40 м, справа — группа");
        }

        /// <summary>
        /// The blessed land's look: jade-green ground (its grid kept, to measure by) and, at the
        /// square kilometre's edge, the aperture's wall — a pale glow rising out of sight.
        /// </summary>
        void ApertureLook()
        {
            var ground = new Color(0.46f, 0.62f, 0.5f);
            GroundMaterial.color = ground;

            if (GroundMaterial.HasProperty("_BaseColor"))
            {
                GroundMaterial.SetColor("_BaseColor", ground);
            }

            var b = Location.bounds;
            var wall = GuSkills.Glow(new Color(0.42f, 0.78f, 0.7f) * 0.3f);
            var height = 260f;

            void Wall(Vector3 centre, Vector3 size)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Aperture Wall";
                Destroy(go.GetComponent<Collider>());
                go.transform.position = centre;
                go.transform.localScale = size;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = wall;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var y = b.min.y + height / 2;
            Wall(new Vector3(b.center.x, y, b.max.z), new Vector3(b.size.x, height, 0.5f));
            Wall(new Vector3(b.center.x, y, b.min.z), new Vector3(b.size.x, height, 0.5f));
            Wall(new Vector3(b.max.x, y, b.center.z), new Vector3(0.5f, height, b.size.z));
            Wall(new Vector3(b.min.x, y, b.center.z), new Vector3(0.5f, height, b.size.z));
        }

        /// <summary>The proving ground's stones never run out.</summary>
        void TopUpStones()
        {
            const int Endless = 999;
            var stones = cultivator.Stones;

            if (stones < Endless)
            {
                cultivator.Bag.Add(ItemBook.Stone, Endless - stones, clock.Minutes);
            }
        }

        /// <summary>Every wolf and Gu Master there is sent away (the proving ground's page).</summary>
        public void ClearWolves()
        {
            foreach (var wolf in Wolf.All.ToArray())
            {
                if (wolf != null)
                {
                    Destroy(wolf.gameObject);
                }
            }

            foreach (var master in GuMaster.All.ToArray())
            {
                if (master != null)
                {
                    Destroy(master.gameObject);
                }
            }
        }

        /// <summary>The hour set at once (the proving ground's page).</summary>
        public void SetHour(float hour) => Commands.Run($"/time set {hour.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

        /// <summary>Back at the start of the proving ground, on one's feet.</summary>
        public void BackToStart()
        {
            body.PlaceAt(Location.spawn);
            body.Facing = Location.spawnYaw;
            view.Yaw = Location.spawnYaw;
            health.Set(Health.Max);
        }

        /// <summary>A name after a "·" or a word: its first letter small, the proper names in it as they are ("деревня Гу Юэ").</summary>
        static string LowerFirst(string name) => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);

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
                skin = parts.Length > 4 ? parts[4] : "fair",
                stature = parts.Length > 5 ? parts[5] : "middle",
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
            melee = hero.AddComponent<Melee>();
            melee.Hit += OnFistLanded;
            melee.Parried += OnParried;
            dodge = hero.AddComponent<Dodge>();
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
                // Sixteen-year-olds' bodies (MakeHuman, Tools/Character): the hero's and the young villagers'.
                teenMale = Resources.Load<GameObject>("Models/human-teen-male"),
                teenFemale = Resources.Load<GameObject>("Models/human-teen-female"),
                cloth = ClothMaterial,
                faint = FaintMaterial,
            };
            HeroView.TeenBodies = picture.Parts.teenMale != null && picture.Parts.teenFemale != null;
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
            melee.Scale = picture.Scale;
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

            // The main menu and the hero's making show the hero from outside.
            if ((next == Mode.Title || next == Mode.Create) && view.FirstPerson)
            {
                SetFirstPerson(false);
            }

            // The paused world's moment to let go of the streamed squares left behind (unseen behind the menu).
            if (next == Mode.Paused && ChunkStreamer.Current != null)
            {
                ChunkStreamer.Current.LetGo();
            }
            view.Following = playing || next == Mode.Paused || next == Mode.Talking || next == Mode.Chat;

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
                // The chat opens itself (OpenChat), with what is to be in its line.
                Mode.Chat => menus.Current,
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

            // A new hero: born with the talent of the way chosen (an extreme physique by its chance).
            if (made)
            {
                var path = Mathf.Clamp(ChosenPath < 0 ? 1 : ChosenPath, 0, Talents.Paths.Length - 1);
                cultivator.Begin(path, Talents.Roll(Talents.Paths[path], new System.Random()));
            }

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
            if (page == Menus.Page.Map)
            {
                Learnt("map");
            }

            if (page == Menus.Page.Fusion)
            {
                Learnt("fusion");
            }

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

        public void NewGame() => Begin(false);

        /// <summary>
        /// A new game from the first morning: the save gone, the Gu Master, the tasks and the beds new.
        /// The hero made afresh — or, after a death, the same hero (look and name) going straight on.
        /// </summary>
        void Begin(bool sameHero)
        {
            // After a death the same way goes on, with the talent one was born with.
            var path = cultivator.Path;
            var talent = cultivator.Aperture.Talent;
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

            if (sameHero)
            {
                cultivator.Begin(path, talent);
                Wear(look);
                saved = true;
                Enter(Mode.Playing);
                Save();

                return;
            }

            Wear(Look.Default(look.Gender));
            ChosenPath = -1;
            Enter(Mode.Create);
        }

        public void ResetGame() => menus.FadeThrough(NewGame);

        public void BackToTitle()
        {
            if (Sandbox)
            {
                LeaveSandbox();

                return;
            }

            if (mode == Mode.Playing || mode == Mode.Paused)
            {
                Save();
            }

            Enter(Mode.Title);
        }

        /// <summary>Where the straw dummies stand (the middle one), for the task that sends the hero to them.</summary>
        Vector3? dummiesAt;

        /// <summary>Straw dummies to try skills on: in the academy's yard, or (on the proving ground) before the spawn.</summary>
        void PlaceDummies(VillageLayout layout)
        {
            var straw = Lit("Straw", 0xc9b07a);
            var wood = Lit("Post", 0x5a4330);

            if (layout != null && layout.TryPlace("academy yard", out var yard))
            {
                var turn = Quaternion.Euler(0, yard.yaw, 0);

                dummiesAt = OnGround(yard.at + turn * new Vector3(0, 0, 7), 3);

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

            using (Costs.Time("map drawn"))
            {
                map = plan != null
                    ? LocalMap.World(plan, new Rect(Location.bounds.min.x, Location.bounds.min.z, Location.bounds.size.x, Location.bounds.size.z))
                    : LocalMap.Grid(Location.bounds);
            }

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

            // The minimap's, round where the hero comes in (they are put there only once the game runs).
            if (plan != null)
            {
                using (Costs.Time("near map drawn"))
                {
                    ShowNearMap(LocalMap.Around(player.Spawn, NearSpan, plan));
                }
            }
        }

        /// <summary>The minimap's map round the hero, drawn again — a few rows a frame — when they have walked a third of it away from its middle.</summary>
        void RedrawNearMap()
        {
            var plan = WorldPlan.Current;

            if (plan == null || body == null)
            {
                return;
            }

            if (nearPainting != null)
            {
                using (Costs.Time("near map step"))
                {
                    var done = nearPainting.Step(NearRowsPerFrame);

                    if (done != null)
                    {
                        nearPainting = null;
                        ShowNearMap(done);
                    }
                }

                return;
            }

            var at = body.transform.position;

            if (nearMap != null && Vector2.Distance(new Vector2(at.x, at.z), nearMap.Area.center) < NearSpan * 0.3f)
            {
                return;
            }

            nearPainting = LocalMap.AroundInSteps(at, NearSpan, plan);
        }

        /// <summary>Puts a newly drawn minimap in place of the old one.</summary>
        void ShowNearMap(LocalMap next)
        {
            if (nearMap != null)
            {
                Destroy(nearMap.Texture);
            }

            nearMap = next;

            // The village's places are worth a label at this scale.
            foreach (var mark in map.Marks)
            {
                nearMap.Marks.Add(new MapMark { Label = mark.Label, At = mark.At, Major = true });
            }
        }

        /// <summary>Fainted: the world goes dark, and the hero wakes in the village some hours later, weak.</summary>
        /// <summary>Dead: the world goes dark, and the path begins again from the first morning, the same hero.</summary>
        void OnFainted()
        {
            if (dying)
            {
                return;
            }

            // On the proving ground a death costs nothing: back on one's feet at the start.
            if (Sandbox)
            {
                dying = true;
                menus.Banner("Вы погибли", "Апертура бессмертного не отпускает: вы снова на ногах");
                menus.FadeThrough(() =>
                {
                    dying = false;
                    BackToStart();
                });

                return;
            }

            dying = true;
            StopAiming();
            cultivator.CancelAbsorbing(null);
            menus.Notify("Вы погибли");
            menus.Banner("Вы погибли", "Путь начинается заново — с первого утра");
            menus.FadeThrough(() =>
            {
                dying = false;
                Begin(true);
                menus.Notify("Вы погибли. Путь начинается заново — с первого утра");
            });
        }

        bool dying;

        /// <summary>A point dropped onto whatever is under it, looked for from `above` metres over it.</summary>
        static Vector3 OnGround(Vector3 at, float above = 100)
        {
            at.y = UnityEngine.Physics.Raycast(new Vector3(at.x, at.y + above, at.z), Vector3.down, out var hit, 400, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : at.y;

            return at;
        }

        void OnStruck(TrainingDummy dummy, BodyPart? part, float damage, bool broke)
        {
            DamageNumbers.Show(dummy.transform.position + Vector3.up * 1.5f, damage, broke || (part.HasValue && Wounds.Of(part.Value).Critical) ? DamageNumbers.Vital : DamageNumbers.Dealt, broke);
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
            Learnt("talk");

            talkingTo = who;
            who.BeginTalk(body.transform.position);
            quests.Count("talk:" + who.Spec.Name);
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

            // A thing to deliver to this one (a task's): handed over.
            var task = quests.Current;

            if (task != null && !quests.Ready && task.Event.StartsWith("deliver:") && task.Event.EndsWith("@" + name))
            {
                var item = task.Event.Substring(8, task.Event.IndexOf('@') - 8);

                if (cultivator.Bag.Has(item, 1))
                {
                    choices.Add(($"Отдать: {ItemBook.Find(item)?.Name.ToLowerInvariant()}", () =>
                    {
                        cultivator.Bag.Take(item, 1);
                        quests.Count(task.Event);
                        Say("Вот спасибо! Передай мою благодарность тому, кто прислал.");
                    }));
                }
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
                    menus.Banner("Новое задание", quest.Title);

                    if (quest.Gives != null)
                    {
                        var got = cultivator.Bag.Add(quest.Gives, quest.GivesCount, clock.Minutes);
                        menus.Notify($"Получено: {ItemBook.Find(quest.Gives)?.Name} ×{got}");
                    }

                    Say("Ступай. И не трать эссенцию попусту.");
                }),
                ("В другой раз", () => Say("Как знаешь. Поручение подождёт.")),
            });
        }

        /// <summary>A task handed back: the giver's thanks and the reward in stones.</summary>
        void HandIn(Villager who)
        {
            if (quests.Ready && quests.Current.Giver == who.Spec.Name && cultivator.Bag.Room(ItemBook.Stone, clock.Minutes) < quests.Current.RewardStones)
            {
                menus.Notify("В сумке нет места для награды — освободите ячейку и приходите снова");

                return;
            }

            var quest = quests.HandIn(who.Spec.Name);

            if (quest == null)
            {
                return;
            }

            var got = cultivator.Bag.Add(ItemBook.Stone, quest.RewardStones, clock.Minutes);
            menus.Notify($"Задание «{quest.Title}» сдано: получено {ItemBook.Stones(got)}");
            menus.Banner("Задание выполнено", $"{quest.Title} · +{ItemBook.Stones(got)}");
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

        // The frame's parts, for the profiler (a development build's timed run, -profile).
        static readonly Unity.Profiling.ProfilerMarker PeopleMarker = new Unity.Profiling.ProfilerMarker("GuWorld.People");
        static readonly Unity.Profiling.ProfilerMarker SkyMarker = new Unity.Profiling.ProfilerMarker("GuWorld.Sky");
        static readonly Unity.Profiling.ProfilerMarker AtmosphereMarker = new Unity.Profiling.ProfilerMarker("GuWorld.Atmosphere");
        static readonly Unity.Profiling.ProfilerMarker InsectsMarker = new Unity.Profiling.ProfilerMarker("GuWorld.Insects");
        static readonly Unity.Profiling.ProfilerMarker MapMarker = new Unity.Profiling.ProfilerMarker("GuWorld.Map");
        static readonly Unity.Profiling.ProfilerMarker HeroMarker = new Unity.Profiling.ProfilerMarker("GuWorld.Cultivator");
        static readonly Unity.Profiling.ProfilerMarker MenusMarker = new Unity.Profiling.ProfilerMarker("GuWorld.Menus");

        void Update()
        {
            if (redirecting)
            {
                return;
            }

            // A hit's moment of stillness over (unless something else has stopped time since: a page, the pause).
            if (hitStopUntil > 0 && Time.unscaledTime >= hitStopUntil)
            {
                hitStopUntil = 0;

                if (Mathf.Approximately(Time.timeScale, HitStopScale))
                {
                    Time.timeScale = 1;
                }
            }

            if (screenshot != null)
            {
                screenshotFrame++;

                // The first frame is the scene's own loading; after it, the village fills in.
                if (screenshotFrame > 1 && screenshotFrame <= 150)
                {
                    startTimes.Add(Time.unscaledDeltaTime * 1000);
                }

                var made = crowds.Sum(c => c.People.Count);

                if (made != peopleMade)
                {
                    peopleMade = made;
                    lastMadeAt = screenshotFrame;
                }

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
                        case "sandbox": OpenPage(Menus.Page.Sandbox); break;
                        case "fusion": OpenPage(Menus.Page.Fusion); break;
                        case "shop": OpenShop(Trade.Shops[0]); break;
                        case "hua": OpenShop(Trade.Shops[1]); break;
                    }
                }

                // `-aim`: the first key's Gu aimed (`-aimgu <id>`: that Gu's) over the picture.
                var aimed = aimGu != null ? GuBook.Find(aimGu) : cultivator.KindOn(0);

                if (aimShot && screenshotFrame > 100 && aimed != null)
                {
                    // On the immortal's aperture the Gu is put in hand (its aim shows of itself).
                    if (Sandbox && screenshotFrame == 101)
                    {
                        PutInSlot(SandboxSlot, aimed);
                    }

                    ShowAim(aimed);
                }

                if (wolvesShot > 0 && screenshotFrame == 60)
                {
                    wolves.CallNear(wolvesShot, view.transform.forward);
                }

                if (doShot != null && (screenshotFrame == 50 || screenshotFrame == 140))
                {
                    // Twice: a command marked "!" waits for the second turn (a Gu used once it is refined and the world has moved).
                    foreach (var line in doShot.Split(';'))
                    {
                        var command = line.Trim();
                        var later = command.StartsWith("!");

                        if (command.Length > 1 && (screenshotFrame == 50) != later)
                        {
                            Debug.Log($"-do {command}: {Commands.Run(command.TrimStart('!'))}");
                        }
                    }
                }

                if (chatShot != null && screenshotFrame == 110)
                {
                    menus.Notify("Пример сообщения в чате");
                    Enter(Mode.Chat);
                    menus.OpenChat(chatShot);
                }

                if (absorbShot && screenshotFrame == 120)
                {
                    cultivator.Aperture.Essence = 10;
                    cultivator.StartAbsorbing();
                }

                // `-cast`: the first slot used just before the picture (the blade in flight).
                if (screenshotFrame == 150 + benchFrames - castFrames && castAt)
                {
                    cultivator.Use(0);
                }

                // `-blow`, `-guard`: the fists.
                if (blowShot.HasValue && screenshotFrame == 150 + benchFrames - blowFrames)
                {
                    melee.Launch(blowShot.Value);
                }

                // `-roll frames`: a roll forward that many frames before the picture.
                if (rollFrames > 0 && screenshotFrame == 150 + benchFrames - rollFrames)
                {
                    dodge.Roll(body.transform.forward);
                }

                if (blowShot.HasValue && screenshotFrame >= 150 + benchFrames - blowFrames)
                {
                    testLog.AppendLine($"frame {screenshotFrame}: blow {melee.Current} phase {melee.Phase:0.00} rig {picture.Figure?.Rig.BlowPhase:0.00} " +
                                       $"guard {picture.Figure?.Rig.Guard:0.00} free {body.Free} dt {Time.deltaTime:0.000} scale {Time.timeScale}");
                }

                if (guardShot && screenshotFrame > 100)
                {
                    melee.Guarding = true;
                }

                // `-pin`: the mark put before the map opens (frame 100); with "go", gone to from the map a moment later.
                if (pinShot is Vector3 pin && screenshotFrame == 95)
                {
                    menus.PinMap(pin.x, pin.z);
                }

                if (orbitView.HasValue && screenshotFrame == 2)
                {
                    view.Yaw += orbitTurn;
                }

                if (!float.IsNaN(pitchShot))
                {
                    view.Pitch = pitchShot;
                }

                if (pinShot.HasValue && pinGo && screenshotFrame == 115)
                {
                    menus.TeleportToMapPin();
                }

                if (fistsShot && screenshotFrame == 100)
                {
                    melee.Ready = true;
                }

                // `-climb`: before the wall, taking hold of it, then up it (W held) — its steps written down.
                if (climbShot is Vector4 climb)
                {
                    ClimbCheck(climb);
                }

                if (testLog != null && screenshotFrame == 150 + benchFrames + 1)
                {
                    System.IO.File.WriteAllText(screenshot + ".log.txt", testLog.ToString());
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
                        // The main thread's own work, without its wait for the screen.
                        cpuSum += timings[0].cpuMainThreadFrameTime - timings[0].cpuMainThreadPresentWaitTime;
                        renderSum += timings[0].cpuRenderThreadFrameTime;
                        gpuCount++;
                    }
                }

                if (screenshotFrame == 150 + benchFrames)
                {
                    if (UnityEngine.Profiling.Profiler.enabled)
                    {
                        UnityEngine.Profiling.Profiler.enabled = false;
                        UnityEngine.Profiling.Profiler.enableBinaryLog = false;
                        UnityEngine.Profiling.Profiler.logFile = "";
                    }

                    ScreenCapture.CaptureScreenshot(screenshot);

                    // `-handlog`: how the hero's fingers are bent at the picture.
                    if (Has(System.Environment.GetCommandLineArgs(), "-handlog") && picture.Figure?.Body != null)
                    {
                        System.IO.File.WriteAllText(screenshot + ".hands.txt", picture.Figure.Body.HandReport());
                    }

                    if (frameTimes.Count > 0)
                    {
                        frameTimes.Sort();
                        var average = frameTimes.Average();
                        var p95 = frameTimes[Mathf.Min(frameTimes.Count - 1, (int)(frameTimes.Count * 0.95f))];
                        System.IO.File.WriteAllText(screenshot + ".txt",
                            $"frames {frameTimes.Count}, average {average:0.0} ms ({1000 / average:0} fps), 95% under {p95:0.0} ms, worst {frameTimes[frameTimes.Count - 1]:0.0} ms, " +
                            $"quality {GameSettings.Quality}, memory {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024)} MB" + System.Environment.NewLine +
                            $"gpu {(gpuCount > 0 ? (gpuSum / gpuCount).ToString("0.0") + " ms" : "n/a")}, cpu {(gpuCount > 0 ? (cpuSum / gpuCount).ToString("0.0") + " ms" : "n/a")}, render thread {(gpuCount > 0 ? (renderSum / gpuCount).ToString("0.0") + " ms" : "n/a")}, vsync {QualitySettings.vSyncCount}" +
                            string.Concat(counters.Select(c => $", {c.Key} {(c.Value.recorder.Valid ? (c.Key == "Main Thread" ? (c.Value.sum / frameTimes.Count / 1e6).ToString("0.0") + " ms" : (c.Value.sum / frameTimes.Count).ToString("0")) : "n/a")}")) +
                            string.Concat(memory.Select(m => $", {m.Key} {(m.Value.Valid ? (m.Value.LastValue / (1024 * 1024)).ToString() + " MB" : "n/a")}")) + System.Environment.NewLine +
                            Census() + System.Environment.NewLine +
                            StartReport() + System.Environment.NewLine +
                            $"costs: {Costs.Report()}" + System.Environment.NewLine);
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
            // The hitboxes only over the game itself (not the main menu, the hero's making, a page).
            HitboxView.InPlay = mode == Mode.Playing || mode == Mode.Talking || mode == Mode.Chat;
            weather = forcedWeather.HasValue ? GuWorld.World.Weather.Of(forcedWeather.Value)
                : mode == Mode.Title ? GuWorld.World.Weather.Fair
                : GuWorld.World.Weather.At(clock.Minutes);
            // What the weather and the hour do in a fight (fire in rain, the moon at night).
            Battle.Rain = weather.Rain;
            var hourNow = clock.MinuteOfDay / 60;
            Battle.Night = hourNow < 5 || hourNow >= 20.5f;

            // Esc while playing (PlayerController frees the cursor) pauses.
            if (mode == Mode.Playing && screenshot == null && Cursor.lockState != CursorLockMode.Locked)
            {
                Enter(Mode.Paused);
                Save();
            }

            if (keyboard != null)
            {
                FunctionKeys(keyboard);

                // Debug: go through the weathers.
                if (debug && !keyboard.f3Key.isPressed && keyboard.rKey.wasPressedThisFrame && !melee.Ready)
                {
                    forcedWeather = forcedWeather switch
                    {
                        null => GuWorld.World.Sky.Clear,
                        GuWorld.World.Sky.Clear => GuWorld.World.Sky.Cloudy,
                        GuWorld.World.Sky.Cloudy => GuWorld.World.Sky.Rain,
                        _ => null,
                    };
                }

                // Esc on a menu steps back (not the Esc that just paused; the chat closes itself).
                if (!wasPlaying && mode != Mode.Chat && keyboard.escapeKey.wasPressedThisFrame)
                {
                    menus.Back();
                }

                // T: the chat; /: the chat with a command begun.
                if (mode == Mode.Playing && screenshot == null && (keyboard.tKey.wasPressedThisFrame || keyboard.slashKey.wasPressedThisFrame))
                {
                    var start = keyboard.slashKey.wasPressedThisFrame ? "/" : "";
                    StopAiming();
                    Enter(Mode.Chat);
                    menus.OpenChat(start);
                }
            }

            // The village lives by the clock (the main menu's by its evening).
            var hourOfDay = (forcedTime ?? (mode == Mode.Title ? TitleTimeOfDay : clock.TimeOfDay)) * 24;
            using (PeopleMarker.Auto())
            {
                foreach (var crowd in crowds)
                {
                    crowd.Tick(hourOfDay, Time.deltaTime, view.transform.position);
                }
            }

            HerbBed.Now = clock.Minutes;

            if (mode == Mode.Playing && (nearPainting != null || Time.frameCount % 30 == 0))
            {
                using (MapMarker.Auto())
                {
                    RedrawNearMap();
                }
            }

            if (mode == Mode.Playing && screenshot == null)
            {
                Interact(keyboard);
            }

            if (mode == Mode.Playing && Time.frameCount % 20 == 0)
            {
                Visits();
            }

            if (mode == Mode.Playing)
            {
                Unstick();

                if (Time.unscaledTime >= questTargetAt)
                {
                    questTargetAt = Time.unscaledTime + 0.25f;
                    QuestTarget = Sandbox ? null : FindQuestTarget();
                }

                if (cultivator.Absorbing)
                {
                    Learnt("absorb");
                }

                if (melee.Ready)
                {
                    Learnt("fists");
                }
            }

            // The Gu Master's keys: the Gu (held: aimed; let go: used), the stone, the view, the hero's pages.
            if (mode == Mode.Playing && keyboard != null && screenshot == null)
            {
                Skills(keyboard, Mouse.current);

                if (keyboard.vKey.wasPressedThisFrame)
                {
                    if (cultivator.Absorbing) cultivator.CancelAbsorbing("Поглощение прервано");
                    else cultivator.StartAbsorbing();
                }

                if (keyboard.f5Key.wasPressedThisFrame)
                {
                    SetFirstPerson(!view.FirstPerson);
                }

                // + and −: the minimap closer in and farther out.
                if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)
                {
                    menus.ZoomMinimap(-1);
                }
                else if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)
                {
                    menus.ZoomMinimap(1);
                }

                // On the immortal's aperture Tab (and the bag's and aperture's keys) open all the Gu.
                if (Sandbox && (keyboard.tabKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame ||
                                keyboard.kKey.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame)) OpenPage(Menus.Page.Sandbox);
                else if (keyboard.kKey.wasPressedThisFrame) OpenPage(Menus.Page.Aperture);
                else if (keyboard.iKey.wasPressedThisFrame) OpenPage(Menus.Page.Bag);
                else if (keyboard.mKey.wasPressedThisFrame) OpenPage(Menus.Page.Map);
                else if (keyboard.bKey.wasPressedThisFrame) OpenPage(Menus.Page.Body);
                else if (keyboard.uKey.wasPressedThisFrame) OpenPage(Menus.Page.Fusion);
                else if (Sandbox && keyboard.pKey.wasPressedThisFrame) OpenPage(Menus.Page.Sandbox);
            }
            else if (mode == Mode.Paused && keyboard != null && !wasPlaying && IsHeroPage(menus.Current))
            {
                // On the hero's pages the same keys go between them, and the page's own key closes it.
                var to = keyboard.kKey.wasPressedThisFrame ? Menus.Page.Aperture : keyboard.iKey.wasPressedThisFrame ? Menus.Page.Bag :
                    keyboard.mKey.wasPressedThisFrame ? Menus.Page.Map : keyboard.bKey.wasPressedThisFrame ? Menus.Page.Body :
                    keyboard.uKey.wasPressedThisFrame ? Menus.Page.Fusion :
                    Sandbox && keyboard.pKey.wasPressedThisFrame ? Menus.Page.Sandbox : Menus.Page.None;

                if (Sandbox && (keyboard.tabKey.wasPressedThisFrame || keyboard.kKey.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame))
                {
                    to = Menus.Page.Sandbox;
                }

                if (to != Menus.Page.None)
                {
                    if (to == menus.Current) Resume();
                    else menus.Show(to);
                }
            }

            if (debug && keyboard != null && !keyboard.f3Key.isPressed && keyboard.gKey.wasPressedThisFrame)
            {
                cultivator.NextGrade();
            }

            if (debug && keyboard != null && keyboard.yKey.wasPressedThisFrame)
            {
                cultivator.Bag.Add(ItemBook.Stone, 50, clock.Minutes);
                menus.Notify("Отладка: +50 первобытных камней");
            }

            if (mode != Mode.Playing)
            {
                StopAiming();
                melee.Stop();
            }

            // Drawing in a stone: walking slowly is all one can do; a jump, a run or a climb breaks it off.
            if (cultivator.Absorbing && (mode == Mode.Playing || mode == Mode.Talking))
            {
                if ((!body.Free && !body.Sitting) || body.Velocity.y > 1.5f || body.Velocity.y < -5 || (keyboard != null && screenshot == null &&
                    (keyboard.spaceKey.wasPressedThisFrame || (PlayerController.Sprinting(keyboard) && body.HorizontalSpeed > 0.5f))))
                {
                    cultivator.CancelAbsorbing("Поглощение прервано");
                }

                cultivator.UpdateAbsorbing(Time.deltaTime);
            }

            // Gu on the body: running faster, jumping higher — and held fast inside a ward.
            player.SpeedScale = cultivator.Warded ? 0 : (cultivator.Absorbing ? 0.45f : 1) * cultivator.SpeedBoost * (health.Shocked ? 0.45f : 1);
            player.NoSprint = cultivator.Absorbing;
            body.JumpScale = cultivator.Warded ? 0.1f : cultivator.JumpBoost;

            if (mode == Mode.Playing || mode == Mode.Talking || mode == Mode.Chat)
            {
                var before = clock.Minutes;
                clock.Advance(Time.deltaTime);
                using (HeroMarker.Auto()) cultivator.Tick(clock.Minutes);
                // Sitting where no beast hunts the hero: meditation.
                cultivator.Meditating = body.Sitting && !Foes.Hunting(body.transform.position, 40);

                if (cultivator.Meditating)
                {
                    Learnt("meditate");
                }
                cultivator.Step(Time.deltaTime);

                if (Sandbox)
                {
                    TopUpStones();
                }
                health.Mend(clock.Minutes - before);
                sinceSave += Time.deltaTime;

                if (sinceSave >= AutosaveEvery)
                {
                    Save();
                }
            }

            // The main menu's own light and fair sky; the game's clock and weather everywhere else.
            var time = forcedTime ?? (mode == Mode.Title ? TitleTimeOfDay : clock.TimeOfDay);
            using (SkyMarker.Auto()) sky.Apply(time, weather);
            StudioLight();
            using (AtmosphereMarker.Auto()) atmosphere?.Apply(sky, weather, time, view.transform);
            using (InsectsMarker.Auto()) insects?.Apply(sky, weather, view.transform);

            if (Time.unscaledDeltaTime > 0)
            {
                fps = Mathf.Lerp(fps, 1 / Time.unscaledDeltaTime, 0.05f);
            }

            using (MenusMarker.Auto()) menus.SetFps(fps);
        }

        // ── The Gu's keys ────────────────────────────────────────────

        /// <summary>The Gu's key held down (its skill being aimed), or -1.</summary>
        int aiming = -1;
        SkillAim aim;
        Keyboard keysOf;
        UnityEngine.InputSystem.Controls.KeyControl[] guKeys;

        /// <summary>
        /// The Gu's keys, 1–4, the way a MOBA has them: held down, the skill is aimed — its path or ring
        /// shown toward the crosshair — and let go, it is used; the right mouse button puts it away unused
        /// (the left uses it at once). Nothing aimed, the left button uses the first key's Gu at once; with
        /// the fists ready (F) the mouse fights with them instead — the left button a blow (jab, cross, kick
        /// in a chain), the right held a guard — and Q, R throw the special blows.
        /// </summary>
        void Skills(Keyboard keyboard, Mouse mouse)
        {
            if (keysOf != keyboard)
            {
                keysOf = keyboard;
                guKeys = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key };
            }

            var click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            melee.Strength = (1 + 0.6f * (cultivator.Aperture.Rank - 1)) * cultivator.FistPower;
            // A blow turns to what lies the way the camera looks.
            melee.AimForward = view.transform.forward;

            // Alt (or the mouse's back button): a roll the way the hero moves — backward, standing still.
            if (keyboard.leftAltKey.wasPressedThisFrame || (mouse != null && mouse.backButton.wasPressedThisFrame))
            {
                var forward = view.transform.forward;
                forward.y = 0;
                forward = forward.sqrMagnitude > 0.01f ? forward.normalized : body.transform.forward;
                var right = Vector3.Cross(Vector3.up, forward);
                var way = forward * ((keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)) +
                          right * ((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0));

                if (dodge.Roll(way))
                {
                    melee.Stop();
                    StopAiming();
                    Learnt("dodge");
                }
            }

            // F: the fists ready, or put away.
            if (keyboard.fKey.wasPressedThisFrame)
            {
                melee.Ready = !melee.Ready;
                menus.Notify(melee.Ready ? "Кулаки наготове" : "Кулаки опущены");
            }

            if (Sandbox)
            {
                SandboxSkills(keyboard, mouse, click);

                return;
            }

            if (aiming < 0)
            {
                for (var i = 0; i < guKeys.Length && aiming < 0; i++)
                {
                    if (guKeys[i].wasPressedThisFrame)
                    {
                        // An empty key says so; a Gu's starts aiming.
                        if (cultivator.KindOn(i) == null) cultivator.Use(i);
                        else aiming = i;
                    }
                }

                if (aiming < 0)
                {
                    if (melee.Ready)
                    {
                        if (click)
                        {
                            melee.Press();
                        }

                        if (keyboard.qKey.wasPressedThisFrame && !keyboard.f3Key.isPressed)
                        {
                            Special(Blow.Heavy, "Мощный удар");
                        }

                        if (keyboard.rKey.wasPressedThisFrame)
                        {
                            Special(Blow.Spin, "Удар с разворота");
                        }

                        melee.Guarding = mouse != null && mouse.rightButton.isPressed && body.Free;
                    }
                    else
                    {
                        if (click)
                        {
                            cultivator.Use(0);
                        }

                        melee.Guarding = false;
                    }

                    // Fighting: the hero faces where the crosshair is.
                    player.FaceView = melee.Busy && melee.Target == null;

                    return;
                }

                melee.Guarding = false;
            }

            var kind = cultivator.KindOn(aiming);

            if (kind == null || (mouse != null && mouse.rightButton.wasPressedThisFrame))
            {
                StopAiming();

                return;
            }

            if (guKeys[aiming].isPressed && !click)
            {
                ShowAim(kind);

                return;
            }

            // Let go (or the left button while it is held): used, where the crosshair points.
            var key = aiming;
            StopAiming();
            if (cultivator.Use(key))
            {
                Learnt("cast");
            }
        }

        // ── The immortal's aperture: eight Gu at hand ─────────────────

        /// <summary>The aperture's eight cells at hand (Gu kinds; null: empty), and the one chosen.</summary>
        public string[] SandboxBar { get; } = new string[SandboxCells];
        public int SandboxSlot { get; set; }
        public const int SandboxCells = 8;

        /// <summary>What is at hand when one comes in: one of each sort, to try at once.</summary>
        static readonly string[] SandboxStart =
        {
            "moonlight", "bone_spear", "exploding_egg", "ice_blade", "bronze_skin", "eating_wind", "nine_leaf_grass", "fire_dragon",
        };

        /// <summary>
        /// A Gu put in a cell at hand (null: emptied). A Gu that can be refined goes into the aperture's
        /// ring with it — fed, rested — and leaves it when no cell holds it any more; one used once
        /// stays only at hand (it is never spent here).
        /// </summary>
        public void PutInSlot(int slot, GuKind kind)
        {
            if (slot < 0 || slot >= SandboxCells)
            {
                return;
            }

            var old = SandboxBar[slot];
            SandboxBar[slot] = kind?.Id;

            if (old != null && old != kind?.Id && System.Array.IndexOf(SandboxBar, old) < 0)
            {
                cultivator.Release(cultivator.First(old));
            }

            if (kind != null && !kind.OneUse)
            {
                cultivator.Grant(kind, -1, true);
            }
        }

        /// <summary>
        /// The keys on the immortal's aperture, as simple as can be: 1–8 or the wheel pick a cell,
        /// the left button uses its Gu where the crosshair points (the right button held shows where
        /// it will go). The fists (F) fight as anywhere.
        /// </summary>
        void SandboxSkills(Keyboard keyboard, Mouse mouse, bool click)
        {
            var digits = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key,
                keyboard.digit5Key, keyboard.digit6Key, keyboard.digit7Key, keyboard.digit8Key };

            for (var i = 0; i < digits.Length; i++)
            {
                if (digits[i].wasPressedThisFrame)
                {
                    SandboxSlot = i;
                }
            }

            var wheel = mouse != null ? mouse.scroll.ReadValue().y : 0;

            if (wheel != 0)
            {
                SandboxSlot = (SandboxSlot + (wheel < 0 ? 1 : -1) + SandboxCells) % SandboxCells;
            }

            if (melee.Ready)
            {
                StopAiming();

                if (click)
                {
                    melee.Press();
                }

                if (keyboard.qKey.wasPressedThisFrame && !keyboard.f3Key.isPressed) Special(Blow.Heavy, "Мощный удар");
                if (keyboard.rKey.wasPressedThisFrame) Special(Blow.Spin, "Удар с разворота");
                melee.Guarding = mouse != null && mouse.rightButton.isPressed && body.Free;
                player.FaceView = melee.Busy && melee.Target == null;

                return;
            }

            melee.Guarding = false;
            var kind = GuBook.Find(SandboxBar[SandboxSlot]);

            // The chosen Gu's aim is always there: what it will do, where.
            if (kind != null && !kind.OneUse && !kind.Passive)
            {
                ShowAim(kind);
            }
            else
            {
                StopAiming();
            }

            if (!click)
            {
                return;
            }

            if (kind == null)
            {
                menus.Notify($"Ячейка {SandboxSlot + 1} пуста — Tab: выбрать Гу");

                return;
            }

            if (kind.OneUse)
            {
                cultivator.ActivateKind(kind);

                return;
            }

            // A Gu that died of being forced comes back to the ring at once here.
            if (cultivator.First(kind.Id) == null)
            {
                cultivator.Grant(kind, -1, true);
            }

            cultivator.UseGu(kind.Id);
        }

        /// <summary>A special blow (Q, R) thrown — or why not, when it still rests.</summary>
        void Special(Blow blow, string name)
        {
            if (!melee.Special(blow) && melee.RestLeft(blow) > 0)
            {
                menus.Notify($"{name} — ещё {melee.RestLeft(blow):0.0} с");
            }
        }

        /// <summary>
        /// A skill being aimed, shown as what it will do (SkillAim): a missile's very course and the missile flying it,
        /// a blast's arc and the ground it covers, a sweep's arc, a rush's or a blink's way and where it ends, a leap's
        /// arc, a skill on oneself round the hero — worked out as the skill itself works it out.
        /// </summary>
        void ShowAim(GuWorld.Cultivation.GuKind kind)
        {
            aim ??= SkillAim.Create(body.transform);
            var usable = cultivator.Usable(kind);
            var feet = body.transform.position;
            var target = cultivator.AimPoint(kind);
            var thrown = kind.Skill is SkillKind.Moonblade or SkillKind.Bolt or SkillKind.Tame;
            player.FaceView = kind.Aimed || kind.Skill is SkillKind.Cleave or SkillKind.Dash or SkillKind.Flight;
            view.Aiming = thrown;
            var size = GuShapes.Size(kind, cultivator.MoonScale);

            switch (kind.Skill)
            {
                case SkillKind.Moonblade:
                case SkillKind.Bolt:
                case SkillKind.Tame:
                {
                    var shot = cultivator.PlanShot(kind, target);
                    var reach = (kind.Reach > 0 ? kind.Reach : shot.Length) * (kind.Skill == SkillKind.Moonblade ? cultivator.MoonScale : 1);
                    aim.ShowShot(shot.From, shot.Direction, shot.Length, reach, kind, size, cultivator.NextRoll, usable, shot.Hits, shot.Normal);
                    break;
                }

                case SkillKind.Burst:
                    aim.ShowBlast(cultivator.HandFor(target), cultivator.BlastPoint(target), kind.Radius, kind, size, usable);
                    break;

                case SkillKind.Cleave:
                    aim.ShowSweep(feet, target - feet, kind.Reach, 60, kind, usable);
                    break;

                case SkillKind.Dash:
                    aim.ShowDash(feet, cultivator.DashEnd(kind, target), kind, usable);
                    break;

                case SkillKind.Blink:
                    aim.ShowBlink(feet, cultivator.BlinkPoint(kind, target), kind, usable);
                    break;

                case SkillKind.Decoy:
                {
                    var forward = target - feet;
                    forward.y = 0;
                    aim.ShowFigureAt(feet + (forward.sqrMagnitude > 0.01f ? forward.normalized : body.transform.forward) * 2.5f, kind, usable);
                    break;
                }

                case SkillKind.Flight:
                    aim.ShowLeap(feet, cultivator.FlightStart(kind), 25, kind, usable);
                    break;

                case SkillKind.JadeSkin:
                case SkillKind.Skin:
                case SkillKind.Ward:
                case SkillKind.Hide:
                    aim.ShowSelf(feet, 1, kind, usable, SkillAim.Self.Shell);
                    break;

                case SkillKind.Howl:
                case SkillKind.Flash:
                    aim.ShowSelf(feet, kind.Radius, kind, usable, SkillAim.Self.Reach);
                    break;

                case SkillKind.Sense:
                    aim.ShowSelf(feet, Mathf.Min(kind.Range, 30), kind, usable, SkillAim.Self.Reach);
                    break;

                default:
                    aim.ShowSelf(feet, 1, kind, usable, SkillAim.Self.Body);
                    break;
            }
        }

        void StopAiming()
        {
            aiming = -1;

            if (aim != null)
            {
                aim.Hide();
            }

            if (player != null)
            {
                player.FaceView = false;
            }

            if (view != null)
            {
                view.Aiming = false;
            }
        }

        /// <summary>Into the hero's eyes (F5) or back behind them.</summary>
        void SetFirstPerson(bool on)
        {
            view.SetFirstPerson(on);
            picture.FirstPerson = on;
        }

        /// <summary>A "visit:" task: done once the hero is within 30 m of its place.</summary>
        void Visits()
        {
            var task = quests.Current;

            if (task == null || quests.Ready || !task.Event.StartsWith("visit:") || !Places.TryGetValue(task.Event.Substring(6), out var place))
            {
                return;
            }

            var at = body.transform.position;

            if (new Vector2(at.x - place.x, at.z - place.z).magnitude < 30)
            {
                quests.Count(task.Event);
            }
        }

        // ── The function keys (as Minecraft has them) ─────────────────

        bool f3Combo;

        /// <summary>
        /// F3 with another key, Minecraft's way: F3+B the hitboxes, F3+G the borders of the streamed squares,
        /// F3+Q the list of these keys; F3 alone (let go without another) the debug panel. F4 and F7 the hitbox
        /// mode on and off; F1 hides the interface; F2 saves a picture of the screen.
        /// </summary>
        void FunctionKeys(Keyboard keyboard)
        {
            if (keyboard.f3Key.isPressed)
            {
                if (keyboard.bKey.wasPressedThisFrame)
                {
                    f3Combo = true;
                    Hitboxes = !Hitboxes;
                    menus.Notify(Hitboxes ? "Хитбоксы показаны (F3+B — скрыть)" : "Хитбоксы скрыты");
                }

                if (keyboard.gKey.wasPressedThisFrame)
                {
                    f3Combo = true;
                    HitboxView.ChunkBorders = !HitboxView.ChunkBorders;
                    menus.Notify(HitboxView.ChunkBorders ? "Границы участков мира показаны (128 м)" : "Границы участков скрыты");
                }

                if (keyboard.qKey.wasPressedThisFrame)
                {
                    f3Combo = true;
                    menus.Notify("F3 — отладка · F3+B — хитбоксы · F3+G — границы участков · F7 — режим хитбоксов · F1 — скрыть интерфейс · F2 — снимок экрана · F5 — вид · T — чат · / — команда");
                }
            }

            if (keyboard.f3Key.wasReleasedThisFrame)
            {
                if (!f3Combo)
                {
                    debug = !debug;
                }

                f3Combo = false;
            }

            // F4 or F7: the hitbox mode on or off (it stays as left, the settings keep it).
            if (keyboard.f4Key.wasPressedThisFrame || keyboard.f7Key.wasPressedThisFrame)
            {
                Hitboxes = !Hitboxes;
                menus.Notify(Hitboxes ? "Режим хитбоксов включён (F7 — выключить)" : "Режим хитбоксов выключен");
            }

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                menus.HudHidden = !menus.HudHidden;
            }

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                var folder = System.IO.Path.Combine(Application.persistentDataPath, "Screenshots");
                System.IO.Directory.CreateDirectory(folder);
                var file = System.IO.Path.Combine(folder, $"gu_world_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
                ScreenCapture.CaptureScreenshot(file);
                menus.Notify($"Снимок сохранён: {file}");
            }
        }

        // ── The chat's commands (ICommandHost) ───────────────────────

        Commands commands;
        Dictionary<string, Vector3> places;

        public Commands Commands => commands ??= new Commands(this);

        public void CloseChat()
        {
            if (mode == Mode.Chat)
            {
                Enter(Mode.Playing);
            }
        }

        public WorldClock Clock => clock;

        public IReadOnlyDictionary<string, Vector3> Places
        {
            get
            {
                if (places != null)
                {
                    return places;
                }

                places = new Dictionary<string, Vector3> { ["spawn"] = player.Spawn };

                if (Location.id == QingMao.Id)
                {
                    foreach (var settlement in QingMao.Settlements)
                    {
                        places[settlement.Id] = new Vector3(settlement.Centre.x, float.NaN, settlement.Centre.y);
                    }

                    foreach (var (name, at) in new[] { ("peak", QingMao.Peak), ("fore_peak", QingMao.ForePeak), ("spring", QingMao.Spring), ("cave", QingMao.SecretCave),
                                 ("grove", QingMao.Grove), ("pool", QingMao.Pool), ("waterfall", QingMao.FallTop) })
                    {
                        places[name] = new Vector3(at.x, float.NaN, at.y);
                    }
                }

                // The hero's own village's squares, gates, stalls…
                foreach (var layout in FindObjectsByType<VillageLayout>(FindObjectsSortMode.None))
                {
                    if (layout.Settlement != "gu_yue")
                    {
                        continue;
                    }

                    foreach (var spot in layout.Places)
                    {
                        places.TryAdd(spot.name.Replace(' ', '_').ToLowerInvariant(), spot.at);
                    }
                }

                return places;
            }
        }

        public void Teleport(Vector3 at)
        {
            // The streamed squares there first, so the hero stands on what is built there, not under it.
            if (ChunkStreamer.Current != null)
            {
                ChunkStreamer.Current.LoadNowAround(at);
            }

            UnityEngine.Physics.SyncTransforms();

            if (float.IsNaN(at.y))
            {
                var probe = new Vector3(at.x, 0, at.z);
                at.y = (Location.id == QingMao.Id ? QingMao.GroundAt(probe) : OnGround(probe, 300).y) + 1;
            }

            // Onto what one can stand on there with room above — a roof, a terrace, the ground — never inside a house.
            at = body.SafeSpot(at) ?? at;
            body.PlaceAt(at);
            player.PlaceCamera();
            // The houses there may only now be made round the hero: watched for a few seconds.
            unstickUntil = Time.unscaledTime + 4;
        }

        float unstickUntil;

        /// <summary>After a teleport: if what was made there since holds the hero inside it, out onto it.</summary>
        void Unstick()
        {
            if (Time.unscaledTime > unstickUntil || body == null || body.OnWall || !body.Stuck)
            {
                return;
            }

            if (body.SafeSpot(body.transform.position, 40) is Vector3 free)
            {
                body.PlaceAt(free);
            }
        }

        public void SetWeather(GuWorld.World.Sky? sky) => forcedWeather = sky;

        public bool FirstPerson
        {
            get => view.FirstPerson;
            set => SetFirstPerson(value);
        }

        public bool Hitboxes
        {
            get => GameSettings.ShowHitboxes;
            set
            {
                GameSettings.ShowHitboxes = value;
                HitboxView.On = value;
            }
        }

        public void Kill() => health.Strike(Health.Max * 10);

        public int SpawnWolves(int count) => wolves != null ? wolves.CallNear(count) : 0;

        WolfPacks wolves;

        /// <summary>The wolves' dens: in the forest round the hero's village, well outside its walls (none on the proving ground).</summary>
        IEnumerable<(Vector3 at, WolfKind[] kinds)> WolfDens()
        {
            if (Location.id != QingMao.Id)
            {
                yield break;
            }

            var village = QingMao.Find("gu_yue");

            (Vector3, WolfKind[]) Den(float degrees, float beyond, WolfKind[] kinds)
            {
                var angle = degrees * Mathf.Deg2Rad;
                var at = new Vector3(village.Centre.x + Mathf.Cos(angle) * (village.Radius + beyond), 0, village.Centre.y + Mathf.Sin(angle) * (village.Radius + beyond));
                at.y = QingMao.GroundAt(at);

                return (at, kinds);
            }

            // Round the village: grey wolves, the lightning wolves' outriders, one bold lightning wolf with its pack.
            var g = WolfKinds.Grey;
            var l = WolfKinds.Lightning;
            yield return Den(205, 210, new[] { g, g, g });
            yield return Den(160, 210, new[] { g, g, g, g });
            yield return Den(250, 210, new[] { g, g, l });
            yield return Den(295, 230, new[] { l, l, l });
            yield return Den(25, 260, WolfPacks.PackOf(WolfKinds.Bold));
            // Deeper in the forest: a frenzy lightning wolf's pack; and far up, the wolf tide's den with its thunder crown wolf.
            yield return Den(230, 700, WolfPacks.PackOf(WolfKinds.Frenzy));
            yield return Den(320, 750, WolfPacks.PackOf(WolfKinds.Frenzy));
            yield return Den(275, 1200, WolfPacks.PackOf(WolfKinds.ThunderCrown));
        }

        /// <summary>A pack of a kind of wolf called up near the hero (a king comes with its pack); how many came.</summary>
        public int SpawnPack(WolfKind kind, int count) =>
            wolves != null ? wolves.CallNear(kind.Tier > 0 ? WolfPacks.PackOf(kind, 1) : WolfPacks.PackOf(kind, Mathf.Max(1, count)), view.transform.forward) : 0;

        Patrols patrols;

        /// <summary>The Bai's and the Xiong's villages, whose Gu Masters keep posts round them (none on the proving ground).</summary>
        IEnumerable<(Vector3 village, float radius, string clan)> ClanLands()
        {
            if (Location.id != QingMao.Id)
            {
                yield break;
            }

            foreach (var id in new[] { "bai", "xiong" })
            {
                var village = QingMao.Find(id);
                var at = new Vector3(village.Centre.x, 0, village.Centre.y);
                at.y = QingMao.GroundAt(at);

                yield return (at, village.Radius, id);
            }
        }

        public int SpawnMasters(string clan, int rank, int count)
        {
            if (patrols == null)
            {
                return 0;
            }

            var kinds = new MasterKind[Mathf.Clamp(count, 1, 6)];

            for (var i = 0; i < kinds.Length; i++)
            {
                kinds[i] = MasterKinds.Of(clan, rank);
            }

            return patrols.CallNear(kinds, view.transform.forward);
        }

        /// <summary>A Gu Master says something aloud: heard within earshot.</summary>
        void OnMasterSpoke(GuMaster master, string line)
        {
            if (body != null && Vector3.Distance(master.transform.position, body.transform.position) < 60)
            {
                menus.Notify($"{master.Title}: «{line}»");
            }
        }

        /// <summary>A Gu Master killed: the stones in their pouch into the bag, and now and then one of their Gu.</summary>
        void OnMasterKilled(GuMaster master)
        {
            var kind = master.Kind;
            var stones = kind.Rank switch
            {
                1 => Random.Range(4, 11),
                2 => Random.Range(18, 41),
                _ => Random.Range(70, 151),
            };
            var kept = cultivator.Bag.Add(ItemBook.Stone, stones, clock.Minutes);
            menus.Notify($"{master.Title} повержен — первобытных камней: {kept}");

            foreach (var (id, chance) in new[] { (kind.Far, 0.3f), (kind.Shield, 0.15f), (kind.Close, 0.15f), (kind.Might, 0.1f) })
            {
                var found = GuBook.Find(id);

                if (found != null && Random.value < chance && cultivator.Bag.Add(found.Item, 1, clock.Minutes) > 0)
                {
                    menus.Notify($"С тела снят Гу: {found.Name} — в сумке");
                }
            }

            if (kind.Tier > 0)
            {
                menus.Banner($"{master.Title} повержен", master.Rung);
            }

            quests.Count("kill:master");
        }

        /// <summary>A beast king does something the hero should know.</summary>
        void OnWolfRoared(Wolf wolf, string what)
        {
            if (body != null && Vector3.Distance(wolf.transform.position, body.transform.position) < 60)
            {
                menus.Notify($"{wolf.Kind.Name} {what}");
            }
        }

        /// <summary>`-climb x,z,yaw,seconds` (a check): the hero stood there facing the wall, taking hold of it at frame 100, climbing for the seconds given.</summary>
        void ClimbCheck(Vector4 climb)
        {
            var yaw = climb.z * Mathf.Deg2Rad;

            if (screenshotFrame == 100)
            {
                if (float.IsNaN(climb.x))
                {
                    // The nearest house: stood a little way out from it, on the side toward the hero, facing it.
                    var at = body.transform.position;
                    Collider house = null;

                    foreach (var collider in UnityEngine.Physics.OverlapSphere(at, 60, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (collider is MeshCollider && collider.GetComponentInParent<HitboxTag>() is HitboxTag tag && tag.Kind == HitboxKind.Building &&
                            collider.bounds.size.y > 4 && collider.bounds.size.y < 14 &&
                            (house == null || (collider.bounds.center - at).sqrMagnitude < (house.bounds.center - at).sqrMagnitude))
                        {
                            house = collider;
                        }
                    }

                    if (house == null)
                    {
                        testLog.AppendLine("frame 100: no house near");

                        return;
                    }

                    var middle = house.bounds.center;
                    var away = at - middle;
                    away.y = 0;
                    away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
                    // Out of the house along that line, then back to just before its wall.
                    var outside = middle + away * (house.bounds.extents.magnitude + 2);
                    var feet = new Vector3(outside.x, middle.y + 2, outside.z);

                    if (house.Raycast(new Ray(feet, -away), out var wall, 100))
                    {
                        outside = wall.point + wall.normal * 0.8f;
                    }

                    climb = new Vector4(outside.x, outside.z, Mathf.Atan2(-away.x, -away.z) * Mathf.Rad2Deg, climb.w);
                    climbShot = climb;
                    yaw = climb.z * Mathf.Deg2Rad;
                    testLog.AppendLine($"frame 100: the nearest house {house.name} ({house.transform.parent?.name}), its bounds {house.bounds}");
                }

                Teleport(new Vector3(climb.x, float.NaN, climb.y));
                body.Facing = yaw;
                view.Yaw = yaw + Mathf.PI;
                var held = body.GrabWall(Mathf.Sin(yaw), Mathf.Cos(yaw));
                testLog.AppendLine($"frame 100: at {body.transform.position}, took hold of a wall: {held}{(held ? $", its normal {body.WallNormal}" : "")}");
            }
            else if (screenshotFrame > 100 && screenshotFrame <= 100 + climb.w * 60)
            {
                if (body.OnWall)
                {
                    body.WallTick(Time.deltaTime, 1, 0, false, false);
                }

                if (screenshotFrame % 20 == 0)
                {
                    testLog.AppendLine($"frame {screenshotFrame}: at {body.transform.position}, on the wall {body.OnWall}, climbing over {body.ClimbProgress?.ToString("0.00") ?? "-"}, grounded {body.Grounded}");
                }
            }
        }

        /// <summary>A fist or a foot landed: the camera jolts a little (more on something solid that does not give).</summary>
        void OnFistLanded(Collider what, Vector3 point)
        {
            // On a body: a spark, the camera jolted, and the world held still for a blink (longer for a finishing blow).
            var onBody = what.GetComponent<HitZone>() != null || what.GetComponentInParent<IStrikable>() != null;
            var heavy = melee.Current is Blow.Kick or Blow.Heavy or Blow.Spin;
            view.Shake(onBody ? (heavy ? 0.22f : 0.12f) : 0.04f);
            GuGlow.Flash(point, onBody ? (heavy ? 0.45f : 0.3f) : 0.16f, new Color(1f, 0.85f, 0.6f), 0.16f);

            if (onBody && mode == Mode.Playing)
            {
                HitStop(heavy ? 0.09f : 0.05f);
            }
        }

        Dodge dodge;

        /// <summary>A blow parried: a bright spark, the word, the world held still a little longer than for a hit.</summary>
        void OnParried(Vector3 point, GameObject source)
        {
            GuGlow.Flash(point, 0.5f, new Color(1f, 0.95f, 0.75f), 0.22f);
            DamageNumbers.Word(point + Vector3.up * 0.3f, "парирование", DamageNumbers.Vital);
            Learnt("parry");
            view.Shake(0.18f);

            if (mode == Mode.Playing)
            {
                HitStop(0.12f);
            }
        }

        const float HitStopScale = 0.05f;
        float hitStopUntil;

        /// <summary>The world held almost still for a moment (a blow's weight).</summary>
        void HitStop(float seconds)
        {
            if (Time.timeScale < 0.99f && !Mathf.Approximately(Time.timeScale, HitStopScale))
            {
                return;
            }

            Time.timeScale = HitStopScale;
            hitStopUntil = Time.unscaledTime + seconds;
        }

        /// <summary>A blow landed where it can be seen: its number rises from the wound.</summary>
        void OnStrikeShown(Vector3 at, string who, BodyPart? part, float harm, bool down)
        {
            var vital = part.HasValue && Wounds.Of(part.Value).Critical;
            DamageNumbers.Show(at, harm, who == "Вы" ? DamageNumbers.Taken : vital || down ? DamageNumbers.Vital : DamageNumbers.Dealt, vital || down);
        }

        /// <summary>A wolf killed: its fang into the bag, and the task counts it.</summary>
        void OnWolfKilled(Wolf wolf)
        {
            // Its fangs; and from a beast king's body, now and then, the Gu that lived in it.
            var kind = wolf.Kind;
            var kept = cultivator.Bag.Add(ItemBook.WolfFang, kind.Fangs, clock.Minutes);
            menus.Notify(kept > 0 ? $"{kind.Name} (ур. {wolf.Level}) убит — клыков в сумке: {kept}" : $"{kind.Name} убит — клыки не поместились в сумку");

            foreach (var (gu, chance) in kind.Drops)
            {
                var found = GuBook.Find(gu);

                if (found != null && Random.value < chance && cultivator.Bag.Add(found.Item, 1, clock.Minutes) > 0)
                {
                    menus.Notify($"Из тела зверя вышел Гу: {found.Name} — в сумке");
                }
            }

            if (kind.Tier > 0)
            {
                menus.Banner($"{kind.Name} повержен", $"{kind.TierName} · уровень {wolf.Level}");
            }
            quests.Count("kill:wolf");
        }

        static bool IsHeroPage(Menus.Page page) => page == Menus.Page.Bag || page == Menus.Page.Aperture || page == Menus.Page.Map || page == Menus.Page.Body || page == Menus.Page.Fusion ||
                                                   page == Menus.Page.Sandbox;

        /// <summary>
        /// What the hero can do where they stand: talk to the one before them (E), or gather
        /// a bed in flower beside them (E held for a moment) — or see that it is bare.
        /// </summary>
        void Interact(Keyboard keyboard)
        {
            var at = body.transform.position;
            var pressed = keyboard != null && keyboard.eKey.wasPressedThisFrame;

            // On a wall: how to climb it and get off it.
            if (body.OnWall)
            {
                menus.SetPrompt("W/S — вверх, вниз   ·   A/D — в сторону   ·   Пробел — оттолкнуться   ·   C — отпустить");

                return;
            }

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
            if (redirecting)
            {
                return;
            }

            if (orbitView is Vector3 orbit && body != null)
            {
                var around = body.Facing + orbit.x * Mathf.Deg2Rad;
                var look = body.transform.position + Vector3.up * orbit.z;
                view.Following = false;
                view.transform.position = look + new Vector3(Mathf.Sin(around), 0, Mathf.Cos(around)) * orbit.y + Vector3.up * 0.25f;
                view.transform.rotation = Quaternion.LookRotation(look - view.transform.position, Vector3.up);

                return;
            }

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
            var height = HeroView.HeightOf(look);
            var facing = body.Facing + 0.55f + 0.35f * Mathf.Sin(titleAngle);
            var direction = new Vector3(Mathf.Sin(facing), 0, Mathf.Cos(facing));
            var target = body.transform.position + Vector3.up * height * 0.62f;
            var aside = Vector3.Cross(Vector3.up, direction).normalized * 0.9f;
            var camera = view.transform;
            camera.position = target + direction * 3.4f + Vector3.up * 0.1f + aside;
            camera.rotation = Quaternion.LookRotation(target + aside - camera.position, Vector3.up);
        }

        /// <summary>Making the hero: the camera in front of them, turned round by dragging right of the panel, the wheel bringing it from the whole figure in to the face.</summary>
        void CreatorCamera()
        {
            var mouse = Mouse.current;

            if (mouse != null && mouse.position.ReadValue().x > menus.CreatorPanelWidth)
            {
                if (mouse.leftButton.isPressed)
                {
                    creatorAngle -= mouse.delta.ReadValue().x * 0.008f;
                }

                var wheel = mouse.scroll.ReadValue().y;

                if (Mathf.Abs(wheel) > 0.01f)
                {
                    creatorZoomTo = Mathf.Clamp01(creatorZoomTo + Mathf.Sign(wheel) * 0.2f);
                }
            }

            creatorZoom = Mathf.Lerp(creatorZoom, creatorZoomTo, 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
            var z = creatorZoom * creatorZoom * (3 - 2 * creatorZoom);
            var height = HeroView.HeightOf(look);
            var target = body.transform.position + Vector3.up * height * Mathf.Lerp(0.55f, 0.93f, z);
            var distance = Mathf.Lerp(3.1f, 0.8f, z);
            var facing = body.Facing + creatorAngle;
            var direction = new Vector3(Mathf.Sin(facing), 0, Mathf.Cos(facing));
            // Seen a little from the right of the screen's middle, leaving room for the panel.
            var aside = Vector3.Cross(Vector3.up, direction).normalized * Mathf.Lerp(0.75f, 0.18f, z);
            var camera = view.transform;
            camera.position = target + direction * distance + Vector3.up * Mathf.Lerp(0.25f, 0.02f, z) + aside;
            camera.rotation = Quaternion.LookRotation(target + aside - camera.position, Vector3.up);
        }

        bool studioLit;

        /// <summary>
        /// Making the hero: a portrait's light — the sun swung round to shine on them from before them, a little from the
        /// left and above, warm and without the houses' shadows; the moon a cool fill from the other side. Back to the
        /// sky's own the moment the making is over.
        /// </summary>
        /// <summary>The sun's shadows before the hero's making lit it its own way.</summary>
        LightShadows sunShadows = LightShadows.Soft;

        void StudioLight()
        {
            var lit = mode == Mode.Create && sky != null && sky.Sun != null && body != null;

            if (!lit)
            {
                if (studioLit && sky != null && sky.Sun != null)
                {
                    sky.Sun.shadows = sunShadows;
                }

                studioLit = false;

                return;
            }

            if (!studioLit)
            {
                sunShadows = sky.Sun.shadows;
            }

            studioLit = true;
            sky.Sun.enabled = true;
            var toCamera = view.transform.position - body.transform.position;
            toCamera.y = 0;
            toCamera = toCamera.sqrMagnitude > 0.01f ? toCamera.normalized : Vector3.forward;
            var left = Vector3.Cross(toCamera, Vector3.up);
            var key = (toCamera + left * 0.5f + Vector3.up * 0.8f).normalized;
            sky.Sun.transform.rotation = Quaternion.LookRotation(-key);
            sky.Sun.color = new Color(1f, 0.94f, 0.86f);
            sky.Sun.intensity = 1.6f;
            sky.Sun.shadows = LightShadows.None;

            if (sky.Moon != null)
            {
                sky.Moon.enabled = true;
                var fill = (toCamera - left * 0.8f + Vector3.up * 0.3f).normalized;
                sky.Moon.transform.rotation = Quaternion.LookRotation(-fill);
                sky.Moon.color = new Color(0.7f, 0.78f, 0.95f);
                sky.Moon.intensity = 0.45f;
            }
        }

        void OnDestroy()
        {
            // Leaving play mode in the editor must not leave the editor paused, or its render pipeline changed.
            Time.timeScale = 1;
            TrainingDummy.Struck -= OnStruck;
            Wounds.Landed -= OnWound;
            Wounds.Struck -= OnStrikeShown;
            TreeBranch.Creaking -= OnCreak;
            TreeBranch.Snapped -= OnSnap;
            Wolf.Killed -= OnWolfKilled;
            Wolf.Roared -= OnWolfRoared;
            GuMaster.Killed -= OnMasterKilled;
            GuMaster.Spoke -= OnMasterSpoke;
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
            if (clock == null || body == null || screenshot != null || mode == Mode.Create || Sandbox)
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
            if (redirecting)
            {
                return;
            }

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
                "R — погода · G — талант · Y — +50 камней · F3+Q — все клавиши",
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
