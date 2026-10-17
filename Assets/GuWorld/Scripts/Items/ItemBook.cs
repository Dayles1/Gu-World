using System.Collections.Generic;

namespace GuWorld.Items
{
    /// <summary>The bag's filters (the design's inventory tabs).</summary>
    public enum ItemCategory
    {
        Gu,
        Food,
        Herb,
        Material,
    }

    /// <summary>What the bag's main action does with an item.</summary>
    public enum ItemUse
    {
        None,
        /// <summary>A Gu: refine it into the aperture (its ring).</summary>
        Refine,
        /// <summary>A primeval stone: draw its essence into the sea.</summary>
        Absorb,
        /// <summary>Food for people: eaten, it heals.</summary>
        Eat,
        /// <summary>Food for Gu: feeds the Gu that eat it.</summary>
        Feed,
    }

    /// <summary>A kind of thing one can carry.</summary>
    public class ItemKind
    {
        /// <summary>The game's id (saved), and the lore's record it is drawn from (none for the game's own).</summary>
        public string Id;
        public string LoreRef;
        public string Name;
        /// <summary>What it is, in a few words ("Трава · корм для Гу").</summary>
        public string Kind;
        /// <summary>A few lines about it, in the game's own words.</summary>
        public string About;
        public ItemCategory Category;
        /// <summary>A Gu's rank (0 for everything else).</summary>
        public int Rank;
        /// <summary>How many fit in one cell of the bag.</summary>
        public int Stack = 1;
        /// <summary>The weight of one, in the bag's units.</summary>
        public float Weight;
        /// <summary>
        /// What it costs in primeval stones, for `Bundle` of them (moon orchid petals go
        /// ten to a stone). 0: not for sale.
        /// </summary>
        public int Price;
        public int Bundle = 1;
        /// <summary>Game minutes it keeps before it spoils; 0: it keeps.</summary>
        public double Keeps;
        public ItemUse Use;
        /// <summary>Health it gives back when eaten.</summary>
        public float Heal;
        /// <summary>For a Gu: the kind of Gu it is (GuBook).</summary>
        public string Gu;
        /// <summary>Its icon (UI/Icons), shown in its own colour.</summary>
        public string Icon;
        /// <summary>The bag's button for its use, where it is not the use's own word ("Выпить", "Нанести").</summary>
        public string Verb;
        /// <summary>Food any Gu eats besides its own: in a meal it stands in for one portion of the Gu's own food.</summary>
        public bool AnyGu;
    }

    /// <summary>
    /// The things the game knows. Each is drawn from the lore where the lore has
    /// it (what a Gu eats, how fast petals wilt, what petals cost); everything
    /// the lore does not say — weights, the other prices — is the game's own, to
    /// be tuned. The words about them are the game's own.
    /// </summary>
    public static class ItemBook
    {
        public const string Stone = "primeval_stone";
        public const string Petal = "moon_orchid_petal";
        public const string JadeRock = "jade_rock";
        public const string MoonlightGu = "gu_moonlight";
        public const string JadeSkinGu = "gu_jade_skin";
        public const string Noodles = "noodles";
        public const string BambooWine = "green_bamboo_wine";
        public const string WolfFang = "wolf_fang";
        public const string IntimateGrass = "intimate_grass";
        public const string Salve = "herb_salve";
        public const string MeatBuns = "meat_buns";
        public const string WhiteGrains = "white_grains_liquid";

        /// <summary>A game day in minutes.</summary>
        const double Day = 24 * 60;

        static readonly Dictionary<string, ItemKind> all = new Dictionary<string, ItemKind>();

