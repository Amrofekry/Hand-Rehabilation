using System.Collections.Generic;

namespace GripRehab.Product
{
    public struct SafetyMessage
    {
        public string title, body;
        public bool canResume, stopped;
    }

    /// <summary>
    /// Stage 5.3: plain-language wording for the research safety pause and stop. Every screen
    /// carries the research-indicator label; nothing here is a diagnosis or a validated measure.
    /// </summary>
    public static class SafetyMessages
    {
        public const string Indicator = "RESEARCH INDICATOR  ·  unvalidated estimate, not a medical assessment";
        static readonly string[] Reasons =
        {
            "your grip force fell well below your baseline",
            "your hand movement became less steady than your baseline",
            "your face looked strained for a while",
            "your pulse estimate rose above your resting level"
        };

        /// <summary>Joins the reasons for a sign mask (bit 0 force, 1 motion, 2 face, 3 pulse).</summary>
        public static string Signs(int signs)
        {
            var parts = new List<string>();
            for (int i = 0; i < Reasons.Length; i++) if ((signs & (1 << i)) != 0) parts.Add(Reasons[i]);
            return parts.Count == 0 ? "" : string.Join(" and ", parts);
        }

        public static SafetyMessage Describe(FatigueSafetyDecision decision, bool sourceChanged, bool finishedByPlayer = false)
        {
            if (decision.state == "stopped")
                return new SafetyMessage
                {
                    stopped = true, title = finishedByPlayer ? "Session finished" : "Session stopped for today",
                    body = finishedByPlayer ? "You finished the session during a rest pause. That was the right call if you felt tired." :
                        "Two different research signs appeared together: " + Signs(decision.signs) +
                        ". The session has ended and will not restart automatically. Please rest your hand."
                };
            if (sourceChanged)
                return new SafetyMessage
                {
                    title = "Input changed",
                    body = "The input source, hand, grip shape or calibration changed during this attempt, so readings can no longer be compared with your baseline. Finish this attempt, then collect a new baseline for this input."
                };
            if (decision.signs != 0)
                return new SafetyMessage
                {
                    canResume = true, title = "Time for a short rest",
                    body = "A research sign appeared and stayed: " + Signs(decision.signs) +
                        ". Rest your hand. Resume when you feel ready and the signal has settled, or finish the session."
                };
            if (decision.state == "warning" && decision.reason != null && decision.reason.StartsWith("invalid"))
                return new SafetyMessage
                {
                    canResume = true, title = "Paused: signal lost",
                    body = "We lost the hand or sensor signal, so practice is paused. This is not counted as tiredness. When the signal is back and steady, press Resume."
                };
            return new SafetyMessage { canResume = true, title = "Paused", body = "Practice is paused. Resume when you are ready, or finish the session." };
        }

        public static string ResumeStatus(bool ready) => ready ? "Signals have settled. Resume when you feel ready." :
            "Waiting for a few seconds of fresh, settled signal before Resume is allowed.";
    }
}
