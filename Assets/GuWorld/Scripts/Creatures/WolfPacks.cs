using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Creatures
{
    /// <summary>
    /// The wolf packs: dens in the forest round the hero's village, outside its walls, each with
    /// a pack that comes out when the hero is near (and goes, unseen, when they are far). A pack
    /// killed off comes back to its den after a while. Packs can also be called up beside the
    /// hero (the /wolves command).
    /// </summary>
    public class WolfPacks : MonoBehaviour
    {
        /// <summary>A pack comes out within this of its den, m; goes beyond that.</summary>
        const float ComeOut = 260;
        const float GoAway = 420;
        /// <summary>Seconds before a pack killed off is back at its den.</summary>
        const float Returns = 300;

        class Den
        {
            public Vector3 At;
            public int Size;
            public int Pack;
            public readonly List<Wolf> Wolves = new List<Wolf>();
            public float BackAt;
            /// <summary>Called up by the command: not refilled, forgotten once its wolves are gone.</summary>
            public bool Called;
        }

        readonly List<Den> dens = new List<Den>();
        Transform hero;
        float nextLook;
        int seed = 1;

        /// <summary>Where wolves are to be found from a point: the nearest living wolf, or else the nearest den (null: none).</summary>
        public Vector3? Nearest(Vector3 from)
        {
            Vector3? best = null;
            var bestDistance = float.PositiveInfinity;

            foreach (var den in dens)
            {
                foreach (var wolf in den.Wolves)
                {
                    if (wolf != null && (wolf.transform.position - from).sqrMagnitude < bestDistance)
                    {
                        bestDistance = (wolf.transform.position - from).sqrMagnitude;
                        best = wolf.transform.position;
                    }
                }
            }

            if (best.HasValue)
            {
                return best;
            }

            foreach (var den in dens)
            {
                if (!den.Called && (den.At - from).sqrMagnitude < bestDistance)
                {
                    bestDistance = (den.At - from).sqrMagnitude;
                    best = den.At;
                }
            }

            return best;
        }

        public static WolfPacks Create(Transform hero, IEnumerable<Vector3> denPoints, int packSize = 3)
        {
            var packs = new GameObject("Wolf Packs").AddComponent<WolfPacks>();
            packs.hero = hero;

            foreach (var at in denPoints)
            {
                packs.dens.Add(new Den { At = at, Size = packSize, Pack = packs.dens.Count + 1 });
            }

            return packs;
        }

        void Update()
        {
            if (hero == null || Time.time < nextLook)
            {
                return;
            }

            nextLook = Time.time + 1;
            var at = hero.position;

            for (var i = dens.Count - 1; i >= 0; i--)
            {
                var den = dens[i];
                den.Wolves.RemoveAll(w => w == null);
                var distance = Flat(at - den.At).magnitude;
                var living = den.Wolves.FindAll(w => !w.Dead);

                if (den.Called)
                {
                    if (den.Wolves.Count == 0)
                    {
                        dens.RemoveAt(i);
                    }

                    continue;
                }

                if (distance > GoAway)
                {
                    // Out of sight: back in the den (the living ones; the dead lie where they fell).
                    foreach (var wolf in living)
                    {
                        den.Wolves.Remove(wolf);
                        Destroy(wolf.gameObject);
                    }

                    continue;
                }

                if (living.Count == 0 && den.Wolves.Count == 0 && distance < ComeOut && Time.time >= den.BackAt)
                {
                    for (var k = 0; k < den.Size; k++)
                    {
                        var angle = k * Mathf.PI * 2 / den.Size;
                        Place(den, den.At + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 3);
                    }
                }
                else if (living.Count == 0 && den.Wolves.Count > 0)
                {
                    // All killed: the den empty for a while.
                    den.BackAt = Time.time + Returns;
                }
            }
        }

        /// <summary>A pack called up beside the hero (`count` wolves, 18–26 m off; toward a way, if given); how many came.</summary>
        public int CallNear(int count, Vector3? toward = null)
        {
            if (hero == null)
            {
                return 0;
            }

            var angle = toward.HasValue ? Mathf.Atan2(toward.Value.z, toward.Value.x) + (Random.value - 0.5f) * 0.6f : Random.value * Mathf.PI * 2;
            var at = hero.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (18 + Random.value * 8);
            var den = new Den { At = at, Size = count, Pack = 1000 + dens.Count, Called = true };
            dens.Add(den);

            for (var k = 0; k < count; k++)
            {
                var a = k * Mathf.PI * 2 / count;
                Place(den, at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 2.5f);
            }

            return den.Wolves.Count;
        }

        void Place(Den den, Vector3 at)
        {
            // On whatever is there: the ground, a rock (looked for from above).
            if (UnityEngine.Physics.Raycast(new Vector3(at.x, at.y + 60, at.z), Vector3.down, out var hit, 200, ~0, QueryTriggerInteraction.Ignore))
            {
                at = hit.point + Vector3.up * 0.05f;
            }
            else
            {
                at.y = GuWorld.World.QingMao.GroundAt(at) + 0.05f;
            }

            den.Wolves.Add(Wolf.Make(at, den.At, den.Pack, hero, seed++));
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
