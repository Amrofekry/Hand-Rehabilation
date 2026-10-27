using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GripRehab
{
    /// <summary>
    /// Stage 13 voice probe (opt-in: -apartment-voice-probe). Checks that the Windows keyword recognizer starts on this computer
    /// (platform only: no words are spoken), then feeds SYNTHETIC transcripts through the real router and actions: menu navigation,
    /// spelling into the profile name with review, password refusal, moving, looking, grabbing, releasing, pausing, an activity
    /// with instructions and a confirmed restart, low-confidence confirmation, the main menu, and feedback that covers no text.
    /// Writes Logs/voice-probe.json. Recognition accuracy with a real voice is a separate human check.
    /// </summary>
    public sealed class RehabVoiceProbe : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, value; public bool passed; }
        [Serializable] public sealed class Report { public bool passed; public string utc, scope, detail = "", platform; public int errors; public Check[] checks; }
        readonly List<Check> checks = new List<Check>(); int errors; string platform = "";
        void Add(string name, bool passed, string value) => checks.Add(new Check { name = name, passed = passed, value = value });

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-apartment-voice-probe") >= 0)
                new GameObject("Voice probe").AddComponent<RehabVoiceProbe>();
        }
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string text, string stack, LogType kind) { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors++; }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), scope = "SYNTHETIC transcripts through the real router and game actions in the rendered Windows player; recognizer start checked on this computer, no speech" };
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
            RehabVoiceInput.Mode = mode; PlayerPrefs.DeleteKey("GripRehab.Voice.Mode");
            report.checks = checks.ToArray(); report.errors = errors; report.platform = platform;
            report.passed = failure == null && errors == 0 && checks.Count > 0 && checks.All(c => c.passed);
            report.detail = failure != null ? failure.ToString() : errors > 0 ? errors + " error(s) logged" : string.Join(", ", checks.Where(c => !c.passed).Select(c => c.name + "=" + c.value));
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/voice-probe.json", JsonUtility.ToJson(report, true));
            Debug.Log("[REHAB_VOICE] " + (report.passed ? "PASS" : "FAIL") + " " + report.detail);
            Application.Quit(report.passed ? 0 : 1);
        }

        RehabVoiceInput voice;
        IEnumerator Say(string phrase, VoiceConfidence confidence = VoiceConfidence.High, float wait = .25f)
        { voice.Inject(phrase, confidence); yield return null; yield return new WaitForSecondsRealtime(wait); }
        VoiceDecision Last => voice.LastDecision;

        IEnumerator Play()
        {
            yield return new WaitForSecondsRealtime(2);
            var shell = FindAnyObjectByType<RehabAppShell>(); var app = shell.experience;
            voice = RehabVoiceInput.Instance; if (voice == null) throw new InvalidOperationException("Voice input missing");
            shell.SetResearchIdentity(null); shell.ShowHome(); yield return new WaitForSecondsRealtime(.5f);

            // 1. Platform: the real recognizer starts on this computer (no speech is judged), then voice goes off so room noise cannot act.
            RehabVoiceInput.Mode = VoiceMode.AlwaysOn; yield return new WaitForSecondsRealtime(1f);
            platform = "microphones=" + Microphone.devices.Length + (Microphone.devices.Length > 0 ? " (" + Microphone.devices[0] + ")" : "") + ", unavailable=" + (voice.Unavailable ?? "none") + ", recognizer running=" + voice.RecognizerRunning;
            Add("platform: Windows keyword recognizer starts (offline)", voice.Unavailable == null && voice.RecognizerRunning, platform);
            RehabVoiceInput.Mode = VoiceMode.Off; yield return null;
            Add("off stops the recognizer", !voice.RecognizerRunning, voice.RecognizerRunning.ToString());

            // 2. Menus: next moves the selection; back on the home page is refused with feedback.
            EventSystem.current.SetSelectedGameObject(null);
            yield return Say("next"); var first = EventSystem.current.currentSelectedGameObject;
            yield return Say("next"); var second = EventSystem.current.currentSelectedGameObject;
            Add("menu: next moves the selection", first != null && second != null && first != second, (first != null ? first.name : "none") + " -> " + (second != null ? second.name : "none"));
            yield return Say("back");
            Add("home: back is refused, with feedback", Last.outcome == VoiceOutcome.Rejected && voice.ToastVisible, Last + " | toast: " + voice.ToastText);
            yield return ToastClear("home");
            yield return Say("make me a sandwich");
            Add("unknown phrase refused", Last.outcome == VoiceOutcome.Rejected, Last.ToString());

            // 3. Spelling into the profile name, with review; passwords refused.
            shell.OpenResearchProfile(); yield return new WaitForSecondsRealtime(.6f);
            var name = shell.nameField; name.text = ""; EventSystem.current.SetSelectedGameObject(name.gameObject); yield return null;
            foreach (var w in new[] { "alpha", "mike", "romeo", "oscar" }) yield return Say(w, wait: .1f);
            Add("spelling stays in review, not in the field", name.text == "" && voice.SpellingText == "Amro", "field '" + name.text + "', buffer '" + voice.SpellingText + "'");
            yield return Say("done");
            Add("done shows the text for review", voice.ToastText.Contains("\"Amro\""), voice.ToastText);
            yield return Shot("voice-spelling-review");
            yield return Say("confirm");
            Add("confirm enters the name", name.text == "Amro", "'" + name.text + "'");
            var type = name.contentType; name.contentType = InputField.ContentType.Password; EventSystem.current.SetSelectedGameObject(null); yield return null;
            EventSystem.current.SetSelectedGameObject(name.gameObject); yield return null;
            yield return Say("bravo");
            Add("password field refuses voice", Last.outcome == VoiceOutcome.Rejected && name.text == "Amro", Last.ToString());
            name.contentType = type; name.text = ""; EventSystem.current.SetSelectedGameObject(null);
            shell.ShowHome(); yield return new WaitForSecondsRealtime(.5f);

            // 4. Apartment: move, stop, look, grab, release, pause and resume.
            shell.openModuleButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(1f); app.SetHardware(false); app.player.SetPaused(false); yield return new WaitForSecondsRealtime(.3f);
            var player = app.player; var start = player.transform.position;
            if (player.IsSeated) player.Stand();
            start = player.transform.position;
            yield return Say("move back", wait: 1f); // away from the station table
            yield return Say("stop", wait: .4f); var stopped = player.transform.position;
            yield return new WaitForSecondsRealtime(.6f);
            float walked = Vector3.Distance(Flat(start), Flat(stopped)), drift = Vector3.Distance(Flat(stopped), Flat(player.transform.position));
            Add("move back walks, stop stops", walked > .3f && drift < .02f, "walked " + walked.ToString("0.00") + " m, after stop " + drift.ToString("0.000") + " m");
            player.Teleport(start, player.transform.rotation); yield return new WaitForSecondsRealtime(.3f);
            var ball = app.choices.SelectedObject;
            yield return Say("look at object", wait: .3f);
            float angle = Vector3.Angle(player.view.transform.forward, ball.transform.position - player.view.transform.position);
            Add("look at object", angle < 8, angle.ToString("0.0") + "°");
            yield return Say("grab", wait: .5f);
            Add("grab picks it up", app.Held == ball, Last + " held=" + (app.Held != null ? app.Held.displayName : "none"));
            yield return Say("grab");
            Add("grab while holding is refused", Last.outcome == VoiceOutcome.Rejected, Last.ToString());
            yield return Say("release", wait: .5f);
            Add("release lets go", app.Held == null, Last.ToString());
            yield return Say("release");
            Add("release with empty hands is refused", Last.outcome == VoiceOutcome.Rejected, Last.ToString());
            yield return Say("grab", VoiceConfidence.Low);
            Add("low confidence asks to confirm", Last.outcome == VoiceOutcome.NeedsConfirmation && app.Held == null, Last.ToString());
            yield return Say("cancel");
            yield return Say("open inventory");
            Add("open inventory pauses (session options)", player.Paused, Last.ToString());
            yield return Say("move left");
            Add("moving while paused is refused", Last.outcome == VoiceOutcome.Rejected, Last.ToString());
            yield return Say("resume");
            Add("resume", !player.Paused, Last.ToString());

            // 5. Activity: start by voice, instructions, restart needs confirm.
            var runner = app.GetComponent<ActivityRunner>();
            yield return Say("start bookshelf", wait: 1f);
            Add("start an activity by voice", runner.Active && runner.Current.kind == ActivityKind.Bookshelf, Last + " / " + runner.Feedback);
            yield return Say("instructions");
            Add("instructions read the current step", voice.ToastText.StartsWith("Step 1"), voice.ToastText);
            yield return ToastClear("activity");
            yield return Shot("voice-activity-instructions");
            yield return Say("start cutting");
            Add("second activity refused while one runs", Last.outcome == VoiceOutcome.Rejected, Last.ToString());
            yield return Say("restart");
            Add("restart asks to confirm", Last.outcome == VoiceOutcome.NeedsConfirmation, Last.ToString());
            yield return Say("confirm", wait: 1f);
            Add("confirm restarts it", Last.outcome == VoiceOutcome.Execute && runner.Active && runner.Current.steps.Current == 0, Last.ToString());
            runner.End(false); yield return new WaitForSecondsRealtime(.5f);

            // 6. Sleep, then main menu with confirm.
            yield return Say("stop listening"); yield return Say("grab");
            Add("asleep ignores commands", Last.outcome == VoiceOutcome.Ignored && app.Held == null, Last.ToString());
            yield return Say("start listening");
            yield return Say("main menu"); bool asked = Last.outcome == VoiceOutcome.NeedsConfirmation;
            yield return Say("confirm", wait: 1f);
            Add("main menu after confirm", asked && shell.BlocksGameplay, Last + " page=" + shell.CurrentPage);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        /// <summary>The feedback panel covers no other visible text.</summary>
        IEnumerator ToastClear(string where)
        {
            yield return null;
            if (!voice.ToastVisible) { Add("feedback covers no text (" + where + ")", false, "feedback not visible"); yield break; }
            var t = new Vector3[4]; voice.ToastRect.GetWorldCorners(t); var toast = Rect.MinMaxRect(t[0].x, t[0].y, t[2].x, t[2].y);
            var hits = new List<string>();
            // In menus the menu covers the gameplay HUD, so only the menu's own text can be hidden by the feedback.
            var shell = FindAnyObjectByType<RehabAppShell>(); var menu = shell.BlocksGameplay && shell.root != null ? shell.root.transform : null;
            foreach (var g in FindObjectsByType<Graphic>(FindObjectsSortMode.None))
            {
                if (!(g is Text) && !(g is TMPro.TMP_Text) || !g.isActiveAndEnabled || g.transform.IsChildOf(voice.transform) || g.canvasRenderer.GetInheritedAlpha() < .5f) continue;
                if (menu != null && !g.transform.IsChildOf(menu)) continue;
                var text = g is Text a ? a.text : ((TMPro.TMP_Text)g).text; if (string.IsNullOrWhiteSpace(text)) continue;
                var canvas = g.canvas; if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                var c = new Vector3[4]; g.rectTransform.GetWorldCorners(c); var r = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
                if (r.Overlaps(toast)) hits.Add(g.name + " '" + text.Substring(0, Math.Min(24, text.Length)).Replace("\n", " ") + "'");
            }
            Add("feedback covers no text (" + where + ")", hits.Count == 0, hits.Count == 0 ? "clear" : string.Join("; ", hits));
        }

        static IEnumerator Shot(string name)
        {
            Directory.CreateDirectory(Path.Combine("Logs", "voice-shots"));
            yield return new WaitForEndOfFrame();
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.GetFullPath(Path.Combine("Logs", "voice-shots", name + ".png")), shot.EncodeToPNG());
            Destroy(shot);
        }
    }
}
