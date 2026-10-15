using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Body
{
    /// <summary>
    /// Draws the people as the art direction asks (Reference/Design, "Hero 2.0"):
    /// cel-shaded with a dark outline, GuWorld/Toon. Every opaque material of a
    /// body becomes a toon copy of itself — its texture, tiling, colour and
    /// sides — made once per material; see-through ones (stubble) stay as they
    /// are. The eyes and brows, thin two-sided cards, get no outline.
    /// </summary>
    public static class Toon
    {
        static Shader shader;
        static readonly Dictionary<(Material, bool), Material> made = new Dictionary<(Material, bool), Material>();
        /// <summary>Each outlined toon material's source, for its copy without the outline (Plain).</summary>
        static readonly Dictionary<Material, Material> sourceOf = new Dictionary<Material, Material>();

        /// <summary>
        /// The same toon material without its outline (the outline is a second pass over the whole body: from
        /// afar it is a pixel wide and costs as much as the body). Any other material is given back as it is.
        /// </summary>
        public static Material Plain(Material toon) => toon != null && sourceOf.TryGetValue(toon, out var source) ? From(source, false) : toon;

        public static Shader Shader => shader != null ? shader : shader = Shader.Find("GuWorld/Toon");

        /// <summary>The skin's shadow tint.</summary>
        static readonly Color SkinShade = new Color(0.84f, 0.7f, 0.7f);

        static bool Outlined(string part) => part != "Eyes" && part != "Eyebrows";

        /// <summary>Turns every renderer under a body to toon materials.</summary>
        public static void Apply(GameObject body)
        {
            if (Shader == null)
            {
                return;
            }

            foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var outlined = Outlined(renderer.name);

                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = From(materials[i], outlined);
                }

                renderer.sharedMaterials = materials;
            }
        }

        /// <summary>The toon copy of a material (the material itself when it is see-through).</summary>
        public static Material From(Material source, bool outlined)
        {
            if (source == null || source.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent || Shader == null)
            {
                return source;
            }

            if (made.TryGetValue((source, outlined), out var done) && done != null)
            {
                return done;
            }

            var toon = new Material(Shader) { name = source.name + " (toon)" };

            foreach (var name in new[] { "_BaseMap", "baseColorTexture", "_MainTex" })
            {
                if (source.HasProperty(name) && source.GetTexture(name) != null)
                {
                    toon.SetTexture("_BaseMap", source.GetTexture(name));
                    toon.SetTextureScale("_BaseMap", source.GetTextureScale(name));
                    toon.SetTextureOffset("_BaseMap", source.GetTextureOffset(name));

                    break;
                }
            }

            foreach (var name in new[] { "_BaseColor", "baseColorFactor", "_Color" })
            {
                if (source.HasProperty(name))
                {
                    toon.SetColor("_BaseColor", source.GetColor(name));

                    break;
                }
            }

            // URP Lit says its sides as _Cull; the models' glTF materials are all two-sided.
            toon.SetFloat("_Cull", source.HasProperty("_Cull") ? source.GetFloat("_Cull") : 0);

            if (source.IsKeywordEnabled("_ALPHATEST_ON"))
            {
                toon.EnableKeyword("_ALPHATEST_ON");
                toon.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff")
                    : source.HasProperty("alphaCutoff") ? source.GetFloat("alphaCutoff") : 0.5f);
            }

            if (!outlined)
            {
                toon.SetShaderPassEnabled("SRPDefaultUnlit", false);
            }
            else
            {
                sourceOf[toon] = source;
            }

            // The shadows are cold, but for the skin's: warm, rosy (the art direction's).
            if (source.name.StartsWith("MI_Superhero"))
            {
                toon.SetColor("_ShadeColor", SkinShade);
            }

            made[(source, outlined)] = toon;

            return toon;
        }
    }
}
