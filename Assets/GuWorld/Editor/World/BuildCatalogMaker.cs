using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuWorld.Render;
using GuWorld.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuWorld.Editor
{
    /// <summary>
    /// Makes the builder's catalog (Resources/BuildCatalog, read by WorldBuilder in the game): for every model in
    /// Models/Village (the game-ready copies, VillageModels.cs) and Models/Trees, a prefab under Resources/Build ready to
    /// stand in the world — near shape up close, far beyond, solid by the full shape (its doors open), a tree climbable
    /// (its trunk and limbs, from its .climb.json, as the artist's GuClimbTreeSetup does it) — with its footprint
    /// measured. Run before every player build (GuWorldBuild), or from the menu.
    /// </summary>
    public static class BuildCatalogMaker
    {
        const string Village = "Assets/GuWorld/Models/Village";
        const string Trees = "Assets/GuWorld/Models/Trees";
        const string Prefabs = "Assets/GuWorld/Resources/Build";
        const string CatalogPath = "Assets/GuWorld/Resources/BuildCatalog.asset";

        /// <summary>The builder's names and sections for the models (anything else goes under "Прочее" by its file name).</summary>
        static readonly Dictionary<string, (string label, string section)> Names = new Dictionary<string, (string, string)>
        {
            ["guyue_bamboo_house"] = ("Бамбуковый дом", "Дома"),
            ["prop_poor_house"] = ("Бедный дом", "Дома"),
            ["prop_shop"] = ("Лавка", "Дома"),
            ["prop_shop_small_a"] = ("Лавочка А", "Дома"),
            ["prop_shop_small_b"] = ("Лавочка Б", "Дома"),
            ["prop_shop_small_c"] = ("Лавочка В", "Дома"),
            ["prop_herbalist"] = ("Лавка травника", "Дома"),
            ["prop_smithy"] = ("Кузница", "Дома"),
            ["prop_granary"] = ("Амбар", "Дома"),
            ["prop_eatery"] = ("Харчевня", "Дома"),
            ["prop_gambling_tent"] = ("Игорный шатёр", "Дома"),
            ["prop_tavern"] = ("Таверна", "Дома"),
            ["prop_teahouse"] = ("Чайная", "Дома"),
            ["prop_inn"] = ("Постоялый двор", "Дома"),
            ["prop_gu_nursery"] = ("Питомник Гу", "Дома"),
            ["prop_clan_main_hall"] = ("Главный зал клана", "Клан"),
            ["prop_council_hall"] = ("Зал совета", "Клан"),
            ["prop_ancestral_hall"] = ("Зал предков", "Клан"),
            ["prop_academy_great"] = ("Академия", "Клан"),
            ["prop_academy"] = ("Малая академия", "Клан"),
            ["prop_dorm_great"] = ("Общежитие", "Клан"),
            ["prop_academy_dorm"] = ("Малое общежитие", "Клан"),
            ["prop_refectory"] = ("Трапезная", "Клан"),
            ["prop_gu_vault"] = ("Хранилище Гу", "Клан"),
            ["prop_tavern_great"] = ("Большая таверна", "Клан"),
            ["prop_teahouse_octagon"] = ("Восьмигранный чайный дом", "Клан"),
            ["prop_inn_courtyard"] = ("Гостиный двор", "Клан"),
            ["prop_market_row"] = ("Торговый ряд", "Клан"),
            ["guyue_pavilion_LOD0"] = ("Лунный павильон", "Клан"),
            ["fort_wall_segment"] = ("Стена крепости", "Крепость"),
            ["fort_battle_gate"] = ("Боевые ворота", "Крепость"),
            ["fort_watchtower"] = ("Сторожевая башня", "Крепость"),
            ["prop_bamboo_fence"] = ("Бамбуковый забор", "Крепость"),
            ["prop_stairs_grand"] = ("Большая лестница", "Лестницы и тропы"),
            ["prop_stairs_medium"] = ("Лестница", "Лестницы и тропы"),
            ["prop_steps_small"] = ("Ступени", "Лестницы и тропы"),
            ["prop_paths"] = ("Тропинка из камней", "Лестницы и тропы"),
            ["prop_paving_tiles"] = ("Мощение", "Лестницы и тропы"),
            ["prop_wooden_ladder"] = ("Приставная лестница", "Лестницы и тропы"),
            ["prop_village_well"] = ("Колодец", "Декор"),
            ["prop_moon_lantern_post"] = ("Фонарь", "Декор"),
            ["prop_clan_stele"] = ("Стела клана", "Декор"),
            ["prop_bronze_ding"] = ("Бронзовый дин", "Декор"),
            ["prop_moon_gate"] = ("Лунные ворота", "Декор"),
            ["prop_bench_stone"] = ("Каменная скамья", "Декор"),
            ["prop_bench_wood"] = ("Деревянная скамья", "Декор"),
            ["prop_lotus_vat"] = ("Чаша с лотосами", "Декор"),
            ["prop_bonsai_pine"] = ("Бонсай", "Декор"),
            ["prop_go_table"] = ("Стол для го", "Декор"),
            ["prop_weapon_rack"] = ("Стойка с оружием", "Декор"),
            ["prop_market_goods"] = ("Товары", "Декор"),
            ["prop_barrels"] = ("Бочки", "Декор"),
            ["prop_firewood_stack"] = ("Дрова", "Декор"),
            ["prop_drying_rack"] = ("Сушилка", "Декор"),
            ["prop_bamboo_grove"] = ("Бамбуковая роща", "Деревья"),
            ["tree_plum_8m"] = ("Слива (8 м)", "Деревья"),
            ["tree_deadwood_25m"] = ("Сухое дерево (25 м)", "Деревья"),
            ["tree_pine_40m"] = ("Сосна (40 м)", "Деревья"),
            ["tree_camphor_70m"] = ("Камфора (70 м)", "Деревья"),
            ["tree_ancient_100m"] = ("Древнее дерево (100 м)", "Деревья"),
            ["prop_ancestral_altar"] = ("Алтарь предков", "Внутри"),
            ["prop_canopy_bed"] = ("Кровать с балдахином", "Внутри"),
            ["prop_luohan_bed"] = ("Кушетка", "Внутри"),
            ["prop_curio_shelf"] = ("Полка с редкостями", "Внутри"),
            ["prop_folding_screen"] = ("Ширма", "Внутри"),
            ["prop_gu_jar_shelf"] = ("Полка с кувшинами Гу", "Внутри"),
            ["prop_medicine_cabinet"] = ("Аптечный шкаф", "Внутри"),
            ["prop_writing_desk"] = ("Письменный стол", "Внутри"),
            ["prop_moonlight_gu"] = ("Гу Лунного Света", "Гу"),
            ["prop_white_jade_gu"] = ("Гу Белого Нефрита", "Гу"),
            ["prop_liquor_worm"] = ("Винный Червь", "Гу"),
            ["prop_spring_autumn_cicada"] = ("Нефритовая цикада", "Гу"),
        };

        static readonly string[] SectionOrder = { "Дома", "Клан", "Крепость", "Лестницы и тропы", "Декор", "Деревья", "Внутри", "Гу", "Прочее" };
        static readonly HashSet<string> Stretched = new HashSet<string> { "prop_stairs_grand", "prop_stairs_medium", "prop_steps_small", "prop_wooden_ladder" };
        static readonly HashSet<string> Repeated = new HashSet<string> { "fort_wall_segment", "prop_bamboo_fence", "prop_paths", "prop_paving_tiles" };

        [MenuItem("Gu World/Make the Builder's Catalog", priority = 41)]
        public static void Make()
        {
            Directory.CreateDirectory(Prefabs);
            var entries = new List<BuildCatalog.Entry>();
            var models = new List<(string id, string path, string climb)>();

            foreach (var folder in Directory.GetDirectories(Village))
            {
                var id = Path.GetFileName(folder);
                var path = $"{Village}/{id}/{id}.gltf";

                if (File.Exists(path))
                {
                    models.Add((id, path, null));
                }
            }

            foreach (var file in Directory.GetFiles(Trees, "*.glb"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                var climb = $"{Trees}/{id}.climb.json";
                models.Add((id, $"{Trees}/{id}.glb", File.Exists(climb) ? climb : null));
            }

            foreach (var (id, path, climb) in models)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (source == null)
                {
                    Debug.LogWarning($"Gu World catalog: {path} is not imported");
                    continue;
                }

                var root = new GameObject(id);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.transform.SetParent(root.transform, false);
                var (label, section) = Names.TryGetValue(id, out var named) ? named : (id, "Прочее");

                if (climb != null)
                {
                    MakeClimbable(root, model, File.ReadAllText(climb));
                }
                else
                {
                    Dress(root, model, section == "Гу" || section == "Внутри" || section == "Декор");
                }

                // The footprint, from the near shape (in the model's own frame: the prefab stands unturned at the origin).
                var bounds = new Bounds();
                var first = true;

                foreach (var renderer in root.GetComponentsInChildren<Renderer>().Where(r => !r.name.StartsWith("Far")))
                {
                    if (first) bounds = renderer.bounds;
                    else bounds.Encapsulate(renderer.bounds);
                    first = false;
                }

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Prefabs}/{id}.prefab");
                Object.DestroyImmediate(root);
                entries.Add(new BuildCatalog.Entry
                {
                    Id = id, Label = label, Section = section, Prefab = prefab,
                    Size = new Vector2(bounds.size.x, bounds.size.z), Height = bounds.max.y, Middle = new Vector2(bounds.center.x, bounds.center.z),
                    Stretch = Stretched.Contains(id), Repeat = Repeated.Contains(id),
                });
            }

            var catalog = AssetDatabase.LoadAssetAtPath<BuildCatalog>(CatalogPath);

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BuildCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Entries = entries.OrderBy(e => System.Array.IndexOf(SectionOrder, e.Section)).ThenBy(e => e.Label).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            BuildLog.Write($"the builder's catalog: {entries.Count} models");
        }

        /// <summary>A building or prop: near shape up close, far beyond (no shadow), solid by its near shape (small things vanish sooner).</summary>
        static void Dress(GameObject root, GameObject model, bool small)
        {
            var near = new List<Renderer>();
            var far = new List<Renderer>();

            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                (renderer.name.StartsWith("Far") ? far : near).Add(renderer);
                renderer.shadowCastingMode = renderer.name.StartsWith("Far") ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }

            var group = root.AddComponent<LODGroup>();
            var (nearSize, farSize) = small ? (0.04f, 0.01f) : (0.1f, 0.006f);
            group.SetLODs(far.Count > 0 ? new[] { new LOD(nearSize, near.ToArray()), new LOD(farSize, far.ToArray()) } : new[] { new LOD(farSize, near.ToArray()) });
            group.RecalculateBounds();

            if (near.Count > 0 && near[0].GetComponent<MeshFilter>() is MeshFilter filter && filter.sharedMesh != null)
            {
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }

            root.AddComponent<HitboxTag>().Kind = small ? HitboxKind.Prop : HitboxKind.Building;
        }

        [System.Serializable] class Solid { public float[] center; public float[] size; public float pitch; }
        [System.Serializable] class Limb { public string node; public float Length; public float RootThickness; public float TipThickness; public Solid[] solids; }
        [System.Serializable] class Meta { public string name; public float Height; public float BaseRadius; public float TopRadius; public float ClimbHeight; public Limb[] branches; }

        /// <summary>
        /// A tree one climbs (as the artist's GuClimbTreeSetup does it): ClimbTree on the root, a TreeBranch with boxes
        /// to walk on on each Branch_NN, the trunk solid by its meshes, the far node beyond.
        /// </summary>
        static void MakeClimbable(GameObject root, GameObject model, string json)
        {
            var meta = JsonUtility.FromJson<Meta>(json);
            var tree = root.AddComponent<ClimbTree>();
            tree.BaseRadius = meta.BaseRadius;
            tree.TopRadius = meta.TopRadius;
            tree.ClimbHeight = meta.ClimbHeight;
            root.AddComponent<HitboxTag>().Kind = HitboxKind.Tree;

            if (Find(model.transform, "Trunk") is Transform trunk)
            {
                foreach (var filter in trunk.GetComponentsInChildren<MeshFilter>())
                {
                    if (!filter.name.ToLower().Contains("leaves") && filter.GetComponent<MeshCollider>() == null && filter.sharedMesh != null)
                    {
                        filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    }
                }
            }

            var branches = new List<TreeBranch>();

            foreach (var limb in meta.branches ?? new Limb[0])
            {
                if (!(Find(model.transform, limb.node) is Transform node))
                {
                    continue;
                }

                var branch = node.gameObject.AddComponent<TreeBranch>();
                branch.Length = limb.Length;
                branch.RootThickness = limb.RootThickness;
                branch.TipThickness = limb.TipThickness;
                branch.Health = 120 + limb.RootThickness * limb.RootThickness * 900;
                branch.Tree = tree;
                node.gameObject.AddComponent<HitboxTag>().Kind = HitboxKind.Branch;

                foreach (var s in limb.solids ?? new Solid[0])
                {
                    var solid = new GameObject("Solid");
                    solid.transform.SetParent(node, false);
                    // glTF is right-handed and glTFast mirrors X on import: the json's X is mirrored too (a pitch about X is not).
                    solid.transform.localPosition = new Vector3(-s.center[0], s.center[1], s.center[2]);
                    solid.transform.localRotation = Quaternion.Euler(s.pitch, 0, 0);
                    solid.AddComponent<BoxCollider>().size = new Vector3(s.size[0], s.size[1], s.size[2]);
                }

                branches.Add(branch);
            }

            tree.Branches = branches.ToArray();

            if (Find(model.transform, "Far") is Transform far)
            {
                var near = model.GetComponentsInChildren<Renderer>().Where(r => !r.transform.IsChildOf(far)).ToArray();
                var farRenderers = far.GetComponentsInChildren<Renderer>();

                foreach (var r in farRenderers)
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }

                var group = root.AddComponent<LODGroup>();
                group.SetLODs(new[] { new LOD(0.12f, near), new LOD(0.006f, farRenderers) });
                group.RecalculateBounds();
            }
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                if (Find(child, name) is Transform found)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
