using System;
using System.IO;
using UnityEngine;

namespace GripRehab
{
    /// <summary>
    /// Opt-in latency report (2026-11-02): <c>-vision-latency &lt;seconds&gt;</c> measures, for every new hand frame, the time
    /// from camera capture (the bridge's capture clock and its ageMs) to the frame the pose is applied in, then writes
    /// Logs/vision-latency.json and quits. It splits the bridge part (capture to send, mostly inference) from the Unity part
    /// (receive to applied). It does not include camera exposure or display time, which need a physical reference.
    /// </summary>
    [DefaultExecutionOrder(200)] // after FingerPoseDriver has applied this frame's pose
    public sealed class VisionLatencyMeter : MonoBehaviour
    {
        public readonly LatencyStats Total = new LatencyStats(), Bridge = new LatencyStats(), Unity = new LatencyStats(), Interval = new LatencyStats();
        VisionUdpReceiver vision; float seconds; double firstAt = -1, lastArrival = -1; long lastRight = -1, lastLeft = -1; string source = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-vision-latency");
            if (at < 0) return;
            var meter = new GameObject("Vision latency meter (-vision-latency)").AddComponent<VisionLatencyMeter>();
            meter.seconds = at + 1 < args.Length && float.TryParse(args[at + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? Mathf.Clamp(s, 5, 600) : 30;
            Application.runInBackground = true;
        }

        void LateUpdate()
        {
            if (vision == null) { vision = FindAnyObjectByType<VisionUdpReceiver>(); if (vision == null) return; }
            double now = Time.realtimeSinceStartupAsDouble;
            Sample(vision.LatestRight, vision.RightAgeSeconds, ref lastRight, now);
            Sample(vision.LatestLeft, vision.LeftAgeSeconds, ref lastLeft, now);
            if (firstAt >= 0 && now - firstAt >= seconds || firstAt < 0 && Time.realtimeSinceStartup > seconds + 60) Finish();
        }

        void Sample(LandmarkFrame frame, double ageSeconds, ref long lastSequence, double now)
        {
            if (frame == null || frame.sequence == lastSequence || double.IsInfinity(ageSeconds)) return;
            lastSequence = frame.sequence;
            if (firstAt < 0) firstAt = now;
            float total = (float)(ageSeconds * 1000), bridge = frame.ageMs;
            Total.Add(total); Bridge.Add(bridge); Unity.Add(total - bridge);
            double arrival = now - ageSeconds;
            if (lastArrival >= 0) Interval.Add((float)((arrival - lastArrival) * 1000));
            lastArrival = arrival; source = frame.source;
        }

        void Finish()
        {
            var r = new Report
            {
                scope = "Capture (bridge clock) to applied pose; excludes camera exposure and display. Input described by the caller.",
                frameSource = source, samples = Total.Count, seconds = seconds,
                totalMedianMs = Total.Median, totalP95Ms = Total.Percentile(95), bridgeMedianMs = Bridge.Median, bridgeP95Ms = Bridge.Percentile(95),
                unityMedianMs = Unity.Median, unityP95Ms = Unity.Percentile(95), frameIntervalMedianMs = Interval.Median,
                passed = Total.Count >= 30, utc = DateTime.UtcNow.ToString("O")
            };
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/vision-latency.json", JsonUtility.ToJson(r, true));
            enabled = false; Application.Quit(r.passed ? 0 : 1);
        }

        [Serializable] sealed class Report
        {
            public string scope, frameSource, utc; public int samples; public float seconds;
            public float totalMedianMs, totalP95Ms, bridgeMedianMs, bridgeP95Ms, unityMedianMs, unityP95Ms, frameIntervalMedianMs;
            public bool passed;
        }
    }
}
