using System.Collections.Generic;
using GuWorld.World;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// The shape of Qing Mao Mountain as nature made it, before anyone built on it:
    /// one great massif whose main ridge runs west to east through the peak, its
    /// southern flanks long and gentle, its northern (the back mountain) shorter and
    /// steeper; the fore mountain, a lower peak in front to the south-east, joined
    /// to it by a saddle; spurs and gullies running down every side; behind, a cliff
    /// band where the Bai river falls into a deep pool; streams in valleys down to
    /// the plain at the foot, where the land settles at about 40 m.
    ///
    /// Heights in metres above the plain's lowest ground, as pure functions of
    /// position (X east, Z north): the builder samples them for the terrains. The
    /// streams' beds are worked out once (never rising downstream).
    /// </summary>
    public static class MountainShape
    {
        /// <summary>The main ridge, west to east: (x, crest height, z).</summary>
        static readonly Vector3[] Ridge =
        {
            new Vector3(-8000, 430, 2900), new Vector3(-6200, 1100, 2750), new Vector3(-3900, 1620, 2420), new Vector3(-1600, 1960, 2100),
            new Vector3(300, 2150, 1800), new Vector3(2300, 1930, 1480), new Vector3(4400, 1580, 1000), new Vector3(6500, 1100, 450),
            new Vector3(8000, 430, 160),
        };

        /// <summary>The back mountain's cliff band, west to east, and how far the ground drops over it at the waterfall.</summary>
        static readonly Vector2[] Cliff =
        {
            new Vector2(-3300, 6020), new Vector2(-2700, 6090), new Vector2(-2150, 6135), new Vector2(-1720, 6110), new Vector2(-1250, 6060), new Vector2(-700, 5960), new Vector2(-150, 5820),
        };

        const float CliffDrop = 88;

        public class Stream
        {
            public string Name;
            public Vector2[] Points;
            /// <summary>Half the channel's width at the source and at the mouth, m.</summary>
            public float HalfFrom;
            public float HalfTo;
            /// <summary>How deep and wide its valley is cut at the mouth (it grows from the source).</summary>
            public float Valley;
            public float ValleyWidth;
            /// <summary>The bed, every few metres down the stream, never rising (worked out by Prepare).</summary>
            public List<(Vector2 at, float bed, float half)> Bed;
        }

        static Vector2 V(float x, float z) => new Vector2(x, z);

        public static readonly Stream[] Streams =
        {
            // From the Gu Yue's spirit spring past the village's west side, down to the willow hamlet and off the map.
            new Stream
            {
                Name = "Ручей источника", HalfFrom = 1.6f, HalfTo = 6, Valley = 34, ValleyWidth = 230,
                Points = new[] { QingMao.Spring, V(-2410, -1520), V(-2560, -1700), V(-2650, -1980), V(-2780, -2400), V(-3050, -3000), V(-3420, -3700), V(-3820, -4500), V(-4380, -5400), V(-5000, -6050), V(-5550, -6900), V(-6200, -8200), V(-6700, -10100) },
            },
            // The Bai river above the cliff: from the back mountain's high gullies to the lip of the fall.
            new Stream
            {
                Name = "Река Бай (верхняя)", HalfFrom = 1.5f, HalfTo = 5, Valley = 40, ValleyWidth = 260,
                Points = new[] { V(-650, 4050), V(-930, 4600), V(-1200, 5150), V(-1480, 5640), V(-1650, 5950), QingMao.FallTop + V(0, -24) },
            },
            // …and below it: out of the pool, north past the Bai village and on to the plain.
            new Stream
            {
                Name = "Река Бай", HalfFrom = 6, HalfTo = 9, Valley = 22, ValleyWidth = 280,
                Points = new[] { QingMao.Pool + V(40, 50), V(-1540, 6620), V(-1320, 7300), V(-900, 8000), V(-250, 8800), V(300, 9500), V(500, 10100) },
            },
            // The fore stream, between the fore mountain and the great one, past the Xiong village.
            new Stream
            {
                Name = "Ручей передней горы", HalfFrom = 1.5f, HalfTo = 6, Valley = 38, ValleyWidth = 250,
                Points = new[] { V(2450, -2300), V(2700, -3100), V(2980, -3900), V(3480, -5150), V(4100, -6100), V(4800, -6950), V(5500, -7800), V(6100, -8900), V(6500, -10100) },
            },
            new Stream
            {
                Name = "Восточный ручей", HalfFrom = 1.5f, HalfTo = 5, Valley = 36, ValleyWidth = 240,
                Points = new[] { V(4300, 2400), V(5100, 2900), V(6000, 3300), V(7200, 3800), V(8600, 4300), V(10100, 4700) },
            },
            new Stream
            {
                Name = "Западный ручей", HalfFrom = 1.5f, HalfTo = 5, Valley = 36, ValleyWidth = 240,
                Points = new[] { V(-4300, 1050), V(-5200, 900), V(-6200, 1000), V(-7300, 1150), V(-8600, 900), V(-10100, 700) },
            },
            new Stream
            {
                Name = "Южный ручей", HalfFrom = 1.4f, HalfTo = 5, Valley = 30, ValleyWidth = 220,
                Points = new[] { V(-300, -1600), V(-450, -2700), V(-650, -3900), V(-800, -5200), V(-850, -6500), V(-950, -7350), V(-1100, -8600), V(-1300, -10100) },
            },
        };

        static float SmoothStep(float min, float max, float x)
        {
            if (x <= min) return 0;
            if (x >= max) return 1;
            x = (x - min) / (max - min);

            return x * x * (3 - 2 * x);
        }

        /// <summary>Layered noise, about −1…1.</summary>
        public static float Fbm(float x, float z, int octaves)
        {
            float sum = 0, amplitude = 1, total = 0;

            for (var i = 0; i < octaves; i++)
            {
                sum += (Mathf.PerlinNoise(x + 31.7f * i + 101.3f, z - 17.3f * i + 57.9f) * 2 - 1) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                x *= 2.03f;
                z *= 2.03f;
            }

            return sum / total;
        }

        /// <summary>Sharp-crested noise (spurs and ridges), 0…1.</summary>
        public static float Ridged(float x, float z, int octaves = 4)
        {
            float sum = 0, amplitude = 1, total = 0;

            for (var i = 0; i < octaves; i++)
            {
                var n = 1 - Mathf.Abs(Mathf.PerlinNoise(x + 7.1f * i + 13.7f, z + 3.9f * i + 71.1f) * 2 - 1);
                sum += n * n * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                x *= 2.1f;
                z *= 2.1f;
            }

            return sum / total;
        }

        static float SmoothMax(float a, float b, float k)
        {
            var h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);

            return Mathf.Lerp(a, b, h) + k * h * (1 - h);
        }

        /// <summary>
        /// How far the main ridge is, and its crest there — blended over the nearest stretches of it, so the crest
        /// does not jump where one stretch stops being the nearest (that left straight scars down the mountain).
        /// </summary>
        static (float distance, float crest, float side) ToRidge(Vector2 p)
        {
            var nearest = float.PositiveInfinity;

            for (var i = 0; i + 1 < Ridge.Length; i++)
            {
                nearest = Mathf.Min(nearest, Segment(p, i, out _));
            }

            float weights = 0, crest = 0, distance = 0;

            for (var i = 0; i + 1 < Ridge.Length; i++)
            {
                var d = Segment(p, i, out var c);
                var w = Mathf.Exp(-(d - nearest) / 350);
                weights += w;
                crest += c * w;
                distance += d * w;
            }

            return (distance / weights, crest / weights, 1);
        }

        /// <summary>How far a stretch of the ridge is, and its crest at the nearest point.</summary>
        static float Segment(Vector2 p, int i, out float crest)
        {
            var a = new Vector2(Ridge[i].x, Ridge[i].z);
            var b = new Vector2(Ridge[i + 1].x, Ridge[i + 1].z);
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            crest = Mathf.Lerp(Ridge[i].y, Ridge[i + 1].y, t);

            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>The ridge's line at a given X (Z there), the ends held level beyond them.</summary>
        static float RidgeZ(float x)
        {
            if (x <= Ridge[0].x) return Ridge[0].z;

            for (var i = 0; i + 1 < Ridge.Length; i++)
            {
                if (x <= Ridge[i + 1].x)
                {
                    return Mathf.Lerp(Ridge[i].z, Ridge[i + 1].z, (x - Ridge[i].x) / (Ridge[i + 1].x - Ridge[i].x));
                }
            }

            return Ridge[Ridge.Length - 1].z;
        }

        /// <summary>The broad shape, without spurs or small bumps: the massif, the fore mountain, the plain.</summary>
        public static float Broad(float x, float z)
        {
            var p = new Vector2(x, z);
            var (distance, crest, _) = ToRidge(p);
            // The back (north) is steeper than the long southern flanks — blended across the ridge, so no seam.
            var width = Mathf.Lerp(5700, 4300, SmoothStep(-1800, 1800, z - RidgeZ(x)));
            var main = crest * Mathf.Exp(-Mathf.Pow(distance / width, 1.3f));
            var fore = 1010 * Mathf.Exp(-Mathf.Pow(Vector2.Distance(p, QingMao.ForePeak) / 2300, 1.55f));
            var h = SmoothMax(main, fore + main * 0.22f, 160);

            // The plain at the foot: the mountain's skirts end along a wandering line (not the map's square), and only
            // the last few hundred metres before the map's edge are made sure to be plain.
            var fromMiddle = p - new Vector2(300, -400);
            var reach = fromMiddle.magnitude;
            var way = reach > 1 ? fromMiddle / reach : Vector2.right;
            var foot = 8300 + 1100 * Fbm(way.x * 1.3f + 41, way.y * 1.3f + 17, 3) + 500 * Mathf.Sin(Mathf.Atan2(way.y, way.x) * 3 + 1);
            var skirts = 1 - SmoothStep(foot - 2600, foot + 900, reach);
            var edge = QingMao.Half - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            var plain = 38 + 14 * Fbm(x * 0.00045f, z * 0.00045f, 3);

            return Mathf.Lerp(plain, plain + h, skirts * SmoothStep(0, 700, edge));
        }

        /// <summary>How rugged the ground is here: 0 on the plain, 1 high on the mountain.</summary>
        static float Rugged(float broad) => SmoothStep(80, 1400, broad);

        /// <summary>
        /// The shoulders the settlements sit on: a gentle bench in the mountain's side (its middle, how far it is level
        /// and how far it blends out, m, and its slope downhill). Villages are built where the mountain gives room.
        /// </summary>
        static readonly (string id, float inner, float outer, float slope)[] Benches =
        {
            ("gu_yue", 430, 1150, 0.18f),
            ("xiong", 280, 820, 0.13f),
            ("bai", 230, 600, 0.06f),
            ("hamlet_willow", 110, 380, 0.09f),
            ("hamlet_ford", 100, 360, 0.08f),
            ("hamlet_terraces", 100, 360, 0.08f),
            ("hamlet_west", 110, 420, 0.1f),
            ("hamlet_north", 100, 360, 0.07f),
            ("hamlet_pine", 90, 320, 0.08f),
            ("hamlet_mill", 100, 340, 0.07f),
            ("hamlet_east", 90, 320, 0.08f),
            ("hamlet_honey", 95, 330, 0.08f),
            ("hamlet_stone", 90, 320, 0.08f),
            ("hamlet_mist", 90, 320, 0.07f),
            ("hamlet_bamboo", 85, 300, 0.08f),
        };

        static (Vector2 centre, float height, Vector2 rise, float inner, float outer)[] benches;

        static (Vector2, float, Vector2, float, float)[] BenchPlanes()
        {
            if (benches != null)
            {
                return benches;
            }

            var list = new List<(Vector2, float, Vector2, float, float)>();

            foreach (var (id, inner, outer, slope) in Benches)
            {
                var c = QingMao.Find(id).Centre;
                // Its height: the ground round it, smoothed; its fall: the broad shape's.
                var h = 0f;

                for (var i = 0; i < 8; i++)
                {
                    var a = i * Mathf.PI / 4;
                    h += Raw(c.x + Mathf.Cos(a) * inner * 0.5f, c.y + Mathf.Sin(a) * inner * 0.5f, false);
                }

                h /= 8;
                var gx = Broad(c.x + 50, c.y) - Broad(c.x - 50, c.y);
                var gz = Broad(c.x, c.y + 50) - Broad(c.x, c.y - 50);
                var uphill = new Vector2(gx, gz).normalized;
                list.Add((c, h, uphill * slope, inner, outer));
            }

            return benches = list.ToArray();
        }

        /// <summary>Which way is downhill at a settlement's bench (X, Z, unit length).</summary>
        public static Vector2 DownhillAt(Vector2 centre)
        {
            var gx = Broad(centre.x + 50, centre.y) - Broad(centre.x - 50, centre.y);
            var gz = Broad(centre.x, centre.y + 50) - Broad(centre.x, centre.y - 50);

            return -new Vector2(gx, gz).normalized;
        }

        /// <summary>The land before the benches: the broad shape, its spurs and gullies, its bumps.</summary>
        /// <summary>
        /// Where the ground breaks into ledges and cliff bands, 0…1: in patches on the high slopes, none on the
        /// summit (it is climbed) — the trails go round these (MountainTrails).
        /// </summary>
        public static float RockAt(float x, float z, float broad = float.NaN)
        {
            if (float.IsNaN(broad))
            {
                broad = Broad(x, z);
            }

            var summit = SmoothStep(260, 650, Vector2.Distance(new Vector2(x, z), QingMao.Peak));
            // The way over the ridge from the peak down to the back mountain's waterfall: no ledges along it (the route
            // walk could jump down 5 m ledges on the ridge's north face, but not climb back up them).
            summit *= SmoothStep(800, 1100, QingMao.ToSegment(new Vector2(x, z), QingMao.Peak, QingMao.FallTop));

            return SmoothStep(0.58f, 0.74f, Mathf.PerlinNoise(x * 0.0011f + 71, z * 0.0011f + 13)) * SmoothStep(250, 1300, broad) * summit;
        }

        static float Raw(float x, float z, bool small)
        {
            var broad = Broad(x, z);
            var rugged = Rugged(broad);
            // Spurs and gullies running down from the peak, a few hundred metres apart, sharper higher up…
            var toPeak = new Vector2(x - QingMao.Peak.x, z - QingMao.Peak.y);
            var r = toPeak.magnitude;
            var round = r > 1 ? toPeak / r : Vector2.right;
            // Round the peak by direction (no seam where the angle wraps), wandering a little with distance.
            var wander = Fbm(r * 0.0004f + 3, 11, 2) * 0.5f;
            var radial = (Ridged(round.x * 2.2f + wander + 20, round.y * 2.2f + r * 0.00012f + 4, 3) - 0.45f) * 130 * rugged * SmoothStep(400, 2000, r);
            // …and others every way, for the lesser ridges.
            var spurs = (Ridged(x * 0.00042f, z * 0.00042f) - 0.42f) * 150 * (0.2f + rugged);
            var hills = Fbm(x * 0.0016f + 5, z * 0.0016f + 9, 4) * (12 + 34 * rugged);
            var h = broad + radial + spurs + hills;

            // Rock: on the high slopes, here and there, the ground breaks into ledges and cliff bands (each ~22 m band
            // flat on top, its face steep), where the trails will not go. The villages' benches smooth it away near them.
            var rock = RockAt(x, z, broad);

            if (rock > 0)
            {
                const float band = 22;
                var level = h / band;
                var floor = Mathf.Floor(level);
                var ledges = (floor + SmoothStep(0.7f, 1, level - floor)) * band;
                h = Mathf.Lerp(h, ledges, rock * 0.9f);
            }

            if (small)
            {
                h += Fbm(x * 0.012f, z * 0.012f, 3) * (1.4f + 2.0f * rugged);
            }

            return h;
        }

        /// <summary>The ground without the streams: the land, the settlements' benches, the back cliff.</summary>
        public static float Land(float x, float z)
        {
            var p = new Vector2(x, z);
            var h = Raw(x, z, true);

            foreach (var (centre, height, rise, inner, outer) in BenchPlanes())
            {
                var d = Vector2.Distance(p, centre);

                if (d >= outer)
                {
                    continue;
                }

                var w = 1 - SmoothStep(inner, outer, d);
                var plane = height + Vector2.Dot(p - centre, rise);
                // Level, but not flat: a little of the land's lie stays.
                var lie = Fbm(x * 0.006f + 2, z * 0.006f + 7, 3) * 3.5f + Fbm(x * 0.03f, z * 0.03f, 2) * 0.8f;
                h = Mathf.Lerp(h, plane + lie, w);
            }

            return h - CliffStep(p);
        }

        /// <summary>How much lower the ground is behind (north of) the back cliff: sharp at the waterfall, softening towards its ends.</summary>
        static float CliffStep(Vector2 p)
        {
            var best = float.PositiveInfinity;
            var signed = 0f;
            var total = 0f;
            var length = 0f;

            for (var i = 0; i + 1 < Cliff.Length; i++)
            {
                length += Vector2.Distance(Cliff[i], Cliff[i + 1]);
            }

            for (var i = 0; i + 1 < Cliff.Length; i++)
            {
                var a = Cliff[i];
                var b = Cliff[i + 1];
                var ab = b - a;
                var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                var d = Vector2.Distance(p, a + ab * t);

                if (d < best)
                {
                    best = d;
                    signed = ab.x * (p.y - a.y) - ab.y * (p.x - a.x) >= 0 ? d : -d;
                }

                total += ab.magnitude;
            }

            if (best > 900)
            {
                return 0;
            }

            // 0 at the cliff's ends, 1 round the waterfall (about the middle).
            var fall = Vector2.Distance(p, QingMao.FallTop);
            var strength = Mathf.Clamp01(1 - fall / (length * 0.55f));
            strength = strength * strength * (3 - 2 * strength);
            var sharp = Mathf.Lerp(160, 7, strength);

            // North of the line (positive) is below the cliff: the mountain stands to the south, the pool lies to the north.
            return CliffDrop * strength * SmoothStep(-sharp, sharp, signed) * (1 - SmoothStep(380, 900, best));
        }

        // ── The streams ───────────────────────────────────────────────

        static bool prepared;
        static readonly Dictionary<Vector2Int, List<(Stream stream, int index)>> streamCells = new Dictionary<Vector2Int, List<(Stream, int)>>();
        const float StreamCell = 200;
        const int Coarse = 8;

        /// <summary>Works out each stream's bed down its length, and a lookup of where they run.</summary>
        public static void Prepare()
        {
            if (prepared)
            {
                return;
            }

            streamCells.Clear();

            foreach (var stream in Streams)
            {
                stream.Bed = new List<(Vector2, float, float)>();
                var length = 0f;

                for (var i = 0; i + 1 < stream.Points.Length; i++)
                {
                    length += Vector2.Distance(stream.Points[i], stream.Points[i + 1]);
                }

                var lowest = float.PositiveInfinity;
                var gone = 0f;

                for (var i = 0; i + 1 < stream.Points.Length; i++)
                {
                    var a = stream.Points[i];
                    var b = stream.Points[i + 1];
                    var segment = Vector2.Distance(a, b);
                    var steps = Mathf.CeilToInt(segment / 5);

                    for (var s = 0; s < steps; s++)
                    {
                        var along = gone + segment * s / steps;
                        var straight = Vector2.Lerp(a, b, (float)s / steps);
                        // It wanders, but leaves its source and its mouth where they are, and goes round the villages.
                        var free = SmoothStep(0, 450, along) * SmoothStep(0, 450, length - along);
                        var wandered = straight + Meander(straight, (b - a).normalized) * free;
                        var at = Vector2.Lerp(wandered, KeepOut(wandered), SmoothStep(0.1f, 0.5f, free));
                        var t = along / length;
                        var valleyFloor = Land(at.x, at.y) - stream.Valley * Mathf.Lerp(0.45f, 1, t);
                        lowest = Mathf.Min(lowest, valleyFloor - 1.2f);
                        // Never deeper than a few metres under its own valley's floor: where a stream goes round a village over
                        // higher ground, a bed that only ever falls would lie a hundred metres down a slot (it rises there a little).
                        lowest = Mathf.Max(lowest, valleyFloor - 9f);
                        var index = stream.Bed.Count;
                        stream.Bed.Add((at, lowest, Mathf.Lerp(stream.HalfFrom, stream.HalfTo, t)));

                        // Only every eighth sample is looked up first; the nearest is then found among its neighbours.
                        if (index % Coarse == 0)
                        {
                            var cell = new Vector2Int(Mathf.FloorToInt(at.x / StreamCell), Mathf.FloorToInt(at.y / StreamCell));

                            if (!streamCells.TryGetValue(cell, out var list))
                            {
                                streamCells[cell] = list = new List<(Stream, int)>();
                            }

                            list.Add((stream, index));
                        }
                    }

                    gone += segment;
                }
            }

            prepared = true;
        }

        /// <summary>A point pushed out of the settlements (a stream runs past them, not through).</summary>
        static Vector2 KeepOut(Vector2 at)
        {
            foreach (var place in QingMao.Settlements)
            {
                var away = at - place.Centre;
                var room = place.Radius * 1.25f + 25;

                if (away.magnitude < room)
                {
                    at = place.Centre + (away.sqrMagnitude > 1 ? away.normalized : Vector2.right) * room;
                }
            }

            return at;
        }

        /// <summary>A stream wanders off its straight line by up to a couple of hundred metres.</summary>
        static Vector2 Meander(Vector2 at, Vector2 along)
        {
            var across = new Vector2(-along.y, along.x);

            return across * (Fbm(at.x * 0.0011f + 40, at.y * 0.0011f - 20, 3) * 140 + Fbm(at.x * 0.006f + 9, at.y * 0.006f, 2) * 25);
        }

        /// <summary>The nearest point of any stream's bed within reach: how far, the bed there, its half width, how far down the stream (0…1).</summary>
        public static bool NearStream(Vector2 p, float reach, out float distance, out float bed, out float half, out Stream which)
        {
            Prepare();
            distance = float.PositiveInfinity;
            bed = 0;
            half = 0;
            which = null;
            var nearest = 0;
            // The coarse samples are up to 40 m apart: look a little further for them.
            var r = Mathf.CeilToInt((reach + 40) / StreamCell);
            var c = new Vector2Int(Mathf.FloorToInt(p.x / StreamCell), Mathf.FloorToInt(p.y / StreamCell));

            for (var i = -r; i <= r; i++)
            {
                for (var k = -r; k <= r; k++)
                {
                    if (!streamCells.TryGetValue(new Vector2Int(c.x + i, c.y + k), out var list))
                    {
                        continue;
                    }

                    foreach (var (stream, index) in list)
                    {
                        var sample = stream.Bed[index];
                        var d = (sample.at - p).sqrMagnitude;

                        if (d < distance)
                        {
                            distance = d;
                            which = stream;
                            nearest = index;
                        }
                    }
                }
            }

            if (which == null)
            {
                distance = float.PositiveInfinity;

                return false;
            }

            // Among the samples round the nearest coarse one, the nearest.
            for (var i = Mathf.Max(0, nearest - Coarse); i <= Mathf.Min(which.Bed.Count - 1, nearest + Coarse); i++)
            {
                var sample = which.Bed[i];
                var d = (sample.at - p).sqrMagnitude;

                if (d <= distance)
                {
                    distance = d;
                    bed = sample.bed;
                    half = sample.half;
                }
            }

            if (bed == 0)
            {
                var sample = which.Bed[nearest];
                bed = sample.bed;
                half = sample.half;
            }

            distance = Mathf.Sqrt(distance);

            return distance <= reach;
        }

        /// <summary>
        /// The ground as nature made it: the land with the streams' valleys and channels
        /// cut in, and the pool under the waterfall.
        /// </summary>
        public static float Natural(float x, float z)
        {
            var p = new Vector2(x, z);
            var h = Land(x, z);

            if (NearStream(p, 340, out var distance, out var bed, out var half, out var stream))
            {
                // The valley: a soft V round the stream.
                var valley = stream.Valley * (1 - SmoothStep(0, stream.ValleyWidth, distance));
                h -= valley * valley / stream.Valley;

                // The channel: banks down to the bed at one slope (~29°) all the way up to the land, however deep the bed
                // lies — where a stream runs round a village over higher ground its bed (never rising) lies deep, and banks
                // that ended 22 m out made a slot with sheer walls a trail fell into. Now it is a wide valley one can walk.
                var bank = bed + Mathf.Max(0, distance - half) * 0.55f;
                h = Mathf.Min(h, bank);
            }

            // The deep pool under the fall.
            var toPool = Vector2.Distance(p, QingMao.Pool);

            if (toPool < QingMao.PoolRadius + 40)
            {
                var floor = PoolLevel - 7 + toPool / QingMao.PoolRadius * 4;
                var shore = PoolLevel + 0.4f + Mathf.Max(0, toPool - QingMao.PoolRadius) * 0.18f;
                var dip = toPool < QingMao.PoolRadius ? floor : shore;
                h = Mathf.Min(h, Mathf.Lerp(dip, h, SmoothStep(QingMao.PoolRadius, QingMao.PoolRadius + 40, toPool)));
            }

            return h;
        }

        static float poolLevel = float.NaN;

        /// <summary>The pool's water: below every bank round it, so it lies in its basin.</summary>
        public static float PoolLevel
        {
            get
            {
                if (float.IsNaN(poolLevel))
                {
                    var lowest = float.PositiveInfinity;

                    for (var i = 0; i < 36; i++)
                    {
                        var a = i * Mathf.PI / 18;
                        var at = QingMao.Pool + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (QingMao.PoolRadius + 12);
                        // Not the cliff side: there the water meets the rock.
                        if (Vector2.Dot(at - QingMao.Pool, Vector2.down) > 0.5f * (QingMao.PoolRadius + 12))
                        {
                            continue;
                        }

                        lowest = Mathf.Min(lowest, Land(at.x, at.y));
                    }

                    poolLevel = lowest - 1.2f;
                }

                return poolLevel;
            }
        }

        /// <summary>The water's surface over a stream's bed near a point (NaN away from water).</summary>
        public static float WaterAt(Vector2 p)
        {
            if (Vector2.Distance(p, QingMao.Pool) < QingMao.PoolRadius + 2)
            {
                return PoolLevel;
            }

            return NearStream(p, 30, out _, out var bed, out _, out _) ? bed + 0.9f : float.NaN;
        }
    }
}
