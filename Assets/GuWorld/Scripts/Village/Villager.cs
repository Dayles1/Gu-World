using System.Collections.Generic;
using GuWorld.Body;
using GuWorld.Combat;
using GuWorld.Render;
using UnityEngine;

namespace GuWorld.Village
{
    /// <summary>
    /// One villager going about their day: what their plan says for the hour
    /// (Roster) — sleeping at home, on the doorstep, at a post, strolling about
    /// a place — and walking there along the village's ways (VillageLayout). In
    /// sight they are a PersonFigure, posed as they walk or stand; at home asleep
    /// they are out of sight. Spoken to, they stop and turn to the speaker.
    ///
    /// Where they are follows from the time: when the clock jumps (a loaded game,
    /// the debug panel's hour), they are simply where they should be.
    ///
    /// Their body can be wounded part by part (HitZones, Wounds): struck, they
    /// flinch and run for home; struck down, they lie where they fell until the
    /// hero is far away, and are home again afterwards.
    /// </summary>
    public class Villager : MonoBehaviour, IStrikable, IZoned
    {
        public const float MaxHealth = 100;
        /// <summary>Seconds a struck villager runs for home before going about their day again.</summary>
        const float FleeTime = 9;
        /// <summary>Poses are worked out only this near the camera; farther, the figure stands still or is hidden.</summary>
        static float PoseDistance => GuWorld.UI.GameSettings.Current.PeoplePose;
        static float SightDistance => GuWorld.UI.GameSettings.Current.PeopleSight;
        /// <summary>The body's parts are there for blows only this near the camera (a blade flies 10 m).</summary>
        const float ZonesNear = 30;
        /// <summary>Posed every frame this near; farther, every second frame, and past twice this every third.</summary>
        const float PoseEveryFrame = 14;
        /// <summary>The body's outline only this near (farther it is a pixel and a second pass over the body).</summary>
        const float OutlineNear = 22;
        static readonly Unity.Profiling.ProfilerMarker PoseMarker = new Unity.Profiling.ProfilerMarker("GuWorld.People.Pose");

        public VillagerSpec Spec { get; private set; }
        public int Index { get; private set; }
        public bool Asleep { get; private set; }
        public bool Talking { get; private set; }

        VillageLayout layout;
        PersonFigure figure;
        CapsuleCollider solid;
        readonly List<Vector3> route = new List<Vector3>();
        Activity activity;
        /// <summary>Taken up an activity yet (none at first, and after getting up or fleeing: the hour's is begun afresh).</summary>
        bool busy;
        float wait;
        float yaw;
        float facing;
        float speed;
        float turnRate;
        Vector3 talkTo;
        System.Random random;
        List<HitZone> zones;
        bool seated;
        float fleeing;
        float downFor = -1;
        bool zonesOn = true;
        bool outlineOn = true;
        float poseTime;
        /// <summary>The way they face as last given to their transform (turning it moves a hundred joints and parts).</summary>
        float shownFacing = float.NaN;
        /// <summary>Near enough to be seen: their feet are put on the ground as they walk (farther, the ground is not looked up).</summary>
        bool grounded = true;

        public float Health { get; private set; } = MaxHealth;
        /// <summary>Struck down: lying where they fell.</summary>
        public bool Down => downFor >= 0;

        VillageLayout.Spot Home => layout.Houses[Mathf.Abs(Spec.Home) % layout.Houses.Length];

        public void Init(VillagerSpec spec, int index, VillageLayout village, HumanBody.Parts parts)
        {
            Spec = spec;
            Index = index;
            layout = village;
            random = new System.Random(index * 7919 + 13);
            name = spec.Name;
            figure = new PersonFigure(transform, parts, spec.Look, spec.Build, spec.Height);
            figure.Rig.Stoop = spec.Stoop;

            // Shadows only near: a crowd of skinned bodies is not cheap.
            foreach (var renderer in figure.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.updateWhenOffscreen = false;
            }

            // One kinematic body for the person: their capsule and their parts move as one
            // (a collider moved without a body is the physics' costliest kind).
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            solid = gameObject.AddComponent<CapsuleCollider>();
            solid.radius = 0.3f;
            solid.height = spec.Height;
            solid.center = new Vector3(0, spec.Height / 2, 0);
            zones = HitZones.Build(figure.Rig, this);
        }

        /// <summary>The body's parts there for blows (near) or left out of the physics (far).</summary>
        void SetZones(bool on)
        {
            if (zonesOn == on)
            {
                return;
            }

            zonesOn = on;

            foreach (var zone in zones)
            {
                zone.Shape.enabled = on;
            }
        }

