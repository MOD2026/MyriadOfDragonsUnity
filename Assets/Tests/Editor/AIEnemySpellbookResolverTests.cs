using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.AI;
using MyriadOfDragons.Battle;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Real gap closure (see AIEnemySpellbookResolver's own doc comment): the AI's spellbook must
    /// come from its own difficulty tier, never from the player's ownedSpellIds/equippedSpellIds.
    /// Also proves the deliberate exclusions: every Stage-gated and SpellBookGrant-gated spell
    /// (Cinder Lash, Fault Line, Vital Spark, Renewal, Banner of Ashes, Sun Lance, Tempest Brand)
    /// never appears at any tier - that's a real, flagged-not-guessed gap, not an oversight.
    /// </summary>
    public class AIEnemySpellbookResolverTests
    {
        private static readonly string[] NeverAvailable =
        {
            "Cinder Lash", "Fault Line", "Vital Spark", "Renewal", "Banner of Ashes", "Sun Lance", "Tempest Brand",
        };

        [TestCase(AIDifficultyTier.Novice)]
        [TestCase(AIDifficultyTier.Apprentice)]
        [TestCase(AIDifficultyTier.Veteran)]
        [TestCase(AIDifficultyTier.Master)]
        [TestCase(AIDifficultyTier.Titan)]
        public void ResolveSpellbook_EveryTier_ReturnsARealNonEmptyCastableLoadout(AIDifficultyTier tier)
        {
            List<AvatarSpell> spellbook = AIEnemySpellbookResolver.ResolveSpellbook(tier);

            Assert.IsTrue(spellbook.Count > 0, $"{tier}: must never resolve to an empty spellbook.");
            Assert.IsTrue(spellbook.All(s => !string.IsNullOrEmpty(s.Id)), $"{tier}: every resolved spell must be a real catalog entry.");
        }

        [TestCase(AIDifficultyTier.Novice)]
        [TestCase(AIDifficultyTier.Apprentice)]
        [TestCase(AIDifficultyTier.Veteran)]
        [TestCase(AIDifficultyTier.Master)]
        [TestCase(AIDifficultyTier.Titan)]
        public void ResolveSpellbook_EveryTier_NeverIncludesAStageOrSpellBookGatedSpell(AIDifficultyTier tier)
        {
            List<string> names = AIEnemySpellbookResolver.ResolveSpellbook(tier).Select(s => s.Name).ToList();

            foreach (string forbidden in NeverAvailable)
            {
                CollectionAssert.DoesNotContain(names, forbidden,
                    $"{tier}: {forbidden} is Stage/SpellBookGrant-gated - no locked tier-to-stage mapping exists, so it must never appear.");
            }
        }

        [Test]
        public void ResolveSpellbook_EveryTier_NeverEquipsTwoAvatarStrikes()
        {
            foreach (AIDifficultyTier tier in System.Enum.GetValues(typeof(AIDifficultyTier)).Cast<AIDifficultyTier>())
            {
                int avatarStrikeCount = AIEnemySpellbookResolver.ResolveSpellbook(tier).Count(s => s.Effect == SpellEffect.AvatarStrike);
                Assert.LessOrEqual(avatarStrikeCount, 1, $"{tier}: MOS's max-1-AvatarStrike-equipped lock must hold for the AI too.");
            }
        }

        [Test]
        public void ResolveSpellbook_IsDeterministic_SameTierAlwaysResolvesTheSameLoadout()
        {
            List<string> first = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Veteran).Select(s => s.Id).ToList();
            List<string> second = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Veteran).Select(s => s.Id).ToList();

            CollectionAssert.AreEqual(first, second, "No randomness - a given tier must always resolve the same loadout.");
        }

        [Test]
        public void ResolveSpellbook_EveryTier_FirestormAlwaysOutclassesEmberWaveOnMagnitude()
        {
            // Ember Wave (magnitude 3, L5-gated) can never win the LaneDamage slot over Firestorm
            // (magnitude 4, always unlocked) under "highest magnitude wins per effect type" - the
            // same structural quirk the Locked Decisions Register flagged for the player's own
            // auto-equip. Real and expected, not a bug: proves tier boundaries clearing a gate
            // (Novice's band-top of 10 clears Ember Wave's L5) don't by themselves guarantee a
            // spell gets equipped if a stronger same-type option is always available too.
            foreach (AIDifficultyTier tier in System.Enum.GetValues(typeof(AIDifficultyTier)).Cast<AIDifficultyTier>())
            {
                List<string> names = AIEnemySpellbookResolver.ResolveSpellbook(tier).Select(s => s.Name).ToList();
                CollectionAssert.Contains(names, "Firestorm", $"{tier}: expected Firestorm as the LaneDamage pick.");
                CollectionAssert.DoesNotContain(names, "Ember Wave", $"{tier}: Ember Wave can never outclass Firestorm's magnitude.");
            }
        }

        [Test]
        public void ResolveSpellbook_ApprenticeAndAbove_UseStoneJudgmentOverDivineBolt()
        {
            // Stone Judgment (magnitude 120) beats Divine Bolt (magnitude 100) under the same
            // "highest magnitude wins per effect type" selection SpellLoadoutAutoEquip already
            // uses - once a tier's own band-top clears Stone Judgment's L12 gate (Apprentice's
            // top is 25, already past it), it must be the one AvatarStrike slot, not Divine Bolt.
            foreach (AIDifficultyTier tier in new[] { AIDifficultyTier.Apprentice, AIDifficultyTier.Veteran, AIDifficultyTier.Master, AIDifficultyTier.Titan })
            {
                List<string> names = AIEnemySpellbookResolver.ResolveSpellbook(tier).Select(s => s.Name).ToList();
                CollectionAssert.Contains(names, "Stone Judgment", $"{tier}: expected Stone Judgment as the AvatarStrike pick.");
                CollectionAssert.DoesNotContain(names, "Divine Bolt", $"{tier}: Divine Bolt should have been outclassed by Stone Judgment.");
            }
        }

        [Test]
        public void ResolveSpellbook_NoviceTier_StillUsesDivineBolt_StoneJudgmentNotYetCleared()
        {
            // Novice's own band-top (10) sits below Stone Judgment's L12 gate - the only tier
            // where Divine Bolt should still be the AvatarStrike pick.
            List<string> names = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Novice).Select(s => s.Name).ToList();
            CollectionAssert.Contains(names, "Divine Bolt");
            CollectionAssert.DoesNotContain(names, "Stone Judgment");
        }
    }
}
