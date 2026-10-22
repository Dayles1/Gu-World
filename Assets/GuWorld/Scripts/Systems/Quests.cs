using System;
using System.Collections.Generic;
using GuWorld.Items;
using UnityEngine;

namespace GuWorld.Systems
{
    /// <summary>A task: who gives it, what it asks, what counts towards it and what it pays.</summary>
    public class QuestKind
    {
        public string Id;
        public string Title;
        /// <summary>The villager who gives it and takes it back (their name).</summary>
        public string Giver;
        /// <summary>Who the giver is (for the HUD's "who waits for you").</summary>
        public string GiverTitle;
        /// <summary>What to do, in a line (the HUD's goal).</summary>
        public string Goal;
        /// <summary>
        /// What counts toward it: "hit_dummy", "gather:&lt;item&gt;", "buy:&lt;item&gt;", "feed:&lt;gu&gt;",
        /// "kill:wolf", "visit:&lt;place&gt;" (Commands' places), "talk:&lt;name&gt;", "deliver:&lt;item&gt;@&lt;name&gt;".
        /// </summary>
        public string Event;
        public int Target;
        /// <summary>A thing the giver hands over with the task (to deliver), and how many.</summary>
        public string Gives;
        public int GivesCount = 1;
        public int RewardStones;
        /// <summary>What the giver says handing it out, and taking it back.</summary>
        public string Offer;
        public string Thanks;

        /// <summary>A side task (taken beside the academy's), a clan mission of a day; the task that must be done before it; the rank it asks.</summary>
        public bool Side;
        public bool Mission;
        public string After;
        public int MinRank;
        /// <summary>What it pays besides stones: merit with the clan, and a thing (how many).</summary>
        public int RewardMerit;
        public string RewardItem;
        public int RewardItemCount;
    }

    /// <summary>Tasks as saved: the one taken, how far along, and the ones done.</summary>
    [Serializable]
    public class QuestSave
    {
        public string current;
        public int progress;
        public string[] done = new string[0];
        /// <summary>The side tasks taken and how far along each; the merit with the clan (now, and ever earned).</summary>
        public string[] sides = new string[0];
        public int[] side_progress = new int[0];
        public int merit;
        public int merit_earned;
    }

    /// <summary>
    /// The hero's tasks (the design's HUD task card): one at a time, taken from
    /// the one who gives it, counted as it is done, and handed back to them for
    /// the reward. The first are the academy's lessons for a new Gu Master:
    /// the moonblade, the Gu's food, feeding it, buying more. The words are the
    /// game's own.
    /// </summary>
    public class Quests
    {
        public const string Teacher = "Гу Юэ Чанмин";

