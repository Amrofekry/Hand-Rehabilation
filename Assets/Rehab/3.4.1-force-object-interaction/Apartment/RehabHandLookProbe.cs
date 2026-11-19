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
    /// Hand look and walk probe (opt-in: -apartment-handlook-probe). Sends SYNTHETIC camera-hand frames over the real
    /// loopback vision port in the rendered player (no webcam): the work zone does not turn, the edges turn the view each way,
    /// pointing walks and stops, a fist does not walk, lost tracking stops at once, and the setting turns it off.
    /// Writes Logs/handlook-probe.json. How it feels with your real hand is your check.
    /// </summary>
    public sealed class RehabHandLookProbe : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, value; public bool passed; }
        [Serializable] public sealed class Report { public bool passed; public string utc, scope, detail = ""; public int errors; public Check[] checks; }
        readonly List<Check> checks = new List<Check>(); int errors;
        void Add(string name, bool passed, string value) => checks.Add(new Check { name = name, passed = passed, value = value });
        enum Shape { Open, Fist, Point, Pinch }
        float handX = .5f, handY = .5f, handRoll; Shape shape = Shape.Open; bool sending; long sequence;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (Array.IndexOf(Environment.GetCommandLineArgs(), "-apartment-handlook-probe") >= 0) new GameObject("Hand look probe").AddComponent<RehabHandLookProbe>(); }
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string text, string stack, LogType kind) { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors++; }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), scope = "SYNTHETIC camera-hand frames over the loopback vision port in the rendered Windows player; no webcam or person" };
            StartCoroutine(Stream());
            Exception failure = null; var routine = Play();
            while (true)
            {
                object next = null; bool more;
                try { more = routine.MoveNext(); if (more) next = routine.Current; }
                catch (Exception error) { failure = error; break; }
                if (!more) break;
                yield return next;
            }
            sending = false; HandLocomotion.Enabled = true; PlayerPrefs.DeleteKey("GripRehab.HandLocomotion");
            report.checks = checks.ToArray(); report.errors = errors;
            report.passed = failure == null && errors == 0 && checks.Count > 0 && checks.All(c => c.passed);
            report.detail = failure != null ? failure.ToString() : errors > 0 ? errors + " error(s) logged" : string.Join(", ", checks.Where(c => !c.passed).Select(c => c.name + "=" + c.value));
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/handlook-probe.json", JsonUtility.ToJson(report, true));
            Debug.Log("[REHAB_HANDLOOK] " + (report.passed ? "PASS" : "FAIL") + " " + report.detail);
            Application.Quit(report.passed ? 0 : 1);
        }

        /// <summary>30 frames a second of the current hand, as the camera bridge would send them.</summary>
        IEnumerator Stream()
        {
            using (var udp = new UdpClient())
            {
                var target = new IPEndPoint(IPAddress.Loopback, 27817);
                while (true)
                {
                    if (sending)
                    {
                        var json = JsonUtility.ToJson(Frame(handX, handY, shape, handRoll));
                        var bytes = Encoding.UTF8.GetBytes(json); udp.Send(bytes, bytes.Length, target);
                    }
                    yield return new WaitForSecondsRealtime(1 / 30f);
                }
            }
        }

        LandmarkFrame Frame(float cx, float cy, Shape s, float roll = 0)
        {
            var f = new LandmarkFrame { schemaVersion = 1, source = "mediapipe-test", handedness = "Right", confidence = .95f, sequence = ++sequence,
                captureSeconds = Time.realtimeSinceStartupAsDouble, ageMs = 1, landmarks = new Vector3[21] };
            // Palm centre (mean of wrist and finger bases) lands on (cx, cy).
            float wy = cy + .08f;
            f.landmarks[0] = new Vector3(cx, wy, 0);
            for (int finger = 0; finger < 4; finger++)
            {
                int n = 5 + finger * 4; float x = cx + (finger - 1.5f) * .045f;
                bool straight = s == Shape.Open || s == Shape.Point && finger == 0 || s == Shape.Pinch && finger > 0; // pinch = OK sign
                f.landmarks[n] = new Vector3(x, wy - .10f, 0);
                f.landmarks[n + 1] = new Vector3(x, wy - .18f, 0);
                f.landmarks[n + 2] = new Vector3(x, wy + (straight ? -.26f : -.12f), 0);
                f.landmarks[n + 3] = new Vector3(x, wy + (straight ? -.34f : -.04f), 0);
            }
            for (int i = 1; i < 5; i++) f.landmarks[i] = new Vector3(cx - (.12f - i * .015f) - .04f, wy - .03f - i * .02f, 0);
            if (s == Shape.Pinch) f.landmarks[4] = f.landmarks[8] + new Vector3(.005f, .005f, 0); // thumb tip on the index tip
            if (roll != 0)
            {
                // Twist like a dial around the palm centre: positive turns clockwise on screen (image y points down).
                var c = new Vector3(cx, cy, 0); float r = roll * Mathf.Deg2Rad, cos = Mathf.Cos(r), sin = Mathf.Sin(r);
                for (int i = 0; i < 21; i++) { var d = f.landmarks[i] - c; f.landmarks[i] = c + new Vector3(d.x * cos - d.y * sin, d.x * sin + d.y * cos, 0); }
            }
            return f;
        }

        static string AimFacts(ApartmentExperience app, SqueezableObject cup, SqueezableObject plate)
        {
            var ray = app.player.view.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            string A(SqueezableObject o) => o == null ? "-" : o.displayName + " pivot " + Vector3.Angle(ray.direction, o.transform.position - ray.origin).ToString("0.0") + " com " + Vector3.Angle(ray.direction, o.Body.worldCenterOfMass - ray.origin).ToString("0.0");
            string hit = Physics.Raycast(ray, out var h, 3, ~(1 << 8), QueryTriggerInteraction.Ignore) ? h.collider.name + "<" + (h.collider.GetComponentInParent<SqueezableObject>() != null ? h.collider.GetComponentInParent<SqueezableObject>().displayName : "") + ">" : "nothing";
            float d = Physics.Raycast(ray, out var h2, 3, ~(1 << 8), QueryTriggerInteraction.Ignore) ? h2.distance : -1;
            var reach = typeof(ApartmentExperience).GetMethod("CanReach", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(app, new object[] { cup });
            return "[ray " + hit + " at " + d.ToString("0.00") + " m; cup at " + Vector3.Distance(ray.origin, cup.transform.position).ToString("0.00") + " m; reach " + reach + "; seated " + app.player.IsSeated + " eye " + ray.origin.ToString("F2") + "; " + A(cup) + "; " + A(plate) + "]";
        }

        static string AimedName(ApartmentExperience app)
        {
            var args = new object[] { null };
            var r = typeof(ApartmentExperience).GetMethod("KeyboardTarget", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(app, args) as SqueezableObject;
            var view = app.player.view; var ray = view.ViewportPointToRay(new Vector3(.5f, .5f, 0)); var station = app.choices.SelectedObject;
            string hit = Physics.Raycast(ray, out var h, app.settings.reachMeters, ~(1 << 8), QueryTriggerInteraction.Ignore) ? h.collider.name + "@" + h.distance.ToString("0.00") + " layer " + LayerMask.LayerToName(h.collider.gameObject.layer) : "nothing";
            float along = Vector3.Dot(station.transform.position - ray.origin, ray.direction);
            var point = station.ClosestSurface(ray.GetPoint(Mathf.Clamp(along, 0, app.settings.reachMeters)));
            float score = GraspMotionPolicy.AimScore(ray.origin, ray.direction, point, app.settings.reachMeters, app.settings.pickupAssistDegrees);
            var list = typeof(ApartmentExperience).GetField("objects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.GetValue(app) as System.Collections.IEnumerable;
            bool inList = false; if (list != null) foreach (var o in list) if (ReferenceEquals(o, station)) inList = true;
            return (r != null ? r.displayName : "none") + " [ray hits " + hit + "; ball score " + score.ToString("0.000") + " inList " + inList + " angle " + Vector3.Angle(ray.direction, station.transform.position - ray.origin).ToString("0.0") + "]";
        }

        IEnumerator Hold(float x, float y, Shape s, float seconds, float roll = 0) { handX = x; handY = y; shape = s; handRoll = roll; sending = true; yield return new WaitForSecondsRealtime(seconds); }
        static float Signed(float from, float to) => Mathf.DeltaAngle(from, to);

        IEnumerator Play()
        {
            yield return new WaitForSecondsRealtime(2);
            var shell = FindAnyObjectByType<RehabAppShell>(); var app = shell.experience; var player = app.player;
            shell.SetResearchIdentity(null);
            shell.openModuleButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(1);
            app.SetCameraMode(); player.SetPaused(false); if (player.IsSeated) player.Stand();
            var loco = HandLocomotion.Instance; if (loco == null) throw new InvalidOperationException("HandLocomotion missing");
            yield return Hold(.5f, .5f, Shape.Open, 1f);
            Add("camera hand is tracked", loco.Active, "active=" + loco.Active + " camera=" + app.CameraMode);
            var hint = FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).FirstOrDefault(t => t.text.StartsWith(HandLocomotion.HandHint));
            Add("controls card shows the hand controls in camera mode", hint != null, hint != null ? hint.text.Replace("\n", " | ") : "not found");

            float yaw0 = player.transform.eulerAngles.y; var pos0 = player.transform.position;
            yield return Hold(.62f, .6f, Shape.Open, 2f);
            Add("work zone (reach and grab) does not turn", Mathf.Abs(Signed(yaw0, player.transform.eulerAngles.y)) < .5f, Signed(yaw0, player.transform.eulerAngles.y).ToString("0.00") + "°");

            yaw0 = player.transform.eulerAngles.y;
            yield return Hold(.92f, .5f, Shape.Open, 2f);
            float right = Signed(yaw0, player.transform.eulerAngles.y);
            Add("hand at the right edge turns right", right > 40, right.ToString("0.0") + "° in 2 s");
            Add("turn speed stays under the cap", right <= loco.Policy.maxYawDegPerSec * 2 + 1, right.ToString("0.0") + "° in 2 s (cap " + loco.Policy.maxYawDegPerSec + "°/s)");
            yaw0 = player.transform.eulerAngles.y;
            yield return Hold(.08f, .5f, Shape.Open, 2f);
            float left = Signed(yaw0, player.transform.eulerAngles.y);
            Add("hand at the left edge turns left", left < -40, left.ToString("0.0") + "°");

            float Pitch() { float p = player.view.transform.localEulerAngles.x; return p > 180 ? p - 360 : p; }
            yield return Hold(.5f, .5f, Shape.Open, .6f); float pitch0 = Pitch();
            yield return Hold(.5f, .08f, Shape.Open, 1.2f); float upDelta = Pitch() - pitch0;
            Add("hand at the top looks up", upDelta < -10, upDelta.ToString("0.0") + "°");
            yield return Hold(.5f, .97f, Shape.Open, 1.2f); float downDelta = Pitch() - pitch0;
            Add("hand at the bottom looks down", downDelta > upDelta + 10, downDelta.ToString("0.0") + "°");
            yield return Hold(.5f, .5f, Shape.Open, .6f);
            player.LookAt(player.view.transform.position + player.transform.forward); // level view again

            // Walk away from the station: turn around first so the table is behind.
            player.transform.Rotate(0, 180, 0); yield return Hold(.5f, .5f, Shape.Open, .5f);
            var start = player.transform.position; float walked0 = loco.WalkedMeters;
            yield return Hold(.5f, .5f, Shape.Point, 2f);
            var walkedTo = player.transform.position; float walked = Flat(walkedTo - start);
            Add("pointing walks forward", walked > .5f && Vector3.Dot(walkedTo - start, player.transform.forward) > 0, walked.ToString("0.00") + " m in 2 s");
            yield return Hold(.5f, .5f, Shape.Open, .4f); var stopAt = player.transform.position;
            yield return Hold(.5f, .5f, Shape.Open, .6f);
            Add("open hand stops walking", Flat(player.transform.position - stopAt) < .02f, Flat(player.transform.position - stopAt).ToString("0.000") + " m after stop");
            var fistAt = player.transform.position;
            yield return Hold(.5f, .5f, Shape.Fist, 1.5f);
            Add("a fist (grab) does not walk", Flat(player.transform.position - fistAt) < .02f, Flat(player.transform.position - fistAt).ToString("0.000") + " m");

            // Fist grabs the station object (was G / E); opening the hand releases it (was Q).
            player.transform.Rotate(0, 180, 0); var station = app.choices.SelectedObject;
            player.LookAt(station.transform.position); yield return Hold(.5f, .5f, Shape.Open, .5f);
            // Go back to the living-room station the way the game does (seated, facing its objects), as a patient would. The
            // point-walk above ends at a timing-dependent spot; improvised teleports landed at table height or pushed a chair
            // into the seated line of sight (2026-11-06).
            app.choices.SelectStation(false); yield return new WaitForSecondsRealtime(.3f);
            station = app.choices.SelectedObject; player.LookAt(station.transform.position);
            string Off() => Vector3.Angle(player.view.transform.forward, station.transform.position - player.view.transform.position).ToString("0.0");
            string Sample() { var loco2 = HandLocomotion.Instance; float pitch = player.view.transform.localEulerAngles.x; if (pitch > 180) pitch -= 360;
                return "[off " + Off() + " yaw " + player.transform.eulerAngles.y.ToString("0.0") + " pitch " + pitch.ToString("0.0") + " pos " + player.transform.position.ToString("F2") +
                    " yawRate " + (loco2 != null ? loco2.Policy.YawRate.ToString("0.0") : "-") + " pitchRate " + (loco2 != null ? loco2.Policy.PitchRate.ToString("0.0") : "-") + " walk " + (loco2 != null ? loco2.Policy.Walk.ToString("0.00") : "-") + " seated " + player.IsSeated + "]"; }
            string trace = "after look " + Sample(); yield return null; trace += " +1f " + Sample(); yield return new WaitForSecondsRealtime(.2f); trace += " +0.2s " + Sample();
            shape = Shape.Fist; yield return new WaitForSecondsRealtime(.3f); trace += " fist+0.3s " + Sample();
            yield return Hold(.5f, .5f, Shape.Fist, .7f); trace += " fist+1s " + Sample();
            Add("a held fist grabs the object (no key)", app.Held == station, "held=" + (app.Held != null ? app.Held.displayName : "none") +
                (app.Held == station ? "" : " status=" + app.Status + " score=" + app.CameraGripScore.ToString("0.00") + " gate=" + app.CameraGateClosed +
                " dist=" + Vector3.Distance(player.view.transform.position, station.transform.position).ToString("0.00") + " active=" + station.gameObject.activeInHierarchy +
                " broken=" + station.IsBroken + " paused=" + player.Paused + " reach=" + typeof(ApartmentExperience).GetMethod("CanReach", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(app, new object[] { station }) + " blocker=" + (Physics.Linecast(player.view.transform.position, station.ClosestSurface(player.view.transform.position), out var block, ~(1 << 8), QueryTriggerInteraction.Ignore) ? block.collider.name : "none") + " fps=" + (1f / Mathf.Max(1e-4f, Time.smoothDeltaTime)).ToString("0") + " aimed=" + AimedName(app) + " view(" + trace + ")" + " pivot=" + station.transform.position.ToString("F2") + " com=" + (station.Body != null ? station.Body.worldCenterOfMass.ToString("F2") : "-")));
            yield return Hold(.5f, .5f, Shape.Fist, 1f);
            Add("keeping the fist keeps holding", app.Held == station, "held=" + (app.Held != null ? app.Held.displayName : "none"));
            yield return Hold(.5f, .5f, Shape.Open, .8f);
            Add("opening the hand releases it", app.Held == null, "held=" + (app.Held != null ? app.Held.displayName : "none"));

            // Pinch held opens the options panel (was TAB).
            int opened = loco.OptionsOpened;
            // Look away from the objects first: a pinch aimed at an object within reach is a grab (by design), not the menu.
            player.LookAt(player.view.transform.position + Vector3.up * 2 + player.transform.forward * .3f); yield return Hold(.5f, .5f, Shape.Open, .3f);
            yield return Hold(.5f, .5f, Shape.Pinch, 1.3f);
            Add("a held pinch opens the options panel", player.Paused && loco.OptionsOpened == opened + 1, "paused=" + player.Paused);
            player.SetPaused(false); yield return Hold(.5f, .5f, Shape.Open, .5f);

            // In an activity, a twist turns the placement (was , and .).
            var runner = app.GetComponent<ActivityRunner>();
            if (runner.Begin(ActivityKind.CoffeeTable, 2))
            {
                player.SetPaused(false); yield return new WaitForSecondsRealtime(.8f);
                var cup = runner.Current.items.Select(i => i.Item).FirstOrDefault(i => i != null && i.displayName == "Cup");
                if (cup != null)
                {
                    // The resting hand row (ApartmentExperience.HandCentreY) points the camera hand at the view centre.
                    float rest = ApartmentExperience.HandCentreY;
                    player.LookAt(cup.transform.position); yield return Hold(.5f, rest, Shape.Open, .4f);
                    // Grab the way a patient does: close the hand on the cup (camera gate + aim assist), not TryGrab with an open hand.
                    float yaw0p = runner.PlacementYaw;
                    yield return Hold(.5f, rest, Shape.Fist, .8f, 0); bool grabbed = app.Held == cup;
                    yield return Hold(.5f, rest, Shape.Fist, .6f, 40);
                    float turned = runner.PlacementYaw - yaw0p;
                    Add("twisting the hand turns the placement", grabbed && turned > 30, "grabbed=" + grabbed + " turned " + turned.ToString("0") + "° held=" + (app.Held != null ? app.Held.displayName : "none") + " twist=" + loco.Actions.PlacementYaw + " active=" + loco.Active + " score=" + app.CameraGripScore.ToString("0.00") + " status=" + app.Status + " " + AimFacts(app, cup, runner.Current.items.Select(i => i.Item).FirstOrDefault(i => i != null && i.displayName == "Plate")));
                    yield return Hold(.5f, rest, Shape.Fist, .6f, -40);
                    Add("twisting the other way turns it back", runner.PlacementYaw - yaw0p < -30, (runner.PlacementYaw - yaw0p).ToString("0") + "°");
                }
                else Add("twisting the hand turns the placement", false, "no cup in the coffee-table activity");
                app.Release(); runner.End(false); yield return Hold(.5f, .5f, Shape.Open, .5f);
            }
            else Add("twisting the hand turns the placement", false, "activity did not start: " + runner.Feedback);

            // Lost tracking while walking and turning: everything stops at once.
            yield return Hold(.92f, .5f, Shape.Point, 1.5f);
            sending = false; yield return new WaitForSecondsRealtime(.7f);
            var lostAt = player.transform.position; float lostYaw = player.transform.eulerAngles.y;
            yield return new WaitForSecondsRealtime(.8f);
            Add("lost tracking stops walking and turning", Flat(player.transform.position - lostAt) < .02f && Mathf.Abs(Signed(lostYaw, player.transform.eulerAngles.y)) < .5f && !loco.Policy.Walking,
                Flat(player.transform.position - lostAt).ToString("0.000") + " m, " + Signed(lostYaw, player.transform.eulerAngles.y).ToString("0.00") + "°");

            // Paused (options panel): no turning.
            yield return Hold(.5f, .5f, Shape.Open, .5f);
            player.SetPaused(true); float pausedYaw = player.transform.eulerAngles.y;
            yield return Hold(.92f, .5f, Shape.Open, 1f);
            Add("paused: the hand does not turn the view", Mathf.Abs(Signed(pausedYaw, player.transform.eulerAngles.y)) < .5f, Signed(pausedYaw, player.transform.eulerAngles.y).ToString("0.00") + "°");
            player.SetPaused(false);

            // Setting off: the hand no longer turns (mouse users).
            HandLocomotion.Enabled = false; yield return Hold(.5f, .5f, Shape.Open, .3f); float offYaw = player.transform.eulerAngles.y;
            yield return Hold(.92f, .5f, Shape.Open, 1f);
            Add("setting off: no hand turning", Mathf.Abs(Signed(offYaw, player.transform.eulerAngles.y)) < .5f, Signed(offYaw, player.transform.eulerAngles.y).ToString("0.00") + "°");
            HandLocomotion.Enabled = true;

            // Keyboard mode: hand frames do nothing (camera mode only).
            app.SetHardware(false); player.SetPaused(false); yield return Hold(.5f, .5f, Shape.Open, .3f); float kbYaw = player.transform.eulerAngles.y;
            yield return Hold(.92f, .5f, Shape.Point, 1f);
            Add("keyboard mode: camera frames do not move the player", Mathf.Abs(Signed(kbYaw, player.transform.eulerAngles.y)) < .5f && !loco.Active, Signed(kbYaw, player.transform.eulerAngles.y).ToString("0.00") + "°");
            sending = false; shell.ReturnHome();
        }

        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
    }
}
