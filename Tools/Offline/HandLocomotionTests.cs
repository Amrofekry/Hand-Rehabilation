using System;
using GripRehab;

// Executes the production hand look/walk policy with fabricated palm positions only (no camera or Unity).
class HandLocomotionTests
{
    static int passed;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    static HandLocomotionPolicy Run(float x, float y, bool point, float seconds, HandLocomotionPolicy p = null, bool usable = true, float conf = .9f)
    {
        p = p ?? new HandLocomotionPolicy();
        for (float t = 0; t < seconds; t += 1 / 30f) p.Step(x, y, point, usable, conf, 1 / 30f);
        return p;
    }
    static int Main()
    {
        try
        {
            Check(Run(.5f, .5f, false, 2).YawRate == 0 && Run(.5f, .5f, false, 2).PitchRate == 0, "centre: no turning");
            Check(Run(.68f, .65f, false, 2).YawRate == 0 && Run(.68f, .65f, false, 2).PitchRate == 0, "anywhere in the work zone: no turning (reach and grab freely)");
            Check(Run(.85f, .5f, false, 2).YawRate > 0, "hand to the right: turn right");
            Check(Run(.15f, .5f, false, 2).YawRate < 0, "hand to the left: turn left");
            Check(Run(.5f, .1f, false, 2).PitchRate < 0 && Run(.5f, .9f, false, 2).PitchRate > 0, "hand up looks up, down looks down");
            Check(Run(.8f, .5f, false, 2).YawRate < Run(.92f, .5f, false, 2).YawRate, "further out turns faster");
            var p = new HandLocomotionPolicy();
            Check(Math.Abs(Run(1.05f, .5f, false, 3).YawRate - p.maxYawDegPerSec) < .5f && Run(1.05f, .5f, false, 3).YawRate <= p.maxYawDegPerSec, "turn rate is capped");
            var ease = Run(.95f, .5f, false, 1 / 30f);
            Check(ease.YawRate > 0 && ease.YawRate < .5f * p.maxYawDegPerSec, "turning eases in, no jerk");
            var back = Run(.95f, .5f, false, 2); Run(.5f, .5f, false, 1 / 30f, back);
            Check(back.YawRate > 0, "turning eases out"); Run(.5f, .5f, false, 1.5f, back);
            Check(back.YawRate == 0, "returning to the work zone stops turning");
            Check(!Run(.5f, .5f, true, .15f).Walking, "a brief point does not walk");
            var walk = Run(.5f, .5f, true, 1.5f);
            Check(walk.Walk == 1, "a held point walks at full speed");
            Run(.5f, .5f, false, .25f, walk);
            Check(!walk.Walking, "any other hand shape stops walking quickly");
            var both = Run(.9f, .5f, true, 1.5f);
            Check(both.Walking && both.YawRate > 0, "walk and turn together");
            var lost = Run(.9f, .5f, true, 1.5f); lost.Step(.9f, .5f, true, false, .9f, 1 / 30f);
            Check(lost.YawRate == 0 && lost.Walk == 0, "lost tracking or a menu stops everything at once");
            var doubtful = Run(.9f, .5f, true, 1.5f); doubtful.Step(.9f, .5f, true, true, .2f, 1 / 30f);
            Check(doubtful.YawRate == 0 && doubtful.Walk == 0, "low confidence stops everything");
            var bad = Run(.9f, .5f, true, 1.5f); bad.Step(float.NaN, .5f, true, true, .9f, 1 / 30f);
            Check(bad.YawRate == 0 && bad.Walk == 0, "bad coordinates stop everything");
            var hitch = Run(.95f, .5f, false, 1); float before = hitch.YawRate; hitch.Step(.95f, .5f, false, true, .9f, 5f);
            Check(hitch.YawRate == before, "a long frame hitch does not jump the turn");
            // Actions that replace keys.
            int Count(HandActionPolicy a, bool fist, bool pinch, bool holding, float seconds, ref int options)
            { int grabs = 0; for (float t = 0; t < seconds; t += 1 / 30f) { a.Step(fist, pinch, holding, 0, true, 1 / 30f); if (a.GrabRequested) grabs++; if (a.OptionsRequested) options++; } return grabs; }
            var act = new HandActionPolicy(); int opts = 0;
            Check(Count(act, true, false, false, .2f, ref opts) == 0, "a brief fist does not grab");
            Check(Count(act, true, false, false, 2f, ref opts) == 1, "a held fist grabs once, not repeatedly");
            Count(act, false, false, false, .2f, ref opts);
            Check(Count(act, true, false, false, .6f, ref opts) == 1, "opening and closing again grabs again");
            Check(Count(new HandActionPolicy(), true, false, true, 2f, ref opts) == 0, "a fist while already holding does not grab again");
            opts = 0; var pinchAct = new HandActionPolicy(); Count(pinchAct, false, true, false, .5f, ref opts);
            Check(opts == 0, "a brief pinch does not open options"); Count(pinchAct, false, true, false, 2f, ref opts);
            Check(opts == 1, "a held pinch opens options once");
            var tw = new HandActionPolicy(); tw.Step(true, false, true, 0, true, 1 / 30f);
            Check(tw.PlacementYaw == 0, "twist starts at zero when the object is grabbed");
            tw.Step(true, false, true, 8, true, 1 / 30f); Check(tw.PlacementYaw == 0, "small twists are ignored (dead zone)");
            tw.Step(true, false, true, 45, true, 1 / 30f); Check(tw.PlacementYaw > 0 && tw.PlacementYaw % 5 == 0, "twisting right turns the placement right in 5° steps: " + tw.PlacementYaw);
            tw.Step(true, false, true, -45, true, 1 / 30f); Check(tw.PlacementYaw < 0, "twisting left turns it left");
            tw.Step(true, false, true, 175, true, 1 / 30f); float big = tw.PlacementYaw; tw.Step(true, false, true, -175, true, 1 / 30f);
            Check(Math.Abs(tw.PlacementYaw) <= 180 && Math.Abs(big) <= 180, "turn stays within ±180°");
            tw.Step(false, false, false, 30, true, 1 / 30f); Check(float.IsNaN(tw.PlacementYaw), "no placement turn when not holding");
            Check(Math.Abs(HandActionPolicy.Roll(.5f, .6f, .5f, .5f)) < .01f && HandActionPolicy.Roll(.5f, .6f, .6f, .5f) > 40, "roll: fingers up is 0, leaning right is positive");
            var lostAct = new HandActionPolicy(); for (int i = 0; i < 9; i++) lostAct.Step(true, false, false, 0, true, 1 / 30f);
            lostAct.Step(true, false, false, 0, false, 1 / 30f); bool fired = false; for (int i = 0; i < 5; i++) { lostAct.Step(true, false, false, 0, true, 1 / 30f); fired |= lostAct.GrabRequested; }
            Check(!fired, "lost tracking restarts the fist hold (no grab from half a hold)");

            Console.WriteLine("HAND LOCOMOTION TESTS PASSED: " + passed);
            return 0;
        }
        catch (Exception error) { Console.WriteLine("FAIL " + error.Message); return 1; }
    }
}
