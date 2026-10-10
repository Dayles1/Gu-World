using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.World
{
    /// <summary>
    /// Qing Mao Mountain (青茅山, lore: loc_qing_mao_mountain) — the first arc's
    /// world: where things are and how high the ground is everywhere. The lore
    /// gives the places and how they sit (three villages: Gu Yue at the
    /// mountain's waist, Xiong on the fore mountain, Bai on the back one; the
    /// spring the clan settled for; a bamboo grove); every measure here — the
    /// mountain's size, the coordinates, the paths — is the game's own
    /// assumption (DATA_CONTRACT §7), to be tuned.
    ///
    /// The map is 1.6 km square, centred on the origin, Y up. The peak is to the
    /// north (+Z); the village faces south, down the slope. Pure functions of
    /// position, so the editor's builder (QingMaoBuilder) and the game agree.
    /// </summary>
    public static class QingMao
    {
        public const string Id = "qing_mao";
        public const string Title = "Гора Цинмао · деревня Гу Юэ";
        public const float Size = 1600;
        public const float Half = Size / 2;
        /// <summary>The terrain's height range, m (the peak is well under it).</summary>
        public const float MaxHeight = 700;

        public static readonly Vector2 Peak = new Vector2(0, 260);
        /// <summary>Gu Yue village: its middle and the radius of its walls.</summary>
        public static readonly Vector2 Village = new Vector2(0, -120);
        public const float VillageRadius = 92;
        /// <summary>The spirit spring the clan settled for, up the slope behind the village.</summary>
        public static readonly Vector2 Spring = new Vector2(-75, -5);
        /// <summary>The bamboo grove east of the village, and its radius.</summary>
        public static readonly Vector2 Grove = new Vector2(195, -110);
        public const float GroveRadius = 85;
        /// <summary>The fore mountain with the Xiong village, and the back mountain with the Bai village.</summary>
        public static readonly Vector2 Xiong = new Vector2(280, -460);
        public static readonly Vector2 Bai = new Vector2(-190, 600);
        public const float SmallVillageRadius = 42;

        /// <summary>The paths, as polylines on the ground (X, Z).</summary>
        public static readonly Vector2[][] Paths =
        {
            // South gate, down the slope to the fore mountain and the Xiong village.
            new[] { V(0, -212), V(18, -258), V(-12, -305), V(40, -355), V(120, -398), V(205, -430), V(260, -452) },
            // North gate to the spring.
            new[] { V(0, -28), V(-35, -14), V(-62, -6) },
            // North gate, zigzagging up to the peak.
            new[] { V(0, -28), V(14, 12), V(-34, 60), V(30, 112), V(-22, 168), V(16, 220), V(0, 255) },
            // East gate to the bamboo grove.
            new[] { V(92, -120), V(138, -114), V(185, -110) },
            // West gate to the river, and down along it.
            new[] { V(-92, -120), V(-128, -128), V(-162, -168), V(-196, -258), V(-250, -372) },
            // Over the top and down the back to the Bai village.
            new[] { V(0, 255), V(-58, 345), V(-118, 448), V(-160, 540), V(-180, 585) },
        };

        /// <summary>The river, from the spring down the west side and off the map.</summary>
        public static readonly Vector2[] River =
        {
            V(-75, -5), V(-108, -52), V(-142, -110), V(-168, -150), V(-205, -240), V(-262, -360), V(-335, -515), V(-430, -680), V(-520, -800),
        };

        public const float RiverHalfWidth = 5;
        public const float PathHalfWidth = 2.2f;

        static Vector2 V(float x, float z) => new Vector2(x, z);

        static float SmoothStep(float min, float max, float x)
        {
            if (x <= min) return 0;
            if (x >= max) return 1;
            x = (x - min) / (max - min);

            return x * x * (3 - 2 * x);
        }

        /// <summary>Layered noise, about -1…1.</summary>
        static float Fbm(float x, float z, int octaves)
        {
            float sum = 0, amplitude = 1, total = 0;

            for (var i = 0; i < octaves; i++)
            {
                sum += (Mathf.PerlinNoise(x + 31.7f * i, z - 17.3f * i) * 2 - 1) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                x *= 2.03f;
                z *= 2.03f;
            }

            return sum / total;
        }

        /// <summary>Sharp-crested noise for the mountain's ridges, 0…1.</summary>
        static float Ridged(float x, float z)
        {
            float sum = 0, amplitude = 1, total = 0;

            for (var i = 0; i < 4; i++)
            {
                var n = 1 - Mathf.Abs(Mathf.PerlinNoise(x + 7.1f * i, z + 3.9f * i) * 2 - 1);
                sum += n * n * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                x *= 2.1f;
                z *= 2.1f;
            }

            return sum / total;
        }

        /// <summary>The broad shape, without the small bumps: the mountain, the fore and back mountains.</summary>
        public static float Shape(float x, float z)
        {
            var d = Vector2.Distance(new Vector2(x, z), Peak);
            var main = 400 * Mathf.Exp(-Mathf.Pow(d / 360, 1.7f));
            var fore = 105 * Mathf.Exp(-(new Vector2(x, z) - (Xiong + V(0, -20))).sqrMagnitude / (160 * 160));
            var back = 175 * Mathf.Exp(-(new Vector2(x, z) - (Bai + V(-10, 40))).sqrMagnitude / (230 * 230));

            return 18 + Mathf.Max(main, Mathf.Max(fore, back) * 0.9f + main * 0.35f);
        }

        /// <summary>The ground without its small bumps: the shape, ridges near the top, rolling slopes lower down.</summary>
        public static float Coarse(float x, float z)
        {
            var d = Vector2.Distance(new Vector2(x, z), Peak);
            var ridges = Ridged(x * 0.0055f + 11, z * 0.0055f + 5) * 55 * Mathf.Exp(-Mathf.Pow(d / 420, 2));
            var hills = Fbm(x * 0.004f + 3, z * 0.004f + 9, 5) * 28;

            return Shape(x, z) + ridges + hills;
        }

        /// <summary>The ground as nature made it: the coarse ground and its small bumps.</summary>
        public static float Natural(float x, float z) => Coarse(x, z) + Fbm(x * 0.03f, z * 0.03f, 3) * 1.6f;

        static float villageLevel = float.NaN;

        /// <summary>The height the village is levelled to, at its middle.</summary>
        public static float VillageLevel => float.IsNaN(villageLevel) ? villageLevel = Natural(Village.x, Village.y) : villageLevel;

        static List<(Vector2 at, float bed)> riverBed;

        /// <summary>The river's bed every few metres, never rising downstream.</summary>
        static List<(Vector2 at, float bed)> RiverBed
        {
            get
            {
                if (riverBed != null)
                {
                    return riverBed;
                }

                riverBed = new List<(Vector2, float)>();
                var lowest = float.PositiveInfinity;

                for (var i = 0; i + 1 < River.Length; i++)
                {
                    var length = Vector2.Distance(River[i], River[i + 1]);
                    var steps = Mathf.CeilToInt(length / 4);

                    for (var s = 0; s < steps; s++)
                    {
                        var at = Vector2.Lerp(River[i], River[i + 1], (float)s / steps);
                        lowest = Mathf.Min(lowest, Natural(at.x, at.y) - 2.5f);
                        riverBed.Add((at, lowest));
                    }
                }

                return riverBed;
            }
        }

        /// <summary>How far a point is from the river, and the bed's height there.</summary>
        public static (float distance, float bed) ToRiver(Vector2 p)
        {
            var best = float.PositiveInfinity;
            var bed = 0f;
            var samples = RiverBed;

            for (var i = 0; i < samples.Count; i++)
            {
                var d = (samples[i].at - p).sqrMagnitude;

                if (d < best)
                {
                    best = d;
                    bed = samples[i].bed;
                }
            }

            return (Mathf.Sqrt(best), bed);
        }

        /// <summary>How far a point is from the nearest path.</summary>
        public static float ToPath(Vector2 p)
        {
            var best = float.PositiveInfinity;

            foreach (var path in Paths)
            {
                for (var i = 0; i + 1 < path.Length; i++)
                {
                    best = Mathf.Min(best, ToSegment(p, path[i], path[i + 1]));
                }
            }

            return best;
        }

        public static float ToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));

            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>
        /// The ground's height: nature's, with the village levelled (a gentle rise to the
        /// north), the two small villages levelled, the paths smoothed and the river cut in.
        /// </summary>
        public static float HeightAt(float x, float z)
        {
            var p = new Vector2(x, z);
            var h = Natural(x, z);

            // The paths: the small bumps smoothed away.
            var path = ToPath(p);
            h = Mathf.Lerp(h, Coarse(x, z), 1 - SmoothStep(PathHalfWidth, PathHalfWidth + 5, path));

            // The villages, levelled.
            h = Level(p, h, Village, VillageRadius, VillageLevel + (z - Village.y) * 0.04f);
            h = Level(p, h, Xiong, SmallVillageRadius, Natural(Xiong.x, Xiong.y));
            h = Level(p, h, Bai, SmallVillageRadius, Natural(Bai.x, Bai.y));

            // Towards the map's edge the land settles into the valley floor.
            var edge = Half - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            h = Mathf.Lerp(6, Mathf.Max(h, 6), SmoothStep(0, 140, edge));

            // The river's channel.
            var (distance, bed) = ToRiver(p);

            if (distance < RiverHalfWidth + 18)
            {
                var bank = bed + Mathf.Max(0, distance - RiverHalfWidth) * 0.45f;
                h = Mathf.Min(h, Mathf.Lerp(bank, h, SmoothStep(RiverHalfWidth + 6, RiverHalfWidth + 18, distance)));
            }

            return h;
        }

        static float Level(Vector2 p, float h, Vector2 centre, float radius, float level)
        {
            var w = 1 - SmoothStep(radius + 6, radius + 46, Vector2.Distance(p, centre));

            return Mathf.Lerp(h, level, w);
        }

        /// <summary>The water's surface over the river bed nearest a point.</summary>
        public static float WaterAt(Vector2 p) => ToRiver(p).bed + 1.2f;

        /// <summary>Where a new game begins: the square before the clan pavilion, facing south down the slope.</summary>
        public static LocationSettings Settings
        {
            get
            {
                var spawn = new Vector3(0, HeightAt(0, -132) + 0.3f, -132);

                return LocationSettings.FromMinMax(
                    Id,
                    Title,
                    new Vector3(-Half + 10, -50, -Half + 10),
                    new Vector3(Half - 10, MaxHeight, Half - 10),
                    spawn,
                    Mathf.PI);
            }
        }
    }
}
