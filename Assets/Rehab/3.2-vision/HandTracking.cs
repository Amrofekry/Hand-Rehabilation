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

    // Per-hand packet ordering. While a stream is live, older or repeated packets are
    // rejected. Once it has gone stale, a restarted worker (sequence back at 1) is a new
    // stream rather than a replay, so its first packet is accepted.
    public static class HandStreamOrder
    {
        public static bool Accepts(long sequence, double capture, long lastSequence, double lastCapture, bool lastStale)
        {
            if (lastStale) return true;
            if (capture > 0 && lastCapture > 0 && capture <= lastCapture) return false;
            if (sequence > 0 && lastSequence > 0 && sequence <= lastSequence) return false;
            return true;
        }
    }

    public enum HandGesture { None = -1, Open = 0, Fist = 1, Point = 2, Pinch = 3 }

    // Stage 3.2: a small geometric classifier on MediaPipe's 21 image landmarks. It is a
    // game input, not a validated gesture recognizer; ambiguous poses return None.
    public static class GestureClassifier
    {
        static float Distance(Vector3 a, Vector3 b) => Mathf.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));

        public static HandGesture Classify(LandmarkFrame frame)
        {
            if (frame == null || frame.landmarks == null || frame.landmarks.Length != 21) return HandGesture.None;
            var p = frame.landmarks; var wrist = p[0];
            float palm = Distance(wrist, p[9]);
            if (!(palm > 1e-4f)) return HandGesture.None;
            if (Distance(p[4], p[8]) < .3f * palm) return HandGesture.Pinch;
            int extended = 0; bool index = false;
            for (int finger = 0; finger < 4; finger++)
            {
                int pip = 6 + finger * 4, tip = 8 + finger * 4;
                bool straight = Distance(wrist, p[tip]) > Distance(wrist, p[pip]) * 1.1f;
                if (straight) { extended++; if (finger == 0) index = true; }
            }
            if (extended == 4) return HandGesture.Open;
            if (extended == 0) return HandGesture.Fist;
            if (extended == 1 && index) return HandGesture.Point;
            return HandGesture.None;
        }
    }

    // Accepts a gesture after it is held steadily; the same gesture again needs a change first.
    public sealed class GestureStabilizer
    {
        public float HoldSeconds = .4f;
        HandGesture candidate = HandGesture.None, lastEmitted = HandGesture.None;
        float held; bool armed = true;

        public void Reset() { candidate = lastEmitted = HandGesture.None; held = 0; armed = true; }

        public bool Step(HandGesture gesture, float deltaSeconds, out HandGesture accepted)
        {
            accepted = HandGesture.None;
            if (gesture != candidate) { candidate = gesture; held = 0; }
            if (gesture == HandGesture.None || gesture != lastEmitted) armed = true;
            if (gesture == HandGesture.None || !armed) return false;
            held += Mathf.Min(Mathf.Max(0, deltaSeconds), .1f);
            if (held < HoldSeconds) return false;
            accepted = gesture; lastEmitted = gesture; armed = false; held = 0;
            return true;
        }
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
