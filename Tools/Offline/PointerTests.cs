using System;
using GripRehab;
using UnityEngine;

// Fabricated landmark/input fixtures only. No webcam, native uGUI or Unity process.
static class PointerTests
{
    static int passed, failed;
    static void Check(bool ok, string label)
    { Console.WriteLine((ok ? "PASS " : "FAIL ") + label); if (ok) passed++; else failed++; }
    static LandmarkFrame Hand(string side = "Right", float scale = 1)
    {
        var f = new LandmarkFrame { schemaVersion = 1, handedness = side, confidence = .95f,
            landmarks = new Vector3[21], imageWidth = 640, imageHeight = 480 };
        for (int i = 0; i < 21; i++) f.landmarks[i] = new Vector3(.5f, .7f, 0);
        for (int finger = 0; finger < 4; finger++)
        {
            int n = 5 + 4 * finger; float x = .4f + finger * .08f;
            f.landmarks[n] = new Vector3(x, .5f, 0);
            f.landmarks[n+1] = new Vector3(x, .42f, 0);
            f.landmarks[n+2] = new Vector3(x, finger == 0 ? .35f : .45f, .02f);
            f.landmarks[n+3] = new Vector3(x, finger == 0 ? .28f : .49f, 0);
        }
        f.landmarks[7].z = 0;
        for (int i = 0; i < 21; i++)
        {
            var delta = (f.landmarks[i] - f.landmarks[0]) * scale;
            if (side == "Left") delta.x = -delta.x;
            f.landmarks[i] = f.landmarks[0] + delta;
        }
        return f;
    }
    static void World(LandmarkFrame f)
    {
        var points = new Vector3[21];
        for (int i = 0; i < 21; i++) points[i] = HandKinematics.PosePoint(f, i);
        f.worldLandmarks = points;
    }
    static void Open(LandmarkFrame f, int n, bool world = false)
    {
        var p = world ? f.worldLandmarks : f.landmarks;
        for (int i = 1; i <= 3; i++) p[n+i] = p[n] + new Vector3(0, -.07f * i, 0);
    }
    static void Curl(LandmarkFrame f, int n, bool world = false)
    {
        var p = world ? f.worldLandmarks : f.landmarks;
        p[n+1] = p[n] + new Vector3(0, -.08f, 0);
        p[n+2] = p[n] + new Vector3(0, -.04f, .03f);
        p[n+3] = p[n] + new Vector3(0, -.01f, 0);
    }
    static void Gestures()
    {
        foreach (string side in new[] { "Right", "Left" })
        {
            foreach (float scale in new[] { 1f, .25f, .08f })
                Check(RehabPointerPolicy.IsPointing(Hand(side, scale)), side + " image pointer at scale " + scale);
            var closed = Hand(side); Curl(closed, 5);
            Check(RehabPointerPolicy.IsPointing(closed), side + " closed hand can select");
            var open = Hand(side); foreach (int n in new[] { 9, 13, 17 }) Open(open, n);
            Check(RehabPointerPolicy.IsPointing(open), side + " open palm can select");
            var limited = Hand(side); Open(limited, 9);
            Check(RehabPointerPolicy.IsPointing(limited), side + " thumb and one other finger may stay extended");
            Open(limited, 13);
            Check(RehabPointerPolicy.IsPointing(limited), side + " no curled-finger requirement");
            var world = Hand(side); World(world);
            var geometry = (Vector3[])world.worldLandmarks.Clone();
            foreach (Vector3 axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
                foreach (float angle in new[] { -75f, 75f })
                {
                    for (int i = 0; i < 21; i++) world.worldLandmarks[i] = Quaternion.AngleAxis(angle, axis) * geometry[i];
                    Check(RehabPointerPolicy.IsPointing(world), side + " world gesture survives " + angle + " rotation on " + axis.x + axis.y + axis.z);
                }
            world.worldLandmarks = geometry; Curl(world, 5, true);
            Check(RehabPointerPolicy.IsPointing(world), side + " bent world index remains a tracked hand");
            world = Hand(side); World(world); Curl(world, 5);
            Check(RehabPointerPolicy.IsPointing(world), side + " world pointer survives curled-looking image projection");
            world = Hand(side); World(world); foreach (int n in new[] { 9, 13, 17 }) Open(world, n, true);
            Check(RehabPointerPolicy.IsPointing(world), side + " open world palm remains a tracked hand");
        }
        var right = Hand(); var left = Hand("Left"); var relaxed = Hand(); Curl(relaxed, 5);
        Check(ReferenceEquals(RehabPointerPolicy.ChooseFrame(relaxed, true, left, true, true), left), "Preferred left is not masked by relaxed right");
        Check(ReferenceEquals(RehabPointerPolicy.ChooseFrame(right, true, left, true, true), left), "Both pointers keep preferred left");
        Check(ReferenceEquals(RehabPointerPolicy.ChooseFrame(right, true, left, true, false), right), "Both pointers keep preferred right");
        var relaxedLeft = Hand("Left"); Curl(relaxedLeft, 5);
        Check(ReferenceEquals(RehabPointerPolicy.ChooseFrame(relaxed, true, relaxedLeft, true, true), relaxedLeft), "Gesture flicker preserves chosen hand");
        Check(ReferenceEquals(RehabPointerPolicy.ChooseFrame(right, true, relaxedLeft, true, true), relaxedLeft), "Finger shape cannot steal the preferred hand");
        Check(RehabPointerPolicy.ChooseFrame(right, false, left, false) == null, "Stale streams never select");
        right.confidence = .1f;
        Check(ReferenceEquals(RehabPointerPolicy.ChooseFrame(right, true, left, true), left), "Invalid frame cannot mask usable left");
        Check(RehabPointerPolicy.ChooseFrame(right, true, null, false) == null, "Bad confidence rejected even with mistaken valid flag");
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var f = Hand(); f.landmarks[8].x = bad;
            Check(!RehabPointerPolicy.IsPointing(f), "Nonfinite tip rejected " + bad);
        }
        foreach(string side in new[]{"Left","Right"}) {
            var stable=Hand(side); var palm=RehabPointerPolicy.PalmScreenPoint(stable,1920,1080);
            foreach(int start in new[]{1,5,9,13,17}) {
                Curl(stable,start);
                Check(Vector2.Distance(palm,RehabPointerPolicy.PalmScreenPoint(stable,1920,1080))<.001f,side+" finger curl does not move palm cursor "+start);
            }
            for(int i=0;i<21;i++)stable.landmarks[i]+=new Vector3(.1f,-.1f,0);
            var moved=RehabPointerPolicy.PalmScreenPoint(stable,1920,1080);
            Check(moved.x>palm.x&&moved.y>palm.y,side+" translated palm moves right/up");
            stable.worldLandmarks=new Vector3[21];
            Check(Vector2.Distance(moved,RehabPointerPolicy.PalmScreenPoint(stable,1920,1080))<.001f,side+" world-centred pose does not move image cursor");
        }
        var malformed = Hand(); malformed.landmarks = new Vector3[20];
        Check(!RehabPointerPolicy.IsPointing(malformed), "Incomplete landmarks rejected");
        malformed = Hand(); malformed.ageMs = 500;
        Check(!RehabPointerPolicy.IsPointing(malformed), "500 ms boundary rejected");
        malformed = Hand(); malformed.handedness = "Unknown";
        Check(!RehabPointerPolicy.IsPointing(malformed), "Unknown side rejected");
        Check(!RehabPointerPolicy.IsPointing(null), "Missing hand rejected");
    }
    static void Gate()
    {
        var gate = new RehabDwellGate(); int clicks = 0;
        gate.Step(1, true, .02f, 5, .2f);
        for (int i = 0; i < 249; i++) if (gate.Step(1, true, .02f, 5, .2f)) clicks++;
        Check(clicks == 0, "Five-second hold never selects early");
        Check(gate.Step(1, true, .02f, 5, .2f), "Five qualified seconds activate");
        for (int i = 0; i < 600; i++) if (gate.Step(1, true, .02f, 5, .2f)) clicks++;
        Check(clicks == 0 && gate.Latched, "Stationary hold does not repeat activation");
        gate.Step(2, true, .02f, 5, .2f);
        Check(gate.Elapsed == 0 && !gate.Latched, "Target switch discards earlier effort");
        for (int i = 0; i < 100; i++) gate.Step(2, true, .02f, 5, .2f);
        float prior = gate.Elapsed;
        for (int i = 0; i < 5; i++) Check(!gate.Step(2, false, .02f, 5, .2f), "Flicker cannot click " + i);
        Check(Math.Abs(gate.Elapsed-prior) < .00001f, "Grace pauses rather than counts time");
        for (int i = 0; i < 20; i++) gate.Step(2, false, .02f, 5, .2f);
        Check(gate.Elapsed == 0 && !gate.Latched, "Long gesture loss resets dwell");
        gate.Step(0, false, .02f, 5, .2f);
        Check(gate.Elapsed == 0 && !gate.Latched, "Tracking or focus loss clears dwell");
        gate.Step(1, true, .1f, 5, .2f); gate.Step(1, true, 40, 5, .2f);
        Check(gate.Elapsed <= .100001f, "Frame hitch cannot fake observed dwell time");
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, -1f })
        {
            prior = gate.Elapsed; Check(!gate.Step(1, true, bad, 5, .2f) && gate.Elapsed == prior, "Bad delta adds no dwell " + bad);
        }
        Check(!gate.Step(1, true, .1f, float.NaN, .2f) && gate.Elapsed == 0, "Bad duration fails closed");
    }
