using System;
using System.IO;
using GripRehab.Product;
using GripRehab.UI;
using UnityEngine;

namespace GripRehab
{
    /// <summary>Composition boundary: binds the existing game to protected research records.</summary>
    [DisallowMultipleComponent]
    public sealed partial class ResearchPortalHost : MonoBehaviour
    {
        RehabAppShell shell;
        ResearchPortalView view;
        GripUiThemeController theme;
        GripUiAccessibility accessibility;
        ProductRepository repository;
        SessionRecord session;
        StagedCapabilityTrial baseline;
        FatigueSafetyPolicy fatigueSafety;
        ResearchAiRuntime fatigueAi;
        ResearchAuxiliaryReceiver auxiliary;
        bool faceConsent, physiologyConsent, proxyDemo, lastModelValid, sourceFault;
        float lastModelProbability;
        TemporalForceWindow aiWindow = new TemporalForceWindow();
        readonly float[] aiFeatures = new float[400];
        double nextConsentWrite, nextAiWindow;
        readonly FeatureWindow features = new FeatureWindow();
        public ResearchGuidanceSettings guidanceSettings;
        GuidanceLimits limits = new GuidanceLimits();
        CapabilityProfile capability;
        double nextSample, nextSave, started;
        long demoSequence;
        Vector3 previousWrist, previousVelocity;
        bool hadWrist, collecting, guidance, liveSession, savingFailed, guest;
        TrialSample previousInput;
        bool hadInput;
        public int InputMode { get; private set; } = 0;
        public bool HasAccount => repository?.Current != null;
        public string ProfileId => repository?.Current?.profileId;
        public bool CanCreateProfile => HasAccount && repository.Current.role == LocalRole.Patient;
        public bool LiveSession => liveSession;
        public string ScreenTitle { get; private set; }
        public ResearchPortalView View => view;
        public string LastSaveProblem {get;private set;} = "";
        bool initialized;
        string storageProblem;
        string pendingProfileId;
        public string StorageDirectory { get; private set; }
        /// <summary>Stage 6 gate: -dashboard-fixtures runs on an isolated store seeded with fabricated demo records.</summary>
        public static bool DashboardFixtureMode => Array.IndexOf(Environment.GetCommandLineArgs(), "-dashboard-fixtures") >= 0;

