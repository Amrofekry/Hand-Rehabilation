using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using GripRehab.Product;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GripRehab
{
    /// <summary>
    /// Stage 7 probe (opt-in: -apartment-perf-probe). Plays every module with SYNTHETIC input in the
    /// rendered player and records frame time, managed allocation per frame, memory growth and errors.
    /// Also measures loopback hand-packet latency and injects malformed packets. Never opens a
    /// camera, device or the player's records (acceptance storage only).
    /// Options: -perf-seconds N (per phase, default 20), -perf-ports-busy (expect ports 27817/27818 taken).
    /// </summary>
    public sealed class RehabPerformanceProbe : MonoBehaviour
    {
        [Serializable] public sealed class Phase
        {
            public string name; public int frames, iterations, errors;
            public float seconds, fpsMean, frameP50Ms, frameP95Ms, frameP99Ms, frameMaxMs, fpsP95Floor;
            public float gcBytesMean, gcBytesP95, gcBytesMax; public bool gcMeasured;
            public float managedStartMB, managedEndMB, managedGrowthPercent, totalStartMB, totalEndMB, totalGrowthPercent;
            public string gcMethod = ""; public int gen0Collections;
        }
        [Serializable] public sealed class Report
        {
            public bool passed; public string utc, unity, graphics, cpu, quality, scope, detail = "";
            public int width, height, phaseSeconds; public bool portsBusyMode;
            public Phase[] phases;
            public float handLatencyP50Ms, handLatencyP95Ms; public int handLatencySamples;
            public int malformedSent, malformedRejected, faultErrors;
            public string visionPortProblem = "", forcePortStatus = "";
            public int totalErrors, totalExceptions;
        }

        RehabAppShell shell; ApartmentExperience app; RehabExerciseSuite suite;
        // Cached so the probe itself allocates almost nothing per frame (its own garbage would be counted).
        static readonly FieldInfo MemoryField = typeof(RehabExerciseSuite).GetField("memory", BindingFlags.NonPublic | BindingFlags.Instance),
            AssemblyField = typeof(RehabExerciseSuite).GetField("assembly", BindingFlags.NonPublic | BindingFlags.Instance),
            MazeField = typeof(RehabExerciseSuite).GetField("maze", BindingFlags.NonPublic | BindingFlags.Instance);
        MazeTask solvedMaze; List<MazeCell> solvedRoute; Vector2[] traceRoute; int traceLevel = -1;
        int errors;
        readonly List<float> frameMs = new List<float>(), gcBytes = new List<float>();
        ProfilerRecorder gcRecorder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-apartment-perf-probe") >= 0)
                new GameObject("Performance probe").AddComponent<RehabPerformanceProbe>();
        }

        static int IntArg(string name, int fallback)
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var v) ? v : fallback;
        }

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() { Application.logMessageReceived -= OnLog; if (gcRecorder.Valid) gcRecorder.Dispose(); }
        void OnLog(string text, string stack, LogType kind) { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors++; }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceName,
                cpu = SystemInfo.processorType, scope = "SYNTHETIC input in the rendered Windows player; no camera, device, person or player records",
                phaseSeconds = IntArg("-perf-seconds", 20), portsBusyMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-perf-ports-busy") >= 0 };
            var phases = new List<Phase>();
            Exception failure = null;
            var routine = Run(report, phases);
            while (true)
            {
                object next = null; bool more;
                try { more = routine.MoveNext(); if (more) next = routine.Current; }
                catch (Exception error) { failure = error; break; }
                if (!more) break;
                yield return next;
            }
            report.phases = phases.ToArray();
            report.width = Screen.width; report.height = Screen.height; report.quality = QualitySettings.names[QualitySettings.GetQualityLevel()];
            report.totalErrors = errors; report.totalExceptions = RehabDiagnostics.ExceptionCount;
            report.passed = failure == null && errors == 0;
            if (failure != null) report.detail = failure.ToString();
            else if (errors > 0) report.detail = errors + " error(s) logged; see the player log";
            Directory.CreateDirectory("Logs");
            File.WriteAllText(report.portsBusyMode ? "Logs/perf-probe-ports.json" : "Logs/perf-probe.json", JsonUtility.ToJson(report, true));
            Debug.Log("[REHAB_PERF] " + (report.passed ? "PASS" : "FAIL") + ": " + phases.Count + " phases, " + report.phaseSeconds + " s each");
            Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Run(Report report, List<Phase> phases)
        {
            yield return new WaitForSecondsRealtime(2);
            shell = FindAnyObjectByType<RehabAppShell>(); app = shell != null ? shell.experience : null;
            if (shell == null || app == null) throw new InvalidOperationException("Game shell not found");
            suite = (RehabExerciseSuite)typeof(RehabAppShell).GetField("exercises", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shell);
            suite.TutorialsEnabled = false;
            shell.SetResearchIdentity(null);
            gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            if (report.portsBusyMode)
            {
                // Ports were taken by the runner before launch: the game must explain it and keep running.
                report.visionPortProblem = app.vision.PortProblem;
                report.forcePortStatus = app.forceReceiver.State.Status;
                if (string.IsNullOrEmpty(report.visionPortProblem) || !report.visionPortProblem.Contains("already in use"))
                    throw new InvalidOperationException("Busy hand-tracking port was not explained: '" + report.visionPortProblem + "'");
                if (report.forcePortStatus == null || !report.forcePortStatus.Contains("port unavailable"))
                    throw new InvalidOperationException("Busy force port was not explained: '" + report.forcePortStatus + "'");
                var busy = new Phase { name = "ports-busy-menu" };
                yield return Measure(busy, 8, null);
                phases.Add(busy);
                yield break;
            }

            var menu = new Phase { name = "menu" };
            shell.ShowHome();
            yield return Measure(menu, report.phaseSeconds, null);
            phases.Add(menu);

            var force = new Phase { name = "module1-force" };
            shell.openModuleButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(1);
            // Keyboard demo force; the player is seated at the living-room station by module start.
            app.SetHardware(false); app.player.SetPaused(true); yield return new WaitForSecondsRealtime(.3f);
            var ball = app.objects[0];
            // Module start already seats the player at the station; just look at the ball from there.
            bool seated = true; app.player.view.transform.LookAt(ball.transform.position);
            yield return new WaitForSecondsRealtime(.2f);
            app.player.SetPaused(false); app.player.view.transform.LookAt(ball.transform.position);
            float t = 0;
            yield return Measure(force, report.phaseSeconds, dt =>
            {
                t += dt;
                if (app.player.Paused) app.player.SetPaused(false);
                if (app.Held == null && app.TryGrab(ball)) force.iterations++;
                app.TestForce = 14 + 8 * Mathf.Sin(t * 1.3f); // a steady squeeze with slow pulses
            });
            if (force.iterations == 0) throw new InvalidOperationException("Module 1 phase never held the ball: seated=" + seated + " paused=" + app.player.Paused + " camera=" + app.CameraMode +
                " hardware=" + app.Hardware + " held=" + (app.Held != null) + " active=" + ball.gameObject.activeInHierarchy + " broken=" + ball.IsBroken +
                " distance=" + Vector3.Distance(app.player.view.transform.position, ball.transform.position).ToString("0.00") + " page=" + shell.CurrentPage + " status=" + app.Status);
            app.TestForce = 0; app.Release(); shell.ReturnHome();
            phases.Add(force);

            var memory = new Phase { name = "module2-memory-assembly" };
            shell.cognitiveLockedButton.onClick.Invoke(); yield return null;
            float clock = 0, half = report.phaseSeconds / 2f; bool assembly = false;
            yield return Measure(memory, report.phaseSeconds, dt =>
            {
                clock += dt;
                if (!assembly && clock > half) { suite.Open(2, 1); assembly = true; }
                DriveModuleTwo(memory, assembly);
            });
            shell.ReturnHome();
            phases.Add(memory);

            var visuo = new Phase { name = "module3-trace-maze" };
            shell.visuomotorLockedButton.onClick.Invoke(); yield return null;
            clock = 0; bool maze = false; int step = 0;
            yield return Measure(visuo, report.phaseSeconds, dt =>
            {
                clock += dt;
                if (!maze && clock > half) { suite.Open(3, 1); maze = true; step = 0; }
                step = DriveModuleThree(visuo, maze, step);
            });
            phases.Add(visuo);

            yield return HandLatency(report);
            yield return Malformed(report);
            shell.ReturnHome();
        }

        void DriveModuleTwo(Phase phase, bool assembly)
        {
            if (suite.Complete) { suite.Tick(); if (suite.Complete && Time.frameCount % 90 == 0) { suite.Restart(); phase.iterations++; } return; }
            if (!assembly)
            {
                var memory = (SequenceMemoryTask)MemoryField.GetValue(suite);
                if (Time.frameCount % 20 == 0 && memory.Correct < memory.Length) suite.Submit(memory.Sequence[memory.Correct]);
                return;
            }
            var board = (AssemblyBoard)AssemblyField.GetValue(suite);
            AssemblyBoard.Piece piece = null; for (int i = 0; i < board.Pieces.Count; i++) if (!board.Pieces[i].Placed) { piece = board.Pieces[i]; break; }
            if (piece == null) return;
            var local = suite.HandCarrying ? new Vector2((piece.TargetX - .5f) * 1040, (piece.TargetY - .5f) * 276) : new Vector2((piece.X - .5f) * 1040, (piece.Y - .5f) * 276);
            suite.AssemblyHand(local, true, Time.unscaledDeltaTime * 4, 5); // 4x speed keeps the board busy without changing the rules
        }

        int DriveModuleThree(Phase phase, bool maze, int step)
        {
            if (suite.Complete) { if (Time.frameCount % 90 == 0) { suite.Restart(); phase.iterations++; return 0; } return step; }
            if (!maze)
            {
                if (traceLevel != suite.Level) { traceRoute = TraceTask.PathFor(suite.Level); traceLevel = suite.Level; }
                var route = traceRoute;
                float s = (Time.frameCount % 240) / 240f * (route.Length - 1);
                int i = Mathf.Min(route.Length - 2, (int)s);
                suite.TracePoint(Vector2.Lerp(route[i], route[i + 1], s - i));
                return step;
            }
            var task = (MazeTask)MazeField.GetValue(suite);
            if (task != solvedMaze) { solvedMaze = task; solvedRoute = task.Solve(); step = 1; }
            if (Time.frameCount % 8 == 0 && step < solvedRoute.Count) { var next = solvedRoute[step]; suite.MoveMaze(next.x - task.X, next.y - task.Y); step++; }
            return step;
        }

        IEnumerator Measure(Phase phase, int seconds, Action<float> drive)
        {
            frameMs.Clear(); gcBytes.Clear();
            int errorsBefore = errors;
            yield return null;
            phase.managedStartMB = Mb(GC.GetTotalMemory(false)); phase.totalStartMB = Mb(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());
            float managedEarly = -1, totalEarly = -1; double start = Time.realtimeSinceStartupAsDouble;
            int gen0 = GC.CollectionCount(0); long heap = GC.GetTotalMemory(false);
            while (Time.realtimeSinceStartupAsDouble - start < seconds)
            {
                float dt = Time.unscaledDeltaTime;
                drive?.Invoke(dt);
                if (Time.frameCount > 3) frameMs.Add(dt * 1000);
                // Release players have no per-frame GC counter, so use managed-heap growth between
                // frames; a frame that ran a collection (heap shrank) is skipped, not counted as 0.
                long now = GC.GetTotalMemory(false);
                if (gcRecorder.Valid && gcRecorder.LastValue > 0) gcBytes.Add(gcRecorder.LastValue);
                else if (now >= heap && Time.frameCount > 3) gcBytes.Add(now - heap);
                heap = now;
                if (managedEarly < 0 && Time.realtimeSinceStartupAsDouble - start > Math.Min(5, seconds * .25))
                { managedEarly = Mb(GC.GetTotalMemory(false)); totalEarly = Mb(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()); }
                yield return null;
            }
            phase.seconds = (float)(Time.realtimeSinceStartupAsDouble - start);
            phase.managedEndMB = Mb(GC.GetTotalMemory(false)); phase.totalEndMB = Mb(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());
            phase.gen0Collections = GC.CollectionCount(0) - gen0;
            phase.gcMethod = gcRecorder.Valid ? "profiler counter" : "managed heap growth per frame (release player)";
            float baseManaged = managedEarly > 0 ? managedEarly : phase.managedStartMB;
            phase.managedGrowthPercent = baseManaged > 0 ? 100 * (phase.managedEndMB - baseManaged) / baseManaged : 0;
            float baseTotal = totalEarly > 0 ? totalEarly : phase.totalStartMB;
            phase.totalGrowthPercent = baseTotal > 0 ? 100 * (phase.totalEndMB - baseTotal) / baseTotal : 0;
            phase.frames = frameMs.Count; phase.errors = errors - errorsBefore;
            if (frameMs.Count > 0)
            {
                var sorted = frameMs.OrderBy(v => v).ToArray();
                phase.frameP50Ms = Pct(sorted, .5f); phase.frameP95Ms = Pct(sorted, .95f); phase.frameP99Ms = Pct(sorted, .99f); phase.frameMaxMs = sorted[sorted.Length - 1];
                phase.fpsMean = 1000f / frameMs.Average(); phase.fpsP95Floor = 1000f / Mathf.Max(.01f, phase.frameP95Ms);
            }
            phase.gcMeasured = gcBytes.Count > 0;
            if (phase.gcMeasured)
            {
                var g = gcBytes.OrderBy(v => v).ToArray();
                phase.gcBytesMean = g.Average(); phase.gcBytesP95 = Pct(g, .95f); phase.gcBytesMax = g[g.Length - 1];
            }
        }

        IEnumerator HandLatency(Report report)
        {
            // Loopback only: time from sending a synthetic landmark packet to the frame that sees it.
            // Camera capture and MediaPipe inference time are not included (no camera is opened).
            var latencies = new List<float>(); var watch = new Stopwatch();
            using (var udp = new UdpClient())
            {
                for (int i = 0; i < 60; i++)
                {
                    long sequence = 900000 + i; long before = app.vision.LatestRight != null ? app.vision.LatestRight.sequence : -1;
                    var frame = Fixture(.5f, .5f); frame.sequence = sequence; frame.captureSeconds = Time.realtimeSinceStartupAsDouble;
                    var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(frame));
                    watch.Restart(); udp.Send(bytes, bytes.Length, "127.0.0.1", app.vision.port);
                    float waited = 0;
                    while ((app.vision.LatestRight == null || app.vision.LatestRight.sequence == before) && waited < .5f) { yield return null; waited += Time.unscaledDeltaTime; }
                    if (app.vision.LatestRight != null && app.vision.LatestRight.sequence == sequence) latencies.Add((float)watch.Elapsed.TotalMilliseconds);
                    yield return new WaitForSecondsRealtime(.03f);
                }
            }
            report.handLatencySamples = latencies.Count;
            if (latencies.Count > 0) { var s = latencies.OrderBy(v => v).ToArray(); report.handLatencyP50Ms = Pct(s, .5f); report.handLatencyP95Ms = Pct(s, .95f); }
        }

        IEnumerator Malformed(Report report)
        {
            int errorsBefore = errors, rejectedBefore = app.vision.RejectedPackets;
            var junk = new[] { "", "{", "not json", "{\"schemaVersion\":99}", "{\"schemaVersion\":1,\"landmarks\":[]}", "{\"schemaVersion\":1,\"confidence\":\"NaN\"}",
                new string('x', 60000), "{\"Newtons\":1e40}", "[]", "{\"schemaVersion\":1,\"handedness\":\"Middle\",\"landmarks\":null}" };
            using (var udp = new UdpClient())
                foreach (var text in junk)
                    foreach (int port in new[] { app.vision.port, 27818, 27820 })
                    {
                        var bytes = Encoding.UTF8.GetBytes(text);
                        try { udp.Send(bytes, bytes.Length, "127.0.0.1", port); report.malformedSent++; } catch (SocketException) { }
                        yield return null;
                    }
            yield return new WaitForSecondsRealtime(.5f);
            report.malformedRejected = app.vision.RejectedPackets - rejectedBefore;
            report.faultErrors = errors - errorsBefore;
            if (report.faultErrors > 0) throw new InvalidOperationException("Malformed packets caused " + report.faultErrors + " logged errors");
        }

        static LandmarkFrame Fixture(float x, float y)
        {
            // Same proven 21-landmark pointing pose as the apartment acceptance runner.
            var f = new LandmarkFrame { schemaVersion = 1, handedness = "Right", source = "mediapipe-acceptance", confidence = .95f, ageMs = 1, landmarks = new Vector3[21] };
            for (int i = 0; i < 21; i++) f.landmarks[i] = new Vector3(x, y, 0);
            f.landmarks[5] = new Vector3(x - .02f, y + .08f, 0); f.landmarks[6] = new Vector3(x - .015f, y + .055f, 0);
            f.landmarks[7] = new Vector3(x - .008f, y + .025f, 0); f.landmarks[8] = new Vector3(x, y, 0);
            foreach (int joint in new[] { 9, 13, 17 })
            {
                f.landmarks[joint] = new Vector3(x + .02f, y + .06f, 0); f.landmarks[joint + 1] = new Vector3(x + .02f, y + .025f, 0);
                f.landmarks[joint + 2] = new Vector3(x + .02f, y + .04f, 0); f.landmarks[joint + 3] = new Vector3(x + .02f, y + .05f, 0);
            }
            var palm = (f.landmarks[0] + f.landmarks[5] + f.landmarks[9] + f.landmarks[13] + f.landmarks[17]) * .2f;
            var shift = new Vector3(x, y, 0) - palm;
            for (int i = 0; i < 21; i++) f.landmarks[i] += shift;
            return f;
        }

        static float Pct(float[] sorted, float p) => sorted[Mathf.Clamp(Mathf.CeilToInt(p * sorted.Length) - 1, 0, sorted.Length - 1)];
        static float Mb(long bytes) => bytes / (1024f * 1024f);
    }
}
