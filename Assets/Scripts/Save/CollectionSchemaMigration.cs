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
        /// <see cref="CardDatabase"/>; false when no database was loaded and every id was therefore
        /// quarantined.
        ///
        /// The flag predates the tightening below and still earns its place: it lets a fixture
        /// ASSERT which branch it got rather than assume, so a wholesale quarantine caused by a
        /// missing database is never mistaken for a genuine id-legality result.</summary>
        public static bool LastApplyVerifiedIdsAgainstDatabase { get; private set; }

        private static bool DefaultIsKnownCardId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            if (CardDatabase.Instance != null)
            {
                LastApplyVerifiedIdsAgainstDatabase = true;
                return CardDatabase.Instance.GetCard(id) != null;
            }

            // No database: REJECT rather than accept. The old fallback accepted any non-empty id,
            // which made the harness structurally more permissive than production - EditMode
            // fixtures silently validated ids the live runtime quarantines, so a green migration
            // test proved nothing about id legality.
            //
            // Safe because production always has the database by the time a profile migrates
            // (verified by sweep d177c14); the permissive branch was reachable only from EditMode
            // and pre-bootstrap paths. Migration is non-destructive either way - rejected ids land
            // in collectionMigrationUnknownIds, and the legacy cardCollection list is never mutated,
            // so nothing is lost even if a load does precede the database.
            //
            // A fixture that genuinely does not care about id legality must now say so out loud by
            // passing its own predicate to Apply, e.g. Apply(profile, _ => true).
            LastApplyVerifiedIdsAgainstDatabase = false;
            return false;
        }
    }
}
