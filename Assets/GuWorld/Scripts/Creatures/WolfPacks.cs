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
            /// <summary>The wolves that live there, by kind (a king among its pack).</summary>
            public BeastKind[] Kinds;
            public int Pack;
            public readonly List<Beast> Wolves = new List<Beast>();
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

        /// <summary>The packs of the dens given: where each den is and who lives there.</summary>
        public static WolfPacks Create(Transform hero, IEnumerable<(Vector3 at, BeastKind[] kinds)> denPoints)
        {
            var packs = new GameObject("Beast Packs").AddComponent<WolfPacks>();
            packs.hero = hero;

            foreach (var (at, kinds) in denPoints)
            {
                packs.dens.Add(new Den { At = at, Kinds = kinds, Pack = packs.dens.Count + 1 });
            }

            return packs;
        }

        /// <summary>A pack of `count` of a kind, and — for a beast king — its escort of the kind below it (a boar king's sounder, a monkey king's troop).</summary>
        public static BeastKind[] PackOf(BeastKind kind, int count = 1)
        {
            var pack = new List<BeastKind>();

            for (var i = 0; i < count; i++)
            {
                pack.Add(kind);
            }

            var escort = kind.Shape switch
            {
                BeastShape.Boar when kind.Tier > 0 => (BeastKinds.Boar, 4),
                BeastShape.Monkey when kind.Tier > 0 => (BeastKinds.Monkey, 5),
                BeastShape.Wolf => kind.Tier switch
                {
                    1 => (WolfKinds.Lightning, 3),
                    2 => (WolfKinds.Lightning, 4),
                    3 => (WolfKinds.Bold, 2),
                    _ => (null, 0),
                },
                _ => (null, 0),
            };

            for (var i = 0; i < escort.Item2; i++)
            {
                pack.Add(escort.Item1);
            }

            if (kind.Tier == 3)
            {
                for (var i = 0; i < 4; i++)
                {
                    pack.Add(WolfKinds.Lightning);
                }
            }

            return pack.ToArray();
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
                    for (var k = 0; k < den.Kinds.Length; k++)
                    {
                        var angle = k * Mathf.PI * 2 / den.Kinds.Length;
                        Place(den, den.At + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (3 + 1.5f * den.Kinds[k].Scale), den.Kinds[k]);
                    }
                }
                else if (living.Count == 0 && den.Wolves.Count > 0)
                {
                    // All killed: the den empty for a while.
                    den.BackAt = Time.time + Returns;
                }
            }
        }

        /// <summary>A pack called up beside the hero (`count` grey wolves, 18–26 m off; toward a way, if given); how many came.</summary>
        public int CallNear(int count, Vector3? toward = null) => CallNear(PackOf(WolfKinds.Grey, count), toward);

        /// <summary>A pack of these wolves called up beside the hero (further off for the bigger ones); how many came.</summary>
        public int CallNear(BeastKind[] kinds, Vector3? toward = null)
        {
            if (hero == null || kinds.Length == 0)
            {
                return 0;
            }

            var biggest = 1f;

            foreach (var kind in kinds)
            {
                biggest = Mathf.Max(biggest, kind.Scale);
            }

            var angle = toward.HasValue ? Mathf.Atan2(toward.Value.z, toward.Value.x) + (Random.value - 0.5f) * 0.6f : Random.value * Mathf.PI * 2;
            var at = hero.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (16 + 4 * biggest + Random.value * 8);
            var den = new Den { At = at, Kinds = kinds, Pack = 1000 + dens.Count, Called = true };
            dens.Add(den);

            for (var k = 0; k < kinds.Length; k++)
            {
                var a = k * Mathf.PI * 2 / kinds.Length;
                Place(den, at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (k == 0 ? 0 : 2.5f + kinds[0].Scale), kinds[k]);
            }

            return den.Wolves.Count;
        }

        void Place(Den den, Vector3 at, BeastKind kind)
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

            den.Wolves.Add(Beast.Make(at, den.At, den.Pack, hero, seed++, kind));
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
