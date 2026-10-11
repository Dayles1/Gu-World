using GuWorld.UI;
using GuWorld.World;
using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>
    /// The small life in the air round the camera: swarms of gnats hanging over
    /// the grass, butterflies wandering by day, dragonflies darting by the water,
    /// and at night fireflies blinking in the dark — none in the rain, fewer high
    /// on the mountain, as many as the graphics level allows. Particles that follow
    /// the camera and drift on their own; their textures are painted here.
    /// </summary>
    public class Insects : MonoBehaviour
    {
        ParticleSystem gnats;
        ParticleSystem butterflies;
        ParticleSystem fireflies;
        ParticleSystem dragonflies;
        Transform followed;
        Material firefly;

        public static Insects Create()
        {
            var insects = new GameObject("Insects").AddComponent<Insects>();
            insects.Build();

            return insects;
        }

        static Texture2D Dot(int size, float sharpness)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f) / size, new Vector2(0.5f, 0.5f)) * 2;
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - d), sharpness)));
                }
            }

            texture.Apply();

            return texture;
        }

        /// <summary>A butterfly in two frames (wings open, wings closed), side by side.</summary>
        static Texture2D Wings()
        {
            const int size = 64;
            var texture = new Texture2D(size * 2, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

            for (var frame = 0; frame < 2; frame++)
            {
                var spread = frame == 0 ? 1f : 0.25f;

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var u = (x + 0.5f) / size * 2 - 1;
                        var v = (y + 0.5f) / size * 2 - 1;
                        var body = Mathf.Abs(u) < 0.06f && Mathf.Abs(v) < 0.5f;
                        var wu = Mathf.Abs(u) / spread;
                        var upper = (wu - 0.45f) * (wu - 0.45f) / 0.2f + (v - 0.2f) * (v - 0.2f) / 0.22f < 1;
                        var lower = (wu - 0.35f) * (wu - 0.35f) / 0.12f + (v + 0.35f) * (v + 0.35f) / 0.1f < 1;
                        var colour = new Color(0, 0, 0, 0);

                        if (upper || lower)
                        {
                            var edge = Mathf.Clamp01(wu * 1.4f - 0.5f);
                            colour = Color.Lerp(Color.white, new Color(0.25f, 0.25f, 0.3f), edge);
                        }

                        if (body)
                        {
                            colour = new Color(0.15f, 0.12f, 0.1f, 1);
                        }

                        texture.SetPixel(frame * size + x, y, colour);
                    }
                }
            }

            texture.Apply();

            return texture;
        }

        static Material Material(Texture texture, bool additive)
        {
            var shader = Shader.Find("GuWorld/Sky") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var material = new Material(shader) { mainTexture = texture };

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            }

            material.renderQueue = 3020;

            return material;
        }

        ParticleSystem System(string name, Material material, int max, float size, float lifetime, Vector3 box, float noise, float noiseFrequency)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime * 1.3f);
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.3f);
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var turbulence = system.noise;
            turbulence.enabled = true;
            turbulence.strength = noise;
            turbulence.frequency = noiseFrequency;
            turbulence.scrollSpeed = 0.4f;
            turbulence.quality = ParticleSystemNoiseQuality.Medium;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Play();

            return system;
        }

        void Build()
        {
            var dot = Dot(16, 2);
            gnats = System("Gnats", Material(dot, false), 600, 0.03f, 3, new Vector3(30, 2.5f, 30), 0.6f, 1.2f);
            var gnatMain = gnats.main;
            gnatMain.startColor = new Color(0.08f, 0.08f, 0.07f, 0.85f);
            // In clouds: bursts of many close together.
            var gnatShape = gnats.shape;
            gnatShape.shapeType = ParticleSystemShapeType.Sphere;
            gnatShape.radius = 0.6f;

            butterflies = System("Butterflies", Material(Wings(), false), 40, 0.13f, 14, new Vector3(40, 3, 40), 1.6f, 0.12f);
            var sheet = butterflies.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 1;
            sheet.cycleCount = 60;
            var butterflyMain = butterflies.main;
            butterflyMain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.82f, 0.7f), new Color(0.75f, 0.55f, 0.3f));
            var flutter = butterflies.GetComponent<ParticleSystemRenderer>();
            flutter.alignment = ParticleSystemRenderSpace.View;

            firefly = Material(Dot(32, 2.5f), true);
            fireflies = System("Fireflies", firefly, 260, 0.11f, 7, new Vector3(50, 4, 50), 0.9f, 0.25f);
            var fireMain = fireflies.main;
            fireMain.startColor = new Color(0.75f, 1, 0.35f, 1);
            // They blink: brighter and dimmer through their lives.
            var blink = fireflies.colorOverLifetime;
            blink.enabled = true;
            blink.color = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                alphaKeys = new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0.1f, 0.35f), new GradientAlphaKey(1, 0.55f), new GradientAlphaKey(0.2f, 0.75f), new GradientAlphaKey(0, 1) },
            };

            dragonflies = System("Dragonflies", Material(Dot(16, 1.5f), false), 24, 0.12f, 6, new Vector3(30, 1.5f, 30), 3.5f, 0.6f);
            var dragonMain = dragonflies.main;
            dragonMain.startColor = new Color(0.2f, 0.32f, 0.38f, 1);
            var stretch = dragonflies.GetComponent<ParticleSystemRenderer>();
            stretch.renderMode = ParticleSystemRenderMode.Stretch;
            stretch.velocityScale = 0.08f;
            stretch.lengthScale = 3;
        }

        /// <summary>Follows the camera and sets how many of each fly, by the hour, the weather and the place.</summary>
        public void Apply(DaySky sky, Weather weather, Transform camera)
        {
            if (camera == null)
            {
                return;
            }

            followed = camera;
            var level = GameSettings.Current.Insects;
            var night = sky != null ? sky.Night : 0;
            var dry = 1 - Mathf.Clamp01(weather.Rain * 2);
            var height = camera.position.y;
            var low = 1 - Mathf.Clamp01((height - 1400) / 500);
            var day = (1 - night) * dry * low * level;
            var water = 0f;
            var plan = WorldPlan.Current;

            if (plan != null)
            {
                var p = new Vector2(camera.position.x, camera.position.z);
                water = 1 - Mathf.Clamp01((WorldPlan.Distance(plan.Streams, p) - 5) / 40);
            }

            Rate(gnats, 2.2f * day * (0.6f + 0.4f * Mathf.PerlinNoise(Time.time * 0.05f, 3)), true);
            Rate(butterflies, 2.5f * day * Mathf.Clamp01(1 - weather.Cover), false);
            Rate(fireflies, 34 * night * dry * low * level * (0.6f + water), false);
            Rate(dragonflies, 4 * day * water, false);
        }

        float burst;

        void Rate(ParticleSystem system, float rate, bool bursts)
        {
            var emission = system.emission;

            if (!bursts)
            {
                emission.rateOverTime = rate;

                return;
            }

            // Gnats come in clouds: now and then a burst of forty at one spot.
            emission.rateOverTime = 0;
            burst += rate * Time.deltaTime;

            if (burst >= 1)
            {
                burst = 0;

                if (followed != null)
                {
                    var spot = followed.position + new Vector3(Random.Range(-12f, 12f), Random.Range(-1.2f, 0.5f), Random.Range(-12f, 12f));
                    var shape = system.shape;
                    shape.position = system.transform.InverseTransformPoint(spot);
                    system.Emit(40);
                }
            }
        }

        void LateUpdate()
        {
            if (followed == null)
            {
                return;
            }

            var at = followed.position;
            var ahead = Vector3.ProjectOnPlane(followed.forward, Vector3.up).normalized * 8;
            butterflies.transform.position = at + ahead + Vector3.down * 0.5f;
            fireflies.transform.position = at + ahead + Vector3.down * 0.3f;
            dragonflies.transform.position = at + ahead + Vector3.down * 1;
            gnats.transform.position = at;
        }
    }
}
