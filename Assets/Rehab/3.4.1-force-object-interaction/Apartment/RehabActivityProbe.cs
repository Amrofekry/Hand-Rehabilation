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
        [Serializable] public sealed class Report { public bool passed; public string utc, scope, detail = ""; public int errors; public Run[] runs; }

        ApartmentExperience app; ActivityRunner runner; int errors;
        readonly List<Run> runs = new List<Run>();

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
            report.runs = runs.ToArray(); report.errors = errors;
            report.passed = failure == null && errors == 0 && runs.Count == 12 && runs.All(r => r.completed);
            report.detail = failure != null ? failure.ToString() : errors > 0 ? errors + " error(s) logged" : runs.Any(r => !r.completed) ? "Not completed: " + string.Join(", ", runs.Where(r => !r.completed).Select(r => r.activity + " L" + r.level)) : "";
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
            // The options panel (TAB) with the new activity row, for the by-eye check.
            app.player.SetPaused(true); yield return new WaitForSecondsRealtime(1.5f); yield return Shot("options-panel-activities"); app.player.SetPaused(false);
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
