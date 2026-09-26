using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GripRehab.Editor
{
    // Creates editable, saved uGUI objects once. Re-running never replaces edits.
    public static class RehabShellAuthoring
    {
        static readonly Color Navy = new Color(.025f, .065f, .105f, .97f);
        static readonly Color Panel = new Color(.055f, .12f, .17f, .98f);
        static readonly Color Card = new Color(.085f, .17f, .22f, 1);
        static readonly Color Mint = new Color(.34f, .88f, .73f, 1);
        static readonly Color Blue = new Color(.19f, .48f, .69f, 1);
        static readonly Color White = new Color(.92f, .97f, .98f, 1);
        static readonly Color Muted = new Color(.66f, .78f, .81f, 1);
        static readonly Color Disabled = new Color(.12f, .20f, .25f, 1);
        static Font font;

        [MenuItem("Grip Rehab/Author Main Menu and HUD")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);
            if (UnityEngine.Object.FindAnyObjectByType<RehabAppShell>() != null)
            {
                Debug.Log("[REHAB_UI] Existing shell preserved; edit its saved objects in the scene.");
                return;
            }
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            var app = UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
            var choices = UnityEngine.Object.FindAnyObjectByType<ApartmentChoices>();
            if (canvas == null || app == null || choices == null) throw new Exception("Apartment UI is not available");
            var previous = canvas.transform.Cast<Transform>().ToArray();

            var chrome = PanelAt(canvas.transform, "Gameplay top bar", new Vector2(.5f, 1),
                new Vector2(0, -7), new Vector2(1540, 40), new Color(.02f, .06f, .095f, .80f));
            chrome.GetComponent<RectTransform>().pivot = new Vector2(.5f, 1);
            var shell = chrome.AddComponent<RehabAppShell>();
            shell.experience = app; shell.choices = choices;
            shell.telemetry = Label(chrome.transform, "Live mode and units", "MODULE 01  /  CAMERA", new Vector2(-115, -20), new Vector2(1130, 30), 15, Mint, TextAnchor.MiddleLeft);
            shell.gameplayHomeButton = ButtonAt(chrome.transform, "Main menu button", "MAIN MENU", new Vector2(680, -20), new Vector2(145, 32), Card, 14);

            var overlay = PanelAt(canvas.transform, "Rehab main menu overlay", new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(1600, 900), Navy);
            Stretch(overlay.GetComponent<RectTransform>());
            shell.root = overlay.AddComponent<CanvasGroup>();
            shell.homePanel = Page(overlay.transform, "Home page");
            shell.profilePanel = Page(overlay.transform, "New patient profile page");
            shell.modulePanel = Page(overlay.transform, "Training module page");
            shell.patientPanel = Page(overlay.transform, "Patient dashboard preview");
            shell.doctorPanel = Page(overlay.transform, "Doctor dashboard preview");
            SetPage(shell.profilePanel, false); SetPage(shell.modulePanel, false);
            SetPage(shell.patientPanel, false); SetPage(shell.doctorPanel, false);
            var groups = previous.Select(t => t.GetComponent<CanvasGroup>() ?? t.gameObject.AddComponent<CanvasGroup>()).ToList();
            groups.Add(chrome.GetComponent<CanvasGroup>() ?? chrome.AddComponent<CanvasGroup>());
            shell.gameplayLayers = groups.ToArray();

            Home(shell); Profile(shell); Modules(shell); Patient(shell); Doctor(shell);
            PolishExisting(app, choices);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            ApartmentAuthoring.VerifyScene();
            Debug.Log("[REHAB_UI] PASS: saved main menu, profile, modules, dashboard previews and gameplay chrome");
        }

        // One-time visual correction for the authored scene. Keep this separate from Build so
        // later user edits to saved menu objects are never overwritten by verification.
        public static void PolishSavedShell()
        {
            var scene = EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);
            var shell = UnityEngine.Object.FindAnyObjectByType<RehabAppShell>();
            if (shell == null) throw new Exception("Author the menu before polishing it");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            shell.root.GetComponent<Image>().color = new Color(Navy.r, Navy.g, Navy.b, 1f);
            var continueLabel = shell.continueButton.GetComponentInChildren<Text>();
            continueLabel.text = "CONTINUE PRACTICE";
            foreach (var dropdown in new[] { shell.profilePicker, shell.genderPicker,
                         shell.affectedPicker, shell.dominantPicker })
            {
                var arrow = dropdown.transform.Find("Arrow");
                if (arrow == null) continue;
                var icon = arrow.GetComponent<Image>();
                if (icon != null) icon.color = Color.clear;
                if (arrow.Find("Chevron") == null)
                    Label(arrow, "Chevron", "▼", Vector2.zero, new Vector2(36, 36), 15, Mint);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[REHAB_UI] Saved menu contrast, dropdown arrows and checkpoint label polished");
        }

        static CanvasGroup Page(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            return go.GetComponent<CanvasGroup>();
        }

        static void SetPage(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1 : 0; group.interactable = visible; group.blocksRaycasts = visible;
        }

        static void Home(RehabAppShell s)
        {
            var p = s.homePanel.transform;
            var hero = PanelAt(p, "Introduction", new Vector2(.5f, .5f), new Vector2(-350, 0), new Vector2(720, 720), Panel);
            Label(hero.transform, "Eyebrow", "HAND REHABILITATION  /  APARTMENT LAB", new Vector2(-5, 265), new Vector2(620, 30), 17, Mint, TextAnchor.MiddleLeft);
            Label(hero.transform, "Title", "GRIP  /  REHAB", new Vector2(-5, 174), new Vector2(620, 95), 62, White, TextAnchor.MiddleLeft);
            Label(hero.transform, "Statement", "A calm space to practise meaningful hand movement.", new Vector2(-5, 80), new Vector2(620, 80), 27, White, TextAnchor.MiddleLeft);
            Label(hero.transform, "Description", "Explore a familiar apartment. Move your virtual hand, grasp everyday objects, and see how they respond.", new Vector2(-5, -55), new Vector2(620, 95), 19, Muted, TextAnchor.UpperLeft);
            // Five tapered bars suggest finger motion without importing extra assets.
            var lengths = new[] { 90f, 130f, 145f, 126f, 95f };
            for (int i = 0; i < lengths.Length; i++)
                PanelAt(hero.transform, "Finger motion " + (i + 1), new Vector2(.5f, .5f),
                    new Vector2(-225 + i * 75, -226 + (lengths[i] - 90) * .27f), new Vector2(52, lengths[i]),
                    i == 1 ? Mint : new Color(.12f, .42f, .49f, 1));
            Label(hero.transform, "Prototype note", "LOCAL PROTOTYPE   •   MODULE 01 AVAILABLE   •   GP2 MODULES PLANNED",
                new Vector2(-5, -320), new Vector2(620, 28), 13, Muted, TextAnchor.MiddleLeft);

            var actions = PanelAt(p, "Patient actions", new Vector2(.5f, .5f), new Vector2(430, 0), new Vector2(670, 720), Card);
            Label(actions.transform, "Welcome", "Your training space", new Vector2(0, 302), new Vector2(565, 44), 31, White);
            s.homeGreeting = Label(actions.transform, "Active profile", "Begin with a local patient profile", new Vector2(0, 250), new Vector2(565, 37), 17, Muted);
            s.startButton = ButtonAt(actions.transform, "Start game", "START GAME", new Vector2(0, 186), new Vector2(552, 57), Mint, 19, Navy);
            s.continueButton = ButtonAt(actions.transform, "Continue saved session", "CONTINUE", new Vector2(0, 120), new Vector2(552, 57), Blue, 18);
            s.newProfileButton = ButtonAt(actions.transform, "New profile", "NEW PROFILE", new Vector2(0, 54), new Vector2(552, 55), Panel, 17);
            s.profilePicker = DropdownAt(actions.transform, "Saved profiles", new Vector2(-88, -20), new Vector2(375, 48), new[] { "No profiles yet" });
            s.chooseProfileButton = ButtonAt(actions.transform, "Select profile", "SELECT", new Vector2(200, -20), new Vector2(174, 48), Blue, 16);
            s.patientButton = ButtonAt(actions.transform, "Patient dashboard", "PATIENT DASHBOARD", new Vector2(-140, -99), new Vector2(275, 54), Panel, 16);
            s.doctorButton = ButtonAt(actions.transform, "Doctor dashboard preview", "DOCTOR PREVIEW", new Vector2(140, -99), new Vector2(275, 54), Panel, 16);
            s.quitButton = ButtonAt(actions.transform, "Quit application", "QUIT", new Vector2(0, -260), new Vector2(552, 48), Disabled, 17);
            Label(actions.transform, "Privacy note", "Profiles stay on this computer. This prototype does not diagnose or prescribe treatment.",
                new Vector2(0, -186), new Vector2(555, 66), 14, Muted);
        }

        static void Profile(RehabAppShell s)
        {
            var p = PanelAt(s.profilePanel.transform, "Patient profile form", new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(1190, 760), Panel).transform;
            Label(p, "Heading", "CREATE PATIENT PROFILE", new Vector2(0, 327), new Vector2(1070, 50), 34, White);
            Label(p, "Instruction", "Self-reported context for your training. Age, name and hand selections are required; other fields are optional.",
                new Vector2(0, 279), new Vector2(1070, 47), 17, Muted);
            s.nameField = Field(p, "Name", new Vector2(-280, 192), new Vector2(495, 50), "Your name", 60);
            s.ageField = Field(p, "Age", new Vector2(280, 192), new Vector2(495, 50), "Years", 3, InputField.ContentType.IntegerNumber);
            s.genderPicker = DropdownAt(p, "Gender (optional)", new Vector2(-280, 83), new Vector2(495, 50),
                new[] { "Prefer not to say", "Female", "Male" });
            s.affectedPicker = DropdownAt(p, "Hand to train", new Vector2(280, 83), new Vector2(495, 50),
                new[] { "Right", "Left", "Both" });
            s.dominantPicker = DropdownAt(p, "Dominant hand", new Vector2(-280, -26), new Vector2(495, 50),
                new[] { "Right", "Left", "Both" });
            s.painField = Field(p, "Pain today (optional, 0–10)", new Vector2(280, -26), new Vector2(495, 50), "Leave blank if unsure", 2, InputField.ContentType.IntegerNumber);
            s.concernField = Field(p, "Hand concern / symptoms (optional)", new Vector2(-280, -136), new Vector2(495, 50), "What is difficult or uncomfortable?", 400);
            s.goalField = Field(p, "Everyday goal (optional)", new Vector2(280, -136), new Vector2(495, 50), "e.g. holding a cup", 200);
            s.formError = Label(p, "Validation message", "Local prototype profile. Leave optional fields blank if preferred.",
                new Vector2(0, -228), new Vector2(1070, 42), 16, Mint);
            s.cancelProfileButton = ButtonAt(p, "Cancel profile", "BACK", new Vector2(-190, -305), new Vector2(220, 53), Disabled, 17);
            s.saveProfileButton = ButtonAt(p, "Save profile", "SAVE PROFILE", new Vector2(162, -305), new Vector2(440, 53), Mint, 18, Navy);
        }

        static void Modules(RehabAppShell s)
        {
            var p = s.modulePanel.transform;
            s.moduleGreeting = Label(p, "Module greeting", "CHOOSE A TRAINING MODULE", new Vector2(0, 315), new Vector2(1360, 55), 34, White);
            Label(p, "Module instruction", "One interactive module is available. GP2 modules are visible for planning.",
                new Vector2(0, 267), new Vector2(1300, 35), 18, Muted);
            var first = ModuleCard(p, "01", "FORCE + OBJECT", "Grip an everyday object. Watch its response and practise a controlled hold.", -478, Mint);
            s.openModuleButton = ButtonAt(first, "Open force object module", "OPEN MODULE 01", new Vector2(0, -142), new Vector2(330, 55), Mint, 18, Navy);
            var second = ModuleCard(p, "02", "COGNITIVE", "Gesture sequence memory and spatial assembly. MediaPipe + YOLO planned.", 0, Blue);
            s.cognitiveLockedButton = ButtonAt(second, "Cognitive module locked", "GP2  /  PLANNED", new Vector2(0, -142), new Vector2(330, 55), Disabled, 17);
            var third = ModuleCard(p, "03", "VISUOMOTOR", "Maze navigation and visual tracing with accuracy feedback. Planned for GP2.", 478, Blue);
            s.visuomotorLockedButton = ButtonAt(third, "Visuomotor module locked", "GP2  /  PLANNED", new Vector2(0, -142), new Vector2(330, 55), Disabled, 17);
            Label(p, "Future pipeline", "COMING LATER  /  Baseline assessment  •  Fatigue-aware adaptation  •  Patient results  •  Doctor history",
                new Vector2(0, -283), new Vector2(1390, 40), 16, Muted);
            s.homeFromModulesButton = ButtonAt(p, "Back to main menu", "BACK TO MENU", new Vector2(0, -357), new Vector2(230, 44), Panel, 15);
        }

        static Transform ModuleCard(Transform parent, string number, string title, string description, float x, Color accent)
        {
            var card = PanelAt(parent, "Module " + number, new Vector2(.5f, .5f), new Vector2(x, 8), new Vector2(420, 440), Card).transform;
            Label(card, "Number", number, new Vector2(0, 140), new Vector2(330, 80), 58, accent);
            Label(card, "Name", title, new Vector2(0, 56), new Vector2(345, 45), 25, White);
            Label(card, "Description", description, new Vector2(0, -38), new Vector2(345, 100), 17, Muted);
            return card;
        }

        static void Patient(RehabAppShell s)
        {
            var p = PanelAt(s.patientPanel.transform, "Patient results layout", new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(1160, 710), Panel).transform;
            Label(p, "Title", "PATIENT DASHBOARD", new Vector2(0, 288), new Vector2(1050, 56), 35, White);
            s.patientSummary = Label(p, "Profile and session state", "No patient selected.", new Vector2(0, 55),
                new Vector2(980, 365), 20, White, TextAnchor.UpperLeft);
            s.homeFromPatientButton = ButtonAt(p, "Back to menu", "BACK TO MENU", new Vector2(0, -285), new Vector2(250, 50), Blue, 17);
        }

        static void Doctor(RehabAppShell s)
        {
            var p = PanelAt(s.doctorPanel.transform, "Doctor roadmap layout", new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(1160, 710), Panel).transform;
            Label(p, "Title", "DOCTOR DASHBOARD  /  PREVIEW", new Vector2(0, 288), new Vector2(1050, 56), 35, White);
            s.doctorSummary = Label(p, "Locked dashboard description", "WBS 5.2 is planned.", new Vector2(0, 34),
                new Vector2(980, 380), 22, Muted, TextAnchor.UpperLeft);
            s.homeFromDoctorButton = ButtonAt(p, "Back to menu", "BACK TO MENU", new Vector2(0, -285), new Vector2(250, 50), Blue, 17);
        }

        static void PolishExisting(ApartmentExperience app, ApartmentChoices choices)
        {
            var old = new Color(.035f, .11f, .15f, .91f);
            foreach (var text in new[] { app.statusText, app.forceText, app.contextText })
            {
                text.font = font; text.color = White; text.raycastTarget = false;
                var image = text.transform.parent.GetComponent<Image>();
                if (image != null) image.color = old;
            }
            app.statusText.fontSize = 16; app.forceText.fontSize = 16; app.contextText.fontSize = 15;
            choices.controls.GetComponent<Image>().color = old;
            foreach (var button in choices.objectButtons.Concat(new[] { choices.livingButton, choices.kitchenButton,
                         choices.handButton, choices.inputButton, choices.resumeButton }))
            {
                if (button == null) continue;
                var label = button.GetComponentInChildren<Text>();
                if (label != null) { label.font = font; label.fontSize = 15; label.color = White; }
                var colors = button.colors;
                colors.highlightedColor = new Color(.70f, 1, .92f);
                colors.pressedColor = new Color(.53f, .85f, .75f);
                colors.disabledColor = new Color(.42f, .52f, .55f, .52f);
                button.colors = colors;
            }
        }

        static GameObject PanelAt(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = true;
            return go;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size,
                          int pixels, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = size; rect.anchoredPosition = position;
            var label = go.GetComponent<Text>(); label.font = font; label.text = value;
            label.fontSize = pixels; label.color = color; label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        static Button ButtonAt(Transform parent, string name, string value, Vector2 position, Vector2 size,
                               Color color, int fontSize, Color? textColor = null)
        {
            var go = PanelAt(parent, name, new Vector2(.5f, .5f), position, size, color);
            var button = go.AddComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
            var colors = button.colors; colors.highlightedColor = new Color(.68f, 1, .92f);
            colors.pressedColor = new Color(.46f, .80f, .72f); button.colors = colors;
            Label(go.transform, "Label", value, Vector2.zero, size - new Vector2(14, 8), fontSize, textColor ?? White);
            return button;
        }

        static InputField Field(Transform parent, string name, Vector2 position, Vector2 size,
                                string hint, int limit, InputField.ContentType type = InputField.ContentType.Standard)
        {
            Label(parent, name + " label", name.ToUpperInvariant(), position + new Vector2(0, 43),
                  new Vector2(size.x, 30), 14, Mint, TextAnchor.MiddleLeft);
            var go = PanelAt(parent, name + " field", new Vector2(.5f, .5f), position, size, Card);
            var input = go.AddComponent<InputField>();
            input.targetGraphic = go.GetComponent<Image>(); input.characterLimit = limit; input.contentType = type;
            var value = Label(go.transform, "Value", "", Vector2.zero, size - new Vector2(25, 6), 18, White, TextAnchor.MiddleLeft);
            var placeholder = Label(go.transform, "Placeholder", hint, Vector2.zero, size - new Vector2(25, 6), 17, Muted, TextAnchor.MiddleLeft);
            input.textComponent = value; input.placeholder = placeholder;
            return input;
        }

        static Dropdown DropdownAt(Transform parent, string name, Vector2 position, Vector2 size, string[] options)
        {
            Label(parent, name + " label", name.ToUpperInvariant(), position + new Vector2(0, 43),
                  new Vector2(size.x, 30), 14, Mint, TextAnchor.MiddleLeft);
            var go = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            go.name = name; go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size;
            var dropdown = go.GetComponent<Dropdown>(); dropdown.ClearOptions(); dropdown.AddOptions(options.ToList());
            go.GetComponent<Image>().color = Card;
            foreach (var text in go.GetComponentsInChildren<Text>(true))
            { text.font = font; text.color = White; text.fontSize = 16; }
            return dropdown;
        }
    }
}
