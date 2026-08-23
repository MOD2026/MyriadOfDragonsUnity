using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Save
{
    /// <summary>
    /// Collection ownership writes. After schema V1, grants go to <see cref="PlayerProfile.cardProgression"/>
    /// only — legacy <see cref="PlayerProfile.cardCollection"/> stays a rollback snapshot.
    /// </summary>
    public static class CollectionProgression
    {
        public static bool OwnsAnyCopy(PlayerProfile profile, string cardId)
        {
            if (profile == null || string.IsNullOrEmpty(cardId)) return false;

            if (profile.UsesCollectionV1)
                return FindRecord(profile, cardId)?.copyCount > 0;

            return profile.cardCollection != null && profile.cardCollection.Contains(cardId);
        }

        /// <summary>Grants exactly one copy. Returns false if already owned or id invalid.</summary>
        public static bool TryGrantFirstCopy(PlayerProfile profile, string cardId, Func<string, bool> isKnownCardId = null)
        {
            if (profile == null || string.IsNullOrEmpty(cardId)) return false;
            if (OwnsAnyCopy(profile, cardId)) return false;

            isKnownCardId ??= _ => true;
            if (!isKnownCardId(cardId)) return false;

            CollectionSchemaMigration.NormalizeCollectionFields(profile);

            if (profile.UsesCollectionV1)
            {
                profile.cardProgression.Add(new CardProgressionRecord
                {
                    cardId = cardId,
                    copyCount = 1,
                    cardLevel = 1,
                    evolutionStep = 0,
                    trainingXp = 0,
                });
                return true;
            }

            profile.cardCollection ??= new List<string>();
            profile.cardCollection.Add(cardId);
            return true;
        }

        private static CardProgressionRecord FindRecord(PlayerProfile profile, string cardId)
        {
            if (profile.cardProgression == null) return null;
            foreach (CardProgressionRecord record in profile.cardProgression)
            {
                if (record.cardId == cardId) return record;
            }

            return null;
        }

        /// <summary>Test and service seam — find a progression row without exposing private lookup.</summary>
        public static CardProgressionRecord FindRecordForTests(PlayerProfile profile, string cardId) =>
            FindRecord(profile, cardId);
    }
}
