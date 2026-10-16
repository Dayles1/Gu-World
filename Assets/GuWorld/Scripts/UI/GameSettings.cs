using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GuWorld.UI
{
    /// <summary>
    /// The player's settings, kept on this computer (PlayerPrefs), apart from
    /// the saved game: a reset of the game leaves them as they are. Each one
    /// takes effect the moment it changes (Apply).
    /// </summary>
    public static class GameSettings
    {
        const string SensitivityKey = "gw.sensitivity";
        const string InvertKey = "gw.invertY";
        const string QualityKey = "gw.quality";
        const string FullscreenKey = "gw.fullscreen";
        const string VSyncKey = "gw.vsync";
        const string FovKey = "gw.fov";
        const string VolumeKey = "gw.volume";
        const string FpsKey = "gw.showFps";
        const string HitboxKey = "gw.hitboxes";

        /// <summary>The hitbox view (F4): every collider near the camera drawn as wire lines.</summary>
        public static bool ShowHitboxes
        {
            get => PlayerPrefs.GetInt(HitboxKey, 0) == 1;
            set => PlayerPrefs.SetInt(HitboxKey, value ? 1 : 0);
        }

        /// <summary>A frames-per-second counter in the top left corner.</summary>
        public static bool ShowFps
        {
            get => PlayerPrefs.GetInt(FpsKey, 0) == 1;
            set => PlayerPrefs.SetInt(FpsKey, value ? 1 : 0);
        }

        /// <summary>How fast the mouse turns the camera (1 = as on the web).</summary>
        public static float Sensitivity
        {
            get => PlayerPrefs.GetFloat(SensitivityKey, 1);
            set => PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, 0.25f, 3));
        }

        public static bool InvertY
        {
            get => PlayerPrefs.GetInt(InvertKey, 0) == 1;
            set => PlayerPrefs.SetInt(InvertKey, value ? 1 : 0);
        }

        /// <summary>
        /// The graphics level, 0 low … 3 ultra: how sharp the picture is drawn (render
        /// scale), how smooth its edges (MSAA), how far and how finely the shadows reach,
        /// how far grass, trees and fine detail are drawn, how many insects fly and how
        /// far people are seen. (The project's quality levels cannot do it: a Windows
        /// build keeps only one of them.)
        /// </summary>
        public static int Quality
        {
            // Kept once read: every villager asks for the level each frame.
            get => quality >= 0 ? quality : quality = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, 1), 0, Levels.Length - 1);
            set
            {
                quality = Mathf.Clamp(value, 0, Levels.Length - 1);
                PlayerPrefs.SetInt(QualityKey, quality);
            }
        }

        static int quality = -1;

        public struct Level
        {
            public string Name;
            public float Scale;
            public int Msaa;
            public float Shadows;
            public int Cascades;
            public float LodBias;
            /// <summary>The terrain's error in pixels (lower: finer far away).</summary>
            public float TerrainError;
            /// <summary>Grass and small plants: how far, m, and how thick, 0…1.</summary>
            public float GrassDistance;
            public float GrassDensity;
            /// <summary>Trees in full, m, and drawn at all, m.</summary>
            public float TreeDetail;
            public float TreeDistance;
            /// <summary>How many insects fly, against the high level's.</summary>
            public float Insects;
            /// <summary>How far people are drawn, m, and posed, m.</summary>
            public float PeopleSight;
            public float PeoplePose;
            /// <summary>How far the camera sees, against the location's own.</summary>
            public float Distance;
            public bool Bloom;
            /// <summary>Shadows' edges softened (the sun's); `SoftQuality` 1 low … 3 high.</summary>
            public bool SoftShadows;
            public int SoftQuality;
            public int ShadowResolution;
            /// <summary>Screen-space ambient occlusion (a whole extra pass over the scene for its normals).</summary>
            public bool Ambient;
            /// <summary>The ground painted from its eight layers this near, m (two passes of four); farther, from its baked picture (one).</summary>
            public float GroundDetail;
        }

        public static readonly Level[] Levels =
        {
            // Below 1 the picture is drawn smaller and enlarged by FSR (sharp enough, far cheaper on a laptop's graphics).
            new Level { Name = "Низкое", Scale = 0.7f, Msaa = 1, Shadows = 30, Cascades = 1, LodBias = 0.6f, TerrainError = 8, GrassDistance = 35, GrassDensity = 0.3f, TreeDetail = 120, TreeDistance = 300, Insects = 0.3f, PeopleSight = 90, PeoplePose = 40, Distance = 0.55f, Bloom = false, SoftShadows = false, SoftQuality = 1, ShadowResolution = 1024, Ambient = false, GroundDetail = 150 },
            new Level { Name = "Среднее", Scale = 0.8f, Msaa = 1, Shadows = 45, Cascades = 2, LodBias = 0.9f, TerrainError = 5, GrassDistance = 55, GrassDensity = 0.5f, TreeDetail = 200, TreeDistance = 500, Insects = 0.6f, PeopleSight = 130, PeoplePose = 60, Distance = 0.75f, Bloom = true, SoftShadows = true, SoftQuality = 1, ShadowResolution = 2048, Ambient = false, GroundDetail = 250 },
            new Level { Name = "Высокое", Scale = 1, Msaa = 2, Shadows = 70, Cascades = 3, LodBias = 1.2f, TerrainError = 3, GrassDistance = 80, GrassDensity = 0.75f, TreeDetail = 320, TreeDistance = 800, Insects = 1, PeopleSight = 170, PeoplePose = 75, Distance = 1, Bloom = true, SoftShadows = true, SoftQuality = 2, ShadowResolution = 2048, Ambient = true, GroundDetail = 450 },
            new Level { Name = "Ультра", Scale = 1, Msaa = 4, Shadows = 120, Cascades = 4, LodBias = 1.8f, TerrainError = 1.5f, GrassDistance = 120, GrassDensity = 1, TreeDetail = 480, TreeDistance = 1400, Insects = 1.5f, PeopleSight = 230, PeoplePose = 110, Distance = 1.25f, Bloom = true, SoftShadows = true, SoftQuality = 3, ShadowResolution = 4096, Ambient = true, GroundDetail = 900 },
        };

        /// <summary>The level in effect.</summary>
        public static Level Current => Levels[Quality];

        // The render pipeline's own values, to give back on leaving play in the editor
        // (there the pipeline is an asset of the project, and changes to it would stay).
        static bool remembered;
        static float originalScale;
        static int originalMsaa;
        static float originalShadows;
        static int originalCascades;
        static bool originalHdr;
        static bool originalDepth;
        static bool originalOpaque;
        static int originalShadowResolution;
        static UpscalingFilterSelection originalUpscaling;
        static object originalSoftQuality;
        static bool originalAmbient;
        static Material noHoles;

        // The asset's soft shadow quality has no public setter.
        static readonly System.Reflection.FieldInfo SoftQualityField = typeof(UniversalRenderPipelineAsset).GetField("m_SoftShadowQuality", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        /// <summary>The renderers' ambient occlusion (SSAO) features.</summary>
        static System.Collections.Generic.IEnumerable<ScriptableRendererFeature> AmbientFeatures(UniversalRenderPipelineAsset urp)
        {
            foreach (var data in urp.rendererDataList.ToArray())
            {
                if (data == null)
                {
                    continue;
                }

                foreach (var feature in data.rendererFeatures)
                {
                    if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                    {
                        yield return feature;
                    }
                }
            }
        }

        static void SetAmbient(UniversalRenderPipelineAsset urp, bool on)
        {
            foreach (var feature in AmbientFeatures(urp))
            {
                feature.SetActive(on);
            }
        }

        static bool AmbientOn(UniversalRenderPipelineAsset urp)
        {
            foreach (var feature in AmbientFeatures(urp))
            {
                return feature.isActive;
            }

            return false;
        }

        /// <summary>Gives the render pipeline its own values back (the editor, on leaving play).</summary>
        public static void Restore()
        {
            if (remembered && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = originalScale;
                urp.msaaSampleCount = originalMsaa;
                urp.shadowDistance = originalShadows;
                urp.shadowCascadeCount = originalCascades;
                urp.supportsHDR = originalHdr;
                urp.supportsCameraDepthTexture = originalDepth;
                urp.supportsCameraOpaqueTexture = originalOpaque;
                urp.mainLightShadowmapResolution = originalShadowResolution;
#pragma warning disable CS0618
                urp.upscalingFilter = originalUpscaling;
#pragma warning restore CS0618
                SoftQualityField?.SetValue(urp, originalSoftQuality);
                SetAmbient(urp, originalAmbient);
            }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
            set => PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        }

        /// <summary>Waiting for the screen: off unless chosen (a frame a little over 16 ms would wait to 33).</summary>
        public static bool VSync
        {
            get => PlayerPrefs.GetInt(VSyncKey, 0) == 1;
            set => PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0);
        }

        /// <summary>The camera's field of view while walking, degrees (it widens a little when running).</summary>
        public static float FieldOfView
        {
            get => PlayerPrefs.GetFloat(FovKey, 65);
            set => PlayerPrefs.SetFloat(FovKey, Mathf.Clamp(value, 55, 90));
        }

        /// <summary>The overall loudness, 0…1.</summary>
        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            set => PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
        }

        /// <summary>The settings changed (things that read the level — insects, people — take it up).</summary>
        public static event System.Action Changed;

        /// <summary>Puts every setting into effect.</summary>
        public static void Apply(Render.ThirdPersonCamera view)
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                if (!remembered)
                {
                    originalScale = urp.renderScale;
                    originalMsaa = urp.msaaSampleCount;
                    originalShadows = urp.shadowDistance;
                    originalCascades = urp.shadowCascadeCount;
                    originalHdr = urp.supportsHDR;
                    originalDepth = urp.supportsCameraDepthTexture;
                    originalOpaque = urp.supportsCameraOpaqueTexture;
                    originalShadowResolution = urp.mainLightShadowmapResolution;
#pragma warning disable CS0618
                    originalUpscaling = urp.upscalingFilter;
#pragma warning restore CS0618
                    originalSoftQuality = SoftQualityField?.GetValue(urp);
                    originalAmbient = AmbientOn(urp);
                    remembered = true;
                }

                urp.renderScale = Current.Scale;
                urp.msaaSampleCount = Current.Msaa;
                urp.shadowDistance = Current.Shadows;
                urp.shadowCascadeCount = Current.Cascades;
                urp.mainLightShadowmapResolution = Current.ShadowResolution;
