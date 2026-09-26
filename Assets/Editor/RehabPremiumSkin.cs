using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GripRehab.Editor
{
    // WBS 3.4 presentation pass. Existing buttons and their listeners survive intact.
    // This is an explicit authoring command, never part of Build or Verify.
    public static class RehabPremiumSkin
    {
        const string UiFolder = "Assets/Rehab/3.4-game-modules/UI";
        static readonly Color Ink = Hex("071820");
        static readonly Color Deep = Hex("102A32");
        static readonly Color Panel = Hex("14333B");
        static readonly Color PanelRaised = Hex("1B424A");
        static readonly Color Jade = Hex("36C9A4");
        static readonly Color Mint = Hex("A4F4D7");
        static readonly Color Ivory = Hex("F3F8F5");
        static readonly Color Pale = Hex("DDEBE7");
        static readonly Color Slate = Hex("526E70");
        static readonly Color White = Hex("F5FBFA");
        static Sprite rounded, backdrop;
        static Font font;

        [MenuItem("Grip Rehab/Apply Premium UI Design")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);
            var shell = UnityEngine.Object.FindAnyObjectByType<RehabAppShell>();
            var app = UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
            var choices = UnityEngine.Object.FindAnyObjectByType<ApartmentChoices>();
            if (shell == null || app == null || choices == null) throw new Exception("Saved app shell is required");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            PrepareSprites();
            SetImage(shell.root.gameObject, White, backdrop, Image.Type.Simple);
            Home(shell);
            Profile(shell);
            Modules(shell);
            Dashboards(shell);
            Hud(shell, app);
            Options(choices);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            ApartmentAuthoring.VerifyScene();
            Debug.Log("[REHAB_PREMIUM_UI] PASS: full menu, profile, modules, HUD and options restyled in saved scene");
        }

        static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var result);
            return result;
        }

        static void PrepareSprites()
        {
            if (!AssetDatabase.IsValidFolder(UiFolder)) AssetDatabase.CreateFolder("Assets/Rehab/3.4-game-modules", "UI");
            var roundedPath = UiFolder + "/Rounded96.png";
            if (!File.Exists(roundedPath))
            {
                var texture = new Texture2D(96, 96, TextureFormat.RGBA32, false);
                for (int y = 0; y < 96; y++) for (int x = 0; x < 96; x++)
                {
                    var px = Mathf.Abs(x + .5f - 48) - 25;
                    var py = Mathf.Abs(y + .5f - 48) - 25;
                    var distance = Mathf.Sqrt(Mathf.Max(px, 0) * Mathf.Max(px, 0) +
                                              Mathf.Max(py, 0) * Mathf.Max(py, 0)) + Mathf.Min(Mathf.Max(px, py), 0) - 21;
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(.5f - distance)));
                }
                texture.Apply(); File.WriteAllBytes(roundedPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(roundedPath);
            }
            var roundedImporter = (TextureImporter)AssetImporter.GetAtPath(roundedPath);
            roundedImporter.textureType = TextureImporterType.Sprite;
            roundedImporter.spriteImportMode = SpriteImportMode.Single;
            roundedImporter.spritePixelsPerUnit = 100;
            roundedImporter.spriteBorder = new Vector4(24, 24, 24, 24);
            roundedImporter.mipmapEnabled = false;
            roundedImporter.SaveAndReimport();
            rounded = AssetDatabase.LoadAssetAtPath<Sprite>(roundedPath);
            var backdropPath = UiFolder + "/MenuBackdrop.png";
            if (!File.Exists(backdropPath))
            {
                var texture = new Texture2D(512, 288, TextureFormat.RGBA32, false);
                for (int y = 0; y < 288; y++) for (int x = 0; x < 512; x++)
                {
                    var u = x / 511f; var v = y / 287f;
                    var color = Color.Lerp(Hex("06151D"), Hex("12333B"), .14f + .49f * u + .19f * v);
                    var glow = Mathf.Exp(-((u - .17f) * (u - .17f) * 16 + (v - .48f) * (v - .48f) * 8));
                    color = Color.Lerp(color, Hex("175B56"), glow * .22f);
                    if (x % 40 == 0 || y % 40 == 0) color = Color.Lerp(color, Mint, .025f);
                    color.a = 1; texture.SetPixel(x, y, color);
                }
                texture.Apply(); File.WriteAllBytes(backdropPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(backdropPath);
            }
            var backdropImporter = (TextureImporter)AssetImporter.GetAtPath(backdropPath);
            backdropImporter.textureType = TextureImporterType.Sprite;
            backdropImporter.spriteImportMode = SpriteImportMode.Single;
            backdropImporter.mipmapEnabled = false;
            backdropImporter.SaveAndReimport();
            backdrop = AssetDatabase.LoadAssetAtPath<Sprite>(backdropPath);
            if (rounded == null || backdrop == null) throw new Exception("Premium UI sprites failed to import");
        }

        static void SetImage(GameObject go, Color color, Sprite sprite = null, Image.Type type = Image.Type.Sliced)
        {
            var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            image.sprite = sprite == null ? rounded : sprite;
            image.type = type;
            image.color = color;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static Text ChildText(Transform root, string name)
        {
            var child = root.Find(name);
            if (child == null) throw new Exception("UI text not found: " + root.name + "/" + name);
            return child.GetComponent<Text>();
        }

        static void Type(Text label, string value, int size, Color color, TextAnchor alignment,
                         Vector2 position, Vector2 bounds)
        {
            label.font = font; label.text = value; label.fontSize = size; label.fontStyle = FontStyle.Normal;
            label.color = color; label.alignment = alignment; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            Place(label.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, bounds);
        }

        static void PaintButton(Button button, Color fill, Color textColor, int fontSize,
                                Vector2 position, Vector2 size, string caption = null)
        {
            Place(button.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, size);
            SetImage(button.gameObject, fill);
            var label = button.GetComponentInChildren<Text>();
            Type(label, caption ?? label.text, fontSize, textColor, TextAnchor.MiddleCenter,
                 Vector2.zero, size - new Vector2(16, 8));
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.86f, 1f, .96f, 1);
            colors.pressedColor = new Color(.75f, .91f, .86f, 1);
            colors.disabledColor = new Color(.72f, .78f, .78f, .46f);
            button.colors = colors;
        }

        static void Home(RehabAppShell s)
        {
            var hero = s.homePanel.transform.Find("Introduction");
            var actions = s.homePanel.transform.Find("Patient actions");
            Place((RectTransform)hero, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  new Vector2(-370, 0), new Vector2(735, 735));
            hero.GetComponent<Image>().color = Color.clear;
            Type(ChildText(hero, "Eyebrow"), "GRIP REHAB  /  INTERACTIVE HAND THERAPY", 17, Mint,
                 TextAnchor.MiddleLeft, new Vector2(-5, 295), new Vector2(665, 34));
            Type(ChildText(hero, "Title"), "MOVE WITH\nCONFIDENCE.", 69, White,
                 TextAnchor.MiddleLeft, new Vector2(-5, 154), new Vector2(675, 176));
            Type(ChildText(hero, "Statement"), "Make every movement count.", 27, Mint,
                 TextAnchor.MiddleLeft, new Vector2(-5, 34), new Vector2(665, 54));
            Type(ChildText(hero, "Description"),
                 "Explore familiar spaces, control your virtual hand, and practise grasping real-world objects at your own pace.",
                 19, Hex("B9D2D1"), TextAnchor.UpperLeft, new Vector2(-5, -60), new Vector2(620, 84));
            for (int i = 0; i < 5; i++)
            {
                var bar = hero.Find("Finger motion " + (i + 1)).GetComponent<Image>();
                var height = new[] { 82f, 108f, 132f, 111f, 91f }[i];
                Place(bar.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                      new Vector2(-259 + i * 78, -219 + (height - 82) * .5f), new Vector2(48, height));
                SetImage(bar.gameObject, i == 2 ? Jade : Hex("225A5C"));
            }
            Type(ChildText(hero, "Prototype note"), "01  /  FORCE + OBJECT AVAILABLE     •     02—03 PLANNED",
                 15, Hex("97B6B5"), TextAnchor.MiddleLeft, new Vector2(-5, -335), new Vector2(660, 29));

            Place((RectTransform)actions, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  new Vector2(435, 0), new Vector2(650, 735));
            SetImage(actions.gameObject, Ivory);
            AddShadow(actions.GetComponent<Image>(), new Color(0, 0, 0, .22f), new Vector2(0, -10));
            Type(ChildText(actions, "Welcome"), "Your training space", 34, Ink,
                 TextAnchor.MiddleLeft, new Vector2(-7, 302), new Vector2(550, 50));
            Type(s.homeGreeting, s.homeGreeting.text, 17, Slate,
                 TextAnchor.MiddleLeft, new Vector2(-7, 252), new Vector2(550, 40));
            PaintButton(s.startButton, Hex("167E6F"), White, 19, new Vector2(0, 176), new Vector2(550, 58), "START PRACTICE  →");
            PaintButton(s.continueButton, Pale, Ink, 18, new Vector2(0, 106), new Vector2(550, 56), "CONTINUE PRACTICE");
            PaintButton(s.newProfileButton, White, Hex("1C5B56"), 17, new Vector2(0, 38), new Vector2(550, 54), "CREATE NEW PROFILE");
            Type(ChildText(actions, "Saved profiles label"), "SAVED PROFILES", 14, Hex("268A78"),
                 TextAnchor.MiddleLeft, new Vector2(-7, -31), new Vector2(550, 28));
            StyleDropdown(s.profilePicker, new Vector2(-94, -80), new Vector2(358, 50));
            PaintButton(s.chooseProfileButton, Hex("27565D"), White, 15,
                        new Vector2(199, -80), new Vector2(156, 50));
            PaintButton(s.patientButton, Pale, Ink, 15, new Vector2(-139, -159), new Vector2(272, 54));
            PaintButton(s.doctorButton, Pale, Ink, 15, new Vector2(139, -159), new Vector2(272, 54));
            Type(ChildText(actions, "Privacy note"),
                 "LOCAL PROTOTYPE  ·  Your profile stays on this computer. No clinical diagnosis.",
                 14, Slate, TextAnchor.MiddleLeft, new Vector2(-7, -235), new Vector2(550, 48));
            PaintButton(s.quitButton, Hex("E7EFEB"), Hex("536C6C"), 15,
                        new Vector2(0, -305), new Vector2(550, 46), "EXIT APPLICATION");
        }

        static void Profile(RehabAppShell s)
        {
            var panel = s.profilePanel.transform.Find("Patient profile form");
            SetImage(panel.gameObject, Ivory);
            AddShadow(panel.GetComponent<Image>(), new Color(0, 0, 0, .2f), new Vector2(0, -9));
            Type(ChildText(panel, "Heading"), "CREATE YOUR PROFILE", 39, Ink,
                 TextAnchor.MiddleLeft, new Vector2(0, 323), new Vector2(1050, 57));
            Type(ChildText(panel, "Instruction"),
                 "A few details help tailor your practice. Required: name, age and hand selections.",
                 17, Slate, TextAnchor.MiddleLeft, new Vector2(0, 273), new Vector2(1050, 47));
            foreach (var field in new[] { s.nameField, s.ageField, s.concernField, s.goalField, s.painField })
            {
                SetImage(field.gameObject, White);
                field.textComponent.color = Ink;
                ((Text)field.placeholder).color = Hex("7A9593");
                var label = field.transform.parent.Find(field.transform.name.Replace(" field", " label"));
                if (label != null) label.GetComponent<Text>().color = Hex("2D6963");
            }
            foreach (var dropdown in new[] { s.genderPicker, s.affectedPicker, s.dominantPicker })
            {
                SetImage(dropdown.gameObject, White);
                dropdown.captionText.color = Ink;
                var label = dropdown.transform.parent.Find(dropdown.name + " label");
                if (label != null) label.GetComponent<Text>().color = Hex("2D6963");
                StyleDropdown(dropdown, dropdown.GetComponent<RectTransform>().anchoredPosition,
                              dropdown.GetComponent<RectTransform>().sizeDelta);
            }
            Type(s.formError, s.formError.text, 16, Hex("A26129"), TextAnchor.MiddleCenter,
                 new Vector2(0, -228), new Vector2(1060, 42));
            PaintButton(s.cancelProfileButton, Pale, Ink, 17,
                        new Vector2(-190, -305), new Vector2(220, 53));
            PaintButton(s.saveProfileButton, Hex("167E6F"), White, 18,
                        new Vector2(162, -305), new Vector2(440, 53));
        }

        static void Modules(RehabAppShell s)
        {
            Type(s.moduleGreeting, s.moduleGreeting.text, 37, White, TextAnchor.MiddleLeft,
                 new Vector2(-18, 311), new Vector2(1330, 58));
            Type(ChildText(s.modulePanel.transform, "Module instruction"),
                 "Choose a training pathway. Force + Object is ready to practise today.",
                 19, Hex("A8C7C5"), TextAnchor.MiddleLeft, new Vector2(-18, 262), new Vector2(1320, 38));
            var cards = new[] { s.modulePanel.transform.Find("Module 01"),
                                s.modulePanel.transform.Find("Module 02"),
                                s.modulePanel.transform.Find("Module 03") };
            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                SetImage(card.gameObject, i == 0 ? Hex("EAF5F0") : Panel);
                AddShadow(card.GetComponent<Image>(), new Color(0, 0, 0, .18f), new Vector2(0, -6));
                var dark = i == 0;
                Type(ChildText(card, "Number"), "0" + (i + 1), 64, dark ? Hex("167E6F") : Jade,
                     TextAnchor.MiddleLeft, new Vector2(0, 137), new Vector2(330, 85));
                Type(ChildText(card, "Name"), new[] { "FORCE + OBJECT", "COGNITIVE", "VISUOMOTOR" }[i],
                     26, dark ? Ink : White, TextAnchor.MiddleLeft, new Vector2(0, 42), new Vector2(345, 50));
                var description = ChildText(card, "Description");
                Type(description, description.text, 18, dark ? Slate : Hex("B1CCCC"),
                     TextAnchor.UpperLeft, new Vector2(0, -49), new Vector2(345, 113));
            }
            PaintButton(s.openModuleButton, Hex("167E6F"), White, 18,
                        new Vector2(0, -142), new Vector2(330, 55), "START MODULE 01  →");
            PaintButton(s.cognitiveLockedButton, Hex("295158"), Hex("B4CCC9"), 16,
                        new Vector2(0, -142), new Vector2(330, 55), "COMING IN GP2");
            PaintButton(s.visuomotorLockedButton, Hex("295158"), Hex("B4CCC9"), 16,
                        new Vector2(0, -142), new Vector2(330, 55), "COMING IN GP2");
            Type(ChildText(s.modulePanel.transform, "Future pipeline"),
                 "COMING LATER  /  BASELINE ASSESSMENT  ·  FATIGUE ENGINE  ·  PATIENT RESULTS  ·  DOCTOR HISTORY",
                 15, Hex("A8C7C5"), TextAnchor.MiddleCenter,
                 new Vector2(0, -286), new Vector2(1390, 40));
            PaintButton(s.homeFromModulesButton, PanelRaised, White, 16,
                        new Vector2(0, -353), new Vector2(230, 47));
        }

        static void Dashboards(RehabAppShell s)
        {
            foreach (var panel in new[] { s.patientPanel.transform.Find("Patient results layout"),
                                         s.doctorPanel.transform.Find("Doctor roadmap layout") })
            {
                SetImage(panel.gameObject, Ivory);
                Type(ChildText(panel, "Title"), panel == s.patientPanel.transform.Find("Patient results layout") ?
                    "PATIENT DASHBOARD" : "DOCTOR DASHBOARD / PREVIEW", 37, Ink,
                    TextAnchor.MiddleLeft, new Vector2(0, 280), new Vector2(1020, 60));
            }
            Type(s.patientSummary, s.patientSummary.text, 22, Hex("244247"), TextAnchor.UpperLeft,
                 new Vector2(0, 48), new Vector2(975, 370));
            Type(s.doctorSummary, s.doctorSummary.text, 23, Hex("48666A"), TextAnchor.UpperLeft,
                 new Vector2(0, 27), new Vector2(975, 370));
            PaintButton(s.homeFromPatientButton, Hex("167E6F"), White, 17,
                        new Vector2(0, -285), new Vector2(250, 50));
            PaintButton(s.homeFromDoctorButton, Hex("167E6F"), White, 17,
                        new Vector2(0, -285), new Vector2(250, 50));
        }

        static void StyleDropdown(Dropdown dropdown, Vector2 position, Vector2 size)
        {
            Place(dropdown.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, size);
            SetImage(dropdown.gameObject, White);
            dropdown.captionText.color = Ink;
            dropdown.captionText.font = font;
            var arrow = dropdown.transform.Find("Arrow/Chevron");
            if (arrow != null) arrow.GetComponent<Text>().color = Hex("167E6F");
            if (dropdown.template != null)
            {
                var templateImage = dropdown.template.GetComponent<Image>();
                if (templateImage != null) templateImage.color = Ivory;
                foreach (var text in dropdown.template.GetComponentsInChildren<Text>(true))
                    text.color = Ink;
            }
        }

        static void AddShadow(Graphic graphic, Color color, Vector2 distance)
        {
            var shadow = graphic.GetComponent<Shadow>() ?? graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color; shadow.effectDistance = distance;
        }

        static void Hud(RehabAppShell shell, ApartmentExperience app)
        {
            // Legacy text stays serialized for compatibility and automated checks, but the
            // patient-facing HUD reads the same live model through RehabAppShell.
            foreach (var text in new[] { app.statusText, app.forceText, app.contextText })
            {
                text.color = Color.clear;
                var background = text.transform.parent.GetComponent<Image>();
                if (background != null) { background.color = Color.clear; background.raycastTarget = false; }
            }
            app.progressFill.color = Color.clear;
            var canvas = shell.root.GetComponentInParent<Canvas>();
            var chrome = shell.telemetry.transform.parent;
            Place((RectTransform)chrome, new Vector2(.5f, 1), new Vector2(.5f, 1),
                  new Vector2(0, -17), new Vector2(1540, 47));
            SetImage(chrome.gameObject, new Color(Deep.r, Deep.g, Deep.b, .95f));
            Type(shell.telemetry, shell.telemetry.text, 15, Hex("BCE9D9"), TextAnchor.MiddleLeft,
                 new Vector2(-333, -23), new Vector2(800, 32));
            PaintButton(shell.gameplayHomeButton, PanelRaised, White, 14,
                        new Vector2(676, -23), new Vector2(150, 34), "MAIN MENU");

            var root = canvas.transform.Find("Premium session HUD");
            if (root == null)
            {
                var go = new GameObject("Premium session HUD", typeof(RectTransform), typeof(CanvasGroup));
                go.transform.SetParent(canvas.transform, false);
                root = go.transform;
            }
            var rect = (RectTransform)root;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var group = root.GetComponent<CanvasGroup>();
            if (!shell.gameplayLayers.Contains(group))
                shell.gameplayLayers = shell.gameplayLayers.Concat(new[] { group }).ToArray();

            var place = Card(root, "Station identity", new Vector2(0, 1), new Vector2(0, 1),
                             new Vector2(26, -89), new Vector2(366, 112), new Color(Deep.r, Deep.g, Deep.b, .94f));
            Label(place, "Overline", "MODULE 01  /  ACTIVE SESSION", 14, Jade,
                  TextAnchor.MiddleLeft, new Vector2(-11, 32), new Vector2(320, 25));
            shell.hudRoom = Label(place, "Room", "Living room station", 26, White,
                  TextAnchor.MiddleLeft, new Vector2(-11, 0), new Vector2(320, 36));
            shell.hudHand = Label(place, "Hand", "RIGHT HAND  ·  MODULE 01", 14, Hex("B1C9C9"),
                  TextAnchor.MiddleLeft, new Vector2(-11, -35), new Vector2(320, 26));

            var metric = Card(root, "Live input card", new Vector2(1, 1), new Vector2(1, 1),
                              new Vector2(-26, -89), new Vector2(354, 199), new Color(Deep.r, Deep.g, Deep.b, .96f));
            shell.hudSource = Label(metric, "Input", "KEYBOARD DEMO", 14, Jade,
                  TextAnchor.MiddleLeft, new Vector2(-2, 73), new Vector2(308, 28));
            shell.hudValue = Label(metric, "Value", "--", 57, White,
                  TextAnchor.MiddleLeft, new Vector2(-70, 19), new Vector2(177, 78));
            shell.hudUnit = Label(metric, "Unit", "newtons", 19, Hex("B1C9C9"),
                  TextAnchor.MiddleLeft, new Vector2(94, 12), new Vector2(117, 35));
            shell.hudProgress = Label(metric, "Progress label", "CONTROLLED HOLD  0.0 / 3.0 S", 13,
                  Hex("B1C9C9"), TextAnchor.MiddleLeft, new Vector2(-2, -46), new Vector2(309, 24));
            var track = Card(metric, "Progress track", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                             new Vector2(0, -70), new Vector2(309, 8), Hex("315259"));
            shell.hudProgressFill = Card(track, "Live progress", new Vector2(0, .5f), new Vector2(0, .5f),
                                         Vector2.zero, new Vector2(309, 8), Jade).GetComponent<Image>();
            shell.hudProgressFill.type = Image.Type.Filled;
            shell.hudProgressFill.fillMethod = Image.FillMethod.Horizontal;
            shell.hudProgressFill.fillOrigin = 0;

            var context = Card(root, "Context guidance", new Vector2(0, 0), new Vector2(0, 0),
                               new Vector2(26, 25), new Vector2(564, 126), new Color(Deep.r, Deep.g, Deep.b, .95f));
            shell.hudObject = Label(context, "Object", "OBJECT  /  SILICONE BALL", 15, Jade,
                  TextAnchor.MiddleLeft, new Vector2(-9, 37), new Vector2(515, 27));
            shell.hudPrompt = Label(context, "Prompt", "Aim at the object and press E.", 20, White,
                  TextAnchor.MiddleLeft, new Vector2(-9, -13), new Vector2(515, 66));

            var controls = Card(root, "Keyboard quick guide", new Vector2(1, 0), new Vector2(1, 0),
                                new Vector2(-26, 25), new Vector2(440, 126), new Color(Deep.r, Deep.g, Deep.b, .95f));
            Label(controls, "Shortcuts title", "QUICK CONTROLS", 14, Jade,
                  TextAnchor.MiddleLeft, new Vector2(-6, 38), new Vector2(395, 23));
            Label(controls, "Shortcuts", "WASD + MOUSE   MOVE / LOOK\nE   GRAB   G   SUMMON   Q   RELEASE\nF   SIT   TAB   OPTIONS   B   INPUT", 16,
                  White, TextAnchor.MiddleLeft, new Vector2(-6, -16), new Vector2(395, 79));
        }

        static Transform Card(Transform parent, string name, Vector2 anchor, Vector2 pivot,
                              Vector2 position, Vector2 size, Color fill)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false); child = go.transform;
            }
            Place((RectTransform)child, anchor, pivot, position, size);
            SetImage(child.gameObject, fill);
            child.GetComponent<Image>().raycastTarget = false;
            return child;
        }

        static Text Label(Transform parent, string name, string value, int size, Color color,
                          TextAnchor alignment, Vector2 position, Vector2 bounds)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Text));
                go.transform.SetParent(parent, false); child = go.transform;
            }
            var text = child.GetComponent<Text>();
            Type(text, value, size, color, alignment, position, bounds);
            return text;
        }

        static void Options(ApartmentChoices choices)
        {
            var root = choices.controls.transform;
            Place((RectTransform)root, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  Vector2.zero, new Vector2(612, 442));
            SetImage(root.gameObject, new Color(Deep.r, Deep.g, Deep.b, .985f));
            AddShadow(root.GetComponent<Image>(), new Color(0, 0, 0, .38f), new Vector2(0, -12));
            Type(choices.heading, "SESSION OPTIONS", 28, White, TextAnchor.MiddleLeft,
                 new Vector2(0, 171), new Vector2(536, 52));
            Label(root, "Room label", "01  /  SELECT ROOM", 14, Mint, TextAnchor.MiddleLeft,
                  new Vector2(0, 124), new Vector2(536, 28));
            Label(root, "Object label", "02  /  SELECT OBJECT", 14, Mint, TextAnchor.MiddleLeft,
                  new Vector2(0, 25), new Vector2(536, 28));
            Label(root, "Session label", "03  /  INPUT AND HAND", 14, Mint, TextAnchor.MiddleLeft,
                  new Vector2(0, -81), new Vector2(536, 28));
            StyleOption(choices.livingButton, -137, 78, 262, 47);
            StyleOption(choices.kitchenButton, 137, 78, 262, 47);
            for (int i = 0; i < choices.objectButtons.Length; i++)
                StyleOption(choices.objectButtons[i], -183 + i * 183, -29, 171, 58);
            StyleOption(choices.inputButton, -137, -122, 262, 50);
            StyleOption(choices.handButton, 137, -122, 262, 50);
            StyleOption(choices.resumeButton, 0, -185, 536, 55);
            choices.resumeButton.image.color = Hex("167E6F");
            choices.heading.text = "SESSION OPTIONS";
        }

        static void StyleOption(Button button, float x, float centerY, float width, float height)
        {
            var rect = button.GetComponent<RectTransform>();
            Place(rect, new Vector2(.5f, 1), new Vector2(.5f, .5f),
                  new Vector2(x, centerY - 221), new Vector2(width, height));
            SetImage(button.gameObject, PanelRaised);
            var text = button.GetComponentInChildren<Text>();
            Type(text, text.text, 16, White, TextAnchor.MiddleCenter,
                 new Vector2(0, -height * .5f), new Vector2(width - 18, height - 6));
            // Button text uses top-center anchors inherited from the legacy scene.
            Place(text.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, .5f),
                  new Vector2(0, -height * .5f), new Vector2(width - 18, height - 6));
        }
    }
}
