using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GripRehab
{
    /// <summary>
    /// Stage 8 contact audit (opt-in: -apartment-contact-probe; add -contact-solver-off for the
    /// baseline). Holds every station object in three orientations at 0, 18 and 35 N of SYNTHETIC
    /// keyboard-demo force, then moves the hand at reach speed (about 0.5 m/s) and shakes it fast (about 3 m/s), and records finger penetration, pad gap,
    /// palm overlap, hold jitter, dent depth and furniture overlap. No camera, device or records.
    /// </summary>
    public sealed class RehabContactProbe : MonoBehaviour
    {
        [Serializable] public sealed class Case
        {
            public string item, orientation, phase, grip = ""; public int gripNumber, padCount; public float force; public bool held;
            public float handPeakMs, penetrationMaxMm, penetrationMeanMm, padGapMeanMm, padGapMaxMm, palmDepthMaxMm, jitterMm, dentMm;
            public float touchingPadsMean, contactsMean; public int furnitureOverlapFrames, frames, limitedJointsMean; public string furnitureCollider = "";
        }
        [Serializable] public sealed class Report
        {
            public bool passed, solver; public string utc, scope, detail = ""; public int errors;
            public Case[] cases;
        }

        ApartmentExperience app; ApartmentChoices choices; int errors;
        static readonly bool GripLabMode = Array.IndexOf(Environment.GetCommandLineArgs(), "-grip-lab") >= 0;
        readonly Collider[] overlaps = new Collider[32];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-apartment-contact-probe") >= 0)
                new GameObject("Contact probe").AddComponent<RehabContactProbe>();
        }
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string text, string stack, LogType kind) { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors++; }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), solver = GraspContactSolver.Enabled,
                scope = "SYNTHETIC keyboard-demo force in the rendered Windows player; no camera, device, person or records" };
            var cases = new List<Case>();
            Exception failure = null;
            var routine = GripLabMode ? RunGripLab(cases) : Run(cases);
            while (true)
            {
                object next = null; bool more;
                try { more = routine.MoveNext(); if (more) next = routine.Current; }
                catch (Exception error) { failure = error; break; }
                if (!more) break;
                yield return next;
            }
            report.cases = cases.ToArray(); report.errors = errors;
            report.passed = failure == null && errors == 0 && cases.Count > 0;
            report.detail = failure != null ? failure.ToString() : errors > 0 ? errors + " error(s) logged" : "";
            Directory.CreateDirectory("Logs");
            File.WriteAllText(GripLabMode ? (report.solver ? "Logs/contact-probe-grips.json" : "Logs/contact-probe-grips-baseline.json") : report.solver ? "Logs/contact-probe.json" : "Logs/contact-probe-baseline.json", JsonUtility.ToJson(report, true));
            Debug.Log("[REHAB_CONTACT] " + (report.passed ? "PASS" : "FAIL") + ": " + cases.Count + " cases, solver " + (report.solver ? "on" : "off"));
            Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Run(List<Case> cases)
        {
            yield return new WaitForSecondsRealtime(2);
            var shell = FindAnyObjectByType<RehabAppShell>();
            app = shell != null ? shell.experience : null; choices = app != null ? app.choices : null;
            if (app == null || choices == null) throw new InvalidOperationException("Apartment not found");
            shell.SetResearchIdentity(null);
            shell.openModuleButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(1);
            app.SetHardware(false);
            var orientations = new[] { ("upright", Quaternion.identity), ("turned 90", Quaternion.Euler(0, 90, 0)), ("tilted 45", Quaternion.Euler(45, 0, 0)) };
            foreach (bool kitchen in new[] { false, true })
            {
                var items = kitchen ? choices.kitchenObjects : choices.livingObjects;
                for (int index = 0; index < items.Length; index++)
                    foreach (var (label, turn) in orientations)
                    {
                        app.Release(); app.ResetObjects(); app.player.SetPaused(true);
                        choices.SelectStation(kitchen); choices.SelectObject(index);
                        yield return new WaitForSecondsRealtime(.3f);
                        var item = items[index];
                        app.player.SetPaused(false);
                        item.Body.rotation = turn * item.Body.rotation; Physics.SyncTransforms();
                        app.player.LookAt(item.transform.position);
                        yield return new WaitForSecondsRealtime(.2f);
                        if (!app.TryGrab(item)) { cases.Add(new Case { item = item.displayName, orientation = label, phase = "grab failed" }); continue; }
                        // After pickup the hand turns and comes into view (about 1 s); hold measurements start after that.
                        yield return new WaitForSecondsRealtime(1f);
                        foreach (float force in new[] { 0f, 18f, 35f })
                        {
                            app.TestForce = force;
                            yield return Measure(cases, item, label, "hold", force, 1.2f, false);
                            if (force == 18) yield return Shot(item.displayName + "-" + label);
                            if (item.IsBroken) break;
                        }
                        if (!item.IsBroken)
                        {
                            app.TestForce = 18;
                            yield return Measure(cases, item, label, "reach move", 18, 2f, true, .08f, .05f, 1f);
                            app.SetDemoHandOffset(false, Vector3.zero);
                            yield return Measure(cases, item, label, "fast shake", 18, 1.5f, true, .18f, .12f, 2.5f);
                        }
                        app.TestForce = 0; app.SetDemoHandOffset(false, Vector3.zero); app.Release();
                    }
            }
            app.ResetObjects(); shell.ReturnHome();
        }

        // Stage 8 grip chart: every Grip Lab prop held in its own grip at 0, 18 and 35 N, then moved at reach speed.
        IEnumerator RunGripLab(List<Case> cases)
        {
            yield return new WaitForSecondsRealtime(2);
            var shell = FindAnyObjectByType<RehabAppShell>();
            app = shell != null ? shell.experience : null; choices = app != null ? app.choices : null;
            if (app == null || choices == null) throw new InvalidOperationException("Apartment not found");
            shell.SetResearchIdentity(null);
            shell.openModuleButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(1);
            app.SetHardware(false);
            if (choices.labObjects.Length != GripCatalog.All.Length) throw new InvalidOperationException("Grip Lab has " + choices.labObjects.Length + " props, chart has " + GripCatalog.All.Length);
            app.player.SetPaused(true); choices.SelectLab();
            for (int index = 0; index < choices.labObjects.Length; index++)
            {
                app.Release(); app.player.SetPaused(true); choices.SelectLabGrip(index);
                yield return new WaitForSecondsRealtime(.3f);
                var item = choices.labObjects[index]; var grip = item.GetComponent<GripProp>().Definition;
                app.player.SetPaused(false); app.player.LookAt(item.transform.position);
                yield return new WaitForSecondsRealtime(.2f);
                if (!app.TryGrab(item)) { cases.Add(new Case { item = item.displayName, grip = grip.name, gripNumber = grip.number, phase = "grab failed" }); continue; }
                yield return new WaitForSecondsRealtime(1f);
                foreach (float force in new[] { 0f, 18f, 35f })
                {
                    app.TestForce = force;
                    yield return Measure(cases, item, "grip lab", "hold", force, 1.2f, false);
                    var last = cases[cases.Count - 1]; last.grip = grip.name; last.gripNumber = grip.number; last.padCount = grip.pads.Length;
                    if (force == 18) yield return Shot(grip.number.ToString("00") + "-" + grip.name + "-" + item.displayName);
                    if (item.IsBroken) break;
                }
                if (!item.IsBroken)
                {
                    app.TestForce = 18;
                    yield return Measure(cases, item, "grip lab", "reach move", 18, 2f, true, .08f, .05f, 1f);
                    var last = cases[cases.Count - 1]; last.grip = grip.name; last.gripNumber = grip.number; last.padCount = grip.pads.Length;
                }
                app.TestForce = 0; app.SetDemoHandOffset(false, Vector3.zero); app.Release();
            }
            app.player.SetPaused(true); choices.SelectStation(false); app.ResetObjects(); shell.ReturnHome();
        }

        IEnumerator Measure(List<Case> cases, SqueezableObject item, string orientation, string phase, float force, float seconds, bool shake, float ampX = 0, float ampY = 0, float hz = 0)
        {
            var c = new Case { item = item.displayName, orientation = orientation, phase = phase, force = force, held = true };
            // Let the hand close and the hold settle before measuring.
            yield return new WaitForSecondsRealtime(.4f);
            var local = new List<Vector3>(); var lastWrist = app.ActiveHand.transform.position; float pen = 0, penSum = 0, gapSum = 0, gapMax = 0, palm = 0, touch = 0, contacts = 0, limited = 0; int n = 0;
            double start = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - start < seconds)
            {
                if (shake)
                {
                    float t = (float)(Time.realtimeSinceStartupAsDouble - start);
                    app.SetDemoHandOffset(false, new Vector3(Mathf.Sin(t * hz * Mathf.PI * 2) * ampX, Mathf.Cos(t * hz * .8f * Mathf.PI * 2) * ampY, 0));
                }
                yield return null;
                var wrist = app.ActiveHand.transform.position;
                if (Time.unscaledDeltaTime > 0) c.handPeakMs = Mathf.Max(c.handPeakMs, Vector3.Distance(wrist, lastWrist) / Time.unscaledDeltaTime);
                lastWrist = wrist;
                if (app.Held != item) { c.held = false; break; }
                var r = app.LastContact; if (!r.valid) continue;
                n++; pen = Mathf.Max(pen, r.maxPenetration); penSum += r.maxPenetration; gapSum += r.padGap; gapMax = Mathf.Max(gapMax, r.padGap);
                palm = Mathf.Max(palm, r.palmDepth); touch += r.touchingPads; contacts += item.ContactCount; limited += r.limitedJoints;
                local.Add(app.ActiveHand.transform.InverseTransformPoint(item.Body.position));
                var hit = Furniture(item); if (hit != null) { c.furnitureOverlapFrames++; if (c.furnitureCollider == "") c.furnitureCollider = hit; }
                c.dentMm = Mathf.Max(c.dentMm, item.MaxDisplacementMeters * 1000);
            }
            c.frames = n;
            if (n > 0)
            {
                c.penetrationMaxMm = pen * 1000; c.penetrationMeanMm = penSum / n * 1000; c.padGapMeanMm = gapSum / n * 1000; c.padGapMaxMm = gapMax * 1000;
                c.palmDepthMaxMm = palm * 1000; c.touchingPadsMean = touch / n; c.contactsMean = contacts / n; c.limitedJointsMean = Mathf.RoundToInt(limited / n);
                if (!shake && local.Count > 2)
                {
                    var mean = local.Aggregate(Vector3.zero, (a, v) => a + v) / local.Count;
                    // Hand-relative position spread while nothing moves: the visible wobble, scaled to world size.
                    c.jitterMm = Mathf.Sqrt(local.Average(v => (v - mean).sqrMagnitude)) * app.ActiveHand.transform.lossyScale.x * 1000;
                }
            }
            cases.Add(c);
        }

        // One screenshot of the held object per orientation, for the by-eye before/after check.
        IEnumerator Shot(string name)
        {
            var folder = Path.Combine("Logs", (GripLabMode ? "grip-shots" : "contact-shots") + (GraspContactSolver.Enabled ? "" : "-baseline"));
            Directory.CreateDirectory(folder);
            yield return new WaitForEndOfFrame();
            // Written directly (CaptureScreenshot's relative path is not the working folder in a player).
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(folder, name.Replace(' ', '-').Replace('/', '-').ToLowerInvariant() + ".png")), shot.EncodeToPNG());
            Destroy(shot);
            yield return null;
        }

        // True collider penetration (> 2 mm) between the held object and any non-player, non-object collider.
        string Furniture(SqueezableObject item)
        {
            var own = Array.FindAll(item.GetComponentsInChildren<Collider>(), x => !x.isTrigger && x.enabled);
            var bounds = new Bounds(item.Body.position, Vector3.zero);
            foreach (var x in own) bounds.Encapsulate(x.bounds);
            int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, overlaps, Quaternion.identity, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = overlaps[i];
                if (other == null || other.transform.IsChildOf(app.player.transform) || other.GetComponentInParent<SqueezableObject>() != null) continue;
                bool deep = false;
                foreach (var x in own)
                    if (Physics.ComputePenetration(x, x.transform.position, x.transform.rotation, other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > .002f) { deep = true; break; }
                if (!deep) continue;
                return other.name + " (" + other.GetType().Name + ")";
            }
            return null;
        }
    }
}
