using System;
using System.IO;
using UnityEngine;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Temporary hang-profiling sink (WH 2026-08-26). Writes flushed lines to
    /// <c>wh_hang_profile_trace.txt</c> in the project cwd so a stall after Debug.Log still
    /// leaves a durable last-step marker. Prefix keeps these greppable in Unity logs too.
    /// </summary>
    public static class WhHangProfileTrace
    {
        public const string FileName = "wh_hang_profile_trace.txt";
        public const string LogPrefix = "[WH-HANG-PROFILE]";

        public static string TracePath =>
            Path.Combine(Directory.GetCurrentDirectory(), FileName);

        public static void Mark(string step, long elapsedMs = -1)
        {
            string msPart = elapsedMs >= 0 ? $"{elapsedMs}ms" : "-";
            string line = $"{DateTime.UtcNow:O}\t{msPart}\t{step}";
            UnityEngine.Debug.Log($"{LogPrefix} {msPart} {step}");
            try
            {
                File.AppendAllText(TracePath, line + Environment.NewLine);
            }
            catch (Exception)
            {
                // Profiling must never alter purchase semantics.
            }
        }
    }
}
