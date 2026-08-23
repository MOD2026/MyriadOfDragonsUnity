using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Makes the 10 Phase-1 spells beyond the starter four actually reachable in real matches,
    /// instead of only existing for SpellCatalogPhase1Tests to construct in isolation. Covers the
    /// unlock resolver (SPELL_CATALOG_v1.md's own "Unlock" column, resolved against real
    /// PlayerProfile/PlayerEmpireData data - Avatar level, unlocked campaign stage ids - with no
    /// new Save schema), the Phase-1 auto-equip stopgap (one spell per effect type, which also
    /// structurally enforces MOS_v1.1.md's "max 1 AvatarStrike equipped" lock), and
    /// BattleController.StartMatch's real wiring of both.
    /// </summary>
    public class SpellLoadoutTests
    {
        // ---------- SpellUnlockResolver ----------

        [TestCase("Firestorm")]
        [TestCase("Mend")]
        [TestCase("War Cry")]
        [TestCase("Divine Bolt")]
        public void IsUnlocked_StarterSpells_AreAlwaysUnlocked(string name)
        {
            AvatarSpell spell = AvatarSpell.CreatePhase1Catalog().Single(s => s.Name == name);
            Assert.IsTrue(SpellUnlockResolver.IsUnlocked(spell, avatarLevel: 1, unlockedStageIds: null));
        }

        [Test]
        public void IsUnlocked_StageGatedSpell_LockedUntilItsStageIsUnlocked()
        {
            AvatarSpell cinderLash = AvatarSpell.CreatePhase1Catalog().Single(s => s.Name == "Cinder Lash");
            Assert.IsFalse(SpellUnlockResolver.IsUnlocked(cinderLash, avatarLevel: 99, unlockedStageIds: new List<string> { "1-1" }),
                "A stage-gated spell must not unlock from Avatar level alone.");
            Assert.IsTrue(SpellUnlockResolver.IsUnlocked(cinderLash, avatarLevel: 1, unlockedStageIds: new List<string> { "1-1", "1-2" }),
                "Cinder Lash's catalog row is Ch1-2 - it must unlock once stage '1-2' is reached.");
        }

        [Test]
        public void IsUnlocked_AvatarLevelGatedSpell_LockedUntilTheRequiredLevel()
        {
            AvatarSpell stoneJudgment = AvatarSpell.CreatePhase1Catalog().Single(s => s.Name == "Stone Judgment");
            Assert.IsFalse(SpellUnlockResolver.IsUnlocked(stoneJudgment, avatarLevel: 11, unlockedStageIds: null));
            Assert.IsTrue(SpellUnlockResolver.IsUnlocked(stoneJudgment, avatarLevel: 12, unlockedStageIds: null));
        }

        [TestCase("Sun Lance")]
        [TestCase("Tempest Brand")]
        public void IsUnlocked_SpellBookGatedSpells_NeverUnlockRegardlessOfProgress(string name)
        {
            // Catalog rows "Ch2 spell book" / "Ch3 spell book" name an acquisition method nothing
            // in the codebase tracks - see SpellUnlockResolver's own class doc comment. Proven
            // here with a deliberately absurd amount of progress, so this can't pass by accident.
            AvatarSpell spell = AvatarSpell.CreatePhase1Catalog().Single(s => s.Name == name);
            var everyStage = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" };
            Assert.IsFalse(SpellUnlockResolver.IsUnlocked(spell, avatarLevel: 999, unlockedStageIds: everyStage));
        }

        [Test]
        public void HasUnresolvableSpellBookGates_IsTrue_DocumentingTheKnownGap()
        {
            Assert.IsTrue(SpellUnlockResolver.HasUnresolvableSpellBookGates,
                "This must flip to false (and this test updated) the day a real spell-book acquisition system ships.");
        }

        [Test]
        public void IsUnlocked_ANonCatalogSpell_IsNeverUnlocked()
        {
            var madeUp = new AvatarSpell("Not In The Catalog", "d", energyCost: 1, cooldownTicks: 1, SpellEffect.LaneDamage, magnitude: 1);
            Assert.IsFalse(SpellUnlockResolver.IsUnlocked(madeUp, avatarLevel: 999, unlockedStageIds: new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" }));
        }

        [Test]
        public void ResolveUnlockedSpells_FreshPlayer_IsExactlyTheStarterFour()
        {
            List<AvatarSpell> unlocked = SpellUnlockResolver.ResolveUnlockedSpells(avatarLevel: 1, unlockedStageIds: null);
            CollectionAssert.AreEquivalent(new[] { "Firestorm", "Mend", "War Cry", "Divine Bolt" }, unlocked.Select(s => s.Name).ToList());
        }

        [Test]
        public void ResolveUnlockedSpells_FullProgress_IncludesEveryReachableSpellButNotTheSpellBookGatedOnes()
        {
            var everyStage = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" };
            List<AvatarSpell> unlocked = SpellUnlockResolver.ResolveUnlockedSpells(avatarLevel: 12, unlockedStageIds: everyStage);

            CollectionAssert.AreEquivalent(
                new[] { "Firestorm", "Mend", "War Cry", "Divine Bolt", "Cinder Lash", "Ember Wave", "Fault Line",
                    "Vital Spark", "Renewal", "Rallying Gale", "Banner of Ashes", "Stone Judgment" },
                unlocked.Select(s => s.Name).ToList());
            CollectionAssert.DoesNotContain(unlocked.Select(s => s.Name).ToList(), "Sun Lance");
            CollectionAssert.DoesNotContain(unlocked.Select(s => s.Name).ToList(), "Tempest Brand");
        }

        // ---------- SpellLoadoutAutoEquip ----------

        [Test]
        public void AutoEquip_FreshPlayer_ResolvesToExactlyTheStarterFourInOrder()
        {
            List<AvatarSpell> loadout = SpellLoadoutAutoEquip.AutoEquip(avatarLevel: 1, unlockedStageIds: null);
            CollectionAssert.AreEqual(new[] { "Firestorm", "Mend", "War Cry", "Divine Bolt" }, loadout.Select(s => s.Name).ToList(),
                "A fresh player's auto-equipped loadout must match the old hardcoded starter four exactly, same order.");
        }

        [Test]
        public void AutoEquip_AlwaysHasAtMostOneAvatarStrikeSpell()
        {
            var everyStage = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" };
            for (int level = 1; level <= 15; level++)
            {
                List<AvatarSpell> loadout = SpellLoadoutAutoEquip.AutoEquip(level, everyStage);
                Assert.LessOrEqual(loadout.Count(s => s.Effect == SpellEffect.AvatarStrike), 1,
                    $"MOS_v1.1.md's max-1-AvatarStrike lock was violated at Avatar level {level}.");
            }
        }

        [Test]
        public void AutoEquip_NeverExceedsFourSpells()
        {
            var everyStage = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" };
            List<AvatarSpell> loadout = SpellLoadoutAutoEquip.AutoEquip(avatarLevel: 999, unlockedStageIds: everyStage);
            Assert.AreEqual(4, loadout.Count, "Exactly one spell per the four live effect types.");
        }

        [Test]
        public void AutoEquip_FullProgress_PicksTheStrongestUnlockedSpellInEachType()
        {
            var everyStage = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" };
            List<AvatarSpell> loadout = SpellLoadoutAutoEquip.AutoEquip(avatarLevel: 12, unlockedStageIds: everyStage);

            // Fault Line (5) > Firestorm (4) > Cinder Lash (2) > Ember Wave (3) - Fault Line wins.
            Assert.IsTrue(loadout.Any(s => s.Name == "Fault Line" && s.Effect == SpellEffect.LaneDamage));
            // Renewal (6) beats Mend (4) and Vital Spark (2).
            Assert.IsTrue(loadout.Any(s => s.Name == "Renewal" && s.Effect == SpellEffect.LaneHeal));
            // Banner of Ashes (+3) beats War Cry (+2) and Rallying Gale (+1).
            Assert.IsTrue(loadout.Any(s => s.Name == "Banner of Ashes" && s.Effect == SpellEffect.LaneAttackBuff));
            // Stone Judgment (120) beats Divine Bolt (100); Sun Lance (75) is never even eligible.
            Assert.IsTrue(loadout.Any(s => s.Name == "Stone Judgment" && s.Effect == SpellEffect.AvatarStrike));
            Assert.IsFalse(loadout.Any(s => s.Name == "Sun Lance"));
        }

        [Test]
        public void AutoEquip_PartialProgress_UpgradesOnlyTheTypesThatHaveANewerUnlockedOption()
        {
            // Only the LaneDamage upgrade (Fault Line, Ch2-4) is reachable - Heal/Buff/Strike
            // upgrades all require progress this scenario deliberately doesn't have.
            var partialStages = new List<string> { "1-1", "2-4" };
            List<AvatarSpell> loadout = SpellLoadoutAutoEquip.AutoEquip(avatarLevel: 1, unlockedStageIds: partialStages);

            CollectionAssert.AreEqual(new[] { "Fault Line", "Mend", "War Cry", "Divine Bolt" }, loadout.Select(s => s.Name).ToList());
        }

        // ---------- BattleController.StartMatch wiring ----------

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private static Card MakeWeakCard(string id) => Card.FromData(new CardData
        {
            id = id, name = "Spell Loadout Test Card", art_file = "x.png", element = "Andras", type = "warrior", rarity = 1,
        });

        private BattleController CreateController()
        {
            var go = new GameObject("SpellLoadoutTestBattleController");
            _spawned.Add(go);
            return go.AddComponent<BattleController>();
        }

        [Test]
        public void StartMatch_OmittedProgressArgs_StillResolvesToTheStarterFour()
        {
            // Backward compatibility: every pre-existing StartMatch call site (every other test
            // file, the scripted tutorial encounter) must see exactly the same spellbook as
            // before this task, without having to be updated to pass real progress.
            BattleController controller = CreateController();
            Card card = MakeWeakCard("loadout_default_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(20, 20, 1000);
            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy);

            CollectionAssert.AreEqual(new[] { "Firestorm", "Mend", "War Cry", "Divine Bolt" }, controller.Spellbook.Select(s => s.Name).ToList());
            CollectionAssert.AreEqual(new[] { "Firestorm", "Mend", "War Cry", "Divine Bolt" }, controller.EnemySpellbook.Select(s => s.Name).ToList());
        }

        [Test]
        public void StartMatch_WithRealProgress_TheSpellbookReflectsIt()
        {
            BattleController controller = CreateController();
            Card card = MakeWeakCard("loadout_progressed_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(20, 20, 1000);
            var everyStage = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" };

            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy,
                avatarLevel: 12, unlockedStageIds: everyStage);

            Assert.IsTrue(controller.Spellbook.Any(s => s.Name == "Stone Judgment"), "The player's spellbook should reflect real unlocked progress.");
            Assert.AreEqual(1, controller.Spellbook.Count(s => s.Effect == SpellEffect.AvatarStrike), "Still exactly one AvatarStrike, never two.");
            Assert.IsTrue(controller.EnemySpellbook.Any(s => s.Name == "Stone Judgment"), "The mirrored enemy spellbook uses the same progress inputs.");
        }
    }
}
