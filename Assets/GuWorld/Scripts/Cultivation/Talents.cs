using UnityEngine;

namespace GuWorld.Cultivation
{
    /// <summary>A way to begin: how hard the game is, which is the talent one is born with.</summary>
    public class Beginning
    {
        public string Name;
        /// <summary>The talent it gives, % of the aperture, and the chance (0…1) to be born an extreme physique instead.</summary>
        public float Talent;
        public float ExtremeChance;
        public string About;

        public Grade Grade => Aperture.GradeOf(Talent);
    }

    /// <summary>
    /// The difficulty is the talent (lore: sys_aptitude, sys_ten_extreme_physique): five
    /// ways to begin, from an A talent near the top of its band — with one chance in ten
    /// of the Ten Extreme Physique — to a D talent. A bigger sea is not all: better talent
    /// also refills it far sooner (Aperture.RefillHours).
    /// </summary>
    public static class Talents
    {
        public static readonly Beginning[] Paths =
        {
            new Beginning
            {
                Name = "Лёгкая",
                Talent = 99,
                ExtremeChance = 0.1f,
                About = "Талант A, 99% — лучший, какой видел клан за поколение. Море почти бездонно и наполняется минут за двадцать. " +
                        "Один шанс из десяти родиться с Телосложением Десяти Крайностей: 100%, эссенция почти не кончается.",
            },
            new Beginning
            {
                Name = "Обычная",
                Talent = 88,
                About = "Талант A, 88%. Гордость семьи: большое море, наполняется часов за пять.",
            },
            new Beginning
            {
                Name = "Сложная",
                Talent = 70,
                About = "Талант B, 70%. Крепкий середняк: море наполняется часов за десять, камни пригодятся.",
            },
            new Beginning
            {
                Name = "Невыносимая",
                Talent = 50,
                About = "Талант C, 50%. Пять-шесть ударов — и море пусто, а наполняется оно часов шестнадцать. Каждый камень на счету.",
            },
            new Beginning
            {
                Name = "Невозможная",
                Talent = 30,
                About = "Талант D, 30%. Клан махнул на вас рукой: море мелкое и наполняется больше суток. Выжить — уже победа.",
            },
        };

        /// <summary>The talent one is born with on a path (the extreme physique by its chance).</summary>
        public static float Roll(Beginning path, System.Random random) =>
            path.ExtremeChance > 0 && random.NextDouble() < path.ExtremeChance ? 100 : path.Talent;

        /// <summary>A refilling time in words ("сутки", "4 ч", "35 мин").</summary>
        public static string Span(float hours)
        {
            if (hours >= 24)
            {
                var days = hours / 24;

                return Mathf.Abs(days - 1) < 0.05f ? "сутки" : $"{days:0.#} сут";
            }

            return hours >= 1 ? $"{hours:0.#} ч" : $"{Mathf.Max(1, hours * 60):0} мин";
        }
    }
}
