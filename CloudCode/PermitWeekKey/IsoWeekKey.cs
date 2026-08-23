using System;

namespace MyriadOfDragons.CloudCode.PermitWeekKey;

/// <summary>
/// Pure ISO-8601 week key computation (YYYY-Www, e.g. "2026-W34"). Implemented directly against
/// the ISO 8601 rule (Monday-start weeks; week 1 is the week containing the year's first Thursday)
/// rather than System.Globalization.ISOWeek, since that type's availability on netstandard2.1
/// under the Cloud Code runtime isn't something this scaffold can verify without deploying. The
/// underlying rule is small and independently testable either way.
/// </summary>
public static class IsoWeekKey
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

        // ISO weekday: Monday = 1 ... Sunday = 7 (DateTime.DayOfWeek has Sunday = 0).
        int isoDayOfWeek = (int)day.DayOfWeek == 0 ? 7 : (int)day.DayOfWeek;

        // The Thursday of this ISO week always falls in the correct ISO week-numbering year,
        // even for the last/first days of December/January that belong to a different week year.
        DateTime thursday = day.AddDays(4 - isoDayOfWeek);
        int isoYear = thursday.Year;

        DateTime jan1 = new DateTime(isoYear, 1, 1);
        int isoWeek = ((thursday - jan1).Days / 7) + 1;

        return (isoYear, isoWeek);
    }
}
