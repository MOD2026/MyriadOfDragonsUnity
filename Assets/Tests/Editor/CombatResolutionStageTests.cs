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

        private static CombatResolutionEvent Beat(
            CombatResolutionEventType type, CombatResolutionSide side = CombatResolutionSide.Player,
            bool hasLane = false, int remainingSlots = -1) =>
            new CombatResolutionEvent(type, side, 1, signedValue: -3,
                lane: Lane.Front, hasLane: hasLane, remainingSlots: remainingSlots);

        private (Color color, Vector2 anchor) PresentAndCapture(CombatResolutionEvent beat)
        {
            _stage.ClearAll();
            _stage.Enqueue(beat);
            _stage.Tick(0.016f);
            return (_stage.ResultIconColorForTests, _stage.ResultIconAnchorMinForTests);
        }

        [Test]
        public void EveryEventType_IsVisuallyDistinguishable_WithoutReadingProse()
        {
            // THE ACCEPTANCE CHECK THIS WAS FAILING. The stage originally had no branching on beat
            // type at all - clash, spell, defeat and avatar-health rendered identically and only the
            // side colour changed. A player could not tell a card dying from their Avatar being hit.
            //
            // Compares the full (colour, shape) pair: the doc forbids colour alone carrying meaning,
            // so two types sharing a tint must still differ in aspect.
            var seen = new System.Collections.Generic.Dictionary<string, CombatResolutionEventType>();

            foreach (CombatResolutionEventType type in new[]
                     {
                         CombatResolutionEventType.ClashResolved,
                         CombatResolutionEventType.SpellResolved,
                         CombatResolutionEventType.CardDefeated,
                         CombatResolutionEventType.LaneStateChanged,
                     })
            {
                (Color color, Vector2 anchor) = PresentAndCapture(Beat(type));
                string signature = color + "|" + anchor;

                Assert.IsFalse(seen.ContainsKey(signature),
                    type + " renders identically to " +
                    (seen.ContainsKey(signature) ? seen[signature].ToString() : "another type") +
                    " - indistinguishable without prose.");
                seen[signature] = type;
            }
        }

        [Test]
        public void ADefeatBeat_DimsRatherThanBrightens_SoLossReadsAsLoss()
        {
            // Direction of change matters, not just difference: a defeat rendered brighter than a
            // clash would read as a win.
            (Color clash, _) = PresentAndCapture(Beat(CombatResolutionEventType.ClashResolved));
            (Color defeat, _) = PresentAndCapture(Beat(CombatResolutionEventType.CardDefeated));

            Assert.Less(defeat.r + defeat.g + defeat.b, clash.r + clash.g + clash.b,
                "A defeat must be visually dimmer than an ordinary clash.");
        }

        [Test]
        public void LanePips_ShowOnlyForLaneBearingBeats()
        {
            // Showing pips on an avatar beat would make it look like a lane event - the pips are
            // what carry "this happened in a lane".
            _stage.ClearAll();
            _stage.Enqueue(Beat(CombatResolutionEventType.AvatarHealthChanged, hasLane: false));
            _stage.Tick(0.016f);
            Assert.IsFalse(_stage.LanePipsVisibleForTests, "An avatar beat has no lane.");

            _stage.ClearAll();
            _stage.Enqueue(Beat(CombatResolutionEventType.ClashResolved, hasLane: true));
            _stage.Tick(0.016f);
            Assert.IsTrue(_stage.LanePipsVisibleForTests, "A lane clash must show its slot pips.");
        }

        [Test]
        public void TheAvatarStrikeFlipbook_IsUsedByThatBeatALONE()
        {
            // The doc forbids reusing the bespoke sheet for any other effect. Enabling it on an
            // ordinary beat would spend a signature moment on a routine one.
            _stage.ClearAll();
            _stage.Enqueue(Beat(CombatResolutionEventType.AvatarStrikeResolved));
            _stage.Tick(0.016f);
            Assert.IsTrue(_stage.StrikeLayerEnabledForTests);

            _stage.ClearAll();
            _stage.Enqueue(Beat(CombatResolutionEventType.ClashResolved));
            _stage.Tick(0.016f);
            Assert.IsFalse(_stage.StrikeLayerEnabledForTests,
                "An ordinary clash must never trigger the AvatarStrike flipbook.");
        }

        [Test]
        public void UnknownSlotState_LeavesPipsLIT_RatherThanBlackingThemOut()
        {
            // RemainingSlots is -1 for "this beat says nothing about slot state". Treating that as
            // zero would extinguish the pips on every ordinary clash.
            _stage.ClearAll();
            _stage.Enqueue(Beat(CombatResolutionEventType.ClashResolved, hasLane: true, remainingSlots: -1));
            _stage.Tick(0.016f);

            Assert.IsTrue(_stage.LanePipsVisibleForTests);
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