        /// <summary>The academy's lessons, in order.</summary>
        public static readonly QuestKind[] Book =
        {
            new QuestKind
            {
                Id = "first_blade",
                Title = "Первый лунный клинок",
                Giver = Teacher,
                GiverTitle = "наставник академии",
                Goal = "Попадите лунным клинком в чучело во дворе академии",
                Event = "hit_dummy",
                Target = 3,
                RewardStones = 5,
                Offer = "Гу Лунного Света — гордость нашего клана. Ступай во двор академии и трижды попади лунным клинком " +
                        "в соломенное чучело. Помни: каждый бросок берёт десятую часть апертуры — считай эссенцию.",
                Thanks = "Неплохо для начала. Держи камни и трать их с умом: эссенция возвращается сама, но небыстро.",
            },
            new QuestKind
            {
                Id = "orchids",
                Title = "Пища для Гу",
                Giver = Teacher,
                GiverTitle = "наставник академии",
                Goal = "Соберите лепестки лунной орхидеи на грядках у источника",
                Event = "gather:" + ItemBook.Petal,
                Target = 8,
                RewardStones = 3,
                Offer = "Гу живые — их кормят. Твой Гу Лунного Света ест лепестки лунной орхидеи: два утром и два вечером. " +
                        "Ступай к грядкам у источника и набери восемь лепестков. Не держи их долго: сорванные, они вянут за пять дней.",
                Thanks = "Свежие лепестки. Гу ест их охотнее, чем лежалые.",
            },
            new QuestKind
            {
                Id = "feed",
                Title = "Сытый Гу",
                Giver = Teacher,
                GiverTitle = "наставник академии",
                Goal = "Накормите Гу Лунного Света (апертура — клавиша K)",
                Event = "feed:moonlight",
                Target = 1,
                RewardStones = 2,
                Offer = "Теперь накорми его. Открой апертуру и дай Гу его пищу. Голодный Гу слабеет, заморенный — гибнет. " +
                        "А если погибнет твой жизненный Гу, удар придётся и по тебе.",
                Thanks = "Так и держи. Мастер, который не кормит своих Гу, скоро остаётся без них.",
            },
            new QuestKind
            {
                Id = "market",
                Title = "Лавка на площади",
                Giver = Teacher,
                GiverTitle = "наставник академии",
                Goal = "Купите лепестки лунной орхидеи в лавке Баошаня на площади",
                Event = "buy:" + ItemBook.Petal,
                Target = 10,
                RewardStones = 2,
                Offer = "Грядки не бесконечны. На площади Баошань продаёт лепестки — десяток за первобытный камень. " +
                        "Купи десяток про запас.",
                Thanks = "Теперь ты знаешь, как прокормить своего Гу. Дальше учись сам. А за стенами нужны руки: " +
                         "загляни к Тешаню у южных ворот.",
            },
            new QuestKind
            {
                Id = "wolves_gate",
                Title = "Волки у южных ворот",
                Giver = "Гу Юэ Тешань",
                GiverTitle = "стражник южных ворот",
                Goal = "Убейте трёх волков в лесу за стенами деревни",
                Event = "kill:wolf",
                Target = 3,
                RewardStones = 8,
                Offer = "Слышал, наставник тебя хвалит. Волки опять подходят к стенам — по ночам воют у самых ворот. " +
                        "Ступай в лес и убей трёх. Держись на расстоянии: лунный клинок режет дальше, чем волк прыгает. " +
                        "И не трать эссенцию до дна — камень в бою поглощать долго.",
                Thanks = "Три клыка — три волка. Неплохо для ученика. Держи камни.",
            },
            new QuestKind
            {
                Id = "cave",
                Title = "Тропа над рощей",
                Giver = "Гу Юэ Шулинь",
                GiverTitle = "бамбукорез",
                Goal = "Поднимитесь над бамбуковой рощей к скале с пещерой (к востоку от деревни)",
                Event = "visit:cave",
                Target = 1,
                RewardStones = 4,
                Offer = "Над рощей, у скалы, есть расщелина — оттуда вечно сочится вода. Давно хочу знать, не обвалилась ли тропа. " +
                        "Сходи, посмотри. Мне с бамбуком не отойти.",
                Thanks = "Цела, говоришь? Хорошо. Странное это место: тихо там, как в храме. Возьми за труды.",
            },
            new QuestKind
            {
                Id = "noodles",
                Title = "Ужин для ночного стража",
                Giver = "Тётушка Хуа",
                GiverTitle = "лапшичная у площади",
                Goal = "Отнесите миску лапши ночному стражнику Лэю (северные ворота)",
                Event = "deliver:" + ItemBook.Noodles + "@Гу Юэ Лэй",
                Target = 1,
                RewardStones = 3,
                Gives = ItemBook.Noodles,
                Offer = "Лэй опять сторожит всю ночь на пустой желудок. Отнеси ему миску лапши к северным воротам, пока горячая. " +
                        "Только не съешь по дороге!",
                Thanks = "Отнёс? Вот и славно. Держи — и заходи поесть сам.",
            },
            new QuestKind
            {
                Id = "elder_word",
                Title = "Весть восточным воротам",
                Giver = "Старейшина Гу Юэ Хуайюй",
                GiverTitle = "хранитель святилища",
                Goal = "Передайте стражнику Хэну у восточных ворот слово старейшины",
                Event = "talk:Гу Юэ Хэн",
                Target = 1,
                RewardStones = 3,
                Offer = "Волчья стая ходит всё ближе. Передай Хэну у восточных ворот: пусть ночью держит факелы зажжёнными " +
                        "и не открывает ворот до рассвета.",
                Thanks = "Передал — хорошо. Клан держится на тех, кто делает малое без напоминаний.",
            },
            new QuestKind
            {
                Id = "wolf_pack",
                Title = "Волчья стая",
                Giver = "Гу Юэ Тешань",
                GiverTitle = "стражник южных ворот",
                Goal = "Убейте шестерых волков: стая подошла к деревне",
                Event = "kill:wolf",
                Target = 6,
                RewardStones = 15,
                Offer = "Это уже не пара бродяг — стая. Шестеро, а может, больше. Возьми камней в запас и бей их у логова, " +
                        "пока они не пришли к нам сами. Ночью они видят дальше — иди днём.",
                Thanks = "Шесть волков. Старейшина узнает об этом. Ты больше не просто ученик.",
            },
        };

