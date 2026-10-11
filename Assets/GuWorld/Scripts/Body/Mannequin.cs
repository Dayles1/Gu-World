using System;
using GuWorld.Physics;
using UnityEngine;

namespace GuWorld.Body
{
    /// <summary>What the body is doing, for the figure (as client/body/mannequin.ts MotionState).</summary>
    public struct MotionState
    {
        /// <summary>Ground speed, m/s, and the walk/run speeds of the stance.</summary>
        public float speed, walkSpeed, runSpeed;
        public Stance stance;
        public bool grounded;
        public float verticalSpeed;
        /// <summary>rad/s, positive turning toward the figure's left (the web's +X).</summary>
        public float turnRate;
        /// <summary>m/s².</summary>
        public float acceleration;
        public bool swimming;
        /// <summary>0…1 through a climb, or null.</summary>
        public float? climb;
        /// <summary>Where the camera looks relative to the body (radians; positive toward the figure's left, as on the web).</summary>
        public float lookYaw, lookPitch;
        /// <summary>Ground height under a world point, to plant the feet on; null in the air.</summary>
        public Func<Vector3, float?> ground;
    }

    /// <summary>
    /// A person's joints, posed every frame (ported from client/body/mannequin.ts).
    /// It has no animation clips: every frame a target pose is worked out from
    /// what the body does (walk, run, crouch, crawl, jump, climb) and the
    /// figure eases into it. While on its feet the legs are placed by inverse
    /// kinematics: each foot follows a step path that matches the ground speed
    /// (no sliding) and lands on the real ground under it, so the knees bend on
    /// slopes and steps. The joints are invisible; the realistic body
    /// (HumanBody) copies their turns onto its bones.
    ///
    /// The pose is worked out in the web's numbers. Unity's world is the web's
    /// mirrored left to right (X negated — as glTFast brings the models in),
    /// so each joint's position has X negated and its turns about Y and Z
    /// change sign when they are set.
    /// </summary>
    public class Mannequin
    {
        public const float Hips = 0.98f;
        /// <summary>Hip joint below the hips' origin.</summary>
        public const float HipJoint = 0.02f;
        const float Thigh = 0.44f;
        const float Shin = 0.45f;
        /// <summary>Ankle above the sole.</summary>
        const float Ankle = 0.07f;
        const float LegSpread = 0.094f;

        enum P
        {
            BodyY, BodyZ, Pitch, Roll, HipsYaw, HipsRoll, Lean, Side, Twist, HeadX, HeadY, Ik,
            FootZL, FootZR, LiftL, LiftR, ThighL, ThighR, KneeL, KneeR, FootL, FootR,
            SpreadL, SpreadR, TurnOut, ArmL, ArmR, ArmOutL, ArmOutR, ElbowL, ElbowR,
            Count,
        }

        static readonly float[] Rest = MakeRest();

        static float[] MakeRest()
        {
            var rest = new float[(int)P.Count];
            rest[(int)P.Ik] = 1;
            rest[(int)P.ArmOutL] = 0.08f;
            rest[(int)P.ArmOutR] = 0.08f;
            rest[(int)P.ElbowL] = -0.12f;
            rest[(int)P.ElbowR] = -0.12f;

            return rest;
        }

        public readonly Transform Root;
        public readonly Transform Body, HipsJoint, Spine, Neck, Head;
        public readonly Transform ShoulderL, ShoulderR, ElbowL, ElbowR;
        public readonly Transform HipL, HipR, KneeL, KneeR, AnkleL, AnkleR;

        /// <summary>How bent the back is besides (an old person's stoop), radians.</summary>
        public float Stoop;

        /// <summary>How far through a throw with the right hand (a moonblade's cut), 0…1; below 0 none.</summary>
        public float Throw = -1;

        /// <summary>A foot came down (loudness 0…1).</summary>
        public Action<float> OnStep = _ => { };