        public void Initialize(RehabAppShell owner, string storageDirectory)
        {
            if (initialized) return;
            initialized = true;
            StorageDirectory = storageDirectory;
            shell = owner;
            guidanceSettings = guidanceSettings != null ? guidanceSettings : Resources.Load<ResearchGuidanceSettings>("ResearchGuidance");
            if (guidanceSettings != null) limits = guidanceSettings.Snapshot();
            var canvas = shell.root.GetComponentInParent<Canvas>();
            theme = canvas.GetComponent<GripUiThemeController>() ?? canvas.GetComponentInChildren<GripUiThemeController>(true);
            accessibility = canvas.GetComponent<GripUiAccessibility>() ?? canvas.GetComponentInChildren<GripUiAccessibility>(true);
            if (theme == null) throw new InvalidOperationException("Research portal needs the saved UI theme controller.");
            view = new GameObject("Research portal", typeof(RectTransform)).AddComponent<ResearchPortalView>();
            view.Initialize(shell.root.transform, theme, accessibility);
            try { repository = new ProductRepository(new WindowsProductStore(storageDirectory), new SystemProductClock()); }
            catch (Exception error) { storageProblem = error.Message; }
            if (DashboardFixtureMode && repository != null && repository.NeedsDoctorSetup)
            {
                try { DashboardFixtures.Seed(repository); }
                catch (Exception error) { storageProblem = "Fabricated demo records could not be created: " + error.Message; Debug.LogWarning("[DASHBOARD_FIXTURES] " + error); repository.SignOut(); }
            }
        }
        void Show(string title, string detail, Action back)
        { ScreenTitle = title; shell.ShowResearchPortal(); view.Show(title, detail, back); }
        public void HidePresentation() => view?.Hide();
        void Guard(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is IOException || error is UnauthorizedAccessException || error is System.ComponentModel.Win32Exception)
            {
                Show("We could not complete that step", error.Message, Welcome);
                view.Button("BACK TO MENU", Welcome, true);
            }
        }
        public void Welcome()
        {
            if (!LeavingGame()) return;
            collecting = false;
            guest = false; shell.SetResearchIdentity(null);
            shell.experience.Release();
            Show("GRIP / REHAB", "Your space to practise, at your pace. Research software; game metrics are not a clinical assessment.", null);
            if (DashboardFixtureMode) view.Text("FABRICATED DEMO DATA · isolated test storage · demo accounts doctor.demo, patient.alpha and patient.beta" + (storageProblem != null ? "\n" + storageProblem : ""), storageProblem != null ? 110 : 60);
            if (repository == null) view.Text("Protected account storage is unavailable. Existing records have been kept. Guest practice is available. " + storageProblem, 120);
            else if (HasAccount)
            {
                view.Text("Signed in as " + repository.Current.displayName + " · " + repository.Current.role);
                view.Button("OPEN DASHBOARD", Dashboard, true);
                if (repository.Current.role == LocalRole.Patient)
                    view.Button("CONTINUE PRACTICE", ContinuePractice, enabled: LinkedCheckpoint() != null);
                view.Button("SIGN OUT", SignOut);
            }
            else
            {
                view.Button("SIGN IN", Login, true);
                view.Button("CREATE PATIENT ACCOUNT", () => Register(false));
                if (repository.NeedsDoctorSetup) view.Button("SET UP LOCAL DOCTOR ACCOUNT", () => Register(true));
            }
            view.Button("GUEST PRACTICE", () => Prepare(true));
            view.Button("APPEARANCE & ACCESSIBILITY", Settings);
            view.Button("HOW TO PLAY", Guide);
            view.Button("QUIT", ConfirmQuit);
            view.FocusFirst();
        }
        void SignOut()
        { if (!LeavingGame()) return; collecting = false; pendingProfileId = null; repository.SignOut(); guest = true; shell.SetResearchIdentity(null); Welcome(); }
        RehabProfile LinkedCheckpoint() => HasAccount && repository.Current.role == LocalRole.Patient && shell.Store != null ?
            shell.Store.Catalog.profiles.Find(p => p.id == ProfileId && p.hasSession) : null;
        public void OpenPreparation() => Prepare(!HasAccount);
        public void ContinuePractice() => Guard(() => {
            if (!CanCreateProfile || LinkedCheckpoint() == null) { Dashboard(); return; }
            if (!LeavingGame()) return;
            guest = false; shell.SetResearchIdentity(ProfileId);
            InputMode = Mathf.Clamp(LinkedCheckpoint().inputMode, 0, 2);
            view.Hide(); shell.ContinueSavedPractice();
        });
        void Login()
        {
            Show("Welcome back", "Accounts and records stay on this Windows user account.", Welcome);
            var username = view.Field("username", "Username", limit:40);
            var password = view.Field("password", "Password", true, 128);
            view.Button("SIGN IN", () => Guard(() => {
                var secret = password.text; password.text = "";
                if (!repository.SignIn(username.text, secret)) throw new InvalidOperationException("Sign-in failed. After repeated attempts, wait 30 seconds and try again.");
                repository.RecoverInterruptedSessions();
                guest = false;
                if (repository.Current.role == LocalRole.Patient && string.IsNullOrEmpty(ProfileId)) { view.Hide(); shell.OpenResearchProfile(); }
                else Dashboard();
            }), true);
            view.FocusFirst();
        }
        void Register(bool doctor)
        {
            Show(doctor ? "Local doctor setup" : "Create your account", doctor ?
                "The first doctor account can view local research records. This does not verify professional credentials." :
                "Choose a username and a password of at least 12 characters. Your hand profile comes next.", Welcome);
            var username = view.Field("username", "Username", limit:40);
            var name = view.Field("name", "Display name", limit:60);
            var password = view.Field("password", "Password", true,128);
            var confirm = view.Field("confirm", "Confirm password", true,128);
            view.Button("CREATE ACCOUNT", () => Guard(() => {
                if (password.text != confirm.text) throw new ArgumentException("Passwords do not match.");
                var secret = password.text; password.text = confirm.text = "";
                repository.Register(username.text, name.text, secret, doctor ? LocalRole.Doctor : LocalRole.Patient);
                repository.SignIn(username.text, secret);
                guest = false;
                if (doctor) Dashboard(); else { view.Hide(); shell.OpenResearchProfile(); }
            }), true);
        }
        public void ProfileCreated(string id)
        {
            if (!CanCreateProfile) { Welcome(); return; }
            pendingProfileId = id;
            try { repository.LinkProfile(id); pendingProfileId = null; Dashboard(); }
            catch (Exception error)
            {
                Show("Profile saved; account link needs attention", error.Message + " Your saved profile has been retained.", Welcome);
                view.Button("RETRY PROFILE LINK", () => ProfileCreated(id), true);
                view.Button("BACK TO MENU", Welcome);
            }
        }
        public bool AuthorizeTraining()
        {
            if (savingFailed) { SavingIssue(); return false; }
            if (guest) { shell.SetResearchIdentity(null); return true; }
            if (!HasAccount) { Welcome(); return false; }
            if (repository.Current.role != LocalRole.Patient) { Dashboard(); return false; }
            if (string.IsNullOrEmpty(ProfileId)) { shell.OpenResearchProfile(); return false; }
            try { shell.SetResearchIdentity(ProfileId); }
            catch (InvalidOperationException error)
            {
                Show("Linked profile unavailable", error.Message, Welcome);
                view.Button("CREATE REPLACEMENT PROFILE", shell.OpenResearchProfile, true);
                return false;
            }
            return true;
        }
        public void Dashboard()
        {
            if (!HasAccount) { Welcome(); return; }
            if (!LeavingGame()) return;
            collecting = false;
            guest = false;
            if (pendingProfileId != null) { ProfileCreated(pendingProfileId); return; }
            if (repository.Current.role == LocalRole.Doctor) { DoctorList(); return; }
            if (string.IsNullOrEmpty(ProfileId)) { shell.OpenResearchProfile(); return; }
            Show("Hello, " + repository.Current.displayName, "Choose a comfortable starting point. You can pause or leave at any time.", Welcome);
            view.Button("PREPARE TO TRAIN", () => Prepare(false), true);
            view.Button("CONTINUE PRACTICE", ContinuePractice, enabled: LinkedCheckpoint() != null);
            view.Button("MY PROGRESS", () => Progress(repository.Current.id));
            view.Button("SESSION HISTORY", () => History(repository.Current.id));
            view.Button("BASELINE SUMMARY", BaselineSummary);
            view.Button("APPEARANCE & ACCESSIBILITY", Settings);
            view.Button("HOW TO PLAY", Guide);
            view.FocusFirst();
        }
        void Prepare(bool guest)
        {
            if (!LeavingGame()) return;
            if (!guest && (!HasAccount || repository.Current.role != LocalRole.Patient)) { Dashboard(); return; }
            if (!guest && string.IsNullOrEmpty(ProfileId)) { shell.OpenResearchProfile(); return; }
            this.guest = guest;
            Show("Prepare to train", guest ? "Guest practice · results will not be saved." : "Step 1 of 3 · choose an input source, then take an optional research baseline.", guest ? (Action)Welcome : Dashboard);
            view.Choice("Input source", new[] { "Camera · visual closure estimate", "Keyboard demo · synthetic force", "Phase 2 · calibrated measured force" }, InputMode,
                selected => { InputMode = selected; Prepare(guest); });
            view.Button(guidance ? "RESEARCH GUIDANCE: ON" : "RESEARCH GUIDANCE: OFF", () => { guidance = !guidance; Prepare(guest); });
            view.Text("Guidance uses unvalidated research thresholds. The public EMG model is a development proxy, never a device-force model. Camera closure is not force in Newtons.");
            view.Button("OPTIONAL FACE / PULSE SENSING", SensingConsent);
            view.Button(proxyDemo ? "SYNTHETIC MODEL DEMO: ON" : "SYNTHETIC MODEL DEMO: OFF",()=>{proxyDemo=!proxyDemo;Prepare(guest);});
            if (!guest)
            {
                // Stage 5.1: guidance compares you with a baseline taken with the same input.
                var saved = repository.LatestBaseline(repository.Current.id);
                if (saved == null) view.Text("No baseline yet. Guidance can still pause for safety, but it cannot adapt difficulty until you collect one.", 80);
                else if (!BaselineMatch.ForInputMode(saved, InputMode))
                    view.Text("Your saved baseline was taken with " + saved.source + ". Collect a new baseline for this input so guidance can compare like with like.", 80);
                view.Button("COLLECT BASELINE", BaselineConsent);
            }
            view.Button("CHOOSE A MODULE", () => Guard(() => {
                shell.SetResearchIdentity(guest ? null : ProfileId); view.Hide(); shell.ShowModules();
            }), true);
            view.FocusFirst();
        }
        void ApplyInput()
        { if (InputMode == 0) shell.experience.SetCameraMode(); else shell.experience.SetHardware(InputMode == 2); }
        public void ApplyChosenInput() => ApplyInput();
        void BaselineConsent()
        {
            Show("Baseline consent", "This optional staged research baseline has three grip trials with rest, followed by motion and endurance stages. It records derived metrics locally without camera images.", () => Prepare(false));
            view.Text("You may cancel at any time. Camera closure is not measured force; Keyboard Demo is synthetic. Consent applies to this trial only.", 110);
            view.Button("I AGREE · START BASELINE", BeginBaseline, true);
            view.Button("NOT NOW", () => Prepare(false));
        }
        void BeginBaseline() => Guard(() => {
            if (!AuthorizeTraining()) return;
            var profile = shell.Store?.Active;
            if (profile != null) shell.experience.SelectHand(profile.affectedHand.StartsWith("Left", StringComparison.OrdinalIgnoreCase));
            ApplyInput(); baseline = new StagedCapabilityTrial(repository.Current.id, ProfileId, true); collecting = true;
            Show("Research baseline", "Step 2 of 3 · follow your usual comfortable hand movement. Camera data is a visual estimate. Keyboard data is synthetic. Stop if uncomfortable.", () => { collecting = false; Dashboard(); });
            view.Coach("GET READY"); view.Progress(); view.Status("Waiting for valid input. Keyboard: hold Space to simulate a squeeze.");
            view.Button("CANCEL BASELINE", () => { collecting = false; Dashboard(); });
        });
        void BaselineSummary()
        {
            var b = repository.LatestBaseline(repository.Current.id);
            Show("Baseline summary", "Research reference only. This is not a clinical strength or range-of-motion assessment.", Dashboard);
            view.Text(b == null ? "No baseline collected yet." : b.source + (b.synthetic ? " · SYNTHETIC" : "") + "\nValid samples: " + b.acceptedSamples +
                (b.hasForce ? "\nPeak: " + b.peakNewtons.ToString("0.0") + " N" : "\nMeasured force: unavailable") +
                "\nVisual hand data: " + (b.hasMotion ? "available" : "unavailable") +
                (b.baselineProtocol != null ? (b.hasForce ? "\nGrip reference: " + b.mvcNewtons.ToString("0.0") + " N; endurance: " + b.enduranceSeconds.ToString("0.0") + " s" : "\nForce/endurance: not measured") + "\nVisual ROM span: " + (b.hasRom ? b.romDegrees.ToString("0.0") + " deg (estimate)" : "unavailable") : ""), 200);
            view.Button("PREPARE TO TRAIN", () => Prepare(false), true);
        }
        void ConfirmQuit()
        {
            Show("Finish for now?", "Your saved records stay on this device.", Welcome);
            view.Button("KEEP PRACTISING", Welcome, true); view.Button("QUIT APPLICATION", () => { if (LeavingGame()) shell.QuitResearchApplication(); });
        }
        void OnDestroy()
        {
            RevokeSensing(); fatigueAi?.Dispose();
            collecting = false;
            if (liveSession) EndSession(RecordState.Interrupted, 0, false);
            if (view != null) Destroy(view.gameObject);
        }
        void Guide()
        {
            Show("Take it one step at a time", "Start with Keyboard Demo if your camera or grip device is not ready.", Welcome);
            view.Text("1. Choose an input on the preparation screen.\n2. Select a module.\n3. Pause whenever you need a break. Your result records which input was used.", 120);
            view.Text("Move: WASD + mouse. E: explicit keyboard pickup. Space: demo squeeze. Q: release. G: bring the selected object to a tracked hand. C: recenter camera hand. Escape: pause/back.", 120);
            view.Text("Hand menu pointer: move either tracked hand in any finger pose. Hold the palm cursor over a button for five seconds. Mouse and keyboard remain available.", 100);
            view.Button("BACK TO MENU", Welcome, true);
        }
    }
}
