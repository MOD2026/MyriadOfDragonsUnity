using System;
using System.Collections.Generic;
using MyriadOfDragons.Cards;
using UnityEngine;

namespace MyriadOfDragons.Save
{
    /// <summary>
    /// Atomic gem-pack open: validate → resolve draws → mutate profile → save with rollback.
    /// Shop UI must call this instead of writing collection fields directly.
    /// </summary>
    public static class CollectionPackReceiptService
    {
        private const string ExcludedPlaceholderId = "dragon";

        private static readonly int[] NormalRarityThresholdsBps =
        {
            5500,  // 1★
            8000,  // 2★
            9200,  // 3★
            9700,  // 4★
            9900,  // 5★
            9980,  // 6★
            10000, // 7★
        };

        private static readonly int[] HighRarityThresholdsBps =
        {
            2000,
            4500,
            7000,
            8600,
            9500,
            9900,
            10000,
        };

        private static readonly Dictionary<string, PackReceiptResult> CommittedReceiptsById =
            new Dictionary<string, PackReceiptResult>(StringComparer.Ordinal);

        /// <summary>Test isolation — clears Phase 1 idempotency cache.</summary>
        public static void ClearCommittedReceiptsForTests() => CommittedReceiptsById.Clear();

        public static bool TryOpenPack(
            PlayerProfile profile,
            string skuId,
            System.Random rng,
            string receiptId,
            out PackReceiptResult result,
            Func<PlayerProfile, bool> saveFn = null)
        {
            result = new PackReceiptResult { SkuId = skuId, ReceiptId = receiptId };
            saveFn ??= SaveSystem.Save;

            if (profile == null)
            {
                result.Error = PackReceiptError.InvalidSku;
                return false;
            }

            if (!string.IsNullOrEmpty(receiptId)
                && CommittedReceiptsById.TryGetValue(receiptId, out PackReceiptResult cached))
            {
                result = CloneResult(cached);
                return true;
            }

            if (!profile.UsesCollectionV1)
            {
                result.Error = PackReceiptError.RequiresMigration;
                return false;
            }

            if (!CollectionPackCatalog.TryGetSku(skuId, out CollectionPackSku sku))
            {
                result.Error = PackReceiptError.InvalidSku;
                return false;
            }

            if (profile.gems < sku.GemCost)
            {
                result.Error = PackReceiptError.InsufficientGems;
                return false;
            }

            if (CardDatabase.Instance == null || !TryBuildGrantablePools(out Dictionary<int, List<Card>> poolsByRarity))
            {
                result.Error = PackReceiptError.EmptyPool;
                return false;
            }

            ProfileSnapshot snapshot = CaptureSnapshot(profile);
            result.GemsBefore = snapshot.Gems;
            result.NormalPityBefore = snapshot.NormalPityMisses;
            result.HighPityBefore5Star = snapshot.HighPityMissesSince5Star;
            result.HighPityBefore7Star = snapshot.HighPityMissesSince7Star;

            int workingNormalPity = snapshot.NormalPityMisses;
            int workingHighPity5 = snapshot.HighPityMissesSince5Star;
            int workingHighPity7 = snapshot.HighPityMissesSince7Star;

            var drawQueue = BuildDrawQueue(sku);
            var resolvedDraws = new List<ResolvedPackDraw>(drawQueue.Count);

            foreach (PackDrawKind drawKind in drawQueue)
            {
                if (!TryResolveDraw(
                        drawKind,
                        rng,
                        poolsByRarity,
                        ref workingNormalPity,
                        ref workingHighPity5,
                        ref workingHighPity7,
                        out ResolvedPackDraw draw))
                {
                    result.Error = PackReceiptError.EmptyPool;
                    return false;
                }

                resolvedDraws.Add(draw);
            }

            int floorTriggeredRarity = 0;
            if (sku.FloorMinRarity > 0)
            {
                floorTriggeredRarity = ApplyBundleFloor(
                    resolvedDraws, sku.FloorMinRarity, rng, poolsByRarity);
            }

            profile.gems = snapshot.Gems - sku.GemCost;
            ApplyDrawMutations(profile, resolvedDraws);
            profile.normalPityMisses = workingNormalPity;
            profile.highPityMissesSince5Star = workingHighPity5;
            profile.highPityMissesSince7Star = workingHighPity7;

            if (!saveFn(profile))
            {
                RestoreSnapshot(profile, snapshot);
                result.Error = PackReceiptError.SaveFailed;
                return false;
            }

            result.Success = true;
            result.Error = PackReceiptError.None;
            if (string.IsNullOrEmpty(receiptId))
                result.ReceiptId = Guid.NewGuid().ToString("N");
            result.GemsSpent = sku.GemCost;
            result.GemsAfter = profile.gems;
            result.NormalPityAfter = profile.normalPityMisses;
            result.HighPityAfter5Star = profile.highPityMissesSince5Star;
            result.HighPityAfter7Star = profile.highPityMissesSince7Star;
            result.FloorTriggeredRarity = floorTriggeredRarity;
            result.Draws = resolvedDraws;

            if (!string.IsNullOrEmpty(result.ReceiptId))
                CommittedReceiptsById[result.ReceiptId] = CloneResult(result);

            return true;
        }

