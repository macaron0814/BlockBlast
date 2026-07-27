using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BlockBlastGame.Editor
{
    [InitializeOnLoad]
    public static class IOSCommandLineBuild
    {
        static readonly string RequestMarkerPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../Build/.request-ios-build"));

        static IOSCommandLineBuild()
        {
            if (!File.Exists(RequestMarkerPath))
                return;

            EditorApplication.delayCall += BuildRequestedFromMarker;
        }

        [MenuItem("BlockBlast/iOS/Build Xcode Project")]
        public static void Build()
        {
            string outputPath = Environment.GetEnvironmentVariable("BLOCKBLAST_IOS_BUILD_PATH");
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = $"Build/iOS-{PlayerSettings.iOS.buildNumber}";

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"iOS build failed: {report.summary.result}");

            Debug.Log($"[IOSCommandLineBuild] Build completed: {outputPath}");
        }

        static void BuildRequestedFromMarker()
        {
            File.Delete(RequestMarkerPath);
            Build();
        }
    }
}
