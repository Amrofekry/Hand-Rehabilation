using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GripRehab.Editor
{
    /// <summary>
    /// Project rule: the Editor console has 0 errors, 0 warnings and 0 logs after opening the project and playing it.
    /// Opens the project in the interactive Editor, waits for the MCP for Unity bridge on port 6400 (up to 2 min), records the console counters, plays for 25 s with voice commands on
    /// (exercising the recognizers), stops, records again and exits 0 only when every counter is zero. Writes
    /// Logs/console-zero.json. The counters are the Console window's own (UnityEditor.LogEntries, internal; read by
    /// reflection). Run without -batchmode:
    /// Unity.exe -projectPath . -executeMethod GripRehab.Editor.ConsoleZeroCheck.Run -logFile Logs/console-zero.log
    /// </summary>
    [InitializeOnLoad]
    static class ConsoleZeroCheck
    {
        const string Phase = "GripRehab.ConsoleZero.Phase", Started = "GripRehab.ConsoleZero.Started", Startup = "GripRehab.ConsoleZero.Startup";
        const string VoiceKey = "GripRehab.Voice.Mode";
        const double PlaySeconds = 25;

        static ConsoleZeroCheck()
        {
            if (UnityEditor.SessionState.GetInt(Phase, 0) == 0) return;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += s => Trace(s.ToString());
            Trace("domain loaded, playing=" + EditorApplication.isPlaying);
        }

        static void Trace(string what)
        {
            try { File.AppendAllText("Logs/console-zero-trace.txt", EditorApplication.timeSinceStartup.ToString("0.0") + "s " + what + Environment.NewLine); } catch { }
        }

        public static void Run()
        {
            UnityEditor.SessionState.SetString(Startup, Counts());
            PlayerPrefs.SetInt(VoiceKey, 2); PlayerPrefs.Save(); // always on: the recognizers are built and started
            UnityEditor.SessionState.SetInt(Phase, 1); UnityEditor.SessionState.SetBool("GripRehab.ConsoleZero.Played", false);
            File.WriteAllText("Logs/console-zero-trace.txt", ""); Trace("run"); UnityEditor.SessionState.SetFloat(Started, (float)EditorApplication.timeSinceStartup);
            // Wait for MCP for Unity's HTTP server first: entering Play at once would reload the domain and drop its queued auto-start.
            UnityEditor.SessionState.SetInt(Phase, 3);
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup, started = UnityEditor.SessionState.GetFloat(Started, (float)now);
            switch (UnityEditor.SessionState.GetInt(Phase, 0))
            {
                case 3:
                    bool up = McpAnswers();
                    if (!up && now - started < 120) break;
                    UnityEditor.SessionState.SetBool("GripRehab.ConsoleZero.Mcp", up); Trace("mcp server answering before play=" + up + " after " + (now - started).ToString("0.0") + "s");
                    UnityEditor.SessionState.SetInt(Phase, 1); UnityEditor.SessionState.SetFloat(Started, (float)now);
                    EditorApplication.isPlaying = true;
                    break;
                case 1:
                    if (EditorApplication.isPlaying && !UnityEditor.SessionState.GetBool("GripRehab.ConsoleZero.Played", false)) { UnityEditor.SessionState.SetBool("GripRehab.ConsoleZero.Played", true); Trace("playing"); }
                    if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode && UnityEditor.SessionState.GetBool("GripRehab.ConsoleZero.Played", false))
                    { Trace("play ended early after " + (now - started).ToString("0.0") + "s"); UnityEditor.SessionState.SetInt(Phase, 2); UnityEditor.SessionState.SetFloat(Started, (float)now); }
                    else if (EditorApplication.isPlaying && now - started > PlaySeconds) { UnityEditor.SessionState.SetInt(Phase, 2); UnityEditor.SessionState.SetFloat(Started, (float)now); EditorApplication.isPlaying = false; }
                    else if (now - started > PlaySeconds + 120) Finish("play mode did not start");
                    break;
                case 2:
                    if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || now - started < 3) break;
                    Finish(null);
                    break;
            }
        }

        static void Finish(string problem)
        {
            Trace("finish " + (problem ?? "ok"));
            EditorApplication.update -= Tick; UnityEditor.SessionState.SetInt(Phase, 0);
            PlayerPrefs.DeleteKey(VoiceKey); PlayerPrefs.Save();
            string after = Counts(), startup = UnityEditor.SessionState.GetString(Startup, "");
            bool zero = problem == null && after == "0,0,0" && startup == "0,0,0", mcp = UnityEditor.SessionState.GetBool("GripRehab.ConsoleZero.Mcp", false) && McpAnswers();
            Trace("mcp server answering=" + mcp);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/console-zero.json", "{\n  \"utc\": \"" + DateTime.UtcNow.ToString("O") + "\",\n  \"passed\": " + (zero ? "true" : "false") +
                ",\n  \"startup\": \"" + startup + "\",\n  \"afterPlay\": \"" + after + "\",\n  \"mcpServer\": " + (mcp ? "true" : "false") +
                ",\n  \"format\": \"errors,warnings,logs\",\n  \"problem\": \"" + (problem ?? "") + "\"\n}\n");
            EditorApplication.Exit(zero ? 0 : 1);
        }

        static bool McpAnswers()
        {
            try { using (var c = new System.Net.Sockets.TcpClient()) return c.ConnectAsync("127.0.0.1", 6400).Wait(300) && c.Connected; }
            catch { return false; }
        }

        /// <summary>"errors,warnings,logs" as the Console window counts them.</summary>
        static string Counts()
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries");
            var method = type?.GetMethod("GetCountsByType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null) return "unknown";
            var args = new object[] { 0, 0, 0 };
            method.Invoke(null, args);
            return args[0] + "," + args[1] + "," + args[2];
        }
    }
}
