using System;
using UnityEngine;

namespace GuWorld.Systems
{
    /// <summary>The body's five sides, each trained by what the hero does.</summary>
    public enum Trait
    {
        /// <summary>Сила: the fists' weight, what the bag carries, the climb.</summary>
        Strength,
        /// <summary>Ловкость: the run, the roll, the landing.</summary>
        Agility,
        /// <summary>Выносливость: how much the body can give before it must rest (stamina), and how soon it is back.</summary>
        Endurance,
        /// <summary>Живучесть: how much health the body holds, and how fast it mends.</summary>
        Vitality,
        /// <summary>Дух: meditation's pace, the breakthrough's chance, the Gu tiring the master less.</summary>
        Spirit,
    }

    /// <summary>The body as saved: each trait's level and what was trained toward the next, the stamina left.</summary>
    [Serializable]
    public class PhysiqueSave
    {
        public int[] levels = new int[0];
        public float[] trained = new float[0];
        public float stamina = -1;
    }

    /// <summary>
    /// The hero's training of the body, shown the way a "system" shows a status: five traits that begin at ten and rise
    /// by doing — blows landed and climbs make strength, runs and rolls agility, every effort spent endurance, wounds
    /// survived vitality, meditation and Gu driven spirit — each level asking a little more than the last. What they give
    /// is small per point and adds up: a trained body hits harder, runs longer, holds more health and breaks through sooner.
    ///
    /// Stamina (Выносливость's pool) is spent by sprinting, rolling, jumping, climbing and blows; empty, the hero cannot
    /// sprint or roll until it is back. It comes back by itself a moment after the last effort.
    /// </summary>
    public class Physique : MonoBehaviour
    {
        public const int StartLevel = 10;
        public const int MaxLevel = 99;
        static readonly int Count = Enum.GetValues(typeof(Trait)).Length;

        readonly int[] levels = Filled(StartLevel);
        readonly float[] trained = new float[Count];

        /// <summary>Stamina now and how long since it was last spent (it comes back after a breath).</summary>
        public float Stamina { get; private set; } = 100;
        float restedFor;

        /// <summary>A trait rose a level (the trait, its new level).</summary>
        public event Action<Trait, int> Raised;

        static int[] Filled(int value)
        {
            var all = new int[Count];

            for (var i = 0; i < Count; i++)
            {
                all[i] = value;
            }

            return all;
        }

        public int Level(Trait trait) => levels[(int)trait];

        /// <summary>What a level takes to rise to the next: a little more each time (40 at ten, ~136 at twenty, ~460 at thirty).</summary>
        public static float Need(int level) => 40 * Mathf.Pow(1.13f, level - StartLevel);

        /// <summary>How far toward the next level a trait is (0…1).</summary>
        public float Progress(Trait trait) => Level(trait) >= MaxLevel ? 1 : Mathf.Clamp01(trained[(int)trait] / Need(Level(trait)));

        /// <summary>The body's level as a whole: one, and one more for every two levels gained in all the traits together.</summary>
        public int Overall
        {
            get
            {
                var gained = 0;

                foreach (var level in levels)
                {
                    gained += level - StartLevel;
                }

                return 1 + gained / 2;
            }
        }

        /// <summary>What the body has come to, in a word (the status window's title).</summary>
        public string Title => Overall switch
        {
            < 5 => "Неокрепший юноша",
            < 15 => "Закалённый",
            < 30 => "Крепкий боец",
            < 50 => "Железное тело",
            < 80 => "Мастер тела",
            _ => "Тело на пределе смертного",
        };

        public static string Name(Trait trait) => trait switch
        {
            Trait.Strength => "Сила",
            Trait.Agility => "Ловкость",
            Trait.Endurance => "Выносливость",
            Trait.Vitality => "Живучесть",
            _ => "Дух",
        };

        public static string Short(Trait trait) => trait switch
        {
            Trait.Strength => "СИЛ",
            Trait.Agility => "ЛОВ",
            Trait.Endurance => "ВЫН",
            Trait.Vitality => "ЖИВ",
            _ => "ДУХ",
        };

        /// <summary>How a trait is trained, in a line (the status window).</summary>
        public static string HowTrained(Trait trait) => trait switch
        {
            Trait.Strength => "удары кулаками, лазание, тяжёлая сумка",
            Trait.Agility => "бег, перекаты, парирование, уклонения",
            Trait.Endurance => "любая трата выносливости",
            Trait.Vitality => "полученные и пережитые раны",
            _ => "медитация, применение Гу, попытки прорыва",
        };

        /// <summary>What a trait gives now, in a line.</summary>
        public string Gives(Trait trait) => trait switch
        {
            Trait.Strength => $"удар ×{StrengthFactor:0.00} · сумка +{BagBonus:0} кг",
            Trait.Agility => $"бег ×{SpeedFactor:0.00} · перекат отдыхает ×{RollRestFactor:0.00} · мягче приземление",
            Trait.Endurance => $"запас {MaxStamina:0} · восстановление {StaminaRegen:0}/с",
            Trait.Vitality => $"здоровье {Health.Base * HealthFactor:0} · заживление ×{MendFactor:0.00}",
            _ => $"медитация ×{MeditationFactor:0.00} · прорыв +{BreakBonus * 100:0.##}% · Гу устают ×{TireFactor:0.00}",
        };

