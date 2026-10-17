using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// The mountain's materials and the textures painted for them (once, as
    /// ordinary assets in Locations/QingMao — kept in the project), and the
    /// folders the builder writes what it makes into (Generated — made again by
    /// every build, so not kept in git).
    ///
    /// The palette is the art direction's (Reference/Design/project/Houses.dc.html),
    /// a shade darker than before: the novel's mountain is a grim place — dark timber,
    /// blue-grey tiles and stone, the lore's dark green bamboo, cold moonlight lanterns.
    /// </summary>
    public static partial class QingMaoBuilder
    {
        const string Root = "Assets/GuWorld/Locations/QingMao";

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        static void Folders()
        {
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Textures");
            Directory.CreateDirectory(Generated + "/Terrain");
            Directory.CreateDirectory(Generated + "/Meshes");
            Directory.CreateDirectory(Generated + "/Prefabs");
            Directory.CreateDirectory(Generated + "/Parts");
            Directory.CreateDirectory(Generated + "/Stages");
            AssetDatabase.Refresh();
        }

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

            material.color = name == "Interior" ? Hex(rgb) : Lifted(Hex(rgb));
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

        /// <summary>A see-through-at-the-edges material (leaves, grass, the trails' ragged edges): alpha-clipped.</summary>
        static Material Cutout(string name, int rgb, Texture2D texture, float tiling = 1, float clip = 0.45f, bool twoSided = true)
        {
            var material = Mat(name, rgb, 0.05f, twoSided, texture, tiling);
            material.SetFloat("_AlphaClip", 1);
            material.SetFloat("_Cutoff", clip);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            material.SetOverrideTag("RenderType", "TransparentCutout");
            EditorUtility.SetDirty(material);

            return material;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);

        /// <summary>
        /// The least a surface's colour may be at its brightest channel (sRGB, as the palette is written): darker, it
        /// reflects so little that in the sun it reads as a hole and in the shade as nothing (needles at 0x111c14 were
        /// under 1% of white). The grim mood is the light's and the grade's, not black paint.
        /// </summary>
        const float ColourFloor = 0x48 / 255f;

        /// <summary>A colour raised to the floor, its hue and its own light and dark kept.</summary>
        static Color Lifted(Color colour)
        {
            var top = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));

            if (top >= ColourFloor || top <= 0.001f)
            {
                return colour;
            }

            var k = ColourFloor / top;

            return new Color(colour.r * k, colour.g * k, colour.b * k, colour.a);
        }

        /// <summary>
        /// Raises every material of the mountain that is darker than the floor (`-executeMethod
        /// GuWorld.Editor.QingMaoBuilder.RepaintMaterials`): the built mountain takes the new colours at once, no build
        /// needed. The dark of the rooms seen through doors (Interior) is left as it is.
        /// </summary>
        public static void RepaintMaterials()
        {
            var changed = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root + "/Materials" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));

                if (material == null || material.name == "Interior" || !material.HasProperty("_BaseColor") || material.shader.name != "Universal Render Pipeline/Lit")
                {
                    continue;
                }

                var colour = material.GetColor("_BaseColor");
                var lifted = Lifted(colour);

                if (lifted != colour)
                {
                    material.SetColor("_BaseColor", lifted);
                    EditorUtility.SetDirty(material);
                    changed++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"GU World: {changed} materials raised to the colour floor");
            BuildLog.Write($"repainted {changed} materials to the colour floor");
        }

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

        /// <summary>Makes and imports a tiling texture: `paint` gives each texel's colour from u, v in 0…1.</summary>
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

        static float SmoothStep(float min, float max, float x)
        {
            if (x <= min) return 0;
            if (x >= max) return 1;
            x = (x - min) / (max - min);

            return x * x * (3 - 2 * x);
        }

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

        // ── The village's materials ───────────────────────────────────

        static Material Bamboo => Mat("Bamboo Wall", 0x3f5a32, 0.25f, false, BambooTexture(), 1);
        static Material OldBamboo => Mat("Old Bamboo Wall", 0x5e5a38, 0.2f, false, BambooTexture(), 1);
        static Material Wood => Mat("Wood", 0x4a3424, 0.15f, false, PlankTexture(), 1);
        static Material DarkWood => Mat("Dark Wood", 0x261b15, 0.15f);
        static Material Lacquer => Mat("Red Lacquer", 0x5a1e1a, 0.35f);
        static Material Tiles => Mat("Roof Tiles", 0x3c4656, 0.25f, true, TilesTexture(), 0.5f);
        static Material TilesWorn => Mat("Roof Tiles Worn", 0x46484a, 0.18f, true, TilesTexture(), 0.5f);
        static Material TilesMoss => Mat("Roof Tiles Moss", 0x3a4636, 0.12f, true, TilesTexture(), 0.5f);
        static Material Thatch => Mat("Thatch", 0x5a4828, 0.05f, true, ThatchTexture(), 0.5f);
        static Material OldThatch => Mat("Old Thatch", 0x4a4232, 0.04f, true, ThatchTexture(), 0.5f);
        static Material Stone => Mat("Stone", 0x56575c, 0.1f, false, StoneTexture(), 0.5f);
        static Material Rubble => Mat("Rubble Stone", 0x4c4a46, 0.08f, false, RubbleTexture(), 0.4f);
        static Material Paper => Glow(Mat("Paper Window", 0xa89e82, 0.1f));
        static Material Gold => Mat("Gold", 0xb08a34, 0.6f);
        static Material Lantern => Glow(Mat("Lantern", 0xb4cbe6, 0.3f));
        static Material WarmLantern => Glow(Mat("Warm Lantern", 0xd8a868, 0.3f));
        static Material Plaster => Mat("Plaster", 0x5c4f42, 0.06f, false, PlasterTexture(), 0.5f);
        static Material Mud => Mat("Mud Wall", 0x5a4a38, 0.04f, false, PlasterTexture(), 0.4f);
        static Material RoofUnder => Mat("Roof Underside", 0x231a15, 0.1f);
        static Material Ridge => Mat("Roof Ridge", 0x181c25, 0.25f);
        static Material Silver => Mat("Silver", 0xb8c2cc, 0.75f);
        static Material Banner => Mat("Banner", 0x1b2540, 0.05f, true);
        static Material Bronze => Mat("Bronze", 0x6a5230, 0.45f);
        static Material Iron => Mat("Iron", 0x3a3c40, 0.55f);
        static Material Interior => Mat("Interior", 0x0b0a0c, 0);
        static Material BaiWall => Mat("Bai Wall", 0xbcb8b0, 0.05f, false, PlasterTexture(), 0.5f);
        static Material BaiRoof => Mat("Bai Roof", 0x3e4650, 0.2f, true, TilesTexture(), 1);
        static Material Snow => Mat("Roof Snow", 0xc8ced4, 0.3f);
        static Material Logs => Mat("Logs", 0x5a3e28, 0.08f, false, BarkTexture(), 0.6f);
        static Material Cloth(string name, int rgb) => Mat($"Cloth {name}", rgb, 0.05f, true);
        static Material Straw => Mat("Straw", 0x8a7448, 0.04f);
        static Material Clay => Mat("Clay", 0x6e4a32, 0.2f);
        static Material Glaze => Mat("Glazed Clay", 0x2a2e2a, 0.55f);
        static Material Soil => Mat("Soil", 0x2e241c, 0.02f);
        static Material Greens => Mat("Greens", 0x3c5a2a, 0.1f, true);
        static Material Water => WaterMaterial();

        static Texture2D bambooTexture, tilesTexture, thatchTexture, stoneTexture, plasterTexture, plankTexture, barkTexture, rubbleTexture;

        static Texture2D BambooTexture() => bambooTexture != null ? bambooTexture : bambooTexture = Texture("Bamboo", 256, (u, v) =>
        {
            // Upright culms side by side, with a node now and then.
            var stripe = Mathf.Abs(Mathf.Repeat(u * 12, 1) - 0.5f) * 2;
            var node = Mathf.Abs(Mathf.Repeat(v * 3 + Mathf.Floor(u * 12) * 0.37f, 1) - 0.5f) < 0.025f;
            var stain = TileFbm(u, v, 4, 11) * 0.18f;
            var shade = 0.8f + 0.18f * (1 - stripe * stripe) - (node ? 0.25f : 0) - stain;
            return new Color(shade, shade, shade);
        });

        static Texture2D TilesTexture() => tilesTexture != null ? tilesTexture : tilesTexture = Texture("Roof Tiles", 256, (u, v) =>
        {
            // The rows are in the roofs' own shape: here only weathering, a tile darker or lighter, streaks of moss.
            var tile = TileNoise(u * 16, v * 32, 16, 61);
            var weather = TileFbm(u, v, 3, 62);
            var moss = Mathf.Pow(TileFbm(u, v, 5, 63), 3) * 0.3f;
            var shade = 0.74f + 0.16f * tile + 0.18f * weather - moss;
            return new Color(shade - moss * 0.3f, shade + moss * 0.2f, shade * 1.02f - moss * 0.4f);
        });

        static Texture2D ThatchTexture() => thatchTexture != null ? thatchTexture : thatchTexture = Texture("Thatch", 256, (u, v) =>
        {
            // Straw in bundles: fine streaks, combed one way.
            var straw = TileNoise(u * 64, v * 6, 64, 71);
            var bundle = TileFbm(u, v, 4, 72);
            var shade = 0.66f + 0.24f * straw + 0.2f * bundle;
            return new Color(shade, shade * 0.97f, shade * 0.88f);
        });

        static Texture2D StoneTexture() => stoneTexture != null ? stoneTexture : stoneTexture = Texture("Dressed Stone", 256, (u, v) =>
        {
            // Dressed blocks in courses, each its own shade, the joints dark.
            var row = Mathf.FloorToInt(v * 4);
            var x = u * 2 + (row % 2) * 0.5f;
            var joint = Mathf.Min(Mathf.Abs(Mathf.Repeat(v * 4, 1) - 0.5f), Mathf.Abs(Mathf.Repeat(x, 1) - 0.5f)) > 0.47f;
            var block = TileNoise(Mathf.Floor(x) * 3.1f, row * 1.7f, 64, 81);
            var grain = TileFbm(u, v, 8, 82);
            var shade = joint ? 0.5f : 0.8f + 0.12f * block + 0.12f * grain;
            return new Color(shade, shade, shade * 1.02f);
        });

        static Texture2D RubbleTexture() => rubbleTexture != null ? rubbleTexture : rubbleTexture = Texture("Rubble Stone", 256, (u, v) =>
        {
            // Field stones laid dry: round-ish lumps of different sizes, dark gaps between.
            var best = 9f;
            var second = 9f;
            var id = 0f;

            for (var i = -1; i <= 1; i++)
            {
                for (var k = -1; k <= 1; k++)
                {
                    var cx = Mathf.Floor(u * 6) + i;
                    var cy = Mathf.Floor(v * 6) + k;
                    var px = cx + TileNoise(cx * 1.7f, cy * 2.3f, 64, 91);
                    var py = cy + TileNoise(cx * 2.9f, cy * 1.3f, 64, 92);
                    var d = Vector2.Distance(new Vector2(u * 6, v * 6), new Vector2(px, py));

                    if (d < best)
                    {
                        second = best;
                        best = d;
                        id = TileNoise(cx * 3.7f, cy * 5.1f, 64, 93);
                    }
                    else if (d < second)
                    {
                        second = d;
                    }
                }
            }

            var gap = second - best < 0.08f;
            var shade = gap ? 0.38f : 0.7f + 0.22f * id + 0.1f * TileFbm(u, v, 8, 94);
            return new Color(shade, shade * 0.98f, shade * 0.95f);
        });

        static Texture2D PlasterTexture() => plasterTexture != null ? plasterTexture : plasterTexture = Texture("Plaster", 256, (u, v) =>
        {
            // Lime over earth: soft blotches, darker streaks run down by the rain, a patch fallen off here and there.
            var blotch = TileFbm(u, v, 4, 51);
            var streak = Mathf.Pow(TileNoise(u * 24, v * 3, 24, 52), 6) * 0.3f;
            var fallen = TileFbm(u, v, 3, 53) > 0.68f ? 0.18f : 0;
            var shade = 0.86f + 0.16f * blotch - streak - fallen;
            return new Color(shade, shade * 0.98f, shade * 0.95f);
        });

        static Texture2D PlankTexture() => plankTexture != null ? plankTexture : plankTexture = Texture("Planks", 256, (u, v) =>
        {
            // Boards side by side, grain along them, each its own shade.
            var board = Mathf.Floor(u * 5);
            var edge = Mathf.Abs(Mathf.Repeat(u * 5, 1) - 0.5f) > 0.46f;
            var grain = TileNoise(u * 40, v * 3, 40, 101) * 0.18f;
            var tone = TileNoise(board * 2.3f, 1, 64, 102) * 0.2f;
            var shade = edge ? 0.5f : 0.75f + grain + tone;
            return new Color(shade, shade * 0.96f, shade * 0.92f);
        });

        static Texture2D BarkTexture() => barkTexture != null ? barkTexture : barkTexture = Texture("Bark", 256, (u, v) =>
        {
            // Deep furrows running up the trunk.
            var furrow = Mathf.Pow(TileNoise(u * 18, v * 2.5f, 18, 111), 2);
            var grain = TileFbm(u, v, 6, 112) * 0.25f;
            var shade = 0.5f + 0.4f * furrow + grain;
            return new Color(shade, shade * 0.95f, shade * 0.9f);
        });

        static Material WaterMaterial()
        {
            var water = Mat("Water", 0x22404a, 0.82f, true);
            water.SetFloat("_Surface", 1);
            water.SetFloat("_Blend", 0);
            water.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            water.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            water.SetFloat("_ZWrite", 0);
            water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            water.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            water.SetOverrideTag("RenderType", "Transparent");
            water.color = new Color(0.14f, 0.26f, 0.3f, 0.82f);

            return water;
        }
    }
}
