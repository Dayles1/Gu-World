using System.Collections.Generic;
using GuWorld.Combat;
using UnityEngine;

namespace GuWorld.Cultivation
{
    /// <summary>
    /// A Gu's missile in flight — a bone spear, a star dart, ghost fire, an icicle: a ball
    /// of light in the Gu's colour, trailing light, flying straight. It strikes the first
    /// body part or solid thing it meets (a body is struck where it is met, as a moonblade
    /// strikes), does to it what its Gu does besides (burns, slows, holds, enslaves), and —
    /// a Gu with a blast — bursts there. Past its reach it is half as strong; it fades at
    /// the end of its range. It never strikes its thrower.
    /// </summary>
    public class GuBolt : MonoBehaviour, IMissile
    {
        const float Speed = 32;
        const float SolidRadius = 0.2f;
        const float BodyRadius = 0.32f;

        GuKind kind;
        Vector3 direction;
        float left;
        float flown;
        float power;
        Collider thrower;
        Material material;
        Light shine;
        TrailRenderer trail;
        float fading = -1;
        /// <summary>A shape that spins about its flight as it goes (a star).</summary>
        Transform spin;

        /// <summary>The last missile thrown (the checks follow it).</summary>
        public static GuBolt Last { get; private set; }

        public bool Flying => fading < 0;
        public Vector3 Heading => direction;
        public Collider Thrower => thrower;

        void OnEnable() => Missiles.All.Add(this);
        void OnDisable() => Missiles.All.Remove(this);

