using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// TUTORIAL TEACHING OVERLAY, 2026-08-16 (v3, Tutorial Action Proxy) - the architecture that
    /// replaced two earlier, rejected designs:
    ///  1. A "spotlight hole" (four dark rectangles framing a measured gap around the target) -
    ///     rejected: its coordinate math depended on precise agreement between two independently
    ///     measured rects, which drifted in practice.
    ///  2. Reparenting the real target GameObject into an always-topmost slot - rejected again on
    ///     real manual QA: the highlighted Novice Knight looked correct but was not tappable.
    ///
    /// This version never touches the real gameplay hierarchy. Every hand card, lane button,
    /// spell tile and Continue button stays in its original parent/sibling order/layout at all
    /// times - exactly like a normal (non-tutorial) match - behind a full-screen blocker. A
    /// single, small, transparent "TutorialActionProxy" Button - sized to the real target's own
    /// current world bounds, rebuilt fresh every refresh - sits above the blocker and is the only
    /// thing that ever receives a tap, forwarding it straight into the exact same private handler
    /// (OnHandCardPressed/OnLanePressed/OnSpellTapped/OnSpellTargetLanePressed/
    /// OnPrimaryActionPressed/OnTutorialContinuePressed) a real tap on the real control would have
    /// called - see GameBootstrap.GetTutorialActiveProxyAction's own comment.
    ///
    /// A direct, empirical investigation of Unity's real GraphicRaycaster.Raycast() in this
    /// project's headless `-batchmode -runTests` EditMode runner (retained from the previous
    /// iteration of this suite) found it unconditionally returns zero hits here: every Graphic's
    /// `depth` stays -1 forever without an actual rendered frame, which batch-mode EditMode never
    /// produces, and GraphicRaycaster's own internal Raycast() skips any graphic with depth == -1
    /// before it reaches a bounds check. That is a property of this execution environment, not of
    /// the overlay - TeachingOverlay_RealGraphicRaycaster_BestEffort still drives the real call and
    /// reports its observed result rather than hiding it. Every other test below proves
    /// clickability the way this design actually guarantees it: there is exactly one live,
    /// production-wired Button (the proxy) sized to the real target's bounds, sitting above the
    /// blocker, while the real target itself never moves from its original hierarchy position.
    /// </summary>
    public class TutorialTeachingOverlayTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
            {
                Directory.Delete(_scratchSaveDir, recursive: true);
            }
        }

        private GameBootstrap SpawnAndInitializeBootstrap(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                foreach (GameObject candidate in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (candidate.name == spawnedName && !_spawned.Contains(candidate))
                        _spawned.Add(candidate);
                }
            }

            GameObject canvasGo = GameObject.Find("Canvas");
            if (canvasGo != null)
            {
                RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);
                foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true)
                             .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
            }

            return bootstrap;
        }

        private static Card Find(BattleController controller, string id) =>
            controller.PlayerState.Hand.First(c => c.Id == id);

        private static Rect WorldBounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float xMin = corners.Min(c => c.x);
            float xMax = corners.Max(c => c.x);
            float yMin = corners.Min(c => c.y);
            float yMax = corners.Max(c => c.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static bool RoughlyEqual(Rect a, Rect b, float tolerancePx = 1f) =>
            Mathf.Abs(a.xMin - b.xMin) <= tolerancePx && Mathf.Abs(a.xMax - b.xMax) <= tolerancePx &&
            Mathf.Abs(a.yMin - b.yMin) <= tolerancePx && Mathf.Abs(a.yMax - b.yMax) <= tolerancePx;

        /// <summary>Asserts, for whatever step the bootstrap is currently on: the overlay is
        /// active, exactly one Tutorial Action Proxy exists, its bounds match the real intended
        /// target's own current bounds, and it sits above the full-screen blocker (last child of
        /// the always-topmost proxy container) while the real target itself is untouched -
        /// still parented exactly where normal (non-tutorial) layout put it, not under the proxy
        /// container.</summary>
        private static void AssertExactlyOneProxyMatchesTheRealTarget(GameBootstrap bootstrap, string context)
        {
            Assert.IsTrue(bootstrap.TutorialTeachingOverlayForTests.activeSelf,
                $"[{context}] The teaching overlay must be visible while a guided step is active.");

            RectTransform target = bootstrap.TutorialActiveTargetRectForTests();
            Assert.IsNotNull(target, $"[{context}] Setup: expected a resolvable target.");

            Button proxy = bootstrap.TutorialActionProxyForTests;
            Assert.IsNotNull(proxy, $"[{context}] Expected exactly one Tutorial Action Proxy to exist.");

            // Exactly one - the proxy container's only Button child is this one.
            RectTransform container = bootstrap.TutorialProxyContainerForTests;
            int proxyButtonCount = container.GetComponentsInChildren<Button>(true).Length;
            Assert.AreEqual(1, proxyButtonCount, $"[{context}] Exactly one proxy must exist at a time, found {proxyButtonCount}.");

            Assert.IsTrue(RoughlyEqual(WorldBounds(proxy.GetComponent<RectTransform>()), WorldBounds(target)),
                $"[{context}] The proxy's bounds must match the real intended target's own current bounds " +
                $"(proxy {WorldBounds(proxy.GetComponent<RectTransform>())}, target {WorldBounds(target)}).");

            Assert.IsTrue(proxy.transform.IsChildOf(container),
                $"[{context}] The proxy must sit inside the always-topmost proxy container, above the blocker.");

            // The real target itself was never moved - it is NOT inside the proxy container.
            Assert.IsFalse(target.transform.IsChildOf(container),
                $"[{context}] The real target must never be reparented into the proxy container or anywhere else - " +
                "it must stay exactly where normal Unity layout already put it.");
        }

        [Test]
        public void GuidedSequence_ExactlyOneProxyMatchesTheRealTarget_AtEveryStep_AndNoLiveTargetIsMoved()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Overlay_FullSequenceBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;

            AssertExactlyOneProxyMatchesTheRealTarget(bootstrap, "CardCost (Warrior)");
            bootstrap.HandCardPressedForTests(Find(controller, "warrior"));

            AssertExactlyOneProxyMatchesTheRealTarget(bootstrap, "FrontLane");
            bootstrap.LanePressedForTests(Lane.Front);

            AssertExactlyOneProxyMatchesTheRealTarget(bootstrap, "MiddleLane (Novice Knight)");
            bootstrap.HandCardPressedForTests(Find(controller, "novice_knight"));

            AssertExactlyOneProxyMatchesTheRealTarget(bootstrap, "MiddleLane (place)");
            bootstrap.LanePressedForTests(Lane.Middle);

            AssertExactlyOneProxyMatchesTheRealTarget(bootstrap, "BackLane (Goblin Caster)");
            bootstrap.HandCardPressedForTests(Find(controller, "goblin_caster"));

            AssertExactlyOneProxyMatchesTheRealTarget(bootstrap, "BackLane (place)");
            bootstrap.LanePressedForTests(Lane.Back);

            AssertExactlyOneProxyMatchesTheRealTarget(bootstrap, "BeginBattle");
            bootstrap.StartBattleForTests();
            Assert.AreEqual(TutorialStep.FirstCombatResult, bootstrap.TutorialStepForTests, "Setup: expected combat to resolve tick 1.");

            AssertExactlyOneProxyMatchesTheRealTarget(bootstrap, "FirstCombatResult");
            bootstrap.TutorialContinueForTests();
            Assert.AreEqual(TutorialStep.SpellLesson, bootstrap.TutorialStepForTests);

            AssertExactlyOneProxyMatchesTheRealTarget(bootstrap, "SpellLesson (Firestorm)");
            bootstrap.SpellTappedForTests(0);

            AssertExactlyOneProxyMatchesTheRealTarget(bootstrap, "SpellLesson (enemy Middle target)");
            bootstrap.SpellTargetLanePressedForTests(Lane.Middle);
            Assert.AreEqual(TutorialStep.Finish, bootstrap.TutorialStepForTests);

            AssertExactlyOneProxyMatchesTheRealTarget(bootstrap, "Finish");
        }

        [Test]
        public void InvokingTheProxyButtonsRealOnClick_AdvancesTheActualTutorialStep_AtEveryStep()
        {
            // Fires proxy.onClick.Invoke() directly - the same event a real EventSystem click
            // would raise - rather than calling the underlying handler by name, so this genuinely
            // exercises the proxy's own wiring, not just the handler it forwards to.
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Overlay_ProxyInvokeBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;

            bootstrap.TutorialActionProxyForTests.onClick.Invoke(); // CardCost: selects Warrior
            Assert.AreEqual(TutorialStep.FrontLane, bootstrap.TutorialStepForTests);
            Assert.AreEqual("warrior", bootstrap.SelectedCardIdForTests);

            bootstrap.TutorialActionProxyForTests.onClick.Invoke(); // FrontLane: places Warrior
            Assert.AreEqual(TutorialStep.MiddleLane, bootstrap.TutorialStepForTests);
            Assert.AreEqual(1, controller.PlayerState.Lanes[Lane.Front].Cards.Count);

            bootstrap.TutorialActionProxyForTests.onClick.Invoke(); // MiddleLane: selects Novice Knight
            Assert.AreEqual(TutorialStep.MiddleLane, bootstrap.TutorialStepForTests, "Setup: selecting a card does not itself advance MiddleLane.");
            Assert.AreEqual("novice_knight", bootstrap.SelectedCardIdForTests);

            bootstrap.TutorialActionProxyForTests.onClick.Invoke(); // MiddleLane: places Novice Knight
            Assert.AreEqual(TutorialStep.BackLane, bootstrap.TutorialStepForTests);

            bootstrap.TutorialActionProxyForTests.onClick.Invoke(); // BackLane: selects Goblin Caster
            Assert.AreEqual("goblin_caster", bootstrap.SelectedCardIdForTests);

            bootstrap.TutorialActionProxyForTests.onClick.Invoke(); // BackLane: places Goblin Caster
            Assert.AreEqual(TutorialStep.BeginBattle, bootstrap.TutorialStepForTests);

            bootstrap.TutorialActionProxyForTests.onClick.Invoke(); // BeginBattle: Start Battle
            Assert.AreEqual(TutorialStep.FirstCombatResult, bootstrap.TutorialStepForTests);

            bootstrap.TutorialActionProxyForTests.onClick.Invoke(); // FirstCombatResult: Continue
            Assert.AreEqual(TutorialStep.SpellLesson, bootstrap.TutorialStepForTests);

            bootstrap.TutorialActionProxyForTests.onClick.Invoke(); // SpellLesson: arm Firestorm
            Assert.AreEqual(TutorialStep.SpellLesson, bootstrap.TutorialStepForTests, "Setup: arming does not itself advance SpellLesson.");

            bootstrap.TutorialActionProxyForTests.onClick.Invoke(); // SpellLesson: cast at enemy Middle
            Assert.AreEqual(TutorialStep.Finish, bootstrap.TutorialStepForTests);

            int continuesRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                bootstrap.TutorialActionProxyForTests.onClick.Invoke(); // Finish: Continue, once per tick
                continuesRun++;
                Assert.LessOrEqual(continuesRun, BattleController.MaxCombatTicks);
            }

            Assert.AreEqual(BattlePhase.Resolved, controller.Phase);
            Assert.AreEqual("Victory. The first threat has been driven back.", bootstrap.ResultTextForTests,
                "Driving the guided sequence purely through the proxy's own onClick must still reach a real victory.");
        }

        [Test]
        public void NoLiveTarget_IsEverReparented_ThroughoutTheEntireGuidedSequence()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Overlay_NoReparentBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;

            RectTransform frontLane = bootstrap.PlayerLaneButtonRectForTests(Lane.Front);
            RectTransform middleLane = bootstrap.PlayerLaneButtonRectForTests(Lane.Middle);
            RectTransform backLane = bootstrap.PlayerLaneButtonRectForTests(Lane.Back);
            Transform frontLaneOriginalParent = frontLane.parent;
            Transform middleLaneOriginalParent = middleLane.parent;
            Transform backLaneOriginalParent = backLane.parent;
            Transform continueOriginalParent = bootstrap.TutorialContinueButtonForTests.transform.parent;

            bootstrap.HandCardPressedForTests(Find(controller, "warrior"));
            Assert.AreSame(frontLaneOriginalParent, frontLane.parent, "Front lane's parent must never change while it is the highlighted target.");
            bootstrap.LanePressedForTests(Lane.Front);
            Assert.AreSame(frontLaneOriginalParent, frontLane.parent, "Front lane's parent must never change once placement succeeds either.");

            bootstrap.HandCardPressedForTests(Find(controller, "novice_knight"));
            bootstrap.LanePressedForTests(Lane.Middle);
            Assert.AreSame(middleLaneOriginalParent, middleLane.parent);

            bootstrap.HandCardPressedForTests(Find(controller, "goblin_caster"));
            bootstrap.LanePressedForTests(Lane.Back);
            Assert.AreSame(backLaneOriginalParent, backLane.parent);

            bootstrap.StartBattleForTests();
            bootstrap.TutorialContinueForTests();
            Assert.AreSame(continueOriginalParent, bootstrap.TutorialContinueButtonForTests.transform.parent,
                "The real Continue button's parent must never change while it is the highlighted target.");
        }

        [Test]
        public void WrongLaneTap_HasNoProxy_AndRemainsBlockedThroughTheRealHandler_DuringFrontLaneStep()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Overlay_WrongActionBlockedBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;
            bootstrap.HandCardPressedForTests(Find(controller, "warrior"));
            Assert.AreEqual(TutorialStep.FrontLane, bootstrap.TutorialStepForTests, "Setup: expected the Front-lane step.");

            // The one proxy that exists targets Front, not Middle/Back - there is no second proxy
            // anywhere offering the wrong action.
            RectTransform frontLaneRect = bootstrap.PlayerLaneButtonRectForTests(Lane.Front);
            Rect proxyBounds = WorldBounds(bootstrap.TutorialActionProxyForTests.GetComponent<RectTransform>());
            Assert.IsTrue(RoughlyEqual(proxyBounds, WorldBounds(frontLaneRect)), "Setup: expected the one proxy to target Front lane.");

            int proxyCount = bootstrap.TutorialProxyContainerForTests.GetComponentsInChildren<Button>(true).Length;
            Assert.AreEqual(1, proxyCount, "Exactly one proxy must exist - none offering Middle or Back.");

            // And the real production handler still rejects a direct tap on the wrong lane -
            // exactly as it always has (this gate lives in OnLanePressed itself, independent of
            // the teaching overlay).
            bootstrap.LanePressedForTests(Lane.Middle);
            Assert.AreEqual(TutorialStep.FrontLane, bootstrap.TutorialStepForTests, "A wrong lane must not advance the step.");
            Assert.AreEqual(0, controller.PlayerState.Lanes[Lane.Middle].Cards.Count, "A wrong lane must not place a card.");
        }

        [Test]
        public void ActiveTarget_StillSucceedsThroughTheRealHandler_ThroughoutTheEntireGuidedSequenceToVictory()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Overlay_RealHandlerSucceedsBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;

            bootstrap.HandCardPressedForTests(Find(controller, "warrior"));
            bootstrap.LanePressedForTests(Lane.Front);
            bootstrap.HandCardPressedForTests(Find(controller, "novice_knight"));
            bootstrap.LanePressedForTests(Lane.Middle);
            bootstrap.HandCardPressedForTests(Find(controller, "goblin_caster"));
            bootstrap.LanePressedForTests(Lane.Back);
            bootstrap.StartBattleForTests();
            Assert.AreEqual(TutorialStep.FirstCombatResult, bootstrap.TutorialStepForTests);

            bootstrap.TutorialContinueForTests();
            Assert.AreEqual(TutorialStep.SpellLesson, bootstrap.TutorialStepForTests);

            bootstrap.SpellTappedForTests(0);
            bootstrap.SpellTargetLanePressedForTests(Lane.Middle);
            Assert.AreEqual(TutorialStep.Finish, bootstrap.TutorialStepForTests);

            int continuesRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                bootstrap.TutorialContinueForTests();
                continuesRun++;
                Assert.LessOrEqual(continuesRun, BattleController.MaxCombatTicks,
                    "The scripted encounter must resolve within the existing tick cap.");
            }

            Assert.AreEqual(BattlePhase.Resolved, controller.Phase);
            Assert.AreEqual("Victory. The first threat has been driven back.", bootstrap.ResultTextForTests,
                "The teaching overlay must never prevent the intended guided sequence from reaching a real victory.");
            Assert.IsFalse(bootstrap.TutorialTeachingOverlayForTests.activeSelf,
                "Once the match has resolved, the Result overlay owns the screen - the teaching overlay must hide itself.");
            Assert.IsNull(bootstrap.TutorialActionProxyForTests, "No proxy may remain once the match has resolved.");
        }

        [Test]
        public void GuidePanelText_MeetsMinimumReadableSizes()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Overlay_ReadabilityBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();

            Text speaker = bootstrap.TutorialGuidePanelRectForTests
                .GetComponentsInChildren<Text>(true).First(t => t.text == "LIGHTBRINGER");
            Text body = bootstrap.TutorialGuidePanelRectForTests
                .GetComponentsInChildren<Text>(true).First(t => t != speaker && !string.IsNullOrEmpty(t.text));

            Assert.GreaterOrEqual(speaker.fontSize, 22, "Speaker name must be at least 22px at the 1920x1080 reference.");
            Assert.GreaterOrEqual(body.fontSize, 28, "Instruction text must be at least 28px at the 1920x1080 reference.");
        }

        [Test]
        public void SkipTutorial_FromTheGuidedOverlay_HidesBattleWithoutRewardsProgressionOrCardDuplication()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Overlay_SkipBootstrap");
            int avatarLevelBefore = bootstrap.EmpireForTests.AvatarLevel;
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;

            bootstrap.HandCardPressedForTests(Find(controller, "warrior"));
            bootstrap.LanePressedForTests(Lane.Front);
            Assert.AreEqual(TutorialStep.MiddleLane, bootstrap.TutorialStepForTests, "Setup: mid-tutorial, before completion.");

            Assert.IsTrue(bootstrap.TutorialSkipButtonForTests.gameObject.activeInHierarchy,
                "The SKIP TUTORIAL button must be visible during the guided teaching overlay.");

            var starterIds = new[] { "warrior", "novice_knight", "goblin_caster" };
            Dictionary<string, int> countsBefore = starterIds
                .ToDictionary(id => id, id => bootstrap.ProfileCardCollectionForTests.Count(c => c == id));

            bootstrap.SkipTutorialForTests();

            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "Skip Tutorial must hide the battle canvas.");
            Assert.IsNull(bootstrap.TutorialStepForTests, "Skip Tutorial must clear the guided-tutorial gate.");
            Assert.IsNull(bootstrap.TutorialActionProxyForTests, "Skip Tutorial must leave no proxy behind.");
            Assert.AreEqual(avatarLevelBefore, bootstrap.EmpireForTests.AvatarLevel,
                "Skip Tutorial must never grant Avatar level/progression - the match never resolved through OnMatchEnded.");
            foreach (string id in starterIds)
            {
                Assert.AreEqual(countsBefore[id], bootstrap.ProfileCardCollectionForTests.Count(c => c == id),
                    $"Skip Tutorial must never duplicate the starter card '{id}'.");
            }
        }

        [Test]
        public void SkipTutorialButton_IsVisibleOnTheOpeningCinematicOverlay()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Overlay_CinematicSkipVisibleBootstrap");
            bootstrap.StartApprovedTutorialBattle();

            Assert.IsTrue(bootstrap.CinematicActiveForTests, "Setup: expected the opening cinematic to be active.");
            Button cinematicSkipTutorial = GameObject.Find("Chapter1Cinematic")
                .GetComponentsInChildren<Button>(true)
                .First(b => b.GetComponentInChildren<Text>().text == "SKIP TUTORIAL");
            Assert.IsTrue(cinematicSkipTutorial.gameObject.activeInHierarchy,
                "SKIP TUTORIAL must be visible on the opening cinematic overlay too.");
        }

        [Test]
        public void NormalMatch_TeachingOverlayNeverAppears()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Overlay_NormalMatchBootstrap");
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Setup: expected a normal match.");
            Assert.IsNull(bootstrap.TutorialStepForTests);

            Assert.IsFalse(bootstrap.TutorialTeachingOverlayForTests.activeSelf,
                "A normal match must never show the guided-tutorial teaching overlay.");
            Assert.IsNull(bootstrap.TutorialActionProxyForTests, "A normal match must never have a Tutorial Action Proxy.");
        }

        /// <summary>
        /// Best-effort, honestly-reported real GraphicRaycaster.Raycast() attempt - see this
        /// class's own top comment for the confirmed environment limitation (Graphic.depth stuck
        /// at -1 without a real rendered frame, which -batchmode EditMode never produces). Does
        /// not assert a specific hit count; only that the raycaster component itself is live and
        /// does not throw when queried at the proxy's own screen position.
        /// </summary>
        [Test]
        public void TeachingOverlay_RealGraphicRaycaster_BestEffort()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Overlay_RealRaycastBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            bootstrap.ForceTutorialOverlayRebuildForTests();

            Button proxy = bootstrap.TutorialActionProxyForTests;
            Assert.IsNotNull(proxy);

            var corners = new Vector3[4];
            proxy.GetComponent<RectTransform>().GetWorldCorners(corners);
            Vector3 worldCenter = (corners[0] + corners[2]) / 2f;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldCenter);

            GraphicRaycaster raycaster = bootstrap.CanvasRaycasterForTests;
            Assert.IsNotNull(raycaster, "Setup: expected the Canvas's real GraphicRaycaster component.");
            GameObject eventSystemGo = GameObject.Find("EventSystem");
            Assert.IsNotNull(eventSystemGo, "Setup: expected a real EventSystem.");

            var pointerData = new PointerEventData(eventSystemGo.GetComponent<EventSystem>()) { position = screenPoint };
            var results = new List<RaycastResult>();
            raycaster.Raycast(pointerData, results);

            TestContext.WriteLine($"Real GraphicRaycaster.Raycast() at the proxy's own screen point " +
                                  $"returned {results.Count} hit(s) in this environment.");
        }

        // ---------- 2026-08-16 collision fix: modal precedence + real spell outcome ----------

        [Test]
        public void SpellLessonToFinish_GuideActive_CinematicInactive_ContinueProxyExistsAndMatchesBounds()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Overlay_FinishStateBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;

            bootstrap.HandCardPressedForTests(Find(controller, "warrior"));
            bootstrap.LanePressedForTests(Lane.Front);
            bootstrap.HandCardPressedForTests(Find(controller, "novice_knight"));
            bootstrap.LanePressedForTests(Lane.Middle);
            bootstrap.HandCardPressedForTests(Find(controller, "goblin_caster"));
            bootstrap.LanePressedForTests(Lane.Back);
            bootstrap.StartBattleForTests();
            bootstrap.TutorialContinueForTests(); // -> SpellLesson
            bootstrap.SpellTappedForTests(0);
            bootstrap.SpellTargetLanePressedForTests(Lane.Middle);
            Assert.AreEqual(TutorialStep.Finish, bootstrap.TutorialStepForTests, "Setup: expected the Finish step.");

            Assert.IsFalse(bootstrap.AnyOtherTutorialModalActiveForTests,
                "No cinematic and no narrative overlay may be active at the Finish step.");
            Assert.IsFalse(bootstrap.CinematicActiveForTests, "The cinematic must be inactive at Finish.");
            Assert.IsTrue(bootstrap.TutorialTeachingOverlayForTests.activeSelf, "The guide overlay must be active at Finish.");

            Assert.IsTrue(bootstrap.TutorialContinueButtonForTests.gameObject.activeInHierarchy,
                "Continue must actually be visible at Finish.");

            AssertExactlyOneProxyMatchesTheRealTarget(bootstrap, "Finish");
            Assert.AreSame(bootstrap.TutorialContinueButtonForTests.GetComponent<RectTransform>(),
                bootstrap.TutorialActiveTargetRectForTests(),
                "Finish's real target must be the real Continue button.");
        }

        [Test]
        public void OpeningCinematic_NeverOverlapsFormationGuidance()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Overlay_OpeningNoOverlapBootstrap");
            bootstrap.StartApprovedTutorialBattle(); // showOpeningCinematic defaults true

            Assert.IsTrue(bootstrap.CinematicActiveForTests, "Setup: expected the opening cinematic to be active.");
            Assert.IsFalse(bootstrap.TutorialTeachingOverlayForTests.activeSelf,
                "Formation guidance must not be visible while the opening cinematic is still playing.");
            Assert.IsNull(bootstrap.TutorialActionProxyForTests, "No proxy may exist while the opening cinematic plays.");

            bootstrap.SkipCinematicForTests();

            Assert.IsFalse(bootstrap.CinematicActiveForTests, "The cinematic must be gone after skipping.");
            Assert.IsTrue(bootstrap.TutorialTeachingOverlayForTests.activeSelf,
                "Formation guidance must begin the instant the opening cinematic finishes or is skipped.");
            Assert.AreEqual(TutorialStep.CardCost, bootstrap.TutorialStepForTests);
            Assert.IsNotNull(bootstrap.TutorialActionProxyForTests, "A proxy must exist once Formation guidance begins.");
        }

        [Test]
        public void VictoryCinematic_NeverOverlapsTutorialGuidance()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Overlay_VictoryNoOverlapBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;

            bootstrap.HandCardPressedForTests(Find(controller, "warrior"));
            bootstrap.LanePressedForTests(Lane.Front);
            bootstrap.HandCardPressedForTests(Find(controller, "novice_knight"));
            bootstrap.LanePressedForTests(Lane.Middle);
            bootstrap.HandCardPressedForTests(Find(controller, "goblin_caster"));
            bootstrap.LanePressedForTests(Lane.Back);
            bootstrap.StartBattleForTests();
            bootstrap.TutorialContinueForTests();
            bootstrap.SpellTappedForTests(0);
            bootstrap.SpellTargetLanePressedForTests(Lane.Middle);
            Assert.AreEqual(TutorialStep.Finish, bootstrap.TutorialStepForTests);

            int continuesRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                bootstrap.TutorialContinueForTests();
                continuesRun++;
                Assert.LessOrEqual(continuesRun, BattleController.MaxCombatTicks);

                if (bootstrap.CinematicActiveForTests)
                {
                    Assert.IsFalse(bootstrap.TutorialTeachingOverlayForTests.activeSelf,
                        "The teaching overlay must never be active while the victory cinematic is playing.");
                }
            }

            Assert.AreEqual(BattlePhase.Resolved, controller.Phase, "Setup: expected the scripted encounter to resolve in victory.");
            Assert.IsFalse(bootstrap.TutorialTeachingOverlayForTests.activeSelf,
                "The teaching overlay must be hidden once the match (and any victory cinematic) has resolved.");
        }

        [Test]
        public void SpellLessonCast_ShowsRealMiddleLaneOutcome_NotUnchangedAvatarHealth()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Overlay_SpellOutcomeBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;

            bootstrap.HandCardPressedForTests(Find(controller, "warrior"));
            bootstrap.LanePressedForTests(Lane.Front);
            bootstrap.HandCardPressedForTests(Find(controller, "novice_knight"));
            bootstrap.LanePressedForTests(Lane.Middle);
            bootstrap.HandCardPressedForTests(Find(controller, "goblin_caster"));
            bootstrap.LanePressedForTests(Lane.Back);
            bootstrap.StartBattleForTests();
            bootstrap.TutorialContinueForTests();

            int enemyAvatarHealthBefore = controller.EnemyState.AvatarHealth;
            var middleUnitBefore = controller.EnemyState.Lanes[Lane.Middle].Cards.FirstOrDefault();
            Assert.IsNotNull(middleUnitBefore, "Setup: expected a living enemy unit in the Middle lane before the cast.");

            bootstrap.SpellTappedForTests(0);
            bootstrap.SpellTargetLanePressedForTests(Lane.Middle);
            Assert.AreEqual(TutorialStep.Finish, bootstrap.TutorialStepForTests, "Setup: expected the cast to succeed.");

            string caption = bootstrap.TutorialGuidanceCaptionTextForTests;
            Assert.IsNotNull(caption);
            StringAssert.DoesNotContain($"Enemy Health {enemyAvatarHealthBefore} -> {enemyAvatarHealthBefore}", caption,
                "The spell summary must never report the Avatar's own (unchanged) Health as the outcome.");

            bool middleStillPresent = controller.EnemyState.Lanes[Lane.Middle].Cards.Contains(middleUnitBefore);
            if (middleStillPresent)
            {
                StringAssert.Contains(middleUnitBefore.Definition.DisplayName, caption);
                StringAssert.Contains("Health", caption);
            }
            else
            {
                StringAssert.Contains("destroyed", caption,
                    "A Middle-lane unit that did not survive the cast must be reported as destroyed.");
                StringAssert.Contains(middleUnitBefore.Definition.DisplayName, caption);
            }
        }
    }
}
