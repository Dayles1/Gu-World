using System.Collections.Generic;
using GuWorld.Creatures;
using UnityEngine;

namespace GuWorld.Cultivation
{
    /// <summary>What a Gu does when used.</summary>
    public enum SkillKind
    {
        None,
        /// <summary>Throws a crescent of condensed essence that flies straight and cuts what it meets.</summary>
        Moonblade,
        /// <summary>Held: a hard jade skin that stops every blow, each one paid for in essence.</summary>
        JadeSkin,
        /// <summary>Held: a skin, mane or armour that takes `Amount` (0…1) of every blow.</summary>
        Skin,
        /// <summary>A missile flying straight at the first thing in its way (with `Radius`: bursting there).</summary>
        Bolt,
        /// <summary>A blast at the point aimed at, `Radius` wide.</summary>
        Burst,
        /// <summary>A sweep in front of the hero, `Reach` deep.</summary>
        Cleave,
        /// <summary>The fists `Amount` times stronger for `Duration` s.</summary>
        Might,
        /// <summary>Running `Amount` times faster for `Duration` s.</summary>
        Haste,
        /// <summary>Jumping `Amount` times higher, and no harm from falls, for `Duration` s.</summary>
        Leap,
        /// <summary>Thrown up into the air, falling softly, for `Duration` s.</summary>
        Flight,
        /// <summary>A rush forward, `Amount` m.</summary>
        Dash,
        /// <summary>At once at the point aimed at (`Range` at most).</summary>
        Blink,
        /// <summary>`Amount` health back at once.</summary>
        Heal,
        /// <summary>Held: `Amount` health back each second.</summary>
        Mend,
        /// <summary>Held: beasts notice the hero only from `Amount` of their sight (0: not at all).</summary>
        Hide,
        /// <summary>Held: the beasts within `Range` are known — how many, how near.</summary>
        Sense,
        /// <summary>A sphere for `Duration` s that nothing gets through; the hero cannot move in it.</summary>
        Ward,
        /// <summary>The beasts within `Radius` run for `Duration` s.</summary>
        Howl,
        /// <summary>The beasts within `Radius` blinded for `Duration` s.</summary>
        Flash,
        /// <summary>A straw figure the beasts go for instead, for `Duration` s.</summary>
        Decoy,
        /// <summary>A missile: the beast it strikes is enslaved and follows the hero.</summary>
        Tame,
        /// <summary>Gu and fists `Amount` times stronger for `Duration` s.</summary>
        Berserk,

        // Used once, from the bag, and spent:

        /// <summary>Up one small realm, at the Gu's own rank (lore: Relic Gu).</summary>
        Relic,
        /// <summary>The fists stronger for good: `Amount` more of their strength.</summary>
        Strength,
        /// <summary>The body tougher for good: `Amount` more of its toughness.</summary>
        Bones,
        /// <summary>The sea filled to its cap at once, for a small realm lost.</summary>
        Desperate,

        // At work by themselves while in the ring:

        /// <summary>A liquor worm: the essence refined one small realm denser, at the worm's own rank.</summary>
        Refine,
        /// <summary>The moonblades `Amount` times bigger and longer (Little Light).</summary>
        Amplify,
        /// <summary>Beasts notice the hero only from `Amount` of their sight.</summary>
        Scent,
        /// <summary>Fusing Gu never fails.</summary>
        Surety,
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
        /// <summary>Its line: the Gu it grows from and into ("лунная линия").</summary>
        public string Line;
        /// <summary>The thing it is as an item in the bag (ItemBook).</summary>
        public string Item;
        public string Icon;
        /// <summary>A few lines about it, in the game's own words (the bag's and the codex's).</summary>
        public string About;
        /// <summary>What it costs in a shop, in primeval stones (0: not sold).</summary>
        public int Price;
        public SkillKind Skill;
        public string SkillName;
        /// <summary>
        /// Essence one use costs (a held one: raising it), % of the aperture — for a master of
        /// the Gu's own rank at the initial stage (Aperture.CostOf scales it to the user).
        /// </summary>
        public float Cost;
        /// <summary>Essence a held Gu takes each second, % of the aperture (scaled as Cost is).</summary>
        public float Upkeep;
        /// <summary>Essence a jade skin spends on each point of harm it stops, %.</summary>
        public float PerHarm;
        /// <summary>
        /// Seconds the Gu itself needs between two uses (its own; there is no pause shared
        /// between Gu): a moonblade a fifth of a second, a fire dragon longer.
        /// </summary>
        public float Cooldown = 0.3f;
        /// <summary>How far its strike cuts at full strength, and how far it flies before it fades, m.</summary>
        public float Reach;
        public float Range;
        /// <summary>
        /// The strike's strength (Combat.Strike.Power): what it does depends on the body it
        /// lands on and the part it hits (Combat.Wounds).
        /// </summary>
        public float Power;
        /// <summary>How wide a blast is, m.</summary>
        public float Radius;
        /// <summary>How long an effect lasts, s, and how strong it is (each skill reads it its own way).</summary>
        public float Duration;
        public float Amount;
        /// <summary>What its strike does besides wounding (slows, holds, burns…), for how long and how strongly.</summary>
        public Affliction Afflict;
        public float AfflictFor;
        public float AfflictPower;
        /// <summary>Its light: the missile's, the blast's, the skin's glow.</summary>
        public Color Colour = new Color(0.55f, 0.78f, 1f);
        /// <summary>
        /// How much a use tires it (of 100); a held one tires this much every ten seconds. Tired
        /// past 100 it can still be driven, but forcing it may kill it (Cultivator.Strain).
        /// </summary>
        public float Tire = 25;
        /// <summary>What it eats (ItemBook), how many at a meal, and how often (game minutes); none: it does not eat.</summary>
        public string Food;
        public int Meal = 1;
        public double MealEvery;
        /// <summary>Essence it takes to refine it into one's aperture, % of the aperture (scaled as Cost is).</summary>
        public float RefineCost = 20;

