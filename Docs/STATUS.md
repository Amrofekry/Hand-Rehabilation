# GripRehab status: single source of truth

Updated 2026-11-06 (release v1.0.5). The plan-by-plan audit is [RELEASE_AUDIT.md](RELEASE_AUDIT.md). If another document disagrees with this file, this file wins. Older documents in `Docs/` are dated history; [README.md](README.md) is the index.

| Item | Current value |
|---|---|
| Unity Editor | **2022.3.20f1** (decision D1, 2026-10-23). The 6000.4.4f1 references in older docs are history. |
| Render / UI | URP 14.0.10, uGUI, TextMeshPro 3.0.6 |
| AI runtime | Sentis 2.0.0 CPU, CNN model (GRU operator unsupported in Sentis) |
| Main scene | `Assets/Scenes/GripRehab.unity` |
| Branch / tag | `claude/final-release` = `3.4-upgrade-baseline`, tag `v1.0.5` (releases v1.0.0–v1.0.5; nothing pushed, no remote) |
| Backup | `E:\GP\backups\GripRehab-2026-10-23.zip` (excludes Library, Builds, Logs, venvs) |
| Active plan | Final plan (2026-10-29), all blocks done; Stages 0–13, 15A, 16–19 done. Stages 14 (gaze) and 15 (voice assistant) became optional stages O1/O2 in [OPTIONAL_PLAN](OPTIONAL_PLAN.md). |

## Evidence columns

- **Software:** the code exists and runs.
- **Native test:** automated Unity Editor / Windows player checks. Synthetic or fixture input only.
- **Physical / human:** real hardware, a live person or participant data. Recorded separately and never inferred from the other two columns.

## The software tasks (23 original + 10 added 2026-10-25 + 1 added 2026-10-26)

