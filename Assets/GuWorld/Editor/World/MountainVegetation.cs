using System.Collections.Generic;
using System.Linq;
using GuWorld.World;
using UnityEditor;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// What grows on the mountain: dark firs and old pines higher up, camphor trees
    /// and cypresses lower down, Qing Mao's spear bamboo, dead trees and shrubs (the
    /// terrains' trees, each with simpler shapes for afar); grass, tall grass, ferns
    /// and wild flowers (the terrains' details — the lore's flower seas on the sunny
    /// slopes), sparse in the woods, none on rock, water or paved ground.
    /// </summary>
    public static partial class QingMaoBuilder
    {
        const int Fir = 0, RedPine = 1, Camphor = 2, Cypress = 3, BambooClump = 4, DeadTree = 5, Shrub = 6;

        static TreePrototype[] treePrototypes;
        static DetailPrototype[] detailPrototypes;

        static Material Bark => Mat("Bark", 0x2e241c, 0.05f, false, BarkTexture(), 0.5f);
        static Material PaleBark => Mat("Pale Bark", 0x5a5248, 0.05f, false, BarkTexture(), 0.5f);
        static Material Needles => Mat("Needles", 0x1c2c20, 0.05f);
        static Material PineNeedles => Mat("Pine Needles", 0x24341f, 0.05f);
        static Material Leaves => Mat("Leaves", 0x22341c, 0.08f);
        static Material DarkLeaves => Mat("Dark Leaves", 0x1a2618, 0.06f);
        static Material Culm => Mat("Bamboo Culm", 0x4a6a2c, 0.35f);
        static Material BambooLeaves => Mat("Bamboo Leaves", 0x34522a, 0.1f, true);

        static TreePrototype[] TreePrototypes()
        {
            if (treePrototypes != null)
            {
                return treePrototypes;
            }

            treePrototypes = new[]
            {
                new TreePrototype { prefab = TreePrefab("Fir", FirTree, 0.35f, 7) },
                new TreePrototype { prefab = TreePrefab("Red Pine", RedPineTree, 0.3f, 6) },
                new TreePrototype { prefab = TreePrefab("Camphor", CamphorTree, 0.45f, 4) },
                new TreePrototype { prefab = TreePrefab("Cypress", CypressTree, 0.3f, 5) },
                new TreePrototype { prefab = TreePrefab("Spear Bamboo", BambooTree, 0.45f, 6) },
                new TreePrototype { prefab = TreePrefab("Dead Tree", DeadTreeShape, 0.3f, 5) },
                new TreePrototype { prefab = TreePrefab("Shrub", ShrubShape, 0, 0) },
            };

            return treePrototypes;
        }

        /// <summary>A tree as a prefab: its shape at three levels of detail (the builder is told which), and its trunk solid.</summary>
        static GameObject TreePrefab(string name, System.Action<MeshKit, int> build, float trunkRadius, float trunkHeight)
        {
            var go = new GameObject(name);
            var lods = new List<LOD>();
            // The full tree only near, the plain shapes soon after (a tree 15 m tall: full to ~60 m, middle to ~250 m).
            var cuts = new[] { 0.2f, 0.05f, 0.004f };

            for (var level = 0; level < 3; level++)
            {
                var kit = new MeshKit();
                build(kit, level);
                var (made, used) = kit.Build($"{name} LOD{level}");
                var mesh = Keep(made, $"{Generated}/Meshes/{name} LOD{level}.asset");
                var part = new GameObject($"LOD{level}");
                part.transform.SetParent(go.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = used;
                renderer.shadowCastingMode = level == 2 ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                lods.Add(new LOD(cuts[level], new Renderer[] { renderer }));
            }

            var group = go.AddComponent<LODGroup>();
            group.SetLODs(lods.ToArray());
            group.RecalculateBounds();

            if (trunkRadius > 0)
            {
                var collider = go.AddComponent<CapsuleCollider>();
                collider.radius = trunkRadius;
                collider.height = trunkHeight;
                collider.center = new Vector3(0, trunkHeight / 2, 0);
                go.AddComponent<GuWorld.Render.HitboxTag>().Kind = GuWorld.Render.HitboxKind.Tree;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Generated}/Prefabs/{name}.prefab");
            Object.DestroyImmediate(go);

            return prefab;
        }

        static int Sides(int level, int full) => level == 0 ? full : level == 1 ? Mathf.Max(4, full / 2) : 3;

        /// <summary>A dark fir: a straight trunk under tiers of drooping boughs, narrowing to a spire.</summary>
        static void FirTree(MeshKit kit, int level)
        {
            kit.Cylinder(Bark, Vector3.zero, new Vector3(0, 17, 0), 0.38f, 0.06f, Sides(level, 7));

            if (level == 2)
            {
                kit.Cylinder(Needles, new Vector3(0, 2.5f, 0), new Vector3(0, 18, 0), 3.2f, 0, 4);

                return;
            }

            var tiers = level == 0 ? 8 : 4;

            for (var i = 0; i < tiers; i++)
            {
                var t = (float)i / tiers;
                var y = 2.6f + t * 13.5f;
                var radius = Mathf.Lerp(3.4f, 0.9f, t);
                // Each tier droops: its rim lower than where it meets the trunk.
                kit.Cylinder(Needles, new Vector3(0, y + 2.2f, 0), new Vector3(0, y - 0.3f, 0), 0.25f, radius, Sides(level, 9), false);
                kit.Cylinder(Needles, new Vector3(0, y - 0.3f, 0), new Vector3(0, y - 0.5f, 0), radius, radius * 0.6f, Sides(level, 9), false);
            }

            kit.Cylinder(Needles, new Vector3(0, 15.5f, 0), new Vector3(0, 18.5f, 0), 0.8f, 0, Sides(level, 7));
        }

        /// <summary>An old red pine, as the mountains of the south grow them: a leaning, bending trunk, flat pads of needles on crooked boughs.</summary>
        static void RedPineTree(MeshKit kit, int level)
        {
            var random = new System.Random(5);
            float R() => (float)random.NextDouble();
            var points = new[] { Vector3.zero, new Vector3(0.4f, 3.5f, 0.1f), new Vector3(1.3f, 7, -0.2f), new Vector3(1.0f, 10.5f, 0.3f), new Vector3(1.6f, 13, 0.1f) };

            for (var i = 0; i + 1 < points.Length; i++)
            {
                kit.Cylinder(PaleBark, points[i], points[i + 1], 0.42f - i * 0.07f, 0.35f - i * 0.07f, Sides(level, 6), i == 0);
            }

            if (level == 2)
            {
                kit.Box(PineNeedles, new Vector3(1.4f, 12.2f, 0), new Vector3(7, 2.2f, 6));

                return;
            }

            var pads = level == 0 ? 7 : 3;

            for (var i = 0; i < pads; i++)
            {
                var at = points[2 + i % 3] + Vector3.up * (R() * 0.5f);
                var angle = i * 2.4f + R();
                var reach = 2 + 2.2f * R();
                var tip = at + new Vector3(Mathf.Cos(angle) * reach, 0.6f + R() * 0.8f, Mathf.Sin(angle) * reach);

                if (level == 0)
                {
                    kit.Cylinder(PaleBark, at, tip, 0.14f, 0.07f, 5, false);
                }

                kit.Blob(PineNeedles, tip + Vector3.up * 0.35f, new Vector3(2.2f + R(), 0.65f, 1.9f + R()), i);
            }

            kit.Blob(PineNeedles, points[4] + Vector3.up * 0.6f, new Vector3(2.8f, 0.9f, 2.4f), 9);
        }

        /// <summary>A camphor tree: a thick, splitting trunk under a broad, dark, many-lobed crown.</summary>
        static void CamphorTree(MeshKit kit, int level)
        {
            kit.Cylinder(Bark, Vector3.zero, new Vector3(0, 4.5f, 0), 0.6f, 0.42f, Sides(level, 8));

            if (level == 2)
            {
                kit.Blob(Leaves, new Vector3(0, 8.5f, 0), new Vector3(5.5f, 3.8f, 5.5f), 1);

                return;
            }

            var limbs = new[] { new Vector3(2.4f, 7.5f, 0.6f), new Vector3(-2, 7.8f, -1.2f), new Vector3(0.3f, 8.6f, 2.1f), new Vector3(-0.5f, 9.2f, -0.2f) };

            foreach (var limb in limbs)
            {
                kit.Cylinder(Bark, new Vector3(0, 4.2f, 0), limb, 0.36f, 0.16f, Sides(level, 6), false);
            }

            var random = new System.Random(3);
            var lobes = level == 0 ? 11 : 4;

            for (var i = 0; i < lobes; i++)
            {
                var angle = i * 2.39f;
                var r = level == 0 ? 1.5f + (float)random.NextDouble() * 2.6f : 2.2f;
                var at = new Vector3(Mathf.Cos(angle) * r, 8.2f + (float)random.NextDouble() * 2.2f - r * 0.25f, Mathf.Sin(angle) * r);
                var size = level == 0 ? 2.1f + (float)random.NextDouble() : 3.2f;
                kit.Blob(i % 3 == 0 ? DarkLeaves : Leaves, at, new Vector3(size, size * 0.75f, size), i);
            }
        }

        /// <summary>A cypress: a dark green column, slightly twisted.</summary>
        static void CypressTree(MeshKit kit, int level)
        {
            kit.Cylinder(Bark, Vector3.zero, new Vector3(0, 3, 0), 0.32f, 0.26f, Sides(level, 6));
            var blobs = level == 0 ? 6 : level == 1 ? 3 : 1;

            for (var i = 0; i < blobs; i++)
            {
                var t = (float)i / blobs;
                var y = 3.5f + t * 9.5f + (level == 2 ? 4 : 0);
                var r = Mathf.Lerp(1.6f, 0.7f, t) * (level == 2 ? 1.2f : 1);
                kit.Blob(DarkLeaves, new Vector3(Mathf.Sin(i) * 0.2f, y, Mathf.Cos(i) * 0.2f), new Vector3(r, level == 2 ? 6.5f : 2.4f, r), i);
            }
        }

        /// <summary>A clump of Qing Mao's spear bamboo: tall straight culms, tips sharp, tufts of leaves up high.</summary>
        static void BambooTree(MeshKit kit, int level)
        {
            var random = new System.Random(7);
            float R() => (float)random.NextDouble();
            var culms = level == 0 ? 9 : level == 1 ? 5 : 2;

            for (var i = 0; i < culms; i++)
            {
                var angle = i * 2.4f;
                var foot = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (0.2f + 0.5f * R());
                var height = 9 + 4 * R();
                var lean = new Vector3(foot.x, 0, foot.z).normalized * (0.3f + 0.5f * R());
                var top = foot + new Vector3(lean.x, height, lean.z);
                kit.Cylinder(Culm, foot, top, 0.08f, 0.02f, level == 0 ? 5 : 3, false);

                if (level == 2)
                {
                    continue;
                }

                var tufts = level == 0 ? 4 : 2;

                for (var k = 0; k < tufts; k++)
                {
                    var at = Vector3.Lerp(foot, top, 0.55f + 0.1f * k);
                    var out_ = new Vector3(R() - 0.5f, 0, R() - 0.5f).normalized * (0.3f + 0.25f * R());
                    kit.Blob(BambooLeaves, at + out_, new Vector3(0.4f, 0.7f, 0.4f), i * 5 + k);
                }
            }

            if (level == 2)
            {
                kit.Blob(BambooLeaves, new Vector3(0, 8, 0), new Vector3(1.6f, 3, 1.6f), 1);
            }
        }

        /// <summary>A dead tree: grey, bare and crooked.</summary>
        static void DeadTreeShape(MeshKit kit, int level)
        {
            var points = new[] { Vector3.zero, new Vector3(0.3f, 4, 0.2f), new Vector3(-0.2f, 8, 0.5f), new Vector3(0.4f, 11, 0.2f) };

            for (var i = 0; i + 1 < points.Length; i++)
            {
                kit.Cylinder(PaleBark, points[i], points[i + 1], 0.4f - i * 0.1f, 0.3f - i * 0.1f, Sides(level, 6), i == 0);
            }

            if (level == 2)
            {
                return;
            }

            var random = new System.Random(11);

            for (var i = 0; i < (level == 0 ? 7 : 3); i++)
            {
                var from = points[1 + i % 3];
                var angle = i * 2.2f;
                var to = from + new Vector3(Mathf.Cos(angle) * 2.5f, 1.2f + (float)random.NextDouble() * 1.5f, Mathf.Sin(angle) * 2.5f);
                kit.Cylinder(PaleBark, from, to, 0.13f, 0.03f, Sides(level, 5), false);
            }
        }

        /// <summary>A shrub: a few low lumps of leaves.</summary>
        static void ShrubShape(MeshKit kit, int level)
        {
            var lumps = level == 0 ? 4 : 1;

            for (var i = 0; i < lumps; i++)
            {
                var angle = i * 1.9f;
                var at = level == 0 ? new Vector3(Mathf.Cos(angle) * 0.6f, 0.7f, Mathf.Sin(angle) * 0.6f) : new Vector3(0, 0.8f, 0);
                kit.Blob(i % 2 == 0 ? Leaves : DarkLeaves, at, new Vector3(1.1f, 0.85f, 1.1f) * (level == 0 ? 1 : 1.4f), i);
            }
        }

        static void AddTree(List<TreeInstance> trees, System.Random random, Vector2 corner, float size, Vector2 p, int kind, float scale)
        {
            var u = (p.x - corner.x) / size;
            var v = (p.y - corner.y) / size;

            if (u < 0.001f || u > 0.999f || v < 0.001f || v > 0.999f)
            {
                return;
            }

            float R() => (float)random.NextDouble();
            trees.Add(new TreeInstance
            {
                position = new Vector3(u, 0, v),
                widthScale = scale * (0.85f + 0.3f * R()),
                heightScale = scale,
                rotation = R() * Mathf.PI * 2,
                color = new Color32((byte)(200 + 40 * R()), (byte)(215 + 40 * R()), (byte)(200 + 40 * R()), 255),
                lightmapColor = Color.white,
                prototypeIndex = kind,
            });
        }

        /// <summary>Whether a point is kept clear of wild trees: settlements, their fine ground (it plants its own), streams, trails.</summary>
        static bool Clear(Vector2 p, List<IGroundPlan> plans, float clearance)
        {
            foreach (var plan in plans)
            {
                if (plan.Area.Contains(p))
                {
                    return true;
                }
            }

            foreach (var place in QingMao.Settlements)
            {
                if (Vector2.Distance(p, place.Centre) < place.Radius * 1.2f + clearance)
                {
                    return true;
                }
            }

            if (MountainShape.NearStream(p, 20, out var d, out _, out var half, out _) && d < half + 4 + clearance)
            {
                return true;
            }

            if (Vector2.Distance(p, QingMao.Pool) < QingMao.PoolRadius + 10)
            {
                return true;
            }

            // Room round the giant trees.
            foreach (var (at, _) in giantSpots)
            {
                if ((at - p).sqrMagnitude < 81)
                {
                    return true;
                }
            }

            return TrailNear(p, 2.6f + clearance);
        }

        /// <summary>The woods of a mountain tile: a jittered grid, firs and pines high, camphor and cypress low, bamboo in the grove, a lone tree in the open now and then.</summary>
        static void PlantTrees(TerrainData data, Vector2 corner, List<IGroundPlan> plans)
        {
            data.treePrototypes = TreePrototypes();
            var trees = new List<TreeInstance>();
            var random = new System.Random(Mathf.RoundToInt(corner.x * 7 + corner.y * 13));
            float R() => (float)random.NextDouble();
            // A tree every 18 m or so in the woods (at 12 m the mountain held a million, more than the graphics can draw).
            const float spacing = 18;

            for (var z = corner.y; z < corner.y + TileSize; z += spacing)
            {
                for (var x = corner.x; x < corner.x + TileSize; x += spacing)
                {
                    var p = new Vector2(x + (R() - 0.5f) * spacing * 0.95f, z + (R() - 0.5f) * spacing * 0.95f);
                    var u = (p.x - corner.x) / TileSize;
                    var v = (p.y - corner.y) / TileSize;
                    var height = data.GetInterpolatedHeight(u, v);
                    var woods = Woods(p, height);
                    var grove = Vector2.Distance(p, QingMao.Grove) < QingMao.GroveRadius + 120 ? 1 : 0;

                    if (R() > Mathf.Max(woods * 0.85f, 0.025f) && grove == 0)
                    {
                        continue;
                    }

                    if (data.GetSteepness(u, v) > 40 || Clear(p, plans, 1))
                    {
                        continue;
                    }

                    int kind;
                    var high = SmoothStep(700, 1500, height);

                    if (grove > 0 && R() < 0.85f) kind = BambooClump;
                    else if (R() < 0.04f) kind = DeadTree;
                    else if (R() < 0.12f) kind = Shrub;
                    else if (R() < high * 0.85f) kind = R() < 0.65f ? Fir : RedPine;
                    else kind = R() < 0.55f ? Camphor : R() < 0.5f ? Cypress : RedPine;

                    AddTree(trees, random, corner, TileSize, p, kind, kind == Shrub ? 0.8f + 0.6f * R() : 0.8f + 0.55f * R());

                    // The spear bamboo grows thick.
                    if (kind == BambooClump)
                    {
                        for (var k = 0; k < 3; k++)
                        {
                            AddTree(trees, random, corner, TileSize, p + new Vector2(R() - 0.5f, R() - 0.5f) * 8, BambooClump, 0.8f + 0.4f * R());
                        }
                    }
                }
            }

            data.SetTreeInstances(trees.ToArray(), true);
        }

        /// <summary>A village tile's trees: wild ones where the plan leaves the land wild.</summary>
        static void PlantFineTrees(TerrainData data, Vector2 corner, IGroundPlan plan)
        {
            data.treePrototypes = TreePrototypes();
            var trees = new List<TreeInstance>();
            var size = data.size.x;
            var random = new System.Random(Mathf.RoundToInt(corner.x * 3 + corner.y * 5));
            float R() => (float)random.NextDouble();
            const float spacing = 9;

            for (var z = corner.y; z < corner.y + size; z += spacing)
            {
                for (var x = corner.x; x < corner.x + size; x += spacing)
                {
                    var p = new Vector2(x + (R() - 0.5f) * spacing, z + (R() - 0.5f) * spacing);
                    var u = (p.x - corner.x) / size;
                    var v = (p.y - corner.y) / size;
                    var height = data.GetInterpolatedHeight(u, v);
                    var grove = Vector2.Distance(p, QingMao.Grove) < QingMao.GroveRadius;

                    if (!plan.Wild(p) || data.GetSteepness(u, v) > 38 || TrailNear(p, 3) || giantSpots.Any(g => (g.at - p).sqrMagnitude < 81) || Vector2.Distance(p, QingMao.Pool) < QingMao.PoolRadius + 10)
                    {
                        continue;
                    }

                    if (MountainShape.NearStream(p, 20, out var d, out _, out var half, out _) && d < half + 4)
                    {
                        continue;
                    }

                    if (!grove && R() > Mathf.Max(Woods(p, height), 0.12f) * 0.8f)
                    {
                        continue;
                    }

                    var kind = grove ? BambooClump : R() < 0.15f ? Shrub : R() < 0.5f ? Camphor : R() < 0.5f ? Cypress : Fir;
                    AddTree(trees, random, corner, size, p, kind, 0.8f + 0.5f * R());
                }
            }

            data.SetTreeInstances(trees.ToArray(), true);
        }

        // ── Grass ─────────────────────────────────────────────────────

        static Texture2D GrassTexture(string name, int seed, float height, Color tint, bool flowers)
        {
            return Texture(name, 128, (u, v) =>
            {
                var colour = new Color(0, 0, 0, 0);
                var random = new System.Random(seed);

                for (var b = 0; b < 14; b++)
                {
                    var foot = 0.1f + 0.8f * (float)random.NextDouble();
                    var lean = ((float)random.NextDouble() - 0.5f) * 0.5f;
                    var top = height * (0.55f + 0.45f * (float)random.NextDouble());
                    var width = 0.035f * (1 - v / Mathf.Max(0.01f, top));

                    if (v < top && Mathf.Abs(u - (foot + lean * v * v)) < width)
                    {
                        var shade = 0.55f + 0.45f * v / top;
                        colour = new Color(tint.r * shade, tint.g * shade, tint.b * shade, 1);
                    }

                    if (flowers && b % 3 == 0 && Vector2.Distance(new Vector2(u, v), new Vector2(foot + lean * top * top, top)) < 0.045f)
                    {
                        var pick = b % 2 == 0 ? new Color(0.86f, 0.84f, 0.74f) : b % 4 == 3 ? new Color(0.62f, 0.5f, 0.78f) : new Color(0.86f, 0.72f, 0.32f);
                        colour = new Color(pick.r, pick.g, pick.b, 1);
                    }
                }

                return colour;
            }, false, false);
        }

        static DetailPrototype[] DetailPrototypes()
        {
            if (detailPrototypes != null)
            {
                return detailPrototypes;
            }

            DetailPrototype Textured(Texture2D texture, float minW, float maxW, float minH, float maxH, int healthy, int dry) => new DetailPrototype
            {
                prototypeTexture = texture,
                usePrototypeMesh = false,
                renderMode = DetailRenderMode.Grass,
                minWidth = minW,
                maxWidth = maxW,
                minHeight = minH,
                maxHeight = maxH,
                healthyColor = Hex(healthy),
                dryColor = Hex(dry),
                noiseSpread = 0.4f,
            };

            // Ferns: a mesh of fronds arching out from the middle.
            var kit = new MeshKit();
            var frond = Mat("Fern", 0x2a4022, 0.05f, true);

            for (var i = 0; i < 7; i++)
            {
                var angle = i * 0.9f;
                var out_ = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var side = new Vector3(-out_.z, 0, out_.x) * 0.09f;
                var mid = out_ * 0.35f + Vector3.up * 0.42f;
                var tip = out_ * 0.7f + Vector3.up * 0.18f;
                kit.Triangle(frond, -side, side, mid, Vector3.up);
                kit.Triangle(frond, mid - side * 1.6f, mid + side * 1.6f, tip, Vector3.up);
            }

            var (made, used) = kit.Build("Fern");
            var mesh = Keep(made, $"{Generated}/Meshes/Fern.asset");
            var fern = new GameObject("Fern");
            fern.AddComponent<MeshFilter>().sharedMesh = mesh;
            fern.AddComponent<MeshRenderer>().sharedMaterials = used;
            var fernPrefab = PrefabUtility.SaveAsPrefabAsset(fern, $"{Generated}/Prefabs/Fern.prefab");
            Object.DestroyImmediate(fern);

            detailPrototypes = new[]
            {
                Textured(GrassTexture("Grass Tuft", 3, 0.75f, new Color(0.55f, 0.72f, 0.42f), false), 0.6f, 1.1f, 0.35f, 0.7f, 0x6f8a52, 0x8a8452),
                Textured(GrassTexture("Tall Grass", 5, 0.95f, new Color(0.6f, 0.7f, 0.42f), false), 0.7f, 1.3f, 0.8f, 1.5f, 0x6a804a, 0x9a8a58),
                Textured(GrassTexture("Wild Flowers", 9, 0.8f, new Color(0.5f, 0.66f, 0.4f), true), 0.5f, 0.9f, 0.4f, 0.75f, 0xa0a0a0, 0xa09880),
                new DetailPrototype
                {
                    prototype = fernPrefab,
                    usePrototypeMesh = true,
                    useInstancing = true,
                    renderMode = DetailRenderMode.VertexLit,
                    minWidth = 0.8f,
                    maxWidth = 1.5f,
                    minHeight = 0.7f,
                    maxHeight = 1.4f,
                    healthyColor = Hex(0xd0e0c0),
                    dryColor = Hex(0xb8b890),
                    noiseSpread = 0.3f,
                },
            };

            return detailPrototypes;
        }

        /// <summary>Grass on a terrain: thick in the meadows, thin in the woods (ferns there), none on rock, water or paved ground.</summary>
        static void SowGrass(TerrainData data, Vector2 corner, int resolution, IGroundPlan plan, List<IGroundPlan> plans)
        {
            data.detailPrototypes = DetailPrototypes();
            data.SetDetailResolution(resolution, 32);
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            var size = data.size.x;
            var cell = size / resolution;
            // Instances per cell for full cover: about one tuft per 1.6 m².
            var full = Mathf.Clamp(cell * cell / 1.6f, 1, 250);
            var grass = new int[resolution, resolution];
            var tall = new int[resolution, resolution];
            var flowers = new int[resolution, resolution];
            var ferns = new int[resolution, resolution];

            for (var j = 0; j < resolution; j++)
            {
                for (var i = 0; i < resolution; i++)
                {
                    var p = new Vector2(corner.x + (i + 0.5f) * cell, corner.y + (j + 0.5f) * cell);
                    var u = (i + 0.5f) / resolution;
                    var v = (j + 0.5f) / resolution;
                    var steep = data.GetSteepness(u, v);
                    var height = data.GetInterpolatedHeight(u, v);

                    if (steep > 34 || height > 1980)
                    {
                        continue;
                    }

                    if (plans != null && plans.Exists(f => f.Area.Contains(p)))
                    {
                        continue;
                    }

                    if (MountainShape.NearStream(p, 20, out var d, out _, out var half, out _) && d < half + 1.5f)
                    {
                        continue;
                    }

                    if (Vector2.Distance(p, QingMao.Pool) < QingMao.PoolRadius + 4)
                    {
                        continue;
                    }

                    var allowed = plan != null ? plan.Grass(p.x, p.y) : 1;

                    if (TrailNear(p, 1.4f))
                    {
                        allowed *= 0.1f;
                    }

                    if (allowed <= 0.01f)
                    {
                        continue;
                    }

                    var woods = Woods(p, height);
                    var meadow = Mathf.PerlinNoise(p.x * 0.013f + 5, p.y * 0.013f + 9);
                    var sunny = Mathf.Clamp01(-data.GetInterpolatedNormal(u, v).z * 2 + 0.3f);
                    var thin = 1 - SmoothStep(1500, 1950, height);
                    grass[j, i] = Mathf.RoundToInt(full * allowed * thin * Mathf.Lerp(0.85f, 0.18f, woods) * (0.5f + 0.5f * meadow));
                    tall[j, i] = Mathf.RoundToInt(full * 0.35f * allowed * thin * (1 - woods) * SmoothStep(0.5f, 0.8f, meadow));
                    flowers[j, i] = Mathf.RoundToInt(full * 0.3f * allowed * thin * (1 - woods) * sunny * SmoothStep(0.55f, 0.85f, Mathf.PerlinNoise(p.x * 0.02f + 31, p.y * 0.02f + 7)));
                    ferns[j, i] = Mathf.RoundToInt(full * 0.18f * allowed * woods * (1 - SmoothStep(1300, 1700, height)));
                }
            }

            data.SetDetailLayer(0, 0, 0, grass);
            data.SetDetailLayer(0, 0, 1, tall);
            data.SetDetailLayer(0, 0, 2, flowers);
            data.SetDetailLayer(0, 0, 3, ferns);
        }
    }
}
