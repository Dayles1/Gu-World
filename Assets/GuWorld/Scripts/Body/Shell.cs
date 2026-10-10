using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Body
{
    /// <summary>
    /// A skinned body mesh read once: its vertices in the model's rest pose
    /// (Y up, facing +Z, the figure's left on -X), normals, UVs, triangles and
    /// how much each vertex follows each bone.
    /// </summary>
    public class SkinData
    {
        public readonly SkinnedMeshRenderer Renderer;
        public readonly Mesh Mesh;
        public readonly Vector3[] Vertices;
        public readonly Vector3[] Normals;
        public readonly Vector2[] Uv;
        public readonly int[] Triangles;
        public readonly BoneWeight[] Weights;
        public readonly string[] BoneNames;

        public SkinData(SkinnedMeshRenderer renderer)
        {
            Renderer = renderer;
            Mesh = renderer.sharedMesh;
            Vertices = Mesh.vertices;
            Normals = Mesh.normals;
            Uv = Mesh.uv;
            Triangles = Mesh.triangles;
            Weights = Mesh.boneWeights;
            BoneNames = Array.ConvertAll(renderer.bones, b => b != null ? b.name : "");
        }

        public int Count => Vertices.Length;

        public int BoneIndex(string name) => Array.IndexOf(BoneNames, name);

        /// <summary>How much a vertex follows the bones whose names pass a test.</summary>
        public float Share(int vertex, Func<string, bool> test)
        {
            var w = Weights[vertex];
            var sum = 0f;

            if (w.weight0 > 0 && test(BoneNames[w.boneIndex0])) sum += w.weight0;
            if (w.weight1 > 0 && test(BoneNames[w.boneIndex1])) sum += w.weight1;
            if (w.weight2 > 0 && test(BoneNames[w.boneIndex2])) sum += w.weight2;
            if (w.weight3 > 0 && test(BoneNames[w.boneIndex3])) sum += w.weight3;

            return sum;
        }

        /// <summary>Per vertex: how much it follows the bones whose names pass a test.</summary>
        public float[] Shares(Func<string, bool> test)
        {
            // Asked per bone once, not per vertex.
            var passes = Array.ConvertAll(BoneNames, n => test(n));
            var shares = new float[Count];

            for (var i = 0; i < Count; i++)
            {
                var w = Weights[i];
                shares[i] = (passes[w.boneIndex0] ? w.weight0 : 0) + (passes[w.boneIndex1] ? w.weight1 : 0) +
                            (passes[w.boneIndex2] ? w.weight2 : 0) + (passes[w.boneIndex3] ? w.weight3 : 0);
            }

            return shares;
        }
    }

    /// <summary>
    /// Pieces cut from the body's own skin — a cap of hair, a beard, clothes
    /// (ported from client/body/shell.ts): the part of the skin a coverage
    /// (0…1 per vertex) takes in, lifted off the skin along its normals and
    /// skinned to the same bones so it bends with the body.
    ///
    /// On the web the edge was cut by the material where the vertex alpha fell
    /// under ½; here each triangle is cut along that line itself, so the edge
    /// runs straight through the triangles and any material will do.
    /// </summary>
    public static class Shell
    {
        /// <summary>A vertex being built: where it is, which way it faces, its UV and its bones.</summary>
        struct Corner
        {
            public Vector3 position;
            public Vector3 normal;
            public Vector2 uv;
            public BoneWeight weight;
            public float lift;
        }

        /// <summary>
        /// The covered part of the body's skin, lifted `lift` (m) off it — or, with
        /// `liftOf`, by a lift per vertex (looser cloth), or, with `placed`, where each
        /// vertex is given (cloth that drapes) — as a skinned piece beside the body.
        /// Null when nothing is covered.
        /// </summary>
        public static SkinnedMeshRenderer Cut(
            SkinData body,
            float[] cover,
            float lift,
            Material material,
            string name,
            float[] liftOf = null,
            Vector3[] placed = null)
        {
            var corners = new List<Corner>();
            var triangles = new List<int>();
            var kept = new Dictionary<int, int>();
            var onEdge = new Dictionary<long, int>();

            int Keep(int v)
            {
                if (!kept.TryGetValue(v, out var index))
                {
                    index = corners.Count;
                    corners.Add(new Corner
                    {
                        position = placed != null ? placed[v] : body.Vertices[v],
                        normal = body.Normals[v],
                        uv = body.Uv.Length > 0 ? body.Uv[v] : Vector2.zero,
                        weight = body.Weights[v],
                        lift = liftOf != null ? liftOf[v] : lift,
                    });
                    kept[v] = index;
                }

                return index;
            }

            // Where the edge crosses from a to b (cover ½), shared by both triangles on that side.
            int Cross(int a, int b)
            {
                var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

                if (!onEdge.TryGetValue(key, out var index))
                {
                    var t = Mathf.Clamp01((0.5f - cover[a]) / (cover[b] - cover[a]));
                    index = corners.Count;
                    corners.Add(new Corner
                    {
                        position = placed != null ? Vector3.Lerp(placed[a], placed[b], t) : Vector3.Lerp(body.Vertices[a], body.Vertices[b], t),
                        normal = Vector3.Lerp(body.Normals[a], body.Normals[b], t).normalized,
                        uv = body.Uv.Length > 0 ? Vector2.Lerp(body.Uv[a], body.Uv[b], t) : Vector2.zero,
                        weight = Blend(body.Weights[a], body.Weights[b], t),
                        lift = Mathf.Lerp(liftOf != null ? liftOf[a] : lift, liftOf != null ? liftOf[b] : lift, t),
                    });
                    onEdge[key] = index;
                }

                return index;
            }

            var source = body.Triangles;
            var polygon = new List<int>(4);

            for (var i = 0; i < source.Length; i += 3)
            {
                int a = source[i], b = source[i + 1], c = source[i + 2];
                bool inA = cover[a] >= 0.5f, inB = cover[b] >= 0.5f, inC = cover[c] >= 0.5f;

                if (!inA && !inB && !inC)
                {
                    continue;
                }

                if (inA && inB && inC)
                {
                    triangles.Add(Keep(a));
                    triangles.Add(Keep(b));
                    triangles.Add(Keep(c));

                    continue;
                }

                // The covered part of the triangle: its covered corners and the crossings, in order.
                polygon.Clear();
                var ring = new[] { a, b, c };

                for (var k = 0; k < 3; k++)
                {
                    var p = ring[k];
                    var q = ring[(k + 1) % 3];
                    var inP = cover[p] >= 0.5f;
                    var inQ = cover[q] >= 0.5f;

                    if (inP)
                    {
                        polygon.Add(Keep(p));
                    }

                    if (inP != inQ)
                    {
                        polygon.Add(Cross(p, q));
                    }
                }

                for (var k = 1; k + 1 < polygon.Count; k++)
                {
                    triangles.Add(polygon[0]);
                    triangles.Add(polygon[k]);
                    triangles.Add(polygon[k + 1]);
                }
            }

            if (triangles.Count == 0)
            {
                return null;
            }

            var vertices = new Vector3[corners.Count];
            var normals = new Vector3[corners.Count];
            var uvs = new Vector2[corners.Count];
            var weights = new BoneWeight[corners.Count];

            for (var i = 0; i < corners.Count; i++)
            {
                var corner = corners[i];
                vertices[i] = placed != null ? corner.position : corner.position + corner.normal * corner.lift;
                normals[i] = corner.normal;
                uvs[i] = corner.uv;
                weights[i] = corner.weight;
            }

            return Build(body, name, vertices, normals, uvs, weights, new[] { triangles.ToArray() }, new[] { material });
        }

        /// <summary>
        /// A skinned piece on the body's skeleton, beside the body, from vertices in the
        /// rest pose. Each submesh gets its own material.
        /// </summary>
        public static SkinnedMeshRenderer Build(
            SkinData body,
            string name,
            Vector3[] vertices,
            Vector3[] normals,
            Vector2[] uvs,
            BoneWeight[] weights,
            int[][] submeshes,
            Material[] materials)
        {
            var mesh = new Mesh { name = name };

            if (vertices.Length > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.vertices = vertices;

            if (normals != null)
            {
                mesh.normals = normals;
            }

            if (uvs != null)
            {
                mesh.uv = uvs;
            }

            mesh.boneWeights = weights;
            mesh.bindposes = body.Mesh.bindposes;
            mesh.subMeshCount = submeshes.Length;

            for (var s = 0; s < submeshes.Length; s++)
            {
                mesh.SetTriangles(submeshes[s], s);
            }

            if (normals == null)
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();

            var piece = new GameObject(name);
            var source = body.Renderer.transform;
            piece.transform.SetParent(source.parent, false);
            piece.transform.localPosition = source.localPosition;
            piece.transform.localRotation = source.localRotation;
            piece.transform.localScale = source.localScale;
            piece.layer = source.gameObject.layer;

            var renderer = piece.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = body.Renderer.bones;
            renderer.rootBone = body.Renderer.rootBone;
            renderer.sharedMaterials = materials;
            renderer.updateWhenOffscreen = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            piece.AddComponent<GuWorld.World.OwnedMesh>().Mesh = mesh;

            return renderer;
        }

        /// <summary>Two vertices' bones mixed (t toward b): the four strongest kept, summing to one.</summary>
        public static BoneWeight Blend(BoneWeight a, BoneWeight b, float t)
        {
            var sums = new Dictionary<int, float>(8);

            void Add(int bone, float weight)
            {
                if (weight <= 0) return;
                sums.TryGetValue(bone, out var sum);
                sums[bone] = sum + weight;
            }

            Add(a.boneIndex0, a.weight0 * (1 - t));
            Add(a.boneIndex1, a.weight1 * (1 - t));
            Add(a.boneIndex2, a.weight2 * (1 - t));
            Add(a.boneIndex3, a.weight3 * (1 - t));
            Add(b.boneIndex0, b.weight0 * t);
            Add(b.boneIndex1, b.weight1 * t);
            Add(b.boneIndex2, b.weight2 * t);
            Add(b.boneIndex3, b.weight3 * t);

            return Strongest(sums);
        }

        /// <summary>A weight from bone → share pairs: the four strongest, summing to one.</summary>
        public static BoneWeight Strongest(Dictionary<int, float> sums)
        {
            var list = new List<KeyValuePair<int, float>>(sums);
            list.Sort((x, y) => y.Value.CompareTo(x.Value));
            var total = 0f;

            for (var i = 0; i < Mathf.Min(4, list.Count); i++)
            {
                total += list[i].Value;
            }

            var weight = new BoneWeight();

            if (total <= 0)
            {
                weight.weight0 = 1;

                return weight;
            }

            float W(int i) => i < list.Count ? list[i].Value / total : 0;
            int B(int i) => i < list.Count ? list[i].Key : 0;

            weight.boneIndex0 = B(0); weight.weight0 = W(0);
            weight.boneIndex1 = B(1); weight.weight1 = W(1);
            weight.boneIndex2 = B(2); weight.weight2 = W(2);
            weight.boneIndex3 = B(3); weight.weight3 = W(3);

            return weight;
        }

        /// <summary>A weight split between two bones (share on the first).</summary>
        public static BoneWeight Between(int first, int second, float share)
        {
            if (second < 0 || first == second)
            {
                return new BoneWeight { boneIndex0 = Mathf.Max(0, first), weight0 = 1 };
            }

            return share >= 0.5f
                ? new BoneWeight { boneIndex0 = first, weight0 = share, boneIndex1 = second, weight1 = 1 - share }
                : new BoneWeight { boneIndex0 = second, weight0 = 1 - share, boneIndex1 = first, weight1 = share };
        }

        /// <summary>How much a point is covered, 0…1, from its distance inside an edge.</summary>
        public static float Inside(float distance, float edge = 0.005f) => Mathf.Clamp01(0.5f + distance / edge);
    }
}
