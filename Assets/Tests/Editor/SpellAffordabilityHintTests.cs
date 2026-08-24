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
    /// SPELL AFFORDABILITY HINT, 2026-08-22 - owner: "help casting, not watch numbers and guess".
    /// Proves the plain predicate (SpellAffordability, no UI/Update dependency) and the real
    /// GameBootstrap wiring: the existing SpellRail "SPELLS" heading (no new element) says so once
    /// at least one spell is really castable, restricted to Combat, and reverts on Formation/
    /// Resolved/a new match - through the exact same tutorial-gated `ready` value RefreshPhaseControls
    /// already used for per-button dimming, not a second rule.
    /// </summary>
    public class SpellAffordabilityHintTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsSpellHint_" + System.Guid.NewGuid().ToString("N"));
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

        /// <summary>Same helper pattern every Battle-adjacent test file in this suite already
        /// uses: a normal match requires a confirmed valid saved deck before it deals any hand.</summary>
        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("SpellHint_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;

            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize)
                .ToList();
            Assert.AreEqual(deckSize, deckIds.Count, "Setup: expected enough real cards to fill a full-size deck.");

            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(deckIds),
                activeDeckCardIds = new List<string>(deckIds),
            };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the confirmed deck.");
            SaveSystem.ResetCurrentProfileForTests();

            Object.DestroyImmediate(databaseGo);
        }

        private GameBootstrap SpawnAndInitializeBootstrap(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                GameObject spawned = GameObject.Find(spawnedName);
                if (spawned != null) _spawned.Add(spawned);
            }
            return bootstrap;
        }

        // ---------- Plain predicate (no BattleController/GameBootstrap at all) ----------

        [Test]
        public void IsCastable_TrueWhenOffCooldownAndEnergyCoversCost()
        {
            var spell = new AvatarSpell("Test Bolt", "desc", energyCost: 25, cooldownTicks: 3, SpellEffect.AvatarStrike, magnitude: 10);
            Assert.IsTrue(SpellAffordability.IsCastable(spell, energy: 25), "Exactly enough Energy must count as castable.");
            Assert.IsTrue(SpellAffordability.IsCastable(spell, energy: 100), "More than enough Energy must count as castable.");
        }

        [Test]
        public void IsCastable_FalseWhenEnergyInsufficient()
        {
            var spell = new AvatarSpell("Test Bolt", "desc", energyCost: 25, cooldownTicks: 3, SpellEffect.AvatarStrike, magnitude: 10);
            Assert.IsFalse(SpellAffordability.IsCastable(spell, energy: 24), "One short of cost must not count as castable.");
            Assert.IsFalse(SpellAffordability.IsCastable(spell, energy: 0));
        }

        [Test]
        public void IsCastable_FalseWhenOnCooldownRegardlessOfEnergy()
        {
            var spell = new AvatarSpell("Test Bolt", "desc", energyCost: 10, cooldownTicks: 3, SpellEffect.AvatarStrike, magnitude: 10);
            spell.PutOnCooldown();
            Assert.IsFalse(SpellAffordability.IsCastable(spell, energy: 1000),
                "A spell still on cooldown must never read as castable, no matter how much Energy is available.");
        }

        [Test]
        public void AnyCastable_TrueIfAtLeastOneSpellQualifies()
        {
            var affordable = new AvatarSpell("Cheap", "d", energyCost: 5, cooldownTicks: 1, SpellEffect.LaneHeal, magnitude: 1);
            var tooExpensive = new AvatarSpell("Costly", "d", energyCost: 999, cooldownTicks: 1, SpellEffect.LaneHeal, magnitude: 1);
            Assert.IsTrue(SpellAffordability.AnyCastable(new[] { tooExpensive, affordable }, energy: 5));
        }

        [Test]
        public void AnyCastable_FalseWhenNoneQualify()
        {
            var tooExpensive = new AvatarSpell("Costly", "d", energyCost: 999, cooldownTicks: 1, SpellEffect.LaneHeal, magnitude: 1);
            var onCooldown = new AvatarSpell("Cheap", "d", energyCost: 1, cooldownTicks: 1, SpellEffect.LaneHeal, magnitude: 1);
            onCooldown.PutOnCooldown();
            Assert.IsFalse(SpellAffordability.AnyCastable(new[] { tooExpensive, onCooldown }, energy: 500));
        }

        [Test]
        public void AnyCastable_FalseForEmptyOrNullList()
        {
            Assert.IsFalse(SpellAffordability.AnyCastable(new List<AvatarSpell>(), energy: 1000));
            Assert.IsFalse(SpellAffordability.AnyCastable(null, energy: 1000));
        }

        // ---------- Real GameBootstrap wiring ----------

        [Test]
        public void Formation_NeverShowsTheReadyCue_EvenIfEnergyWereSomehowNonZero()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellHint_FormationBootstrap");
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase, "Setup: expected a fresh match to start in Formation.");

            // Direct Energy grant, bypassing the normal Combat-only accrual path entirely - proves
            // the cue is gated on phase, not merely on the Energy number happening to be 0.
            bootstrap.Battle.SetEnergyForTutorial(999);
            bootstrap.RefreshAllForTests();

            Assert.AreEqual("SPELLS", bootstrap.SpellRailTitleTextForTests,
                "Formation must never show the ready cue, regardless of Energy.");
        }

        [Test]
        public void Combat_WithAnAffordableSpell_ShowsTheReadyCueOnTheExistingSpellRailHeading()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellHint_CombatReadyBootstrap");
            BattleController controller = bootstrap.Battle;

            Card anyCard = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, anyCard, Lane.Front), "Setup: expected a legal placement.");
            bootstrap.StartBattleForTests();
            bootstrap.RefreshAllForTests();
            Assert.AreEqual(BattlePhase.Combat, controller.Phase, "Setup: expected Start Battle to enter Combat.");
            Assert.AreEqual("SPELLS", bootstrap.SpellRailTitleTextForTests,
                "Setup: expected no ready cue immediately on Combat entry with 0 Energy.");

            // Mend (index 1, cost 25) is the default spellbook's cheapest spell - see
            // AvatarSpell.CreateDefaultSpellbook. A normal match has no tutorial-step gate
            // (_tutorialStep is always null), so every spell is eligible, unlike Tutorial.
            controller.SetEnergyForTutorial(25);
            bootstrap.RefreshAllForTests();

            Assert.AreEqual("SPELLS - READY TO CAST", bootstrap.SpellRailTitleTextForTests,
                "Combat with enough Energy for at least one off-cooldown spell must show the ready cue.");
        }

        [Test]
        public void Combat_WithNoAffordableSpell_ShowsThePlainHeading()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellHint_CombatNotReadyBootstrap");
            BattleController controller = bootstrap.Battle;

            Card anyCard = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, anyCard, Lane.Front));
            bootstrap.StartBattleForTests();
            bootstrap.RefreshAllForTests();

            // One short of the cheapest spell (Mend, 25) - still not castable.
            controller.SetEnergyForTutorial(24);
            bootstrap.RefreshAllForTests();

            Assert.AreEqual("SPELLS", bootstrap.SpellRailTitleTextForTests,
                "Combat with insufficient Energy for every spell must show the plain heading, not the ready cue.");
        }

        [Test]
        public void ReadyCue_ClearsWhenANewMatchReturnsToFormation()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellHint_ResetBootstrap");
            BattleController controller = bootstrap.Battle;

            Card anyCard = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, anyCard, Lane.Front));
            bootstrap.StartBattleForTests();
            controller.SetEnergyForTutorial(25);
            bootstrap.RefreshAllForTests();
            Assert.AreEqual("SPELLS - READY TO CAST", bootstrap.SpellRailTitleTextForTests, "Setup: expected the ready cue showing before the reset.");

            // Reset Lineup routes through StartNewMatch (a "new match" per this task's own
            // requirement 3), returning to a fresh Formation with 0 Energy.
            bootstrap.ResetLineupForTests();
            bootstrap.RefreshAllForTests();

            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase, "Setup: expected Reset Lineup to return to Formation.");
            Assert.AreEqual("SPELLS", bootstrap.SpellRailTitleTextForTests,
                "A new match must clear the ready cue, not carry it over from the previous fight.");
        }

        [Test]
        public void SpellHintWork_DidNotChangeAnyLockedConstitutionNumber()
        {
            // Locked 2026-08-21, docs/MVP_COMBAT_PROGRESSION_CONSTITUTION_2026-08-21.md - this
            // task only adds a display-layer cue, so these must read exactly as
            // Chapter1CampaignPlayabilityTests/BattleLogicTests already assert elsewhere.
            Assert.AreEqual(4, LaneBattleResolver.AvatarDamageMultiplier);
            Assert.AreEqual(0.06f, LaneBattleResolver.ExposedAvatarSiegeFraction);
            Assert.IsTrue(LaneBattleResolver.ExposedAvatarSiegeEnabled);
        }
    }
}
