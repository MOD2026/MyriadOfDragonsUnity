using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MyriadOfDragons.Editor
{
    /// <summary>
    /// One-shot Android APK builder for the Chapter 1 ASAP smoke candidate.
    /// Invoked from batchmode: -executeMethod MyriadOfDragons.Editor.AndroidReleaseBuild.BuildDevelopmentApk
    /// Release (non-Development) variant: -executeMethod MyriadOfDragons.Editor.AndroidReleaseBuild.BuildReleaseApk
    /// </summary>
    public static class AndroidReleaseBuild
    {
        private const string PackageName = "com.myriadofdragons.chapter1";
        private const string OutputRelativePath = "Builds/Android/MyriadOfDragons_Chapter1_Dev.apk";
        private const string ReleaseOutputRelativePath = "Builds/Android/MyriadOfDragons_Chapter1_Release.apk";

        public static void BuildDevelopmentApk()
        {
            BuildEnvironmentGuard.EnsureBetaEnvironment("AndroidReleaseBuild");
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outputPath = Path.Combine(projectRoot, OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PackageName);
            PlayerSettings.productName = "Myriad of Dragons";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;

            // ARM64 is the modern Play / device default; match existing project arch bit if set.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            string[] scenes = GetEnabledScenePaths();
            if (scenes.Length == 0)
            {
                Debug.LogError("[AndroidReleaseBuild] No enabled scenes in Build Settings.");
                EditorApplication.Exit(1);
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging,
            };

            Debug.Log($"[AndroidReleaseBuild] Building Development APK -> {outputPath}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[AndroidReleaseBuild] SUCCESS size={summary.totalSize} bytes path={outputPath}");
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError($"[AndroidReleaseBuild] FAILED result={summary.result} errors={summary.totalErrors}");
            EditorApplication.Exit(1);
        }

        /// <summary>
        /// Non-Development counterpart to BuildDevelopmentApk - same package id, ARM64, minSdk 26,
        /// Gradle build system, and enabled-scene list, but with neither BuildOptions.Development
        /// nor BuildOptions.AllowDebugging set, and its own output path (a Dev and a Release APK
        /// must never silently overwrite each other on disk).
        /// </summary>
        public static void BuildReleaseApk()
        {
            BuildEnvironmentGuard.EnsureBetaEnvironment("AndroidReleaseBuild");
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outputPath = Path.Combine(projectRoot, ReleaseOutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PackageName);
            PlayerSettings.productName = "Myriad of Dragons";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;

            // ARM64 is the modern Play / device default; match existing project arch bit if set.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            string[] scenes = GetEnabledScenePaths();
            if (scenes.Length == 0)
            {
                Debug.LogError("[AndroidReleaseBuild] No enabled scenes in Build Settings.");
                EditorApplication.Exit(1);
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                // Neither Development nor AllowDebugging - this is the ship candidate, not the
                // smoke-test build BuildDevelopmentApk produces.
                options = BuildOptions.None,
            };

            Debug.Log($"[AndroidReleaseBuild] Building Release APK -> {outputPath}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[AndroidReleaseBuild] SUCCESS size={summary.totalSize} bytes path={outputPath}");
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError($"[AndroidReleaseBuild] FAILED result={summary.result} errors={summary.totalErrors}");
            EditorApplication.Exit(1);
        }

        private static string[] GetEnabledScenePaths()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                    list.Add(scene.path);
            }
            return list.ToArray();
        }
    }
}
