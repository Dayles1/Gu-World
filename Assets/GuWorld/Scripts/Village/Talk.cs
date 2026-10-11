using GuWorld.Body;
using GuWorld.World;

namespace GuWorld.Village
{
    /// <summary>
    /// What the villagers say — GU World's own words. The world's facts behind
    /// them come from the lore (the clan settled at the spirit spring, three
    /// villages share the mountain, the academy, the awakening ceremony,
    /// aptitude graded A to D, primeval stones as the Gu Masters' coin); the
    /// lines and the people are the game's.
    ///
    /// A greeting that knows the hour and the weather, who they are, the news
    /// (a different piece each day), and what they can tell about the village.
    /// </summary>
    public static class Talk
    {
        public static string Greeting(Villager who, int hour, Sky sky, Gender hero)
        {
            var you = hero == Gender.Female ? "девушка" : "юноша";
            var time = hour < 5 ? "Не спится в такой час?" :
                hour < 11 ? "Доброе утро." :
                hour < 17 ? "Добрый день." :
                hour < 21 ? "Добрый вечер." : "Поздно уже бродить по деревне.";
            var weather = sky switch
            {
                Sky.Rain => " Весенний дождь — хорошо для бамбука, плохо для спины.",
                Sky.Cloudy => " Небо затянуло, к вечеру может и пролиться.",
                _ => hour >= 5 && hour < 19 ? " Славный ясный день." : "",
            };

            return who.Spec.Role switch
            {
                Role.Guard or Role.NightGuard => $"{time} Проходи, {you}, не стой в воротах.{weather}",
                Role.Stall => $"{time} Подходи ближе, {you}, посмотри, что у нас сегодня.{weather}",
                Role.Teacher => $"{time} А, ты из тех, кто ждёт пробуждения.{weather}",
                Role.Student => $"Привет! {weather.Trim()}",
                Role.Elder => $"{time} Не торопись, {you}. Старикам нравится, когда с ними говорят.",
                Role.Child => "Ой! Ты кто? Ты тоже из академии?",
                Role.Idler => $"{time} Присядь, {you}, отдохни со стариком.",
                Role.Drinker => $"{time} Нальёшь — расскажу всё, что знаю. Шучу. Почти.",
                Role.Patrol => $"{time} Не мешайся, {you}, я на обходе.",
                _ when who.Spec.Settlement != "gu_yue" => $"Чужак? Да ещё в одежде Гу Юэ. {time.Replace("Доброе утро.", "Утро не доброе.")} Говори, что надо, и уходи.",
                _ => $"{time}{weather}",
            };
        }

        public static string About(Villager who) => who.Spec.About;

        public static string News(Villager who, int day)
        {
            var mine = who.Spec.Lines;
            var general = who.Spec.Settlement == "gu_yue" ? General : new string[0];
            var pick = (day * 7 + who.Index * 3) % (mine.Length + general.Length);

            return pick < mine.Length ? mine[pick] : general[pick - mine.Length];
        }

        public static string Village(Villager who, int asked)
        {
            var facts = who.Spec.Settlement == "gu_yue" ? Facts : who.Spec.Lines;

            return facts[(asked + who.Index) % facts.Length];
        }

        public static string Farewell(Villager who, Gender hero) => who.Spec.Role switch
        {
            Role.Guard or Role.NightGuard => "Ступай. И держись подальше от леса после заката.",
            Role.Stall => "Заходи ещё — для своих всегда найдётся лишняя миска.",
            Role.Teacher => "Иди. И помни: Гу не терпят небрежности.",
            Role.Elder => "Ступай с миром.",
            Role.Student => "Пока! Увидимся на церемонии!",
            _ => "До встречи.",
        };

        /// <summary>News anyone in the village might tell.</summary>
        static readonly string[] General =
        {
            "Старейшины, говорят, уже готовят церемонию пробуждения. В этом году пробуждаться будет много наших ровесников.",
            "Караван из долины задерживается. Если не придёт до конца месяца — соли станет мало.",
            "У восточных ворот видели следы крупного зверя. Стража теперь ходит туда по двое.",
            "Деревня Бай на задней горе опять выставила дозоры у перевала. Не к добру это.",
            "Весной дожди тут частые. Хорошо хоть дома на сваях — вода под полом бежит, а не через порог.",
            "Кто-то из академии нашёл в роще дикого Гу. Наставники всех расспрашивали, где именно.",
        };

        /// <summary>What there is to know about the village and the mountain.</summary>
        static readonly string[] Facts =
        {
            "Наш клан пришёл на гору Цинмао издалека, много поколений назад. Остались здесь из-за духовного источника выше по склону.",
            "На горе три деревни: мы, Гу Юэ, на середине склона, Сюн — на передней горе, Бай — на задней. Дружбы между нами немного.",
            "Павильон клана в центре — пять ярусов. Там сидят старейшины, туда без дела не ходят.",
            "Академия западнее площади. Туда берут тех, у кого на церемонии пробудилась апертура.",
            "На церемонии пробуждения оценивают талант — от класса A до D. От класса зависит, сколько эссенции поместится в апертуре.",
            "Мастера Гу платят первобытными камнями. Простые люди обходятся медью и рисом.",
            "Гу — живые существа. Их кормят, берегут, а потом уже просят о силе. Голодный Гу и умереть может.",
            "Бамбуковая роща к востоку кормит пол-деревни: из бамбука и дома, и посуда, и вино.",
            "Стены у нас с башнями, и башни выше стен. Ночью на них всегда горит огонь.",
        };
    }
}
