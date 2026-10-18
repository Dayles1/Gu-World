using System;
using System.Collections.Generic;
using GuWorld.Combat;
using GuWorld.Systems;
using UnityEngine;

namespace GuWorld.Creatures
{
    /// <summary>What a Gu can do to a beast besides wounding it (Wolf.Afflict).</summary>
    public enum Affliction
    {
        None,
        Slow,
        Root,
        Sleep,
        Blind,
        Fear,
        /// <summary>Harm every second: fire, poison, a wound that bleeds.</summary>
        Burn,
        /// <summary>Enslaved: it follows its master.</summary>
        Tame,
    }

    /// <summary>
    /// A wolf of Qing Mao (the novel's first arc has its wolf tide round Gu Yue village), of one of the kinds the
    /// mountain knows (WolfKinds) — a grey wolf, a lightning wolf, or a beast king of the lightning wolves — and of a
    /// level within its kind's. Built of simple shapes in code, at its kind's size and in its colours, and drawn like the
    /// people (Body.Anime): a long body, a deep chest, a narrow muzzle, pricked ears, a bushy tail, four legs that trot
    /// in diagonal pairs, eyes that catch the light; a lightning wolf's fur glows, the thunder crown wolf wears a crown
    /// of horns and golden tufts on its paws and tail.
    ///
    /// It roams round its den with its pack; seeing the hero near (further at night, further for the bigger kinds), it
    /// closes in at a run and bites — the legs and arms mostly; a lightning wolf's bite shocks. Struck, it turns on
    /// whoever struck it and calls its pack; an ordinary wolf badly hurt runs back to its den, a king never does. The
    /// kings fight with the Gu in their bodies: they spit lightning, howl their packs into a fury, raise a thunder
    /// shield, go berserk when badly hurt; the thunder crown wolf mends its wounds, sees through stealth and stamps
    /// lightning through the ground about it. Dead, it lies on its side a while; the hero takes its fangs, and from a
    /// king's body a Gu may come.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Wolf : MonoBehaviour, IStrikable, IStaggerable, IFoe
    {
        const float WanderSpeed = 1.6f;
        const float FleeSpeed = 7;
        const float Gravity = 22;
        const float BiteReach = 1.75f;
        /// <summary>How far it sees the hero by day and by night, m; how far from its den it gives up the chase.</summary>
        const float SeesByDay = 22;
        const float SeesByNight = 34;
        const float GivesUp = 90;

        /// <summary>How far the hero can be noticed, as a share of a wolf's sight (a hidden Gu Master: less; unseen: 0).</summary>
        public static float Notice = 1;
        /// <summary>Something wolves go for instead of the hero while it lasts (a scarecrow), and until when (Time.time).</summary>
        public static Transform Decoy;
        public static float DecoyUntil;

        /// <summary>Every wolf there is now.</summary>
        public static readonly List<Wolf> All = new List<Wolf>();
        /// <summary>A wolf killed (by the hero: tasks count them, its fangs and Gu are taken).</summary>
        public static event Action<Wolf> Killed;
        /// <summary>Something a beast king does that the hero should know (it howls, it goes berserk, it raises its shield).</summary>
        public static event Action<Wolf, string> Roared;

        public WolfKind Kind { get; private set; } = WolfKinds.Grey;
        public int Level { get; private set; } = 1;
        public float MaxHealth { get; private set; } = 60;
        public float Health { get; private set; } = 60;
        public bool Dead => state == Mind.Dead;
        /// <summary>Hunting the hero now.</summary>
        public bool Hunting => state == Mind.Chase;
        /// <summary>The den it roams round, and the pack it belongs to.</summary>
        public Vector3 Den;
        public int Pack;
        /// <summary>When it was last struck (Time.time; the HUD shows its name a while after).</summary>
        public float StruckAt { get; private set; } = -100;
        /// <summary>A thunder shield up, berserk, maddened by its king's howl (for the HUD).</summary>
        public bool Shielded => Time.time < shieldUntil;
        public bool Berserk => frenzied;
        public bool Maddened => Time.time < howledUntil;
        /// <summary>How tall it stands, m (the HUD puts its name over it).</summary>
        public float Height => 1.15f * Kind.Scale;

        public string Title => Kind.Name;
        public string Rung => Kind.TierName;
        public int Tier => Kind.Tier;
        public string State => (Shielded ? "  ·  ГРОМОВОЙ ЩИТ" : "") + (Berserk ? "  ·  НЕИСТОВСТВО" : "") + (Maddened ? "  ·  ЯРОСТЬ СТАИ" : "");

        enum Mind
        {
            Roam,
            Chase,
            Flee,
            Dead,
        }

        Mind state;
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

        // What Gu have done to it (Time.time until each lasts; how strong).
        float slowUntil, slowBy, rootUntil, sleepUntil, blindUntil, fearUntil, hurtUntil, hurtPerSecond;
        // What its own Gu do (a king's): when it next spits lightning, howls, raises its shield, stamps; until when they last.
        float nextBolt, nextHowl, nextShield, nextStomp, shieldUntil, howledUntil;
        bool frenzied;
        Cultivation.GuGlow shieldGlow;
        Cultivation.GuKind boltKind;

        /// <summary>Thrown by a blow (a kick, an uppercut), m/s: it fades as the wolf slides.</summary>
        Vector3 push;

        /// <summary>A blow throws it: away (and up) at this speed; it cannot bite for a moment. A king barely moves.</summary>
        public void Push(Vector3 velocity)
        {
            if (state == Mind.Dead)
            {
                return;
            }

            push = velocity / (Kind.Scale * Kind.Scale);
            speed = 0;
            biteReady = Mathf.Max(biteReady, 0.8f);
        }

        /// <summary>Enslaved by a Gu: it follows the hero and never bites them.</summary>
        public bool Tamed { get; private set; }

        // The figure's moving parts.
        Transform figure;
        Transform head;
        Transform tail;
        readonly Transform[] hips = new Transform[4];
        readonly Transform[] knees = new Transform[4];
        Material eyeGlow;

        /// <summary>Each kind's colours, made once.</summary>
        static readonly Dictionary<string, Material[]> paints = new Dictionary<string, Material[]>();
        static Material nose;
        static Material gold;

        /// <summary>A wolf at a point (on the ground there), of a pack with its den, after the hero — of a kind and a level (none: a grey wolf of a level by chance).</summary>
        public static Wolf Make(Vector3 at, Vector3 den, int pack, Transform hero, int seed, WolfKind kind = null, int level = 0)
        {
            kind ??= WolfKinds.Grey;
            var go = new GameObject(kind.Name);
            go.transform.position = at;
            var controller = go.AddComponent<CharacterController>();
            controller.radius = 0.36f * kind.Scale;
            controller.height = Mathf.Max(0.9f * kind.Scale, controller.radius * 2 + 0.01f);
            controller.center = new Vector3(0, 0.47f * kind.Scale, 0);
            controller.slopeLimit = 55;
            controller.stepOffset = Mathf.Min(0.4f * kind.Scale, controller.height * 0.4f);
            controller.skinWidth = 0.03f;
            var wolf = go.AddComponent<Wolf>();
            wolf.controller = controller;
            wolf.Den = den;
            wolf.Pack = pack;
            wolf.hero = hero;
            wolf.heroHealth = hero != null ? hero.GetComponent<Health>() : null;
            wolf.random = new System.Random(seed);
            wolf.Kind = kind;
            wolf.Level = level > 0 ? Mathf.Clamp(level, kind.MinLevel, Mathf.Max(kind.MinLevel, kind.MaxLevel)) : wolf.random.Next(kind.MinLevel, kind.MaxLevel + 1);
            wolf.MaxHealth = wolf.Health = kind.Health * kind.LevelFactor(wolf.Level);
            wolf.facing = (float)wolf.random.NextDouble() * Mathf.PI * 2;
            wolf.roamTo = at;
            wolf.nextBolt = Time.time + 2 + (float)wolf.random.NextDouble() * 2;
            wolf.nextHowl = Time.time + 3;
            wolf.nextShield = Time.time + kind.ShieldEvery * 0.5f;
            wolf.nextStomp = Time.time + 3;

            if (kind.BoltEvery > 0)
            {
                wolf.boltKind = new Cultivation.GuKind
                {
                    Id = "wolf_lightning",
                    Name = "Молния",
                    Rank = Mathf.Max(1, kind.Tier + 1),
                    Range = kind.BoltRange,
                    Reach = kind.BoltRange,
                    Colour = new Color(0.55f, 0.75f, 1f),
                };
            }

            wolf.Build();

            return wolf;
        }

        void OnEnable()
        {
            All.Add(this);
            Foes.All.Add(this);
        }

        void OnDisable()
        {
            All.Remove(this);
            Foes.All.Remove(this);
        }

        void OnDestroy()
        {
            if (shieldGlow != null)
            {
                shieldGlow.Release();
            }
        }

        // ── The figure ────────────────────────────────────────────────

        void Build()
        {
            if (!paints.TryGetValue(Kind.Id, out var colours))
            {
                var back = Paint(Kind.Name + " Back", Kind.Back);

                // Lightning in the fur: its saddle glows faintly.
                if (Kind.Glows)
                {
                    back.EnableKeyword("_EMISSION");
                    back.SetColor("_EmissionColor", Kind.Back * 0.9f + new Color(0.1f, 0.2f, 0.5f) * 0.6f);
                }

                colours = new[] { Paint(Kind.Name + " Fur", Kind.Fur), back, Paint(Kind.Name + " Belly", Kind.Belly) };
                paints[Kind.Id] = colours;
            }

            nose ??= Paint("Wolf Nose", new Color(0.06f, 0.05f, 0.05f));

            if (gold == null)
            {
                gold = Paint("Wolf Gold", new Color(0.95f, 0.75f, 0.3f));
                gold.EnableKeyword("_EMISSION");
                gold.SetColor("_EmissionColor", new Color(0.6f, 0.4f, 0.1f));
            }

            var fur = colours[0];
            var furDark = colours[1];
            var furLight = colours[2];
            // Each wolf its own eyes (they turn red when it goes berserk).
            eyeGlow = Paint(Kind.Name + " Eye", Kind.Eye);
            eyeGlow.EnableKeyword("_EMISSION");
            eyeGlow.SetColor("_EmissionColor", Kind.Eye * 1.8f);

            figure = new GameObject("Figure").transform;
            figure.SetParent(transform, false);
            figure.localScale = Vector3.one * Kind.Scale;

            // The body: a long barrel, a deeper chest before it, a darker saddle over the back.
            Part(PrimitiveType.Capsule, figure, new Vector3(0, 0.6f, -0.08f), new Vector3(90, 0, 0), new Vector3(0.4f, 0.55f, 0.38f), fur);
            Part(PrimitiveType.Sphere, figure, new Vector3(0, 0.64f, 0.32f), Vector3.zero, new Vector3(0.44f, 0.5f, 0.5f), fur);
            Part(PrimitiveType.Sphere, figure, new Vector3(0, 0.74f, -0.05f), Vector3.zero, new Vector3(0.36f, 0.16f, 0.8f), furDark);
            Part(PrimitiveType.Sphere, figure, new Vector3(0, 0.48f, 0.18f), Vector3.zero, new Vector3(0.3f, 0.2f, 0.62f), furLight);

            // A king's mane over its shoulders.
            if (Kind.Tier > 0)
            {
                Part(PrimitiveType.Sphere, figure, new Vector3(0, 0.78f, 0.36f), Vector3.zero, new Vector3(0.5f, 0.36f, 0.42f), furDark);
            }

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
                Part(PrimitiveType.Sphere, head, new Vector3(side * 0.08f, 0.04f, 0.12f), Vector3.zero, new Vector3(0.045f, 0.035f, 0.035f), eyeGlow);
            }

            // The thunder crown: horns of gold rising back from its brow.
            if (Kind.Crown)
            {
                for (var i = -2; i <= 2; i++)
                {
                    Part(PrimitiveType.Capsule, head, new Vector3(i * 0.045f, 0.17f - Mathf.Abs(i) * 0.02f, -0.02f), new Vector3(-30, 0, i * -16),
                        new Vector3(0.035f, 0.1f - Mathf.Abs(i) * 0.015f, 0.035f), gold);
                }
            }

            // The tail, hanging back and down.
            tail = new GameObject("Tail").transform;
            tail.SetParent(figure, false);
            tail.localPosition = new Vector3(0, 0.68f, -0.6f);
            Part(PrimitiveType.Capsule, tail, new Vector3(0, -0.12f, -0.14f), new Vector3(-50, 0, 0), new Vector3(0.13f, 0.2f, 0.13f), fur);
            Part(PrimitiveType.Capsule, tail, new Vector3(0, -0.3f, -0.22f), new Vector3(-20, 0, 0), new Vector3(0.11f, 0.14f, 0.11f), Kind.Crown ? gold : furDark);

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
                Part(PrimitiveType.Sphere, knees[i], new Vector3(0, -0.25f, 0.03f), Vector3.zero, new Vector3(0.1f, 0.06f, 0.13f), Kind.Crown ? gold : furDark);
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

        /// <summary>How hard it is now (a howl's fury, berserk): its bite and its lightning scaled by this.</summary>
        float Fury => Kind.LevelFactor(Level) * (frenzied ? 1.5f : 1) * (Maddened ? 1.3f : 1);

        float ChaseSpeed => Kind.Chase * (frenzied ? 1.4f : 1) * (Maddened ? 1.2f : 1);

        float Reach => BiteReach * Mathf.Sqrt(Kind.Scale) + 0.4f * (Kind.Scale - 1);

        void Update()
        {
            var dt = Time.deltaTime;

            if (dt <= 0)
            {
                return;
            }

            if (state == Mind.Dead)
            {
                Lie(dt);

                return;
            }

            biteReady -= dt;

            // Burning, bleeding, poisoned: harm every second while it lasts.
            if (Time.time < hurtUntil)
            {
                Hurt(hurtPerSecond * dt, new Strike { Direction = transform.forward, Point = transform.position + Vector3.up * Height * 0.6f });

                if (state == Mind.Dead)
                {
                    return;
                }
            }

            // A healing Gu in its body: its wounds close while it is not struck.
            if (Kind.Regen > 0 && Time.time - StruckAt > 3 && Health < MaxHealth)
            {
                Health = Mathf.Min(MaxHealth, Health + Kind.Regen * dt);
            }

            if (shieldGlow != null && !Shielded)
            {
                shieldGlow.Release();
                shieldGlow = null;
            }

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

            // Tamed, it keeps by the hero; afraid, it runs; asleep, held or blinded, it cannot go after anyone.
            if (Tamed)
            {
                return;
            }

            if (Time.time < fearUntil)
            {
                state = Mind.Flee;

                return;
            }

            if (Time.time < sleepUntil || Time.time < blindUntil)
            {
                if (state == Mind.Chase)
                {
                    state = Mind.Roam;
                    roamTo = transform.position;
                }

                return;
            }

            // A hidden hero is seen only from closer, an unseen one not at all — and lost, once out of reach. A lightning eye sees through it all.
            var notice = Kind.SeesHidden ? 1 : Notice;
            var sees = (Night ? SeesByNight : SeesByDay) * Kind.Sight * notice;

            if (state == Mind.Chase && notice <= 0 && heroDistance > Reach * 2)
            {
                state = Mind.Roam;
                roamTo = transform.position;

                return;
            }

            switch (state)
            {
                case Mind.Roam:
                    if (!heroDown && heroDistance < sees)
                    {
                        Hunt();
                    }

                    break;

                case Mind.Chase:
                    // Too far from the den, the hero gone or down: back to roaming.
                    if (heroDown || fromDen > GivesUp * Kind.Sight || heroDistance > GivesUp * 0.7f * Kind.Sight)
                    {
                        state = Mind.Roam;
                        roamTo = Den;
                    }
                    else
                    {
                        if (heroDistance < Reach && biteReady <= 0)
                        {
                            Bite();
                        }

                        Gifts(heroDistance);
                    }

                    break;

                case Mind.Flee:
                    if (fromDen < 6)
                    {
                        state = Mind.Roam;
                        // Rested a little, it may come again.
                        Health = Mathf.Min(MaxHealth, Health + MaxHealth * 0.25f);
                    }

                    break;
            }
        }

        /// <summary>What a beast king's Gu do in a fight: lightning spat, a howl, a shield raised, a stamp of its forepaws.</summary>
        void Gifts(float heroDistance)
        {
            if (Kind.BoltEvery > 0 && Time.time >= nextBolt && heroDistance > Reach * 1.5f && heroDistance < Kind.BoltRange)
            {
                nextBolt = Time.time + Kind.BoltEvery * (0.8f + 0.4f * (float)random.NextDouble()) * (frenzied ? 0.6f : 1);
                Spit();
            }

            if (Kind.HowlEvery > 0 && Time.time >= nextHowl)
            {
                nextHowl = Time.time + Kind.HowlEvery;
                Howl();
            }

            if (Kind.ShieldEvery > 0 && Time.time >= nextShield)
            {
                nextShield = Time.time + Kind.ShieldEvery;
                shieldUntil = Time.time + Kind.ShieldFor;
                shieldGlow?.Release();
                shieldGlow = Cultivation.GuGlow.Hold(transform, 0.9f * Kind.Scale, new Color(0.5f, 0.7f, 1f));
                Roared?.Invoke(this, "поднимает громовой щит");
            }

            if (Kind.StompEvery > 0 && Time.time >= nextStomp && heroDistance < Kind.StompRadius * 0.85f)
            {
                nextStomp = Time.time + Kind.StompEvery;
                Stomp();
            }
        }

        /// <summary>Lightning spat from its jaws at the hero (where they will be, a little ahead).</summary>
        void Spit()
        {
            if (hero == null || boltKind == null)
            {
                return;
            }

            var mouth = head.position + head.forward * 0.3f * Kind.Scale;
            var target = hero.position + Vector3.up * 1.1f;

            if (hero.TryGetComponent<Physics.HeroMotor>(out var motor))
            {
                target += new Vector3(motor.Velocity.x, 0, motor.Velocity.z) * (Vector3.Distance(mouth, target) / 32 * 0.6f);
            }

            lunge = 0;
            Cultivation.GuBolt.Throw(mouth, (target - mouth).normalized, boltKind, controller, Kind.BoltPower * Fury);
        }

        /// <summary>A howl: the wolves of its pack about it go into a fury for a while.</summary>
        void Howl()
        {
            howledUntil = Time.time + 8;

            foreach (var other in All)
            {
                if (other != this && other.Pack == Pack && !other.Dead && !other.Tamed && Flat(other.transform.position - transform.position).magnitude < 40)
                {
                    other.howledUntil = Time.time + 8;

                    if (other.state == Mind.Roam)
                    {
                        other.state = Mind.Chase;
                    }
                }
            }

            Cultivation.GuGlow.Flash(transform.position + Vector3.up * Height * 0.6f, 4 * Kind.Scale, new Color(0.6f, 0.75f, 1f), 0.6f);
            Roared?.Invoke(this, "воет — стая звереет");
        }

        /// <summary>A stamp of the forepaws: lightning through the ground about it, striking and shocking the hero if near.</summary>
        void Stomp()
        {
            lunge = 0;
            Cultivation.GuGlow.Flash(transform.position + Vector3.up * 0.2f, Kind.StompRadius, new Color(0.55f, 0.75f, 1f), 0.5f);

            if (heroHealth != null && Flat(hero.position - transform.position).magnitude < Kind.StompRadius)
            {
                var away = Flat(hero.position - transform.position).normalized;
                heroHealth.TakeStrike(new Strike { Power = Kind.StompPower * Fury, Sharpness = 0.6f, Point = hero.position + Vector3.up * 0.3f, Direction = away, Source = gameObject },
                    BodyPart.ShinL);
                heroHealth.Shock(Kind.Shock * 1.5f);
            }

            Roared?.Invoke(this, "бьёт лапами — молния по земле");
        }

        /// <summary>Turns on the hero, and the pack with it.</summary>
        void Hunt()
        {
            if (state == Mind.Dead || state == Mind.Flee)
            {
                return;
            }

            state = Mind.Chase;

            foreach (var other in All)
            {
                if (other != this && other.Pack == Pack && other.state == Mind.Roam && !other.Tamed && Time.time >= other.sleepUntil && Flat(other.transform.position - transform.position).magnitude < 60)
                {
                    other.state = Mind.Chase;
                }
            }
        }

        void Bite()
        {
            // A scarecrow takes the bite instead.
            if (DecoyActive && Flat(Decoy.position - transform.position).magnitude < Reach * 1.5f)
            {
                biteReady = Kind.BiteEvery;
                lunge = 0;

                return;
            }

            biteReady = Kind.BiteEvery * (frenzied ? 0.7f : 1) * (0.85f + 0.3f * (float)random.NextDouble());
            lunge = 0;

            // Mostly the legs and arms, now and then higher — a big wolf reaches the body and the head.
            var parts = Kind.Scale < 1.5f
                ? new[] { BodyPart.ShinL, BodyPart.ShinR, BodyPart.ThighL, BodyPart.ThighR, BodyPart.ForearmL, BodyPart.ForearmR, BodyPart.HandR, BodyPart.Belly }
                : new[] { BodyPart.Chest, BodyPart.Belly, BodyPart.ThighL, BodyPart.ThighR, BodyPart.UpperArmL, BodyPart.UpperArmR, BodyPart.Head };
            var part = parts[random.Next(parts.Length)];
            var toHero = (hero.position - transform.position).normalized;
            heroHealth?.TakeStrike(new Strike { Power = Kind.Bite * Fury, Sharpness = 0.8f, Point = hero.position + Vector3.up * 0.5f, Direction = toHero, Source = gameObject }, part);

            if (Kind.Shock > 0)
            {
                heroHealth?.Shock(Kind.Shock);
            }
        }

        void Move(float dt)
        {
            Vector3 goal;
            float wanted;

            switch (state)
            {
                case Mind.Chase:
                    goal = DecoyActive && Flat(Decoy.position - transform.position).magnitude < 40 ? Decoy.position : hero.position;
                    // Close in, then hold at the bite's reach.
                    wanted = Flat(goal - transform.position).magnitude > Reach * 0.8f ? ChaseSpeed : 0;
                    break;

                case Mind.Flee:
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

            if (Tamed && hero != null)
            {
                // At the hero's side, a little behind.
                goal = hero.position - hero.forward * 2 * Kind.Scale + hero.right * 1.5f * Kind.Scale;
                var far = Flat(goal - transform.position).magnitude;
                wanted = far > 12 ? FleeSpeed : far > 2 ? WanderSpeed * 2.5f : 0;
            }

            // Held fast or asleep it stands; slowed, it goes slower.
            if (Time.time < rootUntil || Time.time < sleepUntil)
            {
                wanted = 0;
            }
            else if (Time.time < slowUntil)
            {
                wanted *= 1 - slowBy;
            }

            var to = Flat(goal - transform.position);

            if (to.sqrMagnitude > 0.01f)
            {
                var target = Mathf.Atan2(to.x, to.z);
                facing += Mathf.DeltaAngle(facing * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad * (1 - Mathf.Exp(-8 * dt));
            }

            speed = Mathf.MoveTowards(speed, wanted, dt * (wanted > speed ? 10 : 14));
            verticalSpeed = controller.isGrounded ? -1 : verticalSpeed - Gravity * dt;

            // Thrown by a blow: up off its feet, and sliding back until the ground stops it.
            if (push.y > 0)
            {
                verticalSpeed = push.y;
                push.y = 0;
            }

            var forward = new Vector3(Mathf.Sin(facing), 0, Mathf.Cos(facing));
            controller.Move((forward * speed + Vector3.up * verticalSpeed + push) * dt);
            push = Vector3.MoveTowards(push, Vector3.zero, (controller.isGrounded ? 16 : 4) * dt);
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
            var pace = speed / Mathf.Max(1, ChaseSpeed);
            stride += dt * (2.2f + speed * 1.6f / Mathf.Sqrt(Kind.Scale));
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

            figure.localPosition = new Vector3(0, bob, lean * 0.25f) * Kind.Scale;
            figure.localRotation = Quaternion.Euler(lean * 10, 0, 0);
            head.localRotation = Quaternion.Euler(-10 + lean * 25 + Mathf.Sin(stride * 2) * 3 * pace, 0, 0);
            tail.localRotation = Quaternion.Euler(state == Mind.Flee ? 40 : -10 * pace, Mathf.Sin(Time.time * (state == Mind.Chase ? 6 : 2)) * 14, 0);
        }

        bool DecoyActive => Decoy != null && Time.time < DecoyUntil;

        /// <summary>
        /// What a Gu does to it, for some seconds: slows it by `power` (0…1), holds it fast, puts it to sleep
        /// (till it is struck), blinds it, frightens it off, harms it `power` a second (fire, poison, bleeding),
        /// or enslaves it for good. A beast king shrugs off the most of it: half as long, and never enslaved
        /// below its own rung.
        /// </summary>
        public void Afflict(Affliction kind, float seconds, float power)
        {
            if (state == Mind.Dead || (seconds <= 0 && kind != Affliction.Tame))
            {
                return;
            }

            if (Kind.Tier > 0)
            {
                seconds *= 0.5f;

                if (kind == Affliction.Tame || kind == Affliction.Fear)
                {
                    return;
                }
            }

            var until = Time.time + seconds;

            switch (kind)
            {
                case Affliction.Slow:
                    slowBy = Mathf.Clamp01(Mathf.Max(power, Time.time < slowUntil ? slowBy : 0));
                    slowUntil = Mathf.Max(slowUntil, until);
                    break;
                case Affliction.Root:
                    rootUntil = Mathf.Max(rootUntil, until);
                    break;
                case Affliction.Sleep:
                    sleepUntil = Mathf.Max(sleepUntil, until);
                    break;
                case Affliction.Blind:
                    blindUntil = Mathf.Max(blindUntil, until);
                    break;
                case Affliction.Fear:
                    fearUntil = Mathf.Max(fearUntil, until);
                    state = Mind.Flee;
                    break;
                case Affliction.Burn:
                    hurtPerSecond = Time.time < hurtUntil ? Mathf.Max(power, hurtPerSecond) : power;
                    hurtUntil = Mathf.Max(hurtUntil, until);
                    break;
                case Affliction.Tame:
                    Tamed = true;
                    state = Mind.Roam;
                    break;
            }
        }

        /// <summary>Thrown off balance (its bite parried): it reels back and cannot bite for a while — a king for less.</summary>
        public void Stagger(float seconds, Vector3 away)
        {
            if (state == Mind.Dead)
            {
                return;
            }

            seconds /= Mathf.Max(1, Kind.Tier);
            rootUntil = Mathf.Max(rootUntil, Time.time + seconds);
            biteReady = Mathf.Max(biteReady, seconds + 0.3f);
            Push(away.normalized * 4 + Vector3.up * 1.5f);
        }

        /// <summary>Whether something holds it in place now (the checks look).</summary>
        public bool Held => Time.time < rootUntil || Time.time < sleepUntil;

        // ── Struck ────────────────────────────────────────────────────

        /// <summary>
        /// A blow: what it does is how its strength measures against the wolf's toughness (Combat.Wounds) times a
        /// share of its strength — a youth's fist hardly scratches a beast king, a high-rank Gu cuts deep — and a
        /// thunder shield takes most of it.
        /// </summary>
        public void TakeStrike(Strike strike, BodyPart? part)
        {
            if (state == Mind.Dead)
            {
                return;
            }

            var power = strike.Power * Mathf.Clamp01(strike.Sharpness <= 0 ? 1 : strike.Sharpness);
            var toughness = Kind.Toughness * (1 + 0.04f * (Level - Kind.MinLevel));
            var harm = Wounds.Scale(power, toughness) * Mathf.Max(30, power * 0.3f) * (Shielded ? 0.4f : 1);
            Hurt(harm, strike);

            if (state == Mind.Dead || harm < 0.5f)
            {
                return;
            }

            if (Tamed)
            {
                return;
            }

            // An ordinary wolf badly hurt runs for its den; a king fights on — and, badly hurt, goes berserk.
            if (Kind.FrenzyBelow > 0 && !frenzied && Health < MaxHealth * Kind.FrenzyBelow)
            {
                frenzied = true;
                eyeGlow.SetColor("_BaseColor", new Color(1, 0.2f, 0.15f));
                eyeGlow.SetColor("_EmissionColor", new Color(1, 0.15f, 0.1f) * 2.4f);
                Cultivation.GuGlow.Flash(transform.position + Vector3.up * Height * 0.6f, 1.6f * Kind.Scale, new Color(1, 0.25f, 0.2f), 0.7f);
                Roared?.Invoke(this, "впадает в неистовство");
            }

            if (Kind.Tier == 0 && Health < MaxHealth * 0.3f)
            {
                state = Mind.Flee;
            }
            else if (Time.time >= blindUntil)
            {
                Hunt();
            }
        }

        /// <summary>Harm taken (a blow, a burn): reported where it landed; the last of it kills.</summary>
        void Hurt(float harm, Strike strike)
        {
            if (harm <= 0)
            {
                return;
            }

            Health = Mathf.Max(0, Health - harm);
            StruckAt = Time.time;
            // A blow wakes it.
            sleepUntil = 0;

            // A burn's every moment is too small to tell; a blow is told.
            if (harm >= 0.5f)
            {
                Wounds.Report(Kind.Name, null, harm, Health <= 0, strike.Point);
            }

            if (Health <= 0)
            {
                Die(strike);
            }
        }

        void Die(Strike strike)
        {
            state = Mind.Dead;
            speed = 0;
            controller.enabled = false;
            deadFor = 0;
            shieldGlow?.Release();
            shieldGlow = null;
            var away = strike.Direction;
            away.y = 0;

            // Fallen on its side, away from the blow.
            if (away.sqrMagnitude > 0.01f)
            {
                facing = Mathf.Atan2(away.x, away.z) + Mathf.PI / 2;
                transform.rotation = Quaternion.Euler(0, facing * Mathf.Rad2Deg, 0);
            }

            figure.localRotation = Quaternion.Euler(0, 0, 90);
            figure.localPosition = new Vector3(-0.35f, 0.18f, 0) * Kind.Scale;
            Killed?.Invoke(this);
        }

        /// <summary>Lying dead a while, then sinking out of sight.</summary>
        void Lie(float dt)
        {
            deadFor += dt;

            if (deadFor > 25)
            {
                figure.localPosition += Vector3.down * dt * 0.4f * Kind.Scale;
            }

            if (deadFor > 30)
            {
                Destroy(gameObject);
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
