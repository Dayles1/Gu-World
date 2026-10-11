using System;
using System.Collections.Generic;
using GuWorld.Items;
using GuWorld.Physics;
using GuWorld.Render;
using GuWorld.Systems;
using UnityEngine;

namespace GuWorld.Cultivation
{
    /// <summary>A Gu refined into the aperture: which kind, where in the ring, when it last ate.</summary>
    [Serializable]
    public class OwnedGu
    {
        public string kind;
        /// <summary>Its cell in the aperture's ring (0…7).</summary>
        public int slot;
        /// <summary>The game minute of its last meal.</summary>
        public double fedAt;
        /// <summary>The first Gu one refines: its life is tied to its master's (lore: sys_vital_gu).</summary>
        public bool vital;
        /// <summary>How hungry it was when the player was last told.</summary>
        [NonSerialized] public Hunger told;

        public GuKind Kind => GuBook.Find(kind);
    }

    /// <summary>What a Gu Master carries, as saved: the aperture, the Gu in it, the bag, the action bar.</summary>
    [Serializable]
    public class CultivationSave
    {
        /// <summary>Whether this holds a Gu Master at all (a save from before cultivation does not).</summary>
        public bool set;
        public string grade = "C";
        public int rank = 1;
        public string stage = "initial";
        public float essence;
        public OwnedGu[] owned;
        public ItemStack[] bag;
        /// <summary>The action bar: "gu:&lt;kind&gt;" or "item:&lt;id&gt;" per key, empty where nothing is set.</summary>
        public string[] bar = new string[0];
        /// <summary>Whether the jade skin was up.</summary>
        public bool skin;

        // A save from before the bag (format 2's first cultivation): stones counted, Gu by kind, five slots.
        public int stones;
        public string[] gu = new string[0];
        public string[] slots = new string[0];
    }

    /// <summary>
    /// The hero as a Gu Master (the design's aperture page): the aperture and its
    /// sea; the ring of eight cells round it where refined Gu live — five open at
    /// rank one, two more at rank two, the last at three; the bag; and the action
    /// bar, keys 1–9, each holding a Gu's skill or a thing from the bag.
    ///
    /// Gu are alive (lore: sys_vital_gu, res_moon_orchid): each eats its own food
    /// at its own hours. Unfed past its meal it is hungry, then too starved to
    /// answer, then dead — and if that was the first Gu one refined, the blow
    /// falls on its master too. Using a Gu drives it with essence, if there is
    /// enough and it has rested; time refills the sea, a stone tops it up at once.
    ///
    /// Until the awakening ceremony is in the game, a new game starts as a test
    /// Gu Master: C grade, a full sea, a Moonlight Gu in the ring, a few stones
    /// and a day's petals.
    /// </summary>
    public class Cultivator : MonoBehaviour
    {
        public const int Ring = 8;
        public const int BarSize = 9;

        public Aperture Aperture { get; private set; } = Aperture.Full(Grade.C);
        public readonly Bag Bag = new Bag();
        public readonly List<OwnedGu> Gu = new List<OwnedGu>();
        public readonly string[] Bar = new string[BarSize];
        /// <summary>Whether the jade skin is up.</summary>
        public bool SkinOn { get; private set; }
        /// <summary>The game minute the Gu Master last lived up to (Tick).</summary>
        public double Now { get; private set; }
        public int Stones => Bag.Count(ItemBook.Stone);

        /// <summary>Something to tell the player (not enough essence, a Gu hungry…).</summary>
        public event Action<string> Said;
        /// <summary>Something done that tasks may count ("feed:moonlight", "refine:jade_skin", "moonblade").</summary>
        public event Action<string, int> Did;

        readonly Dictionary<string, float> readyAt = new Dictionary<string, float>();
        HeroMotor body;
        HeroView view;
        Health health;
        CharacterController solid;
        public ThirdPersonCamera Camera;

        void Awake()
        {
            body = GetComponent<HeroMotor>();
            view = GetComponent<HeroView>();
            health = GetComponent<Health>();
            solid = GetComponent<CharacterController>();
        }

