using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GripRehab.Editor
{
    // Explicit opt-in only. Does not create/save/regenerate any scene or use real profiles.
    [InitializeOnLoad]
    public static class ResearchJourneyQA
    {
        const string Active = "GripRehab.JourneyQA", Cycle = "GripRehab.JourneyQA.Cycle";
        const string Pending = "GripRehab.JourneyQA.Pending", Passed = "GripRehab.JourneyQA.Passed";
        static double deadline;
        static ResearchJourneyQA()
        {
            EditorApplication.playModeStateChanged += State;
            EditorApplication.update += Tick;
            if (UnityEditor.SessionState.GetBool(Active, false)) deadline = EditorApplication.timeSinceStartup + 180;
        }
        [MenuItem("Grip Rehab/Run Account Journey Tests (3 cycles)")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Leave Play and wait for compilation before journey QA.");
            if (EditorSceneManager.GetActiveScene().path != ApartmentAuthoring.ScenePath)
                throw new InvalidOperationException("Open the authored GripRehab main scene first; this test does not replace your scene.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/journey-editor-qa.txt", "Native journey QA " + DateTime.UtcNow.ToString("O") + "\n");
            UnityEditor.SessionState.SetBool(Active, true); UnityEditor.SessionState.SetInt(Cycle, 0);
            UnityEditor.SessionState.SetBool(Pending, true); UnityEditor.SessionState.SetBool(Passed, false);
            deadline = EditorApplication.timeSinceStartup + 180;
        }
        static void State(PlayModeStateChange state)
        {
            if (!UnityEditor.SessionState.GetBool(Active, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) { deadline = EditorApplication.timeSinceStartup + 180; return; }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            try
            {
                if (!UnityEditor.SessionState.GetBool(Passed, false)) throw new Exception("Journey failed or Play was stopped before completion; see journey-acceptance.json.");
                foreach (int port in new[] { 27817, 27818, 27819 }) using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, port))) { }
                int cycle = UnityEditor.SessionState.GetInt(Cycle, 0) + 1; UnityEditor.SessionState.SetInt(Cycle, cycle);
                File.Copy("Logs/journey-acceptance.json", "Logs/journey-cycle-" + cycle + ".json", true);
                File.AppendAllText("Logs/journey-editor-qa.txt", "PASS cycle " + cycle + ": journey and UDP cleanup\n");
                if (cycle < 3) { UnityEditor.SessionState.SetBool(Pending, true); deadline = EditorApplication.timeSinceStartup + 180; }
                else Finish(true, "Three native journey Play/Stop cycles and UDP cleanup passed.");
            }
            catch (Exception error) { Finish(false, error.Message); }
        }
        static void Tick()
        {
            if (!UnityEditor.SessionState.GetBool(Active, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Native journey timeout."); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode && UnityEditor.SessionState.GetBool(Pending, false))
            {
                UnityEditor.SessionState.SetBool(Pending, false); UnityEditor.SessionState.SetBool(Passed, false);
                EditorApplication.EnterPlaymode(); return;
            }
            if (EditorApplication.isPlaying && ResearchJourneyAcceptance.Finished)
            {
                UnityEditor.SessionState.SetBool(Passed, ResearchJourneyAcceptance.Passed);
                EditorApplication.ExitPlaymode();
            }
        }
        static void Finish(bool passed, string detail)
        {
            UnityEditor.SessionState.SetBool(Active, false); UnityEditor.SessionState.SetBool(Pending, false);
            File.AppendAllText("Logs/journey-editor-qa.txt", (passed ? "PASS: " : "FAIL: ") + detail + "\n");
            RehabGateRunner.CompleteJourneyQa(passed, detail);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }
    }
}
