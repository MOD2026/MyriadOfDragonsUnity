using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.GuildExpedition;

public sealed class GuildExpeditionRulesConfiguration
{
    // §4.2 launch recommendation values - fallback only, same role as SocialSafety/PermitWeekKey's
    // Remote Config fallbacks.
    public const int FallbackAttemptsPerDay = 3;
    public const int FallbackMaxBankedAttempts = 6;
    public const int FallbackPersonalWeeklyCap = 1000;
    public const int MaximumAttemptsPerDay = 20;
    public const int MaximumMaxBankedAttempts = 40;
    public const int MaximumPersonalWeeklyCap = 100_000;

    public GuildExpeditionRulesConfiguration(int attemptsPerDay, int maxBankedAttempts, int personalWeeklyCap)
    {
        AttemptsPerDay = attemptsPerDay;
        MaxBankedAttempts = maxBankedAttempts;
        PersonalWeeklyCap = personalWeeklyCap;
    }

    public int AttemptsPerDay { get; }
    public int MaxBankedAttempts { get; }
    public int PersonalWeeklyCap { get; }
}

public interface IGuildExpeditionRulesConfiguration
{
    Task<GuildExpeditionRulesConfiguration> LoadAsync(IExecutionContext context, IGameApiClient apiClient);
}

public sealed class RemoteConfigGuildExpeditionRulesConfiguration : IGuildExpeditionRulesConfiguration
{
    public const string AttemptsPerDayKey = "guildExpedition.attemptsPerDay";
    public const string MaxBankedAttemptsKey = "guildExpedition.maxBankedAttempts";
    public const string PersonalWeeklyCapKey = "guildExpedition.personalWeeklyCap";

    public async Task<GuildExpeditionRulesConfiguration> LoadAsync(IExecutionContext context, IGameApiClient apiClient)
    {
        try
        {
            string accessToken = context.AccessToken ?? throw new InvalidOperationException("Missing authenticated access token.");
            string projectId = context.ProjectId ?? throw new InvalidOperationException("Missing project context.");
            var response = await apiClient.RemoteConfigSettings.AssignSettingsGetAsync(
                context,
                accessToken,
                projectId,
                context.EnvironmentId,
                null,
                new List<string> { AttemptsPerDayKey, MaxBankedAttemptsKey, PersonalWeeklyCapKey },
                null,
                CancellationToken.None);
            var settings = response.Data.Configs.Settings;
            return new GuildExpeditionRulesConfiguration(
                ReadPositiveInt(settings, AttemptsPerDayKey, GuildExpeditionRulesConfiguration.FallbackAttemptsPerDay, GuildExpeditionRulesConfiguration.MaximumAttemptsPerDay),
                ReadPositiveInt(settings, MaxBankedAttemptsKey, GuildExpeditionRulesConfiguration.FallbackMaxBankedAttempts, GuildExpeditionRulesConfiguration.MaximumMaxBankedAttempts),
                ReadPositiveInt(settings, PersonalWeeklyCapKey, GuildExpeditionRulesConfiguration.FallbackPersonalWeeklyCap, GuildExpeditionRulesConfiguration.MaximumPersonalWeeklyCap));
        }
        catch
        {
            return new GuildExpeditionRulesConfiguration(
                GuildExpeditionRulesConfiguration.FallbackAttemptsPerDay,
                GuildExpeditionRulesConfiguration.FallbackMaxBankedAttempts,
                GuildExpeditionRulesConfiguration.FallbackPersonalWeeklyCap);
        }
    }

    private static int ReadPositiveInt(IReadOnlyDictionary<string, object> settings, string key, int fallback, int maximum)
    {
        if (!settings.TryGetValue(key, out object? value) || value == null)
        {
            return fallback;
        }

        return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0 && parsed <= maximum
            ? parsed
            : fallback;
    }
}
