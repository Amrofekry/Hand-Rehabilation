using System;
using UnityEngine;

namespace GripRehab
{
    // Pure hand/position decisions shared by the runtime cursor and offline tests.
    // Estimated hand shape is not measured force or a clinical range of motion.
    public static class RehabPointerPolicy
    {
        // Same frame validity and freshness source as Module 1. No finger-pose gate.
        public static bool HasTrackedHand(LandmarkFrame frame) => HandKinematics.IsUsable(frame);
        // Compatibility for existing Editor tools and acceptance callers.
        public static bool IsPointing(LandmarkFrame frame) => HasTrackedHand(frame);

        public static LandmarkFrame ChooseFrame(LandmarkFrame right, bool rightValid,
            LandmarkFrame left, bool leftValid, bool? preferredLeft = null)
        {
            rightValid &= HasTrackedHand(right); leftValid &= HasTrackedHand(left);
            if (preferredLeft == true && leftValid) return left;
            if (preferredLeft == false && rightValid) return right;
            return rightValid ? right : leftValid ? left : null;
        }

        public static Vector2 PalmScreenPoint(LandmarkFrame frame, int width, int height)
        {
            if (!HasTrackedHand(frame)) return new Vector2 {x=Math.Max(1,width)*.5f,y=Math.Max(1,height)*.5f};
            // Wrist + four knuckles. Finger tips, bending and world-centred pose
            // coordinates cannot displace the screen cursor.
            var points=frame.landmarks;
            var palm=(points[0]+points[5]+points[9]+points[13]+points[17])*.2f;
            return ScreenPoint(palm,width,height);
        }

        public static float SafeDt(float dt) => float.IsFinite(dt) && dt > 0 ? Math.Min(.1f, dt) : 0;
        public static float Duration(float value) => float.IsFinite(value) && value > 0 ?
            Math.Max(.5f, Math.Min(30f, value)) : 5f;
        public static float Grace(float value) => float.IsFinite(value) ? Math.Max(0, Math.Min(.3f, value)) : .2f;
        public static Vector2 ScreenPoint(Vector3 tip, int width, int height)
        {
            var viewport = HandKinematics.ToViewport(tip);
            return new Vector2 { x = Mathf.Clamp01(viewport.x) * Math.Max(1, width),
                                 y = Mathf.Clamp01(viewport.y) * Math.Max(1, height) };
        }
        public static Vector2 Smooth(Vector2 current, Vector2 desired, bool initialized, float dt, float seconds)
        {
            if (!initialized || !float.IsFinite(current.x) || !float.IsFinite(current.y)) return desired;
            seconds = float.IsFinite(seconds) ? Math.Max(0, Math.Min(1, seconds)) : .055f;
            float blend = seconds == 0 ? 1 : 1 - (float)Math.Exp(-SafeDt(dt) / seconds);
            return new Vector2 { x = current.x + (desired.x - current.x) * blend,
                                 y = current.y + (desired.y - current.y) * blend };
        }
    }

    // A control revealed by navigation must not activate under a stationary hand.
    // Move deliberately or remove the hand, then dwell on the next control.
    public sealed class RehabPointerRearm
    {
        Vector2 activatedAt;
        float released;
        public bool Waiting { get; private set; }
        public void Reset() { Waiting = false; released = 0; }
        public void Activated(Vector2 position) { activatedAt = position; Waiting = true; released = 0; }
        public bool CanSelect(Vector2 position, bool pointing, float dt)
        {
            if (!Waiting) return true;
            if (float.IsFinite(position.x) && float.IsFinite(position.y) && Vector2.Distance(position, activatedAt) >= 32)
            { Reset(); return true; }
            released = pointing ? 0 : released + RehabPointerPolicy.SafeDt(dt);
            if (released > .2f) Reset();
            return false;
        }
    }
}
