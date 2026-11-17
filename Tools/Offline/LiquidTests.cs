using System;
using GripRehab;

// Executes the production liquid core (volume, Torricelli outflow, sloshing, rim spill, tremor estimate, tea rules); no Unity.
class LiquidTests
{
    static int passed;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    static bool Near(double a, double b, double tol) => Math.Abs(a - b) <= tol;
    static int Main()
    {
        try
        {
            // Cup: 3.0 cm radius at the bottom, 3.8 cm at the rim, 8.5 cm deep (about 310 ml).
            var cup = new VesselLiquid(.030f, .038f, .085f);
            double cap = LiquidMath.Ml(cup.Capacity);
            double exact = Math.PI * .085 / 3 * (.03 * .03 + .03 * .038 + .038 * .038) * 1e6;
            Check(Near(cap, exact, .01), "frustum capacity is the exact formula (" + cap.ToString("0.0") + " ml)");
            float last = -1; bool rising = true;
            for (int ml = 0; ml <= 300; ml += 10) { cup.Reset(LiquidMath.M3(ml)); if (cup.Level < last) rising = false; last = cup.Level; }
            Check(rising, "the level rises with every millilitre added");
            cup.Reset(LiquidMath.M3(150));
            Check(Near(LiquidMath.FrustumVolume(.03f, .038f, .085f, cup.Level), LiquidMath.M3(150), 1e-9), "level from volume inverts exactly");

            // Volume is conserved: everything poured is in the cup, over the rim, or left in the pot.
            var pot = new SpoutVessel(.06f, .10f, .075f, .095f, .004f); pot.Fill(LiquidMath.M3(450));
            cup.Reset(); float poured = 0, overRim = 0, outside = 0; var rng = new Random(7);
            for (int i = 0; i < 900; i++)
            {
                float dt = 1 / 60f, pitch = (float)(Math.PI / 180 * (i < 600 ? 55 : 0));
                float o = pot.Pour(pitch, dt); poured += o;
                if (i % 7 == 0) outside += o; else overRim += cup.Add(o);          // a few drops miss the cup
                float ax = (float)(rng.NextDouble() - .5) * 2, az = (float)(rng.NextDouble() - .5) * 2;
                overRim += cup.Step(dt, 0, 0, ax, az);
            }
            double balance = LiquidMath.Ml(LiquidMath.M3(450) - pot.Volume) - LiquidMath.Ml(cup.Volume + overRim + outside);
            Check(Near(balance, 0, .01), "volume conserved: pot = cup + over rim + outside (error " + balance.ToString("0.000") + " ml)");

            // Torricelli: no flow until the surface reaches the spout; flow grows with tilt and stops when empty.
            pot.Fill(LiquidMath.M3(450));
            Check(pot.FlowRate(0) == 0, "upright pot does not pour");
            float slow = pot.FlowRate((float)(Math.PI / 180 * 40)), fast = pot.FlowRate((float)(Math.PI / 180 * 60));
            Check(fast > slow, "more tilt pours faster (" + LiquidMath.Ml(slow).ToString("0") + " -> " + LiquidMath.Ml(fast).ToString("0") + " ml/s)");
            Check(LiquidMath.Ml(fast) > 5 && LiquidMath.Ml(fast) < 120, "pour rate is teapot-like (" + LiquidMath.Ml(fast).ToString("0") + " ml/s)");
            float head = pot.Head((float)(Math.PI / 180 * 60));
            Check(Near(pot.FlowRate((float)(Math.PI / 180 * 60)), .62 * pot.spoutArea * Math.Sqrt(2 * 9.81 * head), 1e-9), "flow is Cd A sqrt(2 g h)");
            pot.Fill(LiquidMath.M3(40));
            float lowTilt = 0; for (int d = 0; d <= 90; d++) if (pot.FlowRate((float)(Math.PI / 180 * d)) > 0) { lowTilt = d; break; }
            pot.Fill(LiquidMath.M3(400));
            float fullTilt = 0; for (int d = 0; d <= 90; d++) if (pot.FlowRate((float)(Math.PI / 180 * d)) > 0) { fullTilt = d; break; }
            Check(lowTilt > fullTilt, "a nearly empty pot must be tipped further (" + fullTilt + " deg full, " + lowTilt + " deg low)");
            pot.Fill(0); Check(pot.FlowRate(1) == 0 && pot.Pour(1, .1f) == 0, "an empty pot pours nothing");

            // Slosh: the free wave rings at the first-mode frequency and dies away; a still cup stays still.
            cup.Reset(LiquidMath.M3(200)); cup.Kick(.6f, 0);
            float w = LiquidMath.SloshOmega(cup.MeanRadius, cup.Level); double expectHz = w / (2 * Math.PI);
            int crossings = 0; float prev = 0; double t = 0, firstAmp = 0, lateAmp = 0, firstUp = -1, lastUp = 0;
            for (int i = 0; i < 6000; i++)
            {
                cup.Step(.001f, 0, 0, 0, 0); t += .001;
                if (prev < 0 && cup.SloshX >= 0) { crossings++; if (firstUp < 0) firstUp = t; lastUp = t; }
                prev = cup.SloshX;
                if (t < .5) firstAmp = Math.Max(firstAmp, Math.Abs(cup.SloshX)); if (t > 5.5) lateAmp = Math.Max(lateAmp, Math.Abs(cup.SloshX));
            }
            double hz = (crossings - 1) / (lastUp - firstUp);
            Check(Near(hz, expectHz * Math.Sqrt(1 - .045 * .045), .15), "free slosh at the first-mode frequency (" + hz.ToString("0.00") + " Hz, formula " + expectHz.ToString("0.00") + " Hz)");
            Check(expectHz > 2.5 && expectHz < 5, "a tea cup sloshes at 2.5-5 Hz");
            Check(lateAmp < firstAmp * .5, "the wave dies away");
            cup.Reset(LiquidMath.M3(200)); for (int i = 0; i < 600; i++) cup.Step(1 / 60f, 0, 0, 0, 0);
            Check(cup.SloshX == 0 && cup.SloshZ == 0, "a still cup stays still");
            // A steady push tilts the surface to a/g, like a real cup carried at constant acceleration.
            for (int i = 0; i < 3000; i++) cup.Step(.002f, 0, 0, 1.962f, 0);
            Check(Near(cup.SloshX, -.2, .01), "steady acceleration tilts the surface to a/g (" + cup.SloshX.ToString("0.000") + ")");
            // Shaking near the slosh frequency builds a bigger wave than shaking far from it.
            float Shake(double f)
            {
                var c = new VesselLiquid(.030f, .038f, .085f); c.Reset(LiquidMath.M3(150)); float peak = 0;
                for (int i = 0; i < 4000; i++) { double tt = i * .001, a = Math.Pow(2 * Math.PI * f, 2) * .001 * Math.Sin(2 * Math.PI * f * tt); c.Step(.001f, 0, 0, (float)a, 0); peak = Math.Max(peak, Math.Abs(c.SloshX)); }
                return peak;
            }
            Check(Shake(expectHz) > 2 * Shake(1.0), "a 1 mm shake near the slosh frequency makes a bigger wave (resonance)");

            // Rim spill: a full cup tipped past its rim angle loses tea; an upright half cup does not.
            cup.Reset(LiquidMath.M3(280)); float spilled = 0;
            for (int i = 0; i < 120; i++) spilled += cup.Step(1 / 60f, .5f, 0, 0, 0);
            Check(LiquidMath.Ml(spilled) > 20, "a full cup tipped 27 deg spills over the rim (" + LiquidMath.Ml(spilled).ToString("0") + " ml)");
            cup.Reset(LiquidMath.M3(120)); spilled = 0;
            for (int i = 0; i < 120; i++) spilled += cup.Step(1 / 60f, .05f, 0, 0, 0);
            Check(spilled == 0, "a half cup held nearly upright keeps its tea");
            Check(LiquidMath.RetainedVolume(.034f, .085f, 0) > LiquidMath.RetainedVolume(.034f, .085f, 1) && LiquidMath.RetainedVolume(.034f, .085f, 1000) < 1e-6, "the more it tilts the less it holds");
            float s1 = 1.25f - 1e-4f, s2 = 1.25f + 1e-4f;
            Check(Near(LiquidMath.RetainedVolume(.034f, .085f, s1), LiquidMath.RetainedVolume(.034f, .085f, s2), 1e-6), "retained volume is continuous where the surface meets the base");
            // Drinking empties it.
            cup.Reset(LiquidMath.M3(200)); float drank = 0; while (cup.Volume > 0) drank += cup.Remove(LiquidMath.M3(3));
            Check(Near(LiquidMath.Ml(drank), 200, .01) && cup.Volume == 0, "drinking takes all 200 ml and no more");

            // Tremor estimate: a 6 Hz, 2 mm-RMS tremor on a slow reach, sampled by a 30 fps camera with 0.3 mm noise.
            var est = new TremorEstimator(); var noise = new Random(3); double amp = 2 * Math.Sqrt(2);
            for (int i = 0; i < 150; i++)
            {
                double tt = i / 30.0 + noise.NextDouble() * .004;
                float px = (float)(40 * tt + amp * Math.Sin(2 * Math.PI * 6 * tt) + (noise.NextDouble() - .5) * .6);
                float py = (float)(-10 * tt + (noise.NextDouble() - .5) * .6);
                est.Add(tt, px, py);
            }
            Check(est.Measure(out float rms, out float trem), "a 5 s window gives a measurement");
            Check(Near(trem, 6, .3), "dominant tremor frequency 6 Hz (" + trem.ToString("0.00") + " Hz)");
            Check(Near(rms, 2, .2), "tremor amplitude 2 mm RMS within 10 % (" + rms.ToString("0.00") + " mm)");
            est.Clear();
            for (int i = 0; i < 150; i++) { double tt = i / 30.0; est.Add(tt, (float)(30 * tt), (float)(5 * tt)); }
            est.Measure(out float still, out _);
            Check(still < .05, "a smooth reach with no tremor reads near 0 mm (" + still.ToString("0.000") + ")");
            est.Clear(); est.Add(0, 0, 0); est.Add(.03, float.NaN, 1); est.Add(.03, 1, 1); est.Add(.02, 1, 1);
            Check(est.Count == 2 && !est.Measure(out _, out _), "NaN and out-of-order samples are ignored; short windows give no value");
            var summary = new TremorSummary(); summary.Add(2, 6); summary.Add(1, 9);
            Check(Near(summary.MeanRmsMm, 1.5, 1e-6) && Near(summary.DominantHz, 7, 1e-6), "summary: mean amplitude and amplitude-weighted frequency");

            // Rules.
            Check(TeaRules.Tolerance(1) > TeaRules.Tolerance(10), "higher levels need a closer fill");
            Check(TeaRules.FillAccuracy(150, 150, 5) == 1 && TeaRules.FillAccuracy(150 * (1 + TeaRules.Tolerance(5)), 150, 5) < 1e-5, "fill accuracy: 1 on the line, 0 at the edge");
            Check(TeaRules.FillAccepted(160, 150, 1) && !TeaRules.FillAccepted(160, 150, 10), "10 ml over passes at level 1, not at level 10");
            Check(TeaRules.PourAccuracy(90, 100) == .9f && TeaRules.PourAccuracy(0, 0) == 1, "pour accuracy is the share that reached the cup");
            var stats = new RunningStats(); foreach (var v in new[] { 10f, 12, 14 }) stats.Add(v);
            Check(Near(stats.Mean, 12, 1e-6) && Near(stats.Sd, 2, 1e-6) && Near(stats.Cv, 2 / 12.0, 1e-6), "running mean, SD and CV");
            Console.WriteLine("LIQUID TESTS PASSED: " + passed);
            return 0;
        }
        catch (Exception error) { Console.WriteLine("FAIL " + error.Message); return 1; }
    }
}
