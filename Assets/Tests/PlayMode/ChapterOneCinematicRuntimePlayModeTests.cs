using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Video;
using MyriadOfDragons.UI;

namespace MyriadOfDragons.Tests.PlayMode
{
    public sealed class ChapterOneCinematicRuntimePlayModeTests
    {
        private bool _reduceMotionBefore;

        [SetUp]
        public void SetUp()
        {
            _reduceMotionBefore = MotionPolicy.ReduceMotion;
            MotionPolicy.ReduceMotion = false;
        }

        [TearDown]
        public void TearDown()
        {
            MotionPolicy.ReduceMotion = _reduceMotionBefore;
            if (GameBootstrap.Instance != null)
                GameBootstrap.Instance.ReturnToCityForTests();
        }

        [UnityTest]
        public IEnumerator OpeningStartsFromCanonicalVideo_ThenHandsOffToLogoAndCompletesOnce()
        {
            yield return WaitForBootstrap();
            GameBootstrap bootstrap = GameBootstrap.Instance;
            bootstrap.SetBattleCanvasVisible(true);
            bootstrap.StartApprovedTutorialBattle();

            VideoPlayer player = null;
            int frames = 0;
            while (player == null && frames++ < 180)
            {
                yield return null;
                GameObject overlay = GameObject.Find("Chapter1Cinematic");
                if (overlay != null) player = overlay.GetComponent<VideoPlayer>();
            }

            Assert.IsNotNull(player, "The normal-motion opening must create a VideoPlayer overlay.");
            Assert.AreEqual(GameBootstrap.OpeningGameplayVideoResourcePath,
                bootstrap.OpeningVideoResourcePathForTests);
            Assert.IsNotNull(Resources.Load<VideoClip>(GameBootstrap.OpeningGameplayVideoResourcePath));

            bootstrap.CompleteOpeningVideoForTests();
            Assert.IsTrue(bootstrap.OpeningLogoEndCardActiveForTests,
                "The logo end card must begin only after the real video completion callback.");
            Assert.IsNotNull(GameObject.Find("Chapter1LogoEndCard"));

            bootstrap.CompleteOpeningLogoEndCardForTests();
            bootstrap.CompleteOpeningLogoEndCardForTests();
            Assert.IsFalse(bootstrap.CinematicActiveForTests);
            Assert.IsNull(GameObject.Find("Chapter1Cinematic"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SkipReplayAndTeardownSuppressLateVideoCallbacks()
        {
            yield return WaitForBootstrap();
            GameBootstrap bootstrap = GameBootstrap.Instance;
            bootstrap.SetBattleCanvasVisible(true);
            bootstrap.StartApprovedTutorialBattle();
            yield return null;

            bootstrap.SkipCinematicForTests();
            bootstrap.CompleteOpeningVideoForTests();
            Assert.IsFalse(bootstrap.CinematicActiveForTests);
            Assert.IsNull(GameObject.Find("Chapter1Cinematic"));

            bootstrap.StartApprovedTutorialBattle();
            yield return null;
            Assert.AreEqual(GameBootstrap.OpeningGameplayVideoResourcePath,
                bootstrap.OpeningVideoResourcePathForTests);
            bootstrap.ReturnToCityForTests();
            bootstrap.CompleteOpeningVideoForTests();
            Assert.IsFalse(bootstrap.CinematicActiveForTests);
            Assert.IsFalse(bootstrap.OpeningLogoEndCardActiveForTests);
            Assert.IsNull(GameObject.Find("Chapter1Cinematic"));
        }

        [UnityTest]
        public IEnumerator ReducedMotionSkipsVideoAndLogoImmediately()
        {
            yield return WaitForBootstrap();
            GameBootstrap bootstrap = GameBootstrap.Instance;
            bootstrap.SetBattleCanvasVisible(true);
            MotionPolicy.ReduceMotion = true;
            bootstrap.StartApprovedTutorialBattle();
            yield return null;

            Assert.IsFalse(bootstrap.CinematicActiveForTests);
            Assert.IsFalse(bootstrap.OpeningLogoEndCardActiveForTests);
            Assert.IsNull(GameObject.Find("Chapter1Cinematic"));
        }

        private static IEnumerator WaitForBootstrap()
        {
            int frames = 0;
            while (GameBootstrap.Instance == null && frames++ < 180)
                yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "GameBootstrap did not auto-boot.");
        }
    }
}
