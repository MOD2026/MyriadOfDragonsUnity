using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>Block X — Collection Sort (Default / Name A–Z / Rarity). Composes with Filter + search.</summary>
    public class CollectionSortTests
    {
        private string _scratchSaveDir;
        private GameObject _databaseGo;
        private GameObject _presenterGo;

        private string _idLowNameHighRarity;
        private string _idHighNameLowRarity;
        private string _idMidNameMidRarity;
        private CardClass _sharedClass;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsCollectionSort_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            _databaseGo = new GameObject("CardDatabase_CollectionSort");
            _databaseGo.AddComponent<CardDatabase>().Initialize();

            // Three cards with distinct display-name order and rarity order, preferably same class
            // so filter composition is easy to assert.
            Card[] pool = CardDatabase.Instance.AllCards
                .Where(c => c != null && !string.IsNullOrEmpty(c.DisplayName))
                .OrderBy(c => c.Id)
                .ToArray();
            Assert.GreaterOrEqual(pool.Length, 3, "Setup: need at least 3 cards.");

            // Prefer one class with ≥3 cards and varied rarities.
            var byClass = pool.GroupBy(c => c.Class).OrderByDescending(g => g.Count()).First();
            _sharedClass = byClass.Key;
            Card[] classPool = byClass.OrderBy(c => c.DisplayName, System.StringComparer.OrdinalIgnoreCase).ToArray();
            if (classPool.Length < 3)
                classPool = pool.OrderBy(c => c.DisplayName, System.StringComparer.OrdinalIgnoreCase).ToArray();

            // Pick A (low name), B (mid), C (high name) then reassign by rarity extremes for rarity assert.
            Card nameFirst = classPool[0];
            Card nameLast = classPool[classPool.Length - 1];
            Card nameMid = classPool[classPool.Length / 2];
            if (nameMid.Id == nameFirst.Id || nameMid.Id == nameLast.Id)
                nameMid = classPool[1];

            _idLowNameHighRarity = nameFirst.Id;
            _idHighNameLowRarity = nameLast.Id;
            _idMidNameMidRarity = nameMid.Id;

            // If rarities are identical, Name sort still changes order vs Default when load order ≠ name order.
            Assert.AreNotEqual(_idLowNameHighRarity, _idHighNameLowRarity);
        }

        [TearDown]
        public void TearDown()
        {
            if (_presenterGo != null) Object.DestroyImmediate(_presenterGo);
            if (_databaseGo != null) Object.DestroyImmediate(_databaseGo);
            Object.DestroyImmediate(GameObject.Find("CollectionCanvas"));
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchSaveDir))
            {
                try { Directory.Delete(_scratchSaveDir, true); }
                catch (IOException) { }
            }
        }

        [Test]
        public void Sort_DefaultVsName_ChangesOrder()
        {
            // Load order deliberately reverse of name A–Z.
            string[] loadOrder = { _idHighNameLowRarity, _idMidNameMidRarity, _idLowNameHighRarity };
            SaveOwned(loadOrder);
            CollectionPresenter presenter = SpawnCollection();

            Assert.AreEqual("Sort: Default", presenter.SortLabelForTests);
            CollectionAssert.AreEqual(loadOrder, presenter.VisibleCardIdsForTests.ToArray());

            presenter.SetSortModeForTests(CollectionSortMode.NameAscending);
            Assert.AreEqual("Sort: Name A-Z", presenter.SortLabelForTests);

            string[] nameOrder = presenter.VisibleCardIdsForTests.ToArray();
            CollectionAssert.AreNotEqual(loadOrder, nameOrder);

            string[] expectedNames = loadOrder
                .Select(id => CardDatabase.Instance.GetCard(id).DisplayName)
                .OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string[] actualNames = nameOrder
                .Select(id => CardDatabase.Instance.GetCard(id).DisplayName)
                .ToArray();
            CollectionAssert.AreEqual(expectedNames, actualNames);
        }

        [Test]
        public void Sort_RarityDescending_OrdersHighToLow()
        {
            string[] loadOrder = { _idLowNameHighRarity, _idMidNameMidRarity, _idHighNameLowRarity };
            SaveOwned(loadOrder);
            CollectionPresenter presenter = SpawnCollection();

            presenter.SetSortModeForTests(CollectionSortMode.RarityDescending);
            Assert.AreEqual("Sort: Rarity", presenter.SortLabelForTests);

            int[] rarities = presenter.VisibleCardIdsForTests
                .Select(id => CardDatabase.Instance.GetCard(id).Rarity)
                .ToArray();
            for (int i = 1; i < rarities.Length; i++)
                Assert.GreaterOrEqual(rarities[i - 1], rarities[i], "Rarity sort must be High→Low.");
        }

        [Test]
        public void Sort_ComposesWithFilterAndSearch()
        {
            // Two of shared class + one other class; search narrows within class.
            Card other = CardDatabase.Instance.AllCards.First(c => c.Class != _sharedClass);
            string[] owned = { _idHighNameLowRarity, other.Id, _idLowNameHighRarity, _idMidNameMidRarity };
            SaveOwned(owned);
            CollectionPresenter presenter = SpawnCollection();

            presenter.SetClassFilterForTests(_sharedClass);
            presenter.SetSortModeForTests(CollectionSortMode.NameAscending);

            string[] visible = presenter.VisibleCardIdsForTests.ToArray();
            Assert.IsFalse(visible.Contains(other.Id), "Filter must still hide other classes.");
            Assert.GreaterOrEqual(visible.Length, 2);

            string[] names = visible.Select(id => CardDatabase.Instance.GetCard(id).DisplayName).ToArray();
            string[] sorted = names.OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase).ToArray();
            CollectionAssert.AreEqual(sorted, names);

            // Search that matches only one of the filtered set.
            string needle = CardDatabase.Instance.GetCard(_idLowNameHighRarity).DisplayName;
            if (needle.Length > 3) needle = needle.Substring(0, 3);
            presenter.SetSearchTextForTests(needle);
            Assert.IsTrue(presenter.VisibleCardIdsForTests.All(id =>
                CardDatabase.Instance.GetCard(id).DisplayName.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0
                || id.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [Test]
        public void SortControl_IsInteractable_AndCyclesLabel()
        {
            SaveOwned(new[] { _idLowNameHighRarity, _idHighNameLowRarity });
            CollectionPresenter presenter = SpawnCollection();

            Transform sort = GameObject.Find("CollectionCanvas").transform.Find("ControlsRow/CollectionSort");
            Assert.NotNull(sort);
            Button button = sort.GetComponent<Button>();
            Assert.IsTrue(button.interactable);

            Assert.AreEqual("Sort: Default", presenter.SortLabelForTests);
            button.onClick.Invoke();
            Assert.AreEqual("Sort: Name A-Z", presenter.SortLabelForTests);
            button.onClick.Invoke();
            Assert.AreEqual("Sort: Rarity", presenter.SortLabelForTests);
            button.onClick.Invoke();
            Assert.AreEqual("Sort: Default", presenter.SortLabelForTests);
        }

        private CollectionPresenter SpawnCollection()
        {
            _presenterGo = new GameObject("CollectionSortHarness");
            var presenter = _presenterGo.AddComponent<CollectionPresenter>();
            presenter.Initialize(onBackToHome: null, onOpenDeckBuilder: null);
            return presenter;
        }

        private void SaveOwned(IEnumerable<string> cardIds)
        {
            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            profile.cardProgression.Clear();
            foreach (string id in cardIds.Distinct())
            {
                profile.cardProgression.Add(new CardProgressionRecord
                {
                    cardId = id,
                    copyCount = 1,
                    evolutionStep = 0,
                    cardLevel = 1,
                });
            }

            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();
        }
    }
}
