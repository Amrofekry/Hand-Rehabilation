using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GripRehab.Product;
using UnityEngine;

namespace GripRehab
{
    /// <summary>
    /// Stage 7.3: rolling log in %LOCALAPPDATA%/GripRehab/logs, capture of unhandled exceptions,
    /// and a diagnostics summary for the "copy diagnostics" button. Only app and device facts and
    /// recent log lines are included: no names, records, images or the computer's name.
    /// </summary>
    public sealed class RehabDiagnostics : MonoBehaviour
    {
        static DiagnosticsLog log;
        static RehabDiagnostics instance;
        static readonly DateTime started = DateTime.UtcNow;
        float smoothedFrame = 1 / 60f;
        public static string LogFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GripRehab", "logs");
        public static string LogFile => log?.FilePath ?? "";
        public static int ErrorCount => log?.Errors ?? 0;
        public static int ExceptionCount => log?.Exceptions ?? 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (log != null) return;
            try { log = new DiagnosticsLog(LogFolder, DateTime.UtcNow); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException) { Debug.LogWarning("Diagnostics log unavailable: " + error.Message); return; }
            Application.logMessageReceivedThreaded += OnLog;
            AppDomain.CurrentDomain.UnhandledException += (_, e) => log?.Write(DateTime.UtcNow, "Exception", "UNHANDLED " + e.ExceptionObject);
            TaskScheduler.UnobservedTaskException += (_, e) => { log?.Write(DateTime.UtcNow, "Exception", "UNOBSERVED TASK " + e.Exception); e.SetObserved(); };
            Application.quitting += () => { log?.Write(DateTime.UtcNow, "Info", "quit after " + (DateTime.UtcNow - started).TotalSeconds.ToString("0") + " s"); log?.Dispose(); };
            log.Write(DateTime.UtcNow, "Info", Facts().Replace("\n", " · "));
            var go = new GameObject("Rehab diagnostics"); DontDestroyOnLoad(go); instance = go.AddComponent<RehabDiagnostics>();
        }

        static void OnLog(string message, string stack, LogType type)
        {
            string level = type == LogType.Exception ? "Exception" : type == LogType.Error || type == LogType.Assert ? "Error" : type == LogType.Warning ? "Warning" : "Info";
            log?.Write(DateTime.UtcNow, level, message, stack);
        }

        void Update() { smoothedFrame = Mathf.Lerp(smoothedFrame, Mathf.Clamp(Time.unscaledDeltaTime, 0, 1), .05f); }

        static string Facts() =>
            "GripRehab " + Application.version + " · Unity " + Application.unityVersion + "\n" +
            "OS " + SystemInfo.operatingSystem + "\nCPU " + SystemInfo.processorType + " (" + SystemInfo.processorCount + " threads) · RAM " + SystemInfo.systemMemorySize + " MB\n" +
            "GPU " + SystemInfo.graphicsDeviceName + " · " + SystemInfo.graphicsDeviceType + " · VRAM " + SystemInfo.graphicsMemorySize + " MB\n" +
            "Screen " + Screen.width + "x" + Screen.height + " · quality " + QualitySettings.names[QualitySettings.GetQualityLevel()];

        /// <summary>Plain-text diagnostics for support: device facts, health counters and recent log lines.</summary>
        public static string Summary()
        {
            var text = new StringBuilder("GripRehab diagnostics (no personal records, names or images)\n");
            text.Append(Facts()).Append('\n');
            text.Append("Uptime ").Append((DateTime.UtcNow - started).TotalMinutes.ToString("0.0")).Append(" min · frame ")
                .Append(instance != null ? (instance.smoothedFrame * 1000).ToString("0.0") + " ms" : "n/a")
                .Append(" · errors ").Append(ErrorCount).Append(" · exceptions ").Append(ExceptionCount).Append('\n');
            text.Append("Log file: ").Append(DiagnosticsLog.Scrub(LogFile)).Append("\nRecent log lines:\n");
            if (log != null) foreach (var line in log.Recent()) text.Append(line).Append('\n');
            return text.ToString();
        }

        /// <summary>Copies <see cref="Summary"/> to the clipboard; returns false when that is not possible.</summary>
        public static bool CopyToClipboard()
        {
            try { GUIUtility.systemCopyBuffer = Summary(); return true; }
            catch (Exception error) { Debug.LogWarning("Could not copy diagnostics: " + error.Message); return false; }
        }
    }
}
