using System;
using System.Collections.Generic;
using System.Linq;
using GuWorld.World;
using UnityEditor;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// The ground a settlement builds on: its own fine terrain (a village is laid out
    /// to the half-metre — terraces, streets, stairs), its heights and its paint.
    /// </summary>
    public interface IGroundPlan
    {
        /// <summary>The fine terrain's square (aligned to the mountain's cells) and its heightmap's resolution.</summary>
        Rect Area { get; }
        int Resolution { get; }
        /// <summary>The height here, given the natural ground's.</summary>
        float Height(float x, float z, float natural);
        /// <summary>Adds to the ground layers' weights here (QingMaoBuilder.Layers' order).</summary>
        void Paint(float x, float z, float height, float steep, float[] weights);
        /// <summary>How thick the grass may grow here, 0…1 (none on streets and floors).</summary>
        float Grass(float x, float z);
        /// <summary>Whether a wild tree may stand here (not in the village, its streets or fields).</summary>
        bool Wild(Vector2 p);
    }

    /// <summary>
    /// The mountain's terrains: sixteen tiles of 5 km for the whole of it (a height
    /// every 4.9 m), and each walled village's own fine tile (a height every 0.61 m),
    /// cut out of the tiles under it (terrain holes) and fitted to them at its edge:
    /// its border follows the mountain's own heights, and a skirt hides any crack
    /// the terrains' level of detail opens between them far away.
    /// </summary>
    public static partial class QingMaoBuilder
    {
        const int TileCount = 4;
        const float TileSize = QingMao.Size / TileCount;
        const int BaseResolution = 1025;
        /// <summary>The mountain's cell: the spacing of its heights, m.</summary>
        const float Cell = TileSize / (BaseResolution - 1);
        const int BaseSplat = 512;
        const int BaseDetail = 512;

        /// <summary>The ground layers, in their order: grass, forest floor, rock, gravel, dirt, mud, flagstone, moss.</summary>
        const int Grass = 0, ForestFloor = 1, Rock = 2, Gravel = 3, Dirt = 4, MudLayer = 5, Flagstone = 6, Moss = 7, LayerCount = 8;
        public const int LayerRockIndex = Rock, LayerGravelIndex = Gravel, LayerDirtIndex = Dirt, LayerMudIndex = MudLayer, LayerFlagstoneIndex = Flagstone, LayerMossIndex = Moss;

        static TerrainLayer[] layers;
        static Material terrainMaterial;

        /// <summary>A fine tile's square for a settlement: centred on it, snapped to the mountain's cells.</summary>
        public static Rect FineArea(Vector2 centre, float size)
        {
            var cells = Mathf.RoundToInt(size / Cell);
            var x = -QingMao.Half + Mathf.Round((centre.x - size / 2 + QingMao.Half) / Cell) * Cell;
            var z = -QingMao.Half + Mathf.Round((centre.y - size / 2 + QingMao.Half) / Cell) * Cell;

            return new Rect(x, z, cells * Cell, cells * Cell);
        }

        static TerrainLayer[] Layers()
        {
            if (layers != null)
            {
                return layers;
            }

            layers = new TerrainLayer[LayerCount];
            layers[Grass] = Layer("Grass", Texture("Grass", 512, (u, v) =>
            {
                var n = TileFbm(u, v, 8, 1);
                var blades = TileNoise(u * 128, v * 128, 128, 2);
                var dry = TileFbm(u, v, 3, 3) > 0.62f ? 0.35f : 0;
                return Color.Lerp(Color.Lerp(Shade(0x34492a, n), Shade(0x45592f, blades), 0.4f), Shade(0x5a5634, n), dry);
            }), 5);
            layers[ForestFloor] = Layer("Forest", Texture("Forest", 512, (u, v) =>
            {
                var n = TileFbm(u, v, 8, 3);
                var litter = TileNoise(u * 96, v * 96, 96, 4);
                var moss = TileFbm(u, v, 5, 5);
                var c = Color.Lerp(Shade(0x26271a, n), Shade(0x4a3a26, litter), litter > 0.6f ? 0.55f : 0.12f);
                return Color.Lerp(c, Shade(0x2c3a20, n), moss > 0.58f ? 0.5f : 0);
            }), 6);
            layers[Rock] = Layer("Rock", Texture("Rock", 512, (u, v) =>
            {
                var n = TileFbm(u, v, 6, 5);
                var streak = TileNoise(u * 4, v * 32, 32, 6);
                var crack = Mathf.Abs(TileFbm(u, v, 5, 7) - 0.5f) < 0.02f ? 0.55f : 1;
                return Shade(0x4e4d48, n * 0.7f + streak * 0.3f) * crack;
            }), 9);
            layers[Gravel] = Layer("Gravel", Texture("Gravel", 512, (u, v) =>
            {
                var n = TileFbm(u, v, 10, 7);
                var pebble = TileNoise(u * 120, v * 120, 120, 8);
                var big = TileNoise(u * 30, v * 30, 30, 9);
                return Color.Lerp(Shade(0x575249, n), Shade(0x7c776a, pebble), pebble > 0.66f ? 0.7f : 0) * (big > 0.75f ? 0.85f : 1);
            }), 4);
            layers[Dirt] = Layer("Dirt", Texture("Dirt", 512, (u, v) =>
            {
                var n = TileFbm(u, v, 10, 17);
                var pebble = TileNoise(u * 160, v * 160, 160, 18);
                var rut = Mathf.Abs(Mathf.Repeat(u * 3 + TileFbm(u, v, 3, 19) * 0.4f, 1) - 0.5f) < 0.04f ? 0.88f : 1;
                return Color.Lerp(Shade(0x5a4632, n), Shade(0x80735e, pebble), pebble > 0.74f ? 0.6f : 0) * rut;
            }), 4);
            layers[MudLayer] = Layer("Mud", Texture("Mud", 512, (u, v) =>
            {
                var n = TileFbm(u, v, 8, 27);
                var wet = TileFbm(u, v, 4, 28);
                var straw = TileNoise(u * 90, v * 9, 90, 29) > 0.9f ? 0.25f : 0;
                var c = Color.Lerp(Shade(0x3e3226, n), Shade(0x241d17, wet), wet > 0.6f ? 0.6f : 0.1f);
                return Color.Lerp(c, Shade(0x6a5a38, n), straw);
            }), 5);
            layers[Flagstone] = Layer("Flagstone", Texture("Flagstone", 512, (u, v) =>
            {
                // Irregular slabs: a jittered grid of cells, dark joints, moss in some joints.
                var cu = u * 7 + TileNoise(u * 7, v * 7, 7, 31) * 0.35f;
                var cv = v * 7 + TileNoise(u * 7 + 3, v * 7, 7, 32) * 0.35f;
                var row = Mathf.Floor(cv);
                cu += (row % 2) * 0.5f;
                var jx = Mathf.Abs(Mathf.Repeat(cu, 1) - 0.5f);
                var jy = Mathf.Abs(Mathf.Repeat(cv, 1) - 0.5f);
                var joint = Mathf.Max(jx, jy) > 0.455f;
                var slab = TileNoise(Mathf.Floor(cu) * 2.1f, row * 3.3f, 64, 33);
                var grain = TileFbm(u, v, 12, 34);
                var moss = TileFbm(u, v, 5, 35) > 0.55f;
                if (joint) return moss ? Shade(0x2e3a22, grain) : Shade(0x2a2926, grain);
                return Shade(0x5e5e5c, slab * 0.6f + grain * 0.4f);
            }), 4);
            layers[Moss] = Layer("Moss", Texture("Moss", 512, (u, v) =>
            {
                var n = TileFbm(u, v, 8, 41);
                var clump = TileNoise(u * 60, v * 60, 60, 42);
                return Color.Lerp(Shade(0x2c3c22, n), Shade(0x405a2a, clump), clump > 0.55f ? 0.6f : 0.15f);
            }), 4);

            return layers;
        }

        static TerrainLayer Layer(string name, Texture2D texture, float tile)
        {
            var path = $"{Root}/Textures/{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            var create = layer == null;

            if (create)
            {
                layer = new TerrainLayer();
            }

            layer.diffuseTexture = texture;
            layer.tileSize = Vector2.one * tile;
            layer.smoothness = 0;
            layer.metallic = 0;

            if (create)
            {
                AssetDatabase.CreateAsset(layer, path);
            }
            else
            {
                EditorUtility.SetDirty(layer);
            }

            return layer;
        }

        static Material TerrainMaterial()
        {
            if (terrainMaterial != null)
            {
                return terrainMaterial;
            }

            var path = $"{Root}/Materials/Terrain.mat";
            terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (terrainMaterial == null)
            {
                terrainMaterial = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
                AssetDatabase.CreateAsset(terrainMaterial, path);
            }

            // Terrain holes (under the villages' own ground) need the keyword.
            terrainMaterial.EnableKeyword("_ALPHATEST_ON");
            EditorUtility.SetDirty(terrainMaterial);

            return terrainMaterial;
        }

        /// <summary>Whether a point lies in the woods (patchy, thinning towards the top and the plain).</summary>
        static float Woods(Vector2 p, float height)
        {
            var n = Mathf.PerlinNoise(p.x * 0.0011f + 40, p.y * 0.0011f + 70) * 0.65f + Mathf.PerlinNoise(p.x * 0.005f + 3, p.y * 0.005f + 9) * 0.35f;
            var high = SmoothStep(1650, 1950, height);
            var low = 1 - SmoothStep(60, 160, height);

            return Mathf.Clamp01((n - 0.38f) * 4) * (1 - high) * (1 - low * 0.7f);
        }

        // ── The mountain's tiles ──────────────────────────────────────

        /// <summary>A terrain's data at its path: the one there (kept, so its GUID stays), else a new one.</summary>
        static TerrainData TerrainAsset(string name)
        {
            var path = $"{Generated}/Terrain/{name}.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);

            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, path);
            }

            return data;
        }

        /// <summary>One of the mountain's tiles: its heights, its holes under the villages, its paint, grass and trees — saved, no object made.</summary>
        static TerrainData BuildBaseTile(string name, Vector2 corner, List<IGroundPlan> plans, Func<float, float, float> natural)
        {
            var data = TerrainAsset(name);
            data.heightmapResolution = BaseResolution;
            data.size = new Vector3(TileSize, QingMao.MaxHeight, TileSize);

            var heights = new float[BaseResolution, BaseResolution];

            for (var j = 0; j < BaseResolution; j++)
            {
                for (var i = 0; i < BaseResolution; i++)
                {
                    heights[j, i] = Mathf.Clamp01(natural(corner.x + i * Cell, corner.y + j * Cell) / QingMao.MaxHeight);
                }
            }

            data.SetHeights(0, 0, heights);

            // Holes under the villages' own ground.
            var holes = new bool[BaseResolution - 1, BaseResolution - 1];

            for (var j = 0; j < BaseResolution - 1; j++)
            {
                for (var i = 0; i < BaseResolution - 1; i++)
                {
                    var middle = new Vector2(corner.x + (i + 0.5f) * Cell, corner.y + (j + 0.5f) * Cell);
                    var solid = true;

                    foreach (var plan in plans)
                    {
                        if (plan.Area.Contains(middle))
                        {
                            solid = false;
                        }
                    }

                    holes[j, i] = solid;
                }
            }

            // Always written: a tile made again over an old one must lose holes it no longer has.
            data.SetHoles(0, 0, holes);
            heights = null;
            holes = null;

            Paint(data, corner, null);
            SowGrass(data, corner, BaseDetail, null, plans);
            PlantTrees(data, corner, plans);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            return data;
        }

        /// <summary>Writes what is made so far and lets go of what is no longer needed (the build is long and large).</summary>
        static void FreeMemory()
        {
            AssetDatabase.SaveAssets();
            EditorUtility.UnloadUnusedAssetsImmediate();
            System.GC.Collect();
        }

        static Terrain MakeTerrain(string name, TerrainData data, Vector2 corner, int group)
        {
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = name;
            go.transform.position = new Vector3(corner.x, 0, corner.y);
            go.AddComponent<GuWorld.Render.HitboxTag>().Kind = GuWorld.Render.HitboxKind.Other;
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = TerrainMaterial();
            terrain.drawInstanced = true;
            terrain.groupingID = group;
            terrain.allowAutoConnect = group == 1;
            terrain.heightmapPixelError = 3;
            terrain.basemapDistance = 1200;
            terrain.treeDistance = 3500;
            terrain.treeBillboardDistance = 320;
            terrain.treeMaximumFullLODCount = 20000;
            terrain.detailObjectDistance = 110;
            terrain.detailObjectDensity = 0.9f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;

            return terrain;
        }

        /// <summary>Paints a terrain's ground layers (the plan's where it has one).</summary>
        static void Paint(TerrainData data, Vector2 corner, IGroundPlan plan)
        {
            data.terrainLayers = Layers();
            // A village's ground painted every 1.2 m at most: finer would cost hundreds of MB while building.
            var resolution = plan == null ? BaseSplat : Mathf.Min(1024, plan.Resolution - 1);
            data.alphamapResolution = resolution;
            var splat = new float[resolution, resolution, LayerCount];
            var size = data.size.x;
            var cell = size / resolution;
            var w = new float[LayerCount];

            for (var j = 0; j < resolution; j++)
            {
                for (var i = 0; i < resolution; i++)
                {
                    var x = corner.x + (i + 0.5f) * cell;
                    var z = corner.y + (j + 0.5f) * cell;
                    var p = new Vector2(x, z);
                    var u = (i + 0.5f) / resolution;
                    var v = (j + 0.5f) / resolution;
                    var steep = data.GetSteepness(u, v);
                    var height = data.GetInterpolatedHeight(u, v);
                    Array.Clear(w, 0, LayerCount);

                    w[Grass] = 1;
                    w[ForestFloor] = Woods(p, height) * 1.8f;
                    w[Rock] = SmoothStep(30, 42, steep) * 3 + SmoothStep(1750, 2050, height) * 1.4f;
                    w[Gravel] = SmoothStep(1500, 1900, height) * 0.5f * Mathf.PerlinNoise(x * 0.01f, z * 0.01f);

                    if (MountainShape.NearStream(p, 40, out var toStream, out _, out var half, out _))
                    {
                        w[Gravel] += (1 - SmoothStep(half, half + 5, toStream)) * 4;
                        w[Moss] += (1 - SmoothStep(half + 3, half + 30, toStream)) * 1.4f;
                    }

                    if (Vector2.Distance(p, QingMao.Pool) < QingMao.PoolRadius + 14)
                    {
                        w[Gravel] += 3;
                    }

                    // Shady northern slopes grow moss.
                    var normal = data.GetInterpolatedNormal(u, v);
                    w[Moss] += Mathf.Clamp01(normal.z * 2) * 0.5f * (1 - SmoothStep(1500, 1800, height));

                    // Trodden earth round the hamlets.
                    foreach (var place in QingMao.Settlements)
                    {
                        if (place.Clan == Clan.Mortal)
                        {
                            var d = Vector2.Distance(p, place.Centre);
                            w[Dirt] += (1 - SmoothStep(place.Radius * 0.6f, place.Radius * 1.1f, d)) * 1.6f * (0.6f + 0.6f * Mathf.PerlinNoise(x * 0.08f, z * 0.08f));
                        }
                    }

                    plan?.Paint(x, z, height, steep, w);
                    var sum = 0f;

                    for (var k = 0; k < LayerCount; k++)
                    {
                        sum += Mathf.Max(0, w[k]);
                    }

                    for (var k = 0; k < LayerCount; k++)
                    {
                        splat[j, i, k] = Mathf.Max(0, w[k]) / sum;
                    }
                }
            }

            data.SetAlphamaps(0, 0, splat);
        }

        // ── A settlement's own ground ─────────────────────────────────

        /// <summary>
        /// A village's fine terrain: the plan's heights, fitted at the edge to the mountain's
        /// own (its border follows the mountain's cells exactly, so the two meet).
        /// </summary>
        static TerrainData BuildFineTile(string name, IGroundPlan plan, Func<float, float, float> natural)
        {
            var area = plan.Area;
            var resolution = plan.Resolution;
            var data = TerrainAsset(name);
            data.heightmapResolution = resolution;
            data.size = new Vector3(area.width, QingMao.MaxHeight, area.height);
            var step = area.width / (resolution - 1);
            var heights = new float[resolution, resolution];
            const float band = 30;

            for (var j = 0; j < resolution; j++)
            {
                if (j % 128 == 0)
                {
                    EditorUtility.DisplayProgressBar("Qing Mao", $"{name}: heights…", (float)j / resolution);
                }

                for (var i = 0; i < resolution; i++)
                {
                    var x = area.x + i * step;
                    var z = area.y + j * step;
                    var edge = Mathf.Min(Mathf.Min(x - area.x, area.xMax - x), Mathf.Min(z - area.y, area.yMax - z));
                    var mountain = edge < band ? CellInterpolated(x, z, natural) : 0;
                    var own = plan.Height(x, z, natural(x, z));
                    heights[j, i] = Mathf.Clamp01(Mathf.Lerp(mountain, own, SmoothStep(0, band, edge)) / QingMao.MaxHeight);
                }
            }

            data.SetHeights(0, 0, heights);
            heights = null;
            EditorUtility.DisplayProgressBar("Qing Mao", $"{name}: painting…", 0.5f);
            Paint(data, area.position, plan);
            EditorUtility.DisplayProgressBar("Qing Mao", $"{name}: grass…", 0.7f);
            SowGrass(data, area.position, Mathf.Min(1024, resolution - 1), plan, null);
            PlantFineTrees(data, area.position, plan);
            EditorUtility.SetDirty(data);
            Skirt(name, area, natural);
            AssetDatabase.SaveAssets();

            return data;
        }

        /// <summary>The mountain's ground as its tiles draw it: its heights at the corners of a cell, blended across it.</summary>
        static float CellInterpolated(float x, float z, Func<float, float, float> natural)
        {
            var gx = (x + QingMao.Half) / Cell;
            var gz = (z + QingMao.Half) / Cell;
            var x0 = Mathf.Floor(gx);
            var z0 = Mathf.Floor(gz);
            var fx = gx - x0;
            var fz = gz - z0;
            float H(float i, float k) => natural(-QingMao.Half + i * Cell, -QingMao.Half + k * Cell);

            if (fx < 1e-4f && fz < 1e-4f)
            {
                return H(x0, z0);
            }

            return Mathf.Lerp(Mathf.Lerp(H(x0, z0), H(x0 + 1, z0), fx), Mathf.Lerp(H(x0, z0 + 1), H(x0 + 1, z0 + 1), fx), fz);
        }

        /// <summary>A strip of earth hanging down from a fine tile's edge, so no light shows between it and the mountain far away (the mesh; LoadGround puts it in the scene).</summary>
        static void Skirt(string name, Rect area, Func<float, float, float> natural)
        {
            var kit = new MeshKit();
            var earth = Mat("Skirt Earth", 0x2a2a20, 0.02f, true);
            var corners = new[] { new Vector2(area.xMin, area.yMin), new Vector2(area.xMax, area.yMin), new Vector2(area.xMax, area.yMax), new Vector2(area.xMin, area.yMax) };

            for (var c = 0; c < 4; c++)
            {
                var a = corners[c];
                var b = corners[(c + 1) % 4];
                var steps = Mathf.RoundToInt(Vector2.Distance(a, b) / Cell);

                for (var s = 0; s < steps; s++)
                {
                    var p0 = Vector2.Lerp(a, b, (float)s / steps);
                    var p1 = Vector2.Lerp(a, b, (float)(s + 1) / steps);
                    var h0 = natural(p0.x, p0.y);
                    var h1 = natural(p1.x, p1.y);
                    var top0 = new Vector3(p0.x, h0 - 0.05f, p0.y);
                    var top1 = new Vector3(p1.x, h1 - 0.05f, p1.y);
                    kit.Quad(earth, top0, top1, top1 + Vector3.down * 6, top0 + Vector3.down * 6, Vector3.Cross(Vector3.up, (Vector3)(p1 - p0)));
                }
            }

            Keep(kit.Build($"{name} Skirt").mesh, $"{Generated}/Meshes/{name} Skirt.asset");
        }
    }
}
