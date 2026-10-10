using System.Collections.Generic;
using System.Diagnostics;
using GuWorld.Content;
using UnityEngine;

namespace GuWorld.World
{
    /// <summary>How the world is loaded around the hero (config/gu_world.php `world.chunks`), metres.</summary>
    [System.Serializable]
    public struct ChunkSettings
    {
        /// <summary>A chunk's side.</summary>
        public float size;
        /// <summary>Things within this are simulated (their colliders are in the world).</summary>
        public float simulationRadius;
        /// <summary>Things within this are drawn.</summary>
        public float visualRadius;
        /// <summary>How much farther than a radius a chunk is kept before being let go, so its edge does not flicker.</summary>
        public float margin;

        public static ChunkSettings Default => new ChunkSettings
        {
            size = 64,
            simulationRadius = 96,
            visualRadius = 192,
            margin = 16,
        };
    }

    /// <summary>
    /// Loads the world around the hero chunk by chunk and lets it go again
    /// behind them (ported from client/world/chunk-manager.ts).
    ///
    /// Chunk (cx, cz) covers [cx·size, (cx+1)·size) on X and the same on Z,
    /// so the grid is anchored at the world's origin. Within the larger of
    /// the two radii a chunk is loaded: its ground built and its things made;
    /// within `visualRadius` it is drawn, within `simulationRadius` its
    /// colliders are on. A chunk keeps a state until it is `margin` beyond
    /// that radius, so walking along an edge does not flip it every frame.
    /// The work is done nearest first, within a time budget per frame.
    ///
    /// Things keep their own stable ids (TestGrounds) and only land in
    /// whichever chunk their anchor point falls in, so changing the chunk
    /// size changes no id.
    /// </summary>
    public class ChunkManager : MonoBehaviour
    {
        public ChunkSettings Settings = ChunkSettings.Default;
        public LocationSettings Location = LocationSettings.TestGrounds;
        /// <summary>Whom the world is loaded around.</summary>
        public Transform Focus;
        /// <summary>Squares of ground per metre.</summary>
        public float Detail = 1;
        public Material GroundMaterial;
        public Material BlockMaterial;
        public Material StoneMaterial;
        /// <summary>Time the chunks may take in a frame, ms: while loading, and while playing.</summary>
        public float LoadingBudget = 12;
        public float PlayingBudget = 4;

        /// <summary>The chunks around the focus are ready: play can begin.</summary>
        public bool Loaded { get; private set; }
        public int LoadedCount => chunks.Count;

        class Chunk
        {
            public GameObject root;
            public readonly List<Renderer> renderers = new List<Renderer>();
            public readonly List<Collider> colliders = new List<Collider>();
            public bool visible = true;
            public bool simulated = true;
        }

        readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
        readonly List<Vector2Int> wanted = new List<Vector2Int>();
        readonly List<Vector2Int> leaving = new List<Vector2Int>();
        readonly Stopwatch clock = new Stopwatch();
        Mesh stoneMesh;

        /// <summary>Loads everything the hero needs at once (before the first frame of play).</summary>
        public void LoadAround(Vector3 point)
        {
            Refresh(point, float.PositiveInfinity, simulatedOnly: true);
            Loaded = true;
        }

        void Update()
        {
            if (Focus == null)
            {
                return;
            }

            Refresh(Focus.position, Loaded ? PlayingBudget : LoadingBudget, simulatedOnly: false);
        }

