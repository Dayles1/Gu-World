using System;
using System.Collections.Generic;
using GuWorld.Combat;
using GuWorld.Systems;
using UnityEngine;

namespace GuWorld.Creatures
{
    /// <summary>
    /// A grey wolf of Qing Mao's forests (the novel's first arc has its wolf tide round Gu Yue
    /// village). Built of simple shapes in code and drawn like the people (Body.Anime): a long
    /// body, a deep chest, a narrow muzzle, pricked ears, a bushy tail, four legs that trot in
    /// diagonal pairs, amber eyes that catch the light at night.
    ///
    /// It roams round its den with its pack; seeing the hero near (further at night), it closes
    /// in at a run and bites — the legs and arms mostly — every second or so; struck, it turns on
    /// whoever struck it and calls its pack; badly hurt it runs back to its den. A moonblade cuts
    /// it badly; two kill it. Dead, it lies on its side a while, and the hero takes a fang.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Wolf : MonoBehaviour, IStrikable
    {
        public const float MaxHealth = 60;
        /// <summary>How hard its body is to wound (against a blow's power, Combat.Wounds).</summary>
        const float Toughness = 70;
        const float WanderSpeed = 1.6f;
        const float ChaseSpeed = 6.4f;
        const float FleeSpeed = 7;
        const float Gravity = 22;
        const float BiteReach = 1.75f;
        const float BiteEvery = 1.1f;
        /// <summary>How far it sees the hero by day and by night, m; how far from its den it gives up the chase.</summary>
        const float SeesByDay = 22;
        const float SeesByNight = 34;
        const float GivesUp = 90;

        /// <summary>Every wolf there is now.</summary>
        public static readonly List<Wolf> All = new List<Wolf>();
        /// <summary>A wolf killed (by the hero: tasks count them).</summary>
        public static event Action<Wolf> Killed;

        public float Health { get; private set; } = MaxHealth;
        public bool Dead => state == State.Dead;
        /// <summary>The den it roams round, and the pack it belongs to.</summary>
        public Vector3 Den;
        public int Pack;

        enum State
        {
            Roam,
            Chase,
            Flee,
            Dead,
        }

        State state;
        CharacterController controller;
        Transform hero;
        Health heroHealth;
        Vector3 roamTo;
        float roamWait;
        float verticalSpeed;
        float speed;
        float facing;
        float biteReady;
        float lunge = -1;
        float deadFor;
        float stride;
        System.Random random;

        // The figure's moving parts.
        Transform figure;
        Transform head;
        Transform tail;
        readonly Transform[] hips = new Transform[4];
        readonly Transform[] knees = new Transform[4];

        static Material fur;
        static Material furDark;
        static Material furLight;
        static Material nose;
        static Material eye;

        /// <summary>A wolf at a point (on the ground there), of a pack with its den, after the hero.</summary>
        public static Wolf Make(Vector3 at, Vector3 den, int pack, Transform hero, int seed)
        {
            var go = new GameObject("Wolf");
            go.transform.position = at;
            var controller = go.AddComponent<CharacterController>();
            controller.radius = 0.36f;
            controller.height = 0.9f;
            controller.center = new Vector3(0, 0.47f, 0);
            controller.slopeLimit = 55;
            controller.stepOffset = 0.4f;
            controller.skinWidth = 0.03f;
            var wolf = go.AddComponent<Wolf>();
            wolf.controller = controller;
            wolf.Den = den;
            wolf.Pack = pack;
            wolf.hero = hero;
            wolf.heroHealth = hero != null ? hero.GetComponent<Health>() : null;
            wolf.random = new System.Random(seed);
            wolf.facing = (float)wolf.random.NextDouble() * Mathf.PI * 2;
            wolf.roamTo = at;
            wolf.Build();

            return wolf;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        // ── The figure ────────────────────────────────────────────────

        void Build()
        {
            if (fur == null)
            {
                fur = Paint("Wolf Fur", new Color(0.36f, 0.34f, 0.32f));
                furDark = Paint("Wolf Back", new Color(0.2f, 0.19f, 0.19f));
                furLight = Paint("Wolf Belly", new Color(0.56f, 0.53f, 0.49f));
                nose = Paint("Wolf Nose", new Color(0.06f, 0.05f, 0.05f));
                eye = Paint("Wolf Eye", new Color(1, 0.72f, 0.2f));
                eye.EnableKeyword("_EMISSION");
                eye.SetColor("_EmissionColor", new Color(1, 0.6f, 0.15f) * 1.6f);
            }

            figure = new GameObject("Figure").transform;
            figure.SetParent(transform, false);

            // The body: a long barrel, a deeper chest before it, a darker saddle over the back.
            Part(PrimitiveType.Capsule, figure, new Vector3(0, 0.6f, -0.08f), new Vector3(90, 0, 0), new Vector3(0.4f, 0.55f, 0.38f), fur);
            Part(PrimitiveType.Sphere, figure, new Vector3(0, 0.64f, 0.32f), Vector3.zero, new Vector3(0.44f, 0.5f, 0.5f), fur);
            Part(PrimitiveType.Sphere, figure, new Vector3(0, 0.74f, -0.05f), Vector3.zero, new Vector3(0.36f, 0.16f, 0.8f), furDark);
            Part(PrimitiveType.Sphere, figure, new Vector3(0, 0.48f, 0.18f), Vector3.zero, new Vector3(0.3f, 0.2f, 0.62f), furLight);

            // The head on its neck: skull, a narrow muzzle, nose, ears, eyes.
            var neck = new GameObject("Neck").transform;
            neck.SetParent(figure, false);
            neck.localPosition = new Vector3(0, 0.74f, 0.48f);
            Part(PrimitiveType.Capsule, neck, new Vector3(0, 0.06f, 0.08f), new Vector3(55, 0, 0), new Vector3(0.24f, 0.2f, 0.24f), fur);
            head = new GameObject("Head").transform;
            head.SetParent(neck, false);
            head.localPosition = new Vector3(0, 0.16f, 0.22f);
            Part(PrimitiveType.Sphere, head, Vector3.zero, Vector3.zero, new Vector3(0.27f, 0.25f, 0.3f), fur);
            Part(PrimitiveType.Capsule, head, new Vector3(0, -0.04f, 0.2f), new Vector3(90, 0, 0), new Vector3(0.13f, 0.14f, 0.11f), fur);
            Part(PrimitiveType.Capsule, head, new Vector3(0, -0.08f, 0.17f), new Vector3(90, 0, 0), new Vector3(0.1f, 0.12f, 0.08f), furLight);
            Part(PrimitiveType.Sphere, head, new Vector3(0, -0.01f, 0.34f), Vector3.zero, new Vector3(0.06f, 0.05f, 0.05f), nose);

            foreach (var side in new[] { -1, 1 })
            {
                Part(PrimitiveType.Cube, head, new Vector3(side * 0.08f, 0.15f, -0.03f), new Vector3(-12, 0, side * -14), new Vector3(0.07f, 0.13f, 0.035f), furDark);
                Part(PrimitiveType.Sphere, head, new Vector3(side * 0.08f, 0.04f, 0.12f), Vector3.zero, new Vector3(0.04f, 0.03f, 0.03f), eye);
            }

            // The tail, hanging back and down.
            tail = new GameObject("Tail").transform;
            tail.SetParent(figure, false);
            tail.localPosition = new Vector3(0, 0.68f, -0.6f);
            Part(PrimitiveType.Capsule, tail, new Vector3(0, -0.12f, -0.14f), new Vector3(-50, 0, 0), new Vector3(0.13f, 0.2f, 0.13f), fur);
            Part(PrimitiveType.Capsule, tail, new Vector3(0, -0.3f, -0.22f), new Vector3(-20, 0, 0), new Vector3(0.11f, 0.14f, 0.11f), furDark);

            // Four legs: a hip (or shoulder) joint, the upper leg, a knee joint, the lower leg and the paw.
            var legAt = new[] { new Vector3(-0.13f, 0.56f, 0.34f), new Vector3(0.13f, 0.56f, 0.34f), new Vector3(-0.13f, 0.56f, -0.42f), new Vector3(0.13f, 0.56f, -0.42f) };

            for (var i = 0; i < 4; i++)
            {
                hips[i] = new GameObject("Hip").transform;
                hips[i].SetParent(figure, false);
                hips[i].localPosition = legAt[i];
                Part(PrimitiveType.Capsule, hips[i], new Vector3(0, -0.15f, 0), Vector3.zero, new Vector3(0.12f, 0.17f, 0.12f), fur);
                knees[i] = new GameObject("Knee").transform;
                knees[i].SetParent(hips[i], false);
                knees[i].localPosition = new Vector3(0, -0.3f, 0);
                Part(PrimitiveType.Capsule, knees[i], new Vector3(0, -0.12f, 0), Vector3.zero, new Vector3(0.085f, 0.14f, 0.085f), fur);
                Part(PrimitiveType.Sphere, knees[i], new Vector3(0, -0.25f, 0.03f), Vector3.zero, new Vector3(0.1f, 0.06f, 0.13f), furDark);
            }

            // Drawn like the people: cel-shaded, an ink outline.
            Body.Anime.Apply(figure.gameObject);
        }

        static Material Paint(string name, Color colour)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.1f);

            return material;
        }

