using System;
using System.Collections.Generic;
using GuWorld.Combat;
using UnityEngine;

namespace GuWorld.World
{
    /// <summary>
    /// A branch of a giant tree: a limb thick enough to walk on near the trunk and
    /// thinning out to its tip. It can be cut through (a moonblade) — a thick one takes
    /// many cuts — and a thin stretch of it cracks under one's weight if one stands
    /// there too long. Broken, it falls with whatever it carried.
    /// </summary>
    public class TreeBranch : MonoBehaviour, IStrikable
    {
        /// <summary>Where it leaves the trunk and where it ends, in its own frame (along +Z).</summary>
        public float Length = 8;
        public float RootThickness = 0.8f;
        public float TipThickness = 0.25f;
        public float Health = 300;
        public ClimbTree Tree;

        /// <summary>It broke: what, why.</summary>
        public static event Action<TreeBranch, string> Snapped;
        /// <summary>It is cracking under someone.</summary>
        public static event Action<TreeBranch> Creaking;

        public bool Broken { get; private set; }
        public Vector3 Root => transform.position;
        public Vector3 Tip => transform.TransformPoint(new Vector3(0, 0, Length));

        float strain;
        bool warned;
        readonly System.Random random = new System.Random();

        /// <summary>How thick it is a distance along it from the trunk.</summary>
        public float ThicknessAt(float along) => Mathf.Lerp(RootThickness, TipThickness, Mathf.Clamp01(along / Mathf.Max(0.1f, Length)));

        /// <summary>How far along it (from the trunk) a point lies.</summary>
        public float Along(Vector3 at) => Mathf.Clamp(transform.InverseTransformPoint(at).z, 0, Length);

        /// <summary>Someone stands on it at `at` for `dt` seconds: a thin stretch strains, cracks, and gives.</summary>
        public void Bear(Vector3 at, float dt)
        {
            if (Broken)
            {
                return;
            }

            var thickness = ThicknessAt(Along(at));

            if (thickness > 0.42f)
            {
                strain = Mathf.Max(0, strain - dt);
                warned = false;

                return;
            }

            // The thinner, the sooner.
            strain += dt * Mathf.Lerp(1.6f, 0.4f, Mathf.InverseLerp(0.2f, 0.42f, thickness));

            if (strain > 1.2f && !warned)
            {
                warned = true;
                Creaking?.Invoke(this);
            }

            if (strain > 3)
            {
                Snap("под тяжестью");
            }
        }

        public void TakeStrike(Strike strike, BodyPart? part)
        {
            if (Broken)
            {
                return;
            }

            // A thick limb takes many cuts; past its reach the blade barely bites.
            var bite = strike.Power * Mathf.Clamp01(strike.Sharpness <= 0 ? 1 : strike.Sharpness) * (0.85f + 0.3f * (float)random.NextDouble());
            Health -= bite;

            if (Health <= 0)
            {
                Snap("перерублена");
            }
            else
            {
                Creaking?.Invoke(this);
            }
        }

        /// <summary>Breaks off and falls (everything on it falls too: it was holding them).</summary>
        public void Snap(string why)
        {
            if (Broken)
            {
                return;
            }

            Broken = true;
            var parent = transform.parent;

            // Out of its tree's level of detail: it is its own thing now.
            var group = GetComponentInParent<LODGroup>();

            if (group != null)
            {
                var mine = new HashSet<Renderer>(GetComponentsInChildren<Renderer>());
                var lods = group.GetLODs();

                for (var i = 0; i < lods.Length; i++)
                {
                    lods[i].renderers = Array.FindAll(lods[i].renderers, r => r != null && !mine.Contains(r));
                }

                group.SetLODs(lods);
            }

            transform.SetParent(null, true);
            var body = gameObject.AddComponent<Rigidbody>();
            body.mass = Mathf.Max(30, Length * RootThickness * RootThickness * 400);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // It tips over from where it was held, the tip going down first.
            body.AddForceAtPosition(Vector3.down * body.mass * 3, Tip, ForceMode.Impulse);

            foreach (var tag in GetComponentsInChildren<Render.HitboxTag>())
            {
                tag.Kind = Render.HitboxKind.Moving;
            }

            Snapped?.Invoke(this, why);
            Destroy(gameObject, 120);
        }
    }
}
