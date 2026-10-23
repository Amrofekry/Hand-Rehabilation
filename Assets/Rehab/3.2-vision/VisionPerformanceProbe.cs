using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GripRehab
{
    // Opt-in performance comparison on the same scene and machine. No image data is saved.
    public sealed class VisionPerformanceProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-vision-perf-qa") >= 0)
                new GameObject("Vision performance probe").AddComponent<VisionPerformanceProbe>();
        }

        IEnumerator Start()
        {
            yield return null;
            Application.runInBackground = true;
            var app = FindAnyObjectByType<ApartmentExperience>();
            var preview = FindAnyObjectByType<VisionPreviewReceiver>();
            if (app != null) { app.player.SetPaused(false); app.SetCameraMode(); }
            float deadline = Time.realtimeSinceStartup + 25;
            while (app != null && !app.cameraLauncher.CameraOpened && Time.realtimeSinceStartup < deadline)
                yield return new WaitForSecondsRealtime(.2f);
            if (app == null || preview == null || !app.cameraLauncher.CameraOpened)
            {
                Debug.LogError("[VISION_PERF] Camera did not open");
                Application.Quit(1);
                yield break;
            }
            app.player.SetPaused(false);
            yield return new WaitForSecondsRealtime(.5f);
            var cameraPreview = new Sample { name = "camera_preview" };
            yield return Measure(app, cameraPreview, 5);
            preview.visible = false;
            var cameraHidden = new Sample { name = "camera_hidden" };
            yield return Measure(app, cameraHidden, 5);
            app.SetHardware(false);
            var keyboardDemo = new Sample { name = "keyboard_demo" };
            yield return Measure(app, keyboardDemo, 5);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/vision-performance.json", JsonUtility.ToJson(new Report {
                utc = DateTime.UtcNow.ToString("O"), graphics = SystemInfo.graphicsDeviceName,
                width = Screen.width, height = Screen.height,
                samples = new[] { cameraPreview, cameraHidden, keyboardDemo }
            }, true));
            Debug.Log("[VISION_PERF] PASS: camera preview, camera hidden, keyboard comparison");
            Application.Quit(0);
        }

        static IEnumerator Measure(ApartmentExperience app, Sample sample, float seconds)
        {
            var frames = new List<float>();
            var ages = new List<float>();
            long lastSequence = -1; bool lastLeft = false;
            float readMs = 0, inferenceMs = 0;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000);
                readMs += app.cameraLauncher.CaptureReadMilliseconds;
                inferenceMs += app.cameraLauncher.InferenceMilliseconds;
                var hand = app.ActiveHand;
                if (app.CameraMode && hand.TrackingUsed &&
                    (hand.LastPoseSequence != lastSequence || app.LeftSelected != lastLeft))
                {
                    var age = app.LeftSelected ? app.vision.LeftAgeSeconds : app.vision.RightAgeSeconds;
                    if (!double.IsInfinity(age)) ages.Add((float)age * 1000);
                    lastSequence = hand.LastPoseSequence; lastLeft = app.LeftSelected;
                    if (app.LeftSelected) sample.leftPoseFrames++; else sample.rightPoseFrames++;
                    if (hand.UsingWorldLandmarks) sample.worldPoseFrames++;
                }
            }
            frames.Sort();
            sample.frames = frames.Count;
            sample.fps = frames.Count / Math.Max(.001f, Time.realtimeSinceStartup - start);
            sample.meanMs = 1000f / Math.Max(.01f, sample.fps);
            sample.p95Ms = frames[Mathf.Clamp(Mathf.CeilToInt(.95f * frames.Count) - 1, 0, frames.Count - 1)];
            sample.maxMs = frames[frames.Count - 1];
            sample.captureReadMs = readMs / frames.Count;
            sample.inferenceMs = inferenceMs / frames.Count;
            sample.poseSamples = ages.Count;
            sample.sourceToPoseMeanMs = sample.sourceToPoseP95Ms = -1;
            if (ages.Count > 0)
            {
                ages.Sort(); float total = 0; foreach (var age in ages) total += age;
                sample.sourceToPoseMeanMs = total / ages.Count;
                sample.sourceToPoseP95Ms = ages[Mathf.Clamp(Mathf.CeilToInt(.95f * ages.Count) - 1, 0, ages.Count - 1)];
            }
            Debug.Log("[VISION_PERF] " + sample.name + " fps=" + sample.fps.ToString("F1") +
                      " p95ms=" + sample.p95Ms.ToString("F1") + " maxms=" + sample.maxMs.ToString("F1"));
        }

        [Serializable] sealed class Report
        {
            public string utc, graphics;
            public int width, height;
            public Sample[] samples;
        }
        [Serializable] sealed class Sample
        {
            public string name;
            public int frames;
            public float fps, meanMs, p95Ms, maxMs;
            public float captureReadMs, inferenceMs, sourceToPoseMeanMs, sourceToPoseP95Ms;
            public int poseSamples, rightPoseFrames, leftPoseFrames, worldPoseFrames;
        }
    }
}
