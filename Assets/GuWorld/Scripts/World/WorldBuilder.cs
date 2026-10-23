using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuWorld.Render;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GuWorld.World
{
    /// <summary>
    /// The builder (F9, the page Menus.BuilderPage): the world edited in the game, as a level editor does it. A free
    /// camera (right mouse to look, WASD, Space/C up and down, Shift faster, the wheel forward); the artist's models put
    /// down from the catalog (BuildCatalog), picked up, moved, turned, scaled, removed — the built village's own things
    /// too; stairs stretched from foot to top, walls, fences and paths laid along a line; the ground raised, lowered,
    /// levelled and smoothed with a brush. What is done is kept (persistentDataPath/builder: world.json, terrain.bin)
    /// and made again at every start: the things put down always stand; the built village's things are found by their
    /// name and place as their squares stream in (ChunkStreamer.SquareArrived) and moved or taken away again.
    /// </summary>
    public class WorldBuilder : MonoBehaviour
    {
        public enum Tool
        {
            Select,
            Place,
            Stretch,
            Raise,
            Lower,
            Flatten,
            Smooth,
        }

        [Serializable]
        class Placed
        {
            public string Id;
            public Vector3 At;
            public Vector3 Euler;
            public Vector3 Scale = Vector3.one;
        }

        [Serializable]
        class Changed
        {
            /// <summary>The built thing: its name and where it first stood (BuilderKey.Of).</summary>
            public string Key;
            public bool Gone;
            public Vector3 At;
            public Vector3 Euler;
            public Vector3 Scale = Vector3.one;
        }

        [Serializable]
        class Saved
        {
            public int Version = 1;
            public List<Placed> Placed = new List<Placed>();
            public List<Changed> Changed = new List<Changed>();
        }

        /// <summary>On a thing put down by the builder: what it is (its record).</summary>
        class PlacedMark : MonoBehaviour
        {
            public Placed Data;
        }

        public static WorldBuilder Current { get; private set; }
        /// <summary>The builder is open: the game leaves the camera to it.</summary>
        public static bool Active { get; private set; }

        /// <summary>The game's camera (moved by the builder while it is open).</summary>
        public ThirdPersonCamera Rig;

        public Tool CurrentTool = Tool.Select;
        public BuildCatalog.Entry Chosen;
        public float Radius = 6;
        public float Strength = 0.5f;
        public float ScaleChoice = 1;
        public bool Snap;
        /// <summary>What the page shows under the tools: what is chosen, what was done.</summary>
        public string Status = "";

        Saved saved = new Saved();
        /// <summary>The screenshot test's run: nothing is saved.</summary>
        bool demo;
        readonly Dictionary<string, Changed> changes = new Dictionary<string, Changed>();
        Transform placedRoot;
        bool loaded;
        string Folder => Path.Combine(Application.persistentDataPath, "builder");

        // The open builder's state.
        Camera eye;
        float yaw, pitch;
        GameObject ghost;
        string ghostId;
        float ghostYaw;
        float heightOffset;
        GameObject selected;
        Vector3 grabOffset;
        bool dragging;
        Vector3? stretchFrom;
        readonly List<GameObject> stretchGhosts = new List<GameObject>();
        LineRenderer ring;
        LineRenderer box;
        float flattenTo = float.NaN;
        readonly HashSet<TerrainData> stroke = new HashSet<TerrainData>();
        /// <summary>The ground's heights changed, per terrain: the square of its heightmap they lie in.</summary>
        readonly Dictionary<TerrainData, RectInt> dirty = new Dictionary<TerrainData, RectInt>();

        void Awake()
        {
            Current = this;
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
                Active = false;
            }

            ChunkStreamer.SquareArrived -= Apply;
        }

        // ── Kept and made again ─────────────────────────────────────────

        void Update()
        {
            // The world's squares and ground are there once the streamer is: the kept work is made again then.
            if (!loaded && ChunkStreamer.Current != null && BuildCatalog.Load() != null)
            {
                loaded = true;
                Load();
            }

            if (Active)
            {
                Tick();
            }
        }

        void Load()
        {
            placedRoot = new GameObject("Built by the player").transform;
            var json = Path.Combine(Folder, "world.json");
            var defaults = Path.Combine(Application.streamingAssetsPath, "builder", "world.json");

            try
            {
                var file = File.Exists(json) ? json : File.Exists(defaults) ? defaults : null;

                if (file != null)
                {
                    saved = JsonUtility.FromJson<Saved>(File.ReadAllText(file)) ?? new Saved();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Gu World builder: the saved world could not be read: {e.Message}");
                saved = new Saved();
            }

            foreach (var change in saved.Changed)
            {
                changes[change.Key] = change;
            }

            foreach (var placed in saved.Placed)
            {
                Make(placed);
            }

            LoadGround();
            ChunkStreamer.SquareArrived += Apply;

            foreach (var square in ChunkStreamer.Current.Instances)
            {
                Apply(square);
            }

            UnityEngine.Physics.SyncTransforms();
        }

        /// <summary>A square of the built world streamed in: its things moved or taken away as the builder left them.</summary>
        void Apply(GameObject square)
        {
            if (square == null || changes.Count == 0)
            {
                return;
            }

            foreach (var group in square.GetComponentsInChildren<LODGroup>(true))
            {
                var thing = group.gameObject;

                if (changes.TryGetValue(BuilderKey.Of(thing), out var change))
                {
                    Change(thing, change);
                }
            }
        }

        static void Change(GameObject thing, Changed change)
        {
            BuilderKey.Keep(thing, change.Key);

            if (change.Gone)
            {
                thing.SetActive(false);

                return;
            }

            thing.transform.SetPositionAndRotation(change.At, Quaternion.Euler(change.Euler));
            thing.transform.localScale = change.Scale;
        }

        GameObject Make(Placed placed)
        {
            var entry = BuildCatalog.Load().Find(placed.Id);

            if (entry == null || entry.Prefab == null)
            {
                return null;
            }

            var thing = Instantiate(entry.Prefab, placed.At, Quaternion.Euler(placed.Euler), placedRoot);
            thing.transform.localScale = placed.Scale;
            thing.AddComponent<PlacedMark>().Data = placed;

            return thing;
        }

        public void Save()
        {
            if (demo)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Folder);
                saved.Changed = changes.Values.ToList();
                File.WriteAllText(Path.Combine(Folder, "world.json"), JsonUtility.ToJson(saved, true));
                SaveGround();
                Status = $"Сохранено: поставлено {saved.Placed.Count}, изменено построенного {saved.Changed.Count}";
            }
            catch (Exception e)
            {
                Status = "Не удалось сохранить: " + e.Message;
            }
        }

        // ── The ground ─────────────────────────────────────────────────

        static Terrain TerrainNamed(string name) => Terrain.activeTerrains.FirstOrDefault(t => t.terrainData != null && t.terrainData.name == name);

        /// <summary>terrain.bin: for every terrain changed, the square of its heights that changed (name, x, z, width, depth, heights as 16 bits).</summary>
        void SaveGround()
        {
            using var writer = new BinaryWriter(File.Create(Path.Combine(Folder, "terrain.bin")));
            writer.Write(1);
            writer.Write(dirty.Count);

            foreach (var pair in dirty)
            {
                var r = pair.Value;
                var heights = pair.Key.GetHeights(r.x, r.y, r.width, r.height);
                writer.Write(pair.Key.name);
                writer.Write(r.x);
                writer.Write(r.y);
                writer.Write(r.width);
                writer.Write(r.height);

                for (var z = 0; z < r.height; z++)
                {
                    for (var x = 0; x < r.width; x++)
                    {
                        writer.Write((ushort)Mathf.RoundToInt(Mathf.Clamp01(heights[z, x]) * 65535));
                    }
                }
            }
        }

        void LoadGround()
        {
            var path = Path.Combine(Folder, "terrain.bin");
            var defaults = Path.Combine(Application.streamingAssetsPath, "builder", "terrain.bin");
            var file = File.Exists(path) ? path : File.Exists(defaults) ? defaults : null;

            if (file == null)
            {
                return;
            }

            try
            {
                using var reader = new BinaryReader(File.OpenRead(file));
                reader.ReadInt32();
                var count = reader.ReadInt32();

                for (var i = 0; i < count; i++)
                {
                    var name = reader.ReadString();
                    var r = new RectInt(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                    var heights = new float[r.height, r.width];

                    for (var z = 0; z < r.height; z++)
                    {
                        for (var x = 0; x < r.width; x++)
                        {
                            heights[z, x] = reader.ReadUInt16() / 65535f;
                        }
                    }

                    if (TerrainNamed(name) is Terrain terrain)
                    {
                        terrain.terrainData.SetHeights(r.x, r.y, heights);
                        Dirty(terrain.terrainData, r);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Gu World builder: the saved ground could not be read: {e.Message}");
            }
        }

        void Dirty(TerrainData data, RectInt r)
        {
            if (dirty.TryGetValue(data, out var was))
            {
                var xMin = Mathf.Min(was.xMin, r.xMin);
                var yMin = Mathf.Min(was.yMin, r.yMin);
                r = new RectInt(xMin, yMin, Mathf.Max(was.xMax, r.xMax) - xMin, Mathf.Max(was.yMax, r.yMax) - yMin);
            }

            dirty[data] = r;
        }

        /// <summary>The brush on every terrain under it (where a village's own ground lies over the mountain's, both: their edges stay together).</summary>
        void Brush(Vector3 centre, float dt)
        {
            foreach (var terrain in Terrain.activeTerrains)
            {
                var data = terrain.terrainData;
                var origin = terrain.GetPosition();
                var size = data.size;
                var res = data.heightmapResolution;
                var x0 = Mathf.Clamp(Mathf.FloorToInt((centre.x - Radius - origin.x) / size.x * (res - 1)) - 1, 0, res - 1);
                var z0 = Mathf.Clamp(Mathf.FloorToInt((centre.z - Radius - origin.z) / size.z * (res - 1)) - 1, 0, res - 1);
                var x1 = Mathf.Clamp(Mathf.CeilToInt((centre.x + Radius - origin.x) / size.x * (res - 1)) + 1, 0, res - 1);
                var z1 = Mathf.Clamp(Mathf.CeilToInt((centre.z + Radius - origin.z) / size.z * (res - 1)) + 1, 0, res - 1);

                if (x1 <= x0 || z1 <= z0)
                {
                    continue;
                }

                var w = x1 - x0 + 1;
                var h = z1 - z0 + 1;
                var heights = data.GetHeights(x0, z0, w, h);
                var before = CurrentTool == Tool.Smooth ? (float[,])heights.Clone() : null;
                var step = Strength * dt;
                var target = (flattenTo - origin.y) / size.y;

                for (var z = 0; z < h; z++)
                {
                    for (var x = 0; x < w; x++)
                    {
                        var px = origin.x + (x0 + x) * size.x / (res - 1);
                        var pz = origin.z + (z0 + z) * size.z / (res - 1);
                        var d = Mathf.Sqrt((px - centre.x) * (px - centre.x) + (pz - centre.z) * (pz - centre.z));

                        if (d > Radius)
                        {
                            continue;
                        }

                        // Full in the middle, nothing at the edge, smoothly between.
                        var f = 1 - d / Radius;
                        f = f * f * (3 - 2 * f);

                        switch (CurrentTool)
                        {
                            case Tool.Raise: heights[z, x] += step * 6 / size.y * f; break;
                            case Tool.Lower: heights[z, x] -= step * 6 / size.y * f; break;
                            case Tool.Flatten: heights[z, x] = Mathf.Lerp(heights[z, x], target, Mathf.Clamp01(step * 8 * f)); break;
                            case Tool.Smooth:
                                if (x > 0 && z > 0 && x < w - 1 && z < h - 1)
                                {
                                    var mean = (before[z - 1, x] + before[z + 1, x] + before[z, x - 1] + before[z, x + 1] + before[z, x]) / 5;
                                    heights[z, x] = Mathf.Lerp(heights[z, x], mean, Mathf.Clamp01(step * 8 * f));
                                }

                                break;
                        }

                        heights[z, x] = Mathf.Clamp01(heights[z, x]);
                    }
                }

                data.SetHeightsDelayLOD(x0, z0, heights);
                stroke.Add(data);
                Dirty(data, new RectInt(x0, z0, w, h));
            }
        }

        void EndStroke()
        {
            foreach (var data in stroke)
            {
                data.SyncHeightmap();
            }

            stroke.Clear();
            flattenTo = float.NaN;
        }

        // ── The open builder ───────────────────────────────────────────

        public bool Ready => loaded && ChunkStreamer.Current != null;

        public void Begin()
        {
            if (Active || Rig == null)
            {
                return;
            }

            Active = true;
            Rig.Following = false;
            eye = Rig.GetComponentInChildren<Camera>();

            if (eye == null)
            {
                eye = Camera.main;
            }
            var euler = Rig.transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x > 180 ? euler.x - 360 : euler.x;
            ring = Line("Brush", 49, new Color(0.95f, 0.8f, 0.4f));
            box = Line("Chosen", 16, new Color(0.55f, 0.85f, 1f));
            Status = "F9 или Esc — выйти (всё сохраняется само)";
        }

        public void End()
        {
            if (!Active)
            {
                return;
            }

            EndStroke();
            ClearGhosts();
            Select(null);

            if (ring != null) Destroy(ring.gameObject);
            if (box != null) Destroy(box.gameObject);

            Save();
            Active = false;
        }

        /// <summary>Esc: one step back — a line begun, a thing chosen, a tool taken up. False when there was nothing to undo (the page closes then).</summary>
        public bool Cancel()
        {
            if (stretchFrom != null)
            {
                stretchFrom = null;
                ClearGhosts();

                return true;
            }

            if (selected != null)
            {
                Select(null);

                return true;
            }

            if (CurrentTool != Tool.Select)
            {
                Choose(Tool.Select);

                return true;
            }

            return false;
        }

        public void Choose(Tool tool, BuildCatalog.Entry entry = null)
        {
            CurrentTool = tool;
            Chosen = entry ?? Chosen;
            ClearGhosts();
            stretchFrom = null;

            if (tool != Tool.Select)
            {
                Select(null);
            }
        }

        LineRenderer Line(string name, int points, Color colour)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.positionCount = points;
            line.widthMultiplier = 0.12f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = colour;
            line.enabled = false;

            return line;
        }

        void ClearGhosts()
        {
            if (ghost != null) Destroy(ghost);
            ghost = null;
            ghostId = null;

            foreach (var g in stretchGhosts)
            {
                if (g != null) Destroy(g);
            }

            stretchGhosts.Clear();
        }

        static GameObject Ghost(BuildCatalog.Entry entry)
        {
            var g = Instantiate(entry.Prefab);
            g.name = "Ghost";

            foreach (var c in g.GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }

            foreach (var b in g.GetComponentsInChildren<MonoBehaviour>())
            {
                b.enabled = false;
            }

            return g;
        }

        bool PointerOnPage => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        bool Pick(out RaycastHit hit)
        {
            var mouse = Mouse.current;
            hit = default;

            return mouse != null && eye != null && UnityEngine.Physics.Raycast(eye.ScreenPointToRay(mouse.position.ReadValue()), out hit, 4000, ~0, QueryTriggerInteraction.Ignore);
        }

        void Tick()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard == null || mouse == null || eye == null)
            {
                return;
            }

            var dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            var ctrl = keyboard.ctrlKey.isPressed;
            var shift = keyboard.shiftKey.isPressed;
            UnityEngine.Physics.SyncTransforms();

            // The camera: look with the right mouse, fly with the keys, the wheel forward (with Ctrl/Alt the wheel is the tools').
            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                yaw += delta.x * 0.15f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.15f, -89, 89);
            }

            var turn = Quaternion.Euler(pitch, yaw, 0);
            var move = Vector3.zero;

            if (!ctrl)
            {
                if (keyboard.wKey.isPressed) move += turn * Vector3.forward;
                if (keyboard.sKey.isPressed) move -= turn * Vector3.forward;
                if (keyboard.dKey.isPressed) move += turn * Vector3.right;
                if (keyboard.aKey.isPressed) move -= turn * Vector3.right;
                if (keyboard.spaceKey.isPressed) move += Vector3.up;
                if (keyboard.cKey.isPressed) move -= Vector3.up;
            }

            var wheel = mouse.scroll.ReadValue().y / 120f;
            var toolWheel = ctrl || keyboard.altKey.isPressed;

            if (!toolWheel && !PointerOnPage)
            {
                move += turn * Vector3.forward * wheel * 6;
            }

            var speed = shift ? 90 : 22;
            Rig.transform.SetPositionAndRotation(Rig.transform.position + move * speed * dt, turn);

            switch (CurrentTool)
            {
                case Tool.Place: PlaceTick(keyboard, mouse, wheel, toolWheel); break;
                case Tool.Stretch: StretchTick(keyboard, mouse); break;
                case Tool.Select: SelectTick(keyboard, mouse, wheel, toolWheel); break;
                default: BrushTick(mouse, dt, wheel, toolWheel); break;
            }

            if (ring != null) ring.enabled = CurrentTool >= Tool.Raise;
        }

        Vector3 Snapped(Vector3 p) => Snap ? new Vector3(Mathf.Round(p.x), p.y, Mathf.Round(p.z)) : p;

        void Turning(Keyboard keyboard, float wheel, bool toolWheel, ref float turnYaw)
        {
            var by = keyboard.shiftKey.isPressed ? 90 : 15;
            if (keyboard.rKey.wasPressedThisFrame) turnYaw += by;
            if (keyboard.tKey.wasPressedThisFrame) turnYaw -= by;
            if (keyboard.altKey.isPressed && wheel != 0) turnYaw += wheel * 5;
        }

        void Scaling(Keyboard keyboard, float wheel)
        {
            if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame) ScaleChoice = Mathf.Min(4, ScaleChoice * 1.1f);
            if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame) ScaleChoice = Mathf.Max(0.2f, ScaleChoice / 1.1f);
            if (keyboard.ctrlKey.isPressed && wheel != 0) ScaleChoice = Mathf.Clamp(ScaleChoice * Mathf.Pow(1.05f, wheel), 0.2f, 4);
        }

        void Raising(Keyboard keyboard, ref float offset)
        {
            var by = keyboard.shiftKey.isPressed ? 1f : 0.1f;
            if (keyboard.pageUpKey.wasPressedThisFrame) offset += by;
            if (keyboard.pageDownKey.wasPressedThisFrame) offset -= by;
        }

        // Putting a model down: it follows the mouse on whatever is under it; the left button puts it there.
        void PlaceTick(Keyboard keyboard, Mouse mouse, float wheel, bool toolWheel)
        {
            if (Chosen == null)
            {
                return;
            }

            if (ghost == null || ghostId != Chosen.Id)
            {
                ClearGhosts();
                ghost = Ghost(Chosen);
                ghostId = Chosen.Id;
            }

            Turning(keyboard, wheel, toolWheel, ref ghostYaw);
            Scaling(keyboard, wheel);
            Raising(keyboard, ref heightOffset);

            if (PointerOnPage || !Pick(out var hit))
            {
                ghost.SetActive(false);

                return;
            }

            ghost.SetActive(true);
            var turn = Quaternion.Euler(0, ghostYaw, 0);
            var middle = turn * new Vector3(Chosen.Middle.x, 0, Chosen.Middle.y) * ScaleChoice;
            var at = Snapped(hit.point - middle);
            at.y = hit.point.y + heightOffset;
            ghost.transform.SetPositionAndRotation(at, turn);
            ghost.transform.localScale = Vector3.one * ScaleChoice;
            Status = $"{Chosen.Label}: левая — поставить, R/T — повернуть, +/- или Ctrl+колесо — размер ({ScaleChoice:0.00}), PgUp/PgDn — выше/ниже ({heightOffset:0.0} м), Esc — отмена";

            if (mouse.leftButton.wasPressedThisFrame)
            {
                var placed = new Placed { Id = Chosen.Id, At = at, Euler = new Vector3(0, ghostYaw, 0), Scale = Vector3.one * ScaleChoice };
                saved.Placed.Add(placed);
                Make(placed);
            }
        }

        // Stretching: a stair from its foot to its top, a wall, a fence or a path laid along the line between two clicks.
        void StretchTick(Keyboard keyboard, Mouse mouse)
        {
            if (Chosen == null || !(Chosen.Stretch || Chosen.Repeat))
            {
                Status = "Протянуть можно лестницы, стену, забор, тропинку и мощение: выберите их слева";

                return;
            }

            Scaling(keyboard, mouse.scroll.ReadValue().y / 120f);

            if (PointerOnPage || !Pick(out var hit))
            {
                return;
            }

            if (stretchFrom == null)
            {
                Status = Chosen.Stretch ? $"{Chosen.Label}: щёлкните, где её низ (или верх), потом другой конец; ширина +/- ({ScaleChoice:0.00})"
                    : $"{Chosen.Label}: щёлкните начало, потом конец; ширина +/- ({ScaleChoice:0.00})";

                if (mouse.leftButton.wasPressedThisFrame)
                {
                    stretchFrom = hit.point;
                }

                return;
            }

            var pieces = Stretched(stretchFrom.Value, hit.point);
            ClearGhosts();

            foreach (var p in pieces)
            {
                var g = Ghost(Chosen);
                g.transform.SetPositionAndRotation(p.At, Quaternion.Euler(p.Euler));
                g.transform.localScale = p.Scale;
                stretchGhosts.Add(g);
            }

            Status = $"{Chosen.Label}: {pieces.Count} шт., {Vector3.Distance(stretchFrom.Value, hit.point):0.0} м — левая кнопка закончить, Esc — отмена";

            if (mouse.leftButton.wasPressedThisFrame)
            {
                foreach (var p in pieces)
                {
                    saved.Placed.Add(p);
                    Make(p);
                }

                stretchFrom = null;
                ClearGhosts();
            }
        }

        /// <summary>The pieces between two points: one stair scaled to the run and the rise, or copies along the line, each on the ground under it.</summary>
        List<Placed> Stretched(Vector3 a, Vector3 b)
        {
            var pieces = new List<Placed>();

            if (Chosen.Stretch)
            {
                var foot = a.y <= b.y ? a : b;
                var top = a.y <= b.y ? b : a;
                var flat = new Vector3(foot.x - top.x, 0, foot.z - top.z);
                var run = Mathf.Max(0.3f, flat.magnitude);
                var rise = Mathf.Max(0.2f, top.y - foot.y);
                var stairYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                var scale = new Vector3(ScaleChoice, rise / Mathf.Max(0.1f, Chosen.Height), run / Mathf.Max(0.1f, Chosen.Size.y));
                // Its foot (the front edge's middle) on the lower point: the origin back from there.
                var frontEdge = new Vector3(Chosen.Middle.x * scale.x, 0, (Chosen.Middle.y + Chosen.Size.y / 2) * scale.z);
                var at = foot - Quaternion.Euler(0, stairYaw, 0) * frontEdge;
                at.y = foot.y;
                pieces.Add(new Placed { Id = Chosen.Id, At = at, Euler = new Vector3(0, stairYaw, 0), Scale = scale });

                return pieces;
            }

            var line = new Vector3(b.x - a.x, 0, b.z - a.z);
            var length = line.magnitude;
            var piece = Mathf.Max(0.5f, Chosen.Size.x * ScaleChoice);
            var count = Mathf.Max(1, Mathf.RoundToInt(length / piece));
            var each = Mathf.Max(0.5f, length / count);
            var dir = length > 0.01f ? line / length : Vector3.right;
            // The model's own X along the line.
            var lineYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - 90;
            var turnLine = Quaternion.Euler(0, lineYaw, 0);

            for (var i = 0; i < count && i < 200; i++)
            {
                var centre = a + dir * each * (i + 0.5f);
                var ground = UnityEngine.Physics.Raycast(centre + Vector3.up * 200, Vector3.down, out var down, 400, ~0, QueryTriggerInteraction.Ignore) ? down.point.y : centre.y;
                var scale = new Vector3(each / Mathf.Max(0.1f, Chosen.Size.x), ScaleChoice, ScaleChoice);
                var at = centre - turnLine * new Vector3(Chosen.Middle.x * scale.x, 0, Chosen.Middle.y * scale.z);
                at.y = ground;
                pieces.Add(new Placed { Id = Chosen.Id, At = at, Euler = new Vector3(0, lineYaw, 0), Scale = scale });
            }

            return pieces;
        }

        // Choosing what stands: a click picks it (the left button held drags it over the ground), keys change it.
        void SelectTick(Keyboard keyboard, Mouse mouse, float wheel, bool toolWheel)
        {
            if (mouse.leftButton.wasPressedThisFrame && !PointerOnPage)
            {
                var picked = Pick(out var hit) ? Thing(hit.collider) : null;

                if (picked != selected)
                {
                    Select(picked);
                }

                dragging = picked != null;
                grabOffset = picked != null ? picked.transform.position - hit.point : Vector3.zero;
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                dragging = false;
            }

            if (selected == null)
            {
                Status = "Выбор: щёлкните дом или вещь. Слева — поставить новое; вверху — протянуть и земля";
                box.enabled = false;

                return;
            }

            var t = selected.transform;
            var changed = false;

            if (dragging && mouse.leftButton.isPressed && PickPast(selected, out var under))
            {
                var to = Snapped(under.point + new Vector3(grabOffset.x, 0, grabOffset.z));
                to.y = under.point.y;

                if ((to - t.position).sqrMagnitude > 0.0001f)
                {
                    t.position = to;
                    changed = true;
                }
            }

            var y = t.eulerAngles.y;
            var before = y;
            Turning(keyboard, wheel, toolWheel, ref y);

            if (!Mathf.Approximately(y, before))
            {
                t.rotation = Quaternion.Euler(0, y, 0);
                changed = true;
            }

            var by = keyboard.shiftKey.isPressed ? 1f : 0.1f;
            if (keyboard.pageUpKey.wasPressedThisFrame) { t.position += Vector3.up * by; changed = true; }
            if (keyboard.pageDownKey.wasPressedThisFrame) { t.position -= Vector3.up * by; changed = true; }

            var grow = keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame ? 1.1f
                : keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame ? 1 / 1.1f
                : keyboard.ctrlKey.isPressed && wheel != 0 ? Mathf.Pow(1.05f, wheel) : 1;

            if (!Mathf.Approximately(grow, 1))
            {
                t.localScale *= grow;
                changed = true;
            }

            if (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
            {
                Remove(selected);

                return;
            }

            if (keyboard.ctrlKey.isPressed && keyboard.dKey.wasPressedThisFrame && selected.GetComponent<PlacedMark>() is PlacedMark mark)
            {
                var copy = new Placed { Id = mark.Data.Id, At = t.position + t.right * 2, Euler = t.eulerAngles, Scale = t.localScale };
                saved.Placed.Add(copy);
                Select(Make(copy));

                return;
            }

            if (changed)
            {
                Record(selected);
            }

            Outline(selected);
            Status = $"{Describe(selected)}: перетащить — левой, R/T — повернуть, +/- — размер, PgUp/PgDn — выше/ниже, Delete — убрать, Ctrl+D — копия";
        }

        /// <summary>What is under the mouse past a thing (dragging it: the ground under it, not itself).</summary>
        bool PickPast(GameObject thing, out RaycastHit hit)
        {
            hit = default;
            var mouse = Mouse.current;

            if (mouse == null || eye == null)
            {
                return false;
            }

            var hits = UnityEngine.Physics.RaycastAll(eye.ScreenPointToRay(mouse.position.ReadValue()), 4000, ~0, QueryTriggerInteraction.Ignore);
            var best = float.PositiveInfinity;
            var found = false;

            foreach (var h in hits)
            {
                if (h.distance < best && !h.collider.transform.IsChildOf(thing.transform))
                {
                    best = h.distance;
                    hit = h;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>What a collider belongs to that the builder can take: a thing put down, else the nearest built thing (a house, a wall, a prop: what has its level of detail).</summary>
        static GameObject Thing(Collider collider)
        {
            if (collider == null || collider is TerrainCollider)
            {
                return null;
            }

            var mark = collider.GetComponentInParent<PlacedMark>();

            if (mark != null)
            {
                return mark.gameObject;
            }

            var group = collider.GetComponentInParent<LODGroup>();

            return group != null ? group.gameObject : null;
        }

        static string Describe(GameObject thing) => thing.GetComponent<PlacedMark>() is PlacedMark mark ? (BuildCatalog.Load().Find(mark.Data.Id)?.Label ?? mark.Data.Id) : thing.name;

        void Select(GameObject thing)
        {
            selected = thing;
            heightOffset = 0;

            if (box != null)
            {
                box.enabled = thing != null;
            }
        }

        /// <summary>A change kept: a thing put down keeps its record; a built thing gets one by its first name and place.</summary>
        void Record(GameObject thing)
        {
            var t = thing.transform;

            if (thing.GetComponent<PlacedMark>() is PlacedMark mark)
            {
                mark.Data.At = t.position;
                mark.Data.Euler = t.eulerAngles;
                mark.Data.Scale = t.localScale;

                return;
            }

            var key = BuilderKey.Of(thing);
            BuilderKey.Keep(thing, key);

            if (!changes.TryGetValue(key, out var change))
            {
                changes[key] = change = new Changed { Key = key };
            }

            change.Gone = false;
            change.At = t.position;
            change.Euler = t.eulerAngles;
            change.Scale = t.localScale;
        }

        void Remove(GameObject thing)
        {
            if (thing.GetComponent<PlacedMark>() is PlacedMark mark)
            {
                saved.Placed.Remove(mark.Data);
                Destroy(thing);
            }
            else
            {
                var key = BuilderKey.Of(thing);
                BuilderKey.Keep(thing, key);
                changes[key] = new Changed { Key = key, Gone = true };
                thing.SetActive(false);
            }

            Select(null);
            Status = "Убрано";
        }

        /// <summary>The chosen thing's box drawn round it.</summary>
        void Outline(GameObject thing)
        {
            var renderers = thing.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is LineRenderer)).ToArray();

            if (renderers.Length == 0)
            {
                box.enabled = false;

                return;
            }

            var b = renderers[0].bounds;

            foreach (var r in renderers)
            {
                b.Encapsulate(r.bounds);
            }

            var lo = b.min;
            var hi = b.max;
            var c = new[]
            {
                new Vector3(lo.x, lo.y, lo.z), new Vector3(hi.x, lo.y, lo.z), new Vector3(hi.x, lo.y, hi.z), new Vector3(lo.x, lo.y, hi.z),
                new Vector3(lo.x, hi.y, lo.z), new Vector3(hi.x, hi.y, lo.z), new Vector3(hi.x, hi.y, hi.z), new Vector3(lo.x, hi.y, hi.z),
            };
            var order = new[] { 0, 1, 2, 3, 0, 4, 5, 1, 5, 6, 2, 6, 7, 3, 7, 4 };
            box.enabled = true;
            box.positionCount = order.Length;

            for (var i = 0; i < order.Length; i++)
            {
                box.SetPosition(i, c[order[i]]);
            }
        }

        // The ground's brush: held down, it works where the mouse is; the wheel with Ctrl is its size, with Alt its strength.
        void BrushTick(Mouse mouse, float dt, float wheel, bool toolWheel)
        {
            if (Keyboard.current.ctrlKey.isPressed && wheel != 0) Radius = Mathf.Clamp(Radius * Mathf.Pow(1.08f, wheel), 1, 60);
            if (Keyboard.current.altKey.isPressed && wheel != 0) Strength = Mathf.Clamp(Strength * Mathf.Pow(1.1f, wheel), 0.05f, 3);

            var names = new Dictionary<Tool, string> { [Tool.Raise] = "Поднять", [Tool.Lower] = "Опустить", [Tool.Flatten] = "Выровнять", [Tool.Smooth] = "Сгладить" };
            Status = $"Земля · {names[CurrentTool]}: держите левую кнопку. Ctrl+колесо — размер ({Radius:0} м), Alt+колесо — сила ({Strength:0.00})";

            if (PointerOnPage || !Pick(out var hit))
            {
                ring.enabled = false;

                return;
            }

            // The ring on the ground.
            ring.enabled = true;

            for (var i = 0; i < ring.positionCount; i++)
            {
                var a = i * Mathf.PI * 2 / (ring.positionCount - 1);
                var p = hit.point + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Radius;
                p.y = UnityEngine.Physics.Raycast(p + Vector3.up * 100, Vector3.down, out var down, 300, ~0, QueryTriggerInteraction.Ignore) ? down.point.y + 0.15f : hit.point.y;
                ring.SetPosition(i, p);
            }

            if (mouse.leftButton.wasPressedThisFrame && !(hit.collider is TerrainCollider))
            {
                Status = "Кисть работает по земле: наведите на землю, не на дом";
            }

            if (mouse.leftButton.isPressed && hit.collider is TerrainCollider)
            {
                if (float.IsNaN(flattenTo))
                {
                    flattenTo = hit.point.y;
                }

                Brush(hit.point, dt);
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                EndStroke();
            }
        }

        /// <summary>Puts the hero on the ground under the middle of the view (and closes nothing: the page does).</summary>
        public Vector3 GroundAhead()
        {
            var ray = eye != null ? eye.ViewportPointToRay(new Vector3(0.5f, 0.5f)) : new Ray(Rig.transform.position, Rig.transform.forward);

            return UnityEngine.Physics.Raycast(ray, out var hit, 4000, ~0, QueryTriggerInteraction.Ignore) ? hit.point : Rig.transform.position;
        }

        /// <summary>
        /// For the screenshot test (`-page builder -builddemo`): a house, a stair stretched up a slope, a wall laid along
        /// a line and a mound raised near a point — what the tools do, done once, never saved.
        /// </summary>
        public void Demo(Vector3 near)
        {
            demo = true;
            var catalog = BuildCatalog.Load();
            var ground = UnityEngine.Physics.Raycast(near + Vector3.up * 300, Vector3.down, out var hit, 600, ~0, QueryTriggerInteraction.Ignore) ? hit.point : near;

            void Put(string id, Vector3 at, float turn, float scale = 1)
            {
                if (catalog.Find(id) != null)
                {
                    Make(new Placed { Id = id, At = at, Euler = new Vector3(0, turn, 0), Scale = Vector3.one * scale });
                }
            }

            Debug.Log($"Gu World builder demo at ({ground.x:0.0}, {ground.y:0.0}, {ground.z:0.0})");
            Put("guyue_bamboo_house", ground + new Vector3(-12, 0, 6), 180, 0.8f);
            Put("prop_village_well", ground + new Vector3(4, 0, 2), 0);
            Put("tree_plum_8m", ground + new Vector3(14, 0, 8), 30);
            Chosen = catalog.Find("prop_stairs_medium");

            if (Chosen != null)
            {
                foreach (var p in Stretched(ground + new Vector3(0, 0, -6), ground + new Vector3(0, 3, -16)))
                {
                    Make(p);
                }
            }

            Chosen = catalog.Find("fort_wall_segment");

            if (Chosen != null)
            {
                foreach (var p in Stretched(ground + new Vector3(-20, 0, -24), ground + new Vector3(20, 0, -24)))
                {
                    Make(p);
                }
            }

            CurrentTool = Tool.Raise;
            Radius = 8;
            Strength = 3;

            for (var i = 0; i < 30; i++)
            {
                Brush(ground + new Vector3(24, 0, -6), 0.1f);
            }

            EndStroke();
            CurrentTool = Tool.Select;
            Chosen = null;
        }

        /// <summary>Takes back everything done to the built village (its things stand where they were built) — the next start shows it.</summary>
        public void ForgetChanges()
        {
            changes.Clear();
            Save();
            Status = "Изменения построенного забыты — они вернутся на места после перезапуска игры";
        }
    }

    /// <summary>A built thing's key for the builder: its name and where it was built (kept once it is moved).</summary>
    public class BuilderKey : MonoBehaviour
    {
        public string Key;

        public static string Of(GameObject thing)
        {
            if (thing.GetComponent<BuilderKey>() is BuilderKey kept)
            {
                return kept.Key;
            }

            var p = thing.transform.position;

            return $"{thing.name}@{Mathf.Round(p.x * 2) / 2:0.0},{Mathf.Round(p.y * 2) / 2:0.0},{Mathf.Round(p.z * 2) / 2:0.0}";
        }

        public static void Keep(GameObject thing, string key)
        {
            if (thing.GetComponent<BuilderKey>() == null)
            {
                thing.AddComponent<BuilderKey>().Key = key;
            }
        }
    }
}
