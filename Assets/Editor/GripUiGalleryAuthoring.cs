using System;
using System.IO;
using GripRehab.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GripRehab.Editor
{
    public static class GripUiGalleryAuthoring
    {
        const string ScenePath = "Assets/UI/GripUiGallery.unity";
        const string Prefabs = "Assets/UI/Components";
        static GripUiTheme palette;
        static Sprite rounded;
        [MenuItem("Grip Rehab/UI/Create Gallery And Ambient Background")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new Exception("Author outside Play");
            var main = EditorSceneManager.OpenScene("Assets/Scenes/GripRehab.unity");
            var owner = UnityEngine.Object.FindAnyObjectByType<GripUiThemeController>();
            if (owner == null || owner.appearanceScrim == null) throw new Exception("Saved appearance references required");
            rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Rehab/3.4-game-modules/UI/Rounded96.png");
            palette = owner.dark;
            var light = owner.lightTheme; var contrast = owner.highContrast;
            var radial = Radial();
            var home = owner.appearanceScrim.transform.parent;
            InstallBackground(home, owner, radial);
            EditorSceneManager.MarkSceneDirty(main); EditorSceneManager.SaveScene(main);
            // Build/Verify do not call this authoring operation. An existing gallery
            // is preserved so later edits to the sandbox are never regenerated.
            if (File.Exists(ScenePath))
            {
                var saved = EditorSceneManager.OpenScene(ScenePath);
                var existingTheme = UnityEngine.Object.FindAnyObjectByType<GripUiThemeController>();
                var existingBackground = UnityEngine.Object.FindAnyObjectByType<GripUiAmbientBackground>();
                if (existingTheme == null || existingBackground == null) throw new Exception("Gallery references require review");
                InstallBackground(existingBackground.transform.parent, existingTheme, radial);
                EditorSceneManager.MarkSceneDirty(saved); EditorSceneManager.SaveScene(saved);
                EditorSceneManager.OpenScene("Assets/Scenes/GripRehab.unity"); Verify(); return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Gallery camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = palette.backdrop;
            var canvasObject = new GameObject("UI Gallery", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(GripUiThemeController), typeof(GripUiGallery));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var theme = canvasObject.GetComponent<GripUiThemeController>(); theme.canvas = canvas;
            theme.dark = palette; theme.lightTheme = light; theme.highContrast = contrast;
            var gallery = canvasObject.GetComponent<GripUiGallery>(); gallery.theme = theme;
            var background = Box(canvas.transform, "Gallery backdrop", GripUiRole.Backdrop);
            background.GetComponent<UnityEngine.UI.Image>().sprite = null;
            Stretch(background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            InstallBackground(background, theme, radial);
            var title = Label(canvas.transform, "Heading", "GRIPREHAB  /  UI GALLERY", GripUiRole.TextOnDark, 38);
            Stretch(title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(32, -80), new Vector2(-32, -24));
            var nav = Rect(canvas.transform, "Theme navigation");
            Stretch(nav, new Vector2(0, 1), new Vector2(1, 1), new Vector2(32, -180), new Vector2(-32, -96));
            theme.darkButton = Button(nav, "Dark", "DARK", GripUiRole.Secondary);
            Place(theme.darkButton.transform, new Vector2(-330, 0), new Vector2(200, 80));
            theme.lightButton = Button(nav, "Light", "LIGHT", GripUiRole.Secondary);
            Place(theme.lightButton.transform, new Vector2(-110, 0), new Vector2(200, 80));
            theme.contrastButton = Button(nav, "Contrast", "HIGH CONTRAST", GripUiRole.Secondary);
            Place(theme.contrastButton.transform, new Vector2(110, 0), new Vector2(200, 80));
            gallery.motion = Button(nav, "Motion", "MOTION: ON", GripUiRole.Secondary);
            Place(gallery.motion.transform, new Vector2(330, 0), new Vector2(200, 80));
            gallery.motionLabel = gallery.motion.GetComponentInChildren<TMP_Text>();
            var left = Box(canvas.transform, "Controls card", GripUiRole.Surface);
            Stretch(left, Vector2.zero, new Vector2(.5f, 1), new Vector2(32, 104), new Vector2(-16, -204));
            var right = Box(canvas.transform, "Feedback card", GripUiRole.Surface);
            Stretch(right, new Vector2(.5f, 0), Vector2.one, new Vector2(16, 104), new Vector2(-32, -204));
            var leftTitle = Label(left, "Card title", "ACTIONS & INPUT", GripUiRole.TextOnLight, 28);
            Row(leftTitle, 24, 48);
            gallery.primary = Button(left, "PrimaryButton", "PRIMARY ACTION", GripUiRole.Primary); Row(gallery.primary.transform, 88, 80);
            gallery.reset = Button(left, "SecondaryButton", "RESET PREVIEW", GripUiRole.Secondary); Row(gallery.reset.transform, 184, 80);
            var disabled = Button(left, "DisabledButton", "UNAVAILABLE  /  PREVIEW", GripUiRole.Secondary);
            disabled.interactable = false; Row(disabled.transform, 280, 80);
            gallery.input = Input(left); Row(gallery.input.transform, 376, 80);
            var rightTitle = Label(right, "Card title", "FEEDBACK & STATES", GripUiRole.TextOnLight, 28); Row(rightTitle, 24, 48);
            var chip = Box(right, "ConnectionChip", GripUiRole.Secondary); Row(chip, 88, 56);
            var chipLabel = Label(chip, "Label", "DEMO ONLY  /  not a device status", GripUiRole.TextOnSecondary, 21);
            Stretch(chipLabel, Vector2.zero, Vector2.one, new Vector2(16, 4), new Vector2(-16, -4));
            gallery.sliderLabel = Label(right, "Slider value", "PREVIEW VALUE  50%", GripUiRole.TextOnLight, 24).GetComponent<TMP_Text>(); Row(gallery.sliderLabel.transform, 164, 40);
            gallery.slider = Slider(right); Row(gallery.slider.transform, 216, 80);
            gallery.toggle = Toggle(right, out gallery.toggleLabel); Row(gallery.toggle.transform, 312, 80);
            var bar = Box(right, "ProgressBar", GripUiRole.Secondary); Row(bar, 408, 24);
            var fill = Box(bar, "Fill", GripUiRole.Accent); Stretch(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            gallery.progress = fill.GetComponent<UnityEngine.UI.Image>(); gallery.progress.type = UnityEngine.UI.Image.Type.Filled;
            gallery.progress.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; gallery.progress.fillAmount = .5f;
            gallery.status = Label(canvas.transform, "Preview status", "INTERACTIVE PREVIEW  /  no patient data", GripUiRole.TextOnDark, 23).GetComponent<TMP_Text>();
            Stretch(gallery.status.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(32, 28), new Vector2(-32, 80));
            new GameObject("Gallery EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Folder("Assets/UI", "Components");
            foreach (var item in new[] { gallery.primary.gameObject, gallery.reset.gameObject, disabled.gameObject,
                gallery.input.gameObject, gallery.slider.gameObject, gallery.toggle.gameObject, chip.gameObject, bar.gameObject })
                if (!File.Exists(Prefabs + "/" + item.name + ".prefab"))
                    PrefabUtility.SaveAsPrefabAsset(item, Prefabs + "/" + item.name + ".prefab");
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            var build = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!build.Exists(s => s.path == ScenePath)) build.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = build.ToArray();
            EditorSceneManager.OpenScene("Assets/Scenes/GripRehab.unity"); Verify();
        }
        public static void Verify()
        {
            var theme = UnityEngine.Object.FindAnyObjectByType<GripUiThemeController>();
            var background = UnityEngine.Object.FindAnyObjectByType<GripUiAmbientBackground>(FindObjectsInactive.Include);
            if (theme == null || background == null || background.layers.Length != 3 || background.theme != theme)
                throw new Exception("Main scene background references missing");
            foreach (var layer in background.layers) if (layer == null || layer.sprite == null || layer.raycastTarget)
                throw new Exception("Decorative layer must have a saved sprite and ignore raycasts");
            if (!File.Exists(ScenePath)) throw new Exception("Saved gallery missing");
            foreach (var name in new[] { "PrimaryButton", "SecondaryButton", "DisabledButton", "InputField", "Slider", "Toggle", "ConnectionChip", "ProgressBar" })
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + name + ".prefab") == null)
                    throw new Exception("UI template missing: " + name);
        }
        static Sprite Radial()
        {
            const string path = "Assets/UI/SoftGlow.png";
            if (!File.Exists(path))
            {
                const int size = 128; var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + .5f) / size * 2 - 1, (y + .5f) / size * 2 - 1);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1 - p.magnitude), 2);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
                }
                texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            }
            var savedImporter = (TextureImporter)AssetImporter.GetAtPath(path);
            savedImporter.textureType = TextureImporterType.Sprite; savedImporter.spriteImportMode = SpriteImportMode.Single;
            savedImporter.mipmapEnabled = false; savedImporter.alphaIsTransparency = true;
            savedImporter.textureCompression = TextureImporterCompression.Uncompressed; savedImporter.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static void InstallBackground(Transform parent, GripUiThemeController theme, Sprite sprite)
        {
            var found = parent.Find("Ambient background");
            if (found != null)
            {
                var existing = found.GetComponent<GripUiAmbientBackground>();
                if (existing == null) throw new Exception("Unexpected existing background; preserve it");
                foreach (var layer in existing.layers) if (layer != null) { layer.sprite = sprite; layer.raycastTarget = false; EditorUtility.SetDirty(layer); }
                return;
            }
            var root = Rect(parent, "Ambient background"); Stretch(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.SetAsFirstSibling(); var ambient = root.gameObject.AddComponent<GripUiAmbientBackground>(); ambient.theme = theme;
            ambient.layers = new UnityEngine.UI.Image[3];
            for (int i = 0; i < 3; i++)
            {
                var layer = Box(root, "Glow " + i, GripUiRole.Accent); layer.GetComponent<GripUiRoleTag>().excludeFromTheme = true;
                Place(layer, new Vector2((i - 1) * 480, i == 1 ? 240 : -120), new Vector2(1000, 900));
                var image = layer.GetComponent<UnityEngine.UI.Image>(); image.sprite = sprite; image.type = UnityEngine.UI.Image.Type.Simple;
                image.color = new Color(theme.dark.accent.r, theme.dark.accent.g, theme.dark.accent.b, theme.dark.backgroundOpacity);
                image.raycastTarget = false; ambient.layers[i] = image;
            }
            EditorUtility.SetDirty(ambient);
        }
        static void Folder(string parent, string name) { if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name); }
        static RectTransform Rect(Transform parent, string name, params Type[] components)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); foreach (var c in components) go.AddComponent(c); return (RectTransform)go.transform; }
        static void Tag(UnityEngine.UI.Graphic graphic, GripUiRole role) { graphic.gameObject.AddComponent<GripUiRoleTag>().role = role; graphic.color = palette.Resolve(role); }
        static RectTransform Box(Transform parent, string name, GripUiRole role)
        { var r = Rect(parent, name, typeof(UnityEngine.UI.Image)); var image = r.GetComponent<UnityEngine.UI.Image>(); image.sprite = rounded; image.type = UnityEngine.UI.Image.Type.Sliced; image.raycastTarget = false; Tag(image, role); return r; }
        static RectTransform Label(Transform parent, string name, string text, GripUiRole role, float size)
        {
            var r = Rect(parent, name, typeof(TextMeshProUGUI)); var t = r.GetComponent<TextMeshProUGUI>();
            t.font = palette.displayFont; t.text = text; t.fontSize = size; t.enableAutoSizing = true;
            t.fontSizeMin = size - 3; t.fontSizeMax = size; t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false; Tag(t, role); return r;
        }
        static UnityEngine.UI.Button Button(Transform parent, string name, string text, GripUiRole role)
        {
            var r = Box(parent, name, role); var image = r.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
            var b = r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic = image;
            var t = Label(r, "Label", text, role == GripUiRole.Primary ? GripUiRole.TextOnPrimary : GripUiRole.TextOnSecondary, 24);
            Stretch(t, Vector2.zero, Vector2.one, new Vector2(20, 8), new Vector2(-20, -8)); return b;
        }
        static TMP_InputField Input(Transform parent)
        {
            var r = Box(parent, "InputField", GripUiRole.Secondary); var image = r.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
            var field = r.gameObject.AddComponent<TMP_InputField>(); field.targetGraphic = image;
            var viewport = Rect(r, "Text area", typeof(UnityEngine.UI.RectMask2D)); Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(20, 8), new Vector2(-20, -8));
            var text = Label(viewport, "Text", "", GripUiRole.TextOnSecondary, 24);
            Stretch(text, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hint = Label(viewport, "Placeholder", "Preview text input", GripUiRole.TextOnSecondary, 24);
            Stretch(hint, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            field.textViewport = viewport; field.textComponent = text.GetComponent<TMP_Text>(); field.placeholder = hint.GetComponent<TMP_Text>();
            field.characterLimit = 48; return field;
        }
        static UnityEngine.UI.Slider Slider(Transform parent)
        {
            var r = Rect(parent, "Slider", typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Slider));
            r.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
            var track = Box(r, "Track", GripUiRole.Secondary); Stretch(track, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(20, -4), new Vector2(-20, 4));
            var area = Rect(r, "Handle area"); Stretch(area, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(20, -24), new Vector2(-20, 24));
            var handle = Box(area, "Handle", GripUiRole.Accent); handle.sizeDelta = new Vector2(48, 0); handle.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var s = r.GetComponent<UnityEngine.UI.Slider>(); s.handleRect = handle; s.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>(); s.value = .5f; return s;
        }
        static UnityEngine.UI.Toggle Toggle(Transform parent, out TMP_Text label)
        {
            var r = Box(parent, "Toggle", GripUiRole.Secondary); var image = r.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
            var toggle = r.gameObject.AddComponent<UnityEngine.UI.Toggle>(); toggle.targetGraphic = image;
            var mark = Box(r, "Checkmark", GripUiRole.Primary); Stretch(mark, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(20, -20), new Vector2(60, 20));
            toggle.graphic = mark.GetComponent<UnityEngine.UI.Image>();
            var t = Label(r, "Label", "PREVIEW SWITCH: OFF", GripUiRole.TextOnSecondary, 24);
            Stretch(t, Vector2.zero, Vector2.one, new Vector2(76, 8), new Vector2(-20, -8)); label = t.GetComponent<TMP_Text>(); return toggle;
        }
        static void Stretch(RectTransform r, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
        { r.anchorMin = min; r.anchorMax = max; r.offsetMin = low; r.offsetMax = high; }
        static void Place(Transform t, Vector2 position, Vector2 size)
        { var r = (RectTransform)t; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = position; r.sizeDelta = size; }
        static void Row(Transform t, float top, float height)
        { Stretch((RectTransform)t, new Vector2(0, 1), Vector2.one, new Vector2(24, -top - height), new Vector2(-24, -top)); }
    }
}
