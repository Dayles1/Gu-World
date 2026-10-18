using GuWorld.Items;
using UnityEngine;

namespace GuWorld.Creatures
{
    /// <summary>The body a beast is built with and moves in.</summary>
    public enum BeastShape
    {
        Wolf,
        Boar,
        Bear,
        Monkey,
        Snake,
    }

    /// <summary>
    /// A kind of beast: its name and its rung among the beasts (an ordinary one, or a beast king ruling a hundred, a
    /// thousand, ten thousand of its kind — lore: sys_beast_king), the levels it comes in, its body, size and colours, its
    /// strength, how it fights (a boar's charge, a monkey's stones, a snake's venom), and the Gu-given gifts a beast king
    /// fights with (lore: a king's body holds one or two Gu that come out when it is attacked) — and what it leaves when
    /// it dies.
    /// </summary>
    public class BeastKind
    {
        public string Id;
        public string Name;
        /// <summary>0 an ordinary wolf; 1 a hundred beast king, 2 a thousand beast king, 3 a myriad beast king.</summary>
        public int Tier;
        public int MinLevel, MaxLevel;

        /// <summary>The body it is built with; how tall it stands and how wide it is (m, at size 1).</summary>
        public BeastShape Shape;
        public float Tall = 1.15f;
        public float Girth = 0.36f;
        /// <summary>Its size against a grey wolf's.</summary>
        public float Scale = 1;
        /// <summary>Its health and how hard its body is to wound (against a blow's power, Combat.Wounds) at its lowest level.</summary>
        public float Health = 60;
        public float Toughness = 70;
        /// <summary>How fast it runs, how hard its bite is (Strike.Power) and how often it bites, s.</summary>
        public float Chase = 6.4f;
        public float Bite = 92;
        public float BiteEvery = 1.1f;
        /// <summary>How far it sees, against a grey wolf.</summary>
        public float Sight = 1;

        public Color Fur = new Color(0.36f, 0.34f, 0.32f);
        public Color Back = new Color(0.2f, 0.19f, 0.19f);
        public Color Belly = new Color(0.56f, 0.53f, 0.49f);
        public Color Eye = new Color(1, 0.6f, 0.15f);
        /// <summary>Lightning crackles in its fur (its back's colour glows), and a crown of horns and golden tufts (the thunder crown wolf).</summary>
        public bool Glows;
        public bool Crown;

        /// <summary>Its bite shocks: the hero is slowed this many seconds.</summary>
        public float Shock;
        /// <summary>Lightning spat at the hero: how often (s; 0 never), how hard, how far.</summary>
        public float BoltEvery, BoltPower, BoltRange = 22;
        /// <summary>A howl that maddens its pack (how often, s; 0 never).</summary>
        public float HowlEvery;
        /// <summary>Below this share of its health it goes berserk (0 never): faster, harder, quicker to bite.</summary>
        public float FrenzyBelow;
        /// <summary>A thunder shield raised every so often (s; 0 never), held this long: it takes only part of every blow.</summary>
        public float ShieldEvery, ShieldFor;
        /// <summary>Health mended each second when it has not been struck for a while (a healing Gu in its body).</summary>
        public float Regen;
        /// <summary>A lightning eye: it sees through stealth and invisibility.</summary>
        public bool SeesHidden;
        /// <summary>A stamp of its forepaws that sends lightning through the ground round it: how often (s; 0 never), how far, how hard.</summary>
        public float StompEvery, StompRadius, StompPower;

        /// <summary>A charge (a boar's): how often (s; 0 never) it rushes headlong at the hero, and how hard it strikes.</summary>
        public float ChargeEvery, ChargePower;
        /// <summary>Stones thrown (a monkey's): how often (s; 0 never), how hard; and how far from the hero it keeps.</summary>
        public float ThrowEvery, ThrowPower, Keep;
        /// <summary>Venom in its bite: harm a second, for how many seconds.</summary>
        public float Venom, VenomFor;
        /// <summary>Once on the hero's trail it never gives it up (lore: a dark venomous snake holds a grudge).</summary>
        public bool Relentless;
        /// <summary>Its body turns to stone fragments as it dies (lore: a jade eye stone monkey).</summary>
        public bool Stone;

