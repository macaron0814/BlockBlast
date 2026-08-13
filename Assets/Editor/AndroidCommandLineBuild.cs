using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlockBlastGame.Editor
{
    public static class AndroidCommandLineBuild
    {
        [MenuItem("BlockBlast/Android/Build Release AAB")]
        public static void BuildReleaseAab()
        {
            string outputPath = Environment.GetEnvironmentVariable("BLOCKBLAST_ANDROID_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.Combine(
                    "Build",
                    "Android",
                    $"Pazupuri-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}.aab");
            }

            BuildAndroid(outputPath, buildAppBundle: true, BuildOptions.None);
        }

        [MenuItem("BlockBlast/Android/Build Test APK")]
        public static void BuildTestApk()
        {
            string outputPath = Environment.GetEnvironmentVariable("BLOCKBLAST_ANDROID_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.Combine(
                    "Build",
                    "Android",
                    $"Pazupuri-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}-test.apk");
            }

            BuildAndroid(outputPath, buildAppBundle: false, BuildOptions.Development);
        }

        private static void BuildAndroid(
            string outputPath,
            bool buildAppBundle,
            BuildOptions buildOptions)
        {
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            EditorUserBuildSettings.buildAppBundle = buildAppBundle;

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = buildOptions
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"Android build failed: {report.summary.result}");

            Debug.Log($"[AndroidCommandLineBuild] Build completed: {outputPath}");
        }
    }
}
