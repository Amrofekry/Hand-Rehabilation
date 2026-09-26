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
            Application.runInBackground = true;
            var app = FindAnyObjectByType<ApartmentExperience>();
            var preview = FindAnyObjectByType<VisionPreviewReceiver>();
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
            yield return Measure(cameraPreview, 5);
            preview.visible = false;
            var cameraHidden = new Sample { name = "camera_hidden" };
            yield return Measure(cameraHidden, 5);
            app.SetHardware(false);
            var keyboardDemo = new Sample { name = "keyboard_demo" };
            yield return Measure(keyboardDemo, 5);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/vision-performance.json", JsonUtility.ToJson(new Report {
                utc = DateTime.UtcNow.ToString("O"), graphics = SystemInfo.graphicsDeviceName,
                width = Screen.width, height = Screen.height,
                samples = new[] { cameraPreview, cameraHidden, keyboardDemo }
            }, true));
            Debug.Log("[VISION_PERF] PASS: camera preview, camera hidden, keyboard comparison");
            Application.Quit(0);
        }

        static IEnumerator Measure(Sample sample, float seconds)
        {
            var frames = new List<float>();
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000);
            }
            frames.Sort();
            sample.frames = frames.Count;
            sample.fps = frames.Count / Math.Max(.001f, Time.realtimeSinceStartup - start);
            sample.meanMs = 1000f / Math.Max(.01f, sample.fps);
            sample.p95Ms = frames[Mathf.Clamp(Mathf.CeilToInt(.95f * frames.Count) - 1, 0, frames.Count - 1)];
            sample.maxMs = frames[frames.Count - 1];
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
        }
    }
}
