using GuWorld.Body;
using UnityEngine;

namespace GuWorld.Creatures
{
    /// <summary>
    /// What a Gu Master of another clan fights with: their rank, the Gu in their aperture — one thrown from afar, one for
    /// close quarters, one to shield the body, one for strength — how strong and how hard they are, how they like to
    /// fight (from afar or up close), how quick they are to roll out of the way.
    /// </summary>
    public class MasterKind
    {
        public string Id;
        /// <summary>What they are in words ("ученик клана Бай"), and their clan ("bai", "xiong", "gu_yue").</summary>
        public string Name;
        public string Clan;
        public int Rank = 1;
        /// <summary>How great a foe: 0 an ordinary one, 1 a leader (their bar across the screen when they hunt the hero is a beast king's of tier 2).</summary>
        public int Tier;

        /// <summary>The Gu thrown from afar (a moonblade, a bolt), the one for close quarters (a sweep), the shield, the strength (fists).</summary>
        public string Far;
        public string Close;
        public string Shield;
        public string Might;

        /// <summary>Their health and toughness (Wounds), the strength of their Gu and of their fists (100: a rank-one moonblade).</summary>
        public float Health = 120;
        public float Toughness = 90;
        public float Power = 90;
        public float Fist = 55;
        /// <summary>Seconds between their throws (a little by chance), how far they like to stand from the hero, how fast they run.</summary>
        public float FarEvery = 2.8f;
        public float Keep = 10;
        public float Run = 4.2f;
        /// <summary>The chance they roll out of the way of a missile or a blow they see coming.</summary>
        public float Dodge = 0.4f;
        /// <summary>How long their shield lasts, s, and how much of a blow it takes (0…1).</summary>
        public float ShieldFor = 6;
        public float ShieldTakes = 0.55f;

        /// <summary>How they look: the hanfu's colour, whether a youth (fifteen) or grown.</summary>
        public Robe Robe = Robe.White;
        public bool Youth;

        public string RankName => $"Гу-мастер {Rank} ранга";
    }

    /// <summary>
    /// The Gu Masters of Qing Mao's other clans (lore: the Bai village by the back mountain's waterfall, of water and ice —
    /// Bai Ning Bing's icicles and ice blade, Bai Bing Yi's water drill; the Xiong village on the fore mountain, of the bear's
    /// strength — Xiong Li's and Xiong Lin's brown bear strength, white jade skin), and the Gu Yue's own moonblades, met as
    /// sparring partners on the proving ground.
    /// </summary>
    public static class MasterKinds
    {
        public static readonly MasterKind BaiYouth = new MasterKind
        {
            Id = "bai_youth", Name = "ученик клана Бай", Clan = "bai", Rank = 1, Youth = true,
            Far = "icicle", Shield = "jade_skin",
            Health = 110, Toughness = 85, Power = 85, Fist = 45, FarEvery = 2.6f, Keep = 11, Dodge = 0.35f, ShieldTakes = 0.45f,
            Robe = Robe.White,
        };

        public static readonly MasterKind BaiWater = new MasterKind
        {
            Id = "bai_water", Name = "Гу-мастер клана Бай", Clan = "bai", Rank = 2,
            Far = "icicle", Close = "water_drill", Shield = "white_jade",
            Health = 240, Toughness = 140, Power = 165, Fist = 70, FarEvery = 2.2f, Keep = 9, Run = 4.6f, Dodge = 0.5f, ShieldTakes = 0.6f,
            Robe = Robe.White,
        };

        public static readonly MasterKind BaiElder = new MasterKind
        {
            Id = "bai_elder", Name = "старейшина клана Бай", Clan = "bai", Rank = 3, Tier = 2,
            Far = "frost_moon", Close = "ice_blade", Shield = "ice_crystal",
            Health = 600, Toughness = 230, Power = 300, Fist = 110, FarEvery = 1.8f, Keep = 10, Run = 5, Dodge = 0.6f, ShieldFor = 7, ShieldTakes = 0.7f,
            Robe = Robe.Indigo,
        };

        public static readonly MasterKind XiongYouth = new MasterKind
        {
            Id = "xiong_youth", Name = "ученик клана Сюн", Clan = "xiong", Rank = 1, Youth = true,
            Far = "bone_spear", Shield = "jade_skin", Might = "black_boar",
            Health = 130, Toughness = 95, Power = 80, Fist = 70, FarEvery = 3.4f, Keep = 2, Run = 4.4f, Dodge = 0.25f, ShieldTakes = 0.45f,
            Robe = Robe.Brown,
        };

        public static readonly MasterKind XiongBear = new MasterKind
        {
            Id = "xiong_bear", Name = "Гу-мастер клана Сюн", Clan = "xiong", Rank = 2,
            Far = "spiral_bone_spear", Shield = "white_jade", Might = "brown_bear_strength",
            Health = 300, Toughness = 160, Power = 150, Fist = 150, FarEvery = 3, Keep = 1.6f, Run = 4.8f, Dodge = 0.3f, ShieldTakes = 0.6f,
            Robe = Robe.Brown,
        };

        public static readonly MasterKind XiongElder = new MasterKind
        {
            Id = "xiong_elder", Name = "старейшина клана Сюн", Clan = "xiong", Rank = 3, Tier = 2,
            Far = "spiral_bone_spear", Close = "fiery_claw", Shield = "golden_shield", Might = "brown_bear_strength",
            Health = 700, Toughness = 250, Power = 280, Fist = 240, FarEvery = 2.4f, Keep = 2, Run = 5.2f, Dodge = 0.4f, ShieldFor = 7, ShieldTakes = 0.65f,
            Robe = Robe.Dark,
        };

