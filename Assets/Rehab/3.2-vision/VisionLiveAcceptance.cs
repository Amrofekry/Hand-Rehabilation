using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace GripRehab
{
    // Opt-in test of the real local camera lifecycle. No recorded images or
    // synthetic landmarks are used; a hand need not be present in the frame.
    public sealed class VisionLiveAcceptance : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-vision-live-qa") >= 0)
                new GameObject("Live local camera acceptance").AddComponent<VisionLiveAcceptance>();
        }

        IEnumerator Start()
        {
            yield return null; // Let all authored Start methods finish first.
            Application.runInBackground = true;
            var app = FindAnyObjectByType<ApartmentExperience>();
            var preview = FindAnyObjectByType<VisionPreviewReceiver>();
            if (app != null) { app.player.SetPaused(false); app.SetCameraMode(); }
            float deadline = Time.realtimeSinceStartup + 30;
            float streamReadyAt = -1;
            while (Time.realtimeSinceStartup < deadline)
            {
                bool streaming = app != null && app.cameraLauncher != null && preview != null &&
                                 app.CameraMode && app.cameraLauncher.CameraOpened &&
                                 preview.ReceivedPackets >= 3;
                if (streaming && streamReadyAt < 0) streamReadyAt = Time.realtimeSinceStartup;
                if (streaming && Time.realtimeSinceStartup - streamReadyAt >= 3 &&
                    app.cameraLauncher.FramesPerSecond >= 5) break;
                yield return new WaitForSecondsRealtime(.2f);
            }
            bool passed = app != null && app.cameraLauncher != null && preview != null &&
                          app.CameraMode && !app.Hardware && app.cameraLauncher.CameraOpened &&
                          app.cameraLauncher.FramesPerSecond >= 5 && preview.ReceivedPackets >= 3 &&
                          (!app.ForceValid || app.CameraGripScore >= 0);
            var result = new LiveResult
            {
                passed = passed,
                cameraState = app?.cameraLauncher?.State,
                error = app?.cameraLauncher?.LastError,
                fps = app?.cameraLauncher?.FramesPerSecond ?? 0,
                previewPackets = preview?.ReceivedPackets ?? 0,
                rightTracked = app?.vision?.RightValid ?? false,
                leftTracked = app?.vision?.LeftValid ?? false,
                readMs = app?.cameraLauncher?.CaptureReadMilliseconds ?? 0,
                inferenceMs = app?.cameraLauncher?.InferenceMilliseconds ?? 0,
                humanMotionVerified = false,
                utc = DateTime.UtcNow.ToString("O")
            };
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/vision-live-acceptance.json", JsonUtility.ToJson(result, true));
            if (passed) Debug.Log("[VISION_LIVE] PASS: physical camera, MediaPipe worker and local preview packets");
            else Debug.LogError("[VISION_LIVE] FAIL: " + result.cameraState + " " + result.error);
            Application.Quit(passed ? 0 : 1);
        }

        [Serializable] sealed class LiveResult
        {
            public bool passed;
            public string cameraState, error, utc;
            public float fps;
            public int previewPackets;
            public bool rightTracked, leftTracked;
            public bool humanMotionVerified;
            public float readMs, inferenceMs;
        }
    }
}
