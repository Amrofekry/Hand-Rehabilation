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
    /// Stage 10 activity probe (opt-in: -apartment-activity-probe). Plays all six activities at two levels with
    /// SYNTHETIC keyboard-demo input through the real path (look at the item, E-equivalent grab, look at the
    /// target, release; squeeze and chop with test force), and records completion, score, steps, errors and
    /// outcomes. Isolated acceptance storage, no camera, device or person.
    /// With -activity-camera (2026-11-02) the same activities are played with the camera hand instead: SYNTHETIC
    /// MediaPipe-shaped frames over the real vision port; the hand closes until the real gate closes to grab (aim assist picks
    /// what is looked at), opens to place, and squeezes in a closed loop on the camera force, as a patient would.
    /// </summary>
    public sealed class RehabActivityProbe : MonoBehaviour
    {
        [Serializable] public sealed class Run
        {
            public string activity; public int level, score, stepsDone, stepsTotal, errors, misses, actions, failedGrabs; public bool completed; public float seconds; public string notes = "";
            public OutcomeView[] outcomes;
        }
        [Serializable] public sealed class OutcomeView { public string key, label, unit; public float value; }
        [Serializable] public sealed class UiCheck { public string name, value; public bool passed; }
        [Serializable] public sealed class Report { public bool passed; public string utc, scope, detail = "", resolution; public int errors; public Run[] runs; public UiCheck[] uiChecks; }

        ApartmentExperience app; ActivityRunner runner; int errors;
        readonly List<Run> runs = new List<Run>();
        readonly List<UiCheck> uiChecks = new List<UiCheck>();
        static bool UiOnly => Array.IndexOf(Environment.GetCommandLineArgs(), "-activity-ui-only") >= 0;
        static string OnlyKind => Environment.GetCommandLineArgs().Where(a => a.StartsWith("-activity-kind=")).Select(a => a.Substring(15)).FirstOrDefault();
        static bool Camera => Array.IndexOf(Environment.GetCommandLineArgs(), "-activity-camera") >= 0 || Intent;
        // Intent mode (2026-11-21): the view stays put and the SYNTHETIC hand only moves part of the way toward each item or
        // place (IntentReach, default 0.7), slowly and with tremor; the movement assist must finish the move.
        static bool Intent => Array.IndexOf(Environment.GetCommandLineArgs(), "-activity-intent") >= 0;
        static float Arg(string name, float fallback) { var a = Environment.GetCommandLineArgs().FirstOrDefault(x => x.StartsWith(name + "=")); return a != null && float.TryParse(a.Substring(name.Length + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback; }
        // Camera mode (2026-11-21) reaches with the hand all the way and steadily (the game works by where the hand is, not
        // where the view looks); intent mode reaches part of the way with tremor.
        static float IntentReach => Intent ? Arg("-intent-reach", .7f) : 1f;
        static float IntentTremor => Intent ? Arg("-intent-tremor", .008f) : 0f;
        static bool HandReach => Camera;
        readonly System.Random shake = new System.Random(7);
        SqueezableObject swapped; // intent mode: the equal item actually held for this step
        float closure; bool sending; long sequence;
        // Tea: the synthetic hand's wrist roll (degrees), lift toward the top of the image (0..1) and an injected tremor.
        float roll, lift, tremorMm, tremorHz = 6;
        // Where the synthetic hand is in the image (offsets) and how big it looks (nearer = bigger: the game's depth cue).
        float handDx, handDy, handScale = 1, wantDx, wantDy, wantScale = 1;
        readonly List<UiCheck> teaChecks = new List<UiCheck>();
        void TeaCheck(string name, bool passed, string value) => teaChecks.Add(new UiCheck { name = name, passed = passed, value = value });
        void Check(string name, bool passed, string value) => uiChecks.Add(new UiCheck { name = name, passed = passed, value = value });

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-apartment-activity-probe") >= 0)
                new GameObject("Activity probe").AddComponent<RehabActivityProbe>();
        }
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string text, string stack, LogType kind) { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors++; }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), scope = Camera ? "SYNTHETIC camera-hand frames over the loopback vision port (closed-loop grip, aim assist) in the rendered Windows player; no webcam, device, person or records" : "SYNTHETIC keyboard-demo input in the rendered Windows player; no camera, device, person or records" };
            if (Camera) StartCoroutine(Stream());
            Exception failure = null; var routine = Play();
            while (true)
            {
                object next = null; bool more;
                try { more = routine.MoveNext(); if (more) next = routine.Current; }
                catch (Exception error) { failure = error; break; }
                if (!more) break;
                yield return next;
            }
            report.runs = runs.ToArray(); report.errors = errors; report.uiChecks = uiChecks.Concat(teaChecks).ToArray(); report.resolution = Screen.width + "x" + Screen.height;
            bool uiPassed = uiChecks.Count > 0 && uiChecks.All(c => c.passed);
            report.passed = failure == null && errors == 0 && uiPassed && (UiOnly || runs.Count == 2 * (OnlyKind != null ? 1 : Enum.GetValues(typeof(ActivityKind)).Length) && runs.All(r => r.completed) && teaChecks.All(c => c.passed));
            report.detail = failure != null ? failure.ToString() : errors > 0 ? errors + " error(s) logged" : !uiPassed ? "UI checks failed: " + string.Join(", ", uiChecks.Where(c => !c.passed).Select(c => c.name + "=" + c.value)) : runs.Any(r => !r.completed) ? "Not completed: " + string.Join(", ", runs.Where(r => !r.completed).Select(r => r.activity + " L" + r.level)) : "";
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/activity-probe.json", JsonUtility.ToJson(report, true));
            Debug.Log("[REHAB_ACTIVITY] " + (report.passed ? "PASS" : "FAIL") + " " + report.detail);
            Application.Quit(report.passed ? 0 : 1);
        }

        IEnumerator Play()
        {
            yield return new WaitForSecondsRealtime(2);
            var shell = FindAnyObjectByType<RehabAppShell>(); app = shell.experience;
            shell.SetResearchIdentity(null);
            shell.openModuleButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(1);
            app.SetHardware(false);
            runner = app.GetComponent<ActivityRunner>();
            if (runner == null) throw new InvalidOperationException("Activity runner missing");
            runner.RecordPlacement = true;
            yield return UiChecks(shell); // keyboard-mode UI and onboarding checks, before any camera switch
            if (UiOnly) { shell.ReturnHome(); yield break; }
            if (Camera) { app.SetCameraMode(); sending = true; closure = 0; yield return new WaitForSecondsRealtime(.5f); }
            foreach (int level in new[] { 2, 8 })
                foreach (ActivityKind kind in Enum.GetValues(typeof(ActivityKind)))
                {
                    if (OnlyKind != null && kind.ToString() != OnlyKind) continue; // development only: one activity
                    var run = new Run { activity = ActivityRules.Title(kind), level = level }; runs.Add(run);
                    if (!runner.Begin(kind, level)) { run.activity += " (did not start: " + runner.Feedback + ")"; continue; }
                    app.player.SetPaused(false); app.TestForce = 0;
                    yield return new WaitForSecondsRealtime(.8f);
                    var activity = runner.Current; double started = Time.realtimeSinceStartupAsDouble; int shot = 0;
                    foreach (var step in activity.Solution().ToList())
                    {
                        run.actions++;
                        yield return Act(step, run);
                        if (++shot == 2 && level == 2) yield return Shot("activity-" + ((int)kind + 1) + "-" + kind + "-during");
                        if (runner.LastResult != null) break;
                    }
                    // Camera hand: a patient who sees an item sticking out of the box picks it up and puts it back in the middle.
                    for (int fix = 0; Camera && fix < 2 && runner.LastResult == null && activity is StorageBoxActivity box2; fix++)
                    {
                        yield return new WaitForSecondsRealtime(1);
                        var problem = box2.LastInsideProblem; int cut = problem.IndexOf(" corner "); if (cut < 0) cut = problem.IndexOf(" moving ");
                        var name = cut > 0 ? problem.Substring(0, cut) : problem;
                        var item = activity.items.FirstOrDefault(a => a != null && a.name == name);
                        if (item == null) break;
                        run.notes += "corrected " + name + " (" + box2.LastInsideProblem + ") | ";
                        yield return CameraAct(ProbeStep.Place(item, () => box2.BoxCentre), run);
                    }
                    double wait = Time.realtimeSinceStartupAsDouble + 4;
                    while (runner.LastResult == null && Time.realtimeSinceStartupAsDouble < wait) yield return null;
                    if (activity is TeaActivity teaActivity) CheckTea(run, teaActivity.tea); // before the runner tidies the props away
                    if (level == 2) yield return Shot("activity-" + ((int)kind + 1) + "-" + kind + "-done");
                    var result = runner.LastResult;
                    run.completed = result != null; run.stepsDone = activity.steps.Current; run.stepsTotal = activity.steps.Count;
                    if (result == null && activity is StorageBoxActivity box) run.notes += "box: " + box.LastInsideProblem + " | ";
                    run.errors = activity.errors; run.misses = activity.misses; run.seconds = (float)(Time.realtimeSinceStartupAsDouble - started);
                    if (result != null) { run.score = result.score; run.outcomes = result.outcomes.Select(o => new OutcomeView { key = o.key, label = o.label, unit = o.unit, value = o.value }).ToArray(); }
                    yield return new WaitForSecondsRealtime(3f); // the runner reports and tidies up after completion
                    runner.End(false); app.TestForce = 0;
                    if (shell.Research != null && !shell.Research.LiveSession) { shell.openModuleButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(1); app.SetHardware(false); }
                    if (Camera && !app.CameraMode) { app.SetCameraMode(); closure = 0; yield return new WaitForSecondsRealtime(.5f); }
                }
            shell.ReturnHome();
        }

        // Stage 11 design-system checks on the live apartment UI (SYNTHETIC; no person).
        IEnumerator UiChecks(RehabAppShell shell)
        {
            string size = Screen.width + "x" + Screen.height;
            var choices = FindAnyObjectByType<ApartmentChoices>();
            var presentation = FindAnyObjectByType<GripRehab.UI.GripUiThemeController>();
            var guide = app.GetComponent<ApartmentGuide>();
            app.player.SetPaused(true); yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("options-panel-" + size);
            // 1. Nothing in the options panel overlaps, and the panel fits on screen.
            var panel = (RectTransform)choices.inputButton.transform.parent;
            var items = panel.GetComponentsInChildren<UnityEngine.UI.Button>().Select(b => (RectTransform)b.transform)
                .Concat(panel.GetComponentsInChildren<UnityEngine.UI.Text>().Where(t => t.GetComponentInParent<UnityEngine.UI.Button>() == null && t.enabled && t.text.Trim().Length > 0).Select(t => t.rectTransform)).ToList();
            var overlaps = new List<string>();
            for (int a = 0; a < items.Count; a++) for (int b = a + 1; b < items.Count; b++)
                    if (Intersect(items[a], items[b]) > 4) overlaps.Add(items[a].name + "/" + items[b].name);
            Check("options panel: no overlapping elements (" + size + ")", overlaps.Count == 0, overlaps.Count == 0 ? "0" : string.Join(";", overlaps.Take(4)));
            var corners = new Vector3[4]; panel.GetWorldCorners(corners);
            Check("options panel fits on screen (" + size + ")", corners[0].y >= 0 && corners[1].y <= Screen.height, corners[0].y.ToString("0") + ".." + corners[1].y.ToString("0"));
            // 2. Focus ring reaches 3:1 against every panel button in all three themes; selection has a marker.
            var es = UnityEngine.EventSystems.EventSystem.current;
            for (int theme = 0; theme < 3 && presentation != null; theme++)
            {
                presentation.Select(theme); yield return new WaitForSecondsRealtime(.5f);
                float worst = 99; string worstName = "";
                foreach (var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>())
                {
                    if (!button.IsInteractable()) continue;
                    es.SetSelectedGameObject(button.gameObject); yield return null; yield return null;
                    var feedback = button.GetComponent<GripRehab.UI.GripUiControlFeedback>();
                    float contrast = feedback != null && feedback.FocusVisible ? feedback.FocusContrast : 0;
                    if (contrast < worst) { worst = contrast; worstName = button.name; }
                }
                Check("focus ring contrast >= 3:1, theme " + theme, worst >= 3, worst.ToString("0.0") + " (" + worstName + ")");
                if (theme == 2) yield return Shot("options-panel-contrast-" + size);
            }
            es.SetSelectedGameObject(null);
            if (presentation != null) { presentation.Select(0); yield return new WaitForSecondsRealtime(.5f); }
            var living = choices.livingButton.GetComponent<GripRehab.UI.GripUiControlFeedback>();
            Check("selected room has a marker, not colour alone", living != null && living.Selected, living != null ? living.Selected.ToString() : "no feedback");
            app.player.SetPaused(false); yield return new WaitForSecondsRealtime(.4f);
            // 3. Input-mode strip: the active input in words, and later stages can report their state.
            var strip = guide != null ? guide.Strip : null; var hudOverlaps = new List<string>();
            if (strip != null)
                foreach (var t in strip.GetComponentInParent<Canvas>().GetComponentsInChildren<UnityEngine.UI.Text>())
                    if (t.enabled && t.canvasRenderer.GetInheritedAlpha() > .01f && t.color.a > .01f && t.text.Trim().Length > 0 && !t.transform.IsChildOf(strip) && Intersect(strip, t.rectTransform) > 4) hudOverlaps.Add(t.text.Trim());
            Check("input strip covers no HUD text (" + size + ")", strip != null && hudOverlaps.Count == 0, hudOverlaps.Count == 0 ? "0" : string.Join(";", hudOverlaps.Take(3)));
            var offscreen = strip.GetComponentInParent<Canvas>().GetComponentsInChildren<UnityEngine.UI.Graphic>()
                .Where(g => g.enabled && g.canvasRenderer.GetInheritedAlpha() > .01f && g.color.a > .01f && (g is UnityEngine.UI.Text tx ? tx.text.Trim().Length > 0 : g.GetComponent<UnityEngine.UI.Button>() != null))
                .Where(g => { var c4 = new Vector3[4]; g.rectTransform.GetWorldCorners(c4); return c4[0].x < -2 || c4[2].x > Screen.width + 2; })
                .Select(g => g.name).ToList();
            Check("HUD text and buttons stay on screen (" + size + ")", offscreen.Count == 0, offscreen.Count == 0 ? "0" : string.Join(";", offscreen.Take(4)));
            Check("input strip names the active input", guide != null && guide.ChannelText(ApartmentGuide.Channel.Input).Contains("KEYBOARD DEMO"), guide != null ? guide.ChannelText(ApartmentGuide.Channel.Input) : "no guide");
            ApartmentGuide.Set(ApartmentGuide.Channel.Voice, true, "LISTENING"); yield return null; yield return null;
            Check("input strip shows a reported state", guide.ChannelText(ApartmentGuide.Channel.Voice) == "VOICE  LISTENING", guide.ChannelText(ApartmentGuide.Channel.Voice));
            ApartmentGuide.Clear(ApartmentGuide.Channel.Voice); yield return null;
            // 4. Onboarding completes from real actions: look, grab, squeeze, release, open options.
            guide.Restart(true); yield return new WaitForSecondsRealtime(.6f);
            yield return Shot("onboarding-step1-" + size);
            var p = app.player.transform; app.player.LookAt(p.position + Quaternion.Euler(0, 50, 0) * Vector3.ProjectOnPlane(p.forward, Vector3.up) * 2 + Vector3.down * .3f);
            yield return new WaitForSecondsRealtime(.4f); int afterLook = guide.Step;
            var ball = choices.SelectedObject; app.player.LookAt(ball.transform.position); yield return new WaitForSecondsRealtime(.3f);
            app.TryGrab(ball); yield return new WaitForSecondsRealtime(.6f); int afterGrab = guide.Step;
            yield return Shot("onboarding-step3-" + size);
            app.TestForce = 12; yield return new WaitForSecondsRealtime(.6f); app.TestForce = 0; int afterSqueeze = guide.Step;
            app.Release(); yield return new WaitForSecondsRealtime(.8f); int afterRelease = guide.Step;
            app.player.SetPaused(true); yield return new WaitForSecondsRealtime(.3f); app.player.SetPaused(false); yield return new WaitForSecondsRealtime(.5f);
            yield return Shot("onboarding-done-" + size);
            Check("onboarding advances from real actions", afterLook == 1 && afterGrab == 2 && afterSqueeze == 3 && afterRelease == 4 && !guide.Showing,
                afterLook + "," + afterGrab + "," + afterSqueeze + "," + afterRelease + " showing=" + guide.Showing);
            PlayerPrefs.DeleteKey(ApartmentGuide.DoneKey); PlayerPrefs.Save();
            yield return new WaitForSecondsRealtime(4.2f);
        }

        static float Intersect(RectTransform a, RectTransform b)
        {
            var ca = new Vector3[4]; var cb = new Vector3[4]; a.GetWorldCorners(ca); b.GetWorldCorners(cb);
            float w = Mathf.Min(ca[2].x, cb[2].x) - Mathf.Max(ca[0].x, cb[0].x), h = Mathf.Min(ca[2].y, cb[2].y) - Mathf.Max(ca[0].y, cb[0].y);
            return w > 0 && h > 0 ? w * h : 0;
        }

        IEnumerator Act(ProbeStep step, Run run)
        {
            if (Camera) { yield return CameraAct(step, run); yield break; }
            switch (step.kind)
            {
                case ProbeStep.Kind.Place:
                case ProbeStep.Kind.Grab:
                    if (app.Held != step.item.Item)
                    {
                        if (app.Held != null) { app.Release(); yield return new WaitForSecondsRealtime(.9f); }
                        app.player.LookAt(step.item.transform.position); yield return new WaitForSecondsRealtime(.25f);
                        if (!app.TryGrab(step.item.Item)) { run.failedGrabs++; yield break; }
                        yield return new WaitForSecondsRealtime(.7f);
                    }
                    if (step.kind == ProbeStep.Kind.Place)
                    {
                        app.player.LookAt(step.aim()); yield return new WaitForSecondsRealtime(.35f);
                        app.Release(); yield return new WaitForSecondsRealtime(1.1f);
                    }
                    break;
                case ProbeStep.Kind.Release:
                    app.player.LookAt(step.aim()); yield return new WaitForSecondsRealtime(.3f);
                    app.Release(); yield return new WaitForSecondsRealtime(.9f);
                    break;
                case ProbeStep.Kind.Squeeze:
                    app.player.LookAt(step.aim()); yield return new WaitForSecondsRealtime(.3f);
                    app.TestForce = step.newtons; yield return new WaitForSecondsRealtime(step.seconds); app.TestForce = 0;
                    yield return new WaitForSecondsRealtime(.4f);
                    break;
                case ProbeStep.Kind.Chop:
                    app.player.LookAt(step.aim()); yield return new WaitForSecondsRealtime(.35f);
                    app.TestForce = step.newtons; yield return new WaitForSecondsRealtime(.45f);
                    app.TestForce = 0; yield return new WaitForSecondsRealtime(.45f);
                    break;
                case ProbeStep.Kind.Pour:
                case ProbeStep.Kind.Drink:
                    yield return Tea(step, run, d => { var t = Tea(); if (t != null) t.TestTilt = d; }, m => { var t = Tea(); if (t != null) t.TestMouth = m; });
                    break;
            }
        }

        // ---- Camera hand (-activity-camera)
        IEnumerator CameraAct(ProbeStep step, Run run)
        {
            switch (step.kind)
            {
                case ProbeStep.Kind.Place:
                case ProbeStep.Kind.Grab:
                    swapped = null;
                    if (Intent && step.item.snappedTo != null && app.Held == null) { run.notes += step.item.Item.displayName + " already placed (an equal choice earlier): step done | "; break; }
                    if (app.Held != step.item.Item)
                    {
                        // Open the hand first, as a patient does: after the game releases an object (activity end), a hand
                        // that stays closed cannot grab again until it opens (intended; the status line says so).
                        // Look at the item's centre; if something in front of it comes up instead, open and look at another
                        // visible part (top, sides), as a patient shifts their gaze.
                        var bounds = step.item.Item.GetComponentInChildren<Renderer>() != null ? step.item.Item.GetComponentInChildren<Renderer>().bounds : new Bounds(step.item.transform.position, Vector3.one * .05f);
                        var looks = new[] { step.item.transform.position, bounds.center + Vector3.up * bounds.extents.y * .8f, bounds.center + Vector3.left * bounds.extents.x * .7f, bounds.center + Vector3.right * bounds.extents.x * .7f };
                        if (HandReach) { yield return IntentGrab(step.item.Item, run); looks = new Vector3[0]; }
                        foreach (var look in looks)
                        {
                            yield return OpenHand(); yield return new WaitForSecondsRealtime(.4f);
                            app.player.LookAt(look); yield return new WaitForSecondsRealtime(.25f);
                            yield return CloseUntilHeld(step.item.Item, 3f);
                            if (app.Held == step.item.Item) break;
                            if (app.Held != null) { var eye = app.player.view.transform.position; var surface = step.item.Item.ClosestSurface(eye);
                                run.notes += "look shifted (got " + app.Held.displayName + ", intent=" + (app.Intent.TargetId >= 0 ? app.Intent.TargetId.ToString() : "-") + " conf " + app.Intent.Confidence.ToString("0.00") + " under=" + (app.HandRayTarget != null ? app.HandRayTarget.displayName : "-") + ", reach=" + Diag(app, "CanReach", step.item.Item) + ", blocker=" + (Physics.Raycast(eye, (surface - eye).normalized, out var blockHit, Vector3.Distance(eye, surface) + .01f, ~(1 << 8), QueryTriggerInteraction.Ignore) ? blockHit.collider.name + "<" + (blockHit.collider.transform.parent != null ? blockHit.collider.transform.parent.name : "") + "> layer " + LayerMask.LayerToName(blockHit.collider.gameObject.layer) + " at " + blockHit.distance.ToString("0.00") + "/" + Vector3.Distance(eye, surface).ToString("0.00") : "none") + ") | "; }
                        }
                        if (app.Held != step.item.Item && !(swapped != null && app.Held == swapped)) { run.failedGrabs++; run.notes += "want=" + step.item.Item.displayName + " held=" + (app.Held != null ? app.Held.displayName : "none") + " score=" + app.CameraGripScore.ToString("0.00") + " gate=" + app.CameraGateClosed + " dist=" + Vector3.Distance(app.player.view.transform.position, step.item.transform.position).ToString("0.00") + " status=" + app.Status + " | "; yield break; }
                        yield return new WaitForSecondsRealtime(.5f);
                    }
                    if (step.kind == ProbeStep.Kind.Place && HandReach)
                    {
                        yield return IntentReachTo(step.aim(), run);
                        var place = app.IntentPlace; var aimAt = step.aim(); var itemAt = app.Held != null ? app.Held.transform.position : Vector3.zero;
                        run.notes += "place: intent=" + (place != null ? place.label : "none") + " item-to-aim " + (new Vector2(itemAt.x - aimAt.x, itemAt.z - aimAt.z).magnitude * 1000).ToString("0") + " mm"
                            + (place != null ? " item-to-intent " + (new Vector2(itemAt.x - place.WorldPosition.x, itemAt.z - place.WorldPosition.z).magnitude * 1000).ToString("0") + " mm" : "") + " pull " + app.Intent.OffsetU.ToString("0.00") + "," + app.Intent.OffsetV.ToString("0.00")
                            + (place != null ? " hand-to-point " + (Vector3.Distance(app.ActiveHand.transform.position, app.IntentPlacePoint) * 1000).ToString("0") + " mm reach " + app.CameraReach.ToString("0.000") + " want " + Vector3.Distance(app.player.view.transform.position, app.IntentPlacePoint).ToString("0.000") : "")
                            + (app.Held != null && app.Held.Body != null ? " com-pivot " + (Vector3.Distance(app.Held.Body.worldCenterOfMass, app.Held.transform.position) * 1000).ToString("0") + " mm" : "") + " | ";
                        yield return OpenHand(); yield return new WaitForSecondsRealtime(1.1f);
                        run.notes += "-> " + runner.Feedback + " | ";
                        wantDx = wantDy = 0;
                    }
                    else if (step.kind == ProbeStep.Kind.Place)
                    {
                        app.player.LookAt(step.aim()); yield return new WaitForSecondsRealtime(.35f);
                        if (Camera) { var aimAt = step.aim(); var itemAt = app.Held != null ? app.Held.transform.position : Vector3.zero; var place = app.IntentPlace;
                            run.notes += "camera place: intent=" + (place != null ? place.label : "none") + " item-to-aim " + (new Vector2(itemAt.x - aimAt.x, itemAt.z - aimAt.z).magnitude * 1000).ToString("0") + " mm | "; }
                        float yawBefore = runner.PlacementYaw;
                        yield return OpenHand(); yield return new WaitForSecondsRealtime(1.1f);
                        if (Camera) run.notes += "-> " + runner.Feedback + " | ";
                        if (runner.Current is StorageBoxActivity)
                            run.notes += runner.LastPlacement + " | ";
                    }
                    break;
                case ProbeStep.Kind.Release:
                    app.player.LookAt(step.aim()); yield return new WaitForSecondsRealtime(.3f);
                    yield return OpenHand(); yield return new WaitForSecondsRealtime(.9f);
                    break;
                case ProbeStep.Kind.Squeeze:
                case ProbeStep.Kind.Chop:
                    if (HandReach && ApartmentExperience.AssistSetting >= AssistLevel.Medium && app.Held != null)
                    {
                        // Movement assist: bring the tool to its work (part of the way in intent mode) and wait; the game cuts
                        // or stirs by itself (no squeeze, no chopping motion).
                        int stepBefore = runner.Current.steps.Current, doneBefore = runner.Current.steps.CurrentDone; // a step can count several cuts
                        yield return IntentReachTo(app.ActiveHand.transform.position + (step.aim() - app.Held.transform.position), run);
                        float until = Time.realtimeSinceStartup + (step.kind == ProbeStep.Kind.Squeeze ? step.seconds + 4 : 4);
                        while (Time.realtimeSinceStartup < until && runner.Current.steps.Current == stepBefore && runner.Current.steps.CurrentDone == doneBefore && runner.LastResult == null) yield return null;
                        run.notes += "auto " + step.kind + ": step " + stepBefore + " -> " + runner.Current.steps.Current + " (" + runner.Feedback + ") | ";
                        wantDx = wantDy = 0;
                        break;
                    }
                    app.player.LookAt(step.aim()); yield return new WaitForSecondsRealtime(.3f);
                    string seenArmed = ""; float peak = 0; yield return SqueezeTo(step.newtons, step.kind == ProbeStep.Kind.Chop ? .45f : step.seconds, f => { if (f > peak) { peak = f; seenArmed = DiagField(runner, "chopArmed") + " valid=" + app.ForceValid + " kind=" + (app.Held != null && app.Held.GetComponent<ActivityItem>() != null ? app.Held.GetComponent<ActivityItem>().kind : "-") + " step=" + runner.Current.steps.Current + " ray=" + Diag(runner, "AimedCollider"); } });
                    run.notes += "[" + seenArmed + " fb=" + runner.Feedback + "] ";
                    if (step.kind == ProbeStep.Kind.Chop) { yield return Relax(step.newtons * .3f); yield return new WaitForSecondsRealtime(.3f);  run.notes += relaxLog + " | relaxed: F=" + app.ForceNewtons.ToString("0.0") + " score=" + app.CameraGripScore.ToString("0.00") + " closure=" + closure.ToString("0.00") + " " + DiagField(runner, "chopArmed") + " | "; } // re-arms the next cut (force under half the chop level)
                    run.notes += step.kind + " want=" + step.newtons.ToString("0") + "N peak=" + peak.ToString("0.0") + "N held=" + (app.Held != null ? app.Held.displayName : "none") + " ray=" + Diag(runner, "AimedCollider") + " | ";
                    yield return new WaitForSecondsRealtime(step.kind == ProbeStep.Kind.Chop ? .45f : .4f);
                    break;
                case ProbeStep.Kind.Pour:
                case ProbeStep.Kind.Drink:
                    // Hand roll R maps to tilt (R - 6) * 1.25 after the dead band, so roll = tilt / 1.25 + 6.
                    yield return Tea(step, run, d => roll = d <= 0 ? 0 : d / 1.25f + 6, m => lift = m ? 1 : 0);
                    break;
            }
        }

        TeaPouring Tea() => runner != null && runner.Current is TeaActivity t ? t.tea : null;

        // ---- Intent mode: reach part of the way and let the movement assist finish.
        Vector3 ActivityCentre() { var items = runner.Current.items.Where(i => i != null).ToList(); return items.Count == 0 ? app.player.view.transform.position + app.player.view.transform.forward : items.Aggregate(Vector3.zero, (a, i) => a + i.transform.position) / items.Count; }
        IEnumerator IntentReachTo(Vector3 world, Run run)
        {
            // Far targets: a patient steps closer first (point-walk), to about 1 m.
            var eye0 = app.player.view.transform.position; var flat = world - eye0; flat.y = 0;
            if (flat.magnitude > 1.1f) run.notes += "far " + flat.magnitude.ToString("0.00") + " m seated=" + app.player.IsSeated + " | ";
            if (flat.magnitude > 1.25f && !app.player.IsSeated) { app.player.Teleport(app.player.transform.position + flat.normalized * (flat.magnitude - 1f), app.player.transform.rotation); yield return new WaitForSecondsRealtime(.2f); run.notes += "stepped closer | "; }
            // The view rests between the activity and the target, so the target is off-centre (a real reach, not a look).
            app.player.LookAt(Vector3.Lerp(ActivityCentre(), world, .5f)); yield return new WaitForSecondsRealtime(.3f);
            var cam = app.player.view; float tanV = Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * .92f, tanH = tanV * cam.aspect;
            var l = cam.transform.InverseTransformPoint(world); if (l.z < .08f) { run.notes += "intent: target behind | "; yield break; }
            float u = l.x / (l.z * tanH), v = (l.y / l.z + .05f) / tanV, scale = app.Intent.RangeScale, reach = IntentReach;
            // Inverse of the game's hand mapping, only part of the way: wrist image x = .5 + u * rangeX, y = centre - v * rangeY.
            float x = .5f + u * ApartmentExperience.HandRangeX * scale * reach, y = ApartmentExperience.HandCentreY - v * ApartmentExperience.HandRangeY * scale * reach;
            wantDx = x - .5f; wantDy = y - ApartmentExperience.HandCentreY;
            float end = Time.realtimeSinceStartup + 2.5f;
            while (Time.realtimeSinceStartup < end && (Mathf.Abs(handDx - wantDx) > .01f || Mathf.Abs(handDy - wantDy) > .01f)) yield return null;
            yield return new WaitForSecondsRealtime(1.2f); // a patient pauses before opening; the assist pulls the rest of the way
            run.notes += "intent: target u " + u.ToString("0.00") + " v " + v.ToString("0.00") + " -> engaged=" + app.Intent.Engaged + " conf " + app.Intent.Confidence.ToString("0.00") + " | ";
        }
        IEnumerator IntentGrab(SqueezableObject item, Run run)
        {
            if (item.GetComponent<ActivityItem>() is ActivityItem placed && placed.snappedTo != null && Intent) { run.notes += item.displayName + " already placed (an equal choice earlier) | "; yield break; }
            yield return OpenHand(); yield return new WaitForSecondsRealtime(.4f);
            yield return IntentReachTo(item.transform.position, run);
            run.notes += "before close: intent=" + (app.IntentItem != null ? app.IntentItem.displayName : "-") + " conf " + app.Intent.Confidence.ToString("0.00") + " under=" + (app.HandRayTarget != null ? app.HandRayTarget.displayName : "-") + " | ";
            yield return CloseUntilHeld(item, 3f);
            if (app.Held != null && app.Held != item)
            {
                // An equal choice (another vegetable for the same pan) is a valid move for the activity, not a wrong grab.
                var want = item.GetComponent<ActivityItem>(); var got = app.Held.GetComponent<ActivityItem>();
                bool equal = Intent && want != null && got != null && got.snappedTo == null && runner.Current.targets.Any(t => t.accepts != null && t.accepts(want) && t.accepts(got) && !t.Full && runner.Current.Allows(got, t));
                run.notes += (equal ? "equal choice " : "intent WRONG GRAB ") + app.Held.displayName + " for " + item.displayName + " | ";
                if (equal) { swapped = app.Held; yield break; }
            }
            wantDx = wantDy = 0;
        }

        // Pour or drink the way a patient watches the cup: tip, slow down near the line, level off; raise the cup, tip, sip.
        IEnumerator Tea(ProbeStep step, Run run, Action<float> tilt, Action<bool> mouth)
        {
            var tea = Tea(); if (tea == null) { run.notes += "no tea | "; yield break; }
            float Seconds() => Time.realtimeSinceStartup;
            if (step.kind == ProbeStep.Kind.Pour)
            {
                app.player.LookAt(step.aim()); yield return new WaitForSecondsRealtime(.4f);
                // Bring the spout over the cup first (a person does not tip the pot until it is there). With the camera
                // hand that is the hand's own position and depth, steered by sight.
                // With the movement assist (Medium or High) the game brings the spout over the cup; the hand just holds it there.
                if (Camera && ApartmentExperience.AssistSetting >= AssistLevel.Medium)
                {
                    // A patient moves the teapot toward the cup (part of the way in intent mode), then the assist finishes.
                    yield return IntentReachTo(app.ActiveHand.transform.position + (tea.AimWant - tea.SpoutTipWorld), run);
                    float until = Time.realtimeSinceStartup + 6; while (Time.realtimeSinceStartup < until && tea.AimError > .015f) yield return null;
                    run.notes += "assist brought the spout to " + (tea.AimError * 1000).ToString("0") + " mm | ";
                }
                else if (Camera) yield return Steer(() => tea.SpoutTipWorld, () => tea.AimWant, .015f, 14, run);
                float settle = Seconds() + 5; while (tea.AimError > .015f && Seconds() < settle) yield return null;
                run.notes += "aim error before tipping " + (tea.AimError * 1000).ToString("0") + " mm | ";
                if (run.level == 2) yield return Shot("activity-7-pourtea-aim" + (Camera ? "-camera" : ""));
                if (Camera && run.level == 8) tremorMm = 1.5f; // a known 1.5 mm RMS, 6 Hz tremor while pouring and drinking
                float end = Seconds() + 30, angle = 0, nextNote = 0, nextFix = 0; bool midShot = false;
                while (tea.CupMl < step.ml - 3 && tea.PotMl > 1 && Seconds() < end)
                {
                    float left = step.ml - tea.CupMl;
                    float want = left > 40 ? 62 : left > 15 ? 48 : 40; // slower near the line
                    if (tea.AimError > .03f) want = Mathf.Min(want, 15); // the spout drifted off the cup: tip back while it comes over again
                    if (Camera && Seconds() > nextFix && tea.AimError > .008f) { nextFix = Seconds() + .12f; Correct(tea.AimWant - tea.SpoutTipWorld, .8f); }
                    angle = Mathf.MoveTowards(angle, want, 60 * Time.unscaledDeltaTime); tilt(angle); yield return null;
                    if (run.level == 2 && !midShot && tea.CupMl > step.ml * .4f && tea.FlowMlPerSecond > 15) { midShot = true; yield return Shot("activity-7-pourtea-pouring" + (Camera ? "-camera" : "")); }
                    if (Seconds() > nextNote) { nextNote = Seconds() + .6f; var e = tea.AimWant - tea.SpoutTipWorld; run.notes += "<a=" + angle.ToString("0") + " err=" + (e * 1000).ToString("F0") + " dx=" + handDx.ToString("0.00") + " dy=" + handDy.ToString("0.00") + " s=" + handScale.ToString("0.00") + " yaw=" + app.player.transform.eulerAngles.y.ToString("0.0") + " pitch=" + app.player.view.transform.localEulerAngles.x.ToString("0.0") + " flow=" + tea.FlowMlPerSecond.ToString("0") + "> "; }
                }
                tilt(0); yield return new WaitForSecondsRealtime(1.4f);
                if (Camera) { wantDx = wantDy = 0; wantScale = 1; }
                run.notes += "poured " + tea.PouredMl.ToString("0") + " ml, cup " + tea.CupMl.ToString("0") + "/" + step.ml.ToString("0") + " ml, spilled " + tea.SpilledMl.ToString("0.0") + " ml | ";
            }
            else
            {
                run.notes += "before mouth: carry spill " + tea.CarrySpillMl.ToString("0.0") + " ml, cup " + tea.CupMl.ToString("0") + " ml " + tea.SpillLog + " | ";
                mouth(true); float end = Seconds() + 6;

                while (!tea.AtMouth && Seconds() < end) yield return null;
                run.notes += "at mouth=" + tea.AtMouth + " after " + (6 - (end - Seconds())).ToString("0.0") + " s (rim to lips " + (tea.MouthDistance * 1000).ToString("0") + " mm, carry spill so far " + tea.CarrySpillMl.ToString("0.0") + " ml) | ";
                // Sip: tip while the cup is at the lips (never away from them), a little further as it empties.
                float angle = 0, nextSip = 0; end = Seconds() + 40;
                while (tea.CupMl > 2 && Seconds() < end)
                {
                    // Tip further only while the lips keep up (a person sips; tipping faster spills down the chin).
                    float want = !tea.AtMouth ? 0 : tea.SipSaturated ? angle : 110;

                    if (Seconds() > nextSip) { nextSip = Seconds() + 2; run.notes += "{a=" + angle.ToString("0") + " mouth=" + tea.AtMouth + " d=" + (tea.MouthDistance * 1000).ToString("0") + " cup=" + tea.CupMl.ToString("0") + " sat=" + tea.SipSaturated + " tilt=" + tea.CurrentTilt.ToString("0") + " po=" + app.PresentationOffset.z.ToString("0.00") + " blend=" + app.HeldPivotBlend.ToString("0.00") + " " + DiagField(tea, "raised") + " held=" + (app.Held != null ? app.Held.displayName : "none") + " lift=" + lift + "} "; }
                    angle = Mathf.MoveTowards(angle, want, (tea.AtMouth ? 15 : 45) * Time.unscaledDeltaTime); tilt(angle); yield return null;
                }
                tilt(0); yield return new WaitForSecondsRealtime(.8f); mouth(false); if (Camera) { wantDx = wantDy = 0; wantScale = 1; } yield return new WaitForSecondsRealtime(1.2f);
                tremorMm = 0;
                run.notes += "drank " + tea.DrankMl.ToString("0") + " ml, left " + tea.CupMl.ToString("0.0") + " ml, carry spill " + tea.CarrySpillMl.ToString("0.0") + " ml | ";
            }
        }

        // ---- Steering the synthetic hand by sight: how does a point (spout tip, cup rim) move when the hand moves right,
        // up or nearer? Measure that once, then correct toward the target in small steps, as a person adjusts by eye.
        readonly Vector3[] jacobian = new Vector3[3];
        IEnumerator Steer(Func<Vector3> point, Func<Vector3> target, float tolerance, float seconds, Run run, float minDy = -.3f, float maxDy = .3f)
        {
            float[] probe = { .03f, .03f, .08f };
            yield return new WaitForSecondsRealtime(.6f);
            for (int k = 0; k < 3; k++)
            {
                var before = point(); Nudge(k, probe[k]); yield return new WaitForSecondsRealtime(.7f);
                jacobian[k] = (point() - before) / probe[k]; Nudge(k, -probe[k]); yield return new WaitForSecondsRealtime(.7f);
            }
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end && (target() - point()).magnitude > tolerance)
            {
                Correct(target() - point(), .7f, minDy, maxDy); yield return new WaitForSecondsRealtime(.35f);
            }
            run.notes += "steered to " + ((target() - point()).magnitude * 1000).ToString("0") + " mm (hand dx " + handDx.ToString("0.00") + " dy " + handDy.ToString("0.00") + " scale " + handScale.ToString("0.00") + ") | ";
        }
        void Nudge(int k, float d) { if (k == 0) wantDx += d; else if (k == 1) wantDy += d; else wantScale += d; }
        // One damped least-squares step: hand change u minimising |J u - error|.
        void Correct(Vector3 error, float gain, float minDy = -.3f, float maxDy = .3f)
        {
            var a = new float[3, 3]; var b = new float[3];
            for (int i = 0; i < 3; i++) { b[i] = Vector3.Dot(jacobian[i], error); for (int j = 0; j < 3; j++) a[i, j] = Vector3.Dot(jacobian[i], jacobian[j]) + (i == j ? 1e-4f : 0); }
            var u = Solve3(a, b);
            // Small steps, and the hand stays clear of the image edges (where the hand-look would turn the view).
            for (int i = 0; i < 3; i++) u[i] = Mathf.Clamp(gain * u[i], i == 2 ? -.05f : -.025f, i == 2 ? .05f : .025f);
            wantDx = Mathf.Clamp(wantDx + u[0], -.2f, .2f); wantDy = Mathf.Clamp(wantDy + u[1], Mathf.Max(minDy, -.2f), Mathf.Min(maxDy, .2f)); wantScale = Mathf.Clamp(wantScale + u[2], .75f, 1.4f);
        }
        static float[] Solve3(float[,] a, float[] b)
        {
            for (int c = 0; c < 3; c++)
            {
                int p = c; for (int r = c + 1; r < 3; r++) if (Mathf.Abs(a[r, c]) > Mathf.Abs(a[p, c])) p = r;
                for (int k = 0; k < 3; k++) { var t = a[c, k]; a[c, k] = a[p, k]; a[p, k] = t; } { var t = b[c]; b[c] = b[p]; b[p] = t; }
                if (Mathf.Abs(a[c, c]) < 1e-9f) return new float[3];
                for (int r = c + 1; r < 3; r++) { float f = a[r, c] / a[c, c]; for (int k = c; k < 3; k++) a[r, k] -= f * a[c, k]; b[r] -= f * b[c]; }
            }
            var x = new float[3];
            for (int r = 2; r >= 0; r--) { float s = b[r]; for (int k = r + 1; k < 3; k++) s -= a[r, k] * x[k]; x[r] = s / a[r, r]; }
            return x;
        }

        void CheckTea(Run run, TeaPouring tea)
        {
            if (tea == null) return;
            string id = (Camera ? "camera" : "keyboard") + " level " + run.level + ": ";
            float tol = TeaRules.Tolerance(run.level);
            TeaCheck(id + "cup filled to the line", Mathf.Abs(tea.FilledMl - tea.TargetMl) <= tea.TargetMl * tol, tea.FilledMl.ToString("0") + " of " + tea.TargetMl.ToString("0") + " ml (tolerance " + (tol * 100).ToString("0") + " %)");
            TeaCheck(id + "volume conserved", Mathf.Abs(TeaPouring.StartMl - tea.PotMl - (tea.CupMl + tea.SpilledMl + tea.DrankMl)) < 1, (TeaPouring.StartMl - tea.PotMl).ToString("0.0") + " ml out of the pot = " + (tea.CupMl + tea.SpilledMl + tea.DrankMl).ToString("0.0") + " ml");
            TeaCheck(id + "a careful pour keeps the tea in the cup", TeaRules.PourAccuracy(tea.IntoCupMl, tea.PouredMl) >= .95f, (100 * TeaRules.PourAccuracy(tea.IntoCupMl, tea.PouredMl)).ToString("0") + " % in the cup");
            TeaCheck(id + "the cup was drunk empty", tea.CupMl < 3 && tea.DrankMl > tea.FilledMl * .8f, tea.DrankMl.ToString("0") + " ml drunk, " + tea.CupMl.ToString("0.0") + " ml left");
            if (Camera && run.level == 8)
            {
                TeaCheck(id + "injected tremor measured (1.5 mm RMS, 6 Hz)", tea.tremor.Samples > 0 && Mathf.Abs(tea.tremor.MeanRmsMm - 1.5f) <= .45f && Mathf.Abs(tea.tremor.DominantHz - 6) <= .75f,
                    tea.tremor.Samples + " windows, " + tea.tremor.MeanRmsMm.ToString("0.00") + " mm, " + tea.tremor.DominantHz.ToString("0.00") + " Hz");
            }
            else if (Camera)
                TeaCheck(id + "a steady hand reads as small tremor", tea.tremor.Samples > 0 && tea.tremor.MeanRmsMm < .6f, tea.tremor.MeanRmsMm.ToString("0.00") + " mm");
            else
                TeaCheck(id + "keyboard runs report no tremor", tea.tremor.Samples == 0, tea.tremor.Samples + " windows");
        }

        // Close gently (no harder than the gate needs) until the object is held; the hold keeps a light grip.
        IEnumerator CloseUntilHeld(SqueezableObject item, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (app.Held != item && Time.realtimeSinceStartup < end) { if (app.CameraGripScore < .66f) closure = Mathf.Min(1, closure + .03f); yield return null; }
        }
        IEnumerator OpenHand()
        {
            closure = 0; float end = Time.realtimeSinceStartup + 2;
            while (app.Held != null && Time.realtimeSinceStartup < end) yield return null;
        }
        // Closed loop on the camera force, like a patient watching the force readout; never lets go (closure stays above the gate's open level).
        IEnumerator SqueezeTo(float newtons, float seconds, Action<float> seen)
        {
            float end = Time.realtimeSinceStartup + seconds + .6f, hold = closure;
            while (Time.realtimeSinceStartup < end) { closure = Mathf.Clamp(closure + Time.unscaledDeltaTime * .05f * (newtons - app.ForceNewtons), .45f, 1.4f); seen(app.ForceNewtons); yield return null; }
            closure = Mathf.Max(.45f, hold);
        }

        // Diagnostics only: what a private aim helper returns right now (name of the collider/object, or its bool).
        static string Diag(object target, string method, params object[] args)
        {
            var m = target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (m == null) return "?";
            var r = m.Invoke(target, args);
            return r is Component c ? c.name + "(" + LayerMask.LayerToName(c.gameObject.layer) + ")" : r == null ? "none" : r.ToString();
        }

        static string DiagField(object target, string field)
        {
            var f = target.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            return f == null ? "?" : field + "=" + f.GetValue(target);
        }

        // Relax the grip until the force is low, without opening the hand far enough to drop what is held.
        string relaxLog = "";
        IEnumerator Relax(float newtons)
        {
            float end = Time.realtimeSinceStartup + 2; int frames = 0;
            relaxLog = "entry F=" + app.ForceNewtons.ToString("0.0") + " target=" + newtons.ToString("0.0") + " score=" + app.CameraGripScore.ToString("0.00") + " closure=" + closure.ToString("0.00");
            // Open while the force is high, but back off whenever the grasp score nears the gate's open level, so the tool is never dropped.
            while (app.ForceNewtons > newtons && Time.realtimeSinceStartup < end) { closure = Mathf.Clamp(closure + (app.CameraGripScore > .32f ? -.01f : .01f), 0, 1.4f); frames++; yield return null; }
            relaxLog += " frames=" + frames + " exit F=" + app.ForceNewtons.ToString("0.0") + " score=" + app.CameraGripScore.ToString("0.00") + " closure=" + closure.ToString("0.00");
        }

        IEnumerator Stream()
        {
            using (var udp = new UdpClient())
            {
                var target = new IPEndPoint(IPAddress.Loopback, 27817);
                while (true)
                {
                    handDx = Mathf.MoveTowards(handDx, wantDx, .45f / 30); handDy = Mathf.MoveTowards(handDy, wantDy, .45f / 30); handScale = Mathf.MoveTowards(handScale, wantScale, 1f / 30);
                    if (sending) { var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(Frame(closure))); udp.Send(bytes, bytes.Length, target); }
                    yield return new WaitForSecondsRealtime(1 / 30f);
                }
            }
        }

        // A right hand in the middle of the image (the free work zone), fingers curled by closure 0 (open) .. 1 (fist) .. 1.4 (tight fist).
        LandmarkFrame Frame(float c)
        {
            var f = new LandmarkFrame { schemaVersion = 1, source = "mediapipe-test", handedness = "Right", confidence = .95f, sequence = ++sequence,
                captureSeconds = Time.realtimeSinceStartupAsDouble, ageMs = 1, landmarks = new Vector3[21] };
            // The resting wrist row (ApartmentExperience.HandCentreY) points the camera hand at the view centre (2026-11-21:
            // the old fixed 0.58 pointed above it once the mapping was centred on the measured resting hand).
            float cx = .5f + handDx, wy = ApartmentExperience.HandCentreY - .36f * lift + handDy;
            f.imageWidth = 640; f.imageHeight = 480;
            f.landmarks[0] = new Vector3(cx, wy, 0);
            for (int finger = 0; finger < 4; finger++)
            {
                int n = 5 + finger * 4; float x = cx + (finger - 1.5f) * .045f;
                f.landmarks[n] = new Vector3(x, wy - .10f, 0);
                f.landmarks[n + 1] = new Vector3(x, wy - .18f, 0);
                f.landmarks[n + 2] = new Vector3(x, wy + Mathf.Lerp(-.26f, -.12f, c), 0);
                f.landmarks[n + 3] = new Vector3(x, wy + Mathf.Lerp(-.34f, -.04f, c), 0);
            }
            for (int i = 1; i < 5; i++) f.landmarks[i] = new Vector3(cx - (.12f - i * .015f) - .04f, wy - .03f - i * .02f, 0);
            // Wrist roll in the image (pixels, so the angle is true on a 640x480 frame), then the tremor shake.
            if (roll != 0)
            {
                float a = roll * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                for (int i = 1; i < 21; i++) { var d = f.landmarks[i] - f.landmarks[0]; float px = d.x * 640, py = d.y * 480; f.landmarks[i] = f.landmarks[0] + new Vector3((px * cs - py * sn) / 640, (px * sn + py * cs) / 480, 0); }
            }
            if (handScale != 1)
                for (int i = 1; i < 21; i++) { var d = f.landmarks[i] - f.landmarks[0]; f.landmarks[i] = f.landmarks[0] + d * handScale; }
            if (IntentTremor > 0 && Tea() == null) // the tea run measures tremor itself: it keeps its own steady/known hand
            {
                // A shaky hand (intent mode): the whole hand moves by a random amount each frame (image units).
                var jitter = new Vector3((float)(shake.NextDouble() - .5) * 2 * IntentTremor, (float)(shake.NextDouble() - .5) * 2 * IntentTremor, 0);
                for (int i = 0; i < 21; i++) f.landmarks[i] += jitter;
            }
            if (tremorMm > 0)
            {
                // TeaPouring scales pixels to millimetres by an 80 mm knuckle breadth; this hand's breadth in pixels sets the scale.
                float breadth = new Vector2((f.landmarks[17].x - f.landmarks[5].x) * 640, (f.landmarks[17].y - f.landmarks[5].y) * 480).magnitude;
                float px = tremorMm * Mathf.Sqrt(2) * breadth / 80f * Mathf.Sin(2 * Mathf.PI * tremorHz * (float)f.captureSeconds);
                for (int i = 0; i < 21; i++) f.landmarks[i] += new Vector3(px / 640, 0, 0);
            }
            return f;
        }


        IEnumerator Shot(string name)
        {
            Directory.CreateDirectory(Path.Combine("Logs", "activity-shots"));
            yield return new WaitForEndOfFrame();
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.GetFullPath(Path.Combine("Logs", "activity-shots", name.ToLowerInvariant() + ".png")), shot.EncodeToPNG());
            Destroy(shot);
        }
    }
}
