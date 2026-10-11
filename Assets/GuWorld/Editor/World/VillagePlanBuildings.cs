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

            if (head != null)
            {
                Buildings.Add(new Building { Kind = big ? BuildingKind.Pavilion : BuildingKind.Hall, Centre = head.Centre, Size = head.Half * 2, Yaw = head.Yaw, Floor = head.Level, Name = big ? "Павильон клана" : "Зал клана", Lit = true });
                Take(head.Centre, head.Half, head.Yaw);
                Places.Add(("pavilion", head.Centre + Down * (head.Half.y + 2.5f), YawFacing(-Down)));
            }

            foreach (var (pad, kind, name) in new[] { ("shrine", BuildingKind.Ancestral, "Зал предков"), ("academy", BuildingKind.Academy, "Академия"), ("hostel", BuildingKind.Hostel, "Общежитие академии") })
            {
                var p = Find(pad);

                if (p == null)
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
                Props.Add(new Prop { Kind = "well", At = Flat(well), Yaw = Square.Yaw });
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

        /// <summary>The houses: shoulder to shoulder along every lane, a little askew, each as deep as the terrace lets it be.</summary>
        void PlaceHouses()
        {
            var big = Place.Clan == Clan.GuYue;
            var lanes = Streets.Where(s => !s.Stair).OrderByDescending(s => s.Main).ToList();
            var seed = 1000;

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

            AssignTrades();
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

        bool TryHouse(Vector2 centre, Vector2 size, float yaw, Street lane, bool edge, ref int seed, BuildingKind? kind_ = null, bool overhang = false)
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

            if (!overhang && high - low > 2.8f)
            {
                return false;
            }

            var floor = overhang ? frontGround + 0.15f : high + 0.12f;
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
            };

            // A shed is small whatever room there was.
            if (building.Kind == BuildingKind.Shed)
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

            // Round the square: shops, a tavern, a teahouse, facing it.
            if (Square != null)
            {
                var near = houses.Where(b => Vector2.Distance(b.Centre, Square.Centre) < Square.Half.magnitude + 16).OrderBy(b => Vector2.Distance(b.Centre, Square.Centre)).ToList();

                for (var i = 0; i < near.Count; i++)
                {
                    Make(near[i], i == 0 ? BuildingKind.Tavern : i == 1 ? BuildingKind.Teahouse : BuildingKind.Shop, i == 0 ? "Таверна у площади" : null);
                }
            }

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
                Props.Add(new Prop { Kind = "weapon rack", At = Flat(gate.At + inward * 6 + side * 4), Yaw = YawFacing(-side) });
                Props.Add(new Prop { Kind = "spears", At = Flat(gate.At + inward * 5 - side * 4.2f), Yaw = YawFacing(side) });
                Props.Add(new Prop { Kind = "brazier", At = Flat(gate.At + inward * 7 - side * 3), Yaw = 0 });
            }
        }

        byte KindOfCell(int cell) => kind[cell];
    }
}