| No. | Task (WBS) | Software | Native test | Physical / human | Evidence |
|---|---|---|---|---|---|
| 1 | Pipeline/Gantt review and requirements (1.1) | Done | n/a | n/a | `SOFTWARE_GRADUATION_PLAN.md` |
| 2 | Accounts, profile, results, Continue (3.4, 5.1, 5.2) | Implemented | Journey suite passes every regression (account, module, results, export, quit) | None | `ai-player-journey.json` |
| 3 | Staged 54 s baseline (4.1) | Implemented, with stage coach, countdown, signal-quality indicator and re-baseline prompt (Stage 5) | Passed (fixtures); player journey runs the coached baseline | None; no real force | `../ai-engine/reports/FINAL_REPORT.md`, `evidence/stage5-2026-10-24/` |
| 4 | Vision metrics and gestures, YOLO (3.2) | Implemented for MediaPipe (hybrid camera hand, row 30); YOLO is optional O3 | MediaPipe path regression-checked | Live human trial not done (B05) | `HANDS_AND_MEDIAPIPE_AR.md` |
| 5 | Deformation and grasp profiles (3.3) | Superseded by rows 24 and 25 (Stage 8 contact, Stage 9 deformation) | Regression-checked | None | `DEFORMATION_ITEM_5.md` (history) |
| 6 | Module 1 Force-Object (3.4.1) | Implemented: rounds (hold/control/pulse), levels, metrics, squeeze liquid, effort gauge, synthesised feedback sounds | Rounds/results checked; sounds checked by EditMode synthesis test | None; sounds not listened to | `player-checks.json`, `evidence/stage4-2026-10-24/` |
| 7 | Module 2 Cognitive (3.4.2) | Implemented: avatar gesture memory (camera/buttons/keys), assembly by hand pointer (5 s pick-up, carry, 2 s place) or mouse, levels | Hand play checked in EditMode and in the build with a synthetic hand | No real webcam hand yet | `RehabExerciseSuite.cs`, `evidence/stage4-2026-10-24/hand-play/` |
| 8 | Module 3 Visuomotor (3.4.3) | Implemented: generated mazes steered by hand pointer (marker follows the hand) or mouse/keys, guided tracing with green/red trail, levels | Hand play checked in EditMode and in the build with a synthetic hand | No real webcam hand yet | `RehabExerciseSuite.cs`, `evidence/stage4-2026-10-24/hand-play/` |
| 9 | 10 Hz feature windows and CSV export (4.2) | Implemented | Passed (40/40 synthetic packets) | No device data | FINAL_REPORT |
| 10 | Seven-class facial model, opt-in (3.2/4.2) | Implemented | Passed (synthetic) | Accuracy not validated | FINAL_REPORT |
| 11 | rPPG HR/HRV, opt-in (3.2/4.2) | Implemented | Passed (synthetic signals) | No reference-sensor validation | FINAL_REPORT |
| 12 | GRU/LSTM/CNN training (4.3) | Implemented on EMG proxy | 32 Python tests pass | Recall 0.49–0.52 vs 0.85 target. **Not fatigue validation.** | FINAL_REPORT |
| 13 | ONNX export and Sentis (4.4) | Implemented (CNN) | Sentis/ORT parity 7.45e-09 | n/a | FINAL_REPORT |
| 14 | Decision policy: adapt / warn / stop (4.5) | Implemented: bounded in-session adaptation in all three modules, plain-language research-labelled pause/stop screens (Stage 5) | 186 native assertions; SYNTHETIC closed-loop replay (EditMode); 10 new product tests | Thresholds experimental; no validated fatigue detection | FINAL_REPORT, `evidence/stage5-2026-10-24/` |
| 15 | Patient dashboard (5.1) | Implemented: My Progress cards (missing = “—”), baseline comparison, per-module statistics and trends, stopped/incomplete marked (Stage 6) | Product tests with exact fixture numbers; walked in the build on fabricated fixtures | No real patient use | `evidence/stage6-2026-10-24/` |
| 16 | Doctor dashboard (5.2) | Implemented: D1–D5 incl. D3 progress with safety events, D4 note templates with author, D5 audit trail and sign-in log, CSV + report PDF + session PDF (Stage 6) | Product tests (no cross-patient mixing, roles, audit); encrypted-store EditMode test; walked in the build | No clinician review | `evidence/stage6-2026-10-24/` |
| 17 | Protected records, recovery, CSV (5.x) | Implemented: DPAPI store with backup recovery; damaged-save handling in the fault probe | DPAPI readback fixed and checked | Not a validated medical record system | FINAL_REPORT |
| 18 | Phase 2 force envelope and BLE bridge (2.4/6.1) | Implemented in software; stale, disconnect and reconnect in the fault probe | Synthetic replay passed | **No Nano/HX711 hardware (B02)** | `BLE_INTEGRATION.md` |
| 19 | English UI, controls, hand pointer (3.4) | Implemented: first-run tutorials, onboarding, design system, hand-first controls, settings for hand reach, steadiness and aim assist | UI checks at 6 sizes (1024×768 to 2560×1440, size verified) × 3 themes, gallery checks pass every regression | Human acceptance OPEN | `ui-player-checks.json`, `gallery-checks.json` |
| 20 | Automated verification (6.2) | Implemented: player suites plus Stage 7 perf/soak/fault probe, rolling diagnostics log and copy-diagnostics page | Reference laptop, 1920×1080, SYNTHETIC input: ≥128 FPS at p95, ≤514 B/frame managed alloc, 2-hour soak with flat memory and 0 errors, loopback hand latency p95 4.2 ms; malformed UDP and busy ports handled (25 Oct); release fault probe 15/15 (2026-11-06): camera cannot open, hand stream lost, force sensor stale/disconnected/reconnected, no microphone, missing model, damaged saves, each explained on screen with 0 console lines | FPS with the camera worker and camera-to-hand latency not measured (needs the webcam) | `evidence/stage7-2026-10-25/` |
| 21 | Documentation and evidence (6.3) | Implemented: index, Arabic guide (md/pdf/docx), architecture pages, Stage 16 audit, slides, demo video | n/a | n/a | [README.md](README.md) |
| 22 | Voice commands | Implemented (Stage 13): offline Windows keyword commands for UI, apartment, activities and session; state-aware, confirmations, feedback; off by default | Offline router tests (140); SYNTHETIC player voice probe; recognizer start checked on this computer | Your microphone session pending | `VOICE_COMMANDS.md`, `AUDIT_LEDGER.md` §10f |
| 23 | Eye/head pointer | Optional (O1 in OPTIONAL_PLAN): not needed for GP1; aim assist and the hand pointer already select targets | — | — | `OPTIONAL_PLAN.md` |
| 24 | Hand–object contact rebuild (3.4.1, 3.3) | Implemented: contact solver (curl stops at the surface), grip-anchored holds, 18-grip chart with code-built objects and a Grip Lab station, chart-like hand presentation (Stage 8); C-01–C-03, C-05–C-09, C-11–C-13 tested, C-04 by eye pending, C-10 in progress | SYNTHETIC rendered-player probes: station objects 19–70 mm → 0–5.7 mm penetration; 18 grips ≤ 1 mm, jitter ≤ 0.1 mm | Fast shake (~2.8 m/s) 13–45 mm transient; no real-hand camera session yet | `AUDIT_LEDGER.md` §10a |
| 25 | Object deformation system rebuild (3.3) | Implemented: chart materials (rigid to soft), spring-damper surface with smoothing and volume preservation, six forms, elastic/plastic/brittle results, Material Lab (Stage 9); D-01, D-03–D-13 tested, D-02 by eye pending | SYNTHETIC material matrix of 15 objects: dent, form, volume, recovery, set, fracture, frame time | Game-scale model, not FEM or measured constants; paper-stack shear hard to see | `AUDIT_LEDGER.md` §10b |
| 26 | Six multi-step activities, living room and kitchen (3.1, 3.4.1) | Implemented (Stage 10): bookshelf, coffee table, storage box, simple meal, cutting, prepare and serve; levels 1–10, scoring, outcomes in sessions and dashboards; A-01–A-06 and A-08–A-12 tested, A-07 by eye pending, A-13 in progress (camera hand) | SYNTHETIC player probe: 12 of 12 runs complete (levels 2 and 8), scores 88–100 | Placement is assisted (upright, game-scale); cutting is a mesh plane cut; your hands-on playtest pending | `AUDIT_LEDGER.md` §10c |
| 27 | UI modernization and design system (3.4, 5.x) | Implemented (Stage 11): tokens, contrast-aware focus, selected marker, error state, redesigned pause/activity selection, onboarding, input-mode strip, 4:3 fit; U-01–U-07, U-11, U-12 tested, U-08 by eye pending, U-09/U-10 wait for gaze and voice | SYNTHETIC UI checks at 4 resolutions × 3 themes; offline contrast tests | Gaze/voice/assistant indicators show OFF until Stages 13–15 | `AUDIT_LEDGER.md` §10d |
| 28 | Audio and sound design upgrade (3.1, 3.4) | Implemented (Stage 12): synthesised material families, impact/grab/release/strain/crack/fracture/cut sounds from real events, 3D sources, GripRehabMixer (Master, SFX, Ambience, Voice) with a setting each, menu-to-play fade, seamless loops | EditMode bank and mixer checks; SYNTHETIC player audio probe | Your listening session pending (S-07, S-09) | `AUDIO_DESIGN.md`, `AUDIT_LEDGER.md` §10e |
| 29 | Speech-to-text and spelling input (3.4) | Partial (Stage 13): offline letter-by-letter spelling with review into text fields; passwords excluded. Free dictation Blocked (Windows dictation is online; you chose offline) | Offline tests; SYNTHETIC probe spelled the profile name | Free dictation not offered | `VOICE_COMMANDS.md` §1 |
| 30 | Hybrid camera hand (3.2) | Implemented: One Euro filter, thumb-aware grasp, hysteresis hold, 0.6 s loss grace, aim assist (counted), HUD state | Benchmark (SYNTHETIC): rest jitter 17.5 → about 10 mm; camera activity run 12/12 after fixes F1–F6; pipeline latency MEASURED median 49.6 ms, p95 67 ms (replayed photos) | Real-hand feel OPEN | `HAND_CONTROL_REPORT.md` |
| 31 | Hardware readiness (Stage 18) | Implemented: calibration wizard (serial and simulated), device status panel | Wizard tests 10, device health tests 14 | Bench trials OPEN | `PHYSICAL_TEST_PROTOCOL.md` |
| 32 | Environment polish (Stage 15A) | Implemented (by-eye): candle glare removed, calmer marble | Perf, gallery and activity checks pass | Human review OPEN | `evidence/final-2026-10-29/env-15a/` |
| 33 | Release (Stage 19) | Implemented: Inno Setup installer, portable zip, first-run check, demo video, slides | Installed, first-run check passed with the real webcam, clean uninstall | Clean-machine install OPEN | `.local/release/` |
| 34 | Gaze-driven UI (3.2) | Optional O1 (was Stage 14) | — | — | `OPTIONAL_PLAN.md` |
| 35 | In-game voice assistant (3.4) | Optional O2 (was Stage 15) | — | — | `OPTIONAL_PLAN.md` |
| 36 | Pipeline, block diagram and Gantt audit (1.1, 6.3) | Done: Stage 16, refreshed for v1.0.5 | n/a | n/a | `STAGE16_AUDIT.md`, `RELEASE_AUDIT.md` |
| 37 | Next-stage research proposal (1.1) | Done: Stage 17 | n/a | n/a | `STAGE17_RESEARCH.md` |
| 38 | Fault handling and release QA (6.2) | Implemented: camera failure reason on the HUD, plain storage messages, fault probe | Fault probe 15/15 (SYNTHETIC) | — | `fault-probe.json`, `history/unused-candidates.md` |

