using GuWorld.Creatures;
using GuWorld.Items;
using UnityEngine;

namespace GuWorld.Cultivation
{
    /// <summary>
    /// The Gu of ranks one to five beyond the first two: each drawn from the lore's record
    /// (its rank, line and what it does where the lore says), the numbers the game's own.
    /// Lines (the lore's): the moon line (Moonlight → Moonglow → the rank-three moons → the
    /// Moonlight Treasure King), the boars and strength Gu, the skins (Bronze Skin the same
    /// at every rank it has), bone spears, liquor worms, relics, stealth.
    /// </summary>
    public static partial class GuBook
    {
        const double Hour = 60;
        const double Day = 24 * 60;

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static readonly Color Fire = C(1f, 0.5f, 0.18f);
        static readonly Color Ice = C(0.72f, 0.92f, 1f);
        static readonly Color Bone = C(0.96f, 0.93f, 0.8f);
        static readonly Color StarLight = C(1f, 0.95f, 0.6f);
        static readonly Color Water = C(0.3f, 0.62f, 1f);
        static readonly Color Thunder = C(0.72f, 0.62f, 1f);
        static readonly Color Blood = C(0.95f, 0.12f, 0.16f);
        static readonly Color Gold = C(1f, 0.8f, 0.3f);
        static readonly Color Light = C(1f, 1f, 0.86f);
        static readonly Color Wind = C(0.7f, 1f, 0.86f);
        static readonly Color Earth = C(0.8f, 0.62f, 0.4f);
        static readonly Color Dark = C(0.45f, 0.4f, 0.6f);

