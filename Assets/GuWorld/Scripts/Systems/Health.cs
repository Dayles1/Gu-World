using System;
using GuWorld.Combat;
using GuWorld.Cultivation;
using GuWorld.Physics;
using UnityEngine;

namespace GuWorld.Systems
{
    /// <summary>
    /// The hero's health (the design's life bar). It mends by itself with time,
    /// faster with food; a hard landing hurts — a fall from a few metres up and
    /// more — unless the jade skin is up to take the blow (lore: gu_jade_skin_gu,
    /// a hard fall cushioned). At nothing the hero faints (GuWorldGame wakes them
    /// in the village).
    ///
    /// The numbers — a hundred points, how fast it mends, how hard a landing
    /// must be to hurt — are the game's own, to be tuned.
    ///
    /// Blows land on the hero's body part by part (HitZones on the figure, Wounds):
    /// the toughness grows with the aperture's rank.
    /// </summary>
    public class Health : MonoBehaviour, IStrikable, IZoned
    {
        public const float Max = 100;
        /// <summary>Mending by itself, points per game hour.</summary>
        public const float MendPerHour = 25;
        /// <summary>Landing faster than this (m/s) hurts: about a three-metre drop.</summary>
        public const float SafeLanding = 13;
        /// <summary>Points lost for each m/s of landing past the safe speed.</summary>
        public const float HarmPerSpeed = 7;

        public float Current = Max;
        /// <summary>The proving ground's mode: harm is taken and shown, but the hero never falls.</summary>
        public bool Immortal;

        /// <summary>Until when (Time.time) a lightning bite's shock slows the hero.</summary>
        public float ShockedUntil { get; private set; }

        public bool Shocked => Time.time < ShockedUntil;

        /// <summary>Lightning through the body: slowed for `seconds`.</summary>
        public void Shock(float seconds)
        {
            if (seconds > 0 && !Down)
            {
                ShockedUntil = Mathf.Max(ShockedUntil, Time.time + seconds);
            }
        }

        /// <summary>Harm got through: how much (after the skin), and whether it was the last.</summary>
        public event Action<float> Hurt;
        public event Action Fainted;

        HeroMotor body;
        Cultivator gu;

        public bool Down => Current <= 0;

        void Awake()
        {
            body = GetComponent<HeroMotor>();
            gu = GetComponent<Cultivator>();

            if (body != null)
            {
                body.Landed += OnLanded;
            }
        }

        void OnDestroy()
        {
            if (body != null)
            {
                body.Landed -= OnLanded;
            }
        }

        void OnLanded(float speed)
        {
            // Wings (a Gu's): the ground is met softly.
            if (speed > SafeLanding && (gu == null || !gu.SoftFall))
            {
                TakeHarm((speed - SafeLanding) * HarmPerSpeed);
            }
        }

        /// <summary>Game minutes pass: health mends by itself (not while fainted).</summary>
        public void Mend(double minutes)
        {
            if (Down || minutes <= 0 || !double.IsFinite(minutes))
            {
                return;
            }

            Current = Mathf.Min(Max, Current + (float)(MendPerHour * minutes / 60));
        }

        public void Heal(float amount)
        {
            if (amount > 0)
            {
                Current = Mathf.Min(Max, Current + amount);
            }
        }

        /// <summary>How hard the hero's body is to wound: a youth's at rank one, harder with every rank and with bones a Gu remade.</summary>
        public float Toughness => 80 * (gu != null ? (1 + 0.8f * (gu.Aperture.Rank - 1)) * gu.ToughnessBoost : 1);

        readonly System.Random random = new System.Random();

        /// <summary>The guard meets blows at the body above this height over the feet, m; and lets through at least this share of a blow's strength as harm.</summary>
        const float GuardFrom = 0.8f;
        const float GuardChip = 0.04f;

        public void TakeStrike(Strike strike, BodyPart? part)
        {
            // Guarding the head: a blow from in front at the upper body lands on a forearm, weakened — but the arms still
            // take some of it (a guard is not a wall); a bite at the legs comes in under it.
            var guarded = TryGetComponent<Melee>(out var melee) && melee.Meets(strike.Direction) &&
                          strike.Point.y - transform.position.y > GuardFrom;
            var power = strike.Power;

            if (guarded)
            {
                strike.Power *= Melee.GuardLets;
                part = random.Next(2) == 0 ? BodyPart.ForearmL : BodyPart.ForearmR;
            }

            var harm = Wounds.Harm(part ?? BodyPart.Chest, strike, Max, Toughness, random);

            if (guarded)
            {
                harm = Mathf.Max(harm, power * GuardChip);
            }

            var through = TakeHarm(harm);
            Wounds.Report("Вы", part, through, Down, strike.Point);
        }

        /// <summary>Harm from outside: the jade skin takes what it can first. Returns what got through.</summary>
        public float TakeHarm(float harm)
        {
            var through = gu != null ? gu.Shield(harm) : harm;

            return Strike(through);
        }

        /// <summary>Harm the skin cannot stop (from within: a vital Gu's death).</summary>
        public float Strike(float harm)
        {
            if (harm <= 0 || Down)
            {
                return 0;
            }

            // Immortal (the proving ground): the harm shows, but never past the last point of health.
            Current = Mathf.Max(Immortal ? 1 : 0, Current - harm);
            Hurt?.Invoke(harm);

            if (Current <= 0)
            {
                Fainted?.Invoke();
            }

            return harm;
        }

        /// <summary>Back on one's feet with some of the health (after fainting, or a loaded game).</summary>
        public void Set(float value) => Current = float.IsFinite(value) ? Mathf.Clamp(value, 0, Max) : Max;
    }
}
