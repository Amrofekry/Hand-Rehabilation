using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace GripRehab
{
    [Serializable]
    public sealed class LandmarkFrame
    {
        public int schemaVersion;
        public double captureSeconds;
        public float confidence;
        public string source;
        public string gesture;
        public float gestureConfidence;
        public Vector3[] landmarks;
    }

    public static class HandKinematics
    {
        // MediaPipe's normalized image coordinates have y down; Unity's viewport has y up.
        public static Vector3 ToViewport(Vector3 normalizedImagePoint) =>
            new Vector3(normalizedImagePoint.x, 1f - normalizedImagePoint.y, normalizedImagePoint.z);

        public static bool IsUsable(LandmarkFrame frame, float minConfidence = 0.65f)
        {
            if (frame == null || frame.schemaVersion != 1 || frame.landmarks == null ||
                frame.landmarks.Length != 21 || frame.confidence < minConfidence ||
                float.IsNaN(frame.confidence) || frame.confidence > 1f) return false;
            foreach (var p in frame.landmarks)
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                    float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z) ||
                    p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1) return false;
            return true;
        }

        public static float PinchDistance(LandmarkFrame frame)
        {
            if (!IsUsable(frame)) return float.NaN;
            return Vector2.Distance(frame.landmarks[4], frame.landmarks[8]);
        }
    }

}
