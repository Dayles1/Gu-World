using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuWorld.World
{
    /// <summary>
    /// What the builder worked out for a location and the game needs to know: the
    /// trails and streams (for the map, and to keep things off them), the
    /// settlements and the named places. Kept in the scene, on one object.
    /// </summary>
    public class WorldPlan : MonoBehaviour
    {
        [Serializable]
        public struct Line
        {
            public string Name;
            public Vector3[] Points;
            /// <summary>Half its width, m.</summary>
            public float Half;
        }

        [Serializable]
        public struct Mark
        {
            /// <summary>A name to go to it by (the chat's /tp), where it has one (a landmark's).</summary>
            public string Id;
            public string Label;
            public Vector3 At;
            public bool Major;
        }

        /// <summary>A building as the map draws it: its middle (X, Z), half its size along its own axes, its turn (degrees) and the colour of its roof.</summary>
        [Serializable]
        public struct Footprint
        {
            public Vector2 Centre;
            public Vector2 Half;
            public float Yaw;
            public Color Tint;
        }

        public Line[] Trails = new Line[0];
        public Line[] Streams = new Line[0];
        /// <summary>The villages' streets, lanes and stairs.</summary>
        public Line[] Streets = new Line[0];
        public Footprint[] Buildings = new Footprint[0];
        public Settlement[] Settlements = new Settlement[0];
        public Mark[] Marks = new Mark[0];

        public static WorldPlan Current { get; private set; }

        /// <summary>Where a named place of the plan is (a landmark, a tunnel's mouth), or null.</summary>
        public static Vector3? Landmark(string id)
        {
            // Asked for before the plan's own Awake (the game sets itself up in its Awake too): found in the scene.
            var plan = Current != null ? Current : FindAnyObjectByType<WorldPlan>();

            if (plan == null || id == null)
            {
                return null;
            }

            foreach (var mark in plan.Marks)
            {
                if (mark.Id == id)
                {
                    return mark.At;
                }
            }

            return null;
        }

        void Awake() => Current = this;

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        /// <summary>How far a point (X, Z) is from the nearest line of a set.</summary>
        public static float Distance(IEnumerable<Line> lines, Vector2 p)
        {
            var best = float.PositiveInfinity;

            foreach (var line in lines)
            {
                if (line.Points == null)
                {
                    continue;
                }

                for (var i = 0; i + 1 < line.Points.Length; i++)
                {
                    var a = line.Points[i];
                    var b = line.Points[i + 1];
                    best = Mathf.Min(best, QingMao.ToSegment(p, new Vector2(a.x, a.z), new Vector2(b.x, b.z)) - line.Half);
                }
            }

            return best;
        }
    }
}
