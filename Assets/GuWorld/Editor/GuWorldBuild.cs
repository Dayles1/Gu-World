using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// Builds the game for Windows into Builds/GuWorld-&lt;version&gt;/, from the
    /// "Gu World" menu or the command line:
    ///
    ///   Unity -batchmode -quit -projectPath . -executeMethod GuWorld.Editor.GuWorldBuild.Windows
    ///
    /// The version is the project's (Player Settings → Version).
    /// </summary>
    public static class GuWorldBuild
    {
        const string ScenePath = "Assets/GuWorld/Scenes/TestGrounds.unity";

        [MenuItem("Gu World/Build for Windows", priority = 40)]
        public static void Windows()
        {
            BuildLog.Begin("Windows player build");
            if (!File.Exists(ScenePath))
            {
                GuWorldSetup.CreateScene();
            }

            // The mountain is made, not kept, and built on its own (Tools/QingMao/build.ps1, or Gu World → Build Qing Mao Mountain):
            // a player build without it would start on the proving ground.
            if (!File.Exists(QingMaoBuilder.ScenePath))
            {
                Debug.LogError($"GU World: Qing Mao Mountain is not built ({QingMaoBuilder.ScenePath}). Build it first: Tools/QingMao/build.ps1.");

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            // Colliders' shapes baked at build time (the mountain's drawn-only meshes are not readable in the game).
            PlayerSettings.bakeCollisionMeshes = true;
            // The GPU's time per frame, for the timed runs (-bench).
            PlayerSettings.enableFrameTimingStats = true;

            // The scenes the project lists (the mountain first, where it exists).
            var scenes = System.Array.ConvertAll(System.Array.FindAll(EditorBuildSettings.scenes, s => s.enabled && File.Exists(s.path)), s => s.path);
            var folder = Path.Combine("Builds", $"GuWorld-{PlayerSettings.bundleVersion}");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(folder, "GuWorld.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });

            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                BuildLog.Write($"built {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB) in {summary.totalTime.TotalSeconds:0} s");
            }
            else
            {
                BuildLog.Write($"the build {summary.result} with {summary.totalErrors} errors");

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }
    }
}
