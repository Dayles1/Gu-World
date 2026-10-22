using System;
using System.Collections.Generic;
using GuWorld.Combat;
using GuWorld.Creatures;
using GuWorld.Items;
using GuWorld.Physics;
using GuWorld.Render;
using GuWorld.Systems;
using UnityEngine;

namespace GuWorld.Cultivation
{
    /// <summary>A Gu refined into the aperture: which kind, where in the ring, when it last ate, how tired it is.</summary>
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
        /// <summary>How tired it is (0…100, past 100: driven by force; saved), and when it was last used (Time.time).</summary>
        public float tired;
        [NonSerialized] public float usedAt = -100;
        /// <summary>Whether the player was told it is spent.</summary>
        [NonSerialized] public bool toldTired;

        public GuKind Kind => GuBook.Find(kind);
    }

    /// <summary>What a Gu Master carries, as saved: the aperture, the Gu in it, the bag, the action bar.</summary>
    [Serializable]
    public class CultivationSave
    {
        /// <summary>Whether this holds a Gu Master at all (a save from before cultivation does not).</summary>
        public bool set;
        public string grade = "C";
        /// <summary>The talent, % of the aperture (0 in a save from before it was counted so: the grade's).</summary>
        public float talent;
        /// <summary>The way the game was begun (Talents.Paths; -1: before there was a choice).</summary>
        public int path = -1;
        public int rank = 1;
        public string stage = "initial";
        public float essence;
        /// <summary>How far toward the next small realm (meditation); the tempering at the peak; the failed breakthroughs.</summary>
        public float progress;
        public float readiness;
        public int failures;
        public OwnedGu[] owned;
        public ItemStack[] bag;
        /// <summary>The action bar: "gu:&lt;kind&gt;" or "item:&lt;id&gt;" per key, empty where nothing is set.</summary>
        public string[] bar = new string[0];
        /// <summary>Whether the jade skin was up (older saves), and the held Gu that were up.</summary>
        public bool skin;
        public string[] held = new string[0];
        /// <summary>Strength and toughness gained for good (boars, iron bones), and the Gu that gave them (each gives once).</summary>
        public float strength;
        public float toughness;
        public string[] once = new string[0];

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
    /// falls on its master too.
    ///
    /// Using a Gu drives it with essence — its price scaled by the ranks and small
    /// realms between it and its master (Aperture.CostOf) — and tires it. There is
    /// no pause shared between Gu: each Gu has only its own short rest. A tired Gu
    /// (past 100) still answers, but each use forces it, and a forced Gu may die
    /// of it (Strain) — the vital one taking its master's health with it. Rested,
    /// it recovers by itself in seconds. Held Gu (skins, stealth, a healing grass)
    /// take essence and tire every second they are up.
    /// </summary>
    public class Cultivator : MonoBehaviour
    {
        public const int Ring = 8;
        /// <summary>The Gu's keys, 1–4.</summary>
        public const int BarSize = 4;
        /// <summary>Seconds it takes to draw in a primeval stone.</summary>
        public const float AbsorbTime = 3;
        /// <summary>How fast a rested Gu recovers (of 100 a second), and after how long unused it starts to.</summary>
        public const float RestRate = 12;
        public const float RestAfter = 1.5f;

        public Aperture Aperture { get; private set; } = Aperture.Full(Grade.C);
        public readonly Bag Bag = new Bag();
        public readonly List<OwnedGu> Gu = new List<OwnedGu>();
        public readonly string[] Bar = new string[BarSize];
        /// <summary>The game minute the Gu Master last lived up to (Tick).</summary>
        public double Now { get; private set; }
        public int Stones => Bag.Count(ItemBook.Stone);
        /// <summary>The way this game was begun (Talents.Paths; -1: unknown).</summary>
        public int Path { get; private set; } = -1;
        /// <summary>Strength and toughness gained for good, as shares more of the body's own.</summary>
        public float Strength { get; private set; }
        public float Toughening { get; private set; }

        /// <summary>Something to tell the player (not enough essence, a Gu hungry…).</summary>
        public event Action<string> Said;
        /// <summary>Something done that tasks may count ("feed:moonlight", "refine:jade_skin", "moonblade").</summary>
        public event Action<string, int> Did;

        readonly Dictionary<string, float> readyAt = new Dictionary<string, float>();
        readonly HashSet<string> held = new HashSet<string>();
        readonly HashSet<string> once = new HashSet<string>();
        readonly Dictionary<SkillKind, (float until, float amount)> buffs = new Dictionary<SkillKind, (float, float)>();
        readonly System.Random random = new System.Random();
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

        public int Open => AllCells ? Ring : OpenSlots(Aperture.Rank);

        // The proving ground's modes (all off in the game itself).

        /// <summary>How much a use tires a Gu (1: as written; a master's trained spirit tires them less).</summary>
        public float TireScale = 1;

        /// <summary>The sea never runs dry: every Gu can be paid for, whatever its rank.</summary>
        public bool EndlessEssence;
        /// <summary>Gu never tire (and so never die of being forced).</summary>
        public bool Untiring;
        /// <summary>Gu are never hungry.</summary>
        public bool Sated;
        /// <summary>All eight cells of the ring open, whatever the rank.</summary>
        public bool AllCells;

        /// <summary>Essence paid for something (free with the endless sea); false when there is not enough.</summary>
        public bool Pay(float cost) => EndlessEssence || Aperture.Spend(cost);

        /// <summary>The Gu in a cell of the ring (null: none).</summary>
        public OwnedGu At(int slot) => Gu.Find(g => g.slot == slot);

        /// <summary>The first Gu of a kind in the ring (null: none).</summary>
        public OwnedGu First(string kind) => Gu.Find(g => g.kind == kind);

        public Hunger HungerOf(OwnedGu gu) => gu.Kind?.HungerAfter(Now - gu.fedAt) ?? Hunger.Dead;

        /// <summary>Game minutes until a Gu's next meal is due (below 0: overdue; infinite: it does not eat).</summary>
        public double MealIn(OwnedGu gu) => gu.Kind?.Food == null ? double.PositiveInfinity : gu.fedAt + gu.Kind.MealEvery - Now;

        /// <summary>What one use of a Gu (for a held one: raising it) costs this master now, % of the aperture.</summary>
        public float CostOf(GuKind kind) => kind == null ? 0 : Aperture.CostOf(kind.Cost, kind.Rank);

        /// <summary>What a held Gu takes each second, and what refining one takes, for this master now.</summary>
        public float UpkeepOf(GuKind kind) => kind == null ? 0 : Aperture.CostOf(kind.Upkeep, kind.Rank);

        public float RefineCostOf(GuKind kind) => kind == null ? 0 : Aperture.CostOf(kind.RefineCost, kind.Rank);

        // ── Saving ────────────────────────────────────────────────────

        /// <summary>A Gu Master from a save — or, with none, the test one — at a game minute.</summary>
        public void Load(CultivationSave saved, double now)
        {
            Now = now;
            Gu.Clear();
            Bag.Clear();
            Array.Clear(Bar, 0, BarSize);
            readyAt.Clear();
            DropAllHeld();
            buffs.Clear();
            once.Clear();
            Strength = 0;
            Toughening = 0;
            Path = -1;

            if (saved == null || !saved.set)
            {
                Aperture = Aperture.Full(Grade.C);
                Gu.Add(new OwnedGu { kind = GuBook.Moonlight.Id, slot = 0, fedAt = now, vital = true });
                Bag.Add(ItemBook.Stone, 10, now);
                Bag.Add(ItemBook.Petal, 8, now);
                Bar[0] = "gu:" + GuBook.Moonlight.Id;

                return;
            }

            var grade = Enum.TryParse<Grade>(saved.grade, true, out var g) ? g : Grade.C;
            Aperture = new Aperture
            {
                Talent = saved.talent > 0 ? saved.talent : Aperture.TalentOf(grade),
                Rank = saved.rank,
                Stage = Enum.TryParse<Stage>(saved.stage, true, out var stage) ? stage : Stage.Initial,
                Essence = saved.essence,
                Progress = saved.progress,
                Readiness = saved.readiness,
                Failures = saved.failures,
            };
            Aperture.Repair();
            Path = saved.path;
            Strength = float.IsFinite(saved.strength) ? Mathf.Max(0, saved.strength) : 0;
            Toughening = float.IsFinite(saved.toughness) ? Mathf.Max(0, saved.toughness) : 0;

            foreach (var id in saved.once ?? new string[0])
            {
                once.Add(id);
            }

            Bag.Load(saved.bag);

            if (saved.owned != null)
            {
                foreach (var gu in saved.owned)
                {
                    if (gu == null || GuBook.Find(gu.kind) == null || gu.slot < 0 || At(gu.slot) != null)
                    {
                        continue;
                    }

                    Gu.Add(new OwnedGu
                    {
                        kind = gu.kind,
                        slot = gu.slot,
                        fedAt = double.IsFinite(gu.fedAt) && gu.fedAt <= now ? gu.fedAt : now,
                        vital = gu.vital && !Gu.Exists(o => o.vital),
                        tired = float.IsFinite(gu.tired) ? Mathf.Clamp(gu.tired, 0, 300) : 0,
                    });
                }

                FitRing();
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

            // The held Gu that were up stay up (a save from before: the jade skin).
            var wasHeld = new List<string>(saved.held ?? new string[0]);

            if (saved.skin)
            {
                wasHeld.Add(GuBook.JadeSkin.Id);
            }

            foreach (var id in wasHeld)
            {
                var gu = First(id);

                if (gu != null && gu.Kind.Held && HungerOf(gu) < Hunger.Starving)
                {
                    Raise(gu.Kind);
                }
            }
        }

        /// <summary>The test Gu Master given the talent of the way the game is begun (a new game).</summary>
        public void Begin(int path, float talent)
        {
            Path = path;
            Aperture = Aperture.Full(talent);
            Said?.Invoke(Aperture.Extreme
                ? "Вы родились с Телосложением Десяти Крайностей: апертура полна до краёв, эссенция почти не кончается"
                : $"Талант: {Aperture.TalentName}");
        }

        /// <summary>
        /// The Gu whose cells are no longer open (the rank fell: the Do-or-Die Gu, an old save) moved into free open cells —
        /// or, with none free, taken out of the aperture back into the bag (never lost); the vital Gu keeps a cell first.
        /// </summary>
        void FitRing()
        {
            var outside = Gu.FindAll(g => g.slot >= Open);
            outside.Sort((a, b) => b.vital.CompareTo(a.vital));

            foreach (var gu in outside)
            {
                var free = -1;

                for (var i = 0; i < Open && free < 0; i++)
                {
                    if (At(i) == null)
                    {
                        free = i;
                    }
                }

                if (free >= 0)
                {
                    gu.slot = free;

                    continue;
                }

                var kind = gu.Kind;
                Release(gu);

                if (kind != null && Bag.Add(kind.Item, 1, Now) > 0)
                {
                    Said?.Invoke($"{kind.Name}: для него нет открытой ячейки — он вернулся в сумку");
                }
                else if (kind != null)
                {
                    Said?.Invoke($"{kind.Name}: нет ни ячейки, ни места в сумке — он улетел");
                }
            }
        }

        /// <summary>Whether a key's entry names a Gu that exists (only Gu go on the keys).</summary>
        static bool Valid(string entry) => entry != null && entry.StartsWith("gu:") && GuBook.Find(entry.Substring(3)) != null;

        public CultivationSave Save()
        {
            var owned = new OwnedGu[Gu.Count];

            for (var i = 0; i < Gu.Count; i++)
            {
                owned[i] = new OwnedGu { kind = Gu[i].kind, slot = Gu[i].slot, fedAt = Gu[i].fedAt, vital = Gu[i].vital, tired = Gu[i].tired };
            }

            return new CultivationSave
            {
                set = true,
                grade = Aperture.Grade.ToString(),
                talent = Aperture.Talent,
                path = Path,
                rank = Aperture.Rank,
                stage = Aperture.Stage.ToString().ToLowerInvariant(),
                essence = Aperture.Essence,
                progress = Aperture.Progress,
                readiness = Aperture.Readiness,
                failures = Aperture.Failures,
                owned = owned,
                bag = Bag.Save(),
                bar = (string[])Bar.Clone(),
                held = new List<string>(held).ToArray(),
                strength = Strength,
                toughness = Toughening,
                once = new List<string>(once).ToArray(),
            };
        }

        // ── Time ──────────────────────────────────────────────────────

        /// <summary>
        /// Lives up to a game minute: the sea refills, the Gu grow hungry — and are told
        /// about as they do — and what spoils in the bag spoils.
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
            Passives();

            if (minutes > 0)
            {
                Aperture.Recover(minutes);
            }

            if (Sated)
            {
                foreach (var gu in Gu)
                {
                    gu.fedAt = now;
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

        /// <summary>The Gu that work by themselves, set on the aperture and the world (only fed ones work).</summary>
        void Passives()
        {
            var refined = false;
            var scent = 1f;

            foreach (var gu in Gu)
            {
                var kind = gu.Kind;

                if (kind == null || HungerOf(gu) >= Hunger.Starving)
                {
                    continue;
                }

                if (kind.Skill == SkillKind.Refine && kind.Rank == Aperture.Rank)
                {
                    refined = true;
                }

                if (kind.Skill == SkillKind.Scent)
                {
                    scent = Mathf.Min(scent, kind.Amount);
                }
            }

            Aperture.Refined = refined;
            Scent = scent;
        }

        /// <summary>A passive Gu of a kind of work, fed, in the ring (null: none).</summary>
        GuKind Working(SkillKind skill)
        {
            foreach (var gu in Gu)
            {
                if (gu.Kind?.Skill == skill && HungerOf(gu) < Hunger.Starving)
                {
                    return gu.Kind;
                }
            }

            return null;
        }

        /// <summary>How far beasts notice the hero by scent (a hidden breath: less), as a share.</summary>
        public float Scent { get; private set; } = 1;

        /// <summary>
        /// A moment of the world (real seconds, while it runs): held Gu take their essence and
        /// tire, rested Gu recover, a rush goes on, the healing grass heals.
        /// </summary>
        public void Step(float dt)
        {
            if (dt <= 0)
            {
                return;
            }

            var hide = 1f;

            foreach (var id in new List<string>(held))
            {
                var gu = First(id);
                var kind = gu?.Kind;

                if (kind == null)
                {
                    Lower(id, null);

                    continue;
                }

                if (!Pay(UpkeepOf(kind) * dt))
                {
                    Aperture.Essence = 0;
                    Lower(id, $"{kind.SkillName} спадает: кончилась эссенция");

                    continue;
                }

                gu.usedAt = Time.time;
                gu.tired = Untiring ? 0 : gu.tired + kind.Tire / 10 * dt * TireScale;
                WarnTired(gu);

                // Held past its strength, it is forced every moment it stays up.
                if (gu.tired >= 100 && random.NextDouble() < dt * StrainChance(gu.tired) * 0.25f)
                {
                    Overworked(gu);

                    continue;
                }

                switch (kind.Skill)
                {
                    case SkillKind.Mend:
                        health?.Heal(kind.Amount * dt);
                        break;
                    case SkillKind.Hide:
                        hide = Mathf.Min(hide, kind.Amount);
                        break;
                }
            }

            foreach (var gu in Gu)
            {
                if (!held.Contains(gu.kind) && Time.time - gu.usedAt > RestAfter && gu.tired > 0)
                {
                    gu.tired = Mathf.Max(0, gu.tired - RestRate * dt);

                    if (gu.tired < 100)
                    {
                        gu.toldTired = false;
                    }
                }
            }

            Meditate(dt);

            if (EndlessEssence)
            {
                Aperture.Essence = Aperture.Cap;
            }

            Beast.Notice = Warded ? 0 : Mathf.Min(hide, Scent);
            Rush(dt);

            if (wardGlow != null && !Warded)
            {
                wardGlow.Release();
                wardGlow = null;
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
                    Lower(gu.kind, null);
                    break;
                case Hunger.Dead:
                    Die(gu, $"{kind.Name} умер от голода");
                    break;
            }
        }

        /// <summary>A Gu dead (of hunger, or of being forced): gone from the ring and the bar; the vital Gu's death strikes its master.</summary>
        void Die(OwnedGu gu, string why)
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

                Lower(gu.kind, null);
            }

            if (gu.vital)
            {
                Aperture.Essence = 0;
                health?.Strike(health.Max * 0.5f);
                Said?.Invoke($"{why}. Это был жизненный Гу — удар отозвался в апертуре");
            }
            else
            {
                Said?.Invoke(why);
            }
        }

        // ── Tiredness ─────────────────────────────────────────────────

        /// <summary>The chance a forced use kills a Gu this tired: one in eight just past 100, rising with every use after.</summary>
        public static float StrainChance(float tired) => tired < 100 ? 0 : Mathf.Clamp(0.12f + (tired - 100) / 250, 0.12f, 0.8f);

        /// <summary>
        /// A tired Gu driven anyway: it may die of it. True when it held (and does what it was
        /// asked); false when it died.
        /// </summary>
        bool Strain(OwnedGu gu)
        {
            if (Untiring || gu.tired < 100 || random.NextDouble() >= StrainChance(gu.tired))
            {
                return true;
            }

            Overworked(gu);

            return false;
        }

        void Overworked(OwnedGu gu) => Die(gu, $"{gu.Kind.Name} не выдержал — его гнали уставшим, и он погиб");

        void WarnTired(OwnedGu gu)
        {
            if (gu.tired >= 100 && !gu.toldTired)
            {
                gu.toldTired = true;
                Said?.Invoke($"{gu.Kind.Name} устал. Дальше — только через силу: он может погибнуть");
            }
        }

        // ── Using ─────────────────────────────────────────────────────

        /// <summary>Seconds before a kind of Gu can be used again (0: ready).</summary>
        public float CooldownLeft(string kind) => kind != null && readyAt.TryGetValue(kind, out var at) ? Mathf.Max(0, at - Time.time) : 0;

        /// <summary>The Gu on a key (null: none set).</summary>
        public GuKind KindOn(int key) => key >= 0 && key < BarSize && Bar[key] != null ? GuBook.Find(Bar[key].Substring(3)) : null;

        /// <summary>Whether a held Gu of a kind is up.</summary>
        public bool IsHeld(string kind) => kind != null && held.Contains(kind);

        /// <summary>The held Gu that are up.</summary>
        public IEnumerable<string> Held => held;

        /// <summary>Whether any skin (jade or other) is up.</summary>
        public bool SkinOn
        {
            get
            {
                foreach (var id in held)
                {
                    if (GuBook.Find(id)?.Skill is SkillKind.JadeSkin or SkillKind.Skin)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Whether a Gu on a key could be used now (for the aim's colour): rested and the essence there.</summary>
        public bool Usable(GuKind kind) => kind != null && CooldownLeft(kind.Id) <= 0 && (IsHeld(kind.Id) || EndlessEssence || Aperture.CanSpend(CostOf(kind)));

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

            if (kind.Passive)
            {
                Said?.Invoke($"{kind.Name} работает сам, пока сыт и в апертуре");

                return false;
            }

            if (CooldownLeft(kindId) > 0 || (body != null && !body.Free && kind.Skill != SkillKind.Ward))
            {
                return false;
            }

            // A held one already up is put down: no essence, no strain.
            if (kind.Held && IsHeld(kindId))
            {
                Lower(kindId, $"{kind.SkillName}: снято");
                readyAt[kindId] = Time.time + kind.Cooldown;

                return true;
            }

            var cost = CostOf(kind);

            if (!Pay(cost))
            {
                Said?.Invoke($"Не хватает первобытной эссенции ({cost:0.#}% апертуры)" + (kind.Rank > Aperture.Rank ? $" — Гу ранга {kind.Rank} дорог для ранга {Aperture.Rank}" : ""));

                return false;
            }

            CancelAbsorbing(null);
            readyAt[kindId] = Time.time + kind.Cooldown;
            gu.usedAt = Time.time;

            // Tired, it is forced — and may die of it, the essence spent for nothing.
            if (!Strain(gu))
            {
                return false;
            }

            gu.tired = Untiring ? 0 : gu.tired + (kind.Held ? kind.Tire * 0.5f : kind.Tire) * TireScale;
            WarnTired(gu);
            Do(kind, target);
            Did?.Invoke(kind.Skill == SkillKind.Moonblade ? "moonblade" : "gu:" + kind.Id, 1);

            return true;
        }

        /// <summary>What a Gu does, used (its essence paid, its strain survived).</summary>
        void Do(GuKind kind, Vector3? target)
        {
            var power = kind.Power * GuPower;

            switch (kind.Skill)
            {
                case SkillKind.Moonblade:
                    ThrowMoonblade(kind, target ?? AimPoint(kind));
                    break;

                case SkillKind.Bolt:
                case SkillKind.Tame:
                {
                    // Thrown exactly as its aim showed it (PlanShot).
                    var shot = PlanShot(kind, target ?? AimPoint(kind));
                    Turn(shot.End);
                    GuBolt.Throw(shot.From, shot.Direction, kind, solid, power);
                    view?.Throw();
                    break;
                }

                case SkillKind.Burst:
                {
                    var at = BlastPoint(target ?? AimPoint(kind));
                    Turn(at);
                    GuSkills.Blast(at, kind.Radius, power * Battle.Scale(kind), kind, solid);
                    view?.Throw();
                    break;
                }

                case SkillKind.Cleave:
                {
                    var forward = Turn(target ?? AimPoint(kind));
                    GuSkills.Blast(transform.position + Vector3.up * 1.1f, kind.Reach, power * Battle.Scale(kind), kind, solid, null, forward);
                    view?.Throw();
                    break;
                }

                case SkillKind.Might:
                case SkillKind.Haste:
                case SkillKind.Leap:
                case SkillKind.Berserk:
                    buffs[kind.Skill] = (Time.time + kind.Duration, kind.Amount);
                    GuGlow.Flash(transform.position + Vector3.up, 1.2f, kind.Colour);
                    Said?.Invoke($"{kind.SkillName} — {kind.Duration:0} с");
                    break;

                case SkillKind.Flight:
                    buffs[SkillKind.Flight] = (Time.time + kind.Duration, 1);

                    if (body != null)
                    {
                        body.Velocity = new Vector3(body.Velocity.x, 0, body.Velocity.z) + Vector3.up * kind.Amount + transform.forward * 6;
                    }

                    GuGlow.Flash(transform.position + Vector3.up, 1.5f, kind.Colour);
                    break;

                case SkillKind.Dash:
                    // Where the crosshair looks (the hero turned that way first), as far as the aim showed.
                    rushLeft = kind.Amount;
                    rushWay = Turn(target ?? AimPoint(kind));
                    GuGlow.Flash(transform.position + Vector3.up, 1, kind.Colour, 0.3f);
                    break;

                case SkillKind.Blink:
                    Blink(kind, target ?? AimPoint(kind));
                    break;

                case SkillKind.Heal:
                    health?.Heal(kind.Amount);
                    GuGlow.Flash(transform.position + Vector3.up, 1.6f, kind.Colour, 0.8f);
                    Said?.Invoke($"{kind.SkillName}: +{kind.Amount:0} здоровья");
                    break;

                case SkillKind.JadeSkin:
                case SkillKind.Skin:
                case SkillKind.Mend:
                case SkillKind.Hide:
                case SkillKind.Sense:
                    Raise(kind);
                    Said?.Invoke($"{kind.SkillName}: поднято — эссенция уходит, пока держится");
                    break;

                case SkillKind.Ward:
                    buffs[SkillKind.Ward] = (Time.time + kind.Duration, 1);
                    wardGlow?.Release();
                    wardGlow = GuGlow.Hold(transform, 1.5f, kind.Colour);
                    Said?.Invoke($"{kind.SkillName}: ничто не пройдёт {kind.Duration:0} с — но и вы не сдвинетесь");
                    break;

                case SkillKind.Howl:
                case SkillKind.Flash:
                {
                    var n = 0;

                    foreach (var wolf in GuSkills.Beasts(transform.position, kind.Radius))
                    {
                        wolf.Afflict(kind.Skill == SkillKind.Howl ? Affliction.Fear : Affliction.Blind, kind.Duration, 1);
                        n++;
                    }

                    GuGlow.Flash(transform.position + Vector3.up, kind.Skill == SkillKind.Flash ? kind.Radius * 0.5f : 3, kind.Colour, 0.5f);
                    Said?.Invoke(n > 0 ? $"{kind.SkillName}: зверей задело — {n}" : $"{kind.SkillName}: зверей рядом нет");
                    break;
                }

                case SkillKind.Decoy:
                    Decoy(kind);
                    break;
            }
        }

        /// <summary>The hero turned toward a point; returns the flat way to it.</summary>
        Vector3 Turn(Vector3 target)
        {
            var flat = target - transform.position;
            flat.y = 0;
            flat = flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;

            if (body != null)
            {
                body.Facing = Mathf.Atan2(flat.x, flat.z);
                transform.rotation = Quaternion.Euler(0, body.Facing * Mathf.Rad2Deg, 0);
            }

            return flat;
        }

        /// <summary>The ground under a point (for a blast aimed at the air).</summary>
        static Vector3 Ground(Vector3 at) =>
            UnityEngine.Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out var hit, 60, ~0, QueryTriggerInteraction.Ignore) ? hit.point : at;

        /// <summary>Where a blast aimed at a point goes off: the ground under it.</summary>
        public Vector3 BlastPoint(Vector3 target) => Ground(target);

        /// <summary>A missile's flight, worked out once for its aim and its throw alike: where it leaves the hand, which way, where it stops.</summary>
        public struct Shot
        {
            public Vector3 From, Direction, End, Normal;
            public float Length;
            /// <summary>Whether it stops on something (a body, a wall, the ground) rather than fading at the end of its range.</summary>
            public bool Hits;
        }

        /// <summary>
        /// The flight of a missile (a moonblade, a bolt) thrown at a point: from the right hand toward it, on as far
        /// as its range, stopped by the first body or solid thing in its way — the same reckoning the missile makes.
        /// </summary>
        public Shot PlanShot(GuKind kind, Vector3 target)
        {
            var flat = target - transform.position;
            flat.y = 0;
            flat = flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;
            var from = HandFor(target);
            var aim = target - from;
            var direction = aim.sqrMagnitude > 0.25f ? aim.normalized : flat;
            var range = Mathf.Max(kind.Range, kind.Reach, 4) * (kind.Skill == SkillKind.Moonblade ? MoonScale : 1);
            var shot = new Shot { From = from, Direction = direction, Length = range, End = from + direction * range, Normal = -direction };

            foreach (var hit in UnityEngine.Physics.SphereCastAll(from, 0.2f, direction, range, ~0, QueryTriggerInteraction.Collide))
            {
                var c = hit.collider;

                if (c.transform.IsChildOf(transform) || hit.distance <= 0 || hit.distance >= shot.Length)
                {
                    continue;
                }

                // A body's parts stop it; its own capsule does not (a missile cuts the parts).
                var counts = c.isTrigger ? c.GetComponent<GuWorld.Combat.HitZone>() != null : c.GetComponentInParent<GuWorld.Combat.IZoned>() == null;

                if (counts)
                {
                    shot.Length = hit.distance;
                    shot.End = from + direction * hit.distance;
                    shot.Normal = hit.normal;
                    shot.Hits = true;
                }
            }

            return shot;
        }

        /// <summary>Where a blink aimed at a point puts the hero: within its range, half a metre short of a wall, on the ground there.</summary>
        public Vector3 BlinkPoint(GuKind kind, Vector3 target)
        {
            var way = target - transform.position;
            way.y = 0;

            if (way.magnitude > kind.Range)
            {
                target = transform.position + way.normalized * kind.Range + Vector3.up * (target.y - transform.position.y);
            }

            target -= way.normalized * 0.5f;
            target.y = Mathf.Max(target.y, transform.position.y - 30);

            return UnityEngine.Physics.Raycast(target + Vector3.up * 1.5f, Vector3.down, out var hit, 40, ~0, QueryTriggerInteraction.Ignore) ? hit.point : target;
        }

        /// <summary>Where a rush aimed at a point ends: as far as it goes, short of what stops the body.</summary>
        public Vector3 DashEnd(GuKind kind, Vector3 target)
        {
            var way = target - transform.position;
            way.y = 0;
            way = way.sqrMagnitude > 0.01f ? way.normalized : transform.forward;
            var length = kind.Amount;

            if (solid != null && UnityEngine.Physics.CapsuleCast(transform.position + Vector3.up * (solid.radius + 0.3f), transform.position + Vector3.up * (solid.height - solid.radius),
                    solid.radius * 0.9f, way, out var hit, length, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform))
            {
                length = Mathf.Max(0, hit.distance - 0.1f);
            }

            return transform.position + way * length;
        }

        /// <summary>The course a flight Gu throws the body on: the speed it leaves with.</summary>
        public Vector3 FlightStart(GuKind kind) => Vector3.up * kind.Amount + transform.forward * 6;

        void Blink(GuKind kind, Vector3 target)
        {
            var at = BlinkPoint(kind, target);
            GuGlow.Flash(transform.position + Vector3.up, 1, kind.Colour, 0.35f);
            body?.PlaceAt(at);
            GuGlow.Flash(transform.position + Vector3.up, 1, kind.Colour, 0.35f);
        }

        static Material straw;
        static Material wood;
        TrainingDummy decoy;

        void Decoy(GuKind kind)
        {
            if (decoy != null)
            {
                Destroy(decoy.gameObject);
            }

            straw ??= Plain(new Color(0.78f, 0.66f, 0.38f));
            wood ??= Plain(new Color(0.42f, 0.3f, 0.2f));
            var at = Ground(transform.position + transform.forward * 2.5f + Vector3.up);
            decoy = TrainingDummy.Make(at, transform.eulerAngles.y + 180, straw, wood);
            Destroy(decoy.gameObject, kind.Duration);
            Beast.Decoy = decoy.transform;
            Beast.DecoyUntil = Time.time + kind.Duration;
            Said?.Invoke($"{kind.SkillName}: звери кинутся на куклу — {kind.Duration:0} с");
        }

        static Material Plain(Color colour)
        {
            var shader = Shader.Find("GuWorld/Toon") ?? Shader.Find("Universal Render Pipeline/Lit");

            return new Material(shader) { color = colour };
        }

        // ── Held Gu ───────────────────────────────────────────────────

        readonly Dictionary<string, GuGlow> heldGlows = new Dictionary<string, GuGlow>();

        void Raise(GuKind kind)
        {
            if (!held.Add(kind.Id))
            {
                return;
            }

            // A skin or a ward shows: a faint glow of its colour round the body.
            if (kind.Skill is SkillKind.JadeSkin or SkillKind.Skin && isActiveAndEnabled)
            {
                heldGlows[kind.Id] = GuGlow.Hold(transform, 0.85f, kind.Colour * 0.6f);
            }
        }

        /// <summary>Puts a held Gu down; `why` is told, if given.</summary>
        void Lower(string kind, string why)
        {
            if (!held.Remove(kind))
            {
                return;
            }

            if (heldGlows.TryGetValue(kind, out var glow))
            {
                if (glow != null) glow.Release();
                heldGlows.Remove(kind);
            }

            if (why != null)
            {
                Said?.Invoke(why);
            }
        }

        void DropAllHeld()
        {
            foreach (var id in new List<string>(held))
            {
                Lower(id, null);
            }
        }

        /// <summary>The beasts a held sense hears: how many within its reach and how far the nearest is (none: -1).</summary>
        public (int count, float nearest) Sensed()
        {
            var reach = 0f;

            foreach (var id in held)
            {
                var kind = GuBook.Find(id);

                if (kind?.Skill == SkillKind.Sense)
                {
                    reach = Mathf.Max(reach, kind.Range);
                }
            }

            if (reach <= 0)
            {
                return (0, -1);
            }

            var count = 0;
            var nearest = float.PositiveInfinity;

            foreach (var wolf in GuSkills.Beasts(transform.position, reach))
            {
                count++;
                nearest = Mathf.Min(nearest, Vector3.Distance(wolf.transform.position, transform.position));
            }

            return (count, count > 0 ? nearest : float.PositiveInfinity);
        }

        // ── What Gu do to the body ────────────────────────────────────

        float Buff(SkillKind kind) => buffs.TryGetValue(kind, out var b) && Time.time < b.until ? b.amount : 0;

        /// <summary>Seconds a timed effect has left (0: none).</summary>
        public float BuffLeft(SkillKind kind) => buffs.TryGetValue(kind, out var b) ? Mathf.Max(0, b.until - Time.time) : 0;

        /// <summary>How much harder Gu strike now (a berserk Gu: twice).</summary>
        public float GuPower => Mathf.Max(1, Buff(SkillKind.Berserk));

        /// <summary>How strong the fists are, against a youth's: strength gained for good, a strength Gu's, a berserk Gu's.</summary>
        public float FistPower => (1 + Strength) * Mathf.Max(1, Buff(SkillKind.Might)) * GuPower;

        /// <summary>How much faster the hero runs, and jumps higher, now.</summary>
        public float SpeedBoost => Mathf.Max(1, Buff(SkillKind.Haste));

        public float JumpBoost => Mathf.Max(1, Buff(SkillKind.Leap));

        /// <summary>Whether a fall does no harm now (wings).</summary>
        public bool SoftFall => Buff(SkillKind.Leap) > 0 || Buff(SkillKind.Flight) > 0;

        /// <summary>Whether a ward is up: no harm gets in, and the hero cannot move.</summary>
        public bool Warded => Buff(SkillKind.Ward) > 0;

        /// <summary>The body's toughness gained for good (iron bones, ice muscles), as a share more.</summary>
        public float ToughnessBoost => 1 + Toughening;

        GuGlow wardGlow;
        float rushLeft;
        Vector3 rushWay;

        /// <summary>A rush going on: the body carried forward fast, stopped by what it meets.</summary>
        void Rush(float dt)
        {
            if (rushLeft <= 0 || solid == null || !solid.enabled)
            {
                rushLeft = 0;

                return;
            }

            var step = Mathf.Min(rushLeft, 40 * dt);
            var before = transform.position;
            solid.Move(rushWay * step);
            var moved = (transform.position - before).magnitude;
            rushLeft = moved < step * 0.3f ? 0 : rushLeft - step;
        }

        /// <summary>
        /// Harm meeting the hero's Gu: a ward stops it all; the best skin up takes its share;
        /// a jade skin stops what is left, each point paid for in essence (lore: harder blows
        /// spend more), and gives way when the sea runs dry. Returns the harm that gets through.
        /// </summary>
        public float Shield(float harm)
        {
            if (harm <= 0)
            {
                return harm;
            }

            if (Warded)
            {
                return 0;
            }

            var best = 0f;
            GuKind jade = null;

            foreach (var id in held)
            {
                var kind = GuBook.Find(id);

                if (kind?.Skill == SkillKind.Skin)
                {
                    best = Mathf.Max(best, kind.Amount);
                }
                else if (kind?.Skill == SkillKind.JadeSkin && (jade == null || kind.PerHarm < jade.PerHarm))
                {
                    jade = kind;
                }
            }

            harm *= 1 - best;

            if (jade == null)
            {
                return harm;
            }

            var perHarm = Aperture.CostOf(jade.PerHarm, jade.Rank);

            if (Pay(harm * perHarm))
            {
                return 0;
            }

            var stopped = Aperture.Essence / perHarm;
            Aperture.Essence = 0;
            Lower(jade.Id, $"{jade.SkillName} треснула: эссенция кончилась");

            return Mathf.Max(0, harm - stopped);
        }

        // ── The bag ───────────────────────────────────────────────────

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

        /// <summary>The main action on one cell of the bag: refine a Gu, use a spent-once Gu, draw in a stone, eat, feed the Gu that eat it.</summary>
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

                case ItemUse.Activate:
                    return Activate(cell);

                case ItemUse.Absorb:
                    // Three seconds in the world, as with the stone key (a page pauses the world: it is begun, not done).
                    return StartAbsorbing();

                case ItemUse.Eat:
                    if (health != null && health.Current >= health.Max - 0.5f)
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

            if (kind == null || kind.OneUse)
            {
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

            var cost = RefineCostOf(kind);

            if (cost > Aperture.Cap && !EndlessEssence)
            {
                Said?.Invoke($"{kind.Name}: ранг {kind.Rank} не очистить мастеру ранга {Aperture.Rank} — нужно {cost:0}% эссенции, а море держит {Aperture.Cap:0}%");

                return false;
            }

            if (!Pay(cost))
            {
                Said?.Invoke($"Чтобы очистить {kind.Name}, нужно {cost:0}% эссенции разом — поглотите камень или подождите");

                return false;
            }

            Bag.TakeFrom(cell, 1);
            var gu = new OwnedGu { kind = kind.Id, slot = slot, fedAt = Now, vital = !Gu.Exists(g => g.vital) };
            Gu.Add(gu);
            Said?.Invoke(gu.vital ? $"{kind.Name} очищен — это ваш жизненный Гу" : $"{kind.Name} очищен и вошёл в апертуру");

            // A new skill goes onto the bar, if it is not there and there is room (a passive one needs no key).
            if (!kind.Passive && Array.IndexOf(Bar, "gu:" + kind.Id) < 0)
            {
                var free = Array.IndexOf(Bar, null);

                if (free >= 0)
                {
                    Bar[free] = "gu:" + kind.Id;
                }
            }

            Passives();
            Did?.Invoke("refine:" + kind.Id, 1);

            return true;
        }

        /// <summary>
        /// A Gu used once and spent, straight from the bag: a relic (one small realm up, at its
        /// own rank), a boar's or a bone's strength for good (each kind gives it once), the
        /// sea filled for a small realm lost.
        /// </summary>
        public bool Activate(int cell) => ActivateKind(GuBook.Find(Bag[cell]?.Kind?.Gu), () => Bag.TakeFrom(cell, 1));

        /// <summary>A Gu used once, of a kind: `spend` takes it away (none: it is not spent — the proving ground's arsenal).</summary>
        public bool ActivateKind(GuKind kind, Action spend = null)
        {
            if (kind == null || !kind.OneUse)
            {
                return false;
            }

            if (kind.Rank > Aperture.Rank + 1 && !EndlessEssence)
            {
                Said?.Invoke($"{kind.Name} ранга {kind.Rank} не подчинится мастеру ранга {Aperture.Rank}");

                return false;
            }

            switch (kind.Skill)
            {
                case SkillKind.Bones when health != null && health.Current <= health.Max * 0.15f + 1:
                    Said?.Invoke($"{kind.Name} перестраивает кости — это больно. Сначала подлечитесь (нужно больше {health.Max * 0.15f + 1:0} здоровья)");

                    return false;
                case SkillKind.Relic when kind.Rank != Aperture.Rank:
                    Said?.Invoke($"{kind.Name} поднимает только мастеров ранга {kind.Rank}");

                    return false;
                case SkillKind.Relic when Aperture.Stage == Stage.Peak:
                    Said?.Invoke("Вы на пике ранга: выше ведёт только прорыв (апертура, K)");

                    return false;
                case SkillKind.Strength or SkillKind.Bones when once.Contains(kind.Id):
                    Said?.Invoke($"Сила {kind.Name} уже в вас — второй такой Гу ничего не прибавит");

                    return false;
                case SkillKind.Desperate when Aperture.Rank == 1 && Aperture.Stage == Stage.Initial:
                    Said?.Invoke("Ниже начальной стадии первого ранга падать некуда");

                    return false;
            }

            var cost = CostOf(kind);

            if (!Pay(cost))
            {
                Said?.Invoke($"{kind.Name}: нужно {cost:0}% эссенции разом" + (cost > Aperture.Cap ? $" — ранг {kind.Rank} не по силам рангу {Aperture.Rank}" : ""));

                return false;
            }

            spend?.Invoke();

            switch (kind.Skill)
            {
                case SkillKind.Relic:
                    Aperture.Advance();
                    Said?.Invoke($"{kind.Name} растворился: вы на {Aperture.Name(Aperture.Stage)} ранга {Aperture.Rank}");
                    break;
                case SkillKind.Strength:
                    once.Add(kind.Id);
                    Strength += kind.Amount;
                    Said?.Invoke($"{kind.SkillName}: сила навсегда +{kind.Amount * 100:0}% (теперь ×{1 + Strength:0.##})");
                    break;
                case SkillKind.Bones:
                    once.Add(kind.Id);
                    Toughening += kind.Amount;
                    Said?.Invoke($"{kind.SkillName}: тело навсегда крепче на {kind.Amount * 100:0}% — это было больно");
                    health?.Strike(health.Max * 0.15f);
                    break;
                case SkillKind.Desperate:
                    if (Aperture.Stage == Stage.Initial)
                    {
                        Aperture.Rank--;
                        Aperture.Stage = Stage.Peak;
                    }
                    else
                    {
                        Aperture.Stage--;
                    }

                    Aperture.Essence = Aperture.Cap;
                    Said?.Invoke($"{kind.SkillName}: море полно — но вы упали до {Aperture.Name(Aperture.Stage)} ранга {Aperture.Rank}");
                    // A lower rank opens fewer cells: the Gu in the closed ones find room, or go back into the bag.
                    FitRing();
                    break;
            }

            GuGlow.Flash(transform.position + Vector3.up, 1.4f, kind.Colour, 0.8f);
            Did?.Invoke("activate:" + kind.Id, 1);

            return true;
        }

        // ── Fusion ────────────────────────────────────────────────────

        /// <summary>What a fusion lacks (Gu, materials), in words; null: nothing.</summary>
        public string Lacks(Recipe recipe)
        {
            var missing = new List<string>();

            foreach (var pair in Counts(recipe.Gu))
            {
                var kind = GuBook.Find(pair.Key);
                var have = Bag.Count(kind.Item) + Gu.FindAll(g => g.kind == pair.Key && !g.vital).Count;

                if (have < pair.Value)
                {
                    missing.Add($"{kind.Name} ×{pair.Value - have}");
                }
            }

            foreach (var (item, count) in recipe.Materials)
            {
                if (Bag.Count(item) < count)
                {
                    missing.Add($"{ItemBook.Find(item)?.Name.ToLowerInvariant()} ×{count - Bag.Count(item)}");
                }
            }

            return missing.Count > 0 ? string.Join(", ", missing) : null;
        }

        static Dictionary<string, int> Counts(string[] ids)
        {
            var counts = new Dictionary<string, int>();

            foreach (var id in ids)
            {
                counts.TryGetValue(id, out var n);
                counts[id] = n + 1;
            }

            return counts;
        }

        /// <summary>What a fusion costs this master, % of the aperture, and its chance.</summary>
        public float FuseCost(Recipe recipe) => Aperture.CostOf(recipe.Cost, GuBook.Find(recipe.Result)?.Rank ?? 1);

        public float FuseChance(Recipe recipe) => Working(SkillKind.Surety) != null ? 1 : recipe.Chance;

        /// <summary>
        /// Refines Gu and materials together into a new Gu (lore: sys_refinement): the Gu put in
        /// come from the bag first, then from the ring (never the vital one); the essence is
        /// paid at once. It may fail — and then all that was put in is lost.
        /// </summary>
        public bool Fuse(Recipe recipe)
        {
            var result = GuBook.Find(recipe.Result);

            if (result == null)
            {
                return false;
            }

            var lacks = Lacks(recipe);

            if (lacks != null)
            {
                Said?.Invoke($"Для слияния не хватает: {lacks}");

                return false;
            }

            var cost = FuseCost(recipe);

            if (!Pay(cost))
            {
                Said?.Invoke($"Слияние требует {cost:0}% эссенции разом" + (cost > Aperture.Cap ? " — больше, чем держит ваше море" : ""));

                return false;
            }

            foreach (var pair in Counts(recipe.Gu))
            {
                var kind = GuBook.Find(pair.Key);
                var left = pair.Value;
                var fromBag = Mathf.Min(left, Bag.Count(kind.Item));
                Bag.Take(kind.Item, fromBag);
                left -= fromBag;

                while (left-- > 0)
                {
                    var owned = Gu.Find(g => g.kind == pair.Key && !g.vital);
                    Gu.Remove(owned);

                    if (First(pair.Key) == null)
                    {
                        Lower(pair.Key, null);

                        for (var i = 0; i < BarSize; i++)
                        {
                            if (Bar[i] == "gu:" + pair.Key) Bar[i] = null;
                        }
                    }
                }
            }

            foreach (var (item, count) in recipe.Materials)
            {
                Bag.Take(item, count);
            }

            if (random.NextDouble() >= FuseChance(recipe))
            {
                Said?.Invoke($"Слияние сорвалось: Гу и материалы погибли");
                Did?.Invoke("fuse-failed:" + result.Id, 1);

                return false;
            }

            if (Bag.Add(result.Item, 1, Now) < 1)
            {
                Said?.Invoke($"{result.Name} родился — но сумка полна, и он упорхнул");

                return false;
            }

            Said?.Invoke($"Слияние удалось: {result.Name} (ранг {result.Rank}) — в сумке, очистите его в апертуру");
            Did?.Invoke("fuse:" + result.Id, 1);

            return true;
        }

        // ── The proving ground's arsenal ──────────────────────────────

        /// <summary>
        /// A Gu put straight into the ring — fed, free, no refining — and onto a key, if one is
        /// given (a Gu already in the ring only goes on the key). A full ring makes room: the Gu
        /// on that key gives way, or the last one that is not vital.
        /// </summary>
        public OwnedGu Grant(GuKind kind, int key = -1, bool quiet = false)
        {
            if (kind == null || kind.OneUse)
            {
                return null;
            }

            var gu = First(kind.Id);

            if (gu == null)
            {
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
                    var old = key >= 0 && key < BarSize && Bar[key] != null ? First(Bar[key].Substring(3)) : null;

                    if (old == null || old.vital)
                    {
                        old = Gu.FindLast(g => !g.vital);
                    }

                    if (old == null)
                    {
                        Said?.Invoke("Кольцо апертуры полно");

                        return null;
                    }

                    slot = old.slot;
                    Release(old);
                }

                gu = new OwnedGu { kind = kind.Id, slot = slot, fedAt = Now, vital = !Gu.Exists(g => g.vital) };
                Gu.Add(gu);
            }

            if (key >= 0 && !kind.Passive)
            {
                Bind(key, "gu:" + kind.Id);
            }

            Passives();

            if (!quiet)
            {
                Said?.Invoke(key >= 0 && !kind.Passive ? $"{kind.Name} — на клавише {key + 1}" : $"{kind.Name} в апертуре");
            }

            return gu;
        }

        /// <summary>A Gu taken out of the ring (the proving ground's arsenal): off its key, put down if held.</summary>
        public void Release(OwnedGu gu)
        {
            if (gu == null || !Gu.Remove(gu))
            {
                return;
            }

            if (First(gu.kind) == null)
            {
                Lower(gu.kind, null);

                for (var i = 0; i < BarSize; i++)
                {
                    if (Bar[i] == "gu:" + gu.kind) Bar[i] = null;
                }
            }

            Passives();
        }

        /// <summary>Every Gu rested at once.</summary>
        public void RestAll()
        {
            foreach (var gu in Gu)
            {
                gu.tired = 0;
                gu.toldTired = false;
            }

            readyAt.Clear();
        }

        // ── Meditation ────────────────────────────────────────────────

        /// <summary>Sitting in peace (no beast hunting near): the hero meditates (set by the game each moment).</summary>
        public bool Meditating;

        /// <summary>How much of the aperture's essence flows into its walls each second of meditation, %.</summary>
        public const float PourRate = 2.5f;

        /// <summary>
        /// Meditation (lore: a Gu Master cultivates by nourishing the aperture's walls with primeval essence): the sea
        /// is poured into the walls and wears them toward the next small realm; when it is worn through, the realm is
        /// reached. At the peak only a breakthrough leads on. A dry sea draws in a stone by itself, if there is one.
        /// </summary>
        void Meditate(float dt)
        {
            if (!Meditating)
            {
                return;
            }

            var peak = Aperture.Stage == Stage.Peak;
            var tempering = peak && Aperture.Progress >= 1;

            // Tempered as far as it goes: nothing more to pour.
            if (tempering && (Aperture.Readiness >= 1 || Aperture.Rank >= 5))
            {
                return;
            }

            var pour = Mathf.Min(Aperture.Essence, PourRate * MeditationScale * dt);

            if (pour <= 0)
            {
                if (Stones > 0 && !Absorbing && !EndlessEssence)
                {
                    StartAbsorbing();
                }

                return;
            }

            Aperture.Essence -= pour;

            // At the peak, the walls worn through: the essence tempers the aperture for the breakthrough.
            if (tempering)
            {
                Aperture.Readiness = Mathf.Min(1, Aperture.Readiness + pour / (Aperture.StageCost * 0.5f));

                if (Aperture.Readiness >= 1)
                {
                    Said?.Invoke("Апертура закалена до предела — шанс прорыва больше не растёт от медитации");
                }

                return;
            }

            Aperture.Progress = Mathf.Min(1, Aperture.Progress + pour / Aperture.StageCost);

            if (Aperture.Progress < 1)
            {
                return;
            }

            if (peak)
            {
                Said?.Invoke(Aperture.Rank >= 5
                    ? "Стенки апертуры истончены до предела — выше пятого ранга пути пока нет"
                    : "Стенки апертуры истончены до предела — можно пытаться прорваться (полное море, апертура K); медитация дальше закаляет апертуру");

                return;
            }

            Aperture.Advance();
            Aperture.Progress = 0;
            Passives();
            GuGlow.Flash(transform.position + Vector3.up, 2, Aperture.RankColour(Aperture.Rank), 1);
            Said?.Invoke($"Медитация принесла плоды: {Aperture.Name(Aperture.Stage)} ранга {Aperture.Rank}");
            Did?.Invoke("stage", (int)Aperture.Stage);
        }

        // ── Cultivation ───────────────────────────────────────────────

        /// <summary>What helps a breakthrough besides talent, tempering and insight: the body's spirit (Physique), as a chance.</summary>
        public float BreakBonus;

        /// <summary>How fast meditation pours the sea into the walls (1: as written; the body's spirit raises it).</summary>
        public float MeditationScale = 1;

        /// <summary>The chance of breaking through now.</summary>
        public float BreakChance => Aperture.BreakChance(BreakBonus);

        /// <summary>
        /// A try at the next rank: from the peak with the walls worn through, the whole sea thrown at the aperture's wall.
        /// It seldom holds (one or two in a hundred, more with talent and preparation); failed, the sea is spent for nothing.
        /// </summary>
        public bool BreakThrough() => BreakThrough((float)random.NextDouble());

        /// <summary>The try with a roll given (the checks: 0 always holds, 1 never).</summary>
        public bool BreakThrough(float roll)
        {
            if (!Aperture.CanBreakThrough)
            {
                Said?.Invoke(Aperture.Stage != Stage.Peak ? "Прорыв возможен только с пиковой стадии"
                    : Aperture.Rank >= 5 ? "Выше пятого ранга пути пока нет"
                    : Aperture.Progress < 1 ? "Стенки апертуры ещё не истончены — медитируйте (X), пока ступень не дойдёт до 100%"
                    : "Для прорыва море должно быть полным");

                return false;
            }

            var chance = BreakChance;

            if (!Aperture.BreakThrough(roll, BreakBonus))
            {
                health?.Strike(health.Max * 0.05f);
                GuGlow.Flash(transform.position + Vector3.up, 1.5f, new Color(0.6f, 0.2f, 0.2f), 0.6f);
                Said?.Invoke($"Прорыв не удался (шанс был {chance * 100:0.#}%): море сгорело о стенку апертуры. " +
                             $"Опыт неудачи: +{Mathf.Min(2, 0.2f * Aperture.Failures):0.#}% к следующей попытке");
                Did?.Invoke("breakthrough-failed", Aperture.Rank);

                return false;
            }

            Passives();
            Said?.Invoke($"Прорыв! Ранг {Aperture.Rank}, {Aperture.Name(Aperture.Stage)}: эссенция — {Aperture.EssenceName}" +
                         (Open > OpenSlots(Aperture.Rank - 1) ? $", открылись новые ячейки ({Open})" : ""));
            GuGlow.Flash(transform.position + Vector3.up, 3, Aperture.RankColour(Aperture.Rank), 1.2f);
            Did?.Invoke("breakthrough", Aperture.Rank);

            return true;
        }

        /// <summary>For testing: a rank and small realm set at once (the sea kept within the talent).</summary>
        public void SetRealm(int rank, Stage stage)
        {
            Aperture.Rank = Mathf.Clamp(rank, 1, 5);
            Aperture.Stage = stage;
            FitRing();
            Passives();
            Said?.Invoke($"Ранг {Aperture.Rank}, {Aperture.Name(Aperture.Stage)}");
        }

        /// <summary>For testing: a talent set at once (the sea kept within it).</summary>
        public void SetTalent(float talent)
        {
            Aperture.Talent = Mathf.Clamp(talent, 20, 100);
            Aperture.Essence = Mathf.Min(Aperture.Essence, Aperture.Cap);
            Said?.Invoke($"Талант: {Aperture.TalentName}, море наполняется за {Talents.Span(Aperture.RefillHours(Aperture.Talent))}");
        }

        /// <summary>For testing: the next way's talent (99 → 88 → 70 → 50 → 30 → 100 → 99…).</summary>
        public void NextGrade()
        {
            float[] talents = { 99, 88, 70, 50, 30, 100 };
            var at = Array.FindIndex(talents, t => Mathf.Abs(t - Aperture.Talent) < 0.5f);
            SetTalent(talents[(at + 1) % talents.Length]);
        }

        // ── Feeding ───────────────────────────────────────────────────

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

            if (kind.Food == null || Now - gu.fedAt < kind.MealEvery * 0.5)
            {
                if (!quiet) Said?.Invoke(kind.Food == null ? $"{kind.Name} не ест" : $"{kind.Name} сыт");

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
                if (gu.Kind.Food == null || Now - gu.fedAt < gu.Kind.MealEvery * 0.5)
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

                if (kind == null || kind.Food == null || HungerOf(gu) == Hunger.Dead)
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

        // ── The moonblade ─────────────────────────────────────────────

        int throws;

        /// <summary>
        /// Where a skill is aimed by default: the middle of the screen (the crosshair), out to the skill's range. A
        /// missile aimed at open ground is aimed at a body's height over it instead — it flies level toward what
        /// stands there, not into the earth a few steps off.
        /// </summary>
        public Vector3 AimPoint(GuKind kind)
        {
            var point = Camera != null
                ? Camera.AimPoint(Mathf.Max(kind.Range, kind.Reach, 4), transform)
                : transform.position + Vector3.up * 1.3f + transform.forward * Mathf.Max(kind.Range, 4);

            if (kind.Skill is SkillKind.Moonblade or SkillKind.Bolt or SkillKind.Tame &&
                UnityEngine.Physics.Raycast(point + Vector3.up * 0.25f, Vector3.down, out var ground, 0.5f, ~0, QueryTriggerInteraction.Ignore) &&
                ground.normal.y > 0.7f && ground.collider.GetComponentInParent<GuWorld.Combat.IStrikable>() == null)
            {
                point = ground.point + Vector3.up * (view != null ? 1.05f * view.Scale : 1);
            }

            return point;
        }

        /// <summary>Where the right hand lets a blade go, thrown toward a point (the hero turned toward it).</summary>
        public Vector3 HandFor(Vector3 target)
        {
            var flat = target - transform.position;
            flat.y = 0;
            flat = flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;
            var height = view != null ? 1.3f * view.Scale : 1.25f;

            return transform.position + Vector3.up * height + Vector3.Cross(Vector3.up, flat) * 0.25f + flat * 0.45f;
        }

        /// <summary>The tilt of the next moonblade about its flight, degrees: each cut the other way, as the arm swings.</summary>
        public float NextRoll => (throws + 1) % 2 == 0 ? 14 : -14;

        /// <summary>How much bigger and longer the moonblades are now (a Little Light beside the moon Gu).</summary>
        public float MoonScale => Working(SkillKind.Amplify)?.Amount ?? 1;

        /// <summary>The cut: the hero turns toward the point aimed at and throws the blade at it from the right hand.</summary>
        void ThrowMoonblade(GuKind kind, Vector3 target)
        {
            // Thrown exactly as its aim showed it (PlanShot).
            var shot = PlanShot(kind, target);
            Turn(target);
            var from = shot.From;
            var aim = shot.Direction;
            // Stronger with every rank; each cut tilted the other way, as the arm swings.
            var power = kind.Power * (1 + 0.6f * (Aperture.Rank - 1)) * GuPower;
            throws++;
            Moonblade.Throw(from, aim, kind, solid, power, throws % 2 == 0 ? 14 : -14, MoonScale);
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

        // ── The action bar ────────────────────────────────────────────

        /// <summary>Sets a Gu on a key (taking it off any other).</summary>
        public void Bind(int key, string entry)
        {
            if (key < 0 || key >= BarSize || !Valid(entry) || GuBook.Find(entry.Substring(3)).Passive)
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
    }
}