        static void AddCatalogue()
        {
            // ── Rank one ──────────────────────────────────────────────

            Add(new GuKind
            {
                Id = "little_light", LoreRef = "gu_little_light_gu", Name = "Гу Маленького Света", Rank = 1, Kind = "помощь", Line = "лунная линия",
                Icon = "star", Colour = Light, Price = 25,
                About = "Искорка света размером с рисовое зерно. Сама не бьёт, но рядом с Гу Лунного Света делает лунный клинок вдвое больше и дальше.",
                Skill = SkillKind.Amplify, SkillName = "Свет в ладони", Amount = 1.6f,
                Food = ItemBook.Petal, Meal = 1, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "bronze_skin", LoreRef = "gu_bronze_skin_gu", Name = "Гу Бронзовой Кожи I", Rank = 1, Kind = "защита", Line = "бронзовая кожа",
                Icon = "shield", Colour = C(0.9f, 0.68f, 0.32f), Price = 35,
                About = "Жучок с медным отливом. Тело светится жёлтым, кожа твердеет: обычный удар теряет почти треть силы.",
                Skill = SkillKind.Skin, SkillName = "Бронзовая кожа", Cost = 4, Upkeep = 0.35f, Amount = 0.3f,
                Food = ItemBook.IronSand, Meal = 2, MealEvery = 3 * Day, RefineCost = 20,
            });
            Add(new GuKind
            {
                Id = "beast_skin", LoreRef = "gu_beast_skin_gu", Name = "Гу Звериной Шкуры", Rank = 1, Kind = "защита", Line = "кожи",
                Icon = "shield", Colour = Earth, Price = 15,
                About = "Самый простой из кожаных Гу: тело покрывает грубая шкура. Держит меньше бронзы, зато дёшев и неприхотлив.",
                Skill = SkillKind.Skin, SkillName = "Звериная шкура", Cost = 3, Upkeep = 0.2f, Amount = 0.2f,
                Food = ItemBook.RawMeat, Meal = 1, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "white_boar", LoreRef = "gu_white_boar_gu", Name = "Гу Белого Кабана", Rank = 1, Kind = "сила", Line = "кабаны",
                Icon = "fist", Colour = C(0.95f, 0.95f, 0.9f), Price = 120,
                About = "Редкий Гу. Применённый, навсегда даёт телу силу одного кабана — сила остаётся, даже когда Гу уже нет.",
                Skill = SkillKind.Strength, SkillName = "Сила белого кабана", Cost = 20, Amount = 0.25f, RefineCost = 0,
            });
            Add(new GuKind
            {
                Id = "black_boar", LoreRef = "gu_black_boar_gu", Name = "Гу Чёрного Кабана", Rank = 1, Kind = "сила", Line = "кабаны",
                Icon = "fist", Colour = C(0.35f, 0.32f, 0.3f), Price = 120,
                About = "Редкий Гу. Навсегда прибавляет силу кабана — и она складывается с белым кабаном.",
                Skill = SkillKind.Strength, SkillName = "Сила чёрного кабана", Cost = 20, Amount = 0.25f, RefineCost = 0,
            });
            Add(new GuKind
            {
                Id = "black_and_white_boar", LoreRef = "gu_black_and_white_boar_gu", Name = "Гу Чёрно-Белого Кабана", Rank = 1, Kind = "сила", Line = "кабаны",
                Icon = "fist", Colour = C(0.7f, 0.7f, 0.7f),
                About = "Чёрный и белый кабан, сплавленные в одного: ранг тот же, первый, — но силы прибавляет за двоих.",
                Skill = SkillKind.Strength, SkillName = "Сила двух кабанов", Cost = 30, Amount = 0.55f, RefineCost = 0,
            });
            Add(new GuKind
            {
                Id = "flower_boar", LoreRef = "gu_flower_boar_gu", Name = "Гу Цветочного Кабана", Rank = 1, Kind = "сила", Line = "кабаны",
                Icon = "fist", Colour = C(1f, 0.7f, 0.8f), Price = 20,
                About = "Пёстрый Гу: на время даёт силу кабана. Держится дольше, чем жук-дровосек, за ту же эссенцию.",
                Skill = SkillKind.Might, SkillName = "Кабанья сила", Cost = 8, Duration = 25, Amount = 1.5f,
                Food = ItemBook.RawMeat, Meal = 1, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "brute_beetle", LoreRef = "gu_brute_force_longhorn_beetle_gu", Name = "Гу Жука-Дровосека Грубой Силы", Rank = 1, Kind = "сила", Line = "жуки-дровосеки",
                Icon = "gu", Colour = C(0.75f, 0.5f, 0.3f), Price = 20,
                About = "На несколько вдохов даёт силу быка — дорого и коротко, но кулак бьёт вдвое.",
                Skill = SkillKind.Might, SkillName = "Бычья сила", Cost = 10, Duration = 8, Amount = 2f,
                Food = ItemBook.RawMeat, Meal = 1, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "yellow_camel_beetle", LoreRef = "gu_yellow_camel_longhorn_beetle_gu", Name = "Гу Жёлтого Верблюжьего Усача", Rank = 1, Kind = "сила", Line = "жуки-дровосеки",
                Icon = "gu", Colour = C(0.95f, 0.8f, 0.4f), Price = 18,
                About = "Жук из кладовой академии: сила вьючного верблюда — не взрыв, но надолго.",
                Skill = SkillKind.Might, SkillName = "Верблюжья выносливость", Cost = 6, Duration = 40, Amount = 1.3f,
                Food = ItemBook.IntimateGrass, Meal = 2, MealEvery = Day, RefineCost = 12,
            });
            Add(new GuKind
            {
                Id = "jin_strength", LoreRef = "gu_jin_strength_gu", Name = "Гу Силы Цзинь", Rank = 1, Kind = "сила", Line = "сила цзинь",
                Icon = "fist", Colour = Gold, Price = 40,
                About = "Путь силы: применённый, навсегда прибавляет силы на один цзинь. Немного — но навсегда.",
                Skill = SkillKind.Strength, SkillName = "Цзинь силы", Cost = 12, Amount = 0.1f, RefineCost = 0,
            });
            Add(new GuKind
            {
                Id = "bone_spear", LoreRef = "gu_bone_spear_gu", Name = "Гу Костяного Копья", Rank = 1, Kind = "атака", Line = "костяные копья",
                Icon = "spear", Colour = Bone, Price = 35,
                About = "Основа костяного наследия: из ладони вылетает острое белое копьё и пробивает зверя насквозь.",
                Skill = SkillKind.Bolt, SkillName = "Костяное копьё", Cost = 9, Reach = 14, Range = 22, Power = 110, Cooldown = 0.4f,
                Food = ItemBook.BeastBone, Meal = 1, MealEvery = Day, RefineCost = 20,
            });
            Add(new GuKind
            {
                Id = "star_dart", LoreRef = "gu_star_dart_gu", Name = "Гу Звёздного Дротика", Rank = 1, Kind = "атака", Line = "звёзды",
                Icon = "star", Colour = StarLight, Price = 25,
                About = "Обычный звёздный Гу: маленький светлый дротик, быстрый и дешёвый. Бьёт слабее клинка, зато далеко.",
                Skill = SkillKind.Bolt, SkillName = "Звёздный дротик", Cost = 6, Reach = 20, Range = 30, Power = 75, Cooldown = 0.25f, Tire = 15,
                Food = ItemBook.IronSand, Meal = 1, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "ghost_fire", LoreRef = "gu_ghost_fire_gu", Name = "Гу Призрачного Огня I", Rank = 1, Kind = "атака", Line = "призрачный огонь",
                Icon = "flame", Colour = C(0.45f, 0.9f, 1f), Price = 35,
                About = "Бледно-голубой огонёк. Попав, прилипает и жжёт ещё несколько секунд.",
                Skill = SkillKind.Bolt, SkillName = "Призрачный огонь", Cost = 9, Reach = 12, Range = 18, Power = 80,
                Afflict = Affliction.Burn, AfflictFor = 4, AfflictPower = 4,
                Food = ItemBook.Charcoal, Meal = 1, MealEvery = Day, RefineCost = 20,
            });
            Add(new GuKind
            {
                Id = "exploding_egg", LoreRef = "gu_exploding_egg_gu", Name = "Гу Взрывающегося Яйца", Rank = 1, Kind = "атака", Line = "взрывы",
                Icon = "flame", Colour = Fire, Price = 40,
                About = "Пёстрое яйцо, что лопается огнём там, куда его бросили. Против стаи — лучшее на первом ранге.",
                Skill = SkillKind.Burst, SkillName = "Взрыв", Cost = 15, Range = 16, Radius = 3.5f, Power = 90, Cooldown = 1, Tire = 34,
                Food = ItemBook.Charcoal, Meal = 2, MealEvery = 2 * Day, RefineCost = 20,
            });
            Add(new GuKind
            {
                Id = "flash_blink", LoreRef = "gu_flash_blink_gu", Name = "Гу Мгновенной Вспышки", Rank = 1, Kind = "контроль", Line = "свет",
                Icon = "star", Colour = Light, Price = 25,
                About = "Взрывается слепящим белым светом. Звери вокруг теряют вас из виду на несколько секунд.",
                Skill = SkillKind.Flash, SkillName = "Вспышка", Cost = 6, Radius = 9, Duration = 4, Cooldown = 2,
                Food = ItemBook.Petal, Meal = 1, MealEvery = Day, RefineCost = 12,
            });
            Add(new GuKind
            {
                Id = "stealth_rock", LoreRef = "gu_stealth_rock_gu", Name = "Гу Скрытного Камня", Rank = 1, Kind = "скрытность", Line = "скрытность",
                Icon = "eye", Colour = Dark, Price = 45,
                About = "Прячет тело — но не одежду и не следы крови. Звери замечают вас только вблизи.",
                Skill = SkillKind.Hide, SkillName = "Каменная тень", Cost = 3, Upkeep = 0.5f, Amount = 0.3f,
                Food = ItemBook.IronSand, Meal = 1, MealEvery = 2 * Day, RefineCost = 20,
            });
            Add(new GuKind
            {
                Id = "life_breath_grass", LoreRef = "gu_life_breath_grass", Name = "Трава Дыхания Жизни", Rank = 1, Kind = "лечение", Line = "травы жизни",
                Icon = "grass", Colour = C(0.55f, 1f, 0.55f), Price = 30,
                About = "Травяной Гу. Пока в него льётся эссенция, раны понемногу затягиваются.",
                Skill = SkillKind.Mend, SkillName = "Дыхание жизни", Cost = 2, Upkeep = 0.5f, Amount = 2,
                Food = ItemBook.SpringWater, Meal = 1, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "scarecrow", LoreRef = "gu_scarecrow_gu", Name = "Гу Пугала", Rank = 1, Kind = "уловка", Line = "травяные куклы",
                Icon = "grass", Colour = C(0.9f, 0.82f, 0.5f), Price = 15,
                About = "Травяная кукла, на которой в академии учатся бить. В бою её ставят, чтобы звери кинулись на неё.",
                Skill = SkillKind.Decoy, SkillName = "Соломенный двойник", Cost = 8, Duration = 10, Cooldown = 3,
                Food = ItemBook.IntimateGrass, Meal = 1, MealEvery = Day, RefineCost = 10,
            });
            Add(new GuKind
            {
                Id = "liquor_worm", LoreRef = "gu_liquor_worm", Name = "Винный Червь", Rank = 1, Kind = "очищение", Line = "винные черви",
                Icon = "worm", Colour = C(1f, 0.9f, 0.6f), Price = 90,
                About = "Пухлый белый червь, любит вино. Очищает эссенцию первого ранга на малую ступень: каждый Гу стоит меньше.",
                Skill = SkillKind.Refine, SkillName = "Очищение эссенции",
                Food = ItemBook.BambooWine, Meal = 1, MealEvery = 2 * Day, RefineCost = 25,
            });
            Add(new GuKind
            {
                Id = "green_copper_relic", LoreRef = "gu_green_copper_relic_gu", Name = "Гу Реликвии Зелёной Меди", Rank = 1, Kind = "культивация", Line = "реликвии",
                Icon = "gu", Colour = C(0.3f, 0.8f, 0.55f), Price = 150,
                About = "Расходный Гу для мастеров первого ранга: применённый, поднимает на малую ступень — и исчезает.",
                Skill = SkillKind.Relic, SkillName = "Подъём на ступень", Cost = 10, RefineCost = 0,
            });

            // ── Rank two ──────────────────────────────────────────────

            Add(new GuKind
            {
                Id = "moonglow", LoreRef = "gu_moonglow_gu", Name = "Гу Лунного Сияния", Rank = 2, Kind = "атака", Line = "лунная линия",
                Icon = "moon", Colour = C(0.65f, 0.85f, 1f),
                About = "Гу Лунного Света, сплавленный с двумя Маленькими Светами. Клинок крупнее и бьёт вдвое.",
                Skill = SkillKind.Moonblade, SkillName = "Лунное сияние", Cost = 10, Reach = 12, Range = 20, Power = 190, Cooldown = 0.25f, Amount = 1.3f, Tire = 18,
                Food = ItemBook.Petal, Meal = 3, MealEvery = 12 * Hour, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "moon_raiment", LoreRef = "gu_moon_raiment", Name = "Лунное Одеяние", Rank = 2, Kind = "защита", Line = "лунная линия",
                Icon = "moon", Colour = C(0.7f, 0.85f, 1f),
                About = "Нефритовая кожа и лунный свет в одном Гу: тело одевает бледное сияние, гасящее почти половину удара.",
                Skill = SkillKind.Skin, SkillName = "Лунное одеяние", Cost = 6, Upkeep = 0.45f, Amount = 0.45f,
                Food = ItemBook.Petal, Meal = 3, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "white_jade", LoreRef = "gu_white_jade_gu", Name = "Гу Белого Нефрита", Rank = 2, Kind = "защита", Line = "нефритовая линия",
                Icon = "jade", Colour = C(0.9f, 1f, 0.95f),
                About = "Белый кабан и нефритовая кожа, сплавленные вместе. Белый нефрит держит любой удар, пока есть чем платить.",
                Skill = SkillKind.JadeSkin, SkillName = "Белый нефрит", Cost = 3, Upkeep = 0.3f, PerHarm = 0.06f, Cooldown = 1,
                Food = ItemBook.JadeRock, Meal = 3, MealEvery = 10 * Day, RefineCost = 18,
            });
            Add(new GuKind
            {
                Id = "black_mane", LoreRef = "gu_black_mane_gu", Name = "Гу Чёрной Гривы", Rank = 2, Kind = "защита", Line = "гривы",
                Icon = "shield", Colour = C(0.3f, 0.28f, 0.3f),
                About = "Тело обрастает чёрной жёсткой гривой — бронёй, что держит клык и коготь.",
                Skill = SkillKind.Skin, SkillName = "Чёрная грива", Cost = 5, Upkeep = 0.35f, Amount = 0.4f,
                Food = ItemBook.RawMeat, Meal = 2, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "carapace", LoreRef = "gu_carapace_gu", Name = "Гу Панциря", Rank = 2, Kind = "защита", Line = "панцири",
                Icon = "shield", Colour = C(0.55f, 0.7f, 0.5f), Price = 160,
                About = "Твёрдый панцирь на спине и плечах. Держит меньше гривы, но почти не тянет эссенцию.",
                Skill = SkillKind.Skin, SkillName = "Панцирь", Cost = 4, Upkeep = 0.12f, Amount = 0.3f,
                Food = ItemBook.BeastBone, Meal = 2, MealEvery = 3 * Day, RefineCost = 14,
            });
            Add(new GuKind
            {
                Id = "bronze_skin_2", LoreRef = "gu_bronze_skin_gu", Name = "Гу Бронзовой Кожи II", Rank = 2, Kind = "защита", Line = "бронзовая кожа",
                Icon = "shield", Colour = C(0.95f, 0.72f, 0.35f),
                About = "Тот же бронзовый Гу, но второго ранга: кожа твёрже, свет гуще.",
                Skill = SkillKind.Skin, SkillName = "Бронзовая кожа", Cost = 5, Upkeep = 0.35f, Amount = 0.45f,
                Food = ItemBook.IronSand, Meal = 4, MealEvery = 3 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "spiral_bone_spear", LoreRef = "gu_spiral_bone_spear_gu", Name = "Гу Спирального Костяного Копья", Rank = 2, Kind = "атака", Line = "костяные копья",
                Icon = "spear", Colour = Bone,
                About = "Костяное копьё, закрученное спиралью: летит дальше и пробивает глубже.",
                Skill = SkillKind.Bolt, SkillName = "Спиральное копьё", Cost = 9, Reach = 18, Range = 28, Power = 220, Cooldown = 0.4f,
                Food = ItemBook.BeastBone, Meal = 2, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "star_arrow", LoreRef = "gu_star_arrow_gu", Name = "Гу Звёздной Стрелы", Rank = 2, Kind = "атака", Line = "звёзды",
                Icon = "star", Colour = StarLight,
                About = "Длинная звёздная стрела с острым концом. Самый дальний удар второго ранга.",
                Skill = SkillKind.Bolt, SkillName = "Звёздная стрела", Cost = 7, Reach = 28, Range = 40, Power = 170, Cooldown = 0.3f, Tire = 15,
                Food = ItemBook.IronSand, Meal = 2, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "ghost_fire_2", LoreRef = "gu_ghost_fire_gu", Name = "Гу Призрачного Огня II", Rank = 2, Kind = "атака", Line = "призрачный огонь",
                Icon = "flame", Colour = C(0.4f, 0.85f, 1f),
                About = "Призрачный огонь второго ранга: тот же Гу, но жар держится дольше и злее.",
                Skill = SkillKind.Bolt, SkillName = "Призрачный огонь", Cost = 9, Reach = 14, Range = 20, Power = 160,
                Afflict = Affliction.Burn, AfflictFor = 5, AfflictPower = 7,
                Food = ItemBook.Charcoal, Meal = 2, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "icicle", LoreRef = "gu_icicle_gu", Name = "Гу Сосульки", Rank = 2, Kind = "атака", Line = "лёд",
                Icon = "flake", Colour = Ice, Price = 200,
                About = "Острые сосульки, пущенные издалека. Раненый холодом зверь двигается вдвое медленнее.",
                Skill = SkillKind.Bolt, SkillName = "Сосулька", Cost = 8, Reach = 16, Range = 26, Power = 150,
                Afflict = Affliction.Slow, AfflictFor = 4, AfflictPower = 0.5f,
                Food = ItemBook.SpringWater, Meal = 2, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "ice_blade", LoreRef = "gu_ice_blade_gu", Name = "Гу Ледяного Клинка", Rank = 2, Kind = "атака", Line = "лёд",
                Icon = "flake", Colour = Ice,
                About = "Ледяные лезвия на руках — для ближнего боя. Взмах режет всех перед вами.",
                Skill = SkillKind.Cleave, SkillName = "Ледяной взмах", Cost = 7, Reach = 3.2f, Power = 200, Cooldown = 0.5f,
                Afflict = Affliction.Slow, AfflictFor = 2, AfflictPower = 0.3f,
                Food = ItemBook.SpringWater, Meal = 2, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "water_drill", LoreRef = "gu_water_drill_gu", Name = "Гу Водяного Бура", Rank = 2, Kind = "атака", Line = "вода",
                Icon = "drop", Colour = Water,
                About = "Вращающийся водяной бур на кулаке: пробивает камень и сталь. Бьёт в упор, но страшно.",
                Skill = SkillKind.Cleave, SkillName = "Водяной бур", Cost = 10, Reach = 2.4f, Power = 300, Cooldown = 0.8f, Tire = 30,
                Food = ItemBook.SpringWater, Meal = 3, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "plasma", LoreRef = "gu_plasma_gu", Name = "Гу Плазмы", Rank = 2, Kind = "атака", Line = "гром",
                Icon = "bolt", Colour = C(0.4f, 0.6f, 1f),
                About = "Выбрасывает синюю плазму. Бьёт не сильно, но плазма держится и жжёт.",
                Skill = SkillKind.Bolt, SkillName = "Плазма", Cost = 8, Reach = 12, Range = 18, Power = 120, Radius = 2,
                Afflict = Affliction.Burn, AfflictFor = 4, AfflictPower = 6,
                Food = ItemBook.Charcoal, Meal = 2, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "onion_explosion", LoreRef = "gu_onion_explosion_gu", Name = "Гу Лукового Взрыва", Rank = 2, Kind = "атака", Line = "взрывы",
                Icon = "flame", Colour = C(1f, 0.75f, 0.4f),
                About = "Знаменитый Гу из лукового ущелья: слой за слоем раскрывается взрывом.",
                Skill = SkillKind.Burst, SkillName = "Луковый взрыв", Cost = 14, Range = 18, Radius = 4.5f, Power = 180, Cooldown = 1, Tire = 34,
                Food = ItemBook.Charcoal, Meal = 3, MealEvery = 2 * Day, RefineCost = 16,
            });
            Add(new GuKind
            {
                Id = "thunder_potato", LoreRef = "gu_charred_thunder_potato_gu", Name = "Гу Обугленного Громового Картофеля", Rank = 2, Kind = "контроль", Line = "гром",
                Icon = "bolt", Colour = Thunder,
                About = "Травяной Гу, что рвётся громом от сотрясения. Оглушает тех, кто рядом.",
                Skill = SkillKind.Burst, SkillName = "Громовой взрыв", Cost = 12, Range = 14, Radius = 4, Power = 130, Cooldown = 1.2f, Tire = 30,
                Afflict = Affliction.Root, AfflictFor = 2,
                Food = ItemBook.SpringWater, Meal = 2, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "shadow_follower", LoreRef = "gu_shadow_follower_gu", Name = "Гу Теневого Спутника", Rank = 2, Kind = "движение", Line = "тени",
                Icon = "eye", Colour = Dark,
                About = "Окутывает тьмой и переносит сквозь тени: миг — и вы там, куда смотрели.",
                Skill = SkillKind.Blink, SkillName = "Шаг сквозь тень", Cost = 12, Range = 10, Cooldown = 1.5f, Tire = 30,
                Food = ItemBook.IntimateGrass, Meal = 3, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "stealth_scales", LoreRef = "gu_stealth_scales_gu", Name = "Гу Скрытной Чешуи", Rank = 2, Kind = "скрытность", Line = "скрытность",
                Icon = "eye", Colour = Dark,
                About = "Скрытный камень, доросший до чешуи: прячет и тело, и одежду. Звери почти не видят вас.",
                Skill = SkillKind.Hide, SkillName = "Скрытная чешуя", Cost = 4, Upkeep = 0.45f, Amount = 0.1f,
                Food = ItemBook.IronSand, Meal = 2, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "earth_ear_grass", LoreRef = "gu_earth_communication_ear_grass", Name = "Трава Земного Уха", Rank = 2, Kind = "разведка", Line = "травы-слухачи",
                Icon = "grass", Colour = Earth, Price = 180,
                About = "Корни уходят в землю и слушают: кто бежит, сколько их, откуда.",
                Skill = SkillKind.Sense, SkillName = "Слух земли", Cost = 3, Upkeep = 0.2f, Range = 120,
                Food = ItemBook.SpringWater, Meal = 2, MealEvery = 2 * Day, RefineCost = 14,
            });
            Add(new GuKind
            {
                Id = "nine_leaf_grass", LoreRef = "gu_nine_leaf_vitality_grass", Name = "Девятилистная Трава Жизни", Rank = 2, Kind = "лечение", Line = "травы жизни",
                Icon = "grass", Colour = C(0.45f, 1f, 0.5f), Price = 220,
                About = "Целебная трава второго ранга. Лист жизненной силы, сорванный разом, закрывает раны.",
                Skill = SkillKind.Heal, SkillName = "Лист жизни", Cost = 15, Amount = 35, Cooldown = 3, Tire = 40,
                Food = ItemBook.SpringWater, Meal = 3, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "four_flavors_liquor", LoreRef = "gu_four_flavors_liquor_worm", Name = "Четырёхвкусный Винный Червь", Rank = 2, Kind = "очищение", Line = "винные черви",
                Icon = "worm", Colour = C(1f, 0.8f, 0.5f),
                About = "Винный червь, вкусивший кислого, сладкого, горького и острого. Очищает эссенцию второго ранга.",
                Skill = SkillKind.Refine, SkillName = "Очищение эссенции",
                Food = ItemBook.BambooWine, Meal = 2, MealEvery = 2 * Day, RefineCost = 20,
            });
            Add(new GuKind
            {
                Id = "red_steel_relic", LoreRef = "gu_red_steel_relic_gu", Name = "Реликтовый Гу Красной Стали", Rank = 2, Kind = "культивация", Line = "реликвии",
                Icon = "gu", Colour = C(0.85f, 0.35f, 0.3f), Price = 600,
                About = "Реликвия второго ранга: мастер второго ранга поднимается на малую ступень.",
                Skill = SkillKind.Relic, SkillName = "Подъём на ступень", Cost = 10, RefineCost = 0,
            });
            Add(new GuKind
            {
                Id = "ten_jin_strength", LoreRef = "gu_strength_of_ten_jin_gu", Name = "Гу Силы в Десять Цзиней", Rank = 2, Kind = "сила", Line = "сила цзинь",
                Icon = "fist", Colour = Gold,
                About = "Сила цзинь, выросшая вдесятеро: навсегда прибавляет десять цзиней силы.",
                Skill = SkillKind.Strength, SkillName = "Десять цзиней", Cost = 14, Amount = 0.35f, RefineCost = 0,
            });
            Add(new GuKind
            {
                Id = "brown_bear_strength", LoreRef = "gu_brown_bear_innate_strength_gu", Name = "Гу Врождённой Силы Бурого Медведя", Rank = 2, Kind = "сила", Line = "звериная сила",
                Icon = "fist", Colour = Earth,
                About = "Навсегда даёт телу силу бурого медведя.",
                Skill = SkillKind.Strength, SkillName = "Медвежья сила", Cost = 18, Amount = 0.6f, RefineCost = 0,
            });
            Add(new GuKind
            {
                Id = "wolf_enslavement", LoreRef = "gu_wolf_enslavement_gu", Name = "Гу Порабощения Волка", Rank = 2, Kind = "порабощение", Line = "порабощение волков",
                Icon = "wolf", Colour = C(0.9f, 0.6f, 0.4f),
                About = "Попав в волка, ломает его волю: зверь идёт за вами и больше не бросается.",
                Skill = SkillKind.Tame, SkillName = "Порабощение", Cost = 15, Reach = 14, Range = 18, Cooldown = 2, Tire = 40,
                Afflict = Affliction.Tame,
                Food = ItemBook.RawMeat, Meal = 3, MealEvery = 2 * Day, RefineCost = 18,
            });
            Add(new GuKind
            {
                Id = "eating_wind", LoreRef = "gu_eating_wind_gu", Name = "Гу Поедания Ветра", Rank = 2, Kind = "движение", Line = "ветер",
                Icon = "wing", Colour = Wind,
                About = "Ветер, проглоченный Гу, несёт мастера: бег вдвое быстрее на полминуты.",
                Skill = SkillKind.Haste, SkillName = "Глоток ветра", Cost = 10, Duration = 30, Amount = 1.6f, Cooldown = 2,
                Food = ItemBook.IntimateGrass, Meal = 3, MealEvery = Day, RefineCost = 15,
            });

            // ── Rank three ────────────────────────────────────────────

            Add(new GuKind
            {
                Id = "golden_moon", LoreRef = "gu_golden_moon_gu", Name = "Гу Золотой Луны", Rank = 3, Kind = "атака", Line = "лунная линия",
                Icon = "moon", Colour = C(1f, 0.85f, 0.45f),
                About = "Лунное сияние третьего ранга: золотой полумесяц ростом в полчеловека.",
                Skill = SkillKind.Moonblade, SkillName = "Золотая луна", Cost = 10, Reach = 14, Range = 22, Power = 420, Cooldown = 0.3f, Amount = 1.7f, Tire = 20,
                Food = ItemBook.Petal, Meal = 6, MealEvery = 12 * Hour, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "frost_moon", LoreRef = "gu_frost_moon_gu", Name = "Гу Морозной Луны", Rank = 3, Kind = "атака", Line = "лунная линия",
                Icon = "moon", Colour = C(0.85f, 0.95f, 1f),
                About = "Белый ледяной клинок: раненого сковывает холод, и он еле движется.",
                Skill = SkillKind.Moonblade, SkillName = "Морозная луна", Cost = 10, Reach = 13, Range = 20, Power = 360, Cooldown = 0.3f, Amount = 1.4f, Tire = 20,
                Afflict = Affliction.Slow, AfflictFor = 5, AfflictPower = 0.6f,
                Food = ItemBook.SpringWater, Meal = 4, MealEvery = 12 * Hour, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "blood_moon", LoreRef = "gu_blood_moon_gu", Name = "Гу Кровавой Луны", Rank = 3, Kind = "атака", Line = "лунная линия",
                Icon = "moon", Colour = Blood,
                About = "Кроваво-красный клинок размером с лицо. Раны от него не закрываются и кровоточат.",
                Skill = SkillKind.Moonblade, SkillName = "Кровавая луна", Cost = 10, Reach = 12, Range = 18, Power = 340, Cooldown = 0.3f, Amount = 1.2f, Tire = 20,
                Afflict = Affliction.Burn, AfflictFor = 6, AfflictPower = 8,
                Food = ItemBook.RawMeat, Meal = 3, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "bronze_skin_3", LoreRef = "gu_bronze_skin_gu", Name = "Гу Бронзовой Кожи III", Rank = 3, Kind = "защита", Line = "бронзовая кожа",
                Icon = "shield", Colour = C(1f, 0.75f, 0.38f),
                About = "Бронзовая кожа третьего ранга: тот же Гу, выросший до старого бронзового блеска.",
                Skill = SkillKind.Skin, SkillName = "Бронзовая кожа", Cost = 5, Upkeep = 0.35f, Amount = 0.55f,
                Food = ItemBook.IronSand, Meal = 8, MealEvery = 3 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "steel_mane", LoreRef = "gu_steel_mane_gu", Name = "Гу Стальной Гривы", Rank = 3, Kind = "защита", Line = "гривы",
                Icon = "shield", Colour = C(0.75f, 0.78f, 0.82f),
                About = "Чёрная грива, ставшая сталью. Знаменитый Гу линии чёрного кабана.",
                Skill = SkillKind.Skin, SkillName = "Стальная грива", Cost = 6, Upkeep = 0.4f, Amount = 0.6f,
                Food = ItemBook.IronSand, Meal = 6, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "heavenly_mugwort", LoreRef = "gu_heavenly_mugwort_gu", Name = "Гу Небесной Полыни", Rank = 3, Kind = "защита", Line = "нефритовая линия",
                Icon = "jade", Colour = C(0.85f, 1f, 0.85f),
                About = "Белый нефрит третьего ранга: тело крепко, как нефрит, и сам лунный клинок теряет силу.",
                Skill = SkillKind.JadeSkin, SkillName = "Небесная полынь", Cost = 3, Upkeep = 0.3f, PerHarm = 0.035f, Cooldown = 1,
                Food = ItemBook.JadeRock, Meal = 5, MealEvery = 10 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "golden_shield", LoreRef = "gu_golden_shield_gu", Name = "Гу Золотого Щита", Rank = 3, Kind = "защита", Line = "золотые щиты",
                Icon = "shield", Colour = Gold,
                About = "Золотой свет облекает мастера бронёй.",
                Skill = SkillKind.Skin, SkillName = "Золотой щит", Cost = 7, Upkeep = 0.45f, Amount = 0.65f,
                Food = ItemBook.IronSand, Meal = 6, MealEvery = 3 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "thunder_shield", LoreRef = "gu_thunder_shield_gu", Name = "Гу Громового Щита", Rank = 3, Kind = "защита", Line = "гром",
                Icon = "bolt", Colour = Thunder,
                About = "Полукруг молний перед мастером — Гу громового волка с гор Цинмао.",
                Skill = SkillKind.Skin, SkillName = "Громовой щит", Cost = 7, Upkeep = 0.5f, Amount = 0.55f,
                Food = ItemBook.Charcoal, Meal = 5, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "ice_crystal", LoreRef = "gu_ice_crystal_gu", Name = "Гу Ледяного Кристалла", Rank = 3, Kind = "защита", Line = "лёд",
                Icon = "flake", Colour = Ice,
                About = "Обращает тело в твёрдый лёд. Из трёх знаменитых ледяных превращений — то, что подходит мужчинам.",
                Skill = SkillKind.Skin, SkillName = "Ледяное тело", Cost = 8, Upkeep = 0.5f, Amount = 0.7f,
                Food = ItemBook.SpringWater, Meal = 6, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "therapy_light", LoreRef = "gu_therapy_light_gu", Name = "Гу Целительного Света", Rank = 3, Kind = "лечение", Line = "свет",
                Icon = "heart", Colour = C(0.75f, 1f, 0.75f),
                About = "Тёплый свет разом останавливает кровь и затягивает лёгкие раны, тяжёлые — наполовину.",
                Skill = SkillKind.Heal, SkillName = "Целительный свет", Cost = 15, Amount = 60, Cooldown = 3, Tire = 40,
                Food = ItemBook.Petal, Meal = 8, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "lava_explosion", LoreRef = "gu_lava_explosion_gu", Name = "Гу Лавового Взрыва", Rank = 3, Kind = "атака", Line = "огонь",
                Icon = "flame", Colour = C(1f, 0.38f, 0.1f),
                About = "Гу лавового крокодила: шар лавы, лопающийся огнём.",
                Skill = SkillKind.Bolt, SkillName = "Лавовый шар", Cost = 12, Reach = 16, Range = 24, Power = 380, Radius = 4, Cooldown = 0.8f, Tire = 30,
                Afflict = Affliction.Burn, AfflictFor = 4, AfflictPower = 10,
                Food = ItemBook.Charcoal, Meal = 6, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "fire_pellet", LoreRef = "gu_fire_pellet_gu", Name = "Гу Огненного Шарика", Rank = 3, Kind = "атака", Line = "огонь",
                Icon = "flame", Colour = Fire,
                About = "Быстрые огненные шарики — один за другим.",
                Skill = SkillKind.Bolt, SkillName = "Огненный шарик", Cost = 6, Reach = 16, Range = 24, Power = 280, Cooldown = 0.2f, Tire = 14,
                Afflict = Affliction.Burn, AfflictFor = 3, AfflictPower = 6,
                Food = ItemBook.Charcoal, Meal = 4, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "fiery_claw", LoreRef = "gu_fiery_claw_gu", Name = "Гу Огненного Когтя", Rank = 3, Kind = "атака", Line = "огонь",
                Icon = "flame", Colour = Fire,
                About = "Из отпечатка ладони рвётся огромный оранжевый коготь — вблизи.",
                Skill = SkillKind.Cleave, SkillName = "Огненный коготь", Cost = 9, Reach = 4.5f, Power = 400, Cooldown = 0.7f,
                Afflict = Affliction.Burn, AfflictFor = 3, AfflictPower = 10,
                Food = ItemBook.Charcoal, Meal = 5, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "tornado", LoreRef = "gu_tornado_gu", Name = "Гу Торнадо", Rank = 3, Kind = "атака", Line = "ветер",
                Icon = "wing", Colour = Wind,
                About = "Вихрь, что поднимается там, куда указали, и швыряет всё вокруг.",
                Skill = SkillKind.Burst, SkillName = "Торнадо", Cost = 14, Range = 22, Radius = 5.5f, Power = 300, Cooldown = 1.5f, Tire = 34,
                Afflict = Affliction.Root, AfflictFor = 1.5f,
                Food = ItemBook.IntimateGrass, Meal = 8, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "broadsword_light", LoreRef = "gu_broadsword_light_gu", Name = "Гу Сияния Меча", Rank = 3, Kind = "атака", Line = "мечи",
                Icon = "spear", Colour = Light,
                About = "В руках мастера возникает меч из света — широкий взмах режет всех перед ним.",
                Skill = SkillKind.Cleave, SkillName = "Световой меч", Cost = 9, Reach = 5, Power = 420, Cooldown = 0.6f,
                Food = ItemBook.IronSand, Meal = 5, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "hand_blade", LoreRef = "gu_hand_blade_gu", Name = "Гу Руки-Клинка", Rank = 3, Kind = "атака", Line = "мечи",
                Icon = "fist", Colour = C(0.85f, 0.88f, 0.95f),
                About = "Рёбра ладоней становятся лезвиями. Быстро и дёшево — ближний бой.",
                Skill = SkillKind.Cleave, SkillName = "Ладонь-клинок", Cost = 5, Reach = 2.6f, Power = 330, Cooldown = 0.3f, Tire = 15,
                Food = ItemBook.IronSand, Meal = 4, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "blue_bird_ice_coffin", LoreRef = "gu_blue_bird_ice_coffin_gu", Name = "Гу Ледяного Гроба Синей Птицы", Rank = 3, Kind = "контроль", Line = "лёд",
                Icon = "flake", Colour = C(0.45f, 0.75f, 1f),
                About = "Ледяная синяя птица летит к цели и лопается ледяным гробом: зверь заперт во льду.",
                Skill = SkillKind.Bolt, SkillName = "Ледяной гроб", Cost = 14, Reach = 20, Range = 30, Power = 260, Cooldown = 2, Tire = 35,
                Afflict = Affliction.Root, AfflictFor = 6,
                Food = ItemBook.SpringWater, Meal = 6, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "water_prison", LoreRef = "gu_water_prison_gu", Name = "Гу Водяной Тюрьмы", Rank = 3, Kind = "контроль", Line = "вода",
                Icon = "drop", Colour = Water,
                About = "Синий водяной шар растёт вокруг цели и держит её.",
                Skill = SkillKind.Bolt, SkillName = "Водяная тюрьма", Cost = 12, Reach = 16, Range = 22, Power = 120, Cooldown = 2, Tire = 32,
                Afflict = Affliction.Root, AfflictFor = 5,
                Food = ItemBook.SpringWater, Meal = 5, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "frost_breath", LoreRef = "gu_frost_breath_gu", Name = "Гу Морозного Дыхания", Rank = 3, Kind = "контроль", Line = "лёд",
                Icon = "flake", Colour = Ice,
                About = "Выдох холода: обмороженные звери вокруг едва шевелятся.",
                Skill = SkillKind.Cleave, SkillName = "Морозное дыхание", Cost = 5, Reach = 6, Power = 150, Cooldown = 0.8f,
                Afflict = Affliction.Slow, AfflictFor = 5, AfflictPower = 0.7f,
                Food = ItemBook.SpringWater, Meal = 5, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "eagle_wings", LoreRef = "gu_eagle_wings_gu", Name = "Гу Орлиных Крыльев", Rank = 3, Kind = "движение", Line = "крылья",
                Icon = "wing", Colour = Earth,
                About = "Орлиные крылья за спиной: прыжок втрое выше, падение не страшно.",
                Skill = SkillKind.Leap, SkillName = "Орлиные крылья", Cost = 10, Duration = 30, Amount = 1.8f, Cooldown = 2,
                Food = ItemBook.RawMeat, Meal = 4, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "thunderwings", LoreRef = "gu_thunderwings_gu", Name = "Гу Громовых Крыльев", Rank = 3, Kind = "движение", Line = "крылья",
                Icon = "bolt", Colour = Thunder,
                About = "Пара молниевых крыльев: короткий полёт — вверх и вперёд.",
                Skill = SkillKind.Flight, SkillName = "Громовые крылья", Cost = 12, Duration = 4, Amount = 14, Cooldown = 3, Tire = 34,
                Food = ItemBook.Charcoal, Meal = 5, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "water_trace", LoreRef = "gu_water_trace_gu", Name = "Гу Водяного Следа", Rank = 3, Kind = "движение", Line = "вода",
                Icon = "drop", Colour = Water,
                About = "Быстрый бег, но мокрые следы остаются за вами.",
                Skill = SkillKind.Haste, SkillName = "Водяной след", Cost = 8, Duration = 40, Amount = 1.9f, Cooldown = 2,
                Food = ItemBook.SpringWater, Meal = 4, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "lightning_eye", LoreRef = "gu_lightning_eye_gu", Name = "Гу Молниевого Глаза", Rank = 3, Kind = "разведка", Line = "гром",
                Icon = "eye", Colour = Thunder,
                About = "Глаз громового волка: видит сквозь маскировку и замечает зверей далеко.",
                Skill = SkillKind.Sense, SkillName = "Молниевый глаз", Cost = 4, Upkeep = 0.25f, Range = 220,
                Food = ItemBook.Charcoal, Meal = 4, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "breath_concealment", LoreRef = "gu_breath_concealment_gu", Name = "Гу Сокрытия Дыхания", Rank = 3, Kind = "скрытность", Line = "скрытность",
                Icon = "grass", Colour = Dark,
                About = "Травинка в ухе прячет дыхание и ауру: звери чуют вас вдвое хуже. Работает сама.",
                Skill = SkillKind.Scent, SkillName = "Скрытое дыхание", Amount = 0.5f,
                Food = ItemBook.IntimateGrass, Meal = 4, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "iron_bone", LoreRef = "gu_iron_bone_gu", Name = "Гу Железной Кости", Rank = 3, Kind = "тело", Line = "кости",
                Icon = "bone", Colour = C(0.7f, 0.72f, 0.78f),
                About = "Расходный Гу: перековывает скелет в железо. Больно до крика — но кости остаются железными.",
                Skill = SkillKind.Bones, SkillName = "Железные кости", Cost = 20, Amount = 0.5f, RefineCost = 0,
            });
            Add(new GuKind
            {
                Id = "ice_muscles", LoreRef = "gu_ice_muscles_gu", Name = "Гу Ледяных Мышц", Rank = 3, Kind = "тело", Line = "лёд",
                Icon = "flake", Colour = Ice,
                About = "Навсегда переделывает плоть в ледяные мышцы — защита, которой не нужна эссенция.",
                Skill = SkillKind.Bones, SkillName = "Ледяные мышцы", Cost = 20, Amount = 0.4f, RefineCost = 0,
            });
            Add(new GuKind
            {
                Id = "jun_strength", LoreRef = "gu_jun_strength_gu", Name = "Гу Силы Цзюнь", Rank = 3, Kind = "сила", Line = "сила цзинь",
                Icon = "fist", Colour = Gold,
                About = "Один цзюнь — тридцать цзиней. Навсегда.",
                Skill = SkillKind.Strength, SkillName = "Цзюнь силы", Cost = 16, Amount = 0.8f, RefineCost = 0,
            });
            Add(new GuKind
            {
                Id = "seven_fragrances_liquor", LoreRef = "gu_seven_fragrances_liquor_worm", Name = "Хмельной Червь Семи Ароматов", Rank = 3, Kind = "очищение", Line = "винные черви",
                Icon = "worm", Colour = C(1f, 0.7f, 0.85f),
                About = "Третья ступень винных червей: очищает эссенцию третьего ранга.",
                Skill = SkillKind.Refine, SkillName = "Очищение эссенции",
                Food = ItemBook.WhiteGrains, Meal = 2, MealEvery = 2 * Day, RefineCost = 18,
            });
            Add(new GuKind
            {
                Id = "white_silver_relic", LoreRef = "gu_white_silver_relic_gu", Name = "Реликтовый Гу Белого Серебра", Rank = 3, Kind = "культивация", Line = "реликвии",
                Icon = "gu", Colour = C(0.85f, 0.88f, 0.92f),
                About = "Реликвия третьего ранга: малая ступень для мастера третьего ранга.",
                Skill = SkillKind.Relic, SkillName = "Подъём на ступень", Cost = 10, RefineCost = 0,
            });

            // ── Rank four ─────────────────────────────────────────────

            Add(new GuKind
            {
                Id = "frost_arrow", LoreRef = "gu_frost_arrow_gu", Name = "Гу Морозной Стрелы", Rank = 4, Kind = "атака", Line = "лёд",
                Icon = "flake", Colour = Ice,
                About = "Стрела мороза — главное её достоинство в том, что она дёшева.",
                Skill = SkillKind.Bolt, SkillName = "Морозная стрела", Cost = 4, Reach = 30, Range = 44, Power = 700, Cooldown = 0.3f, Tire = 15,
                Afflict = Affliction.Slow, AfflictFor = 4, AfflictPower = 0.6f,
                Food = ItemBook.SpringWater, Meal = 10, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "fiery_snake", LoreRef = "gu_fiery_snake_gu", Name = "Гу Огненной Змеи", Rank = 4, Kind = "атака", Line = "огонь",
                Icon = "flame", Colour = Fire,
                About = "Огненная змея, что растёт в полёте в целого питона.",
                Skill = SkillKind.Bolt, SkillName = "Огненная змея", Cost = 10, Reach = 22, Range = 32, Power = 800, Radius = 3, Cooldown = 0.8f,
                Afflict = Affliction.Burn, AfflictFor = 5, AfflictPower = 16,
                Food = ItemBook.Charcoal, Meal = 12, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "fire_dragon", LoreRef = "gu_fire_dragon_gu", Name = "Гу Огненного Дракона", Rank = 4, Kind = "атака", Line = "огонь",
                Icon = "flame", Colour = C(1f, 0.3f, 0.08f),
                About = "Из пламени вырастает огромный огненный дракон и обрушивается на врага.",
                Skill = SkillKind.Bolt, SkillName = "Огненный дракон", Cost = 18, Reach = 26, Range = 36, Power = 1100, Radius = 7, Cooldown = 3, Tire = 45,
                Afflict = Affliction.Burn, AfflictFor = 6, AfflictPower = 20,
                Food = ItemBook.Charcoal, Meal = 20, MealEvery = 3 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "immobilization", LoreRef = "gu_immobilization_gu", Name = "Гу Обездвиживания", Rank = 4, Kind = "контроль", Line = "оковы",
                Icon = "eye", Colour = C(0.8f, 0.7f, 1f),
                About = "Тот, в кого он попал, не может шевельнуться.",
                Skill = SkillKind.Bolt, SkillName = "Обездвиживание", Cost = 12, Reach = 24, Range = 30, Power = 100, Cooldown = 2, Tire = 35,
                Afflict = Affliction.Root, AfflictFor = 9,
                Food = ItemBook.IronSand, Meal = 10, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "golden_bell_shield", LoreRef = "gu_golden_bell_shield_gu", Name = "Гу Щита Золотого Колокола", Rank = 4, Kind = "защита", Line = "золотые щиты",
                Icon = "shield", Colour = Gold,
                About = "Золотой щит, доросший до колокола: удары гаснут о гудящую бронзу.",
                Skill = SkillKind.Skin, SkillName = "Золотой колокол", Cost = 7, Upkeep = 0.45f, Amount = 0.78f,
                Food = ItemBook.IronSand, Meal = 14, MealEvery = 3 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "ivory_armor", LoreRef = "gu_ivory_armor_gu", Name = "Гу Брони из Слоновой Кости", Rank = 4, Kind = "защита", Line = "кости",
                Icon = "bone", Colour = Bone,
                About = "Твёрдая скорлупа слоновой кости. Крепче почти всего на четвёртом ранге.",
                Skill = SkillKind.Skin, SkillName = "Костяная броня", Cost = 9, Upkeep = 0.4f, Amount = 0.82f,
                Food = ItemBook.BeastBone, Meal = 12, MealEvery = 3 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "water_curtain_skyflower", LoreRef = "gu_water_curtain_skyflower_gu", Name = "Гу Небесного Цветка Водяной Завесы", Rank = 4, Kind = "защита", Line = "вода",
                Icon = "drop", Colour = Water,
                About = "Огромный водяной шар: держит даже удары пятого ранга, но и сам мастер не может сдвинуться.",
                Skill = SkillKind.Ward, SkillName = "Водяная завеса", Cost = 16, Duration = 6, Cooldown = 4, Tire = 45,
                Food = ItemBook.SpringWater, Meal = 15, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "chasing_wind", LoreRef = "gu_chasing_wind_gu", Name = "Гу Погони за Ветром", Rank = 4, Kind = "движение", Line = "ветер",
                Icon = "wing", Colour = Wind,
                About = "Скорость, с которой можно догнать ветер.",
                Skill = SkillKind.Haste, SkillName = "Погоня за ветром", Cost = 8, Duration = 45, Amount = 2.4f, Cooldown = 2,
                Food = ItemBook.IntimateGrass, Meal = 12, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "wolf_sprint", LoreRef = "gu_wolf_sprint_gu", Name = "Гу Волчьего Рывка", Rank = 4, Kind = "движение", Line = "волки",
                Icon = "wolf", Colour = C(0.9f, 0.86f, 0.76f),
                About = "Рывок волка: вы проноситесь вперёд прежде, чем враг моргнёт. Эссенции берёт немало.",
                Skill = SkillKind.Dash, SkillName = "Волчий рывок", Cost = 14, Amount = 14, Cooldown = 1, Tire = 30,
                Food = ItemBook.RawMeat, Meal = 10, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "berserk", LoreRef = "gu_berserk_gu", Name = "Гу Берсерка", Rank = 4, Kind = "усиление", Line = "ярость",
                Icon = "heart", Colour = Blood,
                About = "Красный свет заливает тело: вся сила вдвое — на короткое время.",
                Skill = SkillKind.Berserk, SkillName = "Ярость", Cost = 15, Duration = 12, Amount = 2, Cooldown = 6, Tire = 50,
                Food = ItemBook.RawMeat, Meal = 15, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "wolf_howl", LoreRef = "gu_wolf_howl_gu", Name = "Гу Волчьего Воя", Rank = 4, Kind = "контроль", Line = "волки",
                Icon = "wolf", Colour = C(0.85f, 0.85f, 0.95f),
                About = "Вой, слышный за восемьсот шагов. Чужие звери в страхе бегут.",
                Skill = SkillKind.Howl, SkillName = "Волчий вой", Cost = 10, Radius = 40, Duration = 10, Cooldown = 5, Tire = 40,
                Food = ItemBook.RawMeat, Meal = 10, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "nine_eyes_liquor", LoreRef = "gu_nine_eyes_liquor_worm", Name = "Винный Червь с Девятью Глазами", Rank = 4, Kind = "очищение", Line = "винные черви",
                Icon = "worm", Colour = C(0.95f, 0.6f, 1f),
                About = "Девятиглазый червь: очищает жёлтое золото четвёртого ранга.",
                Skill = SkillKind.Refine, SkillName = "Очищение эссенции",
                Food = ItemBook.WhiteGrains, Meal = 6, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "yellow_gold_relic", LoreRef = "gu_yellow_gold_relic_gu", Name = "Реликтовый Гу Жёлтого Золота", Rank = 4, Kind = "культивация", Line = "реликвии",
                Icon = "gu", Colour = Gold,
                About = "Реликвия четвёртого ранга.",
                Skill = SkillKind.Relic, SkillName = "Подъём на ступень", Cost = 10, RefineCost = 0,
            });
            Add(new GuKind
            {
                Id = "ten_jun_strength", LoreRef = "gu_strength_of_ten_jun_gu", Name = "Гу Силы в Десять Цзюней", Rank = 4, Kind = "сила", Line = "сила цзинь",
                Icon = "fist", Colour = Gold,
                About = "Десять цзюней силы — навсегда.",
                Skill = SkillKind.Strength, SkillName = "Десять цзюней", Cost = 16, Amount = 1.6f, RefineCost = 0,
            });
            Add(new GuKind
            {
                Id = "essence_iron_bone", LoreRef = "gu_essence_iron_bone_gu", Name = "Гу Кости из Железной Эссенции", Rank = 4, Kind = "тело", Line = "кости",
                Icon = "bone", Colour = C(0.6f, 0.65f, 0.8f),
                About = "Кости твёрже, чем от железной кости, — закалка долгая, но навсегда.",
                Skill = SkillKind.Bones, SkillName = "Кости железной эссенции", Cost = 20, Amount = 1.2f, RefineCost = 0,
            });

            // ── Rank five ─────────────────────────────────────────────

            Add(new GuKind
            {
                Id = "moonlight_treasure_king", LoreRef = "gu_moonlight_treasure_king_gu", Name = "Гу Короля Сокровищ Лунного Света", Rank = 5, Kind = "атака", Line = "лунная линия",
                Icon = "moon", Colour = C(0.75f, 0.9f, 1f),
                About = "Вершина лунной линии клана Гу Юэ: исполинский клин лунного света.",
                Skill = SkillKind.Moonblade, SkillName = "Король лунного света", Cost = 10, Reach = 20, Range = 32, Power = 1800, Cooldown = 0.4f, Amount = 2.4f, Tire = 22,
                Food = ItemBook.Petal, Meal = 40, MealEvery = 12 * Hour, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "blood_guillotine", LoreRef = "gu_blood_guillotine_gu", Name = "Гу Кровавой Гильотины", Rank = 5, Kind = "атака", Line = "кровь",
                Icon = "spear", Colour = Blood,
                About = "Кровавое лезвие — бой и кормление для него одно: оно пьёт кровь тех, кого режет.",
                Skill = SkillKind.Bolt, SkillName = "Кровавая гильотина", Cost = 12, Reach = 24, Range = 34, Power = 1600, Cooldown = 0.8f,
                Afflict = Affliction.Burn, AfflictFor = 8, AfflictPower = 25,
                Food = ItemBook.RawMeat, Meal = 30, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "heaven_blazing_spark", LoreRef = "gu_heaven_blazing_spark_gu", Name = "Гу Пылающей Небесной Искры", Rank = 5, Kind = "атака", Line = "свет",
                Icon = "star", Colour = C(1f, 0.92f, 0.55f),
                About = "Небесная искра падает туда, куда указали, и выжигает всё вокруг.",
                Skill = SkillKind.Burst, SkillName = "Небесная искра", Cost = 16, Range = 30, Radius = 9, Power = 1500, Cooldown = 2, Tire = 40,
                Afflict = Affliction.Burn, AfflictFor = 4, AfflictPower = 20,
                Food = ItemBook.Petal, Meal = 40, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "invisibility", LoreRef = "gu_invisibility_gu", Name = "Гу Невидимости", Rank = 5, Kind = "скрытность", Line = "скрытность",
                Icon = "eye", Colour = Dark,
                About = "Редчайший Гу: вас не видит никто, кроме особых Гу пятого ранга.",
                Skill = SkillKind.Hide, SkillName = "Невидимость", Cost = 4, Upkeep = 0.4f, Amount = 0,
                Food = ItemBook.IntimateGrass, Meal = 30, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "wind_barrier", LoreRef = "gu_wind_barrier_gu", Name = "Гу Ветряного Барьера", Rank = 5, Kind = "защита", Line = "ветер",
                Icon = "wing", Colour = Wind,
                About = "Огромный шелковистый ветер смыкается вокруг: внутрь не пройдёт ничто.",
                Skill = SkillKind.Ward, SkillName = "Ветряной барьер", Cost = 12, Duration = 10, Cooldown = 5, Tire = 40,
                Food = ItemBook.IntimateGrass, Meal = 30, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "golden_lion_fur", LoreRef = "gu_golden_lion_fur_gu", Name = "Гу Шкуры Золотого Льва", Rank = 5, Kind = "защита", Line = "кожи",
                Icon = "shield", Colour = Gold,
                About = "Золотая львиная шерсть покрывает мастера — защита пятого ранга.",
                Skill = SkillKind.Skin, SkillName = "Золотой лев", Cost = 7, Upkeep = 0.4f, Amount = 0.88f,
                Food = ItemBook.RawMeat, Meal = 30, MealEvery = 3 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "five_element_bear_skin", LoreRef = "gu_five_element_bear_skin_gu", Name = "Гу Шкуры Медведя Пяти Стихий", Rank = 5, Kind = "защита", Line = "кожи",
                Icon = "shield", Colour = C(0.7f, 0.55f, 0.45f),
                About = "Медвежья шкура, впитавшая пять стихий. Почти ничто не пробивает её.",
                Skill = SkillKind.Skin, SkillName = "Медведь пяти стихий", Cost = 8, Upkeep = 0.45f, Amount = 0.92f,
                Food = ItemBook.RawMeat, Meal = 30, MealEvery = 3 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "unlimited_spring_light", LoreRef = "gu_unlimited_spring_light_gu", Name = "Гу Безграничного Весеннего Света", Rank = 5, Kind = "лечение", Line = "свет",
                Icon = "heart", Colour = C(0.8f, 1f, 0.7f),
                About = "Весенний свет, что лечит всё до конца.",
                Skill = SkillKind.Heal, SkillName = "Весенний свет", Cost = 14, Amount = 100, Cooldown = 4, Tire = 40,
                Food = ItemBook.Petal, Meal = 40, MealEvery = Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "earth_escape", LoreRef = "gu_earth_escape_gu", Name = "Гу Побега Сквозь Землю", Rank = 5, Kind = "движение", Line = "земля",
                Icon = "drop", Colour = Earth,
                About = "Нырок в землю — и вы выходите из неё далеко, там, куда смотрели.",
                Skill = SkillKind.Blink, SkillName = "Сквозь землю", Cost = 10, Range = 40, Cooldown = 2, Tire = 30,
                Food = ItemBook.BeastBone, Meal = 20, MealEvery = 2 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "do_or_die", LoreRef = "gu_do_or_die_gu", Name = "Гу Решимости до Конца", Rank = 5, Kind = "культивация", Line = "отчаяние",
                Icon = "heart", Colour = Blood,
                About = "Разом наполняет апертуру эссенцией — ценой малой ступени культивации.",
                Skill = SkillKind.Desperate, SkillName = "До конца", Cost = 0, RefineCost = 0,
            });
            Add(new GuKind
            {
                Id = "undefeated_hundred_battles", LoreRef = "gu_undefeated_hundred_battles_gu", Name = "Гу Непобедимого в Сотне Битв", Rank = 5, Kind = "очищение", Line = "удача",
                Icon = "star", Colour = Gold,
                About = "Редчайший Гу: пока он в апертуре, ни одно слияние Гу не срывается.",
                Skill = SkillKind.Surety, SkillName = "Верная рука",
                Food = ItemBook.WhiteGrains, Meal = 10, MealEvery = 3 * Day, RefineCost = 15,
            });
            Add(new GuKind
            {
                Id = "purple_crystal_relic", LoreRef = "gu_purple_crystal_relic_gu", Name = "Реликтовый Гу Фиолетового Кристалла", Rank = 5, Kind = "культивация", Line = "реликвии",
                Icon = "gu", Colour = C(0.65f, 0.5f, 0.9f),
                About = "Реликвия пятого ранга из кладовых горы Сань Ча.",
                Skill = SkillKind.Relic, SkillName = "Подъём на ступень", Cost = 10, RefineCost = 0,
            });
            Add(new GuKind
            {
                Id = "ironfist_grappling", LoreRef = "gu_ironfist_grappling_gu", Name = "Гу Захвата Железным Кулаком", Rank = 5, Kind = "атака", Line = "сила",
                Icon = "fist", Colour = C(0.4f, 0.4f, 0.45f),
                About = "Огромная чёрная железная рука сгребает всех перед мастером и не отпускает.",
                Skill = SkillKind.Cleave, SkillName = "Железный кулак", Cost = 14, Reach = 7, Power = 1500, Cooldown = 1.5f, Tire = 35,
                Afflict = Affliction.Root, AfflictFor = 4,
                Food = ItemBook.IronSand, Meal = 30, MealEvery = 2 * Day, RefineCost = 15,
            });
        }

