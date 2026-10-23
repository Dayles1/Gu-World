using System.Collections.Generic;
using System.Linq;
using GuWorld.World;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>The village plan's buildings and the things lying about: where each house stands, what it is, what is round it.</summary>
    public partial class VillagePlan
    {
        /// <summary>The doorsteps of the houses people live in (for the villagers' homes).</summary>
        public readonly List<(Vector2 at, float yaw)> Doorsteps = new List<(Vector2, float)>();

        static Vector2 PointAt(List<Vector2> line, float distance, out Vector2 tangent)
        {
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var length = Vector2.Distance(line[i], line[i + 1]);

                if (distance <= length || i + 2 == line.Count)
                {
                    tangent = (line[i + 1] - line[i]).normalized;

                    return Vector2.Lerp(line[i], line[i + 1], Mathf.Clamp01(distance / Mathf.Max(0.001f, length)));
                }

                distance -= length;
            }

            tangent = Vector2.right;

            return line[line.Count - 1];
        }

        /// <summary>The great buildings on their pads, and the places people go to round them.</summary>
        void PlaceSpecial()
        {
            Pad Find(string name) => Pads.FirstOrDefault(p => p.Name == name);
            var head = Find("pavilion");
            var big = Place.Clan == Clan.GuYue;

            // The artist's great buildings, each on its pad, its front down the slope (the market row's up, to the square).
            foreach (var pair in padBuildings)
            {
                var pad = Find(pair.Key);
                var (model, scale, kind, label, turn) = pair.Value;
                var size = QingMaoBuilder.VillageGlb[model].Size * scale;
                Buildings.Add(new Building { Kind = kind, Centre = pad.Centre, Size = size, Yaw = pad.Yaw + turn, Floor = pad.Level, Name = label, Lit = true, Model = model, Scale = scale, Pad = true });
                Take(pad.Centre, pad.Half, pad.Yaw);
                var front = Quaternion.Euler(0, pad.Yaw + turn, 0) * Vector3.forward;
                // Before its front, looking at it (as the pavilion's place always was).
                Places.Add((pair.Key, pad.Centre + new Vector2(front.x, front.z) * (size.y / 2 + 2.5f), pad.Yaw + turn + 180));
            }

            if (Glb)
            {
                foreach (var pair in padBuildings)
                {
                    var pad = Find(pair.Key);
                    var turn = Quaternion.Euler(0, pad.Yaw, 0);
                    Vector2 Front(float x, float z)
                    {
                        var v = turn * new Vector3(x, 0, z);
                        return pad.Centre + new Vector2(v.x, v.z);
                    }

                    var size = QingMaoBuilder.VillageGlb[pair.Value.model].Size * pair.Value.scale;
                    var edge = size.y / 2 + 0.6f;

                    if (pair.Key == "tavern great" || pair.Key == "teahouse")
                    {
                        Decor("prop_bench_wood", Front(-3, edge + 1.5f), pad.Yaw + 180, 1, true, true);
                        Decor("prop_bench_wood", Front(3, edge + 1.5f), pad.Yaw + 180, 1, true, true);
                    }

                    if (pair.Key != "vault" && pair.Key != "market")
                    {
                        Decor("prop_lotus_vat", Front(-6.5f, edge), pad.Yaw, 1, true, true);
                        Decor("prop_lotus_vat", Front(6.5f, edge), pad.Yaw, 1, true, true);
                        Decor("prop_bonsai_pine", Front(-8.5f, edge), pad.Yaw, 1, false, true);
                        Decor("prop_bonsai_pine", Front(8.5f, edge), pad.Yaw, 1, false, true);
                    }
                }

                var academyYard = Find("academy yard");

                if (academyYard != null)
                {
                    var turn = Quaternion.Euler(0, academyYard.Yaw, 0);
                    Vector2 In(float x, float z)
                    {
                        var v = turn * new Vector3(x, 0, z);
                        return academyYard.Centre + new Vector2(v.x, v.z);
                    }

                    for (var i = -1; i <= 1; i++)
                    {
                        Decor("prop_weapon_rack", In(i * 5.5f, -academyYard.Half.y + 1), academyYard.Yaw + 180, 1, true, true);
                    }

                    Decor("prop_go_table", In(academyYard.Half.x - 3, academyYard.Half.y - 3), academyYard.Yaw, 1, true, true);
                    Decor("prop_moon_gate", In(0, academyYard.Half.y + 0.5f), academyYard.Yaw, 1, true, true);
                    Decor("prop_go_table", In(-academyYard.Half.x + 3, academyYard.Half.y - 3), academyYard.Yaw, 1, true, true);
                }
            }

            if (head != null && !Glb)
            {
                Buildings.Add(new Building { Kind = big ? BuildingKind.Pavilion : BuildingKind.Hall, Centre = head.Centre, Size = head.Half * 2, Yaw = head.Yaw, Floor = head.Level, Name = big ? "Павильон клана" : "Зал клана", Lit = true });
                Take(head.Centre, head.Half, head.Yaw);
                Places.Add(("pavilion", head.Centre + Down * (head.Half.y + 2.5f), YawFacing(-Down)));
            }

            foreach (var (pad, kind, name) in new[] { ("shrine", BuildingKind.Ancestral, "Зал предков"), ("academy", BuildingKind.Academy, "Академия"), ("hostel", BuildingKind.Hostel, "Общежитие академии") })
            {
                var p = Find(pad);

                if (p == null || padBuildings.ContainsKey(pad))
                {
                    continue;
                }

                Buildings.Add(new Building { Kind = kind, Centre = p.Centre, Size = p.Half * 2, Yaw = p.Yaw, Floor = p.Level, Name = name, Lit = true });
                Take(p.Centre, p.Half, p.Yaw);
                Places.Add((pad, p.Centre + Down * (p.Half.y + 2), YawFacing(-Down)));
            }

            var yard = Find("academy yard");

            if (yard != null)
            {
                Places.Add(("academy yard", yard.Centre, YawFacing(-Down)));
                Take(yard.Centre, yard.Half * 0.4f, yard.Yaw, KYard);
            }

            // The market on the square: three stalls along its lower side and a well; the square itself.
            if (Square != null)
            {
                var turn = Quaternion.Euler(0, Square.Yaw, 0);
                Vector2 On(float x, float z)
                {
                    var v = turn * new Vector3(x, 0, z);
                    return Square.Centre + new Vector2(v.x, v.z);
                }

                for (var i = 0; i < 3; i++)
                {
                    var at = On((i - 1) * Square.Half.x * 0.55f, Square.Half.y * 0.55f);
                    Props.Add(new Prop { Kind = "stall", At = Flat(at), Yaw = Square.Yaw + 180, Seed = i });
                    Places.Add(($"stall {i}", at + (Square.Centre - at).normalized * -0.9f, YawFacing(Square.Centre - at)));
                }

                var well = On(Square.Half.x * 0.7f, -Square.Half.y * 0.45f);

                if (Glb)
                {
                    Decor("prop_village_well", well, Square.Yaw, 1, true, true);

                    // Behind each stall its goods; the clan's stele and its ding at the square's top, under the hall's stair.
                    for (var i = 0; i < 3; i++)
                    {
                        Decor("prop_market_goods", On((i - 1) * Square.Half.x * 0.55f, Square.Half.y * 0.55f + 2.2f), Square.Yaw + 180, 0.8f, true, true);
                    }

                    Decor("prop_clan_stele", On(-9, -Square.Half.y + 3.2f), Square.Yaw + 180, 1, true, true);
                    Decor("prop_bronze_ding", On(9, -Square.Half.y + 3), Square.Yaw, 1, true, true);

                    foreach (var x in new[] { -0.45f, 0.45f })
                    {
                        Decor("prop_bench_stone", On(Square.Half.x * x, 0), Square.Yaw + 90, 1, true, true);
                    }
                }
                else
                {
                    Props.Add(new Prop { Kind = "well", At = Flat(well), Yaw = Square.Yaw });
                }
                Places.Add(("well", well + Along * 1.8f, YawFacing(-Along)));

                var tree = On(-Square.Half.x * 0.72f, -Square.Half.y * 0.4f);
                Props.Add(new Prop { Kind = "old tree", At = Flat(tree), Yaw = R() * 360 });
                Props.Add(new Prop { Kind = "notice board", At = Flat(On(Square.Half.x * 0.2f, -Square.Half.y * 0.85f)), Yaw = Square.Yaw + 180 });
                Props.Add(new Prop { Kind = "drum", At = Flat(On(-Square.Half.x * 0.25f, -Square.Half.y * 0.8f)), Yaw = Square.Yaw + 180 });

                foreach (var corner in new[] { On(-Square.Half.x + 1, Square.Half.y - 1), On(Square.Half.x - 1, Square.Half.y - 1), On(-Square.Half.x + 1, -Square.Half.y + 1), On(Square.Half.x - 1, -Square.Half.y + 1) })
                {
                    Props.Add(new Prop { Kind = "stone lantern", At = Flat(corner), Yaw = Square.Yaw });
                }

                for (var i = 0; i < 4; i++)
                {
                    Places.Add(($"bench {i}", On(-Square.Half.x * 0.72f + Mathf.Cos(i * 1.57f) * 3.2f, -Square.Half.y * 0.4f + Mathf.Sin(i * 1.57f) * 3.2f), YawFacing(tree - On(-Square.Half.x * 0.72f + Mathf.Cos(i * 1.57f) * 3.2f, -Square.Half.y * 0.4f + Mathf.Sin(i * 1.57f) * 3.2f)) + 180));
                }
            }
        }

        static Vector3 Flat(Vector2 p) => new Vector3(p.x, float.NaN, p.y);

        /// <summary>
        /// One of the artist's props (VillageModels.cs) where its footprint is free — on open ground, or on a paved
        /// place or pad when `paved` (only not where something stands already); its ground taken. Whether it went in.
        /// </summary>
        bool Decor(string model, Vector2 at, float yaw, float scale = 1, bool solid = true, bool paved = false)
        {
            var half = QingMaoBuilder.VillageGlb[model].Size * scale / 2;
            var free = true;

            if (paved)
            {
                Inside(at, half, yaw, 0.1f, cell => free &= !taken[cell] && kind[cell] != KWild && kind[cell] != KStair && rampOf[cell] < 0);
            }
            else
            {
                var cell = CellOf(at);
                free = cell >= 0 && rampOf[cell] < 0 && Free(at, half, yaw, 0.1f);
            }

            if (!free)
            {
                return false;
            }

            ModelProps.Add((model, at, yaw, scale, solid));
            Inside(at, half, yaw, 0, cell => taken[cell] = true);

            return true;
        }

        /// <summary>A trade the village wants near a spot (built from the artist's models): what, where, how far from there it may stand.</summary>
        class Wish
        {
            public BuildingKind Kind;
            public string Name;
            public string Model;
            public float Scale;
            public Vector2 Target;
            public float Radius;
            public bool Done;
        }

        readonly List<Wish> wishes = new List<Wish>();

        /// <summary>The trades of a village built from models, each wanted near its spot (as AssignTrades has them for the procedural one).</summary>
        void WishTrades()
        {
            Vector2 Gate(string name) => Gates.FirstOrDefault(g => g.Name == name)?.At ?? Centre;
            void Want(BuildingKind kind, string name, string model, float scale, Vector2 target, float radius = 55) =>
                wishes.Add(new Wish { Kind = kind, Name = name, Model = model, Scale = scale, Target = target, Radius = radius });

            // In the middle, by the main way and the square: the trades people walk to.
            Want(BuildingKind.Tavern, "Харчевня", "prop_eatery", 0.85f, World(RadiusAlong * 0.12f, -RadiusUp * 0.3f));
            Want(BuildingKind.Teahouse, "Чайная", "prop_teahouse", 0.85f, World(-RadiusAlong * 0.14f, -RadiusUp * 0.22f));
            Want(BuildingKind.Herbalist, "Лавка травника", "prop_herbalist", 0.9f, World(RadiusAlong * 0.2f, RadiusUp * 0.08f));
            Want(BuildingKind.Shop, "Лавка", "prop_shop", 0.9f, World(-RadiusAlong * 0.12f, -RadiusUp * 0.36f));
            Want(BuildingKind.Shop, "Лавка", "prop_shop", 0.9f, World(RadiusAlong * 0.12f, -RadiusUp * 0.45f));
            Want(BuildingKind.Shop, "Лавка", "prop_shop", 0.9f, World(RadiusAlong * 0.28f, -RadiusUp * 0.1f));
            // The second ring: taverns and an inn among the good houses.
            Want(BuildingKind.Tavern, "Таверна «Бамбуковое вино»", "prop_tavern", 0.85f, World(RadiusAlong * 0.4f, -RadiusUp * 0.25f));
            Want(BuildingKind.Tavern, "Таверна «Мутное вино»", "prop_tavern", 0.85f, World(-RadiusAlong * 0.38f, -RadiusUp * 0.42f));
            Want(BuildingKind.Inn, "Постоялый двор", "prop_inn", 0.85f, Gate("gate west"), 70);
            // The edge: the rough trades, the stores, the Gu farm by its spring.
            Want(BuildingKind.Smithy, "Кузница", "prop_smithy", 0.9f, Gate("gate east"), 70);
            Want(BuildingKind.Gambling, "Игорный дом", "prop_gambling_tent", 0.8f, World(RadiusAlong * 0.62f, -RadiusUp * 0.35f), 70);
            Want(BuildingKind.Granary, "Амбар клана", "prop_granary", 0.9f, World(RadiusAlong * 0.6f, RadiusUp * 0.5f), 70);
            Want(BuildingKind.Granary, "Амбар", "prop_granary", 0.9f, World(-RadiusAlong * 0.62f, RadiusUp * 0.45f), 70);
            Want(BuildingKind.GuFarm, "Питомник Гу", "prop_gu_nursery", 0.85f, QingMao.Spring, 90);
        }

        static readonly string[] SmallShops = { "prop_shop_small_a", "prop_shop_small_b", "prop_shop_small_c" };

        /// <summary>
        /// What stands on a lane's next lot when the village is built from models: a wanted trade whose spot is near,
        /// else a small shop now and then near the middle, a poor house at the edge, a family's bamboo house elsewhere.
        /// The lot's size is the model's.
        /// </summary>
        (string model, float scale, BuildingKind kind, Wish wish) LotModel(Vector2 p, bool edge)
        {
            var wish = wishes.Where(w => !w.Done && Vector2.Distance(w.Target, p) < w.Radius).OrderBy(w => Vector2.Distance(w.Target, p)).FirstOrDefault();

            if (wish != null)
            {
                return (wish.Model, wish.Scale, wish.Kind, wish);
            }

            // Rings round the middle (an oval as the village is): the public middle, the good houses, the poor edge.
            var local = Local(p);
            var ring = new Vector2(local.x / RadiusAlong, local.y / RadiusUp).magnitude;

            if (edge || ring > 0.7f)
            {
                return ("prop_poor_house", 0.85f + R() * 0.15f, BuildingKind.Poor, null);
            }

            if (ring < 0.36f)
            {
                return R() < 0.45f ? (SmallShops[random.Next(SmallShops.Length)], 1, BuildingKind.Shop, null) : ("guyue_bamboo_house", 0.8f + R() * 0.06f, BuildingKind.Family, null);
            }

            return R() < 0.06f ? (SmallShops[random.Next(SmallShops.Length)], 1, BuildingKind.Shop, null) : ("guyue_bamboo_house", 0.72f + R() * 0.1f, BuildingKind.Family, null);
        }

        /// <summary>The houses: shoulder to shoulder along every lane, a little askew, each as deep as the terrace lets it be.</summary>
        void PlaceHouses()
        {
            var big = Place.Clan == Clan.GuYue;
            var lanes = Streets.Where(s => !s.Stair).OrderByDescending(s => s.Main).ToList();
            var seed = 1000;

            if (Glb)
            {
                WishTrades();
            }

            foreach (var lane in lanes)
            {
                var length = Length(lane.Points);

                foreach (var side in new[] { 1, -1 })
                {
                    var s = 1 + R() * 2;
                    var sinceAlley = 0;

                    while (s < length - 2)
                    {
                        var p = PointAt(lane.Points, s, out var tangent);
                        var normal = new Vector2(-tangent.y, tangent.x) * side;
                        var uphill = Warp(p + normal * 4) > Warp(p - normal * 4);
                        var inside = InsideDistance(p);
                        var edge = inside < 34;

                        if (Glb)
                        {
                            s += GlbLot(lane, s, side, uphill, edge, ref seed, ref sinceAlley);

                            continue;
                        }

                        var width = edge ? 3.6f + R() * 3 : 4.3f + R() * 4.6f;

                        if (Place.Clan != Clan.GuYue)
                        {
                            width += 0.6f;
                        }

                        var mid = PointAt(lane.Points, s + width / 2, out var midTangent);
                        var n = new Vector2(-midTangent.y, midTangent.x) * side;
                        var setback = lane.Half + 0.25f + R() * (edge ? 0.5f : 1.1f);
                        var depthRoom = Room(mid + n * setback, n, uphill);
                        var depth = Mathf.Min(depthRoom - 0.3f, (edge ? 3.6f : 4.6f) + R() * 3.4f);
                        var placed = false;

                        if (uphill && depth >= 3.2f)
                        {
                            placed = TryHouse(mid + n * (setback + depth / 2), new Vector2(width, depth), YawFacing(-n) + (R() - 0.5f) * 8, lane, edge, ref seed);

                            // A house or a shed behind, reached by the alley beside it.
                            if (placed && depthRoom - depth > 5.5f && R() < 0.55f)
                            {
                                var backDepth = Mathf.Min(depthRoom - depth - 1.2f, 3 + R() * 3.5f);
                                var backWidth = Mathf.Min(width, 3 + R() * 3.5f);
                                TryHouse(mid + n * (setback + depth + 0.9f + backDepth / 2) + midTangent * (R() - 0.5f) * 1.5f, new Vector2(backWidth, backDepth),
                                    YawFacing(-n) + (R() - 0.5f) * 14, lane, true, ref seed, R() < 0.5f ? BuildingKind.Shed : (BuildingKind?)null);
                            }
                        }
                        else if (!uphill && R() < (big ? 0.45f : 0.3f))
                        {
                            // Over the terrace below, on stilts, its door on the lane.
                            var hang = 3.6f + R() * 2.6f;
                            placed = TryHouse(mid + n * (lane.Half + 0.3f + hang / 2), new Vector2(Mathf.Min(width, 6.5f), hang), YawFacing(-n) + (R() - 0.5f) * 6, lane, edge, ref seed, null, true);
                        }

                        sinceAlley++;
                        var gap = edge ? 0.1f + R() * 0.45f : 0.2f + R() * 1.1f;

                        // Now and then an alley between the houses.
                        if (sinceAlley > 3 + random.Next(4))
                        {
                            gap = 1.6f + R() * 0.8f;
                            sinceAlley = 0;
                        }

                        s += placed ? width + gap : 1.5f;
                    }
                }
            }

            if (Glb)
            {
                // Bamboo in the free corners of the outer ring.
                for (var tries = 0; tries < 600; tries++)
                {
                    var local = new Vector2((R() * 2 - 1) * RadiusAlong, (R() * 2 - 1) * RadiusUp);

                    if (new Vector2(local.x / RadiusAlong, local.y / RadiusUp).magnitude > 0.55f && InsideDistance(World(local.x, local.y)) > 6)
                    {
                        Decor("prop_bamboo_grove", World(local.x, local.y), R() * 360, 0.8f + R() * 0.3f, false);
                    }
                }
            }

            AssignTrades();
        }

        /// <summary>
        /// One lot along a lane in a village built from models (PlaceHouses): the model's own size, set back from the
        /// lane, its front to it — on the uphill side into the terrace, on the downhill side now and then a house out
        /// over the terrace below on stilts; a smaller house where the bigger one has no room. Returns how far along the
        /// lane to go on.
        /// </summary>
        float GlbLot(Street lane, float s, int side, bool uphill, bool edge, ref int seed, ref int sinceAlley)
        {
            var p = PointAt(lane.Points, s, out _);
            var first = LotModel(p, edge);
            // Every model stands level on its own terrace (no house out on stilts over the one below).
            var stilts = false;
            var tries = new List<(string model, float scale, BuildingKind kind, Wish wish)> { first };

            if (first.kind != BuildingKind.Poor)
            {
                tries.Add(("prop_poor_house", 0.85f, BuildingKind.Poor, null));
            }

            tries.Add((SmallShops[random.Next(SmallShops.Length)], 1, edge ? BuildingKind.Poor : BuildingKind.Shop, null));

            foreach (var (model, scale, kind, wish) in tries)
            {
                var size = QingMaoBuilder.VillageGlb[model].Size * scale;
                var mid = PointAt(lane.Points, s + size.x / 2, out var midTangent);
                var n = new Vector2(-midTangent.y, midTangent.x) * side;
                var setback = lane.Half + 0.25f + R() * (edge ? 0.5f : 1.1f);
                var placed = false;
                var room = 0f;

                if (uphill)
                {
                    room = Room(mid + n * setback, n, true);

                    if (room - 0.3f >= size.y)
                    {
                        placed = TryHouse(mid + n * (setback + size.y / 2), size, YawFacing(-n) + (R() - 0.5f) * 6, lane, edge, ref seed, kind, false, model, scale);
                    }
                }
                else if (stilts && wish == null && size.y < 8.6f)
                {
                    placed = TryHouse(mid + n * (lane.Half + 0.3f + size.y / 2), size, YawFacing(-n) + (R() - 0.5f) * 4, lane, edge, ref seed, kind, true, model, scale);
                }

                if (!placed)
                {
                    continue;
                }

                if (kind == BuildingKind.Poor && R() < 0.45f)
                {
                    var thing = new[] { "prop_firewood_stack", "prop_drying_rack", "prop_barrels", "prop_bamboo_fence", "prop_wooden_ladder" }[random.Next(5)];
                    var thingScale = thing == "prop_barrels" ? 0.55f : 1;
                    var thingSize = QingMaoBuilder.VillageGlb[thing].Size * thingScale;
                    var beside = mid + midTangent * (size.x / 2 + thingSize.x / 2 + 0.5f) * (R() < 0.5f ? 1 : -1) + n * (setback + size.y / 2);
                    Decor(thing, beside, YawFacing(-n), thingScale, true);
                }

                if (wish != null)
                {
                    wish.Done = true;
                    Buildings[Buildings.Count - 1].Name = wish.Name;
                    Buildings[Buildings.Count - 1].Lit = true;
                }

                // A poor house behind now and then, reached by the alley beside it.
                var back = QingMaoBuilder.VillageGlb["prop_poor_house"].Size * 0.8f;

                if (uphill && room - size.y > back.y + 1.4f && R() < 0.4f)
                {
                    TryHouse(mid + n * (setback + size.y + 0.9f + back.y / 2) + midTangent * (R() - 0.5f) * 1.5f, back,
                        YawFacing(-n) + (R() - 0.5f) * 12, lane, true, ref seed, BuildingKind.Poor, false, "prop_poor_house", 0.8f);
                }

                sinceAlley++;
                var gap = edge ? 0.3f + R() * 0.5f : 0.4f + R() * 1.2f;

                // Now and then an alley between the houses.
                if (sinceAlley > 2 + random.Next(4))
                {
                    gap = 1.8f + R() * 0.8f;
                    sinceAlley = 0;
                }

                return size.x + gap;
            }

            return 1.5f;
        }

        /// <summary>How far one can build from a lane's edge into its side before the terrace ends or something is in the way.</summary>
        float Room(Vector2 from, Vector2 direction, bool uphill)
        {
            var level = Terraced(Warp(from), 0.07f);

            for (var d = 0f; d < 26; d += 0.5f)
            {
                var p = from + direction * d;
                var cell = CellOf(p);

                if (cell < 0 || kind[cell] == KWild || kind[cell] == KLane || kind[cell] == KStair || kind[cell] == KPaved || padOf[cell] >= 0 || taken[cell] || stairNear[cell] < 1.6f || InsideDistance(p) < 2.5f)
                {
                    return d;
                }

                if (uphill && Mathf.Abs(Terraced(Warp(p), 0.07f) - level) > 0.5f)
                {
                    return d;
                }
            }

            return 26;
        }

        bool TryHouse(Vector2 centre, Vector2 size, float yaw, Street lane, bool edge, ref int seed, BuildingKind? kind_ = null, bool overhang = false, string model = null, float scale = 1)
        {
            var half = size / 2;

            if (!Free(centre, half, yaw, 0.15f))
            {
                return false;
            }

            // The ground under it: level enough for a stone footing, or falling away for stilts.
            var turn = Quaternion.Euler(0, yaw, 0);
            var front = Vector3.forward;
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            float frontGround = 0, backGround = 0;

            foreach (var (x, z) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1), (0, 0), (0, 1), (0, -1) })
            {
                var v = turn * new Vector3(x * half.x, 0, z * half.y);
                var g = Terraced(Warp(centre + new Vector2(v.x, v.z)), 0.07f);
                low = Mathf.Min(low, g);
                high = Mathf.Max(high, g);

                if (z == 1 && x == 0) frontGround = g;
                if (z == -1 && x == 0) backGround = g;
            }

            var laneLevel = Terraced(Warp(lane.Points[lane.Points.Count / 2]), 0.07f);

            if (!overhang && high - low > (model != null ? 0.35f : 2.8f))
            {
                return false;
            }

            var floor = overhang ? frontGround + 0.15f : high + (model != null ? 0.03f : 0.12f);
            var building = new Building
            {
                Kind = kind_ ?? (edge ? (R() < 0.45f ? BuildingKind.Poor : R() < 0.25f ? BuildingKind.Shed : BuildingKind.Family) : (R() < 0.08f ? BuildingKind.Shed : BuildingKind.Family)),
                Centre = centre,
                Size = size,
                Yaw = yaw,
                Floor = floor,
                DropFront = Mathf.Max(0, floor - frontGround),
                DropBack = Mathf.Max(0, floor - backGround),
                Seed = seed++,
                Lit = R() < 0.55f,
                Model = model,
                Scale = scale,
            };

            // A shed is small whatever room there was.
            if (building.Kind == BuildingKind.Shed && model == null)
            {
                building.Size = new Vector2(Mathf.Min(size.x, 2.6f + R() * 1.6f), Mathf.Min(size.y, 2.4f + R() * 1.4f));
            }

            Buildings.Add(building);
            Take(centre, half, yaw);

            if (building.Kind == BuildingKind.Family || building.Kind == BuildingKind.Poor)
            {
                var door = turn * new Vector3(0, 0, half.y + 1.1f);
                Doorsteps.Add((centre + new Vector2(door.x, door.z), yaw));
            }

            return true;
        }

        /// <summary>The trades: shops round the square and along the main ways, three taverns (lore ch. 12), the inn by the lower gate, the smithy, the herbalist, the granary, the Gu farm, the gambling den.</summary>
        void AssignTrades()
        {
            var houses = Buildings.Where(b => b.Kind == BuildingKind.Family || b.Kind == BuildingKind.Poor).ToList();
            var big = Place.Clan == Clan.GuYue;

            Building Nearest(Vector2 to, float minArea, params BuildingKind[] from)
            {
                Building best = null;

                foreach (var b in houses)
                {
                    if (from.Length > 0 && !from.Contains(b.Kind)) continue;
                    if (b.Size.x * b.Size.y < minArea) continue;

                    if (best == null || Vector2.Distance(b.Centre, to) < Vector2.Distance(best.Centre, to))
                    {
                        best = b;
                    }
                }

                return best;
            }

            void Make(Building b, BuildingKind kind, string name)
            {
                if (b == null)
                {
                    return;
                }

                b.Kind = kind;
                b.Name = name;
                b.Lit = true;
                houses.Remove(b);
                Doorsteps.RemoveAll(d => Vector2.Distance(d.at, b.Centre) < Mathf.Max(b.Size.x, b.Size.y));
            }

            Vector2 Gate(string name) => Gates.FirstOrDefault(g => g.Name == name)?.At ?? Centre;

            if (Glb)
            {
                // Built from models, the trades stand on their own lots already; one that found no lot near its spot
                // takes the nearest family house's, its model as large as that lot allows.
                foreach (var wish in wishes.Where(w => !w.Done))
                {
                    var size = QingMaoBuilder.VillageGlb[wish.Model].Size;
                    var b = Nearest(wish.Target, 0, BuildingKind.Family);

                    if (b == null)
                    {
                        continue;
                    }

                    var scale = Mathf.Min(wish.Scale, Mathf.Min(b.Size.x / size.x, b.Size.y / size.y));
                    Make(b, wish.Kind, wish.Name);
                    b.Model = wish.Model;
                    b.Scale = scale;
                    b.Size = size * scale;
                    BuildLog.Write($"plan of {Place.Id}: {wish.Name} found no lot near its spot, took a house's (scale {scale:0.00})");
                }
            }
            // Round the square: shops, a tavern, a teahouse, facing it.
            else if (Square != null)
            {
                var near = houses.Where(b => Vector2.Distance(b.Centre, Square.Centre) < Square.Half.magnitude + 16).OrderBy(b => Vector2.Distance(b.Centre, Square.Centre)).ToList();

                for (var i = 0; i < near.Count; i++)
                {
                    Make(near[i], i == 0 ? BuildingKind.Tavern : i == 1 ? BuildingKind.Teahouse : BuildingKind.Shop, i == 0 ? "Таверна у площади" : null);
                }
            }

            // The procedural village's trades: houses near their spots turned over to them.
            if (!Glb)
            {
                Make(Nearest(Gate("gate south"), 40), BuildingKind.Inn, "Постоялый двор");
                Make(Nearest(Gate("gate east"), 30), BuildingKind.Smithy, "Кузница");

                if (big)
                {
                    Make(Nearest(World(RadiusAlong * 0.45f, -RadiusUp * 0.3f), 36), BuildingKind.Tavern, "Таверна «Бамбуковое вино»");
                    Make(Nearest(World(-RadiusAlong * 0.4f, -RadiusUp * 0.45f), 36), BuildingKind.Tavern, "Таверна «Мутное вино»");
                    Make(Nearest(World(-RadiusAlong * 0.25f, RadiusUp * 0.15f), 25), BuildingKind.Herbalist, "Лавка травника");
                    Make(Nearest(World(RadiusAlong * 0.25f, -RadiusUp * 0.05f), 40), BuildingKind.Gambling, "Игорный дом");
                    Make(Nearest(World(RadiusAlong * 0.55f, RadiusUp * 0.55f), 30), BuildingKind.Granary, "Амбар клана");
                    Make(Nearest(QingMao.Spring, 30), BuildingKind.GuFarm, "Питомник Гу");
                    Make(Nearest(World(-RadiusAlong * 0.6f, RadiusUp * 0.4f), 30), BuildingKind.Granary, "Амбар");
                }
                else
                {
                    Make(Nearest(World(RadiusAlong * 0.3f, 0), 30), BuildingKind.Tavern, Place.Clan == Clan.Bai ? "Таверна «Белое вино»" : "Таверна «Медвежья желчь»");
                    Make(Nearest(World(-RadiusAlong * 0.4f, RadiusUp * 0.4f), 25), BuildingKind.Granary, "Амбар");
                }

                // Shops along the main streets near the middle.
                foreach (var b in houses.ToList())
                {
                    if (Vector2.Distance(b.Centre, Centre) < RadiusAlong * 0.45f && R() < 0.22f)
                    {
                        Make(b, BuildingKind.Shop, null);
                    }
                }
            }

            // The taverns' doors are places people drink at.
            var taverns = Buildings.Where(b => b.Kind == BuildingKind.Tavern || b.Kind == BuildingKind.Teahouse).ToList();

            for (var i = 0; i < taverns.Count; i++)
            {
                var turn = Quaternion.Euler(0, taverns[i].Yaw, 0);
                var door = turn * new Vector3(0, 0, taverns[i].Size.y / 2 + 1.6f);
                Places.Add(($"tavern {i}", taverns[i].Centre + new Vector2(door.x, door.z), taverns[i].Yaw + 180));
            }

            // Points along the main lanes for strolling (the "ring" the old roster walks).
            var mains = Streets.Where(s => s.Main && !s.Stair).ToList();

            for (var i = 0; i < 8 && mains.Count > 0; i++)
            {
                var street = mains[i % mains.Count];
                var at = PointAt(street.Points, Length(street.Points) * (0.2f + 0.6f * ((i * 0.37f) % 1)), out _);
                Places.Add(($"ring {i}", at, 0));
            }
        }

        /// <summary>Vegetable gardens in the free corners, behind low fences.</summary>
        void Gardens_()
        {
            for (var tries = 0; tries < 900; tries++)
            {
                var local = new Vector2((R() * 2 - 1) * RadiusAlong, (R() * 2 - 1) * RadiusUp);
                var p = World(local.x, local.y);

                if (InsideDistance(p) < 6 || KindAt(p) != KGround)
                {
                    continue;
                }

                var half = new Vector2(1.6f + R() * 2.4f, 1.2f + R() * 1.6f);
                var yaw = YawFacing(Down) + (R() - 0.5f) * 20;
                var level = Terraced(Warp(p), 0.07f);
                var turn = Quaternion.Euler(0, yaw, 0);
                var flat = true;

                foreach (var (x, z) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                {
                    var v = turn * new Vector3(x * half.x, 0, z * half.y);

                    if (Mathf.Abs(Terraced(Warp(p + new Vector2(v.x, v.z)), 0.07f) - level) > 0.3f)
                    {
                        flat = false;
                    }
                }

                if (!flat || !Free(p, half, yaw, 0.6f))
                {
                    continue;
                }

                Gardens.Add((p, half, yaw));
                Take(p, half, yaw, KGarden);
            }
        }

        static readonly string[] DoorThings = { "broom", "bucket", "water jar", "basket", "sacks", "flower pots", "stool" };

        /// <summary>What lies about: by doors, along walls and lanes, round the trades; trees on the terraces' edges, lamps on the main ways, leaves everywhere.</summary>
        void Scatter()
        {
            foreach (var b in Buildings)
            {
                var turn = Quaternion.Euler(0, b.Yaw, 0);
                Vector2 At(float x, float z)
                {
                    var v = turn * new Vector3(x, 0, z);
                    return b.Centre + new Vector2(v.x, v.z);
                }

                var hx = b.Size.x / 2;
                var hz = b.Size.y / 2;
                var front = hz + 0.45f;

                void Add(string kind, Vector2 at, float yaw, float scale = 1)
                {
                    var cell = CellOf(at);

                    // Not on stairs or the middle of a lane.
                    if (cell >= 0 && (KindOfCell(cell) == KStair || stairNear[cell] < 1.3f))
                    {
                        return;
                    }

                    Props.Add(new Prop { Kind = kind, At = Flat(at), Yaw = yaw, Scale = scale, Seed = random.Next() });
                }

                switch (b.Kind)
                {
                    case BuildingKind.Family:
                    case BuildingKind.Poor:
                        for (var i = 0; i < 1 + random.Next(3); i++)
                        {
                            var x = (R() < 0.5f ? -1 : 1) * (0.9f + R() * (hx - 0.9f));
                            Add(DoorThings[random.Next(DoorThings.Length)], At(x, front + R() * 0.3f), b.Yaw + (R() - 0.5f) * 60);
                        }

                        if (R() < 0.4f) Add("firewood", At((R() < 0.5f ? -1 : 1) * (hx + 0.4f), (R() - 0.5f) * hz), b.Yaw + 90);
                        if (R() < 0.18f) Add("drying rack", At((R() - 0.5f) * hx, front + 1.2f), b.Yaw);
                        if (R() < 0.15f) Add("laundry", At(hx + 1, front - 0.5f), b.Yaw + 90);
                        if (R() < 0.08f) Add("cart", At(-hx - 1.2f, 0), b.Yaw + (R() - 0.5f) * 40);
                        break;
                    case BuildingKind.Shop:
                        Add("crates", At(-hx + 0.8f, front + 0.4f), b.Yaw + R() * 20);
                        Add("goods basket", At(hx - 0.8f, front + 0.3f), b.Yaw);
                        Add("goods basket", At(hx - 1.6f, front + 0.4f), b.Yaw + 30);
                        if (R() < 0.6f) Add("sacks", At(0, front + 0.5f), b.Yaw);
                        Add("sign", At(hx + 0.3f, front), b.Yaw);
                        break;
                    case BuildingKind.Tavern:
                    case BuildingKind.Teahouse:
                        for (var i = 0; i < 2 + random.Next(2); i++)
                        {
                            Add(b.Kind == BuildingKind.Tavern ? "tavern table" : "tea table", At(-hx * 0.6f + i * 2.4f, front + 1.6f + (i % 2) * 0.6f), b.Yaw + (R() - 0.5f) * 30);
                        }

                        if (b.Kind == BuildingKind.Tavern)
                        {
                            Add("wine jars", At(hx + 0.2f, front), b.Yaw);
                            Add("wine jars", At(-hx - 0.2f, front - 1), b.Yaw + 50);
                            Add("bottles", At(hx - 0.5f, front + 1.2f), b.Yaw);
                            Add("bottles", At(-hx + 0.4f, front + 2.6f), b.Yaw + 120);
                            Add("wine flag", At(hx + 0.6f, front + 0.4f), b.Yaw);
                            Add("barrel", At(-hx - 0.5f, front + 0.3f), b.Yaw);
                        }

                        break;
                    case BuildingKind.Smithy:
                        Add("weapon rack", At(-hx + 0.6f, front + 0.6f), b.Yaw);
                        Add("weapon rack", At(hx + 0.6f, 0), b.Yaw - 90);
                        Add("spears", At(hx - 0.4f, front + 0.2f), b.Yaw);
                        Add("anvil", At(0.6f, front + 1.2f), b.Yaw + 15);
                        Add("barrel", At(-1, front + 1.5f), b.Yaw);
                        Add("charcoal", At(-hx - 0.8f, -hz + 1), b.Yaw);
                        break;
                    case BuildingKind.Herbalist:
                        Add("drying rack", At(-hx * 0.5f, front + 1), b.Yaw);
                        Add("drying rack", At(hx * 0.5f, front + 1), b.Yaw);
                        Add("jars", At(hx - 0.4f, front + 0.3f), b.Yaw);
                        Add("goods basket", At(-hx + 0.5f, front + 0.3f), b.Yaw);
                        break;
                    case BuildingKind.Granary:
                        Add("sacks", At(-hx + 1, front + 0.6f), b.Yaw);
                        Add("sacks", At(hx - 1, front + 0.6f), b.Yaw + 40);
                        Add("cart", At(hx + 1.6f, front + 1.2f), b.Yaw + 70);
                        break;
                    case BuildingKind.GuFarm:
                        for (var i = 0; i < 8; i++)
                        {
                            Add("gu pots", At(-hx + 0.8f + (i % 4) * (b.Size.x - 1.6f) / 3, front + 0.8f + (i / 4) * 1.2f), b.Yaw);
                        }

                        break;
                    case BuildingKind.Gambling:
                        Add("dice table", At(-1.2f, front + 1.5f), b.Yaw);
                        Add("dice table", At(1.4f, front + 1.8f), b.Yaw + 25);
                        Add("red lantern pole", At(hx + 0.5f, front + 0.3f), b.Yaw);
                        break;
                    case BuildingKind.Academy:
                        Add("book table", At(-4, front + 5), b.Yaw);
                        Add("book table", At(4, front + 5.5f), b.Yaw + 10);
                        Add("books", At(-6, front + 2.5f), b.Yaw);
                        Add("scrolls", At(5.5f, front + 3), b.Yaw);
                        Add("weapon rack", At(-hx + 1, front + 9), b.Yaw);
                        Add("bench", At(0, front + 8), b.Yaw + 180);
                        break;
                    case BuildingKind.Pavilion:
                    case BuildingKind.Hall:
                        Add("banner", At(-hx + 1, front + 1.5f), b.Yaw);
                        Add("banner", At(hx - 1, front + 1.5f), b.Yaw);
                        break;
                    case BuildingKind.Inn:
                        Add("cart", At(hx + 1.8f, front + 1), b.Yaw + 80);
                        Add("hitching post", At(-hx - 0.6f, front + 0.8f), b.Yaw);
                        Add("hay", At(-hx - 1.6f, front - 1), b.Yaw);
                        Add("barrel", At(hx - 0.5f, front + 0.3f), b.Yaw);
                        break;
                }
            }

            // Along the lanes: baskets for rubbish, barrels, leaves; trees on the terraces' outer edges; lamps along the main ways.
            foreach (var street in Streets)
            {
                var length = Length(street.Points);
                var lastLamp = -99f;
                var lastTree = -99f;

                for (var s = 2f; s < length - 2; s += 3 + R() * 6)
                {
                    var p = PointAt(street.Points, s, out var tangent);
                    var normal = new Vector2(-tangent.y, tangent.x) * (R() < 0.5f ? 1 : -1);
                    var edge = p + normal * (street.Half + 0.35f);
                    var cell = CellOf(edge);

                    if (cell < 0 || taken[cell] || kind[cell] == KWild || stairNear[cell] < 1.2f)
                    {
                        continue;
                    }

                    var downhill = Warp(edge + normal * 2) < Warp(p);

                    if (street.Main && s - lastLamp > 18 && !street.Stair)
                    {
                        Props.Add(new Prop { Kind = "lamp", At = Flat(edge), Yaw = YawFacing(-normal), Seed = random.Next() });
                        lastLamp = s;
                        continue;
                    }

                    if (downhill && s - lastTree > 14 && R() < 0.5f)
                    {
                        Props.Add(new Prop { Kind = R() < 0.25f ? "pine tree" : "tree", At = Flat(edge + normal * 0.8f), Yaw = R() * 360, Scale = 0.75f + R() * 0.5f, Seed = random.Next() });
                        Props.Add(new Prop { Kind = "leaves", At = Flat(edge + normal * 0.5f + tangent * (R() - 0.5f) * 3), Yaw = R() * 360, Scale = 1.4f, Seed = random.Next() });
                        lastTree = s;
                        continue;
                    }

                    var pick = R();
                    var thing = pick < 0.22f ? "rubbish basket" : pick < 0.36f ? "barrel" : pick < 0.5f ? "leaves" : pick < 0.6f ? "crates" : pick < 0.7f ? "bench" : pick < 0.78f ? "stones" : pick < 0.85f ? "rubbish heap" : "leaves";
                    Props.Add(new Prop { Kind = thing, At = Flat(edge), Yaw = YawFacing(-normal) + (R() - 0.5f) * 40, Seed = random.Next() });
                }
            }

            // Fallen leaves scattered all over the village, thicker in the corners.
            for (var i = 0; i < (Place.Clan == Clan.GuYue ? 900 : 400); i++)
            {
                var p = World((R() * 2 - 1) * RadiusAlong, (R() * 2 - 1) * RadiusUp);
                var cell = CellOf(p);

                if (cell < 0 || kind[cell] == KWild || taken[cell] || InsideDistance(p) < 1)
                {
                    continue;
                }

                Props.Add(new Prop { Kind = "leaves", At = Flat(p), Yaw = R() * 360, Scale = 0.6f + R() * 0.8f, Seed = random.Next() });
            }

            // Weapons by the gates, where the guards stand.
            foreach (var gate in Gates)
            {
                var inward = -gate.Out;
                var side = new Vector2(-inward.y, inward.x);
                if (!Glb || !Decor("prop_weapon_rack", gate.At + inward * 9 + side * 6, YawFacing(-side), 1, true, true))
                {
                    Props.Add(new Prop { Kind = "weapon rack", At = Flat(gate.At + inward * 6 + side * 4), Yaw = YawFacing(-side) });
                }
                Props.Add(new Prop { Kind = "spears", At = Flat(gate.At + inward * 5 - side * 4.2f), Yaw = YawFacing(side) });
                Props.Add(new Prop { Kind = "brazier", At = Flat(gate.At + inward * 7 - side * 3), Yaw = 0 });
            }
        }

        byte KindOfCell(int cell) => kind[cell];
    }
}
