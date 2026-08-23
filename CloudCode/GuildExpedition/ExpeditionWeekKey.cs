using System;

namespace MyriadOfDragons.CloudCode.GuildExpedition;

/// <summary>
/// Pure Monday-00:00-UTC-to-Monday week key (YYYY-Www), matching
/// GUILD_EXPEDITION_COMPETITION_RECONCILIATION_2026-08-23.md §3.1's calendar contract exactly:
/// "Monday 00:00 UTC to following Monday 00:00 UTC (end exclusive)". That is precisely an ISO
/// 8601 week boundary, so this is the same algorithm as PermitWeekKey's IsoWeekKey, duplicated
/// (not shared) here deliberately - each CloudCode module in this project is an independent
/// deployable unit with no ProjectReference between siblings (see CloudCode/SocialSafety and
/// CloudCode/PermitWeekKey, which don't reference each other either), so this small, independently
/// tested routine is copied rather than creating a cross-module dependency for one function.
/// </summary>
public static class ExpeditionWeekKey
{
    public static string FromUtcMs(long utcMs)
    {
        DateTime date = DateTimeOffset.FromUnixTimeMilliseconds(utcMs).UtcDateTime.Date;
        return Format(date);
    }

    public static string Format(DateTime date)
    {
        (int year, int week) = GetIsoWeekYearAndWeek(date);
        return $"{year:D4}-W{week:D2}";
    }

    public static (int Year, int Week) GetIsoWeekYearAndWeek(DateTime date)
    {
        DateTime day = date.Date;
        int isoDayOfWeek = (int)day.DayOfWeek == 0 ? 7 : (int)day.DayOfWeek;
        DateTime thursday = day.AddDays(4 - isoDayOfWeek);
        int isoYear = thursday.Year;
        DateTime jan1 = new DateTime(isoYear, 1, 1);
        int isoWeek = ((thursday - jan1).Days / 7) + 1;
        return (isoYear, isoWeek);
    }
}
