using GuWorld.Body;
using UnityEngine;

namespace GuWorld.Village
{
    public enum Role
    {
        Guard,
        NightGuard,
        Stall,
        Teacher,
        Student,
        Elder,
        Washer,
        Cutter,
        Carpenter,
        Wanderer,
        Attendant,
    }

    /// <summary>What someone is doing at an hour of the day.</summary>
    public enum Doing
    {
        /// <summary>At home, out of sight.</summary>
        Sleep,
        /// <summary>On their own doorstep.</summary>
        Porch,
        /// <summary>At a place, facing its way.</summary>
        Stand,
        /// <summary>Strolling about a place, stopping now and then.</summary>
        Wander,
    }

    public struct Activity
    {
        public Doing doing;
        /// <summary>The place (VillageLayout's name); none for Sleep and Porch.</summary>
        public string place;
        /// <summary>How far from the place one wanders, m.</summary>
        public float radius;

        public string Key => $"{doing}:{place}";

        public static Activity At(Doing doing, string place = null, float radius = 0) => new Activity { doing = doing, place = place, radius = radius };
    }

    /// <summary>One villager: who, how they look, where they live and work.</summary>
    public class VillagerSpec
    {
        public string Name;
        public string Title;
        public Role Role;
        public Look Look;
        public HumanBody.Build Build;
        public float Height;
        public float Stoop;
        /// <summary>Their post or workplace (a place's name).</summary>
        public string Post;
        /// <summary>Which house is theirs (an index into the village's doorsteps).</summary>
        public int Home;
        public float Speed = 1.35f;
        public string About;
        public string[] Lines;

        /// <summary>What they do at an hour (0…24).</summary>
        public Activity Plan(float hour)
        {
            switch (Role)
            {
                case Role.Guard:
                    return hour < 6 || hour >= 21 ? Activity.At(Doing.Sleep) : hour < 18 ? Activity.At(Doing.Stand, Post) : Activity.At(Doing.Wander, "square", 22);
                case Role.NightGuard:
                    return hour < 6 || hour >= 18 ? Activity.At(Doing.Stand, Post) : hour < 14 ? Activity.At(Doing.Sleep) : Activity.At(Doing.Porch);
                case Role.Stall:
                    return hour < 6 || hour >= 22 ? Activity.At(Doing.Sleep) : hour < 7 ? Activity.At(Doing.Porch) : hour < 20 ? Activity.At(Doing.Stand, Post) : Activity.At(Doing.Wander, "square", 18);
                case Role.Teacher:
                    return hour < 6 || hour >= 21 ? Activity.At(Doing.Sleep) : hour < 7 ? Activity.At(Doing.Porch) :
                        hour < 12 || (hour >= 13 && hour < 17) ? Activity.At(Doing.Stand, Post) :
                        hour < 13 ? Activity.At(Doing.Wander, "square", 20) : Activity.At(Doing.Wander, "square", 40);
                case Role.Student:
                    return hour < 6 || hour >= 21 ? Activity.At(Doing.Sleep) : hour < 7 ? Activity.At(Doing.Porch) :
                        hour < 12 || (hour >= 13 && hour < 17) ? Activity.At(Doing.Wander, "academy yard", 8) :
                        hour < 13 ? Activity.At(Doing.Wander, "square", 20) : Activity.At(Doing.Wander, "square", 45);
                case Role.Elder:
                    return hour < 5 || hour >= 20 ? Activity.At(Doing.Sleep) : hour < 11 || (hour >= 14 && hour < 18) ? Activity.At(Doing.Stand, Post) :
                        hour < 14 ? Activity.At(Doing.Porch) : Activity.At(Doing.Wander, "square", 15);
                case Role.Washer:
                    return hour < 6 || hour >= 21 ? Activity.At(Doing.Sleep) : hour < 7 || hour >= 17 ? Activity.At(Doing.Porch) :
                        hour < 11 || hour >= 14 ? Activity.At(Doing.Stand, Post) : Activity.At(Doing.Wander, "square", 20);
                case Role.Cutter:
                    return hour < 6 || hour >= 21 ? Activity.At(Doing.Sleep) : hour < 7 ? Activity.At(Doing.Porch) :
                        hour < 12 || (hour >= 13 && hour < 18) ? Activity.At(Doing.Wander, Post, 7) : Activity.At(Doing.Wander, "square", 25);
                case Role.Carpenter:
                    return hour < 6 || hour >= 21 ? Activity.At(Doing.Sleep) : hour < 7 ? Activity.At(Doing.Porch) :
                        hour < 18 ? Activity.At(Doing.Wander, Post, 22) : Activity.At(Doing.Wander, "square", 20);
                case Role.Attendant:
                    return hour < 6 || hour >= 21 ? Activity.At(Doing.Sleep) : hour < 19 ? Activity.At(Doing.Stand, Post) : Activity.At(Doing.Wander, "square", 20);
                default:
                    return hour < 7 || hour >= 21 ? Activity.At(Doing.Sleep) : hour < 8 ? Activity.At(Doing.Porch) : Activity.At(Doing.Wander, Post, 55);
            }
        }
    }

