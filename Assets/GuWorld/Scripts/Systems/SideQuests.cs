using System;
using System.Collections.Generic;
using GuWorld.Items;

namespace GuWorld.Systems
{
    /// <summary>
    /// The village's side tasks — taken beside the academy's lessons, up to three at once, each from its own villager —
    /// and the clan's missions of the day, given out at the clan pavilion: three a day, each done once, paid in stones
    /// and in merit (вклад) for the clan's treasury. The words are the game's own.
    /// </summary>
    public static class SideQuests
    {
        /// <summary>Who gives out the clan's missions and keeps its treasury.</summary>
        public const string Steward = "Гу Юэ Даньфэн";

        public static readonly QuestKind[] Book =
        {
            new QuestKind
            {
                Id = "side_snakes", Side = true, Title = "Змеи у источника",
                Giver = "Гу Юэ Мэйлин", GiverTitle = "стирает у источника",
                Goal = "Убейте двух тёмных змей в зарослях за стенами",
                Event = "kill:snake", Target = 2, RewardStones = 6, RewardMerit = 2, RewardItem = ItemBook.IntimateGrass, RewardItemCount = 5,
                Offer = "Вчера у ручья за стеной опять видела змею — чёрную, в руку толщиной. Дети туда за водой бегают… " +
                        "Убей двух, если найдёшь. Их яд жжёт, как огонь, — береги ноги.",
                Thanks = "Теперь хоть спать спокойно буду. Возьми травы — Гу её любят.",
            },
            new QuestKind
            {
                Id = "side_boars", Side = true, Title = "Кабаны в бамбуке",
                Giver = "Гу Юэ Шулинь", GiverTitle = "бамбукорез",
                Goal = "Убейте трёх диких кабанов к югу и западу от деревни",
                Event = "kill:boar", Target = 3, RewardStones = 8, RewardMerit = 3, RewardItem = ItemBook.RawMeat, RewardItemCount = 4,
                Offer = "Кабаны изрыли всю нижнюю рощу — молодые побеги как скотом потравлены. Уйми троих. " +
                        "Только не стой у них на пути, когда они разгоняются: клыки у них — что ножи.",
                Thanks = "Три кабана! Мясо твоё, мне хватит и того, что бамбук цел.",
            },
            new QuestKind
            {
                Id = "side_bones", Side = true, Title = "Кости для клея",
                Giver = "Гу Юэ Кан", GiverTitle = "плотник",
                Goal = "Принесите плотнику Кану пять звериных костей",
                Event = "deliver:" + ItemBook.BeastBone + "@Гу Юэ Кан", Target = 5, RewardStones = 7, RewardMerit = 2,
                Offer = "Клей на костях держит крепче гвоздя. Принеси пять звериных костей — из кабана, из волка, неважно. " +
                        "У Баошаня они тоже бывают, но он дерёт втридорога.",
                Thanks = "Вот это кости! Крыша над павильоном теперь простоит ещё сто лет.",
            },
            new QuestKind
            {
                Id = "side_salve", Side = true, Title = "Мазь для бабушки Аньнин",
                Giver = "Гу Юэ Аньнин", GiverTitle = "старушка с соседней улицы",
                Goal = "Принесите бабушке Аньнин целебную мазь (её продаёт Баошань)",
                Event = "deliver:" + ItemBook.Salve + "@Гу Юэ Аньнин", Target = 1, RewardStones = 5, RewardMerit = 2, RewardItem = ItemBook.MeatBuns, RewardItemCount = 3,
                Offer = "Колени к дождю ноют, сил нет. У Баошаня есть мазь из трав… Сама я до площади уже не дойду. Принеси, милый.",
                Thanks = "Ох, вот спасибо. Держи булочки — сама лепила, пока руки слушались.",
            },
            new QuestKind
            {
                Id = "side_letter", Side = true, Title = "Письмо на хутор у Брода",
                Giver = "Гу Юэ Хай", GiverTitle = "посыльный",
                Goal = "Доберитесь до хутора у Брода далеко внизу, к югу от деревни",
                Event = "visit:hamlet_ford", Target = 1, RewardStones = 14, RewardMerit = 4,
                Offer = "Меня послали с письмом на хутор у Брода, а я… ну, боюсь я волков. Ты же мастер Гу! " +
                        "Спустись к броду — там тебя ждут. Дорога долгая, возьми камней и еды.",
                Thanks = "Дошёл?! Хуторяне, наверное, рады были. Держи, это от них — и от меня.",
            },
            new QuestKind
            {
                Id = "side_catch", Side = true, Title = "Как ловят Гу",
                Giver = "Гу Юэ Пэнфэй", GiverTitle = "ученик, ждёт пробуждения",
                Goal = "Поймайте дикого Гу в лесу вокруг деревни (удерживайте E у светящейся точки)",
                Event = "catch:*", Target = 1, RewardStones = 5, RewardMerit = 2, RewardItem = ItemBook.Petal, RewardItemCount = 10,
                Offer = "Говорят, дикие Гу летают прямо в лесу — светятся, как светлячки. Поймай одного! " +
                        "Я хочу знать, как это делается, — после церемонии сам пойду.",
                Thanks = "Настоящий дикий Гу! Ну всё, решено — буду ловцом Гу.",
            },
            new QuestKind
            {
                Id = "side_refine", Side = true, Title = "Свиток о новом Гу",
                Giver = "Гу Юэ Вэньцзе", GiverTitle = "ученик академии",
                Goal = "Очистите в апертуру любой новый Гу",
                Event = "refine:*", Target = 1, RewardStones = 6, RewardMerit = 2,
                Offer = "Я пишу свиток о том, как Гу входят в апертуру. Но сам ещё не пробуждён… " +
                        "Очисти какого-нибудь Гу и расскажи мне, каково это.",
                Thanks = "Тянет, как вода в песок? Запишу слово в слово.",
            },
            new QuestKind
            {
                Id = "side_meditate", Side = true, Title = "Тишина в апертуре",
                Giver = "Гу Юэ Чжилань", GiverTitle = "помощница наставника",
                Goal = "Медитацией (X) поднимитесь на следующую ступень",
                Event = "stage", Target = 1, RewardStones = 12, RewardMerit = 4,
                Offer = "Сила мастера — не в одних Гу. Садись и лей эссенцию в стенки апертуры, пока ступень не сдастся. " +
                        "Это долго. Это и есть путь.",
                Thanks = "Ступень выше — и ты это чувствуешь. Наставник будет доволен.",
            },
            new QuestKind
            {
                Id = "side_fangs", Side = true, Title = "Соль задержалась",
                Giver = "Дед Сун", GiverTitle = "торгует рисом и тканями",
                Goal = "Принесите деду Суну шесть волчьих клыков — на обмен в долине",
                Event = "deliver:" + ItemBook.WolfFang + "@Дед Сун", Target = 6, RewardStones = 9, RewardMerit = 2,
                Offer = "Караван с солью стоит у подножия: просят плату клыками — в долине из них делают обереги. " +
                        "Шесть волчьих клыков, и соль будет у всей деревни.",
                Thanks = "Шесть! Караван поднимется к ночи. Ты сделал для деревни больше, чем думаешь.",
            },
            new QuestKind
            {
                Id = "side_demon", Side = true, Title = "Чужой след", After = "wolves_gate",
                Giver = "Гу Юэ Хэн", GiverTitle = "стражник восточных ворот",
                Goal = "Убейте демонического Гу-мастера, что рыщет по горе",
                Event = "kill:demonic", Target = 1, RewardStones = 25, RewardMerit = 8,
                Offer = "Хуторяне говорят: в лесу бродит чужак — не Бай, не Сюн. Режет путников ради их Гу. " +
                        "Демонический путь. Найди его в глуши, подальше от деревни, — и не дай ему уйти.",
                Thanks = "Один демонический практик меньше. Клан этого не забудет.",
            },
            new QuestKind
            {
                Id = "side_boar_king", Side = true, Title = "Король кабанов", After = "wolf_pack", MinRank = 1,
                Giver = "Гу Юэ Тешань", GiverTitle = "стражник южных ворот",
                Goal = "Убейте короля кабанов — его логово в лесу далеко на юго-запад",
                Event = "kill:boar_king", Target = 1, RewardStones = 40, RewardMerit = 12,
                Offer = "Стадо кабанов водит король — зверь размером с телегу, с Гу в теле. Пока он жив, стадо не уйдёт. " +
                        "Ступай на юго-запад, в глухой лес. И возьми с собой кого-нибудь… хотя кого? Возьми камней.",
                Thanks = "Король кабанов! Об этом будут рассказывать у ворот ещё долго.",
            },
            new QuestKind
            {
                Id = "side_tide", Side = true, Title = "Волчий прилив", After = "wolf_pack",
                Giver = "Старейшина Гу Юэ Хуайюй", GiverTitle = "хранитель святилища",
                Goal = "Отбейте волчий прилив (каждую седьмую ночь у деревни)",
                Event = "tide", Target = 1, RewardStones = 30, RewardMerit = 15,
                Offer = "Каждые семь дней с гор идёт волчий прилив. Стражи держат ворота, но волков слишком много. " +
                        "Будь у деревни в ночь прилива и стой до последней волны.",
                Thanks = "Ты стоял, пока стояли волки. Таких клан помнит по именам.",
            },
        };

