using System;
using GripRehab;
using UnityEngine;

// Executes the production camera-hand depth with fabricated (SYNTHETIC) hands: nearer, farther, tilted, swept and shaky.
class HandDepthTests
{
    static int passed;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }

    // A flat right hand: palm 0.09 m long, 0.08 m wide, at distance z (m) from a camera with a 0.6 focal length (image
    // heights per metre at 1 m), centred at (cx, cy); tilt turns it about the vertical axis (foreshortens x).
    static void Hand(float z, float cx, float cy, float tiltDeg, Vector3[] image, Vector3[] world)
    {
        float f = .6f, c = (float)Math.Cos(tiltDeg * Math.PI / 180);
        for (int i = 0; i < 21; i++)
        {
            int finger = i == 0 ? -1 : (i - 1) / 4; int joint = i == 0 ? 0 : (i - 1) % 4;
            float wx = finger < 0 ? 0 : (finger - 2f) * .02f, wy = finger < 0 ? 0 : -.09f - joint * .025f;
            if (finger == 0) { wx = -.045f - joint * .01f; wy = -.03f - joint * .015f; }
            float px = wx * c, py = wy; // tilt about the vertical axis shortens x in the image (and in the 3D plane)
            world[i] = new Vector3(px, py, wx * (float)Math.Sin(tiltDeg * Math.PI / 180));
            image[i] = new Vector3(cx + px * f / z / (4f / 3f), cy + py * f / z, 0);
        }
    }

    static int Main()
    {
        try
        {
            var image = new Vector3[21]; var world = new Vector3[21]; float aspect = 4f / 3f, dt = 1 / 30f;
            var d = new HandDepth();
            for (int i = 0; i < 60; i++) { Hand(.5f, .5f, .6f, 0, image, world); d.Step(image, world, aspect, dt); }
            Check(Math.Abs(d.Offset) < .005f, "a still hand at rest: no depth offset: " + d.Offset);

            // Toward the camera: 0.5 m -> 0.38 m over 1 s (slowly, little travel across the image).
            for (int i = 0; i < 30; i++) { Hand(.5f - .12f * (i + 1) / 30f, .5f, .6f, 0, image, world); d.Step(image, world, aspect, dt); }
            for (int i = 0; i < 15; i++) { d.Step(image, world, aspect, dt); }
            Check(d.Offset > .25f, "moving the hand toward the camera moves the virtual hand forward: " + d.Offset);
            float near = d.Offset;
            // Away again, past rest.
            for (int i = 0; i < 40; i++) { Hand(.38f + .2f * (i + 1) / 40f, .5f, .6f, 0, image, world); d.Step(image, world, aspect, dt); }
            for (int i = 0; i < 15; i++) { d.Step(image, world, aspect, dt); }
            Check(d.Offset < 0 && d.Offset >= -d.maxBack, "moving it away moves the virtual hand back (bounded): " + d.Offset);
            Check(near <= d.maxForward, "forward is bounded");

            // Tilt: with world landmarks the cue follows distance, not tilt (palm-only would see a 25 % smaller hand at 40 deg).
            var t = new HandDepth();
            for (int i = 0; i < 40; i++) { Hand(.5f, .5f, .6f, 0, image, world); t.Step(image, world, aspect, dt); }
            for (int i = 0; i < 30; i++) { Hand(.5f, .5f, .6f, 40f * (i + 1) / 30f, image, world); t.Step(image, world, aspect, dt); }
            var palmOnly = new HandDepth();
            for (int i = 0; i < 40; i++) { Hand(.5f, .5f, .6f, 0, image, world); palmOnly.Step(image, null, aspect, dt); }
            for (int i = 0; i < 30; i++) { Hand(.5f, .5f, .6f, 40f * (i + 1) / 30f, image, world); palmOnly.Step(image, null, aspect, dt); }
            Check(Math.Abs(t.Level) < Math.Abs(palmOnly.Level), "a tilted hand fools the depth less with world landmarks: " + t.Level + " vs palm only " + palmOnly.Level);

            // A sweep across the image at a constant distance with size noise: held, so the depth barely moves.
            var s = new HandDepth(); var rng = new Random(3);
            for (int i = 0; i < 30; i++) { Hand(.5f, .5f, .6f, 0, image, world); s.Step(image, world, aspect, dt); }
            float worst = 0;
            for (int i = 0; i < 45; i++) { Hand(.5f * (1 + (float)(rng.NextDouble() - .5) * .08f), .2f + .6f * i / 44f, .6f, 0, image, world); s.Step(image, world, aspect, dt); worst = Math.Max(worst, Math.Abs(s.Offset)); }
            Check(worst < .05f, "a fast sweep with a noisy size does not push the hand forward or back: " + worst);

            // Shaky still hand (size noise 4 %): the dead zone and smoothing keep the hand steady.
            var n = new HandDepth(); worst = 0;
            for (int i = 0; i < 150; i++) { Hand(.5f * (1 + (float)(rng.NextDouble() - .5) * .08f), .5f, .6f, 0, image, world); n.Step(image, world, aspect, dt); if (i > 30) worst = Math.Max(worst, Math.Abs(n.Offset)); }
            Check(worst < .03f, "a shaky still hand stays put in depth: " + worst);

            // Bad input is ignored; a long gap re-acquires.
            float before = n.Offset; n.Step(null, null, aspect, dt); n.Step(new Vector3[3], null, aspect, dt);
            Check(n.Offset == before, "bad frames are ignored");
            Hand(.3f, .5f, .6f, 0, image, world); n.Step(image, world, aspect, 2f);
            Check(n.Offset == 0 && n.Valid, "after a gap the new hand is the new rest (no jump)");
            Console.WriteLine("ALL " + passed + " HAND DEPTH TESTS PASSED");
            return 0;
        }
        catch (Exception e) { Console.WriteLine("FAIL " + e.Message); return 1; }
    }
}
