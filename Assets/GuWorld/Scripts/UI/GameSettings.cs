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
        /// 0 low, 1 medium, 2 high: how sharp the picture is drawn (render scale), how
        /// smooth its edges (MSAA) and how far the shadows reach. (The project's quality
        /// levels cannot do it: a Windows build keeps only one of them.)
        /// </summary>
        public static int Quality
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, 2), 0, 2);
            set => PlayerPrefs.SetInt(QualityKey, Mathf.Clamp(value, 0, 2));
        }

        static readonly (float scale, int msaa, float shadows)[] Levels =
        {
            (0.75f, 1, 25),
            (1f, 2, 40),
            (1f, 4, 60),
        };

        // The render pipeline's own values, to give back on leaving play in the editor
        // (there the pipeline is an asset of the project, and changes to it would stay).
        static bool remembered;
        static float originalScale;
        static int originalMsaa;
        static float originalShadows;

        /// <summary>Gives the render pipeline its own values back (the editor, on leaving play).</summary>
        public static void Restore()
        {
            if (remembered && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = originalScale;
                urp.msaaSampleCount = originalMsaa;
                urp.shadowDistance = originalShadows;
            }
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
            set => PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        }

        public static bool VSync
        {
            get => PlayerPrefs.GetInt(VSyncKey, 1) == 1;
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
                    remembered = true;
                }

                var level = Levels[Quality];
                urp.renderScale = level.scale;
                urp.msaaSampleCount = level.msaa;
                urp.shadowDistance = level.shadows;
            }

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
