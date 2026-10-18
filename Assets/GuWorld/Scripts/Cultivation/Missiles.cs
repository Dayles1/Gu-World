using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.Cultivation
{
    /// <summary>A Gu's missile in flight (a moonblade, a bolt): where it is, which way it goes, who threw it.</summary>
    public interface IMissile
    {
        bool Flying { get; }
        Vector3 Heading { get; }
        Collider Thrower { get; }
        Transform transform { get; }
    }

    /// <summary>Every missile in flight (a Gu Master sees one coming and rolls out of its way).</summary>
    public static class Missiles
    {
        public static readonly List<IMissile> All = new List<IMissile>();

        /// <summary>
        /// The missile not thrown by `self` that will pass nearest `at` within `seconds` (flying at `speed`), if any passes
        /// within `margin` of it: how soon, and which way it flies.
        /// </summary>
        public static bool Coming(Vector3 at, Collider self, float margin, float seconds, out Vector3 heading)
        {
            heading = Vector3.zero;
            var best = float.PositiveInfinity;

            foreach (var missile in All)
            {
                if (!(missile is Object alive) || alive == null || !missile.Flying || (self != null && missile.Thrower == self))
                {
                    continue;
                }

                var from = missile.transform.position;
                var to = at - from;
                var along = Vector3.Dot(to, missile.Heading);

                // Behind it, or too far ahead to matter yet (about 30 m/s).
                if (along < 0 || along > 30 * seconds)
                {
                    continue;
                }

                var miss = (to - missile.Heading * along).magnitude;

                if (miss < margin && along < best)
                {
                    best = along;
                    heading = missile.Heading;
                }
            }

            return best < float.PositiveInfinity;
        }
    }
}
