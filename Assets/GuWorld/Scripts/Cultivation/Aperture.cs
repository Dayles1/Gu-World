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
        /// <summary>How far toward the next small realm the aperture's walls have been worn by essence poured into them (0…1).</summary>
        public float Progress;

        /// <summary>
        /// How much essence (in shares of the aperture, %) must be poured into the walls to rise one small realm: more with
        /// every realm and every rank. With better talent the sea holds more and refills sooner, so it is poured faster.
        /// </summary>
        public float StageCost => 500 * (1 + 0.35f * (int)Stage) * (1 + 0.5f * (Rank - 1));

        /// <summary>
        /// At the peak, with the walls worn through, meditation goes on tempering the aperture for the breakthrough (0…1): it
        /// takes half a small realm's essence, and it adds up to two in a hundred to the chance. A failed try loses half of it.
        /// </summary>
        public float Readiness;
        /// <summary>Failed breakthroughs since the last rank: each teaches a little (a fifth of a point, up to two points).</summary>
        public int Failures;

        /// <summary>
        /// The chance of breaking through from this rank's peak, before anything else helps: one in fifty from rank one,
        /// a little less from each rank above — most Gu Masters never break through at all.
        /// </summary>
        public static float BaseChance(int rank) => rank switch
        {
            1 => 0.02f,
            2 => 0.016f,
            3 => 0.013f,
            _ => 0.01f,
        };

        /// <summary>How much the talent weighs on the breakthrough: ×0.6 for 30% … ×1.4 for 99%, ×2 for an extreme physique.</summary>
        public float TalentFactor => Extreme ? 2 : Mathf.Lerp(0.6f, 1.4f, Mathf.Clamp01((Talent - 30) / 69));

        /// <summary>What tempering and the failed tries add to the chance.</summary>
        public float TemperingBonus => 0.02f * Mathf.Clamp01(Readiness);
        public float InsightBonus => Mathf.Min(0.02f, 0.002f * Mathf.Max(0, Failures));

        /// <summary>The whole chance of a breakthrough now, with `more` from elsewhere (the body's spirit).</summary>
        public float BreakChance(float more = 0) =>
            Mathf.Clamp01(BaseChance(Rank) * TalentFactor + TemperingBonus + InsightBonus + Mathf.Max(0, more));

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
        /// halves as the talent doubles; past 80 it shrinks with what is left to a hundred, so 99%
        /// is a flood and an extreme physique hardly ever runs dry. On top of that the time is cut
        /// by a tenth for a D talent of 30% up to three tenths for an A talent of 80% and above
        /// (RefillCut): 30% ≈ 29 h, 50% ≈ 16 h, 70% ≈ 10 h, 88% ≈ 4.6 h, 99% ≈ 24 min.
        /// </summary>
        public static float RefillHours(float talent)
        {
            var t = Mathf.Clamp(talent, 1, 100) / 100;
            var hours = 9.6f / t;

            if (t > 0.8f)
            {
                hours *= (1 - t + 0.002f) / 0.202f;
            }

            return hours * (1 - RefillCut(talent));
        }

        /// <summary>How much shorter the refilling is than the base reckoning: 10% for the poorest talent, 30% for the talented (A, 80% and up).</summary>
        public static float RefillCut(float talent) => 0.1f + 0.2f * Mathf.Clamp01((talent - 30) / 50);

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
        /// A try at the next rank (lore: sys_aperture_wall): from the peak, with the walls worn through and a full sea,
        /// the whole sea is thrown against the aperture's wall. False below the peak, before the walls are worn through,
        /// short of a full sea, or at rank five.
        /// </summary>
        public bool CanBreakThrough => Stage == Stage.Peak && Rank < 5 && Progress >= 1 && Essence >= Cap - 0.5f;

        /// <summary>
        /// The try made with a roll (0…1): under the chance, into the next rank's initial stage; over it, the sea is spent for
        /// nothing, half the tempering lost, and the failure is a lesson. Either way the sea is empty after.
        /// </summary>
        public bool BreakThrough(float roll, float more = 0)
        {
            if (!CanBreakThrough)
            {
                return false;
            }

            Essence = 0;

            if (roll >= BreakChance(more))
            {
                Readiness *= 0.5f;
                Failures++;

                return false;
            }

            Rank++;
            Stage = Stage.Initial;
            Progress = 0;
            Readiness = 0;
            Failures = 0;

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
            Progress = float.IsFinite(Progress) ? Mathf.Clamp01(Progress) : 0;
            Readiness = float.IsFinite(Readiness) ? Mathf.Clamp01(Readiness) : 0;
            Failures = Mathf.Clamp(Failures, 0, 1000);
            Essence = float.IsFinite(Essence) ? Mathf.Clamp(Essence, 0, Cap) : Cap;
        }
    }
}