        /// <summary>How many of the ring's cells are open at a rank.</summary>
        public static int OpenSlots(int rank) => rank >= 3 ? Ring : rank == 2 ? 7 : 5;

        public int Open => OpenSlots(Aperture.Rank);

        /// <summary>The Gu in a cell of the ring (null: none).</summary>
        public OwnedGu At(int slot) => Gu.Find(g => g.slot == slot);

        /// <summary>The first Gu of a kind in the ring (null: none).</summary>
        public OwnedGu First(string kind) => Gu.Find(g => g.kind == kind);

        public Hunger HungerOf(OwnedGu gu) => gu.Kind?.HungerAfter(Now - gu.fedAt) ?? Hunger.Dead;

        /// <summary>Game minutes until a Gu's next meal is due (below 0: overdue).</summary>
        public double MealIn(OwnedGu gu) => gu.fedAt + (gu.Kind?.MealEvery ?? 0) - Now;

        // ── Saving ────────────────────────────────────────────────────

        /// <summary>A Gu Master from a save — or, with none, the test one — at a game minute.</summary>
        public void Load(CultivationSave saved, double now)
        {
            Now = now;
            Gu.Clear();
            Bag.Clear();
            Array.Clear(Bar, 0, BarSize);
            readyAt.Clear();
            SkinOn = false;

            if (saved == null || !saved.set)
            {
                Aperture = Aperture.Full(Grade.C);
                Gu.Add(new OwnedGu { kind = GuBook.Moonlight.Id, slot = 0, fedAt = now, vital = true });
                Bag.Add(ItemBook.Stone, 10, now);
                Bag.Add(ItemBook.Petal, 8, now);
                Bar[0] = "gu:" + GuBook.Moonlight.Id;
                Bar[1] = "item:" + ItemBook.Petal;
                Bar[2] = "item:" + ItemBook.Stone;

                return;
            }

            Aperture = new Aperture
            {
                Grade = Enum.TryParse<Grade>(saved.grade, true, out var grade) ? grade : Grade.C,
                Rank = saved.rank,
                Stage = Enum.TryParse<Stage>(saved.stage, true, out var stage) ? stage : Stage.Initial,
                Essence = saved.essence,
            };
            Aperture.Repair();
            Bag.Load(saved.bag);

            if (saved.owned != null)
            {
                foreach (var gu in saved.owned)
                {
                    if (gu == null || GuBook.Find(gu.kind) == null || gu.slot < 0 || gu.slot >= Open || At(gu.slot) != null)
                    {
                        continue;
                    }

                    Gu.Add(new OwnedGu
                    {
                        kind = gu.kind,
                        slot = gu.slot,
                        fedAt = double.IsFinite(gu.fedAt) && gu.fedAt <= now ? gu.fedAt : now,
                        vital = gu.vital && !Gu.Exists(g => g.vital),
                    });
                }
            }
            else
            {
                // The older save: its Gu fed just now, its stones into the bag, its slots onto the bar.
                foreach (var id in saved.gu ?? new string[0])
                {
                    if (GuBook.Find(id) != null && First(id) == null && Gu.Count < Open)
                    {
                        Gu.Add(new OwnedGu { kind = id, slot = Gu.Count, fedAt = now, vital = Gu.Count == 0 });
                    }
                }

                Bag.Add(ItemBook.Stone, Mathf.Max(0, saved.stones), now);

                for (var i = 0; i < (saved.slots?.Length ?? 0) && i < BarSize; i++)
                {
                    if (saved.slots[i] != null && First(saved.slots[i]) != null)
                    {
                        Bar[i] = "gu:" + saved.slots[i];
                    }
                }
            }

            for (var i = 0; i < BarSize && i < (saved.bar?.Length ?? 0); i++)
            {
                Bar[i] = Valid(saved.bar[i]) ? saved.bar[i] : null;
            }

            foreach (var gu in Gu)
            {
                gu.told = HungerOf(gu);
            }

            SkinOn = saved.skin && First(GuBook.JadeSkin.Id) != null && HungerOf(First(GuBook.JadeSkin.Id)) < Hunger.Starving;
        }

