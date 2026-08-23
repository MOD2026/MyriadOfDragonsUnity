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
    /// <summary>Block V — Collection class Filter (All + CardClass). Sort stays Coming Soon.</summary>
    public class CollectionClassFilterTests
    {
        private string _scratchSaveDir;
        private GameObject _databaseGo;
        private GameObject _presenterGo;
        private readonly List<GameObject> _extra = new List<GameObject>();

        private string _warriorId;
        private string _knightId;
        private string _strategistId;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsClassFilter_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            _databaseGo = new GameObject("CardDatabase_ClassFilter");
            _databaseGo.AddComponent<CardDatabase>().Initialize();

            _warriorId = FirstIdOfClass(CardClass.Warrior);
            _knightId = FirstIdOfClass(CardClass.Knight);
            _strategistId = FirstIdOfClass(CardClass.Strategist);
            Assert.IsFalse(string.IsNullOrEmpty(_warriorId), "Setup: need a Warrior card in CardDatabase.");
            Assert.IsFalse(string.IsNullOrEmpty(_knightId), "Setup: need a Knight card in CardDatabase.");
            Assert.IsFalse(string.IsNullOrEmpty(_strategistId), "Setup: need a Strategist card in CardDatabase.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_presenterGo != null) Object.DestroyImmediate(_presenterGo);
            foreach (GameObject go in _extra)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _extra.Clear();
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
        public void ClassFilter_All_ShowsEveryOwnedCard()
        {
            SaveOwned(new[] { _warriorId, _knightId, _strategistId });
            CollectionPresenter presenter = SpawnCollection();

            Assert.AreEqual("Filter: All", presenter.ClassFilterLabelForTests);
            CollectionAssert.AreEquivalent(
                new[] { _warriorId, _knightId, _strategistId },
                presenter.VisibleCardIdsForTests.ToArray());
            Assert.IsNull(presenter.EmptyStateTextForTests);
        }

        [Test]
        public void ClassFilter_OneClass_HidesOtherClasses()
        {
            SaveOwned(new[] { _warriorId, _knightId, _strategistId });
            CollectionPresenter presenter = SpawnCollection();

            presenter.SetClassFilterForTests(CardClass.Knight);
            Assert.AreEqual("Filter: Knight", presenter.ClassFilterLabelForTests);
            CollectionAssert.AreEquivalent(new[] { _knightId }, presenter.VisibleCardIdsForTests.ToArray());
            Assert.IsFalse(presenter.VisibleCardIdsForTests.Contains(_warriorId));
            Assert.IsFalse(presenter.VisibleCardIdsForTests.Contains(_strategistId));
        }

        [Test]
        public void ClassFilter_ComposesWithSearch()
        {
            SaveOwned(new[] { _warriorId, _knightId, _strategistId });
            CollectionPresenter presenter = SpawnCollection();

            presenter.SetClassFilterForTests(CardClass.Warrior);
            presenter.SetSearchTextForTests(_warriorId);
            CollectionAssert.AreEquivalent(new[] { _warriorId }, presenter.VisibleCardIdsForTests.ToArray());

            presenter.SetSearchTextForTests(_knightId);
            Assert.AreEqual(0, presenter.VisibleCardIdsForTests.Count);
            Assert.AreEqual(CollectionPresenter.EmptyNoMatchCopy, presenter.EmptyStateTextForTests);
        }

        [Test]
        public void ClassFilter_EmptyMatch_DistinguishesFromNoOwned()
        {
            SaveOwned(new[] { _warriorId, _knightId });
            CollectionPresenter presenter = SpawnCollection();

            presenter.SetClassFilterForTests(CardClass.Strategist);
            Assert.AreEqual(0, presenter.VisibleCardIdsForTests.Count);
            Assert.AreEqual(CollectionPresenter.EmptyNoMatchCopy, presenter.EmptyStateTextForTests);

            // Fresh empty profile: no-owned copy, not no-match.
            SaveOwned(System.Array.Empty<string>());
            Object.DestroyImmediate(_presenterGo);
            _presenterGo = null;
            Object.DestroyImmediate(GameObject.Find("CollectionCanvas"));

            CollectionPresenter emptyPresenter = SpawnCollection();
            Assert.AreEqual(0, emptyPresenter.VisibleCardIdsForTests.Count);
            Assert.AreEqual(CollectionPresenter.EmptyNoOwnedCopy, emptyPresenter.EmptyStateTextForTests);
        }

        [Test]
        public void FilterAndSortControls_AreBothLive()
        {
            SaveOwned(new[] { _warriorId });
            CollectionPresenter presenter = SpawnCollection();

            Transform canvas = GameObject.Find("CollectionCanvas").transform;
            Assert.IsNull(canvas.Find("ControlsRow/FilterPlaceholder"));
            Assert.IsNull(canvas.Find("ControlsRow/SortPlaceholder"));
            Assert.NotNull(canvas.Find("ControlsRow/ClassFilter"));
            Transform sort = canvas.Find("ControlsRow/CollectionSort");
            Assert.NotNull(sort);
            Assert.IsTrue(sort.GetComponent<Button>().interactable);
            Assert.AreEqual("Sort: Default", presenter.SortLabelForTests);
            Assert.AreEqual("Filter: All", presenter.ClassFilterLabelForTests);
        }

        private static string FirstIdOfClass(CardClass cardClass)
        {
            Card hit = CardDatabase.Instance.AllCards.FirstOrDefault(c => c.Class == cardClass);
            return hit != null ? hit.Id : null;
        }

        private CollectionPresenter SpawnCollection()
        {
            _presenterGo = new GameObject("CollectionClassFilterHarness");
            var presenter = _presenterGo.AddComponent<CollectionPresenter>();
            presenter.Initialize(onBackToHome: null, onOpenDeckBuilder: null);
            return presenter;
        }

        private void SaveOwned(IEnumerable<string> cardIds)
        {
            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            profile.cardProgression.Clear();
            foreach (string id in cardIds)
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
