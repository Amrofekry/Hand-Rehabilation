using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GripRehab
{
    // A local, visual menu input. No image or landmark is stored by this component.
    // The five-second dwell is deliberately independent of the measured-force input.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(600)]
    public sealed class RehabDwellPointer : MonoBehaviour
    {
        public VisionUdpReceiver vision;
        public VisionCameraLauncher cameraLauncher;
        public RehabAppShell shell;
        [Min(.5f)] public float dwellSeconds = 5f;
        [Min(0)] public float smoothingSeconds = .055f;
        [Range(0, .3f)] public float gestureGraceSeconds = .2f;
        [Range(0, 40)] public float targetMarginPixels = 24f;

        readonly List<RaycastResult> hits = new List<RaycastResult>(16);
        [NonSerialized] PointerEventData pointerEvent;
        GameObject overlay;
        RectTransform cursor;
        Image progress;
        Text hint;
        Text status;
        Selectable target;
        RehabHandSurface captured;
        int targetToken;
        Vector2 position;
        readonly RehabDwellGate dwell = new RehabDwellGate();
        readonly RehabPointerRearm rearm = new RehabPointerRearm();
        [NonSerialized] EventSystem eventSystem;
        int screenWidth, screenHeight;
        bool? lastLeft;
        float gestureInterruption;
        float nextLaunch;
        bool hasPosition;
        bool startedForUi;
        // The acceptance runner injects loopback landmarks without opening a
        // physical webcam. This is never serialized into the authored scene.
        internal bool testNoCameraLaunch;
        public string HoveredTargetName => target != null ? target.name : "";
        public float DwellProgress => Mathf.Clamp01(dwell.Elapsed / RehabPointerPolicy.Duration(dwellSeconds));
        public bool PointingDetected { get; private set; }
        public int ActivationCount { get; private set; }

        public static bool IsPointing(LandmarkFrame frame) => RehabPointerPolicy.IsPointing(frame);
        public static LandmarkFrame ChooseFrame(LandmarkFrame right, bool rightValid,
            LandmarkFrame left, bool leftValid, bool? preferredLeft = null) =>
            RehabPointerPolicy.ChooseFrame(right, rightValid, left, leftValid, preferredLeft);

        void Awake()
        {
            overlay = new GameObject("MediaPipe dwell pointer", typeof(RectTransform), typeof(Canvas));
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 3000;
            var marker = new GameObject("Palm cursor and five-second progress", typeof(RectTransform));
            marker.transform.SetParent(overlay.transform, false);
            cursor = marker.GetComponent<RectTransform>();
            cursor.anchorMin = cursor.anchorMax = Vector2.zero;
            cursor.pivot = new Vector2(.5f, .5f);
            cursor.sizeDelta = new Vector2(58, 58);
            var bg = marker.AddComponent<Image>();
            bg.sprite = RingSprite();
            bg.color = new Color(.14f, .30f, .33f, .85f);
            bg.raycastTarget = false;
            var fill = new GameObject("Dwell progress", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(marker.transform, false);
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            progress = fill.GetComponent<Image>();
            progress.sprite = bg.sprite;
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Radial360;
            progress.fillOrigin = (int)Image.Origin360.Top;
            progress.fillClockwise = true;
            progress.color = new Color(.19f, .91f, .74f, 1);
            progress.raycastTarget = false;
            var dot = new GameObject("Pointer centre", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(marker.transform, false);
            var dotRect = dot.GetComponent<RectTransform>();
            dotRect.anchorMin = dotRect.anchorMax = dotRect.pivot = new Vector2(.5f, .5f);
            dotRect.sizeDelta = new Vector2(10, 10);
            var dotImage = dot.GetComponent<Image>();
            dotImage.color = Color.white; dotImage.raycastTarget = false;
            var label = new GameObject("Dwell instruction", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(marker.transform, false);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(.5f, .5f);
            labelRect.anchoredPosition = new Vector2(0, -43);
            labelRect.sizeDelta = new Vector2(210, 30);
            hint = label.GetComponent<Text>();
            hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hint.fontSize = 16; hint.alignment = TextAnchor.MiddleCenter;
            hint.color = Color.white; hint.raycastTarget = false;
            var statusObject = new GameObject("Camera pointer status", typeof(RectTransform), typeof(Text));
            statusObject.transform.SetParent(overlay.transform, false);
            var statusRect = statusObject.GetComponent<RectTransform>();
            statusRect.anchorMin = statusRect.anchorMax = new Vector2(.5f, 0);
            statusRect.pivot = new Vector2(.5f, 0);
            statusRect.anchoredPosition = new Vector2(0, 24);
            statusRect.sizeDelta = new Vector2(620, 42);
            status = statusObject.GetComponent<Text>();
            status.font = hint.font; status.fontSize = 18;
            status.alignment = TextAnchor.MiddleCenter;
            status.color = new Color(.92f, 1f, .98f, 1f);
            status.raycastTarget = false;
            marker.SetActive(false);
        }

        void Start()
        {
            var presentation = FindAnyObjectByType<GripRehab.UI.GripUiThemeController>();
            if (presentation != null) presentation.RegisterPresentation(overlay.transform);
        }

        static Sprite RingSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Dwell ring (runtime)";
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f),
                        new Vector2(size / 2f, size / 2f));
                    float alpha = Mathf.Clamp01(distance - 22f) * Mathf.Clamp01(30f - distance);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }

        void Update()
        {
            bool optionsOpen = shell != null && shell.CurrentPage == "Game" &&
                shell.experience != null && shell.experience.player != null && shell.experience.player.Paused;
            if (shell == null || !shell.isActiveAndEnabled || (!shell.BlocksGameplay && !optionsOpen) ||
                vision == null || cameraLauncher == null || EventSystem.current == null)
            {
                if (startedForUi && cameraLauncher != null &&
                    (shell == null || shell.experience == null || !shell.experience.CameraMode))
                    cameraLauncher.StopWorker();
                startedForUi = false;
                Hide(); if (status != null) status.enabled = false; return;
            }
            if (!Application.isFocused) { Hide(); status.enabled = false; return; }
            status.enabled = true;
            // Script reload can discard PointerEventData while retaining a Unity
            // object reference. Rebuild the pair whenever either cache is lost.
            if (eventSystem != EventSystem.current || pointerEvent == null)
            {
                Hide(); eventSystem = EventSystem.current;
                pointerEvent = new PointerEventData(eventSystem) { pointerId = -100, button = PointerEventData.InputButton.Left };
            }
            if (screenWidth != Screen.width || screenHeight != Screen.height)
            { Hide(); screenWidth = Screen.width; screenHeight = Screen.height; }
            if (!testNoCameraLaunch && !cameraLauncher.IsRunning && Time.unscaledTime >= nextLaunch)
            {
                nextLaunch = Time.unscaledTime + 5f;
                cameraLauncher.EnsureStarted();
                startedForUi = cameraLauncher.IsRunning;
            }
            LandmarkFrame frame = ChooseFrame(vision.LatestRight, vision.RightValid,
                                              vision.LatestLeft, vision.LeftValid, lastLeft);
            if (frame == null)
            {
                status.text = !string.IsNullOrEmpty(vision.PortProblem) ? vision.PortProblem.ToUpperInvariant() + "  /  MOUSE AND KEYBOARD STILL WORK" :
                    cameraLauncher.State == "FAILED" || cameraLauncher.State == "camera_error" ?
                    "CAMERA UNAVAILABLE  /  USE MOUSE OR KEYBOARD" : cameraLauncher.IsRunning ?
                    "CAMERA READY  /  SHOW YOUR HAND TO SELECT" :
                    "CAMERA STARTING  /  MOUSE AND KEYBOARD ALSO WORK";
                Hide(); return;
            }
            bool pointing = IsPointing(frame);
            PointingDetected = pointing;
            bool left = HandKinematics.IsLeft(frame);
            if (lastLeft.HasValue && lastLeft.Value != left) { SetTarget(null); dwell.Reset(); rearm.Reset(); gestureInterruption = 0; hasPosition = false; }
            lastLeft = left;

            float dt = RehabPointerPolicy.SafeDt(Time.unscaledDeltaTime);
            gestureInterruption = pointing ? 0 : gestureInterruption + dt;
            var desired = RehabPointerPolicy.PalmScreenPoint(frame, Screen.width, Screen.height);
            bool pausePosition = !pointing && target != null &&
                gestureInterruption <= RehabPointerPolicy.Grace(gestureGraceSeconds);
            if (!pausePosition) position = RehabPointerPolicy.Smooth(position, desired, hasPosition, dt, smoothingSeconds);
            hasPosition = true;
            cursor.gameObject.SetActive(true);
            cursor.anchoredPosition = position;
            pointerEvent.Reset();
            pointerEvent.position = position;
            pointerEvent.button = PointerEventData.InputButton.Left;
            hits.Clear();
            EventSystem.current.RaycastAll(pointerEvent, hits);
            Selectable next = null;
            RehabTracePad trace = null;
            RehabHandSurface surface = null;
            bool blockedByControl = false;
            // Respect the frontmost graphic. Never click a control behind a
            // modal/background/disabled control just because it appears later.
            if (hits.Count > 0)
            {
                var hit = hits[0].gameObject;
                next = hit.GetComponentInParent<Selectable>();
                blockedByControl = next != null;
                if (next != null && (!next.IsActive() || !next.IsInteractable() ||
                    !(next is Button || next is Dropdown || next is Toggle || next is TMPro.TMP_Dropdown || next is InputField || next is TMPro.TMP_InputField))) next = null;
                trace = hit.GetComponentInParent<RehabTracePad>();
                surface = hit.GetComponentInParent<RehabHandSurface>();
            }
            if (pointing && trace != null && shell.CurrentPage == "Activity")
                trace.SubmitScreenPoint(position, null);
            if (next == null && !blockedByControl && target != null && target.IsActive() && target.IsInteractable() &&
                NearTarget(target, position, float.IsFinite(targetMarginPixels) ? Mathf.Clamp(targetMarginPixels, 0, 40) : 24) && SameSurface(target, hits)) next = target;
            SetTarget(next);
            bool deliberate = rearm.CanSelect(position, pointing, dt);
            bool activate = dwell.Step(target != null ? targetToken : 0, pointing && deliberate,
                dt, RehabPointerPolicy.Duration(dwellSeconds), RehabPointerPolicy.Grace(gestureGraceSeconds));
            progress.fillAmount = Mathf.Clamp01(DwellProgress);
            status.text = !pointing ? "HAND TRACKING PAUSED" :
                target == null ? "HAND POINTER READY  /  MOVE YOUR HAND TO A BUTTON" :
                (dwell.Latched || rearm.Waiting) ? "SELECTED  /  MOVE YOUR HAND TO SELECT AGAIN" : "SELECTING  /  HOLD YOUR HAND STEADY";
            hint.text = !pointing ? "SHOW YOUR HAND" : target == null ? "POINT AT A BUTTON" :
                (dwell.Latched || rearm.Waiting) ? "SELECTED" : "HOLD " + Mathf.CeilToInt(Mathf.Max(0, RehabPointerPolicy.Duration(dwellSeconds) - dwell.Elapsed)) + "s";
            // Hand-played boards (assembly pieces, maze marker). A carried piece or held marker keeps
            // following the hand even when it leaves the board or passes over a button.
            if (captured != null && !captured.Captures) captured = null;
            var board = captured != null ? captured : next == null ? surface : null;
            if (board != null && shell.CurrentPage == "Activity")
            {
                var feedback = board.Submit(position, null, pointing, dt, RehabPointerPolicy.Duration(dwellSeconds));
                captured = board.Captures ? board : null;
                if (feedback.Active && (target == null || captured != null))
                {
                    if (captured != null) { SetTarget(null); dwell.Reset(); }
                    progress.fillAmount = Mathf.Clamp01(feedback.Progress);
                    hint.text = feedback.Hint; status.text = feedback.Status;
                    return;
                }
            }
            if (activate && target != null && target.IsActive() && target.IsInteractable())
            {
                var selected = target;
                // Use the same uGUI activation handler as a left mouse click.
                // Record the latch before callbacks can navigate or disable us.
                rearm.Activated(position);
                if (ExecuteEvents.Execute(selected.gameObject, pointerEvent, ExecuteEvents.pointerClickHandler))
                    ActivationCount++;
                // Keep the activated target latched while the hand remains on it.
                // Navigation can hide it; the next raycast then clears the latch.
            }
        }

        void Hide()
        {
            if (cursor != null) cursor.gameObject.SetActive(false);
            SetTarget(null); dwell.Reset(); rearm.Reset(); lastLeft = null; PointingDetected = false; captured = null;
            gestureInterruption = 0;
            hasPosition = false;
        }

        // A mouse/menu action can reveal a new button under a stationary hand.
        public void SuppressAfterExternalSelection()
        {
            SetTarget(null);dwell.Reset();
            if(hasPosition)rearm.Activated(position);
        }

        void SetTarget(Selectable next)
        {
            if (target == next) return;
            if (pointerEvent != null && target != null)
                ExecuteEvents.Execute(target.gameObject, pointerEvent, ExecuteEvents.pointerExitHandler);
            target = next;
            unchecked { targetToken++; if (targetToken == 0) targetToken++; }
            if (pointerEvent != null && target != null)
                ExecuteEvents.Execute(target.gameObject, pointerEvent, ExecuteEvents.pointerEnterHandler);
        }
        static bool NearTarget(Selectable item, Vector2 point, float margin)
        {
            var rect = item.transform as RectTransform;
            var canvas = item.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (rect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, point, camera, out var local)) return false;
            float scaledMargin = margin / Mathf.Max(.01f, canvas != null ? canvas.scaleFactor : 1);
            var bounds = rect.rect; bounds.xMin -= scaledMargin; bounds.xMax += scaledMargin;
            bounds.yMin -= scaledMargin; bounds.yMax += scaledMargin; return bounds.Contains(local);
        }
        static bool SameSurface(Selectable item, List<RaycastResult> results)
        {
            if (results.Count == 0) return false;
            var top = results[0].gameObject.transform;
            // A surrounding card can retain a nearby target; a sibling modal
            // scrim cannot. This does not enlarge the actual mouse hit region.
            return item.transform.IsChildOf(top) || top.IsChildOf(item.transform);
        }

        void OnDisable()
        {
            Hide(); if (status != null) status.enabled = false;
            // Release only a worker started for menus; gameplay retains its camera.
            if (startedForUi && cameraLauncher != null &&
                (shell == null || shell.experience == null || !shell.experience.CameraMode))
                cameraLauncher.StopWorker();
            startedForUi = false;
        }

        void OnDestroy()
        {
            if (progress != null && progress.sprite != null)
            {
                var sprite = progress.sprite;
                Destroy(sprite.texture);
                Destroy(sprite);
            }
            if (overlay != null) Destroy(overlay);
        }
    }
}
