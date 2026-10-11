using GuWorld.World;
using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>
    /// What a location's sky holds besides the light (DaySky): stars and the
    /// moon at night, two layers of cloud drifting on the wind, mist lying in
    /// the valleys (thickest in the morning and in rain), rain around the
    /// player, and the village's lights — lanterns and windows glowing, a few
    /// lamps round the square — after dark.
    ///
    /// Its meshes are made when it wakes; each frame Apply colours and moves
    /// them by the time of day and the weather. The materials (GuWorld/Sky and
    /// the village's lit ones) are assets, given by the location's builder.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class Atmosphere : MonoBehaviour
    {
        public Material CloudMaterial;
        public Material MistMaterial;
        public Material StarMaterial;
        public Material MoonMaterial;
        public Material RainMaterial;
        /// <summary>The village's lit materials whose glow comes on at night (their emission is on in the asset).</summary>
        public Material LanternMaterial;
        public Material WindowMaterial;
        public Light[] NightLights = new Light[0];
        /// <summary>Where mist lies: centre (x, height, z) and size, m.</summary>
        public Vector4[] MistSpots = new Vector4[0];
        /// <summary>The cloud layers' heights, m.</summary>
        public float CloudHeight = 780;
        public float HighCloudHeight = 1250;

        Transform stars;
        Transform moon;
        Material[] clouds;
        Material mist;
        Material starGlow;
        Material moonGlow;
        ParticleSystem rain;
        Vector2 wind = new Vector2(0.004f, 0.0015f);
        Transform followed;
        Vector3 moonDirection = Vector3.up;

        void Awake()
        {
            if (StarMaterial != null)
            {
                starGlow = new Material(StarMaterial);
                stars = Part("Stars", StarDome(), starGlow).transform;
            }

            if (MoonMaterial != null)
            {
                moonGlow = new Material(MoonMaterial);
                moon = Part("Moon", Quad(1), moonGlow).transform;
            }

            if (CloudMaterial != null)
            {
                clouds = new[] { new Material(CloudMaterial), new Material(CloudMaterial) };
                clouds[1].mainTextureScale = CloudMaterial.mainTextureScale * 0.6f;
                Part("Clouds", CloudLayer(CloudHeight, 4200), clouds[0]);
                Part("High Clouds", CloudLayer(HighCloudHeight, 4600), clouds[1]);
            }

            if (MistMaterial != null && MistSpots.Length > 0)
            {
                mist = new Material(MistMaterial);

                foreach (var spot in MistSpots)
                {
                    var go = Part("Mist", MistPatch(spot.w), mist);
                    go.transform.position = new Vector3(spot.x, spot.y, spot.z);
                }
            }

            if (RainMaterial != null)
            {
                rain = Rain();
            }
        }

        GameObject Part(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go;
        }

        /// <summary>A dome of stars round the origin (moved with the camera): small quads, each its own brightness.</summary>
        static Mesh StarDome()
        {
            const int count = 1800;
            const float radius = 3000;
            var random = new System.Random(5);
            var vertices = new Vector3[count * 4];
            var uvs = new Vector2[count * 4];
            var colors = new Color[count * 4];
            var triangles = new int[count * 6];

            for (var i = 0; i < count; i++)
            {
                Vector3 direction;

                do
                {
                    direction = new Vector3((float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1);
                }
                while (direction.sqrMagnitude > 1 || direction.sqrMagnitude < 0.01f);

                direction.Normalize();
                direction.y = Mathf.Abs(direction.y) * 0.95f + 0.02f;
                direction.Normalize();
                var brightness = Mathf.Pow((float)random.NextDouble(), 3);
                var size = 5 + 11 * brightness;
                var right = Vector3.Cross(direction, Vector3.up).normalized * size;
                var up = Vector3.Cross(right, direction).normalized * size;
                var centre = direction * radius;
                var tint = Color.Lerp(new Color(0.75f, 0.85f, 1), new Color(1, 0.9f, 0.75f), (float)random.NextDouble());
                tint.a = 0.35f + 0.65f * brightness;

                vertices[i * 4] = centre - right - up;
                vertices[i * 4 + 1] = centre + right - up;
                vertices[i * 4 + 2] = centre + right + up;
                vertices[i * 4 + 3] = centre - right + up;
                uvs[i * 4] = new Vector2(0, 0);
                uvs[i * 4 + 1] = new Vector2(1, 0);
                uvs[i * 4 + 2] = new Vector2(1, 1);
                uvs[i * 4 + 3] = new Vector2(0, 1);

                for (var k = 0; k < 4; k++)
                {
                    colors[i * 4 + k] = tint;
                }

                triangles[i * 6] = i * 4;
                triangles[i * 6 + 1] = i * 4 + 1;
                triangles[i * 6 + 2] = i * 4 + 2;
                triangles[i * 6 + 3] = i * 4;
                triangles[i * 6 + 4] = i * 4 + 2;
                triangles[i * 6 + 5] = i * 4 + 3;
            }

            var mesh = new Mesh { name = "Stars", vertices = vertices, uv = uvs, colors = colors, triangles = triangles };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2.2f);

            return mesh;
        }

        static Mesh Quad(float size)
        {
            var h = size / 2;
            var mesh = new Mesh
            {
                name = "Quad",
                vertices = new[] { new Vector3(-h, -h, 0), new Vector3(h, -h, 0), new Vector3(h, h, 0), new Vector3(-h, h, 0) },
                uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>A wide disc of cloud, sinking towards its rim and fading out there, so it meets the horizon softly.</summary>
        static Mesh CloudLayer(float height, float radius)
        {
            const int rings = 18;
            const int segments = 56;
            var vertices = new Vector3[(rings + 1) * segments + 1];
            var uvs = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new System.Collections.Generic.List<int>();
            vertices[0] = new Vector3(0, height, 0);
            colors[0] = Color.white;

            for (var r = 0; r <= rings; r++)
            {
                var t = (r + 1f) / (rings + 1);
                var distance = radius * t;

                for (var s = 0; s < segments; s++)
                {
                    var a = 2 * Mathf.PI * s / segments;
                    var i = 1 + r * segments + s;
                    vertices[i] = new Vector3(Mathf.Cos(a) * distance, height - (height - 120) * t * t, Mathf.Sin(a) * distance);
                    uvs[i] = new Vector2(vertices[i].x, vertices[i].z) / 1500;
                    colors[i] = new Color(1, 1, 1, 1 - Mathf.Clamp01((t - 0.45f) / 0.5f));
                }
            }

            for (var s = 0; s < segments; s++)
            {
                triangles.AddRange(new[] { 0, 1 + (s + 1) % segments, 1 + s });
            }

            for (var r = 0; r < rings; r++)
            {
                for (var s = 0; s < segments; s++)
                {
                    var a = 1 + r * segments + s;
                    var b = 1 + r * segments + (s + 1) % segments;
                    var c = a + segments;
                    var d = b + segments;
                    triangles.AddRange(new[] { a, b, d, a, d, c });
                }
            }

            var mesh = new Mesh { name = "Cloud Layer", vertices = vertices, uv = uvs, colors = colors, triangles = triangles.ToArray() };
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>A flat patch of mist, its edges fading out all round.</summary>
        static Mesh MistPatch(float size)
        {
            const int cells = 8;
            var vertices = new Vector3[(cells + 1) * (cells + 1)];
            var uvs = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new System.Collections.Generic.List<int>();

            for (var j = 0; j <= cells; j++)
            {
                for (var i = 0; i <= cells; i++)
                {
                    var u = (float)i / cells - 0.5f;
                    var v = (float)j / cells - 0.5f;
                    var index = j * (cells + 1) + i;
                    vertices[index] = new Vector3(u * size, 0, v * size);
                    uvs[index] = new Vector2(u, v) * size / 340;
                    var edge = Mathf.Clamp01(1 - Mathf.Sqrt(u * u + v * v) * 2);
                    colors[index] = new Color(1, 1, 1, edge * edge * (3 - 2 * edge));

                    if (i < cells && j < cells)
                    {
                        triangles.AddRange(new[] { index, index + cells + 1, index + 1, index + 1, index + cells + 1, index + cells + 2 });
                    }
                }
            }

            var mesh = new Mesh { name = "Mist", vertices = vertices, uv = uvs, colors = colors, triangles = triangles.ToArray() };
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>Rain: streaks falling round the camera (the system follows it).</summary>
        ParticleSystem Rain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.duration = 1;
            main.startLifetime = 1.3f;
            main.startSpeed = 0;
            main.startSize = 0.035f;
            main.startColor = new Color(0.78f, 0.84f, 0.9f, 0.55f);
            main.maxParticles = 6000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(60, 1, 60);

            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-1.2f, -0.6f);
            velocity.y = new ParticleSystem.MinMaxCurve(-24, -20);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

            var emission = system.emission;
            emission.rateOverTime = 0;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.045f;
            renderer.lengthScale = 1;
            renderer.sharedMaterial = RainMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            system.Play();

            return system;
        }

        /// <summary>Keeps the stars, the moon and the rain round the camera (after it has moved this frame).</summary>
        void LateUpdate()
        {
            if (followed == null)
            {
                return;
            }

            var at = followed.position;

            if (stars != null)
            {
                stars.position = at;
            }

            if (moon != null)
            {
                moon.position = at + moonDirection * 2600;
                moon.rotation = Quaternion.LookRotation(moonDirection);
                moon.localScale = Vector3.one * 190;
            }

            if (rain != null)
            {
                rain.transform.position = at + Vector3.up * 18 + Vector3.ProjectOnPlane(followed.forward, Vector3.up).normalized * 10;
            }
        }

        /// <summary>Colours and places it all for this moment: the time of day (0…1), the weather and where the camera is.</summary>
        public void Apply(DaySky sky, Weather weather, float timeOfDay, Transform camera)
        {
            var light = sky.Current;
            var night = sky.Night;
            var low = 1 - Mathf.Clamp01(Mathf.Abs(light.sunDirection.y) / 0.3f);
            var clear = 1 - Mathf.Clamp01((weather.Cover - 0.35f) / 0.5f);
            var dt = Time.unscaledDeltaTime;
            followed = camera;
            moonDirection = sky.MoonDirection;

            if (stars != null)
            {
                starGlow.color = new Color(1, 1, 1, Mathf.Pow(night, 2) * clear);
            }

            if (moon != null)
            {
                var direction = sky.MoonDirection;
                var up = Mathf.Clamp01((direction.y + 0.05f) / 0.15f);
                moonGlow.color = new Color(1, 1, 1, Mathf.Lerp(0.15f, 1, night) * up * Mathf.Lerp(1, 0.25f, 1 - clear));
            }

            if (clouds != null)
            {
                // White by day, warm when the sun is low, dark blue at night, grey in rain.
                var day = Color.Lerp(new Color(0.97f, 0.97f, 0.98f), new Color(1, 0.78f, 0.62f), low * 0.85f);
                var tint = Color.Lerp(day, new Color(0.14f, 0.16f, 0.23f), night);
                tint = Color.Lerp(tint, tint.grayscale * new Color(0.78f, 0.8f, 0.84f), weather.Cover * 0.6f + weather.Rain * 0.3f);
                clouds[0].color = new Color(tint.r, tint.g, tint.b, 0.2f + 0.8f * weather.Cover);
                clouds[1].color = new Color(tint.r, tint.g, tint.b, 0.25f + 0.35f * weather.Cover);
                clouds[0].mainTextureOffset += wind * dt * 0.6f;
                clouds[1].mainTextureOffset += wind * dt;
            }

            if (mist != null)
            {
                // Thickest in the early morning, thin by day, back in the evening and in rain.
                var morning = Mathf.Clamp01(1 - Mathf.Abs(timeOfDay - 0.27f) / 0.1f);
                var evening = Mathf.Clamp01(1 - Mathf.Abs(timeOfDay - 0.8f) / 0.12f);
                var amount = Mathf.Clamp01(0.18f + 0.65f * morning + 0.25f * evening + 0.2f * night + 0.5f * weather.Rain);
                var haze = sky.HazeColour;
                var bright = Color.Lerp(haze, Color.white, 0.35f * (1 - night));
                mist.color = new Color(bright.r, bright.g, bright.b, amount * 0.85f);
                mist.mainTextureOffset += wind * dt * 0.3f;
            }

            if (rain != null)
            {
                var emission = rain.emission;
                emission.rateOverTime = 3600 * weather.Rain;
            }

            // The village's lights, coming on as the sun goes down.
            var lit = Mathf.Clamp01(night * 1.2f + weather.Rain * 0.25f * (1 - night));

            if (LanternMaterial != null)
            {
                // The Gu Yue's lanterns burn cold, like moonlight (the art direction's); the windows warm.
                LanternMaterial.SetColor("_EmissionColor", new Color(0.62f, 0.78f, 1) * 2.4f * lit);
            }

            if (WindowMaterial != null)
            {
                WindowMaterial.SetColor("_EmissionColor", new Color(1, 0.72f, 0.4f) * 1.25f * lit);
            }

            foreach (var lamp in NightLights)
            {
                if (lamp != null)
                {
                    lamp.intensity = 2.4f * lit;
                    lamp.enabled = lit > 0.02f;
                }
            }
        }
    }
}
