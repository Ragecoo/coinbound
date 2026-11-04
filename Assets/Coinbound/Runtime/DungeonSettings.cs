using System;
using UnityEngine;

namespace Coinbound
{
    /// <summary>Player preferences, separate from the authored effects asset.</summary>
    public static class DungeonSettings
    {
        private const string Prefix = "Coinbound.Settings.";
        private static bool? appliedFullscreen;
        private static int windowWidth = 1280, windowHeight = 720;
        public static event Action Changed;
        public static float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Volume", 0.65f));
        public static float Brightness => Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "Brightness", 1), 0.7f, 1.35f);
        public static bool Fullscreen => PlayerPrefs.GetInt(Prefix + "Fullscreen", 0) != 0;
        public static bool VSync => PlayerPrefs.GetInt(Prefix + "VSync", 1) != 0;
        public static bool ReducedMotion => PlayerPrefs.GetInt(Prefix + "ReducedMotion", 0) != 0;
        public static bool ShowHints => PlayerPrefs.GetInt(Prefix + "ShowHints", 1) != 0;
        public static bool DamageFlash => PlayerPrefs.GetInt(Prefix + "DamageFlash", 1) != 0;
        public static bool VoiceInput => PlayerPrefs.GetInt(Prefix + "VoiceInput", 1) != 0;

        public static void SetVolume(float value) { PlayerPrefs.SetFloat(Prefix + "Volume", Mathf.Clamp01(value)); Apply(); }
        public static void SetBrightness(float value) { PlayerPrefs.SetFloat(Prefix + "Brightness", Mathf.Clamp(value, .7f, 1.35f)); Apply(); }
        public static void SetToggle(string key, bool value)
        {
            if (key != "Fullscreen" && key != "VSync" && key != "ReducedMotion" && key != "ShowHints" && key != "DamageFlash" && key != "VoiceInput")
                throw new ArgumentException("Unknown preference", nameof(key));
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
            Apply();
        }
        public static void Apply()
        {
            AudioListener.volume = Volume;
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : 120;
            if (!Application.isEditor && !Application.isBatchMode)
                ApplyDisplayMode();
            Shader.SetGlobalFloat("_CoinboundBrightness", Brightness);
            Changed?.Invoke();
        }
        public static void Save() => PlayerPrefs.Save();
        private static void ApplyDisplayMode()
        {
            bool fullscreen = Fullscreen;
            if (appliedFullscreen == fullscreen) return;
            if (fullscreen)
            {
                if (Screen.fullScreenMode == FullScreenMode.Windowed)
                {
                    windowWidth = Screen.width;
                    windowHeight = Screen.height;
                }
                // Borderless mode alone can upscale the old 720p render buffer with filtering.
                // Explicitly render at the desktop resolution so the final image stays 1:1.
                Resolution desktop = Screen.currentResolution;
                Screen.SetResolution(desktop.width, desktop.height, FullScreenMode.FullScreenWindow);
            }
            else if (appliedFullscreen == true || Screen.fullScreenMode != FullScreenMode.Windowed)
                Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
            appliedFullscreen = fullscreen;
        }
        public static void Reset()
        {
            foreach (string key in new[] { "Volume", "Brightness", "Fullscreen", "VSync", "ReducedMotion", "ShowHints", "DamageFlash", "VoiceInput" })
                PlayerPrefs.DeleteKey(Prefix + key);
            Apply(); Save();
        }
    }
}
