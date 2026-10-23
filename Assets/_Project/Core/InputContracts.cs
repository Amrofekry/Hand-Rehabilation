using System;
using UnityEngine;

namespace GripRehab
{
    public enum ForceSourceKind { Unknown, KeyboardDemo, BleBridge, UsbBridge, SyntheticReplay }
    public enum HandSide { Right, Left }

    // Versioned-facing domain sample. Timestamps are receiver-monotonic seconds,
    // never wall-clock DateTime. A source must not present stale data as zero force.
    public readonly struct ForceSample
    {
        public readonly ForceSourceKind Source;
        public readonly double MeasuredAt;
        public readonly double ReceivedAt;
        public readonly uint Sequence;
        public readonly GripShape Shape;
        public readonly long CalibrationRevision;
        public readonly float Newtons;
        public readonly bool Valid;
        public readonly bool Synthetic;

        public ForceSample(ForceSourceKind source, double measuredAt, double receivedAt,
            uint sequence, GripShape shape, long calibrationRevision, float newtons,
            bool valid, bool synthetic)
        {
            Source = source;
            MeasuredAt = measuredAt;
            ReceivedAt = receivedAt;
            Sequence = sequence;
            Shape = shape;
            CalibrationRevision = calibrationRevision;
            Newtons = newtons;
            Synthetic = synthetic;
            Valid = valid && source != ForceSourceKind.Unknown &&
                    !double.IsNaN(measuredAt) && !double.IsInfinity(measuredAt) &&
                    !double.IsNaN(receivedAt) && !double.IsInfinity(receivedAt) &&
                    measuredAt <= receivedAt &&
                    !float.IsNaN(newtons) && !float.IsInfinity(newtons) && newtons >= 0 &&
                    ((source == ForceSourceKind.KeyboardDemo || source == ForceSourceKind.SyntheticReplay) == synthetic);
        }

        public bool IsFresh(double now) => Valid && !double.IsNaN(now) &&
            !double.IsInfinity(now) && now >= ReceivedAt && now >= MeasuredAt &&
            now - MeasuredAt < 0.5;
    }

    public interface IRehabForceSource
    {
        ForceSourceKind Kind { get; }
        bool TryGetSample(double now, out ForceSample sample);
    }

    // Image-normalized coordinates are carried without reallocation. Consumers
    // must not mutate Landmarks. Closure estimates are dimensionless, not Newtons.
    public readonly struct HandSample
    {
        public readonly HandSide Side;
        public readonly Vector3[] Landmarks;
        public readonly double CapturedAt;
        public readonly double ReceivedAt;
        public readonly long Sequence;
        public readonly float Confidence;
        public readonly bool Valid;

        public HandSample(HandSide side, Vector3[] landmarks, double capturedAt,
            double receivedAt, long sequence, float confidence, bool valid)
        {
            Side = side;
            Landmarks = landmarks;
            CapturedAt = capturedAt;
            ReceivedAt = receivedAt;
            Sequence = sequence;
            Confidence = confidence;
            Valid = valid && landmarks != null && landmarks.Length == 21 &&
                    !double.IsNaN(capturedAt) && !double.IsInfinity(capturedAt) &&
                    !double.IsNaN(receivedAt) && !double.IsInfinity(receivedAt) &&
                    capturedAt <= receivedAt &&
                    !float.IsNaN(confidence) && confidence >= 0.5f && confidence <= 1f;
        }

        public bool IsFresh(double now) => Valid && !double.IsNaN(now) &&
            !double.IsInfinity(now) && now >= CapturedAt && now - CapturedAt < 0.5;
    }

    public interface IHandTracker
    {
        bool TryGetHand(HandSide side, double now, out HandSample sample);
    }
}
