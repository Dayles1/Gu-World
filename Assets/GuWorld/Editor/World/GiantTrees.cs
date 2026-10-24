using System.Collections.Generic;
using System.Linq;
using GuWorld.Render;
using GuWorld.World;
using UnityEditor;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// The mountain's giant trees: ancient, dark, forty to fifty metres tall, on
    /// buttress roots taller than a man, their great limbs spiralling up the trunk.
    /// The trunk can be climbed (ClimbTree), the limbs walked out along — they thin
    /// towards the tip — and sat on, and broken (TreeBranch). Six designs, each built
    /// once (its trunk, each limb a mesh of its own with its leaves, and a plain far
    /// shape), planted in old groves, round the villages, along the trails and by the
    /// pool; the ordinary woods keep clear of them.
    /// </summary>
    public static partial class QingMaoBuilder
    {
        class GiantDesign
        {
            public string Name;
            public float Height;
            public float BaseRadius;
            public float TopRadius;
            public float ClimbHeight;
            public (Mesh, Material[]) Trunk;
            public (Mesh, Material[]) Far;
            public List<(Vector3 root, float angle, float pitch, float length, float rootThickness, (Mesh, Material[]) shape, List<(Vector3 centre, Vector3 size, Quaternion turn)> solids)> Limbs = new List<(Vector3, float, float, float, float, (Mesh, Material[]), List<(Vector3, Vector3, Quaternion)>)>();
        }

        static readonly List<(Vector2 at, float radius)> giantSpots = new List<(Vector2, float)>();

        static Material GiantBark => Mat("Giant Bark", 0x16120f, 0.04f, false, BarkTexture(), 0.25f);
        static Material GiantMoss => Mat("Giant Moss", 0x22301c, 0.03f, false, BarkTexture(), 0.3f);
        static Material GiantLeaves => Mat("Giant Leaves", 0x0d160f, 0.05f);
        static Material GiantLeavesDeep => Mat("Giant Leaves Deep", 0x08100b, 0.04f);

        static float GiantRadius(GiantDesign d, float y) => Mathf.Lerp(d.BaseRadius, d.TopRadius, Mathf.Clamp01(y / d.ClimbHeight)) * Mathf.Lerp(1.35f, 1, Mathf.Clamp01(y / 3));

        static GiantDesign MakeGiant(int index)
        {
            var random = new System.Random(900 + index * 31);
            float R() => (float)random.NextDouble();
            // A hundred metres and more (the wild trees round them are now 40–85 m): the oldest trees on the mountain.
            var d = new GiantDesign { Name = $"Giant Tree {index}", Height = 96 + R() * 24, BaseRadius = 3.2f + R() * 1.4f };
            d.TopRadius = d.BaseRadius * 0.38f;
            d.ClimbHeight = d.Height * 0.72f;

            // The trunk: rings tapering up (straight: one climbs round it), its bark furrowed, moss on the lower part.
            // Its shell (the trunk and the roots' fins, not the crown's leaves) is its collider.
            var trunk = new MeshKit { Solid = new SolidShell() };
            const int sides = 14;
            var rings = 12;

            for (var k = 0; k < rings; k++)
            {
                var y0 = d.Height * k / rings;
                var y1 = d.Height * (k + 1) / rings;
                var r0 = k == 0 ? GiantRadius(d, 0) : y0 < d.ClimbHeight ? GiantRadius(d, y0) : Mathf.Lerp(d.TopRadius, 0.25f, (y0 - d.ClimbHeight) / (d.Height - d.ClimbHeight));
                var r1 = y1 < d.ClimbHeight ? GiantRadius(d, y1) : Mathf.Lerp(d.TopRadius, 0.15f, (y1 - d.ClimbHeight) / (d.Height - d.ClimbHeight));
                trunk.Cylinder(k < 3 ? GiantMoss : GiantBark, new Vector3(0, y0 - (k == 0 ? 1.5f : 0), 0), new Vector3(0, y1, 0), r0, r1, sides, k == 0);
            }

            // Buttress roots: fins flaring out from the trunk into the ground.
            var fins = 5 + random.Next(3);

            for (var i = 0; i < fins; i++)
            {
                var a = i * Mathf.PI * 2 / fins + R() * 0.4f;
                var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                var side = new Vector3(dir.z, 0, -dir.x) * 0.22f;
                var high = d.BaseRadius * 0.85f * dir + Vector3.up * (3 + R() * 2.5f);
                var foot = dir * (d.BaseRadius * (2.1f + R() * 0.8f)) + Vector3.down * 0.6f;
                var root = dir * d.BaseRadius * 0.6f + Vector3.down * 0.6f;
                trunk.Triangle(GiantMoss, root + side, foot + side, high + side, side);
                trunk.Triangle(GiantMoss, root - side, foot - side, high - side, -side);
                trunk.Quad(GiantMoss, foot + side, foot - side, high - side, high + side, dir + Vector3.up);
            }

            // The crown over the top.
            for (var i = 0; i < 7; i++)
            {
                var a = i * 2.4f;
                var r = i == 0 ? 0 : 3 + R() * 3;
                trunk.Blob(i % 2 == 0 ? GiantLeaves : GiantLeavesDeep, new Vector3(Mathf.Sin(a) * r, d.Height - 7 + R() * 6, Mathf.Cos(a) * r), new Vector3(10f, 7f, 10f) * (0.8f + R() * 0.5f), index * 10 + i);
            }

            d.Trunk = Save(trunk, $"{d.Name} Trunk");

            // The limbs, spiralling up.
            // Limbs all the way up the climb: more of them on a taller trunk.
            var count = 18 + random.Next(8);
            var far = new MeshKit();
            far.Cylinder(GiantBark, Vector3.down, new Vector3(0, d.Height, 0), d.BaseRadius * 1.1f, 0.3f, 6);
            far.Blob(GiantLeavesDeep, new Vector3(0, d.Height - 6, 0), new Vector3(16, 9, 16), index);

            for (var i = 0; i < count; i++)
            {
                var t = (float)i / (count - 1);
                var height = Mathf.Lerp(d.Height * 0.2f, d.ClimbHeight - 1.5f, t) + (R() - 0.5f) * 1.2f;
                var angle = i * 2.4f + R() * 0.5f;
                var pitch = 8 + R() * 16;
                var length = Mathf.Lerp(12.5f, 6.5f, t) * (0.8f + R() * 0.4f);
                var thick = Mathf.Lerp(1.1f, 0.55f, t) * (0.85f + R() * 0.3f);
                var tip = 0.2f;
                var root = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * (GiantRadius(d, height) - 0.15f) + Vector3.up * height;
                var limb = new MeshKit();
                var solids = new List<(Vector3, Vector3, Quaternion)>();
                // Along its own +Z, rising by its pitch, curving up a little towards the tip.
                const int segments = 6;
                var previous = Vector3.zero;

                for (var s = 1; s <= segments; s++)
                {
                    var u = (float)s / segments;
                    var z = length * u;
                    var point = new Vector3(Mathf.Sin(u * 3 + i) * 0.25f * u, z * Mathf.Tan(pitch * Mathf.Deg2Rad) * 0.4f + u * u * 1.2f, z);
                    var r0 = Mathf.Lerp(thick, tip, (float)(s - 1) / segments) / 2;
                    var r1 = Mathf.Lerp(thick, tip, u) / 2;
                    limb.Cylinder(s < 3 ? GiantMoss : GiantBark, previous, point, r0, r1, 8, s == 1);

                    // A solid box along it where it is thick enough to stand on.
                    if (r0 * 2 > 0.28f)
                    {
                        var middle = (previous + point) / 2;
                        var along = point - previous;
                        var turn = Quaternion.LookRotation(along.normalized, Vector3.up);
                        solids.Add((middle, new Vector3(Mathf.Max(0.45f, r0 * 2 * 0.95f), (r0 + r1) * 0.95f, along.magnitude + 0.05f), turn));
                    }

                    // Twigs and side shoots.
                    if (s >= 3)
                    {
                        var shoot = point + new Vector3((R() - 0.5f) * 3, 1 + R(), (R() - 0.5f) * 2);
                        limb.Cylinder(GiantBark, point, shoot, r1 * 0.6f, 0.04f, 5, false);
                    }

                    previous = point;
                }

                // Leaves on its outer half.
                for (var k = 0; k < 4 + random.Next(3); k++)
                {
                    var u = 0.45f + R() * 0.6f;
                    var at = new Vector3((R() - 0.5f) * 2.5f, length * u * Mathf.Tan(pitch * Mathf.Deg2Rad) * 0.4f + u * u * 1.2f + 1.2f + R(), length * Mathf.Min(1.05f, u));
                    limb.Blob(k % 2 == 0 ? GiantLeaves : GiantLeavesDeep, at, new Vector3(2.6f + R() * 1.6f, 1.8f + R(), 2.6f + R() * 1.6f), index * 100 + i * 10 + k);
                }

                d.Limbs.Add((root, angle, pitch, length, thick, Save(limb, $"{d.Name} Limb {i}"), solids));

                // In the far shape: the limb as a plain rod and a lump of leaves.
                var dir = new Vector3(Mathf.Sin(angle), 0.25f, Mathf.Cos(angle)).normalized;
                far.Cylinder(GiantBark, root, root + dir * length, thick / 2, 0.1f, 4, false);
                far.Blob(GiantLeaves, root + dir * length * 0.85f + Vector3.up * 1.5f, new Vector3(3.5f, 2.2f, 3.5f), i);
            }

            d.Far = Save(far, $"{d.Name} Far");

            return d;
        }

        /// <summary>Where the giant trees stand: in old groves on the slopes, round the villages (outside the walls), along the trails, by the pool.</summary>
        static void ChooseGiantSpots(List<VillagePlan> plans)
        {
            giantSpots.Clear();
            var random = new System.Random(4242);
            float R() => (float)random.NextDouble();

            bool Fits(Vector2 p)
            {
                if (giantSpots.Any(g => Vector2.Distance(g.at, p) < 34) || NearLandmark(p, 24))
                {
                    return false;
                }

                foreach (var plan in plans)
                {
                    if (plan.InsideDistance(p) > -plan.Margin)
                    {
                        return false;
                    }

                    if (plan.Gates.Any(g => Vector2.Distance(g.At, p) < 30))
                    {
                        return false;
                    }
                }

                foreach (var place in QingMao.Settlements)
                {
                    if (place.Clan == Clan.Mortal && Vector2.Distance(place.Centre, p) < place.Radius + 25)
                    {
                        return false;
                    }
                }

                if (MountainShape.NearStream(p, 30, out var d, out _, out var half, out _) && d < half + 12)
                {
                    return false;
                }

                // Clear of the trails by more than a limb's reach (the route walk climbed a low limb off a trail and fell).
                if (Vector2.Distance(p, QingMao.Pool) < QingMao.PoolRadius + 20 || TrailNear(p, 18))
                {
                    return false;
                }

                // Not on steep ground.
                var h = MountainShape.Natural(p.x, p.y);
                var e = MountainShape.Natural(p.x + 4, p.y) - h;
                var n = MountainShape.Natural(p.x, p.y + 4) - h;

                return Mathf.Sqrt(e * e + n * n) / 4 < 0.45f;
            }

            void Add(Vector2 p)
            {
                if (Fits(p))
                {
                    giantSpots.Add((p, 3 + R()));
                }
            }

            // Round each village, just outside its wall.
            foreach (var plan in plans)
            {
                for (var i = 0; i < 40; i++)
                {
                    var a = R() * Mathf.PI * 2;
                    var local = new Vector2(Mathf.Cos(a) * (plan.RadiusAlong + 40 + R() * 160), Mathf.Sin(a) * (plan.RadiusUp + 40 + R() * 140));
                    Add(plan.World(local.x, local.y));
                }
            }

            // Along the trails, every few hundred metres, a little off them.
            foreach (var trail in trails)
            {
                for (var i = 60; i < trail.Points.Count; i += 140 + random.Next(80))
                {
                    var p = trail.Points[i];
                    var next = trail.Points[Mathf.Min(trail.Points.Count - 1, i + 1)];
                    var across = new Vector2(-(next - p).y, (next - p).x).normalized;
                    Add(p + across * (R() < 0.5f ? -1 : 1) * (14 + R() * 20));
                }
            }

            // By the pool and the fall.
            for (var i = 0; i < 16; i++)
            {
                var a = R() * Mathf.PI * 2;
                Add(QingMao.Pool + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (QingMao.PoolRadius + 30 + R() * 90));
            }

            // Old groves on the slopes.
            foreach (var grove in new[] { new Vector2(-1400, 200), new Vector2(1800, -900), new Vector2(-3600, 3900), new Vector2(2600, 4600), new Vector2(-5200, -2600), new Vector2(4800, -2800) })
            {
                for (var i = 0; i < 26; i++)
                {
                    var a = R() * Mathf.PI * 2;
                    Add(grove + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R() * 260);
                }
            }

            Debug.Log($"GU World: {giantSpots.Count} giant trees");
        }

        /// <summary>Plants the giant trees in their spots: each a climbable trunk, its limbs their own breakable objects.</summary>
        static void PlantGiants()
        {
            var root = new GameObject("Giant Trees").transform;
            var designs = new List<GiantDesign>();

            for (var i = 0; i < 6; i++)
            {
                designs.Add(MakeGiant(i));
            }

            var random = new System.Random(77);

            for (var n = 0; n < giantSpots.Count; n++)
            {
                var (at2, _) = giantSpots[n];
                var design = designs[n % designs.Count];
                var yaw = (float)random.NextDouble() * 360;

                if (!InArea(at2))
                {
                    continue;
                }

                var ground = QingMao.GroundAt(new Vector3(at2.x, 0, at2.y));
                var go = new GameObject(design.Name);
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(new Vector3(at2.x, ground - 0.2f, at2.y), Quaternion.Euler(0, yaw, 0));
                var tree = go.AddComponent<ClimbTree>();
                tree.BaseRadius = design.BaseRadius;
                tree.TopRadius = design.TopRadius;
                tree.ClimbHeight = design.ClimbHeight;
                go.AddComponent<HitboxTag>().Kind = HitboxKind.Tree;

                var trunk = new GameObject("Trunk");
                trunk.transform.SetParent(go.transform, false);
                trunk.AddComponent<MeshFilter>().sharedMesh = design.Trunk.Item1;
                var trunkRenderer = trunk.AddComponent<MeshRenderer>();
                trunkRenderer.sharedMaterials = design.Trunk.Item2;
                var near = new List<Renderer> { trunkRenderer };

                // The trunk solid by its own shape: tapering evenly, its roots' fins too (stacked capsules left ledges and waists).
                if (SolidOf(design.Trunk.Item1) is Mesh trunkShell)
                {
                    trunk.AddComponent<MeshCollider>().sharedMesh = trunkShell;
                }

                var branches = new List<TreeBranch>();

                for (var i = 0; i < design.Limbs.Count; i++)
                {
                    var limb = design.Limbs[i];
                    var limbGo = new GameObject($"Branch {i}");
                    limbGo.transform.SetParent(go.transform, false);
                    limbGo.transform.localPosition = limb.root;
                    limbGo.transform.localRotation = Quaternion.Euler(0, limb.angle * Mathf.Rad2Deg, 0);
                    limbGo.AddComponent<MeshFilter>().sharedMesh = limb.shape.Item1;
                    var renderer = limbGo.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = limb.shape.Item2;
                    near.Add(renderer);
                    var branch = limbGo.AddComponent<TreeBranch>();
                    branch.Length = limb.length;
                    branch.RootThickness = limb.rootThickness;
                    branch.TipThickness = 0.2f;
                    branch.Health = 120 + limb.rootThickness * limb.rootThickness * 900;
                    branch.Tree = tree;
                    limbGo.AddComponent<HitboxTag>().Kind = HitboxKind.Branch;

                    foreach (var (centre, size, turn) in limb.solids)
                    {
                        var solid = new GameObject("Solid");
                        solid.transform.SetParent(limbGo.transform, false);
                        solid.transform.localPosition = centre;
                        solid.transform.localRotation = turn;
                        solid.AddComponent<BoxCollider>().size = size;
                    }

                    branches.Add(branch);
                }

                tree.Branches = branches.ToArray();

                var farGo = new GameObject("Far");
                farGo.transform.SetParent(go.transform, false);
                farGo.AddComponent<MeshFilter>().sharedMesh = design.Far.Item1;
                var farRenderer = farGo.AddComponent<MeshRenderer>();
                farRenderer.sharedMaterials = design.Far.Item2;
                farRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var group = go.AddComponent<LODGroup>();
                group.SetLODs(new[] { new LOD(0.12f, near.ToArray()), new LOD(0.006f, new Renderer[] { farRenderer }) });
                group.RecalculateBounds();
            }
        }
    }
}