        /// <summary>What a clan mission can be: its title, goal, event, how many, the stones and the merit it pays.</summary>
        static readonly (string title, string goal, string evt, int target, int stones, int merit, int minRank)[] Pool =
        {
            ("Волки у стен", "Убейте пятерых волков в лесу вокруг деревни", "kill:wolf", 5, 10, 4, 1),
            ("Лепестки для кормушек", "Соберите двенадцать лепестков лунной орхидеи", "gather:" + ItemBook.Petal, 12, 5, 3, 1),
            ("Дикий Гу для академии", "Поймайте дикого Гу для учеников академии", "catch:*", 1, 8, 4, 1),
            ("Кабаны на террасах", "Убейте трёх диких кабанов", "kill:boar", 3, 9, 4, 1),
            ("Змеиный яд", "Убейте двух тёмных змей", "kill:snake", 2, 8, 4, 1),
            ("Медведь на тропе", "Убейте чёрного медведя у передней горы", "kill:bear", 1, 16, 6, 1),
            ("Каменные обезьяны", "Убейте трёх каменных обезьян на северных скалах", "kill:monkey", 3, 14, 5, 1),
            ("Охота на демона", "Убейте демонического Гу-мастера в глуши", "kill:demonic", 1, 20, 8, 1),
            ("Дозор чужого клана", "Победите двух Гу-мастеров клана Бай или Сюн (клан это запомнит)", "kill:master", 2, 18, 7, 2),
            ("Клыки для оберегов", "Сдайте распорядительнице четыре волчьих клыка", "deliver:" + ItemBook.WolfFang + "@" + Steward, 4, 6, 3, 1),
        };