        static void Part(PrimitiveType shape, Transform parent, Vector3 at, Vector3 turn, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            UnityEngine.Object.Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = at;
            part.transform.localEulerAngles = turn;
            part.transform.localScale = size;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // ── Living ────────────────────────────────────────────────────

        void Update()
        {
            var dt = Time.deltaTime;

            if (dt <= 0)
            {
                return;
            }

            if (state == State.Dead)
            {
                Lie(dt);

                return;
            }

            biteReady -= dt;
            Think(dt);
            Move(dt);
            Animate(dt);
        }

        bool Night
        {
            get
            {
                var sun = RenderSettings.sun;

                return sun != null && sun.transform.forward.y > -0.05f;
            }
        }

        void Think(float dt)
        {
            var toHero = hero != null ? hero.position - transform.position : Vector3.zero;
            toHero.y = 0;
            var heroDistance = hero != null ? toHero.magnitude : float.PositiveInfinity;
            var heroDown = heroHealth != null && heroHealth.Down;
            var fromDen = Flat(transform.position - Den).magnitude;

            switch (state)
            {
                case State.Roam:
                    if (!heroDown && heroDistance < (Night ? SeesByNight : SeesByDay))
                    {
                        Hunt();
                    }

                    break;

                case State.Chase:
                    // Too far from the den, the hero gone or down: back to roaming.
                    if (heroDown || fromDen > GivesUp || heroDistance > GivesUp * 0.7f)
                    {
                        state = State.Roam;
                        roamTo = Den;
                    }
                    else if (heroDistance < BiteReach && biteReady <= 0)
                    {
                        Bite();
                    }

                    break;

                case State.Flee:
                    if (fromDen < 6)
                    {
                        state = State.Roam;
                        // Rested a little, it may come again.
                        Health = Mathf.Min(MaxHealth, Health + MaxHealth * 0.25f);
                    }

                    break;
            }
        }

        /// <summary>Turns on the hero, and the pack with it.</summary>
        void Hunt()
        {
            if (state == State.Dead || state == State.Flee)
            {
                return;
            }

            state = State.Chase;

            foreach (var other in All)
            {
                if (other != this && other.Pack == Pack && other.state == State.Roam && Flat(other.transform.position - transform.position).magnitude < 60)
                {
                    other.state = State.Chase;
                }
            }
        }

        void Bite()
        {
            biteReady = BiteEvery * (0.85f + 0.3f * (float)random.NextDouble());
            lunge = 0;

            // Mostly the legs and arms, now and then higher.
            var parts = new[] { BodyPart.ShinL, BodyPart.ShinR, BodyPart.ThighL, BodyPart.ThighR, BodyPart.ForearmL, BodyPart.ForearmR, BodyPart.HandR, BodyPart.Belly };
            var part = parts[random.Next(parts.Length)];
            var toHero = (hero.position - transform.position).normalized;
            heroHealth?.TakeStrike(new Strike { Power = 92, Sharpness = 0.8f, Point = hero.position + Vector3.up * 0.5f, Direction = toHero, Source = gameObject }, part);
        }

        void Move(float dt)
        {
            Vector3 goal;
            float wanted;

            switch (state)
            {
                case State.Chase:
                    goal = hero.position;
                    // Close in, then hold at the bite's reach.
                    wanted = Flat(goal - transform.position).magnitude > BiteReach * 0.8f ? ChaseSpeed : 0;
                    break;

                case State.Flee:
                    goal = Den;
                    wanted = FleeSpeed;
                    break;

                default:
                    roamWait -= dt;

                    if (Flat(roamTo - transform.position).magnitude < 1.5f)
                    {
                        if (roamWait <= 0)
                        {
                            var angle = (float)random.NextDouble() * Mathf.PI * 2;
                            var reach = 6 + (float)random.NextDouble() * 22;
                            roamTo = Den + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * reach;
                            roamWait = 2 + (float)random.NextDouble() * 6;
                        }

                        wanted = 0;
                    }
                    else
                    {
                        wanted = WanderSpeed;
                    }

                    goal = roamTo;
                    break;
            }

            var to = Flat(goal - transform.position);

            if (to.sqrMagnitude > 0.01f)
            {
                var target = Mathf.Atan2(to.x, to.z);
                facing += Mathf.DeltaAngle(facing * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad * (1 - Mathf.Exp(-8 * dt));
            }

            speed = Mathf.MoveTowards(speed, wanted, dt * (wanted > speed ? 10 : 14));
            verticalSpeed = controller.isGrounded ? -1 : verticalSpeed - Gravity * dt;
            var forward = new Vector3(Mathf.Sin(facing), 0, Mathf.Cos(facing));
            controller.Move((forward * speed + Vector3.up * verticalSpeed) * dt);
            transform.rotation = Quaternion.Euler(0, facing * Mathf.Rad2Deg, 0);

            // Fallen out of the world (into the void under a gap): put back at the den.
            if (transform.position.y < -200)
            {
                controller.enabled = false;
                transform.position = Den + Vector3.up * 2;
                controller.enabled = true;
            }
        }

        /// <summary>The trot: diagonal pairs of legs swinging with the speed, the body bobbing, the tail swaying; a lunge for a bite.</summary>
        void Animate(float dt)
        {
            var pace = speed / ChaseSpeed;
            stride += dt * (2.2f + speed * 1.6f);
            var swing = Mathf.Clamp01(speed / 1.5f) * (0.35f + 0.35f * pace);

            for (var i = 0; i < 4; i++)
            {
                // Front left with back right, front right with back left.
                var phase = stride + (i == 0 || i == 3 ? 0 : Mathf.PI);
                var s = Mathf.Sin(phase);
                hips[i].localRotation = Quaternion.Euler(s * swing * Mathf.Rad2Deg, 0, 0);
                var bend = Mathf.Max(0, -Mathf.Cos(phase)) * swing * 1.4f;
                knees[i].localRotation = Quaternion.Euler((i < 2 ? -1 : 1) * bend * Mathf.Rad2Deg, 0, 0);
            }

            var bob = Mathf.Abs(Mathf.Sin(stride)) * 0.03f * pace;
            var lean = 0f;

            if (lunge >= 0)
            {
                lunge += dt;
                var t = lunge / 0.35f;
                lean = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);

                if (t >= 1)
                {
                    lunge = -1;
                }
            }

            figure.localPosition = new Vector3(0, bob, lean * 0.25f);
            figure.localRotation = Quaternion.Euler(lean * 10, 0, 0);
            head.localRotation = Quaternion.Euler(-10 + lean * 25 + Mathf.Sin(stride * 2) * 3 * pace, 0, 0);
            tail.localRotation = Quaternion.Euler(state == State.Flee ? 40 : -10 * pace, Mathf.Sin(Time.time * (state == State.Chase ? 6 : 2)) * 14, 0);
        }

        // ── Struck ────────────────────────────────────────────────────

        public void TakeStrike(Strike strike, BodyPart? part)
        {
            if (state == State.Dead)
            {
                return;
            }

            var harm = MaxHealth * 0.5f * Wounds.Scale(strike.Power * Mathf.Clamp01(strike.Sharpness <= 0 ? 1 : strike.Sharpness), Toughness);
            Health = Mathf.Max(0, Health - harm);
            Wounds.Report("Волк", null, harm, Health <= 0);

            if (Health <= 0)
            {
                Die(strike);

                return;
            }

            if (harm < 0.5f)
            {
                return;
            }

            // Badly hurt, alone or not, it runs for its den; else it turns on whoever struck it.
            if (Health < MaxHealth * 0.3f)
            {
                state = State.Flee;
            }
            else
            {
                Hunt();
            }
        }

        void Die(Strike strike)
        {
            state = State.Dead;
            speed = 0;
            controller.enabled = false;
            deadFor = 0;
            var away = strike.Direction;
            away.y = 0;

            // Fallen on its side, away from the blow.
            if (away.sqrMagnitude > 0.01f)
            {
                facing = Mathf.Atan2(away.x, away.z) + Mathf.PI / 2;
                transform.rotation = Quaternion.Euler(0, facing * Mathf.Rad2Deg, 0);
            }

            figure.localRotation = Quaternion.Euler(0, 0, 90);
            figure.localPosition = new Vector3(-0.35f, 0.18f, 0);
            Killed?.Invoke(this);
        }

        /// <summary>Lying dead a while, then sinking out of sight.</summary>
        void Lie(float dt)
        {
            deadFor += dt;

            if (deadFor > 25)
            {
                figure.localPosition += Vector3.down * dt * 0.4f;
            }

            if (deadFor > 30)
            {
                Destroy(gameObject);
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
