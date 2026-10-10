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
            foreach (var (a, b, c) in Icosphere())
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
