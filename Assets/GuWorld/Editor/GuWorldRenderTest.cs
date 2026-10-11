using System.IO;
using GuWorld.Cultivation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// Draws a few of the game's runtime-made things — a moonblade, wire lines — in an
    /// empty scene with a camera of its own and saves the picture, to check by eye that
    /// they show (batch mode):
    ///
    ///   Unity -batchmode -projectPath . -executeMethod GuWorld.Editor.GuWorldRenderTest.Run -picture out.png
    /// </summary>
    public static class GuWorldRenderTest
    {
        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            var at = System.Array.IndexOf(args, "-picture");
            var path = at >= 0 && at + 1 < args.Length ? args[at + 1] : "render-test.png";

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var camera = Camera.main;
            camera.transform.position = new Vector3(0, 2.2f, -3.5f);
            camera.transform.LookAt(new Vector3(0, 1.2f, 2.5f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.15f, 0.16f, 0.18f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 3;

            Moonblade.Throw(new Vector3(0, 1.3f, 1.5f), Vector3.forward, GuBook.Moonlight, null);

            // Wire lines, as the hitbox view draws them.
            var lines = new GameObject("Lines");
            lines.transform.position = new Vector3(-1.8f, 0, 2);
            var mesh = new Mesh();
            var points = new[] { new Vector3(0, 0, 0), new Vector3(0, 2, 0), new Vector3(0, 2, 0), new Vector3(1, 2, 0), new Vector3(1, 2, 0), new Vector3(1, 0, 0) };
            mesh.SetVertices(points);
            mesh.SetColors(new[] { Color.red, Color.red, Color.green, Color.green, Color.cyan, Color.cyan });
            mesh.SetIndices(new[] { 0, 1, 2, 3, 4, 5 }, MeshTopology.Lines, 0);
            lines.AddComponent<MeshFilter>().sharedMesh = mesh;
            lines.AddComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("GuWorld/Lines"));

            var texture = new RenderTexture(960, 540, 24);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var picture = new Texture2D(960, 540, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            picture.Apply();
            File.WriteAllBytes(path, picture.EncodeToPNG());
            RenderTexture.active = null;
            Debug.Log($"GU World: render test saved to {path}");
            EditorApplication.Exit(0);
        }
    }
}
