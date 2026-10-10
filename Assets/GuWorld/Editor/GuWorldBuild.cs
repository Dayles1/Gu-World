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
            if (!File.Exists(ScenePath))
            {
                GuWorldSetup.CreateScene();
            }

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
                Debug.Log($"GU World: built {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB) in {summary.totalTime.TotalSeconds:0} s");
            }
            else
            {
                Debug.LogError($"GU World: the build {summary.result} with {summary.totalErrors} errors");

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }
    }
}
