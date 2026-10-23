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
        // Optional v1 extensions. World points describe estimated hand shape,
        // centred on the hand; they do not locate the wrist in the apartment.
        public Vector3[] worldLandmarks;
        public int imageWidth, imageHeight;
    }

    public static class HandKinematics
    {
        // MediaPipe image x goes right and y goes down. The imported
        // VirtualGrasp palm basis uses the opposite local depth convention.
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
            if ((frame.imageWidth != 0 || frame.imageHeight != 0) &&
                (frame.imageWidth < 1 || frame.imageHeight < 1 ||
                 frame.imageWidth > 8192 || frame.imageHeight > 8192)) return false;
            if (frame.worldLandmarks != null && frame.worldLandmarks.Length != 0)
            {
                if (frame.worldLandmarks.Length != 21) return false;
                foreach (var p in frame.worldLandmarks)
                    if (!Finite(p) || Mathf.Abs(p.x) > 2 || Mathf.Abs(p.y) > 2 || Mathf.Abs(p.z) > 2) return false;
            }
            foreach (var p in frame.landmarks)
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                    float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z) ||
                    p.x < -.2f || p.x > 1.2f || p.y < -.2f || p.y > 1.2f || Mathf.Abs(p.z) > 4f) return false;
            return true;
        }

        static bool Finite(Vector3 p) =>
            !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) &&
            !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);

        public static bool HasWorldPose(LandmarkFrame frame) =>
            frame?.worldLandmarks != null && frame.worldLandmarks.Length == 21;

        // Angles require one coordinate scale. Normalized x/z use image width;
        // normalized y uses image height. Keep old packets without dimensions
        // compatible, and prefer the model's isotropic estimated world points.
        public static Vector3 PosePoint(LandmarkFrame frame, int index, bool reflectHorizontal = false)
        {
            Vector3 delta;
            if (HasWorldPose(frame)) delta = frame.worldLandmarks[index] - frame.worldLandmarks[0];
            else
            {
                delta = frame.landmarks[index] - frame.landmarks[0];
                if (frame.imageWidth > 0 && frame.imageHeight > 0)
                    delta.y *= (float)frame.imageHeight / frame.imageWidth;
            }
            return new Vector3(reflectHorizontal ? -delta.x : delta.x, -delta.y, -delta.z);
        }

        public static bool TryPalmBasis(Vector3 lateral, Vector3 towardFingers, out Quaternion basis)
        {
            basis = Quaternion.identity;
            if (!Finite(lateral) || !Finite(towardFingers) ||
                lateral.sqrMagnitude < 1e-8f || towardFingers.sqrMagnitude < 1e-8f) return false;
            var up = towardFingers.normalized;
            var side = Vector3.ProjectOnPlane(lateral.normalized, up);
            if (side.sqrMagnitude < .01f) return false; // Collapsed/collinear palm.
            basis = Quaternion.LookRotation(Vector3.Cross(side.normalized, up).normalized, up);
            return true;
        }

        public static bool HasPoseGeometry(LandmarkFrame frame)
        {
            if (!IsUsable(frame)) return false;
            return TryPalmBasis(PosePoint(frame, 5) - PosePoint(frame, 17),
                PosePoint(frame, 9), out _);
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
                Vector3 a = PosePoint(frame, mcp), b = PosePoint(frame, mcp + 1);
                Vector3 c = PosePoint(frame, mcp + 2), d = PosePoint(frame, mcp + 3);
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
