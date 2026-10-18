using UnityEngine;

namespace GuWorld.Cultivation
{
    /// <summary>Talent for cultivation, A (best) to D (lore: sys_aptitude).</summary>
    public enum Grade
    {
        A,
        B,
        C,
        D,
    }

    /// <summary>The four small realms of a rank (lore: sys_rank_one).</summary>
    public enum Stage
    {
        Initial,
        Middle,
        Upper,
        Peak,
    }

    /// <summary>
    /// A Gu Master's aperture and the primeval sea in it — the essence every Gu
    /// is fed and driven with (lore: sys_aperture_and_primeval_essence, sys_aptitude,
    /// sys_primeval_stones). All amounts are shares of the whole aperture, in
    /// percent, as the lore counts them:
    ///
    /// - the talent is how high the sea can stand: D 20–39%, C 40–59%, B 60–79%,
    ///   A 80–99%; a hundred is one of the Ten Extreme Physiques;
    /// - the sea refills by itself, and better talent refills it faster on both
    ///   counts — more of it, and sooner (RefillHours): a 40% sea fills in a day,
    ///   an 80% one in half a day, a 99% one in about half an hour, and an
    ///   extreme physique's in a few minutes;
    /// - each rank's essence is four times as dense as the last (green copper,
    ///   red steel, white silver, yellow gold, purple crystal), and each small
    ///   realm within a rank half as dense again: what a Gu costs is its own
    ///   rank's price divided by how much denser the master's essence is (CostOf);
    /// - a primeval stone's essence can be drawn in to raise the sea at once,
    ///   though never past the talent's cap.
    ///
    /// The exact numbers within the lore's ranges are the game's own, to be tuned.
    /// </summary>
    [System.Serializable]
    public class Aperture
    {
        /// <summary>The talent: how high the sea can stand, % of the aperture (20…100).</summary>
        public float Talent = 50;
        public int Rank = 1;
        public Stage Stage = Stage.Initial;
        /// <summary>The essence in the sea now, % of the aperture.</summary>
        public float Essence;

        /// <summary>How much of the aperture one primeval stone fills, %.</summary>
        public const float StoneWorth = 12;

        /// <summary>The talent's grade: A from 80%, B from 60, C from 40, D below.</summary>
        public Grade Grade => GradeOf(Talent);

        public static Grade GradeOf(float talent) => talent >= 80 ? Grade.A : talent >= 60 ? Grade.B : talent >= 40 ? Grade.C : Grade.D;

        /// <summary>A hundred: one of the Ten Extreme Physiques (lore: sys_ten_extreme_physique).</summary>
        public bool Extreme => Talent >= 99.95f;

        /// <summary>How high the sea can stand, % of the aperture.</summary>
        public float Cap => Talent;

        /// <summary>
        /// Game hours the sea takes to fill from empty to its cap by itself. Up to 80% the time
        /// halves as the talent doubles (40%: a day, 80%: half a day); past 80 it shrinks with
        /// what is left to a hundred, so 99% is a flood and an extreme physique hardly ever runs dry.
        /// </summary>
        public static float RefillHours(float talent)
        {
            var t = Mathf.Clamp(talent, 1, 100) / 100;
            var hours = 9.6f / t;

            if (t > 0.8f)
            {
                hours *= (1 - t + 0.002f) / 0.202f;
            }

            return hours;
        }

        /// <summary>Natural refilling, % of the aperture per game hour.</summary>
        public float RecoveryPerHour => Cap / RefillHours(Talent) * RecoveryBonus;

        /// <summary>Something speeding the refilling (1: nothing).</summary>
        [System.NonSerialized] public float RecoveryBonus = 1;

        /// <summary>How dense a rank's essence is against green copper: four times for each rank.</summary>
        public static float RankDensity(int rank) => Mathf.Pow(4, Mathf.Clamp(rank, 1, 9) - 1);

        /// <summary>How dense a small realm makes it: half as dense again for each.</summary>
        public static float StageDensity(Stage stage) => Mathf.Pow(1.5f, (int)stage);

        /// <summary>
        /// How dense this master's essence is against green copper at the initial stage (a liquor
        /// worm refining it counts as one small realm more).
        /// </summary>
        public float Density => RankDensity(Rank) * StageDensity(Stage) * (Refined ? 1.5f : 1);

        /// <summary>A liquor worm of this rank refines the essence (one small realm's worth).</summary>
        [System.NonSerialized] public bool Refined;

        /// <summary>
        /// What a use costs this master, % of the aperture: `price` is what it costs a master of
        /// the Gu's own rank at the initial stage. Each rank between them divides it — or, for a
        /// Gu above the master, multiplies it — by four; each small realm divides it by 1.5.
        /// </summary>
        public float CostOf(float price, int guRank) => price * RankDensity(guRank) / Density;

