using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.World
{
    /// <summary>A named spot on the map (a gate, the spring, a village).</summary>
    public struct MapMark
    {
        public string Label;
        public Vector3 At;
        /// <summary>Big marks (villages, the peak) are labelled on the minimap too.</summary>
        public bool Major;
    }

    /// <summary>
    /// The location's map, for the HUD's minimap and the map page: drawn once
    /// from the ground as it is — the terrain's heights tinted and shaded from
    /// the north-west — with the river, the paths and the walls drawn over it
    /// where the location has them (Qing Mao). North is up, east right.
    /// </summary>
    public class LocalMap
    {
        public Texture2D Texture;
        /// <summary>The ground the map covers (X, Z of its south-west corner; its width and depth).</summary>
        public Rect Area;
        public readonly List<MapMark> Marks = new List<MapMark>();

        /// <summary>Where a point of the world falls on the map, 0…1 each way.</summary>
        public Vector2 ToMap(Vector3 at) => new Vector2((at.x - Area.x) / Area.width, (at.z - Area.y) / Area.height);

        static readonly Color Water = new Color(0.24f, 0.42f, 0.52f);
        static readonly Color Path = new Color(0.66f, 0.58f, 0.43f);
        static readonly Color Wall = new Color(0.27f, 0.2f, 0.15f);
        static readonly Color Roofs = new Color(0.33f, 0.31f, 0.27f);

        /// <summary>The map of a terrain (and of Qing Mao's features, if it is that location).</summary>
        public static LocalMap Draw(Terrain terrain, bool qingMao, int size = 512)
        {
            var data = terrain.terrainData;
            var origin = terrain.GetPosition();
            var map = new LocalMap { Area = new Rect(origin.x, origin.z, data.size.x, data.size.z) };
            var pixels = new Color[size * size];
            var light = new Vector3(-0.55f, 0.75f, 0.45f).normalized;
            var top = Mathf.Max(1, data.size.y);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size;
                    var v = (y + 0.5f) / size;
                    var h = data.GetInterpolatedHeight(u, v) / top;
                    var normal = data.GetInterpolatedNormal(u, v);
                    var shade = Mathf.Lerp(0.62f, 1.12f, Mathf.Clamp01(Vector3.Dot(normal, light)));
                    // Green lower down, browner and greyer towards the top.
                    var ground = h < 0.45f
                        ? Color.Lerp(new Color(0.27f, 0.39f, 0.23f), new Color(0.35f, 0.42f, 0.26f), h / 0.45f)
                        : Color.Lerp(new Color(0.35f, 0.42f, 0.26f), new Color(0.5f, 0.48f, 0.41f), (h - 0.45f) / 0.55f);
                    var colour = ground * shade;
                    colour.a = 1;
                    pixels[y * size + x] = colour;
                }
            }

            if (qingMao)
            {
                var metres = map.Area.width / size;

                foreach (var path in QingMao.Paths)
                {
                    Line(pixels, size, map, path, QingMao.PathHalfWidth + 0.8f, Path, 0.85f);
                }

                Line(pixels, size, map, QingMao.River, QingMao.RiverHalfWidth + 1, Water, 1);
                Ring(pixels, size, map, QingMao.Village, QingMao.VillageRadius, Mathf.Max(1.6f, metres * 0.8f), Wall);
                Disc(pixels, size, map, QingMao.Village, QingMao.VillageRadius - 4, Roofs, 0.35f);
                Disc(pixels, size, map, QingMao.Xiong, QingMao.SmallVillageRadius, Roofs, 0.45f);
                Disc(pixels, size, map, QingMao.Bai, QingMao.SmallVillageRadius, Roofs, 0.45f);
                Disc(pixels, size, map, QingMao.Grove, QingMao.GroveRadius, new Color(0.2f, 0.36f, 0.18f), 0.35f);

                map.Marks.Add(new MapMark { Label = "Деревня Гу Юэ", At = At(QingMao.Village), Major = true });
                map.Marks.Add(new MapMark { Label = "Деревня Сюн", At = At(QingMao.Xiong), Major = true });
                map.Marks.Add(new MapMark { Label = "Деревня Бай", At = At(QingMao.Bai), Major = true });
                map.Marks.Add(new MapMark { Label = "Вершина", At = At(QingMao.Peak), Major = true });
                map.Marks.Add(new MapMark { Label = "Источник", At = At(QingMao.Spring) });
                map.Marks.Add(new MapMark { Label = "Бамбуковая роща", At = At(QingMao.Grove) });
            }

            map.Texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Location Map" };
            map.Texture.SetPixels(pixels);
            map.Texture.Apply(false, true);

            return map;
        }

        /// <summary>A plain map of flat ground with a grid every 16 m (the proving ground).</summary>
        public static LocalMap Grid(Bounds bounds, int size = 256)
        {
            var map = new LocalMap { Area = new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z) };
            var pixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var wx = map.Area.x + (x + 0.5f) / size * map.Area.width;
                    var wz = map.Area.y + (y + 0.5f) / size * map.Area.height;
                    var line = Mathf.Abs(Mathf.Repeat(wx + 8, 16) - 8) < 0.9f || Mathf.Abs(Mathf.Repeat(wz + 8, 16) - 8) < 0.9f;
                    pixels[y * size + x] = line ? new Color(0.42f, 0.44f, 0.4f) : new Color(0.33f, 0.35f, 0.31f);
                }
            }

            map.Texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Location Map" };
            map.Texture.SetPixels(pixels);
            map.Texture.Apply(false, true);

            return map;
        }

        static Vector3 At(Vector2 p) => new Vector3(p.x, 0, p.y);

        static Vector2 Pixel(LocalMap map, int size, Vector2 p) =>
            new Vector2((p.x - map.Area.x) / map.Area.width * size, (p.y - map.Area.y) / map.Area.height * size);

        /// <summary>A polyline, `half` metres to each side, blended in at `strength`.</summary>
        static void Line(Color[] pixels, int size, LocalMap map, Vector2[] points, float half, Color colour, float strength)
        {
            var scale = size / map.Area.width;
            var radius = half * scale;

            for (var i = 0; i + 1 < points.Length; i++)
            {
                var a = Pixel(map, size, points[i]);
                var b = Pixel(map, size, points[i + 1]);
                var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius - 1));
                var maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius + 1));
                var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius - 1));
                var maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius + 1));

                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var d = QingMao.ToSegment(new Vector2(x + 0.5f, y + 0.5f), a, b);
                        // At least a pixel wide, edges softened.
                        var cover = Mathf.Clamp01(Mathf.Max(radius, 0.7f) + 0.5f - d) * strength;

                        if (cover > 0)
                        {
                            pixels[y * size + x] = Color.Lerp(pixels[y * size + x], colour, cover);
                        }
                    }
                }
            }
        }

        static void Ring(Color[] pixels, int size, LocalMap map, Vector2 centre, float radius, float half, Color colour)
        {
            var points = new Vector2[49];

            for (var i = 0; i < points.Length; i++)
            {
                var a = i / 48f * Mathf.PI * 2;
                points[i] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }

            Line(pixels, size, map, points, half, colour, 1);
        }

        static void Disc(Color[] pixels, int size, LocalMap map, Vector2 centre, float radius, Color colour, float strength)
        {
            var c = Pixel(map, size, centre);
            var r = radius * size / map.Area.width;

            for (var y = Mathf.Max(0, (int)(c.y - r - 1)); y <= Mathf.Min(size - 1, (int)(c.y + r + 1)); y++)
            {
                for (var x = Mathf.Max(0, (int)(c.x - r - 1)); x <= Mathf.Min(size - 1, (int)(c.x + r + 1)); x++)
                {
                    var cover = Mathf.Clamp01(r - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c)) * strength;

                    if (cover > 0)
                    {
                        pixels[y * size + x] = Color.Lerp(pixels[y * size + x], colour, cover);
                    }
                }
            }
        }
    }
}