    /// <summary>
    /// The people of Gu Yue village — GU World's own, none of them from the
    /// novel (each name checked against the lore's index). Clan members carry
    /// the clan's name; the two stall-keepers are common folk.
    /// </summary>
    public static class Roster
    {
        static readonly Color Black = Look.HairBlack;
        static readonly Color Greying = new Color(0.36f, 0.35f, 0.36f);
        static readonly Color Grey = new Color(0.62f, 0.61f, 0.6f);

        static VillagerSpec P(string name, string title, Role role, Gender gender, HairStyle hair, Beard beard, float youth, Color hairColour, int dye, float height, string post, int home, string about, params string[] lines)
        {
            return new VillagerSpec
            {
                Name = name,
                Title = title,
                Role = role,
                Look = Look.Default(gender).With(hair: hair, beard: beard),
                Build = new HumanBody.Build { youth = youth, hair = hairColour, dye = dye },
                Height = height,
                Post = post,
                Home = home,
                About = about,
                Lines = lines,
            };
        }

        public static VillagerSpec[] People()
        {
            var people = new[]
            {
                P("Гу Юэ Тешань", "стражник южных ворот", Role.Guard, Gender.Male, HairStyle.Short, Beard.Full, 0, Black, 4, 1.8f, "gate south", 3,
                    "Я стерегу южные ворота. Отсюда тропа уходит вниз, к деревне Сюн на передней горе.",
                    "С закатом ворота закрывают. Задержишься внизу — ночуй у Сюн, а не в лесу.",
                    "С башен за одну ночь, бывает, не одну стаю зверей примечают."),
                P("Гу Юэ Хэн", "стражник восточных ворот", Role.Guard, Gender.Male, HairStyle.Parted, Beard.Stubble, 0.2f, Black, 4, 1.76f, "gate east", 12,
                    "Восточные ворота — мои. За ними тропа к бамбуковой роще.",
                    "Бамбукорезы уходят на рассвете и к ужину возвращаются. Если кого нет — идём искать."),
                P("Гу Юэ Лэй", "ночной стражник", Role.NightGuard, Gender.Male, HairStyle.Short, Beard.Mustache, 0, Black, 4, 1.74f, "gate north", 20,
                    "Ночью я у северных ворот. Днём сплю — так что не шуми у моего дома.",
                    "Ночью гора другая: по склонам бродят звери, а над деревней только фонари да звёзды."),
                P("Тётушка Хуа", "держит лапшичную у площади", Role.Stall, Gender.Female, HairStyle.Buns, Beard.None, 0, Greying, 7, 1.6f, "stall 0", 6,
                    "Я тётушка Хуа. С утра у меня лапша, к вечеру — бамбуковое вино.",
                    "Мастера Гу платят первобытными камнями, а мы, простые люди, — медью и рисом.",
                    "Лучшее вино — то, что настоялось в свежем бамбуке. Попробуешь, когда подрастёшь."),
                P("Дед Сун", "торгует рисом и тканями", Role.Stall, Gender.Male, HairStyle.Bald, Beard.Full, 0, Grey, 3, 1.66f, "stall 1", 9,
                    "Пятьдесят лет торгую на этой площади. Рис с террас, ткани из долины.",
                    "Когда караван задерживается, цены на соль ползут вверх. Так было всегда."),
                P("Гу Юэ Чанмин", "наставник академии", Role.Teacher, Gender.Male, HairStyle.Long, Beard.Goatee, 0, Greying, 5, 1.76f, "academy", 1,
                    "Я учу в академии тех, кто пробудил апертуру. Скоро, возможно, и тебя.",
                    "Талант оценивают по классам, от A до D. Но класс — не приговор, а только начало пути.",
                    "Гу — живые. Их кормят, берегут и лишь потом просят о силе."),
                P("Гу Юэ Чжилань", "помощница наставника", Role.Teacher, Gender.Female, HairStyle.Bob, Beard.None, 0, Black, 5, 1.64f, "academy yard", 2,
                    "Я помогаю наставнику Чанмину с младшими учениками.",
                    "Первобытная сущность в апертуре восстанавливается сама, но медленно. Камни ускоряют дело."),
                P("Гу Юэ Пэнфэй", "ученик, ждёт пробуждения", Role.Student, Gender.Male, HairStyle.Short, Beard.None, 1, Black, 2, 1.72f, "academy yard", 14,
                    "Я Пэнфэй! Через месяц церемония — я весь извёлся.",
                    "Хочу класс A. Ну или хотя бы B. На C я не согласен!"),
                P("Гу Юэ Сяоюй", "ученица, ждёт пробуждения", Role.Student, Gender.Female, HairStyle.Ponytail, Beard.None, 1, Black, 6, 1.64f, "academy yard", 15,
                    "Сяоюй. Мы с тобой, кажется, одного года — значит, на церемонии будем рядом.",
                    "Говорят, на церемонию придут старейшины клана. Я уже боюсь споткнуться на ступенях."),
                P("Гу Юэ Вэньцзе", "ученик академии", Role.Student, Gender.Male, HairStyle.Parted, Beard.None, 1, Black, 1, 1.75f, "academy yard", 16,
                    "Вэньцзе. Я выучил все свитки о Гу, что есть в академии. Почти все.",
                    "Знаешь, что Гу бывают разных рангов, от первого и выше? Мастера растут вместе с ними."),
                P("Гу Юэ Юньси", "ученица академии", Role.Student, Gender.Female, HairStyle.Long, Beard.None, 1, Black, 2, 1.66f, "academy yard", 17,
                    "Юньси. Мама говорит, мастеру Гу нельзя бояться ни крови, ни насекомых. А я боюсь даже пауков.",
                    "Если у меня пробудится апертура, первым делом попрошусь в ученики к наставнице Чжилань."),
                P("Старейшина Гу Юэ Хуайюй", "хранитель святилища", Role.Elder, Gender.Male, HairStyle.Bald, Beard.Full, 0, Grey, 5, 1.68f, "shrine", 0,
                    "Я смотрю за святилищем предков. Кланом правят живые, но помнят о мёртвых.",
                    "Наш род пришёл сюда издалека и осел у источника. Не забывай, кому обязан этой землёй.",
                    "Поклонись предкам, проходя мимо святилища. Им немного нужно."),
                P("Гу Юэ Мэйлин", "стирает у источника", Role.Washer, Gender.Female, HairStyle.Buns, Beard.None, 0.2f, Black, 3, 1.62f, "spring", 7,
                    "Я Мэйлин. Почти каждый день у источника — бельё, посуда, вода для лапшичной.",
                    "Вода источника холодная даже летом — и чистая, как нигде на горе.",
                    "Источник — сердце деревни. Без него клана бы здесь не было."),
                P("Гу Юэ Шулинь", "бамбукорез", Role.Cutter, Gender.Male, HairStyle.Short, Beard.Stubble, 0, Black, 1, 1.78f, "grove", 10,
                    "Шулинь. Рублю бамбук в восточной роще — для домов, посуды и вина.",
                    "Бамбук в роще растёт быстрее, чем мы успеваем рубить.",
                    "В чаще держись тропы. Звери туда заходят, особенно в сумерках."),
                P("Гу Юэ Кан", "плотник", Role.Carpenter, Gender.Male, HairStyle.Parted, Beard.Mustache, 0, Black, 7, 1.74f, "ring 2", 11,
                    "Кан, плотник. Половину домов в этом квартале ставил я.",
                    "Новый дом из бамбука — за неделю. Крыша — ещё три дня.",
                    "Сваи держат дом над сыростью весенних дождей. Без них полы сгнили бы за год."),
                P("Гу Юэ Цяо", "соседка", Role.Wanderer, Gender.Female, HairStyle.Bob, Beard.None, 0, Black, 6, 1.62f, "square", 8,
                    "Цяо. Живу неподалёку от площади — и всё про всех знаю.",
                    "Слышала, на задней горе снова видели огни деревни Бай. Они всегда держатся особняком."),
                P("Гу Юэ Аньнин", "старушка с соседней улицы", Role.Wanderer, Gender.Female, HairStyle.Buns, Beard.None, 0, Grey, 1, 1.56f, "ring 5", 4,
                    "Аньнин. В мои годы на площади шумели куда больше.",
                    "Ешь досыта, пока молодой. Мастеру Гу силы понадобятся."),
                P("Гу Юэ Хай", "посыльный", Role.Wanderer, Gender.Male, HairStyle.Short, Beard.None, 1, Black, 3, 1.7f, "ring 0", 18,
                    "Хай. Меня вечно гоняют с поручениями — то в академию, то к воротам.",
                    "Если увидишь Пэнфэя — скажи, что наставник его искал. Опять."),
                P("Гу Юэ Жолань", "девушка из соседнего дома", Role.Wanderer, Gender.Female, HairStyle.Ponytail, Beard.None, 1, Black, 7, 1.65f, "ring 6", 19,
                    "Жолань. Ты тоже ждёшь церемонии? Я так волнуюсь, что не сплю.",
                    "Мы с Сяоюй поспорили, у кого класс выше выйдет. Проигравшая моет посуду у тётушки Хуа."),
                P("Гу Юэ Даньфэн", "служит при павильоне клана", Role.Attendant, Gender.Female, HairStyle.Long, Beard.None, 0, Black, 2, 1.66f, "pavilion", 5,
                    "Даньфэн. Я при павильоне клана: встречаю, провожаю, передаю.",
                    "В павильон без дела не входят. Там решают дела всего клана."),
            };

            foreach (var person in people)
            {
                if (person.Build.hair == Grey)
                {
                    person.Stoop = 0.2f;
                    person.Speed = 0.95f;
                }
            }

            return people;
        }
    }
}
