using System;
using System.Collections.Generic;
using System.Linq;
using GuWorld.Render;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuWorld.Editor
{
    /// <summary>One of the artist's buildings: its footprint across (X) and deep (Z), its height, and where the footprint's middle lies from the model's origin (its own X, Z; its front faces +Z).</summary>
    public readonly struct GlbModel
    {
        public readonly Vector2 Size;
        public readonly float Height;
        public readonly Vector2 Middle;

        public GlbModel(float width, float depth, float height, float middleX, float middleZ)
        {
            Size = new Vector2(width, depth);
            Height = height;
            Middle = new Vector2(middleX, middleZ);
        }
    }

    /// <summary>
    /// The village built from the artist's GLB buildings (GLB/, 2026-10-23). The originals are far too heavy for the
    /// game (a family house 124k triangles, the clan's hall 566k, 20 Mpx of textures each), so the copies in
    /// Models/Village/<name>/ are made by a Blender script: every part joined into one mesh "Near" cut to a budget, a
    /// light copy "Far" a fifth of that in one material (its colours in the vertices: one draw a house far off), only
    /// the colour textures, at most 512 px, as files beside the .gltf (Unity compresses them). Sizes below are the
    /// originals' (the copies keep them).
    /// </summary>
    public static partial class QingMaoBuilder
    {
        const string VillageModelsPath = "Assets/GuWorld/Models/Village";

        /// <summary>The models by name, measured in Blender: footprint, height, footprint's middle from the origin.</summary>
        internal static readonly Dictionary<string, GlbModel> VillageGlb = new Dictionary<string, GlbModel>
        {
            ["guyue_bamboo_house"] = new GlbModel(13.34f, 10.55f, 14.39f, -0.81f, 0.17f),
            ["prop_poor_house"] = new GlbModel(7.70f, 7.76f, 4.80f, -0.05f, 0.52f),
            ["prop_shop"] = new GlbModel(7.63f, 9.86f, 7.88f, 0.00f, 0.58f),
            ["prop_shop_small_a"] = new GlbModel(5.43f, 6.26f, 5.36f, 0.01f, -0.00f),
            ["prop_shop_small_b"] = new GlbModel(5.43f, 6.76f, 5.34f, 0.01f, 0.25f),
            ["prop_shop_small_c"] = new GlbModel(5.26f, 5.84f, 4.58f, 0.08f, -0.00f),
            ["prop_herbalist"] = new GlbModel(8.63f, 10.22f, 6.63f, 0.00f, 0.96f),
            ["prop_smithy"] = new GlbModel(10.85f, 9.24f, 7.69f, 0.00f, -0.00f),
            ["prop_granary"] = new GlbModel(10.64f, 9.98f, 7.73f, -0.00f, 0.74f),
            ["prop_eatery"] = new GlbModel(13.63f, 11.10f, 8.69f, 0.00f, -0.00f),
            ["prop_gambling_tent"] = new GlbModel(14.08f, 13.90f, 7.35f, -0.00f, -0.09f),
            ["prop_tavern"] = new GlbModel(12.24f, 12.41f, 9.55f, -0.00f, 0.45f),
            ["prop_teahouse"] = new GlbModel(11.31f, 12.43f, 11.10f, 0.00f, 0.57f),
            ["prop_teahouse_octagon"] = new GlbModel(19.77f, 21.92f, 18.90f, 0.01f, 1.08f),
            ["prop_inn"] = new GlbModel(16.54f, 11.72f, 9.90f, -1.15f, -0.00f),
            ["prop_inn_courtyard"] = new GlbModel(37.41f, 35.98f, 16.59f, -0.00f, -0.82f),
            ["prop_council_hall"] = new GlbModel(15.90f, 12.05f, 9.39f, -0.00f, 0.47f),
            ["prop_academy"] = new GlbModel(19.90f, 18.57f, 9.43f, -0.00f, 3.35f),
            ["prop_academy_dorm"] = new GlbModel(17.62f, 9.80f, 9.15f, -0.09f, -0.00f),
            ["prop_academy_great"] = new GlbModel(57.61f, 66.90f, 22.03f, -0.00f, -2.35f),
            ["prop_ancestral_hall"] = new GlbModel(13.64f, 13.68f, 12.47f, -0.00f, 1.13f),
            ["prop_gu_nursery"] = new GlbModel(14.04f, 11.35f, 8.08f, -0.00f, 0.17f),
            ["prop_clan_main_hall"] = new GlbModel(83.02f, 42.65f, 27.96f, -0.00f, 6.18f),
            ["prop_dorm_great"] = new GlbModel(68.62f, 17.61f, 21.27f, -0.00f, 1.00f),
            ["prop_refectory"] = new GlbModel(42.23f, 30.17f, 15.90f, 0.01f, -5.65f),
            ["prop_market_row"] = new GlbModel(54.63f, 12.61f, 13.73f, 0.00f, -0.00f),
            ["prop_tavern_great"] = new GlbModel(44.20f, 42.91f, 22.69f, -0.00f, -9.95f),
            // The vault is a stair down into the rock: 3 m of gate above the ground, 38 m of halls under it, 117 m deep
            // behind the gate. Its "footprint" here is the gate's alone (its middle the gate's), the halls lie buried.
            ["prop_gu_vault"] = new GlbModel(33.12f, 6.3f, 41.84f, -1.25f, 0f),
            ["fort_wall_segment"] = new GlbModel(7.24f, 8.79f, 10.76f, -0.00f, 0.85f),
            ["fort_battle_gate"] = new GlbModel(15.10f, 14.49f, 18.54f, -0.40f, -1.25f),
            ["fort_watchtower"] = new GlbModel(9.32f, 9.31f, 18.19f, -0.00f, 1.09f),
            // Stairs: their foot at the front (+Z), climbing to the back; the height is the rise.
            ["prop_stairs_grand"] = new GlbModel(8.08f, 23.32f, 9.02f, 0f, -1.46f),
            ["prop_stairs_medium"] = new GlbModel(5.38f, 7.77f, 4.33f, 0f, -0.09f),
            ["prop_steps_small"] = new GlbModel(2.5f, 2.94f, 0.74f, 0f, 0.15f),
            // Props.
            ["prop_moon_lantern_post"] = new GlbModel(2.39f, 1.35f, 5.01f, -0.01f, 0.01f),
            ["prop_village_well"] = new GlbModel(3.67f, 2.80f, 3.61f, 0.01f, -0.00f),
            ["prop_market_goods"] = new GlbModel(3.73f, 1.96f, 1.35f, 0.09f, 0.05f),
            ["prop_clan_stele"] = new GlbModel(3.80f, 5.32f, 5.89f, -0.00f, 0.46f),
            ["prop_bronze_ding"] = new GlbModel(3.10f, 3.10f, 3.04f, -0.00f, -0.00f),
            ["prop_bench_stone"] = new GlbModel(3.41f, 1.31f, 0.62f, -0.76f, 0.19f),
            ["prop_bench_wood"] = new GlbModel(1.90f, 0.72f, 1.01f, -0.00f, -0.00f),
            ["prop_lotus_vat"] = new GlbModel(1.44f, 1.44f, 1.41f, -0.00f, -0.00f),
            ["prop_bonsai_pine"] = new GlbModel(1.05f, 0.53f, 1.37f, -0.01f, -0.01f),
            ["prop_weapon_rack"] = new GlbModel(2.65f, 0.75f, 2.74f, 0.01f, 0.01f),
            ["prop_go_table"] = new GlbModel(1.91f, 1.91f, 0.89f, -0.01f, -0.01f),
            ["prop_firewood_stack"] = new GlbModel(4.03f, 2.06f, 1.62f, -0.52f, 0.52f),
            ["prop_drying_rack"] = new GlbModel(3.45f, 1.62f, 2.25f, -0.28f, 0.39f),
            ["prop_barrels"] = new GlbModel(4.89f, 5.12f, 1.43f, 0.02f, -0.29f),
            ["prop_bamboo_grove"] = new GlbModel(5.95f, 6.24f, 9.23f, 0.07f, -0.00f),
            ["prop_bamboo_fence"] = new GlbModel(7.25f, 0.75f, 2.00f, -1.29f, 0.01f),
            ["prop_moon_gate"] = new GlbModel(7.79f, 1.95f, 4.74f, 0.00f, 0.01f),
            ["prop_wooden_ladder"] = new GlbModel(0.59f, 0.95f, 3.32f, -0.01f, -0.39f),
        };

        /// <summary>Whether a village model's game-ready copy is there (else the procedural building stands in).</summary>
        static bool HasVillageModel(string name) =>
            name != null && VillageGlb.ContainsKey(name) && AssetDatabase.LoadAssetAtPath<GameObject>(VillageModelPath(name)) != null;

        /// <summary>A model's file: glTF with its images beside it, so Unity imports (and compresses) them as its own textures.</summary>
        static string VillageModelPath(string name) => $"{VillageModelsPath}/{name}/{name}.gltf";

        /// <summary>
        /// A village model on its lot: its footprint's middle on `centre`, turned to `yaw` (its front along the turn's +Z),
        /// its origin at `floor`, scaled evenly. Near up close, Far beyond (no shadow), nothing past that (`nearSize`, `farSize`: the screen's share where each ends); solid by Near's
        /// shape (light enough to collide with). Returns the holder, or null when the model is missing.
        /// </summary>
        static GameObject PlaceVillageModel(Transform parent, string name, string label, Vector2 centre, float yaw, float floor, float scale, bool solid = true, float nearSize = 0.1f, float farSize = 0.006f)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(VillageModelPath(name));

            if (asset == null || !VillageGlb.TryGetValue(name, out var model))
            {
                return null;
            }

            var turn = Quaternion.Euler(0, yaw, 0);
            var middle = turn * new Vector3(model.Middle.x, 0, model.Middle.y) * scale;
            var holder = new GameObject(label);
            holder.transform.SetParent(parent, false);
            holder.transform.SetPositionAndRotation(new Vector3(centre.x - middle.x, floor, centre.y - middle.z), turn);
            holder.transform.localScale = Vector3.one * scale;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder.scene);
            instance.transform.SetParent(holder.transform, false);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var near = new List<Renderer>();
            var far = new List<Renderer>();
            MeshFilter farFilter = null;

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                if (renderer.name.StartsWith("Far"))
                {
                    far.Add(renderer);
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    farFilter = farFilter != null ? farFilter : renderer.GetComponent<MeshFilter>();
                }
                else
                {
                    near.Add(renderer);
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }
            }

            var group = holder.AddComponent<LODGroup>();
            group.SetLODs(far.Count > 0
                ? new[] { new LOD(nearSize, near.ToArray()), new LOD(farSize, far.ToArray()) }
                : new[] { new LOD(farSize, near.ToArray()) });
            group.RecalculateBounds();

            // Solid by the full model (its doors open, its rooms walkable: every building has its inside); a mesh is
            // cooked for collisions once however many houses share it.
            if (solid)
            {
                var shape = near.Count > 0 ? near[0].GetComponent<MeshFilter>() : farFilter;

                if (shape != null && shape.sharedMesh != null)
                {
                    shape.gameObject.AddComponent<MeshCollider>().sharedMesh = shape.sharedMesh;
                }
            }

            holder.AddComponent<HitboxTag>().Kind = HitboxKind.Building;

            return holder;
        }

        /// <summary>The lowest and highest ground under a turned rectangle (`along` and `across`: its half sizes), sampled about every metre.</summary>
        static (float low, float high) GroundUnder(Vector2 centre, Vector2 tangent, Vector2 outward, float along, float across, Func<Vector2, float> ground)
        {
            var low = float.PositiveInfinity;
            var high = float.NegativeInfinity;

            for (var a = -along; a <= along + 0.01f; a += Mathf.Max(0.5f, along / 4))
            {
                for (var c = -across; c <= across + 0.01f; c += Mathf.Max(0.5f, across / 2))
                {
                    var g = ground(centre + tangent * a + outward * c);
                    low = Mathf.Min(low, g);
                    high = Mathf.Max(high, g);
                }
            }

            return (low, high);
        }

        /// <summary>A stone footing under a piece of the fort set on the highest ground under it: from below the lowest up to its foot.</summary>
        static void Footing(Blocks stones, Vector2 centre, float yaw, Vector3 size, Vector2 shift, float low, float high)
        {
            if (high - low < 0.25f)
            {
                return;
            }

            var at = centre + shift;
            stones.At(new Vector3(at.x, high, at.y), yaw).Box(Stone, new Vector3(0, -(high - low + 0.6f) / 2 + 0.05f, 0), new Vector3(size.x, high - low + 0.6f, size.z));
        }

        /// <summary>A model placed by its own origin (not its footprint's middle) at a point: the walls' lengths stand on their line.</summary>
        static GameObject PlaceVillageModelAt(Transform parent, string name, string label, Vector2 origin, float yaw, float floor, float scale)
        {
            var middle = Quaternion.Euler(0, yaw, 0) * new Vector3(VillageGlb[name].Middle.x, 0, VillageGlb[name].Middle.y) * scale;

            return PlaceVillageModel(parent, name, label, origin + new Vector2(middle.x, middle.z), yaw, floor, scale);
        }

        /// <summary>
        /// The wall round a village from the artist's fort models: even lengths of wall along its boundary (a little
        /// overlapping, so no light shows at a bend), their stakes outward, each on the lowest ground under it (its
        /// foot sinks into the higher); a battle gate (its doors left out, the way through open) at each gate; a
        /// watchtower astride the wall every ~70 m. False when a model is missing: the procedural wall is built instead.
        /// </summary>
        static bool BuildFortWall(Transform walls, VillagePlan plan, Func<Vector2, float> ground, Blocks stones)
        {
            if (!HasVillageModel("fort_wall_segment") || !HasVillageModel("fort_battle_gate") || !HasVillageModel("fort_watchtower"))
            {
                return false;
            }

            const float wallScale = 0.75f;
            const float gateScale = 0.8f;
            var points = plan.Boundary;
            var total = 0f;
            var along = new List<float>();

            for (var i = 0; i < points.Count; i++)
            {
                along.Add(total);
                total += Vector2.Distance(points[i], points[(i + 1) % points.Count]);
            }

            Vector2 At(float d, out Vector2 tangent)
            {
                d = Mathf.Repeat(d, total);

                for (var i = 0; i < points.Count; i++)
                {
                    var end = i + 1 < points.Count ? along[i + 1] : total;

                    if (d <= end)
                    {
                        var next = points[(i + 1) % points.Count];
                        tangent = (next - points[i]).normalized;

                        return Vector2.Lerp(points[i], next, (d - along[i]) / Mathf.Max(0.001f, end - along[i]));
                    }
                }

                tangent = Vector2.right;

                return points[0];
            }

            var length = VillageGlb["fort_wall_segment"].Size.x * wallScale;
            var count = Mathf.CeilToInt(total / (length * 0.96f));
            var step = total / count;
            var gateRoom = VillageGlb["fort_battle_gate"].Size.x * gateScale / 2 + step / 2 - 0.4f;
            var sinceTower = 40f;
            var built = 0;

            for (var i = 0; i < count; i++)
            {
                var p = At((i + 0.5f) * step, out var tangent);
                var outward = new Vector2(tangent.y, -tangent.x);

                if (plan.InsideDistance(p + outward) > plan.InsideDistance(p))
                {
                    outward = -outward;
                }

                if (plan.Gates.Any(g => Vector2.Distance(g.At, p) < gateRoom))
                {
                    continue;
                }

                var yaw = Mathf.Atan2(outward.x, outward.y) * Mathf.Rad2Deg;
                var (low, high) = GroundUnder(p, tangent, outward, step / 2, 2.4f, ground);
                PlaceVillageModelAt(walls, "fort_wall_segment", "Wall", p, yaw, high, wallScale);
                Footing(stones, p, yaw, new Vector3(step + 0.15f, 0, 4.2f), -outward * 0.9f, low, high);
                built++;
                sinceTower += step;

                if (sinceTower > 70)
                {
                    var (towerLow, towerHigh) = GroundUnder(p, tangent, outward, 3.6f, 3.6f, ground);
                    PlaceVillageModelAt(walls, "fort_watchtower", "Tower", p, yaw, towerHigh, wallScale);
                    Footing(stones, p, yaw, new Vector3(7.2f, 0, 7.2f), Vector2.zero, towerLow, towerHigh);
                    sinceTower = 0;
                }
            }

            foreach (var gate in plan.Gates)
            {
                var yaw = Mathf.Atan2(gate.Out.x, gate.Out.y) * Mathf.Rad2Deg;
                var across = new Vector2(gate.Out.y, -gate.Out.x);
                var (low, high) = GroundUnder(gate.At, across, gate.Out, 5.5f, 5.5f, ground);
                PlaceVillageModelAt(walls, "fort_battle_gate", gate.Name, gate.At, yaw, high, gateScale);
                Footing(stones, gate.At, yaw, new Vector3(11.6f, 0, 11.2f), Vector2.zero, low, high);
            }

            BuildLog.Write($"wall of {plan.Place.Id}: {built} lengths of the fort's wall ({step:0.0} m each), {plan.Gates.Count} gates");

            return true;
        }
    }
}
