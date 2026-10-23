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
        bool hover, focus, pressed;
        float scale = 1;
        public bool FocusVisible => focus && control != null && control.IsInteractable();

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
            bool visible = (hover || focus) && control.IsInteractable();
            var theme = settings.theme.Current;
            for (int i = 0; i < edges.Length; i++)
            { edges[i].enabled = visible; if (visible) edges[i].color = theme.accent; }
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
