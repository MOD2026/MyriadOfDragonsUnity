using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CR-ANIMATION-SPELL-TARGET-EXTRACT-001, 2026-08-28 - behavior-preserving extraction.
    /// Friendly-vs-enemy spell targeting was duplicated inline at three call sites
    /// (IsArmedSpellFriendlyTargeted, PlayCastImpact, and ArmSpellTargeting - the last left
    /// untouched, out of this card's authorized scope) with no test coverage anywhere, since all
    /// three sites lived inside coroutine/MonoBehaviour methods EditMode cannot exercise directly.
    /// GameBootstrap.SpellTargetsFriendlyLane is the single, pure, static source of truth now -
    /// this is its only direct coverage.
    ///
    /// CR-BATTLE-FRIENDLY-LANE-TARGET-FIX-001, 2026-09-16 - the 2026-08-28 extraction only ported
    /// the original 4-effect mapping (LaneDamage/LaneHeal/LaneAttackBuff/AvatarStrike), which
    /// predates the 36-spell catalog's LaneShield/Cleanse/Dispel/Vulnerability/AllLaneAttackBuff/
    /// CrossLaneDamage/AllLaneDamage effects (SPELL_CATALOG_v1.md). Cross-checked against
    /// AvatarSpell.Cast's own switch (the real, sole authority on which side each effect mutates):
    /// LaneShield, Cleanse, and AllLaneAttackBuff all apply to `caster` (the player's own board)
    /// there, but this predicate defaulted them to false (enemy) since they were never added -
    /// confirmed missing, not a new decision, and independently flagged again while wiring the
    /// Battle spell-animation asset package (docs/BATTLE_SPELL_VFX_PACKAGE_HANDOFF.md's "Known
    /// issue found, NOT fixed here"). Every other effect's expectation below is taken directly
    /// from Cast's caster/opponent parameter, not invented: LaneDamage/Dispel/Vulnerability/
    /// CrossLaneDamage/AllLaneDamage all apply to `opponent`. DrawCards/Reposition/Silence are
    /// deliberately excluded - DrawCards has no lane at all, and Reposition/Silence pick a unit
    /// (repositionTarget/silenceTarget), never routing through this lane-friendliness predicate
    /// in the first place.
    /// </summary>
    public class SpellTargetsFriendlyLaneTests
    {
        [TestCase(SpellEffect.LaneDamage, false)]
        [TestCase(SpellEffect.LaneHeal, true)]
        [TestCase(SpellEffect.LaneAttackBuff, true)]
        [TestCase(SpellEffect.AvatarStrike, false)]
        [TestCase(SpellEffect.LaneShield, true)]
        [TestCase(SpellEffect.Cleanse, true)]
        [TestCase(SpellEffect.Dispel, false)]
        [TestCase(SpellEffect.Vulnerability, false)]
        [TestCase(SpellEffect.AllLaneAttackBuff, true)]
        [TestCase(SpellEffect.CrossLaneDamage, false)]
        [TestCase(SpellEffect.AllLaneDamage, false)]
        public void SpellTargetsFriendlyLane_MatchesTheApprovedMapping(SpellEffect effect, bool expectedFriendly)
        {
            Assert.AreEqual(expectedFriendly, GameBootstrap.SpellTargetsFriendlyLane(effect),
                $"STATE UNREACHED: {effect} must map to friendly={expectedFriendly}.");
        }

        [Test]
        public void PreviouslyMissing_LaneShieldCleanseAllLaneAttackBuff_AreNowFriendlyTargeted()
        {
            // The three effects confirmed defective (2026-09-16): AvatarSpell.Cast applies each to
            // `caster`, but SpellTargetsFriendlyLane defaulted all three to enemy-targeted before
            // this fix - named explicitly, one assertion each, so a future regression here fails
            // loudly rather than blending into the table-driven case above.
            Assert.IsTrue(GameBootstrap.SpellTargetsFriendlyLane(SpellEffect.LaneShield),
                "LaneShield applies to caster's own lane in AvatarSpell.Cast - must be friendly-targeted.");
            Assert.IsTrue(GameBootstrap.SpellTargetsFriendlyLane(SpellEffect.Cleanse),
                "Cleanse applies to caster's own lane in AvatarSpell.Cast - must be friendly-targeted.");
            Assert.IsTrue(GameBootstrap.SpellTargetsFriendlyLane(SpellEffect.AllLaneAttackBuff),
                "AllLaneAttackBuff applies to every one of caster's own lanes in AvatarSpell.Cast - must be friendly-targeted.");
        }
    }

    /// <summary>
    /// CR-BATTLE-FRIENDLY-LANE-TARGET-FIX-001, 2026-09-16 - end-to-end proof through the real
    /// input path (OnLanePressed -> IsArmedSpellFriendlyTargeted -> SpellTargetsFriendlyLane),
    /// not just the pure predicate above. Before this fix, arming Ember Guard/Cleansing Root/
    /// Thunder Decree (LaneShield/Cleanse/AllLaneAttackBuff) and tapping the player's own lane
    /// hit the `else CancelSpellTargeting()` branch instead of `OnSpellTargetLanePressed(lane)` -
    /// the cast was silently thrown away, exactly the same user-facing failure a fresh predicate
    /// regression would reintroduce.
    /// </summary>
    public class FriendlyLaneSpellRealWiringTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDFriendlyLaneWiring_" + System.Guid.NewGuid().ToString("N"));
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
                Directory.Delete(_scratchSaveDir, recursive: true);
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
            return bootstrap;
        }

        /// <summary>Same direct-override technique SpellCastRejectReasonTests already uses, plus
        /// an explicit equippedSpellIds so the match's Spellbook actually contains the three
        /// spells under test - the plain 4-slot starter loadout never reaches Phase 3 effects.</summary>
        private static BattleController StartSurvivableMatchWithSpellbook(GameBootstrap bootstrap, params string[] equippedSpellIds)
        {
            BattleController controller = bootstrap.Battle;
            var data = new CardData
            {
                id = "friendly_lane_wiring_card", name = "Friendly Lane Wiring Card", art_file = "x.png",
                element = "Andras", type = "warrior", rarity = 1,
            };
            Card card = Card.FromData(data);
            var economy = new BattleController.MatchEconomy(resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 100000);
            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy,
                equippedSpellIds: equippedSpellIds);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);

            Card handCard = controller.PlayerState.Hand.First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, handCard, Lane.Front), "Setup: expected a legal placement.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");
            return controller;
        }

        [TestCase("ember_guard", SpellEffect.LaneShield)]
        [TestCase("cleansing_root", SpellEffect.Cleanse)]
        [TestCase("thunder_decree", SpellEffect.AllLaneAttackBuff)]
        public void TappingThePlayersOwnLane_ActuallyCastsTheSpell_InsteadOfCancellingTargeting(
            string spellId, SpellEffect expectedEffect)
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("FriendlyLaneWiring_" + spellId);
            BattleController controller = StartSurvivableMatchWithSpellbook(bootstrap, spellId);
            Assert.AreEqual(1, controller.Spellbook.Count, "Setup: expected exactly the one equipped spell.");
            Assert.AreEqual(expectedEffect, controller.Spellbook[0].Effect, "Setup: expected the equipped id to resolve to the effect under test.");

            controller.SetEnergyForTutorial(100);
            bootstrap.SpellTappedForTests(0); // Arms targeting - does not cast yet.

            bootstrap.LanePressedForTests(Lane.Front); // The player's OWN lane - must cast, not cancel.

            Assert.AreEqual(1, controller.SpellCastLog.Count,
                $"{spellId} ({expectedEffect}) must cast when the player taps their own lane, not silently cancel targeting.");
        }
    }
}