        /// <summary>Whether an action bar entry names something that exists.</summary>
        static bool Valid(string entry) =>
            entry != null && (entry.StartsWith("gu:") ? GuBook.Find(entry.Substring(3)) != null : entry.StartsWith("item:") && ItemBook.Find(entry.Substring(5)) != null);

        public CultivationSave Save()
        {
            var owned = new OwnedGu[Gu.Count];

            for (var i = 0; i < Gu.Count; i++)
            {
                owned[i] = new OwnedGu { kind = Gu[i].kind, slot = Gu[i].slot, fedAt = Gu[i].fedAt, vital = Gu[i].vital };
            }

            return new CultivationSave
            {
                set = true,
                grade = Aperture.Grade.ToString(),
                rank = Aperture.Rank,
                stage = Aperture.Stage.ToString().ToLowerInvariant(),
                essence = Aperture.Essence,
                owned = owned,
                bag = Bag.Save(),
                bar = (string[])Bar.Clone(),
                skin = SkinOn,
            };
        }

        // ── Time ──────────────────────────────────────────────────────

        /// <summary>
        /// Lives up to a game minute: the sea refills (and pays for a raised skin), the Gu
        /// grow hungry — and are told about as they do — and what spoils in the bag spoils.
        /// </summary>
        public void Tick(double now)
        {
            var minutes = now - Now;

            if (!double.IsFinite(minutes) || minutes < 0)
            {
                Now = now;

                return;
            }

            Now = now;

            if (minutes > 0)
            {
                Aperture.Recover(minutes);

                if (SkinOn)
                {
                    var upkeep = (float)(GuBook.JadeSkin.Upkeep * minutes);

                    if (!Aperture.Spend(upkeep))
                    {
                        Aperture.Essence = 0;
                        SkinOn = false;
                        Said?.Invoke("Нефритовая кожа спала: кончилась эссенция");
                    }
                }
            }

            for (var i = Gu.Count - 1; i >= 0; i--)
            {
                var gu = Gu[i];
                var hunger = HungerOf(gu);

                if (hunger == gu.told)
                {
                    continue;
                }

                // Only getting worse is news; a meal is told where it is given.
                if (hunger > gu.told)
                {
                    Tell(gu, hunger);
                }

                gu.told = hunger;
            }

            var lost = Bag.Spoil(now);

            if (lost != null)
            {
                foreach (var pair in lost)
                {
                    Said?.Invoke($"Испортилось: {ItemBook.Find(pair.Key)?.Name ?? pair.Key} ×{pair.Value}");
                }
            }
        }

        void Tell(OwnedGu gu, Hunger hunger)
        {
            var kind = gu.Kind;

            switch (hunger)
            {
                case Hunger.Hungry:
                    Said?.Invoke($"{kind.Name} проголодался");
                    break;
                case Hunger.Starving:
                    Said?.Invoke($"{kind.Name} истощён от голода — пока его не накормить, он не ответит");

                    if (kind.Skill == SkillKind.JadeSkin && SkinOn)
                    {
                        SkinOn = false;
                    }

                    break;
                case Hunger.Dead:
                    Die(gu);
                    break;
            }
        }

        /// <summary>A Gu starved to death: gone from the ring and the bar; the vital Gu's death strikes its master.</summary>
        void Die(OwnedGu gu)
        {
            Gu.Remove(gu);
            var kind = gu.Kind;

            if (First(gu.kind) == null)
            {
                for (var i = 0; i < BarSize; i++)
                {
                    if (Bar[i] == "gu:" + gu.kind)
                    {
                        Bar[i] = null;
                    }
                }

                if (kind.Skill == SkillKind.JadeSkin)
                {
                    SkinOn = false;
                }
            }

            if (gu.vital)
            {
                Aperture.Essence = 0;
                health?.Strike(Health.Max * 0.5f);
                Said?.Invoke($"{kind.Name} умер от голода. Это был жизненный Гу — удар отозвался в апертуре");
            }
            else
            {
                Said?.Invoke($"{kind.Name} умер от голода");
            }
        }

