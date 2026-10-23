using System;
using UnityEditor;
using UnityEngine;

namespace GripRehab.Editor
{
    public static class RehabDwellVerification
    {
        [MenuItem("Grip Rehab/Verify Whole-Hand Pointer")]
        public static void Run()
        {
            var right = Fixture("Right");
            var left = Fixture("Left");
            Require(RehabDwellPointer.IsPointing(right), "right pointing hand rejected");
            Require(RehabDwellPointer.IsPointing(left), "left pointing hand rejected");
            var relaxedRight = Fixture("Right");
            relaxedRight.landmarks[8] = new Vector3(.4f, .48f, 0);
            Require(ReferenceEquals(RehabDwellPointer.ChooseFrame(relaxedRight, true, left, true, true), left),
                "preferred left hand was masked by a valid right hand");
            Require(ReferenceEquals(RehabDwellPointer.ChooseFrame(right, true, relaxedRight, false), right),
                "right pointing hand not selected");
            Require(RehabDwellPointer.ChooseFrame(null, false, null, false) == null,
                "invalid hands selected");
            right.landmarks[7] = new Vector3(.41f, .395f, 0);
            right.landmarks[8] = new Vector3(.42f, .37f, 0);
            Require(RehabDwellPointer.IsPointing(right), "foreshortened straight index rejected");
            right.landmarks[8] = new Vector3(.4f, .48f, 0);
            Require(RehabDwellPointer.IsPointing(right), "closed hand rejected by whole-hand selection");
            left.confidence = .1f;
            Require(!RehabDwellPointer.IsPointing(left), "low confidence hand accepted");
            var viewport = HandKinematics.ToViewport(new Vector3(.25f, .75f, 0));
            Require(Mathf.Abs(viewport.x - .25f) < .001f &&
                    Mathf.Abs(viewport.y - .25f) < .001f,
                "camera-to-screen cursor direction inverted");
            var small = Fixture("Right");
            for (int i = 0; i < 21; i++) small.landmarks[i] = new Vector3(.5f, .5f, 0) +
                (small.landmarks[i] - new Vector3(.5f, .5f, 0)) * .08f;
            Require(RehabDwellPointer.IsPointing(small), "distant/small pointing hand rejected by absolute chain length");
            var open = Fixture("Right");
            foreach (int joint in new[] { 9, 13, 17 })
                for (int segment = 1; segment <= 3; segment++) open.landmarks[joint + segment] =
                    open.landmarks[joint] + Vector3.up * (-.07f * segment);
            Require(RehabDwellPointer.IsPointing(open), "open palm rejected by whole-hand selection");
            var flickeringLeft = Fixture("Left"); flickeringLeft.landmarks[8] = flickeringLeft.landmarks[5];
            Require(ReferenceEquals(RehabDwellPointer.ChooseFrame(relaxedRight, true, flickeringLeft, true, true), flickeringLeft),
                "gesture flicker swapped a left cursor to the relaxed right hand");
            VerifyGate();
            Debug.Log("[REHAB_DWELL] PASS: whole-hand scale, both hands, freshness, dwell pause/reset/latch and screen direction");
        }

        static void VerifyGate()
        {
            var gate = new RehabDwellGate();
            gate.Step(1, true, .1f, .5f, .2f);
            for (int i = 0; i < 3; i++) Require(!gate.Step(1, true, .1f, .5f, .2f), "early activation");
            var before = gate.Elapsed;
            Require(!gate.Step(1, false, .1f, .5f, .2f) && !gate.Step(1, false, .1f, .5f, .2f), "activated during unqualified input");
            Require(Mathf.Abs(gate.Elapsed - before) < .0001f, "brief flicker reset or counted dwell time");
            Require(!gate.Step(1, true, .1f, .5f, .2f) && gate.Step(1, true, .1f, .5f, .2f), "qualified time failed to activate after flicker");
            for (int i = 0; i < 20; i++) Require(!gate.Step(1, true, .1f, .5f, .2f), "held target activated twice");
            gate.Step(2, true, .1f, .5f, .2f); Require(gate.Elapsed == 0 && !gate.Latched, "new target carried old progress");
            gate.Step(2, true, .1f, .5f, .2f);
            for (int i = 0; i < 3; i++) gate.Step(2, false, .1f, .5f, .2f);
            Require(gate.Elapsed == 0, "long gesture release failed to reset");
            gate.Step(2, true, 30, 5, .2f); Require(gate.Elapsed == 0, "frame hitch instantly completed selection");
            gate.Step(2, true, 30, 5, .2f); Require(gate.Elapsed <= .10001f, "frame hitch counted unobserved time");
            gate.Reset(); Require(gate.Elapsed == 0 && !gate.Latched, "tracking loss failed to reset");
        }

        static LandmarkFrame Fixture(string side)
        {
            var frame = new LandmarkFrame
            {
                schemaVersion = 1, handedness = side, confidence = .95f,
                landmarks = new Vector3[21]
            };
            for (int i = 0; i < 21; i++) frame.landmarks[i] = new Vector3(.5f, .5f, 0);
            frame.landmarks[5] = new Vector3(.4f, .5f, 0);
            frame.landmarks[6] = new Vector3(.4f, .42f, 0);
            frame.landmarks[7] = new Vector3(.4f, .35f, 0);
            frame.landmarks[8] = new Vector3(.4f, .28f, 0);
            foreach (int joint in new[] { 9, 13, 17 })
            {
                frame.landmarks[joint] = new Vector3(.5f, .5f, 0);
                frame.landmarks[joint + 1] = new Vector3(.5f, .42f, 0);
                frame.landmarks[joint + 3] = new Vector3(.5f, .47f, 0);
            }
            return frame;
        }

        static void Require(bool condition, string reason)
        {
            if (!condition) throw new Exception("Hands-free pointer: " + reason);
        }
    }
}