        /// <summary>Used once from the bag and spent (never refined).</summary>
        public bool OneUse => Skill is SkillKind.Relic or SkillKind.Strength or SkillKind.Bones or SkillKind.Desperate;

        /// <summary>Held: raised, it stays up, taking essence each second, until it is put down.</summary>
        public bool Held => Skill is SkillKind.JadeSkin or SkillKind.Skin or SkillKind.Mend or SkillKind.Hide or SkillKind.Sense;

        /// <summary>At work by itself while in the ring (no key).</summary>
        public bool Passive => Skill is SkillKind.Refine or SkillKind.Amplify or SkillKind.Scent or SkillKind.Surety;

        /// <summary>Aimed at a point (the crosshair): a missile, a blast, a leap to it.</summary>
        public bool Aimed => Skill is SkillKind.Moonblade or SkillKind.Bolt or SkillKind.Burst or SkillKind.Blink or SkillKind.Tame;

        /// <summary>How it is, a time after its last meal (game minutes).</summary>
        public Hunger HungerAfter(double minutes)
        {
            if (Food == null || MealEvery <= 0)
            {
                return Hunger.Fed;
            }

            var meals = minutes / MealEvery;

            return meals < 1 ? Hunger.Fed : meals < 2 ? Hunger.Hungry : meals < 4 ? Hunger.Starving : Hunger.Dead;
        }
    }

    /// <summary>
    /// A fusion (lore: sys_refinement): Gu and materials refined together into another Gu.
    /// Ranks never add up — two rank-two Gu make a rank-two or at most a rank-three Gu,
    /// never a rank four — and some lines stay at one rank (two boars make one boar).
    /// </summary>
    public class Recipe
    {
        public string Result;
        /// <summary>The Gu spent (kinds; a kind twice: two of it).</summary>
        public string[] Gu;
        /// <summary>The materials spent: item id and count.</summary>
        public (string item, int count)[] Materials = new (string, int)[0];
        /// <summary>Essence it takes, % — for a master of the result's rank (scaled as Cost is).</summary>
        public float Cost = 30;
        /// <summary>The chance it takes (0…1); failed, everything put in is lost.</summary>
        public float Chance = 0.8f;
    }

    /// <summary>
    /// The Gu the game knows. Each is drawn from the lore — its rank, its line, its
    /// skill where the lore tells it; the numbers the lore does not give (strength,
    /// costs, what it eats and how often) are the game's own, to be tuned.
    /// </summary>
    public static partial class GuBook
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
            Line = "лунная линия",
            Item = GuWorld.Items.ItemBook.MoonlightGu,
            Icon = "moon",
            Skill = SkillKind.Moonblade,
            SkillName = "Лунный клинок",
            Cost = 10,
            Cooldown = 0.2f,
            Reach = 10,
            Range = 16,
            Power = 100,
            Tire = 18,
            Food = GuWorld.Items.ItemBook.Petal,
            Meal = 2,
            MealEvery = 12 * 60,
            RefineCost = 30,
            Price = 30,
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
            Line = "нефритовая линия",
            Item = GuWorld.Items.ItemBook.JadeSkinGu,
            Icon = "jade",
            Skill = SkillKind.JadeSkin,
            SkillName = "Нефритовая кожа",
            Cost = 2,
            Upkeep = 0.25f,
            PerHarm = 0.12f,
            Cooldown = 1,
            Colour = new Color(0.45f, 0.95f, 0.6f),
            Tire = 20,
            Food = GuWorld.Items.ItemBook.JadeRock,
            Meal = 2,
            MealEvery = 10 * 24 * 60,
            RefineCost = 40,
            Price = 60,
        };

        static readonly Dictionary<string, GuKind> all = new Dictionary<string, GuKind>();
        static readonly List<GuKind> order = new List<GuKind>();

        static GuBook()
        {
            Add(Moonlight);
            Add(JadeSkin);
            AddCatalogue();
        }

        static void Add(GuKind kind)
        {
            kind.Item ??= "gu_" + kind.Id;
            all[kind.Id] = kind;
            order.Add(kind);
        }

        public static GuKind Find(string id) => id != null && all.TryGetValue(id, out var kind) ? kind : null;

        /// <summary>Every Gu, by rank and then as the catalogue lists them.</summary>
        public static IEnumerable<GuKind> All
        {
            get
            {
                for (var rank = 1; rank <= 5; rank++)
                {
                    foreach (var kind in order)
                    {
                        if (kind.Rank == rank)
                        {
                            yield return kind;
                        }
                    }
                }
            }
        }

        public static int Count => order.Count;

        /// <summary>The Gu a bag item is (null: it is not one).</summary>
        public static GuKind OfItem(string item)
        {
            foreach (var kind in order)
            {
                if (kind.Item == item)
                {
                    return kind;
                }
            }

            return null;
        }

        /// <summary>The fusions that make a Gu, or use it.</summary>
        public static IEnumerable<Recipe> RecipesFor(string kind)
        {
            foreach (var recipe in Recipes)
            {
                if (recipe.Result == kind || System.Array.IndexOf(recipe.Gu, kind) >= 0)
                {
                    yield return recipe;
                }
            }
        }

        public static string Name(Hunger hunger) => hunger switch
        {
            Hunger.Hungry => "голоден",
            Hunger.Starving => "истощён",
            Hunger.Dead => "погиб",
            _ => "сыт",
        };
    }
}
