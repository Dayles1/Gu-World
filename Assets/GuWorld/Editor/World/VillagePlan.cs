using System;
using System.Collections.Generic;
using System.Linq;
using GuWorld.World;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>What a building is for: its kind decides its shape, its size and what lies about it.</summary>
    public enum BuildingKind
    {
        Family,
        Poor,
        Shop,
        Tavern,
        Teahouse,
        Inn,
        Smithy,
        Herbalist,
        Granary,
        GuFarm,
        Shed,
        Gambling,
        Pavilion,
        Ancestral,
        Academy,
        Hostel,
        Hall,
        Tower,
    }

    /// <summary>
    /// A village laid out on the mountain's side as such villages are: on terraces
    /// cut along the slope, one above another, held up by dry-stone walls — the lanes
    /// running along each terrace at its foot, stairs climbing from terrace to
    /// terrace, a winding main way from the lower gate up to the one open square in
    /// the middle, where the clan's pavilion stands above the rest. Houses crowd the
    /// lanes shoulder to shoulder, set a little askew, of every size; the poorer ones
    /// thicker at the edges, where the terraces give way to banks; some stand over
    /// the terrace below on stilts. Gardens, sheds and trees fill the corners left.
    ///
    /// The plan is worked out once from the mountain's ground (MountainShape) and is
    /// the village's fine terrain (IGroundPlan) and the list of what to build there
    /// (VillageBuild): the walls, stairs, houses, props, gates and the walkways
    /// people go about on.
    /// </summary>
    public partial class VillagePlan : IGroundPlan
    {
        public class Street
        {
            public string Name;
            public List<Vector2> Points = new List<Vector2>();
            public float Half;
            /// <summary>A stair (climbing between terraces) or a lane along a terrace.</summary>
            public bool Stair;
            public bool Main;
        }

        /// <summary>A level pad cut into the terraces: the square, a great building's yard.</summary>
        public class Pad
        {
            public string Name;
            public Vector2 Centre;
            public Vector2 Half;
            public float Yaw;
            public float Level;
            public bool Paved;
        }

        /// <summary>A retaining wall along a terrace's edge: its line, the ground below and above it.</summary>
        public class Wall
        {
            public List<Vector2> Points = new List<Vector2>();
            public List<float> Low = new List<float>();
            public List<float> High = new List<float>();
        }

        public class Building
        {
            public BuildingKind Kind;
            public Vector2 Centre;
            /// <summary>Its size along its own X (width, across the front) and Z (depth).</summary>
            public Vector2 Size;
            /// <summary>Its turn (degrees, Unity's); its front faces its local +Z.</summary>
            public float Yaw;
            /// <summary>The height of its floor, and how far the ground falls away under its front and back (stilts), m.</summary>
            public float Floor;
            public float DropFront;
            public float DropBack;
            public int Seed;
            public string Name;
            public bool Lit;
        }

        public class Prop
        {
            public string Kind;
            public Vector3 At;
            public float Yaw;
            public float Scale = 1;
            public int Seed;
        }

        public class Gate
        {
            public Vector2 At;
            /// <summary>Out of the village, unit length.</summary>
            public Vector2 Out;
            public string Name;
        }

        public readonly Settlement Place;
        public Rect Area { get; }
        public int Resolution { get; }
        public readonly Vector2 Centre;
        /// <summary>Downhill and along the slope (to the right of downhill), unit length.</summary>
        public readonly Vector2 Down;
        public readonly Vector2 Along;
        public readonly float Step;
        public float Level0;
        public readonly float RadiusAlong;
        public readonly float RadiusUp;

        public readonly List<Vector2> Boundary = new List<Vector2>();
        public readonly List<Street> Streets = new List<Street>();
        public readonly List<Pad> Pads = new List<Pad>();
        public Pad Square;
        public readonly List<Wall> Walls = new List<Wall>();
        public readonly List<Building> Buildings = new List<Building>();
        public readonly List<Prop> Props = new List<Prop>();
        public readonly List<Gate> Gates = new List<Gate>();
        public readonly List<(string name, Vector2 at, float yaw)> Places = new List<(string, Vector2, float)>();
        public readonly List<(Vector2 centre, Vector2 half, float yaw)> Gardens = new List<(Vector2, Vector2, float)>();

        readonly Func<float, float, float> natural;
        readonly System.Random random;
        readonly float spacing;

        // The smoothed ground, on a coarse grid over the tile.
        const float SmoothCell = 4;
        int smoothCount;
        float[] smooth;

        // Rasters at the heightmap's spacing: what each spot is, how near a stair, which pad.
        public const byte KWild = 0, KGround = 1, KLane = 2, KStair = 3, KPaved = 4, KFloor = 5, KGarden = 6, KYard = 7;
        byte[] kind;
        float[] stairNear;
        short[] padOf;
        bool[] taken;

        float R() => (float)random.NextDouble();

        public VillagePlan(Settlement place, Func<float, float, float> natural, int resolution, int seed)
        {
            Place = place;
            this.natural = natural;
            random = new System.Random(seed);
            Area = QingMaoBuilder.FineArea(place.Centre, place.TileSize);
            Resolution = resolution;
            spacing = Area.width / (resolution - 1);
            Centre = place.Centre;
            Down = MountainShape.DownhillAt(place.Centre);
            Along = new Vector2(-Down.y, Down.x);
            Step = place.Clan == Clan.Bai ? 2.4f : place.Clan == Clan.Xiong ? 2.6f : 3.1f;
            // Gu Yue runs down its slope (its lights "a band down the mountainside", ch. 1); the others lie along theirs.
            RadiusAlong = place.Radius * (place.Clan == Clan.GuYue ? 1.0f : 1.25f);
            RadiusUp = place.Radius * (place.Clan == Clan.GuYue ? 1.2f : 0.9f);

            void Phase(string what) => BuildLog.Mark($"plan of {place.Id}: {what}");
            Smooth();
            Level0 = SmoothAt(Centre);
            MakeBoundary();
            Rasters();
            Phase("rasters");
            ChooseSquareAndPads();
            Phase("square and pads");
            ContourStreets();
            Phase("contour streets");
            StairStreets();
            Phase("stairs");
            StampStreets();
            Phase("streets stamped");
            FindGates();
            RetainingWalls();
            Phase("walls");
            PlaceSpecial();
            PlaceHouses();
            Phase("houses");
            Gardens_();
            Scatter();
            Phase("props");
            Debug.Log($"GU World: {place.Name}: {Streets.Count} streets, {Walls.Count} walls, {Buildings.Count} buildings, {Props.Count} things, {Gates.Count} gates");
        }

        // ── The ground ────────────────────────────────────────────────

        void Smooth()
        {
            smoothCount = Mathf.CeilToInt(Area.width / SmoothCell) + 1;
            var raw = new float[smoothCount * smoothCount];

            for (var j = 0; j < smoothCount; j++)
            {
                for (var i = 0; i < smoothCount; i++)
                {
                    raw[j * smoothCount + i] = natural(Area.x + i * SmoothCell, Area.y + j * SmoothCell);
                }
            }

            // Blurred twice: the land's lie without its bumps.
            smooth = raw;

            for (var pass = 0; pass < 3; pass++)
            {
                var next = new float[smooth.Length];

                for (var j = 0; j < smoothCount; j++)
                {
                    for (var i = 0; i < smoothCount; i++)
                    {
                        float sum = 0;
                        var count = 0;

                        for (var dj = -2; dj <= 2; dj++)
                        {
                            for (var di = -2; di <= 2; di++)
                            {
                                var x = Mathf.Clamp(i + di, 0, smoothCount - 1);
                                var y = Mathf.Clamp(j + dj, 0, smoothCount - 1);
                                sum += smooth[y * smoothCount + x];
                                count++;
                            }
                        }

                        next[j * smoothCount + i] = sum / count;
                    }
                }

                smooth = next;
            }
        }

        public float SmoothAt(Vector2 p)
        {
            var gx = Mathf.Clamp((p.x - Area.x) / SmoothCell, 0, smoothCount - 1.001f);
            var gz = Mathf.Clamp((p.y - Area.y) / SmoothCell, 0, smoothCount - 1.001f);
            var x0 = (int)gx;
            var z0 = (int)gz;
            var fx = gx - x0;
            var fz = gz - z0;
            float H(int i, int k) => smooth[k * smoothCount + i];

            return Mathf.Lerp(Mathf.Lerp(H(x0, z0), H(x0 + 1, z0), fx), Mathf.Lerp(H(x0, z0 + 1), H(x0 + 1, z0 + 1), fx), fz);
        }

        /// <summary>The ground's lie with a wander in it: where the terraces' edges run (they follow it).</summary>
        public float Warp(Vector2 p) =>
            SmoothAt(p) + MountainShape.Fbm(p.x * 0.011f + 17, p.y * 0.011f + 3, 3) * 1.5f + MountainShape.Fbm(p.x * 0.0045f + 5, p.y * 0.0045f + 29, 2) * 2.6f;

        /// <summary>How soft a terrace's rise is here: a stone wall (small) or a grassy bank (large), as a share of its width.</summary>
        float Softness(Vector2 p)
        {
            var inside = InsideDistance(p);
            var bank = Mathf.Max(0, MountainShape.Fbm(p.x * 0.009f + 61, p.y * 0.009f + 13, 2)) * 0.9f;

            return Mathf.Clamp(0.07f + bank + (1 - Smooth01(0, 40, inside)) * 0.35f, 0.07f, 0.6f);
        }

        static float Smooth01(float min, float max, float x)
        {
            if (x <= min) return 0;
            if (x >= max) return 1;
            x = (x - min) / (max - min);

            return x * x * (3 - 2 * x);
        }

        /// <summary>The terraced height for a warped height `w`: level steps, each rising over the uphill `e` share of its band.</summary>
        float Terraced(float w, float e)
        {
            var u = (w - Level0) / Step;
            var k = Mathf.Floor(u);
            var f = u - k;

            return Level0 + Step * (k + Smooth01(1 - e, 1, f));
        }

        /// <summary>Which terrace a point is on (its level's number).</summary>
        public int TerraceOf(Vector2 p) => Mathf.FloorToInt((Warp(p) - Level0) / Step);

        /// <summary>The level a terrace's flat lies at.</summary>
        public float LevelOf(int terrace) => Level0 + Step * terrace;

        public float Height(float x, float z, float naturalHeight)
        {
            var p = new Vector2(x, z);
            var inside = InsideDistance(p);

            if (inside < -45)
            {
                return naturalHeight;
            }

            var w = Warp(p);
            var t = Terraced(w, Softness(p));
            var cell = CellOf(p);

            if (cell >= 0)
            {
                // A stair: the rise spread over half the band, so it can be climbed.
                var stair = stairNear[cell];

                if (stair < 2.2f)
                {
                    var s = 1 - Smooth01(1.1f, 2.1f, stair);
                    t = Mathf.Lerp(t, Terraced(w, 0.55f), s);
                }

                // A pad: level — but where a stair comes into it, a ramp over its first 4 m (the route walk met a step
                // a terrace high, more than one can climb, where the stairs reached the squares).
                var pad = padOf[cell];

                if (pad >= 0)
                {
                    t = stair < 2.2f ? Mathf.Lerp(t, Pads[pad].Level, Smooth01(0, 4, PadInside(Pads[pad], p))) : Pads[pad].Level;
                }
            }

            // A little unevenness of trodden ground.
            t += MountainShape.Fbm(x * 0.15f, z * 0.15f, 2) * 0.04f;

            return Mathf.Lerp(naturalHeight, t, Smooth01(-40, 0, inside));
        }

        /// <summary>The terraced ground's height at a point (the plan's own, as the terrain will have it).</summary>
        public float GroundAt(Vector2 p) => Height(p.x, p.y, natural(p.x, p.y));

        // ── The boundary ──────────────────────────────────────────────

        void MakeBoundary()
        {
            const int points = 72;

            for (var i = 0; i < points; i++)
            {
                var a = i * Mathf.PI * 2 / points;
                // Not an oval: grown out along the ways in and pinched where the ground was poor — lobes and bays.
                var seed = Place.Centre.x * 0.001f + Place.Centre.y * 0.0007f;
                var wobble = 1 + 0.2f * MountainShape.Fbm(Mathf.Cos(a) * 1.7f + seed, Mathf.Sin(a) * 1.7f + 5, 3)
                    + 0.08f * Mathf.Sin(a * 2 + seed * 7) + 0.06f * Mathf.Sin(a * 5 + seed * 13) + 0.04f * Mathf.Sin(a * 9 + 2);
                var local = new Vector2(Mathf.Cos(a) * RadiusAlong, Mathf.Sin(a) * RadiusUp) * wobble;
                Boundary.Add(Centre + Along * local.x - Down * local.y);
            }
        }

        /// <summary>How far inside the boundary a point is (negative outside).</summary>
        public float InsideDistance(Vector2 p)
        {
            var inside = false;
            var best = float.PositiveInfinity;

            for (int i = 0, j = Boundary.Count - 1; i < Boundary.Count; j = i++)
            {
                var a = Boundary[i];
                var b = Boundary[j];

                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }

                best = Mathf.Min(best, QingMao.ToSegment(p, a, b));
            }

            return inside ? best : -best;
        }

        /// <summary>A point in the village's own frame: along the slope, and uphill, from its middle.</summary>
        public Vector2 Local(Vector2 p) => new Vector2(Vector2.Dot(p - Centre, Along), -Vector2.Dot(p - Centre, Down));

        public Vector2 World(float along, float up) => Centre + Along * along - Down * up;

        // ── Rasters ───────────────────────────────────────────────────

        void Rasters()
        {
            var count = Resolution * Resolution;
            kind = new byte[count];
            stairNear = Enumerable.Repeat(99f, count).ToArray();
            padOf = Enumerable.Repeat((short)-1, count).ToArray();
            taken = new bool[count];

            for (var j = 0; j < Resolution; j++)
            {
                for (var i = 0; i < Resolution; i++)
                {
                    var p = new Vector2(Area.x + i * spacing, Area.y + j * spacing);

                    if (InsideDistance(p) > 0)
                    {
                        kind[j * Resolution + i] = KGround;
                    }
                }
            }
        }

        public int CellOf(Vector2 p)
        {
            var i = Mathf.RoundToInt((p.x - Area.x) / spacing);
            var j = Mathf.RoundToInt((p.y - Area.y) / spacing);

            return i < 0 || j < 0 || i >= Resolution || j >= Resolution ? -1 : j * Resolution + i;
        }

        public byte KindAt(Vector2 p)
        {
            var cell = CellOf(p);

            return cell >= 0 ? kind[cell] : KWild;
        }

        /// <summary>Calls `visit` for every raster cell within `radius` of a point.</summary>
        void Around(Vector2 p, float radius, Action<int, float> visit)
        {
            var ci = Mathf.RoundToInt((p.x - Area.x) / spacing);
            var cj = Mathf.RoundToInt((p.y - Area.y) / spacing);
            var r = Mathf.CeilToInt(radius / spacing);

            for (var j = cj - r; j <= cj + r; j++)
            {
                for (var i = ci - r; i <= ci + r; i++)
                {
                    if (i < 0 || j < 0 || i >= Resolution || j >= Resolution)
                    {
                        continue;
                    }

                    var d = Vector2.Distance(p, new Vector2(Area.x + i * spacing, Area.y + j * spacing));

                    if (d <= radius)
                    {
                        visit(j * Resolution + i, d);
                    }
                }
            }
        }

        /// <summary>Calls `visit` for every raster cell inside a turned rectangle (grown by `margin`).</summary>
        void Inside(Vector2 centre, Vector2 half, float yaw, float margin, Action<int> visit)
        {
            var reach = half.magnitude + margin;
            var cos = Mathf.Cos(yaw * Mathf.Deg2Rad);
            var sin = Mathf.Sin(yaw * Mathf.Deg2Rad);
            Around(centre, reach, (cell, _) =>
            {
                var p = new Vector2(Area.x + cell % Resolution * spacing, Area.y + cell / Resolution * spacing) - centre;
                var lx = p.x * cos - p.y * sin;
                var lz = p.x * sin + p.y * cos;

                if (Mathf.Abs(lx) <= half.x + margin && Mathf.Abs(lz) <= half.y + margin)
                {
                    visit(cell);
                }
            });
        }

        bool Free(Vector2 centre, Vector2 half, float yaw, float margin = 0.2f)
        {
            var free = true;
            Inside(centre, half, yaw, margin, cell =>
            {
                if (taken[cell] || kind[cell] == KWild || kind[cell] == KLane || kind[cell] == KStair || kind[cell] == KPaved || padOf[cell] >= 0 || stairNear[cell] < 1.8f)
                {
                    free = false;
                }
            });

            return free;
        }

        void Take(Vector2 centre, Vector2 half, float yaw, byte as_ = KFloor)
        {
            Inside(centre, half, yaw, 0, cell =>
            {
                taken[cell] = true;

                if (kind[cell] != KWild)
                {
                    kind[cell] = as_;
                }
            });
        }

        // ── The square and the great buildings' pads ──────────────────

        float YawFacing(Vector2 direction) => Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

        void AddPad(string name, Vector2 centre, Vector2 half, float yaw, bool paved)
        {
            var pad = new Pad { Name = name, Centre = centre, Half = half, Yaw = yaw, Level = Terraced(Warp(centre), 0.07f), Paved = paved };
            var index = (short)Pads.Count;
            Pads.Add(pad);
            Inside(centre, half, yaw, 0, cell =>
            {
                padOf[cell] = index;

                if (paved && kind[cell] != KWild)
                {
                    kind[cell] = KPaved;
                }
            });
        }

        void ChooseSquareAndPads()
        {
            var faceDown = YawFacing(Down);
            var big = Place.Clan == Clan.GuYue;

            // The square: in the middle, a little below it, long along the slope.
            var square = World(0, -8);
            AddPad("square", square, big ? new Vector2(24, 15) : new Vector2(15, 11), faceDown, true);
            Square = Pads[Pads.Count - 1];
            Places.Add(("square", square, faceDown));

            // Above it, the clan's pavilion (or hall) on its own raised terrace, looking down over the square.
            var head = World(0, big ? 21 : 15);
            AddPad("pavilion", head, big ? new Vector2(13, 11.5f) : new Vector2(8, 7), faceDown, true);

            if (big)
            {
                AddPad("shrine", World(36, 17), new Vector2(7, 6.5f), faceDown, true);
                AddPad("academy", World(-50, 14), new Vector2(15, 11), faceDown, true);
                // The academy's yard before it.
                AddPad("academy yard", World(-50, -4), new Vector2(15, 7), faceDown, false);
                AddPad("hostel", World(-78, -14), new Vector2(9, 6), faceDown, false);
            }
        }

        // ── Streets ───────────────────────────────────────────────────

        /// <summary>The lines where the warped height crosses `levels`, inside the village, as polylines (marching squares on a 1 m grid).</summary>
        List<(float level, List<Vector2> line)> Contours(IEnumerable<float> levels, float inset)
        {
            var wanted = levels.ToList();
            var minX = Boundary.Min(p => p.x) - 2;
            var maxX = Boundary.Max(p => p.x) + 2;
            var minZ = Boundary.Min(p => p.y) - 2;
            var maxZ = Boundary.Max(p => p.y) + 2;
            const float cell = 1.2f;
            var nx = Mathf.CeilToInt((maxX - minX) / cell) + 1;
            var nz = Mathf.CeilToInt((maxZ - minZ) / cell) + 1;
            var w = new float[nx * nz];
            var inside = new bool[nx * nz];

            for (var k = 0; k < nz; k++)
            {
                for (var i = 0; i < nx; i++)
                {
                    var p = new Vector2(minX + i * cell, minZ + k * cell);
                    w[k * nx + i] = Warp(p);
                    inside[k * nx + i] = InsideDistance(p) > inset;
                }
            }

            var result = new List<(float, List<Vector2>)>();

            foreach (var level in wanted)
            {
                var segments = new List<(Vector2, Vector2)>();

                for (var k = 0; k + 1 < nz; k++)
                {
                    for (var i = 0; i + 1 < nx; i++)
                    {
                        var corners = new[] { k * nx + i, k * nx + i + 1, (k + 1) * nx + i + 1, (k + 1) * nx + i };

                        if (!corners.All(c => inside[c]))
                        {
                            continue;
                        }

                        var at = new[] { new Vector2(minX + i * cell, minZ + k * cell), new Vector2(minX + (i + 1) * cell, minZ + k * cell), new Vector2(minX + (i + 1) * cell, minZ + (k + 1) * cell), new Vector2(minX + i * cell, minZ + (k + 1) * cell) };
                        var crossings = new List<Vector2>();

                        for (var e = 0; e < 4; e++)
                        {
                            var a = w[corners[e]];
                            var b = w[corners[(e + 1) % 4]];

                            if ((a < level) != (b < level))
                            {
                                var t = (level - a) / (b - a);
                                crossings.Add(Vector2.Lerp(at[e], at[(e + 1) % 4], t));
                            }
                        }

                        if (crossings.Count == 2)
                        {
                            segments.Add((crossings[0], crossings[1]));
                        }
                        else if (crossings.Count == 4)
                        {
                            segments.Add((crossings[0], crossings[1]));
                            segments.Add((crossings[2], crossings[3]));
                        }
                    }
                }

                foreach (var line in Chain(segments))
                {
                    result.Add((level, line));
                }
            }

            return result;
        }

        /// <summary>Joins segments end to end into polylines.</summary>
        static List<List<Vector2>> Chain(List<(Vector2 a, Vector2 b)> segments)
        {
            Vector2Int Key(Vector2 p) => new Vector2Int(Mathf.RoundToInt(p.x * 20), Mathf.RoundToInt(p.y * 20));
            var ends = new Dictionary<Vector2Int, List<int>>();

            for (var s = 0; s < segments.Count; s++)
            {
                foreach (var p in new[] { segments[s].a, segments[s].b })
                {
                    var key = Key(p);

                    if (!ends.TryGetValue(key, out var list))
                    {
                        ends[key] = list = new List<int>();
                    }

                    list.Add(s);
                }
            }

            var used = new bool[segments.Count];
            var lines = new List<List<Vector2>>();

            for (var s = 0; s < segments.Count; s++)
            {
                if (used[s])
                {
                    continue;
                }

                used[s] = true;
                var line = new LinkedList<Vector2>();
                line.AddLast(segments[s].a);
                line.AddLast(segments[s].b);

                foreach (var forward in new[] { true, false })
                {
                    while (true)
                    {
                        var tip = forward ? line.Last.Value : line.First.Value;
                        var next = -1;

                        foreach (var candidate in ends[Key(tip)])
                        {
                            if (!used[candidate])
                            {
                                next = candidate;
                                break;
                            }
                        }

                        if (next < 0)
                        {
                            break;
                        }

                        used[next] = true;
                        var other = Key(segments[next].a) == Key(tip) ? segments[next].b : segments[next].a;

                        if (forward) line.AddLast(other);
                        else line.AddFirst(other);
                    }
                }

                lines.Add(line.ToList());
            }

            return lines;
        }

        /// <summary>A polyline evened out: a point every `every` metres, its corners softened.</summary>
        static List<Vector2> Resample(List<Vector2> points, float every)
        {
            if (points.Count < 2)
            {
                return points;
            }

            var even = new List<Vector2> { points[0] };
            var carried = 0f;

            for (var i = 0; i + 1 < points.Count; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var length = Vector2.Distance(a, b);
                var s = every - carried;

                while (s < length)
                {
                    even.Add(Vector2.Lerp(a, b, s / length));
                    s += every;
                }

                carried = length - (s - every);
            }

            even.Add(points[points.Count - 1]);

            // Soften.
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 1; i + 1 < even.Count; i++)
                {
                    even[i] = (even[i - 1] + even[i] * 2 + even[i + 1]) / 4;
                }
            }

            return even;
        }

        static float Length(List<Vector2> points)
        {
            var length = 0f;

            for (var i = 0; i + 1 < points.Count; i++)
            {
                length += Vector2.Distance(points[i], points[i + 1]);
            }

            return length;
        }

        /// <summary>The lanes along the terraces: at the foot of each terrace, just above the wall below it.</summary>
        void ContourStreets()
        {
            var lowest = Mathf.FloorToInt((Boundary.Min(p => Warp(p)) - Level0) / Step) - 1;
            var highest = Mathf.CeilToInt((Boundary.Max(p => Warp(p)) - Level0) / Step) + 1;
            var levels = new List<float>();

            for (var k = lowest; k <= highest; k++)
            {
                levels.Add(Level0 + Step * (k + 0.2f));
            }

            foreach (var (level, line) in Contours(levels, 7))
            {
                var even = Resample(line, 1.6f);

                // Not across the square or a great building's pad: cut the lane there.
                var piece = new List<Vector2>();

                void Flush()
                {
                    if (Length(piece) > 22)
                    {
                        var terrace = Mathf.FloorToInt((level - Level0) / Step);
                        // Every third terrace's lane is a street, wider; the rest are lanes.
                        var main = Mathf.Abs(terrace) % 3 == 0;
                        Streets.Add(new Street { Name = $"lane {terrace}", Points = new List<Vector2>(piece), Half = main ? 1.7f : 1.1f + R() * 0.3f, Main = main });
                    }

                    piece.Clear();
                }

                foreach (var p in even)
                {
                    var cell = CellOf(p);

                    if (cell >= 0 && padOf[cell] >= 0 && !Pads[padOf[cell]].Paved)
                    {
                        Flush();
                        continue;
                    }

                    if (cell >= 0 && padOf[cell] >= 0)
                    {
                        // Across the square the lane is the square itself.
                        Flush();
                        continue;
                    }

                    piece.Add(p);
                }

                Flush();
            }
        }

        /// <summary>The stairs: climbing from the lowest terrace to the highest every few tens of metres, wandering a little; the main one from the lower gate to the square.</summary>
        void StairStreets()
        {
            var spacingAlong = Place.Clan == Clan.GuYue ? 46 : 40;
            var count = 0;

            for (var a = -RadiusAlong + 22 + R() * 10; a < RadiusAlong - 22; a += spacingAlong * (0.7f + 0.6f * R()))
            {
                var main = Mathf.Abs(a) < spacingAlong * 0.5f;
                var start = World(a, -RadiusUp * 1.2f);

                // From below the village, up until it leaves it.
                var line = new List<Vector2>();
                var p = start;
                var wander = R() * 100;

                for (var s = 0; s < 900; s++)
                {
                    var inside = InsideDistance(p);

                    if (line.Count > 0 && InGreatPad(p))
                    {
                        break;
                    }

                    if (inside > 6)
                    {
                        line.Add(p);
                    }
                    else if (line.Count > 0)
                    {
                        break;
                    }

                    var uphill = Gradient(p);
                    var turn = MountainShape.Fbm(s * 0.03f + wander, a * 0.01f, 2) * 0.7f;
                    var dir = Rotate(uphill, turn);

                    // The main stair heads for the square.
                    if (main)
                    {
                        var toSquare = (Square.Centre - p).normalized;
                        var reached = Local(p).y > Local(Square.Centre).y - Square.Half.y;
                        dir = reached ? uphill : Vector2.Lerp(dir, toSquare, 0.6f).normalized;
                    }

                    p += dir * 1.2f;
                }

                if (Length(line) > 20)
                {
                    Streets.Add(new Street { Name = main ? "main stair" : $"stair {count}", Points = Resample(line, 1.4f), Half = main ? 1.6f : 1.05f, Stair = true, Main = main });
                    count++;
                }
            }
        }

        /// <summary>How far inside a pad's edge a point is, m (negative outside).</summary>
        static float PadInside(Pad pad, Vector2 p)
        {
            var local = Quaternion.Euler(0, -pad.Yaw, 0) * new Vector3(p.x - pad.Centre.x, 0, p.y - pad.Centre.y);

            return Mathf.Min(pad.Half.x - Mathf.Abs(local.x), pad.Half.y - Mathf.Abs(local.z));
        }

        /// <summary>Whether a point is on (or by) a pad other than the square: a great building's terrace.</summary>
        bool InGreatPad(Vector2 p)
        {
            foreach (var pad in Pads)
            {
                if (pad == Square)
                {
                    continue;
                }

                var local = Quaternion.Euler(0, -pad.Yaw, 0) * new Vector3(p.x - pad.Centre.x, 0, p.y - pad.Centre.y);

                if (Mathf.Abs(local.x) < pad.Half.x + 4 && Mathf.Abs(local.z) < pad.Half.y + 4)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Uphill, along the smoothed ground (unit length).</summary>
        Vector2 Gradient(Vector2 p)
        {
            var gx = SmoothAt(p + Vector2.right * 3) - SmoothAt(p - Vector2.right * 3);
            var gz = SmoothAt(p + Vector2.up * 3) - SmoothAt(p - Vector2.up * 3);
            var g = new Vector2(gx, gz);

            return g.sqrMagnitude > 1e-6f ? g.normalized : -Down;
        }

        static Vector2 Rotate(Vector2 v, float radians)
        {
            var c = Mathf.Cos(radians);
            var s = Mathf.Sin(radians);

            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        void StampStreets()
        {
            foreach (var street in Streets)
            {
                for (var i = 0; i + 1 < street.Points.Count; i++)
                {
                    var a = street.Points[i];
                    var b = street.Points[i + 1];
                    var length = Vector2.Distance(a, b);

                    for (var s = 0f; s <= length; s += spacing * 0.8f)
                    {
                        var p = Vector2.Lerp(a, b, s / length);

                        if (street.Stair)
                        {
                            Around(p, 2.4f, (cell, d) => stairNear[cell] = Mathf.Min(stairNear[cell], d));
                        }

                        Around(p, street.Half, (cell, d) =>
                        {
                            if (kind[cell] != KWild && kind[cell] != KPaved)
                            {
                                kind[cell] = street.Stair ? KStair : KLane;
                            }
                        });
                    }
                }
            }
        }

        // ── Gates ─────────────────────────────────────────────────────

        void FindGates()
        {
            // Four gates, as the lore has Gu Yue's (ch. 149): the lower and the upper, and one at each end along the slope.
            var directions = new[] { (Down, "south"), (-Down, "north"), (Along, "east"), (-Along, "west") };

            foreach (var (direction, name) in directions)
            {
                // Where a street meets the boundary nearest that way.
                var best = default(Vector2);
                var bestScore = float.NegativeInfinity;

                foreach (var street in Streets)
                {
                    foreach (var end in new[] { street.Points[0], street.Points[street.Points.Count - 1] })
                    {
                        var score = Vector2.Dot((end - Centre).normalized, direction) * 100 - Mathf.Abs(InsideDistance(end) - 7) * 0.5f + (street.Main ? 12 : 0);

                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = end;
                        }
                    }
                }

                if (bestScore < 40)
                {
                    continue;
                }

                // On the boundary itself, out from that street's end.
                var outward = (best - Centre).normalized;
                var at = best;

                for (var s = 0; s < 40 && InsideDistance(at) > 0; s++)
                {
                    at += outward * 0.5f;
                }

                Gates.Add(new Gate { At = at, Out = outward, Name = $"gate {name}" });
                // A short way in from the gate to the street.
                Streets.Add(new Street { Name = $"gate {name} way", Points = Resample(new List<Vector2> { at + outward * 6, best }, 1.4f), Half = 1.5f, Main = true });
                Places.Add(($"gate {name}", at - outward * 5, YawFacing(outward)));
            }

            StampStreets();
        }

        // ── Retaining walls ───────────────────────────────────────────

        void RetainingWalls()
        {
            var lowest = Mathf.FloorToInt((Boundary.Min(p => Warp(p)) - Level0) / Step) - 1;
            var highest = Mathf.CeilToInt((Boundary.Max(p => Warp(p)) - Level0) / Step) + 1;
            var levels = new List<float>();

            for (var k = lowest; k <= highest; k++)
            {
                // The middle of the rise (for a wall's softness of 0.07).
                levels.Add(Level0 + Step * (k + 1 - 0.035f));
            }

            foreach (var (level, line) in Contours(levels, 1))
            {
                var even = Resample(line, 1.2f);
                var wall = new Wall();
                var terrace = Mathf.FloorToInt((level - Level0) / Step);

                void Flush()
                {
                    if (wall.Points.Count >= 3)
                    {
                        Walls.Add(wall);
                    }

                    wall = new Wall();
                }

                foreach (var p in even)
                {
                    var cell = CellOf(p);
                    // Only where the rise is a wall (not a bank), not at a stair, not inside a pad.
                    var isWall = Softness(p) < 0.16f && cell >= 0 && stairNear[cell] > 1.5f && padOf[cell] < 0;

                    if (!isWall)
                    {
                        Flush();
                        continue;
                    }

                    wall.Points.Add(p);
                    wall.Low.Add(LevelOf(terrace));
                    wall.High.Add(LevelOf(terrace + 1));
                }

                Flush();
            }

            // The pads' edges where they are cut into the slope or built out over it.
            foreach (var pad in Pads)
            {
                var corners = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
                var turn = Quaternion.Euler(0, pad.Yaw, 0);

                for (var c = 0; c < 4; c++)
                {
                    var a3 = turn * new Vector3(corners[c].x * (pad.Half.x + 0.4f), 0, corners[c].y * (pad.Half.y + 0.4f));
                    var b3 = turn * new Vector3(corners[(c + 1) % 4].x * (pad.Half.x + 0.4f), 0, corners[(c + 1) % 4].y * (pad.Half.y + 0.4f));
                    var a = pad.Centre + new Vector2(a3.x, a3.z);
                    var b = pad.Centre + new Vector2(b3.x, b3.z);
                    var wall = new Wall();
                    var steps = Mathf.CeilToInt(Vector2.Distance(a, b) / 1.2f);

                    for (var s = 0; s <= steps; s++)
                    {
                        var p = Vector2.Lerp(a, b, (float)s / steps);
                        var outside = Terraced(Warp(p + (p - pad.Centre).normalized * 1.2f), 0.07f);
                        var low = Mathf.Min(outside, pad.Level);
                        var high = Mathf.Max(outside, pad.Level);
                        var cell = CellOf(p);

                        if (high - low < 0.7f || (cell >= 0 && stairNear[cell] < 1.6f) || InsideDistance(p) < 2)
                        {
                            if (wall.Points.Count >= 2) Walls.Add(wall);
                            wall = new Wall();
                            continue;
                        }

                        wall.Points.Add(p);
                        wall.Low.Add(low);
                        wall.High.Add(high);
                    }

                    if (wall.Points.Count >= 2)
                    {
                        Walls.Add(wall);
                    }
                }
            }
        }

        // ── IGroundPlan ───────────────────────────────────────────────

        public void Paint(float x, float z, float height, float steep, float[] weights)
        {
            var p = new Vector2(x, z);
            var inside = InsideDistance(p);

            if (inside < -35)
            {
                return;
            }

            var noise = Mathf.PerlinNoise(x * 0.35f, z * 0.35f);
            var blotch = Mathf.PerlinNoise(x * 0.06f + 3, z * 0.06f + 8);
            var near = Smooth01(-35, 0, inside);

            switch (KindAt(p))
            {
                case KLane:
                    weights[QingMaoBuilder.LayerDirtIndex] += 6 * near;
                    weights[QingMaoBuilder.LayerMudIndex] += (2 + 4 * blotch) * near;
                    weights[QingMaoBuilder.LayerGravelIndex] += noise > 0.75f ? 2 : 0;
                    break;
                case KStair:
                    weights[QingMaoBuilder.LayerFlagstoneIndex] += 5;
                    weights[QingMaoBuilder.LayerDirtIndex] += 2;
                    break;
                case KPaved:
                    weights[QingMaoBuilder.LayerFlagstoneIndex] += 9;
                    weights[QingMaoBuilder.LayerMossIndex] += blotch > 0.7f ? 2 : 0;
                    break;
                case KFloor:
                case KYard:
                    weights[QingMaoBuilder.LayerDirtIndex] += 5;
                    weights[QingMaoBuilder.LayerMudIndex] += 2 * blotch;
                    break;
                case KGarden:
                    weights[QingMaoBuilder.LayerMudIndex] += 7;
                    break;
                case KGround:
                    // Inside the walls: trodden grass, bare earth worn in patches, moss in the damp corners.
                    weights[QingMaoBuilder.LayerDirtIndex] += (blotch > 0.55f ? 2.2f : 0.5f) * near;
                    weights[QingMaoBuilder.LayerMossIndex] += noise > 0.6f ? 0.8f : 0;
                    break;
                default:
                    // Round the walls, outside: worn ground fading into the wild.
                    weights[QingMaoBuilder.LayerDirtIndex] += 1.2f * near * blotch;
                    break;
            }

            // The terraces' rises: stone where they are walls, grass on the banks.
            if (steep > 40 && inside > -5)
            {
                weights[QingMaoBuilder.LayerRockIndex] += 4;
            }
        }

        public float Grass(float x, float z)
        {
            var p = new Vector2(x, z);

            switch (KindAt(p))
            {
                case KLane:
                case KStair:
                case KFloor:
                case KGarden:
                    return 0;
                case KPaved:
                    // Weeds between the flagstones.
                    return Mathf.PerlinNoise(x * 0.5f, z * 0.5f) > 0.78f ? 0.15f : 0;
                case KYard:
                    return 0.15f;
                case KGround:
                    return 0.45f + 0.4f * Mathf.PerlinNoise(x * 0.08f, z * 0.08f);
                default:
                    var inside = InsideDistance(p);
                    return inside > -12 ? 0.5f : 1;
            }
        }

        public bool Wild(Vector2 p) => InsideDistance(p) < -14 && !Gates.Any(g => Vector2.Distance(g.At, p) < 16);
    }
}