        public static readonly MasterKind GuYueYouth = new MasterKind
        {
            Id = "gu_yue_youth", Name = "ученик клана Гу Юэ", Clan = "gu_yue", Rank = 1, Youth = true,
            Far = "moonlight", Shield = "jade_skin",
            Health = 110, Toughness = 85, Power = 95, Fist = 45, FarEvery = 2.4f, Keep = 10, Dodge = 0.4f, ShieldTakes = 0.45f,
            Robe = Robe.Green,
        };

        public static readonly MasterKind GuYueMoon = new MasterKind
        {
            Id = "gu_yue_moon", Name = "Гу-мастер клана Гу Юэ", Clan = "gu_yue", Rank = 2,
            Far = "moonglow", Close = "ice_blade", Shield = "white_jade",
            Health = 250, Toughness = 140, Power = 170, Fist = 70, FarEvery = 2, Keep = 9, Run = 4.6f, Dodge = 0.55f, ShieldTakes = 0.6f,
            Robe = Robe.Green,
        };

        /// <summary>
        /// The demonic path's rogues (lore: Gu Masters who answer to no clan and take what they want): a wanderer with ghost
        /// fire, a practised one with stronger fire and a beetle's strength, and a leader with a blood moon and a hand blade.
        /// </summary>
        public static readonly MasterKind DemonWanderer = new MasterKind
        {
            Id = "demon_wanderer", Name = "бродяга демонического пути", Clan = Clans.Demonic, Rank = 1,
            Far = "ghost_fire", Shield = "bronze_skin",
            Health = 140, Toughness = 95, Power = 95, Fist = 60, FarEvery = 2.6f, Keep = 8, Run = 4.6f, Dodge = 0.4f, ShieldTakes = 0.45f,
            Robe = Robe.Grey,
        };

        public static readonly MasterKind DemonCultivator = new MasterKind
        {
            Id = "demon_cultivator", Name = "демонический Гу-мастер", Clan = Clans.Demonic, Rank = 2,
            Far = "ghost_fire_2", Close = "ice_blade", Shield = "black_mane", Might = "brute_beetle",
            Health = 280, Toughness = 150, Power = 175, Fist = 110, FarEvery = 2.2f, Keep = 7, Run = 4.9f, Dodge = 0.5f, ShieldTakes = 0.6f,
            Robe = Robe.Dark,
        };

        public static readonly MasterKind DemonLeader = new MasterKind
        {
            Id = "demon_leader", Name = "главарь демонического пути", Clan = Clans.Demonic, Rank = 3, Tier = 2,
            Far = "blood_moon", Close = "hand_blade", Shield = "steel_mane", Might = "yellow_camel_beetle",
            Health = 650, Toughness = 240, Power = 310, Fist = 200, FarEvery = 1.9f, Keep = 8, Run = 5.2f, Dodge = 0.55f, ShieldFor = 7, ShieldTakes = 0.65f,
            Robe = Robe.Dark,
        };

        public static readonly MasterKind[] All =
        {
            BaiYouth, BaiWater, BaiElder, XiongYouth, XiongBear, XiongElder, GuYueYouth, GuYueMoon, DemonWanderer, DemonCultivator, DemonLeader,
        };

        /// <summary>A demonic rogue by rank (1…3).</summary>
        public static MasterKind Demon(int rank) => Mathf.Clamp(rank, 1, 3) switch
        {
            1 => DemonWanderer,
            2 => DemonCultivator,
            _ => DemonLeader,
        };

        public static MasterKind Find(string id) => System.Array.Find(All, k => k.Id == id);

        /// <summary>A clan's fighters by rank (1…3): the Gu Yue have no elder of theirs here.</summary>
        public static MasterKind Of(string clan, int rank) => (clan, Mathf.Clamp(rank, 1, 3)) switch
        {
            ("xiong", 1) => XiongYouth,
            ("xiong", 2) => XiongBear,
            ("xiong", _) => XiongElder,
            ("gu_yue", 1) => GuYueYouth,
            ("gu_yue", _) => GuYueMoon,
            (Clans.Demonic, var r) => Demon(r),
            (_, 1) => BaiYouth,
            (_, 2) => BaiWater,
            _ => BaiElder,
        };

        /// <summary>The surnames the demonic path's rogues go by.</summary>
        static readonly string[] Rogues = { "Ма", "Ду", "Се", "Ло", "Гун", "Цзян", "Хэй", "Шэ" };

        static readonly string[] Given =
        {
            "Лун", "Сюэ", "Фэн", "Юнь", "Хай", "Цин", "Шань", "Мин", "Тао", "Лэй", "Бо", "Цзе", "Хуа", "Ин", "Сун", "Чэн", "Ли", "Вэй",
        };

        /// <summary>A clan's surname before a given name by chance ("Бай Сюэ", "Сюн Шань").</summary>
        public static string NameFor(string clan, System.Random random)
        {
            var surname = clan switch
            {
                "xiong" => "Сюн",
                "gu_yue" => "Гу Юэ",
                // A rogue's own name: no clan stands behind it.
                Clans.Demonic => Rogues[random.Next(Rogues.Length)],
                _ => "Бай",
            };

            return $"{surname} {Given[random.Next(Given.Length)]}";
        }

        /// <summary>A clan's name in words ("клан Бай").</summary>
        public static string ClanName(string clan) => clan switch
        {
            "xiong" => "клан Сюн",
            "gu_yue" => "клан Гу Юэ",
            Clans.Demonic => "демонический путь",
            _ => "клан Бай",
        };
    }
}
