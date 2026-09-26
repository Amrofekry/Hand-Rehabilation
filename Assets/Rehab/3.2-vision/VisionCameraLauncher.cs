using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace GripRehab
{
    // Owns exactly one local webcam/MediaPipe worker for each Play session.
    // No video leaves the machine; the worker sends landmarks over loopback.
    public sealed class VisionCameraLauncher : MonoBehaviour
    {
        [Header("Local camera")]
        public bool autoStartOnPlay = false;
        [Min(0)] public int cameraIndex = 0;
        [Tooltip("Mirror a front-facing webcam before MediaPipe so Right/Left match the patient.")]
        public bool mirror = true;
        public bool swapHandedness;

        public string State { get; private set; } = "OFF";
        public string LastError { get; private set; } = "";
        public float FramesPerSecond { get; private set; }
        public int DetectedHandsPerSecond { get; private set; }
        public bool IsRunning { get { try { return worker != null && !worker.HasExited; } catch { return false; } } }
        public bool CameraOpened => IsRunning && State == "camera_open";

        Process worker;
        readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
        string projectRoot;

        static bool HasArg(string flag) => Array.IndexOf(Environment.GetCommandLineArgs(), flag) >= 0;
        void Start()
        {
            if (autoStartOnPlay && !HasArg("-apartment-smoke") && (!Application.isBatchMode || HasArg("-vision-live-qa")))
                EnsureStarted();

        }

        public void EnsureStarted()
        {
            if (IsRunning) return;
            StopWorker();
            projectRoot = FindProjectRoot();
            if (projectRoot == null) { Fail("Tools/Vision not found next to the project/build"); return; }
            var python = Path.Combine(projectRoot, ".venv", "Scripts", "python.exe");
            var script = Path.Combine(projectRoot, "Tools", "Vision", "mediapipe_bridge.py");
            var model = Path.Combine(projectRoot, "models", "hand_landmarker.task");
            if (!File.Exists(python) || !File.Exists(script) || !File.Exists(model))
            {
                Fail("Vision Python environment, script or model is missing; see Docs/HANDS_AND_MEDIAPIPE_AR.md");
                return;
            }
            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = "-u " + Quote(script) + " --camera " + cameraIndex + " --model " + Quote(model) +
                                (mirror ? " --mirror" : "") + (swapHandedness ? " --swap-handedness" : "") + " --preview-port 27819",
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                worker = new Process { StartInfo = info };
                worker.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) messages.Enqueue(e.Data); };
                worker.ErrorDataReceived += (_, e) => {
                    if (!string.IsNullOrEmpty(e.Data) &&
                        (e.Data.Contains("Traceback") || e.Data.Contains("Error") || e.Data.Contains("Could not open")))
                        messages.Enqueue("VISION_ERROR " + e.Data);
                };
                if (!worker.Start()) { Fail("Python vision process could not start"); return; }
                worker.BeginOutputReadLine();
                worker.BeginErrorReadLine();
                State = "STARTING";
                LastError = "";
                FramesPerSecond = 0;
                DetectedHandsPerSecond = 0;
            }
            catch (Exception error) { StopWorker(); Fail(error.GetType().Name + ": " + error.Message); }
        }

        void Update()
        {
            while (messages.TryDequeue(out var line))
            {
                if (line.StartsWith("VISION_STATUS ", StringComparison.Ordinal))
                {
                    try
                    {
                        var status = JsonUtility.FromJson<CameraStatus>(line.Substring(14));
                        State = status.state ?? "UNKNOWN";
                        FramesPerSecond = status.fps;
                        DetectedHandsPerSecond = status.hands;
                        if (State == "camera_error") LastError = "Camera " + cameraIndex + " could not open";
                    }
                    catch (ArgumentException) { LastError = "Malformed vision status"; }
                }
                else if (line.StartsWith("VISION_ERROR ", StringComparison.Ordinal)) LastError = line.Substring(13);
            }
            if (worker != null && !IsRunning && State != "OFF" && State != "OFF (TEST)")
            {
                State = "FAILED";
                if (string.IsNullOrEmpty(LastError)) LastError = "Camera worker exited; check camera access and Python dependencies";
            }
        }

        public void StopWorker()
        {
            if (worker != null)
            {
                try { if (!worker.HasExited) { worker.Kill(); worker.WaitForExit(1500); } }
                catch (Exception error) { UnityEngine.Debug.LogWarning("[VISION_CAMERA] Worker stop: " + error.Message); }
                worker.Dispose();
                worker = null;
            }
            while (messages.TryDequeue(out _)) { }
            State = "OFF";
            FramesPerSecond = 0;
            DetectedHandsPerSecond = 0;
        }
        void OnDisable() { StopWorker(); }
        void OnApplicationQuit() { StopWorker(); }
        void Fail(string reason) { State = "FAILED"; LastError = reason; UnityEngine.Debug.LogError("[VISION_CAMERA] " + reason); }

        static string FindProjectRoot()
        {
            var dir = new DirectoryInfo(Application.dataPath);
            for (int i = 0; dir != null && i < 6; i++, dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Tools", "Vision", "mediapipe_bridge.py"))) return dir.FullName;
            return null;
        }
        static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

        [Serializable] sealed class CameraStatus
        {
            public string state;
            public int camera, frames, detections, hands;
            public float fps;
        }
    }
}
