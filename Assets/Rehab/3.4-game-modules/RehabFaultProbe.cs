using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using GripRehab.Product;
using UnityEngine;

namespace GripRehab
{
    /// <summary>
    /// Release QA fault probe (opt-in: -apartment-faults, with -rehab-force-replay). Injects the faults a patient can meet and
    /// checks that the game keeps running, tells the player in plain words, and logs no error, warning or exception:
    /// a camera that cannot open (worker started on an absent index, so no webcam is used), the hand stream stopping,
    /// the force sensor going stale and reporting a disconnect, no microphone, a missing fatigue model, and damaged saved
    /// profiles (one copy, then both). SYNTHETIC: every fault is injected; storage is a throwaway temp folder.
    /// Writes Logs/fault-probe.json.
    /// </summary>
    public sealed class RehabFaultProbe : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, value; public bool passed; }
        [Serializable] public sealed class Report { public bool passed; public string utc, scope, detail = ""; public int logs; public string[] logLines; public Check[] checks; }
        readonly List<Check> checks = new List<Check>(); readonly List<string> logLines = new List<string>();
        void Add(string name, bool passed, string value) => checks.Add(new Check { name = name, passed = passed, value = value });

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-apartment-faults") >= 0)
                new GameObject("Fault probe").AddComponent<RehabFaultProbe>();
        }
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        // Console zero holds under faults too: a handled fault is shown to the player, never logged as an error or warning.
        void OnLog(string text, string stack, LogType kind) { if (kind != LogType.Log && !text.StartsWith("[REHAB_FAULTS]", StringComparison.Ordinal)) logLines.Add(kind + ": " + text); }

        RehabAppShell shell; ApartmentExperience app; string tempRoot;

        IEnumerator Start()
        {
            Application.runInBackground = true;
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), scope = "SYNTHETIC injected faults in the rendered Windows player; no webcam opened, throwaway storage" };
            var mode = RehabVoiceInput.Mode;
            Exception failure = null; var routine = Play();
            while (true)
            {
                object next = null; bool more;
                try { more = routine.MoveNext(); if (more) next = routine.Current; }
                catch (Exception error) { failure = error; break; }
                if (!more) break;
                yield return next;
            }
            RehabVoiceInput.SimulateNoMicrophone = false; RehabVoiceInput.Mode = mode; PlayerPrefs.DeleteKey("GripRehab.Voice.Mode");
            if (app != null && app.cameraLauncher != null) app.cameraLauncher.StopWorker();
            try { if (tempRoot != null && Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch (IOException) { }
            report.checks = checks.ToArray(); report.logs = logLines.Count; report.logLines = logLines.ToArray();
            report.passed = failure == null && logLines.Count == 0 && checks.Count > 0 && checks.All(c => c.passed);
            report.detail = failure != null ? failure.ToString() : logLines.Count > 0 ? logLines.Count + " console line(s) logged" : string.Join(", ", checks.Where(c => !c.passed).Select(c => c.name + "=" + c.value));
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/fault-probe.json", JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Play()
        {
            yield return new WaitForSecondsRealtime(2);
            shell = FindAnyObjectByType<RehabAppShell>(); app = shell != null ? shell.experience : null;
            if (app == null) throw new InvalidOperationException("Apartment missing");
            yield return CameraCannotOpen();
            yield return HandStreamLost();
            yield return ForceSensorLost();
            yield return NoMicrophone();
            MissingModel();
            yield return DamagedSaves();
        }

        bool Alive(float since) => Time.unscaledTime > since && Time.frameCount > 0;

        // 1. The camera worker cannot open its camera: the HUD says why and the keyboard fallback works.
        IEnumerator CameraCannotOpen()
        {
            var launcher = app.cameraLauncher; float t0 = Time.unscaledTime;
            app.SetCameraMode(); launcher.cameraIndex = 9; launcher.forceCameraIndex = true; launcher.EnsureStarted();
            float end = Time.realtimeSinceStartup + 40;
            while (Time.realtimeSinceStartup < end && launcher.State != "FAILED") yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            string hud = app.forceText.text;
            Add("camera: absent camera reported as failed", launcher.State == "FAILED" && launcher.LastError.Length > 0, launcher.State + " / " + launcher.LastError);
            Add("camera: HUD tells the player why and what still works", hud.Contains(launcher.LastError) && hud.Contains("Keyboard and mouse still work"), hud.Split('\n')[1]);
            app.SetHardware(false); yield return new WaitForSecondsRealtime(.3f);
            Add("camera: keyboard fallback takes over", !app.CameraMode && app.statusText.text.Contains("KEYBOARD") && !launcher.IsRunning && Alive(t0), app.statusText.text.Replace('\n', ' '));
            launcher.cameraIndex = 0;
        }

        // 2. The hand leaves the camera (stream stops): the HUD shows SEARCHING, then LIVE again when frames return.
        bool sending; long sequence;
        IEnumerator HandStreamLost()
        {
            app.SetCameraMode(); var stream = StartCoroutine(Stream()); sending = true;
            yield return new WaitForSecondsRealtime(1.2f);
            bool live = app.statusText.text.Contains("LIVE");
            sending = false; yield return new WaitForSecondsRealtime(1.5f);
            bool searching = app.statusText.text.Contains("SEARCHING");
            sending = true; yield return new WaitForSecondsRealtime(1.2f);
            bool back = app.statusText.text.Contains("LIVE");
            sending = false; StopCoroutine(stream);
            Add("hand stream: LIVE -> SEARCHING -> LIVE", live && searching && back, "live=" + live + " searching=" + searching + " back=" + back);
            app.SetHardware(false); yield return null;
        }

        IEnumerator Stream()
        {
            using (var udp = new UdpClient())
            {
                var target = new IPEndPoint(IPAddress.Loopback, 27817);
                while (true)
                {
                    if (sending) { var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(OpenHand())); udp.Send(bytes, bytes.Length, target); }
                    yield return new WaitForSecondsRealtime(1 / 30f);
                }
            }
        }

        LandmarkFrame OpenHand()
        {
            var f = new LandmarkFrame { schemaVersion = 1, source = "mediapipe-test", handedness = app.LeftSelected ? "Left" : "Right", confidence = .95f, sequence = ++sequence,
                captureSeconds = Time.realtimeSinceStartupAsDouble, ageMs = 1, landmarks = new Vector3[21] };
            float cx = .5f, wy = .58f;
            f.landmarks[0] = new Vector3(cx, wy, 0);
            for (int finger = 0; finger < 4; finger++)
            {
                int n = 5 + finger * 4; float x = cx + (finger - 1.5f) * .045f;
                f.landmarks[n] = new Vector3(x, wy - .10f, 0); f.landmarks[n + 1] = new Vector3(x, wy - .18f, 0);
                f.landmarks[n + 2] = new Vector3(x, wy - .26f, 0); f.landmarks[n + 3] = new Vector3(x, wy - .34f, 0);
            }
            for (int i = 1; i < 5; i++) f.landmarks[i] = new Vector3(cx - (.12f - i * .015f) - .04f, wy - .03f - i * .02f, 0);
            return f;
        }

        // 3. The force sensor stops sending, then reports a disconnect, then comes back.
        IEnumerator ForceSensorLost()
        {
            // In play, not the paused start menu: force is (correctly) ignored while paused.
            app.SetHardware(true); app.player.SetPaused(false); var panel = app.GetComponent<DeviceStatusPanel>(); var state = app.forceReceiver.State;
            using (var udp = new UdpClient())
            {
                var target = new IPEndPoint(IPAddress.Loopback, 27818); long seq = 0;
                void Send(ForceBridgeFrame frame) { var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(frame)); udp.Send(bytes, bytes.Length, target); }
                ForceBridgeFrame Sample() { seq++; return new ForceBridgeFrame { schemaVersion = 1, kind = "sample", source = "synthetic", sessionId = "fault-probe", connectionId = "fault-link",
                    calibrationRevision = 1, calibrationCrc = 1, shapeId = 1, sequence = seq, sampleMs = seq * 20, rawCounts = 1000, forceMilliNewtons = 15000, flags = 3, ageMs = 5 }; }
                for (float end = Time.realtimeSinceStartup + 4; Time.realtimeSinceStartup < end;) { Send(Sample()); yield return new WaitForSecondsRealtime(.02f); }
                Add("force: live samples accepted", app.ForceValid && state.Status == "Synthetic BLE replay" && panel != null && panel.Health.State == DeviceLinkState.Live,
                    state.Status + " / " + (panel != null ? panel.Health.State.ToString() : "no panel"));
                yield return new WaitForSecondsRealtime(1f);
                Add("force: silence marked stale within 1 s", !app.ForceValid && state.Status.Contains("stale") && app.forceText.text.Contains("NO FRESH SAMPLE") && panel.Health.State == DeviceLinkState.Stale,
                    state.Status + " / " + panel.Health.State);
                Send(new ForceBridgeFrame { schemaVersion = 1, kind = "status", source = "synthetic", sessionId = "fault-probe", connectionId = "fault-link", reason = "BLE disconnected; reconnecting" });
                yield return new WaitForSecondsRealtime(.6f);
                Add("force: disconnect reason shown", !app.ForceValid && app.forceText.text.Contains("BLE disconnected"), state.Status);
                int reconnects = panel.Health.Reconnects;
                for (float end = Time.realtimeSinceStartup + 1; Time.realtimeSinceStartup < end;) { Send(Sample()); yield return new WaitForSecondsRealtime(.02f); }
                Add("force: reconnect recovers and is counted", app.ForceValid && panel.Health.Reconnects > reconnects, state.Status + " / reconnects " + panel.Health.Reconnects);
            }
            yield return new WaitForSecondsRealtime(.7f); app.SetHardware(false); app.player.SetPaused(true); yield return null;
        }

        // 4. Voice turned on with no microphone: a plain reason, no recognizer, the rest of the game unaffected.
        IEnumerator NoMicrophone()
        {
            var voice = RehabVoiceInput.Instance; if (voice == null) throw new InvalidOperationException("Voice input missing");
            RehabVoiceInput.SimulateNoMicrophone = true; RehabVoiceInput.Mode = VoiceMode.AlwaysOn;
            yield return new WaitForSecondsRealtime(.5f);
            Add("microphone: missing device explained, nothing listening", voice.Unavailable != null && voice.Unavailable.StartsWith("No microphone") && !voice.RecognizerRunning, voice.Unavailable ?? "null");
            RehabVoiceInput.Mode = VoiceMode.Off; RehabVoiceInput.SimulateNoMicrophone = false; yield return null;
        }

        // 5. The fatigue model file is missing: unavailable with a reason, no exception, the rule-based limits still decide.
        void MissingModel()
        {
            using (var missing = new ResearchAiRuntime("ResearchAi/__fault_probe_missing__"))
            {
                bool predicted = missing.TryPredict(new float[400], true, true, out _);
                Add("model: missing file reported, inference refused", !missing.Available && !predicted && missing.Status.Contains("missing"), missing.Status);
            }
            var policy = new FatigueSafetyPolicy(new FatigueSafetyConfig { maximumLevel = 3 });
            var decision = policy.Step(Time.realtimeSinceStartupAsDouble, true, 12f, null, null, null, true);
            Add("model: rule-based fatigue limits still run", policy.State.Length > 0 && policy.Level >= 1, policy.State + " level " + policy.Level + " / " + decision);
            using (var real = new ResearchAiRuntime()) Add("model: packaged model still loads", real.Available, real.Status);
        }

        // 6. Damaged saves: one bad copy is recovered from the backup; two bad copies keep the files, explain, and leave guest practice.
        IEnumerator DamagedSaves()
        {
            tempRoot = Path.Combine(Application.temporaryCachePath, "GripRehab-Faults-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot); var file = Path.Combine(tempRoot, "profiles.dat");
            new RehabProfileStore(tempRoot).Create("Fault test", 30, "Prefer not to say", "Right", "Right", "", "", -1);
            File.WriteAllBytes(file, Encoding.UTF8.GetBytes("damaged"));
            var recovered = new RehabProfileStore(tempRoot);
            Add("save: one damaged copy recovered from the backup", recovered.Catalog.profiles.Count == 1 && recovered.Catalog.profiles[0].name == "Fault test" &&
                Directory.GetFiles(tempRoot, "profiles.dat.unreadable-*").Length == 1, recovered.Catalog.profiles.Count + " profile(s)");
            File.WriteAllBytes(file, Encoding.UTF8.GetBytes("damaged")); File.WriteAllBytes(file + ".bak", Encoding.UTF8.GetBytes("damaged"));
            string message = null; try { new RehabProfileStore(tempRoot); } catch (IOException error) { message = error.Message; }
            Add("save: two damaged copies refused, files kept", message != null && message.Contains("retained") && File.Exists(file) && File.Exists(file + ".bak"), message ?? "no error");

            var original = shell.Store; shell.SetStoreForAcceptance(null);
            shell.ShowHome(); yield return new WaitForSecondsRealtime(.3f);
            string home = shell.homeGreeting.text;
            typeof(RehabAppShell).GetMethod("NewProfile", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(shell, null);
            yield return new WaitForSecondsRealtime(.3f);
            string form = shell.formError.text;
            Add("save: player told guest practice still works", home.Contains("guest practice") && form.Contains("guest practice") && !form.Contains("Console"), home + " | " + form);
            shell.SetStoreForAcceptance(original); shell.ShowHome(); yield return new WaitForSecondsRealtime(.3f);
        }
    }
}