        /// <summary>What its body leaves (item, count), and the Gu that may come out of it as it dies (GuBook id, chance).</summary>
        public (string item, int count)[] Spoils = { (ItemBook.WolfFang, 1) };
        public (string gu, float chance)[] Drops = new (string, float)[0];

        /// <summary>How much more of everything (health, bite, lightning) a level above its lowest gives it.</summary>
        public float LevelFactor(int level) => 1 + 0.1f * Mathf.Max(0, level - MinLevel);

        /// <summary>Its rung, in words.</summary>
        public string TierName => Tier switch
        {
            1 => "король ста зверей",
            2 => "король тысячи зверей",
            3 => "король мириад зверей",
            _ => "",
        };
    }

    /// <summary>
    /// The wolves of Qing Mao (lore: sys_wolf_tide): the grey wolves of its forests, and the lightning wolves whose
    /// dens fill every few years — ordinary lightning wolves, bold lightning wolves (hundred beast kings), frenzy
    /// lightning wolves (thousand beast kings) and the hill-sized thunder crown wolf (a myriad beast king) with its
    /// Berserk, healing, Lightning Eye and Thunder Shield Gu. The numbers are the game's own.
    /// </summary>
    public static class WolfKinds
    {
        public static readonly BeastKind Grey = new BeastKind
        {
            Id = "grey",
            Name = "Серый волк",
            MinLevel = 1,
            MaxLevel = 3,
        };

        public static readonly BeastKind Lightning = new BeastKind
        {
            Id = "lightning",
            Name = "Молниевый волк",
            MinLevel = 4,
            MaxLevel = 8,
            Scale = 1.1f,
            Health = 110,
            Toughness = 90,
            Chase = 7.6f,
            Bite = 120,
            BiteEvery = 1f,
            Sight = 1.2f,
            Fur = new Color(0.3f, 0.34f, 0.42f),
            Back = new Color(0.16f, 0.2f, 0.3f),
            Belly = new Color(0.55f, 0.6f, 0.68f),
            Eye = new Color(0.45f, 0.75f, 1f),
            Glows = true,
            Shock = 0.8f,
            Spoils = new[] { (ItemBook.WolfFang, 2) },
        };

        public static readonly BeastKind Bold = new BeastKind
        {
            Id = "bold",
            Name = "Отважный молниевый волк",
            Tier = 1,
            MinLevel = 12,
            MaxLevel = 14,
            Scale = 1.55f,
            Health = 420,
            Toughness = 140,
            Chase = 7.8f,
            Bite = 180,
            BiteEvery = 1f,
            Sight = 1.4f,
            Fur = new Color(0.26f, 0.3f, 0.42f),
            Back = new Color(0.12f, 0.16f, 0.32f),
            Belly = new Color(0.6f, 0.64f, 0.74f),
            Eye = new Color(0.55f, 0.8f, 1f),
            Glows = true,
            Shock = 1f,
            BoltEvery = 5,
            BoltPower = 160,
            HowlEvery = 14,
            Spoils = new[] { (ItemBook.WolfFang, 5) },
            Drops = new[] { ("plasma", 0.25f), ("thunder_potato", 0.2f), ("eating_wind", 0.1f) },
        };

        public static readonly BeastKind Frenzy = new BeastKind
        {
            Id = "frenzy",
            Name = "Неистовый молниевый волк",
            Tier = 2,
            MinLevel = 20,
            MaxLevel = 22,
            Scale = 2.1f,
            Health = 1300,
            Toughness = 220,
            Chase = 8.4f,
            Bite = 300,
            BiteEvery = 0.95f,
            Sight = 1.6f,
            Fur = new Color(0.22f, 0.22f, 0.36f),
            Back = new Color(0.3f, 0.14f, 0.36f),
            Belly = new Color(0.55f, 0.55f, 0.7f),
            Eye = new Color(0.75f, 0.6f, 1f),
            Glows = true,
            Shock = 1.2f,
            BoltEvery = 3.5f,
            BoltPower = 300,
            HowlEvery = 12,
            FrenzyBelow = 0.5f,
            ShieldEvery = 12,
            ShieldFor = 4,
            Spoils = new[] { (ItemBook.WolfFang, 10) },
            Drops = new[] { ("thunder_shield", 0.4f), ("lightning_eye", 0.2f), ("thunderwings", 0.12f) },
        };

