# Grip Rehab: Unity game for hand-grip rehabilitation

A furnished apartment with living-room and kitchen activities. You play with your **hand in front of a camera** (MediaPipe hand tracking), helped by a **movement assist**. Keyboard and mouse also work, and an optional force sensor (BLE) adds grip force. Research software: **not a clinical assessment**.

- **Unity:** 2022.3.20f1. Open this folder in Unity Hub; the main scene is `Assets/Scenes/GripRehab.unity`.
- **First time:** [START_HERE_AR.html](START_HERE_AR.html), then [Docs/HAND_CONTROLS.md](Docs/HAND_CONTROLS.md) (every gesture and setting).
- **Status:** [Docs/STATUS.md](Docs/STATUS.md). **Decisions:** [Docs/DECISIONS.md](Docs/DECISIONS.md). **Project rules:** [CLAUDE.md](CLAUDE.md).

## Folder map
```
GripRehab-Unity/
├── Assets/                      Unity project content
│   ├── Rehab/                   game code, one folder per WBS task
│   │   ├── 3.1-environment/       apartment, stations, settings
│   │   ├── 3.2-vision/            camera hand: bridge launcher, receiver, depth, movement assist, Hand Lab
│   │   ├── 3.3-deformation/       squeezable objects, tea liquid physics
│   │   ├── 3.4-game-modules/      menus, voice, audio, exercise suite, probes
│   │   ├── 3.4.1-force-object-interaction/Apartment/   hand-object interaction and the 7 activities
│   │   ├── 3.4.2-cognitive-performance/  3.4.3-visuomotor-coordination/   modules 2 and 3
│   │   └── 6.1-bluetooth-integration/    force sensor (BLE)
│   ├── Product/  ProductIntegration/  UI/  UIBridge/  _Project/   app shell, profiles, settings, dashboards
│   ├── Scenes/                  GripRehab.unity (the whole game) and the UI gallery
│   ├── ThirdParty/  com.gleechi.unity.virtualgrasp/  TextMesh Pro/   vendor code (never edited)
│   └── Editor/                  build, verification and console-zero tools
├── Packages/  ProjectSettings/  Unity package list and settings (local packages: Tools/AI)
├── Builds/Windows/              the built game (GripRehab.exe)
├── Tools/                       everything that runs outside Unity
│   ├── Vision/                  MediaPipe bridge (laptop webcam, Camo, Phone Link, USB phone), camera delay test
│   ├── Offline/                 C# tests that run without Unity (offline_verify.py)
│   ├── run_*.py                 game test suites (activity, handlook, perf, UI, ...); run_full_regression.py runs all
│   ├── hand_lab_report.py  hand_replay.py  hand_depth_eval.py   real-hand recording, replay and analysis
│   ├── hand_check_report.py     real hand vs game hand: mapping, depth, orientation, grip (one Hand Lab session)
│   ├── hand_pose_check.py       SYNTHETIC hand poses into the game, screenshots of the virtual hand
│   ├── test_*.py                Python tests (python -m unittest discover -s Tools -p "test_*.py")
│   ├── AI/                      local Unity packages used by Packages/manifest.json
│   └── release/                 installer, release and clean-up scripts
├── ai-engine/                   fatigue AI (data, training, ONNX export), research only
├── models/                      hand_landmarker.task and the other bundled models
├── firmware/  hardware/         force-sensor firmware (Arduino) and grip/electronics designs
├── tests/                       test sources per WBS task
├── Docs/                        all documentation (map: Docs/README.md)
├── CVTraining/weights/          small weights of the optional YOLO hand detector (not used by the game)
└── .local/  .venv/              local tools and the Python environment (not in git)
```

## Build and test (Unity Editor closed)
| What | Command |
|---|---|
| Build (hidden) | `"C:/Program Files/Unity/Hub/Editor/2022.3.20f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath . -executeMethod GripRehab.Editor.RehabVerification.BuildWindows -logFile Logs/build.log` |
| Offline C# + Python tests | `ai-engine/.venv-ai/Scripts/python.exe Tools/offline_verify.py --meta --compile --tests` and `python -m unittest discover -s Tools -p "test_*.py"` |
| Activities with the camera hand | `ai-engine/.venv-ai/Scripts/python.exe Tools/run_activity_probe.py --camera` (or `--intent` for a 70 % reach with tremor) |
| Everything | `ai-engine/.venv-ai/Scripts/python.exe Tools/run_full_regression.py` (about 75 min) |
| Clean caches and old outputs | `powershell -ExecutionPolicy Bypass -File Tools/release/Cleanup-Project.ps1` |

## Camera
- **Laptop webcam:** the default (its wide 1280x720 mode at 30 fps). Run CALIBRATE HAND once (Settings, or say "calibrate hand").
- **Phone through Camo:** Camo Studio on the PC, the Camo app on the phone.
- **Phone through Windows Phone Link:** connected camera, no cable.
- **Phone over USB:** needs USB debugging.

Choose in Settings → CAMERA. See [Docs/PHONE_CAMERA.md](Docs/PHONE_CAMERA.md).

## Notes
Single-camera tracking estimates depth: it does not measure it. Force from the camera is a visual estimate unless a sensor is connected. Every assisted grab, placement, cut or stir is recorded separately from the patient's own movement.
