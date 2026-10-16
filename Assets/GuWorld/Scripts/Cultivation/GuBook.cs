using System.Collections.Generic;

namespace GuWorld.Cultivation
{
    /// <summary>What a Gu's active skill does when used.</summary>
    public enum SkillKind
    {
        None,
        /// <summary>Throws a crescent of condensed essence that flies straight and cuts what it meets.</summary>
        Moonblade,
        /// <summary>Wraps the body in a hard jade skin while essence flows into it (on, then off).</summary>
        JadeSkin,
    }

    /// <summary>How a Gu is after its last meal.</summary>
    public enum Hunger
    {
        Fed,
        /// <summary>Its meal is due: it still answers, but it wants feeding.</summary>
        Hungry,
        /// <summary>Long unfed: too weak to be used.</summary>
        Starving,
        /// <summary>Starved to death.</summary>
        Dead,
    }

    /// <summary>A kind of Gu: what it is, what it eats, what using it costs and does.</summary>
    public class GuKind
    {
        /// <summary>The game's id (saved), and the lore's record it is drawn from.</summary>
        public string Id;
        public string LoreRef;
        public string Name;
        public int Rank;
        /// <summary>What it is for ("атака", "защита").</summary>
        public string Kind;
        /// <summary>The thing it is as an item in the bag (ItemBook).</summary>
        public string Item;
        public string Icon;
        public SkillKind Skill;
        public string SkillName;
        /// <summary>Essence one use costs (for a skin: raising it), % of the aperture.</summary>
        public float Cost;
        /// <summary>Essence a held skill takes, % of the aperture per game minute.</summary>
        public float Upkeep;
        /// <summary>Essence a held skin spends on each point of harm it stops, %.</summary>
        public float PerHarm;
        /// <summary>Seconds before it can be used again.</summary>
        public float Cooldown;
        /// <summary>How far its strike cuts at full strength, and how far it flies before it fades, m.</summary>
        public float Reach;
        public float Range;
        /// <summary>
        /// The strike's strength (Combat.Strike.Power) for a rank-one Gu Master: what it
        /// does depends on the body it lands on and the part it hits (Combat.Wounds).
        /// </summary>
        public float Power;
        /// <summary>What it eats (ItemBook), how many at a meal, and how often (game minutes).</summary>
        public string Food;
        public int Meal;
        public double MealEvery;
        /// <summary>Essence it takes to refine it into one's aperture, % of the aperture.</summary>
        public float RefineCost;

        /// <summary>How it is, a time after its last meal (game minutes).</summary>
        public Hunger HungerAfter(double minutes)
        {
            var meals = minutes / MealEvery;

            return meals < 1 ? Hunger.Fed : meals < 2 ? Hunger.Hungry : meals < 4 ? Hunger.Starving : Hunger.Dead;
        }
    }

    /// <summary>
    /// The Gu the game knows. Each is drawn from the lore — its rank, its skill,
    /// what it eats and how often, what using it costs where the lore says; the
    /// numbers the lore does not give (damage, cooldowns, what a skin's upkeep
    /// is, how long a Gu lasts unfed) are the game's own, to be tuned.
    /// </summary>
    public static class GuBook
    {
        /// <summary>
        /// Moonlight Gu (lore: gu_moonlight_gu, sys_moonblade): a rank one Gu, the Gu Yue
        /// clan's own. Driven with essence it throws a pale-blue crescent — a moonblade —
        /// for a tenth of the aperture each throw; it cuts cleanly at about ten metres and
        /// fades some six metres further. It eats moon orchid petals, two at a meal, morning
        /// and evening. By the lore the cut has no cooldown — essence is its only limit; the
        /// game keeps a fifth of a second between throws so a held key does not spray them.
        /// </summary>
        public static readonly GuKind Moonlight = new GuKind
        {
            Id = "moonlight",
            LoreRef = "gu_moonlight_gu",
            Name = "Гу Лунного Света",
            Rank = 1,
            Kind = "атака",
            Item = GuWorld.Items.ItemBook.MoonlightGu,
            Icon = "moon",
            Skill = SkillKind.Moonblade,
            SkillName = "Лунный клинок",
            Cost = 10,
            Cooldown = 0.2f,
            Reach = 10,
            Range = 16,
            Power = 100,
            Food = GuWorld.Items.ItemBook.Petal,
            Meal = 2,
            MealEvery = 12 * 60,
            RefineCost = 30,
        };

        /// <summary>
        /// Jade Skin Gu (lore: gu_jade_skin_gu): a rare rank one defensive Gu. Active, it
        /// covers the body in a jade-coloured skin that needs essence as long as it holds,
        /// and more for every hard blow. It eats jade rock, two pieces every ten days.
        /// </summary>
        public static readonly GuKind JadeSkin = new GuKind
        {
            Id = "jade_skin",
            LoreRef = "gu_jade_skin_gu",
            Name = "Гу Нефритовой Кожи",
            Rank = 1,
            Kind = "защита",
            Item = GuWorld.Items.ItemBook.JadeSkinGu,
            Icon = "jade",
            Skill = SkillKind.JadeSkin,
            SkillName = "Нефритовая кожа",
            Cost = 2,
            Upkeep = 0.25f,
            PerHarm = 0.12f,
            Cooldown = 1,
            Food = GuWorld.Items.ItemBook.JadeRock,
            Meal = 2,
            MealEvery = 10 * 24 * 60,
            RefineCost = 40,
        };

        static readonly Dictionary<string, GuKind> all = new Dictionary<string, GuKind>
        {
            [Moonlight.Id] = Moonlight,
            [JadeSkin.Id] = JadeSkin,
        };

        public static GuKind Find(string id) => id != null && all.TryGetValue(id, out var kind) ? kind : null;

        public static IEnumerable<GuKind> All => all.Values;

        public static string Name(Hunger hunger) => hunger switch
        {
            Hunger.Hungry => "голоден",
            Hunger.Starving => "истощён",
            Hunger.Dead => "погиб",
            _ => "сыт",
        };
    }
}
