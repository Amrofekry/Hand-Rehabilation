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

        public static float Score(LandmarkFrame frame)
        {
            if (!HandKinematics.IsUsable(frame)) return 0;
            if (GestureClassifier.Classify(frame) == HandGesture.Point) return 0;
            return Mathf.Max(HandKinematics.GripScore(frame), PinchScore(frame));
        }

        static float Flat(Vector3 a, Vector3 b) => Mathf.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
    }
}
