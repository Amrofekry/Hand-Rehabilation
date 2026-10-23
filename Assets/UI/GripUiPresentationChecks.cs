using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Profiling;

namespace GripRehab.UI
{
    // Opt-in software QA only. The -apartment-* prefix also isolates the
    // existing shell's profiles and prevents physical camera launch.
    public sealed class GripUiPresentationChecks : MonoBehaviour
    {
        readonly List<string> checks = new List<string>();
        readonly List<Performance> performance = new List<Performance>(2);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-apartment-ui-smoke") < 0) return;
            Application.runInBackground = true;
            new GameObject("Opt-in presentation QA").AddComponent<GripUiPresentationChecks>();
        }
        IEnumerator Start()
        {
            var run = Run();
            while (true)
            {
                object current = null; bool more;
                try { more = run.MoveNext(); if (more) current = run.Current; }
                catch (Exception error) { Finish(false, error.ToString()); yield break; }
                if (!more) break;
                yield return current;
            }
            Finish(true, "");
        }
        void Check(bool condition, string label)
        { if (!condition) throw new Exception(label); checks.Add(label); }
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);
            var ui = FindAnyObjectByType<GripUiAccessibility>();
            Check(ui != null && ui.testing && ui.theme != null, "UI fixtures isolate display preferences and profiles");
            Check(ui.dwellAdapter is IGripUiTestNavigation, "UI has a thin opt-in navigation adapter");
            ((IGripUiTestNavigation)ui.dwellAdapter).ShowHomeForTest(); yield return null;
            var theme = ui.theme;
            Check(theme.appearanceButton.IsInteractable(), "Appearance is reachable from Home");
            theme.appearanceButton.onClick.Invoke();
            Check(theme.appearancePanel.blocksRaycasts && theme.appearancePanel.interactable, "Appearance button opens the settings modal");
            Check(theme.appearanceScrim != null && theme.appearanceScrim.enabled && !theme.homeActions.interactable,
                "Modal blocks pointer and keyboard access to the underlying Home actions");
            Check(ui.dwellSlider.handleRect.rect.height <= 49 && ui.volumeSlider.handleRect.rect.height <= 49,
                "Slider handles remain within their forty-eight pixel height");
            var savedControls = ui.controls;
            Check(savedControls.Length >= 10, "Saved controls participate in feedback");
            var control = theme.darkButton;
            var feedback = control.GetComponent<GripUiControlFeedback>();
            Check(feedback != null, "Buttons have an independent presentation feedback adapter");
            var es = EventSystem.current;
            es.SetSelectedGameObject(control.gameObject); yield return null;
            Check(feedback.FocusVisible, "Keyboard selection displays focus without invoking the button");
            es.SetSelectedGameObject(null);
            for (int palette = 0; palette < 3; palette++)
            {
                theme.Select(palette); yield return new WaitForSecondsRealtime(.35f);
                Check(theme.selectedTheme == palette, "Theme " + palette + " is selected");
                for (int size = 0; size < 3; size++)
                {
                    float target = 1 + .25f * size;
                    while (Mathf.Abs(theme.fontScale - target) > .01f) ui.textButton.onClick.Invoke();
                    Check(Mathf.Abs(theme.fontScale - target) < .01f, "Theme " + palette + " text preference " + target);
                    foreach (bool reduced in new[] { false, true })
                    {
                        if (theme.reduceMotion != reduced) ui.motionButton.onClick.Invoke();
                        yield return null;
                        Check(theme.reduceMotion == reduced, "Reduce motion applies to theme " + palette);
                    }
                    if (size == 2) yield return Screenshot("theme-" + palette + "-appearance-large");
                }
                while (theme.fontScale > 1.01f) ui.textButton.onClick.Invoke(); yield return null;
                yield return Screenshot("theme-" + palette + "-appearance");
                theme.closeButton.onClick.Invoke();
                Check(!theme.appearancePanel.blocksRaycasts, "Closing theme " + palette + " restores Home input");
                Check(!theme.appearanceScrim.enabled && theme.homeActions.interactable, "Home controls are restored after closing modal");
                Check(es.currentSelectedGameObject == theme.appearanceButton.gameObject, "Keyboard focus returns to Appearance after modal closes");
                yield return Screenshot("theme-" + palette + "-home");
                theme.OpenAppearance();
            }
            theme.reduceTransparency = false;
            ui.transparencyButton.onClick.Invoke();
            Check(theme.reduceTransparency, "Solid panel preference toggles without changing a session");
            ui.dwellSlider.value = 2;
            Check(Mathf.Abs(ui.dwellSeconds - 2) < .01f, "Dwell slider applies its two-second lower bound");
            Canvas.ForceUpdateCanvases();
            var sliderArea = (RectTransform)ui.dwellSlider.handleRect.parent;
            var eventData = new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left, position = RectTransformUtility.WorldToScreenPoint(null,
                sliderArea.TransformPoint(new Vector3(sliderArea.rect.xMax, 0, 0))) };
            ui.dwellSlider.OnPointerDown(eventData); ui.dwellSlider.OnPointerUp(eventData);
            Check(ui.dwellSeconds > 7.9f, "Mouse pointer input at the end of the slider changes its bound preference");
            ui.SetDwell(99); Check(ui.dwellSeconds == 8, "Dwell preference is clamped to eight seconds");
            ui.SetDwell(5);
            ui.shorterButton.onClick.Invoke(); ui.longerButton.onClick.Invoke();
            Check(ui.dwellSeconds == 5, "Dwell can be adjusted with point-and-hold buttons without dragging a slider");
            ui.volumeSlider.value = 0; Check(ui.volume == 0, "UI sound can be muted independently");
            ui.SetVolume(2); Check(ui.volume == 1, "UI volume is bounded");
            ui.SetVolume(.15f);
            ui.muteButton.onClick.Invoke(); Check(ui.volume == 0, "Sound button mutes without a slider drag");
            ui.SetVolume(.15f);
            int cues = ui.PlayedCueCount;
            var clickData = new PointerEventData(es) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(theme.darkButton.gameObject, clickData, ExecuteEvents.pointerDownHandler);
            Check(ui.PlayedCueCount == cues, "Pointer press alone does not play a false activation cue");
            ExecuteEvents.Execute(theme.darkButton.gameObject, clickData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(theme.darkButton.gameObject, clickData, ExecuteEvents.pointerClickHandler);
            Check(ui.PlayedCueCount == cues + 1 && ui.LastCueWasClick, "Mouse or hand-dwell click plays one activation cue");
            cues = ui.PlayedCueCount;
            ExecuteEvents.Execute(theme.darkButton.gameObject, new BaseEventData(es), ExecuteEvents.submitHandler);
            Check(ui.PlayedCueCount == cues + 1 && ui.LastCueWasClick, "Keyboard Submit plays the same activation cue");
            theme.Select(0); theme.CloseAppearance();
            Check(FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "UI presentation adds no second EventSystem");
            yield return Measure("Home");
            theme.OpenAppearance(); yield return Measure("Appearance"); theme.CloseAppearance();
        }
        IEnumerator Measure(string screen)
        {
            for (int i = 0; i < 90; i++) yield return null;
            const int count = 300;
            var samples = new float[count];
            double total = 0; long gcTotal = 0, gcPeak = 0, batchPeak = 0;
            using (var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
            using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count"))
            {
                for (int i = 0; i < count; i++)
                {
                    yield return null;
                    samples[i] = Time.unscaledDeltaTime * 1000;
                    total += samples[i];
                    if (gc.Valid) { gcTotal += gc.LastValue; gcPeak = Math.Max(gcPeak, gc.LastValue); }
                    if (batches.Valid) batchPeak = Math.Max(batchPeak, batches.LastValue);
                }
                Array.Sort(samples);
                performance.Add(new Performance { screen = screen, frames = count,
                    averageMs = (float)(total / count), p95Ms = samples[(int)(count * .95f) - 1],
                    averageFps = (float)(1000 * count / total), gcCounterAvailable = gc.Valid,
                    gcAverageBytes = gcTotal / count, gcPeakBytes = gcPeak,
                    batchCounterAvailable = batches.Valid, peakBatches = batchPeak });
            }
        }
        IEnumerator Screenshot(string name)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine("Logs", "ui-screenshots"); Directory.CreateDirectory(path);
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(path, name + ".png"), texture.EncodeToPNG()); Destroy(texture);
        }
        void Finish(bool passed, string error)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/ui-presentation-checks.json", JsonUtility.ToJson(new Result
            { passed = passed, error = error, checks = checks.ToArray(), width = Screen.width, height = Screen.height,
                performance = performance.ToArray(), cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                memoryMb = SystemInfo.systemMemorySize, utc = DateTime.UtcNow.ToString("O") }, true));
            Debug.Log("[UI_PRESENTATION] " + (passed ? "PASS" : "FAIL") + ": " + checks.Count + " " + error);
            Application.Quit(passed ? 0 : 1);
        }
        [Serializable] class Performance
        {
            public string screen; public int frames;
            public float averageMs, p95Ms, averageFps;
            public bool gcCounterAvailable, batchCounterAvailable;
            public long gcAverageBytes, gcPeakBytes, peakBatches;
        }
        [Serializable] class Result
        {
            public bool passed; public string error, utc, cpu, gpu; public string[] checks;
            public int width, height, memoryMb; public Performance[] performance;
        }
    }
}
