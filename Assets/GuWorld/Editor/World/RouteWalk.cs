using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GuWorld.Physics;
using GuWorld.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// Walks the mountain's ways with the game's own hero — its capsule (0.3 × 1.8 m), its speeds,
    /// its jump, its climbing of ledges — the way a player would: running at the next point a few
    /// metres ahead, jumping when held up a moment. A way is passable only if the body gets along it.
    /// Where it is held up for long, falls far, or is pushed off the way, that is written down with
    /// the place, and the walk goes on a little further along (so one bad place does not hide the rest).
    ///
    /// The routes: the trails (from the world's plan) and the streets of each village end to end.
    /// The report goes to Logs/QingMaoBuild/routes.md and the build log.
    ///
    /// Unity -batchmode -projectPath . -executeMethod GuWorld.Editor.RouteWalk.Run [-scene path] [-only name-part]
    /// </summary>
    public static class RouteWalk
    {
        const string Running = "GuWorld.RouteWalk.Running";
        const float Dt = 1f / 60f;
        static int frames;
        static string only;
        /// <summary>`-debug`: where the body is put after each trouble, and where it ends up.</summary>
        static bool debug;

        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            var at = System.Array.IndexOf(args, "-scene");
            var scene = at >= 0 && at + 1 < args.Length ? args[at + 1] : QingMaoBuilder.ScenePath;
            var o = System.Array.IndexOf(args, "-only");
            only = o >= 0 && o + 1 < args.Length ? args[o + 1] : null;
            debug = System.Array.IndexOf(args, "-debug") >= 0;
            SessionState.SetBool(Running + ".debug", debug);
            BuildLog.Begin($"route walk on {scene}");
            EditorSceneManager.OpenScene(scene);
            BuildLog.Mark("scene open");
            frames = 0;
            SessionState.SetBool(Running, true);
            SessionState.SetString(Running + ".only", only ?? "");
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            // Entering play mode reloads the scripts: carry on after it.
            if (SessionState.GetBool(Running, false))
            {
                only = SessionState.GetString(Running + ".only", "");
                only = only.Length == 0 ? null : only;
                debug = SessionState.GetBool(Running + ".debug", false);
                EditorApplication.update += Tick;
            }
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || ++frames < 30)
            {
                if (frames > 4000)
                {
                    Finish("play mode never started", 1);
                }

                return;
            }

            EditorApplication.update -= Tick;
            var code = 0;

            try
            {
                code = WalkAll();
            }
            catch (System.Exception e)
            {
                BuildLog.Error("route walk", e);
                code = 1;
            }

            Finish("done", code);
        }

        static void Finish(string why, int code)
        {
            SessionState.SetBool(Running, false);
            BuildLog.Write($"route walk: {why}");
            EditorApplication.ExitPlaymode();

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        class Trouble
        {
            public string Kind;
            public Vector3 At;
            public string Note;
        }

        static int WalkAll()
        {
            var body = Object.FindAnyObjectByType<HeroMotor>();
            var plan = Object.FindAnyObjectByType<WorldPlan>();

            if (body == null || plan == null)
            {
                BuildLog.Write($"route walk: no hero ({body != null}) or no world plan ({plan != null}) in the scene");

                return 1;
            }

            // The player's controller would tick the body too: the walk drives it alone.
            foreach (var controller in Object.FindObjectsByType<Systems.PlayerController>())
            {
                controller.enabled = false;
            }

            BuildLog.Mark("play mode: walking");
            var routes = new List<(string name, Vector3[] points)>();
            routes.AddRange(plan.Trails.Select(t => ("Тропа: " + t.Name, t.Points)));
            // And back: a drop one jumps down going one way is a wall going the other.
            routes.AddRange(plan.Trails.Select(t => ("Тропа обратно: " + t.Name, t.Points.Reverse().ToArray())));
            routes.AddRange(plan.Streets.Where(s => s.Points.Length >= 2).Select(s => ("Улица: " + s.Name, s.Points)));

            if (only != null)
            {
                routes = routes.Where(r => r.name.Contains(only)).ToList();
            }

            var report = new StringBuilder();
            report.AppendLine($"# Проходимость: {System.DateTime.Now:yyyy-MM-dd HH:mm}");
            report.AppendLine();
            report.AppendLine("Герой: капсула 0.3 × 1.8 м, бег 7.5 м/с, прыжок 1.5 м, ступенька 0.45 м, склон до 50°, залезание на уступы 0.5–2.3 м.");
            report.AppendLine();
            report.AppendLine("| Маршрут | Длина, м | Время, с | Застревания | Падения | Уходы с пути |");
            report.AppendLine("|---|---|---|---|---|---|");
            var details = new StringBuilder();
            var passed = 0;

            foreach (var (name, points) in routes)
            {
                var troubles = new List<Trouble>();
                var (length, seconds) = Walk(body, points, troubles);
                var stuck = troubles.Count(t => t.Kind == "stuck");
                var falls = troubles.Count(t => t.Kind == "fall");
                var off = troubles.Count(t => t.Kind == "off");
                report.AppendLine($"| {name} | {length:0} | {seconds:0} | {stuck} | {falls} | {off} |");

                if (troubles.Count == 0)
                {
                    passed++;
                }
                else
                {
                    details.AppendLine($"- **{name}**:");

                    foreach (var t in troubles.Take(12))
                    {
                        details.AppendLine($"  - {t.Kind} у ({t.At.x:0}, {t.At.y:0}, {t.At.z:0}) — {t.Note}");
                    }

                    if (troubles.Count > 12)
                    {
                        details.AppendLine($"  - … и ещё {troubles.Count - 12}");
                    }
                }

                BuildLog.Write($"route: {name}: {length:0} m in {seconds:0} s, stuck {stuck}, falls {falls}, off {off}");
            }

            report.AppendLine();
            report.AppendLine($"Без замечаний: {passed} из {routes.Count}.");
            report.AppendLine();
            report.AppendLine("## Места с замечаниями");
            report.AppendLine();
            report.Append(details.Length > 0 ? details.ToString() : "Нет.\n");
            Directory.CreateDirectory(BuildLog.Folder);
            File.WriteAllText(Path.Combine(BuildLog.Folder, "routes.md"), report.ToString());
            BuildLog.Write($"route walk: {passed} of {routes.Count} routes without trouble; report in routes.md");

            return 0;
        }

        static float Ground(Vector3 p) => QingMao.GroundAt(p);

        /// <summary>Walks one way: the length along it and the time it took; what held it up goes into `troubles`.</summary>
        static (float length, float seconds) Walk(HeroMotor body, Vector3[] points, List<Trouble> troubles)
        {
            // The way on the ground, a point every metre or so.
            var way = new List<Vector3>();

            for (var i = 0; i + 1 < points.Length; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z))));

                for (var s = 0; s < steps; s++)
                {
                    var p = Vector3.Lerp(a, b, (float)s / steps);
                    way.Add(new Vector3(p.x, 0, p.z));
                }
            }

            way.Add(new Vector3(points[points.Length - 1].x, 0, points[points.Length - 1].z));
            var length = 0f;

            for (var i = 0; i + 1 < way.Count; i++)
            {
                length += Vector3.Distance(way[i], way[i + 1]);
            }

            var start = way[0];
            Stream(new Vector3(start.x, 0, start.z));
            body.PlaceAt(new Vector3(start.x, Ground(start) + 0.3f, start.z));

            for (var k = 0; k < 20; k++)
            {
                body.Tick(Dt, new MotorInput { speedScale = 1 });
            }

            var index = 0;
            var time = 0f;
            var lastProgressTime = 0f;
            var bestIndex = 0;
            var fallFrom = float.NaN;
            var limit = length / 3 + 60;
            var jumpWait = 0f;

            while (index < way.Count - 1 && time < limit)
            {
                var here = body.transform.position;
                var flat = new Vector3(here.x, 0, here.z);

                // The nearest point of the way ahead (within a window), then aim a few metres past it.
                var best = index;
                var bestDistance = float.PositiveInfinity;

                for (var i = index; i < Mathf.Min(way.Count, index + 40); i++)
                {
                    var d = (way[i] - flat).sqrMagnitude;

                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = i;
                    }
                }

                index = best;

                if (index > bestIndex)
                {
                    bestIndex = index;
                    lastProgressTime = time;
                }

                if (Mathf.Sqrt(bestDistance) > 6)
                {
                    troubles.Add(new Trouble { Kind = "off", At = here, Note = $"в {Mathf.Sqrt(bestDistance):0.0} м от пути" });
                    Skip(body, way, ref index, 4);
                    lastProgressTime = time;
                    continue;
                }

                var target = way[Mathf.Min(way.Count - 1, index + 3)];
                var dir = target - flat;
                dir.y = 0;
                dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.zero;

                // Held up a while: a player steps round what is in the way (a well, a cart), left, then right.
                var held = time - lastProgressTime;

                if (held > 1.2f && held < 5.5f)
                {
                    var side = ((int)((held - 1.2f) / 1.1f)) % 2 == 0 ? 70f : -70f;
                    dir = Quaternion.Euler(0, side, 0) * dir;
                }

                // Held up a moment: a player would jump.
                var jump = false;

                if (time - lastProgressTime > 0.6f && jumpWait <= 0)
                {
                    jump = true;
                    jumpWait = 0.8f;
                }

                jumpWait -= Dt;

                // The world's squares round the body, loaded as it goes (the whole walk is one frame: the streamer cannot).
                if ((body.transform.position - streamedAt).sqrMagnitude > 60 * 60)
                {
                    Stream(body.transform.position);
                }

                body.Tick(Dt, new MotorInput { moveX = dir.x, moveZ = dir.z, sprint = true, speedScale = 1, jump = jump });
                time += Dt;

                // A fall: from where it left the ground to where it lands.
                if (!body.Grounded && float.IsNaN(fallFrom))
                {
                    fallFrom = body.transform.position.y;
                }
                else if (body.Grounded && !float.IsNaN(fallFrom))
                {
                    var drop = fallFrom - body.transform.position.y;

                    if (drop > 3.5f)
                    {
                        troubles.Add(new Trouble { Kind = "fall", At = body.transform.position, Note = $"падение на {drop:0.0} м" });
                    }

                    fallFrom = float.NaN;
                }

                // Under the ground or far below it: lost.
                if (body.transform.position.y < Ground(body.transform.position) - 3)
                {
                    troubles.Add(new Trouble { Kind = "fall", At = body.transform.position, Note = "провалился сквозь землю" });
                    Skip(body, way, ref index, 6);
                    lastProgressTime = time;
                    continue;
                }

                if (time - lastProgressTime > 6)
                {
                    troubles.Add(new Trouble { Kind = "stuck", At = body.transform.position, Note = $"6 с без продвижения (на {index} м пути из {way.Count})" });
                    Skip(body, way, ref index, 8);
                    lastProgressTime = time;
                }
            }

            if (index < way.Count - 1)
            {
                troubles.Add(new Trouble { Kind = "stuck", At = body.transform.position, Note = "не дошёл до конца за отведённое время" });
            }

            return (length, time);
        }

        static Vector3 streamedAt = new Vector3(float.MaxValue, 0, 0);
        static int streams;

        static void Stream(Vector3 at)
        {
            streamedAt = at;

            if (ChunkStreamer.Current != null)
            {
                ChunkStreamer.Current.LoadNowAround(at);
                UnityEngine.Physics.SyncTransforms();

                // What the squares left behind used, let go now and then (the walk crosses the whole mountain).
                if (++streams % 25 == 0)
                {
                    EditorUtility.UnloadUnusedAssetsImmediate();
                }
            }
        }

        /// <summary>Puts the body a few metres further along the way (after a trouble, to test the rest).</summary>
        static void Skip(HeroMotor body, List<Vector3> way, ref int index, int metres)
        {
            index = Mathf.Min(way.Count - 1, index + metres);
            var p = way[index];
            Stream(new Vector3(p.x, 0, p.z));
            var before = body.transform.position;
            body.PlaceAt(new Vector3(p.x, Ground(p) + 0.3f, p.z));
            var placed = body.transform.position;

            for (var k = 0; k < 10; k++)
            {
                body.Tick(Dt, new MotorInput { speedScale = 1 });
            }

            if (debug)
            {
                BuildLog.Write($"  skip: from {before} to way {p} (ground {Ground(p):0.0}) placed {placed}, after 10 steps {body.transform.position}, grounded {body.Grounded}, climbing {body.ClimbProgress}, tree {body.Tree}, sitting {body.Sitting}");
            }
        }
    }
}