        /// <summary>
        /// The essence's name at this rank (lore: sys_aperture_and_primeval_essence): green
        /// copper at rank one, red steel at two, white silver at three, yellow gold at four,
        /// purple crystal at five.
        /// </summary>
        public string EssenceName => Rank switch
        {
            1 => "зелёная медь",
            2 => "красная сталь",
            3 => "белое серебро",
            4 => "жёлтое золото",
            _ => "пурпурный кристалл",
        };

        /// <summary>The essence's colour at a rank (the design's rank colours).</summary>
        public static Color RankColour(int rank) => rank switch
        {
            2 => new Color32(0xC2, 0x52, 0x4A, 0xFF),
            3 => new Color32(0xC9, 0xCC, 0xD1, 0xFF),
            4 => new Color32(0xD4, 0xAE, 0x52, 0xFF),
            5 => new Color32(0x9C, 0x7A, 0xD6, 0xFF),
            _ => new Color32(0x4E, 0x9A, 0x86, 0xFF),
        };

        /// <summary>A rank as the seals and cells show it: I … V.</summary>
        public static string RankNumeral(int rank) => rank switch
        {
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            _ => "I",
        };

        public static string Name(Grade grade) => grade.ToString();

        /// <summary>The talent in words: "класс A · 88%", or the extreme physique.</summary>
        public string TalentName => Extreme ? "Телосложение Десяти Крайностей · 100%" : $"класс {Grade} · {Talent:0}%";

        /// <summary>A grade's talent before talent was counted in percent (old saves): the middle of its band.</summary>
        public static float TalentOf(Grade grade) => grade switch
        {
            Grade.A => 88,
            Grade.B => 70,
            Grade.C => 50,
            _ => 30,
        };

        public static string Name(Stage stage) => stage switch
        {
            Stage.Middle => "средняя стадия",
            Stage.Upper => "верхняя стадия",
            Stage.Peak => "пиковая стадия",
            _ => "начальная стадия",
        };

        /// <summary>A sea at its fullest (a fresh aperture, a new game).</summary>
        public static Aperture Full(float talent)
        {
            var aperture = new Aperture { Talent = Mathf.Clamp(talent, 20, 100) };
            aperture.Essence = aperture.Cap;

            return aperture;
        }

        public static Aperture Full(Grade grade) => Full(TalentOf(grade));

        /// <summary>Up one small realm (lore: sys_relic_gu); false at the peak, where only a breakthrough leads on.</summary>
        public bool Advance()
        {
            if (Stage == Stage.Peak)
            {
                return false;
            }

            Stage++;

            return true;
        }

        /// <summary>
        /// From the peak into the next rank's initial stage, the whole sea spent breaking the
        /// aperture's wall (lore: sys_aperture_wall); false below the peak, short of a full sea
        /// or at rank five.
        /// </summary>
        public bool CanBreakThrough => Stage == Stage.Peak && Rank < 5 && Essence >= Cap - 0.5f;

        public bool BreakThrough()
        {
            if (!CanBreakThrough)
            {
                return false;
            }

            Rank++;
            Stage = Stage.Initial;
            Essence = 0;

            return true;
        }

        public bool CanSpend(float amount) => Essence + 1e-4f >= amount;

        /// <summary>Takes essence from the sea; false (and nothing taken) when there is not enough.</summary>
        public bool Spend(float amount)
        {
            if (!CanSpend(amount))
            {
                return false;
            }

            Essence = Mathf.Max(0, Essence - amount);

            return true;
        }

        /// <summary>Lets game minutes pass: the sea refills by itself, up to its cap.</summary>
        public void Recover(double minutes)
        {
            if (minutes <= 0 || double.IsNaN(minutes))
            {
                return;
            }

            Essence = Mathf.Min(Cap, Essence + (float)(RecoveryPerHour * minutes / 60));
        }

        /// <summary>Draws in one stone's essence; false when the sea is already at its cap.</summary>
        public bool Absorb()
        {
            if (Essence >= Cap - 0.01f)
            {
                return false;
            }

            Essence = Mathf.Min(Cap, Essence + StoneWorth);

            return true;
        }

        /// <summary>A loaded aperture made sound: a known grade, the essence within its cap.</summary>
        public void Repair()
        {
            Talent = float.IsFinite(Talent) ? Mathf.Clamp(Talent, 20, 100) : 50;
            if (!System.Enum.IsDefined(typeof(Stage), Stage)) Stage = Stage.Initial;
            Rank = Mathf.Clamp(Rank, 1, 5);
            Essence = float.IsFinite(Essence) ? Mathf.Clamp(Essence, 0, Cap) : Cap;
        }
    }
}
