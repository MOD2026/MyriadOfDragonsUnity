using System;
using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.CloudCode.Telemetry;

/// <summary>Retention windows exactly as locked (docs/LOCKED_DECISIONS_REGISTER.md "Retention telemetry
/// architecture - LOCKED"): D1 24-48h, D7 168-192h, D14 336-360h, D28 672-696h after a player's first
/// server-received event. Each window is treated as half-open [start, end); the lock gives only the hour
/// ranges, so that boundary convention is this code's one interpretive choice.
///
/// THESE ARE QUERY-TIME DEFINITIONS, NEVER STORED. The lock is explicit: retention is "computed from
/// elapsed UTC windows ... in analytics QUERIES, never precomputed/stored" (no d7Retained flag anywhere).
/// This class exists so the windows have one tested definition for the analytics query/export layer and
/// tests; nothing in the ingestion path or any save calls it.
///
/// D30 is NOT defined here: the lock specifies D28 (default Unity dashboards are "D30-shaped", but no D30
/// hour window was ever locked), so inventing one would be guessing. It needs a BS/owner decision.</summary>
public static class RetentionCohorts
{
    private const long HourMs = 3_600_000L;

    public sealed class Window
    {
        public string Name { get; }
        public long StartHours { get; }
        public long EndHours { get; }

        public Window(string name, long startHours, long endHours)
        {
            Name = name;
            StartHours = startHours;
            EndHours = endHours;
        }
    }

    public static readonly IReadOnlyList<Window> Windows = new[]
    {
        new Window("D1", 24, 48),
        new Window("D7", 168, 192),
        new Window("D14", 336, 360),
        new Window("D28", 672, 696),
    };

    /// <summary>Which window (if any) an event at <paramref name="eventUtcMs"/> falls in relative to the
    /// player's <paramref name="anchorUtcMs"/> (their first server-received event). Null = no window
    /// (including anything before the anchor).</summary>
    public static string? Classify(long anchorUtcMs, long eventUtcMs)
    {
        long elapsed = eventUtcMs - anchorUtcMs;
        if (elapsed < 0)
        {
            return null;
        }

        foreach (var window in Windows)
        {
            if (elapsed >= window.StartHours * HourMs && elapsed < window.EndHours * HourMs)
            {
                return window.Name;
            }
        }

        return null;
    }

    public sealed class Report
    {
        public int CohortSize { get; set; }
        public Dictionary<string, int> RetainedByWindow { get; } = Windows.ToDictionary(w => w.Name, _ => 0);
    }

    /// <summary>Computes retained-player counts per window from raw records: each pseudonymous id's anchor
    /// is its earliest ServerReceivedAtUtcMs (the authoritative timestamp - the client timestamp is never
    /// used), and a player is retained in a window if any of its events falls inside it. A pure function
    /// of the records; deterministic and order-independent; nothing is persisted.</summary>
    public static Report Compute(IEnumerable<TelemetryRecord> records)
    {
        var report = new Report();
        foreach (var player in records.GroupBy(r => r.PseudonymousId))
        {
            report.CohortSize++;
            long anchor = player.Min(r => r.ServerReceivedAtUtcMs);
            var hit = new HashSet<string>();
            foreach (var record in player)
            {
                string? window = Classify(anchor, record.ServerReceivedAtUtcMs);
                if (window != null)
                {
                    hit.Add(window);
                }
            }

            foreach (string name in hit)
            {
                report.RetainedByWindow[name]++;
            }
        }

        return report;
    }
}