        readonly float[] pose = (float[])Rest.Clone();
        readonly float[] target = new float[(int)P.Count];
        float stepPhase;
        float crawlPhase;
        float time;
        float squash;
        float groundL;
        float groundR;

        /// <param name="root">The figure's origin: between the feet, facing +Z.</param>
        public Mannequin(Transform root)
        {
            Root = root;
            Body = Joint("Joint Body", root, 0, Hips);
            HipsJoint = Joint("Joint Hips", Body, 0, 0);
            Spine = Joint("Joint Spine", HipsJoint, 0, 0.06f);
            Neck = Joint("Joint Neck", Spine, 0, 0.5f);
            Head = Joint("Joint Head", Neck, 0, 0.08f);
            // Side 1 is the figure's left: +X on the web, -X here.
            ShoulderL = Joint("Joint Shoulder L", Spine, 0.215f, 0.43f);
            ShoulderR = Joint("Joint Shoulder R", Spine, -0.215f, 0.43f);
            ElbowL = Joint("Joint Elbow L", ShoulderL, 0, -0.28f);
            ElbowR = Joint("Joint Elbow R", ShoulderR, 0, -0.28f);
            HipL = Joint("Joint Hip L", HipsJoint, LegSpread, -HipJoint);
            HipR = Joint("Joint Hip R", HipsJoint, -LegSpread, -HipJoint);
            KneeL = Joint("Joint Knee L", HipL, 0, -Thigh);
            KneeR = Joint("Joint Knee R", HipR, 0, -Thigh);
            AnkleL = Joint("Joint Ankle L", KneeL, 0, -Shin);
            AnkleR = Joint("Joint Ankle R", KneeR, 0, -Shin);
        }

        /// <summary>A joint at (x, y) in the web's numbers.</summary>
        static Transform Joint(string name, Transform parent, float x, float y)
        {
            // Named apart from the model's bones ("Head", "pelvis"…), which live beside them.
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = new Vector3(-x, y, 0);

            return joint;
        }

        /// <summary>The web's Euler turn (order XYZ), mirrored into Unity's world.</summary>
        static Quaternion Xyz(float x, float y, float z) =>
            Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right) *
            Quaternion.AngleAxis(-y * Mathf.Rad2Deg, Vector3.up) *
            Quaternion.AngleAxis(-z * Mathf.Rad2Deg, Vector3.forward);

