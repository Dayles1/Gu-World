using GuWorld.Body;
using GuWorld.Combat;
using GuWorld.Physics;
using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>
    /// The hero's picture (ported from personView in client/render/entity-views.ts):
    /// a PersonFigure for the hero's look — fifteen, black-haired, in their own
    /// colours — posed from what the body does, its feet planted on whatever is
    /// under them.
    ///
    /// The Mannequin works in the web's numbers, where turning toward the
    /// figure's left is positive; Unity's world is the web's mirrored, so the
    /// turn rate and the look's yaw change sign on the way in.
    /// </summary>
    [RequireComponent(typeof(HeroMotor))]
    public class HeroView : MonoBehaviour
    {
        /// <summary>Heights at fifteen, m: a tall boy and a tall girl.</summary>
        public static float HeightOf(Gender gender) => gender == Gender.Female ? 1.68f : 1.78f;

        public HumanBody.Parts Parts;
        public ThirdPersonCamera Camera;

        HeroMotor body;
        CharacterController controller;
        PersonFigure figure;
        /// <summary>Seconds into a throw (a moonblade's cut); below 0 none.</summary>
        float throwing = -1;
        const float ThrowTime = 0.32f;

        /// <summary>Starts the right hand's cut (the figure eases into it and out again).</summary>
        public void Throw() => throwing = 0;

        /// <summary>The look the hero is drawn with.</summary>
        public Look Look { get; private set; } = Look.Default(Gender.Male);

        public bool HasBody => figure != null && figure.HasBody;

        /// <summary>The hero's figure (its joints carry the body's parts).</summary>
        public PersonFigure Figure => figure;

        /// <summary>The figure's size against the models' (1 = 1.8 m).</summary>
        public float Scale => HeightOf(Look.Gender) / 1.8f;

        void Awake()
        {
            body = GetComponent<HeroMotor>();
            controller = GetComponent<CharacterController>();
        }

        /// <summary>Builds the hero's body for a look (again, when the look changes).</summary>
        public void Wear(Look look)
        {
            Look = look;
            figure?.Destroy();
            figure = new PersonFigure(transform, Parts, look, HumanBody.Build.ForHero(look), HeightOf(look.Gender));
            HitZones.Build(figure.Rig, GetComponent<IStrikable>());
            Pose(0.5f);
        }

        void LateUpdate()
        {
            // Paused (Time.timeScale 0): the figure holds still.
            if (Time.deltaTime > 0)
            {
                Pose(Time.deltaTime);
            }
        }

        /// <summary>Poses the figure for `dt` seconds of what the body does, and dresses the body in it.</summary>
        public void Pose(float dt)
        {
            if (figure == null || dt <= 0)
            {
                return;
            }

            var (walk, run) = HeroMotor.SpeedOf(body.Stance);
            var lookYaw = 0f;
            var lookPitch = 0f;

            if (Camera != null && Camera.isActiveAndEnabled && Camera.Following)
            {
                lookYaw = -WrapAngle(Camera.Facing - body.Facing);
                lookPitch = Camera.Pitch;
            }

            if (throwing >= 0)
            {
                throwing += dt;
                figure.Rig.Throw = throwing < ThrowTime ? throwing / ThrowTime : -1;

                if (throwing >= ThrowTime)
                {
                    throwing = -1;
                }
            }

            // The figure is drawn smaller than the physics' metres: its speeds are its own.
            var scale = Scale;

            figure.Pose(dt, new MotionState
            {
                speed = body.HorizontalSpeed / scale,
                walkSpeed = walk / scale,
                runSpeed = run / scale,
                stance = body.Stance,
                grounded = body.Grounded,
                verticalSpeed = body.Velocity.y,
                turnRate = -body.TurnRate,
                acceleration = body.Acceleration,
                swimming = false,
                climb = body.ClimbProgress,
                lookYaw = lookYaw,
                lookPitch = lookPitch,
                ground = body.Grounded ? GroundAt : null,
            }, body.LandingSpeed);
            body.LandingSpeed = 0;
        }

        /// <summary>The height of whatever is under a point near the feet (not the hero), in the figure's units.</summary>
        float? GroundAt(Vector3 point)
        {
            var best = float.NegativeInfinity;

            foreach (var hit in UnityEngine.Physics.RaycastAll(point + Vector3.up * 0.6f, Vector3.down, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != controller && hit.point.y > best)
                {
                    best = hit.point.y;
                }
            }

            if (float.IsNegativeInfinity(best))
            {
                return null;
            }

            // The Mannequin compares it with the feet in its own (scaled) units.
            var feet = transform.position.y;

            return feet + (best - feet) / Scale;
        }

        static float WrapAngle(float angle) => Mathf.Atan2(Mathf.Sin(angle), Mathf.Cos(angle));
    }
}
