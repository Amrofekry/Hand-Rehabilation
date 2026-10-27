using UnityEngine;
using UnityEngine.EventSystems;

namespace GripRehab.UI
{
    // Decoration only: never submits, changes selection, or consumes grip input.
    public sealed class GripUiControlFeedback : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        GripUiAccessibility settings;
        UnityEngine.UI.Selectable control;
        UnityEngine.UI.Button button;
        RectTransform rect;
        Vector3 restScale;
        UnityEngine.UI.Image[] edges;
        UnityEngine.UI.Image marker;
        bool hover, focus, pressed;
        float scale = 1, errorUntil = -1;
        public bool FocusVisible => focus && control != null && control.IsInteractable();
        public bool Selected { get; private set; }
        public bool ErrorVisible => Time.unscaledTime < errorUntil;
        // Contrast of the focus ring against this control's own fill (WCAG 2.4.11 asks for 3:1).
        public float FocusContrast { get; private set; }

        // Stage 11: attach the shared feedback (focus ring, hover, cues, selected, error) to any control.
        public static GripUiControlFeedback Attach(GripUiAccessibility owner, UnityEngine.UI.Selectable item)
        {
            if (owner == null || owner.theme == null || item == null) return null;
            var feedback = item.GetComponent<GripUiControlFeedback>();
            if (feedback == null) feedback = item.gameObject.AddComponent<GripUiControlFeedback>();
            if (feedback.settings == null) feedback.Bind(owner, item);
            return feedback;
        }

        // Selected is shown by a bar, not by colour alone.
        public void SetSelected(bool on)
        {
            Selected = on;
            if (marker == null && on && settings != null)
            {
                var go = new GameObject("Selected marker", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(transform, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = new Vector2(.5f, 0); r.anchorMax = new Vector2(.5f, 0); r.pivot = new Vector2(.5f, 0);
                r.anchoredPosition = new Vector2(0, settings.theme.Current.selectedMarkerPixels + 2);
                marker = go.GetComponent<UnityEngine.UI.Image>(); marker.raycastTarget = false;
            }
            if (marker != null) marker.enabled = on;
        }

        public void FlashError()
        {
            if (settings == null) return;
            errorUntil = Time.unscaledTime + settings.theme.Current.errorSeconds;
        }

        Color Fill => control != null && control.targetGraphic != null ? control.targetGraphic.color : Color.black;

        public void Bind(GripUiAccessibility owner, UnityEngine.UI.Selectable item)
        {
            settings = owner; control = item; rect = (RectTransform)transform; restScale = rect.localScale;
            if (edges != null) return;
            button = item as UnityEngine.UI.Button;
            if (button != null) button.onClick.AddListener(Clicked);
            edges = new UnityEngine.UI.Image[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Focus border " + i, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(transform, false);
                var r = (RectTransform)go.transform;
                bool horizontal = i < 2;
                r.anchorMin = horizontal ? new Vector2(0, i) : new Vector2(i - 2, 0);
                r.anchorMax = horizontal ? new Vector2(1, i) : new Vector2(i - 2, 1);
                r.pivot = horizontal ? new Vector2(.5f, i) : new Vector2(i - 2, .5f);
                r.sizeDelta = horizontal ? new Vector2(0, owner.theme.Current.focusPixels) :
                    new Vector2(owner.theme.Current.focusPixels, 0);
                r.anchoredPosition = Vector2.zero;
                edges[i] = go.GetComponent<UnityEngine.UI.Image>(); edges[i].raycastTarget = false;
                edges[i].enabled = false;
            }
        }
        public void OnPointerEnter(PointerEventData data)
        { hover = true; if (control != null && control.IsInteractable()) settings?.PlayCue(false); }
        public void OnPointerExit(PointerEventData data) { hover = pressed = false; }
        public void OnSelect(BaseEventData data)
        { focus = true; if (control != null && control.IsInteractable()) settings?.PlayCue(false); }
        public void OnDeselect(BaseEventData data) { focus = pressed = false; }
        public void OnPointerDown(PointerEventData data)
        { if (control != null && control.IsInteractable()) pressed = true; }
        public void OnPointerUp(PointerEventData data) { pressed = false; }
        // Observe the existing activation event; mouse, Enter and hand dwell
        // get the same cue without implementing another Submit/click handler.
        void Clicked() { if (control != null && control.IsInteractable()) settings?.PlayCue(true); }
        void OnDestroy() { if (button != null) button.onClick.RemoveListener(Clicked); }
        void Update()
        {
            if (settings == null || control == null || edges == null) return;
            bool error = ErrorVisible;
            bool visible = (hover || focus) && control.IsInteractable() || error;
            var theme = settings.theme.Current;
            var fill = Fill;
            var ring = error ? GripUiContrast.MostVisible(fill, theme.danger, theme.textOnDark, theme.backdrop, theme.minimumFocusContrast)
                : GripUiContrast.MostVisible(fill, theme.accent, theme.textOnDark, theme.backdrop, theme.minimumFocusContrast);
            FocusContrast = GripUiContrast.Ratio(ring, fill);
            for (int i = 0; i < edges.Length; i++)
            { edges[i].enabled = visible; if (visible) edges[i].color = ring; }
            if (marker != null && Selected)
            {
                marker.color = GripUiContrast.MostVisible(fill, theme.textOnPrimary, theme.textOnDark, theme.backdrop, 4.5f);
                ((RectTransform)marker.transform).sizeDelta = new Vector2(Mathf.Min(48, rect.rect.width * .4f), theme.selectedMarkerPixels);
            }
            float target = settings.theme.reduceMotion || !control.IsInteractable() ? 1 :
                pressed ? .99f : hover || focus ? theme.hoverScale : 1;
            if (Mathf.Abs(scale - target) < .0001f) return;
            scale = Mathf.MoveTowards(scale, target, Time.unscaledDeltaTime / theme.controlSeconds * .04f);
            rect.localScale = restScale * scale;
        }
        void OnDisable()
        {
            hover = focus = pressed = false; scale = 1;
            if (rect != null) rect.localScale = restScale;
            if (edges != null) for (int i = 0; i < edges.Length; i++) edges[i].enabled = false;
        }
    }
}
