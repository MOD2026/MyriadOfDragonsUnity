using MyriadOfDragons.Battle;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Combat Resolution Stage - the acceptance checks that are actually testable in EditMode.
    ///
    /// Pixel fidelity is not asserted here; particle look is a review question. What IS asserted is
    /// the set of properties whose failure is invisible until it costs something: raycasts leaking
    /// onto the spell rail, the old text log surviving, and numbers displayed without a sign.
    /// </summary>
    public class CombatResolutionStageTests
    {
        private GameObject _host;
        private GameObject _parent;
        private CombatResolutionStage _stage;

        [SetUp]
        public void SetUp()
        {
            _parent = new GameObject("StageParent", typeof(RectTransform));
            _host = new GameObject("StageHost");
            _stage = _host.AddComponent<CombatResolutionStage>();
            _stage.Initialize(_parent.GetComponent<RectTransform>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_parent != null) Object.DestroyImmediate(_parent);
        }

        private static CombatResolutionEvent Clash(int value, CombatResolutionSide side) =>
            new CombatResolutionEvent(CombatResolutionEventType.ClashResolved, side, 1,
                signedValue: value, lane: Lane.Front, hasLane: true);

        [Test]
        public void NoStageChild_ReceivesRaycasts()
        {
            // THE ACCEPTANCE CHECK THAT MATTERS MOST. The spell rail sits directly beneath this
            // stage. One child left raycastable swallows taps on a control the player needs, and
            // nothing looks wrong - it just stops working. Guild Hall shipped that exact bug.
            foreach (Graphic graphic in _stage.RootForTests.GetComponentsInChildren<Graphic>(true))
            {
                Assert.IsFalse(graphic.raycastTarget,
                    "'" + graphic.name + "' would steal taps from the spell rail underneath.");
            }
        }

        [Test]
        public void TheStageIsClipped_SoParticlesCannotEscapeIntoTheBoard()
        {
            // The doc requires all particles clipped to the stage rect - without a mask, an effect
            // can draw over board cards and the hand dock.
            Assert.IsNotNull(_stage.RootForTests.GetComponent<RectMask2D>(),
                "Stage must clip its own contents.");
        }

        [Test]
        public void TheStageContainsNoScrollingTextLog()
        {
            // "The former text-log region contains no scrolling sentences or event history."
            // The only Text permitted is the signed numeric result.
            Text[] texts = _stage.RootForTests.GetComponentsInChildren<Text>(true);
            Assert.AreEqual(1, texts.Length,
                "Exactly one Text - the numeric result. Anything else is a log creeping back.");
            Assert.AreEqual("SignedNumericValue", texts[0].gameObject.name);
        }

        [Test]
        public void TheResultNumber_MeetsTheLegibilityFloor()
        {
            // Doc floor: 32 px minimum at 1920x1080. The old log ran at 15 px, which would be
            // unreadable against a lit aperture.
            Text value = _stage.RootForTests.GetComponentInChildren<Text>(true);
            Assert.GreaterOrEqual(value.fontSize, 32);
            Assert.AreEqual(FontStyle.Bold, value.fontStyle);
        }

        [Test]
        public void APresentedBeat_ShowsAnEXPLICITLYSignedNumber()
        {
            // "6" and "-6" are indistinguishable at a glance. The doc requires the number to agree
            // with the icon and motion direction, which means the sign must be visible.
            _stage.Enqueue(Clash(-6, CombatResolutionSide.Enemy));
            _stage.Tick(0.016f);

            Assert.AreEqual("-6", _stage.ResultTextForTests);

            _stage.ClearAll();
            _stage.Enqueue(Clash(4, CombatResolutionSide.Player));
            _stage.Tick(0.016f);
            Assert.AreEqual("+4", _stage.ResultTextForTests,
                "A positive result must carry its + so it cannot read as damage.");
        }

        [Test]
        public void AZeroResult_ShowsZero_RatherThanBlank()
        {
            // "Blocked/no damage: shield impact and 0 numeric result." A blank would read as the
            // stage failing rather than as a blocked hit.
            _stage.Enqueue(Clash(0, CombatResolutionSide.Player));
            _stage.Tick(0.016f);

            Assert.AreEqual("0", _stage.ResultTextForTests);
        }

        [Test]
        public void ABeatHolds_ThenReleases_SoResultsAreReadable()
        {
            // The hold is what makes a number readable at all. Releasing immediately would flash
            // results faster than a player can register them.
            _stage.Enqueue(Clash(-3, CombatResolutionSide.Enemy));
            _stage.Tick(0.016f);
            Assert.IsTrue(_stage.QueueForTests.HasActive);

            _stage.Tick(CombatResolutionStage.MinimumResultHoldSeconds);
            Assert.IsTrue(_stage.QueueForTests.HasActive,
                "Must still be holding at the legibility floor.");

            _stage.Tick(CombatResolutionStage.OutcomeBandHoldSeconds);
            Assert.IsFalse(_stage.QueueForTests.HasActive, "Then it releases for the next beat.");
        }

        [Test]
        public void TheHoldNeverDropsBelowTheLegibilityFloor()
        {
            // Guards a future timing tweak: someone shortening the band for pace must not silently
            // take it under the doc's 650 ms minimum.
            Assert.GreaterOrEqual(CombatResolutionStage.OutcomeBandHoldSeconds,
                CombatResolutionStage.MinimumResultHoldSeconds);
        }

        [Test]
        public void ClearAll_EmptiesTheQueueAndTheDisplayedResult()
        {
            // Scene exit / result transition / replay skip. A stale number left on the stage would
            // reappear over the next match.
            _stage.Enqueue(Clash(-9, CombatResolutionSide.Enemy));
            _stage.Tick(0.016f);
            Assert.AreEqual("-9", _stage.ResultTextForTests);

            _stage.ClearAll();

            Assert.AreEqual(string.Empty, _stage.ResultTextForTests);
            Assert.AreEqual(0, _stage.QueueForTests.PendingCount);
        }

        [Test]
        public void PresentationNeverBlocks_HoweverFarBehindItFalls()
        {
            // The rule the whole design rests on: combat must never wait for animation. Flooding
            // the stage must not throw, stall, or lose the critical beats.
            for (int i = 0; i < 200; i++) _stage.Enqueue(Clash(-1, CombatResolutionSide.Player));
            _stage.Enqueue(new CombatResolutionEvent(
                CombatResolutionEventType.AvatarHealthChanged, CombatResolutionSide.Player, 99,
                signedValue: -12));

            Assert.DoesNotThrow(() => _stage.Tick(0.016f));
            Assert.LessOrEqual(_stage.QueueForTests.VisibleQueueIndicators, 3,
                "The indicator advertises at most active + 2, however deep the backlog.");
        }
    }
}
