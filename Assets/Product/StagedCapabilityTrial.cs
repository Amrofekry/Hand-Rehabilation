using System;
namespace GripRehab.Product
{
    /// <summary>Timed research protocol. Only valid unique samples advance a stage.
    /// Motion is a visual joint-flexion span, not clinically validated anatomical ROM.</summary>
    public sealed class StagedCapabilityTrial
    {
        readonly CapabilityTrial summary;
        readonly float[] durations = { 3, 5, 3, 5, 3, 5, 10, 10, 10 };
        readonly string[] names = { "Comfortable grip trial 1", "Rest - release the grip", "Comfortable grip trial 2", "Rest - release the grip", "Comfortable grip trial 3", "Rest - release the grip", "Comfortable repeated opening and closing", "Comfortable sustained grip / endurance", "Hold the hand comfortably still / stability" };
        readonly float[] peaks = new float[3];
        TrialSample identity; bool hasIdentity; long sequence = long.MinValue;
        int stage; float elapsed, validSeconds, endurance, minClosure = 1, maxClosure, minRom = 180, maxRom;
        double lastAt = double.NaN, quietSum, quietSquares, quietJitter, holdSum, holdSquares; int motionCount, quietCount, holdCount;
        public string Problem { get; private set; }
        public bool Complete => stage >= durations.Length && Problem == null;
        public string Instruction => Complete ? "Baseline complete" : names[stage];
        // Shown to the player so a hold lands in the intended stage (audit P-021/P-023).
        public int StageNumber => Math.Min(stage + 1, durations.Length);
        public int StageCount => durations.Length;
        public float StageSecondsLeft => Complete ? 0 : Math.Max(0, durations[stage] - elapsed);
        public float Progress { get { float total = elapsed; for (int i = 0; i < stage; i++) total += durations[i]; return Math.Min(1, total / 54); } }
        public StagedCapabilityTrial(string account, string profile, bool consent)
        { summary = new CapabilityTrial(account, profile, consent, 120); }
        public void Step(TrialSample input, float ignoredDelta)
        {
            if (Complete || Problem != null) return;
            if (!input.Fresh || input.sequence <= sequence) return;
            if (hasIdentity && (identity.source != input.source || identity.shape != input.shape || identity.unit != input.unit || identity.calibrationRevision != input.calibrationRevision || identity.synthetic != input.synthetic))
            { Problem = "Source, hand, shape or calibration changed; restart the baseline."; return; }
            if (!hasIdentity) { identity = input; hasIdentity = true; }
            sequence = input.sequence;
            float dt = double.IsNaN(lastAt) ? 0 : (float)(input.capturedAt - lastAt); lastAt = input.capturedAt;
            if (dt <= 0 || dt >= .5f) return;
            dt = Math.Min(dt, .1f); elapsed += dt; validSeconds += dt; summary.Step(input, dt);
            if ((stage == 0 || stage == 2 || stage == 4) && input.unit == InputUnit.Newtons)
                peaks[stage / 2] = Math.Max(peaks[stage / 2], input.value);
            if (stage == 6 && input.MotionValid)
            {
                minClosure = Math.Min(minClosure, input.closure); maxClosure = Math.Max(maxClosure, input.closure); motionCount++;
                if (input.romValid) { minRom = Math.Min(minRom, input.romDegrees); maxRom = Math.Max(maxRom, input.romDegrees); }
            }
            float mvc = Math.Max(peaks[0], Math.Max(peaks[1], peaks[2]));
            if (stage == 7 && input.unit == InputUnit.Newtons)
            {
                holdSum+=input.value;holdSquares+=input.value*input.value;holdCount++;
                if(mvc>0&&input.value>=mvc*.3f)endurance+=dt;
            }
            if(stage==8 && input.MotionValid)
            {quietCount++;quietSum+=input.closure;quietSquares+=input.closure*input.closure;quietJitter+=input.jitter;}
            if (elapsed >= durations[stage]) { stage++; elapsed = 0; }
        }
        public CapabilityProfile Finish()
        {
            if (!Complete) throw new InvalidOperationException(Problem ?? "Finish the staged baseline.");
            // Read the compatible running summary without weakening legacy Finish requirements.
            var result = summary.Snapshot();
            result.settingsVersion = "staged-research-1"; result.validSeconds = validSeconds;
            result.mvcTrial1 = peaks[0]; result.mvcTrial2 = peaks[1]; result.mvcTrial3 = peaks[2];
            result.mvcNewtons = Math.Max(peaks[0], Math.Max(peaks[1], peaks[2]));
            result.enduranceSeconds = endurance; result.closureRange = motionCount > 1 ? maxClosure - minClosure : 0;
            result.hasRom = maxRom >= minRom && motionCount > 1; result.romDegrees = result.hasRom ? maxRom - minRom : 0;
            if(holdCount>1){result.meanNewtons=(float)(holdSum/holdCount);result.forceCv=(float)(Math.Sqrt(Math.Max(0,holdSquares/holdCount-result.meanNewtons*result.meanNewtons))/Math.Max(.001,result.meanNewtons));}
            result.hasStability=quietCount>1;
            result.closureStability=result.hasStability?(float)Math.Sqrt(Math.Max(0,quietSquares/quietCount-(quietSum/quietCount)*(quietSum/quietCount))):0;
            result.motionJitter=result.hasStability?(float)(quietJitter/quietCount):0;
            result.baselineProtocol = "3x3s grip / 5s rest; 10s ROM; 10s sustained 30% reference; 10s quiet stability. Demonstration only, not a prescription.";
            return result;
        }
    }
}
