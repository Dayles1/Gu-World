using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuWorld.Village;
using GuWorld.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuWorld.Editor
{
    /// <summary>
    /// The scene cut into squares for streaming (ChunkStreamer): every built thing of the
    /// villages, hamlets and giant trees — a house, a block of props, a stretch of wall, a
    /// lamp, a tree — goes into the 128 m square its middle lies in; each square is saved as
    /// a prefab under Resources and taken out of the scene, and leaves behind a stand-in for
    /// far away: its houses' plain shapes, its walls, towers and trees in one mesh. What stays
    /// in the scene: the ground, the water, the trails, the villages' layouts (data), the
    /// stand-ins and the streamer.
    /// </summary>
    public static partial class QingMaoBuilder
    {
        const float ChunkSize = 128;

        static string ChunkFolder => testRun ? "QingMaoChunksTest" : "QingMaoChunks";

        /// <summary>Cuts the given parts' things into squares; the streamer it makes knows them all.</summary>
        static ChunkStreamer CutIntoChunks(IEnumerable<GameObject> parts)
        {
            var folder = $"{Generated}/Resources/{ChunkFolder}";
            AssetDatabase.DeleteAsset(folder);
            // The folder made without a refresh (a refresh starts an import worker: a whole Unity more in memory).
            Directory.CreateDirectory(folder);

            var units = new List<GameObject>();

            foreach (var part in parts)
            {
                Collect(part.transform, units, true);
            }

            var groups = new Dictionary<Vector2Int, List<GameObject>>();

            foreach (var unit in units)
            {
                var centre = Middle(unit);
                var key = new Vector2Int(Mathf.FloorToInt(centre.x / ChunkSize), Mathf.FloorToInt(centre.z / ChunkSize));

                if (!groups.TryGetValue(key, out var list))
                {
                    groups[key] = list = new List<GameObject>();
                }

                list.Add(unit);
            }

            var streamer = new GameObject("World Streamer").AddComponent<ChunkStreamer>();
            var standIns = new GameObject("Far Stand-ins").transform;
            var chunks = new List<ChunkStreamer.Chunk>();
            var made = 0;

            foreach (var pair in groups.OrderBy(p => p.Key.y).ThenBy(p => p.Key.x))
            {
                var name = $"c {pair.Key.x} {pair.Key.y}";
                var proxy = StandIn(pair.Value, name, standIns);
                var holder = new GameObject(name);

                foreach (var unit in pair.Value)
                {
                    unit.transform.SetParent(holder.transform, true);
                }

                PrefabUtility.SaveAsPrefabAsset(holder, $"{folder}/{name}.prefab", out var saved);

                if (!saved)
                {
                    throw new System.Exception($"the square {name} was not saved");
                }

                Object.DestroyImmediate(holder);
                chunks.Add(new ChunkStreamer.Chunk { Resource = $"{ChunkFolder}/{name}", Corner = new Vector2(pair.Key.x, pair.Key.y) * ChunkSize, Size = ChunkSize, Proxy = proxy });

                if (++made % 40 == 0)
                {
                    BuildLog.Mark($"assemble: {made} of {groups.Count} squares cut");
                }
            }

            streamer.Chunks = chunks.ToArray();
            BuildLog.Mark($"assemble: {units.Count} things in {chunks.Count} squares of {ChunkSize} m");

            return streamer;
        }

        /// <summary>
        /// The things to stream: the highest objects under a part that are a thing of their own (a component
        /// besides the Transform, or small), not the containers holding them (a village's root with its layout,
        /// "Houses", "Wall"), which stay in the scene, empty.
        /// </summary>
        static void Collect(Transform t, List<GameObject> units, bool top)
        {
            var keep = top || t.GetComponent<VillageLayout>() != null;

            if (!keep && IsThing(t))
            {
                units.Add(t.gameObject);

                return;
            }

            foreach (var child in t.Cast<Transform>().ToList())
            {
                Collect(child, units, false);
            }
        }

        static bool IsThing(Transform t)
        {
            if (t.GetComponents<Component>().Length > 1 || t.childCount == 0)
            {
                return true;
            }

            var bounds = Extent(t.gameObject);

            return bounds.size.x <= 80 && bounds.size.z <= 80;
        }

        static Bounds Extent(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var colliders = go.GetComponentsInChildren<Collider>(true);
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            var first = true;

            foreach (var b in renderers.Select(r => r.bounds).Concat(colliders.Select(c => c.bounds)))
            {
                if (first)
                {
                    bounds = b;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }

            return bounds;
        }

        static Vector3 Middle(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);

            return renderers.Length > 0 ? Extent(go).center : go.transform.position;
        }

        /// <summary>
        /// A square's stand-in: the plainest shape of each of its houses and trees (their LOD groups' last level), its
        /// walls and great buildings (far-seen ones), its towers and gates (tall ones without levels) — one mesh, a
        /// part a material, no shadows. The small things (props, footings, steps) are left out: they vanish far off anyway.
        /// </summary>
        static Renderer StandIn(List<GameObject> units, string name, Transform parent)
        {
            var parts = new Dictionary<Material, List<CombineInstance>>();

            void Add(Renderer renderer)
            {
                if (renderer is not MeshRenderer || renderer.GetComponent<MeshFilter>() is not MeshFilter filter || filter.sharedMesh == null)
                {
                    return;
                }

                var mesh = filter.sharedMesh;
                var materials = renderer.sharedMaterials;

                for (var s = 0; s < mesh.subMeshCount && s < materials.Length; s++)
                {
                    if (materials[s] == null)
                    {
                        continue;
                    }

                    if (!parts.TryGetValue(materials[s], out var list))
                    {
                        parts[materials[s]] = list = new List<CombineInstance>();
                    }

                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = renderer.transform.localToWorldMatrix });
                }
            }

            foreach (var unit in units)
            {
                foreach (var group in unit.GetComponentsInChildren<LODGroup>(true))
                {
                    var lods = group.GetLODs();

                    if (lods.Length == 0)
                    {
                        continue;
                    }

                    // Houses and trees (two levels or more), and what is seen from far (walls, great buildings).
                    if (lods.Length >= 2 || lods[lods.Length - 1].screenRelativeTransitionHeight <= 0.015f)
                    {
                        foreach (var renderer in lods[lods.Length - 1].renderers)
                        {
                            if (renderer != null)
                            {
                                Add(renderer);
                            }
                        }
                    }
                }

                if (unit.GetComponentInChildren<LODGroup>(true) == null)
                {
                    var extent = Extent(unit);

                    if (extent.size.y > 3)
                    {
                        foreach (var renderer in unit.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            Add(renderer);
                        }
                    }
                }
            }

            if (parts.Count == 0)
            {
                return null;
            }

            var pieces = new List<CombineInstance>();
            var materialsUsed = new List<Material>();

            foreach (var pair in parts)
            {
                var piece = new Mesh { indexFormat = IndexFormat.UInt32 };
                piece.CombineMeshes(pair.Value.ToArray(), true, true);
                pieces.Add(new CombineInstance { mesh = piece, transform = Matrix4x4.identity });
                materialsUsed.Add(pair.Key);
            }

            var combined = new Mesh { name = $"Stand-in {name}", indexFormat = IndexFormat.UInt32 };
            combined.CombineMeshes(pieces.ToArray(), false, false);
            combined.RecalculateBounds();

            foreach (var piece in pieces)
            {
                Object.DestroyImmediate(piece.mesh);
            }

            combined = Keep(combined, $"{Generated}/Meshes/Stand-in {name}.asset");
            var go = new GameObject($"Stand-in {name}");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = combined;
            var standIn = go.AddComponent<MeshRenderer>();
            standIn.sharedMaterials = materialsUsed.ToArray();
            standIn.shadowCastingMode = ShadowCastingMode.Off;

            return standIn;
        }
    }
}