        /// <summary>The web's Euler turn (order YXZ, the head's), mirrored.</summary>
        static Quaternion Yxz(float x, float y, float z) =>
            Quaternion.AngleAxis(-y * Mathf.Rad2Deg, Vector3.up) *
            Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right) *
            Quaternion.AngleAxis(-z * Mathf.Rad2Deg, Vector3.forward);

        static float Smooth(float t)
        {
            var x = Mathf.Clamp01(t);

            return x * x * (3 - 2 * x);
        }

        /// <summary>
        /// Two-bone leg IK in the leg's forward/down plane: hip → ankle target
        /// `forward` ahead and `down` below. Answers thigh, knee and foot turns
        /// (limbs swing forward on negative X; the foot stays level).
        /// </summary>
        static (float thigh, float knee, float foot) SolveLeg(float forward, float down)
        {
            var reach = Mathf.Clamp(Mathf.Sqrt(forward * forward + down * down), 0.05f, Thigh + Shin - 0.001f);
            var toTarget = Mathf.Atan2(forward, down);
            var atHip = Mathf.Acos(Mathf.Clamp((Thigh * Thigh + reach * reach - Shin * Shin) / (2 * Thigh * reach), -1, 1));
            var knee = Mathf.PI - Mathf.Acos(Mathf.Clamp((Thigh * Thigh + Shin * Shin - reach * reach) / (2 * Thigh * Shin), -1, 1));
            var thigh = toTarget + atHip;

            return (-thigh, knee, thigh - knee);
        }

        /// <summary>Puts the joints in the rest pose at once (before the body is fitted to them).</summary>
        public void ApplyRest()
        {
            Array.Copy(Rest, pose, pose.Length);
            groundL = groundR = 0;
            Apply();
        }

        public void Update(float dt, MotionState motion, float landingSpeed)
        {
            time += dt;

            if (landingSpeed > 2)
            {
                squash = Mathf.Min(0.16f, landingSpeed * 0.012f);
            }

            squash *= Mathf.Exp(-8 * dt);

            TargetPose(dt, motion);
            var rate = motion.swimming || motion.stance == Stance.Crawl ? 7 : 18;
            var ease = 1 - Mathf.Exp(-rate * dt);
            var slowEase = 1 - Mathf.Exp(-6 * dt);

            for (var i = 0; i < pose.Length; i++)
            {
                var amount = i == (int)P.Pitch || i == (int)P.BodyZ ? slowEase : ease;
                pose[i] += (target[i] - pose[i]) * amount;
            }

            PlantFeet(dt, motion);
            Apply();
        }

        float this[P key]
        {
            get => target[(int)key];
            set => target[(int)key] = value;
        }

        void TargetPose(float dt, MotionState motion)
        {
            Array.Copy(Rest, target, target.Length);

            if (motion.climb.HasValue)
            {
                ClimbPose(motion.climb.Value);
            }
            else if (motion.stance == Stance.Crawl)
            {
                CrawlPose(dt, motion);
            }
            else if (!motion.grounded)
            {
                AirPose(motion);
            }
            else
            {
                GaitPose(dt, motion);
            }

            // The head looks where the camera does; lying down it mostly can't.
            var lookScale = motion.stance == Stance.Crawl || motion.swimming ? 0.3f : 1;
            var lookYaw = Mathf.Clamp(motion.lookYaw, -1.25f, 1.25f) * lookScale;
            this[P.HeadY] += lookYaw * 0.75f;
            this[P.Twist] += lookYaw * 0.15f;
            this[P.HeadX] += Mathf.Clamp(motion.lookPitch * 0.45f, -0.35f, 0.45f) * lookScale;
            this[P.Lean] += Stoop;

            // A throw: the right arm comes up to the shoulder and cuts across in front, the body turning into it.
            if (Throw >= 0 && motion.climb == null)
            {
                var t = Smooth(Throw);
                this[P.ArmR] = -1.4f;
                this[P.ArmOutR] = Mathf.Lerp(1.25f, -0.4f, t);
                this[P.ElbowR] = Mathf.Lerp(-0.9f, -0.15f, t);
                this[P.Twist] += Mathf.Lerp(0.35f, -0.3f, t);
                this[P.HeadY] += Mathf.Lerp(0.2f, -0.1f, t);
            }

            // Keep the eyes level whatever the back does.
            this[P.HeadX] -= this[P.Lean] * 0.7f;
        }

        void GaitPose(float dt, MotionState motion)
        {
            var crouch = motion.stance == Stance.Crouch;
            var speed = motion.speed;
            var run = motion.runSpeed > motion.walkSpeed
                ? Mathf.Clamp01((speed - motion.walkSpeed) / (motion.runSpeed - motion.walkSpeed))
                : 0;
            var moving = Smooth((speed - 0.05f) / 0.5f);
            var half = Mathf.Clamp(speed * 0.13f, 0.06f, crouch ? 0.32f : 0.62f);
            var duty = Mathf.Lerp(0.52f, 0.38f, run);

            if (speed > 0.05f)
            {
                var before = stepPhase;
                stepPhase = (stepPhase + speed * duty / (2 * half) * dt) % 1;

                // A foot lands at the start of each half of the cycle.
                if (stepPhase < before || (before < 0.5f && stepPhase >= 0.5f))
                {
                    OnStep(Smooth((speed - 0.2f) / 1.5f) * (0.6f + 0.4f * run) * (crouch ? 0.45f : 1));
                }
            }

            (float z, float lift) Foot(float phase)
            {
                if (phase < duty)
                {
                    return (half * (1 - 2 * phase / duty), 0);
                }

                var swing = (phase - duty) / (1 - duty);

                return (-half + 2 * half * Smooth(swing),
                    Mathf.Sin(Mathf.PI * swing) * (0.09f + 0.14f * run) * (crouch ? 0.7f : 1));
            }

            var (zL, liftL) = Foot(stepPhase);
            var (zR, liftR) = Foot((stepPhase + 0.5f) % 1);

            this[P.Ik] = 1;
            this[P.FootZL] = zL * moving;
            this[P.FootZR] = zR * moving;
            this[P.LiftL] = liftL * moving;
            this[P.LiftR] = liftR * moving;
            this[P.SpreadL] = this[P.SpreadR] = crouch ? 0.06f : 0;

            var bounce = 0.5f + 0.5f * Mathf.Cos(stepPhase * Mathf.PI * 4);
            this[P.BodyY] = (crouch ? -0.36f : 0) - moving * (0.015f + 0.04f * run) * bounce - run * 0.05f;
            this[P.Lean] = (crouch ? 0.38f : 0) + moving * 0.03f + run * 0.16f;
            this[P.Lean] += Mathf.Clamp(motion.acceleration * 0.02f, -0.12f, 0.18f) * moving;
            // Only a hint of leaning into a turn.
            this[P.Roll] = Mathf.Clamp(-motion.turnRate * speed * 0.006f, -0.07f, 0.07f);
            this[P.Side] = this[P.Roll] * 0.3f;

            var swingL = zL / half;
            var swingR = zR / half;
            var armSwing = (0.35f + 0.5f * run) * moving * (crouch ? 0.5f : 1);
            this[P.ArmL] = -swingR * armSwing - (crouch ? 0.3f : 0);
            this[P.ArmR] = -swingL * armSwing - (crouch ? 0.3f : 0);
            this[P.ElbowL] = this[P.ElbowR] = -0.15f - moving * 0.2f - run * 1.0f - (crouch ? 0.5f : 0);
            this[P.ArmOutL] = this[P.ArmOutR] = 0.08f + run * 0.06f;
            this[P.Twist] = swingL * (0.08f + 0.06f * run) * moving;
            this[P.HipsYaw] = -swingL * 0.08f * moving;
            // The hip over the swinging leg drops a little each step.
            this[P.HipsRoll] = Mathf.Sin(stepPhase * Mathf.PI * 2) * (0.045f - 0.02f * run) * moving * (crouch ? 0.5f : 1);

            // Breathing and a slow shift of weight while standing still.
            var idle = 1 - moving;
            var breath = Mathf.Sin(time * 1.7f);

            this[P.HipsRoll] += Mathf.Sin(time * 0.45f) * 0.03f * idle;
            this[P.ArmL] -= 0.05f * idle;
            this[P.ArmR] -= 0.05f * idle;
            this[P.ElbowL] -= 0.1f * idle;
            this[P.ElbowR] -= 0.1f * idle;
            this[P.Lean] += breath * 0.012f * idle;
            this[P.ArmOutL] += breath * 0.015f * idle;
            this[P.ArmOutR] += breath * 0.015f * idle;
            this[P.HeadX] += Mathf.Sin(time * 0.6f) * 0.03f * idle;
            this[P.BodyY] -= squash;
        }

        void AirPose(MotionState motion)
        {
            var rising = Mathf.Clamp(motion.verticalSpeed / 8, -1, 1);
            var falling = Mathf.Clamp01(-motion.verticalSpeed / 14);

            this[P.Ik] = 0;
            this[P.ThighL] = -0.8f + falling * 0.4f;
            this[P.ThighR] = -0.2f;
            this[P.KneeL] = 1.2f - falling * 0.5f;
            this[P.KneeR] = 0.6f;
            this[P.FootL] = -(this[P.ThighL] + this[P.KneeL]) * 0.8f;
            this[P.FootR] = -(this[P.ThighR] + this[P.KneeR]) * 0.8f;
            this[P.ArmOutL] = this[P.ArmOutR] = 0.5f + rising * 0.2f + falling * 0.5f;
            this[P.ArmL] = -0.4f;
            this[P.ArmR] = 0.2f;
            this[P.ElbowL] = this[P.ElbowR] = -0.6f;
            this[P.Lean] = 0.1f;
            this[P.Roll] = Mathf.Clamp(-motion.turnRate * motion.speed * 0.004f, -0.05f, 0.05f);
            this[P.HeadX] = -rising * 0.12f;
        }

        void CrawlPose(float dt, MotionState motion)
        {
            var moving = Smooth((motion.speed - 0.05f) / 0.4f);
            var before = crawlPhase;
            crawlPhase = (crawlPhase + motion.speed / 0.9f * dt) % 1;

            if (moving > 0.3f && (crawlPhase < before || (before < 0.5f && crawlPhase >= 0.5f)))
            {
                OnStep(0.3f);
            }

            var s = Mathf.Sin(crawlPhase * Mathf.PI * 2) * moving;

            this[P.Ik] = 0;
            this[P.Pitch] = Mathf.PI / 2;
            this[P.BodyY] = 0.17f - Hips;
            this[P.BodyZ] = -0.2f;
            this[P.ThighL] = -0.05f + 0.15f * s;
            this[P.ThighR] = -0.05f - 0.15f * s;
            this[P.SpreadL] = 0.22f + 0.25f * Mathf.Max(0, s);
            this[P.SpreadR] = 0.22f + 0.25f * Mathf.Max(0, -s);
            // Knees bend out to the sides, not up into the air.
            this[P.TurnOut] = 1.3f;
            this[P.KneeL] = 0.35f + 0.8f * Mathf.Max(0, s);
            this[P.KneeR] = 0.35f + 0.8f * Mathf.Max(0, -s);
            this[P.FootL] = this[P.FootR] = 0.4f;
            this[P.ArmL] = -2.5f + 0.35f * s;
            this[P.ArmR] = -2.5f - 0.35f * s;
            this[P.ArmOutL] = this[P.ArmOutR] = 0.35f;
            this[P.ElbowL] = -1.3f - 0.3f * s;
            this[P.ElbowR] = -1.3f + 0.3f * s;
            this[P.Twist] = 0.1f * s;
            this[P.HeadX] = -1.0f;
        }

        void ClimbPose(float progress)
        {
            var legs = Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress / 0.8f));

            this[P.Ik] = 0;
            this[P.BodyY] = -0.05f;

            if (progress < 0.65f)
            {
                var pull = progress / 0.65f;
                this[P.ArmL] = this[P.ArmR] = Mathf.Lerp(-2.9f, -0.9f, Smooth(pull));
                this[P.ElbowL] = this[P.ElbowR] = -0.2f - 1.6f * Mathf.Sin(Mathf.PI * pull);
            }
            else
            {
                var over = (progress - 0.65f) / 0.35f;
                this[P.ArmL] = this[P.ArmR] = Mathf.Lerp(-0.9f, 0, Smooth(over));
                this[P.ElbowL] = this[P.ElbowR] = Mathf.Lerp(-0.3f, -0.15f, over);
            }

            this[P.ArmOutL] = this[P.ArmOutR] = 0.2f;
            this[P.ThighL] = -1.3f * legs;
            this[P.KneeL] = 1.8f * legs;
            this[P.ThighR] = -0.4f * legs;
            this[P.KneeR] = 0.8f * legs;
            this[P.FootL] = -(this[P.ThighL] + this[P.KneeL]) * 0.8f;
            this[P.FootR] = -(this[P.ThighR] + this[P.KneeR]) * 0.8f;
            this[P.Lean] = 0.35f * Mathf.Sin(Mathf.PI * progress);
            this[P.HeadX] = -0.4f * (1 - progress);
        }

        /// <summary>Eases each foot's ground height toward what is really under it.</summary>
        void PlantFeet(float dt, MotionState motion)
        {
            float left = 0, right = 0;

            if (motion.ground != null && motion.grounded && !motion.swimming && !motion.climb.HasValue)
            {
                var y = Root.position.y;

                float At(float x, float forward)
                {
                    var point = Root.TransformPoint(new Vector3(x, 0, forward));
                    var ground = motion.ground(point);

                    return ground.HasValue ? Mathf.Clamp(ground.Value - y, -0.3f, 0.3f) : 0;
                }

                // The left foot is on -X here.
                left = At(-LegSpread, pose[(int)P.FootZL]);
                right = At(LegSpread, pose[(int)P.FootZR]);
            }

            var ease = 1 - Mathf.Exp(-20 * dt);
            groundL += (left - groundL) * ease;
            groundR += (right - groundR) * ease;
        }

        void Apply()
        {
            float Get(P key) => pose[(int)key];

            // The body sinks to reach the lower foot.
            var bodyY = Get(P.BodyY) + Mathf.Min(0, Mathf.Min(groundL, groundR)) * Get(P.Ik);

            Body.localPosition = new Vector3(0, Hips + bodyY, Get(P.BodyZ));
            Body.localRotation = Xyz(Get(P.Pitch), 0, Get(P.Roll));
            HipsJoint.localRotation = Xyz(0, Get(P.HipsYaw), Get(P.HipsRoll));
            // The back straightens above the tilting hips.
            Spine.localRotation = Xyz(Get(P.Lean), Get(P.Twist), Get(P.Side) - Get(P.HipsRoll) * 0.8f);
            Head.localRotation = Yxz(Get(P.HeadX), Get(P.HeadY), 0);

            float thighL = Get(P.ThighL), thighR = Get(P.ThighR);
            float kneeL = Get(P.KneeL), kneeR = Get(P.KneeR);
            float footL = Get(P.FootL), footR = Get(P.FootR);
            var ik = Get(P.Ik);

            if (ik > 0.001f)
            {
                var hipHeight = Hips + bodyY - HipJoint;
                var left = SolveLeg(Get(P.FootZL), hipHeight - Ankle - groundL - Get(P.LiftL));
                var right = SolveLeg(Get(P.FootZR), hipHeight - Ankle - groundR - Get(P.LiftR));
                thighL = Mathf.Lerp(thighL, left.thigh, ik);
                kneeL = Mathf.Lerp(kneeL, left.knee, ik);
                footL = Mathf.Lerp(footL, left.foot, ik);
                thighR = Mathf.Lerp(thighR, right.thigh, ik);
                kneeR = Mathf.Lerp(kneeR, right.knee, ik);
                footR = Mathf.Lerp(footR, right.foot, ik);
            }

            HipL.localRotation = Xyz(thighL, Get(P.TurnOut), Get(P.SpreadL));
            HipR.localRotation = Xyz(thighR, -Get(P.TurnOut), -Get(P.SpreadR));
            KneeL.localRotation = Xyz(kneeL, 0, 0);
            KneeR.localRotation = Xyz(kneeR, 0, 0);
            AnkleL.localRotation = Xyz(footL, 0, 0);
            AnkleR.localRotation = Xyz(footR, 0, 0);

            ShoulderL.localRotation = Xyz(Get(P.ArmL), 0, Get(P.ArmOutL));
            ShoulderR.localRotation = Xyz(Get(P.ArmR), 0, -Get(P.ArmOutR));
            ElbowL.localRotation = Xyz(Get(P.ElbowL), 0, 0);
            ElbowR.localRotation = Xyz(Get(P.ElbowR), 0, 0);
        }
    }
}
