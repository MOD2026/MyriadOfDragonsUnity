using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Proves GameBootstrap.EnsureApprovedStarterCollectionGranted writes Collection V1
    /// ownership into cardProgression (via CollectionProgression.TryGrantFirstCopy), not the
    /// legacy flat cardCollection list.
    /// </summary>
    public class StarterGrantCollectionV1Tests
    {
        private static readonly string[] ApprovedStarterCollectionCardIds =
        {
            "warrior", "novice_knight", "goblin_caster",
            "cleric", "archer_elf", "fox", "bunny", "forest", "tribal_warrior", "undead_soldier",
        };

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsStarterGrantV1_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
            {
                Directory.Delete(_scratchSaveDir, recursive: true);
            }
        }

        private GameBootstrap SpawnAndInitializeBootstrap(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                GameObject spawned = GameObject.Find(spawnedName);
                if (spawned != null) _spawned.Add(spawned);
            }
            return bootstrap;
        }

        private static void SeedFreshCollectionV1Profile(PlayerProfile profile)
        {
            profile.collectionSchemaVersion = CollectionSchemaRules.CurrentCollectionSchemaVersion;
            profile.cardProgression.Clear();
            profile.cardCollection.Clear();
        }

        [Test]
        public void FreshV1Profile_StarterGrant_WritesCardProgressionNotLegacyList()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("StarterGrantV1_FreshProfile");
            SeedFreshCollectionV1Profile(bootstrap.Profile);
            Assert.IsTrue(bootstrap.Profile.UsesCollectionV1);

            bootstrap.EnsureApprovedStarterCollectionGranted();

            Assert.AreEqual(ApprovedStarterCollectionCardIds.Length, bootstrap.Profile.cardProgression.Count,
                "Every approved starter id must become exactly one progression row.");
            foreach (string cardId in ApprovedStarterCollectionCardIds)
            {
                CardProgressionRecord record = CollectionProgression.FindRecordForTests(bootstrap.Profile, cardId);
                Assert.IsNotNull(record, $"Expected a progression record for '{cardId}'.");
                Assert.AreEqual(1, record.copyCount, $"Expected one copy of '{cardId}'.");
                Assert.IsNotNull(CardDatabase.Instance.GetCard(cardId),
                    $"Every granted id must resolve to a real card - '{cardId}' did not.");
            }

            foreach (string cardId in ApprovedStarterCollectionCardIds)
            {
                CollectionAssert.DoesNotContain(bootstrap.Profile.cardCollection, cardId,
                    "V1 starter grant must not mutate the legacy rollback list.");
            }
        }

        [Test]
        public void FreshV1Profile_StarterGrant_PersistsInCardProgressionAcrossReload()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("StarterGrantV1_Persist");
            SeedFreshCollectionV1Profile(bootstrap.Profile);

            bootstrap.EnsureApprovedStarterCollectionGranted();
            Assert.IsTrue(SaveSystem.Exists, "Setup: expected the grant to persist a save file.");

            PlayerProfile reloaded = SaveSystem.Load();
            Assert.IsTrue(reloaded.UsesCollectionV1);
            Assert.AreEqual(ApprovedStarterCollectionCardIds.Length, reloaded.cardProgression.Count);
            foreach (string cardId in ApprovedStarterCollectionCardIds)
            {
                Assert.IsNotNull(CollectionProgression.FindRecordForTests(reloaded, cardId));
                CollectionAssert.DoesNotContain(reloaded.cardCollection, cardId);
            }
        }

        [Test]
        public void V1ProfileWithPartialProgression_ReceivesOnlyMissingStarterIds()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("StarterGrantV1_TopUp");
            SeedFreshCollectionV1Profile(bootstrap.Profile);
            CollectionProgression.TryGrantFirstCopy(bootstrap.Profile, "warrior", _ => true);
            CollectionProgression.TryGrantFirstCopy(bootstrap.Profile, "novice_knight", _ => true);
            CollectionProgression.TryGrantFirstCopy(bootstrap.Profile, "goblin_caster", _ => true);

            bootstrap.EnsureApprovedStarterCollectionGranted();

            Assert.AreEqual(ApprovedStarterCollectionCardIds.Length, bootstrap.Profile.cardProgression.Count);
            foreach (string cardId in ApprovedStarterCollectionCardIds)
            {
                Assert.AreEqual(1, CollectionProgression.FindRecordForTests(bootstrap.Profile, cardId).copyCount);
            }
            Assert.AreEqual(
                ApprovedStarterCollectionCardIds.Length,
                bootstrap.Profile.cardProgression.Select(r => r.cardId).Distinct().Count(),
                "Top-up must not duplicate any starter id.");
        }
    }
}
