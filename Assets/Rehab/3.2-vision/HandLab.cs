using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace GripRehab
{
    /// <summary>
    /// Hand Lab (opt-in: -apartment-hand-lab). A guided two-minute session that measures how the real camera hand maps
    /// into the game: hold still, move left/right, up/down, toward/away, quick moves, open/close, then grab three coffee
    /// table items. Live (default): starts the webcam worker with recording on (landmark numbers and timings only, never
    /// an image). Replay (-hand-lab-replay): no webcam; Tools/hand_replay.py feeds a recorded session over UDP on the same
    /// timeline. Every frame of the game side goes to game.csv: frame timing, raw wrist and palm size, the mapped target
    /// before filtering, the virtual hand, grip score, gate and what is held. Tools/hand_lab_report.py analyses it.
    /// Diagnostics only; nothing here changes how the hand behaves.
    /// </summary>
    public sealed class HandLab : MonoBehaviour
    {
        public struct Step { public string text; public float seconds; public int target; }
        // A fixed timeline (no early advance), so a replay lines up with the recording frame for frame.
        public static readonly Step[] Steps =
        {
            new Step { text = "Hold your hand still in front of the camera, in the middle, palm toward the screen", seconds = 6, target = -1 },
            new Step { text = "Move your hand slowly LEFT and RIGHT, as far as is comfortable", seconds = 12, target = -1 },
            new Step { text = "Move your hand slowly UP and DOWN", seconds = 12, target = -1 },
            new Step { text = "Move your hand slowly TOWARD the screen and BACK toward you", seconds = 12, target = -1 },
            new Step { text = "Make small quick moves in any direction", seconds = 8, target = -1 },
            new Step { text = "Close your hand into a fist and open it, four times", seconds = 10, target = -1 },
            new Step { text = "Reach for the LEFT object, close your hand to pick it up, lift it, then open your hand", seconds = 16, target = 0 },
            new Step { text = "Reach for the MIDDLE object, close your hand to pick it up, lift it, then open your hand", seconds = 16, target = 1 },
            new Step { text = "Reach for the RIGHT object, close your hand to pick it up, lift it, then open your hand", seconds = 16, target = 2 },
            new Step { text = "Done. Thank you: the session is saved.", seconds = 3, target = -1 },
        };

        static bool Replay => Array.IndexOf(Environment.GetCommandLineArgs(), "-hand-lab-replay") >= 0;
        static string OutArg => Environment.GetCommandLineArgs().Where(a => a.StartsWith("-hand-lab-out=")).Select(a => a.Substring(14)).FirstOrDefault();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-apartment-hand-lab") >= 0)
                new GameObject("Hand Lab").AddComponent<HandLab>();
        }

        ApartmentExperience app; Text text; StreamWriter csv; string dir; SqueezableObject[] targets = new SqueezableObject[3];
        int step = -1; float stepStarted; int frames; readonly System.Text.StringBuilder line = new System.Text.StringBuilder(512);
        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        IEnumerator Start()
        {
            Application.runInBackground = true;
            BuildText();
            yield return new WaitForSecondsRealtime(2);
            var shell = FindAnyObjectByType<RehabAppShell>(); app = shell.experience;
            shell.SetResearchIdentity(null); shell.openModuleButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(1);
            app.SetHardware(false);
            dir = OutArg ?? Path.Combine("Logs", "hand-lab", DateTime.Now.ToString("yyyyMMdd-HHmmss") + (Replay ? "-replay" : ""));
            Directory.CreateDirectory(dir);
            app.SetCameraMode();
            if (!Replay)
            {
                // The automated (-apartment-) run never starts the webcam by itself; the Hand Lab does, with recording on.
                app.cameraLauncher.RecordPath = Path.GetFullPath(Path.Combine(dir, "bridge.jsonl"));
                app.cameraLauncher.EnsureStarted();
                Show("Starting the camera...");
                float wait = Time.realtimeSinceStartup + 40;
                while (!app.cameraLauncher.CameraOpened && Time.realtimeSinceStartup < wait) yield return null;
                if (!app.cameraLauncher.CameraOpened) { Show("The camera did not open: " + app.cameraLauncher.LastError); yield return new WaitForSecondsRealtime(6); Finish("camera did not open"); yield break; }
            }
            var runner = app.GetComponent<ActivityRunner>();
            runner.Begin(ActivityKind.CoffeeTable, 1); app.player.SetPaused(false);
            yield return new WaitForSecondsRealtime(1);
            // Three targets, left to right on the screen, and the view on them.
            var view = app.player.view.GetComponent<UnityEngine.Camera>() ?? UnityEngine.Camera.main;
            var items = runner.Current.items.Where(i => i != null && i.Item != null).Select(i => i.Item).ToList();
            var centre = items.Aggregate(Vector3.zero, (s, i) => s + i.transform.position) / Mathf.Max(1, items.Count);
            app.player.LookAt(centre); yield return new WaitForSecondsRealtime(.6f);
            var ordered = items.OrderBy(i => view != null ? view.WorldToScreenPoint(i.transform.position).x : 0).ToList();
            if (ordered.Count >= 3) { targets[0] = ordered[0]; targets[1] = ordered[ordered.Count / 2]; targets[2] = ordered[ordered.Count - 1]; }
            var starts = items.Select(i => (item: i, pos: i.transform.position, rot: i.transform.rotation)).ToList();
            Show("Show your hand to the camera to begin");
            File.WriteAllText(Path.Combine(dir, "ready"), "waiting for the hand"); // the replay starts sending now
            float showUntil = Time.realtimeSinceStartup + (Replay ? 30 : 90);
            while (!(app.vision.RightValid || app.vision.LeftValid) && Time.realtimeSinceStartup < showUntil) yield return null;
            csv = new StreamWriter(Path.Combine(dir, "game.csv"));
            csv.WriteLine("t,step,seq,capture,ageMs,hand,valid,wristX,wristY,palm,tracked,targetX,targetY,targetZ,handX,handY,handZ,size,score,gate,held,targetName,targetDist,fps,paused,reach,rayTarget,viewYaw,viewPitch");
            for (step = 0; step < Steps.Length; step++)
            {
                stepStarted = Time.realtimeSinceStartup;
                var s = Steps[step]; string name = s.target >= 0 && targets[s.target] != null ? targets[s.target].displayName : "";
                int shot = 0;
                // Each grab starts from the same view (the sweeps before may have turned it), so the three targets stay on screen.
                // ... and from the same table: items moved by an earlier grab go back where they started.
                if (s.target >= 0)
                {
                    if (app.Held != null) app.Release();
                    foreach (var (item, pos, rot) in starts)
                    {
                        if (item == null) continue;
                        if (item.Body != null) { if (!item.Body.isKinematic) { item.Body.velocity = Vector3.zero; item.Body.angularVelocity = Vector3.zero; } item.Body.position = pos; item.Body.rotation = rot; }
                        item.transform.SetPositionAndRotation(pos, rot);
                    }
                    app.player.LookAt(centre);
                }
                while (Time.realtimeSinceStartup - stepStarted < s.seconds)
                {
                    // Replay only: a few screenshots per step (the game view, never the camera image) to see the hand.
                    if (Replay && (step == 1 || step >= 6 && step <= 8) && Time.realtimeSinceStartup - stepStarted > 2 + shot * 3 && shot < 4)
                    { shot++; ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(dir, "step" + step + "-" + shot + ".png"))); }
                    // Replay only: a screenshot on request (Tools/hand_pose_check.py writes <name>.shot; the game view only).
                    if (Replay && Time.realtimeSinceStartup >= nextShotLook) { nextShotLook = Time.realtimeSinceStartup + .2f; foreach (var req in Directory.GetFiles(dir, "*.shot")) { ScreenCapture.CaptureScreenshot(Path.ChangeExtension(Path.GetFullPath(req), ".png")); File.Delete(req); } }
                    Show((step + 1) + " / " + Steps.Length + "   " + s.text + (name.Length > 0 ? "  (" + name + ")" : "") + "\n" + Mathf.CeilToInt(s.seconds - (Time.realtimeSinceStartup - stepStarted)) + " s");
                    yield return null;
                }
            }
            Finish("complete");
        }

        float nextShotLook;
        void LateUpdate()
        {
            if (csv == null || step < 0 || step >= Steps.Length || app == null) return;
            var hand = app.ActiveHand; var f = app.LeftSelected ? app.vision.LatestLeft : app.vision.LatestRight;
            bool valid = app.LeftSelected ? app.vision.LeftValid : app.vision.RightValid;
            var target = Steps[step].target >= 0 ? targets[Steps[step].target] : null;
            float palm = 0, wx = float.NaN, wy = float.NaN;
            if (f != null && f.landmarks != null && f.landmarks.Length >= 21)
            {
                float aspect = f.imageWidth > 0 && f.imageHeight > 0 ? f.imageWidth / (float)f.imageHeight : 4f / 3f;
                palm = FingerPoseDriver.ImageDistance(f.landmarks[5], f.landmarks[17], aspect); wx = f.landmarks[0].x; wy = f.landmarks[0].y;
            }
            var local = hand.transform.localPosition; var t = hand.LastTargetLocal;
            line.Clear();
            line.Append(Time.realtimeSinceStartupAsDouble.ToString("0.0000", C)).Append(',').Append(step).Append(',')
                .Append(f != null ? f.sequence : 0).Append(',').Append(f != null ? f.captureSeconds.ToString("0.0000", C) : "").Append(',')
                .Append(f != null ? f.ageMs.ToString("0.0", C) : "").Append(',').Append(app.LeftSelected ? "L" : "R").Append(',').Append(valid ? 1 : 0).Append(',')
                .Append(wx.ToString("0.00000", C)).Append(',').Append(wy.ToString("0.00000", C)).Append(',').Append(palm.ToString("0.00000", C)).Append(',')
                .Append(hand.TrackingUsed ? 1 : 0).Append(',')
                .Append(t.x.ToString("0.0000", C)).Append(',').Append(t.y.ToString("0.0000", C)).Append(',').Append(t.z.ToString("0.0000", C)).Append(',')
                .Append(local.x.ToString("0.0000", C)).Append(',').Append(local.y.ToString("0.0000", C)).Append(',').Append(local.z.ToString("0.0000", C)).Append(',')
                .Append(hand.LastPalmSize.ToString("0.0000", C)).Append(',').Append(app.CameraGripScore.ToString("0.000", C)).Append(',').Append(app.CameraGateClosed ? 1 : 0).Append(',')
                .Append(app.Held != null ? app.Held.displayName.Replace(',', ' ') : "").Append(',').Append(target != null ? target.displayName.Replace(',', ' ') : "").Append(',')
                .Append(target != null ? Vector3.Distance(hand.transform.position, target.transform.position).ToString("0.0000", C) : "").Append(',')
                .Append(app.cameraLauncher != null ? app.cameraLauncher.FramesPerSecond.ToString("0.0", C) : "").Append(',')
                .Append(app.player.Paused ? 1 : 0).Append(',').Append(float.IsFinite(app.CameraReach) ? app.CameraReach.ToString("0.000", C) : "").Append(',')
                .Append(app.HandRayTarget != null ? app.HandRayTarget.displayName.Replace(',', ' ') : "").Append(',')
                .Append(app.player.view.transform.eulerAngles.y.ToString("0.0", C)).Append(',').Append(app.player.view.transform.eulerAngles.x.ToString("0.0", C));
            csv.WriteLine(line.ToString()); frames++;
        }

        void Finish(string how)
        {
            if (csv != null) { csv.Flush(); csv.Dispose(); csv = null; }
            var summary = new System.Text.StringBuilder();
            summary.Append("{\"result\":\"").Append(how).Append("\",\"replay\":").Append(Replay ? "true" : "false").Append(",\"frames\":").Append(frames)
                .Append(",\"utc\":\"").Append(DateTime.UtcNow.ToString("O")).Append("\",\"steps\":[");
            for (int i = 0; i < Steps.Length; i++) summary.Append(i > 0 ? "," : "").Append("{\"text\":\"").Append(Steps[i].text).Append("\",\"seconds\":").Append(Steps[i].seconds.ToString(C)).Append(",\"target\":").Append(Steps[i].target).Append('}');
            summary.Append("],\"targets\":[").Append(string.Join(",", targets.Select(t => "\"" + (t != null ? t.displayName : "") + "\""))).Append("],")
                .Append("\"scope\":\"").Append(Replay ? "REPLAY of a recorded real hand" : "MEASURED: real hand and webcam on this computer").Append("\"}");
            if (dir != null) File.WriteAllText(Path.Combine(dir, "session.json"), summary.ToString());
            if (app != null && app.cameraLauncher != null) app.cameraLauncher.StopWorker();
            Application.Quit(how == "complete" ? 0 : 1);
        }

        void Show(string s) { if (text != null) text.text = s; }
        void BuildText()
        {
            var canvasGo = new GameObject("Hand Lab canvas"); DontDestroyOnLoad(canvasGo);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5000;
            var panel = new GameObject("Panel"); panel.transform.SetParent(canvasGo.transform, false);
            var img = panel.AddComponent<Image>(); img.color = new Color(.05f, .07f, .1f, .82f); img.raycastTarget = false;
            var rt = (RectTransform)panel.transform; rt.anchorMin = new Vector2(.12f, .78f); rt.anchorMax = new Vector2(.88f, .97f); rt.offsetMin = rt.offsetMax = Vector2.zero;
            var go = new GameObject("Text"); go.transform.SetParent(panel.transform, false);
            text = go.AddComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 30; text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            var tr = (RectTransform)go.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(20, 10); tr.offsetMax = new Vector2(-20, -10);
        }
    }
}
