using System.Collections.Generic;
using System.Linq;
using GuWorld.World;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// Gu Yue laid out anew (2026-10-24; the user: take every building down, leave the place empty and level, build it
    /// again). The whole village stands on one level ground: a shelf cut into the mountain's side, dug in on its uphill
    /// edge and banked up on its downhill one, inside a rectangular fort wall — a tower on each corner and every ~70 m,
    /// four gates, each with its road ramping straight out to the mountain's own ground. Inside, a clan's town on
    /// straight streets: the main way from the south gate up to the clan's hall, the cross way from the west gate to the
    /// east one, a lane round the inside of the wall, side streets between the blocks. Where the main way meets the cross
    /// way, the square (the great tavern and the octagonal teahouse at its sides, the market row across the way); above
    /// it the forecourt and the great hall with the ancestors' and the elders' halls at its sides; the academy's grounds
    /// by the north gate. Houses stand in rows along every street, each square to it and facing it: shops and trades in
    /// the middle, the good bamboo houses round them, the poor by the wall. No terrace, stair or retaining wall anywhere.
    ///
    /// The village's own frame: along the slope (Along, "east") and uphill (-Down, "north"), from the settlement's middle.
    /// </summary>
    public partial class VillagePlan
    {
        // The rectangle the wall stands on, m. West of it the stream's gully, east of it the bamboo grove; the level
        // lies 4 m under the mean of the mountain's ground over it (a cut uphill is the mountain's own face, a bank
        // downhill is earth piled up: rather more of the one).
        const float FlatWest = -170, FlatEast = 250, FlatSouth = -105, FlatNorth = 105;
        /// <summary>The main way's line (along) and the cross way's (uphill).</summary>
        const float AxisAlong = 40, CrossUp = -25;
        /// <summary>The level strip kept outside the wall before the ground goes to the mountain's own, m.</summary>
        const float Berm = 6;
        /// <summary>The lane round the inside of the wall: its middle's distance from the wall's line, its half width.</summary>
        const float RingInset = 11, RingHalf = 3;
        /// <summary>The strip inside the wall's line kept for it (its own depth, its gates', its towers'), m.</summary>
        const float WallStrip = 7.5f;
        /// <summary>The steepest a gate's road may ramp to the mountain (rise over run).</summary>
        const float RampGrade = 0.3f;
        /// <summary>How far a house's front stands back from its street's edge, m.</summary>
        const float Setback = 1.5f;

        /// <summary>Whether this is the level village (Gu Yue's): its wall is built by its rectangle (QingMaoBuilder.BuildFortRect).</summary>
        public bool LevelGround => Glb;

        /// <summary>How far outside the boundary the village's earthworks reach: no giant tree nearer.</summary>
        public float Margin => Glb ? 70 : 22;

        /// <summary>A gate's road out to the mountain: from the gate, straight out, level over the berm, then ramping to the ground at its end.</summary>
        class GateRoad
        {
            public Vector2 At;
            public Vector2 Out;
            public Vector2 Across;
            public float Length;
            public float End;
        }

        readonly List<GateRoad> gateRoads = new List<GateRoad>();

        /// <summary>The streets houses stand along, in the village's frame (from, to, half width), the main ones first.</summary>
        readonly List<(Vector2 from, Vector2 to, float half, int order)> lotStreets = new List<(Vector2, Vector2, float, int)>();

        static readonly Vector2 East = Vector2.right, West = Vector2.left, North = Vector2.up, South = Vector2.down;

        Vector2 W(float along, float up) => World(along, up);

        Vector2 W(Vector2 local) => World(local.x, local.y);

        /// <summary>The turn that faces a way given in the village's frame (x along, y uphill).</summary>
        float Facing(Vector2 local) => YawFacing(Along * local.x - Down * local.y);

        /// <summary>A point a turned thing's own (x across its front, z out of its front) puts at, from its middle.</summary>
        static Vector2 Offset(Vector2 centre, float yaw, float x, float z)
        {
            var v = Quaternion.Euler(0, yaw, 0) * new Vector3(x, 0, z);

            return centre + new Vector2(v.x, v.z);
        }

        void LayOutFlat()
        {
            var sum = 0f;
            var count = 0;

            for (var a = FlatWest; a <= FlatEast; a += 10)
            {
                for (var u = FlatSouth; u <= FlatNorth; u += 10)
                {
                    var p = W(a, u);
                    sum += natural(p.x, p.y);
                    count++;
                }
            }

            Level0 = sum / count - 4;
            Boundary.Add(W(FlatWest, FlatSouth));
            Boundary.Add(W(FlatEast, FlatSouth));
            Boundary.Add(W(FlatEast, FlatNorth));
            Boundary.Add(W(FlatWest, FlatNorth));

            void Phase(string what) => BuildLog.Mark($"plan of {Place.Id}: {what}");
            Rasters();
            FlatGates();
            Phase("level ground, wall and gates");
            FlatGreatBuildings();
            FlatPads();
            FlatStreets();
            Phase("great buildings, squares, streets");
            FlatSquareAndGates();
            FlatLanterns();
            FlatHouses();
            Phase("houses");
            FlatDecor();
            FlatGardens();
            Phase("props");
        }

        // ── The ground ────────────────────────────────────────────────

        /// <summary>
        /// The ground's height: level inside the wall and over the berm outside it; beyond, a bank going to the
        /// mountain's own ground (the steeper the more there is to make up, never over ~30°); along each gate's road,
        /// its ramp, with banks down (or up) its sides.
        /// </summary>
        float FlatHeight(Vector2 p, float naturalHeight)
        {
            var outside = -InsideDistance(p);

            if (outside <= Berm)
            {
                return Level0;
            }

            var height = Bank(outside, naturalHeight);

            foreach (var road in gateRoads)
            {
                var d = p - road.At;
                var along = Vector2.Dot(d, road.Out);
                var across = Mathf.Abs(Vector2.Dot(d, road.Across));
                var end = Berm + road.Length;

                if (along < 0 || along > end + 14 || across > 60)
                {
                    continue;
                }

                var onRoad = along <= end ? Mathf.Lerp(Level0, road.End, Mathf.Clamp01((along - Berm) / road.Length)) : naturalHeight;
                var side = 3 + 1.6f * Mathf.Abs(onRoad - height);
                var weight = (1 - Smooth01(5, 5 + side, across)) * (1 - Smooth01(end, end + 12, along));
                height = Mathf.Lerp(height, onRoad, weight);
            }

            return height;
        }

        /// <summary>The bank round the level ground at a distance outside the wall: from the level to the mountain's own.</summary>
        float Bank(float outside, float naturalHeight)
        {
            var rise = naturalHeight - Level0;

            return Level0 + rise * Smooth01(Berm, Berm + 10 + 2.2f * Mathf.Abs(rise), outside);
        }

        void PaintFlat(float x, float z, float steep, float[] weights)
        {
            var p = new Vector2(x, z);
            var outside = -InsideDistance(p);

            if (outside > 160)
            {
                return;
            }

            var noise = Mathf.PerlinNoise(x * 0.35f, z * 0.35f);
            var blotch = Mathf.PerlinNoise(x * 0.06f + 3, z * 0.06f + 8);

            switch (KindAt(p))
            {
                case KLane:
                    weights[QingMaoBuilder.LayerDirtIndex] += 6;
                    weights[QingMaoBuilder.LayerMudIndex] += 1.5f * blotch;
                    weights[QingMaoBuilder.LayerGravelIndex] += noise > 0.6f ? 2 : 0.6f;
                    break;
                case KPaved:
                    weights[QingMaoBuilder.LayerFlagstoneIndex] += 10;
                    weights[QingMaoBuilder.LayerMossIndex] += blotch > 0.75f ? 1.2f : 0;
                    break;
                case KFloor:
                    weights[QingMaoBuilder.LayerDirtIndex] += 5;
                    break;
                case KYard:
                    weights[QingMaoBuilder.LayerDirtIndex] += 5;
                    weights[QingMaoBuilder.LayerGravelIndex] += 1.5f;
                    break;
                case KGarden:
                    weights[QingMaoBuilder.LayerMudIndex] += 7;
                    break;
                case KGround:
                    // Trodden grass, bare earth worn in patches, moss in the corners.
                    weights[QingMaoBuilder.LayerDirtIndex] += blotch > 0.6f ? 1.6f : 0.3f;
                    weights[QingMaoBuilder.LayerMossIndex] += noise > 0.65f ? 0.6f : 0;
                    break;
                default:
                    // Outside: the trodden strip at the wall's foot, then the banks — earth, bare rock where steep.
                    if (outside < Berm + 1.5f)
                    {
                        weights[QingMaoBuilder.LayerDirtIndex] += 4;
                        weights[QingMaoBuilder.LayerGravelIndex] += 1.5f;
                    }

                    break;
            }

            if (outside > 0 && steep > 22)
            {
                weights[QingMaoBuilder.LayerRockIndex] += Smooth01(24, 36, steep) * 4;
                weights[QingMaoBuilder.LayerDirtIndex] += Smooth01(22, 30, steep) * (1 - Smooth01(30, 38, steep)) * 1.5f;
            }
        }

        float GrassFlat(float x, float z)
        {
            var p = new Vector2(x, z);
            var cell = CellOf(p);

            switch (KindAt(p))
            {
                case KLane:
                case KPaved:
                case KFloor:
                case KGarden:
                    return 0;
                case KYard:
                    return 0.1f;
                case KGround:
                    return cell >= 0 && taken[cell] ? 0.2f : 0.35f + 0.35f * Mathf.PerlinNoise(x * 0.08f, z * 0.08f);
                default:
                    var outside = -InsideDistance(p);

                    return outside < Berm + 1 ? 0.15f : outside < Berm + 30 ? 0.7f : 1;
            }
        }

        /// <summary>Wild trees: not on the level ground, its banks near the wall or the gates' roads.</summary>
        bool WildFlat(Vector2 p)
        {
            if (-InsideDistance(p) < Berm + 24 || KindAt(p) != KWild)
            {
                return false;
            }

            foreach (var road in gateRoads)
            {
                var d = p - road.At;
                var along = Vector2.Dot(d, road.Out);

                if (along > -10 && along < Berm + road.Length + 20 && Mathf.Abs(Vector2.Dot(d, road.Across)) < 18)
                {
                    return false;
                }
            }

            return true;
        }

        // ── The gates and their roads ─────────────────────────────────

        void FlatGates()
        {
            void AddGate(string name, float along, float up, Vector2 outward)
            {
                var at = W(along, up);
                var out_ = (Along * outward.x - Down * outward.y).normalized;
                var road = new GateRoad { At = at, Out = out_, Across = new Vector2(-out_.y, out_.x) };
                var length = 20f;

                // As long as its rise asks at the steepest allowed; the ground it meets moves as it grows (found again).
                for (var i = 0; i < 24; i++)
                {
                    var end = at + out_ * (Berm + length);
                    road.End = natural(end.x, end.y);
                    length = Mathf.Max(14, Mathf.Abs(road.End - Level0) / RampGrade);
                }

                var last = at + out_ * (Berm + length);
                road.Length = length;
                road.End = natural(last.x, last.y);
                gateRoads.Add(road);
                Gates.Add(new Gate { At = at, Out = out_, Name = name, Reach = Berm + length + 4 });
                // The guards' place: inside, on the lane round the wall, looking out.
                Places.Add((name, at - out_ * RingInset, YawFacing(out_)));

                // The road: trodden earth (and no wild tree on it).
                var middle = at + out_ * ((Berm + length) / 2);
                Inside(middle, new Vector2(5, (Berm + length) / 2 + 2), YawFacing(out_), 0, cell =>
                {
                    if (kind[cell] == KWild)
                    {
                        kind[cell] = KLane;
                    }
                });

                BuildLog.Write($"plan of {Place.Id}: {name}: its road {length:0} m to the mountain at {road.End - Level0:+0.0;-0.0} m");
            }

            AddGate("gate south", AxisAlong, FlatSouth, South);
            AddGate("gate north", -130, FlatNorth, North);
            AddGate("gate west", FlatWest, CrossUp, West);
            AddGate("gate east", FlatEast, CrossUp, East);

            // The strip inside the wall kept for the wall, its towers and gates: nothing stands or is laid there.
            var width = FlatEast - FlatWest;
            var depth = FlatNorth - FlatSouth;
            void Keep(Vector2 centre, Vector2 half, float yaw) => Inside(centre, half, yaw, 0, cell => taken[cell] = true);
            Keep(W((FlatWest + FlatEast) / 2, FlatSouth + WallStrip / 2), new Vector2(width / 2, WallStrip / 2), Facing(North));
            Keep(W((FlatWest + FlatEast) / 2, FlatNorth - WallStrip / 2), new Vector2(width / 2, WallStrip / 2), Facing(North));
            Keep(W(FlatWest + WallStrip / 2, (FlatSouth + FlatNorth) / 2), new Vector2(depth / 2, WallStrip / 2), Facing(East));
            Keep(W(FlatEast - WallStrip / 2, (FlatSouth + FlatNorth) / 2), new Vector2(depth / 2, WallStrip / 2), Facing(East));

            // The gates' passages through it, paved.
            foreach (var gate in Gates)
            {
                Inside(gate.At - gate.Out * (WallStrip / 2), new Vector2(4, WallStrip / 2 + 0.5f), YawFacing(gate.Out), 0, cell => kind[cell] = KPaved);
            }
        }

        // ── The great buildings, the squares, the streets ─────────────

        /// <summary>One of the great buildings, its footprint's middle at (along, up), facing a way; its place before its front.</summary>
        Building Great(string place, string model, float scale, float along, float up, Vector2 facing, BuildingKind kind, string label)
        {
            var size = QingMaoBuilder.VillageGlb[model].Size * scale;
            var centre = W(along, up);
            var yaw = Facing(facing);

            if (!Free(centre, size / 2, yaw, 0.3f))
            {
                BuildLog.Write($"plan of {Place.Id}: {label} overlaps what stands there before it");
            }

            var b = new Building { Kind = kind, Centre = centre, Size = size, Yaw = yaw, Floor = Level0, Name = label, Lit = true, Model = model, Scale = scale, Pad = true, Seed = random.Next() };
            Buildings.Add(b);
            Take(centre, size / 2, yaw);

            if (place != null)
            {
                Places.Add((place, Offset(centre, yaw, 0, size.y / 2 + 2.5f), yaw + 180));
            }

            return b;
        }

        void FlatGreatBuildings()
        {
            // The clan's great hall at the head of the main way, its forecourt before it; the ancestors' hall and the
            // elders' at the forecourt's sides, facing each other across it.
            Great("pavilion", "prop_clan_main_hall", 0.75f, AxisAlong, 66, South, BuildingKind.Pavilion, "Павильон клана");
            Great("shrine", "prop_ancestral_hall", 1, AxisAlong - 40, 32, East, BuildingKind.Ancestral, "Зал предков");
            Great("council", "prop_council_hall", 1, AxisAlong + 40, 32, West, BuildingKind.Hall, "Зал совета старейшин");
            // The square's sides: the great tavern and the octagonal teahouse facing each other; the market row across
            // the cross way below it, facing up to it.
            Great("tavern great", "prop_tavern_great", 0.5f, 0.25f, -2, East, BuildingKind.Tavern, "Таверна у площади");
            Great("teahouse", "prop_teahouse_octagon", 0.75f, 77.2f, -2, West, BuildingKind.Teahouse, "Чайный дом «Восемь ветров»");
            Great("market", "prop_market_row", 0.7f, 12, -37, North, BuildingKind.Shop, "Торговый ряд");
            // The academy's grounds by the north gate: the academy at the yard's head, the hostel and the refectory at its sides.
            Great("academy", "prop_academy_great", 0.6f, -74, 68, South, BuildingKind.Academy, "Академия");
            Great("hostel", "prop_dorm_great", 0.55f, -120, 27, East, BuildingKind.Hostel, "Общежитие академии");
            Great("refectory", "prop_refectory", 0.55f, -32, 27, West, BuildingKind.Hall, "Трапезная академии");
        }

        void FlatPads()
        {
            var down = Facing(South);
            AddPad("square", W(AxisAlong, -2), new Vector2(28, 17), down, true);
            Square = Pads[Pads.Count - 1];
            Places.Add(("square", W(AxisAlong, -2), down));
            AddPad("forecourt", W(AxisAlong, 32.5f), new Vector2(50, 17.5f), down, true);
            AddPad("academy yard", W(-78, 27.5f), new Vector2(34, 17.5f), down, false);
            Inside(W(-78, 27.5f), new Vector2(34, 17.5f), down, 0, cell =>
            {
                if (!taken[cell] && kind[cell] != KWild)
                {
                    kind[cell] = KYard;
                }
            });
            Places.Add(("academy yard", W(-78, 27.5f), down));
        }

        /// <summary>A straight street (in the village's frame), laid in the ground (paved or trodden); `order`: when houses are set along it.</summary>
        void Way(string name, float a0, float u0, float a1, float u1, float half, bool paved, bool main, int order)
        {
            var from = W(a0, u0);
            var to = W(a1, u1);
            Streets.Add(new Street { Name = name, Points = Resample(new List<Vector2> { from, to }, 1.4f), Half = half, Main = main });
            Inside((from + to) / 2, new Vector2(half, Vector2.Distance(from, to) / 2), YawFacing(to - from), 0, cell =>
            {
                if (kind[cell] != KWild && !taken[cell])
                {
                    kind[cell] = paved ? KPaved : KLane;
                }
            });

            if (order >= 0)
            {
                lotStreets.Add((new Vector2(a0, u0), new Vector2(a1, u1), half, order));
            }
        }

        void FlatStreets()
        {
            var west = FlatWest + RingInset;
            var east = FlatEast - RingInset;
            var south = FlatSouth + RingInset;
            var north = FlatNorth - RingInset;

            // The main way from the south gate to the square; on across the square and the forecourt to the hall.
            Way("Главная дорога", AxisAlong, FlatSouth, AxisAlong, -19, 7, true, true, 0);
            Way("Дорога к залу", AxisAlong, -19, AxisAlong, 49, 4, true, true, -1);
            Way("Поперечная дорога", FlatWest, CrossUp, FlatEast, CrossUp, 6, true, true, 0);
            Way("Северная улица", -130, south, -130, FlatNorth, 4, false, true, 1);
            Way("Нижняя улица", west, -62, east, -62, 3.5f, false, true, 1);
            Way("Верхняя улица", 99, 52, east, 52, 3.5f, false, true, 1);
            Way("Восточная улица", 160, south, 160, north, 3.5f, false, true, 1);
            Way("Улица академии", -78, CrossUp, -78, 10, 3, false, false, 2);
            Way("Переулок", -78, south, -78, CrossUp, 3, false, false, 2);
            Way("Переулок", -19, south, -19, north, 3, false, false, 2);
            Way("Переулок", 99, south, 99, north, 3, false, false, 2);
            Way("Переулок", 205, south, 205, north, 3, false, false, 2);
            Way("Переулок", 99, 15, east, 15, 3, false, false, 2);
            // The lane round the inside of the wall (its corners filled: each side runs on past the other's middle).
            Way("Улица у стены", west - RingHalf, south, east + RingHalf, south, RingHalf, false, false, 3);
            Way("Улица у стены", west - RingHalf, north, east + RingHalf, north, RingHalf, false, false, 3);
            Way("Улица у стены", west, south, west, north, RingHalf, false, false, 3);
            Way("Улица у стены", east, south, east, north, RingHalf, false, false, 3);
        }

        // ── What stands on the square, at the gates, along the ways ───

        /// <summary>A thing of the procedural kit (PropKit) where nothing stands within `radius` (and off the streets unless `onStreet`).</summary>
        bool Thing(string what, Vector2 at, float yaw, float radius = 0.6f, bool onStreet = false, float scale = 1)
        {
            var free = CellOf(at) >= 0;
            Around(at, radius, (cell, _) => free &= !taken[cell] && kind[cell] != KWild && (onStreet || (kind[cell] != KLane && kind[cell] != KPaved)));

            if (!free)
            {
                return false;
            }

            Props.Add(new Prop { Kind = what, At = Flat(at), Yaw = yaw, Scale = scale, Seed = random.Next() });
            Around(at, radius, (cell, _) => taken[cell] = true);

            return true;
        }

        void FlatSquareAndGates()
        {
            var east = Facing(East);
            var west = Facing(West);
            var south = Facing(South);
            var north = Facing(North);

            // The square: three stalls (the noodles, the rice, Baoshan's Gu) facing the main way, their goods beside them.
            var stalls = new[] { (W(21, -9), east), (W(21, 3), east), (W(59, -9), west) };

            for (var i = 0; i < stalls.Length; i++)
            {
                var (at, yaw) = stalls[i];
                Thing("stall", at, yaw, 1.7f, true);
                Places.Add(($"stall {i}", Offset(at, yaw, 0, -1.5f), yaw));
            }

            Decor("prop_market_goods", W(17, -3), east, 0.8f, true, true);
            Decor("prop_market_goods", W(63, -3), west, 0.8f, true, true);
            // The square's well, the old camphor with its ring of stones to sit on, two stone benches, the notice board and the drum.
            Decor("prop_village_well", W(59, 5), west, 1, true, true);
            Places.Add(("well", W(56.4f, 5), east));
            var tree = W(25, 9);
            Thing("old tree", tree, R() * 360, 3.4f, true);

            for (var i = 0; i < 4; i++)
            {
                var seat = tree + (Along * Mathf.Cos(i * Mathf.PI / 2) - Down * Mathf.Sin(i * Mathf.PI / 2)) * 3.2f;
                Places.Add(($"bench {i}", seat, YawFacing(tree - seat) + 180));
            }

            Decor("prop_bench_stone", W(51, 12), south, 1, true, true);
            Decor("prop_bench_stone", W(63, 12), south, 1, true, true);
            Thing("notice board", W(30, -16), south, 0.8f, true);
            Thing("drum", W(50, -16), south, 0.9f, true);

            foreach (var (a, u) in new[] { (14f, -17f), (66f, -17f), (14f, 13f), (66f, 13f) })
            {
                Thing("stone lantern", W(a, u), south, 0.6f, true);
            }

            // The forecourt: two bronze dings and two steles either side of the way up, lotus vats and pines before the hall.
            foreach (var side in new[] { -1, 1 })
            {
                Decor("prop_bronze_ding", W(AxisAlong + side * 16, 30), south, 1, true, true);
                Decor("prop_clan_stele", W(AxisAlong + side * 16, 42), south, 1, true, true);
                Decor("prop_lotus_vat", W(AxisAlong + side * 11, 47.5f), south, 1, true, true);
                Decor("prop_bonsai_pine", W(AxisAlong + side * 14, 47.5f), south, 1, false, true);
                Decor("prop_lotus_vat", W(AxisAlong + side * 34, 47.5f), south, 1, true, true);
                Thing("banner", W(AxisAlong + side * 26, 48), south, 0.5f, true);
            }

            // The academy's yard: weapon racks along its lower side, two go tables.
            foreach (var a in new[] { -104f, -96f, -60f, -52f })
            {
                Decor("prop_weapon_rack", W(a, 12), north, 1, true, true);
            }

            Decor("prop_go_table", W(-102, 40), south, 1, true, true);
            Decor("prop_go_table", W(-54, 40), south, 1, true, true);

            // Inside each gate: a weapon rack and the spears by one side of the way, a brazier by the other.
            foreach (var gate in Gates)
            {
                var inward = -gate.Out;
                var side = new Vector2(-inward.y, inward.x);
                var half = gate.Name == "gate north" ? 4 : gate.Name == "gate south" ? 7 : 6;
                Decor("prop_weapon_rack", gate.At + inward * 17 + side * (half + 3), YawFacing(-side), 1, true);
                Thing("spears", gate.At + inward * 20 + side * (half + 2.5f), YawFacing(-side), 0.8f);
                Thing("brazier", gate.At + inward * 17 - side * (half + 2.5f), 0, 0.8f);
            }
        }

        /// <summary>Lanterns (the artist's posts) along the main way and the cross way, both sides, not at a crossing; round the forecourt and at the gates.</summary>
        void FlatLanterns()
        {
            bool Street(float a, float u)
            {
                var k = KindAt(W(a, u));

                return k == KLane || k == KPaved;
            }

            void Post(float a, float u, Vector2 facing) => Decor("prop_moon_lantern_post", W(a, u), Facing(facing), 1, true, true);

            for (var u = FlatSouth + 22; u < -38; u += 14)
            {
                foreach (var side in new[] { -1, 1 })
                {
                    var edge = AxisAlong + side * 10;

                    if (!Street(edge, u) && !Street(edge, u - 5) && !Street(edge, u + 5))
                    {
                        Post(AxisAlong + side * 6.2f, u, side > 0 ? West : East);
                    }
                }
            }

            for (var a = FlatWest + 24; a < FlatEast - 20; a += 20)
            {
                if (a > 0 && a < 90)
                {
                    continue;
                }

                foreach (var side in new[] { -1, 1 })
                {
                    var edge = CrossUp + side * 9;

                    if (!Street(a, edge) && !Street(a - 5, edge) && !Street(a + 5, edge))
                    {
                        Post(a, CrossUp + side * 5.2f, side > 0 ? South : North);
                    }
                }
            }

            foreach (var u in new[] { 20f, 32f, 44f })
            {
                Post(AxisAlong - 7, u, East);
                Post(AxisAlong + 7, u, West);
            }

            for (var u = CrossUp + 18; u < FlatNorth - 16; u += 22)
            {
                Post(-130 + 3.4f, u, West);
            }
        }

        // ── Houses ────────────────────────────────────────────────────

        void FlatWishes()
        {
            void Want(BuildingKind kind, string name, string model, float scale, float along, float up, float radius = 35) =>
                wishes.Add(new Wish { Kind = kind, Name = name, Model = model, Scale = scale, Target = W(along, up), Radius = radius });

            // Round the square and down the main way: the trades people walk to.
            Want(BuildingKind.Tavern, "Харчевня", "prop_eatery", 0.85f, 62, -42);
            Want(BuildingKind.Teahouse, "Чайная", "prop_teahouse", 0.85f, -40, -40);
            Want(BuildingKind.Herbalist, "Лавка травника", "prop_herbalist", 0.9f, 115, -12);
            Want(BuildingKind.Shop, "Лавка", "prop_shop", 0.9f, 26, -72);
            Want(BuildingKind.Shop, "Лавка", "prop_shop", 0.9f, 54, -72);
            Want(BuildingKind.Shop, "Лавка", "prop_shop", 0.9f, 120, -38);
            // Among the good houses: two taverns and the inn (by the south gate).
            Want(BuildingKind.Tavern, "Таверна «Бамбуковое вино»", "prop_tavern", 0.85f, 140, 25, 40);
            Want(BuildingKind.Tavern, "Таверна «Мутное вино»", "prop_tavern", 0.85f, -100, -45, 40);
            Want(BuildingKind.Inn, "Постоялый двор", "prop_inn", 0.85f, 70, -86, 40);
            // By the wall: the smithy at the east gate, the gambling den, the granaries, the Gu nursery by the north gate (the spring is above it).
            Want(BuildingKind.Smithy, "Кузница", "prop_smithy", 0.9f, 222, -10, 40);
            Want(BuildingKind.Gambling, "Игорный дом", "prop_gambling_tent", 0.8f, 185, -75, 45);
            Want(BuildingKind.Granary, "Амбар клана", "prop_granary", 0.9f, 215, 78, 45);
            Want(BuildingKind.Granary, "Амбар", "prop_granary", 0.9f, -145, -78, 45);
            Want(BuildingKind.GuFarm, "Питомник Гу", "prop_gu_nursery", 0.85f, -145, 72, 45);
        }

        /// <summary>
        /// What may stand on a lot here, the first that fits: a wanted trade near its spot; else by the ring — the poor
        /// in the rows by the wall, shops along the main ways near the middle (`main`: the lot is on one), the good
        /// bamboo houses everywhere else — else a smaller one.
        /// </summary>
        IEnumerable<(string model, float scale, BuildingKind kind, Wish wish)> LotChoices(Vector2 local, bool main)
        {
            var world = W(local);
            var wish = wishes.Where(w => !w.Done && Vector2.Distance(w.Target, world) < w.Radius).OrderBy(w => Vector2.Distance(w.Target, world)).FirstOrDefault();

            if (wish != null)
            {
                yield return (wish.Model, wish.Scale, wish.Kind, wish);
            }

            var small = (SmallShops[random.Next(SmallShops.Length)], 1f, BuildingKind.Shop, (Wish)null);

            if (InsideDistance(world) < 30)
            {
                yield return ("prop_poor_house", 0.85f + R() * 0.1f, BuildingKind.Poor, null);
                yield return ("prop_poor_house", 0.75f, BuildingKind.Poor, null);
            }
            else if (main && Mathf.Abs(local.x - AxisAlong) < 120 && Mathf.Abs(local.y - CrossUp) < 75)
            {
                var pick = R();
                yield return pick < 0.35f ? small : pick < 0.65f ? ("prop_shop", 0.9f, BuildingKind.Shop, null) : ("guyue_bamboo_house", 0.8f, BuildingKind.Family, null);
                yield return small;
            }
            else
            {
                yield return R() < 0.05f ? small : ("guyue_bamboo_house", 0.8f, BuildingKind.Family, null);
                yield return ("guyue_bamboo_house", 0.7f, BuildingKind.Family, null);
                yield return ("prop_poor_house", 0.9f, BuildingKind.Poor, null);
            }
        }

        void FlatHouses()
        {
            FlatWishes();

            foreach (var (from, to, half, order) in lotStreets.OrderBy(s => s.order))
            {
                Lots(from, to, half, 1, order == 0);
                Lots(from, to, half, -1, order == 0);
            }

            AssignTrades();
        }

        /// <summary>
        /// Houses along one side of a straight street (`side`: left or right of its way): one after another, each
        /// square to it, its front `Setback` from its edge, a gap between, an alley now and then; where one does not fit
        /// (a crossing, what stands there) a smaller one is tried, else the next metre.
        /// </summary>
        void Lots(Vector2 from, Vector2 to, float half, int side, bool main)
        {
            var dir = (to - from).normalized;
            var normal = new Vector2(-dir.y, dir.x) * side;
            var length = Vector2.Distance(from, to);
            var yaw = Facing(-normal);
            var s = 0f;
            var since = 0;

            while (s < length - 4)
            {
                var placed = 0f;

                foreach (var (model, scale, kind_, wish) in LotChoices(from + dir * s + normal * (half + 6), main))
                {
                    var size = QingMaoBuilder.VillageGlb[model].Size * scale;

                    if (s + size.x > length)
                    {
                        continue;
                    }

                    var local = from + dir * (s + size.x / 2) + normal * (half + Setback + size.y / 2);
                    var centre = W(local);

                    if (!Free(centre, size / 2, yaw, 0.7f))
                    {
                        continue;
                    }

                    AddHouse(centre, size, yaw, kind_, model, scale, wish);
                    placed = size.x;
                    break;
                }

                if (placed <= 0)
                {
                    s += 1;
                    continue;
                }

                since++;
                var gap = 1.6f + R() * 0.8f;

                if (since >= 3 + random.Next(3))
                {
                    gap = 4.5f;
                    since = 0;
                }

                s += placed + gap;
            }
        }

        void AddHouse(Vector2 centre, Vector2 size, float yaw, BuildingKind kind_, string model, float scale, Wish wish)
        {
            var b = new Building { Kind = kind_, Centre = centre, Size = size, Yaw = yaw, Floor = Level0, Seed = random.Next(), Lit = wish != null || R() < 0.55f, Model = model, Scale = scale, Pad = true };

            if (wish != null)
            {
                wish.Done = true;
                b.Name = wish.Name;
            }

            Buildings.Add(b);
            Take(centre, size / 2, yaw);

            if (kind_ == BuildingKind.Family || kind_ == BuildingKind.Poor)
            {
                Doorsteps.Add((Offset(centre, yaw, 0, size.y / 2 + 1.1f), yaw));
            }
        }

        // ── What lies about ───────────────────────────────────────────

        /// <summary>One of the artist's props off the streets, where nothing stands within `margin` of it.</summary>
        bool Model(string model, Vector2 at, float yaw, float scale = 1, bool solid = true, float margin = 0.6f)
        {
            var half = QingMaoBuilder.VillageGlb[model].Size * scale / 2;

            if (!Free(at, half, yaw, margin))
            {
                return false;
            }

            ModelProps.Add((model, at, yaw, scale, solid));
            Inside(at, half, yaw, 0, cell => taken[cell] = true);

            return true;
        }

        void FlatDecor()
        {
            // By each building's front and sides, what its trade or its household keeps there (never on the street).
            foreach (var b in Buildings.ToList())
            {
                var hx = b.Size.x / 2;
                var front = b.Size.y / 2 + 0.6f;
                Vector2 At(float x, float z) => Offset(b.Centre, b.Yaw, x, z);
                var aside = R() < 0.5f ? 1 : -1;

                switch (b.Kind)
                {
                    case BuildingKind.Family:
                    case BuildingKind.Poor:
                        for (var i = 0; i < random.Next(3); i++)
                        {
                            Thing(DoorThings[random.Next(DoorThings.Length)], At((R() < 0.5f ? -1 : 1) * (1.2f + R() * (hx - 1.4f)), front), b.Yaw + (R() - 0.5f) * 60, 0.4f);
                        }

                        if (b.Kind == BuildingKind.Poor && R() < 0.6f)
                        {
                            var thing = new[] { "prop_firewood_stack", "prop_drying_rack", "prop_barrels" }[random.Next(3)];
                            var scale = thing == "prop_barrels" ? 0.55f : 1;
                            var size = QingMaoBuilder.VillageGlb[thing].Size * scale;
                            Model(thing, At(aside * (hx + size.y / 2 + 0.6f), 0), b.Yaw + 90 * aside, scale, true, 0.4f);
                        }
                        else if (R() < 0.3f)
                        {
                            Thing(R() < 0.5f ? "firewood" : "water jar", At(aside * (hx + 0.7f), (R() - 0.5f) * b.Size.y * 0.6f), b.Yaw + 90, 0.6f);
                        }

                        break;
                    case BuildingKind.Shop:
                        Thing("crates", At(-hx + 0.8f, front), b.Yaw + R() * 20, 0.6f);
                        Thing("goods basket", At(hx - 0.8f, front), b.Yaw, 0.5f);
                        Thing("sacks", At(hx + 0.8f, 0), b.Yaw, 0.6f);
                        break;
                    case BuildingKind.Tavern:
                    case BuildingKind.Teahouse:
                        Thing(b.Kind == BuildingKind.Tavern ? "wine jars" : "water jar", At(hx + 0.8f, front - 1), b.Yaw, 0.6f);
                        Thing("bench", At(-hx + 1.2f, front), b.Yaw + 180, 0.7f);
                        Thing(b.Kind == BuildingKind.Tavern ? "barrel" : "flower pots", At(-hx - 0.8f, front - 1.2f), b.Yaw, 0.5f);
                        break;
                    case BuildingKind.Smithy:
                        Thing("anvil", At(hx + 1.2f, front - 1.5f), b.Yaw + 15, 0.6f);
                        Thing("charcoal", At(-hx - 1, 0), b.Yaw, 0.7f);
                        Model("prop_weapon_rack", At(hx + 1.6f, -1), b.Yaw - 90, 1, true, 0.4f);
                        break;
                    case BuildingKind.Herbalist:
                        Model("prop_drying_rack", At(hx + 1.4f, 0), b.Yaw + 90, 1, true, 0.4f);
                        Thing("jars", At(-hx + 0.6f, front), b.Yaw, 0.5f);
                        break;
                    case BuildingKind.Granary:
                        Thing("sacks", At(-hx - 0.8f, front - 1), b.Yaw, 0.6f);
                        Thing("cart", At(hx + 1.8f, 0), b.Yaw + 90, 1.6f);
                        break;
                    case BuildingKind.GuFarm:
                        for (var i = 0; i < 4; i++)
                        {
                            Thing("gu pots", At(hx + 1.2f, -b.Size.y / 2 + 1.5f + i * 2.2f), b.Yaw, 0.6f);
                        }

                        break;
                    case BuildingKind.Gambling:
                        Thing("red lantern pole", At(hx + 0.8f, front - 0.6f), b.Yaw, 0.4f);
                        break;
                    case BuildingKind.Inn:
                        Thing("cart", At(hx + 1.8f, 0), b.Yaw + 90, 1.6f);
                        Thing("hay", At(-hx - 1.4f, 0), b.Yaw, 1.1f);
                        break;
                }
            }

            // Behind the houses their yards: a vegetable bed the house's width (fenced low), or a tree, or bamboo.
            foreach (var b in Buildings.Where(b => b.Kind == BuildingKind.Family || b.Kind == BuildingKind.Poor).ToList())
            {
                var pick = R();
                var back = b.Size.y / 2 + 1.2f;

                if (pick < 0.5f)
                {
                    foreach (var depth in new[] { 2.2f, 1.6f, 1.1f })
                    {
                        var half = new Vector2(b.Size.x / 2 - 1.2f, depth);
                        var at = Offset(b.Centre, b.Yaw, 0, -(back + depth));

                        if (Free(at, half, b.Yaw, 0.6f))
                        {
                            Gardens.Add((at, half, b.Yaw));
                            Take(at, half, b.Yaw, KGarden);
                            break;
                        }
                    }
                }
                else if (pick < 0.75f)
                {
                    Thing(R() < 0.3f ? "pine tree" : "tree", Offset(b.Centre, b.Yaw, (R() - 0.5f) * b.Size.x * 0.5f, -(back + 1.4f)), R() * 360, 1.4f, false, 0.85f + R() * 0.3f);
                }
                else if (pick < 0.85f)
                {
                    Model("prop_bamboo_grove", Offset(b.Centre, b.Yaw, 0, -(back + 3.4f)), b.Yaw, 0.75f, false, 0.6f);
                }
            }

            // A well for each quarter of the houses, bamboo along the wall where room is left, a tree or two.
            var wells = new List<Vector2>();

            for (var tries = 0; tries < 600 && wells.Count < 8; tries++)
            {
                var local = new Vector2(Mathf.Lerp(FlatWest + 20, FlatEast - 20, R()), Mathf.Lerp(FlatSouth + 20, FlatNorth - 20, R()));
                var at = W(local);

                if (wells.Any(w => Vector2.Distance(w, at) < 70) || Vector2.Distance(at, Square.Centre) < 60)
                {
                    continue;
                }

                if (Model("prop_village_well", at, Facing(South), 1, true, 1.2f))
                {
                    wells.Add(at);
                }
            }

            for (int tries = 0, groves = 0; tries < 1500 && groves < 24; tries++)
            {
                var local = new Vector2(Mathf.Lerp(FlatWest + 14, FlatEast - 14, R()), Mathf.Lerp(FlatSouth + 14, FlatNorth - 14, R()));

                if (InsideDistance(W(local)) < 45 && Model("prop_bamboo_grove", W(local), Facing(South), 0.8f, false, 0.8f))
                {
                    groves++;
                }
            }

            for (int tries = 0, trees = 0; tries < 800 && trees < 25; tries++)
            {
                var local = new Vector2(Mathf.Lerp(FlatWest + 14, FlatEast - 14, R()), Mathf.Lerp(FlatSouth + 14, FlatNorth - 14, R()));

                if (Thing(R() < 0.3f ? "pine tree" : "tree", W(local), R() * 360, 2, false, 0.85f + R() * 0.4f))
                {
                    trees++;
                }
            }

            // Behind the great hall, a row of bamboo.
            foreach (var a in new[] { 16f, 30f, 50f, 64f })
            {
                Model("prop_bamboo_grove", W(a, 87), Facing(South), 0.7f, false, 0.3f);
            }

            // Fallen leaves about the streets and yards.
            for (var i = 0; i < 700; i++)
            {
                var at = W(Mathf.Lerp(FlatWest + 8, FlatEast - 8, R()), Mathf.Lerp(FlatSouth + 8, FlatNorth - 8, R()));
                var cell = CellOf(at);

                if (cell >= 0 && !taken[cell] && kind[cell] != KWild && kind[cell] != KPaved)
                {
                    Props.Add(new Prop { Kind = "leaves", At = Flat(at), Yaw = R() * 360, Scale = 0.6f + R() * 0.8f, Seed = random.Next() });
                }
            }
        }

        /// <summary>Vegetable gardens behind the houses, in the room the rows leave, fenced low.</summary>
        void FlatGardens()
        {
            for (var tries = 0; tries < 1500 && Gardens.Count < 200; tries++)
            {
                var local = new Vector2(Mathf.Lerp(FlatWest + 14, FlatEast - 14, R()), Mathf.Lerp(FlatSouth + 14, FlatNorth - 14, R()));
                var p = W(local);
                var half = new Vector2(2.4f + R() * 1.6f, 1.4f + R() * 0.8f);
                var yaw = Facing(R() < 0.5f ? South : East);

                if (KindAt(p) != KGround || !Free(p, half, yaw, 0.8f))
                {
                    continue;
                }

                Gardens.Add((p, half, yaw));
                Take(p, half, yaw, KGarden);
            }
        }
    }
}
