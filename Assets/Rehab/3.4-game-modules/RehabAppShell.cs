using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GripRehab
{
    // WBS 3.4 shell: patient identity and navigation around the existing 3.4.1 task.
    // Additional research exercises are available as playable prototypes.
    // Read gameplay/force state after ApartmentExperience.Update in the same
    // frame, so stale Phase 2 samples clear the HUD without an extra frame.
    [DefaultExecutionOrder(250)]
    public sealed class RehabAppShell : MonoBehaviour
    {
        public ApartmentExperience experience;
        public ApartmentChoices choices;
        public CanvasGroup root, homePanel, profilePanel, modulePanel, patientPanel, doctorPanel;
        public CanvasGroup[] gameplayLayers;
        public Button startButton, continueButton, newProfileButton, chooseProfileButton, quitButton;
        public Button saveProfileButton, cancelProfileButton, openModuleButton, patientButton, doctorButton;
        public Button homeFromModulesButton, homeFromPatientButton, homeFromDoctorButton, gameplayHomeButton;
        public Button cognitiveLockedButton, visuomotorLockedButton;
        public Dropdown profilePicker, genderPicker, affectedPicker, dominantPicker;
        public InputField nameField, ageField, concernField, goalField, painField;
        public Text homeGreeting, formError, moduleGreeting, patientSummary, doctorSummary, telemetry;
        // Saved, presentation-only view of the existing Module 01 state. No second input pipeline.
        public Text hudRoom, hudHand, hudValue, hudUnit, hudSource, hudObject, hudPrompt, hudProgress;
        public Image hudProgressFill;

        public bool BlocksGameplay { get; private set; }
        public RehabProfileStore Store { get; private set; }
        public string CurrentPage { get; private set; } = "Home";
        public static bool TextEntryHasFocus()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null || !selected.activeInHierarchy) return false;
            var field = selected.GetComponent<UnityEngine.UI.InputField>();
            if (field != null && field.isFocused) return true;
            var tmp = selected.GetComponent<TMPro.TMP_InputField>();
            return tmp != null && tmp.isFocused;
        }
        readonly List<Action> unbind = new List<Action>();
        string lastCheckpoint;
        float nextRefresh;
        float nextCheckpointRetry;
        bool telemetryInitialized;
        bool lastForceValid;
        bool testing;
        bool guestMode;
        RehabExerciseSuite exercises;
        ResearchPortalHost research;
        public ResearchPortalHost Research => research;
        public static bool JourneyAcceptance
        {
            get
            {
                bool requested = Array.IndexOf(Environment.GetCommandLineArgs(), "-apartment-journey") >= 0;
#if UNITY_EDITOR
                requested |= UnityEditor.SessionState.GetBool("GripRehab.JourneyQA", false);
#endif
                return requested;
            }
        }
        string researchReturnPage = "Game";

        void Awake()
        {
            testing = Array.Exists(Environment.GetCommandLineArgs(), a => a.StartsWith("-apartment-"));
            testing |= Array.Exists(Environment.GetCommandLineArgs(), a =>
                a == "-vision-live-qa" || a == "-vision-perf-qa");
            testing |= JourneyAcceptance;
#if UNITY_EDITOR
            testing |= UnityEditor.SessionState.GetBool("ApartmentQA.Active", false);
#endif
            var profileDirectory = testing ?
                Path.Combine(Application.temporaryCachePath, "GripRehab-Acceptance") :
                Path.Combine(Application.persistentDataPath, "GripRehab");
            if (JourneyAcceptance) profileDirectory = Path.Combine(Application.temporaryCachePath, "GripRehab-Journey-" + Guid.NewGuid().ToString("N"));
            // Stage 6 gate: fabricated dashboard demo runs in its own throwaway folder, never the player's records.
            if (ResearchPortalHost.DashboardFixtureMode) profileDirectory = Path.Combine(Application.temporaryCachePath, "GripRehab-Dashboards-" + Guid.NewGuid().ToString("N"));
            try { Store = new RehabProfileStore(profileDirectory); }
            catch (Exception error)
            {
                Debug.LogError("[PROFILE] " + error.Message);
            }
            exercises = new RehabExerciseSuite(root.transform, ShowModules,
                                               SaveExerciseStart, SaveExerciseResult);
            // Stage 3: progression per player and camera gestures from the selected (or only) tracked hand.
            exercises.LevelSource = (module, task) => research != null ? research.LevelFor(module, task) : 1;
            exercises.GestureSource = () =>
            {
                var vision = experience != null ? experience.vision : null;
                if (vision == null) return HandGesture.None;
                var frame = experience.LeftSelected
                    ? (vision.LeftValid ? vision.LatestLeft : vision.RightValid ? vision.LatestRight : null)
                    : (vision.RightValid ? vision.LatestRight : vision.LeftValid ? vision.LatestLeft : null);
                return GestureClassifier.Classify(frame);
            };
            if (!testing && experience != null && experience.vision != null &&
                experience.cameraLauncher != null)
            {
                var pointer = GetComponent<RehabDwellPointer>() ?? gameObject.AddComponent<RehabDwellPointer>();
                pointer.shell = this;
                pointer.vision = experience.vision;
                pointer.cameraLauncher = experience.cameraLauncher;
            }
            // The authored panels stay intact. One themed account surface
            // composes the patient journey around those existing panels.
            if (!testing || JourneyAcceptance)
            {
                research = GetComponent<ResearchPortalHost>() ?? gameObject.AddComponent<ResearchPortalHost>();
                research.Initialize(this, profileDirectory);
            }
        }

        void OnEnable()
        {
            nextRefresh = 0;
            nextCheckpointRetry = 0;
            telemetryInitialized = false;
            Bind(startButton, StartClicked); Bind(continueButton, ContinueClicked);
            Bind(newProfileButton, NewProfile); Bind(chooseProfileButton, ChooseProfile);
            Bind(quitButton, Quit); Bind(saveProfileButton, SaveProfile);
            Bind(cancelProfileButton, ShowHome); Bind(openModuleButton, StartModuleOne);
            Bind(cognitiveLockedButton, StartModuleTwo);
            Bind(visuomotorLockedButton, StartModuleThree);
            Bind(patientButton, ShowPatient); Bind(doctorButton, ShowDoctor);
            Bind(homeFromModulesButton, ShowHome); Bind(homeFromPatientButton, ShowHome);
            Bind(homeFromDoctorButton, ShowHome); Bind(gameplayHomeButton, ReturnHome);
        }

        void OnDisable()
        {
            foreach (var remove in unbind) remove();
            unbind.Clear();
        }

        void Bind(Button button, UnityAction callback)
        {
            if (button == null) return;
            button.onClick.AddListener(callback);
            unbind.Add(() => button.onClick.RemoveListener(callback));
        }

        void Start()
        {
            if (testing && !JourneyAcceptance) { Show(false); return; }
            ShowHome();
        }

        public void SetStoreForAcceptance(RehabProfileStore store)
        {
            if (!testing) throw new InvalidOperationException("Profile store override requires acceptance mode");
            Store = store;
        }

        void Update()
        {
            // Submit is Enter/Return only. During active play
            // Space must squeeze the grip, never submit a previously selected
            // HUD/Menu button (including MAIN MENU or QUIT).
            var events = EventSystem.current;
            if (events != null)
            {
                bool navigation = BlocksGameplay || (experience != null && experience.player != null && experience.player.Paused);
                if (events.sendNavigationEvents != navigation) events.sendNavigationEvents = navigation;
                if (!navigation && events.currentSelectedGameObject != null)
                    events.SetSelectedGameObject(null);
            }
            if (CurrentPage == "Activity")
            {
                if (Application.isFocused && !TextEntryHasFocus()) exercises.Tick();
                return;
            }
            bool validityChanged = !telemetryInitialized || lastForceValid != experience.ForceValid;
            if (!validityChanged && Time.realtimeSinceStartup < nextRefresh) return;
            nextRefresh = Time.realtimeSinceStartup + .25f;
            UpdateTelemetry();
            lastForceValid = experience.ForceValid;
            telemetryInitialized = true;
            if (!testing && !BlocksGameplay) SaveCheckpointIfChanged();
        }

        static void Set(CanvasGroup panel, bool visible)
        {
            if (panel == null) return;
            panel.alpha = visible ? 1 : 0;
            panel.interactable = visible;
            panel.blocksRaycasts = visible;
        }

        void Show(bool visible)
        {
            BlocksGameplay = visible;
            Set(root, visible);
            if (gameplayLayers != null)
                foreach (var layer in gameplayLayers) Set(layer, !visible);
            if (experience != null && experience.player != null && visible)
                experience.player.SetPaused(true);
            if (!visible && EventSystem.current != null)
            {
                EventSystem.current.sendNavigationEvents = false;
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        void Page(string page)
        {
            if (page != "Portal") research?.HidePresentation();
            CurrentPage = page;
            Show(true);
            Set(homePanel, page == "Home"); Set(profilePanel, page == "Profile");
            Set(modulePanel, page == "Modules"); Set(patientPanel, page == "Patient");
            Set(doctorPanel, page == "Doctor");
            Set(exercises.Group, page == "Activity");
        }

        public void ShowHome()
        {
            if (research != null) { research.Welcome(); return; }
            if (experience != null) { experience.Release(); experience.SetHardware(false); }
            Page("Home");
            RefreshProfiles();
            var active = Store == null ? null : Store.Active;
            homeGreeting.text = Store == null ? "Profile storage unavailable — guest practice is available" :
                active == null ? "Move your hand and hold over a button for 5 seconds; Start opens guest practice" :
                "Welcome back, " + active.name + "  |  " + active.affectedHand + " hand";
            continueButton.interactable = active != null && active.hasSession;
        }

        void RefreshProfiles()
        {
            if (profilePicker == null) return;
            profilePicker.ClearOptions();
            var labels = new List<string>();
            if (Store != null) foreach (var profile in Store.Catalog.profiles)
                labels.Add(profile.name + "  \u00b7  " + profile.affectedHand + " hand");
            if (labels.Count == 0) labels.Add("No profiles yet");
            profilePicker.AddOptions(labels);
            if (Store != null && Store.Active != null)
                profilePicker.SetValueWithoutNotify(Store.Catalog.profiles.IndexOf(Store.Active));
            profilePicker.interactable = Store != null && Store.Catalog.profiles.Count > 0;
            chooseProfileButton.interactable = profilePicker.interactable;
        }

        void NewProfile()
        {
            if (research != null && !research.CanCreateProfile) { research.Welcome(); return; }
            if (formError != null) formError.text = Store == null ?
                "Saved profiles could not be opened. Your files were kept unchanged; guest practice still works." :
                "Local prototype profile. Leave optional fields blank if preferred.";
            nameField.text = ageField.text = concernField.text = goalField.text = painField.text = "";
            genderPicker.value = 0; affectedPicker.value = 0; dominantPicker.value = 0;
            Page("Profile");
        }

        void ChooseProfile()
        {
            if (Store == null || Store.Catalog.profiles.Count == 0) return;
            try
            {
                if (Store.Select(Store.Catalog.profiles[Mathf.Clamp(profilePicker.value, 0, Store.Catalog.profiles.Count - 1)].id))
                {
                    guestMode = false;
                    ShowHome();
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                                          error is Win32Exception)
            { homeGreeting.text = "Could not save profile selection. Check local storage access."; Debug.LogError("[PROFILE] " + error.Message); }
        }

        static string Selected(Dropdown picker) => picker.options[picker.value].text;

        void SaveProfile()
        {
            if (research != null && !research.CanCreateProfile) { research.Welcome(); return; }
            if (Store == null) { formError.text = "Saved profiles could not be opened, so a new profile cannot be saved. Use guest practice."; return; }
            if (!int.TryParse(ageField.text.Trim(), out var age)) { formError.text = "Enter a valid age."; return; }
            int pain = -1;
            if (!string.IsNullOrWhiteSpace(painField.text) && !int.TryParse(painField.text.Trim(), out pain))
            { formError.text = "Pain entry must be a whole number from 0 to 10."; return; }
            try
            {
                Store.Create(nameField.text, age, Selected(genderPicker), Selected(affectedPicker),
                    Selected(dominantPicker), concernField.text, goalField.text, pain);
                guestMode = false;
                if (research != null) { research.ProfileCreated(Store.Active.id); return; }
                ShowModules();
            }
            catch (Exception error) when (error is ArgumentException || error is IOException ||
                                          error is UnauthorizedAccessException || error is Win32Exception)
            { formError.text = error.Message; }
        }

        void StartClicked()
        {
            if (research != null) { research.OpenPreparation(); return; }
            guestMode = Store == null || Store.Active == null;
            ShowModules();
        }

        void ContinueClicked()
        {
            if (research != null) { research.ContinuePractice(); return; }
            ContinueSavedPractice();
        }

        public void ContinueSavedPractice()
        {
            if (research != null && !research.AuthorizeTraining()) return;
            var profile = Store == null ? null : Store.Active;
            if (profile == null || !profile.hasSession) return;
            if (profile.lastModule == 2 || profile.lastModule == 3)
                StartExercise(profile.lastModule, profile.lastExercise);
            else StartModuleOne(true);
        }

        public void ShowModules()
        {
            if (research != null && !research.AuthorizeTraining()) return;
            if (research != null && !research.LeavingGame()) return;
            if ((Store == null || Store.Active == null) && !guestMode) guestMode = true;
            moduleGreeting.text = guestMode ? "GUEST PRACTICE  /  Results are not saved" :
                Store.Active.name + "  /  Choose a training module";
            cognitiveLockedButton.interactable = true;
            visuomotorLockedButton.interactable = true;
            UpdateModuleCopy();
            Page("Modules");
        }

        void StartModuleOne() => StartModuleOne(false);

        void StartModuleTwo() => StartExercise(2, 0);
        void StartModuleThree() => StartExercise(3, 0);

        void StartExercise(int module, int task)
        {
            if (research != null && !research.AuthorizeTraining()) return;
            if ((Store == null || Store.Active == null) && !guestMode) { NewProfile(); return; }
            experience.Release();
            experience.SetHardware(false);
            if (research != null) research.ApplyChosenInput();
            exercises.SaveEnabled = !guestMode;
            exercises.Open(module, Mathf.Clamp(task, 0, 1));
            if (research == null || research.LiveSession) Page("Activity");
        }

        void SaveExerciseStart(int module, int task)
        {
            if (research != null && !research.ModuleStarted(module, task)) return;
            if (guestMode || Store == null || Store.Active == null) return;
            try { Store.SaveExerciseStart(module, task); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                                          error is Win32Exception)
            { Debug.LogError("[PROFILE] Exercise checkpoint failed: " + error.Message); }
        }

        void SaveExerciseResult(int module, int task, int score, float seconds)
        {
            if (research != null) research.ExerciseCompleted(score, seconds, exercises.LastOutcomes);
            if (guestMode || Store == null || Store.Active == null) return;
            try { Store.SaveExerciseResult(module, task, score, seconds); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                                          error is Win32Exception)
            { Debug.LogError("[PROFILE] Exercise result failed: " + error.Message); }
        }

        void StartModuleOne(bool resume)
        {
            if (research != null && !research.AuthorizeTraining()) return;
            var profile = Store == null ? null : Store.Active;
            if (profile == null && !guestMode) { NewProfile(); return; }
            if (!resume) { experience.ResetObjects(); choices.ResetSelectionForTests(); }
            choices.SelectStation(resume && profile != null && profile.kitchen);
            choices.SelectObject(resume && profile != null ?
                (profile.kitchen ? profile.kitchenObject : profile.livingObject) : 0);
            experience.SelectHand(profile != null && !guestMode &&
                (resume ? profile.leftHand : profile.affectedHand.StartsWith("Left", StringComparison.OrdinalIgnoreCase)));
            var mode = resume && profile != null ? profile.inputMode : research != null ? research.InputMode : 0;
            if (mode == 2) experience.SetHardware(true);
            else if (mode == 1) experience.SetHardware(false);
            else experience.SetCameraMode();
            if (research != null && !research.ModuleStarted(1, 0)) return;
            Page("Game");
            Show(false);
            experience.player.SetPaused(false);
            lastCheckpoint = null;
            SaveCheckpointIfChanged();
        }

        public void ShowResearchPortal()
        {
            if (CurrentPage == "Game" || CurrentPage == "Activity") researchReturnPage = CurrentPage;
            Page("Portal");
        }

        public void ResumeResearchGame()
        {
            Page(researchReturnPage);
            if (researchReturnPage == "Game")
            {
                Show(false);
                experience.player.SetPaused(false);
            }
        }

        public void OpenResearchProfile() => NewProfile();
        public void SetResearchIdentity(string profileId)
        {
            guestMode = string.IsNullOrEmpty(profileId);
            if (!guestMode && (Store == null || !Store.Select(profileId)))
                throw new InvalidOperationException("Your linked hand profile is unavailable. Ask the local doctor to assign a profile.");
        }
        public void QuitResearchApplication() { if (research != null) research.LeavingGame(); Quit(); }

        public void ReturnHome()
        {
            if (!BlocksGameplay) SaveCheckpointIfChanged();
            ShowHome();
        }

        void SaveCheckpointIfChanged()
        {
            if (guestMode || Store == null || Store.Active == null || BlocksGameplay || choices == null ||
                Time.realtimeSinceStartup < nextCheckpointRetry) return;
            int mode = experience.Hardware ? 2 : experience.CameraMode ? 0 : 1;
            string key = choices.Kitchen + ":" + choices.LivingSelection + ":" + choices.KitchenSelection +
                         ":" + experience.LeftSelected + ":" + mode;
            if (key == lastCheckpoint) return;
            try
            {
                Store.SaveCheckpoint(choices.Kitchen, choices.LivingSelection, choices.KitchenSelection,
                                     experience.LeftSelected, mode);
                lastCheckpoint = key;
                nextCheckpointRetry = 0;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                                          error is Win32Exception)
            { nextCheckpointRetry = Time.realtimeSinceStartup + 5f; Debug.LogError("[PROFILE] Checkpoint save failed: " + error.Message); }
        }

        void ShowPatient()
        {
            if (research != null) { research.Dashboard(); return; }
            var p = Store == null ? null : Store.Active;
            if (p == null) { NewProfile(); return; }
            patientSummary.text = "PATIENT / " + p.name + "\n\nHand to train: " + p.affectedHand +
                "    Dominant hand: " + p.dominantHand +
                "\nYour goal: " + (string.IsNullOrEmpty(p.goal) ? "Not entered" : p.goal) +
                "\n\n" + (p.hasSession ? "Last module " + Mathf.Max(1, p.lastModule) +
                    " • " + p.lastSessionUtc : "No saved session yet") +
                "\nCompleted exercises: " + (p.results == null ? 0 : p.results.Count) +
                "\n\nScores are game metrics, not clinical measures.";
            Page("Patient");
        }

        void ShowDoctor()
        {
            if (research != null) { research.Dashboard(); return; }
            var p = Store == null ? null : Store.Active;
            if (p == null) { NewProfile(); return; }
            int count = p.results == null ? 0 : p.results.Count;
            int best = 0;
            if (p.results != null) foreach (var result in p.results)
                best = Mathf.Max(best, result.score);
            doctorSummary.text = "RESEARCH DASHBOARD  /  LOCAL PROTOTYPE\n\n" +
                "Patient: " + p.name + "     Hand: " + p.affectedHand +
                "\nCompleted exercises: " + count + "     Best game score: " + best + "/100" +
                "\nLast activity: " + (p.hasSession ? p.lastSessionUtc : "None") +
                "\n\nGame scores are not clinical recommendations or measured strength.";
            Page("Doctor");
        }

        void UpdateTelemetry()
        {
            if (telemetry == null && hudValue == null) return;
            string input = experience.CameraMode ? "CAMERA \u00b7 VISUAL ESTIMATE" :
                experience.Hardware ? (experience.ForceValid && experience.forceReceiver.State.IsSynthetic ?
                    "SYNTHETIC REPLAY" : "PHASE 2 \u00b7 " + (experience.ForceValid ? "LIVE" : "WAITING")) : "KEYBOARD DEMO";
            if (telemetry != null)
                telemetry.text = "GRIP REHAB   /   FORCE + OBJECT   /   MODULE 01";
            if (hudRoom == null) return;
            hudRoom.text = choices.Kitchen ? "Kitchen station" : "Living room station";
            hudHand.text = (experience.LeftSelected ? "LEFT" : "RIGHT") + " HAND  \u00b7  MODULE 01";
            hudSource.text = input;
            hudValue.text = experience.CameraMode ? (experience.ForceValid ?
                (experience.CameraGripScore * 100).ToString("F0") : "--") :
                experience.ForceValid ? experience.ForceNewtons.ToString("F1") : "--";
            hudUnit.text = experience.CameraMode ? "% closure" : "newtons";
            hudObject.text = experience.Held == null ? "OBJECT  /  " + choices.SelectedObject.displayName :
                "HOLDING  /  " + experience.Held.displayName + "  \u00b7  " +
                (experience.Held.MaxDisplacementMeters * 1000).ToString("F1") + " mm";
            hudPrompt.text = experience.player.Paused ? "Choose a room, object and input. Press Resume when ready." :
                experience.CameraMode ? (experience.ForceValid ?
                    experience.Held == null ? "Close your tracked hand to grasp, or press G to summon." :
                    "Move your tracked hand to deform the object. Open it to release." :
                    "Show one complete hand to the camera to begin tracking.") :
                experience.Hardware ? (experience.ForceValid ?
                    experience.Held == null ? "Close your hand around the selected object." :
                    "Grip the object with controlled force; release when ready." :
                    "Waiting for a fresh calibrated Phase 2 force sample.") :
                experience.Held == null ? "Aim at the object and press E. Hold Space or click to squeeze." :
                    "Hold Space or click to squeeze. Press Q to release.";
            var rounds = research != null ? research.ForceRounds : null;
            ReadableDigits();
            UpdateEffortGauge(rounds);
            if (rounds != null && !rounds.Complete)
            {
                // Stage 3.1 round HUD: what to do, how far along, and effort against the band.
                var round = rounds.Current;
                hudProgressFill.fillAmount = Mathf.Clamp01(round.Fraction);
                hudProgress.text = "ROUND " + (rounds.RoundIndex + 1) + " / " + rounds.RoundCount + "  ·  " + round.Kind.ToString().ToUpperInvariant() +
                    "  ·  EFFORT " + Mathf.RoundToInt(research.CurrentEffort * 100) + "%";
                if (experience.Held != null && !experience.player.Paused)
                    hudPrompt.text = rounds.Instruction + (round.Breaks > 0 ? "  (cracked " + round.Breaks + "x)" : "") + "\n% of " + research.EffortReferenceLabel;
                return;
            }
            float progress = experience.CameraMode ? experience.CameraGripScore :
                Mathf.Clamp01(experience.ProgressSeconds / experience.RequiredHoldSeconds);
            hudProgressFill.fillAmount = progress;
            hudProgress.text = experience.CameraMode ? "VISUAL HAND CLOSURE" :
                "CONTROLLED HOLD  " + experience.ProgressSeconds.ToString("F1") + " / " + experience.RequiredHoldSeconds.ToString("F1") + " S";
        }

        // ---------- Stage 4.4 HUD presentation (runtime only; the saved scene is unchanged) ----------
        RectTransform effortGauge, effortBand, effortMarker;
        GripRehab.UI.GripUiThemeController uiTheme;

        // The heading font draws zero with a slash ("Ø.Ø"); force digits use the body font.
        void ReadableDigits()
        {
            if (uiTheme == null && root != null) { var owner = root.GetComponentInParent<Canvas>(); if (owner != null) uiTheme = owner.GetComponentInChildren<GripRehab.UI.GripUiThemeController>(true); }
            var body = uiTheme != null && uiTheme.Current != null ? uiTheme.Current.latinFont : null;
            if (body != null && hudValue.font != body) hudValue.font = body;
        }

        // A bar under the round progress: the green target band and a live effort marker.
        void UpdateEffortGauge(GripRehab.Product.ForceRoundSession rounds)
        {
            bool show = rounds != null && !rounds.Complete && hudProgressFill != null;
            if (!show) { if (effortGauge != null) effortGauge.gameObject.SetActive(false); return; }
            if (effortGauge == null)
            {
                var track = hudProgressFill.rectTransform.parent as RectTransform;
                if (track == null) return;
                effortGauge = GaugeRect("Effort gauge", track.parent, new Color(.06f, .09f, .13f, .9f));
                effortGauge.anchorMin = track.anchorMin; effortGauge.anchorMax = track.anchorMax; effortGauge.pivot = track.pivot;
                effortGauge.sizeDelta = new Vector2(track.sizeDelta.x, Mathf.Max(10, track.sizeDelta.y));
                effortGauge.anchoredPosition = track.anchoredPosition - new Vector2(0, Mathf.Max(10, track.rect.height) + 8);
                effortBand = GaugeRect("Target band", effortGauge, new Color(.22f, .84f, .48f, .65f));
                effortMarker = GaugeRect("Effort marker", effortGauge, Color.white);
                effortMarker.sizeDelta = new Vector2(4, 6);
            }
            effortGauge.gameObject.SetActive(true);
            const float scale = 1.2f; // the bar spans 0..120% of the reference
            var round = rounds.Current;
            effortBand.anchorMin = new Vector2(round.BandLow / scale, 0); effortBand.anchorMax = new Vector2(round.BandHigh / scale, 1);
            effortBand.offsetMin = effortBand.offsetMax = Vector2.zero;
            float x = Mathf.Clamp01(research.CurrentEffort / scale);
            effortMarker.anchorMin = new Vector2(x, 0); effortMarker.anchorMax = new Vector2(x, 1);
            effortMarker.offsetMin = new Vector2(-2, -3); effortMarker.offsetMax = new Vector2(2, 3);
            effortMarker.GetComponent<Image>().color = research.CurrentEffort > round.BreakAt && round.Kind == GripRehab.Product.ForceRoundKind.Control
                ? new Color(1, .35f, .4f) : Color.white;
        }

        static RectTransform GaugeRect(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return rect;
        }

        // Module card copy lives in the protected scene; refresh it to describe the Stage 3 games.
        bool moduleCopyUpdated;
        void UpdateModuleCopy()
        {
            if (moduleCopyUpdated || modulePanel == null) return;
            moduleCopyUpdated = true;
            foreach (var text in modulePanel.GetComponentsInChildren<Text>(true))
            {
                if (text.text.StartsWith("Grip an everyday object")) text.text = "Squeeze an everyday object through hold, control and pulse rounds with live effort feedback.";
                else if (text.text.StartsWith("Sequence memory and spatial")) text.text = "Repeat a hand avatar's gestures, then drag shapes onto matching outlines.";
                else if (text.text.StartsWith("Visual tracing and maze")) text.text = "Trace a guided line with green/red feedback and steer through generated mazes.";
            }
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
