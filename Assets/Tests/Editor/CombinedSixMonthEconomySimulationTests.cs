using System;
using System.IO;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.Season;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Combined six-month economy simulation — real production APIs only.
    /// Asserts relationships and the locked F2P Gold total (Circuit + Expedition + free BP),
    /// not invented VIP→Gold conversions.
    /// </summary>
    public class CombinedSixMonthEconomySimulationTests
    {
        private static readonly DateTime StartUtc =
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsSixMonthEcon_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            VipSubscriptionOpenValues.SetNowUtcTicksForTests(null);
        }

        [TearDown]
        public void TearDown()
        {
            VipSubscriptionOpenValues.SetNowUtcTicksForTests(null);
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
        }

        [Test]
        public void EmpireL30Sink_MatchesLockedCombinedCastleBarracksGateTotal()
        {
            Assert.AreEqual(1_779_550,
                CombinedSixMonthEconomySimulation.EmpireL30GoldSinkFromL1());
            Assert.AreEqual(15_000, CombinedSixMonthEconomySimulation.SumFreeTierGold());
            Assert.AreEqual(30_000, CombinedSixMonthEconomySimulation.SumPaidTierGold());
        }

        [Test]
        public void F2PActive_SixMonth_RepeatableGold_MatchesLockedCombinedTotal()
        {
            // Locked BS arithmetic (register): Circuit max day * 183 + Expedition cap * 183 +
            // 6 complete free-pass seasons. Weekly Circuit bonus is clipped by MaxGoldPerDay when
            // all three trials already fill the day — engine behavior, not spreadsheet add-on.
            int circuit = SoloCollectionCircuit.MaxGoldPerDay * CombinedSixMonthEconomySimulation.SimulationDays;
            int expedition = EmpireExpeditionOpenValues.DailyExpeditionGoldCap.Value
                * CombinedSixMonthEconomySimulation.SimulationDays;
            int seasons = CombinedSixMonthEconomySimulation.SimulationDays
                / BattlePassOpenValues.SeasonLengthDays;
            int battlePass = seasons * CombinedSixMonthEconomySimulation.SumFreeTierGold();
            int expected = circuit + expedition + battlePass;
            Assert.AreEqual(483_450, expected, "Locked F2P six-month Gold total drifted.");

            CombinedSixMonthEconomySimulation.Ledger ledger =
                CombinedSixMonthEconomySimulation.Run(
                    CombinedSixMonthEconomySimulation.PersonaKind.F2PActive, StartUtc);

            Assert.AreEqual(CombinedSixMonthEconomySimulation.SimulationDays, ledger.DaysSimulated);
            Assert.AreEqual(circuit, ledger.SoloCircuitGold);
            Assert.AreEqual(expedition, ledger.ExpeditionGold);
            Assert.AreEqual(battlePass, ledger.BattlePassFreeGoldClaimed);
            Assert.AreEqual(0, ledger.BattlePassPaidGoldClaimed,
                "Paid track must refuse until premium unlock persists on the profile.");
            Assert.AreEqual(0, ledger.LoyaltyGoldClaimed,
                "F2P should not reach Gold loyalty rungs (held voucher queue at 250).");
            Assert.AreEqual(expected, ledger.TotalRepeatableGoldEarned);
            Assert.AreEqual(1_779_550, ledger.EmpireL30GoldSink);

            // Relationship: six-month F2P farms are a minority of the Empire L30 sink.
            Assert.Less(ledger.TotalRepeatableGoldEarned, ledger.EmpireL30GoldSink);
            float share = ledger.TotalRepeatableGoldEarned / (float)ledger.EmpireL30GoldSink;
            Assert.Greater(share, 0.20f);
            Assert.Less(share, 0.35f);
        }

        [Test]
        public void RegularAndWhale_SpendGems_VIPAndStamina_LoyaltyGoldStillBlockedByHeldVouchers()
        {
            CombinedSixMonthEconomySimulation.Ledger regular =
                CombinedSixMonthEconomySimulation.Run(
                    CombinedSixMonthEconomySimulation.PersonaKind.RegularSpender, StartUtc);
            CombinedSixMonthEconomySimulation.Ledger whale =
                CombinedSixMonthEconomySimulation.Run(
                    CombinedSixMonthEconomySimulation.PersonaKind.Whale, StartUtc);

            Assert.Greater(regular.GemsSpent, 0);
            Assert.Greater(regular.VipSubscribeCount, 0);
            Assert.Greater(regular.VipGemsSpent, 0);
            Assert.GreaterOrEqual(regular.VipStaminaClaimsApplied + regular.VipStaminaClaimsForfeited, 1);

            Assert.Greater(whale.GemsSpent, regular.GemsSpent);
            Assert.Greater(whale.ShopStaminaPurchases, regular.ShopStaminaPurchases);
            Assert.GreaterOrEqual(whale.LoyaltyPointsEarned, 8000,
                "Whale Gem seed + ladder + VIP should reach the top Loyalty rung in points.");

            // Engine truth: voucher durations still held → ascending claim queue stops at 250 →
            // Gold rungs never pay. Sim must report that, not invent unlocked vouchers.
            Assert.AreEqual(0, regular.LoyaltyGoldClaimed);
            Assert.AreEqual(0, whale.LoyaltyGoldClaimed);
            Assert.Greater(whale.LoyaltyClaimsBlockedAtHeldVoucher, 0);

            // Same free Gold farms as F2P; paid BP table is configured but not claimable.
            Assert.AreEqual(483_450,
                regular.SoloCircuitGold + regular.ExpeditionGold + regular.BattlePassFreeGoldClaimed);
            Assert.AreEqual(0, whale.BattlePassPaidGoldClaimed);
            Assert.AreEqual(6 * CombinedSixMonthEconomySimulation.SumPaidTierGold(),
                whale.BattlePassConfiguredPaidTrackGold);
        }
    }
}
