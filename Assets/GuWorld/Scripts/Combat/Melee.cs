using System.Collections.Generic;
using GuWorld.Physics;
using UnityEngine;

namespace GuWorld.Combat
{
    /// <summary>
    /// The hero's own blows: a jab with the left fist, a cross with the right, a front kick with the right foot (the chain);
    /// and the special ones — a heavy uppercut (Q), a roundhouse kick that sweeps an arc (R).
    /// </summary>
    public enum Blow
    {
        Jab,
        Cross,
        Kick,
        Heavy,
        Spin,
    }

    /// <summary>
    /// Fighting with the body alone, once the fists are ready (their slot, F): the left mouse button a chain of
    /// blows — jab, cross, kick — each
    /// following on if the button comes again before the last is over, the chain starting again after a
    /// pause. A blow lands at its moment: whatever stands within the fist's or the foot's reach before the
    /// hero (at the hips for a kick, the chest for a fist) is struck — a body by its parts (HitZones), as the
    /// moonblade wounds; a wolf, a branch, a dummy as a whole (IStrikable). The fists are a youth's: they hurt
    /// common folk and wolves, and do nothing to a Gu Master far above the hero. Stronger with every rank.
    ///
    /// The right button held, the fists come up to guard the head: a blow from in front lands on the
    /// forearms, weakened (Health). Q throws a heavy uppercut, R a roundhouse kick that strikes everything in
    /// an arc before the hero; each rests a moment before it can be thrown again.
    /// </summary>
    [RequireComponent(typeof(HeroMotor))]
    public class Melee : MonoBehaviour
    {
        public struct Spec
        {
            /// <summary>How long the whole blow takes, s, and when in it the blow lands (0…1).</summary>
            public float Time, LandsAt;
            /// <summary>Its strength (Wounds: 100 a rank-one moonblade), its reach before the body (m, for a figure of 1.8 m) and the height it lands at.</summary>
            public float Power, Reach, Height;
        }

        public static Spec Of(Blow blow) => blow switch
        {
            Blow.Jab => new Spec { Time = 0.34f, LandsAt = 0.38f, Power = 40, Reach = 0.95f, Height = 1.45f },
            Blow.Cross => new Spec { Time = 0.42f, LandsAt = 0.4f, Power = 50, Reach = 1.0f, Height = 1.4f },
            Blow.Heavy => new Spec { Time = 0.66f, LandsAt = 0.52f, Power = 88, Reach = 1.0f, Height = 1.5f },
            Blow.Spin => new Spec { Time = 0.8f, LandsAt = 0.5f, Power = 70, Reach = 1.45f, Height = 1.1f },
            _ => new Spec { Time = 0.58f, LandsAt = 0.45f, Power = 64, Reach = 1.25f, Height = 0.95f },
        };

        /// <summary>How long a special blow rests before it can be thrown again, s (the chain's blows do not).</summary>
        public static float RestOf(Blow blow) => blow switch
        {
            Blow.Heavy => 1.4f,
            Blow.Spin => 2.2f,
            _ => 0,
        };

        /// <summary>A special blow (Q, R) rather than one of the chain's.</summary>
        public static bool IsSpecial(Blow blow) => blow == Blow.Heavy || blow == Blow.Spin;

        /// <summary>How wide the roundhouse sweeps, either side of straight ahead (degrees).</summary>
        const float SpinArc = 80;

        /// <summary>A pause this long after a blow, and the chain starts again from the jab.</summary>
        const float ChainBreak = 0.55f;
        /// <summary>How wide round the line of the blow it finds what it hits, m.</summary>
        const float Width = 0.28f;
        /// <summary>A blow that meets nothing at its own height looks this low too (for a grown-up), m: a wolf's back.</summary>
        const float LowHeight = 0.6f;

        static readonly RaycastHit[] casts = new RaycastHit[32];

        /// <summary>The blow under way (null: none), and how far through it, 0…1.</summary>
        public Blow? Current { get; private set; }
        public float Phase { get; private set; }
        /// <summary>Guarding the head with the forearms (the right button held).</summary>
        public bool Guarding { get; set; }
        /// <summary>Busy with a blow, or guarding: the body faces where the camera looks.</summary>
        public bool Busy => Current.HasValue || Guarding;
        /// <summary>The fists ready (their slot chosen): the mouse fights with them, the stance is a fighter's.</summary>
        public bool Ready;
        /// <summary>The blows' strength scaled by this (the rank's).</summary>
        public float Strength = 1;
        /// <summary>The figure's size against a grown-up's 1.8 m (the reach and the heights shrink with it).</summary>
        public float Scale = 1;

        /// <summary>A blow landed on something (what it was, where): for the HUD's word and the camera's jolt.</summary>
        public event System.Action<Collider, Vector3> Hit;
        /// <summary>A blow began (the figure swings into it).</summary>
        public event System.Action<Blow> Swung;