        void Refresh(Vector3 point, float budget, bool simulatedOnly)
        {
            clock.Restart();

            var size = Settings.size;
            var reach = Mathf.Max(Settings.simulationRadius, Settings.visualRadius);

            // Let go what is past its radius plus the margin.
            leaving.Clear();

            foreach (var pair in chunks)
            {
                if (DistanceTo(pair.Key, point) > reach + Settings.margin)
                {
                    leaving.Add(pair.Key);
                }
            }

            foreach (var key in leaving)
            {
                Destroy(chunks[key].root);
                chunks.Remove(key);
            }

            // What should be loaded, nearest first.
            wanted.Clear();
            var limit = simulatedOnly ? Settings.simulationRadius : reach;
            var minX = Mathf.FloorToInt(Mathf.Max(point.x - limit, Location.bounds.min.x) / size);
            var maxX = Mathf.FloorToInt((Mathf.Min(point.x + limit, Location.bounds.max.x) - 0.001f) / size);
            var minZ = Mathf.FloorToInt(Mathf.Max(point.z - limit, Location.bounds.min.z) / size);
            var maxZ = Mathf.FloorToInt((Mathf.Min(point.z + limit, Location.bounds.max.z) - 0.001f) / size);

            for (var cx = minX; cx <= maxX; cx++)
            {
                for (var cz = minZ; cz <= maxZ; cz++)
                {
                    var key = new Vector2Int(cx, cz);

                    if (!chunks.ContainsKey(key) && DistanceTo(key, point) <= limit)
                    {
                        wanted.Add(key);
                    }
                }
            }

            wanted.Sort((a, b) => DistanceTo(a, point).CompareTo(DistanceTo(b, point)));

            var built = 0;

            foreach (var key in wanted)
            {
                if (clock.Elapsed.TotalMilliseconds > budget)
                {
                    break;
                }

                chunks[key] = Build(key);
                built++;
            }

            // New colliders must be where their objects are before anything casts a ray at them.
            if (built > 0)
            {
                UnityEngine.Physics.SyncTransforms();
            }

            // Drawn and simulated by distance, each with its margin.
            foreach (var pair in chunks)
            {
                var distance = DistanceTo(pair.Key, point);
                var chunk = pair.Value;
                var visible = chunk.visible
                    ? distance <= Settings.visualRadius + Settings.margin
                    : distance <= Settings.visualRadius;
                var simulated = chunk.simulated
                    ? distance <= Settings.simulationRadius + Settings.margin
                    : distance <= Settings.simulationRadius;

                if (visible != chunk.visible)
                {
                    chunk.visible = visible;
                    chunk.renderers.ForEach(r => r.enabled = visible);
                }

                if (simulated != chunk.simulated)
                {
                    chunk.simulated = simulated;
                    chunk.colliders.ForEach(c => c.enabled = simulated);
                }
            }

            if (!Loaded && built == wanted.Count)
            {
                Loaded = true;
            }
        }

