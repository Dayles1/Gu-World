using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.World
{
    /// <summary>
    /// A bed of moon orchids (lore: res_moon_orchid — the clan grows them by its
    /// spring; their petals, blue and pink and curved like a young moon, feed
    /// many Gu). Gathered — held for a couple of seconds — it gives a handful of
    /// petals and stands bare, growing back over half a day of game time. The
    /// game says what time it is (Now) each frame.
    /// </summary>
    public class HerbBed : MonoBehaviour
    {
        /// <summary>The game minute now (set by the game each frame).</summary>
        public static double Now;

        /// <summary>Every bed in the world, in the order they were made (saved by that order).</summary>
        public static readonly List<HerbBed> All = new List<HerbBed>();

        public string Item = GuWorld.Items.ItemBook.Petal;
        public string Label = "Лунная орхидея";
        public int Yield = 4;
        /// <summary>Game minutes to grow back after gathering.</summary>
        public double Regrow = 12 * 60;
        /// <summary>Seconds the gathering takes (the design's "2 с").</summary>
        public const float GatherTime = 2;
        /// <summary>The game minute it is in flower again.</summary>
        public double ReadyAt;

        readonly List<Transform> blooms = new List<Transform>();
        float shown = 1;

        public bool Ready => Now >= ReadyAt;

        /// <summary>Picks it: its petals (0 when it is bare); it grows back in time.</summary>
        public int Gather()
        {
            if (!Ready)
            {
                return 0;
            }

            ReadyAt = Now + Regrow;

            return Yield;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            // The flowers close up and sink when picked, and open again as the bed grows back.
            var target = Ready ? 1f : 0.2f;
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime * 1.5f);

            foreach (var bloom in blooms)
            {
                bloom.localScale = Vector3.one * shown;
            }
        }

        /// <summary>A bed of a few flowers on stems among leaves, at a point on the ground.</summary>
        public static HerbBed Make(Vector3 at, int seed, Material blue, Material pink, Material leaf, Material heart)
        {
            var root = new GameObject("Moon Orchid Bed");
            root.transform.position = at;
            var bed = root.AddComponent<HerbBed>();
            var random = new System.Random(seed);
            float Next(float min, float max) => min + (float)random.NextDouble() * (max - min);

            for (var i = 0; i < 7; i++)
            {
                var angle = i / 7f * Mathf.PI * 2 + Next(-0.3f, 0.3f);
                var distance = i == 0 ? 0 : Next(0.25f, 0.6f);
                var foot = new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
                var height = Next(0.28f, 0.42f);

                // Two long leaves at the foot.
                for (var k = 0; k < 2; k++)
                {
                    var blade = Part(PrimitiveType.Sphere, root.transform, foot + Vector3.up * 0.05f, new Vector3(0.05f, 0.03f, 0.3f), leaf);
                    blade.localRotation = Quaternion.Euler(-12, Next(0, 360), 0);
                }

                Part(PrimitiveType.Cylinder, root.transform, foot + Vector3.up * height * 0.5f, new Vector3(0.015f, height * 0.5f, 0.015f), leaf);

                // The flower: crescent-curved petals round a pale heart, nodding a little.
                var bloom = new GameObject("Bloom").transform;
                bloom.SetParent(root.transform, false);
                bloom.localPosition = foot + Vector3.up * height;
                bloom.localRotation = Quaternion.Euler(Next(-20, 20), Next(0, 360), Next(-20, 20));
                bed.blooms.Add(bloom);
                Part(PrimitiveType.Sphere, bloom, Vector3.zero, Vector3.one * 0.035f, heart);

                for (var p = 0; p < 5; p++)
                {
                    var petal = Part(PrimitiveType.Sphere, bloom, Vector3.zero, new Vector3(0.05f, 0.012f, 0.1f), p % 2 == 0 ? blue : pink);
                    petal.localRotation = Quaternion.Euler(-25, p * 72, 0);
                    petal.localPosition = petal.localRotation * new Vector3(0, 0, 0.055f);
                }
            }

            return bed;
        }

        static Transform Part(PrimitiveType type, Transform parent, Vector3 at, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = at;
            part.transform.localScale = size;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return part.transform;
        }
    }
}
