using System.Collections.Generic;
using GuWorld.Combat;
using GuWorld.Creatures;
using UnityEngine;

namespace GuWorld.Cultivation
{
    /// <summary>
    /// What Gu do to the world: a blast round a point, a sweep before the hero, what a
    /// strike does besides wounding, the beasts within a reach — and the glow they are
    /// drawn with.
    /// </summary>
    public static class GuSkills
    {
        static Material glow;

        /// <summary>A glowing material of a colour: GuWorld/Sky, added (the bloom catches it).</summary>
        public static Material Glow(Color colour)
        {
            if (glow == null)
            {
                var shader = Shader.Find("GuWorld/Sky") ?? Shader.Find("Universal Render Pipeline/Unlit");
                glow = new Material(shader) { name = "Gu Glow", renderQueue = 3050 };

                if (glow.HasProperty("_SrcBlend"))
                {
                    glow.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    glow.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                }
            }

            return new Material(glow) { color = colour };
        }

        /// <summary>What a Gu's strike does to a beast besides wounding it (none to people and things).</summary>
        public static void Afflict(IStrikable target, GuKind kind)
        {
            if (target is IFoe foe && kind.Afflict != Affliction.None)
            {
                foe.Afflict(kind.Afflict, kind.AfflictFor, kind.AfflictPower);
            }
        }

        /// <summary>
        /// A blast round a point: every body and thing within `radius` struck once — a body at
        /// its most vital part the blast reaches — weaker at the edge. With `facing` it is a
        /// sweep: only what lies within ±60° of that way from the point. `spared` is not struck
        /// again (the one a missile already hit). Returns how many were struck.
        /// </summary>
        public static int Blast(Vector3 at, float radius, float power, GuKind kind, Collider thrower, IStrikable spared = null, Vector3? facing = null)
        {
            var bodies = new Dictionary<IStrikable, HitZone>();
            var things = new Dictionary<IStrikable, Collider>();
            var limit = Mathf.Cos(60 * Mathf.Deg2Rad);

            bool Mine(Collider c) => thrower != null && (c == thrower || c.transform.IsChildOf(thrower.transform));

            bool Within(Vector3 point)
            {
                if (facing == null)
                {
                    return true;
                }

                var to = point - at;
                to.y = 0;

                return to.sqrMagnitude < 0.04f || Vector3.Dot(to.normalized, facing.Value) >= limit;
            }

            foreach (var collider in UnityEngine.Physics.OverlapSphere(at, radius, ~0, QueryTriggerInteraction.Collide))
            {
                if (Mine(collider) || !Within(collider.ClosestPoint(at)))
                {
                    continue;
                }

                if (collider.isTrigger)
                {
                    if (collider.TryGetComponent<HitZone>(out var zone) && zone.Owner != null && zone.Owner != spared &&
                        (!bodies.TryGetValue(zone.Owner, out var best) || Wounds.Of(zone.Part).Priority > Wounds.Of(best.Part).Priority))
                    {
                        bodies[zone.Owner] = zone;
                    }

                    continue;
                }

                if (collider.GetComponentInParent<IZoned>() == null && collider.GetComponentInParent<IStrikable>() is IStrikable thing && thing != spared)
                {
                    things[thing] = collider;
                }
            }

            var struck = 0;

            void Strike(IStrikable target, Collider shape, BodyPart? part)
            {
                var point = shape.ClosestPoint(at);
                var away = point - at;
                // Full strength at the heart of it, half at its edge.
                var share = Mathf.Lerp(1, 0.5f, Mathf.Clamp01(away.magnitude / Mathf.Max(radius, 0.1f)));
                away.y = 0;
                target.TakeStrike(new Strike
                {
                    Power = power * share,
                    Sharpness = 1,
                    Point = point,
                    Direction = away.sqrMagnitude > 0.01f ? away.normalized : facing ?? Vector3.forward,
                    Source = thrower != null ? thrower.gameObject : null,
                }, part);
                Afflict(target, kind);
                struck++;
            }

            foreach (var pair in bodies)
            {
                Strike(pair.Key, pair.Value.Shape, pair.Value.Part);
            }

            foreach (var pair in things)
            {
                Strike(pair.Key, pair.Value, null);
            }

            GuGlow.Flash(facing == null ? at : at + facing.Value * radius * 0.4f, facing == null ? radius : radius * 0.7f, kind.Colour);

            return struck;
        }

        /// <summary>The living foes not on the hero's side (beasts, Gu Masters) within a reach of a point.</summary>
        public static IEnumerable<IFoe> Beasts(Vector3 at, float reach) => Foes.Near(at, reach);
    }
}
