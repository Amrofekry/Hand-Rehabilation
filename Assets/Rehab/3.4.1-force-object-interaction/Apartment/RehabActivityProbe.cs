using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GripRehab
{
    /// <summary>
    /// Stage 10 activity probe (opt-in: -apartment-activity-probe). Plays all six activities at two levels with
    /// SYNTHETIC keyboard-demo input through the real path (look at the item, E-equivalent grab, look at the
    /// target, release; squeeze and chop with test force), and records completion, score, steps, errors and
    /// outcomes. Isolated acceptance storage, no camera, device or person.
    /// </summary>
    public sealed class RehabActivityProbe : MonoBehaviour
    {
        [Serializable] public sealed class Run
        {
            public string activity; public int level, score, stepsDone, stepsTotal, errors, misses, actions, failedGrabs; public bool completed; public float seconds;
            public OutcomeView[] outcomes;
        }
        [Serializable] public sealed class OutcomeView { public string key, label, unit; public float value; }
        [Serializable] public sealed class UiCheck { public string name, value; public bool passed; }
        [Serializable] public sealed class Report { public bool passed; public string utc, scope, detail = "", resolution; public int errors; public Run[] runs; public UiCheck[] uiChecks; }

        ApartmentExperience app; ActivityRunner runner; int errors;
        readonly List<Run> runs = new List<Run>();
        readonly List<UiCheck> uiChecks = new List<UiCheck>();
        static bool UiOnly => Array.IndexOf(Environment.GetCommandLineArgs(), "-activity-ui-only") >= 0;
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
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), scope = "SYNTHETIC keyboard-demo input in the rendered Windows player; no camera, device, person or records" };
            Exception failure = null; var routine = Play();
            while (true)
            {
                object next = null; bool more;
                try { more = routine.MoveNext(); if (more) next = routine.Current; }
                catch (Exception error) { failure = error; break; }
                if (!more) break;
                yield return next;
            }
            report.runs = runs.ToArray(); report.errors = errors; report.uiChecks = uiChecks.ToArray(); report.resolution = Screen.width + "x" + Screen.height;
            bool uiPassed = uiChecks.Count > 0 && uiChecks.All(c => c.passed);
            report.passed = failure == null && errors == 0 && uiPassed && (UiOnly || runs.Count == 12 && runs.All(r => r.completed));
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
            yield return UiChecks(shell);
            if (UiOnly) { shell.ReturnHome(); yield break; }
            foreach (int level in new[] { 2, 8 })
                foreach (ActivityKind kind in Enum.GetValues(typeof(ActivityKind)))
                {
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
                    double wait = Time.realtimeSinceStartupAsDouble + 4;
                    while (runner.LastResult == null && Time.realtimeSinceStartupAsDouble < wait) yield return null;
                    if (level == 2) yield return Shot("activity-" + ((int)kind + 1) + "-" + kind + "-done");
                    var result = runner.LastResult;
                    run.completed = result != null; run.stepsDone = activity.steps.Current; run.stepsTotal = activity.steps.Count;
                    run.errors = activity.errors; run.misses = activity.misses; run.seconds = (float)(Time.realtimeSinceStartupAsDouble - started);
                    if (result != null) { run.score = result.score; run.outcomes = result.outcomes.Select(o => new OutcomeView { key = o.key, label = o.label, unit = o.unit, value = o.value }).ToArray(); }
                    yield return new WaitForSecondsRealtime(3f); // the runner reports and tidies up after completion
                    runner.End(false); app.TestForce = 0;
                    if (shell.Research != null && !shell.Research.LiveSession) { shell.openModuleButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(1); app.SetHardware(false); }
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
            }
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
