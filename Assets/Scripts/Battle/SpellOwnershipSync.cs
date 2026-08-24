using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// Spell-Book Acquisition + Ownership Sync (LOCKED 2026-08-24, owner-authorized frozen field).
    /// Grants real, permanent ownership of every spell a profile is currently eligible for via
    /// progression (Avatar level / campaign stage - see SpellUnlockResolver) that it does not
    /// already own. Idempotent by construction: running it twice in a row, or a hundred times,
    /// never removes anything and never re-adds an id already present.
    ///
    /// Deliberately does NOT grant Sun Lance/Tempest Brand - those two are gated on "Ch2/Ch3 spell
    /// book" per the catalog, a fundamentally different acquisition channel (see SpellBookGrant)
    /// that SpellUnlockResolver's own inputs (Avatar level, unlocked stage ids) cannot see. Running
    /// this after a SpellBookGrant call is still correct and harmless - it just has nothing new to
    /// add for those two ids, since the grant itself already added them directly.
    ///
    /// Call sites (per the lock): Avatar level-up (PlayerProfile.RecordMatchResult), a
    /// SpellBookGrant, new-profile creation, and once as a migration repair pass
    /// (SaveMigration.Normalize) - explicitly NEVER at battle start (BattleController.StartMatch
    /// stays read-only: it resolves equippedSpellIds into real AvatarSpell instances, it never
    /// mutates ownership). Stage first-clear is also a required call site per the lock, but that
    /// reward-grant transaction lives in HomePagePresenter.cs (Metagame-owned, not Battle) - not
    /// wired here; flagged separately rather than guessed at or edited without that seat's
    /// coordination.
    /// </summary>
    public static class SpellOwnershipSync
    {
        public static void SynchronizeEligibleSpellOwnership(PlayerProfile profile)
        {
            if (profile == null) return;

            profile.ownedSpellIds ??= new List<string>();
            profile.equippedSpellIds ??= new List<string>();
            bool hadNoOwnedSpells = profile.ownedSpellIds.Count == 0;

            List<AvatarSpell> eligible = SpellUnlockResolver.ResolveUnlockedSpells(profile.avatarLevel, profile.unlockedStageIds);
            foreach (AvatarSpell spell in eligible)
            {
                if (!string.IsNullOrEmpty(spell.Id) && !profile.ownedSpellIds.Contains(spell.Id))
                    profile.ownedSpellIds.Add(spell.Id);
            }

            // Only backfills a genuinely empty loadout (a migrating old save, or a fresh profile
            // that somehow skipped its own field initializer) - never overwrites a real, already-
            // populated equippedSpellIds. A player's chosen loadout is not this service's to touch.
            if (profile.equippedSpellIds.Count == 0 && (hadNoOwnedSpells || profile.ownedSpellIds.Count > 0))
            {
                List<AvatarSpell> autoEquip = SpellLoadoutAutoEquip.AutoEquip(profile.avatarLevel, profile.unlockedStageIds);
                profile.equippedSpellIds = autoEquip
                    .Where(s => !string.IsNullOrEmpty(s.Id))
                    .Select(s => s.Id)
                    .ToList();
            }
        }
    }
}
