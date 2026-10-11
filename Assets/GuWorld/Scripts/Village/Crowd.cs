using GuWorld.Body;
using UnityEngine;

namespace GuWorld.Village
{
    /// <summary>
    /// The ordinary people of a village, besides the named ones (Roster): clan
    /// members and their servants, old and young, going about their day — children
    /// running between the lanes, old folk on the benches, drinkers at the taverns in
    /// the evening, people sweeping their doorsteps, strolling, standing to talk, guards
    /// walking the gates. Made up from a seed, the same every time; names put together
    /// from syllables (not the novel's people), lines in the game's own words.
    /// </summary>
    public static class Crowd
    {
        static readonly string[] Syllables =
        {
            "Лун", "Мин", "Шань", "Хуа", "Цин", "Юй", "Фэн", "Лань", "Тао", "Жун", "Сян", "Бо", "Вэй", "Цзе", "Нин", "Хэ", "Сюэ", "Дэ",
            "Ань", "Цзин", "Пин", "Гуан", "Чэн", "Шу", "Тин", "Жуй", "Сун", "Хуэй", "Кай", "Линь", "Мэй", "Чжу", "Янь", "Сы",
        };

        static readonly string[] CommonMen = { "Дядюшка", "Старый", "Малый", "Рябой", "Хромой", "Тихий" };
        static readonly string[] CommonWomen = { "Тётушка", "Бабушка", "Сестрица", "Хозяйка" };

        static string Given(System.Random random)
        {
            var a = Syllables[random.Next(Syllables.Length)];
            var b = Syllables[random.Next(Syllables.Length)];

            return a == b ? a : a + b.ToLowerInvariant();
        }

        static string ClanName(string settlement) => settlement switch
        {
            "bai" => "Бай",
            "xiong" => "Сюн",
            _ => "Гу Юэ",
        };

        /// <summary>`count` people for a settlement ("gu_yue", "bai", "xiong"), from `seed`.</summary>
        public static VillagerSpec[] Of(string settlement, int count, int seed)
        {
            var random = new System.Random(seed);
            float R() => (float)random.NextDouble();
            var people = new VillagerSpec[count];
            var clan = ClanName(settlement);
            var styles = (HairStyle[])System.Enum.GetValues(typeof(HairStyle));

            for (var i = 0; i < count; i++)
            {
                var gender = R() < 0.5f ? Gender.Male : Gender.Female;
                var age = R();
                var child = age < 0.18f;
                var old = age > 0.82f;
                var member = R() < 0.7f;
                var role = child ? Role.Child : old ? (R() < 0.6f ? Role.Idler : Role.Sweeper) :
                    R() < 0.18f ? Role.Drinker : R() < 0.2f ? Role.Sweeper : R() < 0.12f ? Role.Patrol : Role.Wanderer;
                var given = Given(random);
                string name;

                if (member || child)
                {
                    name = $"{clan} {given}";
                }
                else
                {
                    var titles = gender == Gender.Male ? CommonMen : CommonWomen;
                    name = $"{titles[random.Next(titles.Length)]} {given}";
                }

                var hair = styles[random.Next(styles.Length)];

                if (gender == Gender.Female && hair == HairStyle.Bald)
                {
                    hair = HairStyle.Buns;
                }

                var beard = gender == Gender.Male && !child && R() < (old ? 0.7f : 0.3f) ? (Beard)(1 + random.Next(4)) : Beard.None;
                var hairColour = old ? (R() < 0.5f ? new Color(0.62f, 0.61f, 0.6f) : new Color(0.36f, 0.35f, 0.36f)) : Look.HairBlack;
                var height = child ? 1.25f + R() * 0.25f : gender == Gender.Male ? 1.66f + R() * 0.16f : 1.55f + R() * 0.12f;

                people[i] = new VillagerSpec
                {
                    Name = name,
                    Title = Title(role, gender, member, settlement),
                    Role = role,
                    Look = Look.Default(gender).With(hair: hair, beard: beard),
                    Build = new HumanBody.Build { youth = child ? 1 : old ? 0 : R() * 0.6f, hair = hairColour, dye = random.Next(8), emblem = member && settlement == "gu_yue" ? Emblem.Crescent : Emblem.None },
                    Height = height,
                    Stoop = old ? 0.2f : 0,
                    Speed = child ? 2.1f : old ? 0.9f : 1.2f + R() * 0.3f,
                    Post = role == Role.Patrol ? (R() < 0.5f ? "gate south" : "gate north") : $"ring {random.Next(8)}",
                    Home = random.Next(4000),
                    About = About(role, settlement, member),
                    Lines = Lines(role, settlement),
                    Generic = true,
                    Settlement = settlement,
                };
            }

            return people;
        }

