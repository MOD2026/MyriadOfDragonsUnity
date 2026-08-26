using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// One exception entry. Every field is required by `UI_VERIFICATION_GATE_v1.md` section 4d -
    /// a partially-filled entry is rejected rather than honoured, because the whole point of a
    /// manifest over inline annotations is that an escape hatch costs something to open.
    /// </summary>
    [Serializable]
    public class UiGateException
    {
        public string id;
        public string screen;
        public string controlA;
        public string controlB;
        public string relationship;
        public string reason;
        public string owner;
        public string expires;      // yyyy-MM-dd, UTC

        [NonSerialized] public bool Matched;
    }

    [Serializable]
    public class UiGateExceptionFile
    {
        public List<UiGateException> exceptions = new List<UiGateException>();
    }

    /// <summary>
    /// The machine-checked exception manifest for the UI validation run.
    ///
    /// Section 4d is explicit that inline annotations turn a gate into theatre: an escape route
    /// available at the point of failure gets used routinely, and the suppression list never
    /// shrinks. So exceptions live in ONE reviewable file, every entry carries an owner and an
    /// expiry, and - the part that actually keeps it honest - THE VALIDATOR FAILS ON AN EXCEPTION
    /// THAT NO LONGER MATCHES REAL GEOMETRY. An entry that stops matching is not silently ignored;
    /// it is a finding, because a stale suppression is how a manifest rots into a permanent
    /// allowlist that quietly covers defects nobody remembers granting.
    /// </summary>
    public static class UiGateExceptionManifest
    {
        public static string ManifestPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "ui_gate_exceptions.json"));

        /// <summary>Loads the manifest. A MISSING file is legitimate and means "no exceptions
        /// granted" - that is the correct default and must not fail the run. A file that exists but
        /// cannot be parsed IS a failure: silently treating unreadable JSON as an empty list would
        /// discard real granted exceptions and produce a wall of findings with no explanation.</summary>
        public static List<UiGateException> Load(List<string> findings)
        {
            var result = new List<UiGateException>();
            string path = ManifestPath;
            if (!File.Exists(path)) return result;

            UiGateExceptionFile parsed;
            try
            {
                parsed = JsonUtility.FromJson<UiGateExceptionFile>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                findings.Add("EXCEPTION MANIFEST: '" + path + "' exists but could not be parsed (" +
                             e.Message + "). Refusing to treat it as empty - that would silently " +
                             "drop every granted exception.");
                return result;
            }

            if (parsed?.exceptions == null) return result;

            foreach (UiGateException ex in parsed.exceptions)
            {
                if (ex == null) continue;
                if (IsOverbroad(ex, out string why))
                {
                    // Rejected, NOT honoured. An entry missing its owner or expiry is exactly the
                    // permanent blanket suppression the manifest exists to prevent.
                    findings.Add("EXCEPTION MANIFEST: entry '" + (ex.id ?? "<no id>") + "' is overbroad - " +
                                 why + ". It is rejected, not applied.");
                    continue;
                }

                if (IsExpired(ex, out string expiryProblem))
                {
                    findings.Add("EXCEPTION MANIFEST: entry '" + ex.id + "' " + expiryProblem +
                                 ". An expired exception is a failure, not a silent pass.");
                    continue;
                }

                result.Add(ex);
            }

            return result;
        }

        /// <summary>Reports every loaded exception that never matched real geometry this run.
        /// Called AFTER the traversal, so "matched" reflects measured screens rather than intent.</summary>
        public static void ReportUnmatched(List<UiGateException> applied, List<string> findings)
        {
            foreach (UiGateException ex in applied)
            {
                if (ex.Matched) continue;
                findings.Add("EXCEPTION MANIFEST: entry '" + ex.id + "' (" + ex.screen + ": " +
                             ex.controlA + " / " + ex.controlB + ") matched NOTHING in this run. " +
                             "Either the geometry it excused is gone - in which case delete it - or " +
                             "it never described real geometry. A suppression nobody can point at is " +
                             "how this file rots into a permanent allowlist.");
            }
        }

        /// <summary>True if this overlap between two named controls on this screen is excused.
        /// Order-insensitive: which control the traversal happens to visit first is an
        /// implementation detail, and making an exception depend on it would be a trap.</summary>
        public static bool Excuses(List<UiGateException> applied, string screen, string a, string b)
        {
            foreach (UiGateException ex in applied)
            {
                if (!string.Equals(ex.screen, screen, StringComparison.Ordinal)) continue;

                bool forward = ex.controlA == a && ex.controlB == b;
                bool reverse = ex.controlA == b && ex.controlB == a;
                if (!forward && !reverse) continue;

                ex.Matched = true;
                return true;
            }

            return false;
        }

        private static bool IsOverbroad(UiGateException ex, out string why)
        {
            // Wildcards are rejected outright. "Exact screen + control scope" is a required field,
            // and one '*' would re-create the blanket suppression the manifest replaced.
            foreach (var (name, value) in new[]
                     {
                         ("id", ex.id), ("screen", ex.screen), ("controlA", ex.controlA),
                         ("controlB", ex.controlB), ("relationship", ex.relationship),
                         ("reason", ex.reason), ("owner", ex.owner), ("expires", ex.expires),
                     })
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    why = "required field '" + name + "' is empty";
                    return true;
                }

                if (value.Contains("*"))
                {
                    why = "field '" + name + "' contains a wildcard, which is not an exact scope";
                    return true;
                }
            }

            why = null;
            return false;
        }

        private static bool IsExpired(UiGateException ex, out string problem)
        {
            if (!DateTime.TryParseExact(ex.expires, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime expiry))
            {
                problem = "has an unparseable expiry '" + ex.expires + "' (expected yyyy-MM-dd)";
                return true;
            }

            if (expiry.Date < DateTime.UtcNow.Date)
            {
                problem = "expired on " + ex.expires;
                return true;
            }

            problem = null;
            return false;
        }
    }
}