        readonly HashSet<string> done = new HashSet<string>();
        /// <summary>The side tasks taken (up to MaxSides), and how far along each is.</summary>
        readonly List<QuestKind> sides = new List<QuestKind>();
        readonly Dictionary<string, int> sideProgress = new Dictionary<string, int>();

        /// <summary>How many side tasks the hero can have at once.</summary>
        public const int MaxSides = 3;

        public QuestKind Current { get; private set; }
        public int Progress { get; private set; }
        /// <summary>Done: back to the giver for the reward.</summary>
        public bool Ready => Current != null && Progress >= Current.Target;

        /// <summary>The side tasks taken.</summary>
        public IReadOnlyList<QuestKind> Sides => sides;

        /// <summary>The hero's merit with the clan (вклад): earned by tasks and missions, spent in the clan's treasury; and all ever earned.</summary>
        public int Merit { get; private set; }
        public int MeritEarned { get; private set; }

        /// <summary>A task finished or moved on — something to tell the player.</summary>
        public event Action<string> Said;

        /// <summary>A task of any kind by its id: the academy's, a side task, a mission.</summary>
        public static QuestKind Find(string id) => Array.Find(Book, q => q.Id == id) ?? SideQuests.Find(id);

        /// <summary>The next task waiting for the hero (none while one is taken, or when all are done).</summary>
        public QuestKind Next => Current != null ? null : Array.Find(Book, q => !done.Contains(q.Id));

        public bool IsDone(string id) => done.Contains(id);

        /// <summary>How far along a task taken is (the main one or a side one).</summary>
        public int ProgressOf(QuestKind quest) =>
            quest == null ? 0 : quest == Current ? Progress : sideProgress.TryGetValue(quest.Id, out var n) ? n : 0;

        public bool IsReady(QuestKind quest) => quest != null && ProgressOf(quest) >= quest.Target;

        public bool IsTaken(QuestKind quest) => quest != null && (Current?.Id == quest.Id || sides.Exists(s => s.Id == quest.Id));

        /// <summary>The next task a giver has for the hero (none while another is taken, or when theirs are all done).</summary>
        public QuestKind NextFrom(string giver)
        {
            if (Current != null)
            {
                return null;
            }

            foreach (var quest in Book)
            {
                if (!done.Contains(quest.Id))
                {
                    return quest.Giver == giver ? quest : null;
                }
            }

            return null;
        }

        /// <summary>
        /// The side tasks a giver has for the hero now: not done, not taken, the task before them done, the hero's rank
        /// high enough — and, from the clan's steward, the missions of the day.
        /// </summary>
        public List<QuestKind> SidesFrom(string giver, int rank, int day)
        {
            var offered = new List<QuestKind>();
            var pool = giver == SideQuests.Steward ? SideQuests.Missions(day) : SideQuests.Book;

            foreach (var quest in pool)
            {
                if (quest.Giver == giver && !done.Contains(quest.Id) && !IsTaken(quest) && rank >= quest.MinRank &&
                    (quest.After == null || done.Contains(quest.After)))
                {
                    offered.Add(quest);
                }
            }

            return offered;
        }

        /// <summary>Takes a task: the academy's in hand, a side one beside it (while there is room for it).</summary>
        public bool Take(QuestKind quest)
        {
            if (quest == null || done.Contains(quest.Id) || IsTaken(quest))
            {
                return false;
            }

            if (quest.Side)
            {
                if (sides.Count >= MaxSides)
                {
                    Said?.Invoke($"Побочных поручений уже {MaxSides} — сначала сдайте одно");

                    return false;
                }

                sides.Add(quest);
                sideProgress[quest.Id] = 0;
                Said?.Invoke($"Новое побочное поручение: {quest.Title}");

                return true;
            }

            if (Current != null)
            {
                return false;
            }

            Current = quest;
            Progress = 0;
            Said?.Invoke($"Новое задание: {quest.Title}");

            return true;
        }

        /// <summary>Whether a task's event takes a key: the same, or a pattern ending in "*" that it begins with ("catch:*").</summary>
        public static bool Matches(string pattern, string key) =>
            pattern != null && key != null && (pattern == key || (pattern.EndsWith("*") && key.StartsWith(pattern.Substring(0, pattern.Length - 1))));

