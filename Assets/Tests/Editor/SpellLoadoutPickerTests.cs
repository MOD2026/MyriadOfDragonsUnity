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
        public void TryApply_RejectsTwoHeals()
        {
            var profile = new PlayerProfile { avatarLevel = 1 };
            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            SpellLoadoutApplyResult result = SpellLoadoutSelection.TryApply(profile,
                new[] { "firestorm", "mend", "vital_spark", "divine_bolt" });
            // vital_spark is heal - but fresh pool may not have it. Use war_cry replaced wrongly:
            result = SpellLoadoutSelection.TryApply(profile,
                new[] { "firestorm", "mend", "mend", "divine_bolt" });
            Assert.AreEqual(SpellLoadoutApplyStatus.DuplicateEffect, result.Status);
        }

        [Test]
        public void TryApply_WritesEquippedSpellIds_InSlotOrder()
        {
            var profile = new PlayerProfile
            {
                avatarLevel = 12,
                unlockedStageIds = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" },
            };
            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            SpellLoadoutApplyResult result = SpellLoadoutSelection.TryApply(profile,
                new[] { "cinder_lash", "vital_spark", "rallying_gale", "stone_judgment" });
            Assert.AreEqual(SpellLoadoutApplyStatus.Applied, result.Status);
            CollectionAssert.AreEqual(
                new[] { "cinder_lash", "vital_spark", "rallying_gale", "stone_judgment" },
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
                avatarLevel = 12,
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
            Assert.NotNull(presenter.CanvasObjectForTests.transform.Find("EffectColumns/Column_LaneDamage"));
            Assert.NotNull(presenter.CanvasObjectForTests.transform.Find("ConfirmBar/Btn_ConfirmLoadout"));

            presenter.SelectSpellForTests("cinder_lash");
            presenter.SelectSpellForTests("vital_spark");
            presenter.SelectSpellForTests("rallying_gale");
            presenter.SelectSpellForTests("stone_judgment");

            SpellLoadoutApplyResult applied = presenter.ConfirmForTests();
            Assert.AreEqual(SpellLoadoutApplyStatus.Applied, applied.Status);

            PlayerProfile saved = SaveManager.SaveData;
            CollectionAssert.AreEqual(
                new[] { "cinder_lash", "vital_spark", "rallying_gale", "stone_judgment" },
                saved.equippedSpellIds);
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

            Button openBtn = homeCanvas.transform.Find("Btn_SpellLoadout")?.GetComponent<Button>();
            Assert.NotNull(openBtn);
            openBtn.onClick.Invoke();
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
