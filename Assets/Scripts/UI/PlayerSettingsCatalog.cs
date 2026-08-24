using System;

namespace MyriadOfDragons.UI
{
    /// <summary>Supported player settings values — language list is the day-1 real-time translation surface.</summary>
    public static class PlayerSettingsCatalog
    {
        public const string DefaultLanguageCode = "en";

        public static readonly LanguageOption[] SupportedLanguages =
        {
            new LanguageOption("en", "English"),
            new LanguageOption("es", "Español"),
            new LanguageOption("fr", "Français"),
            new LanguageOption("de", "Deutsch"),
            new LanguageOption("ja", "日本語"),
            new LanguageOption("zh-CN", "中文 (简体)"),
            new LanguageOption("ko", "한국어"),
            new LanguageOption("pt-BR", "Português (Brasil)"),
        };

        public static string NormalizeLanguageCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return DefaultLanguageCode;

            string trimmed = code.Trim();
            foreach (LanguageOption option in SupportedLanguages)
            {
                if (string.Equals(option.Code, trimmed, StringComparison.OrdinalIgnoreCase))
                    return option.Code;
            }

            return DefaultLanguageCode;
        }

        public static bool TryGetDisplayName(string code, out string displayName)
        {
            displayName = null;
            if (string.IsNullOrWhiteSpace(code)) return false;

            foreach (LanguageOption option in SupportedLanguages)
            {
                if (string.Equals(option.Code, code.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    displayName = option.DisplayName;
                    return true;
                }
            }

            return false;
        }

        public static string GetNextLanguageCode(string currentCode)
        {
            string normalized = NormalizeLanguageCode(currentCode);
            for (int i = 0; i < SupportedLanguages.Length; i++)
            {
                if (!string.Equals(SupportedLanguages[i].Code, normalized, StringComparison.Ordinal))
                    continue;

                int next = (i + 1) % SupportedLanguages.Length;
                return SupportedLanguages[next].Code;
            }

            return DefaultLanguageCode;
        }

        public readonly struct LanguageOption
        {
            public LanguageOption(string code, string displayName)
            {
                Code = code;
                DisplayName = displayName;
            }

            public string Code { get; }
            public string DisplayName { get; }
        }
    }
}
