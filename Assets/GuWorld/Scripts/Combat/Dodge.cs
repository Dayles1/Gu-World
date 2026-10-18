using GuWorld.Physics;
using UnityEngine;

namespace GuWorld.Combat
{
    /// <summary>
    /// A roll out of harm's way (Alt): the hero tucks and rolls a few metres the way they move (backward,
    /// standing still), and for the first part of it nothing touches them — a bite, a blade, lightning passes
    /// through. It rests a moment before the next. The figure rolls head over heels (Mannequin's flip).
    /// </summary>
    [RequireComponent(typeof(HeroMotor))]
    public class Dodge : MonoBehaviour
    {
        /// <summary>How long a roll takes, how far it goes, how long of it nothing touches the hero, how long it rests.</summary>
        public const float Time = 0.5f;
        public const float Distance = 4.6f;
        public const float Untouchable = 0.34f;
        public const float Rest = 0.75f;

        HeroMotor body;
        CharacterController solid;
        Vector3 way;
        float at = -1;
        float readyAt;

        /// <summary>How far through a roll, 0…1 (below 0: not rolling).</summary>
        public float Phase => at < 0 ? -1 : Mathf.Clamp01(at / Time);

        public bool Rolling => at >= 0;

        /// <summary>A roll back (head over heels the other way), the hero still facing ahead.</summary>
        public bool Backward { get; private set; }

        /// <summary>Whether nothing can touch the hero now (the first part of a roll).</summary>
        public bool Untouched => at >= 0 && at < Untouchable;

        /// <summary>Seconds before the next roll can be made (0: now).</summary>
        public float RestLeft => Mathf.Max(0, readyAt - UnityEngine.Time.time);

        /// <summary>A roll dodged something (for the HUD's word).</summary>
        public event System.Action Dodged;

        void Awake()
        {
            body = GetComponent<HeroMotor>();
            solid = GetComponent<CharacterController>();
        }

        /// <summary>A roll the way given (flat; none: backward from where the hero faces), if free and rested.</summary>
        public bool Roll(Vector3 direction)
        {
            if (Rolling || RestLeft > 0 || !body.Free || !body.Grounded || body.Stance == Stance.Crawl)
            {
                return false;
            }

            direction.y = 0;
            way = direction.sqrMagnitude > 0.01f ? direction.normalized : -transform.forward;
            // Rolling forward or to a side, the hero turns into it; rolling back, they keep their face to the foe.
            Backward = Vector3.Dot(way, transform.forward) < -0.5f;

            if (!Backward)
            {
                body.Facing = Mathf.Atan2(way.x, way.z);
                transform.rotation = Quaternion.Euler(0, body.Facing * Mathf.Rad2Deg, 0);
            }

            at = 0;
            readyAt = UnityEngine.Time.time + Time + Rest;

            return true;
        }

        /// <summary>Something meant to strike the hero met the roll: told, and nothing happens to them.</summary>
        public void Evaded() => Dodged?.Invoke();

        void Update()
        {
            if (at < 0)
            {
                return;
            }

            var dt = UnityEngine.Time.deltaTime;
            var before = Ease(at / Time);
            at += dt;
            var now = Ease(Mathf.Min(1, at / Time));

            if (solid != null && solid.enabled)
            {
                solid.Move(way * (Distance * (now - before)));
            }

            if (at >= Time)
            {
                at = -1;
            }
        }

        /// <summary>Fast out, slowing at the end.</summary>
        static float Ease(float t)
        {
            var x = 1 - Mathf.Clamp01(t);

            return 1 - x * x;
        }
    }
}