        public static readonly BeastKind ThunderCrown = new BeastKind
        {
            Id = "thunder_crown",
            Name = "Волк Громовой Короны",
            Tier = 3,
            MinLevel = 35,
            MaxLevel = 35,
            Scale = 3.4f,
            Health = 5200,
            Toughness = 380,
            Chase = 9,
            Bite = 540,
            BiteEvery = 1.1f,
            Sight = 2.2f,
            // Lore: eerie blue scales over the whole body, golden tufts on its claws and tail.
            Fur = new Color(0.16f, 0.26f, 0.5f),
            Back = new Color(0.1f, 0.18f, 0.42f),
            Belly = new Color(0.3f, 0.42f, 0.66f),
            Eye = new Color(1f, 0.85f, 0.35f),
            Glows = true,
            Crown = true,
            Shock = 1.5f,
            BoltEvery = 2.2f,
            BoltPower = 520,
            BoltRange = 34,
            HowlEvery = 10,
            FrenzyBelow = 0.4f,
            ShieldEvery = 15,
            ShieldFor = 5,
            Regen = 12,
            SeesHidden = true,
            StompEvery = 6,
            StompRadius = 7,
            StompPower = 420,
            Spoils = new[] { (ItemBook.WolfFang, 30) },
            Drops = new[] { ("berserk", 0.6f), ("thunder_shield", 0.8f), ("lightning_eye", 0.7f) },
        };

        public static readonly BeastKind[] All = { Grey, Lightning, Bold, Frenzy, ThunderCrown };
    }

    /// <summary>
    /// The other beasts of Qing Mao's forests (lore: a wild boar group has a wild boar king, a monkey group a monkey king;
    /// the Xiong clan keeps black bears, and hunting an adult black bear once earned a green copper relic Gu; jade eye
    /// stone monkeys turn to grey rock as they die and leave a jade green pearl — Jade Skin Gu's food; dark venomous
    /// snakes hold a grudge and never give up their prey). The numbers are the game's own.
    /// </summary>
    public static class BeastKinds
    {
        public static readonly BeastKind Boar = new BeastKind
        {
            Id = "boar",
            Name = "Дикий кабан",
            Shape = BeastShape.Boar,
            MinLevel = 3,
            MaxLevel = 6,
            Tall = 0.95f,
            Girth = 0.42f,
            Health = 150,
            Toughness = 110,
            Chase = 6.6f,
            Bite = 110,
            BiteEvery = 1.3f,
            Sight = 0.8f,
            Fur = new Color(0.27f, 0.22f, 0.19f),
            Back = new Color(0.14f, 0.12f, 0.11f),
            Belly = new Color(0.4f, 0.34f, 0.3f),
            Eye = new Color(0.9f, 0.35f, 0.2f),
            ChargeEvery = 5,
            ChargePower = 170,
            Spoils = new[] { (ItemBook.RawMeat, 3), (ItemBook.BeastBone, 1) },
            Drops = new[] { ("black_boar", 0.06f), ("white_boar", 0.04f) },
        };

        public static readonly BeastKind BoarKing = new BeastKind
        {
            Id = "boar_king",
            Name = "Король диких кабанов",
            Shape = BeastShape.Boar,
            Tier = 1,
            MinLevel = 12,
            MaxLevel = 14,
            Scale = 1.75f,
            Tall = 0.95f,
            Girth = 0.42f,
            Health = 950,
            Toughness = 200,
            Chase = 7.2f,
            Bite = 230,
            BiteEvery = 1.2f,
            Sight = 1,
            Fur = new Color(0.2f, 0.16f, 0.14f),
            Back = new Color(0.45f, 0.42f, 0.38f),
            Belly = new Color(0.34f, 0.28f, 0.24f),
            Eye = new Color(1f, 0.3f, 0.15f),
            Crown = true,
            ChargeEvery = 3.8f,
            ChargePower = 340,
            HowlEvery = 16,
            FrenzyBelow = 0.35f,
            Spoils = new[] { (ItemBook.RawMeat, 12), (ItemBook.BeastBone, 6) },
            Drops = new[] { ("black_and_white_boar", 0.4f), ("flower_boar", 0.35f), ("black_boar", 0.3f) },
        };

