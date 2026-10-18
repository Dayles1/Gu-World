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
        /// <summary>
        /// The figure's height setting for the hero, m. On the grown-ups' bodies: a tall boy and a tall girl
        /// of fifteen. The teenagers' own bodies have shorter legs for their height, so the figure (fitted by
        /// the hips) stands taller than its setting: these make them 1.70 and 1.60 m.
        /// </summary>
        public static float HeightOf(Gender gender) => TeenBodies
            ? (gender == Gender.Female ? 1.523f : 1.658f)
            : (gender == Gender.Female ? 1.68f : 1.78f);

        /// <summary>The hero's height for a look: the gender's, made a little shorter or taller.</summary>
        public static float HeightOf(Look look) => HeightOf(look.Gender) * look.HeightFactor;

        /// <summary>The hero is drawn on a teenager's body (HumanBody.Parts' teen models are there).</summary>
        public static bool TeenBodies;

        public HumanBody.Parts Parts;
        public ThirdPersonCamera Camera;

        HeroMotor body;
        CharacterController controller;
        Melee melee;
        PersonFigure figure;
        /// <summary>Seconds into a throw (a moonblade's cut); below 0 none.</summary>
        float throwing = -1;
        const float ThrowTime = 0.32f;

        /// <summary>Streaks of light behind the fists and the feet as they strike (made with the figure).</summary>
        TrailRenderer fistL, fistR, footL, footR;

        /// <summary>Starts the right hand's cut (the figure eases into it and out again).</summary>
        public void Throw() => throwing = 0;

        /// <summary>The look the hero is drawn with.</summary>
        public Look Look { get; private set; } = Look.Default(Gender.Male);

        public bool HasBody => figure != null && figure.HasBody;

        /// <summary>The hero's figure (its joints carry the body's parts).</summary>
        public PersonFigure Figure => figure;

        /// <summary>The figure's size against the models' (1 = 1.8 m).</summary>
        public float Scale => HeightOf(Look) / 1.8f;

        bool eyes;

        /// <summary>Seen from the hero's own eyes: the body casts its shadow but is not drawn (it would fill the view).</summary>
        public bool FirstPerson
        {
            get => eyes;
            set
            {
                eyes = value;
                ShowBody();
            }
        }

        void ShowBody()
        {
            if (figure == null)
            {
                return;
            }

            foreach (var renderer in figure.Root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = eyes ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        void Awake()
        {
            body = GetComponent<HeroMotor>();
            controller = GetComponent<CharacterController>();
            melee = GetComponent<Melee>();
        }

        /// <summary>Builds the hero's body for a look (again, when the look changes).</summary>
        public void Wear(Look look)
        {
            Look = look;
            figure?.Destroy();
            figure = new PersonFigure(transform, Parts, look, HumanBody.Build.ForHero(look), HeightOf(look));
            HitZones.Build(figure.Rig, GetComponent<IStrikable>());
            MakeTrails();
            ShowBody();
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

            // A thrown skill aimed (its key held): the hand drawn back, ready.
            var aiming = Camera != null && Camera.Aiming;
            figure.Rig.Aim = Mathf.MoveTowards(figure.Rig.Aim, aiming ? 1 : 0, dt * (aiming ? 7 : 4));

            // Fists: a blow under way, or the guard — up while guarding, and a while after the last blow.
            if (melee != null)
            {
                figure.Rig.Blow = melee.Current ?? Blow.Jab;
                figure.Rig.BlowPhase = melee.Current.HasValue ? melee.Phase : -1;
                // The fists ready: a fighter's stance (the guard part way up) all the while.
                var guard = melee.Guarding || melee.Current.HasValue ? 1 : melee.SinceBlow < 1.4f ? 0.85f : melee.Ready ? 0.7f : 0;
                figure.Rig.Guard = Mathf.MoveTowards(figure.Rig.Guard, guard, dt * (guard > figure.Rig.Guard ? 8 : 2.5f));
            }

            Streak();

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
                treeClimb = body.Tree != null || body.OnWall ? body.ClimbPhase : null,
                sit = body.Sitting ? (body.SittingOnEdge ? 2 : 1) : 0,
                lookYaw = lookYaw,
                lookPitch = lookPitch,
                ground = body.Grounded && !body.Sitting ? GroundAt : null,
            }, body.LandingSpeed);
            body.LandingSpeed = 0;
        }

        /// <summary>The streaks behind the fists (the forearms' ends) and the feet (the toes), on the figure's joints.</summary>
        void MakeTrails()
        {
            var rig = figure.Rig;
            fistL = Trail(rig.ElbowL, new Vector3(0, -0.27f, 0));
            fistR = Trail(rig.ElbowR, new Vector3(0, -0.27f, 0));
            footL = Trail(rig.AnkleL, new Vector3(0, -0.05f, 0.12f));
            footR = Trail(rig.AnkleR, new Vector3(0, -0.05f, 0.12f));
        }

        static Material streak;

        static TrailRenderer Trail(Transform joint, Vector3 at)
        {
            var go = new GameObject("Blow Trail");
            go.transform.SetParent(joint, false);
            go.transform.localPosition = at;
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.16f;
            trail.minVertexDistance = 0.04f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 0.11f), new Keyframe(1, 0));
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0), new GradientColorKey(new Color(1f, 0.7f, 0.4f), 1) },
                alphaKeys = new[] { new GradientAlphaKey(0.75f, 0), new GradientAlphaKey(0, 1) },
            };
            streak ??= GuWorld.Cultivation.GuSkills.Glow(new Color(1f, 0.9f, 0.75f) * 1.1f);
            trail.sharedMaterial = streak;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;

            return trail;
        }

        /// <summary>The limb a blow is thrown with streaks while it goes out (from the end of the wind-up to just after it lands).</summary>
        void Streak()
        {
            if (fistL == null)
            {
                return;
            }

            var blow = melee != null ? melee.Current : null;
            var active = false;

            if (blow.HasValue)
            {
                var spec = Melee.Of(blow.Value);
                active = melee.Phase > spec.LandsAt - 0.26f && melee.Phase < spec.LandsAt + 0.12f;
            }

            fistL.emitting = active && blow is Blow.Jab or Blow.Hook;
            fistR.emitting = active && blow is Blow.Cross or Blow.Heavy;
            footR.emitting = active && blow is Blow.Kick or Blow.Spin;
            footL.emitting = false;
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
