// GU WORLD ATLAS — Unity editor importer. Place under Assets/GuAtlas/Editor/.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GuAtlas
{
    public class GuAtlasImporter : EditorWindow
    {
        string jsonPath = "";
        float scale = 0.001f;
        bool buildTerrain = true, placeholders = true, skipSky = false;

        [MenuItem("Tools/GU World Atlas/Import World")]
        static void Open() { GetWindow<GuAtlasImporter>("GU World Atlas"); }

        void OnGUI()
        {
            EditorGUILayout.LabelField("world.json из архива экспорта атласа", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            jsonPath = EditorGUILayout.TextField(jsonPath);
            if (GUILayout.Button("...", GUILayout.Width(32)))
            {
                var p = EditorUtility.OpenFilePanel("world.json", "", "json");
                if (!string.IsNullOrEmpty(p)) jsonPath = p;
            }
            EditorGUILayout.EndHorizontal();
            scale = EditorGUILayout.FloatField("Единиц Unity на метр", scale);
            buildTerrain = EditorGUILayout.Toggle("Создать Terrain", buildTerrain);
            placeholders = EditorGUILayout.Toggle("Заглушки без префаба", placeholders);
            skipSky = EditorGUILayout.Toggle("Пропустить небесные слои", skipSky);
            GUI.enabled = File.Exists(jsonPath);
            if (GUILayout.Button("Импортировать")) Import();
            GUI.enabled = true;
        }

        void Import()
        {
            var data = JsonUtility.FromJson<GaFile>(File.ReadAllText(jsonPath));
            if (data == null || data.format != "gu-world-atlas-unity") { Debug.LogError("Не файл экспорта GU World Atlas"); return; }
            var root = new GameObject("GU World - " + data.world.name);
            Undo.RegisterCreatedObjectUndo(root, "GU World import");
            if (buildTerrain && data.world.heightmap != null) BuildTerrain(data, root.transform);
            var byId = new Dictionary<string, GameObject>();
            var folders = new Dictionary<string, Transform>();
            foreach (var o in data.objects)
            {
                if (skipSky && o.layer != null && o.layer.EndsWith("_sky")) continue;
                string key = o.layer + "/" + o.region + "/" + o.category;
                if (!folders.TryGetValue(key, out var folder))
                {
                    folder = new GameObject(key).transform;
                    folder.SetParent(root.transform, false);
                    folders[key] = folder;
                }
                var prefab = Resources.Load<GameObject>(o.prefab);
                GameObject go;
                if (prefab != null) go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                else if (placeholders)
                {
                    go = GameObject.CreatePrimitive(o.category == "settlement" ? PrimitiveType.Cube : o.category == "special" ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
                    float s = Mathf.Max(o.radius * 2f * scale, 0.5f);
                    go.transform.localScale = new Vector3(s, Mathf.Max(s * 0.25f, 0.5f), s);
                }
                else go = new GameObject();
                go.name = o.name;
                go.transform.SetParent(folder, false);
                go.transform.position = new Vector3(o.position.x, o.position.y, o.position.z) * scale;
                go.transform.rotation = Quaternion.Euler(0f, o.rotationY, 0f);
                var c = go.AddComponent<GuAtlasObject>();
                c.atlasId = o.id; c.atlasType = o.type; c.category = o.category; c.region = o.region; c.layer = o.layer;
                c.loreRef = o.loreRef; c.rarity = o.rarity; c.canonStatus = o.canonStatus;
                c.danger = o.danger; c.rankMin = o.rankMin; c.rankMax = o.rankMax;
                if (o.properties != null) c.properties.AddRange(o.properties);
                if (o.path != null && o.path.Length > 1)
                {
                    var lr = go.AddComponent<LineRenderer>();
                    lr.positionCount = o.path.Length;
                    for (int i = 0; i < o.path.Length; i++) lr.SetPosition(i, new Vector3(o.path[i].x, o.path[i].y + 5f, o.path[i].z) * scale);
                    lr.widthMultiplier = Mathf.Max(o.width * scale, 0.2f);
                }
                byId[o.id] = go;
            }
            foreach (var o in data.objects)
                if (!string.IsNullOrEmpty(o.parent) && byId.TryGetValue(o.id, out var child) && byId.TryGetValue(o.parent, out var par))
                    child.transform.SetParent(par.transform, true);
            var rel = root.AddComponent<GuAtlasRelations>();
            if (data.relations != null) rel.relations.AddRange(data.relations);
            Selection.activeGameObject = root;
            Debug.Log("GU World Atlas: импортировано объектов " + byId.Count);
        }

        void BuildTerrain(GaFile data, Transform parent)
        {
            var hm = data.world.heightmap;
            var file = Path.Combine(Path.GetDirectoryName(jsonPath), hm.file);
            if (!File.Exists(file)) { Debug.LogWarning("Нет файла карты высот " + file); return; }
            var bytes = File.ReadAllBytes(file);
            int n = hm.resolution;
            var td = new TerrainData { heightmapResolution = n };
            float range = Mathf.Max(hm.maxHeight - hm.minHeight, 1f);
            float size = data.world.sizeMeters * scale;
            td.size = new Vector3(size, range * scale, size);
            var h = new float[n, n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = (y * n + x) * 2;
                    h[y, x] = (bytes[i] | (bytes[i + 1] << 8)) / 65535f;
                }
            td.SetHeights(0, 0, h);
            AssetDatabase.CreateAsset(td, AssetDatabase.GenerateUniqueAssetPath("Assets/GuWorldTerrain.asset"));
            var t = Terrain.CreateTerrainGameObject(td);
            t.name = "Terrain";
            t.transform.SetParent(parent, false);
            t.transform.position = new Vector3(-size * 0.5f, hm.minHeight * scale, -size * 0.5f);
            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sea.name = "Sea level";
            sea.transform.SetParent(parent, false);
            sea.transform.position = new Vector3(0f, data.world.seaLevel * scale, 0f);
            sea.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
        }
    }
}
#endif
