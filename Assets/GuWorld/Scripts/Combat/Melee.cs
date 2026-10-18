using System.Collections.Generic;
using GuWorld.Physics;
using UnityEngine;

namespace GuWorld.Combat
{
    /// <summary>
    /// The hero's own blows: the chain — a jab with the left fist, a cross with the right, a hook with the left, and
    /// a front kick to finish (it throws a beast back); and the special ones — a heavy uppercut (Q) that lifts, a
    /// roundhouse (R) that turns the whole body round and sweeps everything about it.
    /// </summary>
    public enum Blow
    {
        Jab,
        Cross,
        Kick,
        Heavy,
        Spin,
        Hook,
    }

    /// <summary>
    /// Fighting with the body alone, once the fists are ready (F): the left mouse button a chain of blows — jab,
    /// cross, hook, kick — each following on if the button comes again before the last is over, the chain starting
    /// again after a pause. Every blow is wound up, thrown and drawn back (the figure's poses: Mannequin), and
    /// carries the body a step into it; it turns toward the nearest thing to hit before the hero (within a few
    /// metres and the way the camera looks), so a blow meant for a wolf finds it. It lands at its moment: whatever
    /// stands within the fist's or the foot's reach is struck — a body by its parts (HitZones), a wolf, a branch, a
    /// dummy as a whole (IStrikable); a hook and the roundhouse sweep an arc. The kick and the uppercut throw a beast
    /// back. The fists are a youth's: they hurt common folk and wolves, and do nothing to a Gu Master far above the
    /// hero. Stronger with every rank, and with strength Gu.
    ///
    /// The right button held, the fists come up to guard the head: a blow from in front lands on the forearms,
    /// weakened (Health). Q and R rest a moment before they can be thrown again.
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
            /// <summary>How far the body goes into it, m; how wide it sweeps either side (degrees, 0: straight); how hard it throws a beast back, m/s.</summary>
            public float Lunge, Arc, Knock;
        }

        public static Spec Of(Blow blow) => blow switch
        {
            Blow.Jab => new Spec { Time = 0.36f, LandsAt = 0.42f, Power = 40, Reach = 1.0f, Height = 1.45f, Lunge = 0.3f },
            Blow.Cross => new Spec { Time = 0.42f, LandsAt = 0.45f, Power = 52, Reach = 1.05f, Height = 1.4f, Lunge = 0.4f },
            Blow.Hook => new Spec { Time = 0.46f, LandsAt = 0.5f, Power = 60, Reach = 1.05f, Height = 1.42f, Lunge = 0.25f, Arc = 70 },
            Blow.Heavy => new Spec { Time = 0.7f, LandsAt = 0.55f, Power = 105, Reach = 1.05f, Height = 1.45f, Lunge = 0.55f, Knock = 6 },
            Blow.Spin => new Spec { Time = 0.8f, LandsAt = 0.55f, Power = 82, Reach = 1.6f, Height = 1.1f, Lunge = 0.2f, Arc = 180, Knock = 7 },
            _ => new Spec { Time = 0.62f, LandsAt = 0.5f, Power = 78, Reach = 1.35f, Height = 0.95f, Lunge = 0.45f, Knock = 8 },
        };

        /// <summary>The chain the left button throws, in order (the kick ends it).</summary>
        public static readonly Blow[] Chain = { Blow.Jab, Blow.Cross, Blow.Hook, Blow.Kick };

        /// <summary>How long a special blow rests before it can be thrown again, s (the chain's blows do not).</summary>
        public static float RestOf(Blow blow) => blow switch
        {
            Blow.Heavy => 1.4f,
            Blow.Spin => 2.2f,
            _ => 0,
        };

        /// <summary>A special blow (Q, R) rather than one of the chain's.</summary>
        public static bool IsSpecial(Blow blow) => blow == Blow.Heavy || blow == Blow.Spin;

        /// <summary>The blow's name, as the HUD tells it.</summary>
        public static string Name(Blow blow) => blow switch
        {
            Blow.Jab => "Джеб",
            Blow.Cross => "Кросс",
            Blow.Hook => "Хук",
            Blow.Kick => "Пинок",
            Blow.Heavy => "Апперкот",
            _ => "Разворот",
        };

        /// <summary>A pause this long after a blow, and the chain starts again from the jab.</summary>
        const float ChainBreak = 0.6f;
        /// <summary>How wide round the line of the blow it finds what it hits, m.</summary>
        const float Width = 0.3f;
        /// <summary>A blow that meets nothing at its own height looks this low too (for a grown-up), m: a wolf's back.</summary>
        const float LowHeight = 0.6f;
        /// <summary>How far a blow looks for something to turn to, m, and how far round from where the camera looks (degrees).</summary>
        const float LockReach = 4.5f;
        const float LockArc = 75;
        /// <summary>The share of a blow, before it lands, in which the body goes into it.</summary>
        const float LungeSpan = 0.3f;

        static readonly RaycastHit[] casts = new RaycastHit[32];

        /// <summary>The blow under way (null: none), and how far through it, 0…1.</summary>
        public Blow? Current { get; private set; }
        public float Phase { get; private set; }
        /// <summary>Where the chain is: the next of its blows (0…3).</summary>
        public int ChainStep => next;
        /// <summary>Guarding the head with the forearms (the right button held).</summary>
        public bool Guarding
        {
            get => guarding;
            set
            {
                if (value && !guarding)
                {
                    guardSince = Time.time;
                }

                guarding = value;
            }
        }

        bool guarding;
        float guardSince = -100;

        /// <summary>How soon after the guard comes up a blow is parried (turned aside whole), s.</summary>
        public const float ParryWindow = 0.3f;

        /// <summary>The guard just raised: a blow from in front now is parried.</summary>
        public bool Parrying => guarding && Time.time - guardSince < ParryWindow;

        /// <summary>A blow parried (where, and who struck it): for the spark, the word and the moment's stillness.</summary>
        public event System.Action<Vector3, GameObject> Parried;

        /// <summary>A blow turned aside whole: the one who struck it reels (a beast king less), and the next blow is ready at once.</summary>
        public void Parry(Strike strike)
        {
            if (strike.Source != null && strike.Source.GetComponentInParent<IStaggerable>() is IStaggerable staggered)
            {
                staggered.Stagger(1.4f, Flat(strike.Source.transform.position - transform.position));
            }

            sinceLast = ChainBreak;
            Parried?.Invoke(strike.Point, strike.Source);
        }
        /// <summary>Busy with a blow, or guarding: the body faces where the camera looks (or the thing it turned to).</summary>
        public bool Busy => Current.HasValue || Guarding;
        /// <summary>The fists ready (F): the mouse fights with them, the stance is a fighter's.</summary>
        public bool Ready;
        /// <summary>The blows' strength scaled by this (the rank's, strength Gu's).</summary>
        public float Strength = 1;
        /// <summary>The figure's size against a grown-up's 1.8 m (the reach and the heights shrink with it).</summary>
        public float Scale = 1;
        /// <summary>Where the camera looks, on the ground: a blow turns to what lies that way.</summary>
        public Vector3 AimForward = Vector3.forward;
        /// <summary>The thing the blow under way turned to (null: none — it goes where the camera looks).</summary>
        public Transform Target { get; private set; }

        /// <summary>A blow landed on something (what it was, where): for the HUD, the camera's jolt, the spark.</summary>
        public event System.Action<Collider, Vector3> Hit;
        /// <summary>A blow began (the figure swings into it).</summary>
        public event System.Action<Blow> Swung;

        /// <summary>Seconds since the last blow was over (the fists stay up a while).</summary>
        public float SinceBlow => Current.HasValue ? 0 : sinceLast;

        HeroMotor body;
        CharacterController solid;
        int next;
        bool queued;
        bool landed;
        float sinceLast = 99;
        float lunged;
        readonly float[] restUntil = new float[6];

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
            solid = GetComponent<CharacterController>();
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

            Begin(Chain[next]);
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
            Target = null;
            queued = false;
            Guarding = false;
        }

        void Begin(Blow blow)
        {
            Current = blow;
            Phase = 0;
            landed = false;
            queued = false;
            lunged = 0;
            // The chain goes on from a chain's blow; after a special one, or the kick, it starts again.
            var at = System.Array.IndexOf(Chain, blow);
            next = at < 0 ? 0 : (at + 1) % Chain.Length;
            Target = FindTarget();
            Swung?.Invoke(blow);
        }

        /// <summary>The nearest thing to hit before the hero — within reach and the way the camera looks — or null.</summary>
        Transform FindTarget()
        {
            var forward = Flat(AimForward);
            var limit = Mathf.Cos(LockArc * Mathf.Deg2Rad);
            Transform best = null;
            var bestScore = float.PositiveInfinity;

            foreach (var collider in UnityEngine.Physics.OverlapSphere(transform.position + Vector3.up, LockReach * Scale, ~0, QueryTriggerInteraction.Collide))
            {
                if (Mine(collider))
                {
                    continue;
                }

                var thing = collider.TryGetComponent<HitZone>(out var zone) ? zone.Owner : collider.isTrigger ? null : collider.GetComponentInParent<IStrikable>();

                if (!(thing is Component owner) || owner.transform == transform)
                {
                    continue;
                }

                if (owner.TryGetComponent<GuWorld.Creatures.Wolf>(out var wolf) && (wolf.Dead || wolf.Tamed))
                {
                    continue;
                }

                var to = owner.transform.position - transform.position;
                to.y = 0;
                var distance = to.magnitude;

                if (distance < 0.05f)
                {
                    continue;
                }

                var facing = Vector3.Dot(to / distance, forward);

                if (facing < limit)
                {
                    continue;
                }

                // Near and straight ahead first.
                var score = distance + (1 - facing) * 4;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = owner.transform;
                }
            }

            return best;
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
            var before = Phase;
            Phase += dt / spec.Time;
            Face();
            Lunge(spec, before);

            if (!landed && Phase >= spec.LandsAt)
            {
                landed = true;
                Land(Current.Value, spec);
            }

            // The next blow follows on once this one is most of the way back.
            if (queued && Phase >= 0.72f)
            {
                Begin(Chain[next]);

                return;
            }

            if (Phase >= 1)
            {
                Current = null;
                Target = null;
                sinceLast = 0;
            }
        }

        /// <summary>Turned toward the thing the blow is meant for.</summary>
        void Face()
        {
            if (Target == null)
            {
                return;
            }

            var to = Target.position - transform.position;
            to.y = 0;

            if (to.sqrMagnitude < 0.01f)
            {
                return;
            }

            var wanted = Mathf.Atan2(to.x, to.z);
            body.Facing = Mathf.LerpAngle(body.Facing * Mathf.Rad2Deg, wanted * Mathf.Rad2Deg, 1 - Mathf.Exp(-25 * Time.deltaTime)) * Mathf.Deg2Rad;
            transform.rotation = Quaternion.Euler(0, body.Facing * Mathf.Rad2Deg, 0);
        }

        /// <summary>The body carried into the blow as it is thrown — up to its reach of the thing it is meant for, not into it.</summary>
        void Lunge(Spec spec, float before)
        {
            var start = spec.LandsAt - LungeSpan;
            var share = Mathf.Clamp01((Phase - start) / LungeSpan) - Mathf.Clamp01((before - start) / LungeSpan);

            if (share <= 0 || spec.Lunge <= 0 || solid == null || !solid.enabled)
            {
                return;
            }

            var step = spec.Lunge * Scale * share;

            if (Target != null)
            {
                var to = Target.position - transform.position;
                to.y = 0;
                step = Mathf.Min(step, Mathf.Max(0, to.magnitude - spec.Reach * Scale * 0.75f));
            }

            if (step > 0.001f)
            {
                solid.Move(transform.forward * step);
                lunged += step;
            }
        }

        bool Mine(Collider collider) => collider.transform == transform || collider.transform.IsChildOf(transform);

        /// <summary>A sweeping blow's moment (the hook, the roundhouse): everything within its reach and its arc struck, each body once, where it is most open.</summary>
        void Sweep(Spec spec)
        {
            var forward = transform.forward;
            var centre = transform.position + Vector3.up * spec.Height * Scale;
            var reach = spec.Reach * Scale;
            var limit = Mathf.Cos(spec.Arc * Mathf.Deg2Rad);
            var bodies = new Dictionary<IStrikable, (HitZone zone, float distance)>();
            var things = new Dictionary<IStrikable, Collider>();

            bool InArc(Vector3 point)
            {
                var to = point - transform.position;
                to.y = 0;

                return spec.Arc >= 180 || to.sqrMagnitude < 1e-4f || Vector3.Dot(to.normalized, forward) >= limit;
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
                    // A body's part: the most vital one the blow reaches, for each body.
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
                Knock(pair.Key, strike.Direction, spec);
                Hit?.Invoke(pair.Value, strike.Point);
            }
        }

        /// <summary>A beast thrown back by a blow that can (the kick, the uppercut, the roundhouse).</summary>
        static void Knock(IStrikable target, Vector3 direction, Spec spec)
        {
            if (spec.Knock > 0 && target is GuWorld.Creatures.Wolf wolf && !wolf.Dead)
            {
                wolf.Push(Flat(direction) * spec.Knock + Vector3.up * spec.Knock * 0.35f);
            }
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0;

            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        /// <summary>The blow's moment: the nearest thing along its line, within reach, struck (a sweeping blow: all in its arc).</summary>
        void Land(Blow blow, Spec spec)
        {
            if (spec.Arc > 0)
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
            Collider solidHit = null;
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
                    solidHit = hit.collider;
                    solidPoint = hit.distance <= 0 && hit.point == Vector3.zero ? from + forward * 0.3f : hit.point;
                }
            }

            // Nothing to strike at the blow's own height (air, or a wall): a low body before the hero — a wolf — is struck
            // instead, the fist going down to it, unless the wall is nearer.
            if (met.Count == 0 && (solidHit == null || solidHit.GetComponentInParent<IStrikable>() == null))
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
                        solidHit = hit.collider;
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
            else if (solidHit != null)
            {
                strike.Point = solidPoint;

                if (solidHit.GetComponentInParent<IStrikable>() is IStrikable target)
                {
                    target.TakeStrike(strike, null);
                    Knock(target, forward, spec);
                }

                Hit?.Invoke(solidHit, solidPoint);
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
