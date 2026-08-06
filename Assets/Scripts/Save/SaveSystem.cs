using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace MyriadOfDragons.Save
{
    /// <summary>How a load attempt actually went. Values kept as-is from the current PlayerProfile
    /// design (Success/New/Failed alongside the ones this file actually uses) so nothing that
    /// already references this enum has to change.</summary>
    public enum SaveLoadStatus
    {
        Success,
        Loaded,
        New,
        NewGame,
        Failed,
        RecoveredFromCorruption
    }

    /// <summary>
    /// Reading and writing <see cref="PlayerProfile"/> to disk.
    ///
    /// REBUILT 2026-08-07 around PlayerProfile as the serialised type directly - PlayerProfile is
    /// now `[Serializable]` itself (see its own file), there is no separate SaveData wrapper. This
    /// keeps everything the I/O layer needs to guarantee (atomic write, corruption is quarantined
    /// not overwritten, a scratch directory for tests) without requiring any change to
    /// PlayerProfile's fields or shape - the previous version of this file lost all of that when
    /// it was simplified down to a direct File.WriteAllText/JsonUtility.FromJson pair, which is
    /// what let a crash mid-write leave a truncated file and let a malformed save be silently
    /// overwritten instead of preserved.
    /// </summary>
    public static class SaveSystem
    {
        public const string SaveFileName = "player_profile.json";

        private static PlayerProfile _currentProfile;

        /// <summary>The one shared, live profile for the running session. Lazily loads on first
        /// touch. `Profile` and `CurrentProfile` are the same thing - both names are kept because
        /// both are already depended on elsewhere (CurrencyManager uses Profile, SaveManager uses
        /// CurrentProfile) and neither call site needed to change for this fix.</summary>
        public static PlayerProfile CurrentProfile
        {
            get
            {
                _currentProfile ??= LoadOrCreate();
                return _currentProfile;
            }
            set => _currentProfile = value;
        }

        public static PlayerProfile Profile
        {
            get => CurrentProfile;
            set => CurrentProfile = value;
        }

        /// <summary>Test-only escape hatch. Without this, whichever test first touches
        /// CurrentProfile in a run would cache a real PlayerProfile, and every later test in that
        /// run would silently reuse that stale instance instead of loading its own.</summary>
        public static void ResetCurrentProfileForTests() => _currentProfile = null;

        private static string _rootDirectoryOverride;

        /// <summary>Directory the profile lives in. Application.persistentDataPath by default -
        /// deliberately NOT the project folder, which is read-only in a build.</summary>
        public static string RootDirectory =>
            string.IsNullOrEmpty(_rootDirectoryOverride) ? Application.persistentDataPath : _rootDirectoryOverride;

        public static string SavePath => Path.Combine(RootDirectory, SaveFileName);

        /// <summary>Points saves at a scratch directory. Tests must call
        /// <see cref="ClearRootDirectoryOverride"/> in TearDown - this is static state, and a test
        /// that leaks it points every later test (and any interactive Editor session afterwards)
        /// at a deleted directory.</summary>
        public static void OverrideRootDirectoryForTests(string directory) => _rootDirectoryOverride = directory;

        public static void ClearRootDirectoryOverride() => _rootDirectoryOverride = null;

        public static bool Exists => File.Exists(SavePath);

        // ---------- Pure serialisation (no IO - the testable half) ----------

        public static string Serialize(PlayerProfile profile) => JsonUtility.ToJson(profile, prettyPrint: true);

        /// <summary>
        /// Parses profile JSON. Never throws - a save file is untrusted input (half-written by a
        /// crash, hand-edited, synced mid-write by Drive) and a parse failure must degrade to
        /// "start fresh", never to an exception on the boot path.
        /// </summary>
        public static PlayerProfile Deserialize(string json, out SaveLoadStatus status)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                status = SaveLoadStatus.RecoveredFromCorruption;
                return new PlayerProfile();
            }

            // PlayerProfile has no dedicated version key the way the previous design's SaveData
            // did, so this checks for a field that has been present on every shape this class has
            // had instead. Well-formed JSON of a completely different shape - e.g.
            // {"someOtherGame":true} - does NOT throw and does NOT come back looking obviously
            // wrong: JsonUtility runs every C# field initialiser for a key that's absent from the
            // JSON, so the parsed result is indistinguishable from a genuine fresh profile unless
            // something checks the raw text first.
            if (!json.Contains("\"avatarLevel\""))
            {
                status = SaveLoadStatus.RecoveredFromCorruption;
                return new PlayerProfile();
            }

            PlayerProfile parsed;
            try
            {
                parsed = JsonUtility.FromJson<PlayerProfile>(json);
            }
            catch (Exception)
            {
                status = SaveLoadStatus.RecoveredFromCorruption;
                return new PlayerProfile();
            }

            if (parsed == null)
            {
                status = SaveLoadStatus.RecoveredFromCorruption;
                return new PlayerProfile();
            }

            SaveMigration.Normalize(parsed);
            status = SaveLoadStatus.Loaded;
            return parsed;
        }

        // ---------- Disk IO ----------

        public static PlayerProfile Load() => Load(out _);

        public static PlayerProfile Load(out SaveLoadStatus status)
        {
            string path = SavePath;
            PlayerProfile profile;

            if (!File.Exists(path))
            {
                status = SaveLoadStatus.NewGame;
                profile = new PlayerProfile();
            }
            else
            {
                try
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    profile = Deserialize(json, out status);

                    if (status == SaveLoadStatus.RecoveredFromCorruption)
                    {
                        QuarantineCorruptFile(path);
                    }
                }
                catch (Exception e)
                {
                    // Most likely a Drive-synced file that exists in the directory listing but has
                    // not been hydrated locally yet. Treating an unreadable file as a new game
                    // would overwrite it on the next save, so this returns a fresh profile WITHOUT
                    // touching the file on disk.
                    Debug.LogWarning($"SaveSystem: could not read {path} ({e.GetType().Name}: {e.Message}). " +
                                     "Starting a fresh profile; the existing file has NOT been modified.");
                    status = SaveLoadStatus.RecoveredFromCorruption;
                    profile = new PlayerProfile();
                }
            }

            profile.LoadStatus = status;
            return profile;
        }

        public static PlayerProfile LoadOrCreate() => Load();

        /// <summary>
        /// Writes the profile. Returns false (having logged) rather than throwing, so an autosave
        /// on a full or read-only disk cannot take the game down mid-match.
        /// </summary>
        public static bool Save(PlayerProfile profile)
        {
            if (profile == null) return false;

            string path = SavePath;
            string temp = path + ".tmp";

            try
            {
                Directory.CreateDirectory(RootDirectory);
                string json = Serialize(profile);

                // Write-then-swap, never write-in-place. A save interrupted partway through an
                // in-place write leaves a truncated file that parses as garbage and loses the
                // whole profile; a swap leaves either the old file or the new one, never half of
                // either. This matters more than usual here because the project is synced through
                // Google Drive, which can be reading a file at any moment.
                File.WriteAllText(temp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temp, path, destinationBackupFileName: null);
                    }
                    catch (Exception)
                    {
                        // File.Replace is not supported on every filesystem - notably it fails on
                        // some network and sync-backed volumes. Delete-then-move is a slightly
                        // wider window but works everywhere.
                        File.Delete(path);
                        File.Move(temp, path);
                    }
                }
                else
                {
                    File.Move(temp, path);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveSystem: failed to write {path} - {e.GetType().Name}: {e.Message}");
                TryDelete(temp);
                return false;
            }
        }

        /// <summary>Removes the profile - "reset progress". Returns false if there was nothing to
        /// delete or the delete failed.</summary>
        public static bool Delete()
        {
            if (!File.Exists(SavePath)) return false;
            return TryDelete(SavePath);
        }

        /// <summary>
        /// Renames an unreadable save aside instead of deleting it. A corrupt file is the only
        /// copy of a player's progress that exists; overwriting it is unrecoverable, and the
        /// timestamped name means repeated failed boots do not overwrite each other's evidence.
        /// </summary>
        private static void QuarantineCorruptFile(string path)
        {
            try
            {
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",
                    System.Globalization.CultureInfo.InvariantCulture);
                string quarantined = $"{path}.corrupt-{stamp}";
                File.Move(path, quarantined);
                Debug.LogWarning($"SaveSystem: {path} could not be parsed and was moved to {quarantined}. " +
                                 "A fresh profile has been started.");
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveSystem: could not quarantine the unreadable save at {path} - " +
                               $"{e.GetType().Name}: {e.Message}");
            }
        }

        private static bool TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
