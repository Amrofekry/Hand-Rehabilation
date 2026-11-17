# State (2026-11-18, release v1.1.0)

**Branch:** `claude/tea-activity`; `claude/final-release` and `3.4-upgrade-baseline` point at the same commit, tag `v1.1.0`. No remote; nothing pushed.

## Where things stand
- **Software:** every WBS item is built ([RELEASE_AUDIT](../RELEASE_AUDIT.md)). Nothing is in progress.
- **Verify everything:** `ai-engine/.venv-ai/Scripts/python.exe Tools/run_full_regression.py` (Editor closed, about 75 min). The latest result is `Docs/full-regression.json`. `Tools/test_repo_hygiene.py` fails if a suite is missing from that runner or a document link is broken.
- **Release files:** `.local/release/GripRehab-Setup-1.0.8.exe` and `GripRehab-1.0.8-portable.zip` (`Tools/release/stage_release.py --version <v>`).
- **Layout:** game code in `Assets/Rehab/<WBS>`; the WBS index for the assembly layers is in [PROJECT_MAP](PROJECT_MAP.md) (ADR-WBS-1). Superseded documents are in `Docs/history/`.

## Recent history
- **v1.1.0 (2026-11-18):** seventh activity, pour and drink tea in the living room, with a simulated liquid (ADR-TEA-1) and a camera tremor estimate; camera-hand depth no longer jumps when the wrist rolls (ADR-TRACK-1). The automated hand pointer no longer depends on window focus or starts the webcam.
- **v1.0.8 (2026-11-10):** the regression became one tracked command and now includes checks that had been left out since late September and October: the rendered apartment acceptance, firmware C++, CAD, `tests/2.1`, `2.4`, `4.2`, `4.5`, `6.1`, source fingerprints and the camera models. One real failure: six imported files had been edited without updating `Docs/Source/IMPORT_MANIFEST.json`; each change is now recorded there. `verify_ui_boundary.py` retired to `Docs/history/retired-tools/`.
- **v1.0.5–v1.0.7 (2026-11-06):** fault probe (15 checks), camera failure reason on the HUD, plain storage messages, UI checks at 1080p and 1440p, dead code and unused files removed with the user's OK, app version in step with the release.
- **v1.0.0–v1.0.4 (2026-11-01 to 11-06):** hybrid camera hand (fixes F1–F6, camera activity run 12/12), Stage 18 hardware readiness, installer, slides, demo video. Details: [HAND_CONTROL_REPORT](../HAND_CONTROL_REPORT.md), [STAGE16_AUDIT](../STAGE16_AUDIT.md).

## Watch list
- A-OB1: intermittent activity grab failures seen on 2026-10-28; not reproduced since 2026-11-01. The regression runs the activity probe three times plus the camera run to keep watching it.
- Unity 2022.3 rewrites the Unity-6-format main scene if it saves it. Do not save the scene from the Editor.

## Waiting on the user (needs a person or hardware)
- Run `.local/scratch/block-j-cleanup.ps1` (about 14 GB of junk; the assistant is blocked from mass deletes).
- With Claude and Unity closed, run `E:\GP\codex\Move-GameTo01.ps1` (moves the game to `01_Game` and fixes paths).
- Real-hand playtest of the seven activities with the webcam (the tea pour and drink especially) ([PHYSICAL_TEST_PROTOCOL](../PHYSICAL_TEST_PROTOCOL.md) P5).
- Microphone session (V-01, V-09, V-10) and listening session (S-07, S-09).
- Hardware bench: grips, circuit, calibration, BLE stream (P1–P4).
- Install on a PC that never had the project.

## Decisions
See [DECISIONS](DECISIONS.md) and `Docs/DECISIONS.md`.
