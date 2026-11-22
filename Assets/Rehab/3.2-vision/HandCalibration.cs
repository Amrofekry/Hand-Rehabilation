using System;
using System.Collections.Generic;
using System.Globalization;

namespace GripRehab
{
    /// <summary>
    /// Personal camera-hand calibration (2026-11-22, the user's choice: once, saved; CALIBRATE HAND redoes it). A guided
    /// 30 s run records the wrist (image x, image y down) and the hand's apparent size (HandDepth.RawLevel, log size) while
    /// the patient rests in the middle, reaches far left and right, up and down, near the webcam and far from it. The
    /// result maps the patient's own comfortable range to the full view (left/right, up/down) and sets the depth gains so
    /// their "near" reaches full forward reach and their "far" full back. Unity-free (offline tested), robust to outliers
    /// (10th/90th percentiles, medians), nothing personal stored: eleven numbers.
    /// </summary>
    public sealed class HandCalibration
    {
        public const int Rest = 0, Left = 1, Right = 2, Up = 3, Down = 4, Near = 5, Far = 6, Phases = 7;
        public static readonly string[] Prompts =
        {
            "Rest your hand in the middle, in front of the camera, fingers up",
            "Move your hand FAR LEFT and hold it there",
            "Move your hand FAR RIGHT and hold it there",
            "Move your hand UP and hold it there",
            "Move your hand DOWN and hold it there",
            "Bring your hand NEAR the camera and hold it there",
            "Move your hand FAR from the camera (toward you) and hold it there",
        };

        public struct Result
        {
            public float x0, y0, xMin, xMax, yTop, yBottom, d0, dNear, dFar, jitterXY, jitterD;
            public bool lateralValid, depthValid;
            public bool Any => lateralValid || depthValid;
        }

        readonly List<float>[] xs = new List<float>[Phases], ys = new List<float>[Phases], ds = new List<float>[Phases];
        public HandCalibration() { for (int i = 0; i < Phases; i++) { xs[i] = new List<float>(); ys[i] = new List<float>(); ds[i] = new List<float>(); } }
        public int Count(int phase) => xs[phase].Count;

        /// <summary>One frame of a phase: wrist x (0..1, mirrored image), wrist y (0..1, down), log apparent size (NaN if none).</summary>
        public void Add(int phase, float x, float yDown, float depthLevel)
        {
            if (phase < 0 || phase >= Phases || !Finite(x) || !Finite(yDown)) return;
            xs[phase].Add(x); ys[phase].Add(yDown); if (Finite(depthLevel)) ds[phase].Add(depthLevel);
        }

        /// <summary>Ranges from the recorded phases. Each half of an axis must span a usable distance or that axis stays on the defaults.</summary>
        public Result Solve()
        {
            var r = new Result();
            if (xs[Rest].Count < 10) return r;
            r.x0 = Median(xs[Rest]); r.y0 = Median(ys[Rest]);
            r.jitterXY = (float)Math.Sqrt(Variance(xs[Rest]) + Variance(ys[Rest]));
            if (xs[Left].Count >= 10 && xs[Right].Count >= 10 && ys[Up].Count >= 10 && ys[Down].Count >= 10)
            {
                r.xMin = Percentile(xs[Left], .1f); r.xMax = Percentile(xs[Right], .9f);
                r.yTop = Percentile(ys[Up], .1f); r.yBottom = Percentile(ys[Down], .9f);
                r.lateralValid = r.x0 - r.xMin >= .06f && r.xMax - r.x0 >= .06f && r.y0 - r.yTop >= .04f && r.yBottom - r.y0 >= .04f;
            }
            if (ds[Rest].Count >= 10 && ds[Near].Count >= 10 && ds[Far].Count >= 10)
            {
                r.d0 = Median(ds[Rest]); r.dNear = Percentile(ds[Near], .9f); r.dFar = Percentile(ds[Far], .1f);
                r.jitterD = (float)Math.Sqrt(Variance(ds[Rest]));
                r.depthValid = r.dNear - r.d0 >= .06f && r.d0 - r.dFar >= .04f;
            }
            return r;
        }

        /// <summary>Screen mapping (u, v: -1..1 at the view edges) of a wrist position; scale shrinks the range (the assist).</summary>
        public static void Map(in Result r, float x, float yDown, float scale, out float u, out float v)
        {
            scale = scale > .1f ? scale : 1f;
            u = x >= r.x0 ? (x - r.x0) / ((r.xMax - r.x0) * scale) : (x - r.x0) / ((r.x0 - r.xMin) * scale);
            v = yDown <= r.y0 ? (r.y0 - yDown) / ((r.y0 - r.yTop) * scale) : -(yDown - r.y0) / ((r.yBottom - r.y0) * scale);
        }

        /// <summary>Depth settings for HandDepth: the dead zone from the rest jitter, gains so near/far reach the limits.</summary>
        public static void DepthSettings(in Result r, float maxForward, float maxBack, out float deadZone, out float forwardGain, out float backGain)
        {
            deadZone = Clamp(3 * r.jitterD, .02f, .06f);
            forwardGain = maxForward / Math.Max(.02f, r.dNear - r.d0 - deadZone);
            backGain = maxBack / Math.Max(.02f, r.d0 - r.dFar - deadZone);
        }

        public static string Save(in Result r)
        {
            var c = CultureInfo.InvariantCulture;
            return string.Join(";", "v1", r.x0.ToString(c), r.y0.ToString(c), r.xMin.ToString(c), r.xMax.ToString(c), r.yTop.ToString(c), r.yBottom.ToString(c),
                r.d0.ToString(c), r.dNear.ToString(c), r.dFar.ToString(c), r.jitterXY.ToString(c), r.jitterD.ToString(c), r.lateralValid ? "1" : "0", r.depthValid ? "1" : "0");
        }

        public static bool TryLoad(string text, out Result r)
        {
            r = new Result();
            if (string.IsNullOrEmpty(text)) return false;
            var p = text.Split(';'); if (p.Length != 14 || p[0] != "v1") return false;
            var c = CultureInfo.InvariantCulture; var v = new float[11];
            for (int i = 0; i < 11; i++) if (!float.TryParse(p[i + 1], NumberStyles.Float, c, out v[i]) || !Finite(v[i])) return false;
            r.x0 = v[0]; r.y0 = v[1]; r.xMin = v[2]; r.xMax = v[3]; r.yTop = v[4]; r.yBottom = v[5]; r.d0 = v[6]; r.dNear = v[7]; r.dFar = v[8]; r.jitterXY = v[9]; r.jitterD = v[10];
            r.lateralValid = p[12] == "1" && r.x0 - r.xMin >= .06f && r.xMax - r.x0 >= .06f && r.y0 - r.yTop >= .04f && r.yBottom - r.y0 >= .04f;
            r.depthValid = p[13] == "1" && r.dNear - r.d0 >= .06f && r.d0 - r.dFar >= .04f;
            return r.Any;
        }

        static float Median(List<float> v) => Percentile(v, .5f);
        static float Percentile(List<float> v, float q)
        {
            var a = v.ToArray(); Array.Sort(a);
            float pos = q * (a.Length - 1); int i = (int)Math.Floor(pos); int j = Math.Min(a.Length - 1, i + 1);
            return a[i] + (a[j] - a[i]) * (pos - i);
        }
        static double Variance(List<float> v)
        {
            if (v.Count < 2) return 0; double m = 0; foreach (var x in v) m += x; m /= v.Count;
            double s = 0; foreach (var x in v) s += (x - m) * (x - m); return s / (v.Count - 1);
        }
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
    }
}