        int Above(Trait trait) => Level(trait) - StartLevel;

        // What the traits give.
        public float StrengthFactor => 1 + 0.04f * Above(Trait.Strength);
        public float BagBonus => 1.5f * Above(Trait.Strength);
        public float SpeedFactor => Mathf.Min(1.6f, 1 + 0.012f * Above(Trait.Agility));
        public float RollRestFactor => Mathf.Max(0.5f, 1 - 0.01f * Above(Trait.Agility));
        public float SafeLandingBonus => 0.08f * Above(Trait.Agility);
        public float MaxStamina => 100 + 6 * Above(Trait.Endurance);
        public float StaminaRegen => 22 + 0.8f * Above(Trait.Endurance);
        public float HealthFactor => 1 + 0.06f * Above(Trait.Vitality);
        public float MendFactor => 1 + 0.03f * Above(Trait.Vitality);
        public float MeditationFactor => 1 + 0.03f * Above(Trait.Spirit);
        public float BreakBonus => Mathf.Min(0.01f, 0.0005f * Above(Trait.Spirit));
        public float TireFactor => Mathf.Max(0.6f, 1 - 0.006f * Above(Trait.Spirit));

        /// <summary>Whether the body is spent (no sprint, no roll until some stamina is back).</summary>
        public bool Spent { get; private set; }

        /// <summary>Takes stamina for an effort; false (and nothing taken) when there is not enough. Training endurance by it.</summary>
        public bool Spend(float amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (Stamina < amount)
            {
                return false;
            }

            Drain(amount);

            return true;
        }

        /// <summary>Stamina drained by an effort that goes on (a sprint, a climb): what there is, down to nothing.</summary>
        public void Drain(float amount)
        {
            if (amount <= 0)
            {
                return;
            }

            var spent = Mathf.Min(Stamina, amount);
            Stamina -= spent;
            restedFor = 0;
            Train(Trait.Endurance, spent * 0.06f);

            if (Stamina <= 0.5f)
            {
                Spent = true;
            }
        }

        /// <summary>Stamina full at once (the admin panel).</summary>
        public void Refill()
        {
            Stamina = MaxStamina;
            Spent = false;
        }

        /// <summary>A moment passes: after a breath without effort, stamina comes back.</summary>
        public void Step(float dt)
        {
            restedFor += dt;
            Stamina = Mathf.Min(Stamina, MaxStamina);

            if (restedFor > 0.8f && Stamina < MaxStamina)
            {
                Stamina = Mathf.Min(MaxStamina, Stamina + StaminaRegen * dt);
            }

            // Spent, the body must get back a quarter before it runs again.
            if (Spent && Stamina >= MaxStamina * 0.25f)
            {
                Spent = false;
            }
        }

        /// <summary>Something done that trains a trait (how much); each level reached is told.</summary>
        public void Train(Trait trait, float amount)
        {
            if (amount <= 0 || !float.IsFinite(amount))
            {
                return;
            }

            var i = (int)trait;

            if (levels[i] >= MaxLevel)
            {
                return;
            }

            trained[i] += amount;

            while (levels[i] < MaxLevel && trained[i] >= Need(levels[i]))
            {
                trained[i] -= Need(levels[i]);
                levels[i]++;
                Raised?.Invoke(trait, levels[i]);
            }
        }

        /// <summary>A trait set at once (the admin panel).</summary>
        public void Set(Trait trait, int level)
        {
            levels[(int)trait] = Mathf.Clamp(level, StartLevel, MaxLevel);
            trained[(int)trait] = 0;
        }

        /// <summary>A new body: every trait at ten, stamina full.</summary>
        public void Renew()
        {
            for (var i = 0; i < Count; i++)
            {
                levels[i] = StartLevel;
                trained[i] = 0;
            }

            Stamina = MaxStamina;
            Spent = false;
        }

        public PhysiqueSave Save() => new PhysiqueSave { levels = (int[])levels.Clone(), trained = (float[])trained.Clone(), stamina = Stamina };

        /// <summary>The body from a save (null: a new one); what does not make sense is put right.</summary>
        public void Load(PhysiqueSave saved)
        {
            Renew();

            if (saved?.levels == null)
            {
                return;
            }

            for (var i = 0; i < Count && i < saved.levels.Length; i++)
            {
                levels[i] = Mathf.Clamp(saved.levels[i], StartLevel, MaxLevel);
                trained[i] = saved.trained != null && i < saved.trained.Length && float.IsFinite(saved.trained[i]) ? Mathf.Max(0, saved.trained[i]) : 0;
            }

            Stamina = saved.stamina >= 0 && float.IsFinite(saved.stamina) ? Mathf.Min(saved.stamina, MaxStamina) : MaxStamina;
        }
    }
}
