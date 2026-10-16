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
    /// stand, crouch or crawl (a shorter capsule) and climb onto a ledge (a
    /// scripted move from the foot of the ledge onto its top). It never
    /// leaves its location's bounds. Swimming is not ported yet: the proving
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
        public bool Free => !climbing && Tree == null && !Sitting;

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
        Vector3 climbFrom;
        Vector3 climbTo;
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
            if (climbing || Stance == Stance.Crawl)
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

                climbing = true;
                climbFrom = feet;
                climbTo = to;
                climbTime = 0;
                climbDuration = 0.4f + rise * 0.3f;
                Facing = Mathf.Atan2(dirX, dirZ);
                Velocity = Vector3.zero;
                Stance = Stance.Stand;
                ApplyCapsule();

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

            if (Sitting)
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

                // Running into a ledge in mid-air grabs it.
                if (!Grounded && Velocity.y < 3 && (input.moveX != 0 || input.moveZ != 0))
                {
                    TryClimb(input.moveX, input.moveZ);
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

            Vector3 position;

            // Up the face first, then over the top.
            if (progress < 0.65f)
            {
                var up = Ease(progress / 0.65f);
                position = new Vector3(climbFrom.x, climbFrom.y + (climbTo.y - climbFrom.y) * up, climbFrom.z);
            }
            else
            {
                var over = Ease((progress - 0.65f) / 0.35f);
                position = new Vector3(
                    climbFrom.x + (climbTo.x - climbFrom.x) * over,
                    climbTo.y,
                    climbFrom.z + (climbTo.z - climbFrom.z) * over);
            }

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
                Velocity.y = JumpSpeed * (Stance == Stance.Crouch ? 0.75f : 1);
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
        bool Blocked(Vector3 feet, float height)
        {
            var bottom = feet + Vector3.up * (Radius + 0.05f);
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
