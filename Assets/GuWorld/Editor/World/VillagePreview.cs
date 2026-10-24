using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GuWorld.World;
using UnityEditor;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// Gu Yue's plan drawn from above without building the mountain (about a minute): the ground's kinds and heights,
    /// every building, model and thing, the streets, gates and places — and in red wherever two of them overlap — with a
    /// report beside it (counts, the overlapping pairs, the places). For laying the village out:
    /// <c>-executeMethod GuWorld.Editor.VillagePreview.Run -out plan.png</c> (the report: plan.png.txt).
    /// </summary>
    public static class VillagePreview
    {
        const float PixelsPerMetre = 2;
        const float Margin = 110;

        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, "-out");
            var path = i >= 0 && i + 1 < args.Length ? args[i + 1] : "Logs/guyue-plan.png";
            BuildLog.Begin("Gu Yue's plan drawn");
            MountainShape.Prepare();
            var place = QingMao.Find("gu_yue");
            var plan = new VillagePlan(place, MountainShape.Natural, place.TileSize > 1000 ? 2049 : 1025, QingMaoBuilder.Seed(place.Id));
            Draw(plan, path);

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        /// <summary>A turned rectangle: its middle, half sizes (across its front, deep), its turn (degrees).</summary>
        readonly struct Box
        {
            public readonly string Name;
            public readonly Vector2 Centre;
            public readonly Vector2 Half;
            public readonly float Yaw;

            public Box(string name, Vector2 centre, Vector2 half, float yaw)
            {
                Name = name;
                Centre = centre;
                Half = half;
                Yaw = yaw;
            }

            public Vector2 AxisX => new Vector2(Mathf.Cos(Yaw * Mathf.Deg2Rad), -Mathf.Sin(Yaw * Mathf.Deg2Rad));
            public Vector2 AxisZ => new Vector2(Mathf.Sin(Yaw * Mathf.Deg2Rad), Mathf.Cos(Yaw * Mathf.Deg2Rad));

            public bool Contains(Vector2 p)
            {
                var d = p - Centre;

                return Mathf.Abs(Vector2.Dot(d, AxisX)) <= Half.x && Mathf.Abs(Vector2.Dot(d, AxisZ)) <= Half.y;
            }

            float Reach(Vector2 axis) => Mathf.Abs(Vector2.Dot(AxisX, axis)) * Half.x + Mathf.Abs(Vector2.Dot(AxisZ, axis)) * Half.y;

            /// <summary>How deep two boxes overlap (separating axes), 0 when apart.</summary>
            public float Overlap(Box other)
            {
                var least = float.PositiveInfinity;

                foreach (var axis in new[] { AxisX, AxisZ, other.AxisX, other.AxisZ })
                {
                    var gap = Mathf.Abs(Vector2.Dot(other.Centre - Centre, axis)) - Reach(axis) - other.Reach(axis);

                    if (gap >= 0)
                    {
                        return 0;
                    }

                    least = Mathf.Min(least, -gap);
                }

                return least;
            }
        }

        static void Draw(VillagePlan plan, string path)
        {
            var local = plan.Boundary.Select(plan.Local).ToList();
            var minA = local.Min(p => p.x) - Margin;
            var maxA = local.Max(p => p.x) + Margin;
            var minU = local.Min(p => p.y) - Margin - 20;
            var maxU = local.Max(p => p.y) + Margin * 0.6f;
            var width = Mathf.CeilToInt((maxA - minA) * PixelsPerMetre);
            var height = Mathf.CeilToInt((maxU - minU) * PixelsPerMetre);
            var pixels = new Color32[width * height];
            var level = plan.Level0;

            Vector2 WorldOf(int x, int y) => plan.World(minA + (x + 0.5f) / PixelsPerMetre, minU + (y + 0.5f) / PixelsPerMetre);

            // The ground: what it is inside, how high it lies outside (warm above the level, cool below, a line every 2 m).
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var p = WorldOf(x, y);
                    var kind = plan.KindAt(p);
                    Color c;

                    switch (kind)
                    {
                        case VillagePlan.KLane: c = new Color(0.66f, 0.56f, 0.42f); break;
                        case VillagePlan.KPaved: c = new Color(0.72f, 0.72f, 0.7f); break;
                        case VillagePlan.KYard: c = new Color(0.78f, 0.7f, 0.5f); break;
                        case VillagePlan.KGarden: c = new Color(0.4f, 0.3f, 0.2f); break;
                        case VillagePlan.KFloor: c = new Color(0.3f, 0.3f, 0.3f); break;
                        case VillagePlan.KGround: c = new Color(0.55f, 0.66f, 0.42f); break;
                        default:
                        {
                            var h = plan.GroundAt(p) - level;
                            var t = Mathf.Clamp01(Mathf.Abs(h) / 30);
                            c = h > 0 ? Color.Lerp(new Color(0.45f, 0.58f, 0.38f), new Color(0.75f, 0.55f, 0.3f), t) : Color.Lerp(new Color(0.45f, 0.58f, 0.38f), new Color(0.3f, 0.4f, 0.65f), t);

                            if (Mathf.Abs(h) > 0.3f && Mathf.Repeat(h, 2) < 0.25f)
                            {
                                c *= 0.75f;
                            }

                            break;
                        }
                    }

                    pixels[y * width + x] = c;
                }
            }

            var boxes = new List<(Box box, Color colour, bool solid)>();

            foreach (var b in plan.Buildings)
            {
                var colour = b.Kind switch
                {
                    BuildingKind.Family => new Color(0.55f, 0.2f, 0.15f),
                    BuildingKind.Poor => new Color(0.5f, 0.42f, 0.3f),
                    BuildingKind.Shop => new Color(0.85f, 0.55f, 0.1f),
                    _ => new Color(0.25f, 0.2f, 0.6f),
                };
                boxes.Add((new Box(b.Name ?? b.Model ?? b.Kind.ToString(), b.Centre, b.Size / 2, b.Yaw), colour, true));
            }

            foreach (var (model, at, yaw, scale, _) in plan.ModelProps)
            {
                boxes.Add((new Box(model, at, QingMaoBuilder.VillageGlb[model].Size * scale / 2, yaw), new Color(0.95f, 0.75f, 0.2f), true));
            }

            foreach (var (centre, half, yaw) in plan.Gardens)
            {
                boxes.Add((new Box("garden", centre, half, yaw), new Color(0.25f, 0.45f, 0.15f), true));
            }

            var cover = new byte[width * height];

            foreach (var (box, colour, _) in boxes)
            {
                Fill(box, colour, true);
            }

            void Fill(Box box, Color colour, bool count)
            {
                var r = box.Half.magnitude;
                var c = plan.Local(box.Centre);
                var x0 = Mathf.Max(0, Mathf.FloorToInt((c.x - r - minA) * PixelsPerMetre));
                var x1 = Mathf.Min(width - 1, Mathf.CeilToInt((c.x + r - minA) * PixelsPerMetre));
                var y0 = Mathf.Max(0, Mathf.FloorToInt((c.y - r - minU) * PixelsPerMetre));
                var y1 = Mathf.Min(height - 1, Mathf.CeilToInt((c.y + r - minU) * PixelsPerMetre));

                for (var y = y0; y <= y1; y++)
                {
                    for (var x = x0; x <= x1; x++)
                    {
                        var p = WorldOf(x, y);

                        if (!box.Contains(p))
                        {
                            continue;
                        }

                        var index = y * width + x;
                        var inner = new Box(box.Name, box.Centre, box.Half - Vector2.one * (0.8f / PixelsPerMetre), box.Yaw).Contains(p);
                        pixels[index] = inner ? colour : Color.black;

                        if (count && ++cover[index] > 1)
                        {
                            pixels[index] = Color.red;
                        }
                    }
                }
            }

            // The things of the procedural kit: a dot each (the leaves left out).
            foreach (var prop in plan.Props.Where(p => p.Kind != "leaves"))
            {
                Dot(new Vector2(prop.At.x, prop.At.z), 1, new Color(0.45f, 0.1f, 0.35f));
            }

            foreach (var gate in plan.Gates)
            {
                Dot(gate.At, 4, Color.red);
                Dot(gate.At + gate.Out * gate.Reach, 3, Color.magenta);
            }

            foreach (var (_, at, _) in plan.Places)
            {
                Dot(at, 1, Color.cyan);
            }

            foreach (var (at, _) in plan.Doorsteps)
            {
                Dot(at, 0, Color.white);
            }

            // The wall's line.
            for (var k = 0; k < plan.Boundary.Count; k++)
            {
                var a = plan.Boundary[k];
                var b = plan.Boundary[(k + 1) % plan.Boundary.Count];

                for (var s = 0f; s <= 1; s += 0.2f / Vector2.Distance(a, b))
                {
                    Dot(Vector2.Lerp(a, b, s), 1, Color.black);
                }
            }

            void Dot(Vector2 at, int radius, Color colour)
            {
                var c = plan.Local(at);
                var cx = Mathf.RoundToInt((c.x - minA) * PixelsPerMetre);
                var cy = Mathf.RoundToInt((c.y - minU) * PixelsPerMetre);

                for (var y = cy - radius; y <= cy + radius; y++)
                {
                    for (var x = cx - radius; x <= cx + radius; x++)
                    {
                        if (x >= 0 && y >= 0 && x < width && y < height)
                        {
                            pixels[y * width + x] = colour;
                        }
                    }
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());

            // The report: what stands there, what overlaps what, where the places are.
            var text = new StringBuilder();
            var c3 = CultureInfo.InvariantCulture;
            text.AppendLine(string.Format(c3, "level {0:0.0} m; {1} buildings, {2} models, {3} things, {4} gardens, {5} streets, {6} doorsteps", level, plan.Buildings.Count, plan.ModelProps.Count, plan.Props.Count, plan.Gardens.Count, plan.Streets.Count, plan.Doorsteps.Count));

            foreach (var group in plan.Buildings.GroupBy(b => b.Model ?? b.Kind.ToString()).OrderByDescending(g => g.Count()))
            {
                text.AppendLine($"  {group.Key}: {group.Count()}");
            }

            foreach (var group in plan.ModelProps.GroupBy(m => m.model).OrderByDescending(g => g.Count()))
            {
                text.AppendLine($"  (model) {group.Key}: {group.Count()}");
            }

            foreach (var group in plan.Props.GroupBy(p => p.Kind).OrderByDescending(g => g.Count()))
            {
                text.AppendLine($"  (thing) {group.Key}: {group.Count()}");
            }

            var overlaps = 0;

            for (var a = 0; a < boxes.Count; a++)
            {
                for (var b = a + 1; b < boxes.Count; b++)
                {
                    if ((boxes[a].box.Centre - boxes[b].box.Centre).sqrMagnitude > Mathf.Pow(boxes[a].box.Half.magnitude + boxes[b].box.Half.magnitude, 2))
                    {
                        continue;
                    }

                    var depth = boxes[a].box.Overlap(boxes[b].box);

                    if (depth > 0.05f)
                    {
                        overlaps++;
                        var at = plan.Local(boxes[a].box.Centre);
                        text.AppendLine(string.Format(c3, "OVERLAP {0:0.00} m: {1} and {2} at ({3:0}, {4:0})", depth, boxes[a].box.Name, boxes[b].box.Name, at.x, at.y));
                    }
                }
            }

            text.AppendLine($"{overlaps} overlaps");

            foreach (var gate in plan.Gates)
            {
                var at = plan.Local(gate.At);
                text.AppendLine(string.Format(c3, "{0} at ({1:0}, {2:0}), its road reaches {3:0} m out", gate.Name, at.x, at.y, gate.Reach));
            }

            foreach (var (name, at, _) in plan.Places.Where(p => !p.name.StartsWith("house")))
            {
                var l = plan.Local(at);
                text.AppendLine(string.Format(c3, "place {0} at ({1:0}, {2:0})", name, l.x, l.y));
            }

            foreach (var b in plan.Buildings.Where(b => !string.IsNullOrEmpty(b.Name)))
            {
                var l = plan.Local(b.Centre);
                text.AppendLine(string.Format(c3, "named {0} ({1} x{2:0.00}) at ({3:0}, {4:0})", b.Name, b.Model, b.Scale, l.x, l.y));
            }

            File.WriteAllText(path + ".txt", text.ToString());
            BuildLog.Write($"Gu Yue's plan drawn: {path} ({width}x{height}), {overlaps} overlaps");
        }
    }
}