## Open blockers

B02 physical device, B03 clinical data/ethics, B04 Obi/YOLO licences, B05 live human camera trial. See `BLOCKERS.md`.

## Issues found during Stage 0 (for the Stage 1 ledger)

- ~~Absolute package paths in `Packages/manifest.json`~~: fixed in Stage 2 (A-006) with relative `file:../Tools/...` paths.
- `ROLLBACK_2026_10_12.md` is linked from `TASKS.md` and `SOFTWARE_GRADUATION_PLAN.md` but does not exist. Rollback evidence exists only as JSON under `evidence/rollback-2026-10-12/`.
- ~~UI/gallery results only from the Unity 6 build~~: re-run 23 Oct on the 2022.3.20f1 build and passing (60 checks at each of 2 resolutions, 87 at each of 3). See AUDIT_LEDGER §1.
- ~~VirtualGrasp "No baked grasps found" warning~~: the console is 0 errors / 0 warnings / 0 logs (ConsoleZeroCheck, every regression since 2026-10-28).
- Superseded docs (`TASKS.md`, `REPAIR_REPORT.md`, `CLOSEOUT_2026_10_06.md`, `FINAL_REPORT.md`, `PROGRESS.md`, item docs) were kept in place, not moved to `Docs/archive/`, so existing relative links keep working. Treat them as history.
