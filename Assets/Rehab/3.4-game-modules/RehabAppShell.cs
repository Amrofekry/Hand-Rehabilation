using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GripRehab
{
    // WBS 3.4 shell: patient identity and navigation around the existing 3.4.1 task.
    // Future modules and clinical dashboards remain explicit, non-playable previews.
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
        readonly List<Action> unbind = new List<Action>();
        string lastCheckpoint;
        float nextRefresh;
        bool testing;

        void Awake()
        {
            testing = Array.Exists(Environment.GetCommandLineArgs(), a => a.StartsWith("-apartment-"));
#if UNITY_EDITOR
            testing |= UnityEditor.SessionState.GetBool("ApartmentQA.Active", false);
#endif
            try { Store = new RehabProfileStore(Path.Combine(Application.persistentDataPath, "GripRehab")); }
            catch (Exception error) { Debug.LogError("[PROFILE] " + error.Message); }
        }

        void OnEnable()
        {
            Bind(startButton, StartClicked); Bind(continueButton, ContinueClicked);
            Bind(newProfileButton, NewProfile); Bind(chooseProfileButton, ChooseProfile);
            Bind(quitButton, Quit); Bind(saveProfileButton, SaveProfile);
            Bind(cancelProfileButton, ShowHome); Bind(openModuleButton, StartModuleOne);
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
            if (testing) { Show(false); return; }
            ShowHome();
        }

        public void SetStoreForAcceptance(RehabProfileStore store)
        {
            if (!testing) throw new InvalidOperationException("Profile store override requires acceptance mode");
            Store = store;
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .25f;
            if (BlocksGameplay) return;
            UpdateTelemetry();
            if (!testing) SaveCheckpointIfChanged();
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
        }

        void Page(string page)
        {
            CurrentPage = page;
            Show(true);
            Set(homePanel, page == "Home"); Set(profilePanel, page == "Profile");
            Set(modulePanel, page == "Modules"); Set(patientPanel, page == "Patient");
            Set(doctorPanel, page == "Doctor");
        }

        public void ShowHome()
        {
            if (experience != null) { experience.Release(); experience.SetHardware(false); }
            Page("Home");
            RefreshProfiles();
            var active = Store == null ? null : Store.Active;
            homeGreeting.text = active == null ? "Begin with a local patient profile" :
                "Welcome back, " + active.name + "  |  " + active.affectedHand + " hand";
            continueButton.interactable = active != null && active.hasSession;
        }

        void RefreshProfiles()
        {
            if (profilePicker == null) return;
            profilePicker.ClearOptions();
            var labels = new List<string>();
            if (Store != null) foreach (var profile in Store.Catalog.profiles)
                labels.Add(profile.name + "  ·  " + profile.affectedHand + " hand");
            if (labels.Count == 0) labels.Add("No profiles yet");
            profilePicker.AddOptions(labels);
            if (Store != null && Store.Active != null)
                profilePicker.SetValueWithoutNotify(Store.Catalog.profiles.IndexOf(Store.Active));
            profilePicker.interactable = Store != null && Store.Catalog.profiles.Count > 0;
            chooseProfileButton.interactable = profilePicker.interactable;
        }

        void NewProfile()
        {
            if (formError != null) formError.text = "Local prototype profile. Leave optional fields blank if preferred.";
            nameField.text = ageField.text = concernField.text = goalField.text = painField.text = "";
            genderPicker.value = 0; affectedPicker.value = 0; dominantPicker.value = 0;
            Page("Profile");
        }

        void ChooseProfile()
        {
            if (Store == null || Store.Catalog.profiles.Count == 0) return;
            if (Store.Select(Store.Catalog.profiles[Mathf.Clamp(profilePicker.value, 0, Store.Catalog.profiles.Count - 1)].id))
                ShowHome();
        }

        static string Selected(Dropdown picker) => picker.options[picker.value].text;

        void SaveProfile()
        {
            if (Store == null) { formError.text = "Profile storage is unavailable. Check the Console."; return; }
            if (!int.TryParse(ageField.text.Trim(), out var age)) { formError.text = "Enter a valid age."; return; }
            int pain = -1;
            if (!string.IsNullOrWhiteSpace(painField.text) && !int.TryParse(painField.text.Trim(), out pain))
            { formError.text = "Pain entry must be a whole number from 0 to 10."; return; }
            try
            {
                Store.Create(nameField.text, age, Selected(genderPicker), Selected(affectedPicker),
                    Selected(dominantPicker), concernField.text, goalField.text, pain);
                ShowModules();
            }
            catch (Exception error) when (error is ArgumentException || error is IOException)
            { formError.text = error.Message; }
        }

        void StartClicked()
        {
            if (Store == null || Store.Active == null) NewProfile();
            else ShowModules();
        }

        void ContinueClicked()
        {
            var profile = Store == null ? null : Store.Active;
            if (profile == null || !profile.hasSession) return;
            StartModuleOne(true);
        }

        public void ShowModules()
        {
            if (Store == null || Store.Active == null) { NewProfile(); return; }
            moduleGreeting.text = Store.Active.name + "  /  Choose a training module";
            cognitiveLockedButton.interactable = false;
            visuomotorLockedButton.interactable = false;
            Page("Modules");
        }

        void StartModuleOne() => StartModuleOne(false);

        void StartModuleOne(bool resume)
        {
            var profile = Store == null ? null : Store.Active;
            if (profile == null) { NewProfile(); return; }
            if (!resume) { experience.ResetObjects(); choices.ResetSelectionForTests(); }
            choices.SelectStation(resume && profile.kitchen);
            choices.SelectObject(resume ? (profile.kitchen ? profile.kitchenObject : profile.livingObject) : 0);
            experience.SelectHand(resume && profile.leftHand);
            var mode = resume ? profile.inputMode : 0;
            if (mode == 2) experience.SetHardware(true);
            else if (mode == 1) experience.SetHardware(false);
            else experience.SetCameraMode();
            Page("Game");
            Show(false);
            experience.player.SetPaused(false);
            lastCheckpoint = null;
            SaveCheckpointIfChanged();
        }

        public void ReturnHome()
        {
            if (!BlocksGameplay) SaveCheckpointIfChanged();
            ShowHome();
        }

        void SaveCheckpointIfChanged()
        {
            if (Store == null || Store.Active == null || BlocksGameplay || choices == null) return;
            int mode = experience.Hardware ? 2 : experience.CameraMode ? 0 : 1;
            string key = choices.Kitchen + ":" + choices.LivingSelection + ":" + choices.KitchenSelection +
                         ":" + experience.LeftSelected + ":" + mode;
            if (key == lastCheckpoint) return;
            try
            {
                Store.SaveCheckpoint(choices.Kitchen, choices.LivingSelection, choices.KitchenSelection,
                                     experience.LeftSelected, mode);
                lastCheckpoint = key;
            }
            catch (IOException error) { Debug.LogError("[PROFILE] Checkpoint save failed: " + error.Message); }
        }

        void ShowPatient()
        {
            var p = Store == null ? null : Store.Active;
            if (p == null) { NewProfile(); return; }
            patientSummary.text = "PATIENT / " + p.name + "\n\nHand to train: " + p.affectedHand +
                "    Dominant hand: " + p.dominantHand +
                "\nYour goal: " + (string.IsNullOrEmpty(p.goal) ? "Not entered" : p.goal) +
                "\n\n" + (p.hasSession ? "Module 1 session saved • " + p.lastSessionUtc : "No saved session yet") +
                "\n\nForce history, baseline and clinical progress: planned for WBS 5.1.";
            Page("Patient");
        }

        void ShowDoctor()
        {
            doctorSummary.text = "DOCTOR DASHBOARD  /  WBS 5.2\n\n" +
                "Patient history     ·     Trend charts     ·     Recommendations\n\n" +
                "Preview only — patient records and recommendations are not available yet.";
            Page("Doctor");
        }

        void UpdateTelemetry()
        {
            if (telemetry == null && hudValue == null) return;
            string input = experience.CameraMode ? "CAMERA · VISUAL ESTIMATE" :
                experience.Hardware ? "PHASE 2 · " + (experience.ForceValid ? "LIVE" : "WAITING") : "KEYBOARD DEMO";
            if (telemetry != null)
                telemetry.text = "GRIP REHAB   /   FORCE + OBJECT   /   MODULE 01";
            if (hudRoom == null) return;
            hudRoom.text = choices.Kitchen ? "Kitchen station" : "Living room station";
            hudHand.text = (experience.LeftSelected ? "LEFT" : "RIGHT") + " HAND  ·  MODULE 01";
            hudSource.text = input;
            hudValue.text = experience.CameraMode ? (experience.CameraGripScore * 100).ToString("F0") :
                experience.ForceValid ? experience.ForceNewtons.ToString("F1") : "--";
            hudUnit.text = experience.CameraMode ? "% closure" : "newtons";
            hudObject.text = experience.Held == null ? "OBJECT  /  " + choices.SelectedObject.displayName :
                "HOLDING  /  " + experience.Held.displayName + "  ·  " +
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
            float progress = experience.CameraMode ? experience.CameraGripScore :
                Mathf.Clamp01(experience.ProgressSeconds / 3f);
            hudProgressFill.fillAmount = progress;
            hudProgress.text = experience.CameraMode ? "VISUAL HAND CLOSURE" :
                "CONTROLLED HOLD  " + experience.ProgressSeconds.ToString("F1") + " / 3.0 S";
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
