using System;

namespace GripRehab
{
    public enum DeviceLinkState { Waiting, Live, Stale, Reconnected }

    /// <summary>
    /// Stage 18 device status (2026-11-01): packet rate, loss, sample age and reconnects of the force bridge, read from the
    /// values LiveForceState already exposes (sample count, sequence, age, generation). Unity-free so it is tested
    /// offline with injected dropouts and stale packets. Battery is not in the bridge protocol (schema 1), so it is
    /// reported as unavailable instead of being guessed.
    /// </summary>
    public sealed class DeviceHealth
    {
        public double windowSeconds = 2, staleAfterSeconds = .5, reconnectedShowSeconds = 3;
        public DeviceLinkState State { get; private set; } = DeviceLinkState.Waiting;
        public float RateHz { get; private set; }
        public float LossPercent { get; private set; }
        public int AgeMs { get; private set; }
        public int Reconnects { get; private set; }
        public bool Synthetic { get; private set; }
        public const bool BatteryReported = false;

        int generation = -1, windowCount, lastCount; uint windowSeq, lastSeq; double windowAt = -1, lastSampleAt = -1, reconnectedAt = -10; bool haveSeq;

        public void Reset() { State = DeviceLinkState.Waiting; RateHz = LossPercent = 0; AgeMs = 0; Reconnects = 0; generation = -1; windowAt = lastSampleAt = -1; reconnectedAt = -10; haveSeq = false; }

        /// <summary>One update. sampleCount: total accepted samples; sequence: last accepted sequence; valid: a fresh calibrated sample is available.</summary>
        public void Observe(double now, int sampleCount, uint sequence, int ageMs, bool valid, int linkGeneration, bool synthetic)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return;
            Synthetic = synthetic;
            if (linkGeneration != generation)
            {
                if (generation >= 0) { Reconnects++; reconnectedAt = now; }
                generation = linkGeneration; windowAt = -1; haveSeq = false; RateHz = LossPercent = 0;
            }
            bool fresh = sampleCount != lastCount;
            if (fresh) { lastSampleAt = now; lastCount = sampleCount; lastSeq = sequence; AgeMs = Math.Max(0, ageMs); }
            // A new link's window starts at its first new sample, never at a reset (0) sequence or the old link's count.
            if (windowAt < 0 || !haveSeq) { if (fresh) { windowAt = now; windowCount = sampleCount; windowSeq = sequence; haveSeq = true; } }
            else if (now - windowAt >= windowSeconds)
            {
                int received = sampleCount - windowCount; uint span = lastSeq - windowSeq;
                RateHz = (float)(received / (now - windowAt));
                LossPercent = span > 0 && span < 0x80000000u ? Math.Max(0, Math.Min(100, 100f * ((long)span - received) / span)) : 0;
                windowAt = now; windowCount = sampleCount; windowSeq = lastSeq;
            }
            bool stale = lastSampleAt < 0 || now - lastSampleAt > staleAfterSeconds;
            if (stale) { RateHz = 0; AgeMs = lastSampleAt < 0 ? 0 : (int)Math.Min(int.MaxValue, (now - lastSampleAt) * 1000); }
            State = lastSampleAt < 0 ? DeviceLinkState.Waiting : stale || !valid ? DeviceLinkState.Stale :
                now - reconnectedAt < reconnectedShowSeconds ? DeviceLinkState.Reconnected : DeviceLinkState.Live;
        }
    }
}
