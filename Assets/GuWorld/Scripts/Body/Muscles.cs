using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Body
{
    /// <summary>
    /// Smooths the models' muscles away. The bodies are Quaternius' superhero
    /// bases, with a bodybuilder's chest, belly and arms in the mesh itself; the
    /// people of a mountain clan have none of that. The trunk, arms and legs are
    /// smoothed — Taubin's way, a shrinking pass then a growing one, so the body
    /// keeps its size — while the head, hands and feet stay as they are, and the
    /// clothes (cut from the skin afterwards) come out smooth too.
    ///
    /// On the way the face is marked for a thinner outline (UV3.x, which
    /// GuWorld/Toon reads): the art direction draws it finer there.
    /// </summary>
    public static class Muscles
    {
        /// <summary>Pairs of passes: more, smoother.</summary>
        const int Passes = 16;
        /// <summary>Taubin's λ (towards the neighbours) and μ (back out, a little more).</summary>
        const float Lambda = 0.55f;
        const float Mu = -0.58f;
        /// <summary>How much thinner the outline is on the face (0 as elsewhere, 1 none).</summary>
        const float FaceOutline = 0.5f;

        /// <summary>How much a bone's flesh is smoothed: the trunk and limbs fully, the neck half, the rest not.</summary>
        static float Smoothed(string bone) =>
            bone.StartsWith("spine") || bone == "pelvis" || bone.StartsWith("clavicle") ||
            bone.StartsWith("upperarm") || bone.StartsWith("lowerarm") ||
            bone.StartsWith("thigh") || bone.StartsWith("calf") ? 1
            : bone == "neck_01" ? 0.5f
            : 0;

        /// <summary>Smooths a body's skin (in place when it is already the body's own copy).</summary>
        public static void Smooth(SkinnedMeshRenderer body)
        {
            var source = body.sharedMesh;

            if (source == null || !source.isReadable)
            {
                return;
            }

            var owned = body.GetComponent<GuWorld.World.OwnedMesh>();
            var mesh = owned != null && owned.Mesh == source ? source : Object.Instantiate(source);

            if (mesh != source)
            {
                mesh.name = source.name + " (smooth)";
                body.sharedMesh = mesh;
                body.gameObject.AddComponent<GuWorld.World.OwnedMesh>().Mesh = mesh;
            }

            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var weights = mesh.boneWeights;
            var triangles = mesh.triangles;
            var bones = body.bones;
            var n = vertices.Length;

            // Per bone: how much it is smoothed, and whether it is the head's.
            var smooth = new float[bones.Length];
            var head = new bool[bones.Length];

            for (var b = 0; b < bones.Length; b++)
            {
                smooth[b] = bones[b] != null ? Smoothed(bones[b].name) : 0;

                for (var up = bones[b]; up != null; up = up.parent)
                {
                    if (up.name == "Head")
                    {
                        head[b] = true;

                        break;
                    }
                }
            }

            // The UV seams split vertices that are one point of the skin: weld them by position.
            var weld = new int[n];
            var points = new List<Vector3>();
            var index = new Dictionary<Vector3Int, int>();

            for (var i = 0; i < n; i++)
            {
                var v = vertices[i];
                var key = new Vector3Int(Mathf.RoundToInt(v.x * 20000), Mathf.RoundToInt(v.y * 20000), Mathf.RoundToInt(v.z * 20000));

                if (!index.TryGetValue(key, out var at))
                {
                    at = points.Count;
                    index[key] = at;
                    points.Add(v);
                }

                weld[i] = at;
            }

            var count = points.Count;
            var amount = new float[count];
            var thin = new Vector2[n];

            for (var i = 0; i < n; i++)
            {
                var w = weights[i];
                var share = smooth[w.boneIndex0] * w.weight0 + smooth[w.boneIndex1] * w.weight1 +
                            smooth[w.boneIndex2] * w.weight2 + smooth[w.boneIndex3] * w.weight3;
                amount[weld[i]] = Mathf.Max(amount[weld[i]], share);

                var onHead = (head[w.boneIndex0] ? w.weight0 : 0) + (head[w.boneIndex1] ? w.weight1 : 0) +
                             (head[w.boneIndex2] ? w.weight2 : 0) + (head[w.boneIndex3] ? w.weight3 : 0);
                thin[i] = new Vector2(FaceOutline * onHead, 0);
            }

            // Neighbours, and the open edges (an edge of one triangle only), which stay put.
            var neighbours = new HashSet<int>[count];
            var edges = new Dictionary<long, int>();

            for (var i = 0; i < count; i++)
            {
                neighbours[i] = new HashSet<int>();
            }

            void Edge(int a, int b)
            {
                if (a == b)
                {
                    return;
                }

                neighbours[a].Add(b);
                neighbours[b].Add(a);
                var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                edges[key] = edges.TryGetValue(key, out var seen) ? seen + 1 : 1;
            }

            for (var t = 0; t < triangles.Length; t += 3)
            {
                int a = weld[triangles[t]], b = weld[triangles[t + 1]], c = weld[triangles[t + 2]];
                Edge(a, b);
                Edge(b, c);
                Edge(c, a);
            }

            foreach (var edge in edges)
            {
                if (edge.Value == 1)
                {
                    amount[(int)(edge.Key >> 32)] = 0;
                    amount[(int)(edge.Key & 0xffffffff)] = 0;
                }
            }

            var next = new Vector3[count];

            for (var pass = 0; pass < Passes * 2; pass++)
            {
                var factor = pass % 2 == 0 ? Lambda : Mu;

                for (var i = 0; i < count; i++)
                {
                    if (amount[i] <= 0 || neighbours[i].Count == 0)
                    {
                        next[i] = points[i];

                        continue;
                    }

                    var sum = Vector3.zero;

                    foreach (var k in neighbours[i])
                    {
                        sum += points[k];
                    }

                    next[i] = points[i] + factor * amount[i] * (sum / neighbours[i].Count - points[i]);
                }

                for (var i = 0; i < count; i++)
                {
                    points[i] = next[i];
                }
            }

            // Normals anew where the skin moved, shared across the seams (no hard edges there).
            var faceNormals = new Vector3[count];

            for (var t = 0; t < triangles.Length; t += 3)
            {
                int a = weld[triangles[t]], b = weld[triangles[t + 1]], c = weld[triangles[t + 2]];
                var normal = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
                faceNormals[a] += normal;
                faceNormals[b] += normal;
                faceNormals[c] += normal;
            }

            for (var i = 0; i < n; i++)
            {
                var at = weld[i];
                vertices[i] = points[at];

                if (amount[at] > 0 && faceNormals[at] != Vector3.zero)
                {
                    // Facing the way the model's own normal did (whichever winding it uses).
                    var fresh = faceNormals[at].normalized;
                    normals[i] = Vector3.Dot(fresh, normals[i]) < 0 ? -fresh : fresh;
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.SetUVs(3, thin);
            mesh.RecalculateBounds();
        }
    }
}