        private static List<PackDrawKind> BuildDrawQueue(CollectionPackSku sku)
        {
            var queue = new List<PackDrawKind>(sku.TotalDrawCount);
            for (int i = 0; i < sku.NormalDrawCount; i++) queue.Add(PackDrawKind.Normal);
            for (int i = 0; i < sku.HighDrawCount; i++) queue.Add(PackDrawKind.High);
            return queue;
        }

        private static bool TryResolveDraw(
            PackDrawKind drawKind,
            System.Random rng,
            Dictionary<int, List<Card>> poolsByRarity,
            ref int normalPityMisses,
            ref int highPityMissesSince5Star,
            ref int highPityMissesSince7Star,
            out ResolvedPackDraw draw)
        {
            draw = default;
            draw.DrawKind = drawKind;

            int rarity;
            bool pityForced = false;

            if (drawKind == PackDrawKind.High)
            {
                if (highPityMissesSince7Star >= CollectionSchemaRules.HighPityMissesFor7Star - 1)
                {
                    rarity = 7;
                    pityForced = true;
                }
                else if (highPityMissesSince5Star >= CollectionSchemaRules.HighPityMissesFor5StarPlus - 1)
                {
                    if (!TryPickRandomFromMinimumRarity(rng, poolsByRarity, 5, out Card pityCard))
                        return false;

                    draw.CardId = pityCard.Id;
                    draw.Rarity = pityCard.Rarity;
                    draw.WasPityForced = true;
                    ApplyHighPityAfterDraw(draw.Rarity, ref highPityMissesSince5Star, ref highPityMissesSince7Star);
                    return true;
                }
                else if (!TryRollRarity(rng, HighRarityThresholdsBps, out rarity))
                {
                    return false;
                }
            }
            else if (!TryRollRarity(rng, NormalRarityThresholdsBps, out rarity))
            {
                return false;
            }

            if (!TryPickRandomCardAtRarity(rng, poolsByRarity, rarity, out Card card))
                return false;

            draw.CardId = card.Id;
            draw.Rarity = card.Rarity;
            draw.WasPityForced = pityForced;

            if (drawKind == PackDrawKind.Normal)
            {
                ApplyNormalPityAfterDraw(draw.Rarity, ref normalPityMisses);
            }
            else
            {
                ApplyHighPityAfterDraw(draw.Rarity, ref highPityMissesSince5Star, ref highPityMissesSince7Star);
            }

            return true;
        }

        private static void ApplyNormalPityAfterDraw(int rarity, ref int normalPityMisses)
        {
            if (rarity < 3) normalPityMisses++;
            else normalPityMisses = 0;
        }

        private static void ApplyHighPityAfterDraw(
            int rarity,
            ref int highPityMissesSince5Star,
            ref int highPityMissesSince7Star)
        {
            if (rarity < 5) highPityMissesSince5Star++;
            else highPityMissesSince5Star = 0;

            if (rarity < 7) highPityMissesSince7Star++;
            else highPityMissesSince7Star = 0;
        }

        private static int ApplyBundleFloor(
            List<ResolvedPackDraw> draws,
            int floorMinRarity,
            System.Random rng,
            Dictionary<int, List<Card>> poolsByRarity)
        {
            int maxRarity = 0;
            foreach (ResolvedPackDraw draw in draws)
            {
                if (draw.Rarity > maxRarity) maxRarity = draw.Rarity;
            }

            if (maxRarity >= floorMinRarity) return 0;

            int lowestIndex = -1;
            int lowestRarity = int.MaxValue;
            for (int i = 0; i < draws.Count; i++)
            {
                if (draws[i].DrawKind != PackDrawKind.Normal) continue;
                if (draws[i].Rarity < lowestRarity)
                {
                    lowestRarity = draws[i].Rarity;
                    lowestIndex = i;
                }
            }

            if (lowestIndex < 0) return 0;

            if (!TryPickRandomCardAtRarity(rng, poolsByRarity, floorMinRarity, out Card replacement))
                return 0;

            ResolvedPackDraw rerolled = draws[lowestIndex];
            rerolled.CardId = replacement.Id;
            rerolled.Rarity = replacement.Rarity;
            rerolled.WasFloorReroll = true;
            draws[lowestIndex] = rerolled;
            return floorMinRarity;
        }

        private static bool TryRollRarity(System.Random rng, int[] thresholdsBps, out int rarity)
        {
            int roll = rng.Next(10000);
            for (int i = 0; i < thresholdsBps.Length; i++)
            {
                if (roll < thresholdsBps[i])
                {
                    rarity = i + 1;
                    return true;
                }
            }

            rarity = 0;
            return false;
        }

        private static bool TryPickRandomCardAtRarity(
            System.Random rng,
            Dictionary<int, List<Card>> poolsByRarity,
            int rarity,
            out Card card)
        {
            card = null;
            if (!poolsByRarity.TryGetValue(rarity, out List<Card> pool) || pool.Count == 0)
                return false;

            card = pool[rng.Next(pool.Count)];
            return card != null;
        }

