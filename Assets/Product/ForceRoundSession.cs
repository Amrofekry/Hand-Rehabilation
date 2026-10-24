using System;
using System.Collections.Generic;

namespace GripRehab.Product
{
    public enum ForceRoundKind { Hold, Control, Pulse }

    /// <summary>One Module 1 round. Effort is a fraction of the player's reference (0..1+).</summary>
    public sealed class ForceRound
    {
        public ForceRoundKind Kind { get; internal set; }
        public float BandLow { get; internal set; }
        public float BandHigh { get; internal set; }
        public float BreakAt { get; internal set; }
        public float HoldSeconds { get; internal set; }
        public int PulsesRequired { get; internal set; }
        public float Progress { get; internal set; }
        public int PulseCount { get; internal set; }
        public float Elapsed { get; internal set; }
        public float ReactionSeconds { get; internal set; } = -1;
        public bool Completed { get; internal set; }
        public bool TimedOut { get; internal set; }
        public int Breaks { get; internal set; }
        public int Score { get; internal set; }
        public float Fraction => Kind == ForceRoundKind.Pulse ? PulseCount / (float)PulsesRequired : Progress / HoldSeconds;
        internal float pulseHeld; internal bool pulseArmed = true;
    }

    /// <summary>
    /// Module 1 game loop (Stage 3.1): Hold keeps effort in the band, Control does the same
    /// without crossing the break point (the object "cracks" and the hold restarts), Pulse
    /// needs separate squeeze-and-release repetitions. Produces the PDF's Module 1 metrics:
    /// force production (peak effort), grip endurance (time in band) and stability (in-band CV).
    /// </summary>
    public sealed class ForceRoundSession
    {
        public const float RoundTimeoutSeconds = 30, ReleaseBelow = .15f, PulseHoldSeconds = .3f, MaxStep = .1f;
        readonly List<ForceRound> rounds = new List<ForceRound>();
        int index;
        double bandSum, bandSquares; int bandSamples;
        public ForceTuning Tuning { get; }
        public IReadOnlyList<ForceRound> Rounds => rounds;
        public ForceRound Current => rounds[Math.Min(index, rounds.Count - 1)];
        public int RoundIndex => Math.Min(index, rounds.Count - 1);
        public int RoundCount => rounds.Count;
        public bool Complete => index >= rounds.Count;
        public int RoundsCompleted { get; private set; }
        public int Breaks { get; private set; }
        public float PeakEffort { get; private set; }
        public float InBandSeconds { get; private set; }
        public float StabilityCv => bandSamples < 2 ? 0 : (float)(Math.Sqrt(Math.Max(0, bandSquares / bandSamples - Math.Pow(bandSum / bandSamples, 2))) / Math.Max(1e-3, bandSum / bandSamples));
        public float MeanReactionSeconds
        {
            get { float sum = 0; int n = 0; foreach (var r in rounds) if (r.ReactionSeconds >= 0) { sum += r.ReactionSeconds; n++; } return n == 0 ? 0 : sum / n; }
        }
        public int Score
        {
            get { if (rounds.Count == 0) return 0; int sum = 0; foreach (var r in rounds) sum += r.Score; return sum / rounds.Count; }
        }
        public int Stars => !Complete ? 0 : Score >= 85 ? 3 : Score >= 65 ? 2 : Score > 0 ? 1 : 0;

        public ForceRoundSession(ForceTuning tuning)
        {
            if (tuning.rounds < 1 || !(tuning.bandLow > 0) || !(tuning.bandHigh > tuning.bandLow) || !(tuning.breakAt > tuning.bandHigh) || !(tuning.holdSeconds > 0) || tuning.pulses < 1)
                throw new ArgumentOutOfRangeException(nameof(tuning));
            Tuning = tuning;
            for (int i = 0; i < tuning.rounds; i++)
                rounds.Add(new ForceRound { Kind = (ForceRoundKind)(i % 3), BandLow = tuning.bandLow, BandHigh = tuning.bandHigh,
                    BreakAt = tuning.breakAt, HoldSeconds = tuning.holdSeconds, PulsesRequired = tuning.pulses });
        }