        /// <summary>The clan's three missions of a day (the same for the whole day; another three the next).</summary>
        public static QuestKind[] Missions(int day)
        {
            var random = new Random(day * 7919 + 17);
            var picked = new List<int>();

            while (picked.Count < 3)
            {
                var i = random.Next(Pool.Length);

                if (!picked.Contains(i))
                {
                    picked.Add(i);
                }
            }

            return picked.ConvertAll(i => Mission(day, i)).ToArray();
        }

        /// <summary>A mission of a day by its pool index.</summary>
        static QuestKind Mission(int day, int index)
        {
            var (title, goal, evt, target, stones, merit, minRank) = Pool[index];

            return new QuestKind
            {
                Id = $"mission:{day}:{index}",
                Side = true,
                Mission = true,
                MinRank = minRank,
                Title = $"Поручение клана: {title}",
                Giver = Steward,
                GiverTitle = "распорядительница при павильоне клана",
                Goal = goal,
                Event = evt,
                Target = target,
                RewardStones = stones,
                RewardMerit = merit,
                Offer = $"Поручение на сегодня: {goal.ToLowerInvariant()}. Клан платит камнями и записывает вклад — " +
                        "на вклад в сокровищнице дают то, чего не купишь ни за какие камни.",
                Thanks = "Записала. Вклад твой растёт — заглядывай в сокровищницу.",
            };
        }

        /// <summary>A side task or a mission by its id (a mission's: "mission:day:index").</summary>
        public static QuestKind Find(string id)
        {
            if (id == null)
            {
                return null;
            }

            if (id.StartsWith("mission:"))
            {
                var parts = id.Split(':');

                return parts.Length == 3 && int.TryParse(parts[1], out var day) && int.TryParse(parts[2], out var index) && index >= 0 && index < Pool.Length
                    ? Mission(day, index)
                    : null;
            }

            return Array.Find(Book, q => q.Id == id);
        }
    }
}
