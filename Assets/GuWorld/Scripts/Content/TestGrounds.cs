using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Content
{
    /// <summary>A solid box standing on the ground, centred on (x, z).</summary>
    public struct BlockObject
    {
        public string id;
        public float x, z, width, depth, height;
    }

    /// <summary>A rounded stone sitting on the ground at (x, z).</summary>
    public struct StoneObject
    {
        public string id;
        public float x, z, radius;
    }

    /// <summary>
    /// The proving ground: a field to try the engine on, with what the body's
    /// physics must cope with (ported from client/content/test-grounds.ts).
    ///
    /// - a gentle mound (~20° at its steepest) to walk up and down;
    /// - a steep mound (~58°) to slide off — steeper than one can stand on;
    /// - five steps up to a platform, a crate to climb onto, a wall too high
    ///   to climb (placed by hand, near the spawn);
    /// - stones scattered over the whole field: one chance per cell of a fixed
    ///   16 m grid, seeded by the cell — the field is the same every time.
    ///
    /// Unity's world is the web's mirrored left to right (X negated, as glTFast
    /// brings models in), so everything is laid out on the web's X and placed
    /// at -X: seen in the game, the field looks exactly as it does on the web.
    /// </summary>
    public static class TestGrounds
    {
        struct Mound
        {
            public float x, z, height, radius;
        }

        static readonly Mound GentleMound = new Mound { x = 24, z = -24, height = 6, radius = 14 };
        static readonly Mound SteepMound = new Mound { x = -24, z = -24, height = 8, radius = 4.2f };

        const float StepRise = 0.3f;
        /// <summary>The scattering grid for stones, metres (fixed: ids name its cells).</summary>
        const int StoneCell = 16;
        const float StoneChance = 0.35f;
        /// <summary>No stones this close to the spawn and the hand-placed blocks.</summary>
        const float ClearRadius = 22;

        public static readonly BlockObject[] Blocks = BuildBlocks();

        static BlockObject[] BuildBlocks()
        {
            var blocks = new List<BlockObject>();

            for (var n = 1; n <= 5; n++)
            {
                blocks.Add(new BlockObject
                {
                    id = $"block:test_grounds:step_{n}",
                    x = -(6 + n * 0.6f),
                    z = -4,
                    width = 0.6f,
                    depth = 2.4f,
                    height = n * StepRise,
                });
            }

            blocks.Add(new BlockObject { id = "block:test_grounds:platform", x = -11.4f, z = -4, width = 3, depth = 3, height = 5 * StepRise });
            blocks.Add(new BlockObject { id = "block:test_grounds:crate", x = 6, z = -6, width = 1.4f, depth = 1.4f, height = 1.2f });
            blocks.Add(new BlockObject { id = "block:test_grounds:wall", x = 12, z = 4, width = 0.6f, depth = 8, height = 3.5f });

            return blocks.ToArray();
        }

        /// <summary>The ground's height at (x, z): two smooth round hills on a flat field.</summary>
        public static float HeightAt(float x, float z) =>
            MoundAt(-x, z, GentleMound) + MoundAt(-x, z, SteepMound);

        static float MoundAt(float x, float z, Mound mound)
        {
            var distance = (x - mound.x) * (x - mound.x) + (z - mound.z) * (z - mound.z);

            return mound.height * Mathf.Exp(-distance / (mound.radius * mound.radius));
        }

        /// <summary>A number in [0, 1) from a cell and a salt — the same every time (same bits as the TS hash).</summary>
        static double Hash(int gx, int gz, int salt)
        {
            unchecked
            {
                var h = (gx * 374761393) ^ (gz * 668265263) ^ salt;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                h ^= (int)((uint)h >> 16);

                return (uint)h / 4294967296.0;
            }
        }

        /// <summary>The stone of a cell of the scattering grid, if it has one.</summary>
        static bool TryStoneOf(int gx, int gz, out StoneObject stone)
        {
            stone = default;

            if (Hash(gx, gz, 11) >= StoneChance)
            {
                return false;
            }

            var x = (float)((gx + 0.1 + Hash(gx, gz, 23) * 0.8) * StoneCell);
            var z = (float)((gz + 0.1 + Hash(gx, gz, 37) * 0.8) * StoneCell);

            if (Mathf.Sqrt(x * x + z * z) < ClearRadius)
            {
                return false;
            }

            stone = new StoneObject
            {
                id = $"stone:test_grounds:g{gx}_{gz}",
                x = -x,
                z = z,
                radius = (float)(0.5 + Hash(gx, gz, 53) * 0.9),
            };

            return true;
        }

        /// <summary>The stones whose anchor (in Unity's X) is in [minX, maxX) × [minZ, maxZ).</summary>
        public static List<StoneObject> StonesIn(float minX, float maxX, float minZ, float maxZ)
        {
            var found = new List<StoneObject>();

            // The grid is the web's: its cells cover the mirrored span [-maxX, -minX].
            for (var gx = Mathf.FloorToInt(-maxX / StoneCell); gx * StoneCell <= -minX; gx++)
            {
                for (var gz = Mathf.FloorToInt(minZ / StoneCell); gz * StoneCell < maxZ; gz++)
                {
                    if (TryStoneOf(gx, gz, out var stone) &&
                        stone.x >= minX && stone.x < maxX && stone.z >= minZ && stone.z < maxZ)
                    {
                        found.Add(stone);
                    }
                }
            }

            return found;
        }
    }
}
