using UnityEngine;

namespace GripRehab.UI
{
    // Three shared-sprite UI layers. No camera, render texture, particle system,
    // game input or per-frame object creation. The owner panel controls visibility.
    public sealed class GripUiAmbientBackground : MonoBehaviour
    {
        public GripUiThemeController theme;
        public UnityEngine.UI.Image[] layers;
        [Tooltip("0 = Off, 1 = Low (one layer), 2 = High (three layers)")]
        [Range(0, 2)] public int quality = 2;
        Vector2[] rest;
        float clock;
        public int EffectiveQuality => theme == null || theme.reduceTransparency || theme.selectedTheme == 2 ? 0 :
            SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize < 2048 ? Mathf.Min(1, quality) : quality;
        public bool IsMoving => EffectiveQuality > 0 && theme != null && !theme.reduceMotion;
        void Awake()
        {
            rest = new Vector2[layers.Length];
            for (int i = 0; i < layers.Length; i++) if (layers[i] != null) rest[i] = layers[i].rectTransform.anchoredPosition;
        }
        void Update()
        {
            if (theme == null || rest == null) return;
            var palette = theme.Current;
            if (palette == null) return;
            int active = EffectiveQuality == 2 ? layers.Length : EffectiveQuality;
            if (IsMoving) clock += Time.unscaledDeltaTime;
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i]; if (layer == null) continue;
                bool shown = i < active; if (layer.enabled != shown) layer.enabled = shown;
                if (!shown) continue;
                var tint = i == 1 ? palette.primary : palette.accent; tint.a = palette.backgroundOpacity;
                if (layer.color != tint) layer.color = tint;
                Vector2 offset = IsMoving ? new Vector2(Mathf.Sin(clock * palette.backgroundSpeed + i * 2),
                    Mathf.Cos(clock * palette.backgroundSpeed * .7f + i)) * palette.backgroundTravel : Vector2.zero;
                layer.rectTransform.anchoredPosition = rest[i] + offset;
            }
        }
    }
}
