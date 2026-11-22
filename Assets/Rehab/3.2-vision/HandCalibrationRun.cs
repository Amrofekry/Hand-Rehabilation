using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GripRehab
{
    /// <summary>
    /// The guided CALIBRATE HAND run (2026-11-22): about 30 s of on-screen prompts (rest, far left, far right, up, down, near
    /// the camera, far from it), each held for 3.5 s, the first second ignored while the hand moves there. Hand look and hand
    /// actions pause meanwhile (reaching far left must not turn the view). The result is saved and used at once
    /// (ApartmentExperience.SetCalibration). Started from Settings, by voice ("calibrate hand"), or once on the first camera
    /// session without a calibration. Records numbers only, never an image.
    /// </summary>
    public sealed class HandCalibrationRun : MonoBehaviour
    {
        public static bool Running { get; private set; }
        const float PhaseSeconds = 3.5f, SettleSeconds = 1f;
        ApartmentExperience app; Text text; GameObject canvas;

        /// <summary>Start a calibration (ignored while one runs or without the camera hand's game).</summary>
        public static void Begin(ApartmentExperience app)
        {
            if (Running || app == null) return;
            Running = true; // at once, so a second request before the run starts is ignored
            var go = new GameObject("Hand calibration"); var run = go.AddComponent<HandCalibrationRun>(); run.app = app;
        }

        IEnumerator Start()
        {
            Running = true; HandLocomotion.Suspended = true;
            try
            {
                BuildText();
                if (!app.CameraMode) app.SetCameraMode();
                app.Release();
                Show("Hand calibration (30 s)\nShow your open hand to the camera, fingers up");
                float wait = Time.realtimeSinceStartup + 25;
                while (!Tracked() && Time.realtimeSinceStartup < wait) yield return null;
                if (!Tracked()) { Show("The camera does not see a hand. Calibration stopped; try again from Settings."); yield return new WaitForSecondsRealtime(4); yield break; }
                var cal = new HandCalibration();
                for (int phase = 0; phase < HandCalibration.Phases; phase++)
                {
                    float start = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - start < PhaseSeconds)
                    {
                        float t = Time.realtimeSinceStartup - start;
                        Show((phase + 1) + " / " + HandCalibration.Phases + "   " + HandCalibration.Prompts[phase] + "\n" + Mathf.CeilToInt(PhaseSeconds - t) + " s");
                        if (t >= SettleSeconds && Tracked())
                        {
                            var d = app.ActiveHand.LastImageDelta;
                            cal.Add(phase, d.x + .5f, .5f - d.y, app.Depth.RawLevel);
                        }
                        yield return null;
                    }
                }
                var r = cal.Solve();
                if (r.Any)
                {
                    app.SetCalibration(r);
                    Show("Calibrated: left / right / up / down " + (r.lateralValid ? "OK" : "kept as before (move further next time)") +
                         ", toward / away " + (r.depthValid ? "OK" : "kept as before (move nearer and farther next time)"));
                }
                else Show("Not enough movement was measured; the previous settings are kept. Try again from Settings.");
                yield return new WaitForSecondsRealtime(4);
            }
            finally
            {
                Running = false; HandLocomotion.Suspended = false;
                if (canvas != null) Destroy(canvas);
                Destroy(gameObject);
            }
        }

        bool Tracked() => app != null && app.ActiveHand != null && app.ActiveHand.TrackingUsed;
        void Show(string s) { if (text != null) text.text = s; }
        void BuildText()
        {
            canvas = new GameObject("Hand calibration canvas");
            var c = canvas.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 5001;
            var panel = new GameObject("Panel"); panel.transform.SetParent(canvas.transform, false);
            var img = panel.AddComponent<Image>(); img.color = new Color(.05f, .07f, .1f, .85f); img.raycastTarget = false;
            var rt = (RectTransform)panel.transform; rt.anchorMin = new Vector2(.12f, .76f); rt.anchorMax = new Vector2(.88f, .97f); rt.offsetMin = rt.offsetMax = Vector2.zero;
            var go = new GameObject("Text"); go.transform.SetParent(panel.transform, false);
            text = go.AddComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 30; text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            var tr = (RectTransform)go.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(20, 10); tr.offsetMax = new Vector2(-20, -10);
        }
    }
}
