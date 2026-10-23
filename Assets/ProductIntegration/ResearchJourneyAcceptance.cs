using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GripRehab.Product;
using UnityEngine;
using UnityEngine.UI;

namespace GripRehab
{
    // Native, opt-in journey acceptance. Never opens the real profile directory.
    public sealed class ResearchJourneyAcceptance : MonoBehaviour
    {
        public static bool Finished, Passed;
        readonly List<string> checks = new List<string>();
        RehabAppShell shell;
        ResearchPortalHost portal;
        int runtimeErrors;
        const string Secret = "Fabricated-password-2026";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Finished = Passed = false;
            if (RehabAppShell.JourneyAcceptance)
                new GameObject("Isolated journey acceptance").AddComponent<ResearchJourneyAcceptance>();
        }
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string text, string stack, LogType kind)
        { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) runtimeErrors++; }
        IEnumerator Start()
        {
            Application.runInBackground = true;
            var routine = Run();
            while (true)
            {
                object next = null; bool more;
                try { more = routine.MoveNext(); if (more) next = routine.Current; }
                catch (Exception error) { End(false, error.ToString()); yield break; }
                if (!more) break;
                yield return next;
            }
            End(runtimeErrors == 0, runtimeErrors == 0 ? "" : "Native runtime errors: " + runtimeErrors);
        }
        void Check(bool condition, string description)
        { if (!condition) throw new Exception(description); checks.Add(description); }
        void Click(string name)
        {
            var button = portal.View.GetComponentsInChildren<Button>(true)
                .Single(b => (b.name == name || b.name == "[Selected]  " + name) && b.gameObject.activeInHierarchy);
            Check(button.IsInteractable(), "Enabled action: " + name); button.onClick.Invoke();
        }
        void Field(string name, string value)
        { portal.View.GetComponentsInChildren<InputField>().Single(f => f.name == name).text = value; }
        ProductRepository Reload() => new ProductRepository(new WindowsProductStore(portal.StorageDirectory), new SystemProductClock());
        void Register(string username, bool doctor)
        {
            Click(doctor ? "SET UP LOCAL DOCTOR ACCOUNT" : "CREATE PATIENT ACCOUNT");
            Field("username", username); Field("name", "Fabricated " + username);
            Field("password", Secret); Field("confirm", Secret); Click("CREATE ACCOUNT");
        }
        void Login(string username)
        { Click("SIGN IN"); Field("username", username); Field("password", Secret); Click("SIGN IN"); }
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);
            shell = FindAnyObjectByType<RehabAppShell>(); portal = shell.Research;
            Check(portal != null && portal.ScreenTitle == "GRIP / REHAB", "Ordinary journey entry is initialized");
            Check(portal.StorageDirectory.Contains("GripRehab-Journey-") && !portal.StorageDirectory.StartsWith(Application.persistentDataPath, StringComparison.OrdinalIgnoreCase), "Isolated fabricated storage only");
            Check(FindObjectsByType<ResearchPortalHost>(FindObjectsSortMode.None).Length == 1, "One account host");
            Check(FindObjectsByType<GripRehab.UI.ResearchPortalView>(FindObjectsSortMode.None).Length == 1, "One account view");
            var legacy = shell.Store.Create("Fabricated preserved profile", 30, "Prefer not to say", "Right", "Right", "", "", -1);
            shell.Store.SaveCheckpoint(true, 0, 0, false, 1);
            Click("GUEST PRACTICE");
            Click("Keyboard demo · synthetic force"); Click("CHOOSE A MODULE");
            Check(shell.CurrentPage == "Modules", "Guest reaches saved module selection");
            shell.cognitiveLockedButton.onClick.Invoke();
            Check(shell.CurrentPage == "Activity" && portal.LiveSession, "Guest module starts without account ownership");
            shell.ReturnHome(); Check(shell.Store.Catalog.profiles.Single().results.Count == 0, "Guest does not write the preserved profile");
            Register("patient.one", false);
            Check(shell.CurrentPage == "Profile", "Registration opens the existing profile form");
            shell.nameField.text = "Fabricated patient"; shell.ageField.text = "31";
            shell.affectedPicker.value = shell.affectedPicker.options.FindIndex(o => o.text.StartsWith("Left", StringComparison.OrdinalIgnoreCase));
            shell.saveProfileButton.onClick.Invoke();
            Check(portal.ScreenTitle.StartsWith("Hello, "), "Saved profile is linked to patient dashboard");
            var repo = Reload(); Check(repo.SignIn("patient.one", Secret), "Protected account survives repository restart");
            string profileId = repo.Current.profileId;
            Check(profileId != legacy.id && shell.Store.Catalog.profiles.Count == 2, "New account preserves unrelated legacy profile");
            Click("PREPARE TO TRAIN"); Click("Keyboard demo · synthetic force"); Click("COLLECT BASELINE");
            Check(portal.ScreenTitle == "Baseline consent", "Baseline requires explicit consent");
            Click("NOT NOW"); Check(repo.LatestBaseline(repo.Current.id) == null, "Declining does not write a baseline");
            Click("COLLECT BASELINE"); Click("I AGREE · START BASELINE");
            float deadline = Time.realtimeSinceStartup + 90;
            while (portal.ScreenTitle != "Baseline summary" && Time.realtimeSinceStartup < deadline) yield return null;
            Check(portal.ScreenTitle == "Baseline summary", "Staged research baseline reaches its saved summary");
            repo = Reload(); repo.SignIn("patient.one", Secret);
            Check(repo.LatestBaseline(repo.Current.id)?.synthetic == true, "Baseline source and consent persist");
            Click("PREPARE TO TRAIN"); Click("CHOOSE A MODULE"); shell.openModuleButton.onClick.Invoke();
            Check(shell.CurrentPage == "Game" && portal.LiveSession, "Module 1 starts a protected session");
            Check(shell.experience.LeftSelected, "Patient's selected hand is used");
            shell.choices.SelectStation(true); shell.ReturnHome();
            Check(portal.ScreenTitle == "GRIP / REHAB", "Returning home safely ends the active attempt");
            Click("CONTINUE PRACTICE");
            Check(shell.CurrentPage == "Game" && shell.choices.Kitchen && !shell.experience.CameraMode && !shell.experience.Hardware, "Continue restores room and explicit keyboard source");
            shell.ReturnHome(); Click("OPEN DASHBOARD"); Click("PREPARE TO TRAIN"); Click("CHOOSE A MODULE");
            shell.cognitiveLockedButton.onClick.Invoke();
            var suite = (RehabExerciseSuite)typeof(RehabAppShell).GetField("exercises", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shell);
            var memory = (SequenceMemoryTask)typeof(RehabExerciseSuite).GetField("memory", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(suite);
            var tokens = memory.Sequence.ToArray();
            yield return new WaitForSecondsRealtime(3.6f);
            foreach (var token in tokens) suite.Submit(token);
            Check(portal.ScreenTitle == "Practice complete" && !portal.LiveSession, "Completed task opens result page");
            repo = Reload(); repo.SignIn("patient.one", Secret);
            Check(repo.Sessions(repo.Current.id).Any(s => s.module == 2 && s.state == RecordState.Completed && s.synthetic), "Completion is saved with its source: " + portal.LastSaveProblem + " / " + string.Join(",", repo.Sessions(repo.Current.id).Select(s=>s.module+":"+s.state+":"+s.synthetic)));
            Check(repo.Sessions(repo.Current.id).Count(s => s.module == 1 && s.state == RecordState.Interrupted) == 2, "Interrupted attempts remain distinct");
            Click("BACK TO MENU"); Click("OPEN DASHBOARD"); Click("PREPARE TO TRAIN"); Click("CHOOSE A MODULE");
            shell.visuomotorLockedButton.onClick.Invoke();
            Check(shell.CurrentPage == "Activity" && portal.LiveSession, "Module 3 starts through the same owned journey");
            foreach (var point in TraceTask.Path) { yield return new WaitForSecondsRealtime(.05f); suite.TracePoint(point); }
            Check(portal.ScreenTitle == "Practice complete", "Module 3 result returns to the common result page");
            Click("BACK TO MENU"); Click("SIGN OUT");
            Check(!portal.HasAccount, "Sign out clears the active account");
            Register("doctor.one", true);
            Check(portal.ScreenTitle == "Research workspace", "Doctor registration opens the doctor route");
            shell.OpenResearchProfile(); Check(shell.CurrentPage == "Portal", "Doctor cannot create a patient hand profile through the hidden legacy form");
            Click("OPEN DASHBOARD"); Check(!portal.AuthorizeTraining(), "Doctor cannot start an owned patient session");
            Check(portal.ScreenTitle == "Research workspace", "Denied training stays on the doctor route");
            Click("Fabricated patient.one · patient.one"); Click("ASSIGN AN EXISTING LOCAL PROFILE");
            Click("Fabricated preserved profile · Right");
            portal.Welcome(); Click("SIGN OUT"); Login("patient.one");
            Check(portal.ScreenTitle.StartsWith("Hello, "), "Patient can sign in again");
            Click("CONTINUE PRACTICE");
            Check(shell.CurrentPage == "Game" && shell.choices.Kitchen && !shell.experience.LeftSelected && !shell.experience.CameraMode, "Doctor-linked legacy Continue restores the preserved checkpoint");
            shell.ReturnHome(); Click("OPEN DASHBOARD");
            for (int i = 0; i < 5; i++) { portal.Welcome(); Click("OPEN DASHBOARD"); }
            Check(FindObjectsByType<ResearchPortalHost>(FindObjectsSortMode.None).Length == 1 && FindObjectsByType<GripRehab.UI.ResearchPortalView>(FindObjectsSortMode.None).Length == 1, "Repeated navigation never duplicates hosts or views");
            repo = Reload(); repo.SignIn("patient.one", Secret);
            Check(shell.Store.Catalog.profiles.Any(p => p.id == legacy.id && p.name == legacy.name), "Preserved legacy profile remains unchanged");
            var orphan = repo.Begin(3, 0, true); repo = Reload(); repo.SignIn("patient.one", Secret);
            Check(repo.RecoverInterruptedSessions() == 1 && repo.Sessions(repo.Current.id).Single(s => s.id == orphan.id).state == RecordState.Interrupted, "Crash checkpoint recovers after reload");
            Check(runtimeErrors == 0, "No logged native runtime errors");
        }
        void End(bool passed, string detail)
        {
            Passed = passed; Finished = true;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/journey-acceptance.json", JsonUtility.ToJson(new Result { passed = passed, count = checks.Count,
                checks = checks.ToArray(), detail = detail, utc = DateTime.UtcNow.ToString("O"), scope = "Native uGUI journey; fabricated accounts and input; no live camera/hardware" }, true));
#if UNITY_EDITOR
            if (!UnityEditor.SessionState.GetBool("GripRehab.JourneyQA", false)) UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(passed ? 0 : 1);
#endif
        }
        [Serializable] sealed class Result { public bool passed; public int count; public string[] checks; public string detail, utc, scope; }
    }
}
