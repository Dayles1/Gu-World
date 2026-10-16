using GuWorld.Physics;
using GuWorld.Render;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GuWorld.Systems
{
    /// <summary>
    /// The hero, each frame: turns the held keys (relative to where the camera
    /// looks) into movement for the body's physics, and the mouse into the
    /// camera's turn (ported from client/systems/player.ts and client/input.ts).
    ///
    /// The mouse turns the camera only while the cursor is locked to the
    /// game: a click locks it, Esc frees it — and while it is free the game
    /// is paused. Jumping (or climbing, when there is a ledge ahead),
    /// crouching and crawling come in as one-off keys. Should the body's
    /// numbers ever break, it is put back on the spawn rather than the broken
    /// numbers kept.
    /// </summary>
    [RequireComponent(typeof(HeroMotor))]
    public class PlayerController : MonoBehaviour
    {
        /// <summary>The eyes' height for the camera, by stance.</summary>
        static float EyesOf(Stance stance) => stance switch
        {
            Stance.Crouch => 1.05f,
            Stance.Crawl => 0.5f,
            _ => 1.55f,
        };

        public ThirdPersonCamera Camera;
        public Vector3 Spawn;
        public float SpawnYaw;

        /// <summary>The eyes' heights are a grown-up's: this scales them to the hero's height.</summary>
        public float EyeScale = 1;

        /// <summary>For checking by eye (`-screenshot … -walk`): walks forward, sprinting when 2, without the keys.</summary>
        public int AutoWalk;

        /// <summary>Movement scaled by this (slower while drawing in a stone).</summary>
        public float SpeedScale = 1;
        /// <summary>No running (while drawing in a stone).</summary>
        public bool NoSprint;
        /// <summary>The body turned the way the camera looks (while a skill is aimed; always from the eyes).</summary>
        public bool FaceView;

        /// <summary>Playing (the cursor is locked): held keys count.</summary>
        public bool Active => Cursor.lockState == CursorLockMode.Locked;

        HeroMotor body;
        bool jumpQueued;
        bool cameraPlaced;

        void Awake()
        {
            body = GetComponent<HeroMotor>();
        }

        void Start()
        {
            Respawn();
        }

        public void Respawn()
        {
            body.PlaceAt(Spawn);
            body.Facing = SpawnYaw;

            if (Camera != null)
            {
                // Behind the hero, looking the way they face.
                Camera.Yaw = SpawnYaw + Mathf.PI;
            }

            cameraPlaced = false;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard == null || mouse == null)
            {
                return;
            }

            // Play starts from the menu's buttons (GuWorldGame); Esc stops it.
            if (Active && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // On a giant tree's trunk: W/S climb, A/D go round it, Space jumps off.
            if (body.Tree != null)
            {
                if (Active)
                {
                    static float Key(bool positive, bool negative) => (positive ? 1 : 0) - (negative ? 1 : 0);
                    var up = Key(keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed, keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
                    var around = Key(keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed, keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
                    body.ClimbTick(Time.deltaTime, up, around, keyboard.spaceKey.wasPressedThisFrame);
                    var delta = mouse.delta.ReadValue();
                    var scroll = mouse.scroll.ReadValue().y;
                    Camera.Look(delta.x, delta.y, -Mathf.Sign(scroll) * (scroll != 0 ? 1 : 0));
                }

                transform.rotation = Quaternion.Euler(0, body.Facing * Mathf.Rad2Deg, 0);

                return;
            }

            if (Active)
            {
                // Sitting: X again, or any step, gets up.
                if (keyboard.xKey.wasPressedThisFrame)
                {
                    if (body.Sitting) body.StandUp();
                    else body.SitDown();
                }

                if (body.Sitting && Direction(keyboard).sqrMagnitude > 0.01f)
                {
                    body.StandUp();
                }

                if (keyboard.spaceKey.wasPressedThisFrame && !body.Sitting) Jump();
                if (keyboard.cKey.wasPressedThisFrame || keyboard.leftCtrlKey.wasPressedThisFrame) ToggleStance(Stance.Crouch);
                if (keyboard.zKey.wasPressedThisFrame) ToggleStance(Stance.Crawl);

                var delta = mouse.delta.ReadValue();
                var scroll = mouse.scroll.ReadValue().y;
                Camera.Look(delta.x, delta.y, -Mathf.Sign(scroll) * (scroll != 0 ? 1 : 0));
            }

            var dt = Time.deltaTime;
            var move = Direction(keyboard);

            body.Tick(dt, new MotorInput
            {
                moveX = move.x,
                moveZ = move.y,
                sprint = !NoSprint && (AutoWalk == 2 || (Active && Sprinting(keyboard))),
                jump = jumpQueued,
                speedScale = SpeedScale,
            });
            jumpQueued = false;

            // Aiming, or seen from the eyes: the body faces where the camera looks (walking sideways or back).
            if ((FaceView || (Camera != null && Camera.FirstPerson)) && Camera != null && body.Free)
            {
                body.Facing = Camera.Facing;
            }

            var position = transform.position;

            if (!IsFinite(position) || !float.IsFinite(body.Facing))
            {
                Debug.LogError("GU World: the hero's body broke: back to the spawn");
                Respawn();
                position = transform.position;
            }

            transform.rotation = Quaternion.Euler(0, body.Facing * Mathf.Rad2Deg, 0);
        }

        void LateUpdate()
        {
            if (Camera == null || !Camera.Following)
            {
                return;
            }

            var running = body.HorizontalSpeed > HeroMotor.SpeedOf(body.Stance).walk + 0.5f;
            var eyes = body.Sitting ? (body.SittingOnEdge ? 0.95f : 0.85f) : EyesOf(body.Stance);
            Camera.Follow(Time.deltaTime, transform.position, eyes * EyeScale, running, !cameraPlaced);
            cameraPlaced = true;
        }

        /// <summary>Puts the camera straight behind the hero, looking the way they face.</summary>
        public void PlaceCamera()
        {
            if (Camera != null)
            {
                Camera.Yaw = body.Facing + Mathf.PI;
            }

            cameraPlaced = false;
        }

        /// <summary>Jump — or climb, when there is a ledge ahead to climb onto.</summary>
        void Jump()
        {
            var direction = Direction(Keyboard.current);
            var ahead = direction.magnitude > 0.1f
                ? direction
                : new Vector2(Mathf.Sin(body.Facing), Mathf.Cos(body.Facing));

            if (!body.Grounded || !body.TryClimb(ahead.x, ahead.y))
            {
                jumpQueued = true;
            }
        }

        /// <summary>Shift held.</summary>
        public static bool Sprinting(Keyboard keyboard) => keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

        /// <summary>Crouch or crawl, or stand up again from it.</summary>
        void ToggleStance(Stance stance)
        {
            body.SetStance(body.Stance == stance ? Stance.Stand : stance);
        }

        /// <summary>Where the keys say to go, on the ground (X, Z), at most length 1.</summary>
        Vector2 Direction(Keyboard keyboard)
        {
            if (AutoWalk > 0 && Camera != null)
            {
                return Camera.GroundAxes().forward;
            }

            if (!Active || Camera == null)
            {
                return Vector2.zero;
            }

            static float Axis(bool positive, bool negative) => (positive ? 1 : 0) - (negative ? 1 : 0);

            var forward = Axis(
                keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
            var strafe = Axis(
                keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed,
                keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
            var (forwardAxis, rightAxis) = Camera.GroundAxes();
            var move = forwardAxis * forward + rightAxis * strafe;

            return move.magnitude > 1 ? move.normalized : move;
        }

        static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
