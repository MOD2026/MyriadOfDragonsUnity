using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace MyriadOfDragons.CloudCode.PermitWeekKey.Tests;

/// <summary>Tests assert the ISO 8601 rule's own well-known invariants rather than hardcoded
/// magic dates whose weekday I'd otherwise have to trust without independent verification -
/// the project's own "assert relationships, not magnitudes" non-negotiable applies just as well
/// to a pure date-math routine as it does to a Unity balance test.</summary>
public sealed class IsoWeekKeyTests
{
    [Test]
    public void January4IsAlwaysInWeekOneOfItsOwnYear()
    {
        // Documented ISO 8601 rule: January 4 always falls in week 1 of its own year.
        for (int year = 2000; year <= 2100; year++)
        {
            var (isoYear, isoWeek) = IsoWeekKey.GetIsoWeekYearAndWeek(new DateTime(year, 1, 4));
            Assert.That(isoWeek, Is.EqualTo(1), $"year {year}");
            Assert.That(isoYear, Is.EqualTo(year), $"year {year}");
        }
    }

    [Test]
    public void December28IsAlwaysInTheLastWeekOfItsOwnYear()
    {
        // Documented ISO 8601 rule: December 28 always falls in the last week (52 or 53) of its
        // own year.
        for (int year = 2000; year <= 2100; year++)
        {
            var (isoYear, isoWeek) = IsoWeekKey.GetIsoWeekYearAndWeek(new DateTime(year, 12, 28));
            Assert.That(isoWeek, Is.AnyOf(52, 53), $"year {year}");
            Assert.That(isoYear, Is.EqualTo(year), $"year {year}");
        }
    }

    [Test]
    public void FormatProducesTheIsoWeekStringShape()
    {
        for (int year = 2020; year <= 2030; year++)
        {
            string key = IsoWeekKey.Format(new DateTime(year, 6, 15));
            Assert.That(key, Does.Match(@"^\d{4}-W\d{2}$"), key);
        }
    }

    [Test]
    public void EveryDayMondayToSundayInOneWeekSharesTheSameKey()
    {
        DateTime monday = FindNextMonday(new DateTime(2024, 1, 1));
        string key = IsoWeekKey.Format(monday);
        for (int offset = 0; offset < 7; offset++)
        {
            Assert.That(IsoWeekKey.Format(monday.AddDays(offset)), Is.EqualTo(key), $"offset {offset}");
        }
    }

    [Test]
    public void SundayAndTheFollowingMondayAreAlwaysInDifferentWeeks()
    {
        DateTime monday = FindNextMonday(new DateTime(2024, 1, 1));
        for (int week = 0; week < 60; week++)
        {
            DateTime thisMonday = monday.AddDays(week * 7);
            DateTime priorSunday = thisMonday.AddDays(-1);
            Assert.That(IsoWeekKey.Format(priorSunday), Is.Not.EqualTo(IsoWeekKey.Format(thisMonday)),
                $"week offset {week}");
        }
    }

    [Test]
    public void FromUtcMsMatchesFormatForTheSameInstant()
    {
        var date = new DateTime(2026, 8, 23, 0, 0, 0, DateTimeKind.Utc);
        long utcMs = new DateTimeOffset(date).ToUnixTimeMilliseconds();
        Assert.That(IsoWeekKey.FromUtcMs(utcMs), Is.EqualTo(IsoWeekKey.Format(date)));
    }

    [Test]
    public void TimeOfDayWithinTheSameUtcDateDoesNotChangeTheWeekKey()
    {
        var midnight = new DateTime(2026, 8, 23, 0, 0, 0, DateTimeKind.Utc);
        var lateInDay = new DateTime(2026, 8, 23, 23, 59, 59, DateTimeKind.Utc);
        long midnightMs = new DateTimeOffset(midnight).ToUnixTimeMilliseconds();
        long lateMs = new DateTimeOffset(lateInDay).ToUnixTimeMilliseconds();
        Assert.That(IsoWeekKey.FromUtcMs(lateMs), Is.EqualTo(IsoWeekKey.FromUtcMs(midnightMs)));
    }

    private static DateTime FindNextMonday(DateTime from)
    {
        DateTime date = from;
        while (date.DayOfWeek != DayOfWeek.Monday)
        {
            date = date.AddDays(1);
        }

        return date;
    }
}
