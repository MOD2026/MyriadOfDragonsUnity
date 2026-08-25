using MyriadOfDragons.Data;
using MyriadOfDragons.Economy;
using MyriadOfDragons.Save;
using MyriadOfDragons.Season;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Read-only profile chrome for metagame art shells. Does not invent claim/reward truth —
    /// OpenValues still own mutation refuse until amounts/backends lock.
    /// </summary>
    public static class MetagameShellProfileBinding
    {
        public const string OpenAmountLabel = "OPEN";
        public const string EmptyBackendLabel = "—";

        public static PlayerProfile ProfileOrNull => SaveManager.SaveData ?? SaveSystem.CurrentProfile;

        public static string PlayerDisplayName()
        {
            PlayerProfile profile = ProfileOrNull;
            if (profile == null || string.IsNullOrWhiteSpace(profile.playerName))
                return "Sovereign";
            return profile.playerName.Trim();
        }

        public static string WalletLine()
        {
            PlayerProfile profile = ProfileOrNull;
            if (profile == null) return "Gold — · Gems — · Market Credits —";
            return
                $"Gold {CurrencyManager.GetBalance(profile, CurrencyType.Gold)} · " +
                $"Gems {CurrencyManager.GetBalance(profile, CurrencyType.Gems)} · " +
                $"Market Credits {CurrencyManager.GetBalance(profile, CurrencyType.DragonRelic)}";
        }

        public static string GuildContributionLine()
        {
            PlayerProfile profile = ProfileOrNull;
            int gc = profile == null
                ? 0
                : CurrencyManager.GetBalance(profile, CurrencyType.GuildContribution);
            return $"Guild Contribution {gc}";
        }

        public static string UtcDayKeyLine()
        {
            System.DateTime utc = System.DateTime.UtcNow.Date;
            return $"UTC reset {utc:yyyy-MM-dd}";
        }

        public static string SelfIdentityLine() => $"You: {PlayerDisplayName()}";

        public static string PassSeasonXpLine()
        {
            PlayerProfile profile = ProfileOrNull;
            int xp = profile?.passSeasonXp ?? 0;
            return $"Season XP {xp:N0}";
        }

        /// <summary>Phase-1 display until tier curve locks — shows earned XP, not a template token.</summary>
        public static string PassTierProgressLine()
        {
            PlayerProfile profile = ProfileOrNull;
            int xp = profile?.passSeasonXp ?? 0;
            if (BattlePassOpenValues.SeasonXpPerTier is int perTier && perTier > 0)
                return $"{xp:N0} / {perTier:N0} XP";
            return $"{xp:N0} XP earned";
        }
    }
}
