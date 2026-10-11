using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace GuWorld.Editor
{
    /// <summary>
    /// What the mountain's meshes weigh, by kind (a name without its numbers): how many, their
    /// vertices and memory — to see what to make lighter. Writes to the build log.
    /// Unity -batchmode -projectPath . -executeMethod GuWorld.Editor.MeshReport.Run
    /// </summary>
    public static class MeshReport
    {
        public static void Run()
        {
            BuildLog.Begin("mesh report");
            var groups = new Dictionary<string, (int count, long vertices, long triangles, long bytes)>();
            var guids = AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/GuWorld/Locations/QingMao/Generated/Meshes" });
            var done = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

                if (mesh == null)
                {
                    continue;
                }

                var key = Regex.Replace(mesh.name, @"[-\d\s]+$", "");
                key = Regex.Replace(key, @"\s-?\d+(\s-?\d+)*", " #");
                groups.TryGetValue(key, out var g);
                long triangles = 0;

                for (var s = 0; s < mesh.subMeshCount; s++)
                {
                    triangles += mesh.GetIndexCount(s) / 3;
                }

                groups[key] = (g.count + 1, g.vertices + mesh.vertexCount, g.triangles + triangles, g.bytes + Profiler.GetRuntimeMemorySizeLong(mesh));
                Resources.UnloadAsset(mesh);

                if (++done % 300 == 0)
                {
                    EditorUtility.UnloadUnusedAssetsImmediate();
                }
            }

            // What each kind of thing lying about costs to draw, once.
            var kinds = "barrel,crates,sacks,broom,bucket,water jar,basket,goods basket,rubbish basket,rubbish heap,flower pots,stool,bench,firewood,hay,drying rack,laundry,cart,stall,well,old tree,tree,pine tree,leaves,stones,tavern table,tea table,dice table,book table,books,scrolls,wine jars,bottles,jars,gu pots,weapon rack,spears,anvil,charcoal,sign,wine flag,red lantern,pole lamp,lamp,stone lantern,brazier,banner,notice board,drum,hitching post".Split(',');
            var costs = new List<(string kind, int triangles)>();

            foreach (var kind in kinds)
            {
                var kit = new MeshKit();
                QingMaoBuilder.DrawPropForReport(kit, kind, 7);
                costs.Add((kind, kit.TriangleCount));
            }

            BuildLog.Write("props by triangles: " + string.Join(", ", costs.OrderByDescending(c => c.triangles).Select(c => $"{c.kind} {c.triangles}")));

            var total = groups.Values.Sum(g => g.bytes);
            BuildLog.Write($"meshes: {done}, {total / (1024 * 1024)} MB, {groups.Values.Sum(g => g.vertices):N0} vertices");

            foreach (var pair in groups.OrderByDescending(p => p.Value.bytes).Take(30))
            {
                BuildLog.Write($"  {pair.Key}: {pair.Value.count} meshes, {pair.Value.vertices:N0} vertices, {pair.Value.triangles:N0} triangles, {pair.Value.bytes / (1024 * 1024)} MB");
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
