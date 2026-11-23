# State (2026-11-21, release v1.2.0)

**Branch:** `main` (2026-11-23: fast-forwarded to all work; the other 20 branches, all merged, deleted; version tags kept). Remote `origin` (GitHub) exists; `main` is ahead of it and the assistant pushes nothing without your OK.

## Where things stand
- **Software:** every WBS item is built ([RELEASE_AUDIT](../RELEASE_AUDIT.md)). Nothing is in progress.
- **Verify everything:** `ai-engine/.venv-ai/Scripts/python.exe Tools/run_full_regression.py` (Editor closed, about 75 min). The latest result is `Docs/full-regression.json`. `Tools/test_repo_hygiene.py` fails if a suite is missing from that runner or a document link is broken.
- **Release files:** `.local/release/GripRehab-Setup-1.2.0.exe` and `GripRehab-1.2.0-portable.zip` (`Tools/release/stage_release.py --version <v>`).
- **Layout:** game code in `Assets/Rehab/<WBS>`; the WBS index for the assembly layers is in [PROJECT_MAP](PROJECT_MAP.md) (ADR-WBS-1). Superseded documents are in `Docs/history/`.

## Recent history
- **2026-11-22 (branch `claude/hand-goals`):** the three standing goals: object-led hand and grab range (ADR-GRIP-1); laptop webcam default with its wide mode and CALIBRATE HAND (ADR-MAP-1); `Tools/hand_check_report.py` compares a Hand Lab session's real hand with the game hand (mapping, orientation, grip).
- **2026-11-22 (in v1.2.0):** camera-hand orientation is absolute, seen from behind the hand (ADR-TRACK-3); Camo is the default phone camera with a laptop-webcam fallback and a black-picture warning (ADR-PHONE-2).
- **v1.2.0 (2026-11-21):** the camera hand is the main input. Movement assist (ADR-ASSIST-1, -2): it predicts the object or place and finishes the reach, brings tools to their work, and cuts and stirs by itself. Hand-controlled depth: toward the camera moves the hand forward (ADR-DEPTH-1). Phone camera through Phone Link or USB (ADR-PHONE-1). Tremor only while the hand holds (ADR-TEA-2). Activity probe (SYNTHETIC): keyboard, camera and 70 %-reach intent each 14/14. Root `README.md` folder map; `Tools/release/Cleanup-Project.ps1`.
- **v1.1.0 (2026-11-18):** seventh activity, pour and drink tea in the living room, with a simulated liquid (ADR-TEA-1) and a camera tremor estimate; camera-hand depth no longer jumps when the wrist rolls (ADR-TRACK-1). The automated hand pointer no longer depends on window focus or starts the webcam.
- **v1.0.8 (2026-11-10):** the regression became one tracked command and now includes checks that had been left out since late September and October: the rendered apartment acceptance, firmware C++, CAD, `tests/2.1`, `2.4`, `4.2`, `4.5`, `6.1`, source fingerprints and the camera models. One real failure: six imported files had been edited without updating `Docs/Source/IMPORT_MANIFEST.json`; each change is now recorded there. `verify_ui_boundary.py` retired to `Docs/history/retired-tools/`.
- **v1.0.5–v1.0.7 (2026-11-06):** fault probe (15 checks), camera failure reason on the HUD, plain storage messages, UI checks at 1080p and 1440p, dead code and unused files removed with the user's OK, app version in step with the release.
- **v1.0.0–v1.0.4 (2026-11-01 to 11-06):** hybrid camera hand (fixes F1–F6, camera activity run 12/12), Stage 18 hardware readiness, installer, slides, demo video. Details: [HAND_CONTROL_REPORT](../HAND_CONTROL_REPORT.md), [STAGE16_AUDIT](../STAGE16_AUDIT.md).

## Watch list
- 2026-11-22: two one-off regression failures that did not reproduce (UI checks without fresh output; keyboard tea L8 once in six runs). The camera hand's pads stay about 6 mm off the knife (keyboard hand touches it): open.
- A-OB1: intermittent activity grab failures seen on 2026-10-28; not reproduced since 2026-11-01. The regression runs the activity probe three times plus the camera run to keep watching it.
- Unity 2022.3 rewrites the Unity-6-format main scene if it saves it. Do not save the scene from the Editor.

## Waiting on the user (needs a person or hardware)
- Run `powershell -ExecutionPolicy Bypass -File Tools/release/Cleanup-Project.ps1` with Unity and the game closed (old logs, releases, environments, caches; the assistant is blocked from mass deletes).
- Phone Link camera delay: resume the camera on the phone, point it at the screen, run `.venv/Scripts/python.exe Tools/Vision/camera_latency.py --camera-name "Windows Virtual Camera"` (the 2026-11-21 try got Phone Link's paused picture).
- With Claude and Unity closed, run `E:\GP\codex\Move-GameTo01.ps1` (moves the game to `01_Game` and fixes paths).
- Real-hand playtest with Camo: the hand's look from behind (fingers up, back of the hand toward you, palm turns), depth toward/away, auto cut and stir, tea ([PHYSICAL_TEST_PROTOCOL](../PHYSICAL_TEST_PROTOCOL.md) P5).
- Microphone session (V-01, V-09, V-10) and listening session (S-07, S-09).
- Hardware bench: grips, circuit, calibration, BLE stream (P1–P4).
- Install on a PC that never had the project.

## Decisions
See [DECISIONS](DECISIONS.md) and `Docs/DECISIONS.md`.