        // ── Using ─────────────────────────────────────────────────────

        /// <summary>Seconds before a kind of Gu can be used again (0: ready).</summary>
        public float CooldownLeft(string kind) => kind != null && readyAt.TryGetValue(kind, out var at) ? Mathf.Max(0, at - Time.time) : 0;

        /// <summary>Uses what is set on a key of the action bar.</summary>
        public bool Use(int key)
        {
            var entry = key >= 0 && key < BarSize ? Bar[key] : null;

            if (entry == null)
            {
                Said?.Invoke($"Клавиша {key + 1}: ничего не назначено");

                return false;
            }

            return entry.StartsWith("gu:") ? UseGu(entry.Substring(3)) : UseItem(entry.Substring(5));
        }

        /// <summary>Uses a Gu of a kind: its skill, if it is fed enough, rested, and there is the essence.</summary>
        public bool UseGu(string kindId)
        {
            var gu = First(kindId);
            var kind = GuBook.Find(kindId);

            if (gu == null || kind == null)
            {
                Said?.Invoke($"{kind?.Name ?? "Этого Гу"} нет в апертуре");

                return false;
            }

            if (HungerOf(gu) >= Hunger.Starving)
            {
                Said?.Invoke($"{kind.Name} истощён от голода — сначала накормите его");

                return false;
            }

            if (CooldownLeft(kindId) > 0 || (body != null && !body.Free))
            {
                return false;
            }

            switch (kind.Skill)
            {
                case SkillKind.Moonblade:
                    if (!Aperture.Spend(kind.Cost))
                    {
                        Said?.Invoke($"Не хватает первобытной эссенции ({kind.Cost:0}% апертуры)");

                        return false;
                    }

                    ThrowMoonblade(kind);
                    Did?.Invoke("moonblade", 1);
                    break;

                case SkillKind.JadeSkin:
                    if (SkinOn)
                    {
                        SkinOn = false;
                        Said?.Invoke("Нефритовая кожа снята");
                    }
                    else
                    {
                        if (!Aperture.Spend(kind.Cost))
                        {
                            Said?.Invoke($"Не хватает первобытной эссенции ({kind.Cost:0}% апертуры)");

                            return false;
                        }

                        SkinOn = true;
                        Said?.Invoke("Нефритовая кожа: тело покрыто нефритом, эссенция уходит, пока она держится");
                    }

                    break;

                default:
                    Said?.Invoke($"У {kind.Name} нет навыка, который можно применить");

                    return false;
            }

            readyAt[kindId] = Time.time + kind.Cooldown;

            return true;
        }

        /// <summary>Uses a thing of a kind from the bag (its main action).</summary>
        public bool UseItem(string id)
        {
            var kind = ItemBook.Find(id);

            if (kind == null || !Bag.Has(id, 1))
            {
                Said?.Invoke($"В сумке нет: {kind?.Name ?? id}");

                return false;
            }

            for (var cell = 0; cell < Bag.Cells; cell++)
            {
                if (Bag[cell]?.id == id)
                {
                    return UseCell(cell);
                }
            }

            return false;
        }

        /// <summary>The main action on one cell of the bag: refine a Gu, draw in a stone, eat, feed the Gu that eat it.</summary>
        public bool UseCell(int cell)
        {
            var stack = Bag[cell];
            var kind = stack?.Kind;

            if (kind == null)
            {
                return false;
            }

            switch (kind.Use)
            {
                case ItemUse.Refine:
                    return Refine(cell);

                case ItemUse.Absorb:
                    return AbsorbStone();

                case ItemUse.Eat:
                    if (health != null && health.Current >= Health.Max - 0.5f)
                    {
                        Said?.Invoke("Вы и так полны сил");

                        return false;
                    }

                    Bag.TakeFrom(cell, 1);
                    health?.Heal(kind.Heal);
                    Said?.Invoke($"{kind.Name}: +{kind.Heal:0} здоровья");

                    return true;

                case ItemUse.Feed:
                    return FeedWith(kind.Id);

                default:
                    return false;
            }
        }

