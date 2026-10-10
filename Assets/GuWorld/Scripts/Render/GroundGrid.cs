using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>
    /// The ground's texture (ported from gridTexture in client/render/terrain-chunks.ts):
    /// a light square with a faint line every metre and a darker one every
    /// ten, repeated every 10 m and tinted by the ground's colour.
    /// </summary>
    public static class GroundGrid
    {
        const int Size = 256;

        public static Texture2D Make()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "Ground grid",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
            };
            var paper = Hex(0xd6d6d0);
            var line = Hex(0xb6b6ae);
            var edge = Hex(0x8e8e86);
            var pixels = new Color32[Size * Size];

            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var colour = paper;

                    for (var n = 0; n < 10; n++)
                    {
                        var at = n * Size / 10;

                        if (x == at || y == at)
                        {
                            colour = line;
                        }
                    }

                    if (x < 3 || y < 3)
                    {
                        colour = edge;
                    }

                    pixels[y * Size + x] = colour;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);

            return texture;
        }

        static Color32 Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
