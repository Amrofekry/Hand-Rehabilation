using System;
using UnityEngine;

namespace GripRehab
{
    [Serializable]
    public sealed class LandmarkFrame
    {
        public int schemaVersion;
        public double captureSeconds;
        public float ageMs; // Capture-to-send time measured by the vision process.
        public long sequence;
        public float confidence;
        public string source;
        public string handedness; // "Left" or "Right"; absent in legacy frames means Right.
        public string gesture;
        public float gestureConfidence;
        public Vector3[] landmarks;
    }

    public static class HandKinematics
    {
        // MediaPipe image x goes right and y goes down. Its relative z is negative towards the camera.
        public static Vector3 ToCameraSpace(Vector3 point) =>
            new Vector3(point.x, 1f - point.y, -point.z);

        public static Vector3 ToViewport(Vector3 normalizedImagePoint) =>
            new Vector3(normalizedImagePoint.x, 1f - normalizedImagePoint.y, normalizedImagePoint.z);

        public static bool IsLeft(LandmarkFrame frame) =>
            frame != null && string.Equals(frame.handedness, "Left", StringComparison.OrdinalIgnoreCase);

        public static bool HasKnownHandedness(LandmarkFrame frame) =>
            frame != null && (string.IsNullOrEmpty(frame.handedness) ||
                string.Equals(frame.handedness, "Left", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(frame.handedness, "Right", StringComparison.OrdinalIgnoreCase));

        public static bool IsUsable(LandmarkFrame frame, float minConfidence = 0.65f)
        {
            if (frame == null || frame.schemaVersion != 1 || frame.landmarks == null ||
                frame.landmarks.Length != 21 || !HasKnownHandedness(frame) ||
                float.IsNaN(frame.confidence) || float.IsInfinity(frame.confidence) ||
                frame.confidence < minConfidence || frame.confidence > 1f ||
                double.IsNaN(frame.captureSeconds) || double.IsInfinity(frame.captureSeconds) ||
                frame.captureSeconds < 0 || float.IsNaN(frame.ageMs) ||
                float.IsInfinity(frame.ageMs) || frame.ageMs < 0 || frame.ageMs >= 500) return false;
            foreach (var p in frame.landmarks)
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                    float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z) ||
                    p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1 || Mathf.Abs(p.z) > 4f) return false;
            return true;
        }

        public static float PinchDistance(LandmarkFrame frame)
        {
            if (!IsUsable(frame)) return float.NaN;
            return Vector2.Distance(frame.landmarks[4], frame.landmarks[8]);
        }
    }
}
