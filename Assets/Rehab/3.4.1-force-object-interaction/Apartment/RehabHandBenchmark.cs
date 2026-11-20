using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace GripRehab
{
    /// <summary>
    /// Camera-hand control benchmark (opt-in: -apartment-handbench-probe). Same SYNTHETIC frames for every build, sent over the
    /// real loopback vision port, with MediaPipe-like landmark noise; uses only long-standing public APIs so the same probe
    /// measures the controller before and after a change. Writes Logs/hand-benchmark.json. Not a real camera or person.
    /// Metrics:
    /// - restJitterMm: standard deviation of the virtual wrist (local position) over 3 s with the hand still and noisy input.
    /// - restStepMm: median frame-to-frame wrist movement at rest (visible shake).
    /// - sweepRmsMm / settleMs: a 0.6 s 20 % sideways sweep; RMS error from the ideal straight-line path, and time to settle within 5 mm.
    /// - turnDepthMm: change of virtual hand depth when the palm turns 50° sideways without moving (should be ~0).
    /// - holdDrops: drops while holding with a noisy, partly-open fist for 5 s (should be 0).
    /// - blipHeld: the object is still held after a 0.7 s tracking gap (a brief occlusion).
    /// - activityCameraGrab: a fist at the centre picks up the coffee-table cup with no key press.
    /// - pinchGrab: a thumb–index pinch picks up the station object.
    /// - pointFalseGrab: pointing at an object does not pick it up (false grab = bad).
    /// </summary>
    public sealed class RehabHandBenchmark : MonoBehaviour
    {
        [Serializable] public sealed class Result { public string utc, scope, build; public float restJitterMm, restXYJitterMm, restDepthJitterMm, restStepMm, sweepRmsMm, settleMs, turnDepthMm, gripScoreMin, gripScoreMean; public int holdDrops, errors; public bool blipHeld, activityCameraGrab, pinchGrab, pointFalseGrab; public string detail = ""; }
        enum Shape { Open, Fist, Point, Pinch, NoisyFist }
        float hx = .5f, hy = .5f, noise, turn; Shape shape = Shape.Open; bool sending; long sequence; int errors;
        readonly System.Random random = new System.Random(20261029);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (Array.IndexOf(Environment.GetCommandLineArgs(), "-apartment-handbench-probe") >= 0) new GameObject("Hand benchmark").AddComponent<RehabHandBenchmark>(); }
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string t, string s, LogType k) { if (k == LogType.Error || k == LogType.Exception || k == LogType.Assert) errors++; }

        float Gauss() { double u = 1 - random.NextDouble(), v = random.NextDouble(); return (float)(Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v)); }

        LandmarkFrame Frame()
        {
            var f = new LandmarkFrame { schemaVersion = 1, source = "mediapipe-test", handedness = "Right", confidence = .95f, sequence = ++sequence,
                captureSeconds = Time.realtimeSinceStartupAsDouble, ageMs = 1, landmarks = new Vector3[21] };
            float cx = hx, wy = hy + .08f;
            f.landmarks[0] = new Vector3(cx, wy, 0);
            for (int finger = 0; finger < 4; finger++)
            {
                int n = 5 + finger * 4; float x = cx + (finger - 1.5f) * .045f;
                bool straight = shape == Shape.Open || shape == Shape.Point && finger == 0 || shape == Shape.Pinch && finger > 0;
                float tip = straight ? -.34f : shape == Shape.NoisyFist ? -.04f - (float)random.NextDouble() * .2f : -.04f;
                float mid = straight ? -.26f : shape == Shape.NoisyFist ? -.12f - (float)random.NextDouble() * .1f : -.12f;
                f.landmarks[n] = new Vector3(x, wy - .10f, 0);
                f.landmarks[n + 1] = new Vector3(x, wy - .18f, 0);
                f.landmarks[n + 2] = new Vector3(x, wy + mid, 0);
                f.landmarks[n + 3] = new Vector3(x, wy + tip, 0);
            }
            for (int i = 1; i < 5; i++) f.landmarks[i] = new Vector3(cx - (.12f - i * .015f) - .04f, wy - .03f - i * .02f, 0);
            if (shape == Shape.Pinch)
            {
                // Index curls toward the thumb; thumb tip meets the index tip.
                f.landmarks[6] = new Vector3(cx - .07f, wy - .16f, 0); f.landmarks[7] = new Vector3(cx - .1f, wy - .18f, 0); f.landmarks[8] = new Vector3(cx - .12f, wy - .17f, 0);
                f.landmarks[4] = f.landmarks[8] + new Vector3(.004f, .004f, 0);
            }
            if (turn != 0) // palm turned sideways: x of the hand shrinks around the wrist (foreshortening), z grows
                for (int i = 1; i < 21; i++) { var d = f.landmarks[i] - f.landmarks[0]; f.landmarks[i] = f.landmarks[0] + new Vector3(d.x * Mathf.Cos(turn * Mathf.Deg2Rad), d.y, d.x * Mathf.Sin(turn * Mathf.Deg2Rad)); }
            if (noise > 0) for (int i = 0; i < 21; i++) f.landmarks[i] += new Vector3(Gauss() * noise, Gauss() * noise, Gauss() * noise * .5f);
            return f;
        }

        IEnumerator Stream()
        {
            using (var udp = new UdpClient())
            {
                var target = new IPEndPoint(IPAddress.Loopback, 27817);
                while (true)
                {
                    if (sending) { var b = Encoding.UTF8.GetBytes(JsonUtility.ToJson(Frame())); udp.Send(b, b.Length, target); }
                    yield return new WaitForSecondsRealtime(1 / 30f); // a 30 fps webcam
                }
            }
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            var r = new Result { utc = DateTime.UtcNow.ToString("O"), scope = "SYNTHETIC 30 fps camera-hand frames with Gaussian landmark noise (sigma 0.004 of the image, about 2.6 px at 640) over the loopback vision port; no camera or person",
                build = Application.version + " " + File.GetLastWriteTimeUtc(Application.dataPath).ToString("s") };
            StartCoroutine(Stream());
            Exception failure = null; var routine = Run(r);
            while (true)
            {
                object next = null; bool more;
                try { more = routine.MoveNext(); if (more) next = routine.Current; } catch (Exception e) { failure = e; break; }
                if (!more) break; yield return next;
            }
            sending = false; r.errors = errors; if (failure != null) r.detail = failure.ToString();
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/hand-benchmark.json", JsonUtility.ToJson(r, true));
            Application.Quit(failure == null ? 0 : 1);
        }

        /// <summary>Palm row of a resting real hand (the camera hand then points at the view centre); Hold takes the palm centre.</summary>
        static float Rest => ApartmentExperience.HandCentreY - .08f;
        IEnumerator Hold(float x, float y, Shape s, float seconds, float sigma = 0, float yaw = 0) { hx = x; hy = y; shape = s; noise = sigma; turn = yaw; sending = true; yield return new WaitForSecondsRealtime(seconds); }

        IEnumerator Run(Result r)
        {
            yield return new WaitForSecondsRealtime(2);
            var shell = FindAnyObjectByType<RehabAppShell>(); var app = shell.experience; var player = app.player;
            shell.SetResearchIdentity(null); shell.openModuleButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(1);
            app.SetCameraMode(); player.SetPaused(false); if (player.IsSeated) player.Stand();
            var station = app.choices.SelectedObject; player.LookAt(station.transform.position);
            // Within reach the way the game does it (seated at the station, facing its objects): the camera grab no longer
            // picks up a looked-at object from across the room (2026-11-20), so a start 1.5 m away could not grab.
            if (Vector3.Distance(player.view.transform.position, station.transform.position) > .9f)
            { app.choices.SelectStation(false); yield return new WaitForSecondsRealtime(.3f); station = app.choices.SelectedObject; player.LookAt(station.transform.position); }
            var stationStart = station.transform.position; float eyeStart = Vector3.Distance(player.view.transform.position, stationStart);
            yield return Hold(.5f, Rest, Shape.Open, 1.5f);
            r.detail += "start: eye-to-station " + eyeStart.ToString("0.00") + " m, station moved " + Vector3.Distance(stationStart, station.transform.position).ToString("0.00") + " m during the open hold | ";
            var hand = app.ActiveHand.transform;

            // 0. Pinch picks up the station object, from the starting spot (within reach).
            int optionsBefore0 = HandLocomotion.Instance != null ? HandLocomotion.Instance.OptionsOpened : 0;
            yield return Hold(.5f, Rest, Shape.Pinch, 1.5f);
            r.pinchGrab = app.Held == station;
            r.detail += "pinch@start: optionsOpened=" + ((HandLocomotion.Instance != null ? HandLocomotion.Instance.OptionsOpened : 0) - optionsBefore0) + " dist=" + Vector3.Distance(player.view.transform.position, station.transform.position).ToString("0.00") + " held=" + (app.Held != null ? app.Held.displayName : "none") + " | ";
            yield return Hold(.5f, Rest, Shape.Open, 1.2f); if (app.Held != null) app.Release(); player.SetPaused(false);
            app.ResetObjects(); player.LookAt(station.transform.position); yield return Hold(.5f, Rest, Shape.Open, .8f);

            // 1. Rest jitter with noisy input.
            yield return Hold(.5f, Rest, Shape.Open, .8f, .004f);
            var rest = new List<Vector3>();
            for (float t = 0; t < 3; t += Time.unscaledDeltaTime) { rest.Add(hand.localPosition); yield return null; }
            var mean = rest.Aggregate(Vector3.zero, (a, b) => a + b) / rest.Count;
            r.restJitterMm = Mathf.Sqrt(rest.Average(p => (p - mean).sqrMagnitude)) * 1000;
            r.restXYJitterMm = Mathf.Sqrt(rest.Average(p => { var d = p - mean; return d.x * d.x + d.y * d.y; })) * 1000;
            r.restDepthJitterMm = Mathf.Sqrt(rest.Average(p => (p.z - mean.z) * (p.z - mean.z))) * 1000;
            r.restXYJitterMm = Mathf.Sqrt(rest.Average(p => { var d = p - mean; return d.x * d.x + d.y * d.y; })) * 1000;
            r.restDepthJitterMm = Mathf.Sqrt(rest.Average(p => (p.z - mean.z) * (p.z - mean.z))) * 1000;
            var steps = rest.Skip(1).Select((p, i) => (p - rest[i]).magnitude * 1000).OrderBy(v => v).ToList(); r.restStepMm = steps[steps.Count / 2];

            // 2. Sideways sweep (no noise): RMS from the straight path and settle time.
            yield return Hold(.4f, Rest, Shape.Open, 1.5f); var start = hand.localPosition;
            yield return Hold(.6f, Rest, Shape.Open, 1.5f); var end = hand.localPosition;
            yield return Hold(.4f, Rest, Shape.Open, 1.5f);
            float sweep = .6f, err = 0; int n = 0; shape = Shape.Open; noise = 0; turn = 0;
            for (float t = 0; t < sweep; t += Time.unscaledDeltaTime) { hx = Mathf.Lerp(.4f, .6f, t / sweep); var ideal = Vector3.Lerp(start, end, t / sweep); err += (hand.localPosition - ideal).sqrMagnitude; n++; yield return null; }
            hx = .6f; float settle = 0; while ((hand.localPosition - end).magnitude > .005f && settle < 2) { settle += Time.unscaledDeltaTime; yield return null; }
            r.sweepRmsMm = Mathf.Sqrt(err / Mathf.Max(1, n)) * 1000; r.settleMs = settle * 1000;

            // 3. Palm turned 50° sideways without moving: depth should not change.
            yield return Hold(.5f, Rest, Shape.Open, 1.2f); float z0 = hand.localPosition.z;
            yield return Hold(.5f, Rest, Shape.Open, 1.2f, 0, 50); r.turnDepthMm = Mathf.Abs(hand.localPosition.z - z0) * 1000;
            yield return Hold(.5f, Rest, Shape.Open, 1f);

            // 4. Hold with a noisy, partly open fist.
            player.LookAt(station.transform.position); yield return Hold(.5f, Rest, Shape.Fist, .6f);
            if (app.Held == null) app.TryGrab(station);
            int drops = 0; bool was = app.Held != null; float min = 1, sum = 0; int count = 0;
            hx = .5f; hy = .5f; shape = Shape.NoisyFist; noise = .004f; turn = 0;
            for (float t = 0; t < 5; t += Time.unscaledDeltaTime)
            {
                bool now = app.Held != null; if (was && !now) { drops++; app.TryGrab(station); } was = app.Held != null;
                min = Mathf.Min(min, app.CameraGripScore); sum += app.CameraGripScore; count++; yield return null;
            }
            r.holdDrops = drops; r.gripScoreMin = min; r.gripScoreMean = sum / Mathf.Max(1, count);

            // 5. A 0.25 s tracking gap while holding.
            yield return Hold(.5f, Rest, Shape.Fist, .6f); if (app.Held == null) app.TryGrab(station); yield return Hold(.5f, Rest, Shape.Fist, .5f);
            sending = false; yield return new WaitForSecondsRealtime(.7f); // longer than the receiver's 0.5 s freshness window yield return Hold(.5f, Rest, Shape.Fist, .5f);
            r.blipHeld = app.Held != null;
            yield return Hold(.5f, Rest, Shape.Open, 1.2f); if (app.Held != null) app.Release();

            // 6. Pointing at the object must not pick it up.
            player.LookAt(station.transform.position); yield return Hold(.5f, Rest, Shape.Point, 1.5f);
            r.pointFalseGrab = app.Held != null; if (app.Held != null) app.Release();
            yield return Hold(.5f, Rest, Shape.Open, 1f);

            // 7. Pinch picks up the station object (objects restored first so it is within reach again).
            app.ResetObjects(); player.SetPaused(false); yield return Hold(.5f, Rest, Shape.Open, .8f);
            int optionsBefore = HandLocomotion.Instance != null ? HandLocomotion.Instance.OptionsOpened : 0;
            player.LookAt(station.transform.position);
            if (Vector3.Distance(player.view.transform.position, station.transform.position) > .9f) { app.choices.SelectStation(false); yield return new WaitForSecondsRealtime(.3f); station = app.choices.SelectedObject; player.LookAt(station.transform.position); }
            yield return Hold(.5f, Rest, Shape.Open, .4f);
            yield return Hold(.5f, Rest, Shape.Pinch, 1.5f);
            r.detail += "pinch-late (after other tests, not scored): optionsOpened=" + ((HandLocomotion.Instance != null ? HandLocomotion.Instance.OptionsOpened : 0) - optionsBefore) + " dist=" + Vector3.Distance(player.view.transform.position, station.transform.position).ToString("0.00") + " paused=" + player.Paused + " assisted=" + app.AssistedGrabs + " camera=" + app.CameraMode + " score=" + app.CameraGripScore.ToString("0.00") + " status=" + app.Status + " | "; yield return Hold(.5f, Rest, Shape.Open, 1.2f); if (app.Held != null) app.Release();

            // 8. Camera grab of an activity item with no key press.
            var runner = app.GetComponent<ActivityRunner>();
            if (runner.Begin(ActivityKind.CoffeeTable, 2))
            {
                player.SetPaused(false); yield return Hold(.5f, Rest, Shape.Open, 1f);
                var cup = runner.Current.items.Select(i => i.Item).FirstOrDefault(i => i != null && i.displayName == "Cup");
                if (cup != null) { player.LookAt(cup.transform.position); yield return Hold(.5f, Rest, Shape.Open, .5f); yield return Hold(.5f, Rest, Shape.Fist, 1.5f); r.activityCameraGrab = app.Held == cup; r.detail += "activity: dist=" + Vector3.Distance(player.view.transform.position, cup.transform.position).ToString("0.00") + " paused=" + player.Paused + " assisted=" + app.AssistedGrabs + " camera=" + app.CameraMode + " score=" + app.CameraGripScore.ToString("0.00") + " held=" + (app.Held != null ? app.Held.displayName : "none") + " status=" + app.Status + " | "; }
                yield return Hold(.5f, Rest, Shape.Open, 1f); if (app.Held != null) app.Release(); runner.End(false);
            }
            sending = false; shell.ReturnHome(); yield return new WaitForSecondsRealtime(.5f);
        }
    }
}
