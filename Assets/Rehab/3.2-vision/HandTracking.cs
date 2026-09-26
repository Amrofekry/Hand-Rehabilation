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

        public static bool IsUsable(LandmarkFrame frame, float minConfidence = 0.5f)
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
                    p.x < -.2f || p.x > 1.2f || p.y < -.2f || p.y > 1.2f || Mathf.Abs(p.z) > 4f) return false;
            return true;
        }

        // Dimensionless visual closure; never interpret it as measured grip force.
        // Four finger chains are used because the thumb's opposition differs from flexion.
        public static float GripScore(LandmarkFrame frame)
        {
            if (!IsUsable(frame)) return 0;
            float total = 0;
            for (int finger = 0; finger < 4; finger++)
            {
                int mcp = 5 + finger * 4;
                Vector3 a = frame.landmarks[mcp], b = frame.landmarks[mcp + 1];
                Vector3 c = frame.landmarks[mcp + 2], d = frame.landmarks[mcp + 3];
                float segment = Vector3.Distance(a, b);
                if (segment < .003f) return 0;
                float tipRatio = Vector3.Distance(a, d) / segment;
                float proximity = 1f - Mathf.Clamp01((tipRatio - 1.3f) / 1.6f);
                float bend = (FingerBend(a, b, c) + FingerBend(b, c, d)) / 160f;
                total += .65f * Mathf.Clamp01(bend) + .35f * proximity;
            }
            return total / 4f;
        }

        static float FingerBend(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 first = b - a, second = c - b;
            return first.sqrMagnitude < 1e-8f || second.sqrMagnitude < 1e-8f ? 0 :
                Mathf.Clamp(Vector3.Angle(first, second), 0, 100);
        }

        public static float PinchDistance(LandmarkFrame frame)
        {
            if (!IsUsable(frame)) return float.NaN;
            return Vector2.Distance(frame.landmarks[4], frame.landmarks[8]);
        }
    }

    // Hysteresis and dwell prevent flicker from alternating pickup/drop each frame.
    public sealed class CameraGripGate
    {
        public bool Closed { get; private set; }
        float candidateSeconds;
        public void Reset() { Closed = false; candidateSeconds = 0; }
        public void Step(float score, bool valid, float dt, float closeAt, float openAt, float dwell)
        {
            if (!valid) { Reset(); return; }
            bool candidate = Closed ? score <= openAt : score >= closeAt;
            candidateSeconds = candidate ? candidateSeconds + Mathf.Max(0, dt) : 0;
            if (candidateSeconds >= dwell) { Closed = !Closed; candidateSeconds = 0; }
        }
    }
}
