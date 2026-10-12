using System.Collections.Generic;
using System.Linq;
using GuWorld.World;
using UnityEditor;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// The villages' ordinary buildings, made from a recipe each so no two lanes look
    /// alike: a catalogue of designs per clan and trade — sizes, one to three storeys,
    /// walls of bamboo (new and weathered), lime plaster, mud, planks or logs, curved
    /// hipped roofs of tiles (new, worn, mossy), plain gables, lean-tos and thatch,
    /// porches, galleries, side sheds, outside stairs, shop fronts and signs — each a
    /// little crooked, as old houses are: its walls leaning a degree or two, its
    /// ridge sagging. A design is built once (front to +Z, its floor at 0) with a
    /// plain far shape whose windows still glow at night, and every house picks the
    /// design nearest its plot and is stretched a little to fit.
    /// </summary>
    public static partial class QingMaoBuilder
    {
        public class Design
        {
            public string Name;
            public Clan Clan;
            public BuildingKind Kind;
            public Vector2 Size;
            public float Height;
            public (Mesh mesh, Material[] materials) Near;
            public (Mesh mesh, Material[] materials) Far;
            /// <summary>One can go in: its ground storey is a room (walls with a door's gap, furniture), its height and the door's place.</summary>
            public bool Open;
            public float Storey;
            public float DoorAt;
        }

        /// <summary>The trades one goes into: a tavern, a teahouse, an inn, a shop, the herbalist, the gambling house.</summary>
        static bool Enterable(BuildingKind kind) => kind is BuildingKind.Tavern or BuildingKind.Teahouse or BuildingKind.Inn or BuildingKind.Shop or BuildingKind.Herbalist or BuildingKind.Gambling;

        static readonly Dictionary<(Clan, BuildingKind), List<Design>> catalogue = new Dictionary<(Clan, BuildingKind), List<Design>>();

        /// <summary>The design nearest a plot: its kind (or the nearest kind there is), its size the closest.</summary>
        static Design DesignFor(Clan clan, BuildingKind kind, Vector2 size, int seed)
        {
            if (!catalogue.TryGetValue((clan, kind), out var list) || list.Count == 0)
            {
                list = Catalogue(clan, kind);
            }

            // Among the few nearest in size, one chosen by the house's own seed.
            var near = list.OrderBy(d => Mathf.Abs(Mathf.Log(d.Size.x / size.x)) + Mathf.Abs(Mathf.Log(d.Size.y / size.y))).Take(3).ToList();

            return near[Mathf.Abs(seed) % near.Count];
        }

        static List<Design> Catalogue(Clan clan, BuildingKind kind)
        {
            var list = new List<Design>();
            catalogue[(clan, kind)] = list;
            var count = kind switch
            {
                BuildingKind.Family => 22,
                BuildingKind.Poor => 12,
                BuildingKind.Shed => 6,
                BuildingKind.Shop => 9,
                BuildingKind.Tavern => 4,
                _ => 2,
            };
            var random = new System.Random((int)clan * 1000 + (int)kind * 37 + 5);

            for (var i = 0; i < count; i++)
            {
                list.Add(MakeDesign(clan, kind, i, random));
            }

            return list;
        }

        /// <summary>What a house is made of and how it is shaped: the recipe drawn from its clan, its trade and chance.</summary>
        struct Recipe
        {
            public float Width, Depth;
            public int Storeys;
            public Material Wall, Upper, Roof, Frame;
            public bool Curved, Gable, LeanTo, Thatched, RidgeAlongZ;
            public float Pitch, Overhang, Curl;
            public bool Porch, Gallery, SideShed, OutsideStair, Lantern, Corn, Chimney, OpenFront, Awning, Flag, Sign, Lattice, Snow;
            public float Plinth;
            /// <summary>The open ground level on stakes under the house, m (0: none) — the Gu Yue's way (lore ch. 3).</summary>
            public float Stilts;
            public float Lean, Sag, Twist;
            /// <summary>A room inside (Enterable): not a solid block with a door painted on.</summary>
            public bool Inside;
            public int Windows;
            public float DoorAt;
        }

        static Design MakeDesign(Clan clan, BuildingKind kind, int index, System.Random random)
        {
            float R() => (float)random.NextDouble();
            bool Chance(float p) => random.NextDouble() < p;
            var r = new Recipe();

            // Size: a trade's own, varied.
            (float w0, float w1, float d0, float d1) size = kind switch
            {
                BuildingKind.Shed => (2.6f, 4.2f, 2.4f, 3.6f),
                BuildingKind.Poor => (3.6f, 6.2f, 3.2f, 5),
                BuildingKind.Shop => (4.4f, 7.5f, 4.2f, 6.5f),
                BuildingKind.Tavern => (8, 11.5f, 6.5f, 9),
                BuildingKind.Teahouse => (7, 9.5f, 6, 8),
                BuildingKind.Inn => (10, 13, 6.5f, 8),
                BuildingKind.Smithy => (6, 8, 5, 6.5f),
                BuildingKind.Granary => (5.5f, 7.5f, 4.5f, 6),
                BuildingKind.GuFarm => (6, 8, 5, 6.5f),
                BuildingKind.Gambling => (7, 9, 6, 7.5f),
                BuildingKind.Herbalist => (5, 7, 4.5f, 6),
                _ => (4.4f, 9, 4, 7.5f),
            };
            // Spread the catalogue evenly over the sizes, jittered.
            var t = (index + R() * 0.8f) / Mathf.Max(1, CountOf(kind) - 0.2f);
            r.Width = Mathf.Lerp(size.w0, size.w1, Mathf.Repeat(t * 1.618f + R() * 0.2f, 1));
            r.Depth = Mathf.Lerp(size.d0, size.d1, Mathf.Repeat(t + R() * 0.25f, 1));

            r.Storeys = kind switch
            {
                BuildingKind.Shed or BuildingKind.Poor or BuildingKind.Smithy or BuildingKind.GuFarm => 1,
                BuildingKind.Tavern or BuildingKind.Inn => Chance(0.7f) ? 2 : 3,
                BuildingKind.Teahouse or BuildingKind.Gambling => 2,
                BuildingKind.Shop => Chance(0.55f) ? 2 : 1,
                _ => Chance(clan == Clan.GuYue ? 0.32f : 0.22f) ? 2 : 1,
            };

            if (r.Width < 5 && r.Storeys > 1 && kind == BuildingKind.Family)
            {
                r.Storeys = Chance(0.5f) ? 2 : 1;
            }

            // Walls and roofs by clan, poorer ones plainer.
            var poor = kind == BuildingKind.Poor || kind == BuildingKind.Shed;

            switch (clan)
            {
                case Clan.Bai:
                    r.Wall = poor && Chance(0.5f) ? Mud : BaiWall;
                    r.Upper = Chance(0.6f) ? BaiWall : Wood;
                    r.Roof = poor && Chance(0.5f) ? OldThatch : Chance(0.7f) ? BaiRoof : TilesWorn;
                    r.Gable = Chance(0.7f);
                    r.Curved = !r.Gable && Chance(0.3f);
                    r.Plinth = 0.5f + R() * 0.4f;
                    r.Snow = Chance(0.5f);
                    break;
                case Clan.Xiong:
                    r.Wall = Chance(0.7f) ? Logs : Mud;
                    r.Upper = Logs;
                    r.Roof = Chance(0.75f) ? Thatch : OldThatch;
                    r.Thatched = r.Roof == Thatch || r.Roof == OldThatch;
                    r.Gable = Chance(0.5f);
                    r.Plinth = 0.25f;
                    break;
                case Clan.Mortal:
                    r.Wall = Chance(0.6f) ? Mud : Wood;
                    r.Upper = Wood;
                    r.Roof = Chance(0.8f) ? OldThatch : Thatch;
                    r.Thatched = true;
                    r.Gable = Chance(0.5f);
                    r.Plinth = 0.2f;
                    break;
                default:
                    // Gu Yue: the lore's dark green bamboo, plaster for the better-off, mud and planks for the poor.
                    var pick = R();
                    r.Wall = poor ? (pick < 0.4f ? OldBamboo : pick < 0.75f ? Mud : Wood) : pick < 0.5f ? Bamboo : pick < 0.68f ? OldBamboo : pick < 0.88f ? Plaster : Wood;
                    r.Upper = Chance(0.7f) ? Bamboo : Wood;
                    var roof = R();
                    r.Roof = poor ? (roof < 0.6f ? Thatch : roof < 0.85f ? OldThatch : TilesWorn) : roof < 0.45f ? Tiles : roof < 0.7f ? TilesWorn : roof < 0.85f ? TilesMoss : Thatch;
                    r.Thatched = r.Roof == Thatch || r.Roof == OldThatch;
                    r.Curved = !r.Thatched && Chance(0.75f);
                    r.Gable = !r.Curved && Chance(0.6f);
                    r.Plinth = 0.3f + R() * 0.5f;
                    r.Lattice = true;
                    break;
            }

            r.Thatched |= r.Roof == Thatch || r.Roof == OldThatch;

            if (r.Thatched)
            {
                r.Curved = false;
            }

            r.LeanTo = kind == BuildingKind.Shed && Chance(0.6f);
            r.Frame = DarkWood;
            r.Pitch = r.Thatched ? 0.55f + R() * 0.2f : r.Curved ? 0.42f + R() * 0.18f : 0.48f + R() * 0.2f;
            r.Overhang = r.Thatched ? 0.55f + R() * 0.3f : 0.7f + R() * 0.45f;
            r.Curl = r.Curved ? 0.35f + R() * 0.45f : 0;
            r.RidgeAlongZ = r.Depth > r.Width * 1.15f || Chance(0.15f);

            r.Porch = !poor && Chance(kind == BuildingKind.Family ? 0.4f : 0.7f);
            r.Gallery = r.Storeys > 1 && Chance(0.45f);
            r.SideShed = kind != BuildingKind.Shed && Chance(0.28f);
            r.OutsideStair = r.Storeys > 1 && Chance(0.18f);
            r.Lantern = Chance(clan == Clan.GuYue ? 0.6f : 0.4f) || kind == BuildingKind.Tavern || kind == BuildingKind.Inn;
            r.Corn = Chance(poor ? 0.3f : 0.15f);
            r.Chimney = Chance(0.35f) || kind == BuildingKind.Smithy || kind == BuildingKind.Tavern;
            r.OpenFront = kind == BuildingKind.Smithy;
            r.Inside = Enterable(kind);
            r.Awning = kind == BuildingKind.Shop || kind == BuildingKind.Tavern || kind == BuildingKind.Teahouse || (kind == BuildingKind.Herbalist);
            r.Flag = kind == BuildingKind.Tavern;
            r.Sign = kind != BuildingKind.Family && kind != BuildingKind.Poor && kind != BuildingKind.Shed;
            r.Windows = poor ? random.Next(0, 2) : random.Next(1, 4);
            r.DoorAt = r.OpenFront ? 0 : (R() - 0.5f) * Mathf.Max(0, r.Width - 2.4f) * 0.7f;

            // The Gu Yue's houses stand on massive stakes, their living floor up a stair (lore ch. 3: "at least two
            // floors: wooden stakes for the first level on uneven terrain, living quarters on the second").
            if (clan == Clan.GuYue && (kind == BuildingKind.Family ? Chance(0.72f) : kind == BuildingKind.Poor && Chance(0.6f)))
            {
                r.Stilts = 2.1f + R() * 0.5f;
                r.Porch = false;
                r.OutsideStair = false;
                r.OpenFront = false;
                r.Plinth = 0.1f;
                r.DoorAt = Mathf.Min(-r.Width / 2 + 0.35f + 3.4f, r.Width / 2 - 0.75f);
            }

            // Crooked: older and poorer, more so.
            var age = poor ? 1.6f : kind == BuildingKind.Family ? 1 : 0.6f;
            r.Lean = (R() - 0.5f) * 2.4f * age;
            r.Sag = (0.03f + R() * 0.1f) * age;
            r.Twist = (R() - 0.5f) * 1.5f * age;

            var name = $"{clan} {kind} {index}";
            if (r.Inside)
            {
                r.Lean *= 0.3f;
                r.Twist *= 0.3f;
                r.DoorAt = Mathf.Clamp(r.DoorAt, -r.Width / 2 + 1.2f, r.Width / 2 - 1.2f);
            }

            var design = new Design { Name = name, Clan = clan, Kind = kind, Size = new Vector2(r.Width, r.Depth), Open = r.Inside, Storey = r.Storeys > 1 ? 2.6f : 2.8f, DoorAt = r.DoorAt };
            var near = new MeshKit();

            if (r.Stilts > 0)
            {
                near.Transform = Matrix4x4.Translate(Vector3.up * r.Stilts);
            }

            BuildHouse(near, r, kind, out var height);
            near.Transform = Matrix4x4.identity;

            if (r.Stilts > 0)
            {
                Stilts(near, r);
                height += r.Stilts;
            }

            design.Height = height;
            Crook(near, r, height);
            design.Near = Save(near, $"House {name}");

            var far = new MeshKit();
            FarHouse(far, r, height);
            design.Far = Save(far, $"House {name} far");

            return design;
        }

        /// <summary>
        /// The ground level of a house on stakes: thick posts on stone bases under its floor, beams round its top, a
        /// store walled in bamboo slats at the back, and a stair along the front up to a landing at the door.
        /// </summary>
        static void Stilts(MeshKit kit, Recipe r)
        {
            var w = r.Width;
            var d = r.Depth;
            var h = r.Stilts;
            var front = d / 2 + 0.06f;
            var cols = Mathf.Max(2, Mathf.CeilToInt(w / 2.1f));
            var rows = Mathf.Max(2, Mathf.CeilToInt(d / 2.1f));

            for (var i = 0; i <= cols; i++)
            {
                for (var k = 0; k <= rows; k++)
                {
                    if (i != 0 && i != cols && k != 0 && k != rows && (i + k) % 2 == 1)
                    {
                        continue;
                    }

                    var x = -w / 2 + 0.18f + i * (w - 0.36f) / cols;
                    var z = -d / 2 + 0.18f + k * (d - 0.36f) / rows;
                    kit.Cylinder(DarkWood, new Vector3(x, 0, z), new Vector3(x, h, z), 0.17f, 0.14f, 7);
                    kit.Cylinder(Stone, new Vector3(x, -0.15f, z), new Vector3(x, 0.22f, z), 0.3f, 0.26f, 7);
                }
            }

            kit.Box(DarkWood, new Vector3(0, h - 0.14f, d / 2 - 0.18f), new Vector3(w, 0.24f, 0.22f));
            kit.Box(DarkWood, new Vector3(0, h - 0.14f, -d / 2 + 0.18f), new Vector3(w, 0.24f, 0.22f));
            kit.Box(DarkWood, new Vector3(-w / 2 + 0.18f, h - 0.14f, 0), new Vector3(0.22f, 0.24f, d));
            kit.Box(DarkWood, new Vector3(w / 2 - 0.18f, h - 0.14f, 0), new Vector3(0.22f, 0.24f, d));

            // The store at the back: bamboo slats between the posts, firewood stacked against it.
            kit.Box(OldBamboo, new Vector3(0, (h - 0.3f) / 2, -d / 4 - 0.1f), new Vector3(w - 0.5f, h - 0.3f, d / 2 - 0.3f));
            kit.Box(Wood, new Vector3(-w / 4, 0.45f, 0.1f), new Vector3(w / 3, 0.9f, 0.5f));

            // The stair along the front, from the left corner up to the landing at the door.
            var x0 = -w / 2 + 0.35f;
            var run = Mathf.Max(1.6f, r.DoorAt - 0.55f - x0);
            var steps = Mathf.CeilToInt(h / 0.24f);

            for (var s = 0; s < steps; s++)
            {
                var x = x0 + (s + 0.5f) * run / steps;
                var y = (s + 1) * h / steps;
                kit.Box(Wood, new Vector3(x, y - 0.05f, front + 0.5f), new Vector3(run / steps + 0.06f, 0.1f, 0.8f));
            }

            kit.Box(DarkWood, new Vector3(x0 + run / 2, h / 2 - 0.1f, front + 0.92f), new Vector3(run * 1.08f, 0.08f, 0.06f), Mathf.Atan2(h, run) * Mathf.Rad2Deg * 0);
            kit.Box(Wood, new Vector3(r.DoorAt, h - 0.06f, front + 0.5f), new Vector3(1.3f, 0.12f, 0.85f));
            kit.Cylinder(DarkWood, new Vector3(r.DoorAt + 0.55f, 0, front + 0.85f), new Vector3(r.DoorAt + 0.55f, h, front + 0.85f), 0.07f, 0.07f, 5);
            kit.Cylinder(DarkWood, new Vector3(r.DoorAt - 0.55f, 0, front + 0.85f), new Vector3(r.DoorAt - 0.55f, h, front + 0.85f), 0.07f, 0.07f, 5);
            kit.Box(DarkWood, new Vector3(r.DoorAt, h + 0.85f, front + 0.9f), new Vector3(1.3f, 0.06f, 0.06f));
        }

        /// <summary>
        /// A ground storey one goes into: four walls with a door's gap (its leaves swung open), a plank floor, the
        /// ceiling, warm lanterns hanging, and what the trade has — tables and stools and wine jars for a tavern, an inn
        /// or a teahouse; a counter and shelves of jars and boxes for a shop or the herbalist; tables with dice for
        /// the gambling house.
        /// </summary>
        static void Room(MeshKit kit, Recipe r, BuildingKind kind, float w, float d, float storey)
        {
            const float t = 0.18f;
            const float door = 1.4f;
            const float doorHigh = 2.2f;
            var front = d / 2 - t / 2;
            var random = new System.Random((int)(w * 100 + d * 37) + (int)kind);
            float R() => (float)random.NextDouble();

            // Walls: back, sides, and the front either side of the door and over it.
            kit.Box(r.Wall, new Vector3(0, storey / 2, -d / 2 + t / 2), new Vector3(w, storey, t));
            kit.Box(r.Wall, new Vector3(-w / 2 + t / 2, storey / 2, 0), new Vector3(t, storey, d));
            kit.Box(r.Wall, new Vector3(w / 2 - t / 2, storey / 2, 0), new Vector3(t, storey, d));
            var left = r.DoorAt - door / 2;
            var right = r.DoorAt + door / 2;
            kit.Box(r.Wall, new Vector3((-w / 2 + left) / 2, storey / 2, front), new Vector3(left + w / 2, storey, t));
            kit.Box(r.Wall, new Vector3((right + w / 2) / 2, storey / 2, front), new Vector3(w / 2 - right, storey, t));
            kit.Box(r.Wall, new Vector3(r.DoorAt, (doorHigh + storey) / 2, front), new Vector3(door, storey - doorHigh, t));
            kit.Box(r.Frame, new Vector3(r.DoorAt, doorHigh + 0.07f, front + 0.06f), new Vector3(door + 0.25f, 0.14f, 0.12f));
            // The leaves swung in against the wall.
            kit.Box(DarkWood, new Vector3(left + 0.06f, 1.05f, front - 0.4f), new Vector3(0.05f, 2.1f, 0.7f));
            kit.Box(DarkWood, new Vector3(right - 0.06f, 1.05f, front - 0.4f), new Vector3(0.05f, 2.1f, 0.7f));
            // Paper windows either side, lit from within.
            kit.Box(Paper, new Vector3((-w / 2 + left) / 2, 1.55f, front + 0.1f), new Vector3(Mathf.Min(1.1f, (left + w / 2) * 0.6f), 0.7f, 0.03f));
            kit.Box(Paper, new Vector3((right + w / 2) / 2, 1.55f, front + 0.1f), new Vector3(Mathf.Min(1.1f, (w / 2 - right) * 0.6f), 0.7f, 0.03f));
            // Floor and ceiling.
            kit.Box(Wood, new Vector3(0, 0.02f, 0), new Vector3(w - 2 * t, 0.04f, d - 2 * t));
            kit.Box(DarkWood, new Vector3(0, storey - 0.06f, 0), new Vector3(w - 2 * t, 0.12f, d - 2 * t));

            for (var x = -w / 2 + 0.8f; x < w / 2 - 0.5f; x += 1.6f)
            {
                kit.Box(DarkWood, new Vector3(x, storey - 0.2f, 0), new Vector3(0.12f, 0.16f, d - 2 * t));
            }

            // Lanterns from the beams.
            foreach (var x in new[] { -w / 4, w / 4 })
            {
                kit.Box(DarkWood, new Vector3(x, storey - 0.45f, 0), new Vector3(0.02f, 0.5f, 0.02f));
                kit.Box(WarmLantern, new Vector3(x, storey - 0.85f, 0), new Vector3(0.32f, 0.42f, 0.32f));
            }

            var inner = new Vector2(w - 2 * t - 0.3f, d - 2 * t - 0.3f);

            void Table(Vector3 at, float size)
            {
                kit.Box(Wood, at + new Vector3(0, 0.74f, 0), new Vector3(size, 0.06f, size * 0.8f));

                foreach (var (x, z) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                {
                    kit.Box(DarkWood, at + new Vector3(x * (size / 2 - 0.08f), 0.37f, z * (size * 0.4f - 0.08f)), new Vector3(0.07f, 0.74f, 0.07f));
                }
            }

            void Stool(Vector3 at)
            {
                kit.Box(Wood, at + new Vector3(0, 0.44f, 0), new Vector3(0.36f, 0.06f, 0.36f));
                kit.Box(DarkWood, at + new Vector3(0, 0.21f, 0), new Vector3(0.22f, 0.42f, 0.22f));
            }

            void Jar(Vector3 at, float high)
            {
                kit.Cylinder(R() < 0.5f ? Clay : Glaze, at, at + Vector3.up * high, high * 0.32f, high * 0.22f, 7);
            }

            void Shelves(float z, float from, float to)
            {
                for (var level = 0; level < 3; level++)
                {
                    var y = 0.5f + level * 0.6f;
                    kit.Box(DarkWood, new Vector3((from + to) / 2, y, z), new Vector3(to - from, 0.04f, 0.35f));

                    for (var x = from + 0.2f; x < to - 0.15f; x += 0.3f + R() * 0.2f)
                    {
                        if (R() < 0.5f)
                        {
                            Jar(new Vector3(x, y + 0.02f, z), 0.18f + R() * 0.14f);
                        }
                        else
                        {
                            kit.Box(R() < 0.5f ? Wood : Straw, new Vector3(x, y + 0.1f, z), new Vector3(0.2f, 0.18f, 0.22f));
                        }
                    }
                }

                kit.Box(DarkWood, new Vector3(from + 0.02f, 1.05f, z), new Vector3(0.05f, 2.1f, 0.36f));
                kit.Box(DarkWood, new Vector3(to - 0.02f, 1.05f, z), new Vector3(0.05f, 2.1f, 0.36f));
            }

            switch (kind)
            {
                case BuildingKind.Shop:
                case BuildingKind.Herbalist:
                {
                    // A counter across the room, the keeper's shelves behind it, sacks by the door.
                    var counterZ = -inner.y / 2 + 1.3f;
                    kit.Box(Wood, new Vector3(0, 0.5f, counterZ), new Vector3(inner.x * 0.7f, 1.0f, 0.55f));
                    kit.Box(DarkWood, new Vector3(0, 1.02f, counterZ), new Vector3(inner.x * 0.72f, 0.05f, 0.65f));
                    Shelves(-inner.y / 2 + 0.2f, -inner.x / 2 + 0.2f, inner.x / 2 - 0.2f);

                    for (var i = 0; i < 3; i++)
                    {
                        Jar(new Vector3(-inner.x * 0.25f + i * inner.x * 0.25f, 1.05f, counterZ), 0.22f);
                    }

                    kit.Box(Straw, new Vector3(-inner.x / 2 + 0.5f, 0.35f, inner.y / 2 - 0.5f), new Vector3(0.5f, 0.7f, 0.4f));
                    kit.Box(Straw, new Vector3(-inner.x / 2 + 0.6f, 0.3f, inner.y / 2 - 1.1f), new Vector3(0.45f, 0.6f, 0.4f));
                    break;
                }
                case BuildingKind.Gambling:
                {
                    for (var i = 0; i < 2; i++)
                    {
                        var at = new Vector3((i - 0.5f) * inner.x * 0.5f, 0, -0.2f);
                        Table(at, 1.3f);

                        for (var k = 0; k < 3; k++)
                        {
                            kit.Box(Glow(Mat("Dice", 0xe8e0d0, 0.2f)), at + new Vector3(-0.2f + k * 0.18f, 0.8f, 0.05f * k), Vector3.one * 0.05f);
                        }

                        Stool(at + new Vector3(-0.8f, 0, 0));
                        Stool(at + new Vector3(0.8f, 0, 0));
                    }

                    Shelves(-inner.y / 2 + 0.2f, -inner.x / 2 + 0.3f, 0);
                    break;
                }
                default:
                {
                    // A tavern, a teahouse, an inn: a counter along the back with jars, tables and stools in the room.
                    kit.Box(Wood, new Vector3(inner.x * 0.15f, 0.55f, -inner.y / 2 + 0.7f), new Vector3(inner.x * 0.6f, 1.1f, 0.5f));
                    kit.Box(DarkWood, new Vector3(inner.x * 0.15f, 1.12f, -inner.y / 2 + 0.7f), new Vector3(inner.x * 0.62f, 0.05f, 0.6f));

                    for (var x = -inner.x / 2 + 0.4f; x < -inner.x * 0.18f; x += 0.55f)
                    {
                        Jar(new Vector3(x, 0, -inner.y / 2 + 0.35f), 0.7f + R() * 0.2f);
                    }

                    var tables = Mathf.Max(1, Mathf.FloorToInt(inner.x / 2.4f));

                    for (var i = 0; i < tables; i++)
                    {
                        var at = new Vector3(-inner.x / 2 + (i + 0.5f) * inner.x / tables, 0, inner.y * 0.1f);
                        Table(at, 1.0f);
                        Stool(at + new Vector3(0, 0, -0.7f));
                        Stool(at + new Vector3(0, 0, 0.7f));

                        if (R() < 0.7f)
                        {
                            Jar(at + new Vector3(0.15f, 0.77f, 0), 0.16f);
                            kit.Box(Glaze, at + new Vector3(-0.2f, 0.8f, 0.1f), new Vector3(0.08f, 0.06f, 0.08f));
                        }
                    }

                    break;
                }
            }
        }

        static int CountOf(BuildingKind kind) => kind switch
        {
            BuildingKind.Family => 22,
            BuildingKind.Poor => 12,
            BuildingKind.Shed => 6,
            BuildingKind.Shop => 9,
            BuildingKind.Tavern => 4,
            _ => 2,
        };

        static (Mesh, Material[]) Save(MeshKit kit, string name)
        {
            var built = kit.Build(name);

            return (Keep(built.mesh, $"{Generated}/Meshes/{name}.asset"), built.materials);
        }

        /// <summary>Makes a house crooked: its walls lean (more the higher), its ridge sags in the middle, its top twists a little.</summary>
        static void Crook(MeshKit kit, Recipe r, float height)
        {
            var lean = Mathf.Tan(r.Lean * Mathf.Deg2Rad);
            var twist = r.Twist * Mathf.Deg2Rad;
            var halfSpan = Mathf.Max(0.5f, (r.RidgeAlongZ ? r.Depth : r.Width) / 2 + r.Overhang);
            kit.Deform(p =>
            {
                var up = Mathf.Max(0, p.y);
                var x = p.x + up * lean;
                // The roof's ridge and eaves sag towards the middle of their length.
                var along = r.RidgeAlongZ ? p.z : p.x;
                var sag = up > height * 0.55f ? r.Sag * (1 - Mathf.Clamp01(along * along / (halfSpan * halfSpan))) * Mathf.Clamp01((up - height * 0.55f) / (height * 0.45f)) : 0;
                var a = twist * up / Mathf.Max(1, height);
                var c = Mathf.Cos(a);
                var s = Mathf.Sin(a);

                return new Vector3(x * c - p.z * s, p.y - sag, x * s + p.z * c);
            });
        }

        /// <summary>
        /// A house from its recipe, front to +Z, its floor at 0 (what is under it — a footing,
        /// stilts — is the plot's, VillageBuild): walls in a timber frame, a door, windows,
        /// the storeys, the roof, and what its trade and its owners add.
        /// </summary>
        static void BuildHouse(MeshKit kit, Recipe r, BuildingKind kind, out float height)
        {
            var w = r.Width;
            var d = r.Depth;
            var storey = r.Storeys > 1 ? 2.6f : 2.8f;
            var front = d / 2 + 0.06f;

            // A low floor platform under it.
            kit.Box(Wood, new Vector3(0, -0.08f, 0), new Vector3(w + 0.25f, 0.16f, d + 0.25f));

            // The ground storey.
            if (r.OpenFront)
            {
                // Three walls; the front open on posts over a counter.
                kit.Box(r.Wall, new Vector3(0, storey / 2, -d / 2 + 0.1f), new Vector3(w, storey, 0.2f));
                kit.Box(r.Wall, new Vector3(-w / 2 + 0.1f, storey / 2, 0), new Vector3(0.2f, storey, d));
                kit.Box(r.Wall, new Vector3(w / 2 - 0.1f, storey / 2, 0), new Vector3(0.2f, storey, d));
                kit.Box(Interior, new Vector3(0, storey / 2, -0.2f), new Vector3(w - 0.4f, storey - 0.05f, d - 0.6f));

                foreach (var x in new[] { -w / 2 + 0.1f, -w / 6, w / 6, w / 2 - 0.1f })
                {
                    kit.Box(r.Frame, new Vector3(x, storey / 2, front - 0.05f), new Vector3(0.18f, storey, 0.18f));
                }

                kit.Box(Wood, new Vector3(0, 0.45f, front - 0.5f), new Vector3(w - 0.6f, 0.9f, 0.6f));
                kit.Box(DarkWood, new Vector3(0, 0.92f, front - 0.45f), new Vector3(w - 0.4f, 0.06f, 0.75f));
            }
            else if (r.Inside)
            {
                Room(kit, r, kind, w, d, storey);
            }
            else
            {
                kit.Box(r.Wall, new Vector3(0, storey / 2, 0), new Vector3(w, storey, d));
                // The door: two dark leaves under a lintel.
                kit.Box(DarkWood, new Vector3(r.DoorAt, 1.05f, front), new Vector3(1.2f, 2.1f, 0.06f));
                kit.Box(r.Frame, new Vector3(r.DoorAt, 2.18f, front + 0.02f), new Vector3(1.45f, 0.14f, 0.1f));

                if (r.Lattice)
                {
                    kit.Lattice(DarkWood, Paper, new Vector3(r.DoorAt, 1.6f, front + 0.04f), 0.9f, 0.6f, 0, 0.15f);
                }

                // Windows: on the front either side of the door, and on the sides.
                for (var i = 0; i < r.Windows; i++)
                {
                    var x = i == 0 ? -w / 2 + 1.0f : i == 1 ? w / 2 - 1.0f : 0;

                    if (Mathf.Abs(x - r.DoorAt) < 1.4f || Mathf.Abs(x) > w / 2 - 0.6f)
                    {
                        continue;
                    }

                    Window(kit, r, new Vector3(x, 1.55f, front), 0, 0.95f, 0.75f);
                }

                if (d > 3.6f)
                {
                    Window(kit, r, new Vector3(w / 2 + 0.06f, 1.55f, 0), 90, 0.9f, 0.7f);
                    Window(kit, r, new Vector3(-w / 2 - 0.06f, 1.55f, (r.Width > 6 ? -0.8f : 0.6f)), -90, 0.8f, 0.7f);
                }
            }

            Frame(kit, w, d, 0, storey, w > 6.5f ? w / 3 : 0, r.Frame);
            height = storey;

            // The upper storeys, a little in from the walls below, under a skirt of eaves or over a gallery.
            for (var s = 1; s < r.Storeys; s++)
            {
                var inset = r.Gallery ? 0.9f : 0.35f;
                var uw = w - inset * 2 * (s);
                var ud = d - inset * 1.6f * s;

                if (uw < 2.5f || ud < 2.5f)
                {
                    break;
                }

                if (!r.Gallery && !r.Thatched)
                {
                    // A skirt of tiles between the storeys.
                    kit.CurvedRoof(r.Roof, RoofUnder, Ridge, new Vector3(0, height, 0), w, d, 0.55f, 0.55f, r.Curl * 0.5f, 0, r.Thatched ? 0 : 0.34f);
                }
                else
                {
                    // A gallery round the upper storey, railed.
                    kit.Box(Wood, new Vector3(0, height + 0.05f, 0), new Vector3(w + 0.3f, 0.12f, d + 0.3f));
                    Railing(kit, new Vector3(0, height + 0.1f, 0), w + 0.1f, d + 0.1f, 0, 1, r.Frame, Wood);
                }

                var floor = height + 0.12f;
                var upper = 2.35f;
                kit.Box(r.Upper, new Vector3(0, floor + upper / 2, 0), new Vector3(uw, upper, ud));
                Frame(kit, uw, ud, floor, upper, 0, r.Frame);
                Window(kit, r, new Vector3(0, floor + 1.25f, ud / 2 + 0.06f), 0, Mathf.Min(1.8f, uw - 1.2f), 0.75f);

                if (ud > 3)
                {
                    Window(kit, r, new Vector3(uw / 2 + 0.06f, floor + 1.25f, 0), 90, 0.9f, 0.65f);
                }

                height = floor + upper;
                w = uw;
                d = ud;
            }

            // The roof.
            var roofSpan = r.RidgeAlongZ ? 90 : 0;
            var rw = r.RidgeAlongZ ? d : w;
            var rd = r.RidgeAlongZ ? w : d;
            var rise = Mathf.Min(rw, rd) * r.Pitch;

            if (r.LeanTo)
            {
                // A single slope, high at the back.
                var hb = height + 0.9f;
                var hf = height + 0.1f;
                var o = r.Overhang;
                kit.Quad(r.Roof, new Vector3(-w / 2 - o, hb, -d / 2 - o * 0.5f), new Vector3(w / 2 + o, hb, -d / 2 - o * 0.5f), new Vector3(w / 2 + o, hf, d / 2 + o), new Vector3(-w / 2 - o, hf, d / 2 + o), Vector3.up);
                kit.Quad(RoofUnder, new Vector3(-w / 2 - o, hb - 0.08f, -d / 2 - o * 0.5f), new Vector3(w / 2 + o, hb - 0.08f, -d / 2 - o * 0.5f), new Vector3(w / 2 + o, hf - 0.08f, d / 2 + o), new Vector3(-w / 2 - o, hf - 0.08f, d / 2 + o), Vector3.down);
                kit.Box(r.Wall, new Vector3(0, height + 0.45f, -d / 2 + 0.1f), new Vector3(w, 0.9f, 0.2f));
                height = hb;
            }
            else if (r.Gable)
            {
                GableRoof(kit, r.Roof, new Vector3(0, height, 0), rw, rd, rise, r.Overhang, roofSpan, r.Thatched, r.Wall);
                height += rise;
            }
            else
            {
                kit.CurvedRoof(r.Roof, RoofUnder, r.Thatched ? r.Roof : Ridge, new Vector3(0, height, 0), rw, rd, rise * (r.Thatched ? 1.25f : 1), r.Overhang, r.Thatched ? 0.1f : r.Curl, roofSpan, r.Thatched ? 0 : 0.34f);

                // A tiled ridge's ends turned up into ornaments (the old "ridge beasts", plain here).
                if (!r.Thatched)
                {
                    foreach (var end in new[] { -1f, 1f })
                    {
                        var p = Quaternion.Euler(0, roofSpan, 0) * new Vector3(end * (rw / 2 - 0.15f), 0, 0);
                        kit.Box(Ridge, new Vector3(p.x, height + rise + 0.3f, p.z), new Vector3(0.2f, 0.55f, 0.32f), roofSpan);
                        kit.Box(Ridge, new Vector3(p.x * 1.03f, height + rise + 0.65f, p.z * 1.03f), new Vector3(0.12f, 0.28f, 0.24f), roofSpan + 25 * end);
                    }
                }

                height += rise;
            }

            if (r.Snow)
            {
                kit.Box(Snow, new Vector3(0, height - rise * 0.15f, 0), new Vector3(rw * 0.55f, 0.08f, rd * 0.35f), roofSpan);
            }

            var top = height;
            w = r.Width;
            d = r.Depth;

            // Extras.
            if (r.Porch)
            {
                var depth = 1.4f;
                kit.Box(Wood, new Vector3(0, -0.06f, front + depth / 2), new Vector3(w * 0.8f, 0.14f, depth));

                foreach (var x in new[] { -w * 0.38f, w * 0.38f })
                {
                    kit.Box(r.Frame, new Vector3(x, 1.15f, front + depth - 0.1f), new Vector3(0.14f, 2.3f, 0.14f));
                }

                kit.Quad(r.Roof, new Vector3(-w * 0.45f, 2.55f, front - 0.05f), new Vector3(w * 0.45f, 2.55f, front - 0.05f), new Vector3(w * 0.45f, 2.2f, front + depth + 0.3f), new Vector3(-w * 0.45f, 2.2f, front + depth + 0.3f), Vector3.up);
                kit.Quad(RoofUnder, new Vector3(-w * 0.45f, 2.5f, front - 0.05f), new Vector3(w * 0.45f, 2.5f, front - 0.05f), new Vector3(w * 0.45f, 2.15f, front + depth + 0.3f), new Vector3(-w * 0.45f, 2.15f, front + depth + 0.3f), Vector3.down);
            }

            if (r.Awning)
            {
                var cloth = kind == BuildingKind.Tavern ? Cloth("Wine Red", 0x6a2620) : kind == BuildingKind.Teahouse ? Cloth("Tea Green", 0x2e4a34) : Cloth("Indigo", 0x26304a);
                kit.Quad(cloth, new Vector3(-w / 2, 2.45f, front), new Vector3(w / 2, 2.45f, front), new Vector3(w / 2, 2.0f, front + 1.6f), new Vector3(-w / 2, 2.0f, front + 1.6f), Vector3.up);

                foreach (var x in new[] { -w / 2 + 0.1f, w / 2 - 0.1f })
                {
                    kit.Box(DarkWood, new Vector3(x, 1, front + 1.55f), new Vector3(0.08f, 2.05f, 0.08f));
                }
            }

            if (r.SideShed)
            {
                var sw = 1.8f + (r.Depth % 1);
                kit.Box(r.Wall == BaiWall ? Mud : Wood, new Vector3(w / 2 + sw / 2, 0.95f, -0.3f), new Vector3(sw, 1.9f, d * 0.7f));
                kit.Quad(r.Thatched ? r.Roof : TilesWorn, new Vector3(w / 2 - 0.1f, 2.25f, -d * 0.35f - 0.5f), new Vector3(w / 2 - 0.1f, 2.25f, d * 0.35f), new Vector3(w / 2 + sw + 0.4f, 1.7f, d * 0.35f), new Vector3(w / 2 + sw + 0.4f, 1.7f, -d * 0.35f - 0.5f), Vector3.up);
            }

            if (r.OutsideStair)
            {
                for (var i = 0; i < 9; i++)
                {
                    kit.Box(Wood, new Vector3(-w / 2 - 0.55f, 0.15f + i * 0.29f, d / 2 - 0.4f - i * 0.28f), new Vector3(0.9f, 0.06f, 0.3f));
                }

                kit.Box(r.Frame, new Vector3(-w / 2 - 1.0f, 1.5f, d / 2 - 1.5f), new Vector3(0.06f, 3, 0.06f));
            }

            if (r.Lantern)
            {
                HangingLantern(kit, new Vector3(r.DoorAt + 0.9f, 2.45f, front + 0.45f), 0.85f);
            }

            if (r.Corn)
            {
                // Strings of drying corn and peppers under the eave.
                var dried = Mat("Dried Corn", 0xa07a28, 0.1f);
                var peppers = Mat("Dried Peppers", 0x7a2218, 0.1f);

                for (var i = 0; i < 4; i++)
                {
                    var x = -w / 2 + 0.6f + i * 0.45f;
                    kit.Cylinder(i % 2 == 0 ? dried : peppers, new Vector3(x, 2.4f, front + 0.25f), new Vector3(x, 1.5f, front + 0.25f), 0.07f, 0.05f, 5);
                }
            }

            if (r.Chimney)
            {
                kit.Box(Rubble, new Vector3(w * 0.25f, top - 0.2f, -d * 0.15f), new Vector3(0.5f, 1.4f, 0.5f));
            }

            if (r.Flag)
            {
                // The wine flag: a pole out from the corner, a long cloth.
                kit.Box(DarkWood, new Vector3(w / 2 + 0.2f, 2.4f, front + 0.2f), new Vector3(0.08f, 4.8f, 0.08f));
                kit.Box(DarkWood, new Vector3(w / 2 + 0.75f, 4.6f, front + 0.2f), new Vector3(1.2f, 0.06f, 0.06f));
                kit.Box(Cloth("Wine Flag", 0x8a2a1e), new Vector3(w / 2 + 0.85f, 3.6f, front + 0.2f), new Vector3(0.75f, 2, 0.03f));
            }

            if (r.Sign)
            {
                // A tall board hung by the door, the trade's colour.
                kit.Box(DarkWood, new Vector3(-w / 2 + 0.45f, 1.7f, front + 0.12f), new Vector3(0.5f, 1.6f, 0.06f));
                kit.Box(Gold, new Vector3(-w / 2 + 0.45f, 1.7f, front + 0.16f), new Vector3(0.3f, 1.3f, 0.02f));
            }

            if (r.Inside)
            {
                // A trade one goes into: a short cloth over the door's top, a red lantern either side.
                var curtain = Cloth(kind == BuildingKind.Tavern || kind == BuildingKind.Inn ? "Tavern Curtain" : "Shop Curtain", kind == BuildingKind.Tavern || kind == BuildingKind.Inn ? 0x5a1e18 : 0x1e2c3a);
                kit.Box(curtain, new Vector3(r.DoorAt - 0.36f, 1.95f, front + 0.13f), new Vector3(0.66f, 0.5f, 0.02f));
                kit.Box(curtain, new Vector3(r.DoorAt + 0.36f, 1.95f, front + 0.13f), new Vector3(0.66f, 0.5f, 0.02f));
                var red = Glow(Mat("Red Lantern", 0xa83020, 0.3f));

                foreach (var side in new[] { -1f, 1f })
                {
                    var x = r.DoorAt + side * 1.05f;
                    kit.Box(DarkWood, new Vector3(x, 2.35f, front + 0.3f), new Vector3(0.03f, 0.3f, 0.03f));
                    kit.Cylinder(red, new Vector3(x, 1.85f, front + 0.3f), new Vector3(x, 2.2f, front + 0.3f), 0.2f, 0.2f, 8);
                }
            }

            if (kind == BuildingKind.Smithy)
            {
                // The forge in the open front, its chimney, the coals' glow.
                kit.Box(Rubble, new Vector3(-w / 4, 0.55f, 0), new Vector3(1.4f, 1.1f, 1.2f));
                kit.Box(WarmLantern, new Vector3(-w / 4, 1.12f, 0), new Vector3(0.8f, 0.06f, 0.7f));
                kit.Box(Rubble, new Vector3(-w / 4, top, -0.2f), new Vector3(0.7f, 2.2f, 0.7f));
            }

            if (kind == BuildingKind.GuFarm)
            {
                // A lattice pavilion for feeding Gu: open screens all round.
                foreach (var x in new[] { -w / 4, w / 4 })
                {
                    kit.Lattice(DarkWood, Paper, new Vector3(x, 1.4f, front + 0.05f), w / 2 - 0.4f, 1.6f, 0, 0.12f);
                }
            }

            if (kind == BuildingKind.Granary)
            {
                // Rat guards: flat stones on its posts (the plot gives it stilts).
                foreach (var x in new[] { -w / 2 + 0.3f, w / 2 - 0.3f })
                {
                    foreach (var z in new[] { -d / 2 + 0.3f, d / 2 - 0.3f })
                    {
                        kit.Cylinder(Stone, new Vector3(x, -0.3f, z), new Vector3(x, -0.22f, z), 0.4f, 0.4f, 8);
                    }
                }
            }

            if (kind == BuildingKind.Gambling || kind == BuildingKind.Tavern || kind == BuildingKind.Inn)
            {
                foreach (var x in new[] { -w / 3, w / 3 })
                {
                    HangingLantern(kit, new Vector3(x, Mathf.Min(top - 0.5f, 2.7f + (r.Storeys - 1) * 2.4f), front + 0.6f), 1.1f);
                }
            }

            height = top;
        }

        static void Window(MeshKit kit, Recipe r, Vector3 at, float yaw, float width, float height)
        {
            if (r.Lattice)
            {
                kit.Lattice(DarkWood, Paper, at, width, height, yaw, 0.17f);
            }
            else
            {
                // Plain shutters over a paper pane.
                var turn = Quaternion.Euler(0, yaw, 0);
                kit.Box(Paper, at, new Vector3(width, height, 0.04f), yaw);
                kit.Box(r.Frame, at + turn * new Vector3(-width * 0.75f, 0, 0.04f), new Vector3(width * 0.5f, height + 0.1f, 0.05f), yaw);
                kit.Box(r.Frame, at + turn * new Vector3(0, height / 2 + 0.05f, 0.03f), new Vector3(width + 0.15f, 0.08f, 0.08f), yaw);
            }
        }

        /// <summary>A plain gable roof (straight slopes, the gable ends walled), its ridge along X (or Z with `yaw` 90).</summary>
        static void GableRoof(MeshKit kit, Material roof, Vector3 at, float width, float depth, float rise, float overhang, float yaw, bool thatched, Material gableWall)
        {
            var turn = Quaternion.Euler(0, yaw, 0);
            var w = width / 2 + overhang * 0.6f;
            var d = depth / 2 + overhang;
            Vector3 P(float x, float y, float z) => at + turn * new Vector3(x, y, z);
            var thickness = thatched ? 0.3f : 0.12f;

            foreach (var s in new[] { -1, 1 })
            {
                kit.Quad(roof, P(-w, 0, s * d), P(w, 0, s * d), P(w, rise, 0), P(-w, rise, 0), turn * new Vector3(0, 1, s));
                kit.Quad(RoofUnder, P(-w, -thickness, s * d), P(w, -thickness, s * d), P(w, rise - thickness, 0), P(-w, rise - thickness, 0), turn * new Vector3(0, -1, -s));
                kit.Quad(RoofUnder, P(-w, 0, s * d), P(w, 0, s * d), P(w, -thickness, s * d), P(-w, -thickness, s * d), turn * new Vector3(0, 0, s));
                // The gable ends.
                kit.Triangle(gableWall, P(s * width / 2, 0, -depth / 2), P(s * width / 2, 0, depth / 2), P(s * width / 2, rise * 0.92f, 0), turn * new Vector3(s, 0, 0));
                kit.Triangle(roof, P(s * w, 0, -d), P(s * w, rise, 0), P(s * w, -thickness, -d), turn * new Vector3(s, 0, 0));
                kit.Triangle(roof, P(s * w, 0, d), P(s * w, rise, 0), P(s * w, -thickness, d), turn * new Vector3(s, 0, 0));
            }

            kit.Box(thatched ? roof : Ridge, P(0, rise + 0.06f, 0), new Vector3(w * 2 + 0.2f, 0.18f, 0.3f), yaw);

            if (!thatched)
            {
                // The ridge's ends turned up a little.
                foreach (var s in new[] { -1, 1 })
                {
                    kit.Cylinder(Ridge, P(s * (w + 0.1f), rise + 0.06f, 0), P(s * (w + 0.3f), rise + 0.35f, 0), 0.12f, 0.03f, 5);
                }
            }
        }

        /// <summary>A house's far shape: its walls, its roof, its windows (which still glow at night).</summary>
        static void FarHouse(MeshKit kit, Recipe r, float height)
        {
            var w = r.Width;
            var d = r.Depth;
            var walls = r.Storeys > 1 ? 2.6f + (r.Storeys - 1) * 2.45f : 2.8f;
            kit.Box(r.Wall, new Vector3(0, walls / 2, 0), new Vector3(w, walls, d));
            kit.Box(Paper, new Vector3(-w / 4, 1.55f, d / 2 + 0.02f), new Vector3(0.9f, 0.7f, 0.02f));
            kit.Box(Paper, new Vector3(w / 4, walls - 1.1f, d / 2 + 0.02f), new Vector3(0.9f, 0.7f, 0.02f));
            var rise = Mathf.Max(0.6f, height - walls);
            var o = r.Overhang;
            // The roof in its own frame (its ridge along X), turned when the ridge runs along Z.
            var rw = r.RidgeAlongZ ? d : w;
            var rd = r.RidgeAlongZ ? w : d;
            var ridge = Mathf.Max(0.1f, (rw - rd) / 2);
            kit.Transform = Matrix4x4.Rotate(Quaternion.Euler(0, r.RidgeAlongZ ? 90 : 0, 0));
            var a = new Vector3(-rw / 2 - o, walls, -rd / 2 - o);
            var b = new Vector3(rw / 2 + o, walls, -rd / 2 - o);
            var c = new Vector3(rw / 2 + o, walls, rd / 2 + o);
            var e = new Vector3(-rw / 2 - o, walls, rd / 2 + o);
            var r0 = new Vector3(-ridge, walls + rise, 0);
            var r1 = new Vector3(ridge, walls + rise, 0);
            kit.Quad(r.Roof, a, b, r1, r0, Vector3.up + Vector3.back);
            kit.Quad(r.Roof, c, e, r0, r1, Vector3.up + Vector3.forward);
            kit.Triangle(r.Roof, b, c, r1, Vector3.up + Vector3.right);
            kit.Triangle(r.Roof, e, a, r0, Vector3.up + Vector3.left);
            kit.Transform = Matrix4x4.identity;
        }
    }
}
