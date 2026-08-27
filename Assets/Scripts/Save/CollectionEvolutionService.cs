using System;
using System.Collections.Generic;
using MyriadOfDragons.Cards;

namespace MyriadOfDragons.Save
{
    /// <summary>Atomic evolution step: consume dupe + gold (+ permit when required) → bump evolutionStep.</summary>
    public static class CollectionEvolutionService
    {
        private static readonly Dictionary<string, CollectionEvolutionReceiptResult> CommittedReceiptsById =
            new Dictionary<string, CollectionEvolutionReceiptResult>(StringComparer.Ordinal);

        public static void ClearCommittedReceiptsForTests() => CommittedReceiptsById.Clear();

        public static bool TryEvolve(
            PlayerProfile profile,
            string cardId,
            string receiptId,
            out CollectionEvolutionReceiptResult result,
            Func<string, int> resolveRarity = null,
            Func<PlayerProfile, bool> saveFn = null)
        {
            result = new CollectionEvolutionReceiptResult { CardId = cardId, ReceiptId = receiptId };
            saveFn ??= SaveSystem.Save;
            resolveRarity ??= DefaultResolveRarity;

            if (profile == null || string.IsNullOrEmpty(cardId))
            {
                result.Error = CollectionEvolutionError.InvalidCard;
                return false;
            }

            if (!string.IsNullOrEmpty(receiptId)
                && CommittedReceiptsById.TryGetValue(receiptId, out CollectionEvolutionReceiptResult cached))
            {
                result = CloneResult(cached);
                return true;
            }

            if (!profile.UsesCollectionV1)
            {
                result.Error = CollectionEvolutionError.RequiresMigration;
                return false;
            }

            CardProgressionRecord record = CollectionProgression.FindRecordForTests(profile, cardId);
            if (record == null)
            {
                result.Error = CollectionEvolutionError.InvalidCard;
                return false;
            }

            if (record.evolutionStep >= CollectionEvolutionRules.MaxEvolutionStep)
            {
                result.Error = CollectionEvolutionError.MaxStepReached;
                return false;
            }

            if (record.copyCount < 2)
            {
                result.Error = CollectionEvolutionError.InsufficientCopies;
                return false;
            }

            int rarity = resolveRarity(cardId);
            int baseGoldCost = CollectionEvolutionRules.GoldCostForNextStep(rarity, record.evolutionStep);
            int goldDue = CollectionEvolutionRules.ComputeGoldDue(
                profile, rarity, baseGoldCost, out int forgeApplied, out int dustApplied);
            bool permitRequired = CollectionEvolutionRules.RequiresAscensionPermit(record.evolutionStep);
            int sacrificeRequired = CollectionEvolutionRules.GenericSacrificeCreditsRequired(record.evolutionStep);

            if (profile.gold < goldDue)
            {
                result.Error = CollectionEvolutionError.InsufficientGold;
                return false;
            }

            if (sacrificeRequired > 0
                && (profile.collectionWallet?.genericSacrificeCredits ?? 0) < sacrificeRequired)
            {
                result.Error = CollectionEvolutionError.InsufficientSacrificeCredits;
                return false;
            }

            if (permitRequired && profile.ascensionPermitBalance < 1)
            {
                result.Error = CollectionEvolutionError.PermitRequired;
                return false;
            }

            EvolutionSnapshot snapshot = CaptureSnapshot(profile);
            result.Rarity = rarity;
            result.EvolutionStepBefore = record.evolutionStep;
            result.CopiesBefore = record.copyCount;
            result.GoldSpent = goldDue;
            result.ForgeCreditsSpent = forgeApplied;
            result.DustSpent = dustApplied;
            result.SacrificeCreditsSpent = sacrificeRequired;

            record.copyCount--;
            record.evolutionStep++;
            profile.gold -= goldDue;
            CollectionEvolutionRules.ApplyMaterialSpend(profile, rarity, forgeApplied, dustApplied, sacrificeRequired);

            result.PermitSpent = false;
            if (permitRequired)
            {
                if (!CollectionAscensionPermits.TrySpendOne(profile))
                {
                    RestoreSnapshot(profile, snapshot);
                    result.Error = CollectionEvolutionError.PermitRequired;
                    return false;
                }

                result.PermitSpent = true;
            }

            result.EvolutionStepAfter = record.evolutionStep;
            result.CopiesAfter = record.copyCount;

            bool saved;
            try
            {
                saved = saveFn(profile);
            }
            catch
            {
                // A throwing save leaves the profile mutated with the copy, gold, materials and
                // any spent permit already consumed. Restore before the exception escapes.
                RestoreSnapshot(profile, snapshot);
                throw;
            }

            if (!saved)
            {
                RestoreSnapshot(profile, snapshot);
                result.Error = CollectionEvolutionError.SaveFailed;
                result.Success = false;
                return false;
            }

            result.Success = true;
            result.Error = CollectionEvolutionError.None;
            if (!string.IsNullOrEmpty(receiptId))
                CommittedReceiptsById[receiptId] = CloneResult(result);
            return true;
        }