        /// <summary>
        /// The fusions the game knows — the lore's where it has them (Moonglow from Moonlight
        /// and two Little Lights, White Jade from White Boar and Jade Skin, Moon Raiment from
        /// Jade Skin and Moonlight, Stealth Scales from Stealth Rock), the rest following the
        /// lines. A rank is never more than one above the highest Gu put in.
        /// </summary>
        public static readonly Recipe[] Recipes =
        {
            new Recipe { Result = "moonglow", Gu = new[] { "moonlight", "little_light", "little_light" }, Materials = new[] { (ItemBook.Petal, 20) }, Cost = 25, Chance = 0.75f },
            new Recipe { Result = "moon_raiment", Gu = new[] { "jade_skin", "moonlight" }, Materials = new[] { (ItemBook.Petal, 10), (ItemBook.JadeRock, 4) }, Cost = 25, Chance = 0.7f },
            new Recipe { Result = "white_jade", Gu = new[] { "white_boar", "jade_skin" }, Materials = new[] { (ItemBook.JadeRock, 6) }, Cost = 25, Chance = 0.7f },
            new Recipe { Result = "black_and_white_boar", Gu = new[] { "black_boar", "white_boar" }, Materials = new[] { (ItemBook.RawMeat, 5) }, Cost = 20, Chance = 0.8f },
            new Recipe { Result = "black_mane", Gu = new[] { "black_boar", "beast_skin" }, Materials = new[] { (ItemBook.RawMeat, 5) }, Cost = 25, Chance = 0.7f },
            new Recipe { Result = "bronze_skin_2", Gu = new[] { "bronze_skin", "bronze_skin" }, Materials = new[] { (ItemBook.IronSand, 10) }, Cost = 25, Chance = 0.75f },
            new Recipe { Result = "bronze_skin_3", Gu = new[] { "bronze_skin_2", "bronze_skin_2" }, Materials = new[] { (ItemBook.IronSand, 25) }, Cost = 25, Chance = 0.7f },
            new Recipe { Result = "ghost_fire_2", Gu = new[] { "ghost_fire", "ghost_fire" }, Materials = new[] { (ItemBook.Charcoal, 10) }, Cost = 25, Chance = 0.75f },
            new Recipe { Result = "spiral_bone_spear", Gu = new[] { "bone_spear", "bone_spear" }, Materials = new[] { (ItemBook.BeastBone, 10) }, Cost = 25, Chance = 0.75f },
            new Recipe { Result = "star_arrow", Gu = new[] { "star_dart", "star_dart" }, Materials = new[] { (ItemBook.IronSand, 8) }, Cost = 25, Chance = 0.75f },
            new Recipe { Result = "stealth_scales", Gu = new[] { "stealth_rock", "beast_skin" }, Materials = new[] { (ItemBook.IronSand, 8) }, Cost = 25, Chance = 0.7f },
            new Recipe { Result = "four_flavors_liquor", Gu = new[] { "liquor_worm" }, Materials = new[] { (ItemBook.BambooWine, 4), (ItemBook.WhiteGrains, 2), (ItemBook.Petal, 10), (ItemBook.IntimateGrass, 5) }, Cost = 30, Chance = 0.7f },
            new Recipe { Result = "ten_jin_strength", Gu = new[] { "jin_strength", "jin_strength" }, Materials = new[] { (ItemBook.RawMeat, 6) }, Cost = 25, Chance = 0.8f },
            new Recipe { Result = "golden_moon", Gu = new[] { "moonglow", "little_light" }, Materials = new[] { (ItemBook.Petal, 40), (ItemBook.IronSand, 10) }, Cost = 30, Chance = 0.6f },
            new Recipe { Result = "frost_moon", Gu = new[] { "moonglow", "icicle" }, Materials = new[] { (ItemBook.Petal, 30), (ItemBook.SpringWater, 10) }, Cost = 30, Chance = 0.6f },
            new Recipe { Result = "blood_moon", Gu = new[] { "moonglow" }, Materials = new[] { (ItemBook.Petal, 30), (ItemBook.RawMeat, 15) }, Cost = 30, Chance = 0.6f },
            new Recipe { Result = "heavenly_mugwort", Gu = new[] { "white_jade" }, Materials = new[] { (ItemBook.JadeRock, 20), (ItemBook.IntimateGrass, 10) }, Cost = 30, Chance = 0.6f },
            new Recipe { Result = "steel_mane", Gu = new[] { "black_mane" }, Materials = new[] { (ItemBook.IronSand, 25) }, Cost = 30, Chance = 0.6f },
            new Recipe { Result = "seven_fragrances_liquor", Gu = new[] { "four_flavors_liquor" }, Materials = new[] { (ItemBook.WhiteGrains, 6), (ItemBook.BambooWine, 6) }, Cost = 30, Chance = 0.6f },
            new Recipe { Result = "jun_strength", Gu = new[] { "ten_jin_strength", "ten_jin_strength", "ten_jin_strength" }, Materials = new[] { (ItemBook.RawMeat, 15) }, Cost = 30, Chance = 0.6f },
            new Recipe { Result = "golden_bell_shield", Gu = new[] { "golden_shield" }, Materials = new[] { (ItemBook.IronSand, 60) }, Cost = 30, Chance = 0.5f },
            new Recipe { Result = "nine_eyes_liquor", Gu = new[] { "seven_fragrances_liquor" }, Materials = new[] { (ItemBook.WhiteGrains, 15) }, Cost = 30, Chance = 0.5f },
            new Recipe { Result = "ten_jun_strength", Gu = new[] { "jun_strength", "jun_strength" }, Materials = new[] { (ItemBook.RawMeat, 30) }, Cost = 30, Chance = 0.5f },
        };
    }
}
