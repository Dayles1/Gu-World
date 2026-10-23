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
        /// <summary>How far through the moves of climbing a tree's trunk (it grows as they climb), or null when not on one.</summary>
        public float? treeClimb;
        /// <summary>Sitting: 0 not, 1 on the ground, 2 on an edge with the legs hanging over it, 3 on a seat (a bench, a stool).</summary>
        public int sit;
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
            BodyX, BodyY, BodyZ, Pitch, Roll, HipsYaw, HipsRoll, Lean, Side, Twist, HeadX, HeadY, Ik,
            FootZL, FootZR, LiftL, LiftR, ThighL, ThighR, KneeL, KneeR, FootL, FootR,
            // The feet's pitch on top of what the legs' IK gives them (heel to toe as they walk; positive: toes down).
            RollL, RollR,
            SpreadL, SpreadR, TurnOut, ArmL, ArmR, ArmOutL, ArmOutR, ElbowL, ElbowR,
            // How the hands close: 0 loosely curled, 1 a fist, below 0 open and straight.
            Grip,
            // The whole body turned round about its own axis (a roundhouse spins it a full turn), radians.
            Spin,
            // The whole body turned head over heels (a roll), radians.
            Flip,
            // The chest apart from the lower back (the upper spine): bent forward, turned about the spine; and the wrists
            // (positive: the hand bent forward, toward the palm), so the hands are not stiff on the forearms.
            ChestLean, ChestTwist, WristL, WristR,
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
            // Hanging hands bend a little toward the palms.
            rest[(int)P.WristL] = 0.12f;
            rest[(int)P.WristR] = 0.12f;

            return rest;
        }

        public readonly Transform Root;
        public readonly Transform Body, HipsJoint, Spine, Chest, Neck, Head;
        public readonly Transform ShoulderL, ShoulderR, ElbowL, ElbowR, WristL, WristR;
        public readonly Transform HipL, HipR, KneeL, KneeR, AnkleL, AnkleR;

        /// <summary>How bent the back is besides (an old person's stoop), radians.</summary>
        public float Stoop;

        /// <summary>How far through a throw with the right hand (a moonblade's cut), 0…1; below 0 none.</summary>
        public float Throw = -1;

        /// <summary>How far through a roll (a dodge), 0…1; below 0 none — and whether it goes backward (head over heels the other way).</summary>
        public float RollPhase = -1;
        public bool RollBack;

        /// <summary>A blow of the body's own (a fist, a foot) and how far through it, 0…1; below 0 none.</summary>
        public Combat.Blow Blow;
        public float BlowPhase = -1;
        /// <summary>How far the fists are up before the face to guard it, 0…1.</summary>
        public float Guard;

        /// <summary>Arms held out from the sides besides, radians (a figure shown on the body page).</summary>
        public float ArmsOut;

        /// <summary>How far a thrown skill is held ready (its key held down, aimed), 0…1: the right hand drawn back, open.</summary>
        public float Aim;

        /// <summary>How the hands are closed now (0 loosely curled, 1 a fist, below 0 open): HumanBody curls the fingers by it.</summary>
        public float Grip => pose[(int)P.Grip];

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
        /// <summary>How still the figure stands on its feet (1 standing, 0 walking, or not on its feet), and how much it runs — from the gait, for the stances over it.</summary>
        float still;
        float running;

        /// <param name="root">The figure's origin: between the feet, facing +Z.</param>
        public Mannequin(Transform root)
        {
            Root = root;
            Body = Joint("Joint Body", root, 0, Hips);
            HipsJoint = Joint("Joint Hips", Body, 0, 0);
            Spine = Joint("Joint Spine", HipsJoint, 0, 0.06f);
            // The upper back: the chest, the neck and the shoulders turn on it, against the lower back.
            Chest = Joint("Joint Chest", Spine, 0, 0.24f);
            Neck = Joint("Joint Neck", Chest, 0, 0.26f);
            Head = Joint("Joint Head", Neck, 0, 0.08f);
            // Side 1 is the figure's left: +X on the web, -X here.
            ShoulderL = Joint("Joint Shoulder L", Chest, 0.215f, 0.19f);
            ShoulderR = Joint("Joint Shoulder R", Chest, -0.215f, 0.19f);
            ElbowL = Joint("Joint Elbow L", ShoulderL, 0, -0.28f);
            ElbowR = Joint("Joint Elbow R", ShoulderR, 0, -0.28f);
            WristL = Joint("Joint Wrist L", ElbowL, 0, -0.25f);
            WristR = Joint("Joint Wrist R", ElbowR, 0, -0.25f);
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
            // A blow is snapped into: the figure follows it closely (else it would lag behind a jab and never reach it).
            var rate = motion.swimming || motion.stance == Stance.Crawl ? 7 : BlowPhase >= 0 || RollPhase >= 0 ? 34 : 18;
            var ease = 1 - Mathf.Exp(-rate * dt);
            var slowEase = 1 - Mathf.Exp(-6 * dt);
            // A full turn is no turn: the spin eases the short way round (a roundhouse ends where it began).
            pose[(int)P.Spin] = target[(int)P.Spin] + Mathf.DeltaAngle(target[(int)P.Spin] * Mathf.Rad2Deg, pose[(int)P.Spin] * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            pose[(int)P.Flip] = target[(int)P.Flip] + Mathf.DeltaAngle(target[(int)P.Flip] * Mathf.Rad2Deg, pose[(int)P.Flip] * Mathf.Rad2Deg) * Mathf.Deg2Rad;

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
            still = 0;
            running = 0;

            if (motion.treeClimb.HasValue)
            {
                TreePose(motion.treeClimb.Value);
            }
            else if (motion.sit > 0)
            {
                SitPose(motion.sit >= 2);

                if (motion.sit == 3)
                {
                    // On a seat: the hips at its height, the feet on the ground.
                    this[P.BodyY] += 0.42f;
                    this[P.KneeL] = this[P.KneeR] = 1.55f;
                    this[P.ArmL] = this[P.ArmR] = -0.45f;
                    this[P.ElbowL] = this[P.ElbowR] = -0.8f;
                    this[P.ArmOutL] = this[P.ArmOutR] = 0.12f;
                }
            }
            else if (motion.climb.HasValue)
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
            this[P.ArmOutL] += ArmsOut;
            this[P.ArmOutR] += ArmsOut;

            var free = motion.climb == null && motion.treeClimb == null && motion.sit == 0 && motion.stance != Stance.Crawl;

            // The hands: loosely curled, closing as the figure runs, gripping as it climbs.
            this[P.Grip] = motion.climb.HasValue || motion.treeClimb.HasValue ? 0.85f : 0.45f * running;

            // A thrown skill held ready: the right hand raised before the chest, open, as if holding the light that will
            // fly from it, the shoulders turned a little back after it; the head still on the mark.
            if (Aim > 0.01f && Throw < 0 && free)
            {
                var a = Aim;
                this[P.ArmR] = Mathf.Lerp(this[P.ArmR], -0.5f, a);
                this[P.ArmOutR] = Mathf.Lerp(this[P.ArmOutR], 0.3f, a);
                this[P.ElbowR] = Mathf.Lerp(this[P.ElbowR], -1.45f, a);
                this[P.ArmL] = Mathf.Lerp(this[P.ArmL], -0.2f, a);
                this[P.ElbowL] = Mathf.Lerp(this[P.ElbowL], -0.45f, a);
                this[P.Twist] -= 0.15f * a;
                this[P.HeadY] += 0.15f * a;
                this[P.Grip] = Mathf.Lerp(this[P.Grip], -0.35f, a);
                this[P.WristR] = Mathf.Lerp(this[P.WristR], -0.45f, a);
            }

            // A throw: wound up (as the aim holds it), the right arm cuts across in front from right to left, open-handed,
            // the shoulders and the hips turning into it and the left arm swinging back; the head stays on the mark.
            if (Throw >= 0 && motion.climb == null && motion.treeClimb == null)
            {
                var t = Throw;
                // The hand goes out from before the chest to the shoulder's height at the side, then sweeps across.
                var rise = Smooth(t / 0.35f);
                var cut = Smooth((t - 0.25f) / 0.45f);
                this[P.ArmR] = Mathf.Lerp(Mathf.Lerp(-0.5f, -0.9f, rise), -1.5f, cut);
                this[P.ArmOutR] = Mathf.Lerp(Mathf.Lerp(0.3f, 1.15f, rise), -0.5f, cut);
                this[P.ElbowR] = Mathf.Lerp(Mathf.Lerp(-1.45f, -0.9f, rise), -0.08f, cut);
                this[P.ArmL] = Mathf.Lerp(-0.2f, -0.3f, cut);
                this[P.ArmOutL] = Mathf.Lerp(0.1f, 0.45f, cut);
                this[P.ElbowL] = -0.5f;
                var turn = Mathf.Lerp(-0.3f, 0.45f, cut);
                var hips = Mathf.Lerp(-0.1f, 0.2f, cut);
                this[P.Twist] += turn;
                this[P.HipsYaw] += hips;
                this[P.HeadY] -= (turn + hips) * 0.8f;
                this[P.Lean] += 0.08f * cut;
                this[P.ChestTwist] += turn * 0.5f;
                this[P.Grip] = -0.4f;
                // The wrist snaps through the cut, the palm leading.
                this[P.WristR] = Mathf.Lerp(-0.5f, 0.35f, cut);
            }

            // Fighting: the fists up before the face, and a blow thrown from there.
            if ((Guard > 0.01f || BlowPhase >= 0) && free)
            {
                FightPose();
            }

            // A roll: tucked tight — knees to the chest, arms round them, chin down — the whole body turning head over heels.
            if (RollPhase >= 0 && free)
            {
                var t = Mathf.Clamp01(RollPhase);
                var tuck = Mathf.Sin(Mathf.PI * Mathf.Min(1, t * 1.15f));
                this[P.Ik] = 1 - tuck;
                this[P.BodyY] -= 0.42f * tuck;
                this[P.ThighL] = this[P.ThighR] = -1.7f * tuck;
                this[P.KneeL] = this[P.KneeR] = 2.3f * tuck;
                this[P.FootL] = this[P.FootR] = -0.4f * tuck;
                this[P.Lean] += 0.8f * tuck;
                this[P.ArmL] = Mathf.Lerp(this[P.ArmL], -1.1f, tuck);
                this[P.ArmR] = Mathf.Lerp(this[P.ArmR], -1.1f, tuck);
                this[P.ElbowL] = Mathf.Lerp(this[P.ElbowL], -1.9f, tuck);
                this[P.ElbowR] = Mathf.Lerp(this[P.ElbowR], -1.9f, tuck);
                this[P.HeadX] += 0.5f * tuck;
                var turn = 1 - (1 - t) * (1 - t);
                this[P.Flip] = (RollBack ? -1 : 1) * Mathf.PI * 2 * turn;
            }

            // Keep the eyes level whatever the back does, and on the way ahead whatever the shoulders do.
            this[P.HeadX] -= this[P.Lean] * 0.7f + this[P.ChestLean] * 0.8f;
            this[P.HeadY] -= this[P.ChestTwist] * 0.85f;
        }

        /// <summary>Out and back: rising to 1 by `at`, held a moment, back to 0 by the end.</summary>
        static float OutAndBack(float t, float at) => t < at ? Smooth(t / at) : 1 - Smooth((t - at - 0.08f) / Mathf.Max(0.01f, 1 - at - 0.08f));

        static float EaseOut(float t)
        {
            var x = 1 - Mathf.Clamp01(t);

            return 1 - x * x * x;
        }

        /// <summary>
        /// A blow's two movements through its time `t`: the wind-up (rising to 1 by `wound`, gone again by `lands`) and the
        /// blow itself (nothing until `wound`, snapping out to 1 by `lands`, held until `held`, drawn back by the end).
        /// </summary>
        static (float wind, float strike) Phases(float t, float wound, float lands, float held)
        {
            var wind = t < wound ? Smooth(t / wound) : 1 - Smooth((t - wound) / Mathf.Max(0.01f, lands - wound));
            var strike = t < wound ? 0 : t < lands ? EaseOut((t - wound) / Mathf.Max(0.01f, lands - wound)) : t < held ? 1 : 1 - Smooth((t - held) / Mathf.Max(0.01f, 1 - held));

            return (wind, strike);
        }

        /// <summary>
        /// The guard (the elbows in, the forearms up, the fists before the chin, the chin down), and a blow from it:
        /// a jab — the left fist straight out, the shoulders turning behind it; a cross — the right, the hips turning
        /// too; a front kick — the right knee up, the leg snapped out at the hips' height and drawn back, the body
        /// leaning back over the standing leg.
        /// </summary>
        void FightPose()
        {
            var guard = BlowPhase >= 0 ? 1 : Guard;
            // The hands close into fists as soon as they come up.
            this[P.Grip] = Mathf.Max(this[P.Grip], Mathf.Clamp01(guard * 1.6f));

            // Standing with the fists up: a fighter's stance — the left foot ahead, the right behind and its heel up,
            // the knees soft, the body turned behind the left shoulder (the head still to the front), bouncing a little
            // on the balls of the feet. Walking, the feet keep to the gait.
            var stand = guard * still;

            if (stand > 0.01f)
            {
                this[P.FootZL] += 0.15f * stand;
                this[P.FootZR] -= 0.13f * stand;
                this[P.SpreadL] += 0.04f * stand;
                this[P.SpreadR] += 0.08f * stand;
                this[P.LiftR] += 0.02f * stand;
                this[P.RollR] += 0.3f * stand;
                this[P.BodyY] -= (0.05f + 0.014f * (0.5f + 0.5f * Mathf.Sin(time * 7.5f))) * stand;
                this[P.HipsYaw] -= 0.32f * stand;
                this[P.Twist] -= 0.12f * stand;
                this[P.HeadY] += 0.38f * stand;
            }
            this[P.ArmL] = Mathf.Lerp(this[P.ArmL], -0.75f, guard);
            this[P.ArmR] = Mathf.Lerp(this[P.ArmR], -0.6f, guard);
            this[P.ElbowL] = Mathf.Lerp(this[P.ElbowL], -2.1f, guard);
            this[P.ElbowR] = Mathf.Lerp(this[P.ElbowR], -2.25f, guard);
            this[P.ArmOutL] = Mathf.Lerp(this[P.ArmOutL], 0.2f, guard);
            this[P.ArmOutR] = Mathf.Lerp(this[P.ArmOutR], 0.2f, guard);
            this[P.Lean] += 0.06f * guard;
            this[P.HeadX] += 0.1f * guard;

            if (BlowPhase < 0)
            {
                return;
            }

            var t = Mathf.Clamp01(BlowPhase);

            // Every blow is wound up, snapped out and drawn back; the head keeps to the mark whatever the shoulders do
            // (what they turn, it turns back).
            switch (Blow)
            {
                case Combat.Blow.Jab:
                {
                    // The left fist straight out from the guard, the left foot stepping in, the shoulders turning behind it.
                    var (wind, strike) = Phases(t, 0.2f, 0.42f, 0.55f);
                    this[P.ArmL] = Mathf.Lerp(Mathf.Lerp(-0.75f, -0.55f, wind), -1.56f, strike);
                    this[P.ElbowL] = Mathf.Lerp(Mathf.Lerp(-2.1f, -2.35f, wind), -0.04f, strike);
                    this[P.ArmOutL] = Mathf.Lerp(0.2f, -0.12f, strike);
                    Turn(0.1f * wind - 0.5f * strike, -0.15f * strike);
                    this[P.Lean] += 0.14f * strike;
                    this[P.FootZL] += 0.14f * strike;
                    this[P.BodyY] -= 0.03f * strike;
                    break;
                }
                case Combat.Blow.Cross:
                {
                    // The right fist across the body's line, the hips driving it, the back heel turning up.
                    var (wind, strike) = Phases(t, 0.22f, 0.45f, 0.58f);
                    this[P.ArmR] = Mathf.Lerp(Mathf.Lerp(-0.6f, -0.42f, wind), -1.58f, strike);
                    this[P.ElbowR] = Mathf.Lerp(Mathf.Lerp(-2.25f, -2.45f, wind), -0.04f, strike);
                    this[P.ArmOutR] = Mathf.Lerp(0.2f, -0.24f, strike);
                    Turn(-0.22f * wind + 0.75f * strike, -0.1f * wind + 0.42f * strike);
                    this[P.Lean] += 0.18f * strike;
                    this[P.RollR] += 0.5f * strike;
                    this[P.FootZR] += 0.06f * strike;
                    this[P.BodyY] -= 0.04f * strike;
                    break;
                }
                case Combat.Blow.Hook:
                {
                    // The left arm bent square at the shoulder's height, swung out wide, then round across the front.
                    var (wind, strike) = Phases(t, 0.25f, 0.5f, 0.62f);
                    var raise = Mathf.Max(wind, strike);
                    this[P.ArmL] = Mathf.Lerp(-0.75f, -1.35f, raise);
                    this[P.ArmOutL] = Mathf.Lerp(0.2f, Mathf.Lerp(1.0f, -0.5f, strike), raise);
                    this[P.ElbowL] = Mathf.Lerp(-2.1f, -1.5f, raise);
                    Turn(0.35f * wind - 0.85f * strike, 0.15f * wind - 0.4f * strike);
                    this[P.Lean] += 0.1f * strike;
                    this[P.Side] += 0.08f * strike;
                    this[P.RollL] += 0.3f * strike;
                    this[P.BodyY] -= 0.05f * raise;
                    break;
                }
                case Combat.Blow.Heavy:
                {
                    // Down into the knees, the right fist dropped back by the hip; then up through the legs and the hips onto
                    // the toes, the fist driven up under the chin before it — the head following it up.
                    var (load, drive) = Phases(t, 0.32f, 0.55f, 0.66f);
                    this[P.BodyY] -= 0.18f * load - 0.03f * drive;
                    this[P.ArmR] = Mathf.Lerp(Mathf.Lerp(-0.6f, 0.35f, load), -1.7f, drive);
                    this[P.ElbowR] = Mathf.Lerp(Mathf.Lerp(-2.25f, -1.3f, load), -1.5f, drive);
                    this[P.ArmOutR] = Mathf.Lerp(Mathf.Lerp(0.2f, 0.35f, load), 0.02f, drive);
                    Turn(-0.4f * load + 0.7f * drive, -0.18f * load + 0.32f * drive);
                    this[P.Lean] += 0.22f * load - 0.14f * drive;
                    this[P.HeadX] -= 0.2f * drive;
                    this[P.RollR] += 0.45f * drive;
                    this[P.RollL] += 0.25f * drive;
                    break;
                }
                case Combat.Blow.Spin:
                {
                    // A roundhouse: the whole body turns a full circle on the left foot, the right leg swung out high and wide
                    // as it comes round, the arms flung out for the balance.
                    var turn = Smooth((t - 0.06f) / 0.74f);
                    var lift = t < 0.3f ? Smooth((t - 0.08f) / 0.22f) : t < 0.7f ? 1 : 1 - Smooth((t - 0.7f) / 0.3f);
                    var snap = t < 0.35f ? 0 : t < 0.55f ? EaseOut((t - 0.35f) / 0.2f) : 1 - Smooth((t - 0.62f) / 0.2f);
                    this[P.Spin] = Mathf.PI * 2 * turn;
                    this[P.Ik] = 0;
                    this[P.BodyY] = -0.05f;
                    this[P.ThighL] = 0.05f;
                    this[P.KneeL] = 0.22f;
                    this[P.FootL] = -0.12f;
                    this[P.ThighR] = -1.05f * lift;
                    this[P.SpreadR] = 1.35f * lift;
                    this[P.KneeR] = Mathf.Lerp(1.7f, 0.1f, snap) * lift;
                    this[P.FootR] = 0.3f * snap;
                    this[P.Side] -= 0.38f * lift;
                    this[P.Roll] -= 0.16f * lift;
                    this[P.ArmOutL] += 0.85f * lift;
                    this[P.ArmOutR] += 0.6f * lift;
                    this[P.ArmL] += 0.3f * lift;
                    this[P.ArmR] += 0.3f * lift;
                    this[P.ElbowL] += 1.2f * lift;
                    this[P.ElbowR] += 1.2f * lift;
                    break;
                }
                default:
                {
                    // The finishing kick: the right knee drawn up high (the chamber), the leg snapped straight out at the hips'
                    // height, toes pointed, the body leaning back over the standing leg, the arms out for the balance.
                    var chamber = t < 0.28f ? Smooth(t / 0.28f) : t < 0.7f ? 1 : 1 - Smooth((t - 0.7f) / 0.3f);
                    var (_, extend) = Phases(t, 0.28f, 0.5f, 0.64f);
                    this[P.Ik] = 0;
                    this[P.BodyY] = -0.03f;
                    this[P.ThighL] = 0.06f;
                    this[P.KneeL] = 0.18f;
                    this[P.FootL] = -0.08f;
                    this[P.ThighR] = -1.6f * chamber - 0.12f * extend;
                    this[P.KneeR] = Mathf.Lerp(2.1f * chamber, 0.05f, extend);
                    this[P.FootR] = 0.4f * extend;
                    this[P.Lean] -= 0.32f * chamber;
                    this[P.HeadX] += 0.18f * chamber;
                    this[P.ArmOutL] += 0.4f * chamber;
                    this[P.ArmOutR] += 0.25f * chamber;
                    this[P.ArmL] += 0.35f * extend;
                    break;
                }
            }
        }

        /// <summary>The shoulders (`twist`) and the hips (`hips`) turned into a blow; the head turned back to keep on the mark.</summary>
        void Turn(float twist, float hips)
        {
            this[P.Twist] += twist;
            this[P.HipsYaw] += hips;
            this[P.HeadY] -= (twist + hips) * 0.85f;
        }

        void GaitPose(float dt, MotionState motion)
        {
            var crouch = motion.stance == Stance.Crouch;
            var speed = motion.speed;
            var run = motion.runSpeed > motion.walkSpeed
                ? Mathf.Clamp01((speed - motion.walkSpeed) / (motion.runSpeed - motion.walkSpeed))
                : 0;
            var moving = Smooth((speed - 0.05f) / 0.5f);
            // Turning on the spot: short steps in place, so the feet do not slide round.
            var turning = Mathf.Clamp01((Mathf.Abs(motion.turnRate) - 1.2f) / 2) * (1 - moving);
            var stepSpeed = Mathf.Max(speed, turning * 0.8f);
            var stepping = Mathf.Max(moving, turning * 0.5f);
            var half = Mathf.Clamp(stepSpeed * 0.13f, 0.06f, crouch ? 0.32f : 0.62f);
            var duty = Mathf.Lerp(0.52f, 0.38f, run);
            still = (1 - moving) * (1 - turning);
            running = run * moving;

            if (stepSpeed > 0.05f)
            {
                var before = stepPhase;
                stepPhase = (stepPhase + stepSpeed * duty / (2 * half) * dt) % 1;

                // A foot lands at the start of each half of the cycle.
                if (stepPhase < before || (before < 0.5f && stepPhase >= 0.5f))
                {
                    OnStep(Smooth((stepSpeed - 0.2f) / 1.5f) * (0.6f + 0.4f * run) * (crouch ? 0.45f : 1));
                }
            }

            // A foot through the cycle: how far ahead, how high, and its pitch. On the ground it comes down heel first
            // (toes up), rolls flat, and rises onto its toes before it leaves (its heel lifting); in the air it swings
            // toes up to the next heel strike — running, its heel kicking up behind it first.
            (float z, float lift, float roll) Foot(float phase)
            {
                if (phase < duty)
                {
                    var stance = phase / duty;
                    var heel = Smooth((stance - 0.55f) / 0.45f);

                    return (half * (1 - 2 * stance), 0.045f * heel, -0.22f * (1 - Smooth(stance / 0.18f)) + 0.38f * heel);
                }

                var swing = (phase - duty) / (1 - duty);
                var shape = Mathf.Pow(swing, Mathf.Lerp(1, 0.65f, run));
                // Running, the knee comes up high and the heel kicks up behind.
            var lift = Mathf.Sin(Mathf.PI * shape) * (0.09f + 0.22f * run) * (crouch ? 0.7f : 1) + 0.045f * (1 - Smooth(swing / 0.25f));

                return (-half + 2 * half * Smooth(swing), lift, Mathf.Lerp(0.38f, -0.22f, Smooth(swing / 0.55f)));
            }

            var (zL, liftL, rollL) = Foot(stepPhase);
            var (zR, liftR, rollR) = Foot((stepPhase + 0.5f) % 1);
            // Small steps roll the feet less.
            var rolling = stepping * Mathf.Clamp01(half / 0.25f) * (crouch ? 0.6f : 1);

            this[P.Ik] = 1;
            this[P.FootZL] = zL * stepping;
            this[P.FootZR] = zR * stepping;
            this[P.LiftL] = liftL * stepping;
            this[P.LiftR] = liftR * stepping;
            this[P.RollL] = rollL * rolling;
            this[P.RollR] = rollR * rolling;
            this[P.SpreadL] = this[P.SpreadR] = crouch ? 0.06f : 0;

            // Lowest as a foot comes down walking; running, lowest in the middle of the stance and highest in the flight.
            var walkBounce = 0.5f + 0.5f * Mathf.Cos(stepPhase * Mathf.PI * 4);
            var runBounce = 0.5f + 0.5f * Mathf.Cos((stepPhase - duty * 0.5f) * Mathf.PI * 4);
            var bounce = Mathf.Lerp(walkBounce, runBounce, run);
            this[P.BodyY] = (crouch ? -0.36f : 0) - moving * (0.015f + 0.04f * run) * bounce - run * 0.05f;
            this[P.Lean] = (crouch ? 0.3f : 0) + moving * 0.03f + run * 0.12f;
            // The chest over it: a runner leans from the ankles and carries the chest forward, a walker upright.
            this[P.ChestLean] = (crouch ? 0.1f : 0) + run * 0.09f * moving;
            this[P.Lean] += Mathf.Clamp(motion.acceleration * 0.02f, -0.12f, 0.18f) * moving;
            // Leaning into a turn, more the faster it is taken.
            this[P.Roll] = Mathf.Clamp(-motion.turnRate * speed * 0.009f, -0.13f, 0.13f);
            this[P.Side] = this[P.Roll] * 0.3f;

            var swingL = zL / half;
            var swingR = zR / half;
            // The arms swing from the shoulders against the legs — wider running, pumping at a right angle.
            var armSwing = (0.42f + 0.55f * run) * moving * (crouch ? 0.5f : 1);
            this[P.ArmL] = -swingR * armSwing - (crouch ? 0.3f : 0) - run * 0.12f * moving;
            this[P.ArmR] = -swingL * armSwing - (crouch ? 0.3f : 0) - run * 0.12f * moving;
            this[P.ElbowL] = this[P.ElbowR] = -0.15f - moving * 0.2f - run * 1.15f - (crouch ? 0.5f : 0);
            // An arm bends more as it swings forward, and straightens going back.
            this[P.ElbowL] -= Mathf.Max(0, swingR) * (0.22f + 0.35f * run) * moving;
            this[P.ElbowR] -= Mathf.Max(0, swingL) * (0.22f + 0.35f * run) * moving;
            this[P.ArmOutL] = this[P.ArmOutR] = 0.08f + run * 0.08f;
            // The wrists trail the swing a little, the hands loose walking and firm running.
            this[P.WristL] = 0.12f + 0.12f * swingR * (1 - run) * moving - 0.1f * run * moving;
            this[P.WristR] = 0.12f + 0.12f * swingL * (1 - run) * moving - 0.1f * run * moving;
            // The pelvis turns with the leg going forward, the chest the other way with the arms; the lower back between.
            this[P.HipsYaw] = -swingL * (0.11f + 0.04f * run) * moving;
            this[P.Twist] = swingL * 0.04f * moving;
            this[P.ChestTwist] = swingL * (0.12f + 0.1f * run) * moving;
            // The hip over the swinging leg drops a little each step.
            this[P.HipsRoll] = Mathf.Sin(stepPhase * Mathf.PI * 2) * (0.045f - 0.02f * run) * moving * (crouch ? 0.5f : 1);

            // Standing at ease: breathing, the feet a little apart and one a little ahead, the weight slowly going over
            // from one leg to the other (the body over the standing leg, the free one's hip dropping, its knee soft
            // and its heel up), and now and then a look aside.
            var idle = still;
            var breath = Mathf.Sin(time * 1.7f);
            var weight = Mathf.Clamp(Mathf.Sin(time * 0.31f) * 1.7f, -1, 1);

            this[P.FootZL] += 0.05f * idle;
            this[P.FootZR] -= 0.03f * idle;
            this[P.SpreadL] += 0.03f * idle;
            this[P.SpreadR] += 0.03f * idle;
            this[P.BodyX] = -0.025f * weight * idle;
            this[P.HipsRoll] += 0.04f * weight * idle;
            this[P.LiftR] += 0.02f * Mathf.Max(0, weight) * idle;
            this[P.RollR] += 0.22f * Mathf.Max(0, weight) * idle;
            this[P.LiftL] += 0.02f * Mathf.Max(0, -weight) * idle;
            this[P.RollL] += 0.22f * Mathf.Max(0, -weight) * idle;
            this[P.ArmL] -= 0.05f * idle;
            this[P.ArmR] -= 0.05f * idle;
            this[P.ElbowL] -= 0.1f * idle;
            this[P.ElbowR] -= 0.1f * idle;
            // Breathing: the chest rises and opens, the shoulders with it.
            this[P.ChestLean] -= breath * 0.025f * idle;
            this[P.Lean] += breath * 0.006f * idle;
            this[P.ArmOutL] += breath * 0.015f * idle;
            this[P.ArmOutR] += breath * 0.015f * idle;
            // Now and then a hand moves: the fingers flexed, a wrist turned.
            this[P.WristL] += Mathf.Sin(time * 0.43f) * 0.08f * idle;
            this[P.WristR] += Mathf.Sin(time * 0.37f + 1.3f) * 0.08f * idle;
            this[P.HeadX] += Mathf.Sin(time * 0.6f) * 0.03f * idle;
            this[P.HeadY] += Mathf.Sin(time * 0.21f) * Mathf.Max(0, Mathf.Sin(time * 0.13f)) * 0.3f * idle;
            this[P.BodyY] -= squash;
        }

        /// <summary>
        /// In the air: going up, the knees drawn up (one more than the other) and the arms lifted; coming down, the
        /// legs reaching for the ground and the arms spread for balance, the eyes on where it will land.
        /// </summary>
        void AirPose(MotionState motion)
        {
            var up = Mathf.Clamp01(motion.verticalSpeed / 6);
            var down = Mathf.Clamp01(-motion.verticalSpeed / 10);

            this[P.Ik] = 0;
            this[P.ThighL] = Mathf.Lerp(-0.75f, -0.4f, down) - 0.25f * up;
            this[P.ThighR] = Mathf.Lerp(-0.25f, -0.12f, down) - 0.15f * up;
            this[P.KneeL] = Mathf.Lerp(1.15f, 0.55f, down) + 0.25f * up;
            this[P.KneeR] = Mathf.Lerp(0.75f, 0.35f, down) + 0.15f * up;
            this[P.FootL] = -(this[P.ThighL] + this[P.KneeL]) * 0.8f;
            this[P.FootR] = -(this[P.ThighR] + this[P.KneeR]) * 0.8f;
            this[P.ArmL] = Mathf.Lerp(-0.55f, -0.25f, down);
            this[P.ArmR] = Mathf.Lerp(0.15f, -0.2f, down);
            this[P.ArmOutL] = this[P.ArmOutR] = 0.45f + 0.15f * up + 0.55f * down;
            this[P.ElbowL] = this[P.ElbowR] = -0.6f + 0.25f * down;
            this[P.Lean] = 0.1f - 0.06f * down;
            this[P.ChestLean] = -0.06f * up + 0.05f * down;
            this[P.WristL] = this[P.WristR] = -0.15f * down;
            this[P.Roll] = Mathf.Clamp(-motion.turnRate * motion.speed * 0.004f, -0.05f, 0.05f);
            this[P.HeadX] = -up * 0.12f + down * 0.15f;
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
            this[P.Lean] = 0.25f * Mathf.Sin(Mathf.PI * progress);
            this[P.ChestLean] = 0.12f * Mathf.Sin(Mathf.PI * progress);
            this[P.WristL] = this[P.WristR] = 0.5f;
            this[P.HeadX] = -0.4f * (1 - progress);
        }

        /// <summary>Hugging a trunk: the arms up round it, a knee raised, the hands and feet taking turns as they climb.</summary>
        void TreePose(float phase)
        {
            var s = Mathf.Sin(phase * Mathf.PI * 2);
            this[P.Ik] = 0;
            this[P.BodyY] = -0.08f;
            this[P.BodyZ] = 0.05f;
            this[P.Lean] = 0.2f;
            this[P.ArmL] = -2.5f + 0.35f * s;
            this[P.ArmR] = -2.5f - 0.35f * s;
            this[P.ArmOutL] = this[P.ArmOutR] = 0.45f;
            this[P.ElbowL] = -1.0f - 0.25f * s;
            this[P.ElbowR] = -1.0f + 0.25f * s;
            this[P.ThighL] = -0.9f - 0.45f * Mathf.Max(0, -s);
            this[P.ThighR] = -0.9f - 0.45f * Mathf.Max(0, s);
            this[P.KneeL] = 1.3f + 0.5f * Mathf.Max(0, -s);
            this[P.KneeR] = 1.3f + 0.5f * Mathf.Max(0, s);
            this[P.SpreadL] = this[P.SpreadR] = 0.25f;
            this[P.FootL] = -(this[P.ThighL] + this[P.KneeL]) * 0.7f;
            this[P.FootR] = -(this[P.ThighR] + this[P.KneeR]) * 0.7f;
            this[P.HeadX] = -0.35f;
        }

        /// <summary>Sitting: on the ground with the knees up and the arms on them, or on an edge with the legs hanging down, the hands on it either side.</summary>
        void SitPose(bool edge)
        {
            this[P.Ik] = 0;

            if (edge)
            {
                this[P.BodyY] = -(Hips - HipJoint) + 0.04f;
                this[P.ThighL] = this[P.ThighR] = -1.5f;
                this[P.KneeL] = 1.35f + 0.1f * Mathf.Sin(time * 0.8f);
                this[P.KneeR] = 1.35f + 0.1f * Mathf.Sin(time * 0.8f + 2);
                this[P.FootL] = this[P.FootR] = 0.1f;
                this[P.SpreadL] = this[P.SpreadR] = 0.08f;
                this[P.ArmL] = this[P.ArmR] = 0.15f;
                this[P.ArmOutL] = this[P.ArmOutR] = 0.35f;
                this[P.ElbowL] = this[P.ElbowR] = -0.15f;
                this[P.Lean] = 0.08f;
            }
            else
            {
                this[P.BodyY] = -(Hips - HipJoint) + 0.1f;
                this[P.ThighL] = this[P.ThighR] = -1.15f;
                this[P.KneeL] = this[P.KneeR] = 2.1f;
                this[P.FootL] = this[P.FootR] = -0.6f;
                this[P.SpreadL] = this[P.SpreadR] = 0.22f;
                this[P.ArmL] = this[P.ArmR] = -0.75f;
                this[P.ArmOutL] = this[P.ArmOutR] = 0.2f;
                this[P.ElbowL] = this[P.ElbowR] = -0.9f;
                this[P.Lean] = 0.25f;
            }

            // Breathing.
            this[P.Lean] += Mathf.Sin(time * 1.7f) * 0.012f;
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
            var bodyX = Get(P.BodyX);

            Body.localPosition = new Vector3(-bodyX, Hips + bodyY, Get(P.BodyZ));
            Body.localRotation = Quaternion.AngleAxis(-Get(P.Spin) * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(Get(P.Flip) * Mathf.Rad2Deg, Vector3.right) *
                                 Xyz(Get(P.Pitch), 0, Get(P.Roll));
            HipsJoint.localRotation = Xyz(0, Get(P.HipsYaw), Get(P.HipsRoll));
            // The back straightens above the tilting hips; the chest on it bends and turns on its own.
            Spine.localRotation = Xyz(Get(P.Lean), Get(P.Twist), Get(P.Side) - Get(P.HipsRoll) * 0.8f);
            Chest.localRotation = Xyz(Get(P.ChestLean), Get(P.ChestTwist), 0);
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

            footL += Get(P.RollL);
            footR += Get(P.RollR);
            // The body moved aside over the feet: the legs lean back to them (the feet stay where they stood).
            var lean = bodyX / (Thigh + Shin) * ik;

            HipL.localRotation = Xyz(thighL, Get(P.TurnOut), Get(P.SpreadL) - lean);
            HipR.localRotation = Xyz(thighR, -Get(P.TurnOut), -(Get(P.SpreadR) + lean));
            KneeL.localRotation = Xyz(kneeL, 0, 0);
            KneeR.localRotation = Xyz(kneeR, 0, 0);
            AnkleL.localRotation = Xyz(footL, 0, 0);
            AnkleR.localRotation = Xyz(footR, 0, 0);

            ShoulderL.localRotation = Xyz(Get(P.ArmL), 0, Get(P.ArmOutL));
            ShoulderR.localRotation = Xyz(Get(P.ArmR), 0, -Get(P.ArmOutR));
            ElbowL.localRotation = Xyz(Get(P.ElbowL), 0, 0);
            ElbowR.localRotation = Xyz(Get(P.ElbowR), 0, 0);
            WristL.localRotation = Xyz(-Get(P.WristL), 0, 0);
            WristR.localRotation = Xyz(-Get(P.WristR), 0, 0);
        }
    }
}
