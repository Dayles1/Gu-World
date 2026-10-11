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
    /// is fed and driven with (lore: sys_aperture_and_primeval_essence,
    /// sys_primeval_stones). All amounts are shares of the whole aperture, in
    /// percent, as the lore counts them:
    ///
    /// - the talent caps how high the sea can stand — about 8–9 of the
    ///   aperture's ten layers at A, 4–5 at C, 2–3 at D;
    /// - the sea refills by itself, faster with better talent — about 4% of the
    ///   aperture an hour at C, 8% at A (game hours);
    /// - a primeval stone's essence can be drawn in to raise the sea at once,
    ///   though never past the talent's cap.
    ///
    /// The exact numbers within the lore's ranges (B's, a stone's worth) are
    /// the game's own, to be tuned.
    /// </summary>
    [System.Serializable]
    public class Aperture
    {
        public Grade Grade = Grade.C;
        public int Rank = 1;
        public Stage Stage = Stage.Initial;
        /// <summary>The essence in the sea now, % of the aperture.</summary>
        public float Essence;

        /// <summary>How much of the aperture one primeval stone fills, %.</summary>
        public const float StoneWorth = 12;

        /// <summary>How high the sea can stand, % of the aperture.</summary>
        public float Cap => Grade switch
        {
            Grade.A => 85,
            Grade.B => 65,
            Grade.C => 45,
            _ => 25,
        };

        /// <summary>Natural refilling, % of the aperture per game hour.</summary>
        public float RecoveryPerHour => Grade switch
        {
            Grade.A => 8,
            Grade.B => 6,
            Grade.C => 4,
            _ => 3,
        };

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

        /// <summary>A rank as the seal shows it: 一 二 三 四 五.</summary>
        public static string RankGlyph(int rank) => rank switch
        {
            2 => "二",
            3 => "三",
            4 => "四",
            5 => "五",
            _ => "一",
        };

        public static string Name(Grade grade) => grade.ToString();

        public static string Name(Stage stage) => stage switch
        {
            Stage.Middle => "средняя стадия",
            Stage.Upper => "верхняя стадия",
            Stage.Peak => "пиковая стадия",
            _ => "начальная стадия",
        };

        /// <summary>A sea at its fullest (a fresh aperture, a new game).</summary>
        public static Aperture Full(Grade grade)
        {
            var aperture = new Aperture { Grade = grade };
            aperture.Essence = aperture.Cap;

            return aperture;
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
            if (!System.Enum.IsDefined(typeof(Grade), Grade)) Grade = Grade.C;
            if (!System.Enum.IsDefined(typeof(Stage), Stage)) Stage = Stage.Initial;
            Rank = Mathf.Clamp(Rank, 1, 9);
            Essence = float.IsFinite(Essence) ? Mathf.Clamp(Essence, 0, Cap) : Cap;
        }
    }
}
