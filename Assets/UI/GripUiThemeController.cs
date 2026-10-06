using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GripRehab.UI
{
    // Read-only presentation adapter: restyles existing Graphics without touching
    // their Button listeners, input fields, scene navigation or gameplay state.
    public sealed class GripUiThemeController : MonoBehaviour
    {
        public Canvas canvas;
        public GripUiTheme dark, lightTheme, highContrast;
        public Button appearanceButton, darkButton, lightButton, contrastButton, closeButton;
        public CanvasGroup appearancePanel;
        public CanvasGroup homeActions;
        public Image appearanceScrim;
        public Text activeThemeLabel;
        public bool reduceMotion;
        public int selectedTheme;
        [Range(1f, 1.5f)] public float fontScale = 1f;
        public bool reduceTransparency;
        public event System.Action PresentationApplied;

        readonly List<Binding> bindings = new List<Binding>(256);
        readonly HashSet<Graphic> registered = new HashSet<Graphic>();
        readonly Dictionary<Font, TMPro.TMP_FontAsset> generatedFonts = new Dictionary<Font, TMPro.TMP_FontAsset>();
        Coroutine transition;

        struct Binding
        {
            public Graphic graphic;
            public GripUiRole role;
            public float alpha;
            public float fontSize;
            public Sprite artwork;
        }

        void Start()
        {
            if (canvas == null || dark == null || lightTheme == null || highContrast == null) return;
            CacheGraphics();
            // Use saved default. No patient or game data is written by the UI.
            selectedTheme = Mathf.Clamp(selectedTheme, 0, 2);
            ApplyImmediate();
            if (appearanceButton != null) appearanceButton.onClick.AddListener(OpenAppearance);
            if (darkButton != null) darkButton.onClick.AddListener(UseDark);
            if (lightButton != null) lightButton.onClick.AddListener(UseLight);
            if (contrastButton != null) contrastButton.onClick.AddListener(UseContrast);
            if (closeButton != null) closeButton.onClick.AddListener(CloseAppearance);
            CloseAppearance();
        }

        void OnDestroy()
        {
            if (appearanceButton != null) appearanceButton.onClick.RemoveListener(OpenAppearance);
            if (darkButton != null) darkButton.onClick.RemoveListener(UseDark);
            if (lightButton != null) lightButton.onClick.RemoveListener(UseLight);
            if (contrastButton != null) contrastButton.onClick.RemoveListener(UseContrast);
            if (closeButton != null) closeButton.onClick.RemoveListener(CloseAppearance);
            foreach (var font in generatedFonts.Values)
            {
                foreach (var texture in font.atlasTextures) if (texture != null) Destroy(texture);
                if (font.material != null) Destroy(font.material);
                Destroy(font);
            }
            generatedFonts.Clear();
        }

        void Update()
        {
            if (appearancePanel != null && appearancePanel.blocksRaycasts && Input.GetKeyDown(KeyCode.Escape))
                CloseAppearance();
        }

        void CacheGraphics()
        {
            RegisterPresentation(canvas.transform);
        }

        // Explicitly called when a runtime page/overlay is built. No hierarchy
        // scan in Update, no listener changes, and no duplicate registration.
        public void RegisterPresentation(Transform root)
        {
            if (root == null) return;
            bindings.RemoveAll(item => item.graphic == null);
            registered.RemoveWhere(graphic => graphic == null);
            var graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic == null || graphic.color.a < 0.04f) continue;
                if (!(graphic is Image) && !(graphic is Text) && !(graphic is TMPro.TMP_Text) && !(graphic is ResearchChart)) continue;
                var tag = graphic.GetComponent<GripUiRoleTag>();
                if (tag != null && tag.excludeFromTheme) continue;
                if (!registered.Add(graphic)) continue;
                var role = InferRole(graphic);
                bindings.Add(new Binding { graphic = graphic, role = role,
                    alpha = graphic.color.a, fontSize = graphic is Text text ? text.fontSize :
                        graphic is TMPro.TMP_Text tmp ? tmp.fontSize : 0,
                    artwork = role == GripUiRole.Backdrop && graphic is Image image ? image.sprite : null });
            }
            ApplyImmediate();
        }

        GripUiRole InferRole(Graphic graphic)
        {
            var explicitRole = graphic.GetComponent<GripUiRoleTag>();
            if (explicitRole != null) return explicitRole.role;
            if (graphic is Text || graphic is TMPro.TMP_Text)
            {
                var parentButton = graphic.GetComponentInParent<Button>();
                if (parentButton != null && parentButton.targetGraphic != null)
                {
                    var fill = parentButton.targetGraphic.color;
                    var buttonRole = parentButton.targetGraphic.GetComponent<GripUiRoleTag>();
                    if (buttonRole != null && buttonRole.role == GripUiRole.Primary || IsPrimary(fill))
                        return GripUiRole.TextOnPrimary;
                    if (buttonRole == null || buttonRole.role == GripUiRole.Secondary)
                        return GripUiRole.TextOnSecondary;
                    if (Luma(fill) > 0.55f) return GripUiRole.TextOnSecondary;
                }
                // Legacy labels have inconsistent saved colors. Use the
                // nearest painted ancestor so text contrasts with its card.
                for (Transform parent = graphic.transform.parent;
                     parent != null && parent != canvas.transform; parent = parent.parent)
                {
                    var panel = parent.GetComponent<Image>();
                    if (panel == null || panel.color.a < .1f) continue;
                    if (panel.name == "Rehab main menu overlay") return GripUiRole.TextOnDark;
                    return Luma(panel.color) > .55f ? GripUiRole.TextOnLight : GripUiRole.TextOnDark;
                }
                return Luma(graphic.color) > 0.58f ? GripUiRole.TextOnDark : GripUiRole.TextOnLight;
            }
            if (graphic.transform == canvas.transform || graphic.name == "Rehab main menu overlay")
                return GripUiRole.Backdrop;
            if (graphic.name == "Arrow" || graphic.name == "Pointer centre") return GripUiRole.TextOnDark;
            if (graphic.name == "Handle" || graphic.name == "Fill" || graphic.name == "Checkmark" ||
                graphic.name.StartsWith("Finger motion ", System.StringComparison.Ordinal)) return GripUiRole.Accent;
            if (graphic.GetComponent<Dropdown>() != null || graphic.GetComponent<InputField>() != null ||
                graphic.GetComponent<TMPro.TMP_Dropdown>() != null || graphic.GetComponent<TMPro.TMP_InputField>() != null)
                return GripUiRole.Surface;
            if (graphic.GetComponent<Button>() != null)
            {
                var c = graphic.color;
                if (IsPrimary(c))
                    return GripUiRole.Primary;
                return GripUiRole.Secondary;
            }
            if (IsPrimary(graphic.color))
                return GripUiRole.Accent;
            var value = Luma(graphic.color);
            if (value > 0.55f) return GripUiRole.Surface;
            return GripUiRole.DarkSurface;
        }

        static float Luma(Color color) => 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        static bool IsPrimary(Color c) => c.r < .45f && c.g > c.r * 1.5f && c.b > .4f;

        public TMPro.TMP_FontAsset BodyFont => RuntimeFont(Current != null ? Current.latinFont : null);
        public TMPro.TMP_FontAsset HeadingFont => RuntimeFont(Current != null ? Current.headingFont : null);
        TMPro.TMP_FontAsset RuntimeFont(Font source)
        {
            if (source == null) return Current != null ? Current.displayFont : null;
            if (generatedFonts.TryGetValue(source, out var cached)) return cached;
            // Embedded TTFs supply a reusable dynamic atlas once per font,
            // including pages created after the initial Canvas startup.
            var font = TMPro.TMP_FontAsset.CreateFontAsset(source);
            if (font == null) return Current.displayFont;
            font.name = source.name + " (HandMate UI)";
            generatedFonts.Add(source, font);
            if (source == Current.headingFont && source != Current.latinFont)
            {
                var body = BodyFont;
                // Runtime-created TMP assets have no serialized fallback list.
                // Gallery scene transitions must initialize it before adding the
                // body font (which supplies symbols absent from Orbitron).
                if (body != null && body != font)
                {
                    if (font.fallbackFontAssetTable == null)
                        font.fallbackFontAssetTable = new List<TMPro.TMP_FontAsset>();
                    font.fallbackFontAssetTable.Add(body);
                }
            }
            return font;
        }

        public GripUiTheme Current => selectedTheme == 1 ? lightTheme : selectedTheme == 2 ? highContrast : dark;
        public void RefreshPresentation() => ApplyImmediate();
        public void UseDark() => Select(0);
        public void UseLight() => Select(1);
        public void UseContrast() => Select(2);

        public void Select(int index)
        {
            selectedTheme = Mathf.Clamp(index, 0, 2);
            if (transition != null) StopCoroutine(transition);
            var duration = reduceMotion ? 0f : Current.transitionSeconds;
            if (duration <= 0.01f) ApplyImmediate();
            else transition = StartCoroutine(Blend(duration));
            UpdateLabel();
        }

        IEnumerator Blend(float duration)
        {
            var start = new Color[bindings.Count];
            for (int i = 0; i < bindings.Count; i++)
                start[i] = bindings[i].graphic != null ? bindings[i].graphic.color : Color.clear;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                for (int i = 0; i < bindings.Count; i++)
                {
                    var item = bindings[i];
                    if (item.graphic == null) continue;
                    var target = TargetColor(item);
                    if (item.role == GripUiRole.Backdrop && selectedTheme == 0 && item.artwork != null)
                    { target = Current.backdropArtworkTint; target.a *= item.alpha; }
                    item.graphic.color = Color.Lerp(start[i], target, eased);
                }
                yield return null;
            }
            ApplyImmediate();
            transition = null;
        }

        void ApplyImmediate()
        {
            var palette = Current;
            if (palette == null) return;
            // Keep each backdrop's own artwork, including dynamically built pages.
            for (int i = 0; i < bindings.Count; i++)
            {
                var item = bindings[i];
                if (item.graphic == null) continue;
                var color = TargetColor(item);
                if (item.role == GripUiRole.Backdrop && item.graphic is Image backdrop)
                    backdrop.sprite = selectedTheme == 0 ? item.artwork : null;
                if (item.role == GripUiRole.Backdrop && selectedTheme == 0 && item.artwork != null)
                { color = palette.backdropArtworkTint; color.a *= item.alpha; }
                item.graphic.color = color;
                if (item.graphic is Text text)
                {
                    var font = item.fontSize >= 30 && palette.headingFont != null ? palette.headingFont : palette.latinFont;
                    if (font != null) text.font = font;
                    // Fit within the authored control: never enlarge its hit area
                    // into adjacent controls or hide an action behind overflowing text.
                    text.resizeTextForBestFit = true;
                    text.resizeTextMinSize = Mathf.Min(14, Mathf.RoundToInt(item.fontSize));
                    text.resizeTextMaxSize = Mathf.RoundToInt(item.fontSize * fontScale);
                    text.fontSize = text.resizeTextMaxSize;
                }
                if (item.graphic is TMPro.TMP_Text tmp)
                {
                    var font = item.fontSize >= 30 ? HeadingFont : BodyFont;
                    if (font != null) tmp.font = font;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = Mathf.Min(14, item.fontSize);
                    tmp.fontSizeMax = item.fontSize * fontScale;
                    tmp.fontSize = tmp.fontSizeMax;
                }
            }
            if (appearanceScrim != null)
            { var scrim = palette.backdrop; scrim.a = palette.modalOpacity; appearanceScrim.color = scrim; }
            UpdateLabel();
            PresentationApplied?.Invoke();
        }

        Color TargetColor(Binding item)
        {
            var color = Current.Resolve(item.role);
            bool panel = item.role == GripUiRole.Surface || item.role == GripUiRole.DarkSurface;
            color.a *= reduceTransparency && panel ? 1f : item.alpha;
            return color;
        }

        void UpdateLabel()
        {
            if (activeThemeLabel != null)
                activeThemeLabel.text = selectedTheme == 1 ? "LIGHT" : selectedTheme == 2 ? "HIGH CONTRAST" : "DARK";
        }

        public void OpenAppearance()
        {
            if (appearancePanel == null) return;
            appearancePanel.alpha = 1; appearancePanel.interactable = true;
            appearancePanel.blocksRaycasts = true;
            if (appearanceScrim != null) appearanceScrim.enabled = true;
            if (homeActions != null) homeActions.interactable = false;
            if (UnityEngine.EventSystems.EventSystem.current != null && darkButton != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(darkButton.gameObject);
        }

        public void CloseAppearance()
        {
            if (appearancePanel == null) return;
            bool wasOpen = appearancePanel.blocksRaycasts;
            appearancePanel.alpha = 0; appearancePanel.interactable = false;
            appearancePanel.blocksRaycasts = false;
            if (appearanceScrim != null) appearanceScrim.enabled = false;
            if (homeActions != null) homeActions.interactable = true;
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (wasOpen && events != null && appearanceButton != null && appearanceButton.IsInteractable())
                events.SetSelectedGameObject(appearanceButton.gameObject);
        }
    }
}
