using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GripRehab.Editor
{
    /// <summary>
    /// Keeps the Editor console at zero on a clean start, and keeps MCP for Unity fully configured for this project.
    ///
    /// 1. MCP for Unity (CoplayDev 10.0.0): stdio transport (the bridge starts with the Editor; .mcp.json starts the server),
    ///    setup marked complete, uvx from the project's .local\bin on drive E. Keys are the package's EditorPrefKeys.
    /// 2. Known informational messages (MCP info lines, VirtualGrasp start-up lines, the free-tier "no baked grasps"
    ///    warning, audit A-014) go to Logs/editor-info.log instead of the console. Only these exact messages are moved:
    ///    errors and exceptions are never filtered, and any other log or warning still shows.
    /// Not active in batch mode, so build and test logs stay complete.
    /// </summary>
    [InitializeOnLoad]
    static class EditorConsoleHygiene
    {
        const string McpInfo = "<b><color=#2EA3FF>MCP-FOR-UNITY</color></b>:", McpDebug = "<b><color=#6AA84F>MCP-FOR-UNITY</color></b>:";
        static readonly string[] KnownInfo = { "Initialized VirtualGrasp SDK", "[VirtualGrasp] Registered avatar", "[REHAB_VG] Native registration and both 15-joint contact bindings available" };
        static readonly string[] KnownWarnings = { "No baked grasps found. Set your grasp db in MyVirtualGrasp" };
        static readonly string LogPath = Path.GetFullPath("Logs/editor-info.log");
        static readonly object Gate = new object();

        static EditorConsoleHygiene()
        {
            if (Application.isBatchMode) return;
            ConfigureMcp();
            if (!(Debug.unityLogger.logHandler is QuietHandler)) Debug.unityLogger.logHandler = new QuietHandler(Debug.unityLogger.logHandler);
        }

        static void ConfigureMcp()
        {
            // stdio: Unity hosts the bridge (port 6400) in every domain, restarting it after each reload; Claude Code launches the
            // server from .mcp.json. The HTTP mode's one-shot auto-start is lost when Unity reloads the domain at start-up.
            SetBool("MCPForUnity.UseHttpTransport", false);
            SetBool("MCPForUnity.AutoStartOnLoad", false);
            SetBool("MCPForUnity.SetupCompleted", true);
            var uvx = Path.GetFullPath(".local/bin/uvx.exe");
            if (File.Exists(uvx) && EditorPrefs.GetString("MCPForUnity.UvxPath", "") != uvx) EditorPrefs.SetString("MCPForUnity.UvxPath", uvx);
        }

        static void SetBool(string key, bool value) { if (EditorPrefs.GetBool(key, !value) != value) EditorPrefs.SetBool(key, value); }

        static bool Known(LogType type, string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            if (type == LogType.Log)
            {
                if (message.StartsWith(McpInfo, StringComparison.Ordinal) || message.StartsWith(McpDebug, StringComparison.Ordinal)) return true;
                foreach (var k in KnownInfo) if (message.StartsWith(k, StringComparison.Ordinal)) return true;
            }
            else if (type == LogType.Warning)
                foreach (var k in KnownWarnings) if (message.StartsWith(k, StringComparison.Ordinal)) return true;
            return false;
        }

        static void Record(LogType type, string message)
        {
            try { lock (Gate) File.AppendAllText(LogPath, DateTime.Now.ToString("s") + " " + type + " " + message + Environment.NewLine); }
            catch { /* the info log is best effort; never throw from a log handler */ }
        }

        sealed class QuietHandler : ILogHandler
        {
            readonly ILogHandler inner;
            public QuietHandler(ILogHandler inner) { this.inner = inner; }
            public void LogFormat(LogType type, UnityEngine.Object context, string format, params object[] args)
            {
                string message = args == null || args.Length == 0 ? format : SafeFormat(format, args);
                if (Known(type, message)) { Record(type, message); return; }
                inner.LogFormat(type, context, format, args);
            }
            public void LogException(Exception exception, UnityEngine.Object context) => inner.LogException(exception, context);
            static string SafeFormat(string format, object[] args) { try { return string.Format(format, args); } catch (FormatException) { return format; } }
        }
    }
}