        /// <summary>Refines a Gu from the bag into a free cell of the ring, for essence (lore: sys_refinement).</summary>
        public bool Refine(int cell)
        {
            var kind = GuBook.Find(Bag[cell]?.Kind?.Gu);

            if (kind == null)
            {
                return false;
            }

            if (kind.Rank > Aperture.Rank)
            {
                Said?.Invoke($"Гу ранга {kind.Rank} не удержать в апертуре ранга {Aperture.Rank}");

                return false;
            }

            var slot = -1;

            for (var i = 0; i < Open && slot < 0; i++)
            {
                if (At(i) == null)
                {
                    slot = i;
                }
            }

            if (slot < 0)
            {
                Said?.Invoke("В апертуре нет свободной ячейки");

                return false;
            }

            if (!Aperture.Spend(kind.RefineCost))
            {
                Said?.Invoke($"Чтобы очистить {kind.Name}, нужно {kind.RefineCost:0}% эссенции разом — поглотите камень или подождите");

                return false;
            }

            Bag.TakeFrom(cell, 1);
            var gu = new OwnedGu { kind = kind.Id, slot = slot, fedAt = Now, vital = !Gu.Exists(g => g.vital) };
            Gu.Add(gu);
            Said?.Invoke(gu.vital ? $"{kind.Name} очищен — это ваш жизненный Гу" : $"{kind.Name} очищен и вошёл в апертуру");

            // A new skill goes onto the bar, if it is not there and there is room.
            if (Array.IndexOf(Bar, "gu:" + kind.Id) < 0)
            {
                var free = Array.IndexOf(Bar, null);

                if (free >= 0)
                {
                    Bar[free] = "gu:" + kind.Id;
                }
            }

            Did?.Invoke("refine:" + kind.Id, 1);

            return true;
        }

        /// <summary>
        /// Feeds a Gu its meal from the bag. A Gu fed less than half a meal's time ago is
        /// not fed again (the food would be wasted).
        /// </summary>
        public bool Feed(OwnedGu gu, bool quiet = false)
        {
            var kind = gu?.Kind;

            if (kind == null)
            {
                return false;
            }

            if (Now - gu.fedAt < kind.MealEvery * 0.5)
            {
                if (!quiet) Said?.Invoke($"{kind.Name} сыт");

                return false;
            }

            var food = ItemBook.Find(kind.Food);

            if (!Bag.Take(kind.Food, kind.Meal))
            {
                if (!quiet) Said?.Invoke($"Нечем кормить {kind.Name}: нужно {food?.Name.ToLowerInvariant()} ×{kind.Meal}");

                return false;
            }

            gu.fedAt = Now;
            gu.told = Hunger.Fed;
            Said?.Invoke($"{kind.Name} накормлен ({food?.Name.ToLowerInvariant()} ×{kind.Meal})");
            Did?.Invoke("feed:" + kind.Id, 1);

            return true;
        }

        /// <summary>Feeds every Gu that is due (the design's "feed all"); returns how many ate.</summary>
        public int FeedAll()
        {
            var fed = 0;
            var wanting = new List<string>();

            foreach (var gu in Gu.ToArray())
            {
                if (Now - gu.fedAt < gu.Kind.MealEvery * 0.5)
                {
                    continue;
                }

                if (Feed(gu, true))
                {
                    fed++;
                }
                else
                {
                    wanting.Add(gu.Kind.Name);
                }
            }

            if (wanting.Count > 0)
            {
                Said?.Invoke($"Нечем кормить: {string.Join(", ", wanting)}");
            }
            else if (fed == 0)
            {
                Said?.Invoke("Все Гу сыты");
            }

            return fed;
        }

