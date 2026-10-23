using System;
using GripRehab;
using GripRehab.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GripRehab.Editor
{
    // Explicit one-shot authoring. Build and Verify never regenerate this UI.
    public static class GripUiThemeAuthoring
    {
        const string ScenePath = "Assets/Scenes/GripRehab.unity";
        const string ThemeFolder = "Assets/UI/Themes";

        [MenuItem("Grip Rehab/UI/Install Theme Controls")]
        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var shell = UnityEngine.Object.FindAnyObjectByType<RehabAppShell>();
            if (shell == null || shell.root == null || shell.homePanel == null)
                throw new Exception("Saved UI shell is required");
            if (!AssetDatabase.IsValidFolder(ThemeFolder))
                AssetDatabase.CreateFolder("Assets/UI", "Themes");
            var dark = Theme("Dark", "1A2230", "202F43", "1A2230", "00F0FF", "2B415C", "00F0FF",
                "FFFFFF", "FFFFFF", "112030", "FFFFFF", "C2D2E6", "C2D2E6");
            var light = Theme("Light", "E8F3FA", "FFFFFF", "F2F7FC", "007783", "DDEAF5", "006976",
                "1A2230", "1A2230", "FFFFFF", "1A2230", "42576B", "42576B");
            var contrast = Theme("HighContrast", "000000", "000000", "000000", "FFFF00", "FFFFFF", "FFFF00",
                "FFFFFF", "FFFFFF", "000000", "000000", "FFFFFF", "FFFFFF");
            var controller = shell.root.GetComponent<GripUiThemeController>() ??
                             shell.root.gameObject.AddComponent<GripUiThemeController>();
            controller.canvas = shell.root.GetComponentInParent<Canvas>();
            controller.dark = dark; controller.lightTheme = light; controller.highContrast = contrast;
            controller.selectedTheme = 0;
            var actions = shell.homePanel.transform.Find("Patient actions");
            if (actions == null) throw new Exception("Saved home actions panel is missing");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Rehab/3.4-game-modules/UI/Rounded96.png");
            if (sprite == null) throw new Exception("Saved rounded UI sprite is missing");
            var title = actions.Find("Welcome")?.GetComponent<RectTransform>();
            if (title != null)
            { title.sizeDelta = new Vector2(350, title.sizeDelta.y); title.anchoredPosition = new Vector2(-76, 302); }
            controller.appearanceButton = Button(actions, "Appearance", "APPEARANCE",
                new Vector2(208, 302), new Vector2(142, 64), sprite, Hex("DDEBE7"), Hex("071820"), 14);
            var panel = shell.homePanel.transform.Find("Appearance settings");
            if (panel == null)
            {
                var go = new GameObject("Appearance settings", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                go.transform.SetParent(shell.homePanel.transform, false);
                panel = go.transform;
            }
            var rect = (RectTransform)panel;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(435, 0);
            rect.sizeDelta = new Vector2(650, 735);
            var image = panel.GetComponent<Image>();
            image.sprite = sprite; image.type = Image.Type.Sliced; image.color = Hex("102A32");
            controller.appearancePanel = panel.GetComponent<CanvasGroup>();
            controller.appearancePanel.alpha = 0;
            controller.appearancePanel.interactable = false;
            controller.appearancePanel.blocksRaycasts = false;
            Label(panel, "Heading", "APPEARANCE", new Vector2(0, 269), new Vector2(550, 60), 32, Hex("F5FBFA"));
            Label(panel, "Description", "Choose a comfortable display. This changes visuals only.",
                new Vector2(0, 208), new Vector2(550, 48), 17, Hex("B9D2D1"));
            controller.darkButton = Button(panel, "Dark theme", "DARK  /  Calm navy",
                new Vector2(0, 112), new Vector2(550, 68), sprite, Hex("167E6F"), Color.white, 20);
            controller.lightButton = Button(panel, "Light theme", "LIGHT  /  Clear daylight",
                new Vector2(0, 28), new Vector2(550, 68), sprite, Hex("DDEBE7"), Hex("071820"), 20);
            controller.contrastButton = Button(panel, "High contrast theme", "HIGH CONTRAST  /  Bold edges",
                new Vector2(0, -56), new Vector2(550, 68), sprite, Hex("DDEBE7"), Hex("071820"), 20);
            controller.activeThemeLabel = Label(panel, "Current theme", "DARK",
                new Vector2(0, -143), new Vector2(550, 36), 16, Hex("B9D2D1"));
            controller.closeButton = Button(panel, "Close appearance", "BACK TO HOME",
                new Vector2(0, -244), new Vector2(550, 64), sprite, Hex("DDEBE7"), Hex("071820"), 19);
            panel.SetAsLastSibling();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[UI_THEME] PASS: saved three themes and home appearance controls");
        }

        static GripUiTheme Theme(string name, string backdrop, string surface, string darkSurface,
            string primary, string secondary, string accent, string textOnDark, string textOnLight,
            string textOnPrimary, string textOnSecondary, string mutedOnDark, string mutedOnLight)
        {
            string path = ThemeFolder + "/" + name + ".asset";
            var theme = AssetDatabase.LoadAssetAtPath<GripUiTheme>(path);
            if (theme == null)
            { theme = ScriptableObject.CreateInstance<GripUiTheme>(); AssetDatabase.CreateAsset(theme, path); }
            theme.backdrop = Hex(backdrop); theme.surface = Hex(surface);
            theme.darkSurface = Hex(darkSurface); theme.primary = Hex(primary);
            theme.secondary = Hex(secondary); theme.accent = Hex(accent);
            theme.textOnDark = Hex(textOnDark); theme.textOnLight = Hex(textOnLight);
            theme.textOnPrimary = Hex(textOnPrimary); theme.textOnSecondary = Hex(textOnSecondary);
            theme.mutedOnDark = Hex(mutedOnDark); theme.mutedOnLight = Hex(mutedOnLight);
            theme.danger = Hex(name == "Light" ? "AD243B" : name == "HighContrast" ? "FFAAAA" : "FF9AA7");
            theme.warning = Hex(name == "Light" ? "7C4C00" : name == "HighContrast" ? "FFFF00" : "F9D67E");
            theme.success = Hex(name == "Light" ? "076850" : name == "HighContrast" ? "AAFFAA" : "77E3BE");
            theme.latinFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/HandMate/Fonts/HandMate-Montserrat-Regular.ttf");
            theme.headingFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/HandMate/Fonts/Orbitron-VariableFont_wght.ttf");
            theme.backdropArtworkTint = Hex("586A80");
            EditorUtility.SetDirty(theme);
            return theme;
        }

        static Color Hex(string code)
        { ColorUtility.TryParseHtmlString("#" + code, out var color); return color; }

        static Button Button(Transform parent, string name, string caption, Vector2 position,
            Vector2 size, Sprite sprite, Color fill, Color textColor, int fontSize)
        {
            var found = parent.Find(name);
            var go = found == null ? new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)) : found.gameObject;
            if (found == null) go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = go.GetComponent<Image>(); image.sprite = sprite;
            image.type = Image.Type.Sliced; image.color = fill;
            var button = go.GetComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = Hex("E2FFF5"); colors.pressedColor = Hex("BDD7CF");
            colors.selectedColor = Hex("D4FFEF"); button.colors = colors;
            Label(go.transform, "Label", caption, Vector2.zero, size - new Vector2(20, 8), fontSize, textColor);
            return button;
        }

        static Text Label(Transform parent, string name, string value, Vector2 position,
            Vector2 size, int fontSize, Color color)
        {
            var found = parent.Find(name);
            var go = found == null ? new GameObject(name, typeof(RectTransform), typeof(Text)) : found.gameObject;
            if (found == null) go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize; text.color = color; text.alignment = TextAnchor.MiddleCenter;
            text.text = value; text.raycastTarget = false;
            return text;
        }
    }
}
