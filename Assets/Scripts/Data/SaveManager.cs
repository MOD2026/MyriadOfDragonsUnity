using MyriadOfDragons.Save;

namespace MyriadOfDragons.Data
{
    /// <summary>
    /// Thin façade over <see cref="SaveSystem.CurrentProfile"/> for the metagame UI (home screen,
    /// shop, deck builder) - lets that code call SaveManager.Load()/Save() without depending on
    /// the Save namespace's naming directly. Bridges to CurrentProfile specifically, not a fresh
    /// PlayerProfile.LoadOrCreate() each time, so the metagame UI and the battle side
    /// (GameBootstrap.Initialize) are always looking at the same in-memory profile - see
    /// SaveSystem.CurrentProfile's own note on why two independently-loaded copies is the bug this
    /// exists to prevent.
    /// </summary>
    public static class SaveManager
    {
        public static PlayerProfile SaveData => SaveSystem.CurrentProfile;

        public static void Load()
        {
            _ = SaveSystem.CurrentProfile;
        }

        public static void Save()
        {
            SaveSystem.Save(SaveSystem.CurrentProfile);
        }
    }
}
