using System.Collections.Generic;
using GuWorld.Body;
using GuWorld.Cultivation;
using GuWorld.Items;
using GuWorld.Systems;
using GuWorld.World;
using GuWorld.Physics;
using GuWorld.Render;
using GuWorld.State;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// A quick check that the proving ground works, run from the command line:
    ///
    ///   Unity -batchmode -projectPath . -executeMethod GuWorld.Editor.GuWorldSmokeTest.Run
    ///
    /// Opens the scene, plays it, and drives the hero's body directly (no
    /// keyboard in batch mode): standing on the ground at the spawn, walking,
    /// jumping, climbing the crate, not walking through the wall. Exits with 0
    /// when all is well, 1 otherwise; each check is logged.
    /// </summary>
    [InitializeOnLoad]
    public static class GuWorldSmokeTest
    {
        const float Dt = 1f / 60f;
        /// <summary>Survives the domain reload on entering play mode, so the check picks up again after it.</summary>
        const string Running = "GuWorld.SmokeTest.Running";
        static int frames;
        static readonly List<string> failures = new List<string>();

        static GuWorldSmokeTest()
        {
            if (SessionState.GetBool(Running, false))
            {
                EditorApplication.update += Tick;
            }
        }

        public static void Run()
        {
            // A new game at the spawn: the player's own save is put aside and given back at the end.
            if (File.Exists(SaveGame.FilePath))
            {
                File.Copy(SaveGame.FilePath, Backup, true);
                SaveGame.Delete();
            }

            EditorSceneManager.OpenScene("Assets/GuWorld/Scenes/TestGrounds.unity");
            frames = 0;
            failures.Clear();
            SessionState.SetBool(Running, true);
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || ++frames < 10)
            {
                if (frames > 2000)
                {
                    Finish("play mode never started");
                }

                return;
            }

            EditorApplication.update -= Tick;

            try
            {
                Checks();
            }
            catch (System.Exception error)
            {
                failures.Add($"threw: {error}");
            }

            Finish(null);
        }

        static void Checks()
        {
            var body = Object.FindAnyObjectByType<HeroMotor>();
            var world = Object.FindAnyObjectByType<ChunkManager>();
            Check(body != null, "the hero exists");
            Check(world != null && world.LoadedCount > 0, $"chunks are loaded ({world?.LoadedCount})");

            // Let the body settle at the spawn.
            Run(body, 0.5f, Vector2.zero);
            var spawn = body.transform.position;
            Check(body.Grounded, $"the hero stands at the spawn ({spawn})");
            Check(Mathf.Abs(spawn.y) < 0.1f && Vector2.Distance(new Vector2(spawn.x, spawn.z), new Vector2(0, 12)) < 0.2f,
                $"the spawn is (0, 0, 12) on flat ground ({spawn})");

            // Walk toward +Z for two seconds: about 4.2 m/s, minus the speed-up.
            Run(body, 2, new Vector2(0, 1));
            var walked = body.transform.position.z - spawn.z;
            Check(walked > 7 && walked < 8.6f, $"walking for 2 s covers ~8 m ({walked:0.00} m)");
            Check(body.Grounded, "still on the ground after walking");

            // Stop, then jump: up and back down.
            Run(body, 1, Vector2.zero);
            var before = body.transform.position.y;
            body.Tick(Dt, new MotorInput { jump = true, speedScale = 1 });
            var peak = before;

            for (var t = 0f; t < 1.2f; t += Dt)
            {
                body.Tick(Dt, new MotorInput { speedScale = 1 });
                peak = Mathf.Max(peak, body.transform.position.y);
            }

            Check(peak - before > 1.2f && peak - before < 1.7f, $"a jump rises ~1.5 m ({peak - before:0.00} m)");
            Check(body.Grounded, "lands again after the jump");

            // The crate (1.2 m, at 6, -6 — the web's -6 mirrored): walk up to it and climb.
            Teleport(body, new Vector3(3, 0, -6));
            Run(body, 2, new Vector2(1, 0));
            Check(body.transform.position.x < 5.4f, $"the crate stops the walk ({body.transform.position.x:0.00})");
            Check(body.TryClimb(1, 0), "there is a ledge to climb onto");
            Run(body, 1.5f, Vector2.zero);
            Check(Mathf.Abs(body.transform.position.y - 1.2f) < 0.1f, $"standing on the crate ({body.transform.position.y:0.00} m)");

            // The wall (3.5 m, at x = 12): too high to climb, cannot be walked through.
            Teleport(body, new Vector3(9, 0, 4));
            Run(body, 3, new Vector2(1, 0));
            Check(body.transform.position.x < 11.6f, $"the wall stops the walk ({body.transform.position.x:0.00})");
            Check(!body.TryClimb(1, 0), "the wall is too high to climb");

            // The steep mound (~58°, at 24, -24): one cannot stand on its side.
            Teleport(body, new Vector3(24, 0, -17));
            Run(body, 3, new Vector2(0, -1));
            Check(body.transform.position.y < 3, $"the steep mound cannot be walked up ({body.transform.position.y:0.00} m)");

            Check(body.transform.position.z < -18.5f, $"…and the walk did reach its foot (z {body.transform.position.z:0.00})");

            // The gentle mound (6 m, at -24, -24): can be walked to the top.
            Teleport(body, new Vector3(-24, 0, 4));
            Run(body, 7, new Vector2(0, -1));
            Check(body.transform.position.y > 5, $"the gentle mound can be walked up ({body.transform.position.y:0.00} m)");

            // Steps and stairs (a street's stone steps, a house's wooden ones, a high threshold): walked and run up
            // without a jump, the feet carried over each step.
            foreach (var (rise, tread, sprint) in new[] { (0.16f, 0.35f, false), (0.22f, 0.3f, false), (0.22f, 0.3f, true), (0.3f, 0.45f, false), (0.4f, 0.5f, true), (0.45f, 0.6f, false) })
            {
                Teleport(body, new Vector3(-60, 0, 60));
                var foot = body.transform.position;
                var stairs = new GameObject("Test Stairs");

                for (var s = 0; s < 5; s++)
                {
                    // Solid steps from the ground up; the last a landing.
                    var height = (s + 1) * rise;
                    var depth = s == 4 ? 6 : tread;
                    var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    step.transform.SetParent(stairs.transform, false);
                    step.transform.localScale = new Vector3(3, height, depth);
                    step.transform.position = foot + new Vector3(0, height / 2, 1.5f + s * tread + depth / 2);
                }

                UnityEngine.Physics.SyncTransforms();
                // Far enough to be on the landing, not past it.
                Run(body, sprint ? 1.1f : 1.8f, new Vector2(0, 1), sprint);
                var up = body.transform.position.y - foot.y;
                Check(up > 5 * rise - 0.05f, $"{(sprint ? "running" : "walking")} up {rise * 100:0} cm steps without a jump ({up:0.00} of {5 * rise:0.00} m)");
                Object.DestroyImmediate(stairs);
                UnityEngine.Physics.SyncTransforms();
            }

            // The camera's axes: right of where it looks is to the right on screen.
            // A new game starts on making the hero; play, so the camera follows.
            Object.FindAnyObjectByType<GuWorldGame>().Play();
            var camera = Object.FindAnyObjectByType<ThirdPersonCamera>();
            camera.Follow(0.016f, body.transform.position, 1.55f, false, true);
            var (forward, right) = camera.GroundAxes();
            var view = camera.transform;
            var screenRight = new Vector2(view.right.x, view.right.z).normalized;
            var screenForward = new Vector2(view.forward.x, view.forward.z).normalized;
            Check(Vector2.Dot(right, screenRight) > 0.99f, $"D walks to the screen's right ({right} vs {screenRight})");
            Check(Vector2.Dot(forward, screenForward) > 0.99f, $"W walks where the camera looks ({forward} vs {screenForward})");

            var before2 = camera.Yaw;
            camera.Look(100, 0, 0);
            camera.Follow(0.016f, body.transform.position, 1.55f, false, true);
            var turned = new Vector2(view.forward.x, view.forward.z).normalized;
            var cross = screenForward.x * turned.y - screenForward.y * turned.x;
            Check(cross < 0, $"the mouse to the right turns the view to the right (yaw {before2:0.00} → {camera.Yaw:0.00})");

            // F5: the camera in the hero's eyes, looking the way the view turns; and back behind them.
            camera.SetFirstPerson(true);
            camera.Follow(0.016f, body.transform.position, 1.55f, false, true);
            var eyes = camera.transform.position - body.transform.position;
            Check(camera.FirstPerson && Mathf.Abs(eyes.y - 1.55f) < 0.2f && new Vector2(eyes.x, eyes.z).magnitude < 0.3f,
                $"F5 puts the camera in the hero's eyes ({eyes.y:0.00} m up, {new Vector2(eyes.x, eyes.z).magnitude:0.00} m aside)");
            camera.SetFirstPerson(false);
            camera.Follow(0.016f, body.transform.position, 1.55f, false, true);
            Check(!camera.FirstPerson && (camera.transform.position - body.transform.position).magnitude > 1.5f, "F5 again puts it back behind the hero");

            // The Gu Master: the test start, a moonblade's cost and rest, the sea refilling, a stone.
            var gu = Object.FindAnyObjectByType<Cultivator>();
            var game = Object.FindAnyObjectByType<GuWorldGame>();
            var health = gu.GetComponent<Health>();
            var moon = gu.First(GuBook.Moonlight.Id);
            Check(gu != null && gu.Aperture.Grade == Grade.C && Mathf.Approximately(gu.Aperture.Essence, 45) && moon != null && moon.vital &&
                  gu.Bar[0] == "gu:moonlight" && gu.Stones == 10 && gu.Bag.Count(ItemBook.Petal) == 8,
                $"a new game is the test Gu Master: C grade, a full sea of 45%, a vital Moonlight Gu on key 1, 10 stones, 8 petals ({gu?.Aperture.Grade}, {gu?.Aperture.Essence}, {gu?.Bar[0]}, {gu?.Stones}, {gu?.Bag.Count(ItemBook.Petal)})");
            Check(gu.Use(0) && Mathf.Approximately(gu.Aperture.Essence, 35) && Object.FindAnyObjectByType<Moonblade>() != null,
                $"a moonblade costs a tenth of the aperture and flies ({gu.Aperture.Essence})");
            Check(!gu.Use(0) && Mathf.Approximately(gu.Aperture.Essence, 35), "a second throw at once waits for the Gu to rest");
            var start = gu.Now;
            gu.Tick(start + 60);
            Check(Mathf.Abs(gu.Aperture.Essence - 39) < 0.01f, $"an hour refills 4% at C grade ({gu.Aperture.Essence:0.00})");
            Check(gu.AbsorbStone() && Mathf.Approximately(gu.Aperture.Essence, 45) && gu.Stones == 9, $"a stone tops the sea up to its cap ({gu.Aperture.Essence}, {gu.Stones} left)");
            gu.Aperture.Essence = 5;
            Check(!gu.Use(0) && Mathf.Approximately(gu.Aperture.Essence, 5), "without the essence the Gu cannot be used");

            // Drawing in a stone takes three seconds; a blow breaks it off and the stone is kept.
            gu.Aperture.Essence = 20;
            var stonesBefore = gu.Stones;
            Check(gu.StartAbsorbing() && gu.Absorbing, "V begins drawing in a stone");
            gu.UpdateAbsorbing(1.5f);
            Check(gu.Absorbing && Mathf.Approximately(gu.Aperture.Essence, 20) && gu.Stones == stonesBefore,
                $"half way through it has given nothing yet ({gu.AbsorbProgress:0.00})");
            gu.UpdateAbsorbing(1.6f);
            Check(!gu.Absorbing && gu.Aperture.Essence > 31 && gu.Stones == stonesBefore - 1,
                $"after three seconds the stone is in the sea ({gu.Aperture.Essence:0}%, {gu.Stones} left)");
            gu.StartAbsorbing();
            health.Strike(1);
            Check(!gu.Absorbing && gu.Stones == stonesBefore - 1, "a blow breaks it off, the stone kept");
            health.Set(Health.Max);
            gu.Aperture.Essence = 5;

            // Hunger (lore: two petals a meal, twice a day): hungry after a meal's time, fed from the bag, too weak to answer if starved.
            gu.Tick(start + 12 * 60 + 1);
            Check(gu.HungerOf(moon) == Hunger.Hungry, $"after twelve hours the Moonlight Gu is hungry ({gu.HungerOf(moon)})");
            Check(gu.Feed(moon) && gu.Bag.Count(ItemBook.Petal) == 6 && gu.HungerOf(moon) == Hunger.Fed, $"feeding it takes two petals ({gu.Bag.Count(ItemBook.Petal)} left)");
            Check(!gu.Feed(moon, true) && gu.Bag.Count(ItemBook.Petal) == 6, "a Gu just fed is not fed again");
            gu.Aperture.Essence = 45;
            gu.Tick(gu.Now + 24 * 60 + 1);
            Check(gu.HungerOf(moon) == Hunger.Starving && !gu.UseGu(GuBook.Moonlight.Id), $"a day unfed it is too weak to use ({gu.HungerOf(moon)})");
            gu.Feed(moon);
            Check(gu.HungerOf(moon) == Hunger.Fed, "fed again, it answers again");

            // Petals wilt in five days; what is taken comes from the pile that wilts first.
            var bag = new Bag();
            bag.Add(ItemBook.Petal, 10, 0);
            bag.Add(ItemBook.Petal, 10, 3 * 24 * 60);
            Check(bag[0].count == 10 && bag[1].count == 10, "petals of different days lie apart");
            bag.Take(ItemBook.Petal, 4);
            Check(bag[0].count == 6 && bag[1].count == 10, "the older petals are used first");
            var lost = bag.Spoil(5 * 24 * 60 + 1);
            Check(lost != null && lost[ItemBook.Petal] == 6 && bag.Count(ItemBook.Petal) == 10, "after five days the old petals have wilted");
            Check(bag.Room(ItemBook.Stone, 0) <= (int)((Bag.Capacity - bag.Weight) / 0.02f) + 1, "the bag's weight limits what fits");

            // A fall from ten metres hurts; health mends with time.
            health.Set(Health.Max);
            Teleport(body, body.transform.position + Vector3.up * 10);
            Run(body, 2, Vector2.zero);
            Check(body.Grounded && health.Current < Health.Max - 30 && health.Current > 0, $"a ten-metre fall hurts ({health.Current:0})");
            var hurt = health.Current;
            health.Mend(60);
            Check(Mathf.Abs(health.Current - (hurt + Health.MendPerHour)) < 0.01f, $"an hour mends {Health.MendPerHour:0} ({health.Current:0})");

            // A Jade Skin Gu, bought and refined: it takes a ring cell and the bar; its skin turns harm into essence spent.
            var baoshan = Trade.Of("Гу Юэ Баошань");
            gu.Bag.Add(ItemBook.Stone, 200, gu.Now);
            var stones = gu.Stones;
            var deal = game.Trade.Buy(baoshan, new System.Collections.Generic.Dictionary<string, int> { [ItemBook.JadeSkinGu] = 1, [ItemBook.Petal] = 1, [ItemBook.JadeRock] = 1 }, gu.Bag, gu.Now, out var paid);
            Check(deal == Deal.Done && paid == 95 && gu.Stones == stones - 95 && gu.Bag.Count(ItemBook.JadeSkinGu) == 1 && gu.Bag.Count(ItemBook.JadeRock) == 1,
                $"Baoshan sells a Jade Skin Gu, ten petals and a jade rock for 95 stones ({deal}, {paid})");
            Check(game.Trade.Buy(baoshan, new System.Collections.Generic.Dictionary<string, int> { [ItemBook.JadeSkinGu] = 1 }, gu.Bag, gu.Now, out _) == Deal.NoStock, "his one Jade Skin Gu is sold for today");
            var cell = -1;

            for (var i = 0; i < Bag.Cells; i++)
            {
                if (gu.Bag[i]?.id == ItemBook.JadeSkinGu) cell = i;
            }

            gu.Aperture.Essence = 45;
            Check(gu.Refine(cell) && gu.First(GuBook.JadeSkin.Id) != null && !gu.First(GuBook.JadeSkin.Id).vital && Mathf.Approximately(gu.Aperture.Essence, 5) &&
                  System.Array.IndexOf(gu.Bar, "gu:jade_skin") >= 0, $"refining it takes 40% and puts it in the ring and on the bar ({gu.Aperture.Essence})");
            gu.Aperture.Essence = 45;
            Check(gu.UseGu(GuBook.JadeSkin.Id) && gu.SkinOn && Mathf.Approximately(gu.Aperture.Essence, 43), "the jade skin rises for 2%");
            health.Set(Health.Max);
            health.TakeHarm(50);
            Check(Mathf.Approximately(health.Current, Health.Max) && Mathf.Abs(gu.Aperture.Essence - 37) < 0.01f, $"the skin stops a blow for essence ({health.Current}, {gu.Aperture.Essence:0.00})");
            gu.Tick(gu.Now + 10);
            Check(Mathf.Abs(gu.Aperture.Essence - (37 - 2.5f + 10 * 4 / 60f)) < 0.01f, $"held, it costs its upkeep ({gu.Aperture.Essence:0.00})");

            // The Gu Master saved and loaded is the same.
            var saved = gu.Save();
            var petals = gu.Bag.Count(ItemBook.Petal);
            gu.Load(saved, gu.Now);
            Check(gu.Gu.Count == 2 && gu.Bag.Count(ItemBook.Petal) == petals && gu.Bar[0] == "gu:moonlight" && gu.SkinOn && gu.First(GuBook.Moonlight.Id).vital,
                $"saved and loaded, the Gu Master is the same ({gu.Gu.Count} Gu, {gu.Bag.Count(ItemBook.Petal)} petals)");

            // The academy's first task: taken from the teacher, done, handed back for stones.
            var quests = game.Quests;
            var first = quests.NextFrom(Quests.Teacher);
            Check(first != null && first.Id == "first_blade", $"the teacher has the first task ({first?.Id})");
            quests.Take(first);
            quests.Count("hit_dummy", 3);
            stones = gu.Stones;
            Check(quests.Ready && quests.HandIn(Quests.Teacher) == first && quests.Current == null && quests.NextFrom(Quests.Teacher)?.Id == "orchids",
                "three hits finish it, and the next task waits");

            // A moon orchid bed gives petals and stands bare until it grows back.
            Check(HerbBed.All.Count == 6, $"six moon orchid beds ({HerbBed.All.Count})");

            if (HerbBed.All.Count > 0)
            {
                var bed = HerbBed.All[0];
                HerbBed.Now = gu.Now;
                var before3 = gu.Bag.Count(ItemBook.Petal);
                game.Gather(bed);
                Check(gu.Bag.Count(ItemBook.Petal) == before3 + bed.Yield && !bed.Ready, $"gathering a bed gives {bed.Yield} petals and leaves it bare");
            }

            Check(game.Map != null && game.Map.Texture != null, "the location has a map");

            // The chat's commands: the hour, a thing given, a jump across the ground; what is suggested as one types.
            var said = game.Commands.Run("/time set 22");
            var hour = game.Clock.MinuteOfDay / 60;
            Check(hour > 21.9 && hour < 22.1, $"/time set 22 sets the hour ({hour:0.00}: {said})");
            var stonesHad = gu.Stones;
            said = game.Commands.Run("/give primeval_stone 5");
            Check(gu.Stones == stonesHad + 5, $"/give puts things in the bag ({said})");
            said = game.Commands.Run("/tp 5 20");
            Check(Vector2.Distance(new Vector2(body.transform.position.x, body.transform.position.z), new Vector2(5, 20)) < 0.5f, $"/tp x z moves the hero there ({said})");
            Check(game.Commands.Suggest("/ti", out _).Contains("/time") && game.Commands.Suggest("/weather r", out var usage).Contains("rain") && usage != null,
                "typing suggests the commands, then their choices");
            Check(Systems.Commands.Normalize(".ез 1 2") == "/tp 1 2" && Systems.Commands.Normalize("/время") == "/время",
                "a line typed with the Russian layout is read as the keys meant (but not a Russian command)");
            said = game.Commands.Run("/время set 7");
            Check(System.Math.Abs(game.Clock.MinuteOfDay / 60 - 7) < 0.1, $"the commands answer to Russian names too ({said})");
            Check(game.Commands.Run("/nonsense").StartsWith("!"), "an unknown command is refused");

            // Wolves: called up beside the hero; a moonblade cuts one, a second kills it, its fang is the hero's.
            var came = game.SpawnWolves(1);
            var wolf = Creatures.Wolf.All.Find(w => !w.Dead);
            Check(came == 1 && wolf != null && wolf.GetComponentsInChildren<MeshRenderer>().Length > 10, $"/wolves calls up a wolf beside the hero ({came})");

            if (wolf != null)
            {
                var fangs = gu.Bag.Count(ItemBook.WolfFang);
                var cut = new Combat.Strike { Power = 100, Sharpness = 1, Direction = Vector3.forward };
                wolf.TakeStrike(cut, null);
                Check(!wolf.Dead && wolf.Health < Creatures.Wolf.MaxHealth - 20, $"a moonblade cuts a wolf deep ({wolf.Health:0} of {Creatures.Wolf.MaxHealth})");
                wolf.TakeStrike(cut, null);
                Check(wolf.Dead && gu.Bag.Count(ItemBook.WolfFang) == fangs + 1, "a second kills it, and its fang is the hero's");
            }

            Check(Quests.Book.Length >= 9 && System.Array.Exists(Quests.Book, q => q.Event == "kill:wolf") && System.Array.Exists(Quests.Book, q => q.Gives != null),
                $"the villagers have tasks past the academy's: wolves, a place, a delivery, a word ({Quests.Book.Length})");
            Teleport(body, new Vector3(0, 0, 12));

            // A moonblade thrown at a dummy cuts it.
            var dummy = Object.FindAnyObjectByType<TrainingDummy>();
            Check(dummy != null, "a training dummy stands before the spawn");

            if (dummy != null)
            {
                var hero = body.GetComponent<CharacterController>();
                var target = dummy.transform.position + Vector3.up * 1.35f;
                var from = target + Vector3.back * 6;
                UnityEngine.Physics.SyncTransforms();
                var blade = Moonblade.Throw(from, target - from, GuBook.Moonlight, hero);

                for (var i = 0; i < 60 && blade != null && blade.Flying; i++)
                {
                    blade.Step(Dt);
                }

                Check(dummy.Health < TrainingDummy.Sturdiness - 10 && dummy.Health > 0,
                    $"the moonblade cuts the dummy ({dummy.Health:0} of {TrainingDummy.Sturdiness})");
            }

            // Wounds: a blow far weaker than the body does nothing; an equal one takes the part's share.
            Check(Combat.Wounds.Scale(30, 100) == 0 && Mathf.Approximately(Combat.Wounds.Scale(100, 100), 1) && Combat.Wounds.Scale(400, 100) > 1.9f,
                $"blows scale by strength against toughness ({Combat.Wounds.Scale(30, 100)}, {Combat.Wounds.Scale(100, 100)}, {Combat.Wounds.Scale(400, 100):0.00})");
            var headShare = Combat.Wounds.Harm(Combat.BodyPart.Head, new Combat.Strike { Power = 100, Sharpness = 1 }, 100, 100, null);
            var handShare = Combat.Wounds.Harm(Combat.BodyPart.HandL, new Combat.Strike { Power = 100, Sharpness = 1 }, 100, 100, null);
            Check(headShare > 60 && headShare < 70 && handShare < 6, $"the head takes far more than a hand ({headShare:0}, {handShare:0.0})");
            Check(body.GetComponentsInChildren<Combat.HitZone>().Length == Combat.HitZones.Specs.Length,
                $"the hero's body has its parts ({body.GetComponentsInChildren<Combat.HitZone>().Length})");

            // A giant tree: climbed, a branch stepped onto, sat on at its edge, cut off.
            {
                var treeGo = new GameObject("Test Tree");
                treeGo.transform.position = new Vector3(-30, 0, 30);
                var tree = treeGo.AddComponent<World.ClimbTree>();
                tree.BaseRadius = 1;
                tree.TopRadius = 0.6f;
                tree.ClimbHeight = 12;
                var trunk = treeGo.AddComponent<CapsuleCollider>();
                trunk.radius = 0.95f;
                trunk.height = 14;
                trunk.center = new Vector3(0, 7, 0);
                var limbGo = new GameObject("Branch");
                limbGo.transform.SetParent(treeGo.transform, false);
                limbGo.transform.localPosition = new Vector3(0, 6, 0.8f);
                var limb = limbGo.AddComponent<World.TreeBranch>();
                limb.Length = 7;
                limb.RootThickness = 0.8f;
                limb.TipThickness = 0.25f;
                limb.Health = 150;
                limb.Tree = tree;
                var solidGo = new GameObject("Solid");
                solidGo.transform.SetParent(limbGo.transform, false);
                solidGo.transform.localPosition = new Vector3(0, 0, 3.5f);
                solidGo.AddComponent<BoxCollider>().size = new Vector3(0.8f, 0.75f, 7);
                tree.Branches = new[] { limb };
                UnityEngine.Physics.SyncTransforms();

                Teleport(body, new Vector3(-30, 0, 31.6f));
                Check(World.ClimbTree.Near(body.transform.position, 0.9f, out _) == tree && body.GrabTree(tree), "the hero takes hold of a giant tree's trunk");

                for (var t = 0f; t < 4; t += Dt)
                {
                    body.ClimbTick(Dt, 1, 0, false);
                }

                Check(body.Tree == tree && body.TreeHeight > 5.2f && body.transform.position.y > 5, $"W climbs the trunk ({body.TreeHeight:0.0} m up)");
                Check(body.StepOntoBranch() && body.Tree == null, "E steps from the trunk onto the branch beside it");
                UnityEngine.Physics.SyncTransforms();
                Run(body, 0.5f, Vector2.zero);
                Check(body.Grounded && body.transform.position.y > 6 && body.Branch == limb, $"standing on the branch ({body.transform.position.y:0.00} m, on {body.Branch?.name})");
                Run(body, 1.2f, new Vector2(0, 1));
                Run(body, 0.3f, Vector2.zero);
                Check(body.SitDown() && body.Sitting, $"X sits down on the branch (on its edge: {body.SittingOnEdge})");
                body.StandUp();
                limb.TakeStrike(new Combat.Strike { Power = 400, Sharpness = 1, Direction = Vector3.right }, null);
                Check(limb.Broken && limb.GetComponent<Rigidbody>() != null, "a hard enough cut breaks the branch off and it falls");
                Object.DestroyImmediate(treeGo);
                Teleport(body, new Vector3(0, 0, 12));
            }

            // The body: the model worn, its feet on the ground, its head up top.
            var picture = Object.FindAnyObjectByType<HeroView>();
            Check(picture != null && picture.HasBody, "the hero wears the realistic body");

            if (picture != null && picture.HasBody)
            {
                Teleport(body, new Vector3(0, 0, 12));
                // Let the figure ease into its pose over a few frames.
                for (var i = 0; i < 60; i++)
                {
                    picture.Pose(Dt);
                }

                var renderers = picture.GetComponentsInChildren<SkinnedMeshRenderer>();
                var head = FindDeep(picture.transform, "Head");
                var footL = FindDeep(picture.transform, "foot_l");
                var footR = FindDeep(picture.transform, "foot_r");
                var feet = body.transform.position;
                Check(renderers.Length > 0, $"the body has skinned meshes ({renderers.Length})");
                Check(head != null && head.position.y - feet.y > 1.45f && head.position.y - feet.y < 1.75f,
                    $"the head is at a fifteen-year-old's height ({(head ? head.position.y - feet.y : -1):0.00} m)");
                Check(footL != null && footR != null && Mathf.Abs(footL.position.y - feet.y) < 0.2f && Mathf.Abs(footR.position.y - feet.y) < 0.2f,
                    $"the feet are on the ground ({(footL ? footL.position.y - feet.y : -1):0.00}, {(footR ? footR.position.y - feet.y : -1):0.00})");
                var leftHand = FindDeep(picture.transform, "hand_l");
                var local = body.transform.InverseTransformPoint(leftHand.position);
                Check(local.x < 0, $"the left hand is on the hero's left ({local.x:0.00})");
                Check(Mathf.Abs(local.z) < 0.25f && local.y < 1.1f, $"the arms hang down at rest ({local})");

                // Every look builds: each gender with each hairstyle, and each beard.
                foreach (Gender gender in System.Enum.GetValues(typeof(Gender)))
                {
                    foreach (HairStyle hair in System.Enum.GetValues(typeof(HairStyle)))
                    {
                        CheckLook(picture, Look.Default(gender).With(hair: hair));
                    }
                }

                foreach (Beard beard in System.Enum.GetValues(typeof(Beard)))
                {
                    CheckLook(picture, Look.Default(Gender.Male).With(beard: beard));
                }
            }
        }

        /// <summary>Builds a look on the hero and checks its parts are there.</summary>
        static void CheckLook(HeroView picture, Look look)
        {
            picture.Wear(look);
            var names = new HashSet<string>();

            foreach (var r in picture.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (r.sharedMesh != null && r.sharedMesh.vertexCount > 0)
                {
                    names.Add(r.name);
                }
            }

            var hair = look.Hair == HairStyle.Bald || names.Contains("Hair_cap") || names.Contains(Hair.ModelHair(look.Hair) ?? "-");
            var tailOrCurtain = look.Hair switch
            {
                HairStyle.Ponytail => names.Contains("Hair_tail"),
                HairStyle.Bob or HairStyle.Long => names.Contains("Hair_curtain"),
                _ => true,
            };
            var beard = look.BeardStyle == Beard.None || names.Contains("Beard");
            var clothes = names.Contains("Cloth_trousers") && names.Contains("Cloth_tunic") && names.Contains("Cloth_skirt") &&
                          names.Contains("Cloth_sash") && names.Contains("Cloth_shoes") &&
                          names.Contains("Cloth_wraps") && names.Contains("Cloth_sash_ends");
            Check(picture.HasBody && hair && tailOrCurtain && beard && clothes,
                $"the look {look.Key} is built (hair {hair}, {tailOrCurtain}, beard {beard}, clothes {clothes}: {string.Join(" ", names)})");
        }

        static Transform FindDeep(Transform parent, string name)
        {
            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }

            return null;
        }

        static void Run(HeroMotor body, float seconds, Vector2 move, bool sprint = false)
        {
            for (var t = 0f; t < seconds; t += Dt)
            {
                body.Tick(Dt, new MotorInput { moveX = move.x, moveZ = move.y, sprint = sprint, speedScale = 1 });
            }
        }

        static void Teleport(HeroMotor body, Vector3 at)
        {
            body.PlaceAt(at);
            Run(body, 0.3f, Vector2.zero);
        }

        static void Check(bool ok, string what)
        {
            Debug.Log($"GU World smoke: {(ok ? "ok  " : "FAIL")} {what}");

            if (!ok)
            {
                failures.Add(what);
            }
        }

        static string Backup => SaveGame.FilePath + ".smoke-backup";

        static void Finish(string problem)
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }

            SaveGame.Delete();

            if (File.Exists(Backup))
            {
                File.Move(Backup, SaveGame.FilePath);
            }

            SessionState.EraseBool(Running);

            if (problem != null)
            {
                failures.Add(problem);
            }

            Debug.Log(failures.Count == 0
                ? "GU World smoke: all checks passed"
                : $"GU World smoke: {failures.Count} failed:\n  " + string.Join("\n  ", failures));
            EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
        }
    }
}
