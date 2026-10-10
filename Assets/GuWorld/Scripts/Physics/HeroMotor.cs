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
        const float GroundAcceleration = 30;
        const float GroundBraking = 38;
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
        public Stance Stance { get; private set; } = Stance.Stand;

        /// <summary>The box the body is kept in.</summary>
        public Bounds Bounds = new Bounds(Vector3.zero, Vector3.one * 100000);

        public float Height => HeightOf(Stance);
        public float HorizontalSpeed => new Vector2(Velocity.x, Velocity.z).magnitude;
        /// <summary>0…1 through a climb, or null when not climbing.</summary>
        public float? ClimbProgress => climbing ? climbTime / climbDuration : null;
        public bool Free => !climbing;

        CharacterController controller;
        float accumulator;
        float sinceGrounded;
        float jumpBuffered;
        float lastSpeed;

        bool climbing;
        Vector3 climbFrom;
        Vector3 climbTo;
        float climbTime;
        float climbDuration;

        // Collected from the controller's hits during one Move.
        bool hitWalkable;
        Vector3 walkableNormal;

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
            accumulator = 0;
            climbing = false;
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

                accumulator = Mathf.Min(accumulator + dt, Step * MaxSteps);

                while (accumulator >= Step)
                {
                    StepOnce(input);
                    accumulator -= Step;
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
                accumulator = 0;
            }
        }

        void StepOnce(MotorInput input)
        {
            var wasGrounded = Grounded;

            sinceGrounded = wasGrounded ? 0 : sinceGrounded + Step;
            jumpBuffered = Mathf.Max(0, jumpBuffered - Step);

            var (walk, run) = SpeedOf(Stance);
            var speed = (input.sprint ? run : walk) * input.speedScale;
            var wishX = input.moveX * speed;
            var wishZ = input.moveZ * speed;
            var braking = wishX * Velocity.x + wishZ * Velocity.z <= 0 ||
                          Mathf.Sqrt(wishX * wishX + wishZ * wishZ) < HorizontalSpeed;
            var acceleration = (wasGrounded ? (braking ? GroundBraking : GroundAcceleration) : AirAcceleration) * Step;
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

            Velocity.y -= Gravity * Step;
            var fallSpeed = -Velocity.y;

            // The controller only steps up while it thinks it stands.
            controller.stepOffset = wasGrounded && !jumped ? StepUp : 0;

            hitWalkable = false;
            controller.Move(Velocity * Step);

            Grounded = hitWalkable;

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
            }

            KeepInside();
        }

        /// <summary>
        /// Standing on ground that is flat enough; against steeper ground (or a
        /// wall) the velocity into it is taken away, so the body slides instead
        /// of climbing.
        /// </summary>
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var normal = hit.normal;

            if (normal.y >= Walkable && hit.point.y <= transform.position.y + Radius)
            {
                hitWalkable = true;
                walkableNormal = normal;

                return;
            }

            var into = Vector3.Dot(Velocity, normal);

            if (into < 0)
            {
                Velocity -= normal * into;
            }
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
