# GripRehab status: single source of truth

Updated 2026-10-24 (Gantt W6). If another document disagrees with this file, this file wins. Older documents in `Docs/` are dated history.

| Item | Current value |
|---|---|
| Unity Editor | **2022.3.20f1** (decision D1, 2026-10-23). The 6000.4.4f1 references in older docs are history. |
| Render / UI | URP 14.0.10, uGUI, TextMeshPro 3.0.6 |
| AI runtime | Sentis 2.0.0 CPU, CNN model (GRU operator unsupported in Sentis) |
| Main scene | `Assets/Scenes/GripRehab.unity` |
| Branch / tag | `3.4-upgrade-baseline`, tag `v0.9-prototype-2026-10-23` |
| Backup | `E:\GP\backups\GripRehab-2026-10-23.zip` (excludes Library, Builds, Logs, venvs) |
| Active plan | [Ultimate upgrade plan](superpowers/plans/2026-10-23-ultimate-upgrade-plan.md). Stages 0–5 are done, including the Stage 3 hands-on playtest, the Stage 4 by-eye review, the Stage 5 SYNTHETIC closed-loop gate and their fixes; details are in [AUDIT_LEDGER.md](AUDIT_LEDGER.md) §5–8. Stage 6 needs authorization. |

## Evidence columns

- **Software:** the code exists and runs.
- **Native test:** automated Unity Editor / Windows player checks. Synthetic or fixture input only.
- **Physical / human:** real hardware, a live person or participant data. Recorded separately and never inferred from the other two columns.

## The 23 software tasks

