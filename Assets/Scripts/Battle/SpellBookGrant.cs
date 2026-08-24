using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.Battle
{
    public enum SpellBookGrantStatus
    {
        Applied,
        AlreadyClaimed,
        FinaleNotCleared,
        UnknownFinaleStage,

        /// <summary>A book's own grant list named a spell id that isn't a real catalog member -
        /// the whole transaction refuses before any mutation, never a partial grant.</summary>
        UnknownSpellId,
    }

    public sealed class SpellBookGrantResult
    {
        public SpellBookGrantStatus Status;
        public List<string> UnlockedSpellNames = new List<string>();
    }

    /// <summary>
    /// Spell-Book Acquisition + Ownership Sync (LOCKED 2026-08-24): "chapter-finale first-clear-
    /// only grant (no drop/purchase/trade/farm), permanent ownership, not consumable." This is the
    /// real acquisition channel SpellUnlockResolver's own doc comment named as missing for Sun
    /// Lance ("Ch2 spell book") and Tempest Brand ("Ch3 spell book") - both were permanently locked
    /// with no way to ever become true until this transaction existed.
    ///
    /// Finale stage ids are HomePagePresenter.ChapterFinalePermitStageIds' own values for Ch2/Ch3
    /// ("2-21", "3-30") - the existing, shipped source of truth for "which stage is a chapter
    /// finale" (also used there for the existing Permit grant). Not re-derived or guessed at;
    /// hardcoded here rather than referencing that class directly, since HomePagePresenter.cs is
    /// Metagame-owned UI and Battle/Save code should not depend on it.
    /// </summary>
    public static class SpellBookGrant
    {
        private static readonly Dictionary<string, string[]> GrantedSpellIdsByFinaleStageId = new Dictionary<string, string[]>
        {
            ["2-21"] = new[] { "sun_lance" },
            ["3-30"] = new[] { "tempest_brand" },
        };

        /// <summary>
        /// verify finale victory -> verify not already claimed -> resolve book definition -> add
        /// spell ids not already owned -> record claim -> save once -> report unlocked names.
        /// ownedSpellIds.Contains(id) doubles as both the eligibility gate and the permanent claim
        /// record - each of these two books grants exactly one spell, and ownership is never
        /// revoked, so there is no case where "already owns the spell" and "already claimed the
        /// book" could disagree; a separate claimed-book-ids list would just be a second place the
        /// same fact could drift out of sync.
        /// </summary>
        public static SpellBookGrantResult TryGrant(PlayerProfile profile, string finaleStageId, bool persist = true)
        {
            var result = new SpellBookGrantResult();

            if (profile == null || string.IsNullOrEmpty(finaleStageId))
            {
                result.Status = SpellBookGrantStatus.UnknownFinaleStage;
                return result;
            }

            if (!GrantedSpellIdsByFinaleStageId.TryGetValue(finaleStageId, out string[] spellIds))
            {
                result.Status = SpellBookGrantStatus.UnknownFinaleStage;
                return result;
            }

            if (profile.claimedStageRewardIds == null || !profile.claimedStageRewardIds.Contains(finaleStageId))
            {
                result.Status = SpellBookGrantStatus.FinaleNotCleared;
                return result;
            }

            profile.ownedSpellIds ??= new List<string>();

            if (spellIds.All(id => profile.ownedSpellIds.Contains(id)))
            {
                result.Status = SpellBookGrantStatus.AlreadyClaimed;
                return result;
            }

            // Validate every id resolves to a real catalog spell BEFORE mutating anything - an
            // unknown id fails the whole transaction, never a partial grant.
            List<AvatarSpell> catalog = AvatarSpell.CreatePhase1Catalog();
            foreach (string id in spellIds)
            {
                if (catalog.All(s => s.Id != id))
                {
                    result.Status = SpellBookGrantStatus.UnknownSpellId;
                    return result;
                }
            }

            foreach (string id in spellIds)
            {
                if (profile.ownedSpellIds.Contains(id)) continue;
                profile.ownedSpellIds.Add(id);
                result.UnlockedSpellNames.Add(catalog.Single(s => s.Id == id).Name);
            }

            // Required sync call site: a Spell Book grant is itself an ownership-changing
            // progression transaction (fills in anything else newly eligible in the same pass).
            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            if (persist) SaveSystem.Save(profile);

            result.Status = SpellBookGrantStatus.Applied;
            return result;
        }
    }
}
