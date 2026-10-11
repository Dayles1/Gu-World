using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GuWorld.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GuWorld.Editor
{
    /// <summary>
    /// The mountain built in stages, each on its own and saved when done, so that a build
    /// never holds the whole mountain in the making at once (16 GB machines) and one that
    /// stops can go on from where it stopped:
    ///
    /// 1. <c>terrain</c> — the sixteen tiles' ground, one at a time, each saved and let go;
    /// 2. <c>fine</c> — each walled village's own fine ground;
    /// 3. <c>village-…</c> — each village built and saved as a prefab (with its part of the world's plan);
    /// 4. <c>water</c>, <c>trails</c>, <c>hamlets</c>, <c>giants</c> — likewise, a prefab each
    ///    (the trails, hamlets and giants stand on the saved ground, loaded for them);
    /// 5. <c>assemble</c> — the scene: the ground, the prefabs, the sky, the game, the plan.
    ///
    /// Every stage begins in an empty scene and ends with what it made written out and
    /// unloaded. A stage that is done leaves a marker (Generated/Stages); a build run with no
    /// stages named runs the ones not done, so after a failure it simply goes on. Running a
    /// stage again un-marks the stages that used what it made. The log
    /// (Logs/QingMaoBuild/progress.log) has a line a step with the memory held.
    ///
    /// Command line (one Unity at a time; Tools/QingMao/build.ps1 runs it under a memory guard):
    /// <c>Unity -batchmode -quit -executeMethod GuWorld.Editor.QingMaoBuilder.BuildStages [-stages terrain,fine] [-test] [-area x,z,size]</c>.
    /// <c>-test</c> builds into GeneratedTest (not the game's); <c>-area</c> builds only what lies in a square.
    /// </summary>
    public static partial class QingMaoBuilder
    {
        /// <summary>The stages, in order.</summary>
        public static readonly string[] Stages = { "terrain", "fine", "village-gu_yue", "village-xiong", "village-bai", "water", "trails", "hamlets", "giants", "assemble" };

        /// <summary>What each stage uses that other stages make (run one again, and these are made again too).</summary>
        static readonly Dictionary<string, string[]> Uses = new Dictionary<string, string[]>
        {
            ["trails"] = new[] { "terrain", "fine" },
            ["hamlets"] = new[] { "terrain" },
            ["giants"] = new[] { "terrain", "fine" },
            ["assemble"] = Stages.Take(Stages.Length - 1).ToArray(),
        };

        static string[] except = new string[0];
        static bool testRun;
        static Rect? only;

        static string Generated => testRun ? Root + "/GeneratedTest" : Root + "/Generated";
        public static string ScenePath => Generated + "/QingMao.unity";
        static string StageFolder => Generated + "/Stages";
        static string PartPath(string name) => $"{Generated}/Parts/{name}.prefab";

        /// <summary>The villages' plans and the trails, worked out once a run (deterministic: the same in every run).</summary>
        static List<VillagePlan> plans;
        /// <summary>The ground's objects in the open scene (terrains and their skirts), not part of what a stage saves.</summary>
        static readonly List<GameObject> groundObjects = new List<GameObject>();

        [MenuItem("Gu World/Build Qing Mao Mountain", priority = 3)]
        public static void Build() => Run(null, false, null);

        [MenuItem("Gu World/Build Qing Mao Mountain (every stage again)", priority = 4)]
        public static void BuildAgain() => Run(Stages, false, null);

        /// <summary>The command line's way in: -stages a,b (else the unfinished ones), -test, -area x,z,size.</summary>
        public static void BuildStages()
        {
            var args = Environment.GetCommandLineArgs();

            string Arg(string name)
            {
                var i = Array.IndexOf(args, name);

                return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            }

            var list = Arg("-stages")?.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            // `-except a,b`: the stages not done, but not these (the guard runs the scene in a Unity of its own).
            except = Arg("-except")?.Split(',').Select(s => s.Trim()).ToArray() ?? new string[0];
            Rect? area = null;

            if (Arg("-area") is string text)
            {
                var v = text.Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                area = new Rect(v[0] - v[2] / 2, v[1] - v[2] / 2, v[2], v[2]);
            }

            var ok = Run(list, args.Contains("-test"), area);

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        static bool Run(string[] list, bool test, Rect? area)
        {
            var active = EditorSceneManager.GetActiveScene();

            if (!Application.isBatchMode && !(string.IsNullOrEmpty(active.path) && !active.isDirty) && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            testRun = test;
            only = area;
            plans = null;
            var unknown = list?.Except(Stages).ToArray() ?? new string[0];
            BuildLog.Begin($"Qing Mao: {(list == null ? "the stages not done" : string.Join(", ", list))}{(test ? ", test build" : "")}{(area is Rect r ? $", only {r}" : "")}");

            if (unknown.Length > 0)
            {
                BuildLog.Write($"unknown stages: {string.Join(", ", unknown)} (the stages are {string.Join(", ", Stages)})");

                return false;
            }

            Folders();
            BuildLog.Mark("start", true);

            // The editor's idle import workers (each a whole Unity, 1.4 GB) are stopped by the guard (build.ps1),
            // not by changing the user's settings here: a run stopped half-way would leave them changed.
            try
            {
                return RunAll(list);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static bool RunAll(string[] list)
        {

            foreach (var stage in Stages)
            {
                var asked = list != null && list.Contains(stage);

                if ((list != null && !asked) || except.Contains(stage))
                {
                    continue;
                }

                if (!asked && Done(stage))
                {
                    BuildLog.Write($"{stage}: done before, skipped");

                    continue;
                }

                var started = DateTime.Now;
                BuildLog.Mark($"{stage}: begins");

                try
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    groundObjects.Clear();
                    RunStage(stage, asked);
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    groundObjects.Clear();
                    // What a stage made for itself alone is let go (the plans and trails stay: the others need them).
                    built.Clear();
                    catalogue.Clear();
                    FreeMemory();
                }
                catch (Exception e)
                {
                    BuildLog.Error(stage, e);
                    Debug.LogException(e);

                    return false;
                }

                var seconds = (DateTime.Now - started).TotalSeconds;
                MarkDone(stage, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}, {seconds:0} s, peak {BuildLog.PeakMB} MB");
                BuildLog.Mark($"{stage}: done in {seconds:0} s", true);
            }

            BuildLog.Write($"finished; the most memory held at any step: {BuildLog.PeakMB} MB");

            return true;
        }

        /// <summary>Opens the project and does nothing more: a first run compiles the scripts and imports the assets, so the build after it needs less memory.</summary>
        public static void Warm()
        {
            BuildLog.Begin("warm-up: scripts compiled, assets imported");
            BuildLog.Mark("ready", true);

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        static void RunStage(string stage, bool force)
        {
            switch (stage)
            {
                case "terrain":
                    Prepare();
                    BuildTerrainStage(force);
                    break;
                case "fine":
                    Prepare();
                    BuildFineStage(force);
                    break;
                case "water":
                    Prepare();
                    BuildWater();
                    SavePart("Water");
                    break;
                case "trails":
                    Prepare();
                    LoadGround();
                    BuildTrails(at => QingMao.GroundAt(at));
                    SavePart("Trails");
                    break;
                case "hamlets":
                {
                    Prepare();
                    LoadGround();
                    var part = new PlanPart();

                    foreach (var place in QingMao.Settlements.Where(s => s.Clan == Clan.Mortal && InArea(s.Centre)))
                    {
                        BuildHamlet(place, (x, z) => QingMao.GroundAt(new Vector3(x, 0, z)), part.Buildings);
                        BuildLog.Mark($"hamlets: {place.Id}");
                    }

                    SavePart("Hamlets");
                    SavePlanPart(stage, part);
                    break;
                }
                case "giants":
                    Prepare();
                    LoadGround();
                    PlantGiants();
                    SavePart("Giant Trees");
                    break;
                case "assemble":
                    Assemble();
                    break;
                default:
                {
                    // A village: built on its plan, saved as a prefab with its part of the world's plan.
                    Prepare();
                    var id = stage.Substring("village-".Length);
                    var plan = plans.First(p => p.Place.Id == id);
                    var part = new PlanPart();

                    if (InArea(plan.Area))
                    {
                        BuildVillage(plan, part.Buildings, part.Streets, part.Marks);

                        if (plan.Place.Clan == Clan.GuYue)
                        {
                            BuildSecretCave(plan);
                        }

                        SavePart($"Village {id}");
                    }

                    SavePlanPart(stage, part);
                    break;
                }
            }
        }

        // ── What every stage needs ────────────────────────────────────

        static float Natural(float x, float z) => MountainShape.Natural(x, z);

        /// <summary>The streams, the villages' plans, the trails, the giants' spots: worked out once a run, the same every time.</summary>
        static void Prepare()
        {
            if (plans != null)
            {
                return;
            }

            materials.Clear();
            built.Clear();
            catalogue.Clear();
            trails.Clear();
            trailCells.Clear();
            bridged.Clear();
            nightLamps.Clear();
            giantSpots.Clear();
            trailHeights = null;
            layers = null;
            treePrototypes = null;
            detailPrototypes = null;
            terrainMaterial = null;
            fallMaterial = null;

            EditorUtility.DisplayProgressBar("Qing Mao", "The mountain's streams…", 0.02f);
            MountainShape.Prepare();
            BuildLog.Mark("prepare: streams");
            var made = new List<VillagePlan>();

            foreach (var place in QingMao.Settlements.Where(s => s.TileSize > 0))
            {
                EditorUtility.DisplayProgressBar("Qing Mao", $"Laying out {place.Name}…", 0.05f);
                var resolution = place.TileSize > 1000 ? 2049 : 1025;
                made.Add(new VillagePlan(place, Natural, resolution, Seed(place.Id)));
                BuildLog.Mark($"prepare: plan of {place.Id}");
            }

            EditorUtility.DisplayProgressBar("Qing Mao", "Finding the trails…", 0.08f);
            LayAllTrails(made, Natural);
            BuildLog.Mark($"prepare: {trails.Count} trails");
            ChooseGiantSpots(made);
            BuildLog.Mark($"prepare: {giantSpots.Count} giant trees' spots");
            plans = made;
        }

        /// <summary>A seed from a name, the same in every run and on every machine (unlike string.GetHashCode).</summary>
        public static int Seed(string text)
        {
            unchecked
            {
                var hash = (int)2166136261;

                foreach (var c in text ?? "")
                {
                    hash = (hash ^ c) * 16777619;
                }

                return hash & 0x7fffffff;
            }
        }

        static bool InArea(Vector2 p) => only is not Rect r || r.Contains(p);
        static bool InArea(Rect area) => only is not Rect r || r.Overlaps(area);

        // ── Stage outputs ─────────────────────────────────────────────

        static string Marker(string name) => $"{StageFolder}/{name}.done";

        static bool Done(string stage)
        {
            if (!File.Exists(Marker(stage)))
            {
                return false;
            }

            // A marker without what it marks (deleted by hand) is not done.
            return stage switch
            {
                "terrain" => BaseTileNames().All(n => File.Exists($"{Generated}/Terrain/{n.name}.asset")),
                "water" => File.Exists(PartPath("Water")),
                "trails" => File.Exists(PartPath("Trails")),
                "hamlets" => File.Exists(PartPath("Hamlets")),
                "giants" => File.Exists(PartPath("Giant Trees")),
                "assemble" => File.Exists(ScenePath),
                _ => true,
            };
        }

        static void MarkDone(string stage, string note)
        {
            Directory.CreateDirectory(StageFolder);
            File.WriteAllText(Marker(stage), note);

            // What used this stage's work is out of date now.
            foreach (var pair in Uses.Where(u => u.Value.Contains(stage)))
            {
                if (File.Exists(Marker(pair.Key)))
                {
                    File.Delete(Marker(pair.Key));
                    BuildLog.Write($"{pair.Key}: to be made again (it uses {stage})");
                }
            }
        }

        /// <summary>A world plan's part, made by a stage (the buildings, streets and named places of a village or the hamlets).</summary>
        [Serializable]
        class PlanPart
        {
            public List<WorldPlan.Footprint> Buildings = new List<WorldPlan.Footprint>();
            public List<WorldPlan.Line> Streets = new List<WorldPlan.Line>();
            public List<WorldPlan.Mark> Marks = new List<WorldPlan.Mark>();
        }

        static void SavePlanPart(string stage, PlanPart part)
        {
            Directory.CreateDirectory(StageFolder);
            File.WriteAllText($"{StageFolder}/{stage}.plan.json", JsonUtility.ToJson(part));
        }

        static PlanPart LoadPlanPart(string stage)
        {
            var path = $"{StageFolder}/{stage}.plan.json";

            return File.Exists(path) ? JsonUtility.FromJson<PlanPart>(File.ReadAllText(path)) : new PlanPart();
        }

        /// <summary>Everything the stage put in the scene (not the ground) saved as one prefab, then taken out of the scene.</summary>
        static void SavePart(string name)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var made = scene.GetRootGameObjects().Where(go => !groundObjects.Contains(go)).ToList();
            var root = new GameObject(name);

            foreach (var go in made)
            {
                go.transform.SetParent(root.transform, true);
            }

            Directory.CreateDirectory($"{Generated}/Parts");
            BuildLog.Mark($"{name}: saving the prefab ({root.GetComponentsInChildren<Transform>(true).Length} objects)");
            PrefabUtility.SaveAsPrefabAsset(root, PartPath(name), out var saved);

            if (!saved)
            {
                throw new Exception($"the prefab {PartPath(name)} was not saved");
            }

            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Saves a made object as an asset at `path`. Where one is there already it keeps it (and so its
        /// GUID, by which the other stages' prefabs and the scene know it) and only changes what is in it.
        /// </summary>
        static T Keep<T>(T made, string path) where T : Object
        {
            // A name may hold what a file's may not (a trail's "a: b"): those characters are swapped out.
            var folder = Path.GetDirectoryName(path)?.Replace(Path.DirectorySeparatorChar, '/');
            var file = Path.GetFileName(path);

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                file = file.Replace(c, '-');
            }

            path = $"{folder}/{file}";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);

            if (existing == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }

                AssetDatabase.CreateAsset(made, path);

                return made;
            }

            var name = existing.name;
            EditorUtility.CopySerialized(made, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(made);

            return existing;
        }

        // ── The ground ────────────────────────────────────────────────

        static IEnumerable<(string name, Vector2 corner)> BaseTileNames()
        {
            for (var tz = 0; tz < TileCount; tz++)
            {
                for (var tx = 0; tx < TileCount; tx++)
                {
                    yield return ($"Mountain {tx}-{tz}", new Vector2(-QingMao.Half + tx * TileSize, -QingMao.Half + tz * TileSize));
                }
            }
        }

        static void BuildTerrainStage(bool force)
        {
            var groundPlans = plans.Cast<IGroundPlan>().ToList();
            var index = 0;

            foreach (var (name, corner) in BaseTileNames())
            {
                index++;

                if (!InArea(new Rect(corner, Vector2.one * TileSize)))
                {
                    continue;
                }

                if (!force && File.Exists(Marker($"tile {name}")) && File.Exists($"{Generated}/Terrain/{name}.asset"))
                {
                    BuildLog.Write($"terrain: {name} done before, skipped");

                    continue;
                }

                EditorUtility.DisplayProgressBar("Qing Mao", $"The mountain: tile {index} of {TileCount * TileCount}…", (float)index / (TileCount * TileCount));
                BuildBaseTile(name, corner, groundPlans, Natural);
                FreeMemory();
                MarkDone($"tile {name}", DateTime.Now.ToString("s"));
                BuildLog.Mark($"terrain: {name}");
            }
        }

        static void BuildFineStage(bool force)
        {
            foreach (var plan in plans.Where(p => InArea(p.Area)))
            {
                var name = $"Village {plan.Place.Id}";

                if (!force && File.Exists(Marker($"tile {name}")) && File.Exists($"{Generated}/Terrain/{name}.asset"))
                {
                    BuildLog.Write($"fine: {name} done before, skipped");

                    continue;
                }

                BuildFineTile(name, plan, Natural);
                FreeMemory();
                MarkDone($"tile {name}", DateTime.Now.ToString("s"));
                BuildLog.Mark($"fine: {name}", true);
            }
        }

        /// <summary>The saved ground put in the open scene: the mountain's tiles (joined), the villages' own, their skirts.</summary>
        static void LoadGround()
        {
            groundObjects.Clear();
            var tiles = new Terrain[TileCount * TileCount];
            var index = 0;

            foreach (var (name, corner) in BaseTileNames())
            {
                var data = AssetDatabase.LoadAssetAtPath<TerrainData>($"{Generated}/Terrain/{name}.asset");

                if (data != null)
                {
                    tiles[index] = MakeTerrain(name, data, corner, 1);
                    groundObjects.Add(tiles[index].gameObject);
                }

                index++;
            }

            for (var tz = 0; tz < TileCount; tz++)
            {
                for (var tx = 0; tx < TileCount; tx++)
                {
                    Terrain At(int x, int z) => x >= 0 && z >= 0 && x < TileCount && z < TileCount ? tiles[z * TileCount + x] : null;
                    At(tx, tz)?.SetNeighbors(At(tx - 1, tz), At(tx, tz + 1), At(tx + 1, tz), At(tx, tz - 1));
                }
            }

            foreach (var plan in plans)
            {
                var name = $"Village {plan.Place.Id}";
                var data = AssetDatabase.LoadAssetAtPath<TerrainData>($"{Generated}/Terrain/{name}.asset");

                if (data == null)
                {
                    continue;
                }

                var terrain = MakeTerrain(name, data, plan.Area.position, 2);
                terrain.heightmapPixelError = 2;
                terrain.basemapDistance = 400;
                groundObjects.Add(terrain.gameObject);
                var skirt = AssetDatabase.LoadAssetAtPath<Mesh>($"{Generated}/Meshes/{name} Skirt.asset");

                if (skirt != null)
                {
                    var go = new GameObject($"{name} Skirt");
                    go.AddComponent<MeshFilter>().sharedMesh = skirt;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = Mat("Skirt Earth", 0x2a2a20, 0.02f, true);
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    groundObjects.Add(go);
                }
            }

            UnityEngine.Physics.SyncTransforms();
            BuildLog.Mark($"ground loaded ({groundObjects.Count} objects)", true);
        }

        // ── The scene ─────────────────────────────────────────────────

        static void Assemble()
        {
            Prepare();
            var scene = EditorSceneManager.GetActiveScene();
            LoadGround();

            foreach (var part in new[] { "Water", "Trails", "Village gu_yue", "Village xiong", "Village bai", "Hamlets", "Giant Trees" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PartPath(part));

                if (prefab == null)
                {
                    BuildLog.Write($"assemble: there is no {part} (its stage did not make one)");

                    continue;
                }

                PrefabUtility.InstantiatePrefab(prefab, scene);
                BuildLog.Mark($"assemble: {part}");
            }

            // Meshes only drawn (not any collider's shape) need no copy in memory in the game, besides the one on the
            // graphics card (on this machine the same memory): marked so. Colliders' meshes stay readable.
            var solid = new HashSet<Mesh>(Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(c => c.sharedMesh).Where(m => m != null));
            var drawnOnly = 0;

            foreach (var mesh in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(f => f.sharedMesh).Where(m => m != null).Distinct())
            {
                if (solid.Contains(mesh) || !AssetDatabase.Contains(mesh) || !mesh.isReadable)
                {
                    continue;
                }

                var serialized = new SerializedObject(mesh);
                serialized.FindProperty("m_IsReadable").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                drawnOnly++;
            }

            AssetDatabase.SaveAssets();
            BuildLog.Mark($"assemble: {drawnOnly} meshes marked drawn-only (not readable in the game), {solid.Count} kept for colliders");

            // The lamps lit at night are the villages' own.
            nightLamps.Clear();
            nightLamps.AddRange(Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.name == "Lamp"));

            BuildSurroundings();
            BuildAtmosphere();
            var start = plans.FirstOrDefault(p => p.Place.Clan == Clan.GuYue && InArea(p.Area)) ?? plans.FirstOrDefault(p => InArea(p.Area)) ?? plans[0];
            BuildGame(start);

            var whole = new PlanPart();

            foreach (var stage in Stages.Where(s => s.StartsWith("village-") || s == "hamlets"))
            {
                var part = LoadPlanPart(stage);
                whole.Buildings.AddRange(part.Buildings);
                whole.Streets.AddRange(part.Streets);
                whole.Marks.AddRange(part.Marks);
            }

            BuildPlan(whole.Buildings, whole.Streets, whole.Marks);
            BuildLog.Mark("assemble: the scene is put together", true);

            // The scene there before is kept beside it until this one is saved.
            var previous = Generated + "/QingMao (previous).unity";

            if (File.Exists(ScenePath))
            {
                AssetDatabase.DeleteAsset(previous);
                AssetDatabase.CopyAsset(ScenePath, previous);
            }

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new Exception($"the scene {ScenePath} was not saved");
            }

            AssetDatabase.SaveAssets();

            // First in the build: the game begins on the mountain (not a test build).
            if (!testRun)
            {
                var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
                scenes.AddRange(EditorBuildSettings.scenes.Where(s => s.path != ScenePath && File.Exists(s.path)));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            BuildLog.Write($"assemble: saved {ScenePath} ({trails.Count} trails)");
        }
    }
}
