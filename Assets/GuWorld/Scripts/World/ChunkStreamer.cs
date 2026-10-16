using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.World
{
    /// <summary>
    /// The mountain's built things in squares (128 m): only the squares near the camera are
    /// in the world — loaded in the background as one comes near (within LoadRadius), let go
    /// as one goes away (beyond UnloadRadius). Each square has a stand-in for far away (its
    /// houses' and walls' plain shapes in one mesh), shown while the square itself is not
    /// there. The ground (the terrains), the water and the plan are always there.
    ///
    /// The builder (QingMaoBuilder.Assemble) cuts the squares into prefabs under
    /// Resources/QingMaoChunks and fills in the list.
    /// </summary>
    public class ChunkStreamer : MonoBehaviour
    {
        [Serializable]
        public struct Chunk
        {
            /// <summary>The prefab's path under Resources.</summary>
            public string Resource;
            /// <summary>Its square: south-west corner (X, Z) and size, m.</summary>
            public Vector2 Corner;
            public float Size;
            /// <summary>Its stand-in for far away (may be null).</summary>
            public Renderer Proxy;
        }

        public Chunk[] Chunks = new Chunk[0];
        public float LoadRadius = 260;
        public float UnloadRadius = 360;
        /// <summary>How many squares load at once.</summary>
        public int Parallel = 2;
        /// <summary>
        /// While playing, what the dropped squares used is let go only once this many have piled up (it stops
        /// the game for a moment); otherwise when the game pauses (LetGo), where the moment is not seen.
        /// </summary>
        public int LetGoAfter = 24;

        public static ChunkStreamer Current { get; private set; }

        /// <summary>How many squares are in the world now, and loading.</summary>
        public int Loaded { get; private set; }
        public int Loading { get; private set; }

        enum State
        {
            Away,
            Asking,
            Making,
            Here,
        }

        class Slot
        {
            public State State;
            public ResourceRequest Request;
            public AsyncInstantiateOperation<GameObject> Making;
            public GameObject Instance;
        }

        Slot[] slots;
        Vector3 lastAt = new Vector3(float.NaN, 0, 0);
        float nextLook;
        int dropped;

        void Awake()
        {
            Current = this;
            slots = new Slot[Chunks.Length];

            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = new Slot();
            }
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        static Vector3 Watcher()
        {
            var camera = Camera.main;

            return camera != null ? camera.transform.position : Vector3.zero;
        }

        float DistanceTo(int i, Vector3 at)
        {
            var c = Chunks[i];
            var dx = Mathf.Max(c.Corner.x - at.x, 0, at.x - (c.Corner.x + c.Size));
            var dz = Mathf.Max(c.Corner.y - at.z, 0, at.z - (c.Corner.y + c.Size));

            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        void Update()
        {
            var at = Watcher();
            var jumped = float.IsNaN(lastAt.x) || Vector3.Distance(at, lastAt) > 150;

            // A jump (the start, a teleport, a picture from somewhere else): the squares round the new place at once.
            if (jumped)
            {
                lastAt = at;
                LoadNowAround(at);
                nextLook = Time.unscaledTime + 0.2f;

                return;
            }

            Finish();

            if (Time.unscaledTime < nextLook)
            {
                return;
            }

            nextLook = Time.unscaledTime + 0.2f;
            lastAt = at;

            // The nearest wanted square first.
            var best = -1;
            var bestDistance = float.PositiveInfinity;

            for (var i = 0; i < Chunks.Length; i++)
            {
                var distance = DistanceTo(i, at);
                var slot = slots[i];

                if (slot.State == State.Here && distance > UnloadRadius)
                {
                    Drop(i);
                }
                else if (slot.State == State.Away && distance < LoadRadius && distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            if (best >= 0 && Loading < Parallel)
            {
                var slot = slots[best];
                slot.State = State.Asking;
                slot.Request = Resources.LoadAsync<GameObject>(Chunks[best].Resource);
                Loading++;
            }

            // Let go of what the dropped squares used, now and then (not every time: it takes a moment).
            if (dropped >= LetGoAfter)
            {
                LetGo();
            }
        }

        /// <summary>Lets go of what the squares dropped so far used (the game calls it as it pauses).</summary>
        public void LetGo()
        {
            if (dropped == 0)
            {
                return;
            }

            dropped = 0;

            using (Engine.Costs.Time("chunk assets let go"))
            {
                Resources.UnloadUnusedAssets();
            }
        }

        /// <summary>Moves the loads along: an asked prefab, once there, is made (spread over frames); a made one shows.</summary>
        void Finish()
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];

                if (slot.State == State.Asking && slot.Request.isDone)
                {
                    var prefab = slot.Request.asset as GameObject;

                    if (prefab == null)
                    {
                        slot.State = State.Away;
                        Loading--;
                        continue;
                    }

                    slot.Making = InstantiateAsync(prefab, transform);
                    slot.State = State.Making;
                }
                else if (slot.State == State.Making && slot.Making.isDone)
                {
                    slot.Instance = slot.Making.Result.Length > 0 ? slot.Making.Result[0] : null;
                    slot.Making = null;
                    slot.Request = null;
                    Arrived(i);
                    Loading--;
                }
            }
        }

        void Arrived(int i)
        {
            slots[i].State = State.Here;
            Loaded++;

            if (Chunks[i].Proxy != null)
            {
                Chunks[i].Proxy.enabled = false;
            }
        }

        void Drop(int i)
        {
            var slot = slots[i];

            if (slot.Instance != null)
            {
                Destroy(slot.Instance);
            }

            slot.Instance = null;
            slot.State = State.Away;
            Loaded--;
            dropped++;

            if (Chunks[i].Proxy != null)
            {
                Chunks[i].Proxy.enabled = true;
            }
        }

        /// <summary>The squares within reach of a place, loaded and made at once (a moment's pause, at a jump).</summary>
        public void LoadNowAround(Vector3 at)
        {
            using var cost = Engine.Costs.Time("chunks loaded at once");

            for (var i = 0; i < Chunks.Length; i++)
            {
                var distance = DistanceTo(i, at);
                var slot = slots[i];

                if (distance > UnloadRadius && slot.State == State.Here)
                {
                    Drop(i);
                }
                else if (distance < LoadRadius && slot.State == State.Away)
                {
                    var prefab = Resources.Load<GameObject>(Chunks[i].Resource);

                    if (prefab != null)
                    {
                        slot.Instance = Instantiate(prefab, transform);
                        Arrived(i);
                    }
                }
            }
        }
    }
}
