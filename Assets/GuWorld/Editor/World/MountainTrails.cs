using System;
using System.Collections.Generic;
using GuWorld.World;
using UnityEditor;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// The mountain's trails, found the way feet find them: the cheapest way over the
    /// ground (A* on a 16 m grid), where every step costs its length and more the
    /// steeper it climbs, so the trails wind and zigzag up the slopes instead of
    /// running straight; a stream is crossed rarely (a plank bridge stands there), and
    /// no trail runs through a village it does not lead to. Trails already laid are
    /// cheap to walk, so later ones join them into a net. Then smoothed, and laid on
    /// the ground as worn earth of uneven width — grass growing into it, stones
    /// showing — with steps of flat stones where it climbs steeply.
    /// </summary>
    public static partial class QingMaoBuilder
    {
        const float TrailGrid = 16;
        static readonly int TrailCells = Mathf.RoundToInt(QingMao.Size / TrailGrid);

        public class Trail
        {
            public string Name;
            /// <summary>The trail's line on the ground (X, Z), every two metres or so.</summary>
            public List<Vector2> Points = new List<Vector2>();
            public float Half = 1;
        }

        static readonly List<Trail> trails = new List<Trail>();
        static readonly Dictionary<Vector2Int, List<(Vector2 a, Vector2 b)>> trailCells = new Dictionary<Vector2Int, List<(Vector2, Vector2)>>();
        const float TrailCell = 64;
        static float[] trailHeights;
        static bool[] trailUsed;
        /// <summary>The rock (ledges, cliff bands) on the trail grid, spread 2 cells out: a trail keeps off a cliff's edge too.</summary>
        static float[] trailRock;

        /// <summary>Whether a trail runs within `reach` of a point.</summary>
        static bool TrailNear(Vector2 p, float reach)
        {
            var c = new Vector2Int(Mathf.FloorToInt(p.x / TrailCell), Mathf.FloorToInt(p.y / TrailCell));
            var r = Mathf.CeilToInt(reach / TrailCell);

            for (var i = -r; i <= r; i++)
            {
                for (var k = -r; k <= r; k++)
                {
                    if (!trailCells.TryGetValue(new Vector2Int(c.x + i, c.y + k), out var list))
                    {
                        continue;
                    }

                    foreach (var (a, b) in list)
                    {
                        if (QingMao.ToSegment(p, a, b) < reach)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        static void IndexTrail(Trail trail)
        {
            for (var i = 0; i + 1 < trail.Points.Count; i++)
            {
                var a = trail.Points[i];
                var b = trail.Points[i + 1];
                var mid = (a + b) / 2;
                var cell = new Vector2Int(Mathf.FloorToInt(mid.x / TrailCell), Mathf.FloorToInt(mid.y / TrailCell));

                if (!trailCells.TryGetValue(cell, out var list))
                {
                    trailCells[cell] = list = new List<(Vector2, Vector2)>();
                }

                list.Add((a, b));
            }
        }

        static int Index(int i, int k) => k * TrailCells + i;

        static Vector2 CellCentre(int i, int k) => new Vector2(-QingMao.Half + (i + 0.5f) * TrailGrid, -QingMao.Half + (k + 0.5f) * TrailGrid);

        /// <summary>The ground's heights on the trails' grid (once).</summary>
        static void TrailGround(Func<float, float, float> natural)
        {
            if (trailHeights != null)
            {
                return;
            }

            trailHeights = new float[TrailCells * TrailCells];
            trailUsed = new bool[TrailCells * TrailCells];
            var rock = new float[TrailCells * TrailCells];

            for (var k = 0; k < TrailCells; k++)
            {
                if (k % 100 == 0)
                {
                    EditorUtility.DisplayProgressBar("Qing Mao", "Trails: the lie of the land…", (float)k / TrailCells);
                }

                for (var i = 0; i < TrailCells; i++)
                {
                    var p = CellCentre(i, k);
                    trailHeights[Index(i, k)] = natural(p.x, p.y);
                    rock[Index(i, k)] = MountainShape.RockAt(p.x, p.y);
                }
            }

            // Spread two cells (32 m) round: the edge of a cliff band is as bad as the band.
            trailRock = new float[rock.Length];

            for (var k = 0; k < TrailCells; k++)
            {
                for (var i = 0; i < TrailCells; i++)
                {
                    var most = 0f;

                    for (var dk = -2; dk <= 2; dk++)
                    {
                        for (var di = -2; di <= 2; di++)
                        {
                            var x = Mathf.Clamp(i + di, 0, TrailCells - 1);
                            var z = Mathf.Clamp(k + dk, 0, TrailCells - 1);
                            most = Mathf.Max(most, rock[Index(x, z)]);
                        }
                    }

                    trailRock[Index(i, k)] = most;
                }
            }
        }

        /// <summary>A binary heap of grid cells by cost, for A*.</summary>
        class Heap
        {
            readonly List<(float cost, int cell)> items = new List<(float, int)>();

            public int Count => items.Count;

            public void Push(float cost, int cell)
            {
                items.Add((cost, cell));
                var i = items.Count - 1;

                while (i > 0)
                {
                    var parent = (i - 1) / 2;

                    if (items[parent].cost <= items[i].cost)
                    {
                        break;
                    }

                    (items[parent], items[i]) = (items[i], items[parent]);
                    i = parent;
                }
            }

            public int Pop()
            {
                var top = items[0].cell;
                var last = items[items.Count - 1];
                items.RemoveAt(items.Count - 1);

                if (items.Count > 0)
                {
                    items[0] = last;
                    var i = 0;

                    while (true)
                    {
                        var l = i * 2 + 1;
                        var r = l + 1;
                        var smallest = i;

                        if (l < items.Count && items[l].cost < items[smallest].cost) smallest = l;
                        if (r < items.Count && items[r].cost < items[smallest].cost) smallest = r;

                        if (smallest == i)
                        {
                            break;
                        }

                        (items[smallest], items[i]) = (items[i], items[smallest]);
                        i = smallest;
                    }
                }

                return top;
            }
        }

        /// <summary>The cheapest way on foot from one point to another (null if there is none), avoiding the settlements not at either end.</summary>
        static List<Vector2> FindWay(Vector2 from, Vector2 to, string[] allowed)
        {
            int Cell(float v) => Mathf.Clamp(Mathf.FloorToInt((v + QingMao.Half) / TrailGrid), 0, TrailCells - 1);
            var start = Index(Cell(from.x), Cell(from.y));
            var goal = Index(Cell(to.x), Cell(to.y));
            var cost = new Dictionary<int, float> { [start] = 0 };
            var came = new Dictionary<int, int>();
            var open = new Heap();
            var closed = new HashSet<int>();
            open.Push(0, start);
            var steps = new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1), (2, 1), (1, 2), (-2, 1), (-1, 2), (2, -1), (1, -2), (-2, -1), (-1, -2) };
            // Every settlement is gone round, its own too (a trail ends outside its gate, it does not cut through the
            // houses and over the wall to get there); only near its two ends may a trail come close.
            var avoid = new List<Settlement>(QingMao.Settlements);

            var goalAt = CellCentre(goal % TrailCells, goal / TrailCells);

            while (open.Count > 0)
            {
                var current = open.Pop();

                if (current == goal)
                {
                    break;
                }

                if (!closed.Add(current))
                {
                    continue;
                }

                var ci = current % TrailCells;
                var ck = current / TrailCells;
                var h0 = trailHeights[current];

                foreach (var (di, dk) in steps)
                {
                    var ni = ci + di;
                    var nk = ck + dk;

                    if (ni < 1 || nk < 1 || ni >= TrailCells - 1 || nk >= TrailCells - 1)
                    {
                        continue;
                    }

                    var next = Index(ni, nk);

                    if (closed.Contains(next))
                    {
                        continue;
                    }

                    var length = TrailGrid * Mathf.Sqrt(di * di + dk * dk);
                    var rise = Mathf.Abs(trailHeights[next] - h0) / length;

                    if (rise > 0.62f)
                    {
                        continue;
                    }

                    // Gentle ground is cheap; the steeper, the dearer — so the trail winds.
                    var step = length * (1 + 14 * rise * rise + (rise > 0.36f ? 40 * (rise - 0.36f) : 0));
                    var at = CellCentre(ni, nk);

                    if (MountainShape.NearStream(at, 24, out var toStream, out _, out var half, out _) && toStream < half + 6)
                    {
                        step += 260;
                    }

                    if (Vector2.Distance(at, QingMao.Pool) < QingMao.PoolRadius + 15)
                    {
                        continue;
                    }

                    // Ledges and cliff bands (finer than this grid sees) and their edges: gone round (the route walk fell off them).
                    var rock = trailRock[next];

                    if (rock > 0.01f)
                    {
                        step += length * 80 * rock;
                    }

                    var nearEnd = Vector2.Distance(at, from) < 45 || Vector2.Distance(at, to) < 45;

                    foreach (var place in avoid)
                    {
                        var own = Array.IndexOf(allowed, place.Id) >= 0;
                        var keep = own ? place.Radius * 1.3f + 6 : place.Radius * 1.35f + 30;

                        if (!(own && nearEnd) && Vector2.Distance(at, place.Centre) < keep)
                        {
                            step += 5000;
                        }
                    }

                    // The ground is not even where the map is smooth (stones, roots, thickets): a way across it bends.
                    step *= 0.8f + 0.5f * Mathf.PerlinNoise(at.x * 0.006f + 31, at.y * 0.006f + 7);

                    // The ways already worn are easier to walk: trails join into a net.
                    if (trailUsed[next])
                    {
                        step *= 0.55f;
                    }

                    var total = cost[current] + step;

                    if (!cost.TryGetValue(next, out var known) || total < known)
                    {
                        cost[next] = total;
                        came[next] = current;
                        open.Push(total + Vector2.Distance(at, goalAt) * 1.05f, next);
                    }
                }
            }

            if (!came.ContainsKey(goal))
            {
                return null;
            }

            var way = new List<Vector2>();

            for (var cell = goal; ; cell = came[cell])
            {
                trailUsed[cell] = true;
                way.Insert(0, CellCentre(cell % TrailCells, cell / TrailCells));

                if (cell == start)
                {
                    break;
                }
            }

            way[0] = from;
            way[way.Count - 1] = to;

            return way;
        }

        /// <summary>Rounds a grid way into a trail: corners cut (Chaikin), then a point every two metres.</summary>
        static List<Vector2> Smooth(List<Vector2> way)
        {
            var points = way;

            for (var pass = 0; pass < 3; pass++)
            {
                var next = new List<Vector2> { points[0] };

                for (var i = 0; i + 1 < points.Count; i++)
                {
                    next.Add(Vector2.Lerp(points[i], points[i + 1], 0.25f));
                    next.Add(Vector2.Lerp(points[i], points[i + 1], 0.75f));
                }

                next.Add(points[points.Count - 1]);
                points = next;
            }

            var even = new List<Vector2> { points[0] };
            var carried = 0f;

            for (var i = 0; i + 1 < points.Count; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var length = Vector2.Distance(a, b);
                var s = 2 - carried;

                while (s < length)
                {
                    // A little wander, as feet go round stones and roots.
                    var at = Vector2.Lerp(a, b, s / length);
                    var across = new Vector2(-(b - a).y, (b - a).x).normalized;
                    even.Add(at + across * (Mathf.PerlinNoise(at.x * 0.08f, at.y * 0.08f) - 0.5f) * 0.9f);
                    s += 2;
                }

                carried = length - (s - 2);
            }

            even.Add(points[points.Count - 1]);

            return even;
        }

        /// <summary>Lays a trail from one point to another (avoiding the other settlements), and remembers it.</summary>
        static Trail LayTrail(string name, Vector2 from, Vector2 to, float half, params string[] ends)
        {
            var way = FindWay(from, to, ends);

            if (way == null || way.Count < 2)
            {
                Debug.LogWarning($"GU World: no way for the trail \"{name}\"");

                return null;
            }

            var trail = new Trail { Name = name, Points = Smooth(way), Half = half };
            trails.Add(trail);
            IndexTrail(trail);

            return trail;
        }

        // ── On the ground ─────────────────────────────────────────────

        static Texture2D trailTexture;

        /// <summary>Worn earth with ragged, grassy edges and bare patches; across the texture is across the trail.</summary>
        static Texture2D TrailTexture() => trailTexture != null ? trailTexture : trailTexture = Texture("Trail", 256, (u, v) =>
        {
            var n = TileFbm(u, v, 6, 141);
            var pebble = TileNoise(u * 80, v * 80, 80, 142);
            var edge = Mathf.Abs(u - 0.5f) * 2;
            var ragged = TileFbm(u * 2, v, 8, 143);
            // Fully worn in the middle, giving way to grass at uneven edges, with tufts here and there in the middle too.
            var worn = 1 - SmoothStep(0.45f, 0.95f, edge + (ragged - 0.5f) * 0.7f);
            var tuft = TileNoise(u * 24, v * 24, 24, 144) > 0.86f ? 0.6f : 0;
            var a = Mathf.Clamp01(worn - tuft);
            var c = Color.Lerp(Shade(0x4e3e2c, n), Shade(0x75695a, pebble), pebble > 0.72f ? 0.65f : 0);
            return new Color(c.r, c.g, c.b, a);
        }, false, true);

        /// <summary>
        /// The trails on the ground: a ribbon of worn earth hugging the terrain (in pieces of
        /// a few hundred metres), steps of flat stones where it climbs steeply, a plank
        /// bridge where it crosses a stream.
        /// </summary>
        static void BuildTrails(Func<Vector3, float> ground)
        {
            var root = new GameObject("Trails").transform;
            var earth = Cutout("Trail", 0xffffff, TrailTexture(), 1, 0.4f, false);
            earth.SetFloat("_Smoothness", 0);
            var count = 0;

            foreach (var trail in trails)
            {
                var points = trail.Points;
                var piece = new MeshKit();
                var pieceStart = 0;
                var steps = new MeshKit();
                var lastStep = float.NegativeInfinity;

                for (var i = 0; i + 1 < points.Count; i++)
                {
                    var a = points[i];
                    var b = points[i + 1];

                    // A test build of part of the mountain lays only the trails' stretches on its ground.
                    if (!InArea(a) && !InArea(b))
                    {
                        if (!piece.IsEmpty)
                        {
                            Emit(root, $"{trail.Name} {count++}", piece);
                            piece = new MeshKit();
                        }

                        pieceStart = i;
                        continue;
                    }

                    var along = (b - a).normalized;
                    var across = new Vector2(-along.y, along.x);
                    var wa = trail.Half * (0.8f + 0.5f * Mathf.PerlinNoise(a.x * 0.05f, a.y * 0.05f));
                    var wb = trail.Half * (0.8f + 0.5f * Mathf.PerlinNoise(b.x * 0.05f, b.y * 0.05f));
                    Vector3 P(Vector2 p, float side, float width)
                    {
                        var q = p + across * side * width;
                        return new Vector3(q.x, ground(new Vector3(q.x, 0, q.y)) + 0.05f, q.y);
                    }

                    // Across the trail: three strips, so it bends with the ground.
                    var va0 = i * 0.5f;
                    var vb0 = (i + 1) * 0.5f;

                    for (var s = 0; s < 3; s++)
                    {
                        var s0 = -1 + s * 2 / 3f;
                        var s1 = -1 + (s + 1) * 2 / 3f;
                        piece.QuadUV(earth, P(a, s0, wa), P(a, s1, wa), P(b, s1, wb), P(b, s0, wb),
                            new Vector2((s0 + 1) / 2, va0), new Vector2((s1 + 1) / 2, va0), new Vector2((s1 + 1) / 2, vb0), new Vector2((s0 + 1) / 2, vb0));
                    }

                    // Steps where it climbs steeply: a flat stone across the trail every half metre of height.
                    var ha = ground(new Vector3(a.x, 0, a.y));
                    var hb = ground(new Vector3(b.x, 0, b.y));
                    var slope = Mathf.Abs(hb - ha) / Mathf.Max(0.1f, Vector2.Distance(a, b));

                    if (slope > 0.28f && Mathf.Abs(Mathf.Min(ha, hb) - lastStep) > 0.45f)
                    {
                        lastStep = Mathf.Min(ha, hb);
                        var mid = (a + b) / 2;
                        var yaw = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
                        steps.Box(Rubble, new Vector3(mid.x, (ha + hb) / 2 - 0.05f, mid.y), new Vector3(wa * 1.9f, 0.22f, 0.55f), yaw);
                    }

                    // A bridge over a stream.
                    if (MountainShape.NearStream(a, 12, out var toStream, out var bed, out var half, out _) && toStream < half + 1 && i % 3 == 0)
                    {
                        Bridge(root, a, along, half * 2 + 5, ground);
                    }

                    // A piece every 150 points (300 m), for culling.
                    if (i - pieceStart >= 150 || i + 2 == points.Count)
                    {
                        Emit(root, $"{trail.Name} {count++}", piece);
                        piece = new MeshKit();
                        pieceStart = i;
                    }
                }

                Emit(root, $"{trail.Name} steps", steps, true);
            }
        }

        static readonly HashSet<Vector2Int> bridged = new HashSet<Vector2Int>();

        /// <summary>A plank bridge `length` long along `along`, centred on `at`, its ends on the banks.</summary>
        static void Bridge(Transform root, Vector2 at, Vector2 along, float length, Func<Vector3, float> ground)
        {
            var key = new Vector2Int(Mathf.RoundToInt(at.x / 30), Mathf.RoundToInt(at.y / 30));

            if (!bridged.Add(key))
            {
                return;
            }

            var a = at - along * length / 2;
            var b = at + along * length / 2;
            var ha = ground(new Vector3(a.x, 0, a.y));
            var hb = ground(new Vector3(b.x, 0, b.y));
            var deck = Mathf.Max(ha, hb) + 0.25f;
            var yaw = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
            var kit = new MeshKit();
            kit.Box(Wood, new Vector3(0, -0.06f, 0), new Vector3(2.2f, 0.12f, length));

            for (var z = -length / 2; z < length / 2; z += 0.5f)
            {
                kit.Box(DarkWood, new Vector3(0, 0.01f, z + 0.25f), new Vector3(2.3f, 0.04f, 0.42f));
            }

            foreach (var x in new[] { -1.1f, 1.1f })
            {
                kit.Box(DarkWood, new Vector3(x, 0.55f, 0), new Vector3(0.08f, 0.08f, length));

                for (var z = -length / 2; z <= length / 2; z += 1.6f)
                {
                    kit.Box(DarkWood, new Vector3(x, 0.1f, z), new Vector3(0.1f, 1.1f, 0.1f));
                }
            }

            foreach (var z in new[] { -length / 2 + 0.5f, length / 2 - 0.5f })
            {
                kit.Box(Stone, new Vector3(0, -1.2f, z), new Vector3(2.6f, 2.4f, 1.2f));
            }

            var made = kit.Build("Bridge");
            var built = (mesh: Keep(made.mesh, $"{Generated}/Meshes/Bridge {key.x} {key.y}.asset"), made.materials);
            var go = new GameObject("Bridge");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(new Vector3(at.x, deck, at.y), Quaternion.Euler(0, yaw, 0));
            go.AddComponent<MeshFilter>().sharedMesh = built.mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = built.materials;
            var deckCollider = go.AddComponent<BoxCollider>();
            deckCollider.center = new Vector3(0, -0.05f, 0);
            deckCollider.size = new Vector3(2.3f, 0.2f, length);
            go.AddComponent<GuWorld.Render.HitboxTag>().Kind = GuWorld.Render.HitboxKind.Building;
        }

        static void Emit(Transform root, string name, MeshKit kit, bool solid = false)
        {
            if (kit.IsEmpty)
            {
                return;
            }

            var (made, used) = kit.Build(name);
            var mesh = Keep(made, $"{Generated}/Meshes/Trail {name}.asset");
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = used;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (solid)
            {
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                go.AddComponent<GuWorld.Render.HitboxTag>().Kind = GuWorld.Render.HitboxKind.Prop;
            }
        }
    }
}
