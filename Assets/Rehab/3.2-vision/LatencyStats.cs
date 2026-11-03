using System;

namespace GripRehab
{
    /// <summary>
    /// Fixed-size latency sample store with median and percentile (2026-11-02). Adding a sample never allocates; the
    /// percentile copies into a reused buffer only when a report is asked for. Unity-free; tested offline.
    /// </summary>
    public sealed class LatencyStats
    {
        readonly float[] samples, sorted;
        int next;
        public int Count { get; private set; }
        public LatencyStats(int capacity = 2048) { samples = new float[Math.Max(1, capacity)]; sorted = new float[samples.Length]; }

        /// <summary>Adds one sample in milliseconds; non-finite or negative values are ignored. The oldest is dropped when full.</summary>
        public void Add(float ms)
        {
            if (float.IsNaN(ms) || float.IsInfinity(ms) || ms < 0) return;
            samples[next] = ms; next = (next + 1) % samples.Length; if (Count < samples.Length) Count++;
        }

        public void Clear() { next = 0; Count = 0; }
        public float Median => Percentile(50);

        /// <summary>Nearest-rank percentile (0-100) of the stored samples; NaN when empty.</summary>
        public float Percentile(float p)
        {
            if (Count == 0) return float.NaN;
            Array.Copy(samples, sorted, Count); Array.Sort(sorted, 0, Count);
            int rank = (int)Math.Ceiling(Math.Max(0, Math.Min(100, p)) / 100.0 * Count);
            return sorted[Math.Max(0, Math.Min(Count - 1, rank - 1))];
        }
    }
}
