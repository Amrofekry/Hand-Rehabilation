using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using GripRehab.Composition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GripRehab.Editor
{
    // Native assembly/lifecycle smoke test. Does not claim to test camera,
    // focused input, profiles or medical outcomes. No test input is injected.
    [InitializeOnLoad]
    public static class SceneCompositionQA
    {
        const string Active = "GripRehab.SceneQA.Active", Cycle = "GripRehab.SceneQA.Cycle", Checked = "GripRehab.SceneQA.Checked";
        static double deadline, settle;
        static SceneCompositionQA()
        {
            if (UnityEditor.SessionState.GetBool(Active, false)) { deadline = EditorApplication.timeSinceStartup + 90; settle = EditorApplication.timeSinceStartup + 3; }
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += State;
            Application.logMessageReceived += (text, stack, kind) => {
                if (UnityEditor.SessionState.GetBool(Active, false) && (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert))
                    UnityEditor.SessionState.SetInt("GripRehab.SceneQA.Errors", UnityEditor.SessionState.GetInt("GripRehab.SceneQA.Errors", 0) + 1);
            };
        }
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) throw new OperationCanceledException();
            SceneOrganization.ConfigureBuildScenes();
            UnityEditor.SessionState.SetInt(Cycle, 0); UnityEditor.SessionState.SetInt("GripRehab.SceneQA.Errors", 0);
            File.WriteAllText("Logs/scene-play-cycles.txt", "Native scene assembly QA " + DateTime.UtcNow.ToString("O") + "\n");
            UnityEditor.SessionState.SetBool(Active, true); Next();
        }
        static void Next()
        {
            // Test the shipped entry path, not only an already-bound Editor workspace.
            EditorSceneManager.OpenScene(SceneComposition.Main);
            UnityEditor.SessionState.SetBool(Checked, false); deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if (!UnityEditor.SessionState.GetBool(Active, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) { deadline = EditorApplication.timeSinceStartup + 90; settle = EditorApplication.timeSinceStartup + 3; }
            if (state != PlayModeStateChange.EnteredEditMode) return;
            try
            {
                if (!UnityEditor.SessionState.GetBool(Checked, false) || UnityEditor.SessionState.GetInt("GripRehab.SceneQA.Errors", 0) != 0)
                    throw new Exception("Native scene checks failed or runtime logged errors.");
                foreach (int port in new[] { 27817, 27818, 27819 }) using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, port))) { }
                int cycle = UnityEditor.SessionState.GetInt(Cycle, 0) + 1; UnityEditor.SessionState.SetInt(Cycle, cycle);
                File.AppendAllText("Logs/scene-play-cycles.txt", "PASS cycle " + cycle + ": scene startup, bindings, single instances, SDK, both rooms/hands, UI and socket cleanup\n");
                if (cycle == 3) Finish(true, "Three cold scene-load Play/Stop cycles and socket cleanup passed.");
                else EditorApplication.delayCall += Next;
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Tick()
        {
            if (!UnityEditor.SessionState.GetBool(Active, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Scene startup timed out."); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < settle) return;
            try
            {
                if (!UnityEditor.SessionState.GetBool(Checked, false))
                {
                    var layout = SceneComposition.Current;
                    var boot = UnityEngine.Object.FindAnyObjectByType<SceneBootstrap>();
                    Require(layout == null ? boot == null && SceneManager.sceneCount == 1 : boot != null && boot.Ready && layout.Loaded && SceneManager.sceneCount == 7, "Working scene did not load with the expected architecture.");
                    var app = UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
                    Require(app != null && app.statusText != null && app.forceText != null && app.objects.All(o => o != null) && app.seats.All(s => s != null), "Gameplay cross-scene references missing.");
                    Require(app.virtualGrasp.Ready && app.contactRig != null, "Native VirtualGrasp/contact rig not ready.");
                    Require(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1 && UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "Duplicate camera/EventSystem.");
                    Require(UnityEngine.Object.FindObjectsByType<LiveForceReceiver>(FindObjectsSortMode.None).Length == 1 && UnityEngine.Object.FindObjectsByType<VisionUdpReceiver>(FindObjectsSortMode.None).Length == 1 && UnityEngine.Object.FindObjectsByType<VisionPreviewReceiver>(FindObjectsSortMode.None).Length == 1, "Duplicate receivers.");
                    app.SetHardware(false); app.SelectHand(true);
                    Require(app.virtualGrasp.outputMesh.sharedMesh == app.virtualGrasp.leftOnlyMesh && app.contactRig.pose == app.leftHand, "Left SDK hand binding failed.");
                    app.SelectHand(false);
                    Require(app.virtualGrasp.outputMesh.sharedMesh == app.virtualGrasp.rightOnlyMesh && app.contactRig.pose == app.rightHand, "Right SDK hand binding failed.");
                    Require(app.choices.SelectStation(true) && app.choices.Kitchen && app.choices.SelectedObject != null, "Kitchen bindings missing.");
                    Require(app.choices.SelectStation(false) && !app.choices.Kitchen && app.choices.SelectedObject != null, "Living/seat bindings missing.");
                    Require(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Any(b => b.IsInteractable()), "No usable UI controls.");
                    Require(app.objects.All(o => o.profile != null && o.visual != null), "Material/visual assets missing.");
                    var selected = app.choices.SelectedObject;
                    Require(!selected.profile.deformable || selected.HasLocalizedDeformation, "Selected deformable object did not initialize its mesh.");
                    UnityEditor.SessionState.SetBool(Checked, true); settle = EditorApplication.timeSinceStartup + 2;
                    if (UnityEditor.SessionState.GetInt(Cycle, 0) == 0) ScreenCapture.CaptureScreenshot("Logs/scene-composition-menu.png");
                }
                else EditorApplication.ExitPlaymode();
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
        static void Finish(bool passed, string detail)
        {
            UnityEditor.SessionState.SetBool(Active, false);
            File.WriteAllText("Logs/scene-play-result.json", JsonUtility.ToJson(new Result { passed = passed,
                cycles = UnityEditor.SessionState.GetInt(Cycle, 0), detail = detail, utc = DateTime.UtcNow.ToString("O") }, true));
            RehabGateRunner.CompleteSceneQa(passed, detail);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            else EditorApplication.delayCall += SceneOrganization.OpenWorkspace;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
        [Serializable] sealed class Result { public bool passed; public int cycles; public string detail, utc; }
    }
}