        public void TakeStrike(Strike strike, BodyPart? part)
        {
            if (Down || Asleep)
            {
                return;
            }

            var harm = Wounds.Harm(part ?? BodyPart.Chest, strike, MaxHealth, Spec.Toughness, random);
            Health = Mathf.Max(0, Health - harm);
            var down = Health <= 0;
            Wounds.Report(Spec.Name, part, harm, down, strike.Point);

            if (harm < 0.5f)
            {
                // It did not get through: they only turn to see who it was.
                if (strike.Source != null)
                {
                    talkTo = strike.Source.transform.position;
                }

                return;
            }

            Talking = false;

            if (down)
            {
                downFor = 0;
                route.Clear();
                speed = 0;
                // Fallen on their back, the way the blow pushed them.
                var away = strike.Direction;
                away.y = 0;
                facing = away.sqrMagnitude > 0.01f ? Mathf.Atan2(-away.x, -away.z) : facing;
                figure.Root.localRotation = Quaternion.Euler(-88, 0, 0);
                figure.Root.localPosition = new Vector3(0, 0.12f, 0);

                return;
            }

            // Off home at a run.
            seated = false;
            fleeing = FleeTime;
            Go(Home.at);
        }

        /// <summary>Back on their feet at home, whole (after lying struck down while the hero was away).</summary>
        void GetUp()
        {
            downFor = -1;
            Health = MaxHealth;
            figure.Root.localRotation = Quaternion.identity;
            figure.Root.localPosition = Vector3.zero;
            route.Clear();
            Place(Home.at);
            busy = false;
        }

        /// <summary>A step of the day: `hour` 0…24, `dt` seconds; `jumped` when the clock leapt (be where you should be).</summary>
        public void Tick(float hour, float dt, Vector3 camera, bool jumped)
        {
            var distance = Vector3.Distance(camera, transform.position);
            SetZones(distance < (zonesOn ? ZonesNear + 4 : ZonesNear));
            var near = distance < SightDistance + 10;

            // Come near again: back on the ground where they are (far off it was not looked up).
            if (near && !grounded && !Down)
            {
                Place(transform.position);
            }

            grounded = near;

            if (Down)
            {
                downFor += dt;
                transform.rotation = Quaternion.Euler(0, facing * Mathf.Rad2Deg, 0);
                figure.SetVisible(distance < SightDistance);
                solid.enabled = false;

                // Out of sight a while: carried home, and whole again.
                if (downFor > 60 && distance > 120)
                {
                    GetUp();
                }

                return;
            }

            if (fleeing > 0)
            {
                fleeing -= dt;

                if (fleeing <= 0)
                {
                    busy = false;
                }
            }

            var plan = Spec.Plan(hour);

            if (fleeing <= 0 && (!busy || !plan.Same(activity)))
            {
                var first = !busy;
                busy = true;
                activity = plan;
                Begin(first || jumped);
            }

            if (Talking)
            {
                speed = Mathf.MoveTowards(speed, 0, dt * 4);
                var toward = talkTo - transform.position;
                Turn(Mathf.Atan2(toward.x, toward.z), dt);
            }
            else if (route.Count > 0)
            {
                Walk(dt);
            }
            else
            {
                speed = Mathf.MoveTowards(speed, 0, dt * 4);

                if (activity.doing == Doing.Stand || activity.doing == Doing.Porch || activity.doing == Doing.Sit)
                {
                    Turn(yaw * Mathf.Deg2Rad, dt);
                }
                else if (activity.doing == Doing.Wander && fleeing <= 0)
                {
                    wait -= dt;

                    if (wait <= 0)
                    {
                        Go(WanderTarget());
                    }
                }
            }

            if (!(Mathf.Abs(facing - shownFacing) < 1e-4f))
            {
                shownFacing = facing;
                transform.rotation = Quaternion.Euler(0, facing * Mathf.Rad2Deg, 0);
            }

            var seen = !Asleep && distance < SightDistance;
            figure.SetVisible(seen);
            outlineOn = distance < (outlineOn ? OutlineNear + 3 : OutlineNear);
            figure.SetOutline(outlineOn);

            if (solid.enabled == Asleep)
            {
                solid.enabled = !Asleep;
            }

            // Farther people are posed less often (their own turn, so not all in one frame), for the time since.
            poseTime += dt;
            var every = distance < PoseEveryFrame ? 1 : distance < PoseEveryFrame * 2 ? 2 : 3;

            if (seen && distance < PoseDistance && poseTime > 0 && (Time.frameCount + Index) % every == 0)
            {
                var scale = figure.Scale;
                var posed = Mathf.Min(poseTime, 0.25f);
                poseTime = 0;
                using var marker = PoseMarker.Auto();
                figure.Pose(posed, new MotionState
                {
                    speed = speed / scale,
                    walkSpeed = 1.4f / scale,
                    runSpeed = 3 / scale,
                    stance = Physics.Stance.Stand,
                    grounded = true,
                    turnRate = -turnRate,
                    sit = seated && route.Count == 0 && !Talking ? 3 : 0,
                });
            }
        }

