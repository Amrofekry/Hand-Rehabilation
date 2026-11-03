using UnityEngine;

namespace GripRehab
{
    /// <summary>
    /// Camera grasp score (2026-10-29). HandKinematics.GripScore measures four-finger flexion and ignores the thumb, so pinch
    /// grips (small objects) could not grab, and a pointing hand (three fingers curled) scored high enough to grab by accident.
    /// The grasp score is the larger of the power-grip score and a thumb–finger pinch score, and pointing scores 0 (pointing
    /// means walk). Visual closure only: never a measured force.
    /// </summary>
    public static class HandGrasp
    {
        /// <summary>1 when the thumb tip touches the index or middle tip, 0 when it is a palm length away or more.</summary>
        public static float PinchScore(LandmarkFrame frame)
        {
            if (!HandKinematics.IsUsable(frame)) return 0;
            var p = frame.landmarks;
            float palm = Flat(p[0], p[9]);
            if (!(palm > 1e-4f)) return 0;
            float gap = Mathf.Min(Flat(p[4], p[8]), Flat(p[4], p[12])) / palm;
            return 1f - Mathf.Clamp01((gap - .25f) / .55f);
        }

        static LandmarkFrame lastFrame; static long lastSequence = -1; static double lastCapture; static float lastWristX, lastTipX; static HandGesture lastGesture;
        /// <summary>GestureClassifier.Classify, computed once per received frame (the grasp score and hand look both need it).</summary>
        public static HandGesture Gesture(LandmarkFrame frame)
        {
            if (frame == null || frame.landmarks == null || frame.landmarks.Length != 21) return HandGesture.None;
            if (ReferenceEquals(frame, lastFrame) && frame.sequence == lastSequence && frame.captureSeconds == lastCapture &&
                frame.landmarks[0].x == lastWristX && frame.landmarks[8].x == lastTipX) return lastGesture;
            lastFrame = frame; lastSequence = frame.sequence; lastCapture = frame.captureSeconds; lastWristX = frame.landmarks[0].x; lastTipX = frame.landmarks[8].x;
            return lastGesture = GestureClassifier.Classify(frame);
        }

        public static float Score(LandmarkFrame frame)
        {
            if (!HandKinematics.IsUsable(frame)) return 0;
            if (Gesture(frame) == HandGesture.Point) return 0;
            return Mathf.Max(HandKinematics.GripScore(frame), PinchScore(frame));
        }

        /// <summary>
        /// Estimated (visual, not measured) grip force from the power-grip score. A holding hand sits in a gentle band
        /// (about 8-20 N), a firm fist reaches 25 N at score 0.80 (2026-11-02: the 0.85 knee was above what a real fist gives,
        /// so level 8 tasks were out of reach), and only a deliberate hard squeeze (about 0.86 and up) passes porcelain's 40 N.
        /// The original linear map (score x 80 N) put an ordinary hold at about 60 N.
        /// </summary>
        public static float CameraForce(float powerScore, float maxNewtons)
        {
            float s = Mathf.Clamp01(powerScore), max = Mathf.Max(25f, maxNewtons);
            if (s < .55f) return s / .55f * 8f;
            if (s < .80f) return 8f + (s - .55f) / .25f * 17f;
            return 25f + (s - .80f) / .20f * (max - 25f);
        }

        static float Flat(Vector3 a, Vector3 b) => Mathf.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
    }
}
