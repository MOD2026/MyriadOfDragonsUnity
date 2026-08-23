using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>COLLECTION_PACK_RECEIPT_v1 §9/§10 — patched Claude review 2026-08-22.</summary>
    public class CollectionPackReceiptTests
    {
        private string _scratchSaveDir;
        private GameObject _databaseGo;

        [SetUp]
        public void SetUp()
        {
            CollectionPackReceiptService.ClearCommittedReceiptsForTests();
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsPackReceipt_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            _databaseGo = new GameObject("CardDatabase_PackReceiptTests");
            _databaseGo.AddComponent<CardDatabase>().Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            CollectionPackReceiptService.ClearCommittedReceiptsForTests();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_databaseGo != null) Object.DestroyImmediate(_databaseGo);

            try
            {
                if (Directory.Exists(_scratchSaveDir)) Directory.Delete(_scratchSaveDir, true);
            }
            catch (IOException) { }
        }

        [Test]
        public void InsufficientGems_NoMutation()
        {
            PlayerProfile profile = NewMigratedProfile(gems: 100, normalPity: 4, highPity5: 6, highPity7: 11);
            profile.cardProgression.Add(new CardProgressionRecord { cardId = "warrior", copyCount = 2 });
            int goldBefore = profile.gold;
            int permitBefore = profile.ascensionPermitBalance;

            bool ok = CollectionPackReceiptService.TryOpenPack(
                profile,
                CollectionPackCatalog.SingleSigilSkuId,
                new System.Random(1),
                receiptId: null,
                out PackReceiptResult result);

            Assert.IsFalse(ok);
            Assert.AreEqual(PackReceiptError.InsufficientGems, result.Error);
            Assert.AreEqual(100, profile.gems);
            Assert.AreEqual(4, profile.normalPityMisses);
            Assert.AreEqual(6, profile.highPityMissesSince5Star);
            Assert.AreEqual(11, profile.highPityMissesSince7Star);
            Assert.AreEqual(2, profile.cardProgression[0].copyCount);
            Assert.AreEqual(goldBefore, profile.gold);
            Assert.AreEqual(permitBefore, profile.ascensionPermitBalance);
        }

        [Test]
        public void EmptyPool_NoMutation_DistinctFromInsufficientGems()
        {
            PlayerProfile profile = NewMigratedProfile(gems: 500, normalPity: 3, highPity5: 2, highPity7: 7);
            profile.cardProgression.Add(new CardProgressionRecord { cardId = "warrior", copyCount = 2 });
            int goldBefore = profile.gold;
            int progressionCount = profile.cardProgression.Count;

            // Enough gems so this cannot be InsufficientGems; empty CardDatabase yields EmptyPool.
            if (_databaseGo != null)
            {
                Object.DestroyImmediate(_databaseGo);
                _databaseGo = null;
            }

            Assert.IsTrue(CardDatabase.Instance == null,
                "Setup: CardDatabase must be absent to force EmptyPool.");

            bool ok = CollectionPackReceiptService.TryOpenPack(
                profile,
                CollectionPackCatalog.SingleSigilSkuId,
                new System.Random(1),
                receiptId: null,
                out PackReceiptResult result);

            Assert.IsFalse(ok);
            Assert.AreEqual(PackReceiptError.EmptyPool, result.Error);
            Assert.AreNotEqual(PackReceiptError.InsufficientGems, result.Error);
            Assert.AreEqual(500, profile.gems);
            Assert.AreEqual(3, profile.normalPityMisses);
            Assert.AreEqual(2, profile.highPityMissesSince5Star);
            Assert.AreEqual(7, profile.highPityMissesSince7Star);
            Assert.AreEqual(progressionCount, profile.cardProgression.Count);
            Assert.AreEqual(2, profile.cardProgression[0].copyCount);
            Assert.AreEqual(goldBefore, profile.gold);
        }

        [Test]
        public void NormalDrawPity_IncrementsOnLowRarity_ResetsOnThreeStarOrHigher()
        {
            bool sawIncrement = false;
            bool sawReset = false;

            for (int seed = 0; seed < 4000 && (!sawIncrement || !sawReset); seed++)
            {
                PlayerProfile profile = NewMigratedProfile(gems: 50_000, normalPity: 4);
                if (!CollectionPackReceiptService.TryOpenPack(
                        profile,
                        CollectionPackCatalog.SingleSigilSkuId,
                        new System.Random(seed),
                        receiptId: null,
                        out PackReceiptResult result))
                    continue;

                Assert.AreEqual(1, result.Draws.Count);
                ResolvedPackDraw draw = result.Draws[0];
                Assert.AreEqual(PackDrawKind.Normal, draw.DrawKind);

                if (draw.Rarity < 3)
                {
                    Assert.AreEqual(5, profile.normalPityMisses,
                        $"seed {seed}: rarity {draw.Rarity} must increment normal pity");
                    sawIncrement = true;
                }
                else
                {
                    Assert.AreEqual(0, profile.normalPityMisses,
                        $"seed {seed}: rarity {draw.Rarity} must reset normal pity");
                    sawReset = true;
                }
            }

            Assert.IsTrue(sawIncrement, "Expected at least one Normal draw below 3★ to increment pity.");
            Assert.IsTrue(sawReset, "Expected at least one Normal draw at 3★+ to reset pity.");
        }

        [Test]
        public void SingleSigil_OneNormalDraw()
        {
            PlayerProfile profile = NewMigratedProfile(gems: 500);
            int progressionBefore = profile.cardProgression.Count;

            bool ok = CollectionPackReceiptService.TryOpenPack(
                profile,
                CollectionPackCatalog.SingleSigilSkuId,
                new System.Random(42),
                receiptId: null,
                out PackReceiptResult result);

            Assert.IsTrue(ok, result.Error.ToString());
            Assert.AreEqual(350, profile.gems);
            Assert.AreEqual(150, result.GemsSpent);
            Assert.AreEqual(1, result.Draws.Count);
            Assert.AreEqual(PackDrawKind.Normal, result.Draws[0].DrawKind);
            Assert.AreNotEqual("dragon", result.Draws[0].CardId);
            Assert.AreEqual(progressionBefore + 1, profile.cardProgression.Count);
        }

        [Test]
        public void InversePricing_Table()
        {
            Assert.IsTrue(CollectionPackCatalog.HasInverseGemPerCardOrdering());

            CollectionPackSku single = CollectionPackCatalog.AllSkus[0];
            CollectionPackSku scout = CollectionPackCatalog.AllSkus[1];
            CollectionPackSku warband = CollectionPackCatalog.AllSkus[2];
            CollectionPackSku legion = CollectionPackCatalog.AllSkus[3];

            Assert.AreEqual(150f, single.GemsPerCard, 0.001f);
            Assert.AreEqual(160f, scout.GemsPerCard, 0.001f);
            Assert.That(warband.GemsPerCard, Is.EqualTo(1650f / 9f).Within(0.01f));
            Assert.AreEqual(200f, legion.GemsPerCard, 0.001f);
            Assert.Less(single.GemsPerCard, scout.GemsPerCard);
            Assert.Less(scout.GemsPerCard, warband.GemsPerCard);
            Assert.Less(warband.GemsPerCard, legion.GemsPerCard);
        }

        [Test]
        public void HighPity_Forces5Star_On10thMiss()
        {
            PlayerProfile profile = NewMigratedProfile(gems: 5000);
            profile.highPityMissesSince5Star = 9;
            profile.highPityMissesSince7Star = 0;

            bool ok = CollectionPackReceiptService.TryOpenPack(
                profile,
                CollectionPackCatalog.ScoutCacheSkuId,
                new System.Random(7),
                receiptId: null,
                out PackReceiptResult result);

            Assert.IsTrue(ok, result.Error.ToString());
            ResolvedPackDraw highDraw = result.Draws.First(d => d.DrawKind == PackDrawKind.High);
            Assert.GreaterOrEqual(highDraw.Rarity, 5);
            Assert.IsTrue(highDraw.WasPityForced);
            Assert.AreEqual(0, profile.highPityMissesSince5Star);
        }

        [Test]
        public void HighPity_Forces7Star_On60thMiss()
        {
            PlayerProfile profile = NewMigratedProfile(gems: 5000);
            profile.highPityMissesSince7Star = 59;
            profile.highPityMissesSince5Star = 0;

            bool ok = CollectionPackReceiptService.TryOpenPack(
                profile,
                CollectionPackCatalog.ScoutCacheSkuId,
                new System.Random(11),
                receiptId: null,
                out PackReceiptResult result);

            Assert.IsTrue(ok, result.Error.ToString());
            ResolvedPackDraw highDraw = result.Draws.First(d => d.DrawKind == PackDrawKind.High);
            Assert.AreEqual(7, highDraw.Rarity);
            Assert.IsTrue(highDraw.WasPityForced);
            Assert.AreEqual(0, profile.highPityMissesSince7Star);
        }

        [Test]
        public void Pity_SurvivesSaveReload()
        {
            PlayerProfile profile = NewMigratedProfile(gems: 5000);
            profile.highPityMissesSince5Star = 3;
            profile.highPityMissesSince7Star = 12;
            profile.normalPityMisses = 5;

            Assert.IsTrue(CollectionPackReceiptService.TryOpenPack(
                profile,
                CollectionPackCatalog.SingleSigilSkuId,
                new System.Random(99),
                receiptId: null,
                out PackReceiptResult result));

            Assert.IsTrue(SaveSystem.Save(profile));

            PlayerProfile reloaded = SaveSystem.Load(out SaveLoadStatus status);
            Assert.AreEqual(SaveLoadStatus.Loaded, status);
            Assert.AreEqual(profile.normalPityMisses, reloaded.normalPityMisses);
            Assert.AreEqual(profile.highPityMissesSince5Star, reloaded.highPityMissesSince5Star);
            Assert.AreEqual(profile.highPityMissesSince7Star, reloaded.highPityMissesSince7Star);
            Assert.AreEqual(profile.cardProgression.Count, reloaded.cardProgression.Count);
            Assert.AreEqual(
                profile.cardProgression[0].copyCount,
                reloaded.cardProgression[0].copyCount);
        }

        [Test]
        public void IntraReceipt_SequentialHighPity_Warband()
        {
            const int startPity5 = 8;
            for (int seed = 0; seed < 5000; seed++)
            {
                PlayerProfile profile = NewMigratedProfile(gems: 100_000);
                profile.highPityMissesSince5Star = startPity5;
                profile.highPityMissesSince7Star = 0;

                if (!CollectionPackReceiptService.TryOpenPack(
                        profile,
                        CollectionPackCatalog.WarbandCacheSkuId,
                        new System.Random(seed),
                        receiptId: null,
                        out PackReceiptResult result))
                    continue;

                List<ResolvedPackDraw> highDraws = result.Draws
                    .Where(d => d.DrawKind == PackDrawKind.High)
                    .ToList();
                Assert.AreEqual(2, highDraws.Count);

                if (highDraws[0].Rarity >= 5) continue;
                if (!highDraws[1].WasPityForced) continue;

                Assert.GreaterOrEqual(highDraws[1].Rarity, 5);
                Assert.AreEqual(0, profile.highPityMissesSince5Star,
                    $"seed {seed}: second High must reset 5★ pity after forced hit");
                return;
            }

            Assert.Fail("No seed produced sequential Warband High pity (first miss, second forced ≥5★).");
        }

        [Test]
        public void IntraReceipt_SequentialHighPity_Legion()
        {
            const int startPity5 = 6;
            for (int seed = 0; seed < 5000; seed++)
            {
                PlayerProfile profile = NewMigratedProfile(gems: 500_000);
                profile.highPityMissesSince5Star = startPity5;
                profile.highPityMissesSince7Star = 0;

                if (!CollectionPackReceiptService.TryOpenPack(
                        profile,
                        CollectionPackCatalog.LegionCacheSkuId,
                        new System.Random(seed),
                        receiptId: null,
                        out PackReceiptResult result))
                    continue;

                List<ResolvedPackDraw> highDraws = result.Draws
                    .Where(d => d.DrawKind == PackDrawKind.High)
                    .ToList();
                Assert.AreEqual(4, highDraws.Count);

                if (highDraws[0].Rarity >= 5 || highDraws[1].Rarity >= 5 || highDraws[2].Rarity >= 5)
                    continue;
                if (!highDraws[3].WasPityForced)
                    continue;

                Assert.GreaterOrEqual(highDraws[3].Rarity, 5);
                Assert.AreEqual(0, profile.highPityMissesSince5Star,
                    $"seed {seed}: fourth High must reset 5★ pity after three sequential misses");
                return;
            }

            Assert.Fail("No seed produced sequential Legion High pity (3 misses then forced ≥5★).");
        }

        [Test]
        public void Duplicate_IncrementsCopyCount()
        {
            PlayerProfile profile = NewMigratedProfile(gems: 10000);
            var rng = new System.Random(1234);

            Assert.IsTrue(CollectionPackReceiptService.TryOpenPack(
                profile,
                CollectionPackCatalog.SingleSigilSkuId,
                rng,
                receiptId: null,
                out PackReceiptResult first));
            string firstId = first.Draws[0].CardId;

            Assert.IsTrue(CollectionPackReceiptService.TryOpenPack(
                profile,
                CollectionPackCatalog.SingleSigilSkuId,
                rng,
                receiptId: null,
                out PackReceiptResult second));
            string secondId = second.Draws[0].CardId;

            Assert.AreEqual(firstId, secondId);
            CardProgressionRecord record = profile.cardProgression.Find(r => r.cardId == firstId);
            Assert.NotNull(record);
            Assert.AreEqual(2, record.copyCount);
        }

        [Test]
        public void PlaceholderNeverDrawn()
        {
            PlayerProfile profile = NewMigratedProfile(gems: 2_000_000);
            var rng = new System.Random(2026);

            for (int i = 0; i < 1000; i++)
            {
                Assert.IsTrue(CollectionPackReceiptService.TryOpenPack(
                    profile,
                    CollectionPackCatalog.SingleSigilSkuId,
                    rng,
                    receiptId: null,
                    out PackReceiptResult result), $"draw {i}");

                foreach (ResolvedPackDraw draw in result.Draws)
                {
                    Assert.AreNotEqual("dragon", draw.CardId);
                }
            }
        }

        [Test]
        public void ScoutFloor_AtLeastOne2Star()
        {
            PlayerProfile profile = NewMigratedProfile(gems: 2_000_000);
            var rng = new System.Random(808);

            for (int i = 0; i < 100; i++)
            {
                Assert.IsTrue(CollectionPackReceiptService.TryOpenPack(
                    profile,
                    CollectionPackCatalog.ScoutCacheSkuId,
                    rng,
                    receiptId: null,
                    out PackReceiptResult result), $"bundle {i}");

                Assert.IsTrue(result.Draws.Any(d => d.Rarity >= 2), $"bundle {i} missing 2★+ floor");
            }
        }

        [Test]
        public void WarbandFloor_AtLeastOne3Star()
        {
            PlayerProfile profile = NewMigratedProfile(gems: 2_000_000);
            var rng = new System.Random(909);

            for (int i = 0; i < 100; i++)
            {
                Assert.IsTrue(CollectionPackReceiptService.TryOpenPack(
                    profile,
                    CollectionPackCatalog.WarbandCacheSkuId,
                    rng,
                    receiptId: null,
                    out PackReceiptResult result), $"bundle {i}");

                Assert.IsTrue(result.Draws.Any(d => d.Rarity >= 3), $"bundle {i} missing 3★+ floor");
            }
        }

        [Test]
        public void LegionFloor_AtLeastOne5Star()
        {
            PlayerProfile profile = NewMigratedProfile(gems: 5_000_000);
            var rng = new System.Random(1010);

            for (int i = 0; i < 100; i++)
            {
                Assert.IsTrue(CollectionPackReceiptService.TryOpenPack(
                    profile,
                    CollectionPackCatalog.LegionCacheSkuId,
                    rng,
                    receiptId: null,
                    out PackReceiptResult result), $"bundle {i}");

                Assert.IsTrue(result.Draws.Any(d => d.Rarity >= 5), $"bundle {i} missing 5★+ floor");
            }
        }

        [Test]
        public void FloorReRoll_NormalSlotOnly()
        {
            for (int seed = 0; seed < 5000; seed++)
            {
                PlayerProfile profile = NewMigratedProfile(gems: 500_000);
                profile.highPityMissesSince5Star = 4;
                profile.highPityMissesSince7Star = 7;

                if (!CollectionPackReceiptService.TryOpenPack(
                        profile,
                        CollectionPackCatalog.ScoutCacheSkuId,
                        new System.Random(seed),
                        receiptId: null,
                        out PackReceiptResult result))
                    continue;

                if (result.FloorTriggeredRarity <= 0) continue;

                Assert.IsTrue(result.Draws.Any(d => d.WasFloorReroll && d.DrawKind == PackDrawKind.Normal),
                    "Floor correction must target a Normal slot.");
                Assert.IsFalse(result.Draws.Any(d => d.WasFloorReroll && d.DrawKind == PackDrawKind.High),
                    "Floor correction must never target a High slot.");

                int expectedHighPity5 = ComputeExpectedHighPity5AfterDraws(4, result.Draws);
                int expectedHighPity7 = ComputeExpectedHighPity7AfterDraws(7, result.Draws);
                Assert.AreEqual(expectedHighPity5, profile.highPityMissesSince5Star,
                    $"seed {seed}: floor re-roll must not alter High 5★ pity");
                Assert.AreEqual(expectedHighPity7, profile.highPityMissesSince7Star,
                    $"seed {seed}: floor re-roll must not alter High 7★ pity");
                return;
            }

            Assert.Fail("No seed triggered a Scout floor re-roll within search budget.");
        }

        [Test]
        public void SaveFailure_Rollback()
        {
            PlayerProfile profile = NewMigratedProfile(gems: 500, normalPity: 2, highPity5: 3, highPity7: 4);
            profile.cardProgression.Add(new CardProgressionRecord { cardId = "warrior", copyCount = 1 });
            int goldBefore = profile.gold;
            int permitBefore = profile.ascensionPermitBalance;

            bool ok = CollectionPackReceiptService.TryOpenPack(
                profile,
                CollectionPackCatalog.SingleSigilSkuId,
                new System.Random(5),
                receiptId: "rollback-test",
                out PackReceiptResult result,
                saveFn: _ => false);

            Assert.IsFalse(ok);
            Assert.AreEqual(PackReceiptError.SaveFailed, result.Error);
            Assert.AreEqual(500, profile.gems);
            Assert.AreEqual(2, profile.normalPityMisses);
            Assert.AreEqual(3, profile.highPityMissesSince5Star);
            Assert.AreEqual(4, profile.highPityMissesSince7Star);
            Assert.AreEqual(1, profile.cardProgression.Count);
            Assert.AreEqual(1, profile.cardProgression[0].copyCount);
            Assert.AreEqual(goldBefore, profile.gold);
            Assert.AreEqual(permitBefore, profile.ascensionPermitBalance);
        }

        [Test]
        public void NeverGrantsPermitOrGold()
        {
            PlayerProfile profile = NewMigratedProfile(gems: 5000);
            profile.gold = 4242;
            profile.ascensionPermitBalance = 7;

            Assert.IsTrue(CollectionPackReceiptService.TryOpenPack(
                profile,
                CollectionPackCatalog.WarbandCacheSkuId,
                new System.Random(314),
                receiptId: null,
                out PackReceiptResult result));

            Assert.AreEqual(4242, profile.gold);
            Assert.AreEqual(7, profile.ascensionPermitBalance);
            Assert.AreEqual(9, result.Draws.Count);
        }

        [Test]
        public void Idempotency_SameReceiptId()
        {
            PlayerProfile profile = NewMigratedProfile(gems: 5000);
            const string receiptId = "idem-pack-receipt-001";

            Assert.IsTrue(CollectionPackReceiptService.TryOpenPack(
                profile,
                CollectionPackCatalog.SingleSigilSkuId,
                new System.Random(55),
                receiptId,
                out PackReceiptResult first));

            int gemsAfterFirst = profile.gems;
            int copyCountAfterFirst = TotalCopyCount(profile);

            Assert.IsTrue(CollectionPackReceiptService.TryOpenPack(
                profile,
                CollectionPackCatalog.SingleSigilSkuId,
                new System.Random(99999),
                receiptId,
                out PackReceiptResult second));

            Assert.AreEqual(gemsAfterFirst, profile.gems, "Retry must not spend Gems again.");
            Assert.AreEqual(copyCountAfterFirst, TotalCopyCount(profile), "Retry must not increment copies.");
            Assert.AreEqual(first.ReceiptId, second.ReceiptId);
            Assert.AreEqual(first.Draws[0].CardId, second.Draws[0].CardId);
            Assert.AreEqual(first.GemsSpent, second.GemsSpent);
        }

        private static int TotalCopyCount(PlayerProfile profile)
        {
            int total = 0;
            foreach (CardProgressionRecord record in profile.cardProgression)
                total += record.copyCount;
            return total;
        }

        private static int ComputeExpectedHighPity5AfterDraws(int startCounter, IReadOnlyList<ResolvedPackDraw> draws)
        {
            int counter = startCounter;
            foreach (ResolvedPackDraw draw in draws)
            {
                if (draw.DrawKind != PackDrawKind.High) continue;
                if (draw.Rarity < 5) counter++;
                else counter = 0;
            }

            return counter;
        }

        private static int ComputeExpectedHighPity7AfterDraws(int startCounter, IReadOnlyList<ResolvedPackDraw> draws)
        {
            int counter = startCounter;
            foreach (ResolvedPackDraw draw in draws)
            {
                if (draw.DrawKind != PackDrawKind.High) continue;
                if (draw.Rarity < 7) counter++;
                else counter = 0;
            }

            return counter;
        }

        private static PlayerProfile NewMigratedProfile(
            int gems,
            int normalPity = 0,
            int highPity5 = 0,
            int highPity7 = 0)
        {
            var profile = new PlayerProfile
            {
                gems = gems,
                normalPityMisses = normalPity,
                highPityMissesSince5Star = highPity5,
                highPityMissesSince7Star = highPity7,
            };
            CollectionSchemaMigration.Apply(profile);
            return profile;
        }
    }
}
