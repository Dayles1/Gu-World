using GuWorld.World;
using UnityEngine;

namespace GuWorld.Render
{
    /// <summary>
    /// Light by the time of day: where the sun is, how bright and what colour,
    /// the sky's and the haze's colour (ported from client/render/sky.ts and
    /// the light part of client/render/stage.ts). A pure function of the time
    /// of day (0 midnight, 0.5 noon) and the weather. The sun rises at 06:00 in
    /// the east (the web's +X, -X here) and sets at 18:00 in the west; the moon
    /// stands across the sky from it and lights the night faintly.
    ///
    /// With a sky material (a procedural skybox, RenderSettings.skybox) the
    /// camera draws the sky itself; without one (the proving ground) it clears
    /// to the sky's colour. Clouds dim the sun and grey the sky; rain thickens
    /// the haze and draws it in.
    /// </summary>
    public class DaySky : MonoBehaviour
    {
        // A darker, colder palette than the web's: the novel's mountain is a grim place.
        static readonly Color NightSky = Hex(0x070a14);
        static readonly Color LowSky = Hex(0xa8806c);
        static readonly Color DaySkyColor = Hex(0x8496a6);
        static readonly Color NightGround = Hex(0x0e1014);
        static readonly Color DayGround = Hex(0x4e4f46);
        static readonly Color LowSun = Hex(0xf0a070);
        static readonly Color HighSun = Hex(0xf2ead8);
        static readonly Color MoonLight = new Color(0.5f, 0.6f, 0.88f);
        /// <summary>The least light of a clear night (the stars, the sky's own glow): the village still shows, dimly and blue.</summary>
        static readonly Color NightFloor = Hex(0x1c2436);
        /// <summary>The sky's own glow at dusk, after the sun and before the moon: the light of the blue hour.</summary>
        static readonly Color DuskGlow = Hex(0x3c3848);
        const float SunStrength = 2.05f;

        public Light Sun;
        /// <summary>The moon's light (a second, shadowless directional light), made when there is a sky material.</summary>
        public Light Moon;
        public Camera View;
        /// <summary>The haze: where it starts and where nothing shows through it, m (in fair weather).</summary>
        public float FogStart = 60;
        public float FogEnd = 260;

        /// <summary>What the last Apply worked out, for the clouds, stars and lights.</summary>
        public DayLight Current { get; private set; }
        /// <summary>0 by day … 1 deep in the night.</summary>
        public float Night { get; private set; }
        /// <summary>Towards the moon, unit length.</summary>
        public Vector3 MoonDirection { get; private set; }
        public Color HazeColour { get; private set; }

        public struct DayLight
        {
            /// <summary>Toward the sun, unit length.</summary>
            public Vector3 sunDirection;
            public Color sunColor;
            public float sunIntensity;
            public Color sky;
            public Color ground;
            public float ambientIntensity;
        }

        void Start()
        {
            // The tone curve: neutral, not ACES — ACES pressed the shade and the middle tones down into black (the mood is
            // the grade's and the light's, not lost detail). The scene's own profile (built with ACES) is set so here.
            foreach (var volume in FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
            {
                if (volume.isGlobal && volume.sharedProfile != null && volume.profile.TryGet<UnityEngine.Rendering.Universal.Tonemapping>(out var tone))
                {
                    tone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.Neutral);
                }
            }
        }

        public static DayLight LightAt(float timeOfDay)
        {
            var angle = (timeOfDay - 0.25f) * Mathf.PI * 2;
            // East is the web's +X, which is -X in Unity's mirrored world.
            var sunDirection = new Vector3(-Mathf.Cos(angle) * 0.85f, Mathf.Sin(angle), 0.35f).normalized;
            var height = sunDirection.y;
            var day = SmoothStep(height, -0.1f, 0.25f);
            var low = 1 - SmoothStep(Mathf.Abs(height), 0, 0.3f);

            return new DayLight
            {
                sunDirection = sunDirection,
                sunColor = Color.Lerp(HighSun, LowSun, low),
                sunIntensity = SunStrength * SmoothStep(height, -0.02f, 0.18f),
                sky = Color.Lerp(Color.Lerp(NightSky, DaySkyColor, day), LowSky, low * 0.5f),
                ground = Color.Lerp(NightGround, DayGround, day),
                ambientIntensity = 0.24f + 1.05f * day,
            };
        }

        public void Apply(float timeOfDay) => Apply(timeOfDay, Weather.Fair);

