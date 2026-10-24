using System;
using GripRehab.Product;
using UnityEngine;

namespace GripRehab.Editor
{
    // Stage 5 gate: the closed adaptive loop on the SYNTHETIC policy fixture. Each step goes
    // through the research safety policy, the bounded adaptive level and every module's settings,
    // and each pause or stop is checked for its plain-language, research-labelled wording.
    // Synthetic replay only: no camera, device, person or patient record is involved.
    public static class ClosedLoopVerification
    {
        [Serializable] class Step { public double now; public bool valid; public float[] signals; public string state; public int level, signs; }
        [Serializable] class Case { public string name; public Step[] steps; }
        [Serializable] class Replay { public Case[] cases; }

        public static void Run()
        {
            var replay = JsonUtility.FromJson<Replay>(Resources.Load<TextAsset>("ResearchAi/SYNTHETIC_policy-fixture").text);
            Require(replay != null && replay.cases.Length >= 5, "SYNTHETIC policy fixture loads");
            int raised = 0, paused = 0, stopped = 0;
            foreach (var c in replay.cases)
            {
                const int start = 2;
                var policy = new FatigueSafetyPolicy(); var adaptive = new AdaptiveDifficulty(start, policy.Level);
                var rounds = new ForceRoundSession(ExerciseTuning.Force(start)); int previous = start;
                foreach (var row in c.steps)
                {
                    var d = policy.Step(row.now, row.valid, Signal(row, 0), Signal(row, 1), Signal(row, 2), Signal(row, 3), true);
                    Require(d.state == row.state && d.level == row.level && d.signs == row.signs, c.name + " policy matches the fixture at " + row.now);
                    if (adaptive.Apply(d.level)) rounds.Retune(ExerciseTuning.Force(adaptive.Level));
                    int level = adaptive.Level;
                    Require(Math.Abs(level - previous) <= 1 && Math.Abs(level - start) <= AdaptiveDifficulty.MaxOffset, c.name + " level step is bounded at " + row.now);
                    Require(rounds.Rounds[rounds.RoundIndex].BandHigh <= 1 && rounds.CurrentTuning.holdSeconds <= 6 &&
                        ExerciseTuning.MemoryLength(level) <= 8 && ExerciseTuning.MazeCells(level) <= 13, c.name + " module settings in range at " + row.now);
                    previous = level;
                    if (d.state != "active")
                    {
                        var message = SafetyMessages.Describe(d, false);
                        Require(!string.IsNullOrEmpty(message.title) && !message.body.Contains("persistent sign") && !message.body.Contains("invalid input"), c.name + " shows plain language, not policy codes");
                        Require(d.state == "stopped" ? message.stopped && !message.canResume : message.canResume, c.name + " offers the right choices");
                    }
                }
                if (adaptive.Level > start) raised++;
                if (policy.State == "warning") paused++;
                if (policy.State == "stopped") stopped++;
                if (c.name == "stable")
                    Require(adaptive.Level == start + 2 && rounds.CurrentTuning.bandLow > ExerciseTuning.Force(start).bandLow &&
                        ExerciseTuning.MemoryLength(adaptive.Level) > ExerciseTuning.MemoryLength(start), "steady play raises Module 1-3 difficulty by two bounded steps");
                if (c.name == "two") Require(policy.State == "stopped" && adaptive.Level == start, "two signs stop without raising difficulty");
                if (c.name == "dropout") Require(policy.State == "warning" && policy.Signs == 0, "signal loss pauses without counting a sign");
            }
            Require(raised >= 1 && paused >= 2 && stopped >= 1, "the replay exercises raise, pause and stop paths");
            Debug.Log("[REHAB_CLOSED_LOOP] PASS: SYNTHETIC replay drives bounded difficulty in Modules 1-3, plain-language pauses and a latched stop (" + replay.cases.Length + " cases)");
        }

        static float? Signal(Step row, int i) => row.signals[i] < 0 ? (float?)null : row.signals[i];

        static void Require(bool condition, string reason)
        {
            if (!condition) throw new Exception("Closed loop: " + reason);
        }
    }
}
