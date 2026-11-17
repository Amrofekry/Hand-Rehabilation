using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GripRehab.Product
{
    /// <summary>One dashboard card. Value is "—" when nothing was measured, never a made-up 0.</summary>
    public struct SummaryCard
    {
        public string label, value, source;
        public bool missing;
    }

    /// <summary>Per-module statistics for the patient and doctor dashboards (Stage 6, WBS 5.1/5.2).</summary>
    public sealed class ModuleProgress
    {
        public int module, attempts, completed, safetyStops, incomplete, sessionsWithPauses;
        public int? latestScore, bestScore, latestLevel;
        /// <summary>Completed scores only, oldest to newest, 0..1 for the chart (last 20).</summary>
        public float[] scoreTrend = new float[0];
        public SessionRecord latest;
    }

    public static class ProgressSummary
    {
        public const string Missing = "—";
        static readonly string[] ModuleNames = { "", "Force + Object", "Cognitive", "Visuomotor" };
        public static string ModuleName(int module) => module >= 1 && module <= 3 ? ModuleNames[module] : "Module " + module;

        static IEnumerable<SessionRecord> Ordered(IEnumerable<SessionRecord> sessions) =>
            (sessions ?? Enumerable.Empty<SessionRecord>()).Where(s => s != null).OrderBy(s => s.startedUtc ?? "", StringComparer.Ordinal);

        /// <summary>Plain label for a session's outcome; stopped and incomplete attempts stand out.</summary>
        public static string StateLabel(SessionRecord s)
        {
            switch (s.state)
            {
                case RecordState.Completed: return "Completed";
                case RecordState.SafetyStopped: return "STOPPED · research safety";
                case RecordState.Interrupted: return "INCOMPLETE";
                default: return "IN PROGRESS";
            }
        }

        /// <summary>A completed attempt's score, or "—": an unfinished attempt has no score, not a score of 0.</summary>
        public static string ScoreText(SessionRecord s) => s.state == RecordState.Completed ? s.score + "/100" : Missing;

        public static ModuleProgress[] ByModule(IEnumerable<SessionRecord> sessions)
        {
            var all = Ordered(sessions).ToList();
            var result = new ModuleProgress[3];
            for (int m = 1; m <= 3; m++)
            {
                var mine = all.Where(s => s.module == m && s.state != RecordState.InProgress).ToList();
                var done = mine.Where(s => s.state == RecordState.Completed).ToList();
                var p = new ModuleProgress
                {
                    module = m, attempts = mine.Count, completed = done.Count,
                    safetyStops = mine.Count(s => s.state == RecordState.SafetyStopped),
                    incomplete = mine.Count(s => s.state == RecordState.Interrupted),
                    sessionsWithPauses = mine.Count(s => s.policyEvents != null && s.policyEvents.Any(e => e.status == "warning" || e.status == "stopped")),
                    latest = done.LastOrDefault(),
                    scoreTrend = done.Skip(Math.Max(0, done.Count - 20)).Select(s => Math.Max(0, Math.Min(100, s.score)) / 100f).ToArray()
                };
                if (done.Count > 0) { p.latestScore = done[done.Count - 1].score; p.bestScore = done.Max(s => s.score); }
                var leveled = mine.LastOrDefault(s => s.level > 0);
                if (leveled != null) p.latestLevel = leveled.adaptedLevel > 0 ? leveled.adaptedLevel : leveled.level;
                result[m - 1] = p;
            }
            return result;
        }

        /// <summary>The latest completed value of an outcome across the given modules, or null.</summary>
        public static OutcomeMetric Latest(IEnumerable<SessionRecord> sessions, params string[] keys)
        {
            foreach (var s in Ordered(sessions).Reverse())
            {
                if (s.state != RecordState.Completed || s.outcomes == null) continue;
                foreach (var key in keys)
                {
                    var o = s.outcomes.FirstOrDefault(x => x != null && x.key == key);
                    if (o != null && !float.IsNaN(o.value) && !float.IsInfinity(o.value)) return o;
                }
            }
            return null;
        }

        static SummaryCard Card(string label, OutcomeMetric o, string format, string unitOverride = null)
        {
            if (o == null) return new SummaryCard { label = label, value = Missing, missing = true, source = "not measured yet" };
            string unit = unitOverride ?? o.unit;
            return new SummaryCard { label = label, value = o.value.ToString(format, CultureInfo.InvariantCulture) + (string.IsNullOrEmpty(unit) ? "" : " " + unit), source = o.label };
        }

        /// <summary>The six patient summary cards from the latest completed attempts (Tremor comes from the camera-hand tea activity).</summary>
        public static List<SummaryCard> Cards(IEnumerable<SessionRecord> sessions)
        {
            var list = sessions?.ToList() ?? new List<SessionRecord>();
            return new List<SummaryCard>
            {
                Card("Force", Latest(list, "force.peakEffort"), "0"),
                Card("Endurance", Latest(list, "force.enduranceSeconds"), "0.0"),
                Card("Stability", Latest(list, "force.stabilityCv"), "0.000", "CV"),
                Card("Reaction time", Latest(list, "force.reactionSeconds", "memory.reactionSeconds"), "0.00"),
                Card("Accuracy", Latest(list, "trace.accuracy", "memory.accuracy", "maze.efficiency", "assembly.pathEfficiency", "tea.pourAccuracyPct"), "0"),
                Card("Tremor", Latest(list, "tea.tremorRmsMm"), "0.0")
            };
        }

        /// <summary>
        /// Latest Module 1 peak against the baseline grip reference, only when both are measured
        /// force in Newtons from the same source. Otherwise "—" with the reason.
        /// </summary>
        public static string BaselineComparison(IEnumerable<SessionRecord> sessions, CapabilityProfile baseline)
        {
            var latest = Ordered(sessions).LastOrDefault(s => s.module == 1 && s.state == RecordState.Completed);
            if (baseline == null) return Missing + " (no baseline yet)";
            if (latest == null) return Missing + " (no completed Module 1 attempt yet)";
            if (!latest.hasForce || !baseline.hasForce || baseline.mvcNewtons <= 0) return Missing + " (needs measured force in both)";
            var source = latest.metrics != null && latest.metrics.Count > 0 ? latest.metrics[0].source : null;
            if (source != null && baseline.source != null && source != baseline.source) return Missing + " (different input from the baseline)";
            float percent = 100f * latest.peakNewtons / baseline.mvcNewtons;
            return latest.peakNewtons.ToString("0.0", CultureInfo.InvariantCulture) + " N today vs " + baseline.mvcNewtons.ToString("0.0", CultureInfo.InvariantCulture) +
                " N baseline (" + percent.ToString("0", CultureInfo.InvariantCulture) + "%)" + (latest.synthetic || baseline.synthetic ? " · SYNTHETIC" : "");
        }

        /// <summary>Research pause and stop events, newest first, for the doctor's D3 view.</summary>
        public static List<string> SafetyEvents(IEnumerable<SessionRecord> sessions, int limit = 12)
        {
            var lines = new List<string>();
            foreach (var s in Ordered(sessions).Reverse())
            {
                if (s.policyEvents == null) continue;
                string last = null;
                foreach (var e in s.policyEvents)
                {
                    if (e == null || (e.status != "warning" && e.status != "stopped") || e.status == last) { last = e?.status; continue; }
                    last = e.status;
                    lines.Add((s.startedUtc ?? "") + " · " + ModuleName(s.module) + " · " + (e.status == "stopped" ? "STOP" : "PAUSE") +
                        " at " + e.seconds.ToString("0", CultureInfo.InvariantCulture) + " s · " + Plain(e));
                    if (lines.Count >= limit) return lines;
                }
            }
            return lines;
        }

        static string Plain(PolicyEvent e)
        {
            if (e.signs != 0) return SafetyMessages.Signs(e.signs);
            if (e.reason != null && e.reason.StartsWith("invalid")) return "signal lost (not counted as tiredness)";
            if (e.reason != null && e.reason.StartsWith("finished by the player")) return "finished by the player";
            return "paused";
        }
    }

    /// <summary>D4: templated recommendations a doctor can start from and then edit.</summary>
    public static class NoteTemplates
    {
        public static readonly string[] All =
        {
            "Continue at the current level. Review progress in one week.",
            "Shorten sessions and add rest breaks between rounds.",
            "Collect a new research baseline with the same input before the next session.",
            "Review the recorded research safety pauses or stops with the patient before continuing.",
            "Focus on steady grip control (stay inside the target band) rather than peak force."
        };
    }
}
