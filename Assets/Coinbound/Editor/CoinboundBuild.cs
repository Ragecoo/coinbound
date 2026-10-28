using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Coinbound.Editor
{
    public static class CoinboundBuild
    {
        [MenuItem("Coinbound/Build Windows preview")]
        public static void BuildWindowsPreview()
        {
            FullScreenMode mode = PlayerSettings.fullScreenMode;
            int width = PlayerSettings.defaultScreenWidth;
            int height = PlayerSettings.defaultScreenHeight;
            bool native = PlayerSettings.defaultIsNativeResolution;
            try
            {
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultIsNativeResolution = false;
                PlayerSettings.defaultScreenWidth = 1280;
                PlayerSettings.defaultScreenHeight = 720;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                    locationPathName = "Builds/Coinbound/Coinbound.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Build failed: {report.summary.result}");
                Debug.Log("Coinbound Windows preview built successfully.");
            }
            finally
            {
                PlayerSettings.fullScreenMode = mode;
                PlayerSettings.defaultIsNativeResolution = native;
                PlayerSettings.defaultScreenWidth = width;
                PlayerSettings.defaultScreenHeight = height;
            }
        }
    }
}