        public static GuBolt Throw(Vector3 from, Vector3 direction, GuKind kind, Collider thrower, float power)
        {
            var go = new GameObject("Gu Bolt " + kind.Id);
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(direction, Vector3.up));
            var bolt = go.AddComponent<GuBolt>();
            bolt.kind = kind;
            bolt.direction = direction.normalized;
            bolt.left = Mathf.Max(kind.Range, kind.Reach, 4);
            bolt.power = power * Battle.Scale(kind);
            bolt.thrower = thrower;
            // Its own shape (GuShapes): a spear, a star, a tongue of fire, a shard of ice — as its aim showed it.
            var shape = GuShapes.Size(kind);
            var size = Mathf.Max(shape.x, shape.y);
            var ball = new GameObject("Shape");
            ball.transform.SetParent(go.transform, false);
            ball.transform.localScale = shape;
            ball.AddComponent<MeshFilter>().sharedMesh = GuShapes.For(kind);
            bolt.spin = GuShapes.Spins(kind) ? ball.transform : null;
            bolt.material = GuSkills.Glow(kind.Colour);
            bolt.material.color = kind.Colour * 2.2f;
            var renderer = ball.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = bolt.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bolt.shine = go.AddComponent<Light>();
            bolt.shine.type = LightType.Point;
            bolt.shine.color = kind.Colour;
            bolt.shine.range = 5 + kind.Rank;
            bolt.shine.intensity = 2;
            bolt.shine.shadows = LightShadows.None;
            bolt.trail = go.AddComponent<TrailRenderer>();
            bolt.trail.time = 0.18f;
            bolt.trail.minVertexDistance = 0.12f;
            bolt.trail.widthCurve = new AnimationCurve(new Keyframe(0, size * 0.9f), new Keyframe(1, 0));
            bolt.trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(kind.Colour, 0), new GradientColorKey(kind.Colour, 1) },
                alphaKeys = new[] { new GradientAlphaKey(0.8f, 0), new GradientAlphaKey(0, 1) },
            };
            bolt.trail.sharedMaterial = bolt.material;
            bolt.trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Last = bolt;

            return bolt;
        }

        void Update() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            if (dt <= 0 || this == null)
            {
                return;
            }

            if (fading >= 0)
            {
                fading += dt / 0.15f;
                var a = Mathf.Clamp01(1 - fading);
                material.color = kind.Colour * 2.2f * a;
                shine.intensity = 2 * a;

                if (fading >= 1)
                {
                    Destroy(material);
                    Destroy(gameObject);
                }

                return;
            }

            if (spin != null)
            {
                spin.localRotation *= Quaternion.Euler(0, 0, 720 * dt);
            }

            var step = Mathf.Min(Speed * dt, left);

            if (Hit(step, out var point, out var zone, out var solid))
            {
                transform.position = point;
                Land(point, zone, solid);
                Spend();

                return;
            }

            transform.position += direction * step;
            left -= step;
            flown += step;

            if (left <= 0.001f)
            {
                // A blast goes off where it ends, too.
                if (kind.Radius > 0)
                {
                    GuSkills.Blast(transform.position, kind.Radius, power * 0.7f, kind, thrower);
                }

                Spend();
            }
        }

        void Land(Vector3 point, HitZone zone, Collider solid)
        {
            var target = zone != null ? zone.Owner : solid != null ? solid.GetComponentInParent<IStrikable>() : null;

            // An enslaving Gu does not wound: it takes the beast's will.
            if (kind.Afflict == GuWorld.Creatures.Affliction.Tame)
            {
                if (target is GuWorld.Creatures.Beast wolf)
                {
                    GuSkills.Afflict(wolf, kind);
                }

                GuGlow.Flash(point, 1, kind.Colour);

                return;
            }

            var strike = new Strike
            {
                Power = power,
                Sharpness = flown <= Mathf.Max(kind.Reach, 1) ? 1 : 0.5f,
                Point = point,
                Direction = direction,
                Source = thrower != null ? thrower.gameObject : null,
            };

            if (zone != null)
            {
                zone.Owner?.TakeStrike(strike, zone.Part);
            }
            else
            {
                target?.TakeStrike(strike, null);
            }

            GuSkills.Afflict(target, kind);

            if (kind.Radius > 0)
            {
                GuSkills.Blast(point, kind.Radius, power * 0.7f, kind, thrower, target);
            }
            else
            {
                GuGlow.Flash(point, 0.6f, kind.Colour, 0.25f);
            }
        }

        void Spend()
        {
            fading = 0;

            if (trail != null)
            {
                trail.emitting = false;
            }
        }

        bool Mine(Collider collider) => thrower != null && (collider == thrower || collider.transform.IsChildOf(thrower.transform));

        /// <summary>The first thing along the next step: a part of a body, or a solid thing — whichever is nearer.</summary>
        bool Hit(float step, out Vector3 point, out HitZone zone, out Collider solid)
        {
            point = default;
            zone = null;
            solid = null;
            var distance = float.PositiveInfinity;

            foreach (var hit in UnityEngine.Physics.SphereCastAll(transform.position, SolidRadius, direction, step, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Mine(hit.collider) || hit.collider.GetComponentInParent<IZoned>() != null)
                {
                    continue;
                }

                if (hit.distance < distance)
                {
                    distance = hit.distance;
                    point = hit.distance <= 0 && hit.point == Vector3.zero ? transform.position : hit.point;
                    solid = hit.collider;
                }
            }

            var met = new List<(HitZone, float)>();

            foreach (var hit in UnityEngine.Physics.SphereCastAll(transform.position, BodyRadius, direction, step, ~0, QueryTriggerInteraction.Collide))
            {
                if (hit.collider.isTrigger && !Mine(hit.collider) && hit.collider.TryGetComponent<HitZone>(out var part))
                {
                    met.Add((part, hit.distance));
                }
            }

            var chosen = HitZones.Choose(met);

            if (chosen != null)
            {
                var at = float.PositiveInfinity;

                foreach (var (part, d) in met)
                {
                    if (part == chosen)
                    {
                        at = Mathf.Min(at, d);
                    }
                }

                if (at <= distance)
                {
                    point = chosen.Shape.ClosestPoint(transform.position + direction * at);
                    point = float.IsFinite(point.x) ? point : chosen.Shape.bounds.center;
                    zone = chosen;
                    solid = null;
                }
            }

            return zone != null || solid != null;
        }
    }
}