        static ItemBook()
        {
            Add(new ItemKind
            {
                Id = MoonlightGu,
                LoreRef = "gu_moonlight_gu",
                Name = "Гу Лунного Света",
                Kind = "Гу · атака",
                About = "Голубой полумесяц из прозрачного кристалла, лёгкий, как лист бумаги. Знак клана Гу Юэ: " +
                        "из ладони мастера он срывается лунным клинком. Ест лепестки лунной орхидеи — утром и вечером.",
                Category = ItemCategory.Gu,
                Rank = 1,
                Weight = 0.1f,
                Price = 30,
                Use = ItemUse.Refine,
                Gu = "moonlight",
                Icon = "moon",
            });
            Add(new ItemKind
            {
                Id = JadeSkinGu,
                LoreRef = "gu_jade_skin_gu",
                Name = "Гу Нефритовой Кожи",
                Kind = "Гу · защита",
                About = "Плоский зелёный жучок с нефритовым отсветом — один из лучших защитных Гу первого ранга. " +
                        "Пока в него льётся эссенция, тело покрыто твёрдой нефритовой кожей. Ест нефритовый камень — раз в десять дней.",
                Category = ItemCategory.Gu,
                Rank = 1,
                Weight = 0.1f,
                Price = 90,
                Use = ItemUse.Refine,
                Gu = "jade_skin",
                Icon = "jade",
            });
            Add(new ItemKind
            {
                Id = Petal,
                LoreRef = "res_moon_orchid",
                Name = "Лепесток лунной орхидеи",
                Kind = "Трава · корм для Гу",
                About = "Голубой с розовым лепесток, изогнутый, как молодой месяц. Пища Гу Лунного Света. " +
                        "Сорванный, вянет за пять дней.",
                Category = ItemCategory.Herb,
                Stack = 100,
                Weight = 0.01f,
                // Ten petals for a primeval stone (lore: res_moon_orchid).
                Price = 1,
                Bundle = 10,
                Keeps = 5 * Day,
                Use = ItemUse.Feed,
                Icon = "orchid",
            });
            Add(new ItemKind
            {
                Id = JadeRock,
                LoreRef = "res_jade_rock",
                Name = "Нефритовый камень",
                Kind = "Материал · корм для Гу",
                About = "Кусок зелёного камня. Им кормят Гу Нефритовой Кожи — и он недёшев.",
                Category = ItemCategory.Material,
                Stack = 50,
                Weight = 0.2f,
                Price = 4,
                Use = ItemUse.Feed,
                Icon = "rock",
            });
            Add(new ItemKind
            {
                Id = Stone,
                LoreRef = "sys_primeval_stones",
                Name = "Первобытный камень",
                Kind = "Валюта · ресурс",
                About = "Серо-белый овальный камень — сгусток природной эссенции. Им платят; мастер Гу может " +
                        "вытянуть из него эссенцию в апертуру — от камня остаётся пыль.",
                Category = ItemCategory.Material,
                Stack = 999,
                Weight = 0.02f,
                Use = ItemUse.Absorb,
                Icon = "stone",
            });
            Add(new ItemKind
            {
                Id = Noodles,
                Name = "Миска лапши",
                Kind = "Еда",
                About = "Горячая лапша тётушки Хуа. Сытно: силы возвращаются быстрее.",
                Category = ItemCategory.Food,
                Stack = 5,
                Weight = 0.5f,
                Price = 1,
                Keeps = Day,
                Use = ItemUse.Eat,
                Heal = 35,
                Icon = "bowl",
            });
            Add(new ItemKind
            {
                Id = BambooWine,
                Name = "Зелёное бамбуковое вино",
                Kind = "Еда · напиток",
                About = "Вино, настоянное в свежем бамбуке, — лучшее в деревне. Согревает.",
                Category = ItemCategory.Food,
                Stack = 10,
                Weight = 0.6f,
                Price = 2,
                Use = ItemUse.Eat,
                Heal = 15,
                Icon = "jar",
                Verb = "Выпить",
            });
            Add(new ItemKind
            {
                Id = WolfFang,
                Name = "Волчий клык",
                Kind = "Добыча",
                About = "Клык серого волка с гор Цинмао. Стража платит за них: так в деревне считают убитых волков.",
                Category = ItemCategory.Material,
                Stack = 30,
                Weight = 0.05f,
                Price = 2,
                Use = ItemUse.None,
                Icon = "wolf",
            });
            Add(new ItemKind
            {
                Id = IntimateGrass,
                LoreRef = "res_intimate_grass",
                Name = "Трава близости",
                Kind = "Трава · подкормка для Гу",
                About = "Бледно-зелёная трава, дешёвая подкормка: её съест любой Гу. В трапезе стебель заменяет одну " +
                        "порцию его собственной еды — но не всю трапезу. Выручает, когда своего корма не хватает.",
                Category = ItemCategory.Herb,
                Stack = 50,
                Weight = 0.02f,
                // Cheap (lore: res_intimate_grass); five stalks for a stone is the game's own.
                Price = 1,
                Bundle = 5,
                Keeps = 4 * Day,
                Use = ItemUse.Feed,
                AnyGu = true,
                Icon = "grass",
            });
            Add(new ItemKind
            {
                Id = Salve,
                Name = "Целебная мазь",
                Kind = "Лекарство",
                About = "Горшочек густой мази на горных травах. Втёртая в раны, снимает боль и затягивает порезы — " +
                        "в деревне её держат охотники и стража.",
                Category = ItemCategory.Herb,
                Stack = 10,
                Weight = 0.15f,
                Price = 6,
                Use = ItemUse.Eat,
                Heal = 50,
                Icon = "salve",
                Verb = "Нанести",
            });
            Add(new ItemKind
            {
                Id = MeatBuns,
                Name = "Паровые булочки с мясом",
                Kind = "Еда",
                About = "Пышные булочки из бамбуковой пароварки, с рубленой свининой и луком. Две — на камень; " +
                        "в дорогу берут по нескольку.",
                Category = ItemCategory.Food,
                Stack = 10,
                Weight = 0.25f,
                Price = 1,
                Bundle = 2,
                Keeps = 2 * Day,
                Use = ItemUse.Eat,
                Heal = 25,
                Icon = "bun",
            });
            Add(new ItemKind
            {
                Id = WhiteGrains,
                LoreRef = "res_white_grains_liquid",
                Name = "Вино «Белое зерно»",
                Kind = "Еда · напиток",
                About = "Острое вино деревни Бай, одно из трёх знаменитых вин горы Цинмао. В Гу Юэ его привозят нечасто — " +
                        "и берут дороже своего бамбукового.",
                Category = ItemCategory.Food,
                Stack = 10,
                Weight = 0.6f,
                Price = 4,
                Use = ItemUse.Eat,
                Heal = 20,
                Icon = "gourd",
                Verb = "Выпить",
            });
        }

        static void Add(ItemKind kind) => all[kind.Id] = kind;

        public static ItemKind Find(string id) => id != null && all.TryGetValue(id, out var kind) ? kind : null;

        public static IEnumerable<ItemKind> All => all.Values;

        public static string Name(ItemCategory category) => category switch
        {
            ItemCategory.Gu => "Гу",
            ItemCategory.Food => "Еда",
            ItemCategory.Herb => "Травы",
            _ => "Материалы",
        };

        /// <summary>"3 лепестка": a count with the right Russian ending for the few words the game counts.</summary>
        public static string Count(int n, string one, string few, string many)
        {
            var tens = n % 100;
            var units = n % 10;

            return $"{n} " + (tens >= 11 && tens <= 14 ? many : units == 1 ? one : units >= 2 && units <= 4 ? few : many);
        }

        public static string Stones(int n) => Count(n, "первобытный камень", "первобытных камня", "первобытных камней");

        /// <summary>Stones counted short, for prices ("4 камня").</summary>
        public static string Price(int n) => Count(n, "камень", "камня", "камней");
    }
}
