using System.IO;
using System.Linq;
using MyriadOfDragons.Save;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// The save system, exercised against a scratch directory rather than the real
    /// persistentDataPath - a test suite that writes to the machine's actual player profile would
    /// destroy the progress of whoever ran it.
    ///
    /// REWRITTEN 2026-08-07 against PlayerProfile as the serialised type directly (see
    /// SaveSystem.cs's own note) - the previous version of this file tested a SaveData/PlayerProfile
    /// split that PlayerProfile.cs no longer has. The emphasis is still deliberately on the failure
    /// paths rather than the happy path: a save system that round-trips a valid file is easy, the
    /// ones that lose people's progress lose it to a half-written file or a null list nobody ever
    /// serialised.
    /// </summary>
    public class SaveSystemTests
    {
        private string _scratchDirectory;

        [SetUp]
        public void SetUp()
        {
            _scratchDirectory = Path.Combine(Path.GetTempPath(), "MOD_SaveTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_scratchDirectory);
            SaveSystem.OverrideRootDirectoryForTests(_scratchDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            // The override is static state. A test that leaked it would point every later test in
            // the run - and any interactive Editor session afterwards - at a deleted directory.
            SaveSystem.ClearRootDirectoryOverride();
            // Same leak risk for the cached singleton - a test that populated CurrentProfile would
            // otherwise hand every later test in the run a stale in-memory profile instead of
            // letting it load its own.
            SaveSystem.ResetCurrentProfileForTests();

            try
            {
                if (Directory.Exists(_scratchDirectory)) Directory.Delete(_scratchDirectory, recursive: true);
            }
            catch (IOException)
            {
                // A leftover scratch directory in %TEMP% is not worth failing a green test over.
            }
        }

        private void WriteRawSaveFile(string contents) => File.WriteAllText(SaveSystem.SavePath, contents);

        // ---------- First run ----------

        [Test]
        public void Load_WithNoFileOnDisk_ReportsNewGameRatherThanCorruption()
        {
            PlayerProfile profile = SaveSystem.Load(out SaveLoadStatus status);

            // These two used to be indistinguishable, and they must not be: a first run shows the
            // tutorial, a failed load shows the tutorial AND has lost something.
            Assert.AreEqual(SaveLoadStatus.NewGame, status);
            Assert.AreEqual(1, profile.avatarLevel, "A new player must start at level 1, not 0 - a " +
                                                    "level-0 profile derives a 0-slot deck and an unplayable match.");
        }

        // ---------- Round trip ----------

        [Test]
        public void SaveThenLoad_PreservesEveryPersistedField()
        {
            var written = new PlayerProfile
            {
                avatarLevel = 31,
                castleLevel = 12,
                barracksLevel = 25,
                gateLevel = 4,
                totalMatches = 40,
                totalWins = 27,
                winStreak = 3,
                gold = 2500,
                gems = 300,
                constructionMaterials = 4200,
                expeditionDayKeyUtc = "2026-08-24",
                expeditionGoldEarnedTodayUtc = 350,
                expeditionAttemptsTodayUtc = 2,
            };
            written.hasSeenIntro = true;
            written.seenChapters.Add("prologue");
            written.seenChapters.Add("chapter_two");
            written.cardCollection.Add("dragon_007");
            written.activeDeckCardIds.Clear();
            written.activeDeckCardIds.Add("dragon_007");

            Assert.IsTrue(SaveSystem.Save(written));

            PlayerProfile read = SaveSystem.Load(out SaveLoadStatus status);

            Assert.AreEqual(SaveLoadStatus.Loaded, status);
            Assert.AreEqual(31, read.avatarLevel);
            Assert.AreEqual(12, read.castleLevel);
            Assert.AreEqual(25, read.barracksLevel);
            Assert.AreEqual(4, read.gateLevel);
            Assert.AreEqual(40, read.totalMatches);
            Assert.AreEqual(27, read.totalWins);
            Assert.AreEqual(3, read.winStreak);
            Assert.AreEqual(2500, read.gold);
            Assert.AreEqual(300, read.gems);
            Assert.AreEqual(4200, read.constructionMaterials);
            Assert.AreEqual("2026-08-24", read.expeditionDayKeyUtc);
            Assert.AreEqual(350, read.expeditionGoldEarnedTodayUtc);
            Assert.AreEqual(2, read.expeditionAttemptsTodayUtc);
            Assert.IsTrue(read.hasSeenIntro);
            CollectionAssert.AreEqual(new[] { "prologue", "chapter_two" }, read.seenChapters);
            CollectionAssert.AreEqual(new[] { "dragon_007" }, read.activeDeckCardIds);
            CollectionAssert.Contains(read.cardCollection, "dragon_007");
        }

        [Test]
        public void Save_OverAnExistingProfile_ReplacesItWithoutLeavingATempFile()
        {
            SaveSystem.Save(new PlayerProfile { avatarLevel = 5 });
            SaveSystem.Save(new PlayerProfile { avatarLevel = 9 });

            Assert.AreEqual(9, SaveSystem.Load().avatarLevel);
            Assert.IsFalse(File.Exists(SaveSystem.SavePath + ".tmp"),
                "The write-then-swap left its temp file behind - it would be picked up as debris " +
                "and, worse, means the swap did not complete.");
        }

        // ---------- Corruption ----------

        [Test]
        public void Load_WithMalformedJson_QuarantinesTheFileAndStartsFresh()
        {
            WriteRawSaveFile("{ this is not json at all ");

            PlayerProfile profile = SaveSystem.Load(out SaveLoadStatus status);

            Assert.AreEqual(SaveLoadStatus.RecoveredFromCorruption, status);
            Assert.AreEqual(1, profile.avatarLevel);
            Assert.IsFalse(File.Exists(SaveSystem.SavePath), "The unreadable file should have been moved aside.");

            // Renamed, never deleted: a corrupt save is still the only copy of that player's
            // progress, and destroying it removes any chance of recovering it by hand.
            string[] quarantined = Directory.GetFiles(_scratchDirectory, "*.corrupt-*");
            Assert.AreEqual(1, quarantined.Length, "The corrupt save was not preserved for recovery.");
        }

        [Test]
        public void Load_WithWellFormedJsonOfTheWrongShape_IsTreatedAsCorruption()
        {
            // JsonUtility does not throw on this - it returns a PlayerProfile with every field at
            // its default, which is indistinguishable from a real save unless something checks.
            // Checking for a known field name in the raw text is that check.
            WriteRawSaveFile("{\"someOtherGame\":true,\"score\":42}");

            SaveSystem.Load(out SaveLoadStatus status);

            Assert.AreEqual(SaveLoadStatus.RecoveredFromCorruption, status);
        }

        [Test]
        public void Load_WithAnEmptyFile_IsTreatedAsCorruption()
        {
            // The exact shape a crash mid-write used to leave behind, before write-then-swap.
            WriteRawSaveFile(string.Empty);

            SaveSystem.Load(out SaveLoadStatus status);

            Assert.AreEqual(SaveLoadStatus.RecoveredFromCorruption, status);
        }

        [Test]
        public void Load_WithTruncatedJson_DoesNotThrow()
        {
            string valid = SaveSystem.Serialize(new PlayerProfile { avatarLevel = 20 });
            WriteRawSaveFile(valid.Substring(0, valid.Length / 2));

            Assert.DoesNotThrow(() => SaveSystem.Load(out _),
                "A half-written save must degrade to a fresh profile, never throw on the boot path.");
        }

        // ---------- Normalisation ----------

        [Test]
        public void Normalize_ClampsLevelsThatWouldProduceAnUnplayableMatch()
        {
            var profile = new PlayerProfile { avatarLevel = 0, castleLevel = -5, barracksLevel = 0, gateLevel = -1 };

            SaveMigration.Normalize(profile);

            Assert.AreEqual(1, profile.avatarLevel);
            Assert.AreEqual(1, profile.castleLevel);
            Assert.AreEqual(1, profile.barracksLevel);
            Assert.AreEqual(1, profile.gateLevel);
        }

        [Test]
        public void Normalize_ReplacesNullListsRatherThanLeavingThemForTheFirstCallerToTripOver()
        {
            // JsonUtility leaves a List field null when the key is absent from the JSON - the
            // single most common source of a NullReferenceException on a load path.
            var profile = new PlayerProfile
            {
                seenChapters = null,
                cardCollection = null,
                activeDeckCardIds = null,
                unlockedStageIds = null,
                inventoryAssets = null,
            };

            SaveMigration.Normalize(profile);

            Assert.IsNotNull(profile.seenChapters);
            Assert.IsNotNull(profile.cardCollection);
            Assert.IsNotNull(profile.activeDeckCardIds);
            Assert.IsNotNull(profile.unlockedStageIds);
            Assert.IsNotNull(profile.inventoryAssets);
        }

        [Test]
        public void Normalize_RepairsAWinCountThatExceedsMatchesPlayed()
        {
            var profile = new PlayerProfile { totalMatches = 2, totalWins = 10 };

            SaveMigration.Normalize(profile);

            Assert.GreaterOrEqual(profile.totalMatches, profile.totalWins,
                "A win rate above 100% divides every downstream calculation into nonsense.");
        }

        [Test]
        public void Normalize_ClampsStaminaToItsOwnMaxAndNeverBelowZero()
        {
            var overMax = new PlayerProfile { stamina = 999, maxStamina = 100 };
            SaveMigration.Normalize(overMax);
            Assert.AreEqual(100, overMax.stamina, "Current stamina must never read above its own maximum.");

            var negative = new PlayerProfile { stamina = -5, gold = -10, gems = -3 };
            SaveMigration.Normalize(negative);
            Assert.AreEqual(0, negative.stamina);
            Assert.AreEqual(0, negative.gold);
            Assert.AreEqual(0, negative.gems);
        }

        [Test]
        public void Normalize_ClampsExpeditionMaterialsAndDailyCountersToNeverBelowZero()
        {
            var profile = new PlayerProfile
            {
                constructionMaterials = -50,
                expeditionGoldEarnedTodayUtc = -1,
                expeditionAttemptsTodayUtc = -1,
            };

            SaveMigration.Normalize(profile);

            Assert.AreEqual(0, profile.constructionMaterials);
            Assert.AreEqual(0, profile.expeditionGoldEarnedTodayUtc);
            Assert.AreEqual(0, profile.expeditionAttemptsTodayUtc);
        }

        [Test]
        public void Normalize_ReplacesANullExpeditionDayKeyWithEmptyString()
        {
            // JsonUtility leaves a string field null (not empty) when its key is absent from the
            // JSON - a pre-2026-08-24 save on disk predates this field entirely.
            var profile = new PlayerProfile { expeditionDayKeyUtc = null };

            SaveMigration.Normalize(profile);

            Assert.IsNotNull(profile.expeditionDayKeyUtc);
            Assert.AreEqual(string.Empty, profile.expeditionDayKeyUtc);
        }

        // ---------- SaveSystem.CurrentProfile ----------

        [Test]
        public void CurrentProfile_ReturnsTheSameInstanceOnRepeatedAccess()
        {
            PlayerProfile first = SaveSystem.CurrentProfile;
            PlayerProfile second = SaveSystem.CurrentProfile;
            Assert.AreSame(first, second, "Two systems reading CurrentProfile must get the same live object.");
        }

        [Test]
        public void CurrentProfile_ChangesFromOneReaderAreVisibleToAnother()
        {
            // The actual point of a shared singleton over "everyone calls LoadOrCreate() and gets
            // their own copy": one system's change must be visible to another WITHOUT a
            // save-then-reload round trip, in the same running session.
            int before = SaveSystem.CurrentProfile.gold;

            SaveSystem.CurrentProfile.gold += 777;

            Assert.AreEqual(before + 777, SaveSystem.CurrentProfile.gold,
                "A second reference obtained afterward must see the change the first one made.");
        }

        [Test]
        public void Profile_IsTheSameSingletonAsCurrentProfile()
        {
            // SaveSystem.Profile and SaveSystem.CurrentProfile are two names for the same shared
            // instance (CurrencyManager uses one, SaveManager uses the other) - they must never
            // drift into two independently-loaded copies, which is the exact bug CurrentProfile
            // exists to prevent in the first place.
            Assert.AreSame(SaveSystem.Profile, SaveSystem.CurrentProfile);
        }

        // ---------- PlayerProfile behaviour ----------

        [Test]
        public void RecordMatchResult_TracksMatchesAndWinsAcrossAReload()
        {
            var profile = PlayerProfile.LoadOrCreate();

            profile.RecordMatchResult(isVictory: true);
            SaveSystem.Save(profile);

            var reloaded = PlayerProfile.LoadOrCreate();
            Assert.AreEqual(1, reloaded.totalMatches);
            Assert.AreEqual(1, reloaded.totalWins);
        }

        [Test]
        public void RecordMatchResult_ResetsWinStreakOnALoss()
        {
            var profile = PlayerProfile.LoadOrCreate();

            profile.RecordMatchResult(isVictory: true);
            profile.RecordMatchResult(isVictory: true);
            Assert.AreEqual(2, profile.winStreak);

            profile.RecordMatchResult(isVictory: false);
            Assert.AreEqual(0, profile.winStreak, "A loss must end the current streak.");
        }

        [Test]
        public void MarkChapterSeen_IsIdempotent()
        {
            var profile = PlayerProfile.LoadOrCreate();

            profile.MarkChapterSeen("prologue");
            profile.MarkChapterSeen("prologue");

            Assert.AreEqual(1, profile.seenChapters.Count(id => id == "prologue"));
            Assert.IsTrue(profile.HasSeenChapter("prologue"));
        }

        [Test]
        public void ResetProgress_RemovesTheFileFromDisk()
        {
            var profile = new PlayerProfile { avatarLevel = 12 };
            SaveSystem.Save(profile);
            Assert.IsTrue(SaveSystem.Exists);

            Assert.IsTrue(SaveSystem.Delete());

            Assert.IsFalse(SaveSystem.Exists);
        }
    }
}
