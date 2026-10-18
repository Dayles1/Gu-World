using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Creatures
{
    /// <summary>
    /// Whatever fights the hero — a beast (Beast) or a Gu Master of another clan (GuMaster): what the HUD shows
    /// over its head, what a Gu can do to it besides wounding it, how a blow throws it.
    /// </summary>
    public interface IFoe
    {
        /// <summary>Its name over its head ("Серый волк", "Бай Лун"), and what it is in words ("король тысячи зверей", "Гу-мастер 2 ранга").</summary>
        string Title { get; }
        string Rung { get; }
        /// <summary>How great a foe it is: 0 an ordinary one, 1…3 a beast king of a hundred, a thousand, a myriad (or a Gu Master of that weight).</summary>
        int Tier { get; }
        int Level { get; }
        float Health { get; }
        float MaxHealth { get; }
        bool Dead { get; }
        /// <summary>After the hero now.</summary>
        bool Hunting { get; }
        /// <summary>Enslaved: on the hero's side.</summary>
        bool Tamed { get; }
        /// <summary>A shield up (most of a blow is taken), berserk (its name red).</summary>
        bool Shielded { get; }
        bool Berserk { get; }
        /// <summary>When it was last struck (Time.time).</summary>
        float StruckAt { get; }
        /// <summary>How tall it stands, m.</summary>
        float Height { get; }
        /// <summary>What the boss bar adds after the health ("ЩИТ", "НЕИСТОВСТВО").</summary>
        string State { get; }
        Transform transform { get; }

        void Afflict(Affliction kind, float seconds, float power);
        /// <summary>A blow throws it: away (and up) at this speed.</summary>
        void Push(Vector3 velocity);
    }

    /// <summary>Every foe there is now (wolves and Gu Masters register themselves).</summary>
    public static class Foes
    {
        public static readonly List<IFoe> All = new List<IFoe>();

        /// <summary>The living foes not on the hero's side within a reach of a point (flat).</summary>
        public static IEnumerable<IFoe> Near(Vector3 at, float reach)
        {
            foreach (var foe in All.ToArray())
            {
                if (foe is Object alive && alive != null && !foe.Dead && !foe.Tamed)
                {
                    var to = foe.transform.position - at;
                    to.y = 0;

                    if (to.magnitude <= reach)
                    {
                        yield return foe;
                    }
                }
            }
        }

        /// <summary>Whether any living foe hunts the hero within a reach of a point.</summary>
        public static bool Hunting(Vector3 at, float reach)
        {
            foreach (var foe in Near(at, reach))
            {
                if (foe.Hunting)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>How many living foes there are (not on the hero's side).</summary>
        public static int Living()
        {
            var n = 0;

            foreach (var foe in All)
            {
                if (foe is Object alive && alive != null && !foe.Dead && !foe.Tamed)
                {
                    n++;
                }
            }

            return n;
        }
    }
}
