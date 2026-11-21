using System;
using System.Globalization;
using System.IO;
using System.Text;
using GripRehab.Product;
using UnityEngine;

namespace GripRehab
{
    public sealed partial class ResearchPortalHost
    {
        void DoctorList(string filter = "", bool descending = false, int page = 0) => Guard(() => {
            repository.RequireDoctor();
            Show("Research workspace", "D1 · local patients. Account roles control access; notes are not automated diagnoses.", Welcome);
            var search = view.Field("search", "Search patient name or username", limit:60); search.text = filter;
            view.Button("SEARCH", () => DoctorList(search.text, descending));
            view.Button(descending ? "SORT: Z TO A" : "SORT: A TO Z", () => DoctorList(search.text, !descending));
            var patients = Array.FindAll(repository.Patients(), p => p.displayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || p.username.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            Array.Sort(patients, (a, b) => (descending ? -1 : 1) * string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
            if (patients.Length == 0) view.Text("No matching patients. Create a patient account from the main menu.");
            for (int i = page * 20; i < Math.Min(patients.Length, (page + 1) * 20); i++)
            { var patient = patients[i]; view.Button(patient.displayName + " · " + patient.username, () => PatientDetail(patient.id)); }
            if (page > 0) view.Button("PREVIOUS PAGE", () => DoctorList(filter, descending, page - 1));
            if ((page + 1) * 20 < patients.Length) view.Button("NEXT PAGE", () => DoctorList(filter, descending, page + 1));
            // D5: this doctor's own sign-in record, and signing out from the workspace.
            view.Button("MY SIGN-IN LOG", MyAccess);
            view.Button("SIGN OUT", SignOut);
            view.FocusFirst();
        });
        void MyAccess() => Guard(() => {
            var entries = repository.MyAccess();
            Show("My sign-in log", "D5 · audit trail of this doctor account on this computer, newest first.", () => DoctorList());
            if (entries.Length == 0) view.Text("No sign-in entries yet.");
            for (int i = entries.Length - 1; i >= Math.Max(0, entries.Length - 30); i--) view.Text(entries[i].utc + " · " + entries[i].action, 40);
        });
        void PatientDetail(string id) => Guard(() => {
            repository.RequireDoctor(); var patient = repository.Account(id);
            repository.RecordAudit("viewed patient record", id);
            Show(patient.displayName, "D2 · profile and source-attributed research records", () => DoctorList());
            var profile = shell.Store?.Catalog.profiles.Find(p => p.id == patient.profileId);
            view.Text(profile == null ? "No hand profile linked yet." : "Hand: " + profile.affectedHand + " · Dominant: " + profile.dominantHand +
                "\nGoal: " + profile.goal + "\nSelf-reported concern: " + profile.concern, 130);
            if (profile != null && profile.results != null) view.Text("Preserved legacy exercise results: " + profile.results.Count + ". Their source was not recorded by the earlier format.", 72);
            view.Button("PROGRESS BY MODULE", () => Progress(id), true);
            view.Button("SESSION HISTORY & STATISTICS", () => History(id));
            view.Button("NOTES & RECOMMENDATIONS", () => Notes(id));
            view.Button("ASSIGN AN EXISTING LOCAL PROFILE", () => AssignProfile(id));
            view.Button("EXPORT CSV / PDF", () => Exports(id));
            view.Button("AUDIT TRAIL", () => AuditTrail(id));
        });
        // Stage 6.1 / D3: summary cards, baseline comparison, per-module statistics and trends.
        void Progress(string id) => Guard(() => {
            var sessions = repository.Sessions(id);
            bool doctor = repository.Current.role == LocalRole.Doctor;
            Show(doctor ? "Progress by module" : "My progress", (doctor ? "D3 · " : "") + "Game measures from completed attempts. " + ProgressSummary.Missing + " means not measured yet. Not a clinical assessment.",
                () => { if (doctor) PatientDetail(id); else Dashboard(); });
            var cards = ProgressSummary.Cards(sessions);
            view.Text(string.Join("\n", cards.ConvertAll(c => c.label + ":  " + c.value + (c.missing ? "" : "   (" + c.source + ")")).ToArray()), 40 + 28 * cards.Count);
            view.Text("Compared with the baseline:  " + ProgressSummary.BaselineComparison(sessions, repository.LatestBaseline(id)), 60);
            foreach (var m in ProgressSummary.ByModule(sessions))
            {
                view.Text("Module " + m.module + " · " + ProgressSummary.ModuleName(m.module) + "\nAttempts " + m.attempts + " · completed " + m.completed +
                    " · incomplete " + m.incomplete + " · stopped for research safety " + m.safetyStops + " · with pauses " + m.sessionsWithPauses +
                    "\nLatest score " + (m.latestScore.HasValue ? m.latestScore + "/100" : ProgressSummary.Missing) + " · best " + (m.bestScore.HasValue ? m.bestScore + "/100" : ProgressSummary.Missing) +
                    " · level " + (m.latestLevel.HasValue ? m.latestLevel.ToString() : ProgressSummary.Missing), 104);
                if (m.scoreTrend.Length > 1) view.Chart(m.scoreTrend, "Module " + m.module + " score / 100 · completed attempts, oldest to newest");
            }
            if (doctor)
            {
                var events = ProgressSummary.SafetyEvents(sessions);
                view.Text("Research pause and stop events (research indicator, unvalidated):" + (events.Count == 0 ? "\nnone recorded" : "\n" + string.Join("\n", events.ToArray())), 50 + 26 * Math.Max(1, events.Count));
            }
            view.Button("SESSION HISTORY", () => History(id));
        });
        void AuditTrail(string id) => Guard(() => {
            var entries = repository.AuditTrail(id);
            Show("Audit trail", "D5 · who viewed, annotated or exported this record, newest first. Record content is not copied here.", () => PatientDetail(id));
            if (entries.Length == 0) view.Text("No entries yet.");
            for (int i = entries.Length - 1; i >= Math.Max(0, entries.Length - 40); i--)
                view.Text(entries[i].utc + " · " + AccountName(entries[i].actorId) + " · " + entries[i].action, 40);
        });
        string AccountName(string id)
        {
            try { return repository.Account(id)?.displayName ?? id; }
            catch (UnauthorizedAccessException) { return id; }
        }
        void AssignProfile(string id) => Guard(() => {
            repository.RequireDoctor();
            Show("Link preserved profile", "Select the correct local hand profile. A profile can belong to only one patient account.", () => PatientDetail(id));
            if (shell.Store == null || shell.Store.Catalog.profiles.Count == 0) view.Text("No earlier profiles are available.");
            else foreach (var profile in shell.Store.Catalog.profiles)
            { string profileId = profile.id; view.Button(profile.name + " · " + profile.affectedHand, () => Guard(() => { repository.AssignImportedProfile(id, profileId); PatientDetail(id); })); }
        });
        void History(string id, int days = 0, int page = 0) => Guard(() => {
            var records = repository.Sessions(id);
            if (days > 0) records = Array.FindAll(records, s => DateTime.TryParse(s.startedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) && at.ToUniversalTime() >= DateTime.UtcNow.AddDays(-days));
            Array.Sort(records, (a, b) => string.Compare(b.startedUtc, a.startedUtc, StringComparison.Ordinal));
            Show("Session history", "D3–D4 · game scores and recorded sources. Stopped and incomplete attempts are marked and have no score.", () => { if (repository.Current.role == LocalRole.Doctor) PatientDetail(id); else Dashboard(); });
            view.Button(days == 0 ? "RANGE: ALL · SHOW LAST 7 DAYS" : days == 7 ? "RANGE: 7 DAYS · SHOW LAST 30" : "RANGE: 30 DAYS · SHOW ALL", () => History(id, days == 0 ? 7 : days == 7 ? 30 : 0));
            if (records.Length == 0) view.Text("No sessions in this period. Your next saved practice will appear here.");
            // Stage 6.1: unfinished attempts have no score, so they stay out of the chart instead of showing as 0.
            var completed = Array.FindAll(records, s => s.state == RecordState.Completed);
            var scores = new float[Math.Min(32, completed.Length)];
            for (int i = 0; i < scores.Length; i++) scores[scores.Length - i - 1] = completed[i].score / 100f;
            if (scores.Length > 1) view.Chart(scores, "Game score / 100 · completed attempts, oldest to newest");
            for (int i = page * 12; i < Math.Min(records.Length, (page + 1) * 12); i++)
            { var record = records[i]; view.Button("Module " + record.module + " · " + ProgressSummary.StateLabel(record) + " · " + ProgressSummary.ScoreText(record) + " · " + record.startedUtc, () => SessionDetail(id, record)); }
            if (page > 0) view.Button("PREVIOUS PAGE", () => History(id, days, page - 1));
            if ((page + 1) * 12 < records.Length) view.Button("NEXT PAGE", () => History(id, days, page + 1));
            view.Button("EXPORT REPORT", () => Exports(id));
        });
        void SessionDetail(string id, SessionRecord record)
        {
            Show("Session result", ProgressSummary.StateLabel(record) + (record.synthetic ? " · SYNTHETIC DATA" : " · source details below"), () => History(id));
            view.Text("Module " + record.module + " · task " + (record.task + 1) + " · level " + record.level + (record.adaptedLevel > 0 && record.adaptedLevel != record.level ? " to " + record.adaptedLevel + " (adapted)" : "") +
                "\nGame score: " + ProgressSummary.ScoreText(record) + "\nDuration: " + record.durationSeconds.ToString("0.0") +
                " s · active grip: " + record.activeSeconds.ToString("0.0") + " s · input available: " + record.validSeconds.ToString("0.0") + " s\n" + (record.hasForce ? "Peak: " + record.peakNewtons.ToString("0.0") + " N" : "Measured force: not available"), 156);
            if (record.outcomes != null && record.outcomes.Count > 0)
            {
                var shown = record.outcomes.FindAll(o => o.key != "stars" && o.key != "level");
                if (shown.Count > 0) view.Text(string.Join("\n", shown.ConvertAll(o => o.label + ": " + o.value.ToString("0.##") + (string.IsNullOrEmpty(o.unit) ? "" : " " + o.unit)).ToArray()), 30 + 26 * shown.Count);
            }
            if (record.metrics.Count > 0) view.Text("Input: " + record.metrics[0].source + " · " + record.metrics[0].unit + "\nSamples saved: " + record.metrics.Count, 80);
            if (record.policyEvents.Count == 0) view.Text("No research guidance events were recorded.");
            foreach (var item in record.policyEvents)
                view.Text(item.seconds.ToString("0.0") + " s · " + item.status + " · " + item.reason + "\nThresholds: " + item.settingsVersion, 80);
            view.Button("EXPORT THIS SESSION'S METRICS", () => Guard(() => {
                var path = ExportPath("metrics", ".csv"); File.WriteAllText(path, ResearchExport.MetricsCsv(record), new UTF8Encoding(true)); Audited("exported session metrics CSV", id); Exported(path, () => SessionDetail(id, record));
            }));
            view.Button("EXPORT THIS SESSION AS PDF", () => Guard(() => {
                var path = ExportPath("session", ".pdf"); File.WriteAllBytes(path, ResearchExport.SessionPdf(id, record)); Audited("exported session PDF", id); Exported(path, () => SessionDetail(id, record));
            }));
        }
        void Audited(string action, string patientId) { if (repository.Current != null && repository.Current.role == LocalRole.Doctor) repository.RecordAudit(action, patientId); }
        void Notes(string id) => Notes(id, "");
        void Notes(string id, string draft) => Guard(() => {
            repository.RequireDoctor();
            Show("Notes & recommendations", "D4 · clinician-authored: every saved entry keeps its author and time. Not an automated diagnosis.", () => PatientDetail(id));
            var notes = repository.Notes(id);
            if (notes.Length == 0) view.Text("No notes have been saved.");
            for (int i = Math.Max(0, notes.Length - 20); i < notes.Length; i++)
                view.Text(notes[i].createdUtc + " · written by " + AccountName(notes[i].doctorId) + "\n" + notes[i].text, 150);
            view.Text("Start from a template, then edit it before saving:", 40);
            foreach (var template in NoteTemplates.All) { string text = template; view.Button("TEMPLATE: " + text, () => Notes(id, text)); }
            var field = view.Field("note", "New research note (up to 2000 characters)", limit:2000, multiline:true); field.text = draft;
            view.Button("SAVE NOTE", () => Guard(() => { repository.AddNote(id, field.text); Notes(id); }), true);
        });
        void Exports(string id)
        {
            Show("Export research records", "Exports are readable files outside protected storage. Create them only when you intend to share or archive these records.", () => History(id));
            view.Button("EXPORT SESSION CSV", () => Guard(() => { var path = ExportPath("sessions", ".csv"); File.WriteAllText(path, ResearchExport.SessionsCsv(repository.Sessions(id)), new UTF8Encoding(true)); Audited("exported sessions CSV", id); Exported(path, () => Exports(id)); }), true);
            view.Button("EXPORT ENGLISH PDF REPORT", () => Guard(() => { var path = ExportPath("report", ".pdf"); File.WriteAllBytes(path, ResearchExport.Pdf(id, repository.Sessions(id))); Audited("exported PDF report", id); Exported(path, () => Exports(id)); }));
        }
        static string ExportPath(string label, string extension)
        {
            var folder = Path.Combine(Application.persistentDataPath, "GripRehab", "Exports"); Directory.CreateDirectory(folder);
            return Path.Combine(folder, label + "-" + Guid.NewGuid().ToString("N") + extension);
        }
        void Exported(string path, Action back)
        { Show("Export saved", "The file was created by your explicit export action.", back); view.Text(path, 110); view.Button("BACK", back, true); }
        static float NextStep(float volume) { float[] steps = { 0, .3f, .5f, .7f, 1 }; foreach (var step in steps) if (step > volume + .01f) return step; return 0; }
        // Stage 7.3: shows exactly what "copy diagnostics" puts on the clipboard before it does.
        void Diagnostics()
        {
            Show("Diagnostics for support", "Device facts, health counters and recent log lines. No names, records, passwords or images are included.", Settings);
            var lines = RehabDiagnostics.Summary().Split('\n');
            view.Text(string.Join("\n", lines, 0, Math.Min(lines.Length, 14)), 300);
            view.Status("Nothing has been copied yet.");
            view.Button("COPY DIAGNOSTICS", () => view.SetStatus(RehabDiagnostics.CopyToClipboard() ?
                "Copied to the clipboard. Paste it into a support message if you choose to share it." : "Copying failed. The log file folder is shown below."), true);
            view.Text("Log folder: " + Product.DiagnosticsLog.Scrub(RehabDiagnostics.LogFolder), 60);
        }
        void Settings() => SettingsAt(1);
        void SettingsAt(float scroll)
        {
            Show("Make it comfortable", "English interface · appearance preferences are separate from patient records.", Welcome);
            view.Button((theme.SelectedTheme == 0 ? "[Selected]  " : "") + "DARK", () => { theme.UseDark(); accessibility?.Save(); SettingsAt(view.ScrollPosition); });
            view.Button((theme.SelectedTheme == 1 ? "[Selected]  " : "") + "LIGHT", () => { theme.UseLight(); accessibility?.Save(); SettingsAt(view.ScrollPosition); });
            view.Button((theme.SelectedTheme == 2 ? "[Selected]  " : "") + "HIGH CONTRAST", () => { theme.UseContrast(); accessibility?.Save(); SettingsAt(view.ScrollPosition); });
            if (accessibility != null)
            {
                view.Button(theme.reduceMotion ? "REDUCE MOTION: ON" : "REDUCE MOTION: OFF", () => { accessibility.ToggleMotion(); SettingsAt(view.ScrollPosition); });
                view.Button(theme.reduceTransparency ? "SOLID PANELS: ON" : "SOLID PANELS: OFF", () => { accessibility.ToggleTransparency(); SettingsAt(view.ScrollPosition); });
                view.Button("TEXT SIZE: " + Mathf.RoundToInt(theme.fontScale * 100) + "%", () => { accessibility.CycleText(); SettingsAt(view.ScrollPosition); });
                view.Text("Hand pointer dwell: " + accessibility.dwellSeconds.ToString("0.0") + " seconds", 40);
                view.Button("SHORTER DWELL", () => { accessibility.ShorterDwell(); SettingsAt(view.ScrollPosition); });
                view.Button("LONGER DWELL", () => { accessibility.LongerDwell(); SettingsAt(view.ScrollPosition); });
                view.Button("UI SOUND: " + Mathf.RoundToInt(accessibility.volume * 100) + "% · TOGGLE MUTE", () => { accessibility.ToggleMute(); SettingsAt(view.ScrollPosition); });
            }
            // Stage 4: game feedback sounds, room ambience and the virtual hand's appearance (per computer).
            view.Button("GAME SOUNDS: " + (RehabAudio.Muted ? "OFF" : "ON"), () => { RehabAudio.Muted = !RehabAudio.Muted; SettingsAt(view.ScrollPosition); });
            // Stage 12: one volume per mixer group (master, effects, ambience, voice).
            view.Button("MASTER VOLUME: " + Mathf.RoundToInt(RehabAudio.MasterVolume * 100) + "%", () => { RehabAudio.MasterVolume = NextStep(RehabAudio.MasterVolume); SettingsAt(view.ScrollPosition); });
            view.Button("EFFECTS VOLUME: " + Mathf.RoundToInt(RehabAudio.EffectsVolume * 100) + "%", () => { RehabAudio.EffectsVolume = NextStep(RehabAudio.EffectsVolume); SettingsAt(view.ScrollPosition); });
            view.Button("AMBIENCE VOLUME: " + Mathf.RoundToInt(RehabAudio.AmbienceVolume * 100) + "%", () => { RehabAudio.AmbienceVolume = NextStep(RehabAudio.AmbienceVolume); SettingsAt(view.ScrollPosition); });
            view.Button("VOICE VOLUME: " + Mathf.RoundToInt(RehabAudio.VoiceVolume * 100) + "%", () => { RehabAudio.VoiceVolume = NextStep(RehabAudio.VoiceVolume); SettingsAt(view.ScrollPosition); });
            // Camera hand: turn the view at the camera edges and walk by pointing (mouse and keyboard stay optional).
            view.Button("HAND LOOK & WALK (CAMERA): " + (HandLocomotion.Enabled ? "ON" : "OFF"), () => { HandLocomotion.Enabled = !HandLocomotion.Enabled; SettingsAt(view.ScrollPosition); });
            // Camera hand control (2026-10-29): reach, steadiness, aim assist and recalibration, no code changes needed.
            view.Button("HAND REACH (CAMERA): " + HandControlProfile.SensitivityNames[HandLocomotion.Sensitivity], () => { HandLocomotion.Sensitivity = (HandLocomotion.Sensitivity + 1) % 3; SettingsAt(view.ScrollPosition); });
            view.Button("HAND STEADINESS (CAMERA): " + HandControlProfile.SteadinessNames[HandLocomotion.Steadiness], () => { HandLocomotion.Steadiness = (HandLocomotion.Steadiness + 1) % 3; SettingsAt(view.ScrollPosition); });
            view.Button("AIM ASSIST FOR GRABBING: " + (HandLocomotion.AimAssist ? "ON (ASSISTED)" : "OFF (REACH ONLY)"), () => { HandLocomotion.AimAssist = !HandLocomotion.AimAssist; SettingsAt(view.ScrollPosition); });
            // 2026-11-20: the Android phone's camera over USB (higher fps, wider view) or the laptop webcam; restarts the camera.
            view.Button("CAMERA: " + (PlayerPrefs.GetInt(VisionCameraLauncher.PhonePrefKey, 0) == 1 ? "PHONE (USB)" : "LAPTOP WEBCAM"), () => {
                PlayerPrefs.SetInt(VisionCameraLauncher.PhonePrefKey, PlayerPrefs.GetInt(VisionCameraLauncher.PhonePrefKey, 0) == 1 ? 0 : 1);
                var launcher = UnityEngine.Object.FindAnyObjectByType<VisionCameraLauncher>();
                if (launcher != null && launcher.IsRunning) { launcher.StopWorker(); launcher.EnsureStarted(); }
                SettingsAt(view.ScrollPosition); });
            view.Button("RECALIBRATE CAMERA HAND", () => { var e = UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>(); if (e != null) { e.Release(); e.ActiveHand.RecenterTracking(); } SettingsAt(view.ScrollPosition); });
            // Stage 13: voice commands (offline keyword recognition), off by default.
            view.Button("VOICE COMMANDS: " + RehabVoiceInput.ModeName(RehabVoiceInput.Mode) + (RehabVoiceInput.Mode != VoiceMode.Off && RehabVoiceInput.Instance != null && RehabVoiceInput.Instance.Unavailable != null ? " (UNAVAILABLE)" : ""), () => { RehabVoiceInput.Mode = (VoiceMode)(((int)RehabVoiceInput.Mode + 1) % 3); SettingsAt(view.ScrollPosition); });
            view.Button("VIRTUAL HAND: " + RehabPresentation.HandToneNames[RehabPresentation.HandTone], () => { RehabPresentation.HandTone = (RehabPresentation.HandTone + 1) % RehabPresentation.HandToneNames.Length; SettingsAt(view.ScrollPosition); });
            view.Button("DIAGNOSTICS FOR SUPPORT", Diagnostics);
            view.Button("BACK TO MENU", Welcome, true);
            view.ScrollPosition = scroll;
        }
    }
}
