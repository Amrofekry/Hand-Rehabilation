using System;

namespace GripRehab.Product
{
    [Flags] public enum ResearchSigns { None = 0, Force = 1, Motion = 2 }
    public enum GuidanceState { Active, MissingInput, Warning, Stopped }
    [Serializable] public sealed class GuidanceLimits
    {
        public string version = "research-demo-2";
        public int minimumLevel = 1, maximumLevel = 5;
        public double persistence = 1.5, stableSeconds = 8, cooldown = 10;
        public float forceDecline = .35f, jitterRatio = 2, minimumJitter = .025f;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(version) || minimumLevel < 1 || maximumLevel < minimumLevel ||
                !TrialSample.Finite(persistence) || persistence <= 0 || !TrialSample.Finite(stableSeconds) || stableSeconds <= 0 ||
                !TrialSample.Finite(cooldown) || cooldown <= 0 || forceDecline <= 0 || forceDecline >= 1 ||
                !TrialSample.Finite(forceDecline) || !TrialSample.Finite(jitterRatio) || jitterRatio <= 1 ||
                !TrialSample.Finite(minimumJitter) || minimumJitter <= 0) throw new ArgumentException("Invalid research guidance limits.");
        }
    }
    /// <summary>Named statistical fallback; categories, not correlated features, count as signs.</summary>
    public static class StatisticalPredictor
    {
        public static ResearchSigns Evaluate(WindowFeatures features, CapabilityProfile baseline, GuidanceLimits limits)
        {
            if (baseline == null) return ResearchSigns.None;
            var signs = ResearchSigns.None;
            if (features.forceValid && baseline.hasForce && baseline.meanNewtons > 0 &&
                features.forceMean < baseline.meanNewtons * (1 - limits.forceDecline)) signs |= ResearchSigns.Force;
            if (features.motionValid && baseline.hasMotion &&
                features.jitter > Math.Max(limits.minimumJitter, baseline.motionJitter * limits.jitterRatio)) signs |= ResearchSigns.Motion;
            return signs;
        }
    }
    /// <summary>Explicit-resume warning and latched stop. Missing input never increases difficulty.</summary>
    public sealed class GuidancePolicy
    {
        readonly GuidanceLimits limits;
        readonly double[] since = { double.NaN, double.NaN };
        double last = double.NegativeInfinity, stable = double.NaN, changed = double.NegativeInfinity;
        bool valid;
        ResearchSigns current;
        public GuidanceState State { get; private set; }
        public ResearchSigns Confirmed { get; private set; }
        public int Level { get; private set; }
        public GuidancePolicy(GuidanceLimits limits) { this.limits = limits; limits.Validate(); Level = limits.minimumLevel; }
        public void Step(double now, bool inputValid, bool mayIncrease, ResearchSigns signs)
        {
            if (!TrialSample.Finite(now) || now <= last || (signs & ~(ResearchSigns.Force | ResearchSigns.Motion)) != 0) throw new ArgumentException("Invalid guidance observation.");
            last = now; if (State == GuidanceState.Stopped) return;
            valid = inputValid; current = signs;
            if (!valid)
            {
                stable = double.NaN; since[0] = since[1] = double.NaN; Confirmed = ResearchSigns.None;
                if (State != GuidanceState.Warning) State = GuidanceState.MissingInput; return;
            }
            if (State == GuidanceState.MissingInput) State = GuidanceState.Active;
            Confirmed = ResearchSigns.None;
            for (int i = 0; i < 2; i++)
            {
                var sign = (ResearchSigns)(1 << i);
                if ((signs & sign) == 0) { since[i] = double.NaN; continue; }
                if (double.IsNaN(since[i])) since[i] = now;
                if (now - since[i] >= limits.persistence) Confirmed |= sign;
            }
            if (Confirmed == (ResearchSigns.Force | ResearchSigns.Motion)) { Stop(); return; }
            if (Confirmed != ResearchSigns.None)
            {
                if (State != GuidanceState.Warning) { Level = Math.Max(limits.minimumLevel, Level - 1); changed = now; }
                State = GuidanceState.Warning; stable = double.NaN; return;
            }
            if (State == GuidanceState.Warning) return;
            if (!mayIncrease || signs != ResearchSigns.None) { stable = double.NaN; return; }
            if (double.IsNaN(stable)) stable = now;
            if (now - stable >= limits.stableSeconds && now - changed >= limits.cooldown)
            { Level = Math.Min(limits.maximumLevel, Level + 1); changed = stable = now; }
        }
        public void Resume()
        {
            if (State != GuidanceState.Warning || !valid || current != ResearchSigns.None || Confirmed != ResearchSigns.None)
                throw new InvalidOperationException("Wait for valid, cleared inputs before resuming.");
            State = GuidanceState.Active; stable = double.NaN; changed = last;
        }
        public void Stop() { State = GuidanceState.Stopped; stable = double.NaN; }
    }
}
