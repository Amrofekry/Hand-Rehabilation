using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GripRehab
{
    // Scene-bound application: authored references survive editing, play and builds.
    public sealed class RehabApp : MonoBehaviour
    {
        [SerializeField, Tooltip("Shared demonstration settings, editable outside Play mode.")]
        private RehabSettings _settings;
        public RehabSettings Settings => _settings;
        private readonly DemoForceSource _demoForce = new DemoForceSource();
        private readonly SessionClock _clock = new SessionClock();
        [SerializeField] private RehabEnvironment _environment = new RehabEnvironment();
        [SerializeField] private GripDeformation _deformation = new GripDeformation();
        [SerializeField] private ModuleVisuals _moduleVisuals = new ModuleVisuals();
        private TrainingMode _mode = TrainingMode.Force;
        private RoomKind _room = RoomKind.LivingRoom;
        private GripShape _shape = GripShape.Sphere;
        private ForceHoldTask _forceTask;
        private SequenceMemoryTask _memoryTask;
        private SpatialAssemblyTask _assemblyTask;
        private TraceTask _traceTask;
        private MazeTask _mazeTask;
        [SerializeField] private VisionUdpReceiver _vision;
        [SerializeField] private LiveForceReceiver _liveForce;
        private bool _useLiveForce;
        private int _seenForceGeneration;
        private int _processedForceSamples;
        private long _processedSampleMs;
        [SerializeField] private GameObject _hand;
        [SerializeField] private Transform[] _fingerRoots = new Transform[4];
        private bool _previewPointer;
        [SerializeField] private Text _title;
        [SerializeField] private Text _subtitle;
        [SerializeField] private Text _instructions;
        [SerializeField] private Text _metrics;
        [SerializeField] private Text _center;
        [SerializeField] private Text _inputStatus;
        [SerializeField] private Text _startLabel;
        [SerializeField] private Text _pauseLabel;
        [SerializeField] private Image[] _traceMarkers;
        [SerializeField] private Font _font;
        [SerializeField] private Font _monoFont;
        private double _memoryRevealUntil;
        private float _nextUiUpdate;

        private void Awake()
        {
            if (!Application.isPlaying) return;
            if (_settings == null || _hand == null || _title == null || _liveForce == null || _vision == null)
            { Debug.LogError("Grip Rehab scene references are incomplete. Open Assets/Scenes/GripRehab.unity."); enabled=false; return; }
            _room=_settings.initialRoom; _shape=_settings.initialShape;
            _environment.Build(_room); _deformation.SetShape(_shape);
            ResetModule(); RefreshUI();
        }

#if UNITY_EDITOR
        // Explicit one-time authoring; never invoked by builds or normal play.
        public void AuthorScene(RehabSettings settings)
        {
            _settings=settings;
            _font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _monoFont=Font.CreateDynamicFontFromOSFont("Consolas",18);
            PrepareCamera(); _environment.AuthorRooms(); _deformation.AuthorGrips();
            _hand=BuildHand();
            _vision=gameObject.AddComponent<VisionUdpReceiver>();
            _liveForce=gameObject.AddComponent<LiveForceReceiver>();
            BuildUI(); _moduleVisuals.AuthorModules(); ResetModule(); RefreshUI();
        }
#endif
        public void InvokeCommand(string command)
        {
            switch(command)
            {
                case "Force Interaction": SelectMode(TrainingMode.Force); break;
                case "Sequence Memory": SelectMode(TrainingMode.Memory); break;
                case "Spatial Assembly": SelectMode(TrainingMode.Assembly); break;
                case "Visual Tracing": SelectMode(TrainingMode.Tracing); break;
                case "Maze Navigation": SelectMode(TrainingMode.Maze); break;
                case "Living Room / Kitchen": _room=_room==RoomKind.LivingRoom?RoomKind.Kitchen:RoomKind.LivingRoom; _environment.Build(_room); RefreshUI(); break;
                case "Sphere / Cylinder / Pencil": if(!_useLiveForce) { _shape=_shape==GripShape.Pencil?GripShape.Sphere:_shape+1; _deformation.SetShape(_shape); _deformation.SetVisible(_mode==TrainingMode.Force); RefreshUI(); } break;
                case "Force: Keyboard Demo": SelectForceSource(false); break;
                case "Force: BLE Device": SelectForceSource(true); break;
                case "Start": StartSession(); break;
                case "Pause": TogglePause(); break;
                default: if(int.TryParse(command,out var token)&&token>=1&&token<=4) SubmitPiece(token-1); break;
            }
        }

        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-rehab-smoke") >= 0) StartCoroutine(SmokeTest());
            if (Array.IndexOf(args, "-rehab-vision-smoke") >= 0) StartCoroutine(VisionSmokeTest());
            if (Array.IndexOf(args, "-rehab-force-smoke") >= 0)
            {
                SelectForceSource(true);
                StartCoroutine(ForceSmokeTest());
            }
            foreach (var arg in args)
            {
                const string modePrefix = "-rehab-preview-mode=";
                const string roomPrefix = "-rehab-preview-room=";
                if (arg.StartsWith(modePrefix, StringComparison.OrdinalIgnoreCase) &&
                    Enum.TryParse(arg.Substring(modePrefix.Length), true, out TrainingMode previewMode))
                    SelectMode(previewMode);
                if (arg.StartsWith(roomPrefix, StringComparison.OrdinalIgnoreCase) &&
                    Enum.TryParse(arg.Substring(roomPrefix.Length), true, out RoomKind previewRoom))
                {
                    _room = previewRoom;
                    _environment.Build(_room);
                    RefreshUI();
                }
            }
            _previewPointer = Array.IndexOf(args, "-rehab-screenshot") >= 0;
            if (_previewPointer) StartCoroutine(CapturePreview());
        }

        private IEnumerator SmokeTest()
        {
            yield return null;
            yield return null;
            if (_title == null || _hand == null || _forceTask == null || _vision == null)
            {
                Debug.LogError("[REHAB_SMOKE] FAIL: runtime composition incomplete");
                Application.Quit(1);
                yield break;
            }
            foreach (TrainingMode mode in Enum.GetValues(typeof(TrainingMode)))
            {
                SelectMode(mode);
                StartSession();
                yield return null;
            }
            Debug.Log("[REHAB_SMOKE] PASS: boot, UI, five exercise modes and runtime state");
            Application.Quit(0);
        }

        private IEnumerator VisionSmokeTest()
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 8;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (_vision != null && _vision.TrackingValid && _vision.Latest.source == "synthetic")
                {
                    Debug.Log("[REHAB_VISION_SMOKE] PASS: synthetic UDP landmarks reached Unity and passed validation");
                    Application.Quit(0);
                    yield break;
                }
                yield return null;
            }
            Debug.LogError("[REHAB_VISION_SMOKE] FAIL: no valid synthetic landmarks received");
            Application.Quit(1);
        }

        private IEnumerator ForceSmokeTest()
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 8;
            while (Time.realtimeSinceStartupAsDouble < deadline && !_liveForce.Latest.Valid)
                yield return null;
            if (!_liveForce.Latest.Valid)
            {
                Debug.LogError("[REHAB_FORCE_SMOKE] FAIL: no validated force packet");
                Application.Quit(1);
                yield break;
            }
            StartSession();
            while (Time.realtimeSinceStartupAsDouble < deadline && _clock.State != SessionState.Complete)
                yield return null;
            if (_clock.State == SessionState.Complete && _liveForce.Latest.Simulated)
            {
                Debug.Log("[REHAB_FORCE_SMOKE] PASS: verified synthetic Phase 2 packets completed force hold");
                Application.Quit(0);
            }
            else
            {
                Debug.LogError($"[REHAB_FORCE_SMOKE] FAIL: state={_clock.State} progress={_forceTask.ProgressSeconds:F2} samples={_liveForce.State.SampleCount} force={_liveForce.Latest.Valid} status={_liveForce.State.Status}");
                Application.Quit(1);
            }
        }

        private IEnumerator CapturePreview()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"rehab-preview-{_mode.ToString().ToLowerInvariant()}.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(2f);
            Debug.Log($"[REHAB_PREVIEW] {path}");
            Application.Quit(0);
        }

        private void PrepareCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }
            camera.transform.position = new Vector3(0, 2.45f, -6.5f);
            camera.transform.LookAt(new Vector3(0, 1.2f, 1f));
            camera.fieldOfView = 56;
            camera.clearFlags = CameraClearFlags.Skybox;
            RehabVisualQuality.Configure(camera);
        }

        private GameObject BuildHand()
        {
            var root = new GameObject("Articulated virtual hand");
            root.transform.position = new Vector3(.62f, 1.65f, -.22f);
            root.transform.rotation = Quaternion.Euler(55f, 24f, -12f);
            root.transform.localScale = Vector3.one * 1.27f;
            HandPart(root.transform, "Wrist", new Vector3(0, 0, -.21f),
                new Vector3(.24f, .15f, .29f), new Color(.77f, .52f, .38f));
            HandPart(root.transform, "Palm", new Vector3(0, 0, .06f),
                new Vector3(.43f, .18f, .42f), new Color(.82f, .58f, .43f));
            for (var i = 0; i < 4; i++)
            {
                var x = -.145f + .095f * i;
                var finger = new GameObject($"Finger {i + 1} joint").transform;
                finger.SetParent(root.transform, false);
                finger.localPosition = new Vector3(x, .035f, .23f);
                _fingerRoots[i] = finger;
                var length = i == 0 || i == 3 ? .85f : 1f;
                HandPart(finger, "Proximal phalanx", new Vector3(0, 0, .11f * length),
                    new Vector3(.083f, .078f, .27f * length), new Color(.83f, .59f, .44f));
                HandPart(finger, "Middle phalanx", new Vector3(0, 0, .30f * length),
                    new Vector3(.074f, .07f, .20f * length), new Color(.84f, .6f, .45f));
                HandPart(finger, "Fingertip", new Vector3(0, 0, .43f * length),
                    new Vector3(.068f, .065f, .14f * length), new Color(.85f, .61f, .46f));
                HandPart(finger, "Nail", new Vector3(0, .05f, .455f * length),
                    new Vector3(.047f, .009f, .073f), new Color(.91f, .78f, .69f));
            }
            var thumb = new GameObject("Opposed thumb").transform;
            thumb.SetParent(root.transform, false);
            thumb.localPosition = new Vector3(-.25f, .02f, .04f);
            thumb.localRotation = Quaternion.Euler(0, -48f, 0);
            HandPart(thumb, "Thumb base", new Vector3(0, 0, .09f),
                new Vector3(.1f, .09f, .24f), new Color(.82f, .58f, .43f));
            HandPart(thumb, "Thumb tip", new Vector3(0, 0, .24f),
                new Vector3(.09f, .085f, .15f), new Color(.84f, .6f, .45f));
            return root;
        }

        private static void HandPart(Transform parent, string name, Vector3 position,
            Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            RehabMaterials.Apply(part.GetComponent<Renderer>(), color);
        }

        private void Update()
        {
            _demoForce.Shape = _shape;
            _demoForce.Step(Time.realtimeSinceStartupAsDouble, Time.deltaTime);
            var force = _useLiveForce ? _liveForce.Latest : _demoForce.Latest;
            if (_useLiveForce && _seenForceGeneration != _liveForce.State.Generation)
            {
                _seenForceGeneration = _liveForce.State.Generation;
                _clock.Reset();
                _forceTask.Reset();
            }
            if (_useLiveForce && _liveForce.Latest.Valid && _shape != _liveForce.State.Shape)
            {
                _shape = _liveForce.State.Shape;
                _deformation.SetShape(_shape);
            }
            if (_useLiveForce && _mode == TrainingMode.Force && _clock.State == SessionState.Running &&
                !force.Valid)
            {
                _clock.Pause();
                _forceTask.Reset();
            }
            _clock.Step(Time.deltaTime);
            _deformation.Step(_mode == TrainingMode.Force && _clock.State == SessionState.Running ?
                (force.Valid ? force.Newtons : 0) : 0, Time.deltaTime);

            var mouse = Input.mousePosition;
            var pointer = _previewPointer ? new Vector3(.64f, .55f, 0) :
                _vision != null && _vision.TrackingValid ?
                HandKinematics.ToViewport(_vision.Latest.landmarks[8]) :
                mouse.x < 1f && mouse.y < 1f ? new Vector3(.63f, .55f, 0) :
                new Vector3(mouse.x / Screen.width, mouse.y / Screen.height, 0);
            _hand.transform.position = Vector3.Lerp(_hand.transform.position,
                new Vector3(Mathf.Lerp(-2.2f, 2.2f, pointer.x), Mathf.Lerp(.9f, 2.2f, pointer.y), -.1f),
                1 - Mathf.Exp(-12f * Time.deltaTime));
            var curl = Mathf.Clamp01((force.Valid ? force.Newtons : 0) / 38f);
            foreach (var finger in _fingerRoots)
                finger.localRotation = Quaternion.Slerp(finger.localRotation,
                    Quaternion.Euler(48f * curl, 0, 0), 1 - Mathf.Exp(-8f * Time.deltaTime));

            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if (Input.GetKeyDown(KeyCode.R)) StartSession();
            if (_clock.State == SessionState.Running)
            {
                switch (_mode)
                {
                    case TrainingMode.Force:
                        if (_useLiveForce)
                        {
                            if (force.Valid && _liveForce.State.SampleCount != _processedForceSamples)
                            {
                                var sampleMs = _liveForce.State.SampleMs;
                                var interval = (uint)((uint)sampleMs - (uint)_processedSampleMs) / 1000f;
                                _forceTask.Step(force, Mathf.Min(interval, .1f));
                                _processedSampleMs = sampleMs;
                                _processedForceSamples = _liveForce.State.SampleCount;
                            }
                        }
                        else _forceTask.Step(force, Time.deltaTime);
                        if (_forceTask.Complete) CompleteSession();
                        break;
                    case TrainingMode.Memory:
                        if (Time.realtimeSinceStartupAsDouble > _memoryRevealUntil)
                        {
                            for (var i = 0; i < 4; i++)
                                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) SubmitMemory(i);
                        }
                        break;
                    case TrainingMode.Assembly:
                        for (var i = 0; i < 4; i++)
                            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) SubmitPiece(i);
                        break;
                    case TrainingMode.Tracing:
                        if (Input.GetMouseButton(0))
                        {
                            _traceTask.Submit(pointer);
                            if (_traceTask.Complete) CompleteSession();
                        }
                        break;
                    case TrainingMode.Maze:
                        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) MoveMaze(-1, 0);
                        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MoveMaze(1, 0);
                        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) MoveMaze(0, -1);
                        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) MoveMaze(0, 1);
                        break;
                }
            }
            if (Time.unscaledTime >= _nextUiUpdate)
            {
                _nextUiUpdate = Time.unscaledTime + .08f;
                RefreshUI();
            }
        }

        private void MoveMaze(int dx, int dy)
        {
            _mazeTask.Move(dx, dy);
            _moduleVisuals.UpdateMaze(_mazeTask.X, _mazeTask.Y);
            if (_mazeTask.Complete) CompleteSession();
        }

        private void SubmitMemory(int token)
        {
            if (_mode != TrainingMode.Memory || _clock.State != SessionState.Running ||
                Time.realtimeSinceStartupAsDouble <= _memoryRevealUntil) return;
            _memoryTask.Submit(token);
            if (_memoryTask.Complete) CompleteSession();
        }

        private void SubmitPiece(int piece)
        {
            if (_clock.State != SessionState.Running) return;
            if (_mode == TrainingMode.Memory) SubmitMemory(piece);
            else if (_mode == TrainingMode.Assembly)
            {
                if (_assemblyTask.Place(piece))
                    _moduleVisuals.UpdateAssembly(_assemblyTask.Placed, piece);
                if (_assemblyTask.Complete) CompleteSession();
            }
        }

        private void ResetModule()
        {
            _forceTask = new ForceHoldTask(_settings.targetNewtons, _settings.toleranceNewtons, _settings.holdSeconds);
            _memoryTask = new SequenceMemoryTask(2026 + DateTime.UtcNow.DayOfYear);
            _assemblyTask = new SpatialAssemblyTask();
            _traceTask = new TraceTask();
            _mazeTask = new MazeTask();
            _memoryRevealUntil = 0;
            _deformation.SetVisible(_mode == TrainingMode.Force);
            _moduleVisuals.Show(_mode, _mazeTask);
        }

        private void StartSession()
        {
            if (_mode == TrainingMode.Force && _useLiveForce &&
                (!_liveForce.Latest.Valid || _liveForce.State.Shape != _shape))
            {
                RefreshUI();
                return;
            }
            ResetModule();
            _clock.Start();
            _seenForceGeneration = _liveForce.State.Generation;
            _processedForceSamples = _liveForce.State.SampleCount;
            _processedSampleMs = _liveForce.State.SampleMs;
            if (_mode == TrainingMode.Memory)
                _memoryRevealUntil = Time.realtimeSinceStartupAsDouble + _settings.memoryRevealSeconds;
            RefreshUI();
        }

        private void CompleteSession()
        {
            _clock.Complete();
            Debug.Log($"Rehab session completed: {_mode}; elapsed={_clock.ElapsedSeconds:F1}s; simulated={(_useLiveForce ? _liveForce.Latest.Simulated : true)}");
            RefreshUI();
        }

        private void TogglePause()
        {
            if (_clock.State == SessionState.Running) _clock.Pause();
            else if (_clock.State == SessionState.Paused &&
                     (!_useLiveForce || _mode != TrainingMode.Force || _liveForce.Latest.Valid)) _clock.Resume();
            RefreshUI();
        }

        private void SelectMode(TrainingMode mode)
        {
            _mode = mode;
            _clock.Reset();
            ResetModule();
            RefreshUI();
        }

        private void SelectForceSource(bool live)
        {
            _useLiveForce = live;
            _seenForceGeneration = _liveForce.State.Generation;
            if (live && _liveForce.Latest.Valid)
            {
                _shape = _liveForce.State.Shape;
                _deformation.SetShape(_shape);
            }
            _clock.Reset();
            ResetModule();
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (_title == null) return;
            _title.text = "GRIP / REHAB";
            _subtitle.text = $"{_mode.ToString().ToUpperInvariant()}  •  {_room}  •  {_shape}";
            _inputStatus.text = _vision != null && _vision.TrackingValid ?
                "HAND TRACKING: " + (_vision.Latest.source == "synthetic" ? "synthetic replay" : "external landmarks (unverified)") :
                "HAND TRACKING: mouse demo / no camera model";
            _startLabel.text = _clock.State == SessionState.Running ? "RESTART SESSION" : "START SESSION";
            _pauseLabel.text = _clock.State == SessionState.Paused ? "RESUME" : "PAUSE";
            _center.font = _mode == TrainingMode.Maze ? _monoFont : _font;
            _center.fontSize = _mode == TrainingMode.Maze ? 28 : 23;
            if (_traceMarkers != null)
                for (var i = 0; i < _traceMarkers.Length; i++)
                {
                    _traceMarkers[i].gameObject.SetActive(_mode == TrainingMode.Tracing);
                    _traceMarkers[i].color = i < _traceTask.NextWaypoint ? new Color(.36f, .9f, .62f) :
                        i == _traceTask.NextWaypoint ? new Color(1f, .76f, .3f) : new Color(.38f, .69f, .78f);
                }
            var forceReading = _useLiveForce ? _liveForce.Latest : _demoForce.Latest;
            var source = _useLiveForce ? (_liveForce.State.IsSynthetic ? "SYNTHETIC REPLAY" : "BLE HARDWARE") : "KEYBOARD DEMO";
            var status = $"STATE  {_clock.State.ToString().ToUpperInvariant()}\nTIME   {_clock.ElapsedSeconds:F1} s\nSOURCE {source}";
            switch (_mode)
            {
                case TrainingMode.Force:
                    _instructions.text = _useLiveForce ?
                        "BLE input requires a calibrated shape and USB start.\nNo force progress while disconnected or stale." :
                        "Hold SPACE to squeeze. UP / DOWN adjusts demo force.\nKeep force inside the configured target range.";
                    _metrics.text = $"{status}\n\nFORCE   {(forceReading.Valid ? forceReading.Newtons.ToString("F1") : "--")} N\nTARGET  {_forceTask.TargetNewtons:F1} ± {_forceTask.ToleranceNewtons:F1} N\nHOLD    {_forceTask.ProgressSeconds:F1} / {_forceTask.HoldSeconds:F1} s\nPEAK    {_forceTask.PeakNewtons:F1} N";
                    if (_useLiveForce) _metrics.text += $"\nSHAPE   {_liveForce.State.Shape}  CAL {_liveForce.State.CalibrationRevision}\nSAMPLES {_liveForce.State.SampleCount}  AGE {_liveForce.State.LastAgeMs} ms\n{_liveForce.State.Status}";
                    _center.text = _clock.State == SessionState.Complete ? "HOLD COMPLETE" :
                        _clock.State == SessionState.Running ? $"{_forceTask.ProgressSeconds / _forceTask.HoldSeconds:P0}  HOLD PROGRESS" : "PRESS START TO BEGIN";
                    break;
                case TrainingMode.Memory:
                    _instructions.text = "Watch the four-number sequence, then repeat it.\nUse keys 1–4 or the number buttons.";
                    _metrics.text = $"{status}\n\nCORRECT {_memoryTask.Correct} / {_memoryTask.Length}\nRESETS  {_memoryTask.Mistakes}";
                    _center.text = _clock.State == SessionState.Running &&
                        Time.realtimeSinceStartupAsDouble <= _memoryRevealUntil ?
                        "REMEMBER  " + _memoryTask.SequenceDisplay :
                        _clock.State == SessionState.Complete ? "SEQUENCE COMPLETE" :
                        _clock.State == SessionState.Ready ? "PRESS START TO SEE THE SEQUENCE" :
                        "REPEAT THE SEQUENCE: 1  2  3  4";
                    break;
                case TrainingMode.Assembly:
                    _instructions.text = "Place pieces 1–4 into the shown slot order.\nUse the numbered buttons or keys; mistakes count but do not erase progress.";
                    _metrics.text = $"{status}\n\nPIECES {_assemblyTask.Placed} / 4\nERRORS {_assemblyTask.Errors}";
                    _center.text = $"TARGET  {_assemblyTask.TargetDisplay}\nPLACED  {_assemblyTask.ProgressDisplay}";
                    break;
                case TrainingMode.Tracing:
                    _instructions.text = "Hold left mouse button and visit the waypoints in order.\nThis demo tracks pointer accuracy, not measured hand ROM.";
                    _metrics.text = $"{status}\n\nWAYPOINT {_traceTask.NextWaypoint} / {TraceTask.Path.Length}\nACCURACY {_traceTask.Accuracy:P0}";
                    _center.text = _clock.State == SessionState.Complete ? "TRACE COMPLETE" :
                        "WAYPOINTS   ●   ●   ●   ●   ●";
                    break;
                default:
                    _instructions.text = "Navigate P to G with arrow keys or WASD.\nWalls cost 8 points; extra steps cost 2 points.";
                    _metrics.text = $"{status}\n\nMOVES {_mazeTask.Moves}\nWALL HITS {_mazeTask.Collisions}\nSCORE {_mazeTask.Score:F0}";
                    _center.text = _mazeTask.Draw();
                    break;
            }
        }

        private void BuildUI()
        {
            var canvasObject = new GameObject("Rehab HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("UI Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            }
            var left = Panel(canvasObject.transform, "Navigation", new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(345, 0), new Color(.055f, .095f, .13f, .95f));
            var stack = left.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(20, 20, 26, 20);
            stack.spacing = 10;
            stack.childControlHeight = true;
            stack.childForceExpandHeight = false;
            _title = Label(left, "Brand", 29, FontStyle.Bold, Color.white, 46);
            _subtitle = Label(left, "Mode", 16, FontStyle.Normal, new Color(.5f, .86f, .8f), 58);
            Label(left, "Training", 16, FontStyle.Bold, new Color(.72f, .8f, .86f), 30);
            Button(left, "Force Interaction", () => SelectMode(TrainingMode.Force));
            Button(left, "Sequence Memory", () => SelectMode(TrainingMode.Memory));
            Button(left, "Spatial Assembly", () => SelectMode(TrainingMode.Assembly));
            Button(left, "Visual Tracing", () => SelectMode(TrainingMode.Tracing));
            Button(left, "Maze Navigation", () => SelectMode(TrainingMode.Maze));
            Label(left, "Setting", 16, FontStyle.Bold, new Color(.72f, .8f, .86f), 32);
            Button(left, "Living Room / Kitchen", () =>
            {
                _room = _room == RoomKind.LivingRoom ? RoomKind.Kitchen : RoomKind.LivingRoom;
                _environment.Build(_room); RefreshUI();
            });
            Button(left, "Sphere / Cylinder / Pencil", () =>
            {
                if (_useLiveForce) return; // Physical shape comes from calibration metadata.
                _shape = _shape == GripShape.Pencil ? GripShape.Sphere : _shape + 1;
                _deformation.SetShape(_shape); RefreshUI();
            });
            Button(left, "Force: Keyboard Demo", () => SelectForceSource(false));
            Button(left, "Force: BLE Device", () => SelectForceSource(true));
            _inputStatus = Label(left, "Input status", 14, FontStyle.Normal, new Color(.85f, .71f, .49f), 62);

            var right = Panel(canvasObject.transform, "Metrics", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-375, -400), new Vector2(-25, -25), new Color(.055f, .095f, .13f, .92f));
            right.sizeDelta = new Vector2(350, 375);
            _metrics = AbsoluteLabel(right, "Session metrics", 19, FontStyle.Normal, Color.white);

            var bottom = Panel(canvasObject.transform, "Exercise controls", new Vector2(.5f, 0),
                new Vector2(.5f, 0), new Vector2(-380, 22), new Vector2(380, 240),
                new Color(.055f, .095f, .13f, .94f));
            _center = AbsoluteLabel(bottom, "Exercise display", 23, FontStyle.Bold, new Color(.46f, .92f, .82f));
            _center.rectTransform.anchorMin = new Vector2(.03f, .49f);
            _center.rectTransform.anchorMax = new Vector2(.97f, .95f);
            _center.alignment = TextAnchor.MiddleCenter;
            _instructions = AbsoluteLabel(bottom, "Instructions", 16, FontStyle.Normal, Color.white);
            _instructions.rectTransform.anchorMin = new Vector2(.04f, .22f);
            _instructions.rectTransform.anchorMax = new Vector2(.96f, .52f);
            _instructions.alignment = TextAnchor.MiddleCenter;
            var start = FloatingButton(bottom, "Start", new Vector2(.04f, .02f), new Vector2(.3f, .22f), StartSession);
            _startLabel = start.GetComponentInChildren<Text>();
            var pause = FloatingButton(bottom, "Pause", new Vector2(.32f, .02f), new Vector2(.57f, .22f), TogglePause);
            _pauseLabel = pause.GetComponentInChildren<Text>();
            _traceMarkers = new Image[TraceTask.Path.Length];
            for (var i = 0; i < _traceMarkers.Length; i++)
            {
                var point = TraceTask.Path[i];
                var marker = new GameObject($"Trace waypoint {i + 1}", typeof(RectTransform), typeof(Image));
                var rect = marker.GetComponent<RectTransform>();
                rect.SetParent(canvasObject.transform, false);
                rect.anchorMin = rect.anchorMax = point;
                rect.sizeDelta = new Vector2(28, 28);
                marker.GetComponent<Image>().raycastTarget = false;
                _traceMarkers[i] = marker.GetComponent<Image>();
            }
            for (var i = 0; i < 4; i++)
            {
                var token = i;
                FloatingButton(bottom, (i + 1).ToString(), new Vector2(.59f + i * .097f, .02f),
                    new Vector2(.68f + i * .097f, .22f), () => SubmitPiece(token));
            }
        }

        private static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            obj.GetComponent<Image>().color = color;
            return rect;
        }

        private Text Label(Transform parent, string name, int size, FontStyle style, Color color, float height)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            var element = obj.GetComponent<LayoutElement>();
            element.preferredHeight = height;
            var label = obj.GetComponent<Text>();
            label.font = _font; label.fontSize = size; label.fontStyle = style;
            label.color = color; label.raycastTarget = false;
            label.alignment = TextAnchor.MiddleLeft;
            return label;
        }

        private Text AbsoluteLabel(Transform parent, string name, int size, FontStyle style, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(.06f, .06f);
            rect.anchorMax = new Vector2(.94f, .94f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var label = obj.GetComponent<Text>();
            label.font = _mode == TrainingMode.Maze ? _monoFont : _font;
            label.fontSize = size; label.fontStyle = style; label.color = color;
            label.alignment = TextAnchor.UpperLeft; label.raycastTarget = false;
            return label;
        }

        private void Button(Transform parent, string caption, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<LayoutElement>().preferredHeight = 46;
            obj.GetComponent<Image>().color = new Color(.13f, .25f, .31f);
            obj.GetComponent<Button>().onClick.AddListener(action);
            var text = AbsoluteLabel(obj.transform, "Label", 17, FontStyle.Normal, Color.white);
            text.text = caption; text.alignment = TextAnchor.MiddleCenter;
        }

        private GameObject FloatingButton(Transform parent, string caption, Vector2 min, Vector2 max,
            UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            obj.GetComponent<Image>().color = new Color(.13f, .48f, .46f);
            obj.GetComponent<Button>().onClick.AddListener(action);
            var label = AbsoluteLabel(rect, "Label", 15, FontStyle.Bold, Color.white);
            label.text = caption.ToUpperInvariant(); label.alignment = TextAnchor.MiddleCenter;
            return obj;
        }
    }
}




