using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.AI;
using MyriadOfDragons.Battle;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// AI Tier -> Stage-Gated Spell Access (LOCKED 2026-08-24, docs/LOCKED_DECISIONS_REGISTER.md):
    /// cumulative, authored progression fiction, verified here against the register's own worked
    /// magnitudes rather than re-derived - Novice=Cinder Lash+Vital Spark; Apprentice adds Fault
    /// Line+Renewal; Veteran adds Sun Lance+Banner of Ashes; Master adds Tempest Brand; Titan
    /// inherits all 7, no exclusive. The register's own note that Cinder Lash/Vital Spark/Sun
    /// Lance/Tempest Brand become eligible but stay unselected (weaker same-effect-type options
    /// always win), while Fault Line/Renewal/Banner of Ashes do change tier loadouts, is exactly
    /// what these tests prove against the real catalog Magnitudes - not assumed.
    ///
    /// Apprentice+ now also resolve Cleansing Root and Oracle Sight into the loadout (real,
    /// investigated interaction between two separately-locked changes - see
    /// ResolveSpellbook_Apprentice_FaultLineAndRenewalTakeOver_WarCryStillHolds's own doc comment
    /// for the full mechanism: Cleansing Root/Oracle Sight's real AvatarLevel unlock Rules plus the
    /// loadout expansion's AI 6-slot cap combine to make both reachable and selectable for the
    /// first time). Not part of the original AI Tier -> Stage-Gated Spell Access table above -
    /// this pool is the resolver's own separate AvatarLevel-gated pool, documented on
    /// AIEnemySpellbookResolver itself.
    /// </summary>
    public class AIEnemySpellbookResolverTests
    {
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
        public void ResolveSpellbook_Novice_IsExactlyTheStarterFour()
        {
            // Novice's own band-top (10) hasn't cleared Stone Judgment's L12 gate yet, and neither
            // Cinder Lash (magnitude 2) nor Vital Spark (magnitude 2) can outclass Firestorm(4)/
            // Mend(4) - the register's own "become eligible but stay unselected" case.
            List<string> names = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Novice).Select(s => s.Name).ToList();
            CollectionAssert.AreEquivalent(new[] { "Firestorm", "Mend", "War Cry", "Divine Bolt" }, names);
        }

        /// <summary>Real, intended interaction between two SEPARATELY locked changes, not a
        /// bug: Cleansing Root (Avatar L16) and Oracle Sight (Avatar L20) got real AvatarLevel
        /// unlock Rules during the Full 36-Spell Catalogue Diagnosis work; the loadout expansion
        /// (LOCKED 2026-08-25) raised the AI's own slot cap to match the player's tier-unlocked
        /// count ("AI may equip up to the same 6-slot cap... tier-gated as before"). Apprentice's
        /// representative level (25) clears both L16/L20 gates, and its 6-slot cap (via
        /// SpellLoadoutAutoEquip.RequiredSlotCount) now has room to actually select them - Cleanse
        /// and DrawCards are effect types nothing else in this tier's pool competes for, so both
        /// win their slot automatically. Neither the acquisition-channel spec nor the loadout-
        /// expansion spec name this specific cross-effect explicitly; this is this investigation's
        /// own read of their combined, correct consequence - flagged as such, not presented as a
        /// separately re-confirmed locked fact.</summary>
        [Test]
        public void ResolveSpellbook_Apprentice_FaultLineAndRenewalTakeOver_WarCryStillHolds()
        {
            // Fault Line (magnitude 5) and Renewal (magnitude 6) both outclass their starter
            // counterparts once unlocked at Apprentice - the register's "materially change tier
            // loadouts" case. Banner of Ashes isn't unlocked until Veteran, so War Cry still holds
            // LaneAttackBuff. Stone Judgment (L12, cleared at rep level 25) replaces Divine Bolt.
            // Cleansing Root/Oracle Sight now also win their own (previously unavailable) slots -
            // see this method's own doc comment.
            List<string> names = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Apprentice).Select(s => s.Name).ToList();
            CollectionAssert.AreEquivalent(new[] { "Fault Line", "Renewal", "War Cry", "Stone Judgment", "Cleansing Root", "Oracle Sight" }, names);
        }

        [TestCase(AIDifficultyTier.Veteran)]
        [TestCase(AIDifficultyTier.Master)]
        [TestCase(AIDifficultyTier.Titan)]
        public void ResolveSpellbook_VeteranAndAbove_BannerOfAshesTakesOverLaneAttackBuff(AIDifficultyTier tier)
        {
            // Banner of Ashes (magnitude 3) outclasses War Cry (2) and Rallying Gale (1) once
            // unlocked at Veteran - stays the pick through Master/Titan too (Tempest Brand, Master's
            // own addition, never competes for this slot - it's LaneDamage, magnitude 3, and always
            // loses to Fault Line's 5). Cleansing Root/Oracle Sight also win their own slots at
            // every one of these tiers, same real cross-effect as Apprentice's own test above -
            // their 6-slot cap fills exactly the same way regardless of which tier past Apprentice
            // is asked (all three tiers' representative levels clear both L16/L20 easily).
            List<string> names = AIEnemySpellbookResolver.ResolveSpellbook(tier).Select(s => s.Name).ToList();
            CollectionAssert.AreEquivalent(new[] { "Fault Line", "Renewal", "Banner of Ashes", "Stone Judgment", "Cleansing Root", "Oracle Sight" }, names,
                $"{tier}: expected the same converged loadout as Veteran - Master/Titan's own additions (Tempest Brand) never win a slot.");
        }

        [Test]
        public void ResolveSpellbook_MasterAndTitan_ResolveToTheIdenticalLoadout()
        {
            // Register: "Titan inherits all 7, no [Titan-]exclusive" - Titan's own pool differs
            // from Master's only in name, not composition, so the two tiers must converge exactly.
            List<string> master = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Master).Select(s => s.Id).ToList();
            List<string> titan = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Titan).Select(s => s.Id).ToList();
            CollectionAssert.AreEquivalent(master, titan);
        }

        [TestCase(AIDifficultyTier.Novice, "cinder_lash")]
        [TestCase(AIDifficultyTier.Novice, "vital_spark")]
        [TestCase(AIDifficultyTier.Veteran, "sun_lance")]
        [TestCase(AIDifficultyTier.Master, "tempest_brand")]
        public void ResolveSpellbook_TheFourNeverSelectedSpells_StayEligibleButUnselected(AIDifficultyTier tier, string neverSelectedId)
        {
            // The register's own explicit case: these become part of the pool at their tier but a
            // same-effect-type option with higher Magnitude always wins the slot instead.
            List<string> ids = AIEnemySpellbookResolver.ResolveSpellbook(tier).Select(s => s.Id).ToList();
            CollectionAssert.DoesNotContain(ids, neverSelectedId, $"{tier}: {neverSelectedId} should stay eligible-but-unselected, per the locked decision.");
        }

        [Test]
        public void ResolveSpellbook_NeverReadsAnyPlayerState()
        {
            // Structural proof, not just a naming convention: ResolveSpellbook's only parameter is
            // the tier itself - there is no PlayerProfile, no ownedSpellIds, no unlockedStageIds
            // input path into this method at all, so it cannot read player state even by accident.
            var method = typeof(AIEnemySpellbookResolver).GetMethod("ResolveSpellbook");
            Assert.AreEqual(1, method.GetParameters().Length);
            Assert.AreEqual(typeof(AIDifficultyTier), method.GetParameters()[0].ParameterType);
        }
    }
}
