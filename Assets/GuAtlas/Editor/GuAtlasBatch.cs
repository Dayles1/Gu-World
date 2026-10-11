// Runs the GU World Atlas import without its window (batch mode or a menu click),
// into a scene of its own: Assets/GuAtlas/GuWorldAtlas.unity.
#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GuAtlas
{
    public static class GuAtlasBatch
    {
        const string ScenePath = "Assets/GuAtlas/GuWorldAtlas.unity";
        const string DefaultJson = "mir_gu-unity/world.json";
        const float DefaultScale = 0.001f;

        /// <summary>
        /// Imports the atlas export (world.json, heightmap.r16 beside it) into a new scene with
        /// GuAtlasImporter's own Import, at 1 Unity unit = 1 km unless told otherwise:
        /// Unity -batchmode -quit -executeMethod GuAtlas.GuAtlasBatch.ImportIntoScene [-atlasJson path] [-atlasScale 0.001]
        /// </summary>
        [MenuItem("Tools/GU World Atlas/Import Into Atlas Scene")]
        public static void ImportIntoScene()
        {
            var json = Path.GetFullPath(Argument("-atlasJson") ?? DefaultJson);
            var scale = float.TryParse(Argument("-atlasScale"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : DefaultScale;

            if (!File.Exists(json))
            {
                Debug.LogError($"GU World Atlas: no world.json at {json}");

                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var importer = ScriptableObject.CreateInstance<GuAtlasImporter>();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(GuAtlasImporter).GetField("jsonPath", flags).SetValue(importer, json);
            typeof(GuAtlasImporter).GetField("scale", flags).SetValue(importer, scale);
            typeof(GuAtlasImporter).GetMethod("Import", flags).Invoke(importer, null);
            UnityEngine.Object.DestroyImmediate(importer);

            // A camera high over the middle of the world, looking down at it.
            var camera = Camera.main;

            if (camera != null)
            {
                var size = 3200000 * scale;
                camera.transform.position = new Vector3(0, size * 0.55f, -size * 0.55f);
                camera.transform.LookAt(Vector3.zero);
                camera.farClipPlane = size * 3;
                camera.nearClipPlane = Mathf.Max(0.1f, size * 0.0005f);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"GU World Atlas: the world is in {ScenePath} (scale {scale} Unity units per metre)");
        }

        /// <summary>A picture of the atlas scene from its camera (for checking): -atlasShot path.png</summary>
        public static void Snapshot()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var camera = Camera.main;
            var target = new RenderTexture(1600, 900, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var picture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            picture.Apply();
            camera.targetTexture = null;
            RenderTexture.active = null;
            File.WriteAllBytes(Argument("-atlasShot") ?? "atlas.png", picture.EncodeToPNG());
            Debug.Log("GU World Atlas: picture taken");
        }

        static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, name);

            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }
    }
}
#endif
