using System.Collections.Generic;
using System.Linq;
using GuWorld.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuWorld.Editor
{
    /// <summary>
    /// The places on the mountain besides the villages, the hamlets and the trails (the "landmarks" stage): the Moon
    /// Pavilion (the clan's hall built from the artist's model, on a stone terrace above the village, its moon gate and
    /// lanterns before it), a bamboo manor by the spear bamboo grove, the ancestors' shrine (altar, bronze ding, the
    /// clan's stele), the stone forest (rock pillars on a high plateau, where the jade-skin Gu live), a demonic path
    /// camp far out in the wilds, two old watchtowers on the ways to the Bai and the Xiong, and two tunnels bored
    /// through ridges (their mouths cut into the ground's tiles as holes). Where each stands is worked out in Prepare
    /// from the mountain's natural shape — the same every run — so the terrain stage keeps its trees and grass off them
    /// and cuts the tunnels' mouths, and the trails lead to some of them.
    /// </summary>
    public static partial class QingMaoBuilder
    {
        /// <summary>A place built on the mountain: its id (for the map, the chat's /tp), its name, kind, middle, turn, the ground it takes.</summary>
        class Landmark
        {
            public string Id;
            public string Name;
            public string Kind;
            public Vector2 At;
            public float Yaw;
            public float Radius;
        }

        /// <summary>A tunnel through a ridge: its two mouths (on the ground), half its width and its height.</summary>
        class Tunnel
        {
            public string Id;
            public string Name;
            public Vector2 A;
            public Vector2 B;
            public float FloorA;
            public float FloorB;
            public float Half = 4.5f;
            public float Height = 7;

            public float Length => Vector2.Distance(A, B);
            public Vector2 Along => (B - A).normalized;
            public float FloorAt(float t) => Mathf.Lerp(FloorA, FloorB, t);
        }

        static readonly List<Landmark> landmarks = new List<Landmark>();
        static readonly List<Tunnel> tunnels = new List<Tunnel>();

        /// <summary>The artist's models (GLB ASSETS), imported by glTFast.</summary>
        const string Models = "Assets/GuWorld/Models/Landmarks";

        // ── Where they stand (Prepare) ─────────────────────────────────

        /// <summary>Chooses every landmark's and tunnel's place, before the trails are laid (some lead to them).</summary>
        static void ChooseLandmarks(List<VillagePlan> plans)
        {
            landmarks.Clear();
            tunnels.Clear();
            var home = QingMao.GuYue;
            var bai = QingMao.Bai;
            var xiong = QingMao.Xiong;

            Add("moon_pavilion", "Лунный павильон", "pavilion", home, 380, 760, 30, 12);
            Add("ancestor_shrine", "Святилище предков", "shrine", home, 300, 560, 14, 14);
            Add("bamboo_manor", "Бамбуковая усадьба", "manor", QingMao.Grove, 200, 420, 24, 14);
            Add("stone_forest", "Каменный лес", "stone_forest", home, 1300, 2300, 85, 22);
            Add("demon_camp", "Лагерь демонического пути", "camp", home, 2400, 3400, 24, 16);
            Add("watchtower_bai", "Старая башня на пути к Бай", "tower", Vector2.Lerp(home, bai, 0.45f), 0, 700, 10, 18);
            Add("watchtower_xiong", "Старая башня на пути к Сюн", "tower", Vector2.Lerp(home, xiong, 0.45f), 0, 700, 10, 18);

            FindTunnel("tunnel_old", "Старый тоннель", home, 700, 2800);
            FindTunnel("tunnel_smugglers", "Тоннель контрабандистов", home, 2600, 5200);

            BuildLog.Write($"landmarks: {string.Join(", ", landmarks.Select(l => $"{l.Id} ({l.At.x:0}, {l.At.y:0})"))}; " +
                           $"tunnels: {string.Join(", ", tunnels.Select(t => $"{t.Id} {t.Length:0} m"))}");
        }

        /// <summary>Whether a point is free for a landmark: not in a settlement, a stream, the pool, another landmark, off the map.</summary>
        static bool FreeForLandmark(Vector2 p, float radius)
        {
            if (Mathf.Abs(p.x) > QingMao.Half - 400 || Mathf.Abs(p.y) > QingMao.Half - 400)
            {
                return false;
            }

            if (QingMao.Settlements.Any(s => Vector2.Distance(p, s.Centre) < s.Radius + radius + 90))
            {
                return false;
            }

            if (MountainShape.NearStream(p, radius + 40, out var d, out _, out var half, out _) && d < half + radius + 10)
            {
                return false;
            }

            if (Vector2.Distance(p, QingMao.Pool) < QingMao.PoolRadius + radius + 60 || Vector2.Distance(p, QingMao.SecretCave) < radius + 60 ||
                Vector2.Distance(p, QingMao.Grove) < QingMao.GroveRadius + radius + 30)
            {
                return false;
            }

            return !landmarks.Any(l => Vector2.Distance(l.At, p) < l.Radius + radius + 80) &&
                   !tunnels.Any(t => QingMao.ToSegment(p, t.A, t.B) < t.Half + radius + 40);
        }

        /// <summary>How steep the ground is round a point, in degrees: the most it rises or falls to a ring of points about it.</summary>
        static float SlopeAround(Vector2 p, float radius)
        {
            var h = Natural(p.x, p.y);
            var most = 0f;

            for (var k = 0; k < 12; k++)
            {
                var a = k * Mathf.PI / 6;
                var q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                most = Mathf.Max(most, Mathf.Abs(Natural(q.x, q.y) - h));
            }

            return Mathf.Atan2(most, radius) * Mathf.Rad2Deg;
        }

        /// <summary>A landmark put on the flattest free ground in a ring round a point (the same every run): none if no ground fits.</summary>
        static void Add(string id, string name, string kind, Vector2 round, float from, float to, float radius, float steepest)
        {
            var random = new System.Random(Seed(id));
            Vector2? best = null;
            var bestScore = float.PositiveInfinity;

            for (var i = 0; i < 700; i++)
            {
                var a = (float)random.NextDouble() * Mathf.PI * 2;
                var r = Mathf.Lerp(from, to, Mathf.Sqrt((float)random.NextDouble()));
                var p = round + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;

                if (!FreeForLandmark(p, radius) || Natural(p.x, p.y) > 1900)
                {
                    continue;
                }

                var slope = SlopeAround(p, radius);

                if (slope > steepest)
                {
                    continue;
                }

                // Flatter is better; nearer the ring's middle a little better.
                var score = slope + (r - from) / Mathf.Max(1, to - from) * 2;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }

            if (best is Vector2 at)
            {
                // Facing downhill (the view, and the way one comes up to it).
                var down = Vector2.zero;

                for (var k = 0; k < 8; k++)
                {
                    var dir = new Vector2(Mathf.Cos(k * Mathf.PI / 4), Mathf.Sin(k * Mathf.PI / 4));
                    down += dir * (Natural(at.x, at.y) - Natural(at.x + dir.x * radius, at.y + dir.y * radius));
                }

                var yaw = down.sqrMagnitude > 0.01f ? Mathf.Atan2(down.x, down.y) * Mathf.Rad2Deg : 0;
                landmarks.Add(new Landmark { Id = id, Name = name, Kind = kind, At = at, Yaw = yaw, Radius = radius });
            }
            else
            {
                BuildLog.Write($"landmarks: no ground for {id}");
            }
        }

        /// <summary>
        /// A tunnel through a ridge somewhere in a ring round a point: two mouths on gentle ground 140–260 m apart and at
        /// nearly the same height, with the ridge between them well over the vault — the most rock over it, the shortest.
        /// </summary>
        static void FindTunnel(string id, string name, Vector2 round, float from, float to)
        {
            var random = new System.Random(Seed(id));
            Tunnel best = null;
            var bestScore = float.NegativeInfinity;

            for (var i = 0; i < 2500; i++)
            {
                var a = (float)random.NextDouble() * Mathf.PI * 2;
                var r = Mathf.Lerp(from, to, (float)random.NextDouble());
                var middle = round + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                var turn = (float)random.NextDouble() * Mathf.PI;
                var along = new Vector2(Mathf.Cos(turn), Mathf.Sin(turn));
                var length = 120 + (float)random.NextDouble() * 200;
                var pa = middle - along * length / 2;
                var pb = middle + along * length / 2;
                var ha = Natural(pa.x, pa.y);
                var hb = Natural(pb.x, pb.y);

                if (Mathf.Abs(ha - hb) > length * 0.12f || ha > 1850 || hb > 1850 || !FreeForLandmark(pa, 12) || !FreeForLandmark(pb, 12) ||
                    SlopeAround(pa, 10) > 28 || SlopeAround(pb, 10) > 28 || TrailNear(middle, 20))
                {
                    continue;
                }

                // The rock over the vault along the middle half: the least of it must be thick.
                var least = float.PositiveInfinity;

                for (var k = 1; k < 10; k++)
                {
                    var t = 0.25f + 0.5f * k / 10;
                    var p = Vector2.Lerp(pa, pb, t);
                    least = Mathf.Min(least, Natural(p.x, p.y) - Mathf.Lerp(ha, hb, t));
                }

                if (least < 15)
                {
                    continue;
                }

                var score = Mathf.Min(least, 60) - length * 0.08f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = new Tunnel { Id = id, Name = name, A = pa, B = pb, FloorA = ha, FloorB = hb };
                }
            }

            if (best != null)
            {
                tunnels.Add(best);
            }
            else
            {
                BuildLog.Write($"landmarks: no ridge for {id}");
            }
        }

        /// <summary>Whether a point is taken by a landmark or a tunnel's way (trees, grass and giants keep off it), with room to spare.</summary>
        static bool NearLandmark(Vector2 p, float room)
        {
            foreach (var landmark in landmarks)
            {
                if ((landmark.At - p).sqrMagnitude < (landmark.Radius + room) * (landmark.Radius + room))
                {
                    return true;
                }
            }

            foreach (var tunnel in tunnels)
            {
                // The mouths' approaches: 25 m out from each.
                var a = tunnel.A - tunnel.Along * 25;
                var b = tunnel.B + tunnel.Along * 25;

                if (QingMao.ToSegment(p, a, b) < tunnel.Half + 3 + room && (Vector2.Distance(p, tunnel.A) < 45 || Vector2.Distance(p, tunnel.B) < 45))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a cell of the mountain's ground (given by its middle) is cut away for a tunnel: any part of it over the
        /// tunnel's way, its ground above the floor somewhere and not everywhere well over the vault — else the tile's ground
        /// would show inside. The cut is ragged (whole cells of 4.9 m, turned to the tunnel): the rock mound over the tunnel
        /// and the aprons before its mouths (BuildTunnel) reach past it on every side.
        /// </summary>
        static bool TunnelMouth(Vector2 middle)
        {
            foreach (var tunnel in tunnels)
            {
                var along = tunnel.Along;
                var d = Vector2.Dot(middle - tunnel.A, along);
                var s = Vector2.Dot(middle - tunnel.A, new Vector2(-along.y, along.x));
                // How far the cell reaches from its middle along the tunnel (and as far across it: the cell is square).
                var reach = Cell / 2 * (Mathf.Abs(along.x) + Mathf.Abs(along.y));

                if (Mathf.Abs(s) > tunnel.Half + 0.3f + reach || d < -reach - 0.3f || d > tunnel.Length + reach + 0.3f)
                {
                    continue;
                }

                // The ground at the cell's corners (the tile draws it straight between them) over the floor beside each.
                var low = float.PositiveInfinity;
                var high = float.NegativeInfinity;

                for (var k = 0; k < 4; k++)
                {
                    var c = middle + new Vector2((k & 1) == 0 ? -Cell / 2 : Cell / 2, (k & 2) == 0 ? -Cell / 2 : Cell / 2);
                    var over = Natural(c.x, c.y) - tunnel.FloorAt(Mathf.Clamp01(Vector2.Dot(c - tunnel.A, along) / tunnel.Length));
                    low = Mathf.Min(low, over);
                    high = Mathf.Max(high, over);
                }

                if (high > 0.2f && low < tunnel.Height + 1.5f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Cuts the tunnels into the mountain's tiles again, where they pass: the "landmarks" stage owns them, so a tunnel
        /// built anew needs no new terrain (the "terrain" stage cuts the same, through TunnelMouth).
        /// </summary>
        static void RecutTunnels()
        {
            foreach (var (name, corner) in BaseTileNames())
            {
                var data = AssetDatabase.LoadAssetAtPath<TerrainData>($"{Generated}/Terrain/{name}.asset");

                if (data == null || !InArea(new Rect(corner, Vector2.one * TileSize)))
                {
                    continue;
                }

                var changed = false;

                foreach (var tunnel in tunnels)
                {
                    var low = Vector2.Min(tunnel.A, tunnel.B) - Vector2.one * 30 - corner;
                    var high = Vector2.Max(tunnel.A, tunnel.B) + Vector2.one * 30 - corner;
                    var i0 = Mathf.Max(0, Mathf.FloorToInt(low.x / Cell));
                    var j0 = Mathf.Max(0, Mathf.FloorToInt(low.y / Cell));
                    var i1 = Mathf.Min(BaseResolution - 2, Mathf.CeilToInt(high.x / Cell));
                    var j1 = Mathf.Min(BaseResolution - 2, Mathf.CeilToInt(high.y / Cell));

                    if (i1 < i0 || j1 < j0)
                    {
                        continue;
                    }

                    // Solid where the ground stands (as BuildBaseTile has it: the villages' own tiles lie over holes).
                    var holes = new bool[j1 - j0 + 1, i1 - i0 + 1];
                    var cut = 0;

                    for (var j = j0; j <= j1; j++)
                    {
                        for (var i = i0; i <= i1; i++)
                        {
                            var middle = new Vector2(corner.x + (i + 0.5f) * Cell, corner.y + (j + 0.5f) * Cell);
                            var mouth = TunnelMouth(middle);
                            holes[j - j0, i - i0] = !mouth && !plans.Any(plan => plan.Area.Contains(middle));
                            cut += mouth ? 1 : 0;
                        }
                    }

                    data.SetHoles(i0, j0, holes);
                    changed = true;
                    BuildLog.Write($"landmarks: {tunnel.Id} cut {cut} cells of {name}");
                }

                if (changed)
                {
                    EditorUtility.SetDirty(data);
                }
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>The trails to the landmarks near the village (laid with the others).</summary>
        static void LayLandmarkTrails(System.Func<string, string, Vector2> gate)
        {
            foreach (var landmark in landmarks)
            {
                var to = landmark.At + new Vector2(Mathf.Sin(landmark.Yaw * Mathf.Deg2Rad), Mathf.Cos(landmark.Yaw * Mathf.Deg2Rad)) * (landmark.Radius + 6);

                switch (landmark.Id)
                {
                    case "moon_pavilion":
                        LayTrail($"Тропа к {landmark.Name.ToLowerInvariant().Replace("лунный павильон", "Лунному павильону")}", gate("gu_yue", "gate north"), to, 1.1f, "gu_yue");
                        break;
                    case "ancestor_shrine":
                        LayTrail("Тропа к святилищу предков", gate("gu_yue", "gate west"), to, 0.9f, "gu_yue");
                        break;
                    case "bamboo_manor":
                        LayTrail("Тропа к бамбуковой усадьбе", QingMao.Grove + new Vector2(60, -40), to, 0.9f);
                        break;
                }
            }
        }

        // ── Building them (the "landmarks" stage) ──────────────────────

        /// <summary>Builds every landmark and tunnel on the ground as built, under one root; the world's plan gets their marks.</summary>
        static void BuildLandmarks(PlanPart part)
        {
            var root = new GameObject("Landmarks").transform;

            foreach (var landmark in landmarks)
            {
                if (!InArea(landmark.At))
                {
                    continue;
                }

                var go = new GameObject(landmark.Name).transform;
                go.SetParent(root, false);

                switch (landmark.Kind)
                {
                    case "pavilion": BuildPavilion(go, landmark); break;
                    case "shrine": BuildShrine(go, landmark); break;
                    case "manor": BuildManor(go, landmark); break;
                    case "stone_forest": BuildStoneForest(go, landmark); break;
                    case "camp": BuildCamp(go, landmark); break;
                    case "tower": BuildTower(go, landmark); break;
                }

                part.Marks.Add(new WorldPlan.Mark { Id = landmark.Id, Label = landmark.Name, At = new Vector3(landmark.At.x, Ground(landmark.At), landmark.At.y), Major = true });
                BuildLog.Mark($"landmarks: {landmark.Id}");
            }

            foreach (var tunnel in tunnels)
            {
                if (!InArea(tunnel.A) && !InArea(tunnel.B))
                {
                    continue;
                }

                BuildTunnel(root, tunnel);
                part.Marks.Add(new WorldPlan.Mark { Id = tunnel.Id, Label = tunnel.Name, At = new Vector3(tunnel.A.x, tunnel.FloorA, tunnel.A.y), Major = true });
                part.Marks.Add(new WorldPlan.Mark { Id = tunnel.Id + "_far", Label = tunnel.Name + " (выход)", At = new Vector3(tunnel.B.x, tunnel.FloorB, tunnel.B.y) });
                BuildLog.Mark($"landmarks: {tunnel.Id}");
            }
        }

        static float Ground(Vector2 p) => QingMao.GroundAt(new Vector3(p.x, 0, p.y));

        /// <summary>The highest and lowest ground under a rectangle (turned), sampled every 2 m.</summary>
        static (float low, float high) GroundUnder(Vector2 centre, float yaw, float width, float depth)
        {
            var turn = Quaternion.Euler(0, yaw, 0);
            var low = float.PositiveInfinity;
            var high = float.NegativeInfinity;

            for (var z = -depth / 2; z <= depth / 2; z += 2)
            {
                for (var x = -width / 2; x <= width / 2; x += 2)
                {
                    var o = turn * new Vector3(x, 0, z);
                    var h = Ground(centre + new Vector2(o.x, o.z));
                    low = Mathf.Min(low, h);
                    high = Mathf.Max(high, h);
                }
            }

            return (low, high);
        }

        /// <summary>
        /// A dressed-stone terrace for a building: its top just over the highest ground under it, its sides down into
        /// the lowest, and steps down the front (+Z) to the ground. Returns the top's height.
        /// </summary>
        static float Terrace(Transform parent, string name, Vector2 centre, float yaw, float width, float depth, float above = 0.4f)
        {
            var (low, high) = GroundUnder(centre, yaw, width + 2, depth + 2);
            var top = high + above;
            var kit = new MeshKit();
            var height = top - (low - 2);
            kit.Box(Stone, new Vector3(0, -height / 2, 0), new Vector3(width, height, depth));
            // A coping of darker stone round the top.
            kit.Box(Rubble, new Vector3(0, -0.15f, depth / 2 - 0.3f), new Vector3(width + 0.4f, 0.4f, 0.7f));
            kit.Box(Rubble, new Vector3(0, -0.15f, -depth / 2 + 0.3f), new Vector3(width + 0.4f, 0.4f, 0.7f));
            kit.Box(Rubble, new Vector3(width / 2 - 0.3f, -0.15f, 0), new Vector3(0.7f, 0.4f, depth + 0.4f));
            kit.Box(Rubble, new Vector3(-width / 2 + 0.3f, -0.15f, 0), new Vector3(0.7f, 0.4f, depth + 0.4f));

            // The steps down the front, to where the ground in front is.
            var front = new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad));
            var foot = Ground(centre + front * (depth / 2 + 3));
            var rise = Mathf.Max(0, top - foot);
            var steps = Mathf.CeilToInt(rise / 0.22f);

            for (var s = 0; s < steps; s++)
            {
                var y = -rise + (s + 1) * rise / steps;
                kit.Box(Stone, new Vector3(0, y - (y + rise) / 2, depth / 2 + (steps - s) * 0.32f - 0.16f), new Vector3(Mathf.Min(width * 0.4f, 8), y + rise, 0.34f));
            }

            Place(parent, name, Save(kit, $"Landmark {name}"), new Vector3(centre.x, top, centre.y), yaw);

            return top;
        }

        /// <summary>One of the artist's models placed (its LOD1 too where it has one, drawn far off), solid by its own meshes.</summary>
        static GameObject Model(Transform parent, string file, Vector3 at, float yaw, string far = null, bool solid = true)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{file}");

            if (asset == null)
            {
                BuildLog.Write($"landmarks: the model {file} is not imported");

                return null;
            }

            var holder = new GameObject(System.IO.Path.GetFileNameWithoutExtension(file));
            holder.transform.SetParent(parent, false);
            holder.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            var near = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder.scene);
            near.transform.SetParent(holder.transform, false);
            PrefabUtility.UnpackPrefabInstance(near, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            GameObject farOne = null;

            if (far != null && AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{far}") is GameObject farAsset)
            {
                farOne = (GameObject)PrefabUtility.InstantiatePrefab(farAsset, holder.scene);
                farOne.transform.SetParent(holder.transform, false);
                PrefabUtility.UnpackPrefabInstance(farOne, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            var nearRenderers = near.GetComponentsInChildren<Renderer>();
            var farRenderers = farOne != null ? farOne.GetComponentsInChildren<Renderer>() : new Renderer[0];

            foreach (var renderer in nearRenderers.Concat(farRenderers))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }

            // Near: the full model; far: the lighter one; farther still, nothing (the haze).
            var group = holder.AddComponent<LODGroup>();
            group.SetLODs(farOne != null
                ? new[] { new LOD(0.25f, nearRenderers), new LOD(0.02f, farRenderers) }
                : new[] { new LOD(0.015f, nearRenderers) });
            group.RecalculateBounds();

            if (solid)
            {
                // Solid by the lighter model's meshes where there is one (the colliders weigh less).
                foreach (var filter in (farOne != null ? farOne : near).GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh != null)
                    {
                        filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    }
                }
            }

            holder.AddComponent<GuWorld.Render.HitboxTag>().Kind = GuWorld.Render.HitboxKind.Building;

            return holder;
        }

        /// <summary>A point in a landmark's own frame (X right, Z its front) on the ground there.</summary>
        static Vector3 OnGroundAt(Landmark landmark, float x, float z, float above = 0)
        {
            var o = Quaternion.Euler(0, landmark.Yaw, 0) * new Vector3(x, 0, z);
            var p = landmark.At + new Vector2(o.x, o.z);

            return new Vector3(p.x, Ground(p) + above, p.y);
        }

        static Vector3 OnTop(Landmark landmark, float x, float z, float top)
        {
            var o = Quaternion.Euler(0, landmark.Yaw, 0) * new Vector3(x, 0, z);

            return new Vector3(landmark.At.x + o.x, top, landmark.At.y + o.z);
        }

        /// <summary>The Moon Pavilion: the artist's hall on a stone terrace, its moon gate and lantern posts down the approach, a ding and the clan's stele.</summary>
        static void BuildPavilion(Transform root, Landmark landmark)
        {
            // The hall's court reaches 15.5 m out in front, its back 10.7 m behind: the terrace under all of it.
            var centre = landmark.At;
            var top = Terrace(root, "Pavilion Terrace", centre + Front(landmark) * 2.5f, landmark.Yaw, 27, 29);
            Model(root, "guyue_pavilion_LOD0.glb", new Vector3(centre.x, top, centre.y), landmark.Yaw, "guyue_pavilion_LOD1.glb");
            Model(root, "prop_moon_gate.glb", OnGroundAt(landmark, 0, 26), landmark.Yaw);

            foreach (var z in new[] { 31f, 38f, 45f })
            {
                Model(root, "prop_moon_lantern_post.glb", OnGroundAt(landmark, -4.5f, z), landmark.Yaw);
                Model(root, "prop_moon_lantern_post.glb", OnGroundAt(landmark, 4.5f, z), landmark.Yaw);
            }

            Model(root, "prop_bronze_ding.glb", OnGroundAt(landmark, -10, 30), landmark.Yaw);
            Model(root, "prop_clan_stele.glb", OnGroundAt(landmark, 10, 30), landmark.Yaw - 90);
        }

        static Vector2 Front(Landmark landmark) => new Vector2(Mathf.Sin(landmark.Yaw * Mathf.Deg2Rad), Mathf.Cos(landmark.Yaw * Mathf.Deg2Rad));

        /// <summary>The ancestors' shrine: a small stone court, the altar with its offerings, a ding of incense, the stele, two lanterns.</summary>
        static void BuildShrine(Transform root, Landmark landmark)
        {
            var top = Terrace(root, "Shrine Court", landmark.At, landmark.Yaw, 14, 12, 0.3f);
            Model(root, "prop_ancestral_altar.glb", OnTop(landmark, 0, -3.5f, top), landmark.Yaw);
            Model(root, "prop_folding_screen.glb", OnTop(landmark, 0, -5.2f, top), landmark.Yaw);
            Model(root, "prop_bronze_ding.glb", OnTop(landmark, 0, 1.5f, top), landmark.Yaw);
            Model(root, "prop_clan_stele.glb", OnTop(landmark, -4.5f, -1, top), landmark.Yaw + 90);
            Model(root, "prop_moon_lantern_post.glb", OnTop(landmark, -5.5f, 4.5f, top), landmark.Yaw);
            Model(root, "prop_moon_lantern_post.glb", OnTop(landmark, 5.5f, 4.5f, top), landmark.Yaw);
            Model(root, "prop_gu_jar_shelf.glb", OnTop(landmark, 5, -3, top), landmark.Yaw - 90);
        }

        /// <summary>A bamboo manor by the grove: two of the artist's bamboo houses on low footings, bamboo clumps about them, a lantern.</summary>
        static void BuildManor(Transform root, Landmark landmark)
        {
            for (var i = 0; i < 2; i++)
            {
                var x = i == 0 ? -8 : 9;
                var z = i == 0 ? 0 : -6;
                var o = Quaternion.Euler(0, landmark.Yaw, 0) * new Vector3(x, 0, z);
                var at = landmark.At + new Vector2(o.x, o.z);
                var top = Terrace(root, $"Manor Footing {i}", at + Front(landmark) * 0.2f, landmark.Yaw + (i == 0 ? 0 : -20), 14, 12, 0.5f);
                Model(root, "guyue_bamboo_house.glb", new Vector3(at.x, top, at.y), landmark.Yaw + (i == 0 ? 0 : -20));
            }

            var random = new System.Random(Seed(landmark.Id));

            for (var i = 0; i < 6; i++)
            {
                var a = (float)random.NextDouble() * Mathf.PI * 2;
                var r = 17 + (float)random.NextDouble() * 5;
                Model(root, "prop_bamboo_grove.glb", OnGroundAt(landmark, Mathf.Cos(a) * r, Mathf.Sin(a) * r - 4), (float)random.NextDouble() * 360);
            }

            Model(root, "prop_moon_lantern_post.glb", OnGroundAt(landmark, 0, 11), landmark.Yaw);
        }

        static Material JadeRock => Mat("Jade Rock", 0x4c6e5c, 0.2f, false, RubbleTexture(), 0.3f);

        /// <summary>The stone forest: some eighty rock pillars on the plateau, stacked boulders 8–30 m tall, a few of a jade green.</summary>
        static void BuildStoneForest(Transform root, Landmark landmark)
        {
            var random = new System.Random(Seed(landmark.Id));
            float R() => (float)random.NextDouble();
            var pillars = new List<Vector2>();
            var kit = new MeshKit();
            var piece = 0;
            var origin = new Vector3(landmark.At.x, Ground(landmark.At), landmark.At.y);

            for (var tries = 0; tries < 900 && pillars.Count < 80; tries++)
            {
                var a = R() * Mathf.PI * 2;
                var r = Mathf.Sqrt(R()) * landmark.Radius;
                var p = landmark.At + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;

                if (pillars.Any(q => Vector2.Distance(p, q) < 8) || TrailNear(p, 4))
                {
                    continue;
                }

                pillars.Add(p);
                var ground = Ground(p) - origin.y;
                var tall = 8 + Mathf.Pow(R(), 1.6f) * 24;
                var radius = 1.4f + R() * 2.4f;
                var jade = R() < 0.12f;
                var y = ground - 1.5f;
                var stones = 2 + Mathf.CeilToInt(tall / 7);

                for (var s = 0; s < stones; s++)
                {
                    var h = tall / stones * (1.25f + R() * 0.3f);
                    var shrink = Mathf.Lerp(1, 0.55f, (float)s / stones);
                    var lean = new Vector2(R() - 0.5f, R() - 0.5f) * 0.8f;
                    kit.Blob(jade && s % 2 == 1 ? JadeRock : Rock2(), new Vector3(p.x - origin.x + lean.x, y + h * 0.5f, p.y - origin.z + lean.y),
                        new Vector3(radius * shrink * (0.85f + R() * 0.3f), h * 0.62f, radius * shrink * (0.85f + R() * 0.3f)), Seed(landmark.Id) + pillars.Count * 7 + s);
                    y += h * 0.82f;
                }

                // A dozen pillars to a mesh (each mesh its own collider).
                if (pillars.Count % 12 == 0)
                {
                    Place(root, $"Pillars {piece}", Save(kit, $"Stone Forest {piece}"), origin, 0);
                    kit = new MeshKit();
                    piece++;
                }
            }

            Place(root, $"Pillars {piece}", Save(kit, $"Stone Forest {piece}"), origin, 0);
            root.gameObject.AddComponent<GuWorld.Render.HitboxTag>().Kind = GuWorld.Render.HitboxKind.Building;
        }

        static Material TentCloth => Mat("Tent Cloth", 0x2e2a26, 0.04f, true);

        /// <summary>A demonic path camp: a palisade of sharpened logs round three dark tents, a fire pit, a rack, a black banner.</summary>
        static void BuildCamp(Transform root, Landmark landmark)
        {
            var origin = new Vector3(landmark.At.x, Ground(landmark.At), landmark.At.y);
            var random = new System.Random(Seed(landmark.Id));
            float R() => (float)random.NextDouble();
            var kit = new MeshKit();
            Vector3 Local(float x, float z, float above = 0)
            {
                var o = Quaternion.Euler(0, landmark.Yaw, 0) * new Vector3(x, 0, z);
                var p = landmark.At + new Vector2(o.x, o.z);

                return new Vector3(o.x, Ground(p) - origin.y + above, o.z);
            }

            // The palisade: a ring of logs, a gap at the front.
            const float ring = 17;

            for (var a = 0f; a < 360; a += 4.2f)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(a, 0)) < 14)
                {
                    continue;
                }

                var x = Mathf.Sin(a * Mathf.Deg2Rad) * ring;
                var z = Mathf.Cos(a * Mathf.Deg2Rad) * ring;
                var foot = Local(x, z, -0.8f);
                var tall = 3.2f + R() * 1.2f;
                kit.Cylinder(Logs, foot, foot + Vector3.up * tall, 0.22f, 0.2f, 6);
                kit.Cylinder(Logs, foot + Vector3.up * tall, foot + Vector3.up * (tall + 0.6f), 0.2f, 0, 6, false);
            }

            // Three tents round the fire.
            for (var i = 0; i < 3; i++)
            {
                var a = (i * 120 + 40) * Mathf.Deg2Rad;
                var at = Local(Mathf.Sin(a) * 9, Mathf.Cos(a) * 9);
                kit.Roof(TentCloth, at + Vector3.up * 0.2f, 4.2f, 3.4f, 2.6f, 0.2f, 0, landmark.Yaw + i * 120 + 40);
                kit.Box(Logs, at + Vector3.up * 1.4f, new Vector3(0.12f, 2.8f, 0.12f));
            }

            // The fire: a ring of stones, logs, embers that glow.
            var fire = Local(0, 0);

            for (var k = 0; k < 9; k++)
            {
                var a = k * Mathf.PI * 2 / 9;
                kit.Blob(Rubble, fire + new Vector3(Mathf.Cos(a) * 1.3f, 0.15f, Mathf.Sin(a) * 1.3f), new Vector3(0.35f, 0.25f, 0.3f), k);
            }

            kit.Cylinder(Logs, fire + new Vector3(-0.8f, 0.25f, -0.3f), fire + new Vector3(0.8f, 0.3f, 0.3f), 0.12f, 0.12f, 6);
            kit.Cylinder(Logs, fire + new Vector3(-0.3f, 0.25f, 0.8f), fire + new Vector3(0.3f, 0.3f, -0.8f), 0.12f, 0.12f, 6);
            kit.Blob(WarmLantern, fire + new Vector3(0, 0.3f, 0), new Vector3(0.55f, 0.3f, 0.55f), 3);

            // A weapon rack and a black banner on a pole by the gap.
            var rack = Local(5, 12);
            kit.Box(Logs, rack + new Vector3(0, 0.8f, 0), new Vector3(2.4f, 0.12f, 0.12f), landmark.Yaw);
            kit.Box(Logs, rack + new Vector3(0, 0.4f, 0), new Vector3(2.4f, 0.12f, 0.12f), landmark.Yaw);
            var pole = Local(-4, 15);
            kit.Cylinder(Logs, pole + Vector3.down * 0.5f, pole + Vector3.up * 6, 0.1f, 0.08f, 6);
            kit.Box(Mat("Black Banner", 0x121012, 0.05f, true), pole + new Vector3(0, 4.6f, 0), new Vector3(0.05f, 2.2f, 1.2f), landmark.Yaw + 90);

            Place(root, "Camp", Save(kit, "Demon Camp"), origin, 0);
            root.gameObject.AddComponent<GuWorld.Render.HitboxTag>().Kind = GuWorld.Render.HitboxKind.Building;
        }

        /// <summary>An old watchtower: a stone tower 15 m tall on a square footing, its top broken, a timber platform on it (climbable from outside).</summary>
        static void BuildTower(Transform root, Landmark landmark)
        {
            var origin = new Vector3(landmark.At.x, Ground(landmark.At), landmark.At.y);
            var (low, high) = GroundUnder(landmark.At, landmark.Yaw, 9, 9);
            var kit = new MeshKit();
            var random = new System.Random(Seed(landmark.Id));
            float R() => (float)random.NextDouble();
            var bottom = low - origin.y - 1.5f;
            var top = high - origin.y + 15;

            kit.Box(Stone, new Vector3(0, (bottom + high - origin.y + 1) / 2, 0), new Vector3(9, high - origin.y + 1 - bottom, 9), landmark.Yaw);
            kit.Box(Stone, new Vector3(0, (high - origin.y + 1 + top) / 2, 0), new Vector3(6.5f, top - (high - origin.y + 1), 6.5f), landmark.Yaw);
            // The broken crenellations: some merlons stand, some have fallen.
            var turn = Quaternion.Euler(0, landmark.Yaw, 0);

            for (var k = 0; k < 12; k++)
            {
                if (R() < 0.35f)
                {
                    continue;
                }

                var side = k / 3;
                var along = (k % 3 - 1) * 2.2f;
                var o = side switch
                {
                    0 => new Vector3(along, 0, 3.0f),
                    1 => new Vector3(3.0f, 0, along),
                    2 => new Vector3(along, 0, -3.0f),
                    _ => new Vector3(-3.0f, 0, along),
                };
                kit.Box(Stone, turn * o + Vector3.up * (top + 0.6f), new Vector3(1.1f, 1.2f + R() * 0.4f, 1.1f), landmark.Yaw);
            }

            // A platform of old planks, a slit window on each side, rubble fallen at the foot.
            kit.Box(Wood, new Vector3(0, top + 0.05f, 0), new Vector3(5.4f, 0.12f, 5.4f), landmark.Yaw);

            for (var k = 0; k < 4; k++)
            {
                kit.Box(Interior, turn * (Quaternion.Euler(0, k * 90, 0) * new Vector3(0, 0, 3.27f)) + Vector3.up * (top - 4), new Vector3(0.4f, 1.4f, 0.1f), landmark.Yaw + k * 90);
            }

            for (var k = 0; k < 8; k++)
            {
                var a = R() * Mathf.PI * 2;
                var r = 5.5f + R() * 3;
                kit.Blob(Rubble, new Vector3(Mathf.Cos(a) * r, low - origin.y + 0.2f, Mathf.Sin(a) * r), new Vector3(0.6f, 0.45f, 0.5f) * (0.6f + R()), k);
            }

            Place(root, "Tower", Save(kit, $"Watchtower {landmark.Id}"), origin, 0);
            root.gameObject.AddComponent<GuWorld.Render.HitboxTag>().Kind = GuWorld.Render.HitboxKind.Building;
        }

        /// <summary>
        /// A tunnel: a vaulted passage of dressed stone along its way (its floor flat across, rising evenly from mouth to
        /// mouth), its inside seen from within; over it a mound of rough rock 27 m wide that hugs the ground just under it
        /// (seen where the ground is cut away for the tunnel, and standing out of the hillside where the ridge is low), a
        /// face of the same rock round each opening and a rocky apron before it — together they cover the ragged cut in the
        /// mountain's tiles (TunnelMouth), so no gap shows anywhere; rock arches round each mouth; a lantern every 30 m and
        /// a warm light at each end.
        /// </summary>
        static void BuildTunnel(Transform root, Tunnel tunnel)
        {
            var go = new GameObject(tunnel.Name).transform;
            go.SetParent(root, false);
            var origin = new Vector3(tunnel.A.x, tunnel.FloorA, tunnel.A.y);
            var along = tunnel.Along;
            var across = new Vector2(-along.y, along.x);
            var w = tunnel.Half;
            var h = tunnel.Height;
            // The cross-section, from the floor's left edge round the vault to its right edge (walls straight to half way).
            var section = new List<Vector2> { new Vector2(-w, 0), new Vector2(-w, h * 0.45f) };

            for (var k = 1; k < 8; k++)
            {
                var a = Mathf.PI * k / 8;
                section.Add(new Vector2(-Mathf.Cos(a) * w, h * 0.45f + Mathf.Sin(a) * h * 0.55f));
            }

            section.Add(new Vector2(w, h * 0.45f));
            section.Add(new Vector2(w, 0));

            var inside = new MeshKit();
            var shell = new MeshKit();
            var mound = new MeshKit();
            var floor = Mat("Tunnel Floor", 0x3e3a34, 0.06f, false, StoneTexture(), 0.5f);
            var steps = Mathf.CeilToInt(tunnel.Length / 4);
            Vector3 At(float t, Vector2 s, float grow = 1)
            {
                var p = tunnel.A + along * (tunnel.Length * t) + across * s.x * grow;

                return new Vector3(p.x, tunnel.FloorAt(t) + s.y * grow, p.y) - origin;
            }

            for (var i = 0; i < steps; i++)
            {
                var t0 = (float)i / steps;
                var t1 = (float)(i + 1) / steps;
                var mid = Vector3.Lerp(At(t0, Vector2.zero), At(t1, Vector2.zero), 0.5f) + Vector3.up * h * 0.45f;

                // The floor.
                inside.Quad(floor, At(t0, new Vector2(-w, 0.02f)), At(t0, new Vector2(w, 0.02f)), At(t1, new Vector2(w, 0.02f)), At(t1, new Vector2(-w, 0.02f)), Vector3.up);

                for (var k = 0; k + 1 < section.Count; k++)
                {
                    var a0 = At(t0, section[k]);
                    var b0 = At(t0, section[k + 1]);
                    var a1 = At(t1, section[k]);
                    var b1 = At(t1, section[k + 1]);
                    var face = (a0 + b0 + a1 + b1) / 4;
                    // Inside: facing in, to the middle.
                    inside.Quad(Stone, a0, b0, b1, a1, mid - face);
                }

                // A lantern on the left wall every 30 m.
                if (i % 8 == 4)
                {
                    inside.Blob(WarmLantern, At((t0 + t1) / 2, new Vector2(-w + 0.25f, h * 0.4f)), new Vector3(0.18f, 0.25f, 0.18f), i);
                }
            }

            // Rock arches round both mouths: boulders piled over and beside each opening.
            foreach (var (end, t, outward) in new[] { (tunnel.A, 0f, -along), (tunnel.B, 1f, along) })
            {
                for (var k = 0; k < 9; k++)
                {
                    var a = Mathf.PI * k / 8;
                    var s = new Vector2(-Mathf.Cos(a) * (w + 1.6f), h * 0.45f + Mathf.Sin(a) * (h * 0.55f + 1.4f));
                    var p = At(t, s) + new Vector3(outward.x, 0, outward.y) * 0.6f;
                    shell.Blob(Rock2(), p, new Vector3(2.0f, 1.6f, 2.4f), k + (t > 0 ? 20 : 0));
                }
            }

            BuildMound(mound, tunnel, section, floor);
            Place(go, "Inside", Save(inside, $"Tunnel {tunnel.Id}"), origin, 0);
            Place(go, "Shell", Save(shell, $"Tunnel {tunnel.Id} Shell"), origin, 0, false);
            Place(go, "Mound", Save(mound, $"Tunnel {tunnel.Id} Mound"), origin, 0);

            // A warm light just inside each mouth.
            foreach (var t in new[] { 0.06f, 0.94f })
            {
                var light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(go, false);
                light.transform.position = origin + At(t, new Vector2(0, h * 0.7f));
                light.type = LightType.Point;
                light.color = new Color(1f, 0.72f, 0.45f);
                light.range = 16;
                light.intensity = 1.6f;
                light.shadows = LightShadows.None;
            }

            go.gameObject.AddComponent<GuWorld.Render.HitboxTag>().Kind = GuWorld.Render.HitboxKind.Building;
        }

        /// <summary>
        /// The rock over a tunnel and round its openings (BuildTunnel): the mound along it, a face round each opening (the
        /// vault's outline to the mound's), an apron before each mouth — paved across the way, rock beside it, easing from
        /// the floor down to the ground 9 m out. Solid: where the ground is cut away one walks on them.
        /// </summary>
        static void BuildMound(MeshKit kit, Tunnel tunnel, List<Vector2> section, Material paving)
        {
            var origin = new Vector3(tunnel.A.x, tunnel.FloorA, tunnel.A.y);
            var along = tunnel.Along;
            var across = new Vector2(-along.y, along.x);
            var w = tunnel.Half;
            var h = tunnel.Height;
            var rock = Rock2();
            // The mound's cross-section, point for point with the vault's (section): across, and over the floor. Its feet
            // (no height) sink a metre under the ground; the rest is never lower than just under the ground, so the tile's
            // ground hides it where it stands and it fills the cut where it does not.
            var shape = new[]
            {
                new Vector2(-(w + 9), float.NaN), new Vector2(-(w + 5.5f), h * 0.45f), new Vector2(-(w + 2.5f), h * 0.85f),
                new Vector2(-w * 0.75f, h + 1.2f), new Vector2(-w * 0.45f, h + 1.7f), new Vector2(0, h + 2),
                new Vector2(w * 0.45f, h + 1.7f), new Vector2(w * 0.75f, h + 1.2f), new Vector2(w + 2.5f, h * 0.85f),
                new Vector2(w + 5.5f, h * 0.45f), new Vector2(w + 9, float.NaN),
            };
            Vector2 Plan(float d, float s) => tunnel.A + along * d + across * s;
            float FloorAt(float d) => tunnel.FloorAt(Mathf.Clamp01(d / tunnel.Length));
            Vector3 Local(Vector2 p, float y) => new Vector3(p.x, y, p.y) - origin;
            Vector3[] Slice(float d)
            {
                var points = new Vector3[shape.Length];

                for (var k = 0; k < shape.Length; k++)
                {
                    var p = Plan(d, shape[k].x);
                    var ground = Ground(p);
                    points[k] = Local(p, float.IsNaN(shape[k].y) ? ground - 1.2f : Mathf.Max(FloorAt(d) + shape[k].y, ground - 0.25f));
                }

                return points;
            }

            // The mound, a slice every 2 m.
            var steps = Mathf.CeilToInt(tunnel.Length / 2);
            var last = Slice(0);
            var first = last;

            for (var i = 1; i <= steps; i++)
            {
                var d = tunnel.Length * i / steps;
                var next = Slice(d);
                // Facing away from a point deep under the way.
                var under = Local(Plan(d - tunnel.Length / steps / 2, 0), FloorAt(d) - 10);

                for (var k = 0; k + 1 < shape.Length; k++)
                {
                    kit.Quad(rock, last[k], last[k + 1], next[k + 1], next[k], (last[k] + last[k + 1] + next[k] + next[k + 1]) / 4 - under);
                }

                last = next;
            }

            // Each opening: its face and its apron.
            foreach (var (d, outline, outward) in new[] { (0f, first, -along), (tunnel.Length, last, along) })
            {
                var floor = FloorAt(d);
                var end = Plan(d, 0);

                for (var k = 0; k + 1 < section.Count; k++)
                {
                    var a = Local(Plan(d, section[k].x), floor + section[k].y);
                    var b = Local(Plan(d, section[k + 1].x), floor + section[k + 1].y);
                    kit.Quad(rock, a, outline[k], outline[k + 1], b, new Vector3(outward.x, 0, outward.y));
                }

                const float apron = 9;
                const int rows = 6;
                var columns = Mathf.CeilToInt(2 * (w + 9) / 1.5f);
                Vector3 Apron(float o, float s)
                {
                    var p = end + outward * o + across * s;
                    var ease = Mathf.Max(SmoothStep(w + 0.5f, w + 3, Mathf.Abs(s)), SmoothStep(0, apron, o));

                    return Local(p, Mathf.Lerp(floor + 0.02f, Ground(p) - 0.15f, ease));
                }

                for (var r = 0; r < rows; r++)
                {
                    var o0 = apron * r / rows;
                    var o1 = apron * (r + 1) / rows;

                    for (var c = 0; c < columns; c++)
                    {
                        var s0 = Mathf.Lerp(-(w + 9), w + 9, (float)c / columns);
                        var s1 = Mathf.Lerp(-(w + 9), w + 9, (float)(c + 1) / columns);
                        kit.Quad(Mathf.Abs(s0 + s1) / 2 < w + 1 ? paving : rock, Apron(o0, s0), Apron(o0, s1), Apron(o1, s1), Apron(o1, s0), Vector3.up);
                    }
                }
            }
        }
    }
}
