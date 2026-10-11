using UnityEngine;

namespace GuWorld.World
{
    /// <summary>A region of the atlas: its name and climate.</summary>
    public class AtlasRegion
    {
        public string Id;
        public string Name;
        public string Climate;
        /// <summary>How misty it is, 0…1.</summary>
        public float Mist;
        public string[] Weather;
        public string[] Nature;
        public string[] Hazards;
    }

    /// <summary>A place on the atlas, in its metres (X east, Z north).</summary>
    public class AtlasPlace
    {
        /// <summary>The atlas' object id, and the lore's record.</summary>
        public string Id;
        public string LoreRef;
        public string Name;
        public Vector2 At;
        public float Radius;
        /// <summary>How wide it is on the atlas, m.</summary>
        public float Width;
        /// <summary>Its height over the land round it, and its top above the sea, m.</summary>
        public float Rise;
        public float Top;
        public AtlasRegion Region;
        public string Rock;
        public int RankMin;
        public int RankMax;
    }

    /// <summary>
    /// GU WORLD ATLAS — the map of the world of Gu the game is laid out by
    /// (mir_gu-unity/world.json, "Мир Гу": 3 200 km square, X east, Z north,
    /// heights from the sea) — as far as Qing Mao Mountain needs it: where the
    /// mountain is in the world, the region it belongs to and that region's
    /// climate, and the world's heightmap (Resources/Atlas/world_heightmap,
    /// 513×513, 16-bit, its first row the south).
    ///
    /// The playable mountain (QingMao) is the inhabited heart of the atlas'
    /// mountain at the game's own scale: the atlas' mountain is 50 km
    /// across, the game's 1.6 km. The atlas marks its own positions for the
    /// three clans' villages as provisional (the novel gives no coordinates);
    /// the game follows the lore there — all three on the one mountain
    /// (lore: loc_qing_mao_mountain).
    /// </summary>
    public static class Atlas
    {
        public const string WorldName = "Мир Гу";
        public const float WorldSize = 3200000;
        public const float MinHeight = -3765.125f;
        public const float MaxHeight = 6765.058f;
        public const int Resolution = 513;

        /// <summary>The Southern Border (atlas region "south").</summary>
        public static readonly AtlasRegion South = new AtlasRegion
        {
            Id = "south",
            Name = "Южная граница",
            Climate = "влажный субтропический",
            Mist = 0.6f,
            Weather = new[] { "затяжные дожди", "густой туман в ущельях", "горные грозы" },
            Nature = new[] { "древние леса", "туманные ущелья", "бамбуковые рощи", "пещеры" },
            Hazards = new[] { "звериные волны", "ядовитые туманы", "войны кланов" },
        };

        /// <summary>Qing Mao Mountain (atlas: mountain_7xneb010mq8).</summary>
        public static readonly AtlasPlace QingMao = new AtlasPlace
        {
            Id = "mountain_7xneb010mq8",
            LoreRef = "loc_qing_mao_mountain",
            Name = "Гора Цинмао",
            At = new Vector2(-585770, -785110),
            Radius = 22000,
            Width = 50000,
            Rise = 3200,
            Top = 4686,
            Region = South,
            Rock = "гранит",
            RankMin = 1,
            RankMax = 2,
        };

        /// <summary>Where a point of the atlas falls on its map, 0…1 each way (north up).</summary>
        public static Vector2 ToMap(Vector2 at) => new Vector2(at.x / WorldSize + 0.5f, at.y / WorldSize + 0.5f);

        static Texture2D worldMap;

        /// <summary>
        /// The world's map: the heightmap coloured — sea by depth, land from green lowlands
        /// through brown uplands to bare grey heights and snow — and shaded from the
        /// north-west. Null if the heightmap is missing.
        /// </summary>
        public static Texture2D WorldMap()
        {
            if (worldMap != null)
            {
                return worldMap;
            }

            var raw = Resources.Load<TextAsset>("Atlas/world_heightmap");

            if (raw == null || raw.bytes.Length < Resolution * Resolution * 2)
            {
                return null;
            }

            var bytes = raw.bytes;
            var n = Resolution;
            var heights = new float[n * n];

            for (var i = 0; i < n * n; i++)
            {
                heights[i] = Mathf.Lerp(MinHeight, MaxHeight, (bytes[i * 2] | (bytes[i * 2 + 1] << 8)) / 65535f);
            }

            var pixels = new Color32[n * n];
            var metresPerPixel = WorldSize / (n - 1);

            for (var y = 0; y < n; y++)
            {
                for (var x = 0; x < n; x++)
                {
                    var h = heights[y * n + x];
                    var dx = heights[y * n + Mathf.Min(n - 1, x + 1)] - heights[y * n + Mathf.Max(0, x - 1)];
                    var dz = heights[Mathf.Min(n - 1, y + 1) * n + x] - heights[Mathf.Max(0, y - 1) * n + x];
                    // Light from the north-west, the slopes exaggerated (the world is wide and flat at this scale).
                    var shade = Mathf.Clamp(1 + (-dx + dz) / (metresPerPixel * 2) * 9, 0.55f, 1.35f);
                    var colour = Tint(h) * shade;
                    colour.a = 1;
                    pixels[y * n + x] = colour;
                }
            }

            worldMap = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Atlas World Map" };
            worldMap.SetPixels32(pixels);
            worldMap.Apply(false, true);

            return worldMap;
        }

        static Color Tint(float h)
        {
            if (h < 0)
            {
                return Color.Lerp(new Color(0.16f, 0.27f, 0.36f), new Color(0.05f, 0.09f, 0.16f), Mathf.Clamp01(-h / 3000));
            }

            if (h < 600) return Color.Lerp(new Color(0.3f, 0.42f, 0.27f), new Color(0.38f, 0.45f, 0.29f), h / 600);
            if (h < 2000) return Color.Lerp(new Color(0.38f, 0.45f, 0.29f), new Color(0.5f, 0.44f, 0.32f), (h - 600) / 1400);
            if (h < 4000) return Color.Lerp(new Color(0.5f, 0.44f, 0.32f), new Color(0.6f, 0.58f, 0.55f), (h - 2000) / 2000);

            return Color.Lerp(new Color(0.6f, 0.58f, 0.55f), new Color(0.92f, 0.93f, 0.95f), Mathf.Clamp01((h - 4000) / 1500));
        }
    }
}
