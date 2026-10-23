using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuWorld.Render
{
    /// <summary>
    /// The terrains' trees drawn in bulk: Unity draws each tree with levels of detail one by
    /// one (thousands of them cost a third of a frame), so the terrains keep their trees only
    /// to be solid (their trunks) and this draws them — gathered into 64 m squares, each square
    /// drawn with the GPU's instancing, its level of detail by its distance, squares out of
    /// view skipped. How far: Distance (the graphics level's).
    /// </summary>
    public class TreeInstancer : MonoBehaviour
    {
        const float CellSize = 64;
        const int Batch = 1023;

        public static TreeInstancer Current { get; private set; }

        /// <summary>How far trees are drawn, m (0: none).</summary>
        public static float Distance = 800;
        /// <summary>Within these the full tree and the middle one are drawn (beyond, the plainest).</summary>
        public static float FullUpTo = 120;
        public static float MiddleUpTo = 420;
        public static float ShadowsUpTo = 110;

        class Kind
        {
            public Mesh[] Meshes = new Mesh[3];
            public Material[][] Materials = new Material[3][];
        }

        class Cell
        {
            public Bounds Bounds;
            public readonly Dictionary<Kind, List<Matrix4x4>> Trees = new Dictionary<Kind, List<Matrix4x4>>();
            public readonly Dictionary<Kind, Matrix4x4[]> Ready = new Dictionary<Kind, Matrix4x4[]>();
        }

        readonly Dictionary<Vector2Int, Cell> cells = new Dictionary<Vector2Int, Cell>();
        readonly Dictionary<GameObject, Kind> kinds = new Dictionary<GameObject, Kind>();
        readonly Plane[] planes = new Plane[6];
        bool gathered;

        void Awake() => Current = this;

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        Kind KindOf(GameObject prefab)
        {
            if (kinds.TryGetValue(prefab, out var kind))
            {
                return kind;
            }

            kind = null;
            var group = prefab.GetComponent<LODGroup>();
            var lods = group != null ? group.GetLODs() : null;

            if (lods != null && lods.Length > 0)
            {
                kind = new Kind();

                for (var level = 0; level < 3; level++)
                {
                    var lod = lods[Mathf.Min(level, lods.Length - 1)];
                    var renderer = lod.renderers.Length > 0 ? lod.renderers[0] : null;
                    var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
                    kind.Meshes[level] = filter != null ? filter.sharedMesh : null;
                    kind.Materials[level] = renderer != null ? renderer.sharedMaterials : new Material[0];
                }
            }

            kinds[prefab] = kind;

            return kind;
        }

        /// <summary>Takes the terrains' trees over: gathered into squares, and the terrains no longer draw them.</summary>
        void Gather()
        {
            gathered = true;

            foreach (var terrain in Terrain.activeTerrains)
            {
                var data = terrain.terrainData;
                var corner = terrain.GetPosition();
                var size = data.size;
                var prototypes = data.treePrototypes;

                foreach (var tree in data.treeInstances)
                {
                    if (tree.prototypeIndex >= prototypes.Length || prototypes[tree.prototypeIndex].prefab == null)
                    {
                        continue;
                    }

                    var kind = KindOf(prototypes[tree.prototypeIndex].prefab);

                    if (kind == null)
                    {
                        continue;
                    }

                    var at = corner + Vector3.Scale(tree.position, size);
                    var key = new Vector2Int(Mathf.FloorToInt(at.x / CellSize), Mathf.FloorToInt(at.z / CellSize));

                    if (!cells.TryGetValue(key, out var cell))
                    {
                        cells[key] = cell = new Cell { Bounds = new Bounds(at, Vector3.zero) };
                    }

                    if (!cell.Trees.TryGetValue(kind, out var list))
                    {
                        cell.Trees[kind] = list = new List<Matrix4x4>();
                    }

                    list.Add(Matrix4x4.TRS(at, Quaternion.Euler(0, tree.rotation * Mathf.Rad2Deg, 0), new Vector3(tree.widthScale, tree.heightScale, tree.widthScale)));
                    // The tree's own size (trunk and crown grow with it): tall ones are seen over the hill before their foot is.
                    cell.Bounds.Encapsulate(new Bounds(at + Vector3.up * 10 * tree.heightScale, new Vector3(12 * tree.widthScale, 20 * tree.heightScale, 12 * tree.widthScale)));
                }

                terrain.treeDistance = 0;
            }

            foreach (var cell in cells.Values)
            {
                foreach (var pair in cell.Trees)
                {
                    cell.Ready[pair.Key] = pair.Value.ToArray();
                }

                cell.Trees.Clear();
            }
        }

        void LateUpdate()
        {
            if (!gathered)
            {
                if (Terrain.activeTerrains.Length == 0)
                {
                    return;
                }

                using (Engine.Costs.Time("trees gathered"))
                {
                    Gather();
                }
            }

            var camera = Camera.main;

            if (camera == null || Distance <= 0)
            {
                return;
            }

            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            var at = camera.transform.position;
            var reach = Mathf.CeilToInt(Distance / CellSize);
            var centre = new Vector2Int(Mathf.FloorToInt(at.x / CellSize), Mathf.FloorToInt(at.z / CellSize));
            var bias = QualitySettings.lodBias;

            for (var dz = -reach; dz <= reach; dz++)
            {
                for (var dx = -reach; dx <= reach; dx++)
                {
                    if (!cells.TryGetValue(new Vector2Int(centre.x + dx, centre.y + dz), out var cell))
                    {
                        continue;
                    }

                    var distance = Mathf.Sqrt(cell.Bounds.SqrDistance(at));

                    if (distance > Distance || !GeometryUtility.TestPlanesAABB(planes, cell.Bounds))
                    {
                        continue;
                    }

                    var level = distance < FullUpTo * bias ? 0 : distance < MiddleUpTo * bias ? 1 : 2;
                    var shadows = distance < ShadowsUpTo ? ShadowCastingMode.On : ShadowCastingMode.Off;

                    foreach (var pair in cell.Ready)
                    {
                        var mesh = pair.Key.Meshes[level];
                        var materials = pair.Key.Materials[level];

                        if (mesh == null)
                        {
                            continue;
                        }

                        var matrices = pair.Value;

                        for (var sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
                        {
                            if (materials[sub] == null)
                            {
                                continue;
                            }

                            var parameters = new RenderParams(materials[sub]) { shadowCastingMode = shadows, receiveShadows = true, worldBounds = cell.Bounds };

                            for (var start = 0; start < matrices.Length; start += Batch)
                            {
                                Graphics.RenderMeshInstanced(parameters, mesh, sub, matrices, Mathf.Min(Batch, matrices.Length - start), start);
                            }
                        }
                    }
                }
            }
        }
    }
}
