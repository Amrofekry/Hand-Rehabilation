using UnityEngine;
using TMPro;

namespace GripRehab.UI
{
    // Presentation sandbox only. It has no profiles, camera, force or therapy data.
    public sealed class GripUiGallery : MonoBehaviour
    {
        public GripUiThemeController theme;
        public UnityEngine.UI.Button primary, motion, reset;
        public UnityEngine.UI.Slider slider;
        public UnityEngine.UI.Toggle toggle;
        public TMP_InputField input;
        public TMP_Text status, sliderLabel, toggleLabel, motionLabel;
        public UnityEngine.UI.Image progress;
        public int activations;
        void Start()
        {
            primary.onClick.AddListener(Activate);
            motion.onClick.AddListener(ToggleMotion);
            reset.onClick.AddListener(ResetPreview);
            slider.onValueChanged.AddListener(SetProgress);
            toggle.onValueChanged.AddListener(SetToggle);
            ResetPreview();
        }
        public void Activate() { activations++; status.text = "PREVIEW CLICK " + activations + "  /  no patient data"; }
        public void ToggleMotion()
        {
            theme.reduceMotion = !theme.reduceMotion; theme.RefreshPresentation();
            motionLabel.text = theme.reduceMotion ? "MOTION: REDUCED" : "MOTION: ON";
        }
        public void SetProgress(float value)
        { progress.fillAmount = value; sliderLabel.text = "PREVIEW VALUE  " + Mathf.RoundToInt(value * 100) + "%"; }
        public void SetToggle(bool enabled) { toggleLabel.text = enabled ? "PREVIEW SWITCH: ON" : "PREVIEW SWITCH: OFF"; }
        public void ResetPreview()
        { activations = 0; status.text = "INTERACTIVE PREVIEW  /  no patient data"; slider.value = .5f; SetProgress(.5f); toggle.isOn = false; SetToggle(false); input.text = ""; }
        void OnDestroy()
        {
            primary.onClick.RemoveListener(Activate); motion.onClick.RemoveListener(ToggleMotion);
            reset.onClick.RemoveListener(ResetPreview); slider.onValueChanged.RemoveListener(SetProgress);
            toggle.onValueChanged.RemoveListener(SetToggle);
        }
    }
}
