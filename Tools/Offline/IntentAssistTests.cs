using System;
using GripRehab;

// Executes the production intent assist with fabricated hand paths only (no camera or Unity). SYNTHETIC.
class IntentAssistTests
{
    static int passed;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }

    // Three objects left / middle / right on the table.
    static void Scene(IntentAssist a, float expectedPrior = 1f, int expected = -1)
    {
        a.BeginFrame();
        a.AddCandidate(10, -.6f, -.2f, expected == 10 ? expectedPrior : 1f);
        a.AddCandidate(11, 0f, -.2f, expected == 11 ? expectedPrior : 1f);
        a.AddCandidate(12, .6f, -.2f, expected == 12 ? expectedPrior : 1f);
    }

    /// <summary>Moves the hand from (u0,v0) to (u1,v1) over seconds at 30 fps with optional tremor; returns the assist.</summary>
    static IntentAssist Move(IntentAssist a, float u0, float v0, float u1, float v1, float seconds, float tremor = 0, int seed = 1, int expected = -1, float expectedPrior = 1f, bool linear = false)
    {
        var rng = new Random(seed);
        int n = Math.Max(1, (int)(seconds * 30));
        for (int i = 1; i <= n; i++)
        {
            float t = i / (float)n, s = linear ? t : t * t * (3 - 2 * t);
            float u = u0 + (u1 - u0) * s + (float)(rng.NextDouble() - .5) * tremor, v = v0 + (v1 - v0) * s + (float)(rng.NextDouble() - .5) * tremor;
            Scene(a, expectedPrior, expected); a.Step(u, v, 1 / 30f, true);
        }
        return a;
    }

    static int Main()
    {
        try
        {
            var a = new IntentAssist();
            Move(a, 0, -.8f, -.45f, -.3f, .6f);
            Check(a.TargetId == 10, "a reach toward the left object picks it as the intent: " + a.TargetId);
            Check(a.Confidence > .6f, "confidence is high when heading and nearness agree: " + a.Confidence);
            Check(a.Pulling && a.OffsetU < 0, "the hand is pulled toward the intended object (Medium)");

            // Heading wins over nearness during a reach: passing near the middle object, still moving right.
            a = new IntentAssist();
            Move(a, -.35f, -.75f, .25f, -.45f, .6f, linear: true);
            Check(a.TargetId == 12, "moving right from near the middle object picks the right one: " + a.TargetId);

            // A reach that stops short, nearer the middle object than the right one, keeps the right one as the intent.
            a = new IntentAssist();
            Move(a, -.3f, -.75f, .32f, -.32f, .6f);
            for (int i = 0; i < 10; i++) { Scene(a); a.Step(.32f, -.32f, 1 / 30f, true); }
            Check(a.TargetId == 12, "a reach stopped short keeps its intent (not the nearer neighbour): " + a.TargetId);

            // Still hand near an object: nearness alone.
            a = new IntentAssist();
            Move(a, .05f, -.25f, .05f, -.25f, .5f);
            Check(a.TargetId == 11 && a.Engaged, "a still hand over the middle object engages it");
            Check(a.CloseFactor < 1f && a.CloseFactor >= .6f, "a partial close grabs the intended object (Medium .75): " + a.CloseFactor);

            // Snap: inside the snap radius the pull completes at Medium.
            float handU = .05f, handV = -.25f;
            for (int i = 0; i < 30; i++) { Scene(a); a.Step(handU, handV, 1 / 30f, true); }
            Check(Math.Abs(handU + a.OffsetU - 0f) < .01f && Math.Abs(handV + a.OffsetV + .2f) < .01f, "near enough, the hand lands on the object");

            // Moving away cancels the pull.
            a = new IntentAssist();
            Move(a, 0, -.6f, -.45f, -.3f, .5f);
            float before = Math.Abs(a.OffsetU);
            Move(a, -.45f, -.3f, .3f, -.6f, .4f);
            Check(a.TargetId != 10 || !a.Pulling || Math.Abs(a.OffsetU) < before, "moving away from a target releases the pull");

            // Off: nothing changes, the hand is exactly the patient's.
            a = new IntentAssist { Level = AssistLevel.Off };
            Move(a, 0, -.8f, -.45f, -.3f, .6f);
            Check(!a.Pulling && a.CloseFactor == 1f && a.OpenFactor == 0f && a.RangeScale == 1f, "assist off changes nothing");

            // Levels order the help.
            float Pull(AssistLevel level) { var x = new IntentAssist { Level = level }; Move(x, 0, -.8f, -.4f, -.3f, .6f); return Math.Abs(x.OffsetU); }
            Check(Pull(AssistLevel.Low) < Pull(AssistLevel.Medium) && Pull(AssistLevel.Medium) < Pull(AssistLevel.High), "Low < Medium < High pull");
            Check(new IntentAssist { Level = AssistLevel.High }.RangeScale < new IntentAssist { Level = AssistLevel.Medium }.RangeScale, "High needs the smallest movement");

            // The pull is bounded in speed: no jump.
            a = new IntentAssist { Level = AssistLevel.High };
            float last = 0, maxStep = 0;
            for (int i = 0; i < 20; i++) { Scene(a); a.Step(-.5f, -.25f, 1 / 30f, true); maxStep = Math.Max(maxStep, Math.Abs(a.OffsetU - last)); last = a.OffsetU; }
            Check(maxStep <= a.maxPullPerSecond / 30f + 1e-4f, "the pull never jumps (rate-limited): " + maxStep);

            // The activity's next item wins a tie.
            a = new IntentAssist();
            Move(a, -.3f, -.2f, -.3f, -.2f, .4f, 0, 1, 10, 3f);
            Check(a.TargetId == 10, "between two equally near objects, the one the activity needs next is chosen");

            // Tremor: a shaky reach still finds the right target, and the belief does not flicker.
            int flips = 0, prev = -1, hits = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var t = new IntentAssist();
                Move(t, 0, -.8f, .55f, -.25f, .7f, .06f, seed);
                if (t.TargetId == 12) hits++;
            }
            Check(hits >= 19, "a shaky reach (tremor 0.06) still finds the right object in 19+/20 runs: " + hits);
            var steady = new IntentAssist(); var rng = new Random(5);
            for (int i = 0; i < 90; i++) { Scene(steady); steady.Step(-.58f + (float)(rng.NextDouble() - .5) * .08f, -.22f + (float)(rng.NextDouble() - .5) * .08f, 1 / 30f, true); if (prev != -1 && steady.TargetId != prev) flips++; prev = steady.TargetId; }
            Check(flips == 0 && prev == 10, "a shaky still hand keeps one target (no flicker): flips " + flips);

            // Lost tracking: the pull eases out, no stale target.
            a = new IntentAssist(); Move(a, 0, -.8f, -.45f, -.3f, .6f);
            for (int i = 0; i < 30; i++) { Scene(a); a.Step(float.NaN, 0, 1 / 30f, false); }
            Check(!a.Pulling && a.Target == -1 && a.CloseFactor == 1f, "lost tracking eases the pull out and drops the target");

            // Bad input never poisons it.
            a = new IntentAssist(); a.BeginFrame(); a.AddCandidate(1, float.NaN, 0); a.AddCandidate(2, 0, 0, -1); a.AddCandidate(3, .1f, .1f);
            a.Step(.1f, .1f, 1 / 30f, true);
            Check(a.TargetId == 3 && !float.IsNaN(a.OffsetU), "invalid candidates are ignored");
            a.BeginFrame(); for (int i = 0; i < 40; i++) a.AddCandidate(i, i * .01f, 0); a.Step(0, 0, 1 / 30f, true);
            Check(a.Target >= 0 && a.Target < IntentAssist.MaxCandidates, "more candidates than slots are capped safely");
            a.Step(0, 0, float.NaN, true); a.Step(0, 0, 5f, true);
            Check(!float.IsNaN(a.Confidence) && !float.IsNaN(a.OffsetU), "bad time steps are safe");

            // A pile of equal choices (four foods in the pan, all going to the plate): split, none is confident; grouped, the
            // pile is (SYNTHETIC, intent probe 2026-11-21: 0.23 each at a 70 % reach).
            float split = 0, grouped = 0; int pick = -1;
            for (int g = 0; g < 2; g++)
            {
                var p = new IntentAssist();
                for (int i = 0; i < 40; i++)
                {
                    p.BeginFrame();
                    for (int k = 0; k < 4; k++) p.AddCandidate(20 + k, .02f * k, .12f + .02f * (k & 1), 3f, g == 1 ? 0 : -1);
                    p.AddCandidate(30, .45f, .35f, .25f); // the pan the foods lie in
                    p.Step(0, 0, 1 / 30f, true);
                }
                if (g == 0) split = p.Confidence; else { grouped = p.Confidence; pick = p.TargetId; }
            }
            Check(split < .5f && grouped > .9f && pick >= 20 && pick <= 23, "equal choices share their confidence: split " + split + ", grouped " + grouped + " (target " + pick + ")");

            Console.WriteLine("ALL " + passed + " INTENT ASSIST TESTS PASSED");
            return 0;
        }
        catch (Exception e) { Console.WriteLine("FAIL " + e.Message); return 1; }
    }
}
