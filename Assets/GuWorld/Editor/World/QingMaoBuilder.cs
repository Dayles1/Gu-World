using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuWorld.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GuWorld.Editor
{
    /// <summary>
    /// Builds Qing Mao Mountain — the first arc's world, 20 km across — as a scene and
    /// the assets it needs, deterministic, the same every time:
    ///
    /// - the mountain's ground (MountainShape): sixteen terrain tiles, painted and
    ///   planted, and each walled village's own fine terrain (MountainTerrain);
    /// - the streams, the deep pool under the back mountain's waterfall, the fall
    ///   itself, the Gu Yue's spring cave;
    /// - the trails between the villages and down to the hamlets, found over the
    ///   ground the way feet find them (MountainTrails), with steps and bridges;
    /// - the three clans' villages, each laid out on its terraces (VillagePlan) and
    ///   built (VillageBuild), and the mortal hamlets at the foot;
    /// - the land round the mountain to the horizon, the sky and its weather, the
    ///   camera, the sun and the game, and the plan the game reads (WorldPlan).
    ///
    /// What it makes goes into Locations/QingMao/Generated (not kept in git: build it
    /// again with this). It is built in stages (QingMaoStages): from the menu (Gu World →
    /// Build Qing Mao Mountain) or the command line, best through Tools/QingMao/build.ps1.
    /// </summary>
    public static partial class QingMaoBuilder
    {
        static readonly Dictionary<string, (Mesh, Material[])> built = new Dictionary<string, (Mesh, Material[])>();

        /// <summary>A mesh made once and kept as an asset.</summary>
        static (Mesh, Material[]) Make(string name, System.Action<MeshKit> build)
        {
            if (built.TryGetValue(name, out var done))
            {
                return done;
            }

            var kit = new MeshKit { Solid = new SolidShell() };
            build(kit);
            var result = Save(kit, name);
            built[name] = result;

            return result;
        }

        /// <summary>Places a built mesh (its own asset, shared by every copy), solid by its own shape unless told not to be.</summary>
        static GameObject Place(Transform parent, string name, (Mesh mesh, Material[] materials) shape, Vector3 at, float yaw, bool solid = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            go.AddComponent<MeshFilter>().sharedMesh = shape.mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = shape.materials;

            // Solid by its shell (what is big enough to bump into), where one was kept.
            if (solid && SolidOf(shape.mesh) is Mesh shell)
            {
                go.AddComponent<MeshCollider>().sharedMesh = shell;
            }

            return go;
        }

        // ── The trails ────────────────────────────────────────────────

        static void LayAllTrails(List<VillagePlan> plans, System.Func<float, float, float> natural)
        {
            TrailGround(natural);
            VillagePlan Of(Clan clan) => plans.First(p => p.Place.Clan == clan);
            Vector2 Gate(Clan clan, string name)
            {
                var plan = Of(clan);
                var gate = plan.Gates.FirstOrDefault(g => g.Name == name) ?? plan.Gates.OrderBy(g => g.Name).First();
                return gate.At + gate.Out * 8;
            }

            Vector2 Hamlet(string id) => QingMao.Find(id).Centre;

            var gy = "gu_yue";
            LayTrail("Тропа к передней горе", Gate(Clan.GuYue, "gate south"), Gate(Clan.Xiong, "gate west"), 1.15f, gy, "xiong");
            LayTrail("Дорога вниз, к броду", Gate(Clan.GuYue, "gate south"), Hamlet("hamlet_ford"), 1.2f, gy, "hamlet_ford");
            LayTrail("Тропа вдоль ручья", Gate(Clan.GuYue, "gate west"), Hamlet("hamlet_willow"), 1.0f, gy, "hamlet_willow");
            LayTrail("Тропа к роще", Gate(Clan.GuYue, "gate east"), QingMao.Grove + new Vector2(60, -40), 0.9f, gy);
            LayTrail("Тропа к вершине", Gate(Clan.GuYue, "gate north"), QingMao.Peak, 0.85f, gy);
            LayTrail("Тропа с вершины к водопаду", QingMao.Peak, Gate(Clan.Bai, "gate south"), 0.85f, "bai");
            LayTrail("Дорога Сюн к террасам", Gate(Clan.Xiong, "gate south"), Hamlet("hamlet_terraces"), 1.1f, "xiong", "hamlet_terraces");
            LayTrail("Дорога Бай на север", Gate(Clan.Bai, "gate north"), Hamlet("hamlet_north"), 1.1f, "bai", "hamlet_north");
            LayTrail("Западная дорога", Hamlet("hamlet_willow"), Hamlet("hamlet_west"), 1.0f, "hamlet_willow", "hamlet_west");
            LayTrail("Дорога в обход горы", Hamlet("hamlet_west"), Gate(Clan.Bai, "gate west"), 1.0f, "hamlet_west", "bai");
            LayTrail("Дорога у подножия", Hamlet("hamlet_ford"), Hamlet("hamlet_terraces"), 1.1f, "hamlet_ford", "hamlet_terraces");
            LayTrail("Тропа Сюн к вершине", Gate(Clan.Xiong, "gate north"), QingMao.Peak, 0.8f, "xiong");
            LayTrail("Медовая тропа", Gate(Clan.Xiong, "gate east"), Hamlet("hamlet_honey"), 0.9f, "xiong", "hamlet_honey");

            // The way round the foot, hamlet to hamlet.
            var ring = new[] { "hamlet_north", "hamlet_stone", "hamlet_honey", "hamlet_east", "hamlet_terraces", "hamlet_mill", "hamlet_ford", "hamlet_willow", "hamlet_pine", "hamlet_bamboo", "hamlet_west", "hamlet_mist", "hamlet_north" };

            for (var i = 0; i + 1 < ring.Length; i++)
            {
                var a = ring[i];
                var b = ring[i + 1];

                // Some stretches are laid above already.
                if ((a == "hamlet_ford" && b == "hamlet_willow") || (a == "hamlet_terraces" && b == "hamlet_mill"))
                {
                    continue;
                }

                LayTrail($"Дорога у подножия ({QingMao.Find(a).Name} — {QingMao.Find(b).Name})", Hamlet(a), Hamlet(b), 1.0f, a, b);
            }
        }

        // ── Water ─────────────────────────────────────────────────────

        static void BuildWater()
        {
            var root = new GameObject("Water").transform;
            var water = Water;

            // The surface over the bed — or over the ground, where the ground as built lies lower than the bed (a village's
            // tile cut it down): water that stood on its bed there hung in the air.
            var lowered = 0;
            var worst = (drop: 0f, at: Vector2.zero);

            float Surface(Vector2 at, float bedHeight)
            {
                // NaN where no terrain lies (GroundAt gives back the height asked with).
                var ground = QingMao.GroundAt(new Vector3(at.x, float.NaN, at.y));

                if (float.IsNaN(ground) || ground >= bedHeight)
                {
                    return bedHeight + 0.9f;
                }

                lowered++;

                if (bedHeight - ground > worst.drop)
                {
                    worst = (bedHeight - ground, at);
                }

                return ground + 0.6f;
            }

            foreach (var stream in MountainShape.Streams)
            {
                var kit = new MeshKit();
                var bed = stream.Bed;
                var piece = 0;

                for (var i = 0; i + 1 < bed.Count; i++)
                {
                    var a = bed[i];
                    var b = bed[i + 1];
                    var along = (b.at - a.at).normalized;
                    var across = new Vector2(-along.y, along.x);
                    var ha = Surface(a.at, a.bed);
                    var hb = Surface(b.at, b.bed);
                    var wa = a.half + 1.6f;
                    var wb = b.half + 1.6f;
                    kit.QuadUV(water,
                        new Vector3(a.at.x - across.x * wa, ha, a.at.y - across.y * wa), new Vector3(a.at.x + across.x * wa, ha, a.at.y + across.y * wa),
                        new Vector3(b.at.x + across.x * wb, hb, b.at.y + across.y * wb), new Vector3(b.at.x - across.x * wb, hb, b.at.y - across.y * wb),
                        new Vector2(0, i * 0.2f), new Vector2(1, i * 0.2f), new Vector2(1, (i + 1) * 0.2f), new Vector2(0, (i + 1) * 0.2f));

                    if ((i + 1) % 120 == 0 || i + 2 == bed.Count)
                    {
                        var (mesh, used) = Save(kit, $"Stream {stream.Name} {piece}");
                        var go = new GameObject($"{stream.Name} {piece++}");
                        go.transform.SetParent(root, false);
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = go.AddComponent<MeshRenderer>();
                        renderer.sharedMaterials = used;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        kit = new MeshKit();
                    }
                }
            }

            BuildLog.Write($"water: {lowered} points of the streams laid on the ground below their beds (the most, {worst.drop:0.0} m at {worst.at})");

            // The deep pool under the waterfall.
            var pool = new MeshKit();
            var centre = new Vector3(QingMao.Pool.x, MountainShape.PoolLevel, QingMao.Pool.y);

            for (var i = 0; i < 48; i++)
            {
                var a0 = 2 * Mathf.PI * i / 48;
                var a1 = 2 * Mathf.PI * (i + 1) / 48;
                pool.Triangle(water, centre, centre + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * (QingMao.PoolRadius + 4), centre + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * (QingMao.PoolRadius + 4), Vector3.up);
            }

            var poolGo = Place(root, "Pool", Save(pool, "Pool"), Vector3.zero, 0, false);
            poolGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // The fall: a sheet of water over the cliff's lip, bowed out, into the pool; spray where it lands.
            var upper = MountainShape.Streams.First(s => s.Name.Contains("верхняя"));
            var lip = upper.Bed[upper.Bed.Count - 1];
            var top = lip.bed + 1.2f;
            var bottom = MountainShape.PoolLevel;
            var fall = new MeshKit();
            var sheet = FallMaterial();
            var dir = (QingMao.Pool - lip.at).normalized;
            var side = new Vector2(-dir.y, dir.x);
            const int rows = 16;

            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < 6; c++)
                {
                    float X(int k) => -6 + k * 2;
                    Vector3 P(int row, int col)
                    {
                        var t = (float)row / rows;
                        var outward = Mathf.Sqrt(t) * 14;
                        var at = lip.at + dir * (4 + outward) + side * X(col) * (1 + t * 0.3f);
                        return new Vector3(at.x, Mathf.Lerp(top, bottom, t * t * 0.4f + t * 0.6f), at.y);
                    }

                    fall.QuadUV(sheet, P(r, c), P(r, c + 1), P(r + 1, c + 1), P(r + 1, c), new Vector2(c / 6f, r / 4f), new Vector2((c + 1) / 6f, r / 4f), new Vector2((c + 1) / 6f, (r + 1) / 4f), new Vector2(c / 6f, (r + 1) / 4f), new Vector3(dir.x, 0, dir.y));
                }
            }

            var fallGo = Place(root, "Waterfall", Save(fall, "Waterfall"), Vector3.zero, 0, false);
            fallGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var spray = Spray(new Vector3(QingMao.Pool.x - dir.x * 30, bottom + 1, QingMao.Pool.y - dir.y * 30) + new Vector3(dir.x, 0, dir.y) * 6);
            spray.transform.SetParent(root, true);
        }

        static Material fallMaterial;

        /// <summary>The waterfall's sheet: streaks of white water, see-through, scrolled down by the Atmosphere.</summary>
        static Material FallMaterial()
        {
            if (fallMaterial != null)
            {
                return fallMaterial;
            }

            var texture = Texture("Falling Water", 128, (u, v) =>
            {
                var streak = TileNoise(u * 24, v * 2, 24, 151);
                var foam = TileFbm(u, v, 4, 152);
                var a = Mathf.Clamp01(0.35f + streak * 0.5f + foam * 0.25f);
                var shade = 0.75f + 0.25f * streak;
                return new Color(shade, shade, shade * 1.03f, a);
            }, false, true);
            fallMaterial = SkyMat("Waterfall", texture, false, 3005, 1);
            fallMaterial.color = new Color(0.8f, 0.86f, 0.9f, 0.8f);

            return fallMaterial;
        }

        /// <summary>Spray rising where the fall lands: soft white puffs drifting up and away.</summary>
        static GameObject Spray(Vector3 at)
        {
            var go = new GameObject("Spray");
            go.transform.position = at;
            var system = go.AddComponent<ParticleSystem>();
            var main = system.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1, 3);
            main.startSize = new ParticleSystem.MinMaxCurve(4, 9);
            main.startColor = new Color(0.85f, 0.9f, 0.95f, 0.22f);
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission;
            emission.rateOverTime = 60;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(14, 2, 6);
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            // Every axis in the same mode (two constants), or Unity logs an error every frame.
            velocity.x = new ParticleSystem.MinMaxCurve(0, 0);
            velocity.y = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(0, 0);
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                alphaKeys = new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0, 1) },
            };
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SkyMat("Spray", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/Mist.png") ?? FallMaterial().mainTexture as Texture2D, false, 3010, 1);
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            return go;
        }

        /// <summary>
        /// The Flower Wine Monk's secret cave (lore ch. 14): a rock wall of piled boulders, a dark crevice in it, a thin
        /// fall of water down its face that wore the crevice, a rope of paper charms over the mouth — above the bamboo grove.
        /// </summary>
        static void BuildSecretCave(VillagePlan plan)
        {
            var at2 = QingMao.SecretCave;
            var ground = plan.GroundAt(at2);
            var face = plan.Down;
            var yaw = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg;
            var kit = new MeshKit();
            var random = new System.Random(4);

            // The rock face: big boulders piled behind and round the mouth.
            for (var i = 0; i < 14; i++)
            {
                var a = Mathf.Lerp(-1.4f, 1.4f, i / 13f);
                var r = 6 + (float)random.NextDouble() * 2;
                var p = new Vector3(Mathf.Sin(a) * r, 1.5f + (float)random.NextDouble() * 3, -Mathf.Cos(a) * r * 0.6f - 2);
                kit.Blob(Rubble, p, new Vector3(2.6f, 2.4f + (float)random.NextDouble() * 2, 2.2f), i);
            }

            kit.Blob(Rock2(), new Vector3(0, 5.5f, -4), new Vector3(7, 3.5f, 4), 31);
            // The mouth: dark.
            kit.RoundOpening(Rubble, Interior, new Vector3(0, -0.5f, -1.2f), 7, 6, 1.6f, 2.0f, 2.2f);
            kit.Box(Interior, new Vector3(0, 1.6f, -3), new Vector3(3.8f, 3.6f, 3.6f));
            // Moss over the stones, a rope with paper charms over the mouth.
            kit.Cylinder(Straw, new Vector3(-2.6f, 4.3f, -0.6f), new Vector3(2.6f, 4.3f, -0.6f), 0.08f, 0.08f, 6);

            for (var i = 0; i < 5; i++)
            {
                kit.Box(Mat("Paper Note", 0xb4ac96, 0.05f), new Vector3(-1.8f + i * 0.9f, 3.9f, -0.55f), new Vector3(0.18f, 0.6f, 0.01f));
            }

            var go = Place(null, "Secret Cave", Save(kit, "Secret Cave"), new Vector3(at2.x, ground, at2.y), yaw);
            go.AddComponent<GuWorld.Render.HitboxTag>().Kind = GuWorld.Render.HitboxKind.Building;

            // The trickle down the rock that wore the crevice: a narrow sheet of falling water beside the mouth.
            var trickle = new MeshKit();
            var sheet = FallMaterial();

            for (var r = 0; r < 6; r++)
            {
                var y0 = 8.5f - r * 1.45f;
                var y1 = y0 - 1.45f;
                trickle.QuadUV(sheet, new Vector3(2.2f, y0, -1.6f + r * 0.05f), new Vector3(3.0f, y0, -1.6f + r * 0.05f), new Vector3(3.0f, y1, -1.55f + r * 0.05f), new Vector3(2.2f, y1, -1.55f + r * 0.05f),
                    new Vector2(0, r / 3f), new Vector2(1, r / 3f), new Vector2(1, (r + 1) / 3f), new Vector2(0, (r + 1) / 3f), Vector3.forward);
            }

            var fall = Place(go.transform, "Trickle", Save(trickle, "Secret Cave Trickle"), new Vector3(at2.x, ground, at2.y), yaw, false);
            fall.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material Rock2() => Mat("Mossy Rock", 0x3e4436, 0.05f, false, RubbleTexture(), 0.25f);

        // ── The land round the mountain ───────────────────────────────

        /// <summary>
        /// The land round the mountain, out to 34 km: from the map's edge (where the plain is) hills roll
        /// up into ranges on the horizon. Steep faces rock, the rest dark woods; the haze does the rest.
        /// </summary>
        static float Surrounding(float x, float z)
        {
            var d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) - QingMao.Half;
            var n = Mathf.PerlinNoise(x * 0.00012f + 3, z * 0.00012f + 8);
            var ridge = 1 - Mathf.Abs(Mathf.PerlinNoise(x * 0.0002f + 17, z * 0.0002f + 2) * 2 - 1);
            var hills = 140 * Mathf.PerlinNoise(x * 0.0006f + 5, z * 0.0006f + 1) * SmoothStep(200, 3000, d);
            var mountains = SmoothStep(2500, 14000, d) * (500 + 1900 * n) * (0.45f + 0.55f * ridge);
            var plain = 38 + 14 * MountainShape.Fbm(x * 0.00045f, z * 0.00045f, 3);

            return plain + hills + mountains;
        }

        static void BuildSurroundings()
        {
            var grassTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/Forest.png");
            var rockTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Textures/Rock.png");
            var land = Mat("Far Land", 0xb8c0b0, 0.02f, false, grassTexture, 0.004f);
            var rock = Mat("Far Rock", 0xc8c4bc, 0.02f, false, rockTexture, 0.003f);
            const float extent = 34000;
            const float cell = 400;
            var cells = Mathf.RoundToInt(extent * 2 / cell);
            var row = cells + 1;
            var vertices = new Vector3[row * row];
            var uvs = new Vector2[vertices.Length];

            for (var j = 0; j <= cells; j++)
            {
                for (var i = 0; i <= cells; i++)
                {
                    var x = -extent + i * cell;
                    var z = -extent + j * cell;
                    // Inside the map it sinks out of sight under the terrains.
                    var inside = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) < QingMao.Half - 1;
                    vertices[j * row + i] = new Vector3(x, inside ? -40 : Surrounding(x, z), z);
                    uvs[j * row + i] = new Vector2(x, z);
                }
            }

            var landTriangles = new List<int>();
            var rockTriangles = new List<int>();

            for (var j = 0; j < cells; j++)
            {
                for (var i = 0; i < cells; i++)
                {
                    var x0 = -extent + i * cell;
                    var z0 = -extent + j * cell;

                    if (x0 >= -QingMao.Half && x0 + cell <= QingMao.Half && z0 >= -QingMao.Half && z0 + cell <= QingMao.Half)
                    {
                        continue;
                    }

                    var a = j * row + i;
                    var b = a + 1;
                    var c = a + row + 1;
                    var d = a + row;

                    foreach (var (p, q, r) in new[] { (a, d, c), (a, c, b) })
                    {
                        var normal = Vector3.Cross(vertices[q] - vertices[p], vertices[r] - vertices[p]).normalized;
                        (normal.y < 0.75f ? rockTriangles : landTriangles).AddRange(new[] { p, q, r });
                    }
                }
            }

            var mesh = new Mesh { name = "Surroundings", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(landTriangles, 0);
            mesh.SetTriangles(rockTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = Keep(mesh, $"{Generated}/Meshes/Surroundings.asset");
            var go = new GameObject("Surroundings");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { land, rock };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ── Sky, game, plan ───────────────────────────────────────────

        /// <summary>
        /// The sky and its weather: the procedural sky, the textures and materials of the
        /// clouds, mist, moon, stars and rain (GuWorld/Sky), the lamps lit at night, the
        /// post-processing — darker and colder than before, as the novel's mountain is —
        /// and the Atmosphere that runs them.
        /// </summary>
        static void BuildAtmosphere()
        {
            var skybox = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/Sky.mat");

            if (skybox == null)
            {
                skybox = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(skybox, $"{Root}/Materials/Sky.mat");
            }

            skybox.SetFloat("_SunDisk", 2);
            skybox.SetFloat("_SunSizeConvergence", 6);
            EditorUtility.SetDirty(skybox);
            RenderSettings.skybox = skybox;

            var cloud = Texture("Clouds", 512, (u, v) =>
            {
                var n = TileFbm(u, v, 4, 21);
                var puff = SmoothStep(0.42f, 0.7f, n);
                var shade = 0.78f + 0.2f * SmoothStep(0.5f, 0.85f, TileFbm(u + 0.01f, v + 0.02f, 4, 21));
                return new Color(shade, shade, shade * 1.02f, puff);
            }, false);
            var haze = Texture("Mist", 256, (u, v) =>
            {
                var n = TileFbm(u, v, 3, 31);
                return new Color(1, 1, 1, SmoothStep(0.25f, 0.8f, n));
            }, false);
            var moonDisc = Texture("Moon", 256, (u, v) =>
            {
                var d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
                var disc = 1 - SmoothStep(0.17f, 0.18f, d);
                var craters = 0.82f + 0.18f * TileFbm(u * 2, v * 2, 3, 41);
                var glow = Mathf.Pow(1 - Mathf.Clamp01(d / 0.5f), 3) * 0.35f;
                return new Color(0.92f, 0.94f, 1, Mathf.Clamp01(disc * craters + glow));
            }, false, false);
            var star = Texture("Star", 64, (u, v) =>
            {
                var d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2;
                return new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - d), 2.5f));
            }, false, false);
            var streak = Texture("Rain", 64, (u, v) =>
            {
                var across = 1 - Mathf.Abs(u - 0.5f) * 2;
                var along = Mathf.Sin(v * Mathf.PI);
                return new Color(1, 1, 1, across * across * along);
            }, false, false);

            var game = new GameObject("Atmosphere");
            var atmosphere = game.AddComponent<GuWorld.Render.Atmosphere>();
            atmosphere.CloudMaterial = SkyMat("Clouds", cloud, false, 3000, 1);
            atmosphere.MistMaterial = SkyMat("Mist", haze, false, 3010, 1);
            atmosphere.StarMaterial = SkyMat("Stars", star, true, 2950, 1);
            atmosphere.MoonMaterial = SkyMat("Moon", moonDisc, true, 2960, 1);
            atmosphere.RainMaterial = SkyMat("Rain", streak, false, 3100, 1);
            atmosphere.LanternMaterial = Lantern;
            atmosphere.WindowMaterial = Paper;
            atmosphere.WarmMaterials = new[] { WarmLantern, Mat("Red Lantern", 0xa83020, 0.3f) };
            atmosphere.FallMaterial = FallMaterial();
            atmosphere.CloudHeight = 2900;
            atmosphere.HighCloudHeight = 3800;
            atmosphere.CloudRadius = 26000;

            // Mist in the valleys: along the streams low down, round the mountain's foot, on the plain.
            var spots = new List<Vector4>();

            foreach (var stream in MountainShape.Streams)
            {
                for (var i = stream.Bed.Count / 3; i < stream.Bed.Count; i += 160)
                {
                    var at = stream.Bed[i].at;
                    spots.Add(new Vector4(at.x, stream.Bed[i].bed + 18, at.y, 700));
                }
            }

            for (var i = 0; i < 20; i++)
            {
                var angle = i * Mathf.PI * 2 / 20 + 0.2f;
                var distance = 8600 + 900 * Mathf.Sin(i * 1.7f);
                var x = Mathf.Cos(angle) * distance;
                var z = Mathf.Sin(angle) * distance;
                spots.Add(new Vector4(x, MountainShape.Natural(Mathf.Clamp(x, -9900, 9900), Mathf.Clamp(z, -9900, 9900)) + 30, z, 2200));
            }

            spots.Add(new Vector4(QingMao.Pool.x, MountainShape.PoolLevel + 12, QingMao.Pool.y, 260));
            atmosphere.MistSpots = spots.ToArray();
            atmosphere.NightLights = nightLamps.ToArray();

            // Post-processing: bloom (the lanterns glow), tone mapping, a heavier vignette, colour drained and
            // darkened — the shadows cold, the lights warm — for the grim mountain the novel draws.
            var profilePath = $"{Generated}/QingMao Post.asset";
            AssetDatabase.DeleteAsset(profilePath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.7f);
            bloom.scatter.Override(0.65f);
            var tone = profile.Add<Tonemapping>(true);
            // Neutral, not ACES: ACES pressed the shade into black (DaySky also sets it in the game).
            tone.mode.Override(TonemappingMode.Neutral);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.5f);
            // Darker and colder than plain, not black: the mood is the sky's and the light's (DaySky), the grade only leans it.
            var colour = profile.Add<ColorAdjustments>(true);
            colour.postExposure.Override(0.1f);
            colour.contrast.Override(10);
            colour.saturation.Override(-18);
            colour.colorFilter.Override(new Color(0.94f, 0.97f, 1f));

            foreach (var component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            EditorUtility.SetDirty(profile);
            var post = new GameObject("Post Processing");
            var volume = post.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        /// <summary>A GuWorld/Sky material: alpha-blended or added (glow), drawn in its place in the queue.</summary>
        static Material SkyMat(string name, Texture2D texture, bool additive, int queue, float tiling)
        {
            var path = $"{Root}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var create = material == null;

            if (create)
            {
                material = new Material(Shader.Find("GuWorld/Sky"));
            }

            material.mainTexture = texture;
            material.mainTextureScale = Vector2.one * tiling;
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.renderQueue = queue;
            material.enableInstancing = true;

            if (create)
            {
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        /// <summary>The camera, the sun and the game, set for this mountain: a new game begins on Gu Yue's square, facing down it.</summary>
        static void BuildGame(VillagePlan guYue)
        {
            var camera = new GameObject("Main Camera") { tag = "MainCamera" };
            camera.AddComponent<Camera>().farClipPlane = 9000;
            camera.AddComponent<AudioListener>();
            camera.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;

            var root = new GameObject("GU World");
            var game = root.AddComponent<GuWorldGame>();
            GuWorldSetup.Configure(game);
            var square = guYue.Square.Centre + guYue.Down * -6;
            var spawn = new Vector3(square.x, guYue.GroundAt(square) + 0.4f, square.y);
            var facing = Mathf.Atan2(guYue.Down.x, guYue.Down.y);
            game.Location = QingMao.Settings(spawn, facing);
            game.UseChunks = false;
            game.FogStart = 220;
            game.FogEnd = 6500;
            game.FarClip = 9000;
        }

        static void BuildPlan(List<WorldPlan.Footprint> footprints, List<WorldPlan.Line> streets, List<WorldPlan.Mark> marks)
        {
            var plan = new GameObject("World Plan").AddComponent<WorldPlan>();
            plan.Trails = trails.Select(t => new WorldPlan.Line { Name = t.Name, Points = t.Points.Select(p => new Vector3(p.x, 0, p.y)).ToArray(), Half = t.Half }).ToArray();
            plan.Streams = MountainShape.Streams.Select(s => new WorldPlan.Line { Name = s.Name, Points = s.Bed.Where((_, i) => i % 4 == 0).Select(b => new Vector3(b.at.x, b.bed, b.at.y)).ToArray(), Half = s.HalfTo }).ToArray();
            plan.Streets = streets.ToArray();
            plan.Buildings = footprints.ToArray();
            plan.Settlements = QingMao.Settlements;
            var all = new List<WorldPlan.Mark>(marks);

            foreach (var place in QingMao.Settlements)
            {
                all.Add(new WorldPlan.Mark { Label = place.Name, At = new Vector3(place.Centre.x, 0, place.Centre.y), Major = true });
            }

            all.Add(new WorldPlan.Mark { Label = "Вершина Цинмао", At = new Vector3(QingMao.Peak.x, 0, QingMao.Peak.y), Major = true });
            all.Add(new WorldPlan.Mark { Label = "Передняя гора", At = new Vector3(QingMao.ForePeak.x, 0, QingMao.ForePeak.y), Major = true });
            all.Add(new WorldPlan.Mark { Label = "Водопад и омут", At = new Vector3(QingMao.Pool.x, 0, QingMao.Pool.y), Major = true });
            all.Add(new WorldPlan.Mark { Label = "Исток ручья", At = new Vector3(QingMao.Spring.x, 0, QingMao.Spring.y) });
            all.Add(new WorldPlan.Mark { Label = "Скала с расщелиной", At = new Vector3(QingMao.SecretCave.x, 0, QingMao.SecretCave.y) });
            all.Add(new WorldPlan.Mark { Label = "Роща копейного бамбука", At = new Vector3(QingMao.Grove.x, 0, QingMao.Grove.y) });
            plan.Marks = all.ToArray();
        }
    }
}
