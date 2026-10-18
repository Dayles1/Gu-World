using System;
using System.Collections.Generic;
using GuWorld.Body;
using GuWorld.Combat;
using GuWorld.Cultivation;
using GuWorld.Render;
using GuWorld.Systems;
using UnityEngine;

namespace GuWorld.Creatures
{
    /// <summary>
    /// A Gu Master of another clan (MasterKind): a person like the villagers (PersonFigure, wounded part by part — HitZones),
    /// who fights with the Gu in their aperture.
    ///
    /// Out of a fight they keep their post, strolling about it. In their clan's land, seeing the hero near, they warn them
    /// off; the hero lingering, they attack — and struck, they attack at once (on the proving ground they fight on sight).
    /// In a fight they keep the distance they like (a Bai with icicles well back, a Xiong with the bear's strength up
    /// close), circling the hero; they throw their Gu at where the hero will be, sweep with their close-quarters Gu, raise
    /// their shield when hurt, close in with their fists; they see a missile or a blow coming and roll out of its way.
    /// Their essence runs low in a long fight, and then only the fists are left. An ordinary one badly hurt runs; a leader
    /// fights to the end. A parried blow staggers them; fear sends them off; a Gu cannot enslave a person.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class GuMaster : MonoBehaviour, IStrikable, IZoned, IStaggerable, IFoe
    {
        const float Gravity = 22;
        const float WalkSpeed = 1.4f;
        /// <summary>How far they see the hero (less at night), m; how far from their post they give up the chase.</summary>
        const float Sees = 28;
        const float GivesUp = 110;
        /// <summary>A roll: how long, how far, how long of it nothing touches them, how long before the next.</summary>
        const float RollTime = 0.5f;
        const float RollDistance = 4.4f;
        const float RollUntouched = 0.34f;
        const float RollRest = 2.2f;
        /// <summary>A throw: how long the arm takes, and when in it the Gu leaves the hand.</summary>
        const float ThrowTime = 0.42f;
        const float ThrowsAt = 0.55f;
        /// <summary>What each Gu costs of their essence (of 100), and how fast it comes back in a fight and out of one, a second.</summary>
        const float FarCost = 12;
        const float CloseCost = 10;
        const float ShieldCost = 15;
        const float RefillFighting = 0.8f;
        const float RefillResting = 4;

        public static readonly List<GuMaster> All = new List<GuMaster>();
        /// <summary>A Gu Master killed (the hero takes their stones, perhaps a Gu).</summary>
        public static event Action<GuMaster> Killed;
        /// <summary>Something a Gu Master says aloud (a warning, a war cry).</summary>
        public static event Action<GuMaster, string> Spoke;

        public MasterKind Kind { get; private set; }
        public string Title { get; private set; }
        public string Rung => $"{Kind.RankName} · {MasterKinds.ClanName(Kind.Clan)}";
        public int Tier => Kind.Tier;
        public int Level => Kind.Rank;
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public bool Dead => mind == Mind.Dead;
        public bool Hunting => mind == Mind.Fight;
        public bool Tamed => false;
        public bool Shielded => Time.time < shieldUntil;
        public bool Berserk => false;
        public float StruckAt { get; private set; } = -100;
        public float Height { get; private set; }
        public string State => (Shielded && shieldKind != null ? $"  ·  {shieldKind.SkillName.ToUpperInvariant()}" : "") + (Essence < FarCost ? "  ·  ЭССЕНЦИЯ НА ИСХОДЕ" : "");

        /// <summary>Fights the hero on sight (the proving ground's); otherwise only in their land after a warning, or struck.</summary>
        public bool Hostile;
        /// <summary>Where they keep: their post, strolled about.</summary>
        public Vector3 Post;
        /// <summary>Their land: within this of its centre they warn the hero off (none: anywhere).</summary>
        public Vector3 Land;
        public float LandRadius = float.PositiveInfinity;
        /// <summary>The essence in their aperture, of 100.</summary>
        public float Essence { get; private set; } = 100;

        enum Mind
        {
            Watch,
            Warn,
            Fight,
            Flee,
            Dead,
        }

        Mind mind;
        CharacterController controller;
        PersonFigure figure;
        List<HitZone> zones;
        Transform hero;
        Health heroHealth;
        Melee heroMelee;
        System.Random random;
        GuKind farKind, closeKind, shieldKind, mightKind;

        Vector3 velocity;
        Vector3 push;
        float verticalSpeed;
        float facing;
        float turnRate;
        Vector3 strollTo;
        float strollWait;
        float strafe = 1;
        float strafeUntil;
        float warnedAt;
        float deadFor;

        float nextFar, nextClose, nextShield, nextBlow, nextDodgeLook, rollReady;
        float throwing = -1;
        float rolling = -1;
        Vector3 rollWay;
        bool rollBack;
        float blowing = -1;
        Blow blow;
        bool blowLanded;
        bool blowIsGu;
        int chain;
        float shieldUntil;
        GuGlow shieldGlow;

        // What Gu have done to them (Time.time until each lasts; how strong).
        float slowUntil, slowBy, rootUntil, sleepUntil, blindUntil, fearUntil, hurtUntil, hurtPerSecond, staggerUntil;

        static readonly HairStyle[] MenHair = { HairStyle.Short, HairStyle.Parted, HairStyle.Ponytail, HairStyle.Long, HairStyle.Bald };
        static readonly HairStyle[] WomenHair = { HairStyle.Buns, HairStyle.Ponytail, HairStyle.Long, HairStyle.Bob };

        /// <summary>A Gu Master of a kind at a point (on the ground there), after the hero.</summary>
        public static GuMaster Make(Vector3 at, MasterKind kind, Transform hero, HumanBody.Parts parts, int seed, bool hostile)
        {
            var random = new System.Random(seed);
            float R() => (float)random.NextDouble();
            var woman = R() < 0.3f;
            var gender = woman ? Gender.Female : Gender.Male;
            var height = kind.Youth ? (woman ? 1.58f : 1.64f) + R() * 0.06f : woman ? 1.6f + R() * 0.1f : 1.72f + R() * 0.12f;
            var title = MasterKinds.NameFor(kind.Clan, random);
            var go = new GameObject(title);
            go.transform.position = at;
            var controller = go.AddComponent<CharacterController>();
            controller.radius = 0.3f;
            controller.height = height;
            controller.center = new Vector3(0, height / 2, 0);
            controller.slopeLimit = 50;
            controller.stepOffset = 0.4f;
            controller.skinWidth = 0.03f;
            var master = go.AddComponent<GuMaster>();
            master.controller = controller;
            master.Kind = kind;
            master.Title = title;
            master.Height = height;
            master.Hostile = hostile;
            master.Post = at;
            master.Land = at;
            master.hero = hero;
            master.heroHealth = hero != null ? hero.GetComponent<Health>() : null;
            master.heroMelee = hero != null ? hero.GetComponent<Melee>() : null;
            master.random = random;
            master.MaxHealth = master.Health = kind.Health;
            master.facing = R() * Mathf.PI * 2;
            master.strollTo = at;
            master.farKind = GuBook.Find(kind.Far);
            master.closeKind = GuBook.Find(kind.Close);
            master.shieldKind = GuBook.Find(kind.Shield);
            master.mightKind = GuBook.Find(kind.Might);
            master.nextFar = Time.time + 1 + R() * 1.5f;

            var styles = woman ? WomenHair : MenHair;
            var beard = !woman && !kind.Youth && R() < (kind.Rank >= 3 ? 0.8f : 0.3f) ? (Beard)(1 + random.Next(4)) : Beard.None;
            var look = Look.Default(gender).With(hair: styles[random.Next(styles.Length)], beard: beard, robe: kind.Robe, skin: kind.Clan == "xiong" ? Skin.Tan : (Skin)random.Next(3));
            var build = new HumanBody.Build
            {
                youth = kind.Youth ? 1 : kind.Rank >= 3 ? 0 : 0.2f + R() * 0.3f,
                hair = kind.Rank >= 3 && R() < 0.6f ? new Color(0.5f, 0.49f, 0.5f) : Look.HairBlack,
                dye = look.Dye,
                emblem = kind.Clan == "gu_yue" ? Emblem.Crescent : Emblem.None,
                skin = look.SkinBrightness,
            };
            master.figure = new PersonFigure(go.transform, parts, look, build, height);

            foreach (var renderer in master.figure.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.updateWhenOffscreen = false;
            }

            master.zones = HitZones.Build(master.figure.Rig, master);
            go.transform.rotation = Quaternion.Euler(0, master.facing * Mathf.Rad2Deg, 0);

            return master;
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

        void OnDestroy() => shieldGlow?.Release();

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        bool Night
        {
            get
            {
                var sun = RenderSettings.sun;

                return sun != null && sun.transform.forward.y > -0.05f;
            }
        }

        // ── Living ────────────────────────────────────────────────────

        void Update()
        {
            var dt = Time.deltaTime;

            if (dt <= 0)
            {
                return;
            }

            if (mind == Mind.Dead)
            {
                Lie(dt);

                return;
            }

            // Burning, bleeding, poisoned: harm every second while it lasts.
            if (Time.time < hurtUntil)
            {
                Hurt(hurtPerSecond * dt, new Strike { Direction = transform.forward, Point = transform.position + Vector3.up * Height * 0.6f }, null);

                if (mind == Mind.Dead)
                {
                    return;
                }
            }

            Essence = Mathf.Min(100, Essence + (mind == Mind.Fight ? RefillFighting : RefillResting) * dt);

            if (shieldGlow != null && !Shielded)
            {
                shieldGlow.Release();
                shieldGlow = null;
            }

            Think(dt);
            Move(dt);
            Act(dt);
            Animate(dt);
        }

        bool Busy => throwing >= 0 || rolling >= 0 || blowing >= 0;

        /// <summary>Held fast, asleep or reeling: they can do nothing.</summary>
        bool Stopped => Time.time < rootUntil || Time.time < sleepUntil || Time.time < staggerUntil;

        void Think(float dt)
        {
            if (hero == null)
            {
                return;
            }

            var distance = Flat(hero.position - transform.position).magnitude;
            var heroDown = heroHealth != null && heroHealth.Down;
            var sees = Sees * (Night ? 0.7f : 1) * Wolf.Notice;

            if (Time.time < fearUntil)
            {
                mind = Mind.Flee;

                return;
            }

            if (Time.time < sleepUntil)
            {
                return;
            }

            switch (mind)
            {
                case Mind.Watch:
                    if (heroDown || Time.time < blindUntil)
                    {
                        break;
                    }

                    if (Hostile && distance < sees)
                    {
                        Fight(Hostile && Kind.Clan == "gu_yue" ? "К бою!" : WarCry());
                    }
                    else if (!Hostile && distance < sees * 0.8f && Flat(hero.position - Land).magnitude < LandRadius)
                    {
                        mind = Mind.Warn;
                        warnedAt = Time.time;
                        Say(Warning());
                    }

                    break;

                case Mind.Warn:
                    if (distance > sees * 1.6f || Flat(hero.position - Land).magnitude > LandRadius + 20)
                    {
                        mind = Mind.Watch;
                    }
                    else if (Time.time - warnedAt > 7 && distance < sees)
                    {
                        Fight(WarCry());
                    }

                    break;

                case Mind.Fight:
                    if (heroDown || distance > GivesUp * 0.6f || Flat(transform.position - Post).magnitude > GivesUp)
                    {
                        mind = Mind.Watch;
                        strollTo = Post;

                        if (heroDown)
                        {
                            Say(Kind.Clan == "xiong" ? "Лежи и не вставай, гуюэвец." : "Знай своё место.");
                        }
                    }

                    break;

                case Mind.Flee:
                    if (distance > 45)
                    {
                        mind = Mind.Watch;
                        strollTo = Post;
                        Health = Mathf.Min(MaxHealth, Health + MaxHealth * 0.2f);
                    }

                    break;
            }
        }

        void Fight(string cry)
        {
            if (mind == Mind.Fight || mind == Mind.Dead)
            {
                return;
            }

            mind = Mind.Fight;
            Hostile = true;
            nextFar = Mathf.Max(nextFar, Time.time + 0.6f);

            if (cry != null)
            {
                Say(cry);
            }
        }

        string Warning() => Kind.Clan switch
        {
            "xiong" => "Эй, гуюэвец! Земля Сюн не для тебя. Проваливай, пока цел.",
            _ => "Стой! Это земля клана Бай. Уходи — второй раз говорить не стану.",
        };

        string WarCry() => Kind.Clan switch
        {
            "xiong" => random.Next(2) == 0 ? "Сам напросился!" : "Сила медведя — посмотрим, что ты против неё!",
            "gu_yue" => "К бою!",
            _ => random.Next(2) == 0 ? "Не понял слов — поймёшь льдом!" : "Клан Бай не прощает чужаков.",
        };

        void Say(string line) => Spoke?.Invoke(this, line);

        /// <summary>What they do in a fight this moment: roll from danger, shield, sweep, strike, throw.</summary>
        void Act(float dt)
        {
            if (mind != Mind.Fight || hero == null || Stopped || Busy || Time.time < blindUntil)
            {
                return;
            }

            var toHero = Flat(hero.position - transform.position);
            var distance = toHero.magnitude;

            // A missile or a blow coming: out of its way, if quick enough to see it.
            if (Time.time >= rollReady && Time.time >= nextDodgeLook)
            {
                nextDodgeLook = Time.time + 0.2f;

                if (Missiles.Coming(transform.position + Vector3.up * Height * 0.6f, controller, 1.3f, 0.45f, out var heading) && random.NextDouble() < Kind.Dodge)
                {
                    var side = Vector3.Cross(Vector3.up, Flat(heading)).normalized;
                    Roll(random.Next(2) == 0 ? side : -side);

                    return;
                }

                if (heroMelee != null && heroMelee.Current.HasValue && heroMelee.Phase < 0.35f && distance < 2.6f && random.NextDouble() < Kind.Dodge * 0.6f)
                {
                    Roll(-toHero);

                    return;
                }
            }

            // Hurt: up with the shield.
            if (shieldKind != null && !Shielded && Time.time >= nextShield && Essence >= ShieldCost && (Health < MaxHealth * 0.8f || distance < 3))
            {
                Essence -= ShieldCost;
                shieldUntil = Time.time + Kind.ShieldFor;
                nextShield = shieldUntil + 7;
                shieldGlow?.Release();
                shieldGlow = GuGlow.Hold(transform, 0.95f, shieldKind.Colour);
                DamageNumbers.Word(transform.position + Vector3.up * (Height + 0.3f), shieldKind.SkillName, shieldKind.Colour);
            }

            // Close: a sweep of a Gu, or the fists.
            if (closeKind != null && distance < closeKind.Reach + 0.4f && Time.time >= nextClose && Essence >= CloseCost)
            {
                Essence -= CloseCost;
                nextClose = Time.time + 2.6f + (float)random.NextDouble() * 1.5f;
                Strike(Blow.Hook, true);

                return;
            }

            if (distance < 1.75f && Time.time >= nextBlow)
            {
                var blows = Melee.Chain;
                Strike(blows[chain % blows.Length], false);
                chain++;

                // A run of two to four blows, then a pause.
                if (chain >= 2 + random.Next(3))
                {
                    chain = 0;
                    nextBlow = Time.time + 1.2f + (float)random.NextDouble();
                }

                return;
            }

            // From afar: a Gu thrown, when it reaches.
            if (farKind != null && Time.time >= nextFar && Essence >= FarCost && distance > 2.5f && distance < Mathf.Max(farKind.Range, farKind.Reach) * 0.9f)
            {
                Essence -= FarCost;
                nextFar = Time.time + Kind.FarEvery * (0.8f + 0.5f * (float)random.NextDouble());
                throwing = 0;
            }
        }

        void Strike(Blow kind, bool gu)
        {
            blow = kind;
            blowIsGu = gu;
            blowing = 0;
            blowLanded = false;
        }

        /// <summary>A roll the way given, turning into it (backward: still facing the hero).</summary>
        void Roll(Vector3 way)
        {
            way = Flat(way);

            if (way.sqrMagnitude < 0.01f)
            {
                way = -transform.forward;
            }

            rollWay = way.normalized;
            rollBack = Vector3.Dot(rollWay, transform.forward) < -0.5f;

            if (!rollBack)
            {
                facing = Mathf.Atan2(rollWay.x, rollWay.z);
            }

            rolling = 0;
            rollReady = Time.time + RollTime + RollRest;
            throwing = -1;
            blowing = -1;
        }

        bool Untouched => rolling >= 0 && rolling < RollUntouched;

        void Move(float dt)
        {
            var wanted = Vector3.zero;
            var look = (Vector3?)null;
            var toHero = hero != null ? Flat(hero.position - transform.position) : Vector3.zero;
            var distance = toHero.magnitude;

            switch (mind)
            {
                case Mind.Fight:
                    look = toHero;
                    var toward = distance > 0.01f ? toHero / distance : transform.forward;

                    if (Time.time >= strafeUntil)
                    {
                        strafe = random.Next(2) == 0 ? -1 : 1;
                        strafeUntil = Time.time + 1.5f + (float)random.NextDouble() * 2;
                    }

                    var side = Vector3.Cross(Vector3.up, toward) * strafe;

                    if (distance > Kind.Keep + 1.5f)
                    {
                        wanted = toward * Kind.Run + side * 0.6f;
                    }
                    else if (distance < Kind.Keep - 2.5f && Kind.Keep > 4)
                    {
                        wanted = -toward * 2.6f + side * 0.8f;
                    }
                    else
                    {
                        wanted = side * (distance < 3 ? 1.2f : 1.9f);
                    }

                    break;

                case Mind.Warn:
                    look = toHero;
                    break;

                case Mind.Flee:
                    wanted = -(distance > 0.01f ? toHero / distance : transform.forward) * Kind.Run * 1.1f;
                    look = wanted;
                    break;

                default:
                    strollWait -= dt;
                    var toStroll = Flat(strollTo - transform.position);

                    if (toStroll.magnitude < 1.2f)
                    {
                        if (strollWait <= 0)
                        {
                            var angle = (float)random.NextDouble() * Mathf.PI * 2;
                            strollTo = Post + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (3 + (float)random.NextDouble() * 10);
                            strollWait = 3 + (float)random.NextDouble() * 6;
                        }
                    }
                    else
                    {
                        wanted = toStroll.normalized * WalkSpeed;
                        look = toStroll;
                    }

                    break;
            }

            // Doing something, held, asleep: they stand (a blow carries them a little into it); slowed, slower.
            if (Busy || Stopped)
            {
                wanted = Vector3.zero;
            }
            else if (Time.time < slowUntil)
            {
                wanted *= 1 - slowBy;
            }

            if (blowing >= 0 && !blowLanded)
            {
                wanted = transform.forward * 1.2f;
            }

            velocity = Vector3.MoveTowards(velocity, wanted, dt * 14);

            // Facing: the hero in a fight, else the way they go (a roll its own).
            if (rolling < 0 && look.HasValue && look.Value.sqrMagnitude > 0.01f)
            {
                var target = Mathf.Atan2(look.Value.x, look.Value.z);
                var before = facing;
                facing += Mathf.DeltaAngle(facing * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad * (1 - Mathf.Exp(-10 * dt));
                turnRate = (facing - before) / dt;
            }
            else
            {
                turnRate = 0;
            }

            var step = velocity;

            if (rolling >= 0)
            {
                var before = Ease(rolling / RollTime);
                rolling += dt;
                var now = Ease(Mathf.Min(1, rolling / RollTime));
                step = rollWay * (RollDistance * (now - before) / dt);

                if (rolling >= RollTime)
                {
                    rolling = -1;
                }
            }

            verticalSpeed = controller.isGrounded ? -1 : verticalSpeed - Gravity * dt;

            if (push.y > 0)
            {
                verticalSpeed = push.y;
                push.y = 0;
            }

            if (controller.enabled)
            {
                controller.Move((step + Vector3.up * verticalSpeed + push) * dt);
            }

            push = Vector3.MoveTowards(push, Vector3.zero, (controller.isGrounded ? 16 : 4) * dt);
            transform.rotation = Quaternion.Euler(0, facing * Mathf.Rad2Deg, 0);

            // Fallen out of the world: back at the post.
            if (transform.position.y < -200)
            {
                controller.enabled = false;
                transform.position = Post + Vector3.up * 2;
                controller.enabled = true;
            }
        }

        static float Ease(float t)
        {
            var x = 1 - Mathf.Clamp01(t);

            return 1 - x * x;
        }

        /// <summary>The throw, the blow, the roll as they go on; the figure posed for them.</summary>
        void Animate(float dt)
        {
            var rig = figure.Rig;

            if (throwing >= 0)
            {
                var before = throwing;
                throwing += dt / ThrowTime;

                if (before < ThrowsAt && throwing >= ThrowsAt)
                {
                    Release();
                }

                if (throwing >= 1)
                {
                    throwing = -1;
                }
            }

            if (blowing >= 0)
            {
                var spec = Melee.Of(blow);
                var time = spec.Time * 1.1f;
                blowing += dt / time;

                if (!blowLanded && blowing >= spec.LandsAt)
                {
                    blowLanded = true;
                    LandBlow(spec);
                }

                if (blowing >= 1)
                {
                    blowing = -1;
                }
            }

            rig.Throw = throwing;
            rig.Blow = blow;
            rig.BlowPhase = blowing;
            rig.RollPhase = rolling >= 0 ? Mathf.Clamp01(rolling / RollTime) : -1;
            rig.RollBack = rollBack;
            var near = hero != null && Flat(hero.position - transform.position).magnitude < 4;
            rig.Guard = Mathf.MoveTowards(rig.Guard, mind == Mind.Fight && near && blowing < 0 ? 1 : 0, dt * 5);

            var distance = Camera.main != null ? Vector3.Distance(Camera.main.transform.position, transform.position) : 0;
            figure.SetVisible(distance < 160);
            figure.SetOutline(distance < 24);

            if (distance < 90)
            {
                var scale = figure.Scale;
                figure.Pose(dt, new MotionState
                {
                    speed = Flat(velocity).magnitude / scale,
                    walkSpeed = 1.4f / scale,
                    runSpeed = 3 / scale,
                    stance = Physics.Stance.Stand,
                    grounded = controller.isGrounded || rolling >= 0,
                    turnRate = -turnRate,
                });
            }
        }

        /// <summary>
        /// Their Gu leaves the hand: at the hero's body where they will be when it gets there — somewhere between the knees and
        /// the shoulders, not always the heart — and a little off, the more so for the lower ranks.
        /// </summary>
        void Release()
        {
            if (hero == null || farKind == null)
            {
                return;
            }

            var hand = figure.Rig.ElbowR != null ? figure.Rig.ElbowR.position + transform.forward * 0.3f : transform.position + Vector3.up * Height * 0.75f;
            var target = hero.position + Vector3.up * (0.6f + 0.9f * (float)random.NextDouble()) + hero.right * (((float)random.NextDouble() - 0.5f) * 0.6f);

            if (hero.TryGetComponent<Physics.HeroMotor>(out var motor))
            {
                target += Flat(motor.Velocity) * (Vector3.Distance(hand, target) / 31 * 0.7f);
            }

            var aim = (target - hand).normalized;
            var off = (4 - Kind.Rank) * 1.2f;
            aim = Quaternion.Euler(((float)random.NextDouble() - 0.5f) * off, ((float)random.NextDouble() - 0.5f) * off * 1.6f, 0) * aim;

            if (farKind.Skill == SkillKind.Moonblade)
            {
                Moonblade.Throw(hand, aim, farKind, controller, Kind.Power, random.Next(2) == 0 ? 14 : -14, farKind.Amount > 0 ? Mathf.Min(farKind.Amount, 1.4f) : 1);
            }
            else
            {
                GuBolt.Throw(hand, aim, farKind, controller, Kind.Power);
            }
        }

        /// <summary>A blow's moment: the fist (or the foot) finds the hero before them; a Gu's sweep strikes all in its arc.</summary>
        void LandBlow(Melee.Spec spec)
        {
            if (hero == null)
            {
                return;
            }

            if (blowIsGu && closeKind != null)
            {
                GuSkills.Blast(transform.position + Vector3.up * 1.1f + transform.forward * 0.5f, closeKind.Reach, Kind.Power * 1.1f, closeKind, controller, null, transform.forward);

                return;
            }

            var to = Flat(hero.position - transform.position);

            if (to.magnitude > spec.Reach * figure.Scale + 0.75f || Vector3.Dot(to.normalized, transform.forward) < 0.3f)
            {
                return;
            }

            var high = blow != Blow.Kick;
            var parts = high ? new[] { BodyPart.Head, BodyPart.Chest, BodyPart.Chest, BodyPart.Belly, BodyPart.Liver } : new[] { BodyPart.ThighL, BodyPart.ThighR, BodyPart.Belly };
            var might = mightKind != null ? 1 + mightKind.Amount : 1;
            heroHealth?.TakeStrike(new Strike
            {
                Power = Kind.Fist * might * spec.Power / 55,
                Sharpness = 1,
                Point = hero.position + Vector3.up * (high ? 1.45f : 0.9f),
                Direction = to.normalized,
                Source = gameObject,
            }, parts[random.Next(parts.Length)]);
        }

        // ── What is done to them ──────────────────────────────────────

        /// <summary>What a Gu does to them, for some seconds (as to a beast): slowed, held, asleep, blinded, afraid, burning. No Gu enslaves a person.</summary>
        public void Afflict(Affliction kind, float seconds, float power)
        {
            if (mind == Mind.Dead || seconds <= 0)
            {
                return;
            }

            // A leader shakes it off sooner.
            seconds *= Kind.Tier > 0 ? 0.5f : 1;
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
                    if (Kind.Tier == 0)
                    {
                        fearUntil = Mathf.Max(fearUntil, until);
                        mind = Mind.Flee;
                    }

                    break;
                case Affliction.Burn:
                    hurtPerSecond = Time.time < hurtUntil ? Mathf.Max(power, hurtPerSecond) : power;
                    hurtUntil = Mathf.Max(hurtUntil, until);
                    break;
            }
        }

        /// <summary>Thrown off balance (their blow parried): they reel back and can do nothing a moment.</summary>
        public void Stagger(float seconds, Vector3 away)
        {
            if (mind == Mind.Dead)
            {
                return;
            }

            staggerUntil = Mathf.Max(staggerUntil, Time.time + seconds / Mathf.Max(1, Kind.Tier));
            throwing = -1;
            blowing = -1;
            Push(away.normalized * 3.5f + Vector3.up * 1.2f);
        }

        public void Push(Vector3 velocity)
        {
            if (mind == Mind.Dead)
            {
                return;
            }

            push = velocity * (Kind.Tier > 0 ? 0.4f : 0.8f);
        }

        /// <summary>
        /// A blow on a part of the body (Wounds): rolling, it passes through them; a shield takes its share. Struck, they fight
        /// (and in their land, warned or not, at once); badly hurt, an ordinary one runs.
        /// </summary>
        public void TakeStrike(Strike strike, BodyPart? part)
        {
            if (mind == Mind.Dead)
            {
                return;
            }

            if (Untouched)
            {
                DamageNumbers.Word(strike.Point, "уклонение", DamageNumbers.Dealt);

                return;
            }

            var harm = Wounds.Harm(part ?? BodyPart.Chest, strike, MaxHealth, Kind.Toughness, random) * (Shielded ? 1 - Kind.ShieldTakes : 1);
            Hurt(harm, strike, part);

            if (mind == Mind.Dead || harm < 0.5f)
            {
                return;
            }

            if (mind != Mind.Fight && mind != Mind.Flee)
            {
                Fight(Kind.Clan == "gu_yue" ? "К бою!" : $"Ты посмел поднять руку на {MasterKinds.ClanName(Kind.Clan)}?!");
            }

            // Sleep ends with a blow.
            sleepUntil = 0;

            if (Kind.Tier == 0 && Health < MaxHealth * 0.22f && mind == Mind.Fight && random.NextDouble() < 0.6f)
            {
                mind = Mind.Flee;
                Say(Kind.Clan == "xiong" ? "Это ещё не конец!" : "Клан узнает об этом!");
            }
            else if (Time.time >= rollReady && !Busy && random.NextDouble() < Kind.Dodge * 0.35f)
            {
                var side = Vector3.Cross(Vector3.up, Flat(strike.Direction)).normalized;
                Roll(random.Next(2) == 0 ? side : -side);
            }
        }

        void Hurt(float harm, Strike strike, BodyPart? part)
        {
            if (harm <= 0)
            {
                return;
            }

            Health = Mathf.Max(0, Health - harm);
            StruckAt = Time.time;

            if (harm >= 0.5f)
            {
                Wounds.Report(Title, part, harm, Health <= 0, strike.Point);
            }

            if (Health <= 0)
            {
                Die(strike);
            }
        }

        void Die(Strike strike)
        {
            mind = Mind.Dead;
            velocity = Vector3.zero;
            controller.enabled = false;
            deadFor = 0;
            shieldGlow?.Release();
            shieldGlow = null;

            foreach (var zone in zones)
            {
                zone.Shape.enabled = false;
            }

            // Fallen on their back, the way the blow pushed them.
            var away = Flat(strike.Direction);

            if (away.sqrMagnitude > 0.01f)
            {
                facing = Mathf.Atan2(-away.x, -away.z);
                transform.rotation = Quaternion.Euler(0, facing * Mathf.Rad2Deg, 0);
            }

            figure.Rig.Throw = -1;
            figure.Rig.BlowPhase = -1;
            figure.Rig.RollPhase = -1;
            figure.Rig.Guard = 0;
            figure.Pose(0.5f, PersonFigure.Standing);
            figure.Root.localRotation = Quaternion.Euler(-88, 0, 0);
            figure.Root.localPosition = new Vector3(0, 0.12f, 0);
            Killed?.Invoke(this);
        }

        /// <summary>Lying where they fell a while, then gone.</summary>
        void Lie(float dt)
        {
            deadFor += dt;

            if (deadFor > 40)
            {
                figure.Root.localPosition += Vector3.down * dt * 0.3f;
            }

            if (deadFor > 46)
            {
                Destroy(gameObject);
            }
        }
    }
}
