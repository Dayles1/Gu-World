using System;
using System.Collections.Generic;
using System.Linq;
using GuWorld.Render;
using GuWorld.Village;
using GuWorld.World;
using UnityEditor;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// A village built from its plan (VillagePlan): its houses (each the design
    /// nearest its plot, near and far shapes, solid), their footings and stilts down
    /// to the ground as it falls, the terraces' retaining walls and the stairs' steps,
    /// the wall round it with its towers, gates and ladders, the things lying about
    /// (gathered into one mesh a block, with boxes for the solid ones), the gardens,
    /// the lamps lit at night, and the walkways its people go about on (VillageLayout).
    /// The hamlets at the foot are built here too, simpler.
    /// </summary>
    public static partial class QingMaoBuilder
    {
        const float Block = 48;

        class Blocks
        {
            readonly Dictionary<Vector2Int, MeshKit> kits = new Dictionary<Vector2Int, MeshKit>();
            readonly Dictionary<Vector2Int, Transform> holders = new Dictionary<Vector2Int, Transform>();
            readonly Transform root;
            readonly string name;

            public Blocks(Transform root, string name)
            {
                this.root = root;
                this.name = name;
            }

            static Vector2Int Key(Vector3 at) => new Vector2Int(Mathf.FloorToInt(at.x / Block), Mathf.FloorToInt(at.z / Block));

            /// <summary>The kit for the block a point lies in, its Transform set to put things there.</summary>
            public MeshKit At(Vector3 at, float yaw, float scale = 1)
            {
                var key = Key(at);

                if (!kits.TryGetValue(key, out var kit))
                {
                    kits[key] = kit = new MeshKit();
                }

                kit.Transform = Matrix4x4.TRS(at, Quaternion.Euler(0, yaw, 0), Vector3.one * scale);

                return kit;
            }

            public MeshKit Raw(Vector3 at)
            {
                var kit = At(at, 0);
                kit.Transform = Matrix4x4.identity;

                return kit;
            }

            /// <summary>The holder object of a block (for the solid boxes there).</summary>
            public Transform Holder(Vector3 at)
            {
                var key = Key(at);

                if (!holders.TryGetValue(key, out var holder))
                {
                    holder = new GameObject($"{name} {key.x} {key.y}").transform;
                    holder.SetParent(root, false);
                    holders[key] = holder;
                }

                return holder;
            }

            public void Box(Vector3 at, float yaw, Vector3 centre, Vector3 size, HitboxKind kind)
            {
                var holder = Holder(at);
                var go = new GameObject("Solid");
                go.transform.SetParent(holder, false);
                go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
                var box = go.AddComponent<BoxCollider>();
                box.center = centre;
                box.size = size;
                go.AddComponent<HitboxTag>().Kind = kind;
            }

            /// <summary>
            /// Builds every block's mesh; `solid` makes it its own collider (steps, walls). `cull`: below this share of the
            /// screen's height (the block is 48 m) it is not drawn — the small things lying about vanish a couple of hundred
            /// metres off, the walls only far away.
            /// </summary>
            public void Finish(bool solid, HitboxKind kind, bool shadows = true, float cull = 0)
            {
                foreach (var pair in kits)
                {
                    if (pair.Value.IsEmpty)
                    {
                        continue;
                    }

                    var holder = Holder(new Vector3(pair.Key.x * Block + 1, 0, pair.Key.y * Block + 1));
                    var meshName = $"{name} {pair.Key.x} {pair.Key.y}";
                    var (mesh, used) = Save(pair.Value, meshName);
                    var go = new GameObject("Mesh");
                    go.transform.SetParent(holder, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = used;
                    renderer.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

                    if (cull > 0)
                    {
                        go.AddComponent<LODGroup>().SetLODs(new[] { new LOD(cull, new Renderer[] { renderer }) });
                    }

                    if (solid)
                    {
                        go.AddComponent<MeshCollider>().sharedMesh = mesh;
                        go.AddComponent<HitboxTag>().Kind = kind;
                    }
                }
            }
        }

        static readonly List<Light> nightLamps = new List<Light>();
        static int openLogged;

        static void BuildVillage(VillagePlan plan, List<WorldPlan.Footprint> footprints, List<WorldPlan.Line> streets, List<WorldPlan.Mark> marks)
        {
            var root = new GameObject(plan.Place.Name).transform;
            var clan = plan.Place.Clan;
            float Ground(Vector2 p) => plan.GroundAt(p);

            var houses = new GameObject("Houses").transform;
            houses.SetParent(root, false);
            var under = new Blocks(root, $"{plan.Place.Id} footings");
            var stones = new Blocks(root, $"{plan.Place.Id} walls");
            var things = new Blocks(root, $"{plan.Place.Id} things");
            var steps = new Blocks(root, $"{plan.Place.Id} steps");
            var count = 0;
            var lamps = 0;

            foreach (var b in plan.Buildings)
            {
                EditorUtility.DisplayProgressBar("Qing Mao", $"{plan.Place.Name}: houses…", (float)count / plan.Buildings.Count);
                PlaceBuilding(houses, b, clan, Ground, under, steps, count++);
                footprints.Add(new WorldPlan.Footprint { Centre = b.Centre, Half = b.Size / 2, Yaw = b.Yaw, Tint = RoofTint(clan, b.Kind) });

                if (!string.IsNullOrEmpty(b.Name) && b.Kind != BuildingKind.Family && b.Kind != BuildingKind.Poor)
                {
                    marks.Add(new WorldPlan.Mark { Label = b.Name, At = new Vector3(b.Centre.x, b.Floor, b.Centre.y), Major = b.Kind == BuildingKind.Pavilion });
                }
            }

            // The terraces' retaining walls: dry stone, a coping of flat stones on top.
            foreach (var wall in plan.Walls)
            {
                for (var i = 0; i + 1 < wall.Points.Count; i++)
                {
                    var a = wall.Points[i];
                    var c = wall.Points[i + 1];
                    var mid = (a + c) / 2;
                    var along = c - a;
                    var length = along.magnitude;

                    if (length < 0.05f)
                    {
                        continue;
                    }

                    var low = Mathf.Min(wall.Low[i], wall.Low[i + 1]) - 0.45f;
                    var high = Mathf.Max(wall.High[i], wall.High[i + 1]) + 0.22f;
                    // Its face towards the lower side.
                    var across = new Vector2(-along.y, along.x) / length;
                    var downhill = plan.Warp(mid + across * 1.5f) < plan.Warp(mid - across * 1.5f) ? across : -across;
                    var face = mid + downhill * 0.22f;
                    var yaw = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
                    var kit = stones.At(new Vector3(face.x, 0, face.y), yaw);
                    kit.Box(Rubble, new Vector3(0, (low + high) / 2, 0), new Vector3(0.75f, high - low, length + 0.06f));
                    kit.Box(Stone, new Vector3(0, high + 0.06f, 0), new Vector3(0.85f, 0.12f, length + 0.08f));
                }
            }

            // The stairs: a stone step wherever the ground rises a step's height.
            foreach (var street in plan.Streets.Where(s => s.Stair))
            {
                var points = street.Points;
                var lastTop = Ground(points[0]);
                var lastAt = points[0];

                for (var i = 0; i + 1 < points.Count; i++)
                {
                    var a = points[i];
                    var c = points[i + 1];
                    var length = Vector2.Distance(a, c);

                    for (var s = 0f; s < length; s += 0.2f)
                    {
                        var p = Vector2.Lerp(a, c, s / length);
                        var g = Ground(p);

                        if (g - lastTop < 0.16f && lastTop - g < 0.16f)
                        {
                            continue;
                        }

                        var top = Mathf.Max(g, lastTop);
                        var mid = (p + lastAt) / 2;
                        var depth = Mathf.Max(0.25f, Vector2.Distance(p, lastAt));
                        var dir = (p - lastAt).normalized;
                        var yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                        steps.At(new Vector3(mid.x, top, mid.y), yaw).Box(Stone, new Vector3(0, -0.2f, 0), new Vector3(street.Half * 2 + 0.2f, 0.4f, depth + 0.05f));
                        lastTop = g;
                        lastAt = p;
                    }
                }
            }

            // The wall round the village, its towers and gates, ladders up it inside.
            if (plan.Place.Walled)
            {
                BuildVillageWall(root, plan, clan, Ground, stones);
            }

            // The things lying about.
            foreach (var prop in plan.Props)
            {
                var at = new Vector3(prop.At.x, Ground(new Vector2(prop.At.x, prop.At.z)), prop.At.z);
                DrawProp(things.At(at, prop.Yaw, prop.Scale), prop.Kind, prop.Seed);

                if (PropSolid(prop.Kind) is (Vector3 centre, Vector3 size) solid)
                {
                    things.Box(at, prop.Yaw, centre * prop.Scale, size * prop.Scale, prop.Kind.Contains("tree") ? HitboxKind.Tree : HitboxKind.Prop);
                }

                // The lamps on the main ways and round the square light the lanes at night (a few: lights are dear).
                if ((prop.Kind == "lamp" || prop.Kind == "brazier" || prop.Kind == "stone lantern") && lamps < (clan == Clan.GuYue ? 40 : 12) && Vector2.Distance(new Vector2(at.x, at.z), plan.Square.Centre) < (clan == Clan.GuYue ? 120 : 70))
                {
                    var lamp = new GameObject("Lamp").AddComponent<Light>();
                    lamp.transform.SetParent(root, false);
                    lamp.transform.position = at + Vector3.up * 2.6f;
                    lamp.type = LightType.Point;
                    lamp.range = 13;
                    lamp.color = prop.Kind == "brazier" ? new Color(1, 0.62f, 0.32f) : new Color(0.72f, 0.82f, 1);
                    lamp.intensity = 0;
                    lamp.shadows = LightShadows.None;
                    lamp.gameObject.AddComponent<NightLamp>();
                    nightLamps.Add(lamp);
                    lamps++;
                }
            }

            // Gardens: beds of dark soil in rows of greens, a low wattle fence round them.
            foreach (var (centre, half, yaw) in plan.Gardens)
            {
                var at = new Vector3(centre.x, Ground(centre), centre.y);
                var kit = things.At(at, yaw);
                var rows = Mathf.Max(2, Mathf.RoundToInt(half.y * 2 / 0.7f));

                for (var r = 0; r < rows; r++)
                {
                    var z = -half.y + 0.35f + r * (half.y * 2 - 0.7f) / Mathf.Max(1, rows - 1);
                    kit.Box(Soil, new Vector3(0, 0.06f, z), new Vector3(half.x * 2 - 0.4f, 0.14f, 0.45f));

                    for (var x = -half.x + 0.5f; x < half.x - 0.4f; x += 0.6f)
                    {
                        kit.Lump(Greens, new Vector3(x, 0.16f, z), new Vector3(0.2f, 0.16f, 0.2f), (int)(x * 10 + z * 7));
                    }
                }

                foreach (var (x, z, w, d) in new[] { (0f, -half.y, half.x * 2, 0.06f), (0f, half.y, half.x * 2, 0.06f), (-half.x, 0f, 0.06f, half.y * 2), (half.x, 0f, 0.06f, half.y * 2) })
                {
                    kit.Box(Wood, new Vector3(x, 0.35f, z), new Vector3(w, 0.5f, d));
                }
            }

            under.Finish(false, HitboxKind.Building, true, 0.035f);
            stones.Finish(false, HitboxKind.Building, true, 0.012f);
            steps.Finish(true, HitboxKind.Prop, false, 0.05f);
            things.Finish(false, HitboxKind.Prop, false, 0.22f);

            // The streets for the map, and the walkways for the people.
            foreach (var street in plan.Streets)
            {
                streets.Add(new WorldPlan.Line { Name = street.Name, Points = street.Points.Select(p => new Vector3(p.x, 0, p.y)).ToArray(), Half = street.Half });
            }

            foreach (var (name, at, yaw) in plan.Places)
            {
                if (name.StartsWith("gate") || name == "square" || name == "pavilion" || name == "academy" || name == "shrine" || name.StartsWith("stall"))
                {
                    marks.Add(new WorldPlan.Mark { Label = MarkName(name, plan.Place.Clan), At = new Vector3(at.x, Ground(at), at.y) });
                }
            }

            BuildVillageLayout(root, plan, Ground);
        }

        static string MarkName(string place, Clan clan) => place switch
        {
            "square" => "Площадь",
            "pavilion" => clan == Clan.GuYue ? "Павильон клана (под ним — духовный источник)" : "Зал клана",
            "academy" => "Академия",
            "shrine" => "Зал предков",
            "gate north" => "Северные ворота",
            "gate south" => "Южные ворота",
            "gate east" => "Восточные ворота",
            "gate west" => "Западные ворота",
            "stall 0" => "Лапшичная",
            "stall 1" => "Рисовая лавка",
            "stall 2" => "Лавка Баошаня",
            _ => place,
        };

        static Color RoofTint(Clan clan, BuildingKind kind) => clan switch
        {
            Clan.Bai => new Color(0.32f, 0.35f, 0.4f),
            Clan.Xiong => new Color(0.42f, 0.36f, 0.24f),
            Clan.Mortal => new Color(0.4f, 0.35f, 0.25f),
            _ => kind == BuildingKind.Poor || kind == BuildingKind.Shed ? new Color(0.4f, 0.36f, 0.26f) : new Color(0.26f, 0.3f, 0.36f),
        };

        /// <summary>A building on its plot: its shape (a great one's own, or the nearest design), its footing or stilts, steps up to its door.</summary>
        static void PlaceBuilding(Transform parent, VillagePlan.Building b, Clan clan, Func<Vector2, float> ground, Blocks under, Blocks steps, int index)
        {
            (Mesh, Material[]) near;
            (Mesh, Material[])? far = null;
            var size = b.Size;
            var height = 6f;
            var scale = Vector3.one;
            Design openDesign = null;

            switch (b.Kind)
            {
                case BuildingKind.Pavilion:
                    near = Make("Clan Pavilion", ClanPavilion);
                    height = 28;
                    break;
                case BuildingKind.Ancestral:
                    near = Make("Ancestral Hall", AncestralHall);
                    height = 10;
                    break;
                case BuildingKind.Academy:
                    near = Make("Academy", AcademyHall);
                    height = 9;
                    break;
                case BuildingKind.Hostel:
                    near = Make("Hostel", HostelHouse);
                    height = 9;
                    break;
                case BuildingKind.Hall:
                    near = Make($"{clan} Hall", kit => VillageHall(kit, clan));
                    height = 8;
                    break;
                default:
                    var design = DesignFor(clan, b.Kind, b.Size, b.Seed);
                    openDesign = design.Open ? design : null;
                    near = design.Near;
                    far = design.Far;
                    height = design.Height;
                    scale = new Vector3(Mathf.Clamp(b.Size.x / design.Size.x, 0.8f, 1.25f), 1, Mathf.Clamp(b.Size.y / design.Size.y, 0.8f, 1.25f));
                    size = new Vector2(design.Size.x * scale.x, design.Size.y * scale.z);
                    break;
            }

            var go = new GameObject(string.IsNullOrEmpty(b.Name) ? $"{b.Kind} {index}" : b.Name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(b.Centre.x, b.Floor, b.Centre.y), Quaternion.Euler(0, b.Yaw, 0));
            go.transform.localScale = scale;
            go.AddComponent<HitboxTag>().Kind = HitboxKind.Building;

            var nearGo = new GameObject("Near");
            nearGo.transform.SetParent(go.transform, false);
            nearGo.AddComponent<MeshFilter>().sharedMesh = near.Item1;
            var nearRenderer = nearGo.AddComponent<MeshRenderer>();
            nearRenderer.sharedMaterials = near.Item2;

            var lods = new List<LOD> { new LOD(far.HasValue ? 0.1f : 0.004f, new Renderer[] { nearRenderer }) };

            if (far.HasValue)
            {
                var farGo = new GameObject("Far");
                farGo.transform.SetParent(go.transform, false);
                farGo.AddComponent<MeshFilter>().sharedMesh = far.Value.Item1;
                var farRenderer = farGo.AddComponent<MeshRenderer>();
                farRenderer.sharedMaterials = far.Value.Item2;
                farRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lods.Add(new LOD(0.0018f, new Renderer[] { farRenderer }));
            }

            var group = go.AddComponent<LODGroup>();
            group.SetLODs(lods.ToArray());
            group.RecalculateBounds();

            // Solid: the body (the great buildings' own shapes are their colliders); one that is gone into, its walls.
            if (far.HasValue && openDesign != null)
            {
                var w = size.x / scale.x;
                var d = size.y / scale.z;
                var storey = openDesign.Storey;
                var doorAt = openDesign.DoorAt;
                const float t = 0.2f;
                const float gap = 1.4f;

                void Wall(Vector3 centre, Vector3 extent)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.center = centre;
                    box.size = extent;
                }

                Wall(new Vector3(0, storey / 2, -d / 2 + t / 2), new Vector3(w, storey, t));
                Wall(new Vector3(-w / 2 + t / 2, storey / 2, 0), new Vector3(t, storey, d));
                Wall(new Vector3(w / 2 - t / 2, storey / 2, 0), new Vector3(t, storey, d));
                var left = doorAt - gap / 2;
                var right = doorAt + gap / 2;
                Wall(new Vector3((-w / 2 + left) / 2, storey / 2, d / 2 - t / 2), new Vector3(left + w / 2, storey, t));
                Wall(new Vector3((right + w / 2) / 2, storey / 2, d / 2 - t / 2), new Vector3(w / 2 - right, storey, t));
                Wall(new Vector3(0, storey + 0.1f, 0), new Vector3(w, 0.2f, d));

                if (openLogged++ < 6)
                {
                    var doorWorld = go.transform.TransformPoint(new Vector3(doorAt, 0, d / 2 + 1.2f));
                    BuildLog.Write($"open {b.Kind} at ({b.Centre.x:0.0}, {b.Floor:0.0}, {b.Centre.y:0.0}) yaw {b.Yaw:0} door outside ({doorWorld.x:0.0}, {doorWorld.z:0.0})");
                }

                // The room lit by its lanterns (the day's light does not get in): a warm light, no shadows.
                var lightGo = new GameObject("Room Light");
                lightGo.transform.SetParent(go.transform, false);
                lightGo.transform.localPosition = new Vector3(0, storey - 0.9f, 0);
                var roomLight = lightGo.AddComponent<Light>();
                roomLight.type = LightType.Point;
                roomLight.color = new Color(1f, 0.74f, 0.46f);
                roomLight.range = Mathf.Max(w, d) * 1.3f + 1;
                roomLight.intensity = 5.5f;
                roomLight.shadows = LightShadows.None;
                lightGo.AddComponent<RoomLight>();

                if (height > storey + 0.5f)
                {
                    Wall(new Vector3(0, storey + (height - storey) / 2, 0), new Vector3(w * 0.85f, height - storey, d * 0.85f));
                }
            }
            else if (far.HasValue)
            {
                var body = go.AddComponent<BoxCollider>();
                body.center = new Vector3(0, Mathf.Min(height, 3.2f) / 2 + 0.05f, 0);
                body.size = new Vector3(size.x / scale.x, Mathf.Min(height, 3.2f), size.y / scale.z);

                if (height > 4)
                {
                    var upper = go.AddComponent<BoxCollider>();
                    upper.center = new Vector3(0, 3.2f + (height - 3.2f) / 2, 0);
                    upper.size = new Vector3(size.x / scale.x * 0.85f, height - 3.2f, size.y / scale.z * 0.85f);
                }
            }
            else
            {
                nearGo.AddComponent<MeshCollider>().sharedMesh = near.Item1;
            }

            if (b.Kind == BuildingKind.Pavilion || b.Kind == BuildingKind.Academy || b.Kind == BuildingKind.Ancestral || b.Kind == BuildingKind.Hostel || b.Kind == BuildingKind.Hall)
            {
                return;
            }

            // Under it: a stone footing where the ground is close, stilts where it falls away.
            var turn = Quaternion.Euler(0, b.Yaw, 0);
            var half = size / 2;
            var cols = Mathf.Max(2, Mathf.CeilToInt(size.x / 2.2f));
            var rows = Mathf.Max(2, Mathf.CeilToInt(size.y / 2.2f));
            var lowest = float.PositiveInfinity;

            for (var i = 0; i <= cols; i++)
            {
                for (var k = 0; k <= rows; k++)
                {
                    var local = new Vector3(-half.x + 0.2f + i * (size.x - 0.4f) / cols, 0, -half.y + 0.2f + k * (size.y - 0.4f) / rows);
                    var v = turn * local;
                    var p = b.Centre + new Vector2(v.x, v.z);
                    var g = ground(p);
                    lowest = Mathf.Min(lowest, g);
                    var drop = b.Floor - g;

                    if (drop > 0.75f && (i == 0 || i == cols || k == 0 || k == rows || (i + k) % 2 == 0))
                    {
                        var foot = new Vector3(p.x, g - 0.3f, p.y);
                        var kit = under.Raw(foot);
                        kit.Cylinder(DarkWood, foot, new Vector3(p.x, b.Floor - 0.1f, p.y), 0.12f, 0.11f, 6);
                        kit.Cylinder(Stone, foot, foot + Vector3.up * 0.45f, 0.24f, 0.2f, 6);
                    }
                }
            }

            var plinth = b.Floor - lowest;

            if (plinth <= 0.9f)
            {
                var kit = under.At(new Vector3(b.Centre.x, b.Floor, b.Centre.y), b.Yaw);
                kit.Box(clan == Clan.Xiong ? Rubble : Stone, new Vector3(0, -(plinth + 0.5f) / 2 - 0.05f, 0), new Vector3(size.x + 0.3f, plinth + 0.5f, size.y + 0.3f));
            }
            else
            {
                // Bracing between the stilts, and a skirt of planks along the front.
                var kit = under.At(new Vector3(b.Centre.x, b.Floor, b.Centre.y), b.Yaw);
                kit.Box(DarkWood, new Vector3(0, -0.25f, half.y - 0.2f), new Vector3(size.x, 0.18f, 0.16f));
                kit.Box(DarkWood, new Vector3(0, -0.25f, -half.y + 0.2f), new Vector3(size.x, 0.18f, 0.16f));
            }

            // Steps up to the door from the ground before it.
            var doorStep = turn * new Vector3(0, 0, half.y + 0.6f);
            var doorGround = ground(b.Centre + new Vector2(doorStep.x, doorStep.z));
            var rise = b.Floor - doorGround;

            if (rise > 0.3f && rise < 3)
            {
                var count = Mathf.CeilToInt(rise / 0.22f);

                for (var s = 0; s < count; s++)
                {
                    var top = b.Floor - (s + 1) * rise / count + rise / count;
                    var z = half.y + 0.2f + s * 0.3f;
                    var v = turn * new Vector3(0, 0, z);
                    steps.At(new Vector3(b.Centre.x + v.x, top, b.Centre.y + v.z), b.Yaw).Box(Wood, new Vector3(0, -0.11f, 0), new Vector3(1.4f, 0.22f, 0.32f));
                }
            }
        }

        static void BuildVillageWall(Transform root, VillagePlan plan, Clan clan, Func<Vector2, float> ground, Blocks stones)
        {
            var walls = new GameObject("Wall").transform;
            walls.SetParent(root, false);
            var points = plan.Boundary;
            var perimeter = new List<(Vector2 at, float distance)>();
            var total = 0f;

            for (var i = 0; i < points.Count; i++)
            {
                perimeter.Add((points[i], total));
                total += Vector2.Distance(points[i], points[(i + 1) % points.Count]);
            }

            const float segment = 3.6f;
            var tower = clan == Clan.GuYue ? Make("Tower", Watchtower) : Make($"{clan} Tower", kit => SmallTower(kit, clan));
            var gate = clan == Clan.GuYue ? Make("Gate", ClanGate) : Make($"{clan} Gate", kit => SmallGate(kit, clan));
            var ladder = Make("Ladder", kit =>
            {
                kit.Box(Wood, new Vector3(-0.3f, 2.6f, 0), new Vector3(0.08f, 5.6f, 0.08f));
                kit.Box(Wood, new Vector3(0.3f, 2.6f, 0), new Vector3(0.08f, 5.6f, 0.08f));

                for (var i = 0; i < 12; i++)
                {
                    kit.Box(Wood, new Vector3(0, 0.3f + i * 0.45f, 0), new Vector3(0.6f, 0.05f, 0.06f));
                }
            });
            var sinceTower = 30f;
            var sinceLadder = 12f;

            Vector2 At(float d, out Vector2 tangent)
            {
                d = Mathf.Repeat(d, total);

                for (var i = 0; i < perimeter.Count; i++)
                {
                    var next = perimeter[(i + 1) % perimeter.Count];
                    var end = i + 1 < perimeter.Count ? next.distance : total;

                    if (d <= end)
                    {
                        var a = perimeter[i].at;
                        tangent = (next.at - a).normalized;

                        return Vector2.Lerp(a, next.at, (d - perimeter[i].distance) / Mathf.Max(0.001f, end - perimeter[i].distance));
                    }
                }

                tangent = Vector2.right;

                return points[0];
            }

            for (var d = 0f; d < total; d += segment)
            {
                var p = At(d + segment / 2, out var tangent);
                var outward = new Vector2(tangent.y, -tangent.x);

                if (plan.InsideDistance(p + outward) > plan.InsideDistance(p))
                {
                    outward = -outward;
                }

                var yaw = Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg + 90;
                var nearGate = plan.Gates.FirstOrDefault(g => Vector2.Distance(g.At, p) < 7);

                if (nearGate != null)
                {
                    if (Vector2.Distance(nearGate.At, p) < segment * 0.6f)
                    {
                        var gateYaw = Mathf.Atan2(nearGate.Out.x, nearGate.Out.y) * Mathf.Rad2Deg;
                        var low = Mathf.Min(ground(nearGate.At + nearGate.Out * 2), ground(nearGate.At - nearGate.Out * 2));
                        var go = Place(walls, nearGate.Name, gate, new Vector3(nearGate.At.x, low - 0.4f, nearGate.At.y), gateYaw);
                        go.AddComponent<HitboxTag>().Kind = HitboxKind.Building;
                    }

                    continue;
                }

                // A length of wall standing on the lowest ground under it, its top level with the highest plus the wall's height.
                var a = p - tangent * segment / 2;
                var b = p + tangent * segment / 2;
                var gl = Mathf.Min(ground(a), Mathf.Min(ground(b), ground(p)));
                var gh = Mathf.Max(ground(a), Mathf.Max(ground(b), ground(p)));
                var height = clan == Clan.Xiong ? 4.2f : 5.4f;
                var kit = stones.At(new Vector3(p.x, gl, p.y), yaw);
                var tall = gh - gl + height;

                if (clan == Clan.Xiong)
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var x = -segment / 2 + (i + 0.5f) * segment / 8;
                        kit.Cylinder(Logs, new Vector3(x, -1.2f, 0), new Vector3(x, tall + 0.3f, 0), 0.24f, 0.2f, 6);
                        kit.Cylinder(Logs, new Vector3(x, tall + 0.3f, 0), new Vector3(x, tall + 0.7f, 0), 0.2f, 0.01f, 6);
                    }

                    kit.Box(Wood, new Vector3(0, tall - 1.2f, 0.35f), new Vector3(segment, 0.12f, 0.6f));
                }
                else
                {
                    var face = clan == Clan.Bai ? BaiWall : Plaster;
                    kit.Box(Stone, new Vector3(0, (gh - gl) / 2 - 0.9f, 0), new Vector3(segment + 0.05f, gh - gl + 3.4f, 1.7f));
                    kit.Box(face, new Vector3(0, gh - gl + 2.6f, 0), new Vector3(segment + 0.05f, 3.6f, 0.95f));
                    kit.Box(DarkWood, new Vector3(0, gh - gl + 4.5f, 0), new Vector3(segment + 0.08f, 0.3f, 1.0f));

                    for (var i = 0; i < 3; i++)
                    {
                        var x = -segment / 2 + (i + 0.5f) * segment / 3;
                        kit.Box(face, new Vector3(x, gh - gl + 5.0f, -0.25f), new Vector3(0.75f, 0.7f, 0.4f));
                        kit.Box(Ridge, new Vector3(x, gh - gl + 5.4f, -0.25f), new Vector3(0.9f, 0.12f, 0.55f));
                    }
                }

                stones.Box(new Vector3(p.x, gl, p.y), yaw, new Vector3(0, tall / 2 - 0.5f, 0), new Vector3(segment + 0.05f, tall + 1, 1.6f), HitboxKind.Building);

                sinceTower += segment;
                sinceLadder += segment;

                if (sinceTower > 58)
                {
                    var outer = p + outward * 0.8f;
                    var go = Place(walls, "Tower", tower, new Vector3(outer.x, Mathf.Min(gl, ground(outer)) - 0.8f, outer.y), yaw);
                    go.AddComponent<HitboxTag>().Kind = HitboxKind.Building;
                    sinceTower = 0;
                }

                if (sinceLadder > 30)
                {
                    var inside = p - outward * 1.5f;
                    var go = Place(walls, "Ladder", ladder, new Vector3(inside.x, ground(inside), inside.y), yaw + 180, false);
                    go.transform.Rotate(-14, 0, 0, Space.Self);
                    sinceLadder = 0;
                }
            }
        }

        /// <summary>A lesser clan's tower: logs (Xiong) or white walls (Bai) up to a lookout under a roof.</summary>
        static void SmallTower(MeshKit kit, Clan clan)
        {
            var wall = clan == Clan.Bai ? BaiWall : Logs;
            var roof = clan == Clan.Bai ? BaiRoof : Thatch;
            kit.Box(Stone, new Vector3(0, 0.5f, 0), new Vector3(4.2f, 3, 4.2f));
            kit.Box(wall, new Vector3(0, 4, 0), new Vector3(3.6f, 4, 3.6f));
            kit.Box(Wood, new Vector3(0, 6.1f, 0), new Vector3(4.4f, 0.2f, 4.4f));
            Railing(kit, new Vector3(0, 6.2f, 0), 4.2f, 4.2f, 0, -1, DarkWood, DarkWood);

            foreach (var x in new[] { -1.9f, 1.9f })
            {
                foreach (var z in new[] { -1.9f, 1.9f })
                {
                    kit.Box(DarkWood, new Vector3(x, 7.2f, z), new Vector3(0.18f, 2.2f, 0.18f));
                }
            }

            GableRoof(kit, roof, new Vector3(0, 8.3f, 0), 4.2f, 4.2f, 1.8f, 0.5f, 0, clan != Clan.Bai, wall);
        }

        /// <summary>A lesser clan's gate: two posts or towers, a beam, a roof.</summary>
        static void SmallGate(MeshKit kit, Clan clan)
        {
            var wall = clan == Clan.Bai ? BaiWall : Logs;

            foreach (var s in new[] { -1, 1 })
            {
                kit.Box(Stone, new Vector3(s * 3.6f, 1, 0), new Vector3(1.8f, 4, 2.6f));
                kit.Box(wall, new Vector3(s * 3.6f, 4.6f, 0), new Vector3(1.6f, 3.4f, 2.3f));
                kit.Box(DarkWood, new Vector3(s * 2.6f, 2.8f, 0), new Vector3(0.3f, 5.6f, 0.3f));
                kit.Box(DarkWood, new Vector3(s * 2.2f, 2.6f, 1.6f), new Vector3(0.15f, 4.8f, 2.4f));
            }

            kit.Box(DarkWood, new Vector3(0, 5.8f, 0), new Vector3(8, 0.4f, 0.5f));
            GableRoof(kit, clan == Clan.Bai ? BaiRoof : Thatch, new Vector3(0, 6.1f, 0), 8, 2.6f, 1.2f, 0.4f, 0, clan != Clan.Bai, wall);
        }

        /// <summary>The walkways: nodes along every street every few metres, joined along it and where streets meet; each doorstep and place joined to the nearest.</summary>
        static void BuildVillageLayout(Transform root, VillagePlan plan, Func<Vector2, float> ground)
        {
            var nodes = new List<Vector2>();
            var edges = new HashSet<(int, int)>();

            int Node(Vector2 at)
            {
                for (var i = nodes.Count - 1; i >= Mathf.Max(0, nodes.Count - 400); i--)
                {
                    if ((nodes[i] - at).sqrMagnitude < 1)
                    {
                        return i;
                    }
                }

                nodes.Add(at);

                return nodes.Count - 1;
            }

            void Link(int a, int b)
            {
                if (a != b)
                {
                    edges.Add(a < b ? (a, b) : (b, a));
                }
            }

            foreach (var street in plan.Streets)
            {
                var previous = -1;
                var carried = 99f;

                for (var i = 0; i < street.Points.Count; i++)
                {
                    carried += i > 0 ? Vector2.Distance(street.Points[i], street.Points[i - 1]) : 0;

                    if (carried < 5 && i + 1 < street.Points.Count)
                    {
                        continue;
                    }

                    carried = 0;
                    var n = Node(street.Points[i]);

                    if (previous >= 0)
                    {
                        Link(previous, n);
                    }

                    previous = n;
                }
            }

            // Where streets meet or pass close: joined (a spatial grid keeps it quick).
            var grid = new Dictionary<Vector2Int, List<int>>();

            for (var i = 0; i < nodes.Count; i++)
            {
                var key = new Vector2Int(Mathf.FloorToInt(nodes[i].x / 6), Mathf.FloorToInt(nodes[i].y / 6));

                if (!grid.TryGetValue(key, out var list))
                {
                    grid[key] = list = new List<int>();
                }

                list.Add(i);
            }

            for (var i = 0; i < nodes.Count; i++)
            {
                var key = new Vector2Int(Mathf.FloorToInt(nodes[i].x / 6), Mathf.FloorToInt(nodes[i].y / 6));

                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dz = -1; dz <= 1; dz++)
                    {
                        if (!grid.TryGetValue(new Vector2Int(key.x + dx, key.y + dz), out var list))
                        {
                            continue;
                        }

                        foreach (var j in list)
                        {
                            if (j > i && (nodes[i] - nodes[j]).sqrMagnitude < 4.5f * 4.5f && Mathf.Abs(ground(nodes[i]) - ground(nodes[j])) < 1.2f)
                            {
                                Link(i, j);
                            }
                        }
                    }
                }
            }

            int Nearest(Vector2 at)
            {
                var best = 0;

                for (var i = 1; i < nodes.Count; i++)
                {
                    if ((nodes[i] - at).sqrMagnitude < (nodes[best] - at).sqrMagnitude)
                    {
                        best = i;
                    }
                }

                return best;
            }

            var spots = new List<VillageLayout.Spot>();

            foreach (var (name, at, yaw) in plan.Places)
            {
                spots.Add(new VillageLayout.Spot { name = name, at = new Vector3(at.x, ground(at), at.y), yaw = yaw });
                Link(Node(at), Nearest(at));
            }

            // The places outside the walls the villagers go to: the spring cave and the grove (Gu Yue's).
            if (plan.Place.Clan == Clan.GuYue)
            {
                var spring = QingMao.Spring + plan.Down * 9;
                spots.Add(new VillageLayout.Spot { name = "spring", at = new Vector3(spring.x, ground(spring), spring.y), yaw = Mathf.Atan2(-plan.Down.x, -plan.Down.y) * Mathf.Rad2Deg });
                Link(Node(spring), Nearest(spring));
            }

            var homes = new List<VillageLayout.Spot>();

            foreach (var (at, yaw) in plan.Doorsteps)
            {
                homes.Add(new VillageLayout.Spot { name = $"house {homes.Count}", at = new Vector3(at.x, ground(at), at.y), yaw = yaw });
                Link(Node(at), Nearest(at));
            }

            var layout = root.gameObject.AddComponent<VillageLayout>();
            layout.Settlement = plan.Place.Id;
            layout.Houses = homes.ToArray();
            layout.Places = spots.ToArray();
            layout.Nodes = nodes.Select(n => new Vector3(n.x, ground(n), n.y)).ToArray();
            layout.Edges = edges.Select(e => new Vector2Int(e.Item1, e.Item2)).ToArray();
        }

        // ── The hamlets ───────────────────────────────────────────────

        /// <summary>A hamlet of common folk at the foot: thatched houses round a yard, fields, haystacks, a cart; no wall.</summary>
        static void BuildHamlet(Settlement place, Func<float, float, float> natural, List<WorldPlan.Footprint> footprints)
        {
            var root = new GameObject(place.Name).transform;
            var random = new System.Random(Seed(place.Id));
            float R() => (float)random.NextDouble();
            float Ground(Vector2 p) => natural(p.x, p.y);
            var under = new Blocks(root, $"{place.Id} footings");
            var things = new Blocks(root, $"{place.Id} things");
            var steps = new Blocks(root, $"{place.Id} steps");
            var houses = new List<(Vector2 at, float r)>();
            var count = 8 + random.Next(10);

            for (var tries = 0; tries < 400 && houses.Count < count; tries++)
            {
                var a = R() * Mathf.PI * 2;
                var d = 6 + R() * place.Radius;
                var at = place.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                var size = new Vector2(3.8f + R() * 3, 3.4f + R() * 2.4f);

                if (houses.Any(h => Vector2.Distance(h.at, at) < h.r + size.magnitude / 2 + 1.5f) || MountainShape.NearStream(at, 12, out _, out _, out _, out _) || TrailNear(at, size.magnitude / 2 + 3))
                {
                    continue;
                }

                var yaw = Mathf.Atan2(place.Centre.x - at.x, place.Centre.y - at.y) * Mathf.Rad2Deg + (R() - 0.5f) * 30;
                var high = float.NegativeInfinity;
                var turn = Quaternion.Euler(0, yaw, 0);

                foreach (var (x, z) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                {
                    var v = turn * new Vector3(x * size.x / 2, 0, z * size.y / 2);
                    high = Mathf.Max(high, Ground(at + new Vector2(v.x, v.z)));
                }

                var b = new VillagePlan.Building { Kind = R() < 0.2f ? BuildingKind.Shed : BuildingKind.Poor, Centre = at, Size = size, Yaw = yaw, Floor = high + 0.15f, Seed = random.Next(), Lit = R() < 0.6f };
                PlaceBuilding(root, b, Clan.Mortal, Ground, under, steps, houses.Count);
                houses.Add((at, size.magnitude / 2));
                footprints.Add(new WorldPlan.Footprint { Centre = at, Half = size / 2, Yaw = yaw, Tint = RoofTint(Clan.Mortal, b.Kind) });

                foreach (var thing in new[] { "firewood", "water jar", "basket", "hay", "cart", "sacks", "broom" })
                {
                    if (R() < 0.3f)
                    {
                        var v = turn * new Vector3((R() - 0.5f) * size.x, 0, size.y / 2 + 0.6f + R());
                        var p = at + new Vector2(v.x, v.z);
                        var where = new Vector3(p.x, Ground(p), p.y);
                        DrawProp(things.At(where, yaw + (R() - 0.5f) * 40), thing, random.Next());

                        if (PropSolid(thing) is (Vector3 c, Vector3 s))
                        {
                            things.Box(where, yaw, c, s, HitboxKind.Prop);
                        }
                    }
                }
            }

            // Fields round it: rows of crops on the gentle ground, and haystacks.
            for (var f = 0; f < 4; f++)
            {
                var a = R() * Mathf.PI * 2;
                var centre = place.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (place.Radius + 18 + R() * 30);
                var yaw = R() * 180;
                var turn = Quaternion.Euler(0, yaw, 0);

                for (var row = -6; row <= 6; row++)
                {
                    for (var s = -10; s <= 10; s += 2)
                    {
                        var v = turn * new Vector3(s, 0, row * 1.1f);
                        var p = centre + new Vector2(v.x, v.z);

                        if (MountainShape.NearStream(p, 8, out _, out _, out _, out _))
                        {
                            continue;
                        }

                        var where = new Vector3(p.x, Ground(p), p.y);
                        var kit = things.At(where, yaw);
                        kit.Box(Soil, new Vector3(0, 0.05f, 0), new Vector3(2.1f, 0.12f, 0.6f));
                        kit.Lump(Greens, new Vector3(-0.5f, 0.2f, 0), new Vector3(0.32f, 0.25f, 0.32f), row * 31 + s);
                        kit.Lump(Greens, new Vector3(0.5f, 0.2f, 0), new Vector3(0.32f, 0.25f, 0.32f), row * 17 + s);
                    }
                }

                var stack = centre + new Vector2(14, 0);
                DrawProp(things.At(new Vector3(stack.x, Ground(stack), stack.y), 0), "hay", random.Next());
            }

            under.Finish(false, HitboxKind.Building, true, 0.035f);
            things.Finish(false, HitboxKind.Prop, false, 0.22f);
            steps.Finish(true, HitboxKind.Prop, false, 0.05f);
        }
    }
}
