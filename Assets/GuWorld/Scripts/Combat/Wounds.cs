using System;
using UnityEngine;

namespace GuWorld.Combat
{
    /// <summary>The parts of a body a blow can land on.</summary>
    public enum BodyPart
    {
        Head,
        Neck,
        Heart,
        Chest,
        Liver,
        Belly,
        Spine,
        Groin,
        UpperArmL,
        UpperArmR,
        ForearmL,
        ForearmR,
        HandL,
        HandR,
        ThighL,
        ThighR,
        ShinL,
        ShinR,
        FootL,
        FootR,
    }

    /// <summary>Something that can be thrown off balance (a parried blow): it reels away for some seconds.</summary>
    public interface IStaggerable
    {
        void Stagger(float seconds, Vector3 away);
    }

    /// <summary>A blow as it lands: how strong it is, how keen, where and which way, and who struck.</summary>
    public struct Strike
    {
        /// <summary>
        /// The blow's strength. 100 is a rank-one Gu Master's moonblade; what it does
        /// depends on how it measures against the body it lands on (Wounds.Scale).
        /// </summary>
        public float Power;
        /// <summary>1 for a clean cut, less for a blade that has lost its edge (past its reach).</summary>
        public float Sharpness;
        public Vector3 Point;
        public Vector3 Direction;
        public GameObject Source;
    }

    /// <summary>Something a blow can land on: a body (its parts are HitZones), a branch, a dummy.</summary>
    public interface IStrikable
    {
        /// <summary>A blow landed — on `part` when the thing has a body (null when it has none).</summary>
        void TakeStrike(Strike strike, BodyPart? part);
    }

    /// <summary>What a body part is, for the body page and for the blows that land on it.</summary>
    public class PartInfo
    {
        public BodyPart Part;
        public string Name;
        /// <summary>The share of a body's whole health a clean blow of equal strength takes here, 0…1.</summary>
        public float Min;
        public float Max;
        /// <summary>A vital place: a blow here can end a fight at once.</summary>
        public bool Critical;
        /// <summary>When the blade passes through several parts at once, the higher counts.</summary>
        public int Priority;
        public Color Colour;
        public string About;
    }

    /// <summary>
    /// How blows wound (the game's own model, after a real body): each part of the
    /// body takes its own share of the health — the head, the neck and the heart most,
    /// a hand or a foot least — and the share is scaled by how the blow's strength
    /// measures against the body's toughness. A weak blow on a far stronger body does
    /// nothing at all; a blow far stronger than the body does more than its share.
    /// </summary>
    public static class Wounds
    {
        /// <summary>Below this share of the body's toughness a blow does not get through at all.</summary>
        public const float Threshold = 0.35f;
        /// <summary>The most a far stronger blow multiplies a part's share by.</summary>
        public const float MaxScale = 2.5f;

        static readonly Color Red = new Color(0.86f, 0.16f, 0.14f);
        static readonly Color Orange = new Color(0.93f, 0.48f, 0.12f);
        static readonly Color Yellow = new Color(0.92f, 0.78f, 0.2f);
        static readonly Color Green = new Color(0.36f, 0.72f, 0.3f);
        static readonly Color Blue = new Color(0.3f, 0.56f, 0.86f);

