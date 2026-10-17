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
    /// rank one, two more at rank two, the last at three; the bag; and the Gu's
    /// keys, 1–4, each holding a refined Gu's skill (changed on the aperture page).
    /// Primeval stones have their own key (V) that does not change: drawing one in
    /// takes three seconds (Absorbing), so the sea cannot be refilled at will in
    /// the middle of a fight.
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
        /// <summary>The Gu's keys, 1–4.</summary>
        public const int BarSize = 4;
        /// <summary>Seconds it takes to draw in a primeval stone.</summary>
        public const float AbsorbTime = 3;

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

            if (health != null)
            {
                health.Hurt += _ => CancelAbsorbing("Поглощение сорвано: вас ударили");
            }
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

            // The Gu's keys (a save from the nine-key bar: its Gu, in order; things from the bag are no longer on keys).
            var key = 0;

            for (var i = 0; i < (saved.bar?.Length ?? 0) && key < BarSize; i++)
            {
                if (Valid(saved.bar[i]) && Array.IndexOf(Bar, saved.bar[i]) < 0)
                {
                    Bar[key++] = saved.bar[i];
                }
            }

            foreach (var gu in Gu)
            {
                gu.told = HungerOf(gu);
            }

            SkinOn = saved.skin && First(GuBook.JadeSkin.Id) != null && HungerOf(First(GuBook.JadeSkin.Id)) < Hunger.Starving;
        }

        /// <summary>Whether a key's entry names a Gu that exists (only Gu go on the keys).</summary>
        static bool Valid(string entry) => entry != null && entry.StartsWith("gu:") && GuBook.Find(entry.Substring(3)) != null;

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

        /// <summary>The Gu on a key (null: none set).</summary>
        public GuKind KindOn(int key) => key >= 0 && key < BarSize && Bar[key] != null ? GuBook.Find(Bar[key].Substring(3)) : null;

        /// <summary>Uses the Gu set on a key, its skill aimed at a point (none: where the middle of the screen points).</summary>
        public bool Use(int key, Vector3? target = null)
        {
            var entry = key >= 0 && key < BarSize ? Bar[key] : null;

            if (entry == null)
            {
                Said?.Invoke($"Клавиша {key + 1}: Гу не назначен (апертура, K)");

                return false;
            }

            return UseGu(entry.Substring(3), target);
        }

        /// <summary>Uses a Gu of a kind: its skill, if it is fed enough, rested, and there is the essence.</summary>
        public bool UseGu(string kindId, Vector3? target = null)
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

                    CancelAbsorbing(null);
                    ThrowMoonblade(kind, target ?? AimPoint(kind));
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
                    // Three seconds in the world, as with the stone key (a page pauses the world: it is begun, not done).
                    return StartAbsorbing();

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
            // Short of its own food by one portion: a stalk of a food any Gu eats (intimate grass) stands in for it —
            // for one portion only, never the whole meal (lore: res_intimate_grass).
            var filler = kind.Meal > 1 && Bag.Count(kind.Food) == kind.Meal - 1 && Bag.Has(ItemBook.IntimateGrass, 1) ? 1 : 0;

            if (Bag.Count(kind.Food) + filler < kind.Meal)
            {
                if (!quiet) Said?.Invoke($"Нечем кормить {kind.Name}: нужно {food?.Name.ToLowerInvariant()} ×{kind.Meal}" +
                                         (kind.Meal > 1 ? " (одну порцию может заменить трава близости)" : ""));

                return false;
            }

            Bag.Take(kind.Food, kind.Meal - filler);

            if (filler > 0)
            {
                Bag.Take(ItemBook.IntimateGrass, filler);
            }

            gu.fedAt = Now;
            gu.told = Hunger.Fed;
            Said?.Invoke($"{kind.Name} накормлен ({food?.Name.ToLowerInvariant()} ×{kind.Meal - filler}" + (filler > 0 ? " и трава близости" : "") + ")");
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
            if (ItemBook.Find(food) is { AnyGu: true } stopgap)
            {
                return Stopgap(stopgap);
            }

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

        /// <summary>
        /// Food any Gu eats besides its own (intimate grass), used from the bag: the hungriest living Gu that is due a
        /// meal is fed, the stalk standing in for one portion of it (Feed) — the rest must still be its own food.
        /// </summary>
        bool Stopgap(ItemKind food)
        {
            OwnedGu hungriest = null;
            var most = 0.0;

            foreach (var gu in Gu)
            {
                var kind = gu.Kind;

                if (kind == null || HungerOf(gu) == Hunger.Dead)
                {
                    continue;
                }

                var meals = (Now - gu.fedAt) / kind.MealEvery;

                if (meals > most)
                {
                    most = meals;
                    hungriest = gu;
                }
            }

            if (hungriest == null || most < 0.5)
            {
                Said?.Invoke(hungriest == null && Gu.Count == 0 ? "Кормить некого: в апертуре нет Гу" : "Ваши Гу сыты");

                return false;
            }

            return Feed(hungriest);
        }

        int throws;

        /// <summary>Where a skill is aimed by default: the middle of the screen (the crosshair), out to the skill's range.</summary>
        public Vector3 AimPoint(GuKind kind) => Camera != null
            ? Camera.AimPoint(Mathf.Max(kind.Range, kind.Reach, 4), transform)
            : transform.position + Vector3.up * 1.3f + transform.forward * Mathf.Max(kind.Range, 4);

        /// <summary>Where the right hand lets a blade go, thrown toward a point (the hero turned toward it).</summary>
        public Vector3 HandFor(Vector3 target)
        {
            var flat = target - transform.position;
            flat.y = 0;
            flat = flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;
            var height = view != null ? 1.3f * view.Scale : 1.25f;

            return transform.position + Vector3.up * height + Vector3.Cross(Vector3.up, flat) * 0.25f + flat * 0.45f;
        }

        /// <summary>The cut: the hero turns toward the point aimed at and throws the blade at it from the right hand.</summary>
        void ThrowMoonblade(GuKind kind, Vector3 target)
        {
            var flat = target - transform.position;
            flat.y = 0;
            flat = flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;

            if (body != null)
            {
                body.Facing = Mathf.Atan2(flat.x, flat.z);
                transform.rotation = Quaternion.Euler(0, body.Facing * Mathf.Rad2Deg, 0);
            }

            var from = HandFor(target);
            var aim = target - from;
            // A point right at the hand (a wall touching it): straight ahead instead.
            aim = aim.sqrMagnitude > 0.25f ? aim.normalized : flat;
            // Stronger with every rank; each cut tilted the other way, as the arm swings.
            var power = kind.Power * (1 + 0.6f * (Aperture.Rank - 1));
            throws++;
            Moonblade.Throw(from, aim, kind, solid, power, throws % 2 == 0 ? 14 : -14);
            view?.Throw();
        }

        // ── Drawing in a stone ────────────────────────────────────────

        float absorbing = -1;

        /// <summary>Drawing in a stone now.</summary>
        public bool Absorbing => absorbing >= 0;

        /// <summary>How far through drawing in a stone, 0…1 (below 0: not drawing one).</summary>
        public float AbsorbProgress => absorbing < 0 ? -1 : Mathf.Clamp01(absorbing / AbsorbTime);

        /// <summary>Begins drawing in a stone (the stone key): three seconds, walking slowly at most.</summary>
        public bool StartAbsorbing()
        {
            if (Absorbing)
            {
                return false;
            }

            if (Stones <= 0)
            {
                Said?.Invoke("Нет первобытных камней");

                return false;
            }

            if (Aperture.Essence >= Aperture.Cap - 0.05f)
            {
                Said?.Invoke("Море эссенции и так полно");

                return false;
            }

            absorbing = 0;

            return true;
        }

        /// <summary>Stops drawing in a stone (the stone is kept); `why` is told, if given.</summary>
        public void CancelAbsorbing(string why)
        {
            if (absorbing < 0)
            {
                return;
            }

            absorbing = -1;

            if (why != null)
            {
                Said?.Invoke(why);
            }
        }

        /// <summary>A moment of drawing in a stone; at the end the stone's essence is in the sea.</summary>
        public void UpdateAbsorbing(float dt)
        {
            if (absorbing < 0)
            {
                return;
            }

            absorbing += dt;

            if (absorbing >= AbsorbTime)
            {
                absorbing = -1;
                AbsorbStone();
            }
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

        /// <summary>Sets a Gu on a key (taking it off any other).</summary>
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
