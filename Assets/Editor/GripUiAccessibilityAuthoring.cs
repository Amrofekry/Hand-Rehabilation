using System;
using GripRehab;
using GripRehab.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GripRehab.Editor
{
    public static class GripUiAccessibilityAuthoring
    {
        [MenuItem("Grip Rehab/UI/Install Accessibility Controls")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new Exception("Author outside Play");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GripRehab.unity") throw new Exception("Open the main scene first");
            var theme = UnityEngine.Object.FindAnyObjectByType<GripUiThemeController>();
            var shell = UnityEngine.Object.FindAnyObjectByType<RehabAppShell>();
            if (theme == null || shell == null || theme.appearancePanel == null) throw new Exception("Existing theme controls required");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) throw new Exception("Import TMP essentials before installing controls");
            foreach (var palette in new[] { theme.dark, theme.lightTheme, theme.highContrast })
            { palette.displayFont = font; EditorUtility.SetDirty(palette); }
            // Convert the default from light cards on a navy backdrop to a full
            // dark palette. Existing semantic foreground bindings remain valid.
            theme.dark.surface = Hex("202F43"); theme.dark.textOnLight = Hex("FFFFFF");
            theme.dark.mutedOnLight = Hex("C2D2E6");
            theme.highContrast.surface = Color.black; theme.highContrast.textOnLight = Color.white;
            theme.highContrast.mutedOnLight = Color.white;
            var panel = (RectTransform)theme.appearancePanel.transform;
            var actions = shell.homePanel.transform.Find("Patient actions");
            if (actions == null) throw new Exception("Existing patient actions missing");
            var actionGroup = actions.GetComponent<CanvasGroup>();
            if (actionGroup == null) actionGroup = Undo.AddComponent<CanvasGroup>(actions.gameObject);
            if (actionGroup == null) throw new Exception("Cannot create the actions input group");
            actionGroup.interactable = true; actionGroup.blocksRaycasts = true;
            theme.homeActions = actionGroup; EditorUtility.SetDirty(actionGroup);
            var scrimRect = Rect(shell.homePanel.transform, "Appearance scrim", Vector2.zero, Vector2.zero, typeof(UnityEngine.UI.Image));
            scrimRect.anchorMin = Vector2.zero; scrimRect.anchorMax = Vector2.one;
            scrimRect.offsetMin = scrimRect.offsetMax = Vector2.zero;
            theme.appearanceScrim = scrimRect.GetComponent<UnityEngine.UI.Image>();
            theme.appearanceScrim.color = new Color(0, 0, 0, .8f); theme.appearanceScrim.enabled = false;
            theme.appearanceScrim.raycastTarget = true;
            var scrimTag = scrimRect.GetComponent<GripUiRoleTag>() ?? scrimRect.gameObject.AddComponent<GripUiRoleTag>();
            scrimTag.excludeFromTheme = true;
            scrimRect.SetAsLastSibling(); panel.SetAsLastSibling();
            panel.anchoredPosition = Vector2.zero; panel.sizeDelta = new Vector2(1080, 760);
            Move(panel, "Heading", new Vector2(0, 306), new Vector2(980, 64));
            Move(panel, "Description", new Vector2(0, 250), new Vector2(980, 42));
            Move(panel, "Dark theme", new Vector2(-255, 152), new Vector2(440, 68));
            Move(panel, "Light theme", new Vector2(-255, 66), new Vector2(440, 68));
            Move(panel, "High contrast theme", new Vector2(-255, -20), new Vector2(440, 68));
            Move(panel, "Current theme", new Vector2(-255, -110), new Vector2(440, 40));
            Move(panel, "Close appearance", new Vector2(0, -304), new Vector2(960, 68));
            var settings = theme.GetComponent<GripUiAccessibility>() ?? theme.gameObject.AddComponent<GripUiAccessibility>();
            settings.theme = theme;
            var bridge = shell.GetComponent<GripDwellSettingsAdapter>() ?? shell.gameObject.AddComponent<GripDwellSettingsAdapter>();
            bridge.shell = shell; settings.dwellAdapter = bridge;
            settings.motionButton = Button(panel, "Reduce motion", "REDUCE MOTION: OFF", new Vector2(255, 152), theme, font, out settings.motionLabel);
            settings.transparencyButton = Button(panel, "Solid panels", "SOLID PANELS: OFF", new Vector2(255, 66), theme, font, out settings.transparencyLabel);
            settings.textButton = Button(panel, "Text size", "TEXT SIZE: 100% (FIT)", new Vector2(255, -20), theme, font, out settings.textLabel);
            settings.dwellLabel = Label(panel, "Hand pointer hold", "HAND POINTER HOLD: 5.0s", new Vector2(255, -102), new Vector2(440, 32), theme, font);
            settings.dwellSlider = Slider(panel, "Dwell time", new Vector2(255, -154), 2, 8, 5, theme);
            settings.shorterButton = Button(panel, "Shorter dwell", "-", new Vector2(75, -154), theme, font, out var shorterLabel);
            settings.longerButton = Button(panel, "Longer dwell", "+", new Vector2(435, -154), theme, font, out var longerLabel);
            ResizeButton(settings.shorterButton, shorterLabel, 64);
            ResizeButton(settings.longerButton, longerLabel, 64);
            Label(panel, "UI sound label", "UI SOUND VOLUME", new Vector2(-255, -184), new Vector2(440, 30), theme, font);
            settings.volumeSlider = Slider(panel, "UI volume", new Vector2(-255, -231), 0, 1, .15f, theme);
            settings.muteButton = Button(panel, "Mute UI", "SOUND", new Vector2(-435, -231), theme, font, out var muteLabel);
            ResizeButton(settings.muteButton, muteLabel, 88); muteLabel.fontSize = 13;
            Label(panel, "Text sizing note", "Larger text adapts to each control.\nMouse and keyboard remain available.",
                new Vector2(-255, 205), new Vector2(440, 32), theme, font).fontSize = 15;
            // Saved controls are inspected once at startup, never found in Update.
            settings.controls = theme.canvas.GetComponentsInChildren<UnityEngine.UI.Selectable>(true);
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(bridge); EditorUtility.SetDirty(theme);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(scene.path);
            Verify();
        }
        public static void Verify()
        {
            var a = UnityEngine.Object.FindAnyObjectByType<GripUiAccessibility>();
            if (a == null || a.theme == null || a.theme.homeActions == null || a.theme.appearanceScrim == null ||
                a.dwellAdapter == null || a.motionButton == null ||
                a.transparencyButton == null || a.textButton == null || a.dwellSlider == null ||
                a.volumeSlider == null || a.controls == null || a.controls.Length < 10)
                throw new Exception("Accessibility references missing");
            foreach (var theme in new[] { a.theme.dark, a.theme.lightTheme, a.theme.highContrast })
            {
                if (theme.displayFont == null) throw new Exception("TMP font missing");
                foreach (var pair in new[] { (theme.textOnLight, theme.surface), (theme.textOnDark, theme.darkSurface),
                    (theme.textOnPrimary, theme.primary), (theme.textOnSecondary, theme.secondary) })
                    if (Contrast(pair.Item1, pair.Item2) < 4.5f) throw new Exception("Theme body text contrast below AA");
            }
        }
        public static float Contrast(Color a, Color b)
        {
            float x = Luminance(a), y = Luminance(b);
            return (Mathf.Max(x, y) + .05f) / (Mathf.Min(x, y) + .05f);
        }
        static float Luminance(Color c) => .2126f * Channel(c.r) + .7152f * Channel(c.g) + .0722f * Channel(c.b);
        static float Channel(float c) => c <= .04045f ? c / 12.92f : Mathf.Pow((c + .055f) / 1.055f, 2.4f);
        static Color Hex(string c) { ColorUtility.TryParseHtmlString("#" + c, out var color); return color; }
        static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size, params Type[] components)
        {
            var found = parent.Find(name);
            GameObject go;
            if (found == null)
            { go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); }
            else go = found.gameObject;
            foreach (var type in components) if (go.GetComponent(type) == null) go.AddComponent(type);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.anchoredPosition = position; r.sizeDelta = size; return r;
        }
        static void Move(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var t = parent.Find(name); if (t == null) throw new Exception("Existing appearance control missing: " + name);
            var r = (RectTransform)t; r.anchoredPosition = pos; r.sizeDelta = size;
            var label = t.Find("Label") as RectTransform;
            if (label != null) label.sizeDelta = size - new Vector2(24, 12);
        }
        static void Role(UnityEngine.UI.Graphic graphic, GripUiRole role)
        {
            var tag = graphic.GetComponent<GripUiRoleTag>() ?? graphic.gameObject.AddComponent<GripUiRoleTag>();
            tag.role = role;
        }
        static TMP_Text Label(Transform parent, string name, string caption, Vector2 pos, Vector2 size,
            GripUiThemeController theme, TMP_FontAsset font)
        {
            var r = Rect(parent, name, pos, size, typeof(TextMeshProUGUI));
            var t = r.GetComponent<TextMeshProUGUI>(); t.font = font; t.fontSize = 20;
            t.text = caption; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            t.color = theme.dark.textOnDark; Role(t, GripUiRole.TextOnDark); return t;
        }
        static UnityEngine.UI.Button Button(Transform parent, string name, string caption, Vector2 pos,
            GripUiThemeController theme, TMP_FontAsset font, out TMP_Text label)
        {
            var r = Rect(parent, name, pos, new Vector2(440, 68), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            var image = r.GetComponent<UnityEngine.UI.Image>();
            image.sprite = theme.lightButton.image.sprite; image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = theme.dark.secondary; Role(image, GripUiRole.Secondary);
            var b = r.GetComponent<UnityEngine.UI.Button>(); b.targetGraphic = image;
            label = Label(r, "Label", caption, Vector2.zero, new Vector2(410, 54), theme, font);
            label.color = theme.dark.textOnSecondary; Role(label, GripUiRole.TextOnSecondary); return b;
        }
        static UnityEngine.UI.Slider Slider(Transform parent, string name, Vector2 pos, float min, float max,
            float value, GripUiThemeController theme)
        {
            var r = Rect(parent, name, pos, new Vector2(280, 64), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Slider));
            var hitArea = r.GetComponent<UnityEngine.UI.Image>(); hitArea.color = Color.clear; hitArea.raycastTarget = true;
            var track = Rect(r, "Track", Vector2.zero, new Vector2(264, 8), typeof(UnityEngine.UI.Image));
            var image = track.GetComponent<UnityEngine.UI.Image>(); image.color = theme.dark.secondary; Role(image, GripUiRole.Secondary);
            image.raycastTarget = false;
            var area = Rect(r, "Handle area", Vector2.zero, new Vector2(264, 48));
            var legacyHandle = r.Find("Handle");
            if (legacyHandle != null) legacyHandle.SetParent(area, false);
            var handle = Rect(area, "Handle", Vector2.zero, new Vector2(40, 0), typeof(UnityEngine.UI.Image));
            handle.GetComponent<UnityEngine.UI.Image>().sprite = theme.lightButton.image.sprite;
            handle.GetComponent<UnityEngine.UI.Image>().type = UnityEngine.UI.Image.Type.Sliced;
            var handleImage = handle.GetComponent<UnityEngine.UI.Image>(); handleImage.color = theme.dark.accent; Role(handleImage, GripUiRole.Accent);
            var slider = r.GetComponent<UnityEngine.UI.Slider>(); slider.minValue = min; slider.maxValue = max;
            slider.handleRect = handle; slider.targetGraphic = handleImage; slider.value = value;
            return slider;
        }
        static void ResizeButton(UnityEngine.UI.Button button, TMP_Text label, float width)
        { ((RectTransform)button.transform).sizeDelta = new Vector2(width, 64); label.rectTransform.sizeDelta = new Vector2(width - 12, 52); }
    }
}
