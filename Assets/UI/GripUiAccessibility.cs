using System;
using UnityEngine;

namespace GripRehab.UI
{
    // Optional bridge to the existing cursor. UI assembly has no gameplay dependency.
    public interface IGripDwellSettings { float DwellSeconds { get; set; } }
    // Read existing navigation only in the opt-in isolated software fixture.
    public interface IGripUiTestNavigation { void ShowHomeForTest(); }

    [DefaultExecutionOrder(500)]
    public sealed class GripUiAccessibility : MonoBehaviour
    {
        public GripUiThemeController theme;
        public UnityEngine.UI.Button motionButton, transparencyButton, textButton;
        public UnityEngine.UI.Button shorterButton, longerButton, muteButton;
        public TMPro.TMP_Text motionLabel, transparencyLabel, textLabel, dwellLabel;
        public UnityEngine.UI.Slider dwellSlider, volumeSlider;
        public MonoBehaviour dwellAdapter;
        public UnityEngine.UI.Selectable[] controls;
        public bool testing;
        public float volume = .15f;
        public float dwellSeconds = 5f;
        IGripDwellSettings dwell;
        AudioSource audioSource;
        AudioClip hoverTone, clickTone;
        float nextHover;
        public int PlayedCueCount { get; private set; }
        public bool LastCueWasClick { get; private set; }
        const string Prefix = "GripRehab.UI.v1.";

