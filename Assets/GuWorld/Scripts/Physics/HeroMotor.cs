using GuWorld.World;
using UnityEngine;

namespace GuWorld.Physics
{
    public enum Stance
    {
        Stand,
        Crouch,
        Crawl,
    }

    /// <summary>What the body is asked to do this step.</summary>
    public struct MotorInput
    {
        /// <summary>Where to go in world space (X, Z), length 0…1.</summary>
        public float moveX;
        public float moveZ;
        public bool sprint;
        /// <summary>True on the frame the jump key went down.</summary>
        public bool jump;
        /// <summary>Movement is scaled by this (slower while busy).</summary>
        public float speedScale;
    }

    /// <summary>
    /// A body's physics: an upright capsule pushed around by its input,
    /// gravity, the ground and solid objects (ported from
    /// client/physics/character.ts). Runs on its own fixed step (120 Hz), so
    /// it behaves the same at any frame rate.
    ///
    /// Unity's CharacterController does the collision; the feel — speeds,
    /// acceleration, braking, coyote time, the jump buffer, sliding off
    /// slopes steeper than 50° — is the original's. Besides moving it can
    /// stand, crouch or crawl (a shorter capsule), climb onto a ledge (a
    /// scripted move from the foot of the ledge onto its top) and climb up a
    /// wall — a house's, a rock face, a tree's trunk — hand over hand, over its
    /// top or out round the eaves onto a roof. It never leaves its location's
    /// bounds. Swimming is not ported yet: the proving
    /// ground has no water.
    ///
    /// The transform's position is the point between the feet.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class HeroMotor : MonoBehaviour
    {
        public const float Radius = 0.3f;

        /// <summary>The capsule's height per stance, m.</summary>
        public static float HeightOf(Stance stance) => stance switch
        {
            Stance.Crouch => 1.2f,
            Stance.Crawl => 0.62f,
            _ => 1.8f,
        };

        /// <summary>Walk and run speeds per stance, m/s.</summary>
        public static (float walk, float run) SpeedOf(Stance stance) => stance switch
        {
            Stance.Crouch => (2.1f, 2.6f),
            Stance.Crawl => (1.1f, 1.3f),
            _ => (4.2f, 7.5f),
        };

        const float Step = 1f / 120f;
        const int MaxSteps = 10;
        const float Gravity = 25;
        const float JumpSpeed = 8.6f;
        const float GroundAcceleration = 24;
        const float GroundBraking = 30;
        const float AirAcceleration = 9;
        /// <summary>The steepest ground one can stand on: cos 50°.</summary>
        static readonly float Walkable = Mathf.Cos(50 * Mathf.Deg2Rad);
        /// <summary>How far down the feet stick to the ground when walking downhill.</summary>
        const float Snap = 0.4f;
        /// <summary>Objects up to this high are stepped onto without climbing.</summary>
        const float StepUp = 0.45f;
        /// <summary>Ledges this high (above the feet) can be climbed onto.</summary>
        const float ClimbMin = 0.5f;
        const float ClimbMax = 2.3f;
        /// <summary>A jump still works this long after walking off an edge…</summary>
        const float CoyoteTime = 0.1f;
        /// <summary>…and a press counts this long before landing.</summary>
        const float JumpBuffer = 0.12f;
        /// <summary>How far inside the location's bounds the body is kept, m.</summary>
        const float Edge = 1;

        public Vector3 Velocity;
        /// <summary>How many times higher a jump goes (a Gu's wings: more).</summary>
        public float JumpScale = 1;
        public bool Grounded { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        /// <summary>Which way the body faces (radians around Y; 0 looks along +Z).</summary>
        public float Facing;
        /// <summary>How fast Facing turns, rad/s (smoothed).</summary>
        public float TurnRate { get; private set; }
        /// <summary>Change of ground speed, m/s² (smoothed).</summary>
        public float Acceleration { get; private set; }
        /// <summary>Downward speed at the last landing, for the animation.</summary>
        public float LandingSpeed;
        /// <summary>The body came down on walkable ground, this fast (m/s, downward).</summary>
        public event System.Action<float> Landed;
        public Stance Stance { get; private set; } = Stance.Stand;

        /// <summary>The box the body is kept in.</summary>
        public Bounds Bounds = new Bounds(Vector3.zero, Vector3.one * 100000);

        public float Height => HeightOf(Stance);
        public float HorizontalSpeed => new Vector2(Velocity.x, Velocity.z).magnitude;
        /// <summary>0…1 through a climb, or null when not climbing.</summary>
        public float? ClimbProgress => climbing ? climbTime / climbDuration : null;
        public bool Free => !climbing && Tree == null && !Sitting && !OnWall;

        /// <summary>Holding onto a wall (a house's, a rock face, a trunk): the surface's normal where the hands are, out toward the body.</summary>
        public bool OnWall { get; private set; }
        public Vector3 WallNormal { get; private set; }

        /// <summary>The giant tree being climbed (null: none), where round its trunk and how high up the feet are.</summary>
        public ClimbTree Tree { get; private set; }
        public float TreeAngle { get; private set; }
        public float TreeHeight { get; private set; }
        /// <summary>How far through its moves the climbing has gone (for the arms' and legs' swing).</summary>
        public float ClimbPhase { get; private set; }
        /// <summary>Sitting (on the ground, or on an edge with the legs hanging over it).</summary>
        public bool Sitting { get; private set; }
        public bool SittingOnEdge { get; private set; }
        /// <summary>The branch under the feet (null: none).</summary>
        public TreeBranch Branch { get; private set; }

        CharacterController controller;
        float sinceGrounded;
        float jumpBuffered;
        float lastSpeed;

        bool climbing;
        /// <summary>The scripted climb's path: up the face and over, or out round the eaves, up and in.</summary>
        Vector3[] climbPath;
        float climbTime;
        float climbDuration;

        /// <summary>On the ground or just off it this step (steps are gone up only then).</summary>
        bool standing;

        // Collected from the controller's hits during one Move.
        bool hitWalkable;
        Vector3 walkableNormal;
        Collider groundCollider;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.radius = Radius;
            controller.slopeLimit = 50;
            controller.stepOffset = StepUp;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0;
            ApplyCapsule();
        }

        /// <summary>Puts the body at (x, z), standing on whatever is there.</summary>
        public void PlaceAt(Vector3 position)
        {
            controller.enabled = false;
            // Whatever is right under the feet (half a metre of slack for a saved position on a block).
            var ground = SurfaceBelow(position + Vector3.up * 0.5f, 400, out var top, out _) ? top : position.y;
            transform.position = new Vector3(position.x, Mathf.Max(position.y, ground), position.z);
            controller.enabled = true;
            Velocity = Vector3.zero;
            climbing = false;
            Tree = null;
            Sitting = false;
            OnWall = false;
        }

        // ── Walls ────────────────────────────────────────────────────

        /// <summary>How far before the body a wall can be taken hold of, m (from the capsule's side).</summary>
        const float WallReach = 0.45f;
        /// <summary>Climbing speeds up, down and sideways, m/s.</summary>
        const float WallUp = 1.25f;
        const float WallDown = 1.8f;
        const float WallSide = 1.3f;
        /// <summary>The hands' and the hips' heights above the feet while climbing.</summary>
        const float Hands = 1.25f;
        const float Hips = 0.6f;
        /// <summary>How far from the surface the body's middle is held.</summary>
        static float Standoff => Radius + 0.06f;

        /// <summary>A surface one can climb: too steep to stand on, not an overhang (a ceiling, an eave's underside); not a body.</summary>
        bool Climbable(RaycastHit hit) =>
            hit.collider != controller && hit.normal.y < Walkable && hit.normal.y > -0.35f && !hit.collider.isTrigger &&
            hit.collider.GetComponentInParent<Combat.IZoned>() == null && !(hit.collider is CharacterController) &&
            (hit.collider.attachedRigidbody == null || hit.collider.attachedRigidbody.isKinematic);

        /// <summary>The nearest climbable surface along a ray (not the body itself).</summary>
        bool WallAlong(Vector3 from, Vector3 direction, float distance, out RaycastHit wall)
        {
            wall = default;
            var best = float.PositiveInfinity;

            foreach (var hit in UnityEngine.Physics.RaycastAll(from, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == controller || hit.distance >= best)
                {
                    continue;
                }

                // Something nearer that is not climbable (a person, a ceiling) is in the way.
                best = hit.distance;
                wall = hit;
            }

            return best < float.PositiveInfinity && Climbable(wall);
        }

        /// <summary>Takes hold of a wall before the body (in the direction given): true when there is one within reach.</summary>
        public bool GrabWall(float dirX, float dirZ)
        {
            if (climbing || Tree != null || Sitting || OnWall || Stance == Stance.Crawl || Velocity.y < -14)
            {
                return false;
            }

            var direction = new Vector3(dirX, 0, dirZ);

            if (direction.sqrMagnitude < 1e-4f)
            {
                return false;
            }

            direction.Normalize();
            var feet = transform.position;

            // At the hands' height, or the hips' (a wall whose top is just above the head is climbed over at once).
            if (!WallAlong(feet + Vector3.up * Hands, direction, Radius + WallReach, out var wall) &&
                !WallAlong(feet + Vector3.up * Hips, direction, Radius + WallReach, out wall))
            {
                return false;
            }

            OnWall = true;
            Stance = Stance.Stand;
            ApplyCapsule();
            Velocity = Vector3.zero;
            Grounded = false;
            HoldWall(wall, feet);

            return true;
        }

        /// <summary>Lets go of the wall: pushed off it (a jump away), or simply dropping.</summary>
        public void LeaveWall(bool jump)
        {
            if (!OnWall)
            {
                return;
            }

            OnWall = false;
            var away = Flat(WallNormal);
            Velocity = jump ? away * 4.2f + Vector3.up * 4.8f : away * 0.6f;

            if (jump)
            {
                Facing = Mathf.Atan2(away.x, away.z);
            }
        }

        /// <summary>
        /// A step of climbing a wall: `up` (−1…1) up or down it, `side` (−1…1) along it (to the body's right),
        /// a push away from it (`jump`), or letting go (`drop`). The hands follow the surface as it turns (round a
        /// trunk, into a corner); at its top the body climbs over onto it, under eaves out round them onto the roof;
        /// at its foot, on the ground again, it lets go.
        /// </summary>
        public void WallTick(float dt, float up, float side, bool jump, bool drop)
        {
            if (!OnWall)
            {
                return;
            }

            if (jump || drop)
            {
                LeaveWall(jump);

                return;
            }

            var normal = Flat(WallNormal);
            var right = Vector3.Cross(normal, Vector3.up).normalized;
            var along = Vector3.ProjectOnPlane(Vector3.up, WallNormal).normalized;
            var feet = transform.position;
            ClimbPhase += (Mathf.Abs(up) + Mathf.Abs(side) * 0.7f) * dt * 1.3f;

            // On the ground again, climbing down: off the wall, standing.
            if (up < 0 && SurfaceBelow(feet + Vector3.up * 0.1f, 0.25f, out _, out var groundNormal) && groundNormal.y >= Walkable)
            {
                OnWall = false;
                PlaceAt(feet);

                return;
            }

            if (up > 0)
            {
                var head = feet + Vector3.up * (HeightOf(Stance.Stand) - 0.1f);

                // Under an overhang (an eave): out round it onto what is above, or no higher.
                if (UnityEngine.Physics.Raycast(head, Vector3.up, out var above, 0.35f, ~0, QueryTriggerInteraction.Ignore) && above.collider != controller)
                {
                    if (!TryOverEave(normal))
                    {
                        up = 0;
                    }
                    else
                    {
                        return;
                    }
                }
            }

            var move = along * (up > 0 ? up * WallUp : up * WallDown) + right * side * WallSide;
            var next = feet + move * dt;

            // The surface again from there, straight ahead at the hands.
            if (WallAlong(next + Vector3.up * Hands, -normal, Standoff + 0.7f, out var wall))
            {
                HoldWall(wall, next);

                return;
            }

            // Nothing at the hands going up: the top — over it.
            if (up > 0 && TryOverTop(normal))
            {
                return;
            }

            // A corner going sideways: round it if the wall goes on there, else stop.
            if (side != 0 && WallAlong(next + Vector3.up * Hands + right * side * (Standoff + 0.3f), -right * side, Standoff + 0.6f, out wall))
            {
                HoldWall(wall, next);
            }
        }

        /// <summary>
        /// Holds the body against a surface: its middle just off it, facing it, the feet at `feet`'s height — unless the
        /// body would be in something else there (a wall meeting this one in a corner, a beam, an eave): then it stays
        /// where it is. Only from the hips up is looked at, so a plinth jutting out at the wall's foot does not stop it.
        /// </summary>
        void HoldWall(RaycastHit wall, Vector3 feet)
        {
            var normal = Flat(wall.normal);
            var at = wall.point + normal * Standoff;
            var target = new Vector3(at.x, feet.y, at.z);

            if (Blocked(target, HeightOf(Stance.Stand), Hips) && !Blocked(transform.position, HeightOf(Stance.Stand), Hips))
            {
                Velocity = Vector3.zero;

                return;
            }

            WallNormal = wall.normal;
            controller.enabled = false;
            transform.position = target;
            controller.enabled = true;
            Facing = Mathf.Atan2(-normal.x, -normal.z);
            Velocity = Vector3.zero;
            Grounded = false;
            KeepInside();
        }

        /// <summary>At a wall's top: over onto it, if there is ground to stand on just beyond the edge within reach.</summary>
        bool TryOverTop(Vector3 normal)
        {
            var feet = transform.position;

            foreach (var forward in new[] { 0.45f, 0.8f })
            {
                var spot = feet - normal * (Standoff + forward);

                if (!SurfaceBelow(spot + Vector3.up * (ClimbMax + 0.3f), ClimbMax + 0.3f - 0.3f, out var top, out var landing) || landing.y < Walkable)
                {
                    continue;
                }

                var to = new Vector3(spot.x, top, spot.z);

                if (top - feet.y < 0.3f || Blocked(to, HeightOf(Stance.Crouch)))
                {
                    continue;
                }

                StartClimb(new[] { feet, new Vector3(feet.x, top + 0.05f, feet.z), to }, 0.45f + (top - feet.y) * 0.25f);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Under eaves: out to their edge, up past it and in onto the roof just inside it — if the roof there can be
        /// stood on and is within reach above the head.
        /// </summary>
        bool TryOverEave(Vector3 normal)
        {
            var feet = transform.position;
            var headTop = feet.y + HeightOf(Stance.Stand);
            const float Highest = 3.6f;
            var roof = new System.Collections.Generic.List<(Vector3 at, Vector3 normal)>();
            var edge = -1f;

            // Outward from the wall: the roof overhead, until there is none (its edge).
            for (var outward = 0.3f; outward <= 2.8f; outward += 0.25f)
            {
                var spot = feet + normal * outward;

                if (SurfaceBelow(spot + Vector3.up * Highest, Highest - (headTop - feet.y) + 0.3f, out var top, out var landing))
                {
                    roof.Add((new Vector3(spot.x, top + 0.02f, spot.z), landing));

                    continue;
                }

                edge = outward;

                break;
            }

            if (edge < 0 || roof.Count == 0)
            {
                return false;
            }

            // The outermost spot of the roof one can stand on, with room there.
            for (var i = roof.Count - 1; i >= 0; i--)
            {
                var (to, landing) = roof[i];

                if (landing.y < Walkable || Blocked(to, HeightOf(Stance.Crouch)))
                {
                    continue;
                }

                var past = feet + normal * (edge + 0.25f);
                StartClimb(new[] { feet, new Vector3(past.x, feet.y + 0.3f, past.z), new Vector3(past.x, to.y + 0.3f, past.z), to }, 0.9f + (to.y - feet.y) * 0.2f);

                return true;
            }

            return false;
        }

        void StartClimb(Vector3[] path, float duration)
        {
            OnWall = false;
            climbing = true;
            climbPath = path;
            climbTime = 0;
            climbDuration = duration;
            Velocity = Vector3.zero;
            Stance = Stance.Stand;
            ApplyCapsule();
            var last = path[path.Length - 1] - path[0];

            if (new Vector2(last.x, last.z).sqrMagnitude > 0.01f)
            {
                Facing = Mathf.Atan2(last.x, last.z);
            }
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0;

            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        // ── Giant trees ───────────────────────────────────────────────

        /// <summary>Takes hold of a tree's trunk where the body stands before it.</summary>
        public bool GrabTree(ClimbTree tree)
        {
            if (tree == null || climbing || Stance == Stance.Crawl)
            {
                return false;
            }

            var local = transform.position - tree.Foot;
            TreeAngle = Mathf.Atan2(local.x, local.z);
            TreeHeight = Mathf.Clamp(local.y + 0.15f, 0.2f, tree.ClimbHeight);
            Tree = tree;
            Sitting = false;
            Stance = Stance.Stand;
            ApplyCapsule();
            Velocity = Vector3.zero;
            HoldTree();

            return true;
        }

        /// <summary>Lets go of the trunk: a jump away from it, or a drop at its foot.</summary>
        public void LeaveTree(bool jump)
        {
            if (Tree == null)
            {
                return;
            }

            var away = new Vector3(Mathf.Sin(TreeAngle), 0, Mathf.Cos(TreeAngle));
            Tree = null;
            Velocity = jump ? away * 4.5f + Vector3.up * 5 : Vector3.zero;
            Facing = Mathf.Atan2(away.x, away.z);
        }

        /// <summary>
        /// A step of climbing: `up` (−1…1) up or down the trunk, `around` (−1…1) round it,
        /// a jump away from it. At its foot, climbing down puts the feet back on the ground.
        /// </summary>
        public void ClimbTick(float dt, float up, float around, bool jump)
        {
            if (Tree == null)
            {
                return;
            }

            if (jump)
            {
                LeaveTree(true);

                return;
            }

            var radius = Tree.RadiusAt(TreeHeight);
            TreeHeight = Mathf.Clamp(TreeHeight + up * 1.4f * dt, 0, Tree.ClimbHeight);
            TreeAngle += around * 1.2f / Mathf.Max(0.5f, radius) * dt;
            ClimbPhase += (Mathf.Abs(up) + Mathf.Abs(around) * 0.7f) * dt * 1.3f;

            if (TreeHeight <= 0.01f && up < 0)
            {
                LeaveTree(false);
                PlaceAt(transform.position);

                return;
            }

            HoldTree();
        }

        void HoldTree()
        {
            controller.enabled = false;
            transform.position = Tree.Surface(TreeAngle, TreeHeight, Radius + 0.1f);
            controller.enabled = true;
            Facing = TreeAngle + Mathf.PI;
            Grounded = false;
            Velocity = Vector3.zero;
        }

        /// <summary>Steps from the trunk onto the branch beside the hands, if there is one: standing on it, facing out along it.</summary>
        public bool StepOntoBranch()
        {
            var branch = Tree != null ? Tree.BranchAt(TreeAngle, TreeHeight + 0.4f) : null;

            if (branch == null)
            {
                return false;
            }

            Tree = null;
            var top = branch.transform.TransformPoint(new Vector3(0, branch.RootThickness * 0.5f + 0.08f, 1.1f));
            controller.enabled = false;
            transform.position = top;
            controller.enabled = true;
            var along = branch.transform.forward;
            Facing = Mathf.Atan2(along.x, along.z);
            Velocity = Vector3.zero;
            Grounded = true;

            return true;
        }

        // ── Sitting ───────────────────────────────────────────────────

        /// <summary>Sits down where the body stands: on an edge (a branch, a wall, a ledge) with the legs over it, or on the ground.</summary>
        public bool SitDown()
        {
            if (!Free || !Grounded || HorizontalSpeed > 0.6f)
            {
                return false;
            }

            var forward = new Vector3(Mathf.Sin(Facing), 0, Mathf.Cos(Facing));
            var feet = transform.position;
            // An edge: nothing to stand on just ahead, for a good way down.
            SittingOnEdge = !SurfaceBelow(feet + forward * 0.5f + Vector3.up * 0.3f, 1.1f, out _, out _);
            Sitting = true;
            Velocity = Vector3.zero;
            Stance = Stance.Stand;
            ApplyCapsule();

            return true;
        }

        public void StandUp()
        {
            Sitting = false;
        }

        /// <summary>Changes stance; false when there is no room to stand taller.</summary>
        public bool SetStance(Stance stance)
        {
            if (!Free)
            {
                return false;
            }

            if (HeightOf(stance) > Height && Blocked(transform.position, HeightOf(stance)))
            {
                return false;
            }

            Stance = stance;
            ApplyCapsule();

            return true;
        }

        /// <summary>
        /// Starts climbing onto a ledge in the given direction, if there is one
        /// within reach. In the air a lower ledge is enough (a grab).
        /// </summary>
        public bool TryClimb(float dirX, float dirZ)
        {
            if (climbing || OnWall || Stance == Stance.Crawl)
            {
                return false;
            }

            var length = Mathf.Sqrt(dirX * dirX + dirZ * dirZ);

            if (length < 1e-3f)
            {
                return false;
            }

            dirX /= length;
            dirZ /= length;

            var feet = transform.position;
            var min = Grounded ? ClimbMin : 0.25f;

            foreach (var reach in new[] { Radius + 0.2f, Radius + 0.45f })
            {
                var x = feet.x + dirX * reach;
                var z = feet.z + dirZ * reach;
                var from = new Vector3(x, feet.y + ClimbMax + 0.05f, z);

                if (!SurfaceBelow(from, ClimbMax + 0.05f - min + 0.01f, out var top, out var normal))
                {
                    continue;
                }

                var rise = top - feet.y;

                if (rise < min || rise > ClimbMax || normal.y < Walkable)
                {
                    continue;
                }

                var toX = x + dirX * 0.3f;
                var toZ = z + dirZ * 0.3f;
                var landingTop = SurfaceBelow(new Vector3(toX, feet.y + ClimbMax + 0.05f, toZ), ClimbMax + 1, out var t, out _)
                    ? t
                    : top;
                var to = new Vector3(toX, Mathf.Max(top, landingTop), toZ);

                if (Blocked(to, HeightOf(Stance.Crouch)))
                {
                    continue;
                }

                // Up the face first, then over the top.
                StartClimb(new[] { feet, new Vector3(feet.x, to.y, feet.z), to }, 0.4f + rise * 0.3f);
                Facing = Mathf.Atan2(dirX, dirZ);

                return true;
            }

            return false;
        }

        public void Tick(float dt, MotorInput input)
        {
            var before = Facing;

            if (Tree != null)
            {
                return;
            }

            if (Sitting || OnWall)
            {
                Velocity = Vector3.zero;
                TurnRate = 0;

                return;
            }

            if (climbing)
            {
                UpdateClimb(dt);
            }
            else
            {
                if (input.jump)
                {
                    jumpBuffered = JumpBuffer;
                }

                // Every frame moves the body by the frame's own time, cut into steps of at most
                // 1/120 s: no frame without a step (which, at high frame rates, made it stutter),
                // and steps small enough to stay steady.
                var time = Mathf.Min(dt, Step * MaxSteps);
                var steps = Mathf.Max(1, Mathf.CeilToInt(time / Step - 0.001f));

                for (var k = 0; k < steps; k++)
                {
                    StepOnce(input, time / steps);
                }

                if (input.moveX != 0 || input.moveZ != 0)
                {
                    var target = Mathf.Atan2(input.moveX, input.moveZ);
                    var turn = WrapAngle(target - Facing);
                    var rate = Stance == Stance.Crawl ? 6 : 11;
                    Facing += turn * (1 - Mathf.Exp(-rate * dt));
                }

                // Running into a ledge in mid-air grabs it; into a wall, takes hold of it.
                if (!Grounded && (input.moveX != 0 || input.moveZ != 0))
                {
                    if (!(Velocity.y < 3 && TryClimb(input.moveX, input.moveZ)) && Velocity.y < 6 && sinceGrounded > 0.15f)
                    {
                        GrabWall(input.moveX, input.moveZ);
                    }
                }
            }

            if (dt > 0)
            {
                var turned = WrapAngle(Facing - before);
                var speed = HorizontalSpeed;
                var smoothing = 1 - Mathf.Exp(-10 * dt);
                TurnRate += (turned / dt - TurnRate) * smoothing;
                Acceleration += ((speed - lastSpeed) / dt - Acceleration) * smoothing;
                lastSpeed = speed;
            }
        }

        void UpdateClimb(float dt)
        {
            climbTime += dt;
            var progress = Mathf.Min(1, climbTime / climbDuration);
            static float Ease(float t) => t * t * (3 - 2 * t);

            // Along the path, each stretch eased: the first (the climb up the face) takes most of the time.
            var stretches = climbPath.Length - 1;
            var first = stretches == 2 ? 0.65f : 1f / stretches;
            int index;
            float within;

            if (progress < first)
            {
                index = 0;
                within = progress / first;
            }
            else
            {
                var rest = (progress - first) / (1 - first) * (stretches - 1);
                index = Mathf.Min(stretches - 1, 1 + Mathf.FloorToInt(rest));
                within = progress >= 1 ? 1 : rest - (index - 1);
            }

            var position = Vector3.Lerp(climbPath[index], climbPath[index + 1], Ease(within));

            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;

            if (progress >= 1)
            {
                climbing = false;
                Grounded = true;
            }
        }

        void StepOnce(MotorInput input, float h)
        {
            var wasGrounded = Grounded;

            sinceGrounded = wasGrounded ? 0 : sinceGrounded + h;
            jumpBuffered = Mathf.Max(0, jumpBuffered - h);

            var (walk, run) = SpeedOf(Stance);
            var speed = (input.sprint ? run : walk) * input.speedScale;
            var wishX = input.moveX * speed;
            var wishZ = input.moveZ * speed;
            var braking = wishX * Velocity.x + wishZ * Velocity.z <= 0 ||
                          Mathf.Sqrt(wishX * wishX + wishZ * wishZ) < HorizontalSpeed;
            var acceleration = (wasGrounded ? (braking ? GroundBraking : GroundAcceleration) : AirAcceleration) * h;
            var dx = wishX - Velocity.x;
            var dz = wishZ - Velocity.z;
            var change = Mathf.Sqrt(dx * dx + dz * dz);
            var scale = change > acceleration ? acceleration / change : 1;
            Velocity.x += dx * scale;
            Velocity.z += dz * scale;

            var jumped = false;

            if (jumpBuffered > 0 && sinceGrounded <= CoyoteTime && Stance != Stance.Crawl)
            {
                Velocity.y = JumpSpeed * Mathf.Sqrt(Mathf.Max(0.1f, JumpScale)) * (Stance == Stance.Crouch ? 0.75f : 1);
                jumpBuffered = 0;
                sinceGrounded = CoyoteTime + 1;
                jumped = true;
            }

            Velocity.y -= Gravity * h;
            var fallSpeed = -Velocity.y;

            // The controller only steps up while it thinks it stands; running up stairs it is off the ground for
            // a moment after each step, so the moment's grace (as for a jump) counts as standing.
            standing = sinceGrounded <= CoyoteTime && !jumped;
            controller.stepOffset = standing ? StepUp : 0;

            hitWalkable = false;
            groundCollider = null;
            var flags = controller.Move(Velocity * h);

            Grounded = hitWalkable;

            // Still against something low ahead: a step, gone up onto (the controller's own stepping misses some).
            if (standing && (flags & CollisionFlags.Sides) != 0 && StepUpOnto(input.moveX, input.moveZ))
            {
                Grounded = true;
            }

            if (Grounded)
            {
                GroundNormal = walkableNormal;

                if (Velocity.y < 0)
                {
                    Velocity.y = 0;
                }
            }

            if (!Grounded && wasGrounded && !jumped && Velocity.y <= 0)
            {
                SnapToGround();
            }

            if (Grounded && !wasGrounded)
            {
                LandingSpeed = Mathf.Max(LandingSpeed, fallSpeed);
                Landed?.Invoke(fallSpeed);
            }

            // On a branch: it bears the weight (a thin stretch may give).
            Branch = Grounded && groundCollider != null ? groundCollider.GetComponentInParent<TreeBranch>() : null;

            if (Branch != null)
            {
                Branch.Bear(transform.position, h);
            }

            KeepInside();
        }

        /// <summary>
        /// Standing on ground that is flat enough; against steeper ground (or a
        /// wall) the velocity into it is taken away, so the body slides instead
        /// of climbing — but not against the face of a step (low, within a
        /// step's height above the feet, while standing): that is stepped up.
        /// </summary>
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var normal = hit.normal;

            if (normal.y >= Walkable && hit.point.y <= transform.position.y + Radius)
            {
                hitWalkable = true;
                walkableNormal = normal;
                groundCollider = hit.collider;

                return;
            }

            if (standing && hit.point.y < transform.position.y + StepUp && normal.y > -0.1f)
            {
                return;
            }

            var into = Vector3.Dot(Velocity, normal);

            if (into < 0)
            {
                Velocity -= normal * into;
            }
        }

        /// <summary>
        /// Goes up onto a step ahead (in the direction asked for): ground no higher than a step above the
        /// feet, flat enough to stand on, with room for the body there. True when it went up.
        /// </summary>
        bool StepUpOnto(float dirX, float dirZ)
        {
            var length = Mathf.Sqrt(dirX * dirX + dirZ * dirZ);

            if (length < 0.1f)
            {
                return false;
            }

            dirX /= length;
            dirZ /= length;
            var feet = transform.position;
            var ahead = new Vector3(feet.x + dirX * (Radius + 0.12f), feet.y + StepUp + 0.05f, feet.z + dirZ * (Radius + 0.12f));

            if (!SurfaceBelow(ahead, StepUp + 0.05f, out var top, out var normal))
            {
                return false;
            }

            var rise = top - feet.y;

            if (rise <= 0.02f || rise > StepUp || normal.y < Walkable)
            {
                return false;
            }

            var to = new Vector3(feet.x + dirX * 0.08f, top + 0.01f, feet.z + dirZ * 0.08f);

            if (Blocked(to, Height))
            {
                return false;
            }

            controller.enabled = false;
            transform.position = to;
            controller.enabled = true;
            walkableNormal = normal;

            if (Velocity.y < 0)
            {
                Velocity.y = 0;
            }

            return true;
        }

        /// <summary>Keeps the feet on the ground when walking down a slope or a step.</summary>
        void SnapToGround()
        {
            var feet = transform.position;

            if (!SurfaceBelow(feet + Vector3.up * 0.05f, Snap + 0.05f, out var top, out var normal) || normal.y < Walkable)
            {
                return;
            }

            controller.Move(Vector3.down * (feet.y - top));
            Grounded = true;
            GroundNormal = normal;
        }

        /// <summary>Keeps the body inside its location: a wall at the edges, a floor and a ceiling.</summary>
        void KeepInside()
        {
            var position = transform.position;
            var x = Mathf.Clamp(position.x, Bounds.min.x + Edge, Bounds.max.x - Edge);
            var y = Mathf.Clamp(position.y, Bounds.min.y, Bounds.max.y);
            var z = Mathf.Clamp(position.z, Bounds.min.z + Edge, Bounds.max.z - Edge);

            if (x != position.x) Velocity.x = 0;
            if (z != position.z) Velocity.z = 0;
            if (y < position.y) Velocity.y = Mathf.Min(0, Velocity.y);
            if (y > position.y) Velocity.y = Mathf.Max(0, Velocity.y);

            if (x != position.x || y != position.y || z != position.z)
            {
                controller.enabled = false;
                transform.position = new Vector3(x, y, z);
                controller.enabled = true;
            }
        }

        void ApplyCapsule()
        {
            var height = Height;
            controller.height = height;
            controller.center = new Vector3(0, height / 2 + controller.skinWidth, 0);
        }

        /// <summary>Whether a capsule of this height standing at `feet` would overlap something.</summary>
        bool Blocked(Vector3 feet, float height, float from = 0)
        {
            var bottom = feet + Vector3.up * (Mathf.Max(from, 0) + Radius + 0.05f);
            var top = feet + Vector3.up * (height - Radius);

            foreach (var hit in UnityEngine.Physics.OverlapCapsule(bottom, top, Radius * 0.95f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit != controller)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The body is in something solid (put there, or something was made round it).</summary>
        public bool Stuck => controller != null && controller.enabled && Blocked(transform.position, Height);

        static readonly RaycastHit[] spots = new RaycastHit[32];

        /// <summary>
        /// Where the body can stand at a point (x, z): the highest surface there one can stand on with room for the body
        /// above it — a roof, a terrace, the ground — looked for from `above` metres up; if there is none, the same
        /// a little way off, in rings round it. Null when nothing is found.
        /// </summary>
        public Vector3? SafeSpot(Vector3 at, float above = 120)
        {
            foreach (var ring in new[] { 0f, 1.5f, 3f, 5f, 8f })
            {
                for (var k = 0; k < (ring == 0 ? 1 : 8); k++)
                {
                    var angle = k * Mathf.PI / 4;
                    var x = at.x + Mathf.Cos(angle) * ring;
                    var z = at.z + Mathf.Sin(angle) * ring;
                    var count = UnityEngine.Physics.RaycastNonAlloc(new Vector3(x, at.y + above, z), Vector3.down, spots, above * 2 + 400, ~0, QueryTriggerInteraction.Ignore);
                    System.Array.Sort(spots, 0, count, HitsByDistance);

                    for (var i = 0; i < count; i++)
                    {
                        var hit = spots[i];

                        if (hit.collider == controller || hit.normal.y < Walkable)
                        {
                            continue;
                        }

                        var feet = hit.point + Vector3.up * 0.02f;

                        if (!Blocked(feet, HeightOf(Stance.Stand)))
                        {
                            return feet;
                        }
                    }
                }
            }

            return null;
        }

        static readonly System.Collections.Generic.IComparer<RaycastHit> HitsByDistance =
            System.Collections.Generic.Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        /// <summary>The first solid surface straight down from `from`, within `distance`.</summary>
        bool SurfaceBelow(Vector3 from, float distance, out float top, out Vector3 normal)
        {
            // RaycastAll is unordered: the highest hit that is not the body itself.
            var best = float.NegativeInfinity;
            normal = Vector3.up;

            foreach (var hit in UnityEngine.Physics.RaycastAll(from, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != controller && hit.point.y > best)
                {
                    best = hit.point.y;
                    normal = hit.normal;
                }
            }

            top = best;

            return !float.IsNegativeInfinity(best);
        }

        static float WrapAngle(float angle) => Mathf.Atan2(Mathf.Sin(angle), Mathf.Cos(angle));
    }
}
