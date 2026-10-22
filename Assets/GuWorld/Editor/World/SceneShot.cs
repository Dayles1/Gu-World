using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GuWorld.Editor
{
    /// <summary>
    /// Pictures of a built scene from the editor, to check by eye what the builder made:
    /// views from points (x,y,z,yaw,pitch) and maps straight down (orthographic). The sun is
    /// set to mid-morning; the game's own sky and weather do not run here.
    ///
    /// Unity -batchmode -projectPath . -executeMethod GuWorld.Editor.SceneShot.Run -scene Assets/…/QingMao.unity
    ///   -shots "view:x,y,z,yaw,pitch,file.png;map:x,z,size,file.png" [-ground] (y is above the ground with -ground)
    /// </summary>
    public static class SceneShot
    {
        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();

            string Arg(string name)
            {
                var i = System.Array.IndexOf(args, name);

                return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            }

            var code = 0;

            try
            {
                BuildLog.Begin($"shots of {Arg("-scene")}");
                EditorSceneManager.OpenScene(Arg("-scene"), OpenSceneMode.Single);
                BuildLog.Mark("scene open");
                var camera = Camera.main ?? new GameObject("Shot Camera").AddComponent<Camera>();
                camera.farClipPlane = 12000;
                var sun = Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);

                if (sun != null)
                {
                    sun.transform.rotation = Quaternion.Euler(42, 35, 0);
                    sun.intensity = 1.3f;
                    sun.color = new Color(1, 0.95f, 0.88f);
                    sun.enabled = true;
                    RenderSettings.sun = sun;
                }

                BuildLog.Write($"sun: {(sun != null ? $"{sun.name} {sun.intensity} {sun.transform.forward} shadows {sun.shadows}" : "none")}");

                if (args.Contains("-nopost"))
                {
                    foreach (var volume in Object.FindObjectsByType<UnityEngine.Rendering.Volume>())
                    {
                        volume.enabled = false;
                    }
                }

                if (args.Contains("-noshadows") && sun != null)
                {
                    sun.shadows = LightShadows.None;
                }

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.5f, 0.55f, 0.62f);
                RenderSettings.ambientEquatorColor = new Color(0.38f, 0.4f, 0.38f);
                RenderSettings.ambientGroundColor = new Color(0.2f, 0.19f, 0.16f);
                RenderSettings.fog = false;
                UnityEngine.Physics.SyncTransforms();

                foreach (var shot in (Arg("-shots") ?? "").Split(';').Where(s => s.Contains(":")))
                {
                    var kind = shot.Substring(0, shot.IndexOf(':'));
                    var parts = shot.Substring(kind.Length + 1).Split(',');
                    var v = parts.Take(parts.Length - 1).Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                    var file = parts[parts.Length - 1];

                    if (kind == "map")
                    {
                        camera.orthographic = true;
                        camera.orthographicSize = v[2] / 2;
                        camera.transform.SetPositionAndRotation(new Vector3(v[0], 4000, v[1]), Quaternion.Euler(90, 0, 0));
                        camera.nearClipPlane = 1;
                        camera.farClipPlane = 8000;
                        Save(camera, file, 1600, 1600);
                    }
                    else
                    {
                        camera.orthographic = false;
                        camera.fieldOfView = 60;
                        camera.nearClipPlane = 0.2f;
                        camera.farClipPlane = 12000;
                        var y = v[1];

                        if (args.Contains("-ground"))
                        {
                            y += World.QingMao.GroundAt(new Vector3(v[0], 0, v[2]));
                        }

                        camera.transform.SetPositionAndRotation(new Vector3(v[0], y, v[2]), Quaternion.Euler(v[4], v[3], 0));
                        Save(camera, file, 1600, 900);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        static void Save(Camera camera, string file, int width, int height)
        {
            var texture = new RenderTexture(width, height, 24);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var picture = new Texture2D(width, height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            picture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
            File.WriteAllBytes(file, picture.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            BuildLog.Mark($"shot {Path.GetFileName(file)}");
        }
    }
}
