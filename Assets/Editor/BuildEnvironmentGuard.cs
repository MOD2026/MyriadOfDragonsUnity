using System.IO;
using UnityEditor;
using UnityEngine;

namespace MyriadOfDragons.Editor
{
    /// <summary>
    /// Verifies the baked Unity Services environment for beta/smoke builds before they run.
    /// Root cause (2026-09-13): UnityServices.InitializeAsync() is called with no
    /// InitializationOptions anywhere in this codebase's production gateways (FriendsGateway,
    /// BazaarGateway, etc.), so the environment baked into
    /// StreamingAssets/UnityServicesProjectConfiguration.json at build time is whatever
    /// ProjectSettings/Packages/com.unity.services.core/Settings.json holds when the Editor
    /// process starts (Unity Services reads this at package init, before any -executeMethod build
    /// script runs - by the time this guard's code executes it is too late to change what will be
    /// baked, only possible to verify it and refuse to ship the wrong one).
    ///
    /// Every inspected handover build (rc14/rc15/rc17) baked "production" - the UGS default
    /// environment (`ugs env list`: isDefault=true) - which has zero Cloud Code modules deployed,
    /// producing live "Module could not be found" errors. The actual root cause: this settings
    /// file was never committed to git (`git ls-files` showed it untracked), so any fresh
    /// checkout/build machine had no snapshot to inherit and fell back to the UGS default
    /// ("production") rather than the project's real backend, `nonprod-validation` (see
    /// docs/LOCKED_DECISIONS_REGISTER.md "CloudCode Modules Deployed to nonprod-validation").
    ///
    /// This guard is the safety net for the case the committed file drifts again (e.g. a build
    /// machine's local Editor Services selection overriding the committed value): it reads the
    /// file directly (no Unity.Services.Core.Environments.Editor assembly reference needed - that
    /// package assembly is `autoReferenced: false` and not wired into this project's default
    /// Editor assembly) and refuses to build rather than silently shipping the wrong backend.
    /// </summary>
    public static class BuildEnvironmentGuard
    {
        public const string RequiredEnvironmentName = "nonprod-validation";
        private const string SettingsRelativePath = "ProjectSettings/Packages/com.unity.services.core/Settings.json";

        /// <summary>Call at the very top of every beta/smoke build method, before
        /// BuildPipeline.BuildPlayer. Exits the batchmode process with a loud failure rather than
        /// silently shipping a build baked against the wrong Unity Services environment.</summary>
        public static void EnsureBetaEnvironment(string callerLogTag)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string settingsPath = Path.Combine(projectRoot, SettingsRelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(settingsPath))
            {
                Debug.LogError($"[{callerLogTag}] {SettingsRelativePath} does not exist - the baked Unity Services environment cannot be verified. Open Window > Services in this Editor once, select '{RequiredEnvironmentName}', and re-run.");
                EditorApplication.Exit(1);
                return;
            }

            string json = File.ReadAllText(settingsPath);
            if (!json.Contains($"\"EnvironmentName\": \"{RequiredEnvironmentName}\"") &&
                !json.Contains($"\"EnvironmentName\":\"{RequiredEnvironmentName}\""))
            {
                Debug.LogError($"[{callerLogTag}] {SettingsRelativePath} does not have EnvironmentName '{RequiredEnvironmentName}' - refusing to build. This would bake the wrong backend environment and reproduce the live 404. Contents: {json}");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[{callerLogTag}] Confirmed {SettingsRelativePath} targets '{RequiredEnvironmentName}'.");
        }
    }
}