        /// <summary>Seconds since the last blow was over (the fists stay up a while).</summary>
        public float SinceBlow => Current.HasValue ? 0 : sinceLast;

        HeroMotor body;
        int next;
        bool queued;
        bool landed;
        float sinceLast = 99;
        readonly float[] restUntil = new float[5];

        /// <summary>How long before a special blow can be thrown again, s (0: now).</summary>
        public float RestLeft(Blow blow) => Mathf.Max(0, restUntil[(int)blow] - Time.time);

        /// <summary>A special blow (Q: the uppercut, R: the roundhouse), if the hero is free and it has rested; false when not thrown.</summary>
        public bool Special(Blow blow)
        {
            if (!body.Free || body.Stance == Stance.Crawl || Current.HasValue || RestLeft(blow) > 0)
            {
                return false;
            }

            Begin(blow);
            restUntil[(int)blow] = Time.time + RestOf(blow);

            return true;
        }

        void Awake()
        {
            body = GetComponent<HeroMotor>();
        }

        /// <summary>The button: a blow now, or the next one in the chain once this one has landed.</summary>
        public void Press()
        {
            if (!body.Free || body.Stance == Stance.Crawl)
            {
                return;
            }

            if (Current.HasValue)
            {
                queued = true;

                return;
            }

            if (sinceLast > ChainBreak)
            {
                next = 0;
            }

            Begin((Blow)next);
        }

        /// <summary>A blow of one's choosing at once (a check's: -blow).</summary>
        public void Launch(Blow blow)
        {
            if (body.Free)
            {
                Begin(blow);
            }
        }

        /// <summary>Drops whatever blow is under way (the hero fell, climbed, sat down).</summary>
        public void Stop()
        {
            Current = null;
            queued = false;
            Guarding = false;
        }

        void Begin(Blow blow)
        {
            Current = blow;
            Phase = 0;
            landed = false;
            queued = false;
            // The chain goes on from a chain's blow; after a special one it starts again.
            next = IsSpecial(blow) ? 0 : ((int)blow + 1) % 3;
            Swung?.Invoke(blow);
        }

        void Update()
        {
            var dt = Time.deltaTime;

            if (dt <= 0)
            {
                return;
            }

            if (!body.Free)
            {
                Stop();
            }

            if (!Current.HasValue)
            {
                sinceLast += dt;

                return;
            }

            var spec = Of(Current.Value);
            Phase += dt / spec.Time;

            if (!landed && Phase >= spec.LandsAt)
            {
                landed = true;
                Land(Current.Value, spec);
            }

            // The next blow follows on once this one is most of the way back.
            if (queued && Phase >= 0.7f)
            {
                Begin((Blow)next);

                return;
            }

            if (Phase >= 1)
            {
                Current = null;
                sinceLast = 0;
            }
        }

        bool Mine(Collider collider) => collider.transform == transform || collider.transform.IsChildOf(transform);

