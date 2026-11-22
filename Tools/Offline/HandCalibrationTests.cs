using System;
using GripRehab;
using UnityEngine;

// Executes the production hand calibration with a fabricated (SYNTHETIC) patient: ranges, mapping, depth gains, saving.
class HandCalibrationTests
{
    static int passed;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    static bool Near(float a, float b, float tol) => Math.Abs(a - b) <= tol;

    static HandCalibration Patient(float leftX, float rightX, float upY, float downY, float near, float far, int seed)
    {
        var rng = new System.Random(seed); var c = new HandCalibration();
        float N(float s) => (float)(rng.NextDouble() - .5) * 2 * s;
        void Phase(int p, float x, float y, float d) { for (int i = 0; i < 60; i++) c.Add(p, x + N(.006f), y + N(.006f), d + N(.004f)); c.Add(p, .99f, .01f, 3f); } // one wild outlier each
        Phase(HandCalibration.Rest, .5f, .7f, 0f);
        Phase(HandCalibration.Left, leftX, .7f, 0f); Phase(HandCalibration.Right, rightX, .7f, 0f);
        Phase(HandCalibration.Up, .5f, upY, 0f); Phase(HandCalibration.Down, .5f, downY, 0f);
        Phase(HandCalibration.Near, .5f, .7f, near); Phase(HandCalibration.Far, .5f, .7f, far);
        return c;
    }

    static int Main()
    {
        try
        {
            var r = Patient(.25f, .78f, .55f, .9f, .25f, -.15f, 1).Solve();
            Check(r.lateralValid && r.depthValid, "a patient who moves in every direction calibrates both mapping and depth");
            HandCalibration.Map(r, r.x0, r.y0, 1, out float u, out float v);
            Check(Near(u, 0, .001f) && Near(v, 0, .001f), "the resting hand maps to the view centre");
            HandCalibration.Map(r, r.xMin, r.y0, 1, out u, out _); Check(Near(u, -1, .001f), "their far left reaches the left edge: " + u);
            HandCalibration.Map(r, r.xMax, r.y0, 1, out u, out _); Check(Near(u, 1, .001f), "their far right reaches the right edge: " + u);
            HandCalibration.Map(r, r.x0, r.yTop, 1, out _, out v); Check(Near(v, 1, .001f), "their up reaches the top edge: " + v);
            HandCalibration.Map(r, r.x0, r.yBottom, 1, out _, out v); Check(Near(v, -1, .001f), "their down reaches the bottom edge: " + v);
            Check(Near(r.xMin, .25f, .015f) && Near(r.xMax, .78f, .015f) && Near(r.yTop, .55f, .015f) && Near(r.yBottom, .9f, .015f), "outliers do not stretch the ranges");
            HandCalibration.Map(r, r.xMax, r.y0, .7f, out u, out _); Check(Near(u, 1 / .7f, .01f), "the assist's range scale shrinks the reach needed");

            HandCalibration.DepthSettings(r, .55f, .15f, out float dead, out float fwd, out float back);
            float atNear = (r.dNear - r.d0 - dead) * fwd, atFar = (r.d0 - r.dFar - dead) * back;
            Check(Near(atNear, .55f, .001f) && Near(atFar, .15f, .001f), "their near reaches full forward reach and their far full back: " + atNear + " / " + atFar);
            Check(dead >= .02f && dead <= .06f, "the dead zone comes from the rest jitter, bounded: " + dead);

            var small = Patient(.47f, .53f, .68f, .72f, .03f, -.02f, 2).Solve();
            Check(!small.lateralValid && !small.depthValid, "too little movement keeps the defaults (nothing calibrated)");
            Check(!new HandCalibration().Solve().Any, "no samples: no calibration");

            Check(HandCalibration.TryLoad(HandCalibration.Save(r), out var back2) && Near(back2.xMax, r.xMax, 1e-5f) && back2.depthValid && back2.lateralValid, "saved and loaded unchanged");
            Check(!HandCalibration.TryLoad("v1;garbage", out _) && !HandCalibration.TryLoad(null, out _) && !HandCalibration.TryLoad("v1;1;2;3;4;5;6;7;8;9;10;11;0;0", out _), "damaged or empty saves are refused");

            // HandDepth with the calibrated rest size: a hand that first appears already near reads as forward at once,
            // instead of becoming the new rest (the old behaviour without a calibration).
            var image = new Vector3[21];
            void Hand(float scale) { for (int i = 0; i < 21; i++) { int f = i == 0 ? -1 : (i - 1) / 4, j = i == 0 ? 0 : (i - 1) % 4; float x = f < 0 ? 0 : (f - 2) * .02f, y = f < 0 ? 0 : -.09f - j * .025f; image[i] = new Vector3(.5f + x * scale, .6f + y * scale, 0); } }
            var rest = new HandDepth(); Hand(1); for (int i = 0; i < 30; i++) rest.Step(image, null, 4f / 3f, 1 / 30f);
            float restLevel = rest.RawLevel;
            var calibrated = new HandDepth { fixedBaseline = restLevel };
            Hand(1.3f); for (int i = 0; i < 30; i++) calibrated.Step(image, null, 4f / 3f, 1 / 30f);
            var plain = new HandDepth(); for (int i = 0; i < 30; i++) plain.Step(image, null, 4f / 3f, 1 / 30f);
            Check(calibrated.Offset > .1f && Math.Abs(plain.Offset) < .01f, "with a calibrated rest a hand that appears near is forward at once: " + calibrated.Offset + " (uncalibrated " + plain.Offset + ")");
            Console.WriteLine("ALL " + passed + " HAND CALIBRATION TESTS PASSED");
            return 0;
        }
        catch (Exception e) { Console.WriteLine("FAIL " + e.Message); return 1; }
    }
}
