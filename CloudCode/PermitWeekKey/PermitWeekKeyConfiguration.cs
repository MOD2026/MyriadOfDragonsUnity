using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.PermitWeekKey;

public sealed class PermitWeekKeyEconomyConfiguration
{
    // Design target per docs/PERMIT_WEEK_KEY_SERVER_STUB_v1.md: "Design target 4/week, hoard 8,
    // enabled only with server issuance and reconciliation." These are the server-side fallback
    // values, independent of (and not read from) the client's local ManualTrustedWeekKey stopgap.
    public const int FallbackWeeklyRate = 4;
    public const int FallbackHoardCap = 8;
    public const int MaximumWeeklyRate = 100;
    public const int MaximumHoardCap = 1000;

    public PermitWeekKeyEconomyConfiguration(int weeklyRate, int hoardCap)
    {
        WeeklyRate = weeklyRate;
        HoardCap = hoardCap;
    }

    public int WeeklyRate { get; }
    public int HoardCap { get; }
}

public interface IPermitWeekKeyEconomyConfiguration
{
    Task<PermitWeekKeyEconomyConfiguration> LoadAsync(IExecutionContext context, IGameApiClient apiClient);
}

public sealed class RemoteConfigPermitWeekKeyEconomyConfiguration : IPermitWeekKeyEconomyConfiguration
{
    public const string WeeklyRateKey = "permitWeekKey.weeklyRate";
    public const string HoardCapKey = "permitWeekKey.hoardCap";

    public async Task<PermitWeekKeyEconomyConfiguration> LoadAsync(IExecutionContext context, IGameApiClient apiClient)
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
                new List<string> { WeeklyRateKey, HoardCapKey },
                null,
                CancellationToken.None);
            var settings = response.Data.Configs.Settings;
            return new PermitWeekKeyEconomyConfiguration(
                ReadPositiveInt(settings, WeeklyRateKey, PermitWeekKeyEconomyConfiguration.FallbackWeeklyRate, PermitWeekKeyEconomyConfiguration.MaximumWeeklyRate),
                ReadPositiveInt(settings, HoardCapKey, PermitWeekKeyEconomyConfiguration.FallbackHoardCap, PermitWeekKeyEconomyConfiguration.MaximumHoardCap));
        }
        catch
        {
            return new PermitWeekKeyEconomyConfiguration(
                PermitWeekKeyEconomyConfiguration.FallbackWeeklyRate,
                PermitWeekKeyEconomyConfiguration.FallbackHoardCap);
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