        static string Title(Role role, Gender gender, bool member, string settlement)
        {
            var she = gender == Gender.Female;

            return role switch
            {
                Role.Child => she ? "девочка" : "мальчишка",
                Role.Idler => she ? "старушка" : "старик",
                Role.Sweeper => she ? "хозяйка" : "хозяин дома",
                Role.Drinker => she ? "завсегдатай таверны" : "любитель выпить",
                Role.Patrol => "стражник",
                _ => member ? (she ? "жительница" : "житель") : (she ? "служанка клана" : "слуга клана"),
            };
        }

        static string About(Role role, string settlement, bool member)
        {
            if (settlement != "gu_yue")
            {
                return settlement == "bai"
                    ? "Здесь деревня Бай. Мы чужих не жалуем, а гуюэвцев — особенно."
                    : "Это деревня Сюн. Медведь — наш знак. Чужак, не задерживайся.";
            }

            return role switch
            {
                Role.Child => "Я тут живу! А ты кто такой? А правда, что у тебя есть Гу?",
                Role.Idler => "Сижу, гляжу, как деревня живёт. Больше мне, старому, и не надо.",
                Role.Drinker => "После работы — чарку бамбукового. Таков порядок.",
                Role.Patrol => "Хожу от ворот к воротам. Скучно — значит, всё в порядке.",
                _ => member ? "Живу здесь, как и мой отец, и его отец. Клан нас кормит, мы служим клану." : "Я служу клану: таскаю воду, рублю дрова, мету дворы. Простая жизнь.",
            };
        }

        static string[] Lines(Role role, string settlement)
        {
            if (settlement == "bai")
            {
                return new[]
                {
                    "Водопад шумит день и ночь — привыкаешь, потом без него не уснуть.",
                    "Наш молодой талант, говорят, страшнее всех на горе. Так что веди себя тихо.",
                    "Белое зерновое вино — лучшее из трёх вин Цинмао. Спорить не советую.",
                };
            }

            if (settlement == "xiong")
            {
                return new[]
                {
                    "Медвежья желчь в вине — для силы. Твоему Гу Юэ такое и не снилось.",
                    "Передняя гора первой встречает зверей. Мы привычные.",
                    "Говорят, у вас в Гу Юэ стены каменные. А у нас брёвна — и стоят.",
                };
            }

            return role switch
            {
                Role.Child => new[] { "Мы с ребятами играли у стены, а стражник нас прогнал!", "Когда вырасту — стану мастером Гу и полечу!" },
                Role.Idler => new[] { "В мои годы весна была теплее. Или я был моложе.", "Видишь вон ту крышу? Её мой дед крыл, ещё до большой стаи." },
                Role.Drinker => new[] { "В таверне «Бамбуковое вино» нынче наливают щедрее, чем в «Мутном».", "Вчера кто-то в игорном доме проиграл половину своих камней. Смеху было!" },
                Role.Sweeper => new[] { "Листья, листья — каждое утро листья. Гора, что поделаешь.", "Подметёшь двор — и день начинается правильно." },
                Role.Patrol => new[] { "За стенами по ночам волки подходят близко. Слышал вой?", "У южных ворот тропа на переднюю гору. Сюн там — не лучшие соседи." },
                _ => new[] { "Сегодня на площади опять толкотня у лавок.", "Слыхал, караван скоро придёт. Значит, соль подешевеет." },
            };
        }
    }
}