        private static int DefaultResolveRarity(string cardId)
        {
            Card card = CardDatabase.Instance != null ? CardDatabase.Instance.GetCard(cardId) : null;
            return card?.Rarity ?? 1;
        }

        private sealed class EvolutionSnapshot
        {
            public int Gold;
            public int AscensionPermitBalance;
            public List<CardProgressionRecord> CardProgression;
            public CollectionMaterialWallet Wallet;
        }

        private static EvolutionSnapshot CaptureSnapshot(PlayerProfile profile) =>
            new EvolutionSnapshot
            {
                Gold = profile.gold,
                AscensionPermitBalance = profile.ascensionPermitBalance,
                CardProgression = CloneProgression(profile.cardProgression),
                Wallet = CloneWallet(profile.collectionWallet),
            };

        private static void RestoreSnapshot(PlayerProfile profile, EvolutionSnapshot snapshot)
        {
            profile.gold = snapshot.Gold;
            profile.ascensionPermitBalance = snapshot.AscensionPermitBalance;
            profile.cardProgression = CloneProgression(snapshot.CardProgression);
            profile.collectionWallet = CloneWallet(snapshot.Wallet);
        }

        private static CollectionMaterialWallet CloneWallet(CollectionMaterialWallet source)
        {
            var wallet = new CollectionMaterialWallet();
            if (source == null) return wallet;

            wallet.genericSacrificeCredits = source.genericSacrificeCredits;
            wallet.forgeCredits = source.forgeCredits;
            foreach (RarityMaterialBalance entry in source.dustByRarity ?? new List<RarityMaterialBalance>())
            {
                wallet.dustByRarity.Add(new RarityMaterialBalance { rarity = entry.rarity, dust = entry.dust });
            }

            return wallet;
        }

        private static List<CardProgressionRecord> CloneProgression(List<CardProgressionRecord> source)
        {
            var copy = new List<CardProgressionRecord>();
            if (source == null) return copy;

            foreach (CardProgressionRecord record in source)
            {
                copy.Add(new CardProgressionRecord
                {
                    cardId = record.cardId,
                    copyCount = record.copyCount,
                    cardLevel = record.cardLevel,
                    evolutionStep = record.evolutionStep,
                    trainingXp = record.trainingXp,
                });
            }

            return copy;
        }

        private static CollectionEvolutionReceiptResult CloneResult(CollectionEvolutionReceiptResult source) =>
            new CollectionEvolutionReceiptResult
            {
                Success = source.Success,
                Error = source.Error,
                ReceiptId = source.ReceiptId,
                CardId = source.CardId,
                Rarity = source.Rarity,
                GoldSpent = source.GoldSpent,
                ForgeCreditsSpent = source.ForgeCreditsSpent,
                DustSpent = source.DustSpent,
                SacrificeCreditsSpent = source.SacrificeCreditsSpent,
                PermitSpent = source.PermitSpent,
                EvolutionStepBefore = source.EvolutionStepBefore,
                EvolutionStepAfter = source.EvolutionStepAfter,
                CopiesBefore = source.CopiesBefore,
                CopiesAfter = source.CopiesAfter,
            };
    }
}
