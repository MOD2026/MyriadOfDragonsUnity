using System;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Read surface for the day-1 real-time translation system — language is set in Settings and
    /// persisted on <see cref="PlayerProfile.preferredLanguageCode"/>.
    /// </summary>
    public static class RealtimeTranslationPreferences
    {
        public static event Action<string> LanguageChanged;

        public static string CurrentLanguageCode =>
            PlayerSettingsService.GetPreferredLanguageCode(SaveManager.SaveData);

        public static string CurrentLanguageDisplayName =>
            PlayerSettingsService.GetPreferredLanguageDisplayName(SaveManager.SaveData);

        internal static void NotifyLanguageChanged(string languageCode)
        {
            LanguageChanged?.Invoke(languageCode);
        }
    }
}
