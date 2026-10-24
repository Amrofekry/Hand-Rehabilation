using System;

namespace GripRehab.Product
{
    /// <summary>Force-round settings as fractions of the player's own reference effort.</summary>
    public struct ForceTuning
    {
        public int rounds, pulses;
        public float bandLow, bandHigh, breakAt, holdSeconds;
    }

    /// <summary>
    /// One 1..10 difficulty scale for every exercise (Stage 3.0). Level 1 reproduces the
    /// legacy exercises exactly so existing checks and saved checkpoints keep their meaning.
    /// These are versioned game settings, not clinical prescriptions.
    /// </summary>
    public static class ExerciseTuning
    {
        public const int MinLevel = 1, MaxLevel = 10;
        public const string Version = "exercise-tuning-1";

        public static int Clamp(int level) => Math.Max(MinLevel, Math.Min(MaxLevel, level));
        static float T(int level) => (Clamp(level) - 1) / (float)(MaxLevel - 1);
        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        public static ForceTuning Force(int level)
        {
            float t = T(level), centre = Lerp(.40f, .65f, t), half = Lerp(.15f, .07f, t);
            return new ForceTuning
            {
                rounds = 3 + (Clamp(level) - 1) / 3,
                bandLow = centre - half, bandHigh = centre + half,
                breakAt = Math.Min(1f, centre + half + Lerp(.25f, .12f, t)),
                holdSeconds = Lerp(2.5f, 6f, t),
                pulses = 3 + (int)Math.Round(Lerp(0, 3, t))
            };
        }

        public static int MemoryLength(int level) => 4 + (int)Math.Round(T(level) * 4);
        public static int AssemblyPieces(int level) => 4 + (int)Math.Round(T(level) * 2);
        public static float TraceTolerance(int level) => Lerp(.12f, .07f, T(level));
        /// <summary>0 = the legacy hand-authored 7x5 maze; otherwise an odd generated size.</summary>
        public static int MazeCells(int level) => Clamp(level) == 1 ? 0 : 5 + 2 * (int)Math.Round(T(level) * 4);

        /// <summary>Between sessions: up after a strong result, down after a weak one.</summary>
        public static int NextLevel(int current, int lastScore) =>
            Clamp(lastScore >= 85 ? current + 1 : lastScore < 50 ? current - 1 : current);
    }
}