        void Start()
        {
            testing |= Array.Exists(Environment.GetCommandLineArgs(), a => a.StartsWith("-apartment-") ||
                a == "-vision-live-qa" || a == "-vision-perf-qa");
#if UNITY_EDITOR
            testing |= UnityEditor.SessionState.GetBool("ApartmentQA.Active", false);
#endif
            dwell = dwellAdapter as IGripDwellSettings;
            if (!testing)
            {
                theme.selectedTheme = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "theme", 0), 0, 2);
                theme.reduceMotion = PlayerPrefs.GetInt(Prefix + "motion", 0) == 1;
                theme.reduceTransparency = PlayerPrefs.GetInt(Prefix + "opaque", 0) == 1;
                theme.fontScale = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "text", 1), 1, 1.5f);
                dwellSeconds = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "dwell", 5), 2, 8);
                volume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "volume", .15f));
            }
            theme.Select(theme.selectedTheme);
            motionButton.onClick.AddListener(ToggleMotion);
            transparencyButton.onClick.AddListener(ToggleTransparency);
            textButton.onClick.AddListener(CycleText);
            shorterButton.onClick.AddListener(ShorterDwell);
            longerButton.onClick.AddListener(LongerDwell);
            muteButton.onClick.AddListener(ToggleMute);
            dwellSlider.SetValueWithoutNotify(dwellSeconds);
            volumeSlider.SetValueWithoutNotify(volume);
            dwellSlider.onValueChanged.AddListener(SetDwell);
            volumeSlider.onValueChanged.AddListener(SetVolume);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; audioSource.spatialBlend = 0;
            // Stage 12: UI cues follow the master volume of the project mixer (Resources/Audio/GripRehabMixer); they keep their own volume and mute.
            var mixer = Resources.Load<UnityEngine.Audio.AudioMixer>("Audio/GripRehabMixer");
            var master = mixer != null ? mixer.FindMatchingGroups("Master") : null;
            if (master != null && master.Length > 0) audioSource.outputAudioMixerGroup = master[0];
            hoverTone = Tone(theme.Current.hoverToneHz, .045f);
            clickTone = Tone(theme.Current.clickToneHz, .075f);
            if (controls != null)
                for (int i = 0; i < controls.Length; i++)
                    if (controls[i] != null)
                    {
                        var feedback = controls[i].GetComponent<GripUiControlFeedback>() ??
                            controls[i].gameObject.AddComponent<GripUiControlFeedback>();
                        feedback.Bind(this, controls[i]);
                    }
            UpdateLabels();
            ApplyDwell();
        }

        public void ToggleMotion()
        { theme.reduceMotion = !theme.reduceMotion; theme.RefreshPresentation(); UpdateLabels(); Save(); }
        public void ToggleTransparency()
        { theme.reduceTransparency = !theme.reduceTransparency; theme.RefreshPresentation(); UpdateLabels(); Save(); }
        public void CycleText()
        {
            theme.fontScale = theme.fontScale < 1.1f ? 1.25f : theme.fontScale < 1.4f ? 1.5f : 1f;
            theme.RefreshPresentation(); UpdateLabels(); Save();
        }
        public void SetDwell(float value)
        { dwellSeconds = Mathf.Clamp(value, 2, 8); ApplyDwell(); UpdateLabels(); Save(); }
        public void SetVolume(float value) { volume = Mathf.Clamp01(value); Save(); }
        public void ShorterDwell() { SetDwell(dwellSeconds - 1); dwellSlider.SetValueWithoutNotify(dwellSeconds); }
        public void LongerDwell() { SetDwell(dwellSeconds + 1); dwellSlider.SetValueWithoutNotify(dwellSeconds); }
        public void ToggleMute() { SetVolume(volume > 0 ? 0 : .15f); volumeSlider.SetValueWithoutNotify(volume); }
        void ApplyDwell() { if (dwell != null) dwell.DwellSeconds = dwellSeconds; }
        void UpdateLabels()
        {
            motionLabel.text = theme.reduceMotion ? "REDUCE MOTION: ON" : "REDUCE MOTION: OFF";
            transparencyLabel.text = theme.reduceTransparency ? "SOLID PANELS: ON" : "SOLID PANELS: OFF";
            textLabel.text = "TEXT SIZE: " + Mathf.RoundToInt(theme.fontScale * 100) + "% (FIT)";
            dwellLabel.text = "HAND POINTER HOLD: " + dwellSeconds.ToString("0.0") + "s";
        }
        public void Save()
        {
            if (testing) return;
            PlayerPrefs.SetInt(Prefix + "theme", theme.selectedTheme);
            PlayerPrefs.SetInt(Prefix + "motion", theme.reduceMotion ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "opaque", theme.reduceTransparency ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "text", theme.fontScale);
            PlayerPrefs.SetFloat(Prefix + "dwell", dwellSeconds);
            PlayerPrefs.SetFloat(Prefix + "volume", volume);
        }
        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() { Save(); }
        public void PlayCue(bool click)
        {
            if (audioSource == null || volume <= 0 || (!click && Time.unscaledTime < nextHover)) return;
            if (!click) nextHover = Time.unscaledTime + .15f;
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(click ? clickTone : hoverTone, volume);
            PlayedCueCount++; LastCueWasClick = click;
        }
        static AudioClip Tone(float hz, float seconds)
        {
            const int rate = 22050;
            var data = new float[Mathf.CeilToInt(rate * seconds)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / (data.Length - 1);
                data[i] = Mathf.Sin(2 * Mathf.PI * hz * i / rate) * Mathf.Sin(Mathf.PI * t) * .12f;
            }
            var clip = AudioClip.Create("Original UI tone", data.Length, 1, rate, false);
            clip.SetData(data, 0); return clip;
        }
        void OnDestroy()
        {
            if (motionButton != null) motionButton.onClick.RemoveListener(ToggleMotion);
            if (transparencyButton != null) transparencyButton.onClick.RemoveListener(ToggleTransparency);
            if (textButton != null) textButton.onClick.RemoveListener(CycleText);
            if (shorterButton != null) shorterButton.onClick.RemoveListener(ShorterDwell);
            if (longerButton != null) longerButton.onClick.RemoveListener(LongerDwell);
            if (muteButton != null) muteButton.onClick.RemoveListener(ToggleMute);
            if (dwellSlider != null) dwellSlider.onValueChanged.RemoveListener(SetDwell);
            if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(SetVolume);
            if (hoverTone != null) Destroy(hoverTone);
            if (clickTone != null) Destroy(clickTone);
        }
    }
}
