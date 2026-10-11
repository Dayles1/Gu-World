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
        /// <summary>What to do, in a line (the HUD's goal).</summary>
        public string Goal;
        /// <summary>What counts: an event's key ("hit_dummy", "gather:moon_orchid_petal", "feed:moonlight", "buy:…").</summary>
        public string Event;
        public int Target;
        public int RewardStones;
        /// <summary>What the giver says handing it out, and taking it back.</summary>
        public string Offer;
        public string Thanks;
    }

    /// <summary>Tasks as saved: the one taken, how far along, and the ones done.</summary>
    [Serializable]
    public class QuestSave
    {
        public string current;
        public int progress;
        public string[] done = new string[0];
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
                Goal = "Купите лепестки лунной орхидеи в лавке Баошаня на площади",
                Event = "buy:" + ItemBook.Petal,
                Target = 10,
                RewardStones = 2,
                Offer = "Грядки не бесконечны. На площади Баошань продаёт лепестки — десяток за первобытный камень. " +
                        "Купи десяток про запас.",
                Thanks = "Теперь ты знаешь, как прокормить своего Гу. Дальше учись сам — и приходи, когда будешь готов к большему.",
            },
        };

        readonly HashSet<string> done = new HashSet<string>();

        public QuestKind Current { get; private set; }
        public int Progress { get; private set; }
        /// <summary>Done: back to the giver for the reward.</summary>
        public bool Ready => Current != null && Progress >= Current.Target;

        /// <summary>A task finished or moved on — something to tell the player.</summary>
        public event Action<string> Said;

        public static QuestKind Find(string id) => Array.Find(Book, q => q.Id == id);

        public bool IsDone(string id) => done.Contains(id);

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

        public void Take(QuestKind quest)
        {
            if (quest == null || Current != null || done.Contains(quest.Id))
            {
                return;
            }

            Current = quest;
            Progress = 0;
            Said?.Invoke($"Новое задание: {quest.Title}");
        }

        /// <summary>Counts something done towards the task taken.</summary>
        public void Count(string key, int n = 1)
        {
            if (Current == null || Ready || Current.Event != key || n <= 0)
            {
                return;
            }

            Progress = Mathf.Min(Current.Target, Progress + n);

            if (Ready)
            {
                Said?.Invoke($"Задание выполнено: {Current.Title}. Вернитесь к: {Current.Giver}");
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

            return quest;
        }

        public QuestSave Save() => new QuestSave
        {
            current = Current?.Id,
            progress = Progress,
            done = new List<string>(done).ToArray(),
        };

        /// <summary>Tasks from a save (unknown ones dropped, the progress kept within the target).</summary>
        public void Load(QuestSave saved)
        {
            done.Clear();
            Current = null;
            Progress = 0;

            if (saved == null)
            {
                return;
            }

            foreach (var id in saved.done ?? new string[0])
            {
                if (Find(id) != null)
                {
                    done.Add(id);
                }
            }

            var current = Find(saved.current);

            if (current != null && !done.Contains(current.Id))
            {
                Current = current;
                Progress = Mathf.Clamp(saved.progress, 0, current.Target);
            }
        }
    }
}
