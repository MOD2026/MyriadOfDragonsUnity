using System;
using System.Collections.Generic;
using MyriadOfDragons.Cards;

namespace MyriadOfDragons.Save
{
    /// <summary>Atomic duplicate burn: validate → consume copy → grant yield → save with rollback.</summary>
    public static class CollectionBurnService
    {
        private static readonly Dictionary<string, CollectionBurnReceiptResult> CommittedReceiptsById =
            new Dictionary<string, CollectionBurnReceiptResult>(StringComparer.Ordinal);

        public static void ClearCommittedReceiptsForTests() => CommittedReceiptsById.Clear();

        public static bool TryBurnCopy(
            PlayerProfile profile,
            string cardId,
            CollectionBurnPath path,
            string receiptId,
            out CollectionBurnReceiptResult result,
            Func<string, int> resolveRarity = null,
            Func<PlayerProfile, bool> saveFn = null)
        {
            result = new CollectionBurnReceiptResult
            {
                CardId = cardId,
                Path = path,
                ReceiptId = receiptId,
            };
            saveFn ??= SaveSystem.Save;
            resolveRarity ??= DefaultResolveRarity;

            if (profile == null || string.IsNullOrEmpty(cardId))
            {
                result.Error = CollectionBurnError.InvalidCard;
                return false;
            }

            if (!string.IsNullOrEmpty(receiptId)
                && CommittedReceiptsById.TryGetValue(receiptId, out CollectionBurnReceiptResult cached))
            {
                result = CloneResult(cached);
                return true;
            }

            if (!profile.UsesCollectionV1)
            {
                result.Error = CollectionBurnError.RequiresMigration;
                return false;
            }

            CardProgressionRecord record = CollectionProgression.FindRecordForTests(profile, cardId);
            if (record == null)
            {
                result.Error = CollectionBurnError.InvalidCard;
                return false;
            }

            if (record.copyCount < 2)
            {
                result.Error = CollectionBurnError.InsufficientCopies;
                return false;
            }

            int rarity = resolveRarity(cardId);
            int yield = CollectionBurnRules.GetYield(path, rarity);
            if (yield <= 0)
            {
                result.Error = CollectionBurnError.InvalidCard;
                return false;
            }

            BurnSnapshot snapshot = CaptureSnapshot(profile);
            result.GoldBefore = snapshot.Gold;
            result.CopiesBefore = record.copyCount;
            result.Rarity = rarity;
            result.YieldAmount = yield;

            record.copyCount--;
            ApplyYield(profile, cardId, path, rarity, yield);

            result.CopiesAfter = record.copyCount;
            result.GoldAfter = profile.gold;

            if (!saveFn(profile))
            {
                RestoreSnapshot(profile, snapshot);
                result.Error = CollectionBurnError.SaveFailed;
                result.Success = false;
                return false;
            }

            result.Success = true;
            result.Error = CollectionBurnError.None;
            if (!string.IsNullOrEmpty(receiptId))
                CommittedReceiptsById[receiptId] = CloneResult(result);
            return true;
        }

        private static void ApplyYield(PlayerProfile profile, string cardId, CollectionBurnPath path, int rarity, int yield)
        {
            CollectionSchemaMigration.NormalizeCollectionFields(profile);

            switch (path)
            {
                case CollectionBurnPath.TrainingXp:
                {
                    CardProgressionRecord xpRecord = CollectionProgression.FindRecordForTests(profile, cardId);
                    if (xpRecord != null)
                    {
                        xpRecord.trainingXp += yield;
                        CollectionTrainingRules.ApplyLevelUps(xpRecord);
                    }
                    break;
                }
                case CollectionBurnPath.GenericSacrifice:
                    profile.collectionWallet.genericSacrificeCredits += yield;
                    break;
                case CollectionBurnPath.ForgeCredit:
                    profile.collectionWallet.forgeCredits += yield;
                    break;
                case CollectionBurnPath.Dust:
                    AddDust(profile.collectionWallet, rarity, yield);
                    break;
            }
        }

        private static void AddDust(CollectionMaterialWallet wallet, int rarity, int amount)
        {
            foreach (RarityMaterialBalance entry in wallet.dustByRarity)
            {
                if (entry.rarity != rarity) continue;
                entry.dust += amount;
                return;
            }

            wallet.dustByRarity.Add(new RarityMaterialBalance { rarity = rarity, dust = amount });
        }

        private static int DefaultResolveRarity(string cardId)
        {
            Card card = CardDatabase.Instance != null ? CardDatabase.Instance.GetCard(cardId) : null;
            return card?.Rarity ?? 1;
        }

        private sealed class BurnSnapshot
        {
            public int Gold;
            public List<CardProgressionRecord> CardProgression;
            public CollectionMaterialWallet Wallet;
        }

        private static BurnSnapshot CaptureSnapshot(PlayerProfile profile)
        {
            return new BurnSnapshot
            {
                Gold = profile.gold,
                CardProgression = CloneProgression(profile.cardProgression),
                Wallet = CloneWallet(profile.collectionWallet),
            };
        }

        private static void RestoreSnapshot(PlayerProfile profile, BurnSnapshot snapshot)
        {
            profile.gold = snapshot.Gold;
            profile.cardProgression = CloneProgression(snapshot.CardProgression);
            profile.collectionWallet = CloneWallet(snapshot.Wallet);
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

        private static CollectionBurnReceiptResult CloneResult(CollectionBurnReceiptResult source) =>
            new CollectionBurnReceiptResult
            {
                Success = source.Success,
                Error = source.Error,
                ReceiptId = source.ReceiptId,
                CardId = source.CardId,
                Path = source.Path,
                Rarity = source.Rarity,
                CopiesBefore = source.CopiesBefore,
                CopiesAfter = source.CopiesAfter,
                YieldAmount = source.YieldAmount,
                GoldBefore = source.GoldBefore,
                GoldAfter = source.GoldAfter,
            };
    }
}
