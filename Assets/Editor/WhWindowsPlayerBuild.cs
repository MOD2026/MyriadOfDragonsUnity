using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MyriadOfDragons.Editor
{
    /// <summary>
    /// Player builder for Windows standalone and Android App Bundle (AAB).
    /// Batchmode:
    ///   -executeMethod MyriadOfDragons.Editor.WhWindowsPlayerBuild.BuildWindows64
    ///   -executeMethod MyriadOfDragons.Editor.WhWindowsPlayerBuild.BuildAndroidAab
    /// </summary>
    public static class WhWindowsPlayerBuild
    {
        private const string OutputRelativePath = "Builds/Windows64/MyriadOfDragons.exe";
        private const string AndroidPackageName = "com.myriadofdragons.chapter1";
        private const string AndroidAabRelativePath = "Builds/Android/MyriadOfDragons_Release.aab";

        public static void BuildWindows64()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outputPath = Path.Combine(projectRoot, OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            var scenes = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                    scenes.Add(scene.path);
            }

            if (scenes.Count == 0)
            {
                Debug.LogError("[WhWindowsPlayerBuild] No enabled scenes in Build Settings.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[WhWindowsPlayerBuild] Scenes: " + string.Join(", ", scenes));
            Debug.Log("[WhWindowsPlayerBuild] Building Windows64 -> " + outputPath);

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.AllowDebugging,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WhWindowsPlayerBuild] SUCCESS size={summary.totalSize} bytes path={outputPath} time={summary.totalTime}");
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError($"[WhWindowsPlayerBuild] FAILED result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings}");
            foreach (BuildStep step in report.steps)
            {
                foreach (BuildStepMessage msg in step.messages)
                {
                    if (msg.type == LogType.Error || msg.type == LogType.Exception || msg.type == LogType.Assert)
                        Debug.LogError("[WhWindowsPlayerBuild] " + msg.content);
                    else if (msg.type == LogType.Warning)
                        Debug.LogWarning("[WhWindowsPlayerBuild] " + msg.content);
                }
            }
            EditorApplication.Exit(1);
        }

        public static void BuildAndroidAab()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outputPath = Path.Combine(projectRoot, AndroidAabRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, AndroidPackageName);
            PlayerSettings.productName = "Myriad of Dragons";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            var scenes = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                    scenes.Add(scene.path);
            }

            if (scenes.Count == 0)
            {
                Debug.LogError("[WhWindowsPlayerBuild] No enabled scenes in Build Settings.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[WhWindowsPlayerBuild] Scenes: " + string.Join(", ", scenes));
            Debug.Log("[WhWindowsPlayerBuild] Building Android AAB -> " + outputPath);

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WhWindowsPlayerBuild] SUCCESS size={summary.totalSize} bytes path={outputPath} time={summary.totalTime}");
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError($"[WhWindowsPlayerBuild] FAILED result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings}");
            foreach (BuildStep step in report.steps)
            {
                foreach (BuildStepMessage msg in step.messages)
                {
                    if (msg.type == LogType.Error || msg.type == LogType.Exception || msg.type == LogType.Assert)
                        Debug.LogError("[WhWindowsPlayerBuild] " + msg.content);
                    else if (msg.type == LogType.Warning)
                        Debug.LogWarning("[WhWindowsPlayerBuild] " + msg.content);
                }
            }
            EditorApplication.Exit(1);
        }
    }
}
