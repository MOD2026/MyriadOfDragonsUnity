using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.Story;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CAMPAIGN LAUNCH POINTER-INPUT SMOKE TEST (acceptance proof) - the prior
    /// CampaignInputContractTests prove Button wiring/hierarchy structurally and drive clicks via
    /// Button.onClick.Invoke()/ExecuteEvents is NOT used there. This file instead loads the real
    /// Assets/Scenes/HomePagePresenter.unity scene in PlayMode (the same scene, same
    /// GameBootstrapLoader auto-boot, same HomePagePresenter.Start() a real player session uses),
    /// obtains a REAL EventSystem/GraphicRaycaster raycast at the Launch Battle button's actual
    /// screen position, and dispatches the click through ExecuteEvents.Execute(...,
    /// pointerClickHandler) - the same mechanism Unity's own input modules use - never
    /// button.onClick.Invoke() directly.
    ///
    /// Stage 1-1 is the only real, always-unlocked Chapter 1 stage a fresh profile can reach
    /// without any other setup, so its real stage-node Button is what's clicked to open the
    /// detail modal (its "1-1_pre" StoryDatabase sequence is removed via reflection before the
    /// Launch click so the raycast/click assertions land on AttemptLaunch directly rather than a
    /// story dialogue overlay - StoryDatabase.sequences is a private static Dictionary this
    /// project's own production code never exposes a test seam for, and the story-overlay system
    /// itself is explicitly out of this task's scope to touch or redesign).
    /// </summary>
    public class CampaignInputPointerSmokeTests
    {
        private string _scratchSaveDir;
        private string _removedSequenceId;
        private StorySequence _removedSequence;

        private static readonly string[] ApprovedStarterCollectionCardIds =
        {
            "warrior", "novice_knight", "goblin_caster",
            "cleric", "archer_elf", "fox", "bunny", "forest", "tribal_warrior", "undead_soldier",
        };

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MyriadOfDragonsPointerSmoke_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            RestoreStorySequenceIfRemoved();

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && System.IO.Directory.Exists(_scratchSaveDir))
            {
                System.IO.Directory.Delete(_scratchSaveDir, recursive: true);
            }
        }

        private static void SaveProfile(int stamina)
        {
            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(ApprovedStarterCollectionCardIds),
                activeDeckCardIds = new List<string>(ApprovedStarterCollectionCardIds),
                stamina = stamina,
            };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: expected the profile to save.");
            SaveSystem.ResetCurrentProfileForTests();
        }

        /// <summary>StoryDatabase.sequences is `private static readonly Dictionary&lt;...&gt;` -
        /// the field reference is readonly but its contents are not, so reflection can remove one
        /// entry for the duration of a single test without ever touching the production file.
        /// Restored in TearDown.</summary>
        private void RemoveStorySequence(string sequenceId)
        {
            FieldInfo field = typeof(StoryDatabase).GetField("sequences", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Setup: expected StoryDatabase.sequences to exist via reflection.");
            var dict = (Dictionary<string, StorySequence>)field.GetValue(null);
            if (dict.TryGetValue(sequenceId, out StorySequence seq))
            {
                _removedSequenceId = sequenceId;
                _removedSequence = seq;
                dict.Remove(sequenceId);
            }
        }

        private void RestoreStorySequenceIfRemoved()
        {
            if (_removedSequenceId == null) return;
            FieldInfo field = typeof(StoryDatabase).GetField("sequences", BindingFlags.NonPublic | BindingFlags.Static);
            var dict = (Dictionary<string, StorySequence>)field.GetValue(null);
            dict[_removedSequenceId] = _removedSequence;
            _removedSequenceId = null;
            _removedSequence = null;
        }

        /// <summary>GameBootstrapLoader's [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] fires
        /// once per Play Mode session at the very first scene load, not again for a scene loaded
        /// mid-session via EditorSceneManager.LoadSceneInPlayMode (as this test does to reach the
        /// real Assets/Scenes/HomePagePresenter.unity) - empirically it still spawns
        /// GameBootstrap for the newly-loaded scene, just with real (frame-based, not
        /// instantaneous) latency this in-editor scene load path apparently has. Waits for the
        /// real GameBootstrap.Instance to appear rather than proceeding against a null one, still
        /// never substituting a direct call for it.</summary>
        private static IEnumerator WaitForGameBootstrap(int maxFrames = 180)
        {
            int waited = 0;
            while (GameBootstrap.Instance == null && waited < maxFrames)
            {
                yield return null;
                waited++;
            }
            Debug.Log($"[CampaignInputPointerSmokeTests] GameBootstrap.Instance appeared after {waited} frame(s): {(GameBootstrap.Instance != null ? "yes" : "NO - timed out")}");
        }

        private static bool TryLoadHomeScenePlayMode(out string blockedReason)
        {
            blockedReason = null;
#if UNITY_EDITOR
            try
            {
                EditorSceneManager.LoadSceneInPlayMode(
                    "Assets/Scenes/HomePagePresenter.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
                return true;
            }
            catch (System.Exception e)
            {
                blockedReason = $"BLOCKED: PlayMode raycast unavailable in this environment - scene load failed: {e.GetType().Name}: {e.Message}";
                return false;
            }
#else
            blockedReason = "BLOCKED: PlayMode raycast unavailable in this environment - no Editor scene-loading API available.";
            return false;
#endif
        }

        [UnityTest]
        public IEnumerator RealPointerClick_OnLaunchBattleButton_ReachesAttemptLaunchExactlyOnce_ForAValidLaunch()
        {
            SaveProfile(stamina: 5);

            if (!TryLoadHomeScenePlayMode(out string blockedReason))
            {
                Assert.Fail(blockedReason);
                yield break;
            }
            yield return null;
            yield return null;

            Screen.SetResolution(1920, 1080, false);
            yield return null;
            Debug.Log($"[CampaignInputPointerSmokeTests] Screen: {Screen.width}x{Screen.height}");

            yield return WaitForGameBootstrap();
            Assert.IsNotNull(GameBootstrap.Instance, "Setup: expected the real GameBootstrap to auto-boot for the loaded scene.");

            HomePagePresenter home = Object.FindFirstObjectByType<HomePagePresenter>();
            Assert.IsNotNull(home, "Setup: expected the real scene to contain a HomePagePresenter.");

            home.OpenStoryCampaignForTests();
            yield return null;

            CampaignMapPresenter campaign = home.GetComponent<CampaignMapPresenter>();
            Assert.IsNotNull(campaign, "Setup: expected OpenStoryCampaignForTests to build a real CampaignMapPresenter.");
            bool nodeClicked = campaign.ClickStageNodeForTests("1-1");
            Assert.IsTrue(nodeClicked, "Requirement 2: expected the real Stage 1-1 node Button to exist and be clickable.");
            yield return null;

            RemoveStorySequence("1-1_pre");

            GameObject launchBtnGo = GameObject.Find("CampaignMapCanvas/StageDetailModal/DetailPanel/Btn_Launch");
            Assert.IsNotNull(launchBtnGo, "Setup: expected the real Launch Battle button to exist in the live hierarchy.");

            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            Assert.IsNotNull(eventSystem, "Setup: expected a real EventSystem to exist in the scene.");

            Canvas canvas = launchBtnGo.GetComponentInParent<Canvas>();
            GraphicRaycaster raycaster = canvas != null ? canvas.GetComponent<GraphicRaycaster>() : null;
            Assert.IsNotNull(raycaster, "Setup: expected a real GraphicRaycaster on the Campaign canvas.");

            RectTransform launchRect = launchBtnGo.GetComponent<RectTransform>();
            Vector2 buttonCentre = launchRect.position; // ScreenSpaceOverlay: world position IS screen position.

            var pointerData = new PointerEventData(eventSystem)
            {
                position = buttonCentre,
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
                eligibleForClick = true,
            };
            var results = new List<RaycastResult>();
            raycaster.Raycast(pointerData, results);

            Debug.Log($"[CampaignInputPointerSmokeTests] Raycast hit count: {results.Count}; top hit: {(results.Count > 0 ? results[0].gameObject.name : "<none>")}");

            if (results.Count == 0)
            {
                Assert.Fail("BLOCKED: PlayMode raycast unavailable in this environment - GraphicRaycaster returned zero hits at the Launch Battle button's screen position.");
            }

            Assert.AreEqual("Btn_Launch", results[0].gameObject.name,
                $"Requirement 5: the top actionable raycast hit at the Launch Battle button's centre must be Btn_Launch itself, not '{results[0].gameObject.name}'.");

            int stageBefore = SaveSystem.CurrentProfile.stamina;

            Button hitButton = results[0].gameObject.GetComponent<Button>();
            Debug.Log($"[CampaignInputPointerSmokeTests] hit==launchBtnGo: {results[0].gameObject == launchBtnGo}; Button found: {hitButton != null}; enabled: {hitButton?.enabled}; interactable: {hitButton?.interactable}; IsActive: {hitButton?.IsActive()}; onClick listeners: {hitButton?.onClick.GetPersistentEventCount()}");

            // Requirement 6: dispatch through the real event-system click path.
            GameObject clickHandledBy = ExecuteEvents.ExecuteHierarchy(results[0].gameObject, pointerData, ExecuteEvents.pointerClickHandler);
            Debug.Log($"[CampaignInputPointerSmokeTests] ExecuteHierarchy handled by: {(clickHandledBy != null ? clickHandledBy.name : "<null>")}");
            yield return null;

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.IsNotNull(bootstrap, "Setup: expected the real GameBootstrap to have auto-booted.");
            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "Requirement 3/7: a valid launch reached through the real pointer route must reveal Battle.");
            Assert.AreEqual(stageBefore - 1, SaveSystem.CurrentProfile.stamina,
                "Requirement 7: the real pointer click must invoke the launch callback exactly once (Stamina spent exactly once).");
        }

        [UnityTest]
        public IEnumerator RealPointerClick_OnLaunchBattleButton_ForABlockedStage_ShowsStatusThroughTheRealRoute()
        {
            SaveProfile(stamina: 0); // insufficient Stamina blocks Stage 1-1

            if (!TryLoadHomeScenePlayMode(out string blockedReason))
            {
                Assert.Fail(blockedReason);
                yield break;
            }
            yield return null;
            yield return null;

            Screen.SetResolution(1920, 1080, false);
            yield return null;

            yield return WaitForGameBootstrap();
            Assert.IsNotNull(GameBootstrap.Instance, "Setup: expected the real GameBootstrap to auto-boot for the loaded scene.");

            HomePagePresenter home = Object.FindFirstObjectByType<HomePagePresenter>();
            Assert.IsNotNull(home, "Setup: expected the real scene to contain a HomePagePresenter.");

            home.OpenStoryCampaignForTests();
            yield return null;

            CampaignMapPresenter campaign = home.GetComponent<CampaignMapPresenter>();
            bool nodeClicked = campaign.ClickStageNodeForTests("1-1");
            Assert.IsTrue(nodeClicked, "Requirement 2: expected the real Stage 1-1 node Button to exist and be clickable.");
            yield return null;

            RemoveStorySequence("1-1_pre");

            GameObject launchBtnGo = GameObject.Find("CampaignMapCanvas/StageDetailModal/DetailPanel/Btn_Launch");
            Assert.IsNotNull(launchBtnGo, "Setup: expected the real Launch Battle button to exist in the live hierarchy.");

            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            Canvas canvas = launchBtnGo.GetComponentInParent<Canvas>();
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();

            RectTransform launchRect = launchBtnGo.GetComponent<RectTransform>();
            var pointerData = new PointerEventData(eventSystem) { position = launchRect.position };
            var results = new List<RaycastResult>();
            raycaster.Raycast(pointerData, results);

            Debug.Log($"[CampaignInputPointerSmokeTests] Blocked-case raycast hit count: {results.Count}; top hit: {(results.Count > 0 ? results[0].gameObject.name : "<none>")}");

            if (results.Count == 0)
            {
                Assert.Fail("BLOCKED: PlayMode raycast unavailable in this environment - GraphicRaycaster returned zero hits at the Launch Battle button's screen position.");
            }
            Assert.AreEqual("Btn_Launch", results[0].gameObject.name,
                $"Requirement 5: the top actionable raycast hit must be Btn_Launch itself, not '{results[0].gameObject.name}'.");

            ExecuteEvents.Execute(results[0].gameObject, pointerData, ExecuteEvents.pointerClickHandler);
            yield return null;

            GameBootstrap bootstrap = GameBootstrap.Instance;
            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "Requirement 8: a blocked launch reached through the real pointer route must never reveal Battle.");
            Assert.AreEqual(HomePagePresenter.StaminaBlockedMessage, campaign.StatusTextForTests,
                "Requirement 8: the exact blocked status must appear on the existing Campaign status text through the real pointer route.");
        }
    }
}
