using System.Collections.Generic;
using GuWorld.Body;
using GuWorld.Physics;
using GuWorld.Render;
using GuWorld.State;
using System.IO;
using GuWorld.World;
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

        static void Run(HeroMotor body, float seconds, Vector2 move)
        {
            for (var t = 0f; t < seconds; t += Dt)
            {
                body.Tick(Dt, new MotorInput { moveX = move.x, moveZ = move.y, speedScale = 1 });
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