| No. | Task (WBS) | Software | Native test | Physical / human | Evidence |
|---|---|---|---|---|---|
| 1 | Pipeline/Gantt review and requirements (1.1) | Done | n/a | n/a | `SOFTWARE_GRADUATION_PLAN.md` |
| 2 | Accounts, profile, results, Continue (3.4, 5.1, 5.2) | Partial | 3 journey cycles passed (22 Oct) | None | `evidence/ai-2026-10-22/journey-editor-qa.txt` |
| 3 | Staged 54 s baseline (4.1) | Implemented, with stage coach, countdown, signal-quality indicator and re-baseline prompt (Stage 5) | Passed (fixtures); player journey runs the coached baseline | None; no real force | `../ai-engine/reports/FINAL_REPORT.md`, `evidence/stage5-2026-10-24/` |
| 4 | Vision metrics and gestures, YOLO (3.2) | Partial | MediaPipe path regression-checked | Live human trial not done (B05) | `HANDS_AND_MEDIAPIPE_AR.md` |
| 5 | Deformation and grasp profiles (3.3) | Partial (earlier prototype; soft-body withdrawn) | Regression-checked | None | `DEFORMATION_ITEM_5.md` (history) |
| 6 | Module 1 Force-Object (3.4.1) | Implemented: rounds (hold/control/pulse), levels, metrics, squeeze liquid, effort gauge, synthesised feedback sounds | Rounds/results checked; sounds checked by EditMode synthesis test | None; sounds not listened to | `player-checks.json`, `evidence/stage4-2026-10-24/` |
| 7 | Module 2 Cognitive (3.4.2) | Implemented: avatar gesture memory (camera/buttons/keys), assembly by hand pointer (5 s pick-up, carry, 2 s place) or mouse, levels | Hand play checked in EditMode and in the build with a synthetic hand | No real webcam hand yet | `RehabExerciseSuite.cs`, `evidence/stage4-2026-10-24/hand-play/` |
| 8 | Module 3 Visuomotor (3.4.3) | Implemented: generated mazes steered by hand pointer (marker follows the hand) or mouse/keys, guided tracing with green/red trail, levels | Hand play checked in EditMode and in the build with a synthetic hand | No real webcam hand yet | `RehabExerciseSuite.cs`, `evidence/stage4-2026-10-24/hand-play/` |
| 9 | 10 Hz feature windows and CSV export (4.2) | Implemented | Passed (40/40 synthetic packets) | No device data | FINAL_REPORT |
| 10 | Seven-class facial model, opt-in (3.2/4.2) | Implemented | Passed (synthetic) | Accuracy not validated | FINAL_REPORT |
| 11 | rPPG HR/HRV, opt-in (3.2/4.2) | Implemented | Passed (synthetic signals) | No reference-sensor validation | FINAL_REPORT |
| 12 | GRU/LSTM/CNN training (4.3) | Implemented on EMG proxy | 32 Python tests pass | Recall 0.49–0.52 vs 0.85 target. **Not fatigue validation.** | FINAL_REPORT |
| 13 | ONNX export and Sentis (4.4) | Implemented (CNN) | Sentis/ORT parity 7.45e-09 | n/a | FINAL_REPORT |
| 14 | Decision policy: adapt / warn / stop (4.5) | Implemented: bounded in-session adaptation in all three modules, plain-language research-labelled pause/stop screens (Stage 5) | 186 native assertions; SYNTHETIC closed-loop replay (EditMode); 10 new product tests | Thresholds experimental; no validated fatigue detection | FINAL_REPORT, `evidence/stage5-2026-10-24/` |
| 15 | Patient dashboard (5.1) | Partial | Retained checks | None | `JOURNEY_ITEM_2.md` |
| 16 | Doctor dashboard (5.2) | Partial | Retained checks | None | `JOURNEY_ITEM_2.md` |
| 17 | Protected records, recovery, CSV (5.x) | Partial | DPAPI readback fixed and checked | Not a validated medical record system | FINAL_REPORT |
| 18 | Phase 2 force envelope and BLE bridge (2.4/6.1) | Partial | Synthetic replay passed | **No Nano/HX711 hardware (B02)** | `BLE_INTEGRATION.md` |
| 19 | English UI, controls, hand pointer (3.4) | Partial: plus first-run tutorials, audio and hand-tone settings (Stage 4) | UI checks pass at 2 sizes / 87 gallery checks at 3 sizes (24 Oct, 2022.3 build) | Human accessibility not tested | `UI_COMPLETION_CHECKLIST.md`, `evidence/stage4-2026-10-24/` |
| 20 | Automated verification (6.2) | Partial | Windows smoke/replay/rendered/journey passed (22 Oct) | Full-game FPS/GC and camera latency not measured | `evidence/ai-2026-10-22/` |
| 21 | Documentation and evidence (6.3) | Partial | n/a | n/a | this file |
| 22 | Voice commands | Future (deferred, D4) | — | — | — |
| 23 | Eye/head pointer | Future (deferred, D4) | — | — | — |

## Open blockers

B02 physical device, B03 clinical data/ethics, B04 Obi/YOLO licences, B05 live human camera trial. See `BLOCKERS.md`.

## Issues found during Stage 0 (for the Stage 1 ledger)

- ~~Absolute package paths in `Packages/manifest.json`~~: fixed in Stage 2 (A-006) with relative `file:../Tools/...` paths.
- `ROLLBACK_2026_10_12.md` is linked from `TASKS.md` and `SOFTWARE_GRADUATION_PLAN.md` but does not exist. Rollback evidence exists only as JSON under `evidence/rollback-2026-10-12/`.
- ~~UI/gallery results only from the Unity 6 build~~: re-run 23 Oct on the 2022.3.20f1 build and passing (60 checks at each of 2 resolutions, 87 at each of 3). See AUDIT_LEDGER §1.
- VirtualGrasp still warns "No baked grasps found" (plan Task 2.2).
- Superseded docs (`TASKS.md`, `REPAIR_REPORT.md`, `CLOSEOUT_2026_10_06.md`, `FINAL_REPORT.md`, `PROGRESS.md`, item docs) were kept in place, not moved to `Docs/archive/`, so existing relative links keep working. Treat them as history.
