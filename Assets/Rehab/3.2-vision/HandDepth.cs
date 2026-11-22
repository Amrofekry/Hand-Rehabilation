using System;
using UnityEngine;

namespace GripRehab
{
    /// <summary>
    /// Camera-hand depth from one camera (2026-11-21): the hand nearer the camera moves the virtual hand forward, farther
    /// moves it back. Unity-free (offline tested), no allocation.
    ///
    /// The cue is how big the hand looks: two measures averaged in log space, (1) palm width and length in the image
    /// (aspect corrected) and (2) image bone lengths over the same bones' 3D length across the image plane (MediaPipe world
    /// landmarks; a tilted hand shortens both, so it follows distance, not tilt), median over palm and finger-base bones.
    /// It is smoothed (0.12 s) and held while the hand travels across the image: a deliberate toward/away move changes size
    /// with little travel, a sweep is mostly travel. On two real Hand Lab recordings this cut frame-to-frame depth jitter
    /// about 3x (0.006-0.008 vs 0.012-0.022 in log size; Tools/hand_depth_eval.py, MEASURED landmarks replayed offline).
    /// The baseline (the resting size) is set when the hand appears and then follows very slowly (2 min).
    /// Output: Offset (m), forward positive; Level: log size relative to the baseline.
    /// </summary>
    public sealed class HandDepth
    {
        static readonly int[] BoneA = { 0, 0, 0, 0, 5, 9, 13, 5, 5, 9, 13, 17 };
        static readonly int[] BoneB = { 5, 9, 13, 17, 9, 13, 17, 17, 6, 10, 14, 18 };
        readonly float[] ratios = new float[12];

        public float holdFrom = .15f, holdFull = .45f;  // image heights per second: depth follows less, then holds
        public float smoothSeconds = .12f;
        public float baselineSeconds = 120f;
        public float acquireSeconds = .5f;             // the baseline settles over the first half second of a new hand
        public float deadZone = .03f;                  // log size (about 3 %)
        public float forwardGain = 1.6f, backGain = .5f; // metres per unit of log size beyond the dead zone
        public float maxForward = .55f, maxBack = .15f;
        /// <summary>Calibrated resting size (log; HandCalibration, 2026-11-22). Set: a new hand starts from it (not from the size it
        /// happens to have when it appears) and it still follows slowly (baselineSeconds). NaN: the first size is the rest.</summary>
        public float fixedBaseline = float.NaN;

        float level, baseline, speed, lastX, lastY, seenFor;
        bool primed;

        public float Level => primed ? level - baseline : 0;
        /// <summary>The smoothed log size itself (NaN before a hand), what the calibration records.</summary>
        public float RawLevel => primed ? level : float.NaN;
        public float Offset { get; private set; }
        public bool Valid => primed;

        public void Reset() { primed = false; Offset = 0; speed = 0; seenFor = 0; }

        /// <summary>Raw cue for one frame: log of the hand's apparent size (NaN if the frame cannot give one).</summary>
        public float Cue(Vector3[] image, Vector3[] world, float aspect)
        {
            if (image == null || image.Length != 21 || !(aspect > 0)) return float.NaN;
            float palm = .5f * (Image(image, 5, 17, aspect) + Image(image, 0, 9, aspect));
            if (!(palm > 1e-4f)) return float.NaN;
            float cue = (float)Math.Log(palm);
            if (world != null && world.Length == 21)
            {
                int n = 0;
                for (int i = 0; i < BoneA.Length; i++)
                {
                    float plane = (float)Math.Sqrt(Sq(world[BoneA[i]].x - world[BoneB[i]].x) + Sq(world[BoneA[i]].y - world[BoneB[i]].y));
                    if (plane > .01f) ratios[n++] = Image(image, BoneA[i], BoneB[i], aspect) / plane;
                }
                if (n >= 4)
                {
                    Array.Sort(ratios, 0, n);
                    float median = (n & 1) == 1 ? ratios[n / 2] : .5f * (ratios[n / 2 - 1] + ratios[n / 2]);
                    if (median > 0) cue = .5f * cue + .5f * (float)Math.Log(median); // both measures averaged in log space
                }
            }
            return cue;
        }

        /// <summary>One frame. image: 21 image landmarks (0..1); world: 21 world landmarks (m) or null; dt seconds.</summary>
        public void Step(Vector3[] image, Vector3[] world, float aspect, float dt)
        {
            float cue = Cue(image, world, aspect);
            if (float.IsNaN(cue) || float.IsInfinity(cue)) return;
            float cx = 0, cy = 0;
            cx = (image[0].x + image[5].x + image[9].x + image[13].x + image[17].x) * .2f * aspect;
            cy = (image[0].y + image[5].y + image[9].y + image[13].y + image[17].y) * .2f;
            if (!primed || !(dt > 0) || dt > .5f)
            {
                level = cue; baseline = Fixed ? fixedBaseline : cue; lastX = cx; lastY = cy; speed = 0; seenFor = 0; primed = true; Offset = 0; return;
            }
            float v = (float)Math.Sqrt(Sq(cx - lastX) + Sq(cy - lastY)) / dt;
            lastX = cx; lastY = cy;
            speed += (v - speed) * (1 - (float)Math.Exp(-dt / .15f));
            float hold = Clamp01((speed - holdFrom) / Math.Max(1e-3f, holdFull - holdFrom));
            level += (cue - level) * (1 - (float)Math.Exp(-dt / smoothSeconds)) * (1 - hold);
            seenFor += dt;
            float bt = seenFor < acquireSeconds && !Fixed ? .15f : baselineSeconds;
            baseline += (level - baseline) * (1 - (float)Math.Exp(-dt / bt));
            float x = level - baseline, beyond = Math.Sign(x) * Math.Max(0, Math.Abs(x) - deadZone);
            Offset = beyond >= 0 ? Math.Min(maxForward, beyond * forwardGain) : Math.Max(-maxBack, beyond * backGain);
        }

        bool Fixed => !float.IsNaN(fixedBaseline) && !float.IsInfinity(fixedBaseline);
        static float Image(Vector3[] p, int a, int b, float aspect) => (float)Math.Sqrt(Sq((p[a].x - p[b].x) * aspect) + Sq(p[a].y - p[b].y));
        static float Sq(float v) => v * v;
        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
