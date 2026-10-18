using UnityEngine;

namespace GuWorld.Creatures
{
    /// <summary>
    /// A kind of wolf: its name and its rung among the beasts (an ordinary wolf, or a beast king ruling a hundred, a
    /// thousand, ten thousand of its kind — lore: sys_beast_king), the levels it comes in, its size and colours, its
    /// strength, and the Gu-given gifts a beast king fights with (lore: a king's body holds one or two Gu that come out
    /// when it is attacked) — and what it leaves when it dies.
    /// </summary>
    public class WolfKind
    {
        public string Id;
        public string Name;
        /// <summary>0 an ordinary wolf; 1 a hundred beast king, 2 a thousand beast king, 3 a myriad beast king.</summary>
        public int Tier;
        public int MinLevel, MaxLevel;

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

        /// <summary>Fangs it leaves, and the Gu that may come out of its body as it dies (GuBook id, chance).</summary>
        public int Fangs = 1;
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
        public static readonly WolfKind Grey = new WolfKind
        {
            Id = "grey",
            Name = "Серый волк",
            MinLevel = 1,
            MaxLevel = 3,
            Fangs = 1,
        };

        public static readonly WolfKind Lightning = new WolfKind
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
            Fangs = 2,
        };

        public static readonly WolfKind Bold = new WolfKind
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
            Fangs = 5,
            Drops = new[] { ("plasma", 0.25f), ("thunder_potato", 0.2f), ("eating_wind", 0.1f) },
        };

        public static readonly WolfKind Frenzy = new WolfKind
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
            Fangs = 10,
            Drops = new[] { ("thunder_shield", 0.4f), ("lightning_eye", 0.2f), ("thunderwings", 0.12f) },
        };

        public static readonly WolfKind ThunderCrown = new WolfKind
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
            Fangs = 30,
            Drops = new[] { ("berserk", 0.6f), ("thunder_shield", 0.8f), ("lightning_eye", 0.7f) },
        };

        public static readonly WolfKind[] All = { Grey, Lightning, Bold, Frenzy, ThunderCrown };

        public static WolfKind Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (var kind in All)
            {
                if (kind.Id == id || kind.Name.StartsWith(id ?? "", System.StringComparison.OrdinalIgnoreCase))
                {
                    return kind;
                }
            }

            return null;
        }
    }
}