#if !LEGACY_POINTER
    static Vector2 Point(float x, float y) => new Vector2 { x = x, y = y };
    static void PositionAndRearm()
    {
        foreach (int width in new[] { 1280, 1920, 3840 })
        {
            var p = RehabPointerPolicy.ScreenPoint(new Vector3(.25f,.75f,0),width,720);
            Check(Math.Abs(p.x-width*.25f) < .01f && Math.Abs(p.y-180) < .01f, "Screen direction at " + width);
            Check(RehabPointerPolicy.ScreenPoint(new Vector3(.75f,.75f,0),width,720).x > p.x, "Rightward tip moves screen right " + width);
            Check(RehabPointerPolicy.ScreenPoint(new Vector3(.25f,.25f,0),width,720).y > p.y, "Upward tip moves screen up " + width);
        }
        var clamped = RehabPointerPolicy.ScreenPoint(new Vector3(-.1f,1.1f,0),1280,720);
        Check(clamped.x == 0 && clamped.y == 0, "Off-frame tip stays within screen bounds");
        var old = Point(100,100); var next = Point(800,500);
        Check(Vector2.Distance(RehabPointerPolicy.Smooth(old,next,false,.02f,.055f),next)==0, "New hand snaps to its own position");
        var smooth = RehabPointerPolicy.Smooth(old,next,true,.02f,.055f);
        Check(smooth.x>old.x && smooth.x<next.x && smooth.y>old.y && smooth.y<next.y, "Cursor converges without overshoot");
        Check(Vector2.Distance(RehabPointerPolicy.Smooth(old,next,true,.02f,0),next)==0, "Zero smoothing remains immediate");
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            smooth = RehabPointerPolicy.Smooth(old,next,true,.02f,bad);
            Check(float.IsFinite(smooth.x) && smooth.x>old.x && smooth.x<next.x, "Bad smoothing cannot poison cursor " + bad);
            Check(RehabPointerPolicy.Duration(bad)==5 && RehabPointerPolicy.Grace(bad)==.2f, "Bad preferences use safe defaults " + bad);
            Check(RehabPointerPolicy.SafeDt(bad)==0, "Bad delta is ignored " + bad);
        }
        Check(RehabPointerPolicy.Duration(100)==30 && RehabPointerPolicy.Duration(.1f)==.5f, "Duration stays bounded");
        var rearm = new RehabPointerRearm();
        Check(rearm.CanSelect(old,true,.02f), "Initial cursor can select");
        rearm.Activated(old);
        bool repeat = false; for(int i=0;i<500;i++) repeat |= rearm.CanSelect(old,true,.02f);
        Check(!repeat && rearm.Waiting, "Newly exposed control cannot click under a stationary hand");
        Check(!rearm.CanSelect(Point(110,100),true,.02f), "Small pointer jitter cannot rearm");
        Check(rearm.CanSelect(Point(140,100),true,.02f) && !rearm.Waiting, "Deliberate pointer movement rearms");
        rearm.Activated(old);
        for(int i=0;i<5;i++) rearm.CanSelect(old,false,.02f);
        Check(rearm.Waiting, "Brief gesture flicker cannot rearm a selected control");
        for(int i=0;i<12;i++) Check(!rearm.CanSelect(old,false,.02f) || !rearm.Waiting, "Relaxed gesture does not itself activate " + i);
        Check(rearm.CanSelect(old,true,.02f) && !rearm.Waiting, "Deliberate gesture release allows another dwell");
        rearm.Activated(old); rearm.Reset();
        Check(rearm.CanSelect(next,true,.02f), "Tracking/focus reset leaves no stale rearm latch");
    }
#endif
    static int Main()
    {
        Gestures(); Gate();
#if !LEGACY_POINTER
        PositionAndRearm();
#endif
        Console.WriteLine("Pointer checks: " + passed + " passed, " + failed + " failed (fabricated fixtures only)");
        return failed == 0 ? 0 : 1;
    }
}
