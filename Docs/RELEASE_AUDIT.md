# Release audit: plan against the built game (2026-11-06, v1.0.6)

Every WBS item in `../PROJECT_PLAN.md`, checked against the pipeline, block diagram and Gantt chart (as-built versions in [STAGE16_AUDIT.md](STAGE16_AUDIT.md) §1–2). "Evidence" names the automated check or run; results are SYNTHETIC unless marked MEASURED, BY-EYE or PHYSICAL. Nothing here is a clinical or physical validation.

**Status key:** DONE (software built and checked) · NEEDS HARDWARE · NEEDS A PERSON. No software item is PARTIAL or MISSING.

| WBS | Plan item | Where it is implemented | Evidence | Status |
|---|---|---|---|---|
| 1.1 | Pipeline and block diagrams | `Docs/architecture/`, `STAGE16_AUDIT.md` §1 | Diagrams checked against the code (this table) | DONE |
| 1.2 | Literature and component selection | `../03_Docs/` (outside the game) | Not a game item | DONE (outside the game) |
| 1.3 | Events calendar | `../03_Docs/` (outside the game) | Not a game item | DONE (outside the game) |
| 2.1 | 3D-printed grips and housing | `../02_Hardware/` | — | NEEDS HARDWARE (print and fit) |
| 2.2 | Circuit (Nano 33 IoT, HX711) | `../02_Hardware/`, `firmware/` | — | NEEDS HARDWARE (assemble, measure rails) |
| 2.3 | Load-cell calibration in newtons | `firmware/2.3-*`, `tests/2.3-calibration/`, calibration wizard (`Assets/Rehab/6.1-bluetooth-integration/`) | Calibration unit tests; wizard tests (10) | Software DONE; bench loads NEED HARDWARE |
| 2.4 | Wireless firmware (BLE) | `firmware/2.4-wireless-streaming/`, `Tools/Phase2Bridge/unity_bridge.py` | Frame contract tests; SYNTHETIC replay | Software DONE; Nano stream NEEDS HARDWARE |
| 3.1 | Living room and kitchen | `Assets/Scenes/GripRehab.unity`, `Assets/Rehab/3.1-environment/` | Gallery checks; perf probe; Stage 15A BY-EYE | DONE |
| 3.2 | Vision (MediaPipe hand, gestures; YOLO) | `Assets/Rehab/3.2-vision/`, `Tools/Vision/mediapipe_bridge.py` | Hand benchmark; camera activity run 12/12; latency MEASURED on replayed photos (median 49.6 ms); hand-loss checked by the fault probe | DONE for MediaPipe. YOLO is optional O3 (decision G06). Live hand trial NEEDS A PERSON |
| 3.3 | Deformation (Obi replaced by in-house solver) | `Assets/Rehab/3.3-deformation/`, Stage 9 material lab | Material-lab probe (15 objects); contact probes ×3 | DONE (game-scale model, not FEM) |
| 3.4 | Game-module framework | `Assets/Rehab/3.4-game-modules/` | Journey suite; EditMode 10/10 | DONE |
| 3.4.1 | Module 1 force–object task and six activities | `Assets/Rehab/3.4.1-force-object-interaction/` | Player checks; activity probe ×3 keyboard + camera, 12/12 each | DONE; hands-on playtest NEEDS A PERSON |
| 3.4.2 | Module 2 cognitive (GP2 in the plan) | `Assets/Rehab/3.4.2-cognitive-performance/`, `RehabExerciseSuite.cs` | EditMode and build hand-play checks | DONE (ahead of plan) |
| 3.4.3 | Module 3 visuomotor (GP2 in the plan) | `Assets/Rehab/3.4.3-visuomotor-coordination/`, `RehabExerciseSuite.cs` | EditMode and build hand-play checks | DONE (ahead of plan) |
| 4.1 | Baseline protocol | `ai-engine/4.1-*`, coached 54 s baseline in the product | Journey suite runs the baseline; fixtures | DONE; participant protocol NEEDS A PERSON |
| 4.2 | Data collection and CSV | `ai-engine/4.2-*`, `Assets/Product/TemporalForceWindow.cs` | 40/40 SYNTHETIC packets; schema tests | Software DONE; real data NEEDS A PERSON (ethics) |
| 4.3 | Model training (GRU/LSTM/CNN) | `ai-engine/4.3-*` | pytest 32; recall 0.49–0.52 on the EMG proxy | Software DONE; **not** fatigue validation |
| 4.4 | ONNX and Sentis | `ai-engine/4.4-*`, `Assets/ProductIntegration/ResearchAiRuntime.cs` | Sentis/ORT parity 7.45e-09; missing-model case in the fault probe | DONE (CNN; GRU operator unsupported in Sentis) |
| 4.5 | Decision logic: adapt, warn, stop | `Assets/Product/FatigueSafetyPolicy.cs`, `ai-engine/4.5-*` | 186 native assertions; replay suite | DONE; thresholds experimental |
| 5.1 | Patient dashboard | `Assets/ProductIntegration/ResearchPortalPages.cs` | Product tests with exact fixtures; journey | DONE |
| 5.2 | Doctor dashboard (D1–D5) | `Assets/ProductIntegration/ResearchPortalPages.cs` | Product tests (roles, audit, no patient mixing) | DONE; clinician review NEEDS A PERSON |
| 6.1 | Bluetooth integration | `Assets/Rehab/6.1-bluetooth-integration/` | SYNTHETIC replay; device-health tests (14); stale, disconnect and reconnect in the fault probe | Software DONE; real stream NEEDS HARDWARE |
| 6.2 | Closed-loop testing | `Tools/run_*.py`, `tests/6.2-*` | Full regression green in one run (`Logs/final-n-regression.txt`, 2026-11-06) | DONE (software); physical loop NEEDS HARDWARE |
| 6.3 | Documentation and presentation | `Docs/README.md` index, guide, slides, demo video, installer | First-run check with the real webcam (MEASURED, this computer) | DONE; clean-machine install NEEDS A PERSON |

## What you need to do (the NEEDS rows)
| Owner | Item | Steps |
|---|---|---|
| You | Hardware (2.1–2.4, 6.1, 6.2) | Follow [PHYSICAL_TEST_PROTOCOL.md](PHYSICAL_TEST_PROTOCOL.md) P1–P4: print the grips, assemble the circuit, calibrate with known loads in the wizard, stream into the game and read the device panel. |
| You | Real hand (3.2, 3.4.1) | Protocol P5: play the six activities with the webcam; note grabs that fail and how the hand feels. |
| You | Microphone and listening | `VOICE_COMMANDS.md` V-01, V-09, V-10; `AUDIO_DESIGN.md` S-07, S-09. |
| You | Clean machine | Install `GripRehab-Setup-1.0.6.exe` on a PC without the project; run the first-run check. |
| You | Data and ethics (4.1–4.3) | Needs approval before any participant data. |