        /// <summary>Takes up the hour's activity: off to it — or, when the clock leapt, already there.</summary>
        void Begin(bool there)
        {
            var (target, standYaw) = activity.doing switch
            {
                Doing.Sleep or Doing.Porch => (Home.at, Home.yaw),
                Doing.Stand or Doing.Sit => PlaceOf(activity.place),
                _ => (WanderTarget(), 0f),
            };
            yaw = standYaw;
            seated = false;

            if (there)
            {
                route.Clear();
                Place(target);
                facing = yaw * Mathf.Deg2Rad;
                Arrive();

                return;
            }

            // Out of the house first, onto the doorstep.
            if (Asleep)
            {
                Asleep = false;
                Place(Home.at);
            }

            Go(target);
        }

        (Vector3, float) PlaceOf(string place)
        {
            if (place != null && layout.TryPlace(place, out var spot))
            {
                // A little apart from anyone else standing there.
                var offset = new Vector3((Index % 3 - 1) * 0.9f, 0, 0);
                return (spot.at + Quaternion.Euler(0, spot.yaw, 0) * offset, spot.yaw);
            }

            return (Home.at, Home.yaw);
        }

        /// <summary>Somewhere to stroll to: a way's point within the activity's reach of its place.</summary>
        Vector3 WanderTarget()
        {
            var (centre, _) = PlaceOf(activity.place);
            var near = new List<Vector3>();

            foreach (var node in layout.Nodes)
            {
                if ((node - centre).sqrMagnitude < activity.radius * activity.radius)
                {
                    near.Add(node);
                }
            }

            if (near.Count == 0)
            {
                return centre;
            }

            var at = near[random.Next(near.Count)];

            return at + new Vector3((float)random.NextDouble() - 0.5f, 0, (float)random.NextDouble() - 0.5f) * 2.5f;
        }

        void Go(Vector3 target)
        {
            route.Clear();
            route.AddRange(layout.Route(transform.position, target));
        }

        void Walk(float dt)
        {
            var next = route[0];
            var to = next - transform.position;
            to.y = 0;
            var distance = to.magnitude;
            speed = Mathf.MoveTowards(speed, fleeing > 0 ? 3.6f : Spec.Speed, dt * (fleeing > 0 ? 8 : 3));
            var step = speed * dt;

            if (distance <= Mathf.Max(step, 0.25f))
            {
                Place(next, grounded);
                route.RemoveAt(0);

                if (route.Count == 0)
                {
                    Arrive();
                }

                return;
            }

            Turn(Mathf.Atan2(to.x, to.z), dt);
            Place(transform.position + to / distance * step, grounded);
        }

        void Arrive()
        {
            switch (activity.doing)
            {
                case Doing.Sleep:
                    Asleep = fleeing <= 0;
                    break;
                case Doing.Wander:
                    wait = 4 + (float)random.NextDouble() * 12;
                    break;
                case Doing.Sit:
                    seated = fleeing <= 0;
                    break;
            }
        }

        void Turn(float toward, float dt)
        {
            var before = facing;
            var delta = Mathf.Atan2(Mathf.Sin(toward - facing), Mathf.Cos(toward - facing));
            facing += delta * (1 - Mathf.Exp(-8 * dt));
            turnRate = dt > 0 ? (facing - before) / dt : 0;
        }

        /// <summary>Puts them at a point on the ground (or, far off, at the point's own height: the ground is looked up again as they come near).</summary>
        void Place(Vector3 at, bool ground = true)
        {
            if (ground)
            {
                at.y = Ground(at);
            }

            transform.position = at;
        }

        /// <summary>The ground under a point: the finest terrain there (a village's own, else the mountain's).</summary>
        static float Ground(Vector3 at) => GuWorld.World.QingMao.GroundAt(at);

        public void BeginTalk(Vector3 speaker)
        {
            Talking = true;
            talkTo = speaker;
        }

        public void EndTalk()
        {
            Talking = false;
        }
    }
}
