using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class SpellLoadoutPickerTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsSpellLoadoutPicker_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        [Test]
        public void ResolveSelectablePool_FreshPlayer_IsStarterFourFromUnlockResolver()
        {
            var profile = new PlayerProfile { avatarLevel = 1 };
            List<AvatarSpell> pool = SpellLoadoutSelection.ResolveSelectablePool(profile);
            CollectionAssert.AreEquivalent(
                new[] { "firestorm", "mend", "war_cry", "divine_bolt" },
                pool.Select(s => s.Id).ToList());
            Assert.IsTrue(SpellLoadoutSelection.PoolCoversAllSlots(pool));
        }

        [Test]
        public void ResolveSelectablePool_IncludesOwnedSpellBookGrant()
        {
            var profile = new PlayerProfile
            {
                avatarLevel = 1,
                ownedSpellIds = new List<string> { "sun_lance" },
            };
            List<AvatarSpell> pool = SpellLoadoutSelection.ResolveSelectablePool(profile);
            Assert.IsTrue(pool.Any(s => s.Id == "sun_lance"),
                "SpellBookGrant ownership must remain equippable even though SpellUnlockResolver never unlocks it.");
        }

        [Test]
        public void TryApply_RejectsTwoDifferentHealSpells_AsDuplicateEffect()
        {
            var profile = new PlayerProfile { avatarLevel = 1, unlockedStageIds = new List<string> { "1-6" } }; // unlocks Vital Spark alongside starter Mend
            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            SpellLoadoutApplyResult result = SpellLoadoutSelection.TryApply(profile,
                new[] { "firestorm", "mend", "vital_spark", "divine_bolt" });

            Assert.AreEqual(SpellLoadoutApplyStatus.DuplicateEffect, result.Status);
        }

        [Test]
        public void TryApply_RejectsTheSameSpellIdTwice_AsDuplicateSpellId()
        {
            var profile = new PlayerProfile { avatarLevel = 1 };
            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            SpellLoadoutApplyResult result = SpellLoadoutSelection.TryApply(profile,
                new[] { "firestorm", "mend", "mend", "divine_bolt" });

            Assert.AreEqual(SpellLoadoutApplyStatus.DuplicateSpellId, result.Status);
        }

        [Test]
        public void TryApply_WritesEquippedSpellIds_InSlotOrder()
        {
            var profile = new PlayerProfile
            {
                avatarLevel = 9, // under the L10 five-slot threshold - this test is about slot ORDER/write, not the loadout expansion itself
                unlockedStageIds = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" },
            };
            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            // divine_bolt, not stone_judgment (L12) - stone_judgment's own gate would push this
            // profile's own required slot count to 5 (see SpellLoadoutAutoEquip.RequiredSlotCount),
            // which is a different test's job (SpellLoadoutExpansionTests); this one stays about
            // slot order/write at the original 4.
            SpellLoadoutApplyResult result = SpellLoadoutSelection.TryApply(profile,
                new[] { "cinder_lash", "vital_spark", "rallying_gale", "divine_bolt" });
            Assert.AreEqual(SpellLoadoutApplyStatus.Applied, result.Status);
            CollectionAssert.AreEqual(
                new[] { "cinder_lash", "vital_spark", "rallying_gale", "divine_bolt" },
                profile.equippedSpellIds);
        }

        [Test]
        public void TryApply_RejectsSpellNotInPool()
        {
            var profile = new PlayerProfile { avatarLevel = 1 };
            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            SpellLoadoutApplyResult result = SpellLoadoutSelection.TryApply(profile,
                new[] { "cinder_lash", "mend", "war_cry", "divine_bolt" });
            Assert.AreEqual(SpellLoadoutApplyStatus.NotSelectable, result.Status);
        }

        [Test]
        public void Presenter_BuildsColumns_AndConfirmWritesProfile()
        {
            var profile = new PlayerProfile
            {
                avatarLevel = 9, // under the L10 five-slot threshold - this test is about the presenter's own confirm flow, not expansion
                unlockedStageIds = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" },
                equippedSpellIds = new List<string> { "firestorm", "mend", "war_cry", "divine_bolt" },
            };
            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            var go = new GameObject("LoadoutHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<SpellLoadoutPickerPresenter>();
            presenter.Initialize(onBack: null);

            Assert.NotNull(presenter.CanvasObjectForTests);
            Assert.IsTrue(SpellLoadoutUiLibrary.HasSpellLoadoutV1Pack);
            Assert.AreEqual(SpellLoadoutUiLibrary.ScreenShellName,
                presenter.CanvasObjectForTests.transform.Find("Background")?.GetComponent<Image>()?.sprite?.name);
            Assert.AreEqual(4, presenter.RequiredSlotsForTests);
            Assert.NotNull(presenter.CanvasObjectForTests.transform.Find("EffectColumns/Column_LaneDamage"));
            Assert.NotNull(presenter.CanvasObjectForTests.transform.Find("ConfirmBar/Btn_ConfirmLoadout"));
            Assert.NotNull(presenter.CanvasObjectForTests.transform.Find("SlotSummary/Slot_3"));
            Assert.IsNull(presenter.CanvasObjectForTests.transform.Find("SlotSummary/Slot_4"),
                "L9 must still render exactly four slot cells.");

            presenter.SelectSpellForTests("cinder_lash");
            presenter.SelectSpellForTests("vital_spark");
            presenter.SelectSpellForTests("rallying_gale");
            // divine_bolt is already prefilled for AvatarStrike — re-selecting would toggle it off.

            SpellLoadoutApplyResult applied = presenter.ConfirmForTests();
            Assert.AreEqual(SpellLoadoutApplyStatus.Applied, applied.Status, applied.Message);

            PlayerProfile saved = SaveManager.SaveData;
            CollectionAssert.AreEqual(
                new[] { "cinder_lash", "vital_spark", "rallying_gale", "divine_bolt" },
                saved.equippedSpellIds);
        }

        [Test]
        public void Presenter_AtAvatarL10_RendersFiveSlots_AndConfirmWritesFive()
        {
            var profile = new PlayerProfile
            {
                avatarLevel = 10,
                unlockedStageIds = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3", "4-15" },
                equippedSpellIds = new List<string> { "firestorm", "mend", "war_cry", "divine_bolt" },
            };
            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            var go = new GameObject("LoadoutHarness_L10");
            _spawned.Add(go);
            var presenter = go.AddComponent<SpellLoadoutPickerPresenter>();
            presenter.Initialize(onBack: null);

            Assert.AreEqual(5, presenter.RequiredSlotsForTests);
            Assert.NotNull(presenter.CanvasObjectForTests.transform.Find("SlotSummary/Slot_4"));
            Assert.IsNull(presenter.CanvasObjectForTests.transform.Find("SlotSummary/Slot_5"));

            // Prefill kept the original four; pick a fifth distinct effect from the unlocked pool.
            Assert.AreEqual(4, presenter.SlotSelectionForTests.Count);
            List<AvatarSpell> pool = SpellLoadoutSelection.ResolveSelectablePool(SaveManager.SaveData);
            AvatarSpell fifth = pool.First(s =>
                !presenter.SlotSelectionForTests.ContainsKey(s.Effect));
            presenter.SelectSpellForTests(fifth.Id);

            SpellLoadoutApplyResult applied = presenter.ConfirmForTests();
            Assert.AreEqual(SpellLoadoutApplyStatus.Applied, applied.Status, applied.Message);
            Assert.AreEqual(5, SaveManager.SaveData.equippedSpellIds.Count);
            Assert.AreEqual(5, SaveManager.SaveData.equippedSpellIds.Distinct().Count());
        }

        [Test]
        public void Presenter_AtAvatarL20_RendersSixSlots_AndRefusesConfirmUntilFull()
        {
            var profile = new PlayerProfile
            {
                avatarLevel = 20,
                unlockedStageIds = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3", "4-15", "5-15", "7-15" },
                equippedSpellIds = new List<string> { "firestorm", "mend", "war_cry", "divine_bolt" },
            };
            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            var go = new GameObject("LoadoutHarness_L20");
            _spawned.Add(go);
            var presenter = go.AddComponent<SpellLoadoutPickerPresenter>();
            presenter.Initialize(onBack: null);

            Assert.AreEqual(6, presenter.RequiredSlotsForTests);
            Assert.NotNull(presenter.CanvasObjectForTests.transform.Find("SlotSummary/Slot_5"));

            SpellLoadoutApplyResult incomplete = presenter.ConfirmForTests();
            Assert.AreEqual(SpellLoadoutApplyStatus.WrongCount, incomplete.Status,
                "Migration never auto-fills newly unlocked slots — confirm must refuse until the player picks 6.");

            List<AvatarSpell> pool = SpellLoadoutSelection.ResolveSelectablePool(SaveManager.SaveData);
            foreach (AvatarSpell spell in pool)
            {
                if (presenter.SlotSelectionForTests.Count >= 6) break;
                if (presenter.SlotSelectionForTests.ContainsKey(spell.Effect)) continue;
                presenter.SelectSpellForTests(spell.Id);
            }

            Assert.AreEqual(6, presenter.SlotSelectionForTests.Count);
            SpellLoadoutApplyResult applied = presenter.ConfirmForTests();
            Assert.AreEqual(SpellLoadoutApplyStatus.Applied, applied.Status, applied.Message);
            Assert.AreEqual(6, SaveManager.SaveData.equippedSpellIds.Count);
        }

        [Test]
        public void Home_SpellsButton_OpensPicker_AndBackReturnsHome()
        {
            var profile = new PlayerProfile { avatarLevel = 1, gold = 100, gems = 50, stamina = 30, maxStamina = 100 };
            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();

            var go = new GameObject("HomeLoadoutReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            // Real reachability path since the Home IA rebuild - Spell Loadout is no longer a
            // direct Home button (register: "SpellLoadoutPicker -> modal launched from Collection
            // or Battle preparation. Never permanent navigation"). Neither of those two launch
            // points has been wired yet (real, separate follow-up work, not done in this pass) -
            // the one still-real path today is Avatar's own existing Spell Loadout button
            // (AvatarPresenter's onOpenSpellLoadout callback, unchanged by this rebuild).
            Button identityBtn = homeCanvas.transform.Find("TopHud/IdentityRoot")?.GetComponent<Button>();
            Assert.NotNull(identityBtn, "Setup: expected the identity header to open Avatar.");
            identityBtn.onClick.Invoke();
            GameObject avatarCanvas = GameObject.Find("AvatarCanvas");
            Assert.NotNull(avatarCanvas, "Setup: expected Avatar to open.");
            Button spellBtn = avatarCanvas.transform.Find("Btn_SpellLoadout")?.GetComponent<Button>();
            Assert.NotNull(spellBtn, "Setup: expected Avatar's own Spell Loadout button.");
            spellBtn.onClick.Invoke();
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(SpellLoadoutPickerPresenter.CanvasName));

            Button back = GameObject.Find(SpellLoadoutPickerPresenter.CanvasName).transform
                .Find("LoadoutHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back);
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<SpellLoadoutPickerPresenter>());
        }
    }
}