        /// <summary>Feeds the Gu that eat a food (the bar's or the bag's "feed").</summary>
        public bool FeedWith(string food)
        {
            OwnedGu hungriest = null;

            foreach (var gu in Gu)
            {
                if (gu.Kind.Food == food && (hungriest == null || gu.fedAt < hungriest.fedAt))
                {
                    hungriest = gu;
                }
            }

            if (hungriest == null)
            {
                Said?.Invoke($"Этим не кормят ни одного вашего Гу");

                return false;
            }

            return Feed(hungriest);
        }

        /// <summary>The cut: the hero turns to where the camera looks and throws the blade from the right hand.</summary>
        void ThrowMoonblade(GuKind kind)
        {
            var aim = Camera != null ? Camera.transform.forward : transform.forward;
            // Not into one's own feet: aim no lower than a little below level.
            aim.y = Mathf.Max(aim.y, -0.25f);
            aim.Normalize();
            var flat = new Vector3(aim.x, 0, aim.z).normalized;

            if (flat.sqrMagnitude > 0.01f && body != null)
            {
                body.Facing = Mathf.Atan2(flat.x, flat.z);
                transform.rotation = Quaternion.Euler(0, body.Facing * Mathf.Rad2Deg, 0);
            }

            var height = view != null ? 1.3f * view.Scale : 1.25f;
            var right = Vector3.Cross(Vector3.up, flat);
            var from = transform.position + Vector3.up * height + right * 0.25f + flat * 0.45f;
            Moonblade.Throw(from, aim, kind, solid);
            view?.Throw();
        }

        /// <summary>Draws in a primeval stone's essence.</summary>
        public bool AbsorbStone()
        {
            if (Stones <= 0)
            {
                Said?.Invoke("Нет первобытных камней");

                return false;
            }

            if (!Aperture.Absorb())
            {
                Said?.Invoke("Море эссенции и так полно");

                return false;
            }

            Bag.Take(ItemBook.Stone, 1);
            Said?.Invoke($"Камень поглощён: +{Aperture.StoneWorth:0}% апертуры");

            return true;
        }

        /// <summary>
        /// Harm meeting the jade skin, if it is up: the skin stops what the essence can pay
        /// for (lore: harder blows spend more) and gives way when the sea runs dry. Returns
        /// the harm that gets through.
        /// </summary>
        public float Shield(float harm)
        {
            if (!SkinOn || harm <= 0)
            {
                return harm;
            }

            var cost = harm * GuBook.JadeSkin.PerHarm;

            if (Aperture.Spend(cost))
            {
                return 0;
            }

            var stopped = Aperture.Essence / GuBook.JadeSkin.PerHarm;
            Aperture.Essence = 0;
            SkinOn = false;
            Said?.Invoke("Нефритовая кожа треснула: эссенция кончилась");

            return Mathf.Max(0, harm - stopped);
        }

        // ── The action bar ────────────────────────────────────────────

        /// <summary>Sets something on a key (taking it off any other).</summary>
        public void Bind(int key, string entry)
        {
            if (key < 0 || key >= BarSize || !Valid(entry))
            {
                return;
            }

            for (var i = 0; i < BarSize; i++)
            {
                if (Bar[i] == entry)
                {
                    Bar[i] = null;
                }
            }

            Bar[key] = entry;
        }

        public void Unbind(int key)
        {
            if (key >= 0 && key < BarSize)
            {
                Bar[key] = null;
            }
        }

        /// <summary>For testing: the next talent grade (A → B → C → D → A), the sea kept within it.</summary>
        public void NextGrade()
        {
            Aperture.Grade = (Grade)(((int)Aperture.Grade + 1) % 4);
            Aperture.Essence = Mathf.Min(Aperture.Essence, Aperture.Cap);
            Said?.Invoke($"Талант: класс {Aperture.Grade} (предел {Aperture.Cap:0}%)");
        }
    }
}