        public static readonly BeastKind Bear = new BeastKind
        {
            Id = "bear",
            Name = "Чёрный медведь",
            Shape = BeastShape.Bear,
            MinLevel = 8,
            MaxLevel = 11,
            Scale = 1.35f,
            Tall = 1.25f,
            Girth = 0.5f,
            Health = 650,
            Toughness = 180,
            Chase = 5.6f,
            Bite = 230,
            BiteEvery = 1.6f,
            Sight = 0.9f,
            Fur = new Color(0.1f, 0.09f, 0.09f),
            Back = new Color(0.06f, 0.055f, 0.055f),
            Belly = new Color(0.32f, 0.26f, 0.2f),
            Eye = new Color(0.8f, 0.5f, 0.2f),
            FrenzyBelow = 0.3f,
            Spoils = new[] { (ItemBook.RawMeat, 8), (ItemBook.BeastBone, 4) },
            Drops = new[] { ("green_copper_relic", 0.15f), ("brown_bear_strength", 0.05f) },
        };

        public static readonly BeastKind Monkey = new BeastKind
        {
            Id = "monkey",
            Name = "Нефритовоглазая каменная обезьяна",
            Shape = BeastShape.Monkey,
            MinLevel = 2,
            MaxLevel = 5,
            Scale = 0.8f,
            Tall = 1.1f,
            Girth = 0.32f,
            Health = 80,
            Toughness = 120,
            Chase = 7.4f,
            Bite = 70,
            BiteEvery = 1.1f,
            Sight = 1.1f,
            Fur = new Color(0.46f, 0.46f, 0.44f),
            Back = new Color(0.34f, 0.34f, 0.33f),
            Belly = new Color(0.58f, 0.57f, 0.54f),
            Eye = new Color(0.35f, 1f, 0.55f),
            ThrowEvery = 2.6f,
            ThrowPower = 85,
            Keep = 9,
            Stone = true,
            Spoils = new[] { (ItemBook.JadeRock, 1) },
            Drops = new[] { ("stealth_rock", 0.04f) },
        };

        public static readonly BeastKind MonkeyKing = new BeastKind
        {
            Id = "monkey_king",
            Name = "Король каменных обезьян",
            Shape = BeastShape.Monkey,
            Tier = 1,
            MinLevel = 10,
            MaxLevel = 12,
            Scale = 1.35f,
            Tall = 1.1f,
            Girth = 0.32f,
            Health = 520,
            Toughness = 210,
            Chase = 7.8f,
            Bite = 160,
            BiteEvery = 1,
            Sight = 1.3f,
            Fur = new Color(0.4f, 0.42f, 0.38f),
            Back = new Color(0.25f, 0.4f, 0.3f),
            Belly = new Color(0.6f, 0.6f, 0.55f),
            Eye = new Color(0.4f, 1f, 0.6f),
            Glows = true,
            ThrowEvery = 1.6f,
            ThrowPower = 170,
            Keep = 8,
            HowlEvery = 12,
            Stone = true,
            Spoils = new[] { (ItemBook.JadeRock, 6) },
            Drops = new[] { ("stealth_rock", 0.3f), ("white_jade", 0.12f) },
        };

        public static readonly BeastKind Snake = new BeastKind
        {
            Id = "snake",
            Name = "Тёмная ядовитая змея",
            Shape = BeastShape.Snake,
            MinLevel = 3,
            MaxLevel = 6,
            Tall = 0.45f,
            Girth = 0.25f,
            Health = 70,
            Toughness = 85,
            Chase = 4.4f,
            Bite = 85,
            BiteEvery = 1.4f,
            Sight = 0.7f,
            Fur = new Color(0.1f, 0.12f, 0.1f),
            Back = new Color(0.16f, 0.24f, 0.14f),
            Belly = new Color(0.36f, 0.34f, 0.22f),
            Eye = new Color(1f, 0.85f, 0.2f),
            Venom = 4,
            VenomFor = 6,
            Relentless = true,
            Spoils = new[] { (ItemBook.RawMeat, 1) },
            Drops = new[] { ("stealth_scales", 0.05f) },
        };

        public static readonly BeastKind[] Others = { Boar, BoarKing, Bear, Monkey, MonkeyKing, Snake };

        /// <summary>Every kind of beast: the wolves, then the others.</summary>
        public static readonly BeastKind[] All = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(WolfKinds.All, Others));

        public static BeastKind Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (var kind in All)
            {
                if (kind.Id == id || kind.Name.StartsWith(id, System.StringComparison.OrdinalIgnoreCase))
                {
                    return kind;
                }
            }

            return null;
        }
    }
}
