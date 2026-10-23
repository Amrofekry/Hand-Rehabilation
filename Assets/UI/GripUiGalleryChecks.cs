using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace GripRehab.UI
{
    public sealed class GripUiGalleryChecks : MonoBehaviour
    {
        readonly List<string> checks = new List<string>();
        string runtimeError;
        void OnEnable() { Application.logMessageReceived += ObserveLog; }
        void OnDisable() { Application.logMessageReceived -= ObserveLog; }
        void ObserveLog(string message, string stack, LogType type)
        {
            if (runtimeError == null && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
                runtimeError = message + "\n" + stack;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-apartment-gallery-smoke") < 0 ||
                FindAnyObjectByType<GripUiGalleryChecks>() != null) return;
            var runner = new GameObject("Opt-in isolated UI gallery checks"); DontDestroyOnLoad(runner);
            runner.AddComponent<GripUiGalleryChecks>();
        }
        IEnumerator Start()
        {
            var run = Run();
            while (true)
            {
                object current = null; bool more;
                try { more = run.MoveNext(); if (more) current = run.Current; }
                catch (Exception error) { Finish(false, error.ToString()); yield break; }
                if (!more) break; yield return current;
            }
            Finish(true, "");
        }
        void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks.Add(label); }
        IEnumerator Run()
        {
            yield return SceneManager.LoadSceneAsync("GripUiGallery"); yield return new WaitForSecondsRealtime(.5f);
            var gallery = FindAnyObjectByType<GripUiGallery>();
            Check(gallery != null && gallery.theme != null && gallery.input != null && gallery.progress != null, "Saved sandbox references resolve");
            Check(FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "One EventSystem after scene transition");
            Check(FindObjectsByType<GripUiGallery>(FindObjectsSortMode.None).Length == 1, "One independent UI gallery");
            var ambient = FindAnyObjectByType<GripUiAmbientBackground>();
            Check(ambient != null && ambient.layers.Length == 3, "Shared-sprite background is saved");
            var theme = gallery.theme;
            CheckRuntimeFonts(theme);
            foreach (var layer in ambient.layers) Check(layer != null && layer.sprite != null && !layer.raycastTarget, "Decoration ignores UI input");
            for (int i = 0; i < 3; i++)
            {
                theme.Select(i); yield return new WaitForSecondsRealtime(.4f);
                gallery.primary.onClick.Invoke(); Check(gallery.activations == i + 1, "Theme " + i + " primary action observes a single click");
                gallery.slider.value = .8f; Check(Mathf.Abs(gallery.progress.fillAmount - .8f) < .01f, "Theme " + i + " slider drives preview progress");
                gallery.toggle.isOn = true; Check(gallery.toggleLabel.text.EndsWith("ON"), "Theme " + i + " switch has a text state");
                gallery.input.text = "Preview only"; Check(gallery.input.text == "Preview only", "Theme " + i + " accepts sample text");
                var es = EventSystem.current; es.SetSelectedGameObject(gallery.primary.gameObject);
                Check(es.currentSelectedGameObject == gallery.primary.gameObject, "Theme " + i + " supports keyboard focus");
                var data = new PointerEventData(es) { position = RectTransformUtility.WorldToScreenPoint(null, gallery.primary.transform.position) };
                var hits = new List<RaycastResult>(); es.RaycastAll(data, hits);
                Check(hits.Count > 0 && hits[0].gameObject == gallery.primary.gameObject, "Theme " + i + " primary is reachable through graphic raycast");
                Canvas.ForceUpdateCanvases();
                foreach (var text in gallery.GetComponentsInChildren<TMPro.TMP_Text>())
                { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "Theme " + i + " text fits: " + text.name); }
                yield return Screenshot("gallery-theme-" + i);
                gallery.toggle.isOn = false; gallery.slider.value = .5f;
            }
            theme.Select(0); theme.reduceMotion = true; yield return null;
            Check(!ambient.IsMoving, "Reduce Motion stops decorative drift");
            var before = ambient.layers[0].rectTransform.anchoredPosition; yield return new WaitForSecondsRealtime(.1f);
            Check((before - ambient.layers[0].rectTransform.anchoredPosition).sqrMagnitude < .001f, "Reduced background remains stationary");
            theme.reduceTransparency = true; yield return null;
            Check(ambient.EffectiveQuality == 0 && !ambient.layers[0].enabled, "Solid panels turn off decorative translucency");
            theme.reduceTransparency = false; ambient.quality = 0; yield return null;
            Check(!ambient.layers[0].enabled, "Background Off tier draws no layer");
            ambient.quality = 1; yield return null;
            Check(ambient.layers[0].enabled && !ambient.layers[1].enabled, "Background Low tier draws one layer");
            gallery.reset.onClick.Invoke(); Check(gallery.activations == 0 && gallery.input.text.Length == 0, "Reset clears sandbox state only");
            // Re-create and destroy the actual dynamic fonts across a second
            // native scene load rather than relying on already cached assets.
            yield return SceneManager.LoadSceneAsync("GripUiGallery");
            yield return new WaitForSecondsRealtime(.5f);
            gallery = FindAnyObjectByType<GripUiGallery>();
            Check(gallery != null, "Gallery reopens after font owner cleanup");
            CheckRuntimeFonts(gallery.theme);
            Check(runtimeError == null, "Scene transitions log no runtime errors");
        }
        void CheckRuntimeFonts(GripUiThemeController theme)
        {
            var heading = theme.HeadingFont;
            var body = theme.BodyFont;
            Check(heading != null && body != null, "Native heading and body fonts initialize");
            Check(heading == theme.HeadingFont && body == theme.BodyFont, "Repeated font access reuses the existing atlases");
            Check(heading != body && heading.fallbackFontAssetTable != null &&
                heading.fallbackFontAssetTable.Count == 1 && heading.fallbackFontAssetTable[0] == body,
                "Heading contains one initialized body fallback");
        }
        IEnumerator Screenshot(string name)
        {
            yield return new WaitForEndOfFrame(); Directory.CreateDirectory("Logs/gallery-screenshots");
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
            File.WriteAllBytes("Logs/gallery-screenshots/" + name + ".png", texture.EncodeToPNG()); Destroy(texture);
        }
        void Finish(bool passed, string error)
        {
            if (runtimeError != null) { passed = false; error = runtimeError + "\n" + error; }
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/gallery-checks.json", JsonUtility.ToJson(new Result
            { passed = passed, error = error, checks = checks.ToArray(), width = Screen.width, height = Screen.height, utc = DateTime.UtcNow.ToString("O") }, true));
            Debug.Log("[UI_GALLERY] " + (passed ? "PASS" : "FAIL") + ": " + checks.Count + " " + error);
            Application.Quit(passed ? 0 : 1);
        }
        [Serializable] class Result { public bool passed; public string error, utc; public string[] checks; public int width, height; }
    }
}