        public static readonly PartInfo[] Parts =
        {
            P(BodyPart.Head, "Голова", 0.50f, 0.80f, true, 9, Red, "Череп и мозг. Скользящий удар оглушает, точный — убивает."),
            P(BodyPart.Neck, "Шея", 0.45f, 0.75f, true, 8, Red, "Сонные артерии и горло: такая рана смертельна за минуты."),
            P(BodyPart.Heart, "Сердце", 0.70f, 0.95f, true, 10, Color.Lerp(Red, Color.black, 0.25f), "Левая сторона груди. Пробитое сердце почти всегда — смерть."),
            P(BodyPart.Chest, "Грудь", 0.18f, 0.32f, false, 4, Orange, "Рёбра держат удар; глубокая рана мешает дышать."),
            P(BodyPart.Liver, "Печень", 0.30f, 0.50f, true, 6, Color.Lerp(Red, Orange, 0.5f), "Правый бок под рёбрами: боль сгибает, кровь идёт обильно."),
            P(BodyPart.Belly, "Живот", 0.15f, 0.28f, false, 3, Orange, "Мягкие ткани без защиты. Рана опасна кровопотерей."),
            P(BodyPart.Spine, "Позвоночник", 0.35f, 0.60f, true, 7, Color.Lerp(Red, Color.magenta, 0.35f), "Удар в спину по хребту может обездвижить."),
            P(BodyPart.Groin, "Пах", 0.20f, 0.35f, false, 3, Yellow, "Рядом бедренные артерии; удар выводит из боя."),
            P(BodyPart.UpperArmL, "Плечо", 0.08f, 0.14f, false, 1, Yellow, "Мышцы и кость руки. Рука слабеет."),
            P(BodyPart.UpperArmR, "Плечо", 0.08f, 0.14f, false, 1, Yellow, "Мышцы и кость руки. Рука слабеет."),
            P(BodyPart.ForearmL, "Предплечье", 0.06f, 0.10f, false, 1, Green, "Сухожилия: пальцы перестают слушаться."),
            P(BodyPart.ForearmR, "Предплечье", 0.06f, 0.10f, false, 1, Green, "Сухожилия: пальцы перестают слушаться."),
            P(BodyPart.HandL, "Кисть", 0.03f, 0.06f, false, 0, Blue, "Больно, но не опасно для жизни."),
            P(BodyPart.HandR, "Кисть", 0.03f, 0.06f, false, 0, Blue, "Больно, но не опасно для жизни."),
            P(BodyPart.ThighL, "Бедро", 0.10f, 0.18f, false, 2, Yellow, "Большая мышца и бедренная артерия: глубокий порез опасен."),
            P(BodyPart.ThighR, "Бедро", 0.10f, 0.18f, false, 2, Yellow, "Большая мышца и бедренная артерия: глубокий порез опасен."),
            P(BodyPart.ShinL, "Голень", 0.06f, 0.12f, false, 1, Green, "Кость близко к коже. Хромота."),
            P(BodyPart.ShinR, "Голень", 0.06f, 0.12f, false, 1, Green, "Кость близко к коже. Хромота."),
            P(BodyPart.FootL, "Стопа", 0.03f, 0.06f, false, 0, Blue, "Мелкие кости. Идти больно."),
            P(BodyPart.FootR, "Стопа", 0.03f, 0.06f, false, 0, Blue, "Мелкие кости. Идти больно."),
        };

        static PartInfo P(BodyPart part, string name, float min, float max, bool critical, int priority, Color colour, string about) =>
            new PartInfo { Part = part, Name = name, Min = min, Max = max, Critical = critical, Priority = priority, Colour = colour, About = about };

        public static PartInfo Of(BodyPart part) => Parts[(int)part];

        /// <summary>
        /// How much of a part's share a blow does, by how its strength measures against
        /// the body's toughness: nothing below Threshold, rising to the full share at
        /// equal strength, and on (more slowly) past it, at most MaxScale.
        /// </summary>
        public static float Scale(float power, float toughness)
        {
            if (power <= 0)
            {
                return 0;
            }

            if (toughness <= 0)
            {
                return MaxScale;
            }

            var ratio = power / toughness;

            if (ratio < Threshold)
            {
                return 0;
            }

            if (ratio < 1)
            {
                return Mathf.Pow((ratio - Threshold) / (1 - Threshold), 1.5f);
            }

            return Mathf.Min(MaxScale, 1 + Mathf.Log(ratio, 2) * 0.5f);
        }

        /// <summary>The harm a blow does to a body of `health` points and `toughness`, landing on `part`.</summary>
        public static float Harm(BodyPart part, Strike strike, float health, float toughness, System.Random random)
        {
            var info = Of(part);
            var roll = random != null ? (float)random.NextDouble() : 0.5f;
            var share = Mathf.Lerp(info.Min, info.Max, roll);

            return health * share * Scale(strike.Power * Mathf.Clamp01(strike.Sharpness <= 0 ? 1 : strike.Sharpness), toughness);
        }

        /// <summary>A blow landed and what it did (for the HUD's log): on whom, where, how much, whether it was the end.</summary>
        public static event Action<string, BodyPart?, float, bool> Landed;

        public static void Report(string who, BodyPart? part, float harm, bool down, Vector3? at = null)
        {
            Landed?.Invoke(who, part, harm, down);

            if (at.HasValue)
            {
                Struck?.Invoke(at.Value, who, part, harm, down);
            }
        }

        /// <summary>A blow landed where it can be shown (its point): for the numbers that rise from it.</summary>
        public static event Action<Vector3, string, BodyPart?, float, bool> Struck;

        /// <summary>A line for the log: "Голова −56 (уязвимое место)".</summary>
        public static string Line(string who, BodyPart? part, float harm, bool down)
        {
            var where = part.HasValue ? Of(part.Value).Name.ToLowerInvariant() : null;
            var critical = part.HasValue && Of(part.Value).Critical;

            if (harm < 0.5f)
            {
                return where != null ? $"{who}: {where} — удар не пробил" : $"{who}: удар не пробил";
            }

            var text = where != null ? $"{who}: {where} −{harm:0}" : $"{who}: −{harm:0}";

            if (critical)
            {
                text += " (уязвимое место)";
            }

            return down ? text + " — повержен" : text;
        }
    }
}
