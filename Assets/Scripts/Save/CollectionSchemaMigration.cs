using System;
using System.Collections.Generic;
using MyriadOfDragons.Cards;

namespace MyriadOfDragons.Save
{
    /// <summary>
    /// One-way V1 migration: legacy <see cref="PlayerProfile.cardCollection"/> →
    /// <see cref="PlayerProfile.cardProgression"/> without mutating the legacy list.
    /// Idempotent; safe to call on every load via <see cref="SaveMigration.Normalize"/>.
    /// </summary>
    public static class CollectionSchemaMigration
    {
        public static void Apply(PlayerProfile profile, Func<string, bool> isKnownCardId = null)
        {
            if (profile == null) return;

            NormalizeCollectionFields(profile);

            if (profile.collectionSchemaVersion >= CollectionSchemaRules.CurrentCollectionSchemaVersion)
                return;

            // Interrupted write: discard partial V2 rows and rebuild from untouched legacy list.
            profile.cardProgression.Clear();
            profile.collectionMigrationUnknownIds.Clear();

            MigrateFromLegacyList(profile, isKnownCardId ?? DefaultIsKnownCardId);
            profile.collectionSchemaVersion = CollectionSchemaRules.CurrentCollectionSchemaVersion;
        }

        public static void NormalizeCollectionFields(PlayerProfile profile)
        {
            profile.cardProgression ??= new List<CardProgressionRecord>();
            profile.collectionMigrationUnknownIds ??= new List<string>();
            profile.collectionWallet ??= new CollectionMaterialWallet();
            profile.collectionWallet.dustByRarity ??= new List<RarityMaterialBalance>();
            profile.ascensionPermitWeekKey ??= string.Empty;

            profile.normalPityMisses = Math.Max(0, profile.normalPityMisses);
            profile.highPityMissesSince5Star = Math.Max(0, profile.highPityMissesSince5Star);
            profile.highPityMissesSince7Star = Math.Max(0, profile.highPityMissesSince7Star);
            profile.ascensionPermitBalance = Math.Min(
                CollectionSchemaRules.AscensionPermitHoardCap,
                Math.Max(0, profile.ascensionPermitBalance));
            profile.ascensionPermitsEarnedThisWeek = Math.Max(0, profile.ascensionPermitsEarnedThisWeek);
        }

        /// <summary>
        /// Groups legacy list entries by id. Each list occurrence becomes one copy.
        /// Unknown ids are quarantined in <see cref="PlayerProfile.collectionMigrationUnknownIds"/>.
        /// </summary>
        public static void MigrateFromLegacyList(PlayerProfile profile, Func<string, bool> isKnownCardId)
        {
            if (profile.cardCollection == null || profile.cardCollection.Count == 0)
                return;

            var copyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string rawId in profile.cardCollection)
            {
                if (string.IsNullOrWhiteSpace(rawId))
                    continue;

                string id = rawId.Trim();
                if (!isKnownCardId(id))
                {
                    if (!profile.collectionMigrationUnknownIds.Contains(id))
                        profile.collectionMigrationUnknownIds.Add(id);
                    continue;
                }

                copyCounts.TryGetValue(id, out int count);
                copyCounts[id] = count + 1;
            }

            foreach (var pair in copyCounts)
            {
                profile.cardProgression.Add(new CardProgressionRecord
                {
                    cardId = pair.Key,
                    copyCount = pair.Value,
                    cardLevel = 1,
                    evolutionStep = 0,
                    trainingXp = 0,
                });
            }
        }

        /// <summary>True when the most recent <see cref="Apply"/> validated ids against a real
        /// <see cref="CardDatabase"/>; false when it fell back to accepting any non-empty id because
        /// no database was loaded.
        ///
        /// Exists because that fallback is INVISIBLE, and invisible permissiveness is how a test
        /// suite ends up proving less than it appears to. In EditMode most fixtures never build a
        /// CardDatabase, so they silently take the permissive branch and accept ids the real runtime
        /// would quarantine - a green migration test there says nothing about id legality. This flag
        /// lets a fixture that cares ASSERT which branch it got instead of assuming the strict one.
        ///
        /// Deliberately NOT used to change behaviour: tightening the fallback could quarantine a
        /// real player's cards on any load that legitimately runs before the database exists. That
        /// is a decision above this file, so this makes the gap measurable rather than silently
        /// closing it.</summary>
        public static bool LastApplyVerifiedIdsAgainstDatabase { get; private set; }

        private static bool DefaultIsKnownCardId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            if (CardDatabase.Instance != null)
            {
                LastApplyVerifiedIdsAgainstDatabase = true;
                return CardDatabase.Instance.GetCard(id) != null;
            }

            // EditMode tests and pre-bootstrap loads may not have CardDatabase - accept non-empty ids.
            LastApplyVerifiedIdsAgainstDatabase = false;
            return true;
        }
    }
}
