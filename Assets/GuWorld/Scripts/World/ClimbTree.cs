using System;
using System.Collections.Generic;
using GuWorld.Combat;
using UnityEngine;

namespace GuWorld.World
{
    /// <summary>
    /// A giant tree one can climb (the builder plants them: dark, ancient, forty and
    /// more metres tall): its trunk — where it stands, how tall it can be climbed, how
    /// thick it is at each height — and its branches (TreeBranch), which can be walked
    /// out along, sat on, and broken.
    /// </summary>
    public class ClimbTree : MonoBehaviour
    {
        /// <summary>The trunk's radius at its foot (over the buttresses) and at the top of its climbable part, m.</summary>
        public float BaseRadius = 2;
        public float TopRadius = 0.8f;
        /// <summary>How high the trunk can be climbed, m above its foot.</summary>
        public float ClimbHeight = 30;
        public TreeBranch[] Branches = new TreeBranch[0];

        public static readonly List<ClimbTree> All = new List<ClimbTree>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public Vector3 Foot => transform.position;

        /// <summary>The trunk's radius at a height above its foot.</summary>
        public float RadiusAt(float height)
        {
            var t = Mathf.Clamp01(height / Mathf.Max(1, ClimbHeight));
            // The buttresses flare the foot out; above them it tapers evenly.
            var flare = Mathf.Lerp(1.35f, 1, Mathf.Clamp01(height / 3));
            return Mathf.Lerp(BaseRadius, TopRadius, t) * flare;
        }

        /// <summary>The nearest climbable trunk within `reach` of its surface, and how far that is.</summary>
        public static ClimbTree Near(Vector3 at, float reach, out float distance)
        {
            ClimbTree best = null;
            distance = float.PositiveInfinity;

            foreach (var tree in All)
            {
                var foot = tree.Foot;
                var height = at.y - foot.y;

                if (height < -1.5f || height > tree.ClimbHeight + 1)
                {
                    continue;
                }

                var flat = new Vector2(at.x - foot.x, at.z - foot.z).magnitude - tree.RadiusAt(Mathf.Max(0, height));

                if (flat < reach && flat < distance)
                {
                    best = tree;
                    distance = flat;
                }
            }

            return best;
        }

        /// <summary>A point on the trunk's surface (plus `out_` metres away from it) at an angle round it and a height above its foot.</summary>
        public Vector3 Surface(float angle, float height, float out_)
        {
            var r = RadiusAt(height) + out_;

            return Foot + new Vector3(Mathf.Sin(angle) * r, height, Mathf.Cos(angle) * r);
        }

        /// <summary>The branch whose foot is near a height and angle round the trunk (to step onto it), or null.</summary>
        public TreeBranch BranchAt(float angle, float height)
        {
            TreeBranch best = null;
            var bestScore = float.PositiveInfinity;

            foreach (var branch in Branches)
            {
                if (branch == null || branch.Broken)
                {
                    continue;
                }

                var local = branch.Root - Foot;
                var branchAngle = Mathf.Atan2(local.x, local.z);
                var turn = Mathf.Abs(Mathf.DeltaAngle(branchAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg));
                var rise = Mathf.Abs(local.y - height);

                if (turn < 40 && rise < 1.6f)
                {
                    var score = turn / 40 + rise;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = branch;
                    }
                }
            }

            return best;
        }
    }
}
