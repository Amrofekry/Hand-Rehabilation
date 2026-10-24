using System;
namespace GripRehab.Product
{
    [Serializable] public sealed class FatigueSafetyConfig
    {
        public int consecutive = 3, clearWindows = 3, maximumLevel = 5;
        public float high = .7f, low = .4f, stableSeconds = 8, cooldown = 10;
        public string version = "research-ai-1";
    }
    public struct FatigueSafetyDecision
    { public string state, reason; public int level, signs; }
    /// <summary>One sign per independent modality; duplicate force features never count twice.</summary>
    public sealed class FatigueSafetyPolicy
    {
        readonly FatigueSafetyConfig config;
        readonly int[] counts = new int[4];
        double stable = double.NaN, changed = -1e30, last = -1e30;
        int clear; bool valid;
        public string State { get; private set; } = "active";
        public int Level { get; private set; } = 1;
        public int Signs { get; private set; }
        public FatigueSafetyPolicy(FatigueSafetyConfig settings = null)
        {
            config = settings ?? new FatigueSafetyConfig();
            if (config.consecutive < 1 || config.clearWindows < 1 || config.maximumLevel < 1 ||
                !TrialSample.Finite(config.high) || !TrialSample.Finite(config.low) || config.low < 0 || config.high > 1 || config.low >= config.high ||
                !TrialSample.Finite(config.stableSeconds) || config.stableSeconds <= 0 || !TrialSample.Finite(config.cooldown) || config.cooldown <= 0)
                throw new ArgumentException("Invalid fatigue safety configuration.");
        }
        public FatigueSafetyDecision Step(double now, bool inputValid, float? force, float? motion, float? face, float? physiology, bool potentialStable)
        {
            if (!TrialSample.Finite(now) || now <= last) throw new ArgumentException("Clock must increase.");
            last = now; if (State == "stopped") return Result("stop latched");
            valid = inputValid && Probability(force) && Probability(motion) && Probability(face) && Probability(physiology);
            if (!valid)
            {
                Array.Clear(counts, 0, 4); clear = Signs = 0; stable = double.NaN;
                // Reduce once on entry; repeated missing frames must not repeatedly change difficulty.
                if (State != "warning") Level = Math.Max(1, Level - 1);
                State = "warning"; return Result("invalid input or model; explicit resume required");
            }
            Count(0, force); Count(1, motion); Count(2, face); Count(3, physiology);
            Signs = 0; int n = 0;
            for (int i = 0; i < 4; i++) if (counts[i] >= config.consecutive) { Signs |= 1 << i; n++; }
            bool settled = Clear(force) && Clear(motion) && Clear(face) && Clear(physiology);
            clear = settled ? clear + 1 : 0;
            if (n > 1) { State = "stopped"; stable = double.NaN; return Result("multiple independent signs"); }
            if (n == 1)
            {
                if (State != "warning") { Level = Math.Max(1, Level - 1); changed = now; }
                State = "warning"; stable = double.NaN; return Result("one persistent sign");
            }
            if (State == "warning") return Result("explicit resume required");
            if (!potentialStable || !settled) { stable = double.NaN; return Result("difficulty held"); }
            if (double.IsNaN(stable)) stable = now;
            if (now - stable >= config.stableSeconds && now - changed >= config.cooldown)
            { Level = Math.Min(config.maximumLevel, Level + 1); stable = changed = now; }
            return Result("active");
        }
        static bool Probability(float? p) => !p.HasValue || (TrialSample.Finite(p.Value) && p >= 0 && p <= 1);
        bool Clear(float? p) => !p.HasValue || p <= config.low;
        void Count(int i, float? p)
        { if (!p.HasValue || p <= config.low) counts[i] = 0; else if (p >= config.high) counts[i] = Math.Min(config.consecutive, counts[i] + 1); else if(counts[i]<config.consecutive) counts[i]=0; }
        /// <summary>True when an explicit Resume would be accepted right now.</summary>
        public bool CanResume => State == "warning" && valid && Signs == 0 && clear >= config.clearWindows;
        /// <summary>The player ends the session from a warning; the stop latches like a safety stop.</summary>
        public FatigueSafetyDecision Quit() { State = "stopped"; stable = double.NaN; return Result("finished by the player"); }
        public FatigueSafetyDecision Resume()
        {
            if (State != "warning" || !valid || Signs != 0 || clear < config.clearWindows) throw new InvalidOperationException("Wait for fresh, cleared windows before resuming.");
            State = "active"; changed = last; stable = double.NaN; return Result("resumed");
        }
        FatigueSafetyDecision Result(string reason) => new FatigueSafetyDecision { state = State, reason = reason, level = Level, signs = Signs };
    }
}