        private static bool TryPickRandomFromMinimumRarity(
            System.Random rng,
            Dictionary<int, List<Card>> poolsByRarity,
            int minimumRarity,
            out Card card)
        {
            var eligible = new List<Card>();
            for (int rarity = minimumRarity; rarity <= 7; rarity++)
            {
                if (poolsByRarity.TryGetValue(rarity, out List<Card> pool))
                    eligible.AddRange(pool);
            }

            if (eligible.Count == 0)
            {
                card = null;
                return false;
            }

            card = eligible[rng.Next(eligible.Count)];
            return true;
        }

        private static bool TryBuildGrantablePools(out Dictionary<int, List<Card>> poolsByRarity)
        {
            poolsByRarity = new Dictionary<int, List<Card>>();
            for (int rarity = 1; rarity <= 7; rarity++)
            {
                poolsByRarity[rarity] = new List<Card>();
            }

            foreach (Card card in CardDatabase.Instance.AllCards)
            {
                if (card == null || string.IsNullOrEmpty(card.Id)) continue;
                if (string.Equals(card.Id, ExcludedPlaceholderId, StringComparison.Ordinal)) continue;

                int rarity = Mathf.Clamp(card.Rarity, 1, 7);
                poolsByRarity[rarity].Add(card);
            }

            int total = 0;
            foreach (List<Card> pool in poolsByRarity.Values) total += pool.Count;
            return total > 0;
        }

        private static void ApplyDrawMutations(PlayerProfile profile, List<ResolvedPackDraw> draws)
        {
            CollectionSchemaMigration.NormalizeCollectionFields(profile);

            foreach (ResolvedPackDraw draw in draws)
            {
                CardProgressionRecord record = FindRecord(profile, draw.CardId);
                if (record == null)
                {
                    profile.cardProgression.Add(new CardProgressionRecord
                    {
                        cardId = draw.CardId,
                        copyCount = 1,
                        cardLevel = 1,
                        evolutionStep = 0,
                        trainingXp = 0,
                    });
                }
                else
                {
                    record.copyCount++;
                }
            }
        }

        private static CardProgressionRecord FindRecord(PlayerProfile profile, string cardId)
        {
            if (profile.cardProgression == null || string.IsNullOrEmpty(cardId)) return null;
            foreach (CardProgressionRecord record in profile.cardProgression)
            {
                if (record != null
                    && string.Equals(record.cardId, cardId, StringComparison.Ordinal))
                    return record;
            }

            return null;
        }

        private sealed class ProfileSnapshot
        {
            public int Gems;
            public int Gold;
            public int AscensionPermitBalance;
            public int NormalPityMisses;
            public int HighPityMissesSince5Star;
            public int HighPityMissesSince7Star;
            public List<CardProgressionRecord> CardProgression;
        }

        private static ProfileSnapshot CaptureSnapshot(PlayerProfile profile)
        {
            var snapshot = new ProfileSnapshot
            {
                Gems = profile.gems,
                Gold = profile.gold,
                AscensionPermitBalance = profile.ascensionPermitBalance,
                NormalPityMisses = profile.normalPityMisses,
                HighPityMissesSince5Star = profile.highPityMissesSince5Star,
                HighPityMissesSince7Star = profile.highPityMissesSince7Star,
                CardProgression = CloneProgression(profile.cardProgression),
            };
            return snapshot;
        }

        private static void RestoreSnapshot(PlayerProfile profile, ProfileSnapshot snapshot)
        {
            profile.gems = snapshot.Gems;
            profile.gold = snapshot.Gold;
            profile.ascensionPermitBalance = snapshot.AscensionPermitBalance;
            profile.normalPityMisses = snapshot.NormalPityMisses;
            profile.highPityMissesSince5Star = snapshot.HighPityMissesSince5Star;
            profile.highPityMissesSince7Star = snapshot.HighPityMissesSince7Star;
            profile.cardProgression = CloneProgression(snapshot.CardProgression);
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

        private static PackReceiptResult CloneResult(PackReceiptResult source)
        {
            var clone = new PackReceiptResult
            {
                Success = source.Success,
                Error = source.Error,
                ReceiptId = source.ReceiptId,
                SkuId = source.SkuId,
                GemsSpent = source.GemsSpent,
                GemsBefore = source.GemsBefore,
                GemsAfter = source.GemsAfter,
                NormalPityBefore = source.NormalPityBefore,
                NormalPityAfter = source.NormalPityAfter,
                HighPityBefore5Star = source.HighPityBefore5Star,
                HighPityAfter5Star = source.HighPityAfter5Star,
                HighPityBefore7Star = source.HighPityBefore7Star,
                HighPityAfter7Star = source.HighPityAfter7Star,
                FloorTriggeredRarity = source.FloorTriggeredRarity,
            };

            foreach (ResolvedPackDraw draw in source.Draws)
                clone.Draws.Add(draw);

            return clone;
        }
    }
}
