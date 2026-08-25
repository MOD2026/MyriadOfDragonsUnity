using MyriadOfDragons.Data;
using MyriadOfDragons.Economy;
using MyriadOfDragons.Save;

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
    }
}
