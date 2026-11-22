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
        [Header("Phone camera (Android over USB, 2026-11-20)")]
        [Tooltip("Use the Android phone's camera over USB (adb + scrcpy-server) instead of the webcam. Also -vision-phone, or the saved setting PhonePrefKey.")]
        public bool usePhone;
        public string phoneFacing = "back", phoneSize = "1280x720";
        [Range(15, 240)] public int phoneFps = 60;
        /// <summary>Saved camera choice: 0 laptop webcam, 1 phone over USB, 2 phone through Windows Phone Link (2026-11-21),
        /// 3 phone through the Camo app (2026-11-21; the laptop webcam takes over when Camo is not connected). The laptop webcam is
        /// the default again (2026-11-22, the user's choice: the three standing goals are tuned on it).</summary>
        public const string PhonePrefKey = "GripRehab.CameraSource.Phone";
        public static readonly string[] SourceNames = { "LAPTOP WEBCAM", "PHONE (USB)", "PHONE (PHONE LINK)", "PHONE (CAMO)" };
        public static int SavedSource { get => Mathf.Clamp(PlayerPrefs.GetInt(PhonePrefKey, 0), 0, 3); set => PlayerPrefs.SetInt(PhonePrefKey, Mathf.Clamp(value, 0, 3)); }
        /// <summary>Tests that need a given camera index (the fault probe's missing camera 9) set this to ignore the saved choice.</summary>
        public bool forceCameraIndex;
        public bool PhoneSelected => !forceCameraIndex && (usePhone || HasArg("-vision-phone") || SavedSource == 1);
        public bool PhoneLinkSelected => !forceCameraIndex && !PhoneSelected && !CamoSelected && (HasArg("-vision-phone-link") || SavedSource == 2);
        public bool CamoSelected => !forceCameraIndex && !PhoneSelected && (HasArg("-vision-camo") || SavedSource == 3 && !HasArg("-vision-phone-link"));
        [Tooltip("Part of the Windows camera name of the phone's connected camera in Phone Link.")]
        public string phoneLinkCameraName = "Windows Virtual Camera";
        [Tooltip("Part of the Windows camera name of the Camo virtual camera (Reincubate Camo).")]
        public string camoCameraName = "Camo";
        [Tooltip("Mirror a front-facing webcam before MediaPipe so Right/Left match the patient.")]
        public bool mirror = true;
        [Tooltip("Swap MediaPipe RIGHT/LEFT labels when this webcam reports the opposite physical hand. Enabled for the current laptop camera.")]
        public bool swapHandedness;
        [Tooltip("Request a stable 30 FPS webcam stream with fixed exposure when supported. Disable if the preview becomes too dark.")]
        public bool lowLatencyCapture = true;
        [Range(-10, 0)] public float manualExposure = -5;

        public string State { get; private set; } = "OFF";
        /// <summary>Hand Lab: when set before EnsureStarted, the worker also records landmark numbers and timings (no images) here.</summary>
        public string RecordPath;
        public string LastError { get; private set; } = "";
        public float FramesPerSecond { get; private set; }
        public int DetectedHandsPerSecond { get; private set; }
        public float CaptureReadMilliseconds { get; private set; }
        public float InferenceMilliseconds { get; private set; }
        public bool IsRunning { get { try { return worker != null && !worker.HasExited; } catch { return false; } } }
        public bool CameraOpened => IsRunning && State == "camera_open";

        Process worker;
        bool darkReported;
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
            if (CamoSelected) StartCamoApp(); // Camo's camera streams only while its PC app runs
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
                    Arguments = "-u " + Quote(script) + SourceArguments() + " --model " + Quote(model) +
                                (mirror ? " --mirror" : "") + (swapHandedness ? " --swap-handedness" : "") +
                                " --preview-port 27819" + (string.IsNullOrEmpty(RecordPath) ? "" : " --record " + Quote(RecordPath)),
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
                        CaptureReadMilliseconds = status.readMs;
                        InferenceMilliseconds = status.inferenceMs;
                        if (State == "camera_error") LastError = !string.IsNullOrEmpty(status.detail) ? status.detail : "Camera " + cameraIndex + " could not open";
                        // An open camera reports a black picture (a paused phone camera app) in detail; it clears when the picture returns.
                        else if (State == "camera_open") { if (!string.IsNullOrEmpty(status.detail)) { LastError = status.detail; darkReported = true; } else if (darkReported) { LastError = ""; darkReported = false; } }
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
            CaptureReadMilliseconds = InferenceMilliseconds = 0;
        }
        string SourceArguments()
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            if (CamoSelected)
                return " --camera-name " + Quote(SafeName(camoCameraName)) + " --camera-fps 60 --fallback-webcam --fallback-camera " + cameraIndex;
            if (PhoneLinkSelected)
                return " --camera-name " + Quote(SafeName(phoneLinkCameraName));
            if (!PhoneSelected)
                return " --camera " + cameraIndex + (lowLatencyCapture ? " --low-latency --exposure " + manualExposure.ToString(c) : "");
            // Only safe values reach the command line (they are passed to the phone's shell by the bridge, which checks again).
            string facing = phoneFacing == "front" || phoneFacing == "external" ? phoneFacing : "back";
            string size = System.Text.RegularExpressions.Regex.IsMatch(phoneSize ?? "", @"^[1-9]\d{1,4}x[1-9]\d{1,4}$") ? phoneSize : "1280x720";
            return " --phone --phone-facing " + facing + " --phone-size " + size + " --phone-fps " + Mathf.Clamp(phoneFps, 15, 240).ToString(c);
        }
        static string SafeName(string name) => System.Text.RegularExpressions.Regex.Replace(name ?? "", "[^A-Za-z0-9 ()_.-]", "");

        /// <summary>Camo: the PC app (Camo Studio) must run for its camera to stream; start it if it is installed and not running.</summary>
        public static bool StartCamoApp()
        {
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    string n; try { n = p.ProcessName; } catch { continue; } finally { p.Dispose(); }
                    if (n.IndexOf("CamoStudio", StringComparison.OrdinalIgnoreCase) >= 0 || n.Equals("Camo", StringComparison.OrdinalIgnoreCase)) return true; // not CamoService (always running)
                }
                var exe = FindCamoApp();
                if (exe == null) return false;
                Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized });
                return true;
            }
            catch (Exception) { return false; }
        }
        static string FindCamoApp()
        {
            string[] roots = { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs") };
            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                foreach (var vendor in new[] { "Camo Studio", "Reincubate", "Camo" }) // 2026-11-21: C:\Program Files (x86)\Camo Studio\CamoStudio.exe
                {
                    var dir = Path.Combine(root, vendor);
                    if (!Directory.Exists(dir)) continue;
                    try
                    {
                        foreach (var file in Directory.GetFiles(dir, "Camo*.exe", SearchOption.AllDirectories))
                        {
                            var name = Path.GetFileNameWithoutExtension(file);
                            if (name.IndexOf("uninst", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("update", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("helper", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("service", StringComparison.OrdinalIgnoreCase) < 0) return file;
                        }
                    }
                    catch (Exception) { }
                }
            }
            return null;
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
            public string state, detail;
            public int camera, frames, detections, hands;
            public float fps;
            public float readMs, inferenceMs;
        }
    }
}
