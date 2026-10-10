using System.IO;
using System.Linq;
using GuWorld.State;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuWorld.Editor
{
    /// <summary>
    /// Sets the project up for GU World: the materials, and the proving
    /// ground's scene (a camera, a sun and the GuWorldGame that builds
    /// everything else), first in the build. Done once by itself when the
    /// scene is missing, and again on demand from the "Gu World" menu.
    /// </summary>
    [InitializeOnLoad]
    public static class GuWorldSetup
    {
        const string Root = "Assets/GuWorld";
        const string ScenePath = Root + "/Scenes/TestGrounds.unity";
        const string MaterialsPath = Root + "/Materials";
        const string MaleModelPath = Root + "/Models/human-male.glb";
        const string FemaleModelPath = Root + "/Models/human-female.glb";

        static GuWorldSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !File.Exists(ScenePath))
                {
                    CreateScene();
                }
            };
        }

        [MenuItem("Gu World/Open Test Grounds", priority = 1)]
        public static void Open()
        {
            if (!File.Exists(ScenePath))
            {
                CreateScene();
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        [MenuItem("Gu World/Rebuild Test Grounds Scene", priority = 2)]
        public static void CreateScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);

            var game = new System.Collections.Generic.Dictionary<string, Material>
            {
                ["Ground"] = MaterialAsset("Ground", 0x8c9478),
                ["Block"] = MaterialAsset("Block", 0xb9b4aa),
                ["Stone"] = MaterialAsset("Stone", 0x8f8c84),
                ["Hero"] = MaterialAsset("Hero", 0x4a6b8a),
            };

            // Made beside whatever is open, saved, then closed: the open scene is left alone.
            // Unity cannot add a scene next to an untitled one, so that one (empty in batch mode) is replaced.
            var active = EditorSceneManager.GetActiveScene();
            var alone = Application.isBatchMode || (string.IsNullOrEmpty(active.path) && !active.isDirty);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, alone ? NewSceneMode.Single : NewSceneMode.Additive);

            var camera = new GameObject("Main Camera") { tag = "MainCamera" };
            camera.AddComponent<Camera>();
            camera.AddComponent<AudioListener>();
            SceneManagerMove(camera, scene);

            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            SceneManagerMove(sun, scene);

            var root = new GameObject("GU World");
            var component = root.AddComponent<GuWorldGame>();
            component.GroundMaterial = game["Ground"];
            component.BlockMaterial = game["Block"];
            component.StoneMaterial = game["Stone"];
            component.HeroMaterial = game["Hero"];
            Configure(component);
            SceneManagerMove(root, scene);

            EditorSceneManager.SaveScene(scene, ScenePath);

            if (!alone)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            // In the build, after the mountain if there is one (the game begins on it).
            if (EditorBuildSettings.scenes.All(s => s.path != ScenePath))
            {
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            }

            Debug.Log($"GU World: the proving ground's scene is ready at {ScenePath} (menu Gu World → Open Test Grounds).");
        }

        /// <summary>Gives a game its bodies and the materials the hero is dressed from (any location).</summary>
        public static void Configure(GuWorldGame component)
        {
            component.HeroModel = AssetDatabase.LoadAssetAtPath<GameObject>(MaleModelPath);
            component.HeroModelFemale = AssetDatabase.LoadAssetAtPath<GameObject>(FemaleModelPath);
            component.ClothMaterial = ClothAsset("Cloth", transparent: false);
            component.FaintMaterial = ClothAsset("Faint", transparent: true);
        }

        [MenuItem("Gu World/Delete Save (New Game)", priority = 20)]
        public static void DeleteSave()
        {
            SaveGame.Delete();
            Debug.Log($"GU World: the save is gone ({SaveGame.FilePath}); the next play is a new game.");
        }

        [MenuItem("Gu World/Show Save File", priority = 21)]
        public static void ShowSave()
        {
            EditorUtility.RevealInFinder(File.Exists(SaveGame.FilePath) ? SaveGame.FilePath : Application.persistentDataPath);
        }

        static void SceneManagerMove(GameObject gameObject, UnityEngine.SceneManagement.Scene scene)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject, scene);
        }

        /// <summary>
        /// A white URP Lit material drawn on both sides — the template for the hair and
        /// clothes (copied and coloured at runtime) — or a see-through one for stubble.
        /// Kept as assets so the build keeps the shader variants they need.
        /// </summary>
        static Material ClothAsset(string name, bool transparent)
        {
            Directory.CreateDirectory(MaterialsPath);
            var path = $"{MaterialsPath}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var create = material == null;

            if (create)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }

            material.color = Color.white;
            material.SetFloat("_Cull", 0);
            material.SetFloat("_Smoothness", 0.15f);
            material.doubleSidedGI = true;

            if (transparent)
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_Blend", 0);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.SetOverrideTag("RenderType", "Transparent");
            }

            if (create)
            {
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            AssetDatabase.SaveAssets();

            return material;
        }

        static Material MaterialAsset(string name, int rgb)
        {
            Directory.CreateDirectory(MaterialsPath);
            var path = $"{MaterialsPath}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material != null)
            {
                return material;
            }

            var pipeline = GraphicsSettings.currentRenderPipeline;
            var shader = pipeline != null && pipeline.defaultMaterial != null
                ? pipeline.defaultMaterial.shader
                : Shader.Find("Universal Render Pipeline/Lit");

            material = new Material(shader)
            {
                color = new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f),
            };

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.15f);
            }

            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            return material;
        }
    }
}