        public void Apply(float timeOfDay, Weather weather)
        {
            var light = LightAt(timeOfDay);
            var cover = weather.Cover;
            var dull = SmoothStep(cover, 0.3f, 1);
            // A longer dusk: the blue hour lasts some forty minutes of the game after sunset before it is night.
            Night = 1 - SmoothStep(light.sunDirection.y, -0.32f, 0.05f);

            // Clouds grey the sky and dim the sun.
            var grey = light.sky.grayscale;
            light.sky = Color.Lerp(light.sky, new Color(grey, grey, grey * 1.04f) * 0.92f, 0.65f * dull);
            light.sunIntensity *= 1 - 0.7f * dull;
            light.ambientIntensity *= 1 - 0.2f * dull;
            Current = light;

            if (Sun != null)
            {
                // A directional light shines along its forward: away from the sun.
                Sun.transform.rotation = Quaternion.LookRotation(-light.sunDirection);
                Sun.color = light.sunColor;
                // three.js and URP measure light differently; this keeps noon bright, not burnt.
                Sun.intensity = light.sunIntensity * 0.6f;
                Sun.enabled = light.sunIntensity > 0.001f;
            }

            // The moon: across the sky from the sun, tilted south so it is not straight overhead.
            MoonDirection = new Vector3(-light.sunDirection.x, -light.sunDirection.y, light.sunDirection.z - 0.5f).normalized;

            if (Moon != null)
            {
                Moon.transform.rotation = Quaternion.LookRotation(-MoonDirection);
                Moon.color = MoonLight;
                Moon.intensity = 0.5f * Night * (1 - 0.7f * dull) * SmoothStep(MoonDirection.y, -0.05f, 0.2f);
                Moon.enabled = Moon.intensity > 0.002f;
            }

            // The sky's light. These colours are read as sRGB (the project draws in linear light): a wall in the shade at
            // noon gets about a fifth of white from them — enough to see its timber and plaster, not a black cut-out.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            // The floor of the night's light comes in as the day's goes (not only when it is full night): no darker gap at dusk.
            var dusk = 1 - SmoothStep(light.sunDirection.y, -0.1f, 0.25f);
            var moonGlow = MoonLight * 0.08f * Night * (1 - dull) + NightFloor * dusk * (1 - 0.5f * dull) + DuskGlow * (dusk * (1 - Night) * (1 - 0.4f * dull));
            RenderSettings.ambientSkyColor = light.sky * light.ambientIntensity * 0.85f + moonGlow;
            RenderSettings.ambientEquatorColor = Color.Lerp(light.sky, light.ground, 0.5f) * light.ambientIntensity * 0.85f + moonGlow * 0.8f;
            RenderSettings.ambientGroundColor = light.ground * light.ambientIntensity * 0.55f + moonGlow * 0.4f;

            // The haze: thicker and nearer in rain, a little under cloud.
            var thick = Mathf.Lerp(1, 0.32f, weather.Rain) * Mathf.Lerp(1, 0.85f, dull);
            HazeColour = Color.Lerp(light.sky, light.ground, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = FogStart * thick;
            RenderSettings.fogEndDistance = FogEnd * thick;
            RenderSettings.fogColor = HazeColour;

            var skybox = RenderSettings.skybox;

            if (skybox != null)
            {
                RenderSettings.sun = Sun;
                skybox.SetFloat("_Exposure", Mathf.Lerp(1.25f, 0.55f, dull) * Mathf.Lerp(1, 0.18f, Night));
                skybox.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.85f, 1.4f, dull));
                skybox.SetColor("_SkyTint", Color.Lerp(new Color(0.5f, 0.52f, 0.56f), new Color(0.42f, 0.43f, 0.45f), dull));
                skybox.SetColor("_GroundColor", Color.Lerp(HazeColour, light.ground, 0.3f));
                skybox.SetFloat("_SunSize", Mathf.Lerp(0.045f, 0.01f, dull));
            }

            if (View != null)
            {
                View.clearFlags = skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                View.backgroundColor = light.sky;
            }
        }

        static float SmoothStep(float x, float min, float max)
        {
            if (x <= min) return 0;
            if (x >= max) return 1;
            x = (x - min) / (max - min);

            return x * x * (3 - 2 * x);
        }

        static Color Hex(int rgb) => new Color(
            ((rgb >> 16) & 0xff) / 255f,
            ((rgb >> 8) & 0xff) / 255f,
            (rgb & 0xff) / 255f);
    }
}
