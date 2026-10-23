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
            view.FocusFirst();
        });
        void PatientDetail(string id) => Guard(() => {
            repository.RequireDoctor(); var patient = repository.Account(id);
            Show(patient.displayName, "D2 · profile and source-attributed research records", () => DoctorList());
            var profile = shell.Store?.Catalog.profiles.Find(p => p.id == patient.profileId);
            view.Text(profile == null ? "No hand profile linked yet." : "Hand: " + profile.affectedHand + " · Dominant: " + profile.dominantHand +
                "\nGoal: " + profile.goal + "\nSelf-reported concern: " + profile.concern, 130);
            if (profile != null && profile.results != null) view.Text("Preserved legacy exercise results: " + profile.results.Count + ". Their source was not recorded by the earlier format.", 72);
            view.Button("SESSION HISTORY & STATISTICS", () => History(id), true);
            view.Button("NOTES & RECOMMENDATIONS", () => Notes(id));
            view.Button("ASSIGN AN EXISTING LOCAL PROFILE", () => AssignProfile(id));
            view.Button("EXPORT CSV / PDF", () => Exports(id));
        });
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
            Show("Session history", "D3–D4 · game scores and recorded sources. An interrupted session is never shown as complete.", () => { if (repository.Current.role == LocalRole.Doctor) PatientDetail(id); else Dashboard(); });
            view.Button(days == 0 ? "RANGE: ALL · SHOW LAST 7 DAYS" : days == 7 ? "RANGE: 7 DAYS · SHOW LAST 30" : "RANGE: 30 DAYS · SHOW ALL", () => History(id, days == 0 ? 7 : days == 7 ? 30 : 0));
            if (records.Length == 0) view.Text("No sessions in this period. Your next saved practice will appear here.");
            var scores = new float[Math.Min(32, records.Length)];
            for (int i = 0; i < scores.Length; i++) scores[scores.Length - i - 1] = records[i].score / 100f;
            if (scores.Length > 0) view.Chart(scores, "Game score / 100 · oldest to newest · incomplete attempts retained");
            for (int i = page * 12; i < Math.Min(records.Length, (page + 1) * 12); i++)
            { var record = records[i]; view.Button("Module " + record.module + " · " + record.state + " · " + record.score + "/100 · " + record.startedUtc, () => SessionDetail(id, record)); }
            if (page > 0) view.Button("PREVIOUS PAGE", () => History(id, days, page - 1));
            if ((page + 1) * 12 < records.Length) view.Button("NEXT PAGE", () => History(id, days, page + 1));
            view.Button("EXPORT REPORT", () => Exports(id));
        });
        void SessionDetail(string id, SessionRecord record)
        {
            Show("Session result", record.state + (record.synthetic ? " · SYNTHETIC DATA" : " · source details below"), () => History(id));
            view.Text("Module " + record.module + " · task " + (record.task + 1) + "\nGame score: " + record.score + "/100\nDuration: " + record.durationSeconds.ToString("0.0") +
                " s · active grip: " + record.activeSeconds.ToString("0.0") + " s · input available: " + record.validSeconds.ToString("0.0") + " s\n" + (record.hasForce ? "Peak: " + record.peakNewtons.ToString("0.0") + " N" : "Measured force: not available"), 156);
            if (record.metrics.Count > 0) view.Text("Input: " + record.metrics[0].source + " · " + record.metrics[0].unit + "\nSamples saved: " + record.metrics.Count, 80);
            if (record.policyEvents.Count == 0) view.Text("No research guidance events were recorded.");
            foreach (var item in record.policyEvents)
                view.Text(item.seconds.ToString("0.0") + " s · " + item.status + " · " + item.reason + "\nThresholds: " + item.settingsVersion, 80);
            view.Button("EXPORT THIS SESSION'S METRICS", () => Guard(() => {
                var path = ExportPath("metrics", ".csv"); File.WriteAllText(path, ResearchExport.MetricsCsv(record), new UTF8Encoding(true)); Exported(path, () => SessionDetail(id, record));
            }));
        }
        void Notes(string id) => Guard(() => {
            repository.RequireDoctor();
            Show("Notes & recommendations", "D5 · authored by the signed-in doctor account; every saved entry retains its author and time.", () => PatientDetail(id));
            var notes = repository.Notes(id);
            if (notes.Length == 0) view.Text("No notes have been saved.");
            for (int i = Math.Max(0, notes.Length - 20); i < notes.Length; i++) view.Text(notes[i].createdUtc + " · " + notes[i].doctorId + "\n" + notes[i].text, 150);
            var field = view.Field("note", "New research note (up to 2000 characters)", limit:2000, multiline:true);
            view.Button("SAVE NOTE", () => Guard(() => { repository.AddNote(id, field.text); Notes(id); }), true);
        });
        void Exports(string id)
        {
            Show("Export research records", "Exports are readable files outside protected storage. Create them only when you intend to share or archive these records.", () => History(id));
            view.Button("EXPORT SESSION CSV", () => Guard(() => { var path = ExportPath("sessions", ".csv"); File.WriteAllText(path, ResearchExport.SessionsCsv(repository.Sessions(id)), new UTF8Encoding(true)); Exported(path, () => Exports(id)); }), true);
            view.Button("EXPORT ENGLISH PDF REPORT", () => Guard(() => { var path = ExportPath("report", ".pdf"); File.WriteAllBytes(path, ResearchExport.Pdf(id, repository.Sessions(id))); Exported(path, () => Exports(id)); }));
        }
        static string ExportPath(string label, string extension)
        {
            var folder = Path.Combine(Application.persistentDataPath, "GripRehab", "Exports"); Directory.CreateDirectory(folder);
            return Path.Combine(folder, label + "-" + Guid.NewGuid().ToString("N") + extension);
        }
        void Exported(string path, Action back)
        { Show("Export saved", "The file was created by your explicit export action.", back); view.Text(path, 110); view.Button("BACK", back, true); }
        void Settings()
        {
            Show("Make it comfortable", "English interface · appearance preferences are separate from patient records.", Welcome);
            view.Button("DARK", () => { theme.UseDark(); accessibility?.Save(); Settings(); });
            view.Button("LIGHT", () => { theme.UseLight(); accessibility?.Save(); Settings(); });
            view.Button("HIGH CONTRAST", () => { theme.UseContrast(); accessibility?.Save(); Settings(); });
            if (accessibility != null)
            {
                view.Button(theme.reduceMotion ? "REDUCE MOTION: ON" : "REDUCE MOTION: OFF", () => { accessibility.ToggleMotion(); Settings(); });
                view.Button(theme.reduceTransparency ? "SOLID PANELS: ON" : "SOLID PANELS: OFF", () => { accessibility.ToggleTransparency(); Settings(); });
                view.Button("TEXT SIZE: " + Mathf.RoundToInt(theme.fontScale * 100) + "%", () => { accessibility.CycleText(); Settings(); });
                view.Text("Hand pointer dwell: " + accessibility.dwellSeconds.ToString("0.0") + " seconds", 40);
                view.Button("SHORTER DWELL", () => { accessibility.ShorterDwell(); Settings(); });
                view.Button("LONGER DWELL", () => { accessibility.LongerDwell(); Settings(); });
                view.Button("UI SOUND: " + Mathf.RoundToInt(accessibility.volume * 100) + "% · TOGGLE MUTE", () => { accessibility.ToggleMute(); Settings(); });
            }
            view.Button("BACK TO MENU", Welcome, true);
        }
    }
}
