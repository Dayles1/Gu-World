using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// Builds low-poly meshes out of simple parts — boxes, cylinders, cones,
    /// rough spheres and curved-eave roofs — each part with its material,
    /// into one mesh with a submesh per material. Faces are flat-shaded and
    /// always wound to face outward (Unity's front face: clockwise seen from
    /// the front, its normal (b − a) × (c − a)).
    /// </summary>
    public class MeshKit
    {
        class Part
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uvs = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();
        }

        readonly List<Material> order = new List<Material>();
        readonly Dictionary<Material, Part> parts = new Dictionary<Material, Part>();

        /// <summary>Where the next parts go (position, turn and scale applied to what is added).</summary>
        public Matrix4x4 Transform = Matrix4x4.identity;

        Part For(Material material)
        {
            if (!parts.TryGetValue(material, out var part))
            {
                part = new Part();
                parts[material] = part;
                order.Add(material);
            }

            return part;
        }

        /// <summary>A triangle facing `outward` (in local space, before Transform).</summary>
        public void Triangle(Material material, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0)
            {
                (b, c) = (c, b);
            }

            a = Transform.MultiplyPoint3x4(a);
            b = Transform.MultiplyPoint3x4(b);
            c = Transform.MultiplyPoint3x4(c);
            var normal = Vector3.Cross(b - a, c - a).normalized;

            if (normal == Vector3.zero)
            {
                return;
            }

            var part = For(material);
            var start = part.vertices.Count;
            part.vertices.Add(a);
            part.vertices.Add(b);
            part.vertices.Add(c);

            for (var i = 0; i < 3; i++)
            {
                part.normals.Add(normal);
            }

            // UVs projected along the face's main axis, a metre a unit: textures tile evenly.
            part.uvs.Add(Project(a, normal));
            part.uvs.Add(Project(b, normal));
            part.uvs.Add(Project(c, normal));
            part.triangles.Add(start);
            part.triangles.Add(start + 1);
            part.triangles.Add(start + 2);
        }

        static Vector2 Project(Vector3 p, Vector3 n)
        {
            var x = Mathf.Abs(n.x);
            var y = Mathf.Abs(n.y);
            var z = Mathf.Abs(n.z);

            return y >= x && y >= z ? new Vector2(p.x, p.z) : x >= z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
        }

        public void Quad(Material material, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            Triangle(material, a, b, c, outward);
            Triangle(material, a, c, d, outward);
        }

        /// <summary>Nothing has been added yet.</summary>
        public bool IsEmpty => order.Count == 0;

        /// <summary>A triangle with its own texture coordinates, facing `outward` (up when not given).</summary>
        public void TriangleUV(Material material, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3? outward = null)
        {
            var facing = outward ?? Vector3.up;

            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0)
            {
                (b, c) = (c, b);
                (ub, uc) = (uc, ub);
            }

            a = Transform.MultiplyPoint3x4(a);
            b = Transform.MultiplyPoint3x4(b);
            c = Transform.MultiplyPoint3x4(c);
            var normal = Vector3.Cross(b - a, c - a).normalized;

            if (normal == Vector3.zero)
            {
                return;
            }

            var part = For(material);
            var start = part.vertices.Count;
            part.vertices.Add(a);
            part.vertices.Add(b);
            part.vertices.Add(c);
            part.normals.Add(normal);
            part.normals.Add(normal);
            part.normals.Add(normal);
            part.uvs.Add(ua);
            part.uvs.Add(ub);
            part.uvs.Add(uc);
            part.triangles.Add(start);
            part.triangles.Add(start + 1);
            part.triangles.Add(start + 2);
        }

        /// <summary>A quad (corners in order round it) with its own texture coordinates, facing `outward` (up when not given).</summary>
        public void QuadUV(Material material, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3? outward = null)
        {
            TriangleUV(material, a, b, c, ua, ub, uc, outward);
            TriangleUV(material, a, c, d, ua, uc, ud, outward);
        }

        /// <summary>Everything another kit holds, added to this one (moved by this kit's Transform).</summary>
        public void Append(MeshKit other)
        {
            foreach (var material in other.order)
            {
                var from = other.parts[material];
                var part = For(material);
                var start = part.vertices.Count;

                for (var i = 0; i < from.vertices.Count; i++)
                {
                    part.vertices.Add(Transform.MultiplyPoint3x4(from.vertices[i]));
                    part.normals.Add(Transform.MultiplyVector(from.normals[i]).normalized);
                    part.uvs.Add(from.uvs[i]);
                }

                foreach (var t in from.triangles)
                {
                    part.triangles.Add(start + t);
                }
            }
        }

        /// <summary>How many triangles it holds.</summary>
        public int TriangleCount
        {
            get
            {
                var count = 0;

                foreach (var part in parts.Values)
                {
                    count += part.triangles.Count / 3;
                }

                return count;
            }
        }

        /// <summary>Moves every point already added by `bend` (to make a building crooked: a lean, a sagging ridge).</summary>
        public void Deform(System.Func<Vector3, Vector3> bend)
        {
            foreach (var part in parts.Values)
            {
                for (var i = 0; i < part.vertices.Count; i++)
                {
                    part.vertices[i] = bend(part.vertices[i]);
                }

                // The faces' normals again, from their moved corners.
                for (var t = 0; t + 2 < part.triangles.Count; t += 3)
                {
                    var a = part.vertices[part.triangles[t]];
                    var b = part.vertices[part.triangles[t + 1]];
                    var c = part.vertices[part.triangles[t + 2]];
                    var n = Vector3.Cross(b - a, c - a).normalized;

                    if (n != Vector3.zero)
                    {
                        part.normals[part.triangles[t]] = n;
                        part.normals[part.triangles[t + 1]] = n;
                        part.normals[part.triangles[t + 2]] = n;
                    }
                }
            }
        }

        /// <summary>A box: centre, size and a turn about Y (degrees).</summary>
        public void Box(Material material, Vector3 centre, Vector3 size, float yaw = 0)
        {
            var turn = Quaternion.Euler(0, yaw, 0);
            var h = size / 2;
            Vector3 P(float x, float y, float z) => centre + turn * new Vector3(x * h.x, y * h.y, z * h.z);

            Quad(material, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), turn * Vector3.forward);
            Quad(material, P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), turn * Vector3.back);
            Quad(material, P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), turn * Vector3.right);
            Quad(material, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), turn * Vector3.left);
            Quad(material, P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), Vector3.up);
            Quad(material, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), Vector3.down);
        }

        /// <summary>A cylinder from `a` to `b` (radius at each end), with `sides` faces; capped.</summary>
        public void Cylinder(Material material, Vector3 a, Vector3 b, float radiusA, float radiusB, int sides, bool caps = true)
        {
            var axis = (b - a).normalized;
            var side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            var other = Vector3.Cross(axis, side);
            Vector3 Ring(Vector3 centre, float radius, int i)
            {
                var angle = 2 * Mathf.PI * i / sides;

                return centre + (side * Mathf.Cos(angle) + other * Mathf.Sin(angle)) * radius;
            }

            for (var i = 0; i < sides; i++)
            {
                var a0 = Ring(a, radiusA, i);
                var a1 = Ring(a, radiusA, i + 1);
                var b0 = Ring(b, radiusB, i);
                var b1 = Ring(b, radiusB, i + 1);
                var outward = (a0 + a1 + b0 + b1) / 4 - (a + b) / 2;
                outward -= axis * Vector3.Dot(outward, axis);

                if (radiusB <= 0.0001f)
                {
                    Triangle(material, a0, a1, b, outward);
                }
                else
                {
                    Quad(material, a0, a1, b1, b0, outward);
                }

                if (caps)
                {
                    Triangle(material, a, a0, a1, -axis);

                    if (radiusB > 0.0001f)
                    {
                        Triangle(material, b, b0, b1, axis);
                    }
                }
            }
        }

        /// <summary>A rough sphere (an icosahedron, once subdivided), squashed by `scale`.</summary>
        public void Blob(Material material, Vector3 centre, Vector3 scale, int seed = 0)
        {
            // Small ones need no roundness: 20 faces, not 80 (thousands of them lie about the villages).
            var small = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) < 0.45f;

            foreach (var (a, b, c) in small ? Icosahedron() : Icosphere())
            {
                // Each corner nudged its own way, from the seed, so blobs are not all alike.
                Vector3 Bump(Vector3 p) => p * (1 + 0.12f * Mathf.Sin(seed * 12.9898f + p.x * 7.1f + p.y * 3.7f + p.z * 5.3f));
                var pa = centre + Vector3.Scale(Bump(a), scale);
                var pb = centre + Vector3.Scale(Bump(b), scale);
                var pc = centre + Vector3.Scale(Bump(c), scale);
                Triangle(material, pa, pb, pc, (pa + pb + pc) / 3 - centre);
            }
        }

        static List<(Vector3, Vector3, Vector3)> icosphere;
        static List<(Vector3, Vector3, Vector3)> icosahedron;

        /// <summary>The icosphere's corners before it is divided: 20 faces.</summary>
        static List<(Vector3, Vector3, Vector3)> Icosahedron()
        {
            if (icosahedron != null)
            {
                return icosahedron;
            }

            icosahedron = new List<(Vector3, Vector3, Vector3)>();
            var faces = Icosphere();

            // Every fourth face of the divided one is the middle of an original face; its corners' origins are the others'.
            for (var i = 0; i < faces.Count; i += 4)
            {
                icosahedron.Add((faces[i].Item1, faces[i + 1].Item1, faces[i + 2].Item1));
            }

            return icosahedron;
        }

        /// <summary>A small lump (a cabbage, a clod): eight faces, nudged by the seed.</summary>
        public void Lump(Material material, Vector3 centre, Vector3 scale, int seed = 0)
        {
            var j = 0.85f + 0.3f * Mathf.Abs(Mathf.Sin(seed * 12.9898f));
            var top = centre + Vector3.up * scale.y * j;
            var bottom = centre - Vector3.up * scale.y * 0.4f;
            var turn = Quaternion.Euler(0, seed * 37 % 90, 0);
            var ring = new Vector3[4];

            for (var i = 0; i < 4; i++)
            {
                var a = i * Mathf.PI / 2;
                ring[i] = centre + turn * new Vector3(Mathf.Cos(a) * scale.x, 0, Mathf.Sin(a) * scale.z);
            }

            for (var i = 0; i < 4; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % 4];
                Triangle(material, a, top, b, (a + b + top) / 3 - centre);
                Triangle(material, b, bottom, a, (a + b + bottom) / 3 - centre);
            }
        }

        static List<(Vector3, Vector3, Vector3)> Icosphere()
        {
            if (icosphere != null)
            {
                return icosphere;
            }

            var t = (1 + Mathf.Sqrt(5)) / 2;
            var p = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            for (var i = 0; i < p.Count; i++)
            {
                p[i] = p[i].normalized;
            }

            icosphere = new List<(Vector3, Vector3, Vector3)>();

            for (var i = 0; i < f.Length; i += 3)
            {
                Vector3 a = p[f[i]], b = p[f[i + 1]], c = p[f[i + 2]];
                Vector3 ab = ((a + b) / 2).normalized, bc = ((b + c) / 2).normalized, ca = ((c + a) / 2).normalized;
                icosphere.Add((a, ab, ca));
                icosphere.Add((b, bc, ab));
                icosphere.Add((c, ca, bc));
                icosphere.Add((ab, bc, ca));
            }

            return icosphere;
        }

        /// <summary>
        /// A hipped roof over a `width` × `depth` rectangle (X × Z) whose eaves are at
        /// `base` height: it rises `height` to a ridge along X, overhangs by `overhang`,
        /// and its corners curl up by `curl` — the eaves sweep up towards the corners.
        /// Drawn two-sided (both windings), so its underside shows too.
        /// </summary>
        public void Roof(Material material, Vector3 centre, float width, float depth, float height, float overhang, float curl, float yaw = 0)
        {
            var turn = Quaternion.Euler(0, yaw, 0);
            var w = width / 2 + overhang;
            var d = depth / 2 + overhang;
            var ridge = Mathf.Max(0.05f, (width - depth) / 2);
            const int segments = 8;

            Vector3 P(float x, float y, float z) => centre + turn * new Vector3(x, y, z);

            // An eave point along an edge, lifted towards the ends: t in −1…1.
            float Lift(float t) => curl * Mathf.Pow(Mathf.Abs(t), 3);

            for (var side = -1; side <= 1; side += 2)
            {
                // The long sides (±Z): strips from the eave up to the ridge.
                for (var i = 0; i < segments; i++)
                {
                    var t0 = -1 + 2f * i / segments;
                    var t1 = -1 + 2f * (i + 1) / segments;
                    var e0 = P(t0 * w, Lift(t0), side * d);
                    var e1 = P(t1 * w, Lift(t1), side * d);
                    var r0 = P(t0 * ridge, height, 0);
                    var r1 = P(t1 * ridge, height, 0);
                    var outward = turn * new Vector3(0, 1, side);
                    Quad(material, e0, e1, r1, r0, outward);
                    Quad(material, e0, e1, r1, r0, -outward);
                }

                // The ends (±X): fans from the eave up to the ridge's end.
                for (var i = 0; i < segments; i++)
                {
                    var t0 = -1 + 2f * i / segments;
                    var t1 = -1 + 2f * (i + 1) / segments;
                    var e0 = P(side * w, Lift(t0), t0 * d);
                    var e1 = P(side * w, Lift(t1), t1 * d);
                    var top = P(side * ridge, height, 0);
                    var outward = turn * new Vector3(side, 1, 0);
                    Triangle(material, e0, e1, top, outward);
                    Triangle(material, e0, e1, top, -outward);
                }
            }

            // A ridge beam along the top.
            Box(material, centre + turn * new Vector3(0, height + 0.08f, 0), new Vector3(ridge * 2 + 0.4f, 0.22f, 0.3f), yaw);
        }

        /// <summary>
        /// A Gu Yue hall's roof: hipped over a `width` × `depth` rectangle (X × Z) whose eaves are at
        /// `centre`'s height, rising `height` to a ridge along X. Its slopes are hollow — shallow at the
        /// eaves, steep under the ridge — and its corners swept out and up by `curl`. Tiles on top in
        /// raised rows (`ribs` apart; 0 for none, as thatch), a boarded underside `under`, a fascia
        /// along the eaves, the ridge and the four hips capped with `ridge`, the hips' ends curling up
        /// into horns and the ridge's ends turned up.
        /// </summary>
        public void CurvedRoof(Material tiles, Material under, Material ridge, Vector3 centre, float width, float depth,
                               float height, float overhang, float curl, float yaw = 0, float ribs = 0.34f)
        {
            var turn = Quaternion.Euler(0, yaw, 0);
            var w = width / 2 + overhang;
            var d = depth / 2 + overhang;
            var r = Mathf.Max(0.05f, w - d * 0.92f);
            var flare = curl * 0.55f;
            const int across = 12;
            const int up = 6;
            const float thickness = 0.14f;

            Vector3 P(Vector3 local) => centre + turn * local;

            // A point of the slope: `u` along the eave (−1…1), `v` from the eave (0) to the ridge (1).
            // Sides 0, 1 are the long ones (−Z, +Z), 2, 3 the ends (−X, +X).
            Vector3 Slope(int side, float u, float v)
            {
                var a = Mathf.Pow(Mathf.Abs(u), 3);
                var s = side % 2 == 0 ? -1 : 1;
                Vector3 eave, top;

                if (side < 2)
                {
                    eave = new Vector3(u * (w + flare * a), curl * a, s * (d + flare * a));
                    top = new Vector3(u * r, height, 0);
                }
                else
                {
                    eave = new Vector3(s * (w + flare * a), curl * a, u * (d + flare * a));
                    top = new Vector3(s * r, height, 0);
                }

                var flat = Vector3.Lerp(eave, top, v);
                flat.y = height * Mathf.Pow(v, 1.7f) + eave.y * (1 - v) * (1 - v);

                return flat;
            }

            Vector3 Out(int side) => side < 2 ? new Vector3(0, 1, side == 0 ? -1 : 1) : new Vector3(side == 2 ? -1 : 1, 1, 0);

            for (var side = 0; side < 4; side++)
            {
                var outward = turn * Out(side);
                var sideways = side < 2 ? new Vector3(0, 0, Out(side).z) : new Vector3(Out(side).x, 0, 0);

                for (var i = 0; i < across; i++)
                {
                    var u0 = -1 + 2f * i / across;
                    var u1 = -1 + 2f * (i + 1) / across;

                    for (var k = 0; k < up; k++)
                    {
                        var v0 = (float)k / up;
                        var v1 = (float)(k + 1) / up;
                        var a = Slope(side, u0, v0);
                        var b = Slope(side, u1, v0);
                        var c = Slope(side, u1, v1);
                        var e = Slope(side, u0, v1);
                        Quad(tiles, P(a), P(b), P(c), P(e), outward);

                        var down = Vector3.down * thickness;
                        Quad(under, P(a + down), P(b + down), P(c + down), P(e + down), Vector3.down);
                    }

                    // The fascia along the eave.
                    var f0 = Slope(side, u0, 0);
                    var f1 = Slope(side, u1, 0);
                    Quad(under, P(f0), P(f1), P(f1 + Vector3.down * thickness), P(f0 + Vector3.down * thickness), turn * sideways);
                }

                // Rows of tiles, eave to ridge.
                if (ribs > 0)
                {
                    var length = side < 2 ? 2 * w : 2 * d;
                    var rows = Mathf.Max(2, Mathf.RoundToInt(length / ribs));
                    var half = 0.055f / (length / 2);

                    for (var j = 1; j < rows; j++)
                    {
                        var u = -1 + 2f * j / rows;

                        for (var k = 0; k < up; k++)
                        {
                            var v0 = k == 0 ? -0.02f : (float)k / up;
                            var v1 = Mathf.Min(0.97f, (float)(k + 1) / up);
                            var l0 = Slope(side, u - half, v0);
                            var l1 = Slope(side, u - half, v1);
                            var r0 = Slope(side, u + half, v0);
                            var r1 = Slope(side, u + half, v1);
                            var a0 = (l0 + r0) / 2 + Vector3.up * 0.07f;
                            var a1 = (l1 + r1) / 2 + Vector3.up * 0.07f;
                            Quad(tiles, P(l0), P(a0), P(a1), P(l1), turn * ((a0 + l0) / 2 - r0));
                            Quad(tiles, P(a0), P(r0), P(r1), P(a1), turn * ((a0 + r0) / 2 - l0));
                        }
                    }
                }
            }

            // The hips: a rolled ridge from the ridge's end down to each corner, the corner curling up.
            const float rolled = 0.11f;

            foreach (var (side, u) in new[] { (0, -1f), (0, 1f), (1, -1f), (1, 1f) })
            {
                var previous = Slope(side, u, 1);

                for (var k = up - 1; k >= 0; k--)
                {
                    var next = Slope(side, u, (float)k / up);
                    Cylinder(ridge, P(previous + Vector3.up * rolled), P(next + Vector3.up * rolled), rolled, rolled, 5);
                    previous = next;
                }

                var corner = Slope(side, u, 0) + Vector3.up * rolled;
                var outAlong = new Vector3(u, 0, side == 0 ? -1 : 1).normalized;
                var bend = corner + outAlong * 0.32f * (0.5f + curl) + Vector3.up * 0.12f * (0.5f + curl);
                var tip = bend + outAlong * 0.16f * (0.5f + curl) + Vector3.up * 0.2f * (0.5f + curl);
                Cylinder(ridge, P(corner), P(bend), rolled, rolled * 0.8f, 5);
                Cylinder(ridge, P(bend), P(tip), rolled * 0.8f, 0.02f, 5);
            }

            // The ridge, its ends turned up.
            var ridgeY = height + 0.12f;
            Box(ridge, centre + turn * new Vector3(0, ridgeY, 0), new Vector3(2 * r + 0.3f, 0.3f, 0.3f), yaw);

            foreach (var s in new[] { -1, 1 })
            {
                var end = new Vector3(s * (r + 0.1f), ridgeY, 0);
                var bend = end + new Vector3(s * 0.22f, 0.18f, 0);
                var tip = bend + new Vector3(-s * 0.04f, 0.24f, 0);
                Cylinder(ridge, P(end), P(bend), 0.16f, 0.14f, 6);
                Cylinder(ridge, P(bend), P(tip), 0.14f, 0.03f, 6);
            }
        }

        /// <summary>
        /// A crescent moon, the Gu Yue's mark (as the art direction draws it: the horns to the right),
        /// `radius` across its outer arc and `thickness` deep, standing in the XY plane at `centre`,
        /// turned `roll` degrees within it and `yaw` about Y.
        /// </summary>
        public void Crescent(Material material, Vector3 centre, float radius, float thickness, float roll = 0, float yaw = 0)
        {
            var turn = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(0, 0, roll);
            const int steps = 14;
            var tip = Mathf.Atan2(0.9f, 0.436f);
            var outer = new Vector3[steps + 1];
            var inner = new Vector3[steps + 1];

            for (var i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                var phi = tip + t * (2 * Mathf.PI - 2 * tip);
                var psi = Mathf.PI / 2 + t * Mathf.PI;
                outer[i] = new Vector3(Mathf.Cos(phi), Mathf.Sin(phi), 0) * radius;
                inner[i] = new Vector3(0.436f * radius + 0.9f * radius * Mathf.Cos(psi), 0.9f * radius * Mathf.Sin(psi), 0);
            }

            var front = Vector3.forward * thickness / 2;
            var innerCentre = new Vector3(0.436f * radius, 0, 0);

            for (var i = 0; i < steps; i++)
            {
                Vector3 W(Vector3 p) => centre + turn * p;
                Quad(material, W(outer[i] + front), W(outer[i + 1] + front), W(inner[i + 1] + front), W(inner[i] + front), turn * Vector3.forward);
                Quad(material, W(outer[i] - front), W(outer[i + 1] - front), W(inner[i + 1] - front), W(inner[i] - front), turn * Vector3.back);
                Quad(material, W(outer[i] + front), W(outer[i + 1] + front), W(outer[i + 1] - front), W(outer[i] - front), turn * (outer[i] + outer[i + 1]));
                Quad(material, W(inner[i] + front), W(inner[i + 1] + front), W(inner[i + 1] - front), W(inner[i] - front), turn * (innerCentre - (inner[i] + inner[i + 1]) / 2));
            }
        }

        /// <summary>
        /// A wall `width` × `height` (from `centre`, its foot's middle) and `thickness` deep in the XY
        /// plane, turned `yaw`, with a round hole `radius` across whose middle is `holeY` above the foot:
        /// a moon gate, or a round window. The hole is lined with `lining`, a ring round it on both faces.
        /// </summary>
        public void RoundOpening(Material wall, Material lining, Vector3 centre, float width, float height, float thickness,
                                 float radius, float holeY, float yaw = 0, float ring = 0.12f)
        {
            var turn = Quaternion.Euler(0, yaw, 0);
            var hole = new Vector2(0, holeY);
            const int perSide = 6;
            Vector3 W(Vector2 p, float z) => centre + turn * new Vector3(p.x, p.y, z);

            // The rectangle's corners as seen from the hole's middle, counter-clockwise from the lower right.
            var corners = new[] { new Vector2(width / 2, 0), new Vector2(width / 2, height), new Vector2(-width / 2, height), new Vector2(-width / 2, 0) };
            var front = thickness / 2;

            for (var c = 0; c < 4; c++)
            {
                var from = corners[c];
                var to = corners[(c + 1) % 4];
                var a0 = Mathf.Atan2(from.y - hole.y, from.x - hole.x);
                var a1 = Mathf.Atan2(to.y - hole.y, to.x - hole.x);

                if (a1 < a0)
                {
                    a1 += 2 * Mathf.PI;
                }

                for (var i = 0; i < perSide; i++)
                {
                    var t0 = (float)i / perSide;
                    var t1 = (float)(i + 1) / perSide;
                    var e0 = Vector2.Lerp(from, to, t0);
                    var e1 = Vector2.Lerp(from, to, t1);
                    var c0 = hole + new Vector2(Mathf.Cos(Mathf.Lerp(a0, a1, t0)), Mathf.Sin(Mathf.Lerp(a0, a1, t0))) * radius;
                    var c1 = hole + new Vector2(Mathf.Cos(Mathf.Lerp(a0, a1, t1)), Mathf.Sin(Mathf.Lerp(a0, a1, t1))) * radius;

                    foreach (var z in new[] { front, -front })
                    {
                        Quad(wall, W(c0, z), W(c1, z), W(e1, z), W(e0, z), turn * new Vector3(0, 0, z));
                    }
                }
            }

            // The hole's lining and the rings round it.
            const int round = 24;

            for (var i = 0; i < round; i++)
            {
                var a0 = 2 * Mathf.PI * i / round;
                var a1 = 2 * Mathf.PI * (i + 1) / round;
                var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                var p0 = hole + d0 * radius;
                var p1 = hole + d1 * radius;
                Quad(lining, W(p0, front), W(p1, front), W(p1, -front), W(p0, -front), turn * -(Vector3)(d0 + d1));

                if (ring > 0)
                {
                    var q0 = hole + d0 * (radius + ring);
                    var q1 = hole + d1 * (radius + ring);

                    foreach (var z in new[] { front + 0.03f, -front - 0.03f })
                    {
                        Quad(lining, W(p0, z), W(p1, z), W(q1, z), W(q0, z), turn * new Vector3(0, 0, z));
                    }

                    Quad(lining, W(q0, front + 0.03f), W(q1, front + 0.03f), W(q1, front), W(q0, front), turn * (Vector3)(d0 + d1));
                    Quad(lining, W(q0, -front - 0.03f), W(q1, -front - 0.03f), W(q1, -front), W(q0, -front), turn * (Vector3)(d0 + d1));
                }
            }
        }

        /// <summary>
        /// A lattice window: a paper pane `width` × `height` at `centre` (facing +Z after `yaw`),
        /// framed, with bars across it every `cell` both ways.
        /// </summary>
        public void Lattice(Material frame, Material paper, Vector3 centre, float width, float height, float yaw = 0, float cell = 0.2f)
        {
            var turn = Quaternion.Euler(0, yaw, 0);
            Vector3 At(float x, float y, float z) => centre + turn * new Vector3(x, y, z);
            const float bar = 0.035f;
            const float rim = 0.08f;

            Box(paper, centre, new Vector3(width, height, 0.03f), yaw);
            Box(frame, At(0, height / 2 + rim / 2, 0.02f), new Vector3(width + 2 * rim, rim, 0.09f), yaw);
            Box(frame, At(0, -height / 2 - rim / 2, 0.02f), new Vector3(width + 2 * rim, rim, 0.09f), yaw);
            Box(frame, At(width / 2 + rim / 2, 0, 0.02f), new Vector3(rim, height, 0.09f), yaw);
            Box(frame, At(-width / 2 - rim / 2, 0, 0.02f), new Vector3(rim, height, 0.09f), yaw);

            var columns = Mathf.Max(1, Mathf.RoundToInt(width / cell));
            var rows = Mathf.Max(1, Mathf.RoundToInt(height / cell));

            for (var i = 1; i < columns; i++)
            {
                Box(frame, At(-width / 2 + width * i / columns, 0, 0.03f), new Vector3(bar, height, 0.04f), yaw);
            }

            for (var j = 1; j < rows; j++)
            {
                Box(frame, At(0, -height / 2 + height * j / rows, 0.03f), new Vector3(width, bar, 0.04f), yaw);
            }
        }

        /// <summary>The mesh, a submesh per material in the order first used, and those materials.</summary>
        public (Mesh mesh, Material[] materials) Build(string name)
        {
            var mesh = new Mesh { name = name };
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();

            foreach (var material in order)
            {
                var part = parts[material];
                vertices.AddRange(part.vertices);
                normals.AddRange(part.normals);
                uvs.AddRange(part.uvs);
            }

            if (vertices.Count > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = order.Count;
            var offset = 0;

            for (var m = 0; m < order.Count; m++)
            {
                var part = parts[order[m]];
                var triangles = new int[part.triangles.Count];

                for (var i = 0; i < triangles.Length; i++)
                {
                    triangles[i] = part.triangles[i] + offset;
                }

                mesh.SetTriangles(triangles, m);
                offset += part.vertices.Count;
            }

            mesh.RecalculateBounds();

            return (mesh, order.ToArray());
        }
    }
}
