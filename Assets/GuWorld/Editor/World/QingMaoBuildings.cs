using GuWorld.World;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// The buildings of Qing Mao Mountain, after the art direction's houses sheet
    /// (Reference/Design/project/Houses.dc.html): the Gu Yue's dark timber halls with
    /// blue-grey tiles on hollow roofs whose eaves sweep up at the corners, round "moon"
    /// gates, lattice windows and the silver crescent on ridges, banners and plaques;
    /// the lore's bamboo houses (loc_gu_yue_village: dark green bamboo, many of them)
    /// on stilts under those roofs. The Bai build whitewashed walls under plain slate,
    /// the Xiong logs under thatch. Each is built here into a MeshKit, its front to −Z
    /// for the great buildings and +Z for the houses, as the village is laid out.
    /// </summary>
    public static partial class QingMaoBuilder
    {
        static readonly Vector3 Up = Vector3.up;

        // ── Parts ─────────────────────────────────────────────────────

        /// <summary>A round column from `foot` up `height`, on a stone base.</summary>
        static void Column(MeshKit kit, Vector3 foot, float height, float radius, Material material, bool stoneBase = true)
        {
            if (stoneBase)
            {
                kit.Cylinder(Stone, foot - Up * 0.05f, foot + Up * 0.24f, radius * 1.65f, radius * 1.45f, 8);
            }

            kit.Cylinder(material, foot, foot + Up * height, radius, radius * 0.93f, 8);
        }

        /// <summary>A bracket set on a column's top: blocks stacked and crossed, widening up to the eaves.</summary>
        static void Bracket(MeshKit kit, Vector3 top, float yaw = 0)
        {
            kit.Box(DarkWood, top + Up * 0.08f, new Vector3(0.42f, 0.16f, 0.42f), yaw);
            kit.Box(DarkWood, top + Up * 0.23f, new Vector3(0.95f, 0.14f, 0.26f), yaw);
            kit.Box(DarkWood, top + Up * 0.23f, new Vector3(0.26f, 0.14f, 0.95f), yaw);
            kit.Box(DarkWood, top + Up * 0.37f, new Vector3(1.35f, 0.14f, 0.3f), yaw);
        }

        /// <summary>A paper lantern hanging from `hook` (its glow is the lanterns' at night).</summary>
        static void HangingLantern(MeshKit kit, Vector3 hook, float size = 1)
        {
            var body = hook - Up * (0.45f * size);
            kit.Box(DarkWood, hook - Up * 0.1f * size, new Vector3(0.03f, 0.2f * size, 0.03f));
            kit.Cylinder(DarkWood, body + Up * 0.24f * size, body + Up * 0.3f * size, 0.12f * size, 0.12f * size, 8);
            kit.Cylinder(Lantern, body - Up * 0.22f * size, body + Up * 0.24f * size, 0.19f * size, 0.19f * size, 10);
            kit.Cylinder(Lantern, body - Up * 0.28f * size, body - Up * 0.22f * size, 0.13f * size, 0.19f * size, 10);
            kit.Cylinder(DarkWood, body - Up * 0.34f * size, body - Up * 0.28f * size, 0.12f * size, 0.12f * size, 8);
            kit.Box(Lacquer, body - Up * 0.48f * size, new Vector3(0.04f, 0.26f * size, 0.04f));
        }

        /// <summary>A clan banner on a pole, `height` tall: indigo cloth with the silver crescent.</summary>
        static void ClanBanner(MeshKit kit, Vector3 foot, float height, float yaw = 0)
        {
            var turn = Quaternion.Euler(0, yaw, 0);
            kit.Cylinder(Stone, foot - Up * 0.3f, foot + Up * 0.35f, 0.32f, 0.28f, 8);
            kit.Cylinder(DarkWood, foot, foot + Up * height, 0.09f, 0.07f, 8);
            kit.Blob(Bronze, foot + Up * (height + 0.08f), Vector3.one * 0.14f, 3);
            kit.Box(DarkWood, foot + turn * new Vector3(0.6f, height - 0.15f, 0), new Vector3(1.3f, 0.08f, 0.08f), yaw);
            var cloth = foot + turn * new Vector3(0.62f, height - 1.75f, 0);
            kit.Box(Banner, cloth, new Vector3(1.05f, 3.1f, 0.04f), yaw);
            // The swallow-tailed foot of the cloth.
            kit.Box(Banner, cloth + turn * new Vector3(-0.3f, -1.75f, 0), new Vector3(0.42f, 0.42f, 0.04f), yaw);
            kit.Box(Banner, cloth + turn * new Vector3(0.3f, -1.75f, 0), new Vector3(0.42f, 0.42f, 0.04f), yaw);
            kit.Crescent(Silver, cloth + Up * 0.55f, 0.36f, 0.07f, 0, yaw);
        }

        /// <summary>A board over a door: dark, framed in bronze, the crescent on it (facing −Z).</summary>
        static void Plaque(MeshKit kit, Vector3 centre, float width, float height)
        {
            kit.Box(Bronze, centre + Vector3.forward * 0.02f, new Vector3(width + 0.16f, height + 0.16f, 0.1f));
            kit.Box(DarkWood, centre - Vector3.forward * 0.03f, new Vector3(width, height, 0.1f));
            kit.Crescent(Silver, centre - Vector3.forward * 0.1f, height * 0.3f, 0.04f);
        }

        /// <summary>A crescent standing on a ridge at `top`, horns up, on a short bronze stem.</summary>
        static void Finial(MeshKit kit, Vector3 top, float size)
        {
            kit.Cylinder(Bronze, top, top + Up * size * 0.7f, size * 0.12f, size * 0.07f, 8);
            kit.Crescent(Silver, top + Up * size * 1.25f, size * 0.6f, size * 0.12f, 90);
        }

        /// <summary>A rail round a rectangle (`width` × `depth` about `centre` at its floor), open on the front (−Z or +Z) across `gap`.</summary>
        static void Railing(MeshKit kit, Vector3 centre, float width, float depth, float gap, float frontZ, Material posts, Material rails)
        {
            const float height = 0.85f;
            var corners = new[]
            {
                new Vector3(-width / 2, 0, -depth / 2), new Vector3(width / 2, 0, -depth / 2),
                new Vector3(width / 2, 0, depth / 2), new Vector3(-width / 2, 0, depth / 2),
            };

            for (var c = 0; c < 4; c++)
            {
                var a = corners[c];
                var b = corners[(c + 1) % 4];
                var front = Mathf.Abs(a.z - b.z) < 0.01f && Mathf.Sign(a.z) == Mathf.Sign(frontZ) && gap > 0;
                var length = Vector3.Distance(a, b);
                var along = (b - a) / length;
                var count = Mathf.Max(1, Mathf.RoundToInt(length / 1.1f));

                for (var i = 0; i <= count; i++)
                {
                    var p = a + along * (length * i / count);

                    if (front && Mathf.Abs(p.x) < gap / 2)
                    {
                        continue;
                    }

                    kit.Box(posts, centre + p + Up * height / 2, new Vector3(0.1f, height, 0.1f));
                }

                void Rail(Vector3 from, Vector3 to)
                {
                    var middle = (from + to) / 2;
                    var size = new Vector3(Mathf.Max(0.07f, Mathf.Abs(to.x - from.x)), 0.07f, Mathf.Max(0.07f, Mathf.Abs(to.z - from.z)));
                    kit.Box(rails, centre + middle + Up * height, size);
                    kit.Box(rails, centre + middle + Up * 0.25f, new Vector3(size.x, 0.05f, size.z));
                }

                if (front)
                {
                    var left = a.x < b.x ? a : b;
                    var right = a.x < b.x ? b : a;
                    Rail(left, new Vector3(-gap / 2, 0, left.z));
                    Rail(new Vector3(gap / 2, 0, right.z), right);
                }
                else
                {
                    Rail(a, b);
                }
            }
        }

        /// <summary>Steps up to `height` in front of a face at `frontZ` (going out along `direction`), `width` wide.</summary>
        static void Steps(MeshKit kit, float frontZ, float direction, float height, float width, Material material, int count = 0)
        {
            count = count > 0 ? count : Mathf.Max(2, Mathf.CeilToInt(height / 0.22f));
            var rise = height / count;

            for (var i = 0; i < count; i++)
            {
                var top = height - rise * i;
                var z = frontZ + direction * (0.18f + 0.36f * i);
                kit.Box(material, new Vector3(0, top - rise / 2 - 0.6f, z), new Vector3(width + 0.3f * i, rise + 1.2f, 0.38f));
            }
        }

        /// <summary>
        /// The timber frame of a storey `width` × `depth` from `floor` up `height`: posts at the corners
        /// (and every `bay` along the walls, where no window or door is in the way: 0 for none), a sill
        /// and a head beam all round, a little proud of the walls.
        /// </summary>
        static void Frame(MeshKit kit, float width, float depth, float floor, float height, float bay, Material posts)
        {
            const float post = 0.2f;
            const float proud = 0.05f;

            foreach (var (length, across, alongX) in new[] { (width, depth, true), (depth, width, false) })
            {
                var count = bay > 0 ? Mathf.Max(1, Mathf.RoundToInt(length / bay)) : 1;

                foreach (var s in new[] { -1, 1 })
                {
                    for (var i = 0; i <= count; i++)
                    {
                        var t = -length / 2 + length * i / count;
                        var at = alongX ? new Vector3(t, floor + height / 2, s * (across / 2 + proud)) : new Vector3(s * (across / 2 + proud), floor + height / 2, t);
                        kit.Box(posts, at, new Vector3(post, height, post));
                    }

                    var mid = alongX ? new Vector3(0, 0, s * (across / 2 + proud)) : new Vector3(s * (across / 2 + proud), 0, 0);
                    var size = alongX ? new Vector3(length + post, 0.2f, 0.16f) : new Vector3(0.16f, 0.2f, length + post);
                    kit.Box(posts, mid + Up * (floor + height - 0.1f), size);
                    kit.Box(posts, mid + Up * (floor + 0.1f), size);
                }
            }
        }

        // ── Houses ────────────────────────────────────────────────────

        /// <summary>The houses a village builds: a few kinds, each its own mesh (door to +Z), and their depths.</summary>
        static ((Mesh, Material[]) mesh, float depth)[] HouseKinds(Clan clan) => clan switch
        {
            Clan.Bai => new[]
            {
                (Make("Bai House A", kit => WhiteHouse(kit, 7, 5)), 5f),
                (Make("Bai House B", kit => WhiteHouse(kit, 9, 6)), 6f),
            },
            Clan.Xiong => new[]
            {
                (Make("Xiong House A", kit => LogHouse(kit, 7, 5)), 5f),
                (Make("Xiong House B", kit => LogHouse(kit, 8.5f, 6)), 6f),
            },
            _ => new[]
            {
                (Make("House A", kit => BambooHouse(kit, 7, 5, false, false)), 5f),
                (Make("House B", kit => BambooHouse(kit, 9, 6, false, false)), 6f),
                (Make("House C", kit => BambooHouse(kit, 6, 4.5f, false, true)), 4.5f),
                (Make("House D", kit => BambooHouse(kit, 7.5f, 6, true, false)), 6f),
            },
        };

        /// <summary>
        /// A Gu Yue bamboo house on stilts, its door to +Z: a porch with a rail and steps, a dark
        /// timber frame round walls of green bamboo, a lattice door and windows, a lantern by the
        /// door, and a tiled roof sweeping up at the corners (thatch on the poorest); two storeys
        /// with a skirt of eaves between them, or one.
        /// </summary>
        static void BambooHouse(MeshKit kit, float width, float depth, bool twoStoreys, bool thatched)
        {
            const float floor = 1.1f;
            const float storey = 2.7f;
            var roof = thatched ? Thatch : Tiles;
            var ribs = thatched ? 0 : 0.34f;

            // Stilts, deep enough to reach the ground on any slope.
            var columns = width > 7.2f ? 3 : 2;

            for (var i = 0; i <= columns; i++)
            {
                for (var k = 0; k <= 1; k++)
                {
                    var x = -width / 2 + 0.3f + i * (width - 0.6f) / columns;
                    var z = -depth / 2 + 0.3f + k * (depth - 0.6f);
                    kit.Cylinder(DarkWood, new Vector3(x, -3, z), new Vector3(x, floor, z), 0.14f, 0.13f, 6);
                }
            }

            // The floor, its porch in front, and steps down.
            kit.Box(Wood, new Vector3(0, floor, 0.75f), new Vector3(width + 0.4f, 0.22f, depth + 1.9f));
            kit.Box(DarkWood, new Vector3(0, floor - 0.17f, 0.75f), new Vector3(width + 0.5f, 0.14f, depth + 2));
            Steps(kit, depth / 2 + 1.7f, 1, floor, 1.4f, Wood, 4);
            Railing(kit, new Vector3(0, floor + 0.11f, depth / 2 + 0.85f), width + 0.3f, 1.7f, 1.6f, 1, DarkWood, Wood);

            // The walls in their frame.
            kit.Box(Bamboo, new Vector3(0, floor + storey / 2, 0), new Vector3(width, storey, depth));
            Frame(kit, width, depth, floor, storey, 0, DarkWood);

            // The door: two leaves, latticed above, panelled below.
            var front = depth / 2 + 0.07f;
            kit.Box(DarkWood, new Vector3(0, floor + 1.1f, front), new Vector3(1.5f, 2.2f, 0.08f));

            foreach (var x in new[] { -0.33f, 0.33f })
            {
                kit.Lattice(DarkWood, Paper, new Vector3(x, floor + 1.55f, front + 0.04f), 0.46f, 0.8f, 0, 0.16f);
                kit.Box(Wood, new Vector3(x, floor + 0.55f, front + 0.05f), new Vector3(0.5f, 0.85f, 0.04f));
            }

            // Windows: two in front, one on each side, one at the back.
            kit.Lattice(DarkWood, Paper, new Vector3(-width / 4 - 0.45f, floor + 1.6f, front), 1.2f, 0.85f);
            kit.Lattice(DarkWood, Paper, new Vector3(width / 4 + 0.45f, floor + 1.6f, front), 1.2f, 0.85f);
            kit.Lattice(DarkWood, Paper, new Vector3(width / 2 + 0.07f, floor + 1.6f, 0), 1.3f, 0.85f, 90);
            kit.Lattice(DarkWood, Paper, new Vector3(-width / 2 - 0.07f, floor + 1.6f, 0), 1.3f, 0.85f, -90);
            kit.Lattice(DarkWood, Paper, new Vector3(0, floor + 1.6f, -depth / 2 - 0.07f), 1.4f, 0.85f, 180);

            var eaves = floor + storey;

            if (twoStoreys)
            {
                kit.CurvedRoof(roof, RoofUnder, Ridge, new Vector3(0, eaves, 0), width, depth, 0.75f, 0.85f, 0.35f, 0, ribs);
                var upperWidth = width - 1.3f;
                var upperDepth = depth - 1.1f;
                const float upper = 2.3f;
                kit.Box(Bamboo, new Vector3(0, eaves + upper / 2, 0), new Vector3(upperWidth, upper, upperDepth));
                Frame(kit, upperWidth, upperDepth, eaves, upper, 0, DarkWood);
                kit.Lattice(DarkWood, Paper, new Vector3(0, eaves + 1.25f, upperDepth / 2 + 0.07f), 1.8f, 0.8f);
                kit.Lattice(DarkWood, Paper, new Vector3(0, eaves + 1.25f, -upperDepth / 2 - 0.07f), 1.4f, 0.8f, 180);
                eaves += upper;
                kit.CurvedRoof(roof, RoofUnder, Ridge, new Vector3(0, eaves, 0), upperWidth, upperDepth, 1.7f, 1, 0.6f, 0, ribs);
            }
            else
            {
                kit.CurvedRoof(roof, RoofUnder, Ridge, new Vector3(0, eaves, 0), width, depth, thatched ? 1.7f : 1.9f, 1.05f, thatched ? 0.3f : 0.6f, 0, ribs);
            }

            // A lantern hung under the front eave, by the door.
            HangingLantern(kit, new Vector3(1.1f, floor + storey - 0.05f, depth / 2 + 0.6f), 0.9f);
        }

        /// <summary>A Bai house: whitewashed walls on a grey stone footing, a plain slate roof, dark lattice (door to +Z).</summary>
        static void WhiteHouse(MeshKit kit, float width, float depth)
        {
            const float storey = 3;
            kit.Box(Stone, new Vector3(0, -0.6f, 0), new Vector3(width + 0.4f, 2, depth + 0.4f));
            kit.Box(BaiWall, new Vector3(0, 0.4f + storey / 2, 0), new Vector3(width, storey, depth));
            Frame(kit, width, depth, 0.4f, storey, 0, DarkWood);
            kit.Box(DarkWood, new Vector3(0, 1.5f, depth / 2 + 0.06f), new Vector3(1.2f, 2.2f, 0.08f));
            kit.Lattice(DarkWood, Paper, new Vector3(-width / 4 - 0.4f, 2.1f, depth / 2 + 0.06f), 1.1f, 0.9f, 0, 0.14f);
            kit.Lattice(DarkWood, Paper, new Vector3(width / 4 + 0.4f, 2.1f, depth / 2 + 0.06f), 1.1f, 0.9f, 0, 0.14f);
            kit.CurvedRoof(BaiRoof, RoofUnder, Ridge, new Vector3(0, 0.4f + storey, 0), width, depth, 2.3f, 0.7f, 0.05f, 0, 0.3f);
        }

        /// <summary>A Xiong house: log walls on boulders under a heavy thatch, a wide plank door (to +Z).</summary>
        static void LogHouse(MeshKit kit, float width, float depth)
        {
            const float storey = 2.6f;

            for (var i = 0; i < 6; i++)
            {
                kit.Blob(Stone, new Vector3(-width / 2 + i * width / 5, -0.2f, (i % 2 == 0 ? 1 : -1) * depth / 2), new Vector3(0.7f, 0.6f, 0.7f), i);
            }

            // Logs laid one on another, their ends crossing at the corners.
            for (var y = 0.2f; y < storey; y += 0.32f)
            {
                kit.Cylinder(Logs, new Vector3(-width / 2 - 0.3f, y, depth / 2), new Vector3(width / 2 + 0.3f, y, depth / 2), 0.17f, 0.17f, 6);
                kit.Cylinder(Logs, new Vector3(-width / 2 - 0.3f, y, -depth / 2), new Vector3(width / 2 + 0.3f, y, -depth / 2), 0.17f, 0.17f, 6);
                kit.Cylinder(Logs, new Vector3(width / 2, y + 0.16f, -depth / 2 - 0.3f), new Vector3(width / 2, y + 0.16f, depth / 2 + 0.3f), 0.17f, 0.17f, 6);
                kit.Cylinder(Logs, new Vector3(-width / 2, y + 0.16f, -depth / 2 - 0.3f), new Vector3(-width / 2, y + 0.16f, depth / 2 + 0.3f), 0.17f, 0.17f, 6);
            }

            kit.Box(Interior, new Vector3(0, storey / 2, 0), new Vector3(width - 0.1f, storey, depth - 0.1f));
            kit.Box(DarkWood, new Vector3(0, 1.05f, depth / 2 + 0.16f), new Vector3(1.5f, 2, 0.1f));
            kit.CurvedRoof(Thatch, RoofUnder, Thatch, new Vector3(0, storey + 0.1f, 0), width, depth, 2.2f, 0.9f, 0.15f, 0, 0);
        }

        // ── The great buildings ───────────────────────────────────────

        /// <summary>
        /// The clan pavilion: five storeys on a two-tiered stone podium, each ringed with red columns and
        /// lattice, a bracket set on every column, balconies with rails from the second storey up, and a
        /// sweeping roof over each; the top roof steep, a silver crescent over it. Banners and lanterns before
        /// the steps, the clan's plaque over the door (front to −Z).
        /// </summary>
        static void ClanPavilion(MeshKit kit)
        {
            kit.Box(Stone, new Vector3(0, -0.6f, 0), new Vector3(24, 2.2f, 21));
            kit.Box(Stone, new Vector3(0, 0.1f, 0), new Vector3(22, 2.2f, 19));
            kit.Box(Stone, new Vector3(0, 1.15f, 0), new Vector3(22.4f, 0.14f, 19.4f));
            Steps(kit, -9.5f, -1, 1.2f, 7, Stone, 6);
            Railing(kit, new Vector3(0, 1.2f, 0), 21.6f, 18.6f, 7.4f, -1, Stone, Stone);

            var y = 1.2f;

            for (var storey = 0; storey < 5; storey++)
            {
                var width = 15 - storey * 1.8f;
                var depth = 12.5f - storey * 1.5f;
                var height = storey == 0 ? 4.2f : 3.3f;
                var top = storey == 4;

                kit.Box(Interior, new Vector3(0, y + height / 2, 0), new Vector3(width - 0.5f, height, depth - 0.5f));

                // Columns round it; between them lattice over a low panelled wall (doors on the ground floor's front).
                var across = storey == 0 ? 6 : 5;
                var deep = storey == 0 ? 5 : 4;

                foreach (var (count, length, other, alongX) in new[] { (across, width, depth, true), (deep, depth, width, false) })
                {
                    var bay = length / count;

                    foreach (var s in new[] { -1, 1 })
                    {
                        for (var i = 0; i <= count; i++)
                        {
                            var t = -length / 2 + bay * i;
                            var at = alongX ? new Vector3(t, y, s * other / 2) : new Vector3(s * other / 2, y, t);
                            Column(kit, at, height, 0.24f, Lacquer, storey == 0);
                            Bracket(kit, at + Up * height, alongX ? 0 : 90);

                            if (i == count)
                            {
                                continue;
                            }

                            var middle = t + bay / 2;
                            var yaw = alongX ? (s < 0 ? 180 : 0) : (s < 0 ? -90 : 90);
                            var face = alongX ? new Vector3(middle, 0, s * other / 2) : new Vector3(s * other / 2, 0, middle);
                            var door = storey == 0 && alongX && s < 0 && (i == count / 2 - 1 || i == count / 2);

                            if (door)
                            {
                                kit.Box(DarkWood, face + Up * (y + 1.6f), new Vector3(bay - 0.5f, 3.2f, 0.12f), yaw);
                                kit.Lattice(DarkWood, Paper, face + Up * (y + 2.3f) + new Vector3(0, 0, -0.08f), bay - 0.9f, 1.3f, yaw, 0.18f);
                            }
                            else
                            {
                                var sill = 0.9f;
                                kit.Box(Wood, face + Up * (y + sill / 2), alongX ? new Vector3(bay - 0.4f, sill, 0.14f) : new Vector3(0.14f, sill, bay - 0.4f));
                                var pane = height - sill - 0.7f;
                                kit.Lattice(DarkWood, Paper, face + Up * (y + sill + pane / 2 + 0.1f), bay - 0.6f, pane, yaw, 0.2f);
                            }
                        }
                    }
                }

                // The head beams over the columns.
                kit.Box(Lacquer, new Vector3(0, y + height - 0.2f, -depth / 2), new Vector3(width + 0.3f, 0.32f, 0.3f));
                kit.Box(Lacquer, new Vector3(0, y + height - 0.2f, depth / 2), new Vector3(width + 0.3f, 0.32f, 0.3f));
                kit.Box(Lacquer, new Vector3(-width / 2, y + height - 0.2f, 0), new Vector3(0.3f, 0.32f, depth + 0.3f));
                kit.Box(Lacquer, new Vector3(width / 2, y + height - 0.2f, 0), new Vector3(0.3f, 0.32f, depth + 0.3f));

                if (storey == 0)
                {
                    Plaque(kit, new Vector3(0, y + height - 0.7f, -depth / 2 - 0.2f), 2.6f, 0.9f);
                }

                y += height + 0.45f;

                if (top)
                {
                    kit.CurvedRoof(Tiles, RoofUnder, Ridge, new Vector3(0, y + 0.05f, 0), width + 0.4f, depth + 0.4f, 4.2f, 2.3f, 1.7f);
                    Finial(kit, new Vector3(0, y + 4.45f, 0), 1.3f);
                }
                else
                {
                    kit.CurvedRoof(Tiles, RoofUnder, Ridge, new Vector3(0, y + 0.05f, 0), width + 0.4f, depth + 0.4f, 1.2f, 2.1f, 1.2f);

                    // Hanging lanterns at the eaves' corners.
                    foreach (var sx in new[] { -1, 1 })
                    {
                        foreach (var sz in new[] { -1, 1 })
                        {
                            HangingLantern(kit, new Vector3(sx * (width / 2 + 1.2f), y - 0.3f, sz * (depth / 2 + 1.2f)), 1.2f);
                        }
                    }

                    // The next storey's balcony, over the roof's upper part.
                    y += 0.95f;
                    var nextWidth = 15 - (storey + 1) * 1.8f;
                    var nextDepth = 12.5f - (storey + 1) * 1.5f;
                    kit.Box(Wood, new Vector3(0, y - 0.12f, 0), new Vector3(nextWidth + 1.5f, 0.24f, nextDepth + 1.5f));
                    kit.Box(DarkWood, new Vector3(0, y - 0.3f, 0), new Vector3(nextWidth + 1.6f, 0.14f, nextDepth + 1.6f));
                    Railing(kit, new Vector3(0, y, 0), nextWidth + 1.4f, nextDepth + 1.4f, 0, -1, Lacquer, DarkWood);
                }
            }

            ClanBanner(kit, new Vector3(-5.6f, 0.5f, -10.1f), 7.5f, 180);
            ClanBanner(kit, new Vector3(5.6f, 0.5f, -10.1f), 7.5f, 0);
        }

        /// <summary>
        /// The ancestral hall (the shrine): on a stone podium, a hall of plaster between red columns
        /// under two eaves, a moon gate in its front between lattice windows, the crescent on its ridge;
        /// a bronze incense burner and two stone lanterns before it (front to −Z).
        /// </summary>
        static void AncestralHall(MeshKit kit)
        {
            kit.Box(Stone, new Vector3(0, 0.1f, 0), new Vector3(12, 1.6f, 10));
            kit.Box(Stone, new Vector3(0, 0.88f, 0), new Vector3(12.3f, 0.12f, 10.3f));
            Steps(kit, -5, -1, 0.9f, 4, Stone, 4);

            const float floor = 0.9f;
            const float height = 3.8f;
            const float width = 9;
            const float depth = 7;

            kit.Box(Interior, new Vector3(0, floor + height / 2, 0), new Vector3(width - 0.4f, height, depth - 0.4f));
            kit.Box(Plaster, new Vector3(0, floor + height / 2, depth / 2 - 0.1f), new Vector3(width, height, 0.2f));
            kit.Box(Plaster, new Vector3(width / 2 - 0.1f, floor + height / 2, 0), new Vector3(0.2f, height, depth));
            kit.Box(Plaster, new Vector3(-width / 2 + 0.1f, floor + height / 2, 0), new Vector3(0.2f, height, depth));

            // The front: a moon gate in the middle bay, lattice windows in the side bays.
            kit.RoundOpening(Plaster, DarkWood, new Vector3(0, floor, -depth / 2 + 0.1f), 3.6f, height, 0.2f, 1.25f, 1.45f);
            kit.Box(Plaster, new Vector3(-3.15f, floor + height / 2, -depth / 2 + 0.1f), new Vector3(2.7f, height, 0.2f));
            kit.Box(Plaster, new Vector3(3.15f, floor + height / 2, -depth / 2 + 0.1f), new Vector3(2.7f, height, 0.2f));
            kit.Lattice(DarkWood, Paper, new Vector3(-3.15f, floor + 2, -depth / 2 - 0.02f), 1.5f, 1.4f, 180, 0.18f);
            kit.Lattice(DarkWood, Paper, new Vector3(3.15f, floor + 2, -depth / 2 - 0.02f), 1.5f, 1.4f, 180, 0.18f);

            foreach (var x in new[] { -4.5f, -1.8f, 1.8f, 4.5f })
            {
                Column(kit, new Vector3(x, floor, -depth / 2 - 0.25f), height, 0.24f, Lacquer);
                Bracket(kit, new Vector3(x, floor + height, -depth / 2 - 0.25f));
                Column(kit, new Vector3(x, floor, depth / 2 + 0.15f), height, 0.22f, Lacquer);
            }

            kit.Box(Lacquer, new Vector3(0, floor + height - 0.2f, -depth / 2 - 0.25f), new Vector3(width + 0.4f, 0.34f, 0.3f));
            Plaque(kit, new Vector3(0, floor + height + 0.15f, -depth / 2 - 0.5f), 2.2f, 0.75f);

            // Two eaves: a skirt round the hall, a raised upper roof.
            kit.CurvedRoof(Tiles, RoofUnder, Ridge, new Vector3(0, floor + height + 0.5f, 0), width, depth, 0.9f, 1.5f, 0.8f);
            const float upperWidth = 6.4f;
            const float upperDepth = 4.6f;
            var upper = floor + height + 1.05f;
            kit.Box(Plaster, new Vector3(0, upper + 0.7f, 0), new Vector3(upperWidth, 1.4f, upperDepth));
            Frame(kit, upperWidth, upperDepth, upper, 1.4f, 1.6f, DarkWood);
            kit.CurvedRoof(Tiles, RoofUnder, Ridge, new Vector3(0, upper + 1.45f, 0), upperWidth, upperDepth, 2.1f, 1.4f, 1.1f);
            Finial(kit, new Vector3(0, upper + 1.45f + 2.25f, 0), 0.9f);

            // The incense burner: a bronze cauldron on three legs, on a stone plinth.
            kit.Cylinder(Stone, new Vector3(0, -0.6f, -7.2f), new Vector3(0, 0.55f, -7.2f), 0.8f, 0.75f, 8);

            for (var i = 0; i < 3; i++)
            {
                var a = i * 2 * Mathf.PI / 3;
                var foot = new Vector3(Mathf.Cos(a) * 0.45f, 0.55f, -7.2f + Mathf.Sin(a) * 0.45f);
                kit.Cylinder(Bronze, foot, foot + new Vector3(-Mathf.Cos(a) * 0.1f, 0.5f, -Mathf.Sin(a) * 0.1f), 0.08f, 0.06f, 6);
            }

            kit.Cylinder(Bronze, new Vector3(0, 0.95f, -7.2f), new Vector3(0, 1.55f, -7.2f), 0.48f, 0.62f, 12);
            kit.Cylinder(Bronze, new Vector3(0, 1.55f, -7.2f), new Vector3(0, 1.62f, -7.2f), 0.68f, 0.68f, 12);
            kit.Box(Bronze, new Vector3(-0.55f, 1.8f, -7.2f), new Vector3(0.08f, 0.4f, 0.3f));
            kit.Box(Bronze, new Vector3(0.55f, 1.8f, -7.2f), new Vector3(0.08f, 0.4f, 0.3f));

            foreach (var x in new[] { -3.6f, 3.6f })
            {
                StoneLantern(kit, new Vector3(x, -0.4f, -6.4f));
            }
        }

        /// <summary>A stone lantern: a post, a lamp box with paper windows, a little hipped cap.</summary>
        static void StoneLantern(MeshKit kit, Vector3 foot)
        {
            kit.Box(Stone, foot + Up * 0.2f, new Vector3(0.7f, 0.4f, 0.7f));
            kit.Cylinder(Stone, foot + Up * 0.4f, foot + Up * 1.5f, 0.17f, 0.15f, 8);
            kit.Box(Stone, foot + Up * 1.55f, new Vector3(0.6f, 0.12f, 0.6f));
            kit.Box(Lantern, foot + Up * 1.85f, new Vector3(0.38f, 0.48f, 0.38f));

            foreach (var x in new[] { -0.2f, 0.2f })
            {
                foreach (var z in new[] { -0.2f, 0.2f })
                {
                    kit.Box(Stone, foot + new Vector3(x, 1.85f, z), new Vector3(0.08f, 0.5f, 0.08f));
                }
            }

            kit.Cylinder(Stone, foot + Up * 2.1f, foot + Up * 2.5f, 0.52f, 0.04f, 4);
        }

        /// <summary>
        /// The academy: on its stone platform, a lecture hall of red columns and lattice under a great
        /// roof with the crescent, lower wings on both sides, the academy's plaque over the doors (front −Z).
        /// </summary>
        static void AcademyHall(MeshKit kit)
        {
            kit.Box(Stone, new Vector3(0, 0, 0), new Vector3(21, 1.4f, 12.5f));
            kit.Box(Stone, new Vector3(0, 0.68f, 0), new Vector3(21.3f, 0.12f, 12.8f));
            Steps(kit, -6.25f, -1, 0.7f, 5, Stone, 3);

            const float floor = 0.7f;
            const float height = 4;
            const float width = 12;
            const float depth = 9;

            kit.Box(Plaster, new Vector3(0, floor + height / 2, 0.2f), new Vector3(width - 0.6f, height, depth - 0.8f));

            for (var c = 0; c <= 5; c++)
            {
                var x = -width / 2 + c * width / 5;
                Column(kit, new Vector3(x, floor, -depth / 2), height, 0.26f, Lacquer);
                Bracket(kit, new Vector3(x, floor + height, -depth / 2));

                if (c < 5)
                {
                    var middle = x + width / 10;
                    var face = new Vector3(middle, 0, -depth / 2 + 0.38f);

                    if (c == 2)
                    {
                        kit.Box(DarkWood, face + Up * (floor + 1.5f), new Vector3(1.9f, 3, 0.1f));
                        kit.Lattice(DarkWood, Paper, face + Up * (floor + 2.15f) + new Vector3(0, 0, -0.07f), 1.5f, 1.2f, 180, 0.18f);
                    }
                    else
                    {
                        kit.Box(Wood, face + Up * (floor + 0.45f), new Vector3(width / 5 - 0.4f, 0.9f, 0.12f));
                        kit.Lattice(DarkWood, Paper, face + Up * (floor + 2.2f) + new Vector3(0, 0, -0.07f), width / 5 - 0.6f, 2.2f, 180, 0.22f);
                    }
                }
            }

            kit.Box(Lacquer, new Vector3(0, floor + height - 0.2f, -depth / 2), new Vector3(width + 0.4f, 0.34f, 0.32f));
            Plaque(kit, new Vector3(0, floor + height + 0.15f, -depth / 2 - 0.3f), 2.8f, 0.85f);
            Frame(kit, width - 0.6f, depth - 0.8f, floor, height, 0, DarkWood);
            kit.CurvedRoof(Tiles, RoofUnder, Ridge, new Vector3(0, floor + height + 0.45f, 0), width, depth, 3, 1.5f, 1.1f);
            Finial(kit, new Vector3(0, floor + height + 0.45f + 3.15f, 0), 1);

            // The wings.
            foreach (var s in new[] { -1, 1 })
            {
                var centre = new Vector3(s * 8.3f, 0, 0.4f);
                const float wingHeight = 3.2f;
                kit.Box(Plaster, centre + Up * (floor + wingHeight / 2), new Vector3(4.2f, wingHeight, 7));
                Frame(kit, 4.2f, 7, floor, wingHeight, 0, DarkWood);
                kit.Lattice(DarkWood, Paper, centre + new Vector3(0, floor + 1.8f, -3.57f), 2.2f, 1.2f, 180, 0.2f);
                kit.CurvedRoof(Tiles, RoofUnder, Ridge, centre + Up * (floor + wingHeight), 4.2f, 7, 1.7f, 0.9f, 0.6f, 90);
                HangingLantern(kit, centre + new Vector3(-s * 1.4f, floor + wingHeight - 0.1f, -4.2f));
            }
        }

        /// <summary>The hostel: a long two-storey house, plaster below and bamboo above, rows of lattice windows (front −Z).</summary>
        static void HostelHouse(MeshKit kit)
        {
            kit.Box(Stone, new Vector3(0, 0, 0), new Vector3(15, 1.2f, 9));
            Steps(kit, -4.5f, -1, 0.6f, 2.4f, Stone, 3);
            const float floor = 0.6f;
            kit.Box(Plaster, new Vector3(0, floor + 1.7f, 0), new Vector3(14, 3.4f, 8));
            Frame(kit, 14, 8, floor, 3.4f, 2.8f, DarkWood);
            kit.CurvedRoof(Tiles, RoofUnder, Ridge, new Vector3(0, floor + 3.4f, 0), 14, 8, 0.8f, 1.1f, 0.45f);
            kit.Box(Bamboo, new Vector3(0, floor + 3.4f + 1.5f, 0), new Vector3(12.6f, 3, 6.8f));
            Frame(kit, 12.6f, 6.8f, floor + 3.4f, 3, 0, DarkWood);
            kit.CurvedRoof(Tiles, RoofUnder, Ridge, new Vector3(0, floor + 6.4f, 0), 12.6f, 6.8f, 2.3f, 1.3f, 0.9f);

            for (var c = 0; c < 5; c++)
            {
                var x = -5.6f + c * 2.8f;
                kit.Lattice(DarkWood, Paper, new Vector3(x, floor + 3.4f + 1.6f, -3.47f), 1.5f, 1.1f, 180, 0.18f);

                if (c == 2)
                {
                    kit.Box(DarkWood, new Vector3(x, floor + 1.15f, -4.06f), new Vector3(1.6f, 2.3f, 0.1f));
                    kit.Lattice(DarkWood, Paper, new Vector3(x, floor + 1.6f, -4.12f), 1.1f, 0.9f, 180, 0.16f);
                }
                else
                {
                    kit.Lattice(DarkWood, Paper, new Vector3(x, floor + 1.9f, -4.07f), 1.5f, 1.1f, 180, 0.18f);
                }
            }

            HangingLantern(kit, new Vector3(-1.4f, floor + 3.3f, -4.6f));
            HangingLantern(kit, new Vector3(1.4f, floor + 3.3f, -4.6f));
        }

        /// <summary>
        /// A gate of the village wall: stone towers either side (the wall runs into them), between them
        /// four red pillars under a beam with a bracketed, sweeping roof and the clan's plaque, the leaves
        /// swung open inward, lanterns hanging under the eave (the way through runs along Z).
        /// </summary>
        static void ClanGate(MeshKit kit)
        {
            foreach (var s in new[] { -1, 1 })
            {
                kit.Box(Stone, new Vector3(s * 5.4f, 2, 0), new Vector3(2.6f, 8, 3.2f));
                kit.Box(Plaster, new Vector3(s * 5.4f, 6.6f, 0), new Vector3(2.3f, 1.2f, 2.9f));
                kit.CurvedRoof(Tiles, RoofUnder, Ridge, new Vector3(s * 5.4f, 7.2f, 0), 2.3f, 2.9f, 0.9f, 0.55f, 0.45f, 90, 0.3f);

                foreach (var z in new[] { -0.9f, 0.9f })
                {
                    Column(kit, new Vector3(s * 3.9f, -1, z), 8.6f, 0.3f, Lacquer, false);
                    kit.Cylinder(Stone, new Vector3(s * 3.9f, -1, z), new Vector3(s * 3.9f, 0.5f, z), 0.48f, 0.42f, 8);
                }

                // A leaf, swung open inward.
                kit.Box(DarkWood, new Vector3(s * 3.4f, 2.9f, 2.4f), new Vector3(0.2f, 5.6f, 2.9f));
            }

            kit.Box(Lacquer, new Vector3(0, 6.9f, -0.9f), new Vector3(8.6f, 0.45f, 0.36f));
            kit.Box(Lacquer, new Vector3(0, 6.9f, 0.9f), new Vector3(8.6f, 0.45f, 0.36f));
            kit.Box(DarkWood, new Vector3(0, 7.45f, 0), new Vector3(9, 0.5f, 2.3f));

            for (var i = -3; i <= 3; i++)
            {
                Bracket(kit, new Vector3(i * 1.25f, 7.7f, -0.9f));
                Bracket(kit, new Vector3(i * 1.25f, 7.7f, 0.9f));
            }

            Plaque(kit, new Vector3(0, 5.95f, -1.1f), 2.6f, 1);
            kit.Box(DarkWood, new Vector3(0, 5.95f, 1.06f), new Vector3(2.6f, 1, 0.1f));
            kit.Crescent(Silver, new Vector3(0, 5.95f, 1.13f), 0.3f, 0.04f);
            kit.CurvedRoof(Tiles, RoofUnder, Ridge, new Vector3(0, 8.2f, 0), 8.6f, 2.4f, 1.7f, 1.2f, 1.1f);
            Finial(kit, new Vector3(0, 8.2f + 1.85f, 0), 0.9f);

            foreach (var x in new[] { -2.4f, 2.4f })
            {
                HangingLantern(kit, new Vector3(x, 7.1f, -1.4f), 1.1f);
                HangingLantern(kit, new Vector3(x, 7.1f, 1.4f), 1.1f);
            }
        }

        /// <summary>A length of the village wall: a stone footing, plaster above with a timber band, a walk on top behind merlons.</summary>
        static void WallSegment(MeshKit kit, float length)
        {
            kit.Box(Stone, new Vector3(0, 0.2f, 0), new Vector3(length + 0.1f, 3.2f, 1.6f));
            kit.Box(Plaster, new Vector3(0, 3.55f, 0), new Vector3(length + 0.1f, 3.5f, 0.9f));
            kit.Box(DarkWood, new Vector3(0, 5.25f, 0), new Vector3(length + 0.12f, 0.3f, 0.96f));

            for (var i = 0; i < 4; i++)
            {
                var x = -length / 2 + (i + 0.5f) * length / 4;
                kit.Box(Plaster, new Vector3(x, 5.75f, -0.25f), new Vector3(0.9f, 0.7f, 0.4f));
                kit.Box(Ridge, new Vector3(x, 6.15f, -0.25f), new Vector3(1.05f, 0.12f, 0.55f));
            }
        }

        /// <summary>A watchtower on the wall: a stone base, a plastered storey under an eave, a lookout with rails under a sweeping roof.</summary>
        static void Watchtower(MeshKit kit)
        {
            kit.Box(Stone, new Vector3(0, 1, 0), new Vector3(5, 4, 5));
            kit.Box(Plaster, new Vector3(0, 4.6f, 0), new Vector3(4.4f, 3.2f, 4.4f));
            Frame(kit, 4.4f, 4.4f, 3, 3.2f, 0, DarkWood);

            for (var side = 0; side < 4; side++)
            {
                var yaw = side * 90;
                kit.Lattice(DarkWood, Paper, Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, 2.27f) + Up * 4.9f, 0.9f, 0.7f, yaw, 0.15f);
            }

            kit.CurvedRoof(Tiles, RoofUnder, Ridge, new Vector3(0, 6.2f, 0), 4.4f, 4.4f, 0.6f, 0.8f, 0.45f);
            kit.Box(Wood, new Vector3(0, 6.95f, 0), new Vector3(5, 0.22f, 5));

            foreach (var x in new[] { -2.1f, 2.1f })
            {
                foreach (var z in new[] { -2.1f, 2.1f })
                {
                    Column(kit, new Vector3(x, 7.05f, z), 2.3f, 0.15f, Lacquer, false);
                    HangingLantern(kit, new Vector3(x * 1.3f, 9.2f, z * 1.3f), 0.9f);
                }
            }

            Railing(kit, new Vector3(0, 7.06f, 0), 4.6f, 4.6f, 0, -1, DarkWood, DarkWood);
            kit.CurvedRoof(Tiles, RoofUnder, Ridge, new Vector3(0, 9.35f, 0), 4.2f, 4.2f, 1.9f, 1, 0.8f);
        }

        /// <summary>A lantern post along the streets: a stone foot, a dark post, an arm with a paper lantern.</summary>
        static void LanternPost(MeshKit kit)
        {
            kit.Cylinder(Stone, new Vector3(0, -0.3f, 0), new Vector3(0, 0.3f, 0), 0.25f, 0.2f, 6);
            kit.Box(DarkWood, new Vector3(0, 1.5f, 0), new Vector3(0.14f, 3, 0.14f));
            kit.Box(DarkWood, new Vector3(0.38f, 2.9f, 0), new Vector3(0.9f, 0.1f, 0.1f));
            kit.Box(Ridge, new Vector3(0, 3.05f, 0), new Vector3(0.24f, 0.1f, 0.24f));
            HangingLantern(kit, new Vector3(0.7f, 2.85f, 0), 0.9f);
        }

        /// <summary>The square's well: a stone ring, two posts with a windlass and a little sweeping roof.</summary>
        static void VillageWell(MeshKit kit)
        {
            kit.Cylinder(Stone, new Vector3(0, -0.5f, 0), new Vector3(0, 0.8f, 0), 1.1f, 1.1f, 12);
            kit.Cylinder(Stone, new Vector3(0, 0.8f, 0), new Vector3(0, 0.9f, 0), 1.18f, 1.18f, 12);
            kit.Cylinder(Mat("Well Water", 0x1e2a2c, 0.8f), new Vector3(0, 0.6f, 0), new Vector3(0, 0.7f, 0), 0.9f, 0.9f, 12);

            foreach (var x in new[] { -1, 1 })
            {
                kit.Box(DarkWood, new Vector3(x * 1.05f, 1.6f, 0), new Vector3(0.16f, 2, 0.16f));
            }

            kit.Cylinder(Wood, new Vector3(-1, 1.9f, 0), new Vector3(1, 1.9f, 0), 0.1f, 0.1f, 8);
            kit.CurvedRoof(Tiles, RoofUnder, Ridge, new Vector3(0, 2.6f, 0), 2.2f, 1.4f, 0.8f, 0.5f, 0.4f, 0, 0.28f);
        }

        /// <summary>A small village's hall, as its clan builds it.</summary>
        static void VillageHall(MeshKit kit, Clan clan)
        {
            kit.Box(Stone, Vector3.zero, new Vector3(13, 1.2f, 10));

            if (clan == Clan.Xiong)
            {
                LogHouse(kit, 11, 8);

                return;
            }

            var wall = clan == Clan.Bai ? BaiWall : Plaster;
            kit.Box(wall, new Vector3(0, 2.4f, 0), new Vector3(12, 3.6f, 9));
            Frame(kit, 12, 9, 0.6f, 3.6f, 3, DarkWood);
            kit.CurvedRoof(clan == Clan.Bai ? BaiRoof : Tiles, RoofUnder, Ridge, new Vector3(0, 4.2f, 0), 12, 9, 3, 1.4f, clan == Clan.Bai ? 0.05f : 1);
        }
    }
}
