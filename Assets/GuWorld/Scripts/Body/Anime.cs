using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Body
{
    /// <summary>
    /// Gives a built body GU World's dark-anime look (Shaders/Anime.shader):
    /// every opaque material of the body, hair, beard and clothes is swapped
    /// for a cel-shaded copy with an ink outline, keeping its texture, colour,
    /// tiling and culling. See-through ones (stubble) are left as they are.
    ///
    /// The copies are made once per material and role and shared by every
    /// body that wears them. If the shader can't run here, nothing changes:
    /// the bodies keep their ordinary lit materials.
    /// </summary>
    public static class Anime
    {
        /// <summary>Off: the bodies keep the plain lit look (to compare, or on very weak machines).</summary>
        public static bool Enabled = true;

        /// <summary>What a piece of the body is, for its shading and outline.</summary>
        enum Role { Skin, Eyes, Brows, Hair, Beard, Cloth }

        struct Style
        {
            public Color shade;
            public float outline;
            public float rim;
            public float softness;
        }

        static Style For(Role role)
        {
            switch (role)
            {
                // Skin: warm, rosy shade; a soft edge so faces don't look cut in two.
                case Role.Skin: return new Style { shade = new Color(0.86f, 0.64f, 0.64f), outline = 1.3f, rim = 0.3f, softness = 0.08f };
                // Eyes and brows: no outline (it would swallow them), hardly any shade.
                case Role.Eyes: return new Style { shade = new Color(0.85f, 0.85f, 0.9f), outline = 0, rim = 0, softness = 0.2f };
                case Role.Brows: return new Style { shade = new Color(0.7f, 0.68f, 0.78f), outline = 0, rim = 0, softness = 0.1f };
                // Hair: a deep, cool shade and the strongest line.
                case Role.Hair: return new Style { shade = new Color(0.5f, 0.48f, 0.66f), outline = 1.6f, rim = 0.45f, softness = 0.04f };
                case Role.Beard: return new Style { shade = new Color(0.55f, 0.52f, 0.66f), outline = 0.6f, rim = 0.2f, softness = 0.06f };
                // Clothes: cool, slightly violet shade — the dark-anime feel.
                default: return new Style { shade = new Color(0.58f, 0.56f, 0.72f), outline = 1.4f, rim = 0.3f, softness = 0.04f };
            }
        }

        static Material prototype;
        static bool looked;
        static readonly Dictionary<(Material, Role), Material> made = new Dictionary<(Material, Role), Material>();
        /// <summary>The copies drawn with an outline, and each one's twin without it (Plain).</summary>
        static readonly HashSet<Material> outlined = new HashSet<Material>();
        static readonly Dictionary<Material, Material> plainOf = new Dictionary<Material, Material>();

        /// <summary>
        /// The same anime material without its ink outline (far off the outline is under a pixel
        /// wide and still draws the whole body a second time). Any other material is given back as it is.
        /// </summary>
        public static Material Plain(Material material)
        {
            if (material == null || !outlined.Contains(material))
            {
                return material;
            }

            if (!plainOf.TryGetValue(material, out var plain) || plain == null)
            {
                plain = new Material(material) { name = material.name + " (no outline)" };
                plain.SetShaderPassEnabled("SRPDefaultUnlit", false);
                plainOf[material] = plain;
            }

            return plain;
        }

        static readonly string[] TextureNames = { "baseColorTexture", "_BaseMap", "_MainTex" };
        static readonly string[] ColourNames = { "_BaseColor", "baseColorFactor", "_Color" };
        static readonly string[] CullNames = { "_Cull", "_CullMode" };
        static readonly string[] CutoffNames = { "alphaCutoff", "_Cutoff", "_AlphaCutoff" };

        /// <summary>The material every anime copy starts from (kept in Resources so builds include the shader).</summary>
        static Material Prototype
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    prototype = Resources.Load<Material>("AnimeBody");

                    if (prototype == null)
                    {
                        var shader = Shader.Find("GuWorld/Anime");

                        if (shader != null)
                        {
                            prototype = new Material(shader) { name = "AnimeBody" };
                        }
                    }

                    if (prototype != null && !prototype.shader.isSupported)
                    {
                        Debug.LogWarning("GU World: the anime shader can't run here; the bodies keep the plain look");
                        prototype = null;
                    }
                }

                return prototype;
            }
        }

        /// <summary>Swaps every opaque material of a built body for its anime copy.</summary>
        public static void Apply(GameObject body)
        {
            if (!Enabled || body == null || Prototype == null)
            {
                return;
            }

            foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;

                for (var i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];

                    if (source == null || source.shader == Prototype.shader || source.renderQueue >= 2900)
                    {
                        // Missing, already anime, or see-through (stubble): left alone.
                        continue;
                    }

                    materials[i] = Copy(source, RoleOf(renderer, source));
                    changed = true;
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        static Role RoleOf(Renderer renderer, Material material)
        {
            var name = renderer.name;

            if (name == "Eyes") return Role.Eyes;
            if (name == "Eyebrows") return Role.Brows;
            if (name.StartsWith("Hair")) return Role.Hair;
            if (name.StartsWith("Beard")) return Role.Beard;
            if (name.StartsWith("Cloth")) return Role.Cloth;
            if (material.name.StartsWith("MI_Superhero") || name.StartsWith("Superhero") || name.StartsWith("SuperHero")) return Role.Skin;
            if (material.name.StartsWith("MI_Hair")) return Role.Hair;

            return Role.Cloth;
        }

        static Material Copy(Material source, Role role)
        {
            if (made.TryGetValue((source, role), out var done) && done != null)
            {
                return done;
            }

            var copy = new Material(Prototype) { name = source.name + " (anime)" };

            // The surface: texture with its tiling, and colour.
            foreach (var name in TextureNames)
            {
                if (source.HasProperty(name) && source.GetTexture(name) != null)
                {
                    copy.SetTexture("_BaseMap", source.GetTexture(name));
                    copy.SetTextureScale("_BaseMap", source.GetTextureScale(name));
                    copy.SetTextureOffset("_BaseMap", source.GetTextureOffset(name));

                    break;
                }
            }

            foreach (var name in ColourNames)
            {
                if (source.HasProperty(name))
                {
                    var colour = source.GetColor(name);
                    colour.a = 1;
                    copy.SetColor("_BaseColor", colour);

                    break;
                }
            }

            // Drawn from both sides unless the source said otherwise (the clothes are open shells).
            var cull = 0f;

            foreach (var name in CullNames)
            {
                if (source.HasProperty(name))
                {
                    cull = source.GetFloat(name);

                    break;
                }
            }

            copy.SetFloat("_Cull", cull);

            // Cut-out textures (alpha-tested) stay cut out.
            if (source.renderQueue >= 2450 || source.IsKeywordEnabled("_ALPHATEST_ON"))
            {
                var cutoff = 0.5f;

                foreach (var name in CutoffNames)
                {
                    if (source.HasProperty(name))
                    {
                        cutoff = source.GetFloat(name);

                        break;
                    }
                }

                copy.SetFloat("_AlphaClip", 1);
                copy.SetFloat("_Cutoff", cutoff);
                copy.EnableKeyword("_ALPHATEST_ON");
                copy.renderQueue = 2450;
            }

            var style = For(role);
            copy.SetColor("_ShadeColor", style.shade);
            copy.SetFloat("_OutlineWidth", style.outline);
            copy.SetFloat("_RimStrength", style.rim);
            copy.SetFloat("_ShadeSoftness", style.softness);
            copy.enableInstancing = true;

            // No outline (the eyes, the brows): its pass not drawn at all.
            if (style.outline > 0)
            {
                outlined.Add(copy);
            }
            else
            {
                copy.SetShaderPassEnabled("SRPDefaultUnlit", false);
            }

            made[(source, role)] = copy;

            return copy;
        }
    }
}
