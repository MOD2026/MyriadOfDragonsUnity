using MyriadOfDragons.Save;
using UnityEngine;

namespace MyriadOfDragons.UI
{
    /// <summary>Reads/writes player settings on the live profile and applies global side effects (audio).</summary>
    public static class PlayerSettingsService
    {
        public static bool GetAudioEnabled(PlayerProfile profile) =>
            profile == null || profile.settingsAudioEnabled;

        public static bool GetNotificationsEnabled(PlayerProfile profile) =>
            profile == null || profile.settingsNotificationsEnabled;

        public static string GetPreferredLanguageCode(PlayerProfile profile) =>
            PlayerSettingsCatalog.NormalizeLanguageCode(
                profile?.preferredLanguageCode ?? PlayerSettingsCatalog.DefaultLanguageCode);

        public static string GetPreferredLanguageDisplayName(PlayerProfile profile)
        {
            string code = GetPreferredLanguageCode(profile);
            return PlayerSettingsCatalog.TryGetDisplayName(code, out string displayName)
                ? displayName
                : code;
        }

        public static void SetAudioEnabled(PlayerProfile profile, bool enabled)
        {
            if (profile == null) return;
            profile.settingsAudioEnabled = enabled;
            ApplyAudioGlobally(enabled);
        }

        public static void SetNotificationsEnabled(PlayerProfile profile, bool enabled)
        {
            if (profile == null) return;
            profile.settingsNotificationsEnabled = enabled;
        }

        public static void SetPreferredLanguageCode(PlayerProfile profile, string languageCode)
        {
            if (profile == null) return;
            string normalized = PlayerSettingsCatalog.NormalizeLanguageCode(languageCode);
            if (profile.preferredLanguageCode == normalized) return;

            profile.preferredLanguageCode = normalized;
            RealtimeTranslationPreferences.NotifyLanguageChanged(normalized);
        }

        public static void CyclePreferredLanguage(PlayerProfile profile)
        {
            if (profile == null) return;
            SetPreferredLanguageCode(profile, PlayerSettingsCatalog.GetNextLanguageCode(profile.preferredLanguageCode));
        }

        public static void ApplyFromProfile(PlayerProfile profile)
        {
            ApplyAudioGlobally(GetAudioEnabled(profile));
        }

        public static void ApplyAudioGlobally(bool enabled)
        {
            AudioListener.volume = enabled ? 1f : 0f;
        }

        public static void Persist(PlayerProfile profile)
        {
            if (profile == null) return;
            SaveSystem.Save(profile);
        }
    }
}