#pragma warning disable CS0618
                urp.upscalingFilter = Current.Scale < 1 ? UpscalingFilterSelection.FSR : UpscalingFilterSelection.Auto;
#pragma warning restore CS0618
                // The bright parts' glow (bloom) needs the wider colours; without it they cost only memory and time.
                urp.supportsHDR = Current.Bloom;
                // No shader of the game reads the scene's depth or colour (the ambient occlusion asks for its own).
                urp.supportsCameraDepthTexture = false;
                urp.supportsCameraOpaqueTexture = false;

                if (SoftQualityField != null)
                {
                    SoftQualityField.SetValue(urp, System.Enum.ToObject(SoftQualityField.FieldType, Current.SoftQuality));
                }

                SetAmbient(urp, Current.Ambient);
            }

            var level = Current;

            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional && light.shadows != LightShadows.None)
                {
                    light.shadows = level.SoftShadows ? LightShadows.Soft : LightShadows.Hard;
                }
            }
            QualitySettings.lodBias = level.LodBias;

            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain == null)
                {
                    continue;
                }

                terrain.heightmapPixelError = level.TerrainError;
                terrain.basemapDistance = level.GroundDetail;
                terrain.detailObjectDistance = level.GrassDistance;

                // A village's own ground has no holes (only the mountain's tiles under the villages do): its material
                // without the alpha test, which costs the early depth test on every pixel of ground drawn.
                if (terrain.groupingID == 2 && terrain.materialTemplate != null && terrain.materialTemplate.IsKeywordEnabled("_ALPHATEST_ON"))
                {
                    noHoles ??= new Material(terrain.materialTemplate) { name = terrain.materialTemplate.name + " (no holes)" };
                    noHoles.DisableKeyword("_ALPHATEST_ON");
                    terrain.materialTemplate = noHoles;
                }
                terrain.detailObjectDensity = level.GrassDensity;
                terrain.treeBillboardDistance = level.TreeDetail;
                // The trees are drawn by the TreeInstancer (in bulk); the terrains keep them only to be solid.
                Render.TreeInstancer.Distance = level.TreeDistance;
                terrain.treeDistance = Render.TreeInstancer.Current != null ? 0 : level.TreeDistance;
                terrain.treeCrossFadeLength = 30;
            }

            // Bloom (the lanterns' and the moonblade's glow) only from the medium level up.
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (volume.sharedProfile != null && volume.sharedProfile.TryGet<Bloom>(out var bloom))
                {
                    bloom.active = level.Bloom;
                }
            }

            Changed?.Invoke();

            QualitySettings.vSyncCount = VSync ? 1 : 0;
            AudioListener.volume = Volume;

            // The window's mode only in a built game (the editor keeps its own).
            if (!Application.isEditor && Screen.fullScreen != Fullscreen)
            {
                Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            }

            if (view != null)
            {
                view.SensitivityScale = Sensitivity;
                view.InvertY = InvertY;
                view.BaseFov = FieldOfView;
            }

            PlayerPrefs.Save();
        }
    }
}
