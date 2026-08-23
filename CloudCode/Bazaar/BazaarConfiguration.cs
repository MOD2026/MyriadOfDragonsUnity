using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Bazaar;

/// <summary>Fee/hold/limit numbers, all sourced from the CC-LOCKED decisions in
/// BAZAAR_PHASE1_CC_ACCEPT_2026-08-23.md #1-#2 and PHASE1_BAZAAR_SYSTEM_PACKET_2026-08-23.md §3 -
/// these are locked design numbers, not placeholders (unlike GuildExpedition's manifest). Genesis
/// liquidity numbers (§3/§4 of the accept doc) are NOT here - that's a separate, deferred system,
/// see README.</summary>
public sealed class BazaarRulesConfiguration
{
    public const int FallbackListingFeePercent = 2;
    public const int FallbackListingFeeMinimumGold = 50;
    public const int FallbackSaleTaxPercent = 12;
    public const int FallbackSaleTaxBurnPercent = 6;
    public const long FallbackAcquisitionHoldMs = 7L * 24 * 60 * 60 * 1000;   // 7 days
    public const long FallbackRelistHoldMs = 72L * 60 * 60 * 1000;           // 72 hours
    public const int FallbackMaxSalesPerRollingWindow = 5;
    public const long FallbackRollingSalesWindowMs = 7L * 24 * 60 * 60 * 1000; // 7 days
    public const int MaximumPercent = 100;

    public BazaarRulesConfiguration(
        int listingFeePercent,
        int listingFeeMinimumGold,
        int saleTaxPercent,
        int saleTaxBurnPercent,
        long acquisitionHoldMs,
        long relistHoldMs,
        int maxSalesPerRollingWindow,
        long rollingSalesWindowMs)
    {
        ListingFeePercent = listingFeePercent;
        ListingFeeMinimumGold = listingFeeMinimumGold;
        SaleTaxPercent = saleTaxPercent;
        SaleTaxBurnPercent = saleTaxBurnPercent;
        AcquisitionHoldMs = acquisitionHoldMs;
        RelistHoldMs = relistHoldMs;
        MaxSalesPerRollingWindow = maxSalesPerRollingWindow;
        RollingSalesWindowMs = rollingSalesWindowMs;
    }

    public int ListingFeePercent { get; }
    public int ListingFeeMinimumGold { get; }
    public int SaleTaxPercent { get; }
    public int SaleTaxBurnPercent { get; }
    public long AcquisitionHoldMs { get; }
    public long RelistHoldMs { get; }
    public int MaxSalesPerRollingWindow { get; }
    public long RollingSalesWindowMs { get; }

    public static BazaarRulesConfiguration Fallback() => new(
        FallbackListingFeePercent, FallbackListingFeeMinimumGold,
        FallbackSaleTaxPercent, FallbackSaleTaxBurnPercent,
        FallbackAcquisitionHoldMs, FallbackRelistHoldMs,
        FallbackMaxSalesPerRollingWindow, FallbackRollingSalesWindowMs);
}

public interface IBazaarRulesConfiguration
{
    Task<BazaarRulesConfiguration> LoadAsync(IExecutionContext context, IGameApiClient apiClient);
}

public sealed class RemoteConfigBazaarRulesConfiguration : IBazaarRulesConfiguration
{
    public const string ListingFeePercentKey = "bazaar.listingFeePercent";
    public const string ListingFeeMinimumGoldKey = "bazaar.listingFeeMinimumGold";
    public const string SaleTaxPercentKey = "bazaar.saleTaxPercent";
    public const string SaleTaxBurnPercentKey = "bazaar.saleTaxBurnPercent";

    // Holds and the rolling sales cap are fixed per the CC lock, not exposed for Remote Config
    // retuning in this scaffold - only the percent/Gold-minimum fee inputs (already Remote
    // Config-shaped in the sibling modules) are treated as tunable here.
    public async Task<BazaarRulesConfiguration> LoadAsync(IExecutionContext context, IGameApiClient apiClient)
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
                new List<string> { ListingFeePercentKey, ListingFeeMinimumGoldKey, SaleTaxPercentKey, SaleTaxBurnPercentKey },
                null,
                CancellationToken.None);
            var settings = response.Data.Configs.Settings;
            var fallback = BazaarRulesConfiguration.Fallback();
            return new BazaarRulesConfiguration(
                ReadPercent(settings, ListingFeePercentKey, fallback.ListingFeePercent),
                ReadPositiveInt(settings, ListingFeeMinimumGoldKey, fallback.ListingFeeMinimumGold),
                ReadPercent(settings, SaleTaxPercentKey, fallback.SaleTaxPercent),
                ReadPercent(settings, SaleTaxBurnPercentKey, fallback.SaleTaxBurnPercent),
                fallback.AcquisitionHoldMs,
                fallback.RelistHoldMs,
                fallback.MaxSalesPerRollingWindow,
                fallback.RollingSalesWindowMs);
        }
        catch
        {
            return BazaarRulesConfiguration.Fallback();
        }
    }

    private static int ReadPercent(IReadOnlyDictionary<string, object> settings, string key, int fallback)
        => ReadPositiveIntBounded(settings, key, fallback, BazaarRulesConfiguration.MaximumPercent);

    private static int ReadPositiveInt(IReadOnlyDictionary<string, object> settings, string key, int fallback)
        => ReadPositiveIntBounded(settings, key, fallback, int.MaxValue);

    private static int ReadPositiveIntBounded(IReadOnlyDictionary<string, object> settings, string key, int fallback, int maximum)
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