        /// <summary>The roundhouse's moment: everything within its reach and its arc struck, each body once, where it is most open.</summary>
        void Sweep(Spec spec)
        {
            var forward = transform.forward;
            var centre = transform.position + Vector3.up * spec.Height * Scale;
            var reach = spec.Reach * Scale;
            var limit = Mathf.Cos(SpinArc * Mathf.Deg2Rad);
            var bodies = new Dictionary<IStrikable, (HitZone zone, float distance)>();
            var things = new Dictionary<IStrikable, Collider>();

            bool InArc(Vector3 point)
            {
                var to = point - transform.position;
                to.y = 0;

                return to.sqrMagnitude < 1e-4f || Vector3.Dot(to.normalized, forward) >= limit;
            }

            foreach (var collider in UnityEngine.Physics.OverlapSphere(centre, reach, ~0, QueryTriggerInteraction.Collide))
            {
                if (Mine(collider))
                {
                    continue;
                }

                var point = collider.ClosestPoint(centre);

                if (!InArc(point))
                {
                    continue;
                }

                if (collider.isTrigger)
                {
                    // A body's part: the most vital one the foot reaches, for each body.
                    if (collider.TryGetComponent<HitZone>(out var zone) && zone.Owner != null)
                    {
                        var distance = (point - centre).magnitude;

                        if (!bodies.TryGetValue(zone.Owner, out var best) || Wounds.Of(zone.Part).Priority > Wounds.Of(best.zone.Part).Priority)
                        {
                            bodies[zone.Owner] = (zone, distance);
                        }
                    }

                    continue;
                }

                if (collider.GetComponentInParent<IZoned>() == null && collider.GetComponentInParent<IStrikable>() is IStrikable thing)
                {
                    things[thing] = collider;
                }
            }

            var strike = new Strike { Power = spec.Power * Strength, Sharpness = 1, Source = gameObject };

            foreach (var (zone, _) in bodies.Values)
            {
                strike.Point = zone.Shape.ClosestPoint(centre);
                strike.Direction = Flat(strike.Point - transform.position);
                zone.Owner.TakeStrike(strike, zone.Part);
                Hit?.Invoke(zone.Shape, strike.Point);
            }

            foreach (var pair in things)
            {
                strike.Point = pair.Value.ClosestPoint(centre);
                strike.Direction = Flat(strike.Point - transform.position);
                pair.Key.TakeStrike(strike, null);
                Hit?.Invoke(pair.Value, strike.Point);
            }
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0;

            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        /// <summary>The blow's moment: the nearest thing along its line, within reach, struck (the roundhouse: all in its arc).</summary>
        void Land(Blow blow, Spec spec)
        {
            if (blow == Blow.Spin)
            {
                Sweep(spec);

                return;
            }

            var forward = transform.forward;
            var from = transform.position + Vector3.up * spec.Height * Scale;
            var reach = spec.Reach * Scale;
            // From a little behind the shoulder, so a thing pressed against the hero is still found.
            var start = from - forward * 0.15f;

            // The parts of bodies (triggers), and solid things — a wolf, a branch, a wall that stops the fist.
            var met = new List<(HitZone, float)>();
            Collider solid = null;
            var solidAt = float.PositiveInfinity;
            var solidPoint = Vector3.zero;

            var count = UnityEngine.Physics.SphereCastNonAlloc(start, Width * Scale, forward, casts, reach + 0.15f, ~0, QueryTriggerInteraction.Collide);

            for (var i = 0; i < count; i++)
            {
                var hit = casts[i];

                if (Mine(hit.collider))
                {
                    continue;
                }

                if (hit.collider.isTrigger)
                {
                    if (hit.collider.TryGetComponent<HitZone>(out var zone))
                    {
                        met.Add((zone, hit.distance));
                    }

                    continue;
                }

                // A person's own capsule is not their body: only their parts count.
                if (hit.collider.GetComponentInParent<IZoned>() != null || hit.collider is TerrainCollider)
                {
                    continue;
                }

                if (hit.distance < solidAt)
                {
                    solidAt = hit.distance;
                    solid = hit.collider;
                    solidPoint = hit.distance <= 0 && hit.point == Vector3.zero ? from + forward * 0.3f : hit.point;
                }
            }

            // Nothing to strike at the blow's own height (air, or a wall): a low body before the hero — a wolf — is struck
            // instead, the fist going down to it, unless the wall is nearer.
            if (met.Count == 0 && (solid == null || solid.GetComponentInParent<IStrikable>() == null))
            {
                var low = transform.position + Vector3.up * LowHeight * Scale - forward * 0.15f;
                count = UnityEngine.Physics.SphereCastNonAlloc(low, Width * Scale, forward, casts, reach + 0.15f, ~0, QueryTriggerInteraction.Ignore);

                for (var i = 0; i < count; i++)
                {
                    var hit = casts[i];

                    if (!Mine(hit.collider) && hit.distance < solidAt && hit.collider.GetComponentInParent<IZoned>() == null &&
                        hit.collider.GetComponentInParent<IStrikable>() != null)
                    {
                        solidAt = hit.distance;
                        solid = hit.collider;
                        solidPoint = hit.distance <= 0 && hit.point == Vector3.zero ? low + forward * 0.3f : hit.point;
                    }
                }
            }

            var strike = new Strike
            {
                Power = spec.Power * Strength,
                // Blunt: no edge to lose.
                Sharpness = 1,
                Direction = forward,
                Source = gameObject,
            };
            var chosen = HitZones.Choose(met, 0.12f);
            var zoneAt = float.PositiveInfinity;

            foreach (var (zone, distance) in met)
            {
                if (zone == chosen)
                {
                    zoneAt = Mathf.Min(zoneAt, distance);
                }
            }

            if (chosen != null && zoneAt <= solidAt)
            {
                var point = chosen.Shape.ClosestPoint(start + forward * zoneAt);
                strike.Point = float.IsFinite(point.x) ? point : chosen.Shape.bounds.center;
                chosen.Owner?.TakeStrike(strike, chosen.Part);
                Hit?.Invoke(chosen.Shape, strike.Point);
            }
            else if (solid != null)
            {
                strike.Point = solidPoint;

                if (solid.GetComponentInParent<IStrikable>() is IStrikable target)
                {
                    target.TakeStrike(strike, null);
                }

                Hit?.Invoke(solid, solidPoint);
            }
        }

        /// <summary>A blow from in front, while guarding, lands on a forearm: this much of it gets through.</summary>
        public const float GuardLets = 0.35f;

        /// <summary>Whether a blow coming this way (its direction of travel) is one the guard meets: from in front.</summary>
        public bool Meets(Vector3 direction)
        {
            if (!Guarding)
            {
                return false;
            }

            direction.y = 0;

            // A blow with no way it comes (from above, from within) is not one the forearms meet.
            return direction.sqrMagnitude >= 1e-4f && Vector3.Dot(-direction.normalized, transform.forward) > 0.3f;
        }
    }
}
