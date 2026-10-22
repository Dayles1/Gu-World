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
            Check(gu != null && gu.Aperture.Grade == Grade.C && Mathf.Approximately(gu.Aperture.Essence, 50) && moon != null && moon.vital &&
                  gu.Bar[0] == "gu:moonlight" && gu.Stones == 10 && gu.Bag.Count(ItemBook.Petal) == 8,
                $"a new game is the test Gu Master: C grade, a full sea of 50%, a vital Moonlight Gu on key 1, 10 stones, 8 petals ({gu?.Aperture.Grade}, {gu?.Aperture.Essence}, {gu?.Bar[0]}, {gu?.Stones}, {gu?.Bag.Count(ItemBook.Petal)})");
            Check(gu.Use(0) && Mathf.Approximately(gu.Aperture.Essence, 40) && Object.FindAnyObjectByType<Moonblade>() != null,
                $"a moonblade costs a tenth of the aperture and flies ({gu.Aperture.Essence})");
            Check(!gu.Use(0) && Mathf.Approximately(gu.Aperture.Essence, 40), "a second throw at once waits for the Gu to rest");
            var start = gu.Now;
            gu.Tick(start + 60);
            var hourly = 50 / Aperture.RefillHours(50);
            Check(Mathf.Abs(gu.Aperture.Essence - (40 + hourly)) < 0.01f, $"an hour refills {hourly:0.0}% at a 50% talent ({gu.Aperture.Essence:0.00})");
            Check(gu.AbsorbStone() && Mathf.Approximately(gu.Aperture.Essence, 50) && gu.Stones == 9, $"a stone tops the sea up to its cap ({gu.Aperture.Essence}, {gu.Stones} left)");

            // Talent: the sea's height and how soon it fills — the better, the sooner (40%: ~20 h, 80%: ~8 h, 99%: within the hour, 100%: minutes),
            // the base reckoning (40%: a day, 80%: half a day) cut by 10–30%.
            Check(Mathf.Abs(Aperture.RefillHours(40) - 24 * (1 - Aperture.RefillCut(40))) < 0.01f && Mathf.Abs(Aperture.RefillHours(80) - 12 * 0.7f) < 0.01f &&
                  Aperture.RefillHours(99) < 1 && Aperture.RefillHours(100) < 0.2f && Aperture.RefillHours(30) > Aperture.RefillHours(50),
                $"refilling: 30% {Aperture.RefillHours(30):0.0} h, 40% {Aperture.RefillHours(40):0.0} h, 80% {Aperture.RefillHours(80):0.0} h, 99% {Aperture.RefillHours(99) * 60:0} min, 100% {Aperture.RefillHours(100) * 60:0.0} min");
            Check(Talents.Paths.Length == 5 && Talents.Paths[0].Talent == 99 && Talents.Paths[4].Talent == 30 && Talents.Paths[0].Grade == Grade.A &&
                  Talents.Paths[2].Grade == Grade.B && Talents.Paths[3].Grade == Grade.C && Talents.Paths[4].Grade == Grade.D,
                "five ways to begin: A 99%, A 88%, B 70%, C 50%, D 30%");
            var extremes = 0;
            var dice = new System.Random(7);

            for (var i = 0; i < 1000; i++)
            {
                if (Talents.Roll(Talents.Paths[0], dice) >= 100) extremes++;
            }

            Check(extremes > 60 && extremes < 140 && Talents.Roll(Talents.Paths[1], dice) == 88, $"the easy way is born an extreme physique about one time in ten ({extremes} in 1000)");

            // Ranks and small realms: each rank's essence four times denser, each small realm 1.5 times; a Gu's price divided by it.
            Check(Mathf.Approximately(gu.CostOf(GuBook.Moonlight), 10) && Mathf.Abs(gu.CostOf(GuBook.Find("moonglow")) - 40) < 0.01f,
                $"at rank one a rank-one Gu costs 10%, a rank-two one 40% ({gu.CostOf(GuBook.Find("moonglow"))})");
            gu.SetRealm(1, GuWorld.Cultivation.Stage.Peak);
            Check(Mathf.Abs(gu.CostOf(GuBook.Moonlight) - 2.963f) < 0.01f && Mathf.Abs(gu.CostOf(GuBook.Find("moonglow")) - 11.85f) < 0.01f,
                $"at the peak of rank one: 2.96% and 11.85% ({gu.CostOf(GuBook.Moonlight):0.00}, {gu.CostOf(GuBook.Find("moonglow")):0.00})");
            gu.SetRealm(2, GuWorld.Cultivation.Stage.Initial);
            Check(Mathf.Abs(gu.CostOf(GuBook.Moonlight) - 2.5f) < 0.01f && Mathf.Abs(gu.CostOf(GuBook.Find("moonglow")) - 10) < 0.01f && gu.Open == 7,
                $"at rank two: 2.5% and 10%, seven cells open ({gu.CostOf(GuBook.Moonlight)}, {gu.Open})");
            gu.SetRealm(1, GuWorld.Cultivation.Stage.Initial);
            Check(GuBook.Count >= 52, $"the catalogue holds the first two Gu and at least fifty more ({GuBook.Count})");
            var ranks = new int[6];
            var unmade = new List<string>();

            foreach (var kind in GuBook.All)
            {
                ranks[kind.Rank]++;

                if (ItemBook.Find(kind.Item)?.Gu != kind.Id || !(kind.OneUse || kind.Passive || kind.Food != null) || kind.Name == null || (kind.About ?? ItemBook.Find(kind.Item)?.About) == null)
                {
                    unmade.Add(kind.Id);
                }
            }

            Check(unmade.Count == 0, $"every Gu has its item, its food (unless used once or working by itself), a name and words ({string.Join(", ", unmade)})");

            Check(ranks[1] > 0 && ranks[2] > 0 && ranks[3] > 0 && ranks[4] > 0 && ranks[5] > 0, $"Gu of every rank one to five ({string.Join(" ", ranks)})");

            var wrong = new List<string>();

            foreach (var recipe in GuBook.Recipes)
            {
                var top = 0;

                foreach (var id in recipe.Gu)
                {
                    top = Mathf.Max(top, GuBook.Find(id)?.Rank ?? 99);
                }

                if (GuBook.Find(recipe.Result) == null || GuBook.Find(recipe.Result).Rank > top + 1)
                {
                    wrong.Add(recipe.Result);
                }
            }

            Check(wrong.Count == 0, $"no fusion makes a Gu more than a rank above what goes in ({string.Join(", ", wrong)})");

            // Tiredness: a Gu tires as it is used; tired out, it still answers, but forcing it may kill it.
            Check(Cultivator.StrainChance(99) == 0 && Mathf.Approximately(Cultivator.StrainChance(100), 0.12f) && Mathf.Approximately(Cultivator.StrainChance(1000), 0.8f),
                "a rested Gu is safe; past 100 forcing it risks one in eight, rising to four in five");
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
            health.Set(health.Max);
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
            Check(bag.Room(ItemBook.JadeRock, 0) <= (int)((Bag.Capacity - bag.Weight) / 0.2f) + 1, "the bag's weight limits what fits (stones weigh nothing)");

            // A fall from ten metres hurts; health mends with time.
            health.Set(health.Max);
            Teleport(body, body.transform.position + Vector3.up * 10);
            Run(body, 2, Vector2.zero);
            Check(body.Grounded && health.Current < health.Max - 20 && health.Current > 0, $"a ten-metre fall hurts ({health.Current:0})");
            var hurt = health.Current;
            health.Mend(60);
            Check(Mathf.Abs(health.Current - (hurt + Health.MendPerHour)) < 0.01f, $"an hour mends {Health.MendPerHour:0} ({health.Current:0})");

            // A Jade Skin Gu, bought and refined: it takes a ring cell and the bar; its skin turns harm into essence spent.
            var baoshan = Trade.Of("Гу Юэ Баошань");
            gu.Bag.Add(ItemBook.Stone, 200, gu.Now);
            var stones = gu.Stones;
            // Baoshan has only some of his Gu out each day, at a price a little up or down: a day with the Jade Skin Gu out.
            var jadeGoods = System.Array.Find(baoshan.Goods, g => g.Item == ItemBook.JadeSkinGu);
            var shopDay = 0.0;

            for (var d = 0; d < 60 && !Trade.OutToday(baoshan, jadeGoods, d * 1440.0 + 600); d++)
            {
                shopDay = (d + 1) * 1440.0 + 600;
            }

            var noMerit = 0;
            var price = Trade.PriceOf(baoshan, ItemBook.JadeSkinGu, shopDay, 0) + Trade.PriceOf(baoshan, ItemBook.Petal, shopDay, 0) + Trade.PriceOf(baoshan, ItemBook.JadeRock, shopDay, 0);
            var deal = game.Trade.Buy(baoshan, new System.Collections.Generic.Dictionary<string, int> { [ItemBook.JadeSkinGu] = 1, [ItemBook.Petal] = 1, [ItemBook.JadeRock] = 1 }, gu.Bag, shopDay, out var paid, ref noMerit);
            Check(deal == Deal.Done && paid == price && price > 70 && price < 120 && gu.Stones == stones - price && gu.Bag.Count(ItemBook.JadeSkinGu) == 1 && gu.Bag.Count(ItemBook.JadeRock) == 1,
                $"Baoshan sells a Jade Skin Gu, ten petals and a jade rock for the day's price ({deal}, {paid} of {price})");
            Check(game.Trade.Buy(baoshan, new System.Collections.Generic.Dictionary<string, int> { [ItemBook.JadeSkinGu] = 1 }, gu.Bag, shopDay, out _, ref noMerit) == Deal.NoStock, "his one Jade Skin Gu is sold for today");
            var gusOut = System.Array.FindAll(baoshan.Goods, g => ItemBook.Find(g.Item)?.Category == ItemCategory.Gu && Trade.OutToday(baoshan, g, shopDay)).Length;
            Check(gusOut == baoshan.GuADay, $"he has {baoshan.GuADay} of his Gu out a day ({gusOut})");
            var treasury = Trade.Shops[2];
            var merit = 5;
            Check(treasury.ForMerit && game.Trade.Buy(treasury, new System.Collections.Generic.Dictionary<string, int> { [treasury.Goods[0].Item] = 1 }, gu.Bag, shopDay, out _, ref merit) == Deal.NoMoney && merit == 5,
                $"the clan's treasury sells for merit, and five is not enough ({treasury.Goods.Length} goods)");
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
            health.Set(health.Max);
            health.TakeHarm(50);
            Check(Mathf.Approximately(health.Current, health.Max) && Mathf.Abs(gu.Aperture.Essence - 37) < 0.01f, $"the skin stops a blow for essence ({health.Current}, {gu.Aperture.Essence:0.00})");
            gu.Tick(gu.Now + 10);
            gu.Step(10);
            Check(Mathf.Abs(gu.Aperture.Essence - (37 - 2.5f + 10 * hourly / 60f)) < 0.01f && gu.First(GuBook.JadeSkin.Id).tired > 15,
                $"held, it costs its upkeep each second and tires ({gu.Aperture.Essence:0.00}, tired {gu.First(GuBook.JadeSkin.Id).tired:0})");

            // The Gu Master saved and loaded is the same.
            var saved = gu.Save();
            var petals = gu.Bag.Count(ItemBook.Petal);
            gu.Load(saved, gu.Now);
            Check(gu.Gu.Count == 2 && gu.Bag.Count(ItemBook.Petal) == petals && gu.Bar[0] == "gu:moonlight" && gu.SkinOn && gu.First(GuBook.Moonlight.Id).vital &&
                  Mathf.Approximately(gu.Aperture.Talent, 50),
                $"saved and loaded, the Gu Master is the same ({gu.Gu.Count} Gu, {gu.Bag.Count(ItemBook.Petal)} petals, talent {gu.Aperture.Talent})");
            gu.UseGu(GuBook.JadeSkin.Id);
            Check(!gu.SkinOn, "the skin put down");

            // Fusion (two rank-one boars make one rank-one boar — ranks never add up), a sure hand in the ring making it certain.
            gu.Bag.Add("gu_black_boar", 1, gu.Now);
            gu.Bag.Add("gu_white_boar", 1, gu.Now);
            gu.Bag.Add(ItemBook.RawMeat, 5, gu.Now);
            var boars = System.Array.Find(GuBook.Recipes, r => r.Result == "black_and_white_boar");
            gu.Gu.Add(new OwnedGu { kind = "undefeated_hundred_battles", slot = 4, fedAt = gu.Now });
            gu.Tick(gu.Now);
            gu.Aperture.Essence = 50;
            Check(gu.Lacks(boars) == null && gu.FuseChance(boars) == 1 && gu.Fuse(boars) && gu.Bag.Count("gu_black_and_white_boar") == 1 &&
                  gu.Bag.Count("gu_black_boar") == 0 && gu.Bag.Count(ItemBook.RawMeat) == 0 && Mathf.Approximately(gu.Aperture.Essence, 30),
                $"black and white boar fused into one rank-one Gu for 20% ({gu.Aperture.Essence})");
            gu.Gu.RemoveAll(g => g.kind == "undefeated_hundred_battles");

            // Used once from the bag: a boar's strength for good (once per kind), a relic's small realm.
            int CellOf(string id)
            {
                for (var i = 0; i < Bag.Cells; i++)
                {
                    if (gu.Bag[i]?.id == id) return i;
                }

                return -1;
            }

            Check(gu.Activate(CellOf("gu_black_and_white_boar")) && Mathf.Approximately(gu.Strength, 0.55f) && gu.FistPower > 1.5f,
                $"the boar's strength stays for good (×{gu.FistPower:0.00})");
            gu.Bag.Add("gu_black_and_white_boar", 1, gu.Now);
            Check(!gu.Activate(CellOf("gu_black_and_white_boar")) && Mathf.Approximately(gu.Strength, 0.55f), "a second boar of the same kind adds nothing");
            gu.Bag.Add("gu_green_copper_relic", 1, gu.Now);
            gu.Aperture.Essence = 50;
            Check(gu.Activate(CellOf("gu_green_copper_relic")) && gu.Aperture.Stage == GuWorld.Cultivation.Stage.Middle, $"a green copper relic lifts rank one a small realm ({gu.Aperture.Stage})");

            // From the peak, the walls worn through, a full sea tries the next rank: seldom does it hold.
            gu.SetRealm(1, GuWorld.Cultivation.Stage.Peak);
            gu.Aperture.Progress = 0.5f;
            gu.Aperture.Essence = gu.Aperture.Cap;
            Check(!gu.BreakThrough(0) && gu.Aperture.Rank == 1, "before the walls are worn through there is no breakthrough");
            gu.Aperture.Progress = 1;
            gu.Aperture.Essence = 20;
            Check(!gu.BreakThrough(0) && gu.Aperture.Rank == 1, "short of a full sea there is no breakthrough");
            gu.Aperture.Essence = gu.Aperture.Cap;
            Check(gu.BreakChance > 0.005f && gu.BreakChance < 0.06f, $"the chance is small: {gu.BreakChance * 100:0.##}%");
            Check(!gu.BreakThrough(0.99f) && gu.Aperture.Rank == 1 && gu.Aperture.Essence == 0 && gu.Aperture.Failures == 1,
                "a failed try burns the sea and teaches a little");
            gu.Aperture.Essence = gu.Aperture.Cap;
            Check(gu.BreakThrough(0) && gu.Aperture.Rank == 2 && gu.Aperture.Stage == GuWorld.Cultivation.Stage.Initial && gu.Open == 7 && gu.Aperture.Essence == 0 &&
                  gu.Aperture.Failures == 0,
                $"a try that holds: rank two, seven cells open ({gu.Aperture.Rank}, {gu.Open})");
            gu.SetRealm(1, GuWorld.Cultivation.Stage.Initial);
            gu.Aperture.Essence = gu.Aperture.Cap;

            // A forced Gu: tired past 100 it still answers (it is not blocked) — unless it dies of it.
            var spear = GuBook.Find("bone_spear");
            gu.Bag.Add(spear.Item, 1, gu.Now);
            Check(gu.Refine(CellOf(spear.Item)) && gu.First(spear.Id) != null, "a bone spear refined");
            gu.Aperture.Essence = gu.Aperture.Cap;
            gu.First(spear.Id).tired = 90;
            var thrown = gu.UseGu(spear.Id);
            Check(thrown && gu.First(spear.Id).tired >= 100 && Object.FindAnyObjectByType<GuBolt>() != null,
                $"used tired, the spear flies and is spent ({gu.First(spear.Id)?.tired:0})");

            // Wild Gu live over the mountain: the common ones near, easy to catch for a master of their rank; a rank above, harder.
            var wildOne = World.WildGu.Make(body.transform.position + Vector3.forward * 3, GuBook.Find(GuWorldGame.WildKinds[0][0]), 1);
            var wildThree = World.WildGu.Make(body.transform.position + Vector3.forward * 5, GuBook.Find(GuWorldGame.WildKinds[2][0]), 2);
            Check(System.Array.TrueForAll(GuWorldGame.WildKinds, ids => System.Array.TrueForAll(ids, id => GuBook.Find(id) != null)) &&
                  wildOne.ChanceFor(1) > 0.8f && wildThree.ChanceFor(1) < 0.5f && wildThree.CostFor(gu.Aperture) > wildOne.CostFor(gu.Aperture),
                $"every wild Gu is in the book; a rank-three one is hard to catch at rank one ({wildThree.ChanceFor(1):0.00})");
            Object.Destroy(wildOne.gameObject);
            Object.Destroy(wildThree.gameObject);

            // A snake's venom keeps working in the blood.
            health.Poison(2, 10);
            Check(health.Poisoned, "a snake's venom works in the blood");
            health.Heal(1);
            Check(!health.Poisoned, "a healing Gu draws the venom out");

            // A roll: nothing touches the hero at its start. A guard raised just in time: the blow is parried whole.
            var roll = gu.GetComponent<Combat.Dodge>();
            health.Set(health.Max);
            var rolled = roll.Roll(body.transform.forward);
            health.TakeStrike(new Combat.Strike { Power = 200, Sharpness = 1, Point = body.transform.position + Vector3.up, Direction = -body.transform.forward }, Combat.BodyPart.Chest);
            Check(rolled && roll.Untouched && Mathf.Approximately(health.Current, health.Max), $"a roll lets a blow pass through ({rolled}, {health.Current:0})");
            var fists = gu.GetComponent<Combat.Melee>();
            fists.Guarding = false;
            fists.Guarding = true;
            Check(fists.Parrying, "the guard just raised parries");
            fists.Guarding = false;

            // Meditation: sitting, the sea is poured into the aperture's walls; worn through, the small realm rises.
            gu.SetRealm(1, GuWorld.Cultivation.Stage.Initial);
            gu.Aperture.Progress = 0;
            gu.Aperture.Essence = gu.Aperture.Cap;
            var endless = gu.EndlessEssence;
            gu.EndlessEssence = false;
            gu.Meditating = true;
            gu.Step(1);
            Check(gu.Aperture.Progress > 0 && gu.Aperture.Essence < gu.Aperture.Cap - 2, $"meditating pours essence into the walls ({gu.Aperture.Progress:0.000}, {gu.Aperture.Essence:0.0})");
            gu.Aperture.Progress = 0.9999f;
            gu.Aperture.Essence = gu.Aperture.Cap;
            gu.Step(1);
            Check(gu.Aperture.Stage == GuWorld.Cultivation.Stage.Middle && gu.Aperture.Progress < 0.01f, $"worn through, the small realm rises ({gu.Aperture.Stage})");
            gu.Meditating = false;
            gu.EndlessEssence = endless;
            gu.SetRealm(1, GuWorld.Cultivation.Stage.Initial);

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

            // The chat's cheats wait for the admin mode; with it on: the hour, a thing given, a jump across the ground; what is
            // suggested as one types. (The player's own setting is given back at the end.)
            adminWas = GuWorld.UI.GameSettings.AdminMode;
            GuWorld.UI.GameSettings.AdminMode = false;
            var refused = game.Commands.Run("/give primeval_stone 5");
            Check(refused.StartsWith("!") && game.Commands.Run("/where") != null && !game.Commands.Run("/where").StartsWith("!"),
                $"without the admin mode a cheat is refused, a look is not ({refused})");
            GuWorld.UI.GameSettings.AdminMode = true;
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
            var wolf = Creatures.Beast.All.Find(w => !w.Dead);
            Check(came == 1 && wolf != null && wolf.GetComponentsInChildren<MeshRenderer>().Length > 10, $"/wolves calls up a wolf beside the hero ({came})");

            if (wolf != null)
            {
                var fangs = gu.Bag.Count(ItemBook.WolfFang);
                var cut = new Combat.Strike { Power = 100, Sharpness = 1, Direction = Vector3.forward };
                wolf.TakeStrike(cut, null);
                Check(!wolf.Dead && wolf.Health < wolf.MaxHealth - 20 && wolf.Kind == Creatures.WolfKinds.Grey, $"a moonblade cuts a grey wolf deep ({wolf.Health:0} of {wolf.MaxHealth:0}, level {wolf.Level})");
                wolf.TakeStrike(cut, null);
                Check(wolf.Dead && gu.Bag.Count(ItemBook.WolfFang) == fangs + 1, "a second kills it, and its fang is the hero's");
            }

            // The kinds of wolves: a beast king comes with its pack, is far harder to wound, and a fist hardly scratches it.
            Check(Creatures.WolfKinds.All.Length == 5 && Creatures.WolfKinds.ThunderCrown.Tier == 3 && Creatures.BeastKinds.Find("bold") == Creatures.WolfKinds.Bold,
                "five kinds of wolves, up to the thunder crown wolf, a myriad beast king");
            var pack = game.SpawnPack(Creatures.WolfKinds.Bold, 1);
            var king = Creatures.Beast.All.Find(w => w.Kind == Creatures.WolfKinds.Bold);
            Check(pack == 4 && king != null && king.Level >= 12 && king.MaxHealth > 400, $"a bold lightning wolf comes with three lightning wolves ({pack}, level {king?.Level}, {king?.MaxHealth:0})");

            if (king != null)
            {
                var kingHealth = king.Health;
                king.TakeStrike(new Combat.Strike { Power = 40, Sharpness = 1, Direction = Vector3.forward }, null);
                var fist = kingHealth - king.Health;
                kingHealth = king.Health;
                king.TakeStrike(new Combat.Strike { Power = 400, Sharpness = 1, Direction = Vector3.forward }, null);
                Check(fist < 1 && kingHealth - king.Health > 60, $"a youth's fist hardly scratches a beast king ({fist:0.0}); a rank-three blow cuts it ({kingHealth - king.Health:0})");
            }

            // The other beasts: a boar, a bear, a stone monkey, a snake — each its own body; a stone monkey leaves its jade
            // pearl; a snake's venom keeps working in the blood.
            Check(Creatures.BeastKinds.All.Length == 11 && Creatures.BeastKinds.Find("boar_king")?.Tier == 1 && Creatures.WolfPacks.PackOf(Creatures.BeastKinds.BoarKing).Length == 5,
                "eleven kinds of beasts; the boar king comes with its sounder");

            foreach (var kind in Creatures.BeastKinds.Others)
            {
                var called = game.SpawnPack(kind, 1);
                var beast = Creatures.Beast.All.Find(b => b.Kind == kind && !b.Dead);
                Check(called >= 1 && beast != null && beast.GetComponentsInChildren<MeshRenderer>().Length > 8, $"{kind.Id} comes with a body of its own ({beast?.GetComponentsInChildren<MeshRenderer>().Length})");
            }

            var monkeyKilled = Creatures.Beast.All.Find(b => b.Kind == Creatures.BeastKinds.Monkey && !b.Dead);

            if (monkeyKilled != null)
            {
                var jade = gu.Bag.Count(ItemBook.JadeRock);
                monkeyKilled.TakeStrike(new Combat.Strike { Power = 3000, Sharpness = 1, Direction = Vector3.forward }, null);
                Check(monkeyKilled.Dead && gu.Bag.Count(ItemBook.JadeRock) > jade, "a stone monkey killed leaves its jade");
            }

            game.ClearWolves();

            // The wolf tide: its first wave comes at once, and the task's card shows it.
            game.StartTide(false);
            var tideCard = game.Event;
            Check(tideCard.HasValue && tideCard.Value.title == "Волчий прилив" && Creatures.Beast.All.FindAll(b => !b.Dead).Count >= 6,
                $"a wolf tide's first wave comes ({tideCard?.goal}, {tideCard?.progress})");
            game.StopTide();
            game.ClearWolves();
            Check(!game.Event.HasValue, "the tide stopped, the card is the task's again");

            // Gu Masters of the other clans: their Gu are in the book; called up, they fight on sight; a moonblade wounds one,
            // a blow far stronger kills them, and the stones in their pouch are the hero's.
            Check(Creatures.MasterKinds.All.Length == 11 && Creatures.MasterKinds.Of("xiong", 3).Tier > 0 &&
                  System.Array.TrueForAll(Creatures.MasterKinds.All, k => GuBook.Find(k.Far) != null && (k.Close == null || GuBook.Find(k.Close) != null) &&
                                                                          (k.Shield == null || GuBook.Find(k.Shield) != null) && (k.Might == null || GuBook.Find(k.Might) != null)),
                "eleven kinds of Gu Masters (with the demonic rogues), every Gu of theirs in the book");
            var masters = game.SpawnMasters("bai", 1, 1);
            var bai = Creatures.GuMaster.All.Find(m => !m.Dead);
            Check(masters == 1 && bai != null && bai.Hostile && bai.GetComponentsInChildren<Combat.HitZone>().Length > 10 && Creatures.Foes.All.Contains(bai),
                $"/masters calls up a Bai Gu Master, a body with parts to strike ({masters})");

            if (bai != null)
            {
                var whole = bai.Health;
                bai.TakeStrike(new Combat.Strike { Power = 100, Sharpness = 1, Direction = Vector3.forward, Point = bai.transform.position + Vector3.up }, Combat.BodyPart.Chest);
                Check(bai.Health < whole - 5 && bai.Hunting, $"a moonblade wounds them, and they fight ({whole:0} → {bai.Health:0})");
                var pouch = gu.Stones;
                bai.TakeStrike(new Combat.Strike { Power = 3000, Sharpness = 1, Direction = Vector3.forward, Point = bai.transform.position + Vector3.up * 1.6f }, Combat.BodyPart.Head);
                Check(bai.Dead && gu.Stones > pouch, $"a far stronger blow kills them, their stones are the hero's ({gu.Stones - pouch})");
            }

            game.ClearWolves();
            Check(Creatures.Foes.Living() == 0, "the proving ground's page sends every foe away");

            // Side tasks beside the academy's: taken from a villager, counted, handed in for stones and merit; the clan's missions of a day.
            var sideQuests = new Quests();
            var snakes = sideQuests.SidesFrom("Гу Юэ Мэйлин", 1, 1).Find(q => q.Id == "side_snakes");
            Check(snakes != null && sideQuests.Take(snakes) && sideQuests.Sides.Count == 1, "a villager gives a side task");
            sideQuests.Count("kill:snake", 2);
            Check(sideQuests.IsReady(snakes) && sideQuests.HandInSide(snakes, "Гу Юэ Мэйлин") && sideQuests.Merit == snakes.RewardMerit && sideQuests.IsDone("side_snakes"),
                $"counted and handed in, it pays merit ({sideQuests.Merit})");
            var missions = SideQuests.Missions(5);
            Check(missions.Length == 3 && missions[0].Id != missions[1].Id && Quests.Find(missions[2].Id)?.Title == missions[2].Title && SideQuests.Missions(5)[1].Id == missions[1].Id,
                $"three clan missions a day, the same all day ({string.Join(", ", System.Array.ConvertAll(missions, m => m.Title))})");
            Check(Quests.Matches("catch:*", "catch:jade_skin") && !Quests.Matches("catch:*", "kill:wolf"), "a task's event can be a pattern (any wild Gu)");
            sideQuests.Take(missions[0]);
            var questSave = sideQuests.Save();
            var reloaded = new Quests();
            reloaded.Load(questSave);
            Check(reloaded.Sides.Count == 1 && reloaded.Sides[0].Id == missions[0].Id && reloaded.Merit == sideQuests.Merit && reloaded.IsDone("side_snakes"),
                "side tasks, missions and merit survive a save");

            // The rival clans: wary at first; killing theirs makes them attack on sight, then send war parties; the days cool it.
            Creatures.Clans.Load(null);
            Check(!Creatures.Clans.AttacksOnSight("bai") && Creatures.Clans.AttacksOnSight(Creatures.Clans.Demonic),
                "at first the Bai warn before they fight; the demonic rogues never do");
            Creatures.Clans.Anger("bai", 60);
            Check(Creatures.Clans.AttacksOnSight("bai") && Creatures.Clans.SendsWarParties("bai") && Creatures.Clans.Standing("bai") == "война",
                $"angered, the Bai go to war ({Creatures.Clans.Hostility("bai"):0})");
            Creatures.Clans.Cool(24 * 60 * 40);
            Check(Mathf.Abs(Creatures.Clans.Hostility("bai") - Creatures.Clans.Start("bai")) < 0.5f, "the days cool it back to where it began");
            game.SpawnMasters(Creatures.Clans.Demonic, 2, 1);
            var rogue = Creatures.GuMaster.All.Find(m => !m.Dead && m.Kind.Clan == Creatures.Clans.Demonic);
            Check(rogue != null && rogue.Hostile && rogue.Rung.Contains("демонический"), $"a demonic rogue comes hostile ({rogue?.Title})");
            game.ClearWolves();
            Creatures.Clans.Load(null);

            // The sea refills sooner: a tenth for the poorest talent, three tenths for the talented.
            Check(Mathf.Abs(Aperture.RefillCut(30) - 0.1f) < 0.001f && Mathf.Abs(Aperture.RefillCut(88) - 0.3f) < 0.001f &&
                  Mathf.Abs(Aperture.RefillHours(40) - 24 * (1 - Aperture.RefillCut(40))) < 0.01f,
                $"the sea refills 10–30% sooner (88%: {Aperture.RefillHours(88):0.0} h, 30%: {Aperture.RefillHours(30):0.0} h)");

            // A softer landing: a ten-metre fall hurts about a third of the health, a four-metre one not at all.
            Check(Health.SafeLanding >= 15 && (Mathf.Sqrt(2 * 25 * 10) - Health.SafeLanding) * Health.HarmPerSpeed < 35,
                "a fall hurts less (10 m ≈ 30)");

            // A tamed beast is the hero's: their own blows pass it by, and it bites a foe that hunts them.
            game.SpawnPack(Creatures.WolfKinds.Grey, 2);
            var tamePack = Creatures.Beast.All.FindAll(b => !b.Dead);

            if (tamePack.Count >= 2)
            {
                var pet = tamePack[0];
                var foe = tamePack[1];
                pet.Afflict(Creatures.Affliction.Tame, 1, 1);
                var petHealth = pet.Health;
                pet.TakeStrike(new Combat.Strike { Power = 500, Sharpness = 1, Direction = Vector3.forward, Point = pet.transform.position, Source = body.gameObject }, null);
                Check(pet.Tamed && pet.Health >= petHealth, "the hero's own blow passes their tamed beast by");
                Check(!System.Linq.Enumerable.Contains(Creatures.Foes.Near(pet.transform.position, 5), pet) &&
                      System.Linq.Enumerable.Contains(Creatures.Foes.Near(foe.transform.position, 5), foe),
                    "a tamed beast is no longer counted a foe, its packmate still is");
            }

            game.ClearWolves();

            // The body trains by doing: a trait rises past ten, and gives what it says; stamina is spent and comes back.
            var physique = Object.FindAnyObjectByType<Physique>();
            physique.Renew();
            var healthBefore = health.Max;
            physique.Train(Trait.Vitality, Physique.Need(10) + Physique.Need(11) + 1);
            physique.Train(Trait.Strength, Physique.Need(10) + 1);
            Check(physique.Level(Trait.Vitality) == 12 && health.Max > healthBefore && physique.Level(Trait.Strength) == 11,
                $"training raises a trait, vitality the health ({physique.Level(Trait.Vitality)}, {healthBefore:0} → {health.Max:0})");
            Check(physique.Spend(30) && physique.Stamina < physique.MaxStamina - 29 && !physique.Spend(physique.MaxStamina * 2),
                $"stamina is spent, and an effort past it is refused ({physique.Stamina:0} / {physique.MaxStamina:0})");
            physique.Drain(1000);
            Check(physique.Spent, "drained to nothing, the body is spent");

            for (var i = 0; i < 60; i++)
            {
                physique.Step(0.1f);
            }

            Check(!physique.Spent && physique.Stamina > physique.MaxStamina * 0.25f, $"rested, the stamina comes back ({physique.Stamina:0})");
            physique.Renew();

            // A rank lost (the Do-or-Die Gu, set by hand) never loses a Gu: those in closed cells find room, or go to the bag.
            gu.SetRealm(3, GuWorld.Cultivation.Stage.Initial);

            foreach (var id in new[] { "jade_skin", "bone_spear", "icicle", "star_dart", "eating_wind", "carapace", "white_jade" })
            {
                gu.Grant(GuBook.Find(id), -1, true);
            }

            int GuHeld() => gu.Gu.Count + System.Linq.Enumerable.Sum(GuBook.All, k => gu.Bag.Count(k.Item));
            var heldBefore = GuHeld();
            gu.SetRealm(1, GuWorld.Cultivation.Stage.Initial);
            var after = GuHeld();
            Check(gu.Gu.TrueForAll(g => g.slot < Cultivator.OpenSlots(1)) && after == heldBefore,
                $"a rank lost keeps every Gu: in the open cells or in the bag ({heldBefore} → {after}, {gu.Gu.Count} in the ring)");

            // The weather in a fight: rain dampens fire and feeds ice; the night feeds the moon.
            var (rainWas, nightWas) = (Battle.Rain, Battle.Night);
            Battle.Rain = 1;
            Battle.Night = true;
            Check(Battle.Scale(GuBook.Find("fire_pellet")) < 0.7f && Battle.Scale(GuBook.Find("icicle")) > 1.15f && Battle.Scale(GuBook.Find("moonlight")) > 1.15f &&
                  Mathf.Approximately(Battle.Scale(GuBook.Find("bone_spear")), 1) && Battle.Effects != null,
                $"in rain at night fire is weaker ({Battle.Scale(GuBook.Find("fire_pellet")):0.00}), ice and the moon stronger");
            (Battle.Rain, Battle.Night) = (rainWas, nightWas);

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

        /// <summary>The player's own admin mode, given back at the end (null: not touched).</summary>
        static bool? adminWas;

        static void Finish(string problem)
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }

            if (adminWas.HasValue)
            {
                GuWorld.UI.GameSettings.AdminMode = adminWas.Value;
                adminWas = null;
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
