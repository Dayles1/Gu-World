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
    /// A map for the HUD's minimap and the map page, drawn from the ground as it is —
    /// the terrains' heights tinted and shaded from the north-west — with the
    /// streams, the trails, the villages' streets and roofs drawn over it from the
    /// world's plan (WorldPlan). The whole mountain is one map; the minimap is a
    /// finer one round the hero, drawn again as they walk on. North is up, east right.
    /// </summary>
    public class LocalMap
    {
        public Texture2D Texture;
        /// <summary>The ground the map covers (X, Z of its south-west corner; its width and depth).</summary>
        public Rect Area;
        public readonly List<MapMark> Marks = new List<MapMark>();

        /// <summary>Where a point of the world falls on the map, 0…1 each way.</summary>
        public Vector2 ToMap(Vector3 at) => new Vector2((at.x - Area.x) / Area.width, (at.z - Area.y) / Area.height);

        static readonly Color Water = new Color(0.2f, 0.36f, 0.48f);
        static readonly Color Path = new Color(0.6f, 0.52f, 0.38f);
        static readonly Color Street = new Color(0.52f, 0.46f, 0.36f);
        static readonly Color Roof = new Color(0.24f, 0.26f, 0.3f);
        static readonly Color RoofEdge = new Color(0.1f, 0.1f, 0.12f);

        /// <summary>The terrains, finest first, and a sampler for the height and slope at a point.</summary>
        class Ground
        {
            readonly List<(Rect area, TerrainData data, float y)> terrains = new List<(Rect, TerrainData, float)>();

            public Ground()
            {
                foreach (var terrain in Terrain.activeTerrains)
                {
                    if (terrain == null)
                    {
                        continue;
                    }

                    var corner = terrain.GetPosition();
                    var size = terrain.terrainData.size;
                    terrains.Add((new Rect(corner.x, corner.z, size.x, size.z), terrain.terrainData, corner.y));
                }

                terrains.Sort((a, b) => a.area.width.CompareTo(b.area.width));
            }

            public bool Any => terrains.Count > 0;

            public bool Sample(float x, float z, out float height, out Vector3 normal)
            {
                foreach (var (area, data, y) in terrains)
                {
                    if (area.Contains(new Vector2(x, z)))
                    {
                        var u = (x - area.x) / area.width;
                        var v = (z - area.y) / area.height;
                        height = data.GetInterpolatedHeight(u, v) + y;
                        normal = data.GetInterpolatedNormal(u, v);

                        return true;
                    }
                }

                height = 0;
                normal = Vector3.up;

                return false;
            }
        }

        static Color Tint(float height, Vector3 normal, float top)
        {
            var light = new Vector3(-0.55f, 0.75f, 0.45f).normalized;
            var shade = Mathf.Lerp(0.55f, 1.12f, Mathf.Clamp01(Vector3.Dot(normal, light)));
            var h = Mathf.Clamp01(height / top);
            // Dark green forest lower down, browner and greyer towards the top, rock where it is steep.
            var ground = h < 0.4f
                ? Color.Lerp(new Color(0.2f, 0.3f, 0.18f), new Color(0.27f, 0.34f, 0.2f), h / 0.4f)
                : Color.Lerp(new Color(0.27f, 0.34f, 0.2f), new Color(0.46f, 0.45f, 0.4f), (h - 0.4f) / 0.6f);
            ground = Color.Lerp(ground, new Color(0.4f, 0.39f, 0.37f), Mathf.Clamp01((0.8f - normal.y) * 3));
            var colour = ground * shade;
            colour.a = 1;

            return colour;
        }

        static LocalMap Paint(Rect area, int size, WorldPlan plan, bool fine)
        {
            var map = new LocalMap { Area = area };
            var pixels = new Color[size * size];
            var ground = new Ground();
            var top = 2200f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var wx = area.x + (x + 0.5f) / size * area.width;
                    var wz = area.y + (y + 0.5f) / size * area.height;
                    pixels[y * size + x] = ground.Sample(wx, wz, out var h, out var n) ? Tint(h, n, top) : new Color(0.16f, 0.2f, 0.15f);
                }
            }

            if (plan != null)
            {
                var metres = area.width / size;

                foreach (var stream in plan.Streams)
                {
                    Line(pixels, size, map, stream.Points, Mathf.Max(stream.Half, metres * 0.7f), Water, 1);
                }

                foreach (var trail in plan.Trails)
                {
                    Line(pixels, size, map, trail.Points, Mathf.Max(trail.Half, metres * 0.6f), Path, fine ? 0.9f : 0.75f);
                }

                if (fine)
                {
                    foreach (var street in plan.Streets)
                    {
                        Line(pixels, size, map, street.Points, street.Half, Street, 0.9f);
                    }

                    foreach (var building in plan.Buildings)
                    {
                        Box(pixels, size, map, building);
                    }
                }
                else
                {
                    foreach (var place in plan.Settlements)
                    {
                        Disc(pixels, size, map, place.Centre, place.Radius, Roof, 0.55f);
                    }
                }

                foreach (var mark in plan.Marks)
                {
                    map.Marks.Add(new MapMark { Label = mark.Label, At = mark.At, Major = mark.Major });
                }
            }

            map.Texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = fine ? "Near Map" : "Location Map" };
            map.Texture.SetPixels(pixels);
            map.Texture.Apply(false, true);

            return map;
        }

        /// <summary>The whole location: every terrain there is, with the plan's lines and settlements.</summary>
        public static LocalMap World(WorldPlan plan, Rect area, int size = 1024) => Paint(area, size, plan, false);

        /// <summary>A finer map `span` metres across round a point: streets and roofs too (the minimap's).</summary>
        public static LocalMap Around(Vector3 centre, float span, WorldPlan plan, int size = 512) =>
            Paint(new Rect(centre.x - span / 2, centre.z - span / 2, span, span), size, plan, true);

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

        static Vector2 Pixel(LocalMap map, int size, Vector2 p) =>
            new Vector2((p.x - map.Area.x) / map.Area.width * size, (p.y - map.Area.y) / map.Area.height * size);

        /// <summary>A polyline, `half` metres to each side, blended in at `strength`.</summary>
        static void Line(Color[] pixels, int size, LocalMap map, Vector3[] points, float half, Color colour, float strength)
        {
            if (points == null)
            {
                return;
            }

            var scale = size / map.Area.width;
            var radius = half * scale;

            for (var i = 0; i + 1 < points.Length; i++)
            {
                var a = Pixel(map, size, new Vector2(points[i].x, points[i].z));
                var b = Pixel(map, size, new Vector2(points[i + 1].x, points[i + 1].z));

                if ((a.x < -radius - 2 && b.x < -radius - 2) || (a.y < -radius - 2 && b.y < -radius - 2) ||
                    (a.x > size + radius + 2 && b.x > size + radius + 2) || (a.y > size + radius + 2 && b.y > size + radius + 2))
                {
                    continue;
                }

                var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius - 1));
                var maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius + 1));
                var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius - 1));
                var maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius + 1));

                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var d = QingMao.ToSegment(new Vector2(x + 0.5f, y + 0.5f), a, b);
                        var cover = Mathf.Clamp01(Mathf.Max(radius, 0.7f) + 0.5f - d) * strength;

                        if (cover > 0)
                        {
                            pixels[y * size + x] = Color.Lerp(pixels[y * size + x], colour, cover);
                        }
                    }
                }
            }
        }

        /// <summary>A building's roof seen from above: a dark rectangle with a darker rim.</summary>
        static void Box(Color[] pixels, int size, LocalMap map, WorldPlan.Footprint footprint)
        {
            var scale = size / map.Area.width;
            var c = Pixel(map, size, footprint.Centre);
            var reach = (Mathf.Max(footprint.Half.x, footprint.Half.y) * 1.5f + 1) * scale;

            if (c.x < -reach || c.y < -reach || c.x > size + reach || c.y > size + reach)
            {
                return;
            }

            var cos = Mathf.Cos(footprint.Yaw * Mathf.Deg2Rad);
            var sin = Mathf.Sin(footprint.Yaw * Mathf.Deg2Rad);

            for (var y = Mathf.Max(0, (int)(c.y - reach)); y <= Mathf.Min(size - 1, (int)(c.y + reach)); y++)
            {
                for (var x = Mathf.Max(0, (int)(c.x - reach)); x <= Mathf.Min(size - 1, (int)(c.x + reach)); x++)
                {
                    // Into the building's own axes (yaw turns clockwise seen from above).
                    var dx = (x + 0.5f - c.x) / scale;
                    var dy = (y + 0.5f - c.y) / scale;
                    var lx = dx * cos - dy * sin;
                    var lz = dx * sin + dy * cos;
                    var ex = footprint.Half.x - Mathf.Abs(lx);
                    var ez = footprint.Half.y - Mathf.Abs(lz);

                    if (ex < 0 || ez < 0)
                    {
                        continue;
                    }

                    var rim = Mathf.Min(ex, ez) * scale < 1.2f;
                    pixels[y * size + x] = rim ? RoofEdge : Color.Lerp(Roof, footprint.Tint, 0.5f);
                }
            }
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