        /// <summary>How far a point is from a chunk's square (0 inside it).</summary>
        float DistanceTo(Vector2Int key, Vector3 point)
        {
            var size = Settings.size;
            var dx = Mathf.Max(key.x * size - point.x, 0, point.x - (key.x + 1) * size);
            var dz = Mathf.Max(key.y * size - point.z, 0, point.z - (key.y + 1) * size);

            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        Chunk Build(Vector2Int key)
        {
            var size = Settings.size;
            var minX = key.x * size;
            var minZ = key.y * size;
            var chunk = new Chunk { root = new GameObject($"Chunk {key.x}:{key.y}") };
            chunk.root.transform.SetParent(transform, false);

            BuildGround(chunk, minX, minZ, size);

            foreach (var block in TestGrounds.Blocks)
            {
                if (block.x >= minX && block.x < minX + size && block.z >= minZ && block.z < minZ + size)
                {
                    BuildBlock(chunk, block);
                }
            }

            foreach (var stone in TestGrounds.StonesIn(minX, minX + size, minZ, minZ + size))
            {
                BuildStone(chunk, stone);
            }

            return chunk;
        }

        void BuildGround(Chunk chunk, float minX, float minZ, float size)
        {
            // Only the part of the chunk inside the location.
            var x0 = Mathf.Max(minX, Location.bounds.min.x);
            var z0 = Mathf.Max(minZ, Location.bounds.min.z);
            var x1 = Mathf.Min(minX + size, Location.bounds.max.x);
            var z1 = Mathf.Min(minZ + size, Location.bounds.max.z);

            if (x1 <= x0 || z1 <= z0)
            {
                return;
            }

            var segments = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) * Detail));
            var segmentsZ = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) * Detail));
            var stepX = (x1 - x0) / segments;
            var stepZ = (z1 - z0) / segmentsZ;
            var row = segments + 1;
            var vertices = new Vector3[row * (segmentsZ + 1)];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            const float e = 0.2f;

            for (var j = 0; j <= segmentsZ; j++)
            {
                for (var i = 0; i <= segments; i++)
                {
                    var x = x0 + i * stepX;
                    var z = z0 + j * stepZ;
                    var index = j * row + i;
                    vertices[index] = new Vector3(x, TestGrounds.HeightAt(x, z), z);
                    // Normals from nearby heights, so chunk edges match.
                    var dx = TestGrounds.HeightAt(x + e, z) - TestGrounds.HeightAt(x - e, z);
                    var dz = TestGrounds.HeightAt(x, z + e) - TestGrounds.HeightAt(x, z - e);
                    normals[index] = new Vector3(-dx, 2 * e, -dz).normalized;
                    // The grid texture repeats every 10 m (a line a metre), as on the web.
                    uvs[index] = new Vector2(x / 10, z / 10);
                }
            }

            var triangles = new int[segments * segmentsZ * 6];
            var t = 0;

            for (var j = 0; j < segmentsZ; j++)
            {
                for (var i = 0; i < segments; i++)
                {
                    var a = j * row + i;
                    var b = a + 1;
                    var c = a + row;
                    var d = c + 1;
                    triangles[t++] = a;
                    triangles[t++] = c;
                    triangles[t++] = b;
                    triangles[t++] = b;
                    triangles[t++] = c;
                    triangles[t++] = d;
                }
            }

            var mesh = new Mesh { name = chunk.root.name + " ground" };
            mesh.indexFormat = vertices.Length > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            var ground = new GameObject("Ground");
            ground.transform.SetParent(chunk.root.transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ground.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GroundMaterial;
            var collider = ground.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            chunk.renderers.Add(renderer);
            chunk.colliders.Add(collider);
            ground.AddComponent<OwnedMesh>().Mesh = mesh;
        }

        /// <summary>A block's box from below the ground to its top.</summary>
        void BuildBlock(Chunk chunk, BlockObject block)
        {
            var baseY = TestGrounds.HeightAt(block.x, block.z);
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = block.id;
            box.transform.SetParent(chunk.root.transform, false);
            box.transform.position = new Vector3(block.x, baseY - 1 + (block.height + 1) / 2, block.z);
            box.transform.localScale = new Vector3(block.width, block.height + 1, block.depth);
            var renderer = box.GetComponent<Renderer>();
            renderer.sharedMaterial = BlockMaterial;
            chunk.renderers.Add(renderer);
            chunk.colliders.Add(box.GetComponent<Collider>());
        }

        /// <summary>A stone: a rough rounded rock, partly sunk into the ground.</summary>
        void BuildStone(Chunk chunk, StoneObject stone)
        {
            const float sink = 0.45f;
            var baseY = TestGrounds.HeightAt(stone.x, stone.z);
            var rock = new GameObject(stone.id);
            rock.transform.SetParent(chunk.root.transform, false);
            rock.transform.position = new Vector3(stone.x, baseY + stone.radius * (1 - sink * 2), stone.z);

            // The collider is the true sphere; the picture is turned and squashed its own way, from its id.
            var collider = rock.AddComponent<SphereCollider>();
            collider.radius = stone.radius;

            var seed = 0;

            unchecked
            {
                foreach (var c in stone.id)
                {
                    seed = seed * 31 + c;
                }
            }

            var look = new GameObject("Look");
            look.transform.SetParent(rock.transform, false);
            look.transform.localRotation = Quaternion.Euler(
                (seed % 7) * Mathf.Rad2Deg,
                (seed % 5) * Mathf.Rad2Deg,
                (seed % 3) * Mathf.Rad2Deg);
            look.transform.localScale = new Vector3(1, 0.7f + ((int)((uint)seed >> 3) % 30) / 100f, 1) * stone.radius;
            look.AddComponent<MeshFilter>().sharedMesh = StoneMesh();
            var renderer = look.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = StoneMaterial;

            chunk.renderers.Add(renderer);
            chunk.colliders.Add(collider);
        }

        /// <summary>A unit icosphere with flat faces (one subdivision), shared by every stone.</summary>
        Mesh StoneMesh()
        {
            if (stoneMesh != null)
            {
                return stoneMesh;
            }

            var t = (1 + Mathf.Sqrt(5)) / 2;
            var points = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            var faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            for (var i = 0; i < points.Count; i++)
            {
                points[i] = points[i].normalized;
            }

            // One subdivision: each triangle into four.
            var middles = new Dictionary<long, int>();

            int Middle(int a, int b)
            {
                var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

                if (!middles.TryGetValue(key, out var index))
                {
                    index = points.Count;
                    points.Add(((points[a] + points[b]) / 2).normalized);
                    middles[key] = index;
                }

                return index;
            }

            var finer = new List<int>();

            for (var i = 0; i < faces.Count; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                finer.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            // Flat shading: every triangle its own vertices. Unity winds clockwise, so each face is flipped.
            var vertices = new Vector3[finer.Count];
            var triangles = new int[finer.Count];

            for (var i = 0; i < finer.Count; i += 3)
            {
                vertices[i] = points[finer[i]];
                vertices[i + 1] = points[finer[i + 2]];
                vertices[i + 2] = points[finer[i + 1]];
                triangles[i] = i;
                triangles[i + 1] = i + 1;
                triangles[i + 2] = i + 2;
            }

            stoneMesh = new Mesh { name = "Stone", vertices = vertices, triangles = triangles };
            stoneMesh.RecalculateNormals();
            stoneMesh.RecalculateBounds();

            return stoneMesh;
        }

        void OnDestroy()
        {
            if (stoneMesh != null)
            {
                Destroy(stoneMesh);
            }
        }
    }

    /// <summary>Frees a mesh made at runtime when its object goes.</summary>
    public class OwnedMesh : MonoBehaviour
    {
        public Mesh Mesh;

        void OnDestroy()
        {
            if (Mesh != null)
            {
                Destroy(Mesh);
            }
        }
    }
}
