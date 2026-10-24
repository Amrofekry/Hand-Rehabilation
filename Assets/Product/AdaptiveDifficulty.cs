using System;

namespace GripRehab.Product
{
    /// <summary>
    /// Stage 5.2: carries the research policy's in-session level onto the shared 1..10 exercise
    /// scale. One step per policy decision, never more than <see cref="MaxOffset"/> levels away from
    /// where the session started, and always inside the exercise scale. Game settings only.
    /// </summary>
    public sealed class AdaptiveDifficulty
    {
        public const int MaxOffset = 3;
        public int StartLevel { get; }
        public int PolicyStart { get; }
        public int Level { get; private set; }
        public int Changes { get; private set; }

        public AdaptiveDifficulty(int startLevel, int policyStart)
        {
            StartLevel = ExerciseTuning.Clamp(startLevel); PolicyStart = policyStart; Level = StartLevel;
        }

        /// <returns>True when the exercise level moved (by exactly one step).</returns>
        public bool Apply(int policyLevel)
        {
            int offset = Math.Max(-MaxOffset, Math.Min(MaxOffset, policyLevel - PolicyStart));
            int target = ExerciseTuning.Clamp(StartLevel + offset);
            int next = Level + Math.Sign(target - Level);
            if (next == Level) return false;
            Level = next; Changes++;
            return true;
        }
    }

    /// <summary>Stage 5.4: the public-EMG proxy model only ever reads synthetic Newton input.</summary>
    public static class ProxyModelGuard
    {
        public static bool Applies(bool proxyDemoEnabled, TrialSample input) =>
            proxyDemoEnabled && input.synthetic && input.unit == InputUnit.Newtons;
    }

    /// <summary>Stage 5.1: whether a saved baseline was taken with the input chosen on the Prepare page.</summary>
    public static class BaselineMatch
    {
        /// <param name="inputMode">0 camera, 1 keyboard demo, 2 Phase 2 device (Prepare page order).</param>
        public static bool ForInputMode(CapabilityProfile baseline, int inputMode)
        {
            if (baseline == null || string.IsNullOrEmpty(baseline.source)) return false;
            switch (inputMode)
            {
                case 0: return baseline.unit == InputUnit.ClosureRatio && baseline.source.StartsWith("Camera", StringComparison.Ordinal);
                case 1: return baseline.source == "Keyboard demo";
                case 2: return baseline.source == "Phase 2" || baseline.source == "Synthetic replay";
                default: return false;
            }
        }
    }
}
