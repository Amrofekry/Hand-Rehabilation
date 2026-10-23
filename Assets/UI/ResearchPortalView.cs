using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GripRehab.UI
{
    /// <summary>English, theme-driven presentation surface; no patient storage or input transport.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ResearchPortalView : MonoBehaviour
    {
        GripUiThemeController theme;
        GripUiAccessibility accessibility;
        CanvasGroup group;
        RectTransform content;
        TMP_Text heading, description, liveStatus;
        UnityEngine.UI.Image progress;
        UnityEngine.UI.ScrollRect scroll;
        UnityEngine.UI.Button backButton;
        readonly List<UnityEngine.UI.Button> buttons = new List<UnityEngine.UI.Button>();
        readonly Dictionary<string, UnityEngine.UI.InputField> fields = new Dictionary<string, UnityEngine.UI.InputField>();
        Action back;
        public bool Visible => group != null && group.interactable;
        public GripUiTheme Palette => theme.Current;

        public void Initialize(Transform parent, GripUiThemeController owner, GripUiAccessibility settings)
        {
            theme = owner; accessibility = settings;
            var rect = GetComponent<RectTransform>(); rect.SetParent(parent, false); Fill(rect);
            group = gameObject.AddComponent<CanvasGroup>();
            Paint(gameObject.AddComponent<UnityEngine.UI.Image>(), GripUiRole.Backdrop);
            var page = Rect("Research workspace", rect); Fill(page, 40, 24);
            heading = Label(page, "", 42, 82); Anchor(heading.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -38), new Vector2(0, 76));
            description = Label(page, "", 19, 70); Anchor(description.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -112), new Vector2(0, 65));
            var scrollRoot = Rect("Accessible content", page);
            scrollRoot.anchorMin = new Vector2(0, 0); scrollRoot.anchorMax = new Vector2(1, 1);
            scrollRoot.offsetMin = new Vector2(0, 76); scrollRoot.offsetMax = new Vector2(0, -157);
            scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); scroll.horizontal = false;
            var viewport = Rect("Viewport", scrollRoot); Fill(viewport);
            var maskImage = viewport.gameObject.AddComponent<UnityEngine.UI.Image>(); Paint(maskImage, GripUiRole.Backdrop);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = Palette.gridPixels * 2; layout.padding = new RectOffset(8, 20, 8, 16);
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var footer = Rect("Navigation", page); Anchor(footer, Vector2.zero, new Vector2(1, 0), new Vector2(0, 28), new Vector2(0, 60));
            backButton = AddFooter(footer, "BACK", 0, .31f, () => back?.Invoke());
            AddFooter(footer, "SCROLL UP", .345f, .655f, () => scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + .25f));
            AddFooter(footer, "SCROLL DOWN", .69f, 1, () => scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - .25f));
            Hide();
        }
        UnityEngine.UI.Button AddFooter(RectTransform parent, string caption, float left, float right, Action action)
        {
            var button = MakeButton(parent, caption, action, false); var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(left, 0); rect.anchorMax = new Vector2(right, 1); rect.offsetMin = rect.offsetMax = Vector2.zero;
            return button;
        }
        public void Show(string title, string detail, Action goBack)
        {
            Clear(); back = goBack; heading.text = title; description.text = detail;
            backButton.interactable = goBack != null;
            group.alpha = 1; group.interactable = group.blocksRaycasts = true; transform.SetAsLastSibling();
            scroll.verticalNormalizedPosition = 1;
        }
        void Clear()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            for (int i = content.childCount - 1; i >= 0; i--) { var child = content.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
            fields.Clear(); buttons.Clear(); progress = null; liveStatus = null;
        }
        public void Hide() { if (group != null) { group.alpha = 0; group.interactable = group.blocksRaycasts = false; } }
        public void Text(string text, int height = 72)
        { Label(content, text, Palette.bodySize, height); }
        public void Status(string text)
        { liveStatus = Label(content, text, Palette.bodySize, 88); }
        public void SetStatus(string text) { if (liveStatus != null && liveStatus.text != text) liveStatus.text = text; }
        public void SetProgress(float value) { if (progress != null) progress.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1); }
        public void Progress()
        {
            var track = Rect("Progress", content); Height(track.gameObject, 24);
            Paint(track.gameObject.AddComponent<UnityEngine.UI.Image>(), GripUiRole.DarkSurface);
            var fill = Rect("Fill", track); Fill(fill);
            progress = fill.gameObject.AddComponent<UnityEngine.UI.Image>(); Paint(progress, GripUiRole.Accent);
            progress.type = UnityEngine.UI.Image.Type.Simple;
            // A shared sprite gives Filled geometry an actual texture rect.
            var sample = theme.appearanceButton != null ? theme.appearanceButton.targetGraphic as UnityEngine.UI.Image : null;
            progress.sprite = sample != null ? sample.sprite : null;
        }
        public UnityEngine.UI.InputField Field(string key, string caption, bool secret = false, int limit = 120, bool multiline = false)
        {
            Label(content, caption, Palette.captionSize + 2, 32);
            var rect = Rect(key, content); Height(rect.gameObject, multiline ? 144 : TargetHeight);
            Paint(rect.gameObject.AddComponent<UnityEngine.UI.Image>(), GripUiRole.DarkSurface);
            var input = rect.gameObject.AddComponent<UnityEngine.UI.InputField>(); input.characterLimit = limit;
            input.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            input.contentType = secret ? UnityEngine.UI.InputField.ContentType.Password : UnityEngine.UI.InputField.ContentType.Standard;
            input.lineType = multiline ? UnityEngine.UI.InputField.LineType.MultiLineNewline : UnityEngine.UI.InputField.LineType.SingleLine;
            var textRect = Rect("Editable text", rect); Fill(textRect, 20, 10);
            var text = textRect.gameObject.AddComponent<UnityEngine.UI.Text>(); text.font = Palette.latinFont != null ? Palette.latinFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Palette.bodySize; text.alignment = TextAnchor.MiddleLeft;
            text.color = Palette.textOnDark; text.supportRichText = false; text.raycastTarget = false; input.textComponent = text;
            input.selectionColor = new Color(Palette.accent.r, Palette.accent.g, Palette.accent.b, .4f);
            input.caretColor = Palette.textOnDark; input.customCaretColor = true;
            theme.RegisterPresentation(rect);
            fields.Add(key, input); return input;
        }
        public string Value(string key) => fields.TryGetValue(key, out var input) ? input.text : "";
        public void Choice(string caption, string[] options, int selected, Action<int> choose)
        {
            Text(caption, 38);
            for (int i = 0; i < options.Length; i++) { int index = i; Button((i == selected ? "[Selected]  " : "") + options[i], () => choose(index)); }
        }
        public UnityEngine.UI.Button Button(string caption, Action action, bool primary = false, bool enabled = true)
        {
            var button = MakeButton(content, caption, action, primary); Height(button.gameObject, TargetHeight);
            button.interactable = enabled; buttons.Add(button);
            if (primary && enabled && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);
            return button;
        }
        float TargetHeight => Mathf.Max(Palette.patientTargetPixels, Palette.patientTargetPixels / Mathf.Max(.1f, theme.canvas.scaleFactor));
        UnityEngine.UI.Button MakeButton(Transform parent, string caption, Action action, bool primary)
        {
            var rect = Rect(caption, parent); var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            Paint(image, primary ? GripUiRole.Primary : GripUiRole.Secondary);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => action?.Invoke());
            var label = Label(rect, caption, Palette.bodySize + 2, 64); Fill(label.rectTransform, 20, 8);
            Paint(label, primary ? GripUiRole.TextOnPrimary : GripUiRole.TextOnSecondary);
            if (accessibility != null) rect.gameObject.AddComponent<GripUiControlFeedback>().Bind(accessibility, button);
            return button;
        }
        TMP_Text Label(Transform parent, string text, int size, int height)
        {
            var rect = Rect("Label", parent); Height(rect.gameObject, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.text = text;
            label.font = size >= 30 ? theme.HeadingFont : theme.BodyFont; label.fontSize = size;
            label.enableAutoSizing = true; label.fontSizeMin = size; label.fontSizeMax = size * theme.fontScale;
            label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
            label.enableWordWrapping = true; label.richText = false;
            Paint(label, GripUiRole.TextOnDark); return label;
        }
        public void Chart(float[] values, string caption)
        {
            Text(caption, 42); var rect = Rect("Session score chart", content); Height(rect.gameObject, 180);
            var chart = rect.gameObject.AddComponent<ResearchChart>(); Paint(chart, GripUiRole.Accent); chart.raycastTarget = false; chart.SetValues(values);
        }
        public void FocusFirst()
        {
            if (EventSystem.current == null) return;
            foreach (var button in buttons)
                if (button.IsInteractable()) { EventSystem.current.SetSelectedGameObject(button.gameObject); break; }
        }
        void Update() { if (Visible && Input.GetKeyDown(KeyCode.Escape)) back?.Invoke(); }
        void Paint(UnityEngine.UI.Graphic graphic, GripUiRole role)
        {
            graphic.color = Palette.Resolve(role); var tag = graphic.gameObject.GetComponent<GripUiRoleTag>() ?? graphic.gameObject.AddComponent<GripUiRoleTag>(); tag.role = role;
            theme.RegisterPresentation(graphic.transform);
        }
        static RectTransform Rect(string name, Transform parent)
        { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect; }
        static void Fill(RectTransform rect, float x = 0, float y = 0)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(x, y); rect.offsetMax = new Vector2(-x, -y); }
        static void Height(GameObject obj, float value)
        { var layout = obj.AddComponent<UnityEngine.UI.LayoutElement>(); layout.minHeight = layout.preferredHeight = value; }
        static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        { rect.anchorMin = min; rect.anchorMax = max; rect.anchoredPosition = position; rect.sizeDelta = size; }
    }
}
