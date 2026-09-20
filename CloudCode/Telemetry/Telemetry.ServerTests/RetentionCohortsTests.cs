using NUnit.Framework;

namespace MyriadOfDragons.CloudCode.Telemetry.Tests;

public sealed class RetentionCohortsTests
{
    private const long H = 3_600_000L;

    [TestCase(24, "D1")]
    [TestCase(47, "D1")]
    [TestCase(168, "D7")]
    [TestCase(191, "D7")]
    [TestCase(336, "D14")]
    [TestCase(672, "D28")]
    [TestCase(695, "D28")]
    public void InsideWindow_Classifies(long hours, string expected) =>
        Assert.That(RetentionCohorts.Classify(0, hours * H), Is.EqualTo(expected));

    [TestCase(-1)]
    [TestCase(23)]
    [TestCase(48)]
    [TestCase(167)]
    [TestCase(192)]
    [TestCase(696)]
    [TestCase(720)]
    public void OutsideWindow_IsNull(long hours) =>
        Assert.That(RetentionCohorts.Classify(0, hours * H), Is.Null);

    [Test]
    public void WindowsAreExactlyTheLockedSet_AndD30IsNotInvented() =>
        Assert.That(RetentionCohorts.Windows.Select(w => w.Name), Is.EqualTo(new[] { "D1", "D7", "D14", "D28" }));

    [Test]
    public void Compute_AnchorsOnEarliestServerTimestamp_IgnoresClientTime_AndIsOrderIndependent()
    {
        TelemetryRecord R(string p, long serverH, long clientH = 0) => new()
        {
            PseudonymousId = p, ServerReceivedAtUtcMs = serverH * H, ClientOccurredAtUtcMs = clientH * H,
        };
        var records = new[] { R("a", 30, 999), R("a", 0), R("a", 170), R("b", 5), R("b", 6) };

        var forward = RetentionCohorts.Compute(records);
        var reversed = RetentionCohorts.Compute(records.Reverse());

        Assert.That(forward.CohortSize, Is.EqualTo(2));
        Assert.That(forward.RetainedByWindow["D1"], Is.EqualTo(1));
        Assert.That(forward.RetainedByWindow["D7"], Is.EqualTo(1));
        Assert.That(forward.RetainedByWindow["D28"], Is.EqualTo(0));
        Assert.That(reversed.RetainedByWindow, Is.EqualTo(forward.RetainedByWindow));
    }
}