        /// <summary>Counts something done towards every task taken that it is for.</summary>
        public void Count(string key, int n = 1)
        {
            if (n <= 0)
            {
                return;
            }

            if (Current != null && !Ready && Matches(Current.Event, key))
            {
                Progress = Mathf.Min(Current.Target, Progress + n);

                if (Ready)
                {
                    Said?.Invoke($"Задание выполнено: {Current.Title}. Вернитесь к: {Current.Giver}");
                }
            }

            foreach (var side in sides)
            {
                if (IsReady(side) || !Matches(side.Event, key))
                {
                    continue;
                }

                sideProgress[side.Id] = Mathf.Min(side.Target, ProgressOf(side) + n);

                if (IsReady(side))
                {
                    Said?.Invoke($"Поручение выполнено: {side.Title}. Вернитесь к: {side.Giver}");
                }
            }
        }

        /// <summary>Hands the finished task back to its giver; returns it (for the reward), or null.</summary>
        public QuestKind HandIn(string giver)
        {
            if (!Ready || Current.Giver != giver)
            {
                return null;
            }

            var quest = Current;
            done.Add(quest.Id);
            Current = null;
            Progress = 0;
            AddMerit(quest.RewardMerit);

            return quest;
        }

        /// <summary>Hands a finished side task back to its giver: true when it was (its merit is the hero's).</summary>
        public bool HandInSide(QuestKind quest, string giver)
        {
            if (quest == null || quest.Giver != giver || !IsReady(quest) || !sides.Exists(s => s.Id == quest.Id))
            {
                return false;
            }

            sides.RemoveAll(s => s.Id == quest.Id);
            sideProgress.Remove(quest.Id);
            done.Add(quest.Id);
            AddMerit(quest.RewardMerit);

            return true;
        }

        /// <summary>A side task given up (not the academy's): nothing is paid, it can be taken again.</summary>
        public void Drop(QuestKind quest)
        {
            if (quest != null && sides.RemoveAll(s => s.Id == quest.Id) > 0)
            {
                sideProgress.Remove(quest.Id);
                Said?.Invoke($"Поручение оставлено: {quest.Title}");
            }
        }

        public void AddMerit(int n)
        {
            if (n > 0)
            {
                Merit += n;
                MeritEarned += n;
            }
        }

        /// <summary>Merit spent (the treasury); false (and none spent) when there is not enough.</summary>
        public bool SpendMerit(int n)
        {
            if (n < 0 || Merit < n)
            {
                return false;
            }

            Merit -= n;

            return true;
        }

        /// <summary>The hero's standing with the clan by the merit ever earned: a word, and the shops' discount it brings.</summary>
        public string Standing => MeritEarned switch
        {
            < 20 => "никто",
            < 60 => "замечен",
            < 150 => "уважаем",
            < 300 => "ценим кланом",
            _ => "гордость клана",
        };

        public float Discount => Mathf.Min(0.15f, MeritEarned / 2000f);

        public QuestSave Save()
        {
            var save = new QuestSave
            {
                current = Current?.Id,
                progress = Progress,
                done = new List<string>(done).ToArray(),
                sides = sides.ConvertAll(s => s.Id).ToArray(),
                merit = Merit,
                merit_earned = MeritEarned,
            };
            save.side_progress = Array.ConvertAll(save.sides, id => sideProgress.TryGetValue(id, out var n) ? n : 0);

            return save;
        }

        /// <summary>Tasks from a save (unknown ones dropped, the progress kept within the target).</summary>
        public void Load(QuestSave saved)
        {
            done.Clear();
            sides.Clear();
            sideProgress.Clear();
            Current = null;
            Progress = 0;
            Merit = 0;
            MeritEarned = 0;

            if (saved == null)
            {
                return;
            }

            foreach (var id in saved.done ?? new string[0])
            {
                // A mission's id is kept as it is (it is found by its day and index).
                if (Find(id) != null)
                {
                    done.Add(id);
                }
            }

            var current = Find(saved.current);

            if (current != null && !current.Side && !done.Contains(current.Id))
            {
                Current = current;
                Progress = Mathf.Clamp(saved.progress, 0, current.Target);
            }

            for (var i = 0; saved.sides != null && i < saved.sides.Length && sides.Count < MaxSides; i++)
            {
                var side = Find(saved.sides[i]);

                if (side != null && side.Side && !done.Contains(side.Id) && !IsTaken(side))
                {
                    sides.Add(side);
                    sideProgress[side.Id] = Mathf.Clamp(saved.side_progress != null && i < saved.side_progress.Length ? saved.side_progress[i] : 0, 0, side.Target);
                }
            }

            Merit = Mathf.Max(0, saved.merit);
            MeritEarned = Mathf.Max(Merit, saved.merit_earned);
        }
    }
}
