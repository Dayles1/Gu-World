using System.IO;
using GuWorld.World;
using UnityEditor;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// A picture of the mountain's shape from above, to tune it by eye before building
    /// (batch mode): shaded from the north-west, tinted by height, ground too steep to
    /// walk in red, streams blue, the settlements ringed.
    ///
    ///   Unity -batchmode -quit -projectPath . -executeMethod GuWorld.Editor.MountainPreview.Run -picture out.png [-area x,z,size]
    /// </summary>
    public static class MountainPreview
    {
        public static void Run()
        {
            BuildLog.Begin("mountain preview");
            var args = System.Environment.GetCommandLineArgs();
            var at = System.Array.IndexOf(args, "-picture");
            var path = at >= 0 && at + 1 < args.Length ? args[at + 1] : "mountain.png";
            var areaAt = System.Array.IndexOf(args, "-area");
            var centre = Vector2.zero;
            var span = QingMao.Size;

            if (areaAt >= 0 && areaAt + 1 < args.Length)
            {
                var v = System.Array.ConvertAll(args[areaAt + 1].Split(','), s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
                centre = new Vector2(v[0], v[1]);
                span = v[2];
            }

            const int size = 1024;
            var cell = span / size;
            var heights = new float[size + 1, size + 1];

            for (var j = 0; j <= size; j++)
            {
                for (var i = 0; i <= size; i++)
                {
                    heights[j, i] = MountainShape.Natural(centre.x - span / 2 + i * cell, centre.y - span / 2 + j * cell);
                }
            }

            var picture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var light = new Vector3(-0.5f, 0.7f, 0.5f).normalized;
            var steepCount = 0;
            var lowest = float.PositiveInfinity;
            var highest = float.NegativeInfinity;

            for (var j = 0; j < size; j++)
            {
                for (var i = 0; i < size; i++)
                {
                    var h = heights[j, i];
                    lowest = Mathf.Min(lowest, h);
                    highest = Mathf.Max(highest, h);
                    var dx = (heights[j, i + 1] - h) / cell;
                    var dz = (heights[j + 1, i] - h) / cell;
                    var normal = new Vector3(-dx, 1, -dz).normalized;
                    var shade = Mathf.Lerp(0.45f, 1.15f, Mathf.Clamp01(Vector3.Dot(normal, light)));
                    var t = Mathf.Clamp01(h / 2200);
                    var colour = Color.Lerp(new Color(0.3f, 0.42f, 0.25f), new Color(0.62f, 0.6f, 0.55f), t) * shade;
                    var slope = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;

                    if (slope > 45)
                    {
                        colour = Color.Lerp(colour, Color.red, 0.75f);
                        steepCount++;
                    }
                    else if (slope > 35)
                    {
                        colour = Color.Lerp(colour, new Color(1, 0.6f, 0.2f), 0.45f);
                    }

                    var x = centre.x - span / 2 + (i + 0.5f) * cell;
                    var z = centre.y - span / 2 + (j + 0.5f) * cell;

                    if (!float.IsNaN(MountainShape.WaterAt(new Vector2(x, z))) && MountainShape.NearStream(new Vector2(x, z), Mathf.Max(6, cell), out _, out _, out _, out _))
                    {
                        colour = new Color(0.2f, 0.4f, 0.75f);
                    }

                    // Contour lines every 100 m.
                    if (Mathf.Repeat(h, 100) < 100 * cell / 300)
                    {
                        colour *= 0.82f;
                    }

                    colour.a = 1;
                    picture.SetPixel(i, j, colour);
                }
            }

            foreach (var place in QingMao.Settlements)
            {
                Ring(picture, centre, span, place.Centre, place.Radius, place.Clan == Clan.Mortal ? Color.yellow : Color.white);
            }

            Ring(picture, centre, span, QingMao.Peak, 60, Color.magenta);
            Ring(picture, centre, span, QingMao.Pool, QingMao.PoolRadius, Color.cyan);
            picture.Apply();
            File.WriteAllBytes(path, picture.EncodeToPNG());

            foreach (var place in QingMao.Settlements)
            {
                var h = MountainShape.Natural(place.Centre.x, place.Centre.y);
                var e = MountainShape.Natural(place.Centre.x + 10, place.Centre.y) - h;
                var n = MountainShape.Natural(place.Centre.x, place.Centre.y + 10) - h;
                BuildLog.Write($"{place.Name} at {place.Centre}: {h:0} m, slope {Mathf.Atan(Mathf.Sqrt(e * e + n * n) / 10) * Mathf.Rad2Deg:0.0}°, downhill ({-e:0.00}, {-n:0.00})");
            }

            BuildLog.Write($"peak {MountainShape.Natural(QingMao.Peak.x, QingMao.Peak.y):0} m, pool {MountainShape.PoolLevel:0} m, heights {lowest:0}…{highest:0}, " +
                      $"steeper than 45°: {100f * steepCount / (size * size):0.00}% — picture {path}");
        }

        static void Ring(Texture2D picture, Vector2 centre, float span, Vector2 at, float radius, Color colour)
        {
            var size = picture.width;
            var r = Mathf.Max(3, radius / span * size);
            var c = (at - centre) / span * size + Vector2.one * size / 2;

            for (var a = 0; a < 360; a += 2)
            {
                var x = Mathf.RoundToInt(c.x + Mathf.Cos(a * Mathf.Deg2Rad) * r);
                var y = Mathf.RoundToInt(c.y + Mathf.Sin(a * Mathf.Deg2Rad) * r);

                if (x >= 0 && y >= 0 && x < size && y < size)
                {
                    picture.SetPixel(x, y, colour);
                }
            }
        }
    }
}
