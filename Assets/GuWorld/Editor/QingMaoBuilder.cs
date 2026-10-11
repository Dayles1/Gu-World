using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuWorld.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GuWorld.Editor
{
    /// <summary>
    /// Builds Qing Mao Mountain (the first arc's world) as ordinary assets and a
    /// scene, so it can be opened and reworked by hand in the editor afterwards:
    ///
    /// - the terrain (QingMao.HeightAt), painted — grass, forest floor, rock on
    ///   steep ground, the paths, trodden earth in the villages;
    /// - Gu Yue village at the mountain's waist (its buildings in QingMaoBuildings,
    ///   after the art direction): stone-footed walls with watchtowers and four
    ///   gates, ladders up the walls inside, bamboo houses on stilts, the
    ///   five-storey clan pavilion and its square, the ancestral hall, the
    ///   academy and its hostel, lanterns and banners;
    /// - the Xiong and Bai villages on the fore and back mountains (seen from afar);
    /// - pines, broadleaf trees, the bamboo grove, grass, the river from the
    ///   spirit spring, and far mountains all round.
    ///
    /// Every building and plant here is GU World's own, made of simple shapes.
    /// Deterministic: the same every time it is built. Run it from the menu
    /// (Gu World → Build Qing Mao Mountain) or the command line:
    /// Unity -batchmode -quit -executeMethod GuWorld.Editor.QingMaoBuilder.Build
    /// </summary>
    public static partial class QingMaoBuilder
    {
        const string Root = "Assets/GuWorld/Locations/QingMao";
        public const string ScenePath = Root + "/QingMao.unity";

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        // The village's streets: north–south and east–west through the gates, and a ring.
        static readonly Vector2 C = QingMao.Village;
        const float RingRadius = 58;
        static readonly Vector2 Pavilion = QingMao.Village + new Vector2(0, 20);
        static readonly Vector2 Square = QingMao.Village + new Vector2(0, -2);
        static readonly Vector2 Shrine = QingMao.Village + new Vector2(34, 18);
        static readonly Vector2 Academy = QingMao.Village + new Vector2(-36, 20);
        static readonly Vector2 Hostel = QingMao.Village + new Vector2(-40, -26);

        static Vector2[][] Streets => new[]
        {
            new[] { C + new Vector2(0, 92), C + new Vector2(0, 30) },
            new[] { C + new Vector2(0, -12), C + new Vector2(0, -92) },
            new[] { C + new Vector2(-92, 0), C + new Vector2(92, 0) },
        };

        [MenuItem("Gu World/Build Qing Mao Mountain", priority = 3)]
        public static void Build()
        {
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Textures");
            Directory.CreateDirectory(Root + "/Meshes");
            Directory.CreateDirectory(Root + "/Prefabs");
            materials.Clear();

            var active = EditorSceneManager.GetActiveScene();
            var alone = Application.isBatchMode || (string.IsNullOrEmpty(active.path) && !active.isDirty);

            if (!alone && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EditorUtility.DisplayProgressBar("Qing Mao", "Terrain…", 0.1f);
            var terrain = BuildTerrain();
            EditorUtility.DisplayProgressBar("Qing Mao", "Gu Yue village…", 0.6f);
            var village = new GameObject("Gu Yue Village").transform;
            BuildVillage(village);
            BuildSmallVillage("Xiong Village", QingMao.Xiong, 3, Clan.Xiong);
            BuildSmallVillage("Bai Village", QingMao.Bai, 7, Clan.Bai);
            EditorUtility.DisplayProgressBar("Qing Mao", "Water and horizon…", 0.85f);
            BuildRiver();
            BuildSurroundings();
            BuildAtmosphere();
            BuildGame();
            EditorUtility.ClearProgressBar();

            foreach (var t in Object.FindObjectsByType<Transform>())
            {
                if (t.GetComponent<MeshRenderer>() != null && t.GetComponentInParent<Terrain>() == null)
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                }
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            // First in the build: the game begins on the mountain. The proving ground stays, second.
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => s.path != ScenePath));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"GU World: Qing Mao Mountain is built at {ScenePath} ({terrain.terrainData.treeInstanceCount} trees).");
        }

        // ── Materials and textures ────────────────────────────────────

        static Material Mat(string name, int rgb, float smoothness = 0.12f, bool twoSided = false, Texture2D texture = null, float tiling = 1)
        {
            if (materials.TryGetValue(name, out var cached))
            {
                return cached;
            }

            var path = $"{Root}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var create = material == null;

            if (create)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }

            material.color = Hex(rgb);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Cull", twoSided ? 0 : 2);
            material.doubleSidedGI = twoSided;
            material.enableInstancing = true;

            if (texture != null)
            {
                material.mainTexture = texture;
                material.mainTextureScale = Vector2.one * tiling;
            }

            if (create)
            {
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            materials[name] = material;

            return material;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);

        /// <summary>Value noise that wraps every `period` cells (so a texture made of it tiles).</summary>
        static float TileNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float H(int i, int j)
            {
                i = ((i % period) + period) % period;
                j = ((j % period) + period) % period;
                var h = (uint)(i * 374761393 + j * 668265263 + seed * 2246822519);
                h = (h ^ (h >> 13)) * 1274126177;

                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }

            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);

            return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), fx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx), fy);
        }

        static float TileFbm(float u, float v, int baseCells, int seed)
        {
            float sum = 0, amplitude = 1, total = 0;
            var cells = baseCells;

            for (var o = 0; o < 4; o++)
            {
                sum += TileNoise(u * cells, v * cells, cells, seed + o) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                cells *= 2;
            }

            return sum / total;
        }

        /// <summary>Makes (once) and imports a tiling texture: `paint` gives each texel's colour from u, v in 0…1.</summary>
        static Texture2D Texture(string name, int size, System.Func<float, float, Color> paint, bool matte = true, bool tiles = true)
        {
            var path = $"{Root}/Textures/{name}.png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var colour = paint((float)x / size, (float)y / size);

                    // For the ground the alpha is how glossy URP's terrain draws it (with no mask map): matte.
                    if (matte)
                    {
                        colour.a = 0.06f;
                    }

                    pixels[y * size + x] = colour;
                }
            }

            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.alphaIsTransparency = !matte;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color Shade(int rgb, float amount) => Hex(rgb) * (0.75f + 0.5f * amount);

        // ── The terrain ───────────────────────────────────────────────

        const int HeightResolution = 1025;
        const int SplatResolution = 1024;
        const int DetailResolution = 1024;

        static Terrain BuildTerrain()
        {
            // Made an asset first: the textures Unity makes for its painting are saved inside it.
            var dataPath = $"{Root}/QingMao Terrain.asset";
            AssetDatabase.DeleteAsset(dataPath);
            var data = new TerrainData();
            AssetDatabase.CreateAsset(data, dataPath);
            data.heightmapResolution = HeightResolution;
            data.size = new Vector3(QingMao.Size, QingMao.MaxHeight, QingMao.Size);

            // Heights.
            var heights = new float[HeightResolution, HeightResolution];
            var step = QingMao.Size / (HeightResolution - 1);

            for (var j = 0; j < HeightResolution; j++)
            {
                if (j % 64 == 0)
                {
                    EditorUtility.DisplayProgressBar("Qing Mao", "Terrain heights…", 0.1f + 0.25f * j / HeightResolution);
                }

                for (var i = 0; i < HeightResolution; i++)
                {
                    heights[j, i] = Mathf.Clamp01(QingMao.HeightAt(-QingMao.Half + i * step, -QingMao.Half + j * step) / QingMao.MaxHeight);
                }
            }

            data.SetHeights(0, 0, heights);

            // The ground's layers: grass, forest floor, rock, path, trodden earth.
            var grass = Layer("Grass", Texture("Grass", 512, (u, v) =>
            {
                var n = TileFbm(u, v, 8, 1);
                var blades = TileNoise(u * 128, v * 128, 128, 2);
                return Color.Lerp(Shade(0x5d7a3a, n), Shade(0x6f8a45, blades), 0.35f);
            }), 5);
            var forest = Layer("Forest", Texture("Forest", 512, (u, v) =>
            {
                var n = TileFbm(u, v, 8, 3);
                var litter = TileNoise(u * 96, v * 96, 96, 4);
                return Color.Lerp(Shade(0x4a5532, n), Shade(0x6a5236, litter), litter > 0.62f ? 0.6f : 0.15f);
            }), 6);
            var rock = Layer("Rock", Texture("Rock", 512, (u, v) =>
            {
                var n = TileFbm(u, v, 6, 5);
                var streak = TileNoise(u * 4, v * 32, 32, 6);
                return Shade(0x7a776e, n * 0.7f + streak * 0.3f);
            }), 9);
            var path = Layer("Path", Texture("Path", 512, (u, v) =>
            {
                var n = TileFbm(u, v, 10, 7);
                var pebble = TileNoise(u * 160, v * 160, 160, 8);
                return Color.Lerp(Shade(0x8a7351, n), Shade(0xa59a86, pebble), pebble > 0.72f ? 0.7f : 0);
            }), 4);
            var earth = Layer("Earth", Texture("Earth", 512, (u, v) =>
            {
                // Packed earth with flagstones here and there.
                var n = TileFbm(u, v, 8, 9);
                var gx = Mathf.Abs(Mathf.Repeat(u * 6 + TileNoise(u * 6, v * 6, 6, 10) * 0.3f, 1) - 0.5f);
                var gy = Mathf.Abs(Mathf.Repeat(v * 6, 1) - 0.5f);
                var gap = Mathf.Max(gx, gy) > 0.46f;
                return gap ? Shade(0x5e574b, n) : Shade(0x958d7c, n);
            }), 6);

            data.terrainLayers = new[] { grass, forest, rock, path, earth };
            data.alphamapResolution = SplatResolution;
            var splat = new float[SplatResolution, SplatResolution, 5];
            var cell = QingMao.Size / SplatResolution;

            for (var j = 0; j < SplatResolution; j++)
            {
                if (j % 64 == 0)
                {
                    EditorUtility.DisplayProgressBar("Qing Mao", "Painting the ground…", 0.35f + 0.15f * j / SplatResolution);
                }

                for (var i = 0; i < SplatResolution; i++)
                {
                    var x = -QingMao.Half + (i + 0.5f) * cell;
                    var z = -QingMao.Half + (j + 0.5f) * cell;
                    var p = new Vector2(x, z);
                    var steep = data.GetSteepness((i + 0.5f) / SplatResolution, (j + 0.5f) / SplatResolution);
                    var height = data.GetInterpolatedHeight((i + 0.5f) / SplatResolution, (j + 0.5f) / SplatResolution);

                    var w = new float[5];
                    w[0] = 1;
                    w[1] = Forested(p) ? 1.4f : 0;
                    w[2] = SmoothStep(26, 38, steep) * 3 + SmoothStep(380, 460, height) * 1.2f;

                    var toPath = Mathf.Min(QingMao.ToPath(p), ToStreet(p));
                    w[3] = (1 - SmoothStep(QingMao.PathHalfWidth - 0.5f, QingMao.PathHalfWidth + 1.2f, toPath)) * 4;

                    var inVillage = 1 - SmoothStep(QingMao.VillageRadius - 4, QingMao.VillageRadius + 2, Vector2.Distance(p, C));
                    inVillage = Mathf.Max(inVillage, 1 - SmoothStep(QingMao.SmallVillageRadius - 4, QingMao.SmallVillageRadius + 2, Vector2.Distance(p, QingMao.Xiong)));
                    inVillage = Mathf.Max(inVillage, 1 - SmoothStep(QingMao.SmallVillageRadius - 4, QingMao.SmallVillageRadius + 2, Vector2.Distance(p, QingMao.Bai)));
                    w[4] = inVillage * 2.5f;

                    if (inVillage > 0.5f)
                    {
                        w[1] = 0;
                        w[2] *= 0.2f;
                    }

                    // The river's bed and banks: stones.
                    if (DistanceToRiverLine(p) < QingMao.RiverHalfWidth + 3)
                    {
                        w[2] += 2;
                    }

                    var sum = w.Sum();

                    for (var k = 0; k < 5; k++)
                    {
                        splat[j, i, k] = w[k] / sum;
                    }
                }
            }

            data.SetAlphamaps(0, 0, splat);

            PlantTrees(data);
            SowGrass(data);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.position = new Vector3(-QingMao.Half, 0, -QingMao.Half);
            var terrain = go.GetComponent<Terrain>();
            var terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/Terrain.mat");

            if (terrainMaterial == null)
            {
                terrainMaterial = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
                AssetDatabase.CreateAsset(terrainMaterial, $"{Root}/Materials/Terrain.mat");
            }

            terrain.materialTemplate = terrainMaterial;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 3;
            terrain.basemapDistance = 400;
            terrain.treeDistance = 650;
            terrain.treeBillboardDistance = 650;
            terrain.treeMaximumFullLODCount = 20000;
            terrain.detailObjectDistance = 90;
            terrain.detailObjectDensity = 1;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;

            return terrain;
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

        static float SmoothStep(float min, float max, float x)
        {
            if (x <= min) return 0;
            if (x >= max) return 1;
            x = (x - min) / (max - min);

            return x * x * (3 - 2 * x);
        }

        /// <summary>Whether a point lies in the woods (patchy, thinning towards the top).</summary>
        static bool Forested(Vector2 p)
        {
            var n = Mathf.PerlinNoise(p.x * 0.0065f + 40, p.y * 0.0065f + 70);
            var high = Vector2.Distance(p, QingMao.Peak) < 120;

            return n > 0.47f && !high;
        }

        static float ToStreet(Vector2 p)
        {
            if (Vector2.Distance(p, C) > QingMao.VillageRadius + 4)
            {
                return float.PositiveInfinity;
            }

            var best = Mathf.Abs(Vector2.Distance(p, C) - RingRadius);

            foreach (var street in Streets)
            {
                best = Mathf.Min(best, QingMao.ToSegment(p, street[0], street[1]));
            }

            // The square before the pavilion.
            var square = Square - p;

            if (Mathf.Abs(square.x) < 24 && Mathf.Abs(square.y) < 12)
            {
                best = 0;
            }

            return best;
        }

        static float DistanceToRiverLine(Vector2 p)
        {
            var best = float.PositiveInfinity;

            for (var i = 0; i + 1 < QingMao.River.Length; i++)
            {
                best = Mathf.Min(best, QingMao.ToSegment(p, QingMao.River[i], QingMao.River[i + 1]));
            }

            return best;
        }

        /// <summary>Whether something may grow at a point: not on paths, the river, in or near the villages.</summary>
        static bool Open(Vector2 p, float clearance)
        {
            return QingMao.ToPath(p) > QingMao.PathHalfWidth + clearance &&
                   DistanceToRiverLine(p) > QingMao.RiverHalfWidth + 2 + clearance &&
                   Vector2.Distance(p, C) > QingMao.VillageRadius + 12 + clearance &&
                   Vector2.Distance(p, QingMao.Xiong) > QingMao.SmallVillageRadius + 8 &&
                   Vector2.Distance(p, QingMao.Bai) > QingMao.SmallVillageRadius + 8 &&
                   Vector2.Distance(p, QingMao.Spring) > 14;
        }

        // ── Trees and grass ───────────────────────────────────────────

        static void PlantTrees(TerrainData data)
        {
            var pine = TreePrefab("Pine", BuildPine, 0.3f, 6);
            var broadleaf = TreePrefab("Broadleaf", BuildBroadleaf, 0.35f, 4);
            var bamboo = TreePrefab("Bamboo", BuildBamboo, 0.45f, 6);

            data.treePrototypes = new[]
            {
                new TreePrototype { prefab = pine },
                new TreePrototype { prefab = broadleaf },
                new TreePrototype { prefab = bamboo },
            };

            var trees = new List<TreeInstance>();
            var random = new System.Random(20261010);
            float R() => (float)random.NextDouble();

            void Plant(Vector2 p, int kind, float size)
            {
                var u = (p.x + QingMao.Half) / QingMao.Size;
                var v = (p.y + QingMao.Half) / QingMao.Size;

                if (u < 0.005f || u > 0.995f || v < 0.005f || v > 0.995f)
                {
                    return;
                }

                trees.Add(new TreeInstance
                {
                    position = new Vector3(u, 0, v),
                    widthScale = size * (0.9f + 0.2f * R()),
                    heightScale = size,
                    rotation = R() * Mathf.PI * 2,
                    color = new Color32((byte)(225 + 30 * R()), 255, (byte)(225 + 30 * R()), 255),
                    lightmapColor = Color.white,
                    prototypeIndex = kind,
                });
            }

            // The woods: a jittered grid, pines higher up and broadleaf lower down; a few lone trees in the open.
            const float spacing = 9;

            for (var z = -QingMao.Half; z < QingMao.Half; z += spacing)
            {
                for (var x = -QingMao.Half; x < QingMao.Half; x += spacing)
                {
                    var p = new Vector2(x + (R() - 0.5f) * spacing * 0.9f, z + (R() - 0.5f) * spacing * 0.9f);
                    var forested = Forested(p);

                    if ((!forested && R() > 0.035f) || !Open(p, 1.5f) || Vector2.Distance(p, QingMao.Grove) < QingMao.GroveRadius + 6)
                    {
                        continue;
                    }

                    var u = (p.x + QingMao.Half) / QingMao.Size;
                    var v = (p.y + QingMao.Half) / QingMao.Size;

                    if (data.GetSteepness(u, v) > 38)
                    {
                        continue;
                    }

                    var height = data.GetInterpolatedHeight(u, v);
                    var pineChance = SmoothStep(120, 300, height) * 0.8f + 0.15f;
                    Plant(p, R() < pineChance ? 0 : 1, 0.75f + 0.55f * R());
                }
            }

            // The bamboo grove east of the village: thick clumps.
            for (var z = -QingMao.GroveRadius; z < QingMao.GroveRadius; z += 3.4f)
            {
                for (var x = -QingMao.GroveRadius; x < QingMao.GroveRadius; x += 3.4f)
                {
                    var p = QingMao.Grove + new Vector2(x + (R() - 0.5f) * 2.6f, z + (R() - 0.5f) * 2.6f);
                    var edge = Vector2.Distance(p, QingMao.Grove) / QingMao.GroveRadius + (Mathf.PerlinNoise(p.x * 0.05f, p.y * 0.05f) - 0.5f) * 0.3f;

                    if (edge > 1 || !Open(p, 0.5f))
                    {
                        continue;
                    }

                    Plant(p, 2, 0.8f + 0.45f * R());
                }
            }

            // Bamboo along the river too.
            foreach (var (distance, at) in RiverSide(random))
            {
                if (Open(at, 0.5f) && distance > QingMao.RiverHalfWidth + 3)
                {
                    Plant(at, 2, 0.7f + 0.3f * R());
                }
            }

            data.SetTreeInstances(trees.ToArray(), true);
        }

        static IEnumerable<(float, Vector2)> RiverSide(System.Random random)
        {
            for (var i = 0; i + 1 < QingMao.River.Length; i++)
            {
                var a = QingMao.River[i];
                var b = QingMao.River[i + 1];
                var length = Vector2.Distance(a, b);
                var across = Vector2.Perpendicular((b - a).normalized);

                for (var s = 0f; s < length; s += 6)
                {
                    if (random.NextDouble() > 0.45)
                    {
                        continue;
                    }

                    var side = random.NextDouble() > 0.5 ? 1 : -1;
                    var distance = QingMao.RiverHalfWidth + 4 + (float)random.NextDouble() * 6;
                    yield return (distance, Vector2.Lerp(a, b, s / length) + across * side * distance);
                }
            }
        }

        static GameObject TreePrefab(string name, System.Action<MeshKit> build, float trunkRadius, float trunkHeight)
        {
            var kit = new MeshKit();
            build(kit);
            var (mesh, used) = kit.Build(name);
            var meshPath = $"{Root}/Meshes/{name}.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = used;
            var collider = go.AddComponent<CapsuleCollider>();
            collider.radius = trunkRadius;
            collider.height = trunkHeight;
            collider.center = new Vector3(0, trunkHeight / 2, 0);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/{name}.prefab");
            Object.DestroyImmediate(go);

            return prefab;
        }

        static void BuildPine(MeshKit kit)
        {
            var bark = Mat("Bark", 0x4a3a2c, 0.05f);
            var needles = Mat("Needles", 0x2e4a2c, 0.05f);
            kit.Cylinder(bark, Vector3.zero, new Vector3(0, 9, 0), 0.32f, 0.08f, 6);

            for (var i = 0; i < 4; i++)
            {
                var y = 2.6f + i * 1.65f;
                var radius = 2.6f - i * 0.5f;
                kit.Cylinder(needles, new Vector3(0, y, 0), new Vector3(0, y + 2.6f, 0), radius, 0, 8);
            }
        }

        static void BuildBroadleaf(MeshKit kit)
        {
            var bark = Mat("Bark", 0x4a3a2c, 0.05f);
            var leaves = Mat("Leaves", 0x3e6a2c, 0.08f);
            kit.Cylinder(bark, Vector3.zero, new Vector3(0, 4.2f, 0), 0.34f, 0.18f, 6);
            kit.Cylinder(bark, new Vector3(0, 3.2f, 0), new Vector3(1.2f, 5, 0.3f), 0.14f, 0.07f, 5);
            kit.Cylinder(bark, new Vector3(0, 3.4f, 0), new Vector3(-1, 5.2f, -0.6f), 0.14f, 0.07f, 5);
            kit.Blob(leaves, new Vector3(0, 6, 0), new Vector3(2.6f, 2.1f, 2.6f), 1);
            kit.Blob(leaves, new Vector3(1.5f, 5.4f, 0.4f), new Vector3(1.8f, 1.5f, 1.8f), 2);
            kit.Blob(leaves, new Vector3(-1.3f, 5.6f, -0.8f), new Vector3(1.9f, 1.6f, 1.9f), 3);
            kit.Blob(leaves, new Vector3(0.2f, 7.3f, -0.3f), new Vector3(1.6f, 1.3f, 1.6f), 4);
        }

        static void BuildBamboo(MeshKit kit)
        {
            var culm = Mat("Bamboo Culm", 0x7a9a3c, 0.35f);
            var leaves = Mat("Bamboo Leaves", 0x5b8a34, 0.1f, true);
            var random = new System.Random(7);
            float R() => (float)random.NextDouble();

            for (var i = 0; i < 7; i++)
            {
                var angle = i * 2.4f;
                var foot = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (0.15f + 0.35f * R());
                var height = 7 + 3.5f * R();
                var lean = new Vector3(foot.x, 0, foot.z).normalized * (0.6f + 0.8f * R());
                var top = foot + new Vector3(lean.x, height, lean.z);
                kit.Cylinder(culm, foot, top, 0.07f, 0.04f, 5, false);

                // Feathery tufts along the upper half: small, upright, leaning out from the culm.
                for (var k = 0; k < 5; k++)
                {
                    var at = Vector3.Lerp(foot, top, 0.5f + 0.11f * k);
                    var out_ = new Vector3(R() - 0.5f, 0, R() - 0.5f).normalized * (0.25f + 0.2f * R());
                    kit.Blob(leaves, at + out_, new Vector3(0.32f, 0.55f, 0.32f), i * 5 + k);
                }
            }
        }

        static void SowGrass(TerrainData data)
        {
            var kit = new MeshKit();
            var blade = Mat("Grass Blade", 0x6a8a3e, 0.05f, true);
            var random = new System.Random(3);

            for (var i = 0; i < 9; i++)
            {
                var angle = i * 0.7f + (float)random.NextDouble();
                var foot = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.12f * (float)random.NextDouble();
                var bend = new Vector3(Mathf.Cos(angle * 1.7f), 0, Mathf.Sin(angle * 1.7f)) * 0.18f;
                var side = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 0.03f;
                var tip = foot + bend + Vector3.up * (0.35f + 0.25f * (float)random.NextDouble());
                kit.Triangle(blade, foot - side, foot + side, tip, Vector3.Cross(side, Vector3.up));
            }

            var (mesh, used) = kit.Build("Grass");
            var meshPath = $"{Root}/Meshes/Grass.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            var go = new GameObject("Grass");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = used;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/Grass.prefab");
            Object.DestroyImmediate(go);

            data.detailPrototypes = new[]
            {
                new DetailPrototype
                {
                    prototype = prefab,
                    usePrototypeMesh = true,
                    useInstancing = true,
                    renderMode = DetailRenderMode.VertexLit,
                    minWidth = 0.8f,
                    maxWidth = 1.4f,
                    minHeight = 0.7f,
                    maxHeight = 1.5f,
                    healthyColor = Hex(0xd8f0b0),
                    dryColor = Hex(0xc8c08a),
                    noiseSpread = 0.3f,
                },
            };
            data.SetDetailResolution(DetailResolution, 32);
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            var layer = new int[DetailResolution, DetailResolution];
            var cell = QingMao.Size / DetailResolution;

            for (var j = 0; j < DetailResolution; j++)
            {
                for (var i = 0; i < DetailResolution; i++)
                {
                    var p = new Vector2(-QingMao.Half + (i + 0.5f) * cell, -QingMao.Half + (j + 0.5f) * cell);
                    var steep = data.GetSteepness((i + 0.5f) / DetailResolution, (j + 0.5f) / DetailResolution);

                    if (steep > 30 || QingMao.ToPath(p) < QingMao.PathHalfWidth + 0.6f || Vector2.Distance(p, C) < QingMao.VillageRadius - 2 ||
                        DistanceToRiverLine(p) < QingMao.RiverHalfWidth + 1.5f)
                    {
                        continue;
                    }

                    var meadow = Mathf.PerlinNoise(p.x * 0.02f + 5, p.y * 0.02f + 9);
                    layer[j, i] = Forested(p) ? (meadow > 0.6f ? 1 : 0) : Mathf.RoundToInt(meadow * 4);
                }
            }

            data.SetDetailLayer(0, 0, 0, layer);
        }

        // ── Gu Yue village ────────────────────────────────────────────

        static float Ground(Vector2 p) => QingMao.HeightAt(p.x, p.y);

        /// <summary>The lowest and highest ground under a rectangle (centre, half size, turn about Y in degrees).</summary>
        static (float low, float high) Under(Vector2 centre, Vector2 half, float yaw)
        {
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            var turn = Quaternion.Euler(0, yaw, 0);

            for (var i = -1; i <= 1; i++)
            {
                for (var k = -1; k <= 1; k++)
                {
                    var offset = turn * new Vector3(i * half.x, 0, k * half.y);
                    var h = Ground(centre + new Vector2(offset.x, offset.z));
                    low = Mathf.Min(low, h);
                    high = Mathf.Max(high, h);
                }
            }

            return (low, high);
        }

        /// <summary>Places a built mesh (its own asset, shared by every copy).</summary>
        static GameObject Place(Transform parent, string name, (Mesh mesh, Material[] materials) built, Vector3 at, float yaw, bool solid = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            go.AddComponent<MeshFilter>().sharedMesh = built.mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = built.materials;

            if (solid)
            {
                go.AddComponent<MeshCollider>().sharedMesh = built.mesh;
            }

            return go;
        }

        static readonly Dictionary<string, (Mesh, Material[])> built = new Dictionary<string, (Mesh, Material[])>();

        /// <summary>A mesh made once and kept as an asset.</summary>
        static (Mesh, Material[]) Make(string name, System.Action<MeshKit> build)
        {
            if (built.TryGetValue(name, out var done))
            {
                return done;
            }

            var kit = new MeshKit();
            build(kit);
            var result = kit.Build(name);
            var path = $"{Root}/Meshes/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(result.mesh, path);
            built[name] = result;

            return result;
        }

        // The village's materials: the Gu Yue's palette (Reference/Design/project/Houses.dc.html) —
        // dark timber, blue-grey tiles, blue-grey stone, the lore's dark green bamboo, pale moonlight
        // lanterns — a shade lighter than drawn, for the daylight. More in QingMaoBuildings.
        static Material Bamboo => Mat("Bamboo Wall", 0x4f6b3c, 0.25f, false, BambooTexture(), 1);
        static Material Wood => Mat("Wood", 0x5a3e2a, 0.15f);
        static Material DarkWood => Mat("Dark Wood", 0x2e211a, 0.15f);
        static Material Lacquer => Mat("Red Lacquer", 0x6b2420, 0.35f);
        static Material Tiles => Mat("Roof Tiles", 0x4a566a, 0.25f, true, TilesTexture(), 0.5f);
        static Material Thatch => Mat("Thatch", 0x6a5530, 0.05f, true, ThatchTexture(), 0.5f);
        static Material Stone => Mat("Stone", 0x67686d, 0.1f, false, StoneTexture(), 0.5f);
        static Material Paper => Glow(Mat("Paper Window", 0xbfb394, 0.1f));
        static Material Gold => Mat("Gold", 0xc89a3a, 0.6f);
        static Material Lantern => Glow(Mat("Lantern", 0xbfd6f2, 0.3f));

        /// <summary>A material that can glow: its emission on (dark until the night lights it, Atmosphere).</summary>
        static Material Glow(Material material)
        {
            if (!material.IsKeywordEnabled("_EMISSION"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        static Texture2D bambooTexture;
        static Texture2D tilesTexture;

        static Texture2D BambooTexture() => bambooTexture != null ? bambooTexture : bambooTexture = Texture("Bamboo", 256, (u, v) =>
        {
            // Upright culms side by side, with a node now and then.
            var stripe = Mathf.Abs(Mathf.Repeat(u * 12, 1) - 0.5f) * 2;
            var node = Mathf.Abs(Mathf.Repeat(v * 3 + Mathf.Floor(u * 12) * 0.37f, 1) - 0.5f) < 0.025f;
            var shade = 0.82f + 0.18f * (1 - stripe * stripe) - (node ? 0.25f : 0);
            return new Color(shade, shade, shade);
        });

        static Texture2D TilesTexture() => tilesTexture != null ? tilesTexture : tilesTexture = Texture("Roof Tiles", 256, (u, v) =>
        {
            // The rows are in the roofs' own shape now: here only weathering, a tile darker or lighter.
            var tile = TileNoise(u * 16, v * 32, 16, 61);
            var weather = TileFbm(u, v, 3, 62);
            var shade = 0.78f + 0.14f * tile + 0.16f * weather;
            return new Color(shade, shade, shade * 1.02f);
        });

        static Texture2D thatchTexture;
        static Texture2D stoneTexture;

        static Texture2D ThatchTexture() => thatchTexture != null ? thatchTexture : thatchTexture = Texture("Thatch", 256, (u, v) =>
        {
            // Straw in bundles: fine streaks, combed one way.
            var straw = TileNoise(u * 64, v * 6, 64, 71);
            var bundle = TileFbm(u, v, 4, 72);
            var shade = 0.7f + 0.22f * straw + 0.18f * bundle;
            return new Color(shade, shade * 0.97f, shade * 0.9f);
        });

        static Texture2D StoneTexture() => stoneTexture != null ? stoneTexture : stoneTexture = Texture("Dressed Stone", 256, (u, v) =>
        {
            // Dressed blocks in courses, each its own shade, the joints dark.
            var row = Mathf.FloorToInt(v * 4);
            var x = u * 2 + (row % 2) * 0.5f;
            var joint = Mathf.Min(Mathf.Abs(Mathf.Repeat(v * 4, 1) - 0.5f), Mathf.Abs(Mathf.Repeat(x, 1) - 0.5f)) > 0.47f;
            var block = TileNoise(Mathf.Floor(x) * 3.1f, row * 1.7f, 64, 81);
            var grain = TileFbm(u, v, 8, 82);
            var shade = joint ? 0.55f : 0.82f + 0.12f * block + 0.12f * grain;
            return new Color(shade, shade, shade * 1.02f);
        });

        /// <summary>The main village's doorsteps, as the houses are placed (for its plan).</summary>
        static readonly List<GuWorld.Village.VillageLayout.Spot> doorsteps = new List<GuWorld.Village.VillageLayout.Spot>();
        static readonly List<GuWorld.Village.VillageLayout.Spot> places = new List<GuWorld.Village.VillageLayout.Spot>();

        static void BuildVillage(Transform village)
        {
            built.Clear();
            doorsteps.Clear();
            places.Clear();
            BuildWalls(village);
            BuildPavilion(village);
            BuildShrine(village);
            BuildAcademy(village);
            BuildMarket(village);
            BuildHouses(village, C, QingMao.VillageRadius - 10, 30, 13, true);
            BuildLanterns(village);
            BuildLayout(village);
        }

        /// <summary>The walls: stone footing, plank wall, a walkway's parapet; towers and four gates.</summary>
        static void BuildWalls(Transform village)
        {
            var walls = new GameObject("Walls").transform;
            walls.SetParent(village, false);
            const int segments = 72;
            var radius = QingMao.VillageRadius;
            var segmentLength = 2 * Mathf.PI * radius / segments;

            var wall = Make("Wall Segment", kit => WallSegment(kit, segmentLength));
            var ladder = Make("Ladder", kit =>
            {
                kit.Box(Wood, new Vector3(-0.3f, 2.6f, 0), new Vector3(0.08f, 5.6f, 0.08f));
                kit.Box(Wood, new Vector3(0.3f, 2.6f, 0), new Vector3(0.08f, 5.6f, 0.08f));

                for (var i = 0; i < 12; i++)
                {
                    kit.Box(Wood, new Vector3(0, 0.3f + i * 0.45f, 0), new Vector3(0.6f, 0.05f, 0.06f));
                }
            });
            var tower = Make("Tower", Watchtower);
            var gate = Make("Gate", ClanGate);

            // Segments are centred on the gates' directions (east, north, west, south: every 90°).
            for (var s = 0; s < segments; s++)
            {
                var angle = 2 * Mathf.PI * s / segments;
                var at = C + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                // Facing out: the segment's length runs along the ring.
                var yaw = -angle * Mathf.Rad2Deg + 90;

                if (s % (segments / 4) == 0)
                {
                    var (low, _) = Under(at, new Vector2(6, 1.5f), yaw);
                    Place(walls, $"Gate {s / (segments / 4)}", gate, new Vector3(at.x, low - 0.5f, at.y), yaw);

                    continue;
                }

                var (bottom, top) = Under(at, new Vector2(segmentLength / 2, 0.8f), yaw);
                Place(walls, $"Wall {s}", wall, new Vector3(at.x, bottom - 0.6f, at.y), yaw);

                if (s % 6 == 3)
                {
                    var inside = C + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius - 1.6f);
                    var go = Place(walls, $"Ladder {s}", ladder, new Vector3(inside.x, Ground(inside), inside.y), yaw, false);
                    go.transform.Rotate(-14, 0, 0, Space.Self);
                }

                if (s % 6 == 0 && s % (segments / 4) != 0)
                {
                    var outer = C + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius + 0.5f);
                    var (towerLow, _) = Under(outer, new Vector2(2.5f, 2.5f), yaw);
                    Place(walls, $"Tower {s}", tower, new Vector3(outer.x, towerLow - 1, outer.y), yaw);
                }
            }
        }

        /// <summary>The clan pavilion (QingMaoBuildings.ClanPavilion) at the head of the square.</summary>
        static void BuildPavilion(Transform village)
        {
            var pavilion = Make("Clan Pavilion", ClanPavilion);
            var (low, _) = Under(Pavilion, new Vector2(11, 9.5f), 0);
            Place(village, "Clan Pavilion", pavilion, new Vector3(Pavilion.x, low, Pavilion.y), 0);
        }

        static void BuildShrine(Transform village)
        {
            var shrine = Make("Shrine", AncestralHall);
            var (low, _) = Under(Shrine, new Vector2(6, 5), 0);
            Place(village, "Shrine", shrine, new Vector3(Shrine.x, low, Shrine.y), 0);
        }

        static void BuildAcademy(Transform village)
        {
            var academy = Make("Academy", AcademyHall);
            var (low, _) = Under(Academy, new Vector2(10.5f, 6.2f), 0);
            Place(village, "Academy", academy, new Vector3(Academy.x, low, Academy.y), 0);

            var hostel = Make("Hostel", HostelHouse);
            var (hostelLow, _) = Under(Hostel, new Vector2(7.5f, 4.5f), 180);
            Place(village, "Hostel", hostel, new Vector3(Hostel.x, hostelLow, Hostel.y), 180);
        }

        /// <summary>Houses round the streets: a jittered grid inside the walls, each facing its nearest street.</summary>
        static void BuildHouses(Transform village, Vector2 centre, float outer, float inner, float spacing, bool main, Clan clan = Clan.GuYue)
        {
            var houses = new GameObject("Houses").transform;
            houses.SetParent(village, false);
            var kinds = HouseKinds(clan);
            var random = new System.Random(main ? 11 : (int)(centre.x * 13 + centre.y));
            float R() => (float)random.NextDouble();
            var count = 0;

            var reserved = main
                ? new (Vector2 at, float radius)[] { (Pavilion, 15), (Square, 26), (Shrine, 10), (Academy, 14), (Hostel, 11) }
                : new (Vector2 at, float radius)[] { (centre, 10) };

            for (var z = -outer; z <= outer; z += spacing)
            {
                for (var x = -outer; x <= outer; x += spacing)
                {
                    var p = centre + new Vector2(x + (R() - 0.5f) * 3, z + (R() - 0.5f) * 3);
                    var r = Vector2.Distance(p, centre);

                    if (r > outer || r < inner || reserved.Any(s => Vector2.Distance(p, s.at) < s.radius))
                    {
                        continue;
                    }

                    // The nearest street, and which way it lies.
                    Vector2 toward;
                    float distance;

                    if (main)
                    {
                        var ring = Mathf.Abs(r - RingRadius);
                        var best = ring;
                        var target = centre + (p - centre).normalized * RingRadius;

                        foreach (var street in Streets)
                        {
                            var d = QingMao.ToSegment(p, street[0], street[1]);

                            if (d < best)
                            {
                                best = d;
                                var ab = street[1] - street[0];
                                var t = Mathf.Clamp01(Vector2.Dot(p - street[0], ab) / ab.sqrMagnitude);
                                target = street[0] + ab * t;
                            }
                        }

                        distance = best;
                        toward = target - p;
                    }
                    else
                    {
                        distance = 99;
                        toward = centre - p;
                    }

                    if (distance < 7.5f)
                    {
                        continue;
                    }

                    var yaw = Mathf.Atan2(toward.x, toward.y) * Mathf.Rad2Deg;
                    var which = random.Next(kinds.Length);
                    var (kind, depth) = kinds[which];
                    var (low, high) = Under(p, new Vector2(4.5f, 4), yaw);
                    Place(houses, $"House {count++}", kind, new Vector3(p.x, high - 0.15f, p.y), yaw);

                    if (main)
                    {
                        // The doorstep: just past the foot of the steps, facing out.
                        var step = Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, depth / 2 + 3.3f);
                        var door = p + new Vector2(step.x, step.z);
                        doorsteps.Add(new GuWorld.Village.VillageLayout.Spot { name = $"house {doorsteps.Count}", at = new Vector3(door.x, Ground(door), door.y), yaw = yaw });
                    }
                }
            }
        }

        static void Spot(string name, Vector2 at, float yaw)
        {
            places.Add(new GuWorld.Village.VillageLayout.Spot { name = name, at = new Vector3(at.x, Ground(at), at.y), yaw = yaw });
        }

        /// <summary>Stalls on the square under cloth awnings, with their wares, and a well with a little roof.</summary>
        static void BuildMarket(Transform village)
        {
            var market = new GameObject("Market").transform;
            market.SetParent(village, false);
            var awnings = new[] { Mat("Awning Red", 0x7a2e24, 0.05f, true), Mat("Awning Indigo", 0x2e3a52, 0.05f, true), Mat("Awning Ochre", 0x8a6a3a, 0.05f, true) };
            var wares = new[] { Mat("Ware Green", 0x5a8a3a, 0.2f), Mat("Ware Orange", 0xc8782a, 0.2f), Mat("Ware Cloth", 0xb8a888, 0.1f) };
            var stalls = new[] { Square + new Vector2(-15, -8), Square + new Vector2(15, -8), Square + new Vector2(-15, 8) };

            for (var i = 0; i < stalls.Length; i++)
            {
                var awning = awnings[i];
                var ware = wares[i];
                var stall = Make($"Stall {i}", kit =>
                {
                    kit.Box(Wood, new Vector3(0, 0.85f, 0), new Vector3(3, 0.12f, 1.2f));
                    kit.Box(DarkWood, new Vector3(0, 0.42f, 0), new Vector3(2.8f, 0.8f, 1));

                    foreach (var x in new[] { -1.45f, 1.45f })
                    {
                        kit.Box(DarkWood, new Vector3(x, 1.25f, 0.55f), new Vector3(0.1f, 2.5f, 0.1f));
                        kit.Box(DarkWood, new Vector3(x, 1.5f, -1.1f), new Vector3(0.1f, 3, 0.1f));
                    }

                    // The awning, sloping down towards the buyers (+Z).
                    kit.Quad(awning, new Vector3(-1.7f, 3.05f, -1.3f), new Vector3(1.7f, 3.05f, -1.3f), new Vector3(1.7f, 2.45f, 0.9f), new Vector3(-1.7f, 2.45f, 0.9f), Vector3.up);

                    for (var k = 0; k < 7; k++)
                    {
                        kit.Blob(ware, new Vector3(-1.1f + k * 0.36f, 1.05f, 0.15f * ((k % 2) * 2 - 1)), new Vector3(0.17f, 0.15f, 0.17f), i * 7 + k);
                    }

                    kit.Box(ware, new Vector3(1.9f, 0.3f, -0.4f), new Vector3(0.6f, 0.6f, 0.6f));
                });
                var at = stalls[i];
                var yaw = Mathf.Atan2(Square.x - at.x, Square.y - at.y) * Mathf.Rad2Deg;
                Place(market, $"Stall {i}", stall, new Vector3(at.x, Ground(at), at.y), yaw);
                var behind = Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, -0.95f);
                Spot($"stall {i}", at + new Vector2(behind.x, behind.z), yaw);
            }

            var well = Make("Well", VillageWell);
            var wellAt = Square + new Vector2(15, 8);
            Place(market, "Well", well, new Vector3(wellAt.x, Ground(wellAt), wellAt.y), 0);
            Spot("well", wellAt + new Vector2(-1.8f, 0), 90);
        }

        /// <summary>
        /// The village's plan for the people in it (VillageLayout): named places, the doorsteps,
        /// and the walkways — the streets, the ring, the square, the fronts of the great
        /// buildings, and the paths out to the spring and the grove.
        /// </summary>
        static void BuildLayout(Transform village)
        {
            // Named places.
            Spot("square", Square, 0);
            Spot("pavilion", Pavilion + new Vector2(0, -12.5f), 180);
            Spot("shrine", Shrine + new Vector2(0, -9), 0);
            Spot("academy", Academy + new Vector2(0, -8.5f), 180);
            Spot("academy yard", Academy + new Vector2(0, -13), 0);
            Spot("hostel", Hostel + new Vector2(0, 6.5f), 0);
            Spot("gate north", C + new Vector2(4, 84), 0);
            Spot("gate south", C + new Vector2(-4, -84), 180);
            Spot("gate east", C + new Vector2(84, 4), 90);
            Spot("gate west", C + new Vector2(-84, -4), 270);
            Spot("spring", QingMao.Spring + new Vector2(9, -7), Mathf.Atan2(-9, 7) * Mathf.Rad2Deg);
            Spot("grove", new Vector2(176, -110), 90);

            for (var i = 0; i < 8; i++)
            {
                var a = i * Mathf.PI / 4 + 0.3f;
                Spot($"ring {i}", C + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RingRadius, 0);
            }

            // The walkways.
            var nodes = new List<Vector2>();
            var edges = new HashSet<(int, int)>();

            void Link(int a, int b)
            {
                if (a != b)
                {
                    edges.Add(a < b ? (a, b) : (b, a));
                }
            }

            int Node(Vector2 at)
            {
                for (var i = 0; i < nodes.Count; i++)
                {
                    if ((nodes[i] - at).sqrMagnitude < 1)
                    {
                        return i;
                    }
                }

                nodes.Add(at);

                return nodes.Count - 1;
            }

            void Line(params Vector2[] points)
            {
                var previous = -1;

                for (var i = 0; i + 1 < points.Length; i++)
                {
                    var length = Vector2.Distance(points[i], points[i + 1]);
                    var steps = Mathf.Max(1, Mathf.CeilToInt(length / 8));

                    for (var k = 0; k <= steps; k++)
                    {
                        if (i > 0 && k == 0)
                        {
                            continue;
                        }

                        var n = Node(Vector2.Lerp(points[i], points[i + 1], (float)k / steps));

                        if (previous >= 0)
                        {
                            Link(previous, n);
                        }

                        previous = n;
                    }
                }
            }

            foreach (var street in Streets)
            {
                Line(street);
            }

            var ring = new List<Vector2>();

            for (var i = 0; i <= 48; i++)
            {
                var a = 2 * Mathf.PI * i / 48;
                ring.Add(C + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RingRadius);
            }

            Line(ring.ToArray());

            // The square: a grid.
            for (var z = -8; z <= 8; z += 8)
            {
                Line(Square + new Vector2(-20, z), Square + new Vector2(20, z));
            }

            for (var x = -20; x <= 20; x += 10)
            {
                Line(Square + new Vector2(x, -8), Square + new Vector2(x, 8));
            }

            // Out of the gates: to the spring, the grove, and a way down the south and west paths.
            Line(QingMao.Paths[1]);
            Line(QingMao.Paths[3]);
            Line(QingMao.Paths[0][0], QingMao.Paths[0][1], QingMao.Paths[0][2]);
            Line(QingMao.Paths[4][0], QingMao.Paths[4][1], QingMao.Paths[4][2]);

            // Crossings: nodes of different ways close together are joined.
            for (var a = 0; a < nodes.Count; a++)
            {
                for (var b = a + 1; b < nodes.Count; b++)
                {
                    if ((nodes[a] - nodes[b]).sqrMagnitude < 7 * 7)
                    {
                        Link(a, b);
                    }
                }
            }

            // Every place and doorstep joins the nearest way.
            foreach (var spot in places.Concat(doorsteps).ToList())
            {
                var at = new Vector2(spot.at.x, spot.at.z);
                var nearest = 0;

                for (var i = 1; i < nodes.Count; i++)
                {
                    if ((nodes[i] - at).sqrMagnitude < (nodes[nearest] - at).sqrMagnitude)
                    {
                        nearest = i;
                    }
                }

                Link(Node(at), nearest);
            }

            var layout = village.gameObject.AddComponent<GuWorld.Village.VillageLayout>();
            layout.Houses = doorsteps.ToArray();
            layout.Places = places.ToArray();
            layout.Nodes = nodes.Select(n => new Vector3(n.x, Ground(n), n.y)).ToArray();
            layout.Edges = edges.Select(e => new Vector2Int(e.Item1, e.Item2)).ToArray();
        }

        /// <summary>Lantern posts along the main streets and round the square.</summary>
        static void BuildLanterns(Transform village)
        {
            var lanterns = new GameObject("Lanterns").transform;
            lanterns.SetParent(village, false);
            var post = Make("Lantern Post", LanternPost);
            var count = 0;

            foreach (var street in Streets)
            {
                var length = Vector2.Distance(street[0], street[1]);
                var along = (street[1] - street[0]).normalized;
                var across = Vector2.Perpendicular(along);

                for (var s = 10f; s < length - 6; s += 14)
                {
                    foreach (var side in new[] { -1, 1 })
                    {
                        var p = street[0] + along * s + across * side * 3.4f;
                        var yaw = Mathf.Atan2(-across.x * side, -across.y * side) * Mathf.Rad2Deg - 90;
                        Place(lanterns, $"Lantern {count++}", post, new Vector3(p.x, Ground(p), p.y), yaw, false);
                    }
                }
            }
        }

        /// <summary>A smaller village on its own hill: a palisade, a hall, houses round it (seen from afar for now).</summary>
        static void BuildSmallVillage(string name, Vector2 centre, int seed, Clan clan)
        {
            var village = new GameObject(name).transform;
            var radius = QingMao.SmallVillageRadius;
            var palisade = Make("Palisade", kit =>
            {
                for (var i = 0; i < 9; i++)
                {
                    kit.Cylinder(Wood, new Vector3(-2 + i * 0.5f, -1, 0), new Vector3(-2 + i * 0.5f, 3.4f, 0), 0.22f, 0.12f, 5);
                }
            });
            const int segments = 56;

            for (var s = 0; s < segments; s++)
            {
                if (s % (segments / 2) == segments / 4)
                {
                    continue;
                }

                var angle = 2 * Mathf.PI * s / segments;
                var at = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var yaw = -angle * Mathf.Rad2Deg + 90;
                Place(village, $"Palisade {s}", palisade, new Vector3(at.x, Ground(at), at.y), yaw);
            }

            var hall = Make($"{clan} Hall", kit => VillageHall(kit, clan));
            Place(village, "Hall", hall, new Vector3(centre.x, Ground(centre), centre.y), seed * 30);
            BuildHouses(village, centre, radius - 7, 13, 11, false, clan);
        }

        // ── Water, horizon, the game ──────────────────────────────────

        static void BuildRiver()
        {
            var water = Mat("Water", 0x3c6a72, 0.72f, true);
            water.SetFloat("_Surface", 1);
            water.SetFloat("_Blend", 0);
            water.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            water.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            water.SetFloat("_ZWrite", 0);
            water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            water.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            water.SetOverrideTag("RenderType", "Transparent");
            water.color = new Color(0.24f, 0.42f, 0.45f, 0.78f);

            var kit = new MeshKit();
            var points = new List<Vector2>();

            for (var i = 0; i + 1 < QingMao.River.Length; i++)
            {
                var length = Vector2.Distance(QingMao.River[i], QingMao.River[i + 1]);

                for (var s = 0f; s < length; s += 4)
                {
                    points.Add(Vector2.Lerp(QingMao.River[i], QingMao.River[i + 1], s / length));
                }
            }

            points.Add(QingMao.River[QingMao.River.Length - 1]);
            var halfWidth = QingMao.RiverHalfWidth + 1.5f;

            for (var i = 0; i + 1 < points.Count; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var across = Vector2.Perpendicular((b - a).normalized) * halfWidth;
                var ya = QingMao.WaterAt(a);
                var yb = QingMao.WaterAt(b);
                kit.Quad(water,
                    new Vector3(a.x - across.x, ya, a.y - across.y), new Vector3(a.x + across.x, ya, a.y + across.y),
                    new Vector3(b.x + across.x, yb, b.y + across.y), new Vector3(b.x - across.x, yb, b.y - across.y), Vector3.up);
            }

            // The spring: a round pool where the river begins, ringed with stones.
            var springLevel = QingMao.WaterAt(QingMao.Spring);
            var spring = new Vector3(QingMao.Spring.x, springLevel, QingMao.Spring.y);

            for (var i = 0; i < 20; i++)
            {
                var a0 = 2 * Mathf.PI * i / 20;
                var a1 = 2 * Mathf.PI * (i + 1) / 20;
                kit.Triangle(water, spring, spring + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 9, spring + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * 9, Vector3.up);
            }

            var (mesh, used) = kit.Build("River");
            AssetDatabase.DeleteAsset($"{Root}/Meshes/River.asset");
            AssetDatabase.CreateAsset(mesh, $"{Root}/Meshes/River.asset");
            var go = new GameObject("River");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = used;

            var stones = Make("Spring Stones", k =>
            {
                for (var i = 0; i < 14; i++)
                {
                    var a = 2 * Mathf.PI * i / 14;
                    k.Blob(Stone, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 10, new Vector3(1.3f, 0.8f, 1.1f), i);
                }
            });
            Place(go.transform, "Spring Stones", stones, new Vector3(spring.x, springLevel - 0.4f, spring.z), 0);
        }

        /// <summary>
        /// The land round the mountain, out to 3.2 km: from the map's edge (where the
        /// terrain settles at the valley floor's height) hills roll up into mountains
        /// on the horizon. Steep faces are rock, the rest grass and woods; the haze does
        /// the rest. No edge to the world can be seen.
        /// </summary>
        static float Surrounding(float x, float z)
        {
            var d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) - QingMao.Half;
            var n = Mathf.PerlinNoise(x * 0.0011f + 3, z * 0.0011f + 8);
            var ridge = 1 - Mathf.Abs(Mathf.PerlinNoise(x * 0.0019f + 17, z * 0.0019f + 2) * 2 - 1);
            var hills = 55 * Mathf.PerlinNoise(x * 0.004f + 5, z * 0.004f + 1) * SmoothStep(0, 300, d);
            var mountains = SmoothStep(250, 1900, d) * (260 + 720 * n) * (0.5f + 0.5f * ridge);

            return 6 + hills + mountains;
        }

        static void BuildSurroundings()
        {
            var grassTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/Grass.png");
            var rockTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/Rock.png");
            // White: the textures' own colours, the same as the mountain's ground, so no seam shows.
            var land = Mat("Far Land", 0xffffff, 0.02f, false, grassTexture, 0.2f);
            var rock = Mat("Far Rock", 0xf2f0ea, 0.02f, false, rockTexture, 0.11f);
            const float extent = 3200;
            const float cell = 40;
            var cells = Mathf.RoundToInt(extent * 2 / cell);
            var row = cells + 1;
            var vertices = new Vector3[row * row];
            var uvs = new Vector2[vertices.Length];

            for (var j = 0; j <= cells; j++)
            {
                for (var i = 0; i <= cells; i++)
                {
                    var x = -extent + i * cell;
                    var z = -extent + j * cell;
                    vertices[j * row + i] = new Vector3(x, Surrounding(x, z), z);
                    uvs[j * row + i] = new Vector2(x, z);
                }
            }

            var landTriangles = new List<int>();
            var rockTriangles = new List<int>();

            for (var j = 0; j < cells; j++)
            {
                for (var i = 0; i < cells; i++)
                {
                    var x0 = -extent + i * cell;
                    var z0 = -extent + j * cell;

                    // The terrain's square is the mountain's own.
                    if (x0 >= -QingMao.Half && x0 + cell <= QingMao.Half && z0 >= -QingMao.Half && z0 + cell <= QingMao.Half)
                    {
                        continue;
                    }

                    var a = j * row + i;
                    var b = a + 1;
                    var c = a + row + 1;
                    var d = a + row;

                    // Unity's front face is clockwise seen from above here: a, d, c and a, c, b.
                    foreach (var (p, q, r) in new[] { (a, d, c), (a, c, b) })
                    {
                        var normal = Vector3.Cross(vertices[q] - vertices[p], vertices[r] - vertices[p]).normalized;
                        (normal.y < 0.7f ? rockTriangles : landTriangles).AddRange(new[] { p, q, r });
                    }
                }
            }

            var mesh = new Mesh { name = "Surroundings", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(landTriangles, 0);
            mesh.SetTriangles(rockTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var used = new[] { land, rock };
            AssetDatabase.DeleteAsset($"{Root}/Meshes/Surroundings.asset");
            AssetDatabase.DeleteAsset($"{Root}/Meshes/Horizon.asset");
            AssetDatabase.CreateAsset(mesh, $"{Root}/Meshes/Surroundings.asset");
            var go = new GameObject("Surroundings");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = used;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// The sky and its weather: the procedural sky, the textures and materials of the
        /// clouds, mist, moon, stars and rain (GuWorld/Sky), lamps round the square, the
        /// post-processing (bloom, tone mapping, a little vignette), and the Atmosphere
        /// that runs them.
        /// </summary>
        static void BuildAtmosphere()
        {
            var skybox = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/Sky.mat");

            if (skybox == null)
            {
                skybox = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(skybox, $"{Root}/Materials/Sky.mat");
            }

            skybox.SetFloat("_SunDisk", 2);
            skybox.SetFloat("_SunSizeConvergence", 6);
            EditorUtility.SetDirty(skybox);
            RenderSettings.skybox = skybox;

            var cloud = Texture("Clouds", 512, (u, v) =>
            {
                var n = TileFbm(u, v, 4, 21);
                var puff = SmoothStep(0.44f, 0.72f, n);
                var shade = 0.82f + 0.18f * SmoothStep(0.5f, 0.85f, TileFbm(u + 0.01f, v + 0.02f, 4, 21));
                return new Color(shade, shade, shade * 1.02f, puff);
            }, false);
            var haze = Texture("Mist", 256, (u, v) =>
            {
                var n = TileFbm(u, v, 3, 31);
                return new Color(1, 1, 1, SmoothStep(0.25f, 0.8f, n));
            }, false);
            var moonDisc = Texture("Moon", 256, (u, v) =>
            {
                var d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
                var disc = 1 - SmoothStep(0.17f, 0.18f, d);
                var craters = 0.82f + 0.18f * TileFbm(u * 2, v * 2, 3, 41);
                var glow = Mathf.Pow(1 - Mathf.Clamp01(d / 0.5f), 3) * 0.35f;
                var light = disc * craters + glow;
                return new Color(0.92f, 0.94f, 1, Mathf.Clamp01(light));
            }, false, false);
            var star = Texture("Star", 64, (u, v) =>
            {
                var d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2;
                var a = Mathf.Pow(Mathf.Clamp01(1 - d), 2.5f);
                return new Color(1, 1, 1, a);
            }, false, false);
            var streak = Texture("Rain", 64, (u, v) =>
            {
                var across = 1 - Mathf.Abs(u - 0.5f) * 2;
                var along = Mathf.Sin(v * Mathf.PI);
                return new Color(1, 1, 1, across * across * along);
            }, false, false);

            var game = new GameObject("Atmosphere");
            var atmosphere = game.AddComponent<GuWorld.Render.Atmosphere>();
            atmosphere.CloudMaterial = SkyMat("Clouds", cloud, false, 3000, 1);
            atmosphere.MistMaterial = SkyMat("Mist", haze, false, 3010, 1);
            atmosphere.StarMaterial = SkyMat("Stars", star, true, 2950, 1);
            atmosphere.MoonMaterial = SkyMat("Moon", moonDisc, true, 2960, 1);
            atmosphere.RainMaterial = SkyMat("Rain", streak, false, 3100, 1);
            atmosphere.LanternMaterial = Lantern;
            atmosphere.WindowMaterial = Paper;

            // Mist in the valleys: round the mountain's foot and out in the lowlands.
            var spots = new List<Vector4>();

            foreach (var (x, z) in new[] { (0f, -620f), (-380f, -620f), (360f, -640f), (600f, -180f), (-620f, -60f), (-560f, 380f), (520f, 420f), (120f, 700f) })
            {
                spots.Add(new Vector4(x, QingMao.HeightAt(x, z) + 22, z, 420));
            }

            for (var i = 0; i < 14; i++)
            {
                var angle = i * Mathf.PI * 2 / 14 + 0.2f;
                var distance = 1050 + 180 * Mathf.Sin(i * 1.7f);
                var x = Mathf.Cos(angle) * distance;
                var z = Mathf.Sin(angle) * distance;
                spots.Add(new Vector4(x, Surrounding(x, z) + 30, z, 520));
            }

            atmosphere.MistSpots = spots.ToArray();

            // Lamps round the square and the pavilion, lit at night.
            var lamps = new List<Light>();
            var lampSpots = new[]
            {
                Square + new Vector2(-20, -10), Square + new Vector2(20, -10), Square + new Vector2(-20, 10), Square + new Vector2(20, 10),
                Pavilion + new Vector2(-6, -11), Pavilion + new Vector2(6, -11), Shrine + new Vector2(0, -9), Academy + new Vector2(0, -8),
            };

            foreach (var at in lampSpots)
            {
                var lamp = new GameObject("Lamp").AddComponent<Light>();
                lamp.transform.SetParent(game.transform, false);
                lamp.transform.position = new Vector3(at.x, Ground(at) + 3.2f, at.y);
                lamp.type = LightType.Point;
                lamp.range = 16;
                lamp.color = new Color(0.78f, 0.86f, 1);
                lamp.intensity = 0;
                lamp.shadows = LightShadows.None;
                lamps.Add(lamp);
            }

            atmosphere.NightLights = lamps.ToArray();

            // Post-processing: bloom (the lanterns glow), tone mapping, a little vignette and contrast;
            // as the art direction's evening scene asks, colour a little quieter, the shadows cold
            // and the lights warm.
            var profilePath = $"{Root}/QingMao Post.asset";
            AssetDatabase.DeleteAsset(profilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.65f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.45f);
            var colour = profile.Add<ColorAdjustments>(true);
            colour.contrast.Override(12);
            colour.saturation.Override(-16);
            var toning = profile.Add<SplitToning>(true);
            toning.shadows.Override(new Color(0.36f, 0.44f, 0.62f));
            toning.highlights.Override(new Color(0.62f, 0.56f, 0.46f));
            toning.balance.Override(-15);

            foreach (var component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            EditorUtility.SetDirty(profile);
            var post = new GameObject("Post Processing");
            var volume = post.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        /// <summary>A GuWorld/Sky material: alpha-blended or added (glow), drawn in its place in the queue.</summary>
        static Material SkyMat(string name, Texture2D texture, bool additive, int queue, float tiling)
        {
            var path = $"{Root}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var create = material == null;

            if (create)
            {
                material = new Material(Shader.Find("GuWorld/Sky"));
            }

            material.mainTexture = texture;
            material.mainTextureScale = Vector2.one * tiling;
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            material.renderQueue = queue;
            material.enableInstancing = true;

            if (create)
            {
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        /// <summary>The camera, the sun and the game, set for this mountain.</summary>
        static void BuildGame()
        {
            var camera = new GameObject("Main Camera") { tag = "MainCamera" };
            camera.AddComponent<Camera>().farClipPlane = 4000;
            camera.AddComponent<AudioListener>();
            camera.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;

            var root = new GameObject("GU World");
            var game = root.AddComponent<GuWorldGame>();
            GuWorldSetup.Configure(game);
            game.Location = QingMao.Settings;
            game.UseChunks = false;
            game.FogStart = 140;
            game.FogEnd = 2600;
            game.FarClip = 4000;
        }
    }
}
