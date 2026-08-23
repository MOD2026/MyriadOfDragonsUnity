using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.SocialSafety;

public sealed class SocialSafetyRateLimitConfiguration
{
    public const int FallbackPerMinute = 10;
    public const int FallbackPerDay = 100;
    public const int MaximumPerMinute = 100;
    public const int MaximumPerDay = 1000;

    public SocialSafetyRateLimitConfiguration(int perMinute, int perDay)
    {
        PerMinute = perMinute;
        PerDay = perDay;
    }

    public int PerMinute { get; }
    public int PerDay { get; }
}

public interface ISocialSafetyRateLimitConfiguration
{
    Task<SocialSafetyRateLimitConfiguration> LoadAsync(IExecutionContext context, IGameApiClient apiClient);
}

public sealed class RemoteConfigSocialSafetyRateLimitConfiguration : ISocialSafetyRateLimitConfiguration
{
    public const string PerMinuteKey = "socialSafety.rateLimit.perMinute";
    public const string PerDayKey = "socialSafety.rateLimit.perDay";

    public async Task<SocialSafetyRateLimitConfiguration> LoadAsync(IExecutionContext context, IGameApiClient apiClient)
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
                new List<string> { PerMinuteKey, PerDayKey },
                null,
                CancellationToken.None);
            var settings = response.Data.Configs.Settings;
            return new SocialSafetyRateLimitConfiguration(
                ReadPositiveInt(settings, PerMinuteKey, SocialSafetyRateLimitConfiguration.FallbackPerMinute, SocialSafetyRateLimitConfiguration.MaximumPerMinute),
                ReadPositiveInt(settings, PerDayKey, SocialSafetyRateLimitConfiguration.FallbackPerDay, SocialSafetyRateLimitConfiguration.MaximumPerDay));
        }
        catch
        {
            return new SocialSafetyRateLimitConfiguration(
                SocialSafetyRateLimitConfiguration.FallbackPerMinute,
                SocialSafetyRateLimitConfiguration.FallbackPerDay);
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

public sealed class SocialSafetyRateLimitReservation
{
    private SocialSafetyRateLimitReservation(bool allowed, string? errorCode)
    {
        Allowed = allowed;
        ErrorCode = errorCode;
    }

    public bool Allowed { get; }
    public string? ErrorCode { get; }

    public static SocialSafetyRateLimitReservation Success() => new(true, null);
    public static SocialSafetyRateLimitReservation Failure(string errorCode) => new(false, errorCode);
}

public sealed class SocialSafetyRateLimiter
{
    private const int MaxReservationAttempts = 2;
    private const long MinuteWindowMs = 60_000;
    private const long DayWindowMs = 86_400_000;
    private readonly ISocialSafetyStore _store;
    private readonly ISocialSafetyRateLimitConfiguration _configuration;
    private readonly ISocialSafetyClock _clock;

    public SocialSafetyRateLimiter(ISocialSafetyStore store, ISocialSafetyRateLimitConfiguration configuration, ISocialSafetyClock clock)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<SocialSafetyRateLimitReservation> ReserveAsync(IExecutionContext context, IGameApiClient apiClient)
    {
        var limits = await _configuration.LoadAsync(context, apiClient);
        long now = _clock.UtcNowMs;
        for (int attempt = 0; attempt < MaxReservationAttempts; attempt++)
        {
            try
            {
                var state = await _store.LoadRateStateAsync(context, apiClient);
                ResetExpiredWindows(state, now);
                if (state.MinuteCount >= limits.PerMinute || state.DayCount >= limits.PerDay)
                {
                    return SocialSafetyRateLimitReservation.Failure("RATE_LIMITED");
                }

                state.MinuteCount++;
                state.DayCount++;
                await _store.SaveRateStateAsync(context, apiClient, state);
                return SocialSafetyRateLimitReservation.Success();
            }
            catch (SocialSafetyStorageException exception) when (exception.ErrorCode == "CONFLICT" && attempt + 1 < MaxReservationAttempts)
            {
            }
            catch (SocialSafetyStorageException exception)
            {
                return SocialSafetyRateLimitReservation.Failure(exception.ErrorCode);
            }
        }

        return SocialSafetyRateLimitReservation.Failure("CONFLICT");
    }

    private static void ResetExpiredWindows(SocialSafetyRateState state, long now)
    {
        if (state.MinuteWindowStartUtcMs <= 0 || now - state.MinuteWindowStartUtcMs >= MinuteWindowMs)
        {
            state.MinuteWindowStartUtcMs = now;
            state.MinuteCount = 0;
        }

        if (state.DayWindowStartUtcMs <= 0 || now - state.DayWindowStartUtcMs >= DayWindowMs)
        {
            state.DayWindowStartUtcMs = now;
            state.DayCount = 0;
        }
    }
}