        public string Instruction
        {
            get
            {
                if (Complete) return "Session complete";
                var r = Current; string band = Percent(r.BandLow) + "–" + Percent(r.BandHigh);
                switch (r.Kind)
                {
                    case ForceRoundKind.Control: return "Steady squeeze " + band + " — stay under " + Percent(r.BreakAt) + " or it cracks";
                    case ForceRoundKind.Pulse: return "Squeeze into " + band + " and fully release · " + r.PulseCount + "/" + r.PulsesRequired;
                    default: return "Hold " + band + " for " + r.HoldSeconds.ToString("0.0") + " s";
                }
            }
        }
        static string Percent(float value) => Math.Round(value * 100) + "%";

        public void Step(float effort, float deltaSeconds)
        {
            if (Complete || !TrialSample.Finite(effort) || !TrialSample.Finite(deltaSeconds) || effort < 0 || deltaSeconds <= 0) return;
            float dt = Math.Min(deltaSeconds, MaxStep);
            var r = Current; r.Elapsed += dt;
            PeakEffort = Math.Max(PeakEffort, effort);
            bool inBand = effort >= r.BandLow && effort <= r.BandHigh;
            if (inBand)
            {
                if (r.ReactionSeconds < 0) r.ReactionSeconds = r.Elapsed;
                InBandSeconds += dt; bandSum += effort; bandSquares += effort * effort; bandSamples++;
            }
            switch (r.Kind)
            {
                case ForceRoundKind.Pulse:
                    if (effort < ReleaseBelow) { r.pulseArmed = true; r.pulseHeld = 0; }
                    else if (inBand && r.pulseArmed && (r.pulseHeld += dt) >= PulseHoldSeconds) { r.PulseCount++; r.pulseArmed = false; }
                    if (r.PulseCount >= r.PulsesRequired) Finish(r, false);
                    break;
                default:
                    if (r.Kind == ForceRoundKind.Control && effort > r.BreakAt) { r.Breaks++; Breaks++; r.Progress = 0; break; }
                    r.Progress = inBand ? Math.Min(r.HoldSeconds, r.Progress + dt) : Math.Max(0, r.Progress - dt * .5f);
                    if (r.Progress >= r.HoldSeconds) Finish(r, false);
                    break;
            }
            if (!r.Completed && r.Elapsed >= RoundTimeoutSeconds) Finish(r, true);
        }

        void Finish(ForceRound r, bool timedOut)
        {
            r.Completed = !timedOut; r.TimedOut = timedOut;
            if (timedOut) r.Score = (int)Math.Round(20 * Math.Min(1, r.Fraction));
            else
            {
                float expected = (r.Kind == ForceRoundKind.Pulse ? r.PulsesRequired * 1.2f : r.HoldSeconds) + 2;
                float late = Math.Max(0, r.Elapsed - expected) / Math.Max(1, RoundTimeoutSeconds - expected);
                r.Score = Math.Max(20, (int)Math.Round(100 - 60 * Math.Min(1, late) - 15 * r.Breaks));
                RoundsCompleted++;
            }
            index++;
        }

        /// <summary>Saved with the session and shown on the results page.</summary>
        public List<OutcomeMetric> Outcomes() => new List<OutcomeMetric>
        {
            new OutcomeMetric { key = "force.rounds", label = "Rounds completed", unit = "", value = RoundsCompleted },
            new OutcomeMetric { key = "force.peakEffort", label = "Peak effort", unit = "% ref", value = (float)Math.Round(PeakEffort * 100, 1) },
            new OutcomeMetric { key = "force.enduranceSeconds", label = "Time in target band", unit = "s", value = (float)Math.Round(InBandSeconds, 1) },
            new OutcomeMetric { key = "force.stabilityCv", label = "Grip steadiness (CV)", unit = "", value = (float)Math.Round(StabilityCv, 3) },
            new OutcomeMetric { key = "force.reactionSeconds", label = "Mean time to target", unit = "s", value = (float)Math.Round(MeanReactionSeconds, 2) },
            new OutcomeMetric { key = "force.breaks", label = "Over-squeezes", unit = "", value = Breaks },
            new OutcomeMetric { key = "stars", label = "Stars", unit = "", value = Stars }
        };
    }
}
