using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Body
{
    /// <summary>
    /// Repaints the body's textures once (ported from client/body/paint.ts):
    /// richer colour for the skin, and every texture kept sharp when seen at a
    /// slant. The models' textures are not readable from scripts, so each one
    /// is drawn through the GPU into a copy that is; the copies are cached.
    /// </summary>
    public static class Paint
    {
        /// <summary>How much richer the skin's colours are drawn.</summary>
        public const float SkinSaturation = 1.06f;
        /// <summary>Texture filtering at a slant (the GPU's own limit caps it).</summary>
        const int Anisotropy = 16;

        static readonly Dictionary<(Texture, float), Texture2D> saturated = new Dictionary<(Texture, float), Texture2D>();

        /// <summary>Keeps a material's textures sharp when seen at a slant.</summary>
        public static void Sharpen(Material material)
        {
            foreach (var name in material.GetTexturePropertyNames())
            {
                var texture = material.GetTexture(name);

                if (texture != null && texture.anisoLevel < Anisotropy)
                {
                    texture.anisoLevel = Anisotropy;
                }
            }
        }

        /// <summary>Richer colour: saturation times `amount` (a copy, cached; the source stays as it is).</summary>
        public static Texture2D Saturate(Texture source, float amount)
        {
            if (source == null)
            {
                return null;
            }

            if (saturated.TryGetValue((source, amount), out var done) && done != null)
            {
                return done;
            }

            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Graphics.Blit(source, target);
            RenderTexture.active = target;

            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true, false)
            {
                name = source.name + " (richer)",
                wrapMode = source.wrapMode,
                filterMode = FilterMode.Trilinear,
                anisoLevel = Anisotropy,
            };
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);

            var pixels = copy.GetPixels32();

            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                var grey = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                pixels[i] = new Color32(
                    (byte)Mathf.Clamp(grey + (p.r - grey) * amount, 0, 255),
                    (byte)Mathf.Clamp(grey + (p.g - grey) * amount, 0, 255),
                    (byte)Mathf.Clamp(grey + (p.b - grey) * amount, 0, 255),
                    p.a);
            }

            copy.SetPixels32(pixels);
            copy.Apply(true, false);
            saturated[(source, amount)] = copy;

            return copy;
        }

        /// <summary>A soft woven texture for cloth: faint threads both ways, a little uneven.</summary>
        public static Texture2D Weave()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Weave",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = Anisotropy,
            };
            var pixels = new Color32[size * size];
            var random = new System.Random(7);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var warp = 0.5f + 0.5f * Mathf.Sin(x * Mathf.PI / 2);
                    var weft = 0.5f + 0.5f * Mathf.Sin(y * Mathf.PI / 2);
                    var over = (x / 2 + y / 2) % 2 == 0 ? warp : weft;
                    var value = 0.86f + 0.1f * over + 0.04f * (float)random.NextDouble();
                    var v = (byte)(255 * Mathf.Clamp01(value));
                    pixels[y * size + x] = new Color32(v, v, v, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);

            return texture;
        }
    }
}
