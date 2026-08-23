using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace MyriadOfDragons.CloudCode.GuildExpedition.Tests;

/// <summary>Same structural-invariant approach as PermitWeekKey's IsoWeekKeyTests, since this is
/// the same algorithm (deliberately duplicated, not shared - see ExpeditionWeekKey's class
/// comment).</summary>
public sealed class ExpeditionWeekKeyTests
{
    [Test]
    public void January4IsAlwaysInWeekOneOfItsOwnYear()
    {
        for (int year = 2000; year <= 2100; year++)
        {
            var (isoYear, isoWeek) = ExpeditionWeekKey.GetIsoWeekYearAndWeek(new DateTime(year, 1, 4));
            Assert.That(isoWeek, Is.EqualTo(1), $"year {year}");
            Assert.That(isoYear, Is.EqualTo(year), $"year {year}");
        }
    }

    [Test]
    public void December28IsAlwaysInTheLastWeekOfItsOwnYear()
    {
        for (int year = 2000; year <= 2100; year++)
        {
            var (isoYear, isoWeek) = ExpeditionWeekKey.GetIsoWeekYearAndWeek(new DateTime(year, 12, 28));
            Assert.That(isoWeek, Is.AnyOf(52, 53), $"year {year}");
            Assert.That(isoYear, Is.EqualTo(year), $"year {year}");
        }
    }

    [Test]
    public void FormatProducesTheWeekKeyStringShape()
    {
        for (int year = 2020; year <= 2030; year++)
        {
            string key = ExpeditionWeekKey.Format(new DateTime(year, 6, 15));
            Assert.That(key, Does.Match(@"^\d{4}-W\d{2}$"), key);
        }
    }

    [Test]
    public void MondayThroughSundayShareTheSameKey_MatchingTheMondayToMondayContract()
    {
        DateTime monday = FindNextMonday(new DateTime(2024, 1, 1));
        string key = ExpeditionWeekKey.Format(monday);
        for (int offset = 0; offset < 7; offset++)
        {
            Assert.That(ExpeditionWeekKey.Format(monday.AddDays(offset)), Is.EqualTo(key), $"offset {offset}");
        }

        // §3.1: "Monday 00:00 UTC to following Monday 00:00 UTC (end exclusive)" - the following
        // Monday must already be a new key, not still inside this one.
        Assert.That(ExpeditionWeekKey.Format(monday.AddDays(7)), Is.Not.EqualTo(key));
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
