using System.Collections.Generic;
using System.Reflection;
using MyriadOfDragons.Story;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CR-STORY-SCALER-REGRESSION-007. Neither existing StoryOverlayPresenter test
    /// (CampaignLaunchFeedbackContractTests.cs, ChapterOnePostVictoryStoryTests.cs) touches
    /// BuildUI()/its CanvasScaler at all - both only ever exercise LastPlayedSequenceIdForTests/
    /// ResetTestHooks(). No test covers the actual 1920x1080 landscape lock this presenter's
    /// canvas depends on.
    ///
    /// PlaySequence (the only public entry point) explicitly early-returns before calling BuildUI()
    /// when !Application.isPlaying - there is no public seam that reaches the CanvasScaler from
    /// EditMode. BuildUI() itself has no Play-mode dependency in its own body (pure GameObject/
    /// Component construction, no coroutine, no Update-loop read) - only PlaySequence's own wrapper
    /// gates it. Reflection is used ONLY to invoke that one private method directly, bypassing
    /// PlaySequence's unrelated Play-mode gate, not to reach into private state - matching the
    /// existing NonPublic-BindingFlags precedent already used elsewhere in this suite
    /// (TutorialGuidanceTests.cs). The resulting "StoryCanvas" GameObject and its CanvasScaler are
    /// then read via the real, named scene hierarchy, not further reflection.
    /// </summary>
    public class StoryOverlayScalerRegressionTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp() => StoryOverlayPresenter.ResetTestHooks();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            StoryOverlayPresenter.ResetTestHooks();
        }

        private CanvasScaler BuildStoryOverlayAndGetScaler()
        {
            var host = new GameObject("StoryOverlayScalerTestHost");
            _spawned.Add(host);
            var presenter = host.AddComponent<StoryOverlayPresenter>();

            MethodInfo buildUI = typeof(StoryOverlayPresenter).GetMethod("BuildUI", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(buildUI, "STATE UNREACHED: StoryOverlayPresenter.BuildUI was not found - the presenter's shape changed.");
            buildUI.Invoke(presenter, null);

            Transform storyCanvas = host.transform.Find("StoryCanvas");
            Assert.IsNotNull(storyCanvas, "STATE UNREACHED: BuildUI did not create the real 'StoryCanvas' GameObject.");
            CanvasScaler scaler = storyCanvas.GetComponent<CanvasScaler>();
            Assert.IsNotNull(scaler, "STATE UNREACHED: StoryCanvas carries no CanvasScaler.");
            return scaler;
        }

        [Test]
        public void BuildUI_CreatesACanvasScaler_OnTheStoryCanvas()
        {
            CanvasScaler scaler = BuildStoryOverlayAndGetScaler();
            Assert.IsNotNull(scaler);
        }

        [Test]
        public void BuildUI_UsesScaleWithScreenSize()
        {
            CanvasScaler scaler = BuildStoryOverlayAndGetScaler();
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode,
                "Locked: this game is landscape 1920x1080 - a fixed/constant-size scaler would not track the real device viewport.");
        }

        [Test]
        public void BuildUI_ReferenceResolutionIs1920x1080()
        {
            CanvasScaler scaler = BuildStoryOverlayAndGetScaler();
            Assert.AreEqual(1920f, scaler.referenceResolution.x, 0.01f,
                "Locked: this game is landscape 1920x1080, never portrait.");
            Assert.AreEqual(1080f, scaler.referenceResolution.y, 0.01f);
        }

        [Test]
        public void BuildUI_MatchWidthOrHeight_MatchesTheSharedProjectConstant()
        {
            CanvasScaler scaler = BuildStoryOverlayAndGetScaler();
            Assert.AreEqual(UISharedFoundation.MatchWidthOrHeight, scaler.matchWidthOrHeight, 0.0001f,
                "The story overlay's scaler must track the same project-wide match value every other " +
                "screen uses (UISharedFoundation.MatchWidthOrHeight), not a locally hardcoded one that " +
                "could silently drift from it.");
        }

        /// <summary>PRODUCTIVE CODING TASK - Tutorial/Story Revamp V2 binding. Proves BuildUI()
        /// loads the exact approved sprite, not just "some" sprite - compares the presenter's
        /// loaded Sprite object against a fresh, independent Resources.Load of the same path, and
        /// separately confirms that path is the one the packaging commits actually delivered
        /// (Assets/Resources/UI/RevampV2Approved/TutorialStoryOverlay/tutorial_story_overlay_v2.png).</summary>
        [Test]
        public void BuildUI_BindsTheApprovedTutorialStoryBackdropSprite()
        {
            var host = new GameObject("StoryOverlayBackdropBindingTestHost");
            _spawned.Add(host);
            var presenter = host.AddComponent<StoryOverlayPresenter>();

            MethodInfo buildUI = typeof(StoryOverlayPresenter).GetMethod("BuildUI", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(buildUI, "STATE UNREACHED: StoryOverlayPresenter.BuildUI was not found - the presenter's shape changed.");
            buildUI.Invoke(presenter, null);

            Sprite boundSprite = presenter.BackdropSpriteForTests;
            Assert.IsNotNull(boundSprite,
                "StoryBackdrop must load the approved Revamp V2 sprite, not fall back to the plain " +
                "dim color - if this fails, the asset is missing from Resources or was moved.");

            Sprite independentLoad = Resources.Load<Sprite>(StoryOverlayPresenter.ApprovedBackdropResourcePath);
            Assert.IsNotNull(independentLoad, "Setup: the approved backdrop must exist at the exact packaged path.");
            Assert.AreSame(independentLoad, boundSprite,
                "BuildUI must bind the exact approved sprite object from ApprovedBackdropResourcePath - " +
                "not a different sprite that happens to also be non-null.");
            Assert.AreEqual("tutorial_story_overlay_v2", boundSprite.texture.name,
                "Bound sprite's source texture must be the exact approved file, by name, not a same-shaped substitute.");

            // Preservation: tutorial flow, text, buttons, navigation, and state logic are
            // untouched by this binding - re-confirm the rest of the built hierarchy is exactly
            // as before (dialogue panel, portraits, skip button all still present).
            Transform storyCanvas = host.transform.Find("StoryCanvas");
            Assert.IsNotNull(storyCanvas.Find("DialoguePanel"), "DialoguePanel must be unchanged by this binding.");
            Assert.IsNotNull(storyCanvas.Find("DialoguePanel/SpeakerNameText"), "Speaker name text must be unchanged.");
            Assert.IsNotNull(storyCanvas.Find("DialoguePanel/DialogueContentText"), "Dialogue body text must be unchanged.");
            Assert.IsNotNull(storyCanvas.Find("Btn_Skip"), "Skip button/navigation must be unchanged.");
            Assert.IsNotNull(storyCanvas.Find("Portrait_Left"), "Left portrait slot must be unchanged.");
            Assert.IsNotNull(storyCanvas.Find("Portrait_Right"), "Right portrait slot must be unchanged.");
        }
    }
}
