using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GripRehab.Editor
{
    public static class VisionCameraAuthoring
    {
        [MenuItem("Grip Rehab/Vision/Install Local Camera Controller")]
        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);
            var app = UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
            if (app == null || app.vision == null || app.settings == null)
                throw new Exception("Apartment vision references missing");
            var launcher = app.vision.GetComponent<VisionCameraLauncher>();
            if (launcher == null) launcher = app.vision.gameObject.AddComponent<VisionCameraLauncher>();
            launcher.cameraIndex = 0;
            launcher.mirror = true;
            launcher.autoStartOnPlay = false; // ApartmentExperience starts it after choosing camera mode.
            app.cameraLauncher = launcher;
            var preview = app.vision.GetComponent<VisionPreviewReceiver>();
            if (preview == null) preview = app.vision.gameObject.AddComponent<VisionPreviewReceiver>();
            var canvas = app.statusText.GetComponentInParent<Canvas>();
            if (canvas == null) throw new Exception("Apartment HUD Canvas is missing");
            var panel = canvas.transform.Find("Local camera preview");
            if (panel == null)
            {
                var panelObject = new GameObject("Local camera preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                panel = panelObject.transform;
                panel.SetParent(canvas.transform, false);
                var panelRect = (RectTransform)panel;
                panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(1, 0);
                panelRect.sizeDelta = new Vector2(290, 222);
                panelRect.anchoredPosition = new Vector2(-18, 86);
                panelObject.GetComponent<Image>().color = new Color(.025f, .07f, .11f, .94f);
                var video = new GameObject("Mirrored camera and 21 landmarks", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                video.transform.SetParent(panel, false);
                var videoRect = (RectTransform)video.transform;
                videoRect.anchorMin = Vector2.zero; videoRect.anchorMax = Vector2.one;
                videoRect.offsetMin = new Vector2(9, 29); videoRect.offsetMax = new Vector2(-9, -9);
                var label = new GameObject("Preview label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                label.transform.SetParent(panel, false);
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = new Vector2(1, 0);
                labelRect.offsetMin = new Vector2(10, 3); labelRect.offsetMax = new Vector2(-10, 27);
                var text = label.GetComponent<Text>(); text.text = "LOCAL CAMERA  /  LANDMARKS  /  V TO HIDE";
                text.font = app.statusText.font; text.fontSize = 12; text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(.78f, .94f, 1f);
            }
            preview.experience = app;
            preview.panel = panel.GetComponent<CanvasGroup>();
            preview.image = panel.GetComponentInChildren<RawImage>();
            preview.panel.alpha = 0; preview.panel.blocksRaycasts = false;
            app.settings.startWithCamera = true;
            app.settings.startWithHardware = false;
            EditorUtility.SetDirty(launcher);
            EditorUtility.SetDirty(preview);
            EditorUtility.SetDirty(app);
            EditorUtility.SetDirty(app.settings);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Verify();
            Debug.Log("[VISION_AUTHORING] PASS: camera mode is the saved default; local worker belongs to Play lifecycle");
        }

        [MenuItem("Grip Rehab/Vision/Verify Local Camera Controller")]
        public static void Verify()
        {
            var app = UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
            if (app == null || app.cameraLauncher == null || app.cameraLauncher != app.vision.GetComponent<VisionCameraLauncher>() ||
                !app.settings.startWithCamera || app.settings.startWithHardware ||
                UnityEngine.Object.FindObjectsByType<VisionCameraLauncher>(FindObjectsSortMode.None).Length != 1 ||
                UnityEngine.Object.FindObjectsByType<VisionPreviewReceiver>(FindObjectsSortMode.None).Length != 1 ||
                app.vision.GetComponent<VisionPreviewReceiver>().image == null)
                throw new Exception("Camera launcher/scene settings are incomplete");
        }
    }
}
