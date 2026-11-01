using UnityEngine;
using UnityEngine.UI;

namespace GripRehab
{
    /// <summary>
    /// Stage 18 device status panel (2026-11-01): a small read-only card in sensor mode showing the force bridge link
    /// (LIVE / STALE / WAITING / RECONNECTED), packet rate, loss, sample age, reconnects and calibration revision.
    /// Built in code like the hand-look indicators, so no scene edit. The text refreshes twice a second and only when
    /// it changed. Reconnecting is the bridge's job (unity_bridge.py); the panel shows it, it does not drive it.
    /// </summary>
    public sealed class DeviceStatusPanel : MonoBehaviour
    {
        public readonly DeviceHealth Health = new DeviceHealth();
        ApartmentExperience app; CanvasGroup group; Text text; Image dot; float nextText; string shown = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var e = FindAnyObjectByType<ApartmentExperience>();
            if (e != null && e.forceReceiver != null && e.GetComponent<DeviceStatusPanel>() == null) e.gameObject.AddComponent<DeviceStatusPanel>();
        }

        void Awake() { app = GetComponent<ApartmentExperience>(); Build(); }

        void Update()
        {
            bool on = app != null && app.Hardware && app.forceReceiver != null;
            group.alpha = on ? 1 : 0;
            if (!on) return;
            var state = app.forceReceiver.State; double now = Time.realtimeSinceStartupAsDouble;
            state.TryGetSample(now, out var sample);
            Health.Observe(now, state.SampleCount, sample.Sequence, state.LastAgeMs, state.Latest.Valid, state.Generation, state.IsSynthetic);
            if (Time.unscaledTime < nextText) return;
            nextText = Time.unscaledTime + .5f;
            var s = Health.State;
            dot.color = s == DeviceLinkState.Live ? new Color(.21f, .79f, .64f) : s == DeviceLinkState.Reconnected ? new Color(.95f, .75f, .25f) : new Color(.9f, .35f, .3f);
            var line = "SENSOR  " + (s == DeviceLinkState.Live ? "LIVE" : s == DeviceLinkState.Stale ? "STALE" : s == DeviceLinkState.Waiting ? "WAITING" : "RECONNECTED") +
                (Health.Synthetic ? "  (SYNTHETIC REPLAY)" : "") +
                "\nRATE " + Health.RateHz.ToString("0") + " Hz   LOSS " + Health.LossPercent.ToString("0.0") + " %   AGE " + Health.AgeMs + " ms" +
                "\nRECONNECTS " + Health.Reconnects + "   CAL REV " + state.CalibrationRevision + "   BATTERY n/a";
            if (line != shown) { shown = line; text.text = line; }
        }

        void Build()
        {
            var go = new GameObject("Device status", typeof(Canvas), typeof(CanvasGroup)); go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 840;
            group = go.GetComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
            var card = new GameObject("Card", typeof(Image)); card.transform.SetParent(go.transform, false);
            var r = card.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = r.pivot = new Vector2(1, 1); r.anchoredPosition = new Vector2(-16, -96); r.sizeDelta = new Vector2(330, 66);
            var bg = card.GetComponent<Image>(); bg.color = new Color(.06f, .08f, .1f, .82f); bg.raycastTarget = false;
            var d = new GameObject("Link", typeof(Image)); d.transform.SetParent(card.transform, false);
            var dr = d.GetComponent<RectTransform>(); dr.anchorMin = dr.anchorMax = dr.pivot = new Vector2(0, 1); dr.anchoredPosition = new Vector2(10, -10); dr.sizeDelta = new Vector2(10, 10);
            dot = d.GetComponent<Image>(); dot.raycastTarget = false;
            var t = new GameObject("Text", typeof(Text)); t.transform.SetParent(card.transform, false);
            var tr = t.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(28, 4); tr.offsetMax = new Vector2(-8, -4);
            text = t.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 13; text.color = new Color(.92f, .95f, .97f);
            text.raycastTarget = false; text.alignment = TextAnchor.UpperLeft; text.text = "SENSOR  WAITING";
        }
    }
}
