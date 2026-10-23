# Product decisions

| ID | Date | Decision and reason |
| --- | --- | --- |
| D01 | 2026-09-28 | Keep `E:\GP\codex\GripRehab-Unity` as the single **delivery** repo; copy relevant Phase 2 sources into it with provenance, leaving the parent repo intact. This follows the user's explicit project-home request. |
| D02 | 2026-09-28 | Preserve Gantt WBS IDs/dates and mark full Modules 2/3, doctor and AI as requested scope extension beyond the chart's GP1 plan. Do not edit source calendar to imply those tasks were originally scheduled. |
| D03 | 2026-09-28 | Keep BLE v1 service/characteristics and datagram format unchanged. Negotiate v2 explicitly; reject unknown versions rather than interpreting them as v1. |
| D04 | 2026-09-28 | Distinguish Demo/Synthetic, camera closure estimates, hardware Newtons and any model estimate in UI, records and tests. Never auto-fallback from missing hardware to keyboard. |
| D05 | 2026-09-28 | The current non-Obi mesh deformation and geometric vision are legitimate engineering fallbacks if licensed compatible Obi/YOLO assets or validated models are unavailable, provided the UI and final report label them. |
| D06 | 2026-09-28 | Treat this as research software, not a certified therapeutic device. Clinical thresholds, participant fatigue accuracy and physical sensor validation require separate evidence. |
| D07 | 2026-09-28 | Import the last committed, export-verified R2 CAD `assembly.scad` into the unified repo. Preserve the parent's uncommitted exploded-assembly variant in `evidence/` rather than shipping mismatched export hashes or altering the parent checkout. |

| UI01 | 2026-09-29 | Keep uGUI: the current saved scene and input bindings use it. TMP and DOTween are absent; legacy Text cannot yet claim correct Arabic shaping/RTL. |
| UI02 | 2026-09-29 | Absent Splash/Login/Settings/Baseline/Fatigue/History/Charts routes remain labeled concepts until real contracts exist. UI-only work cannot invent their backend or change flow. |
| UI03 | 2026-09-29 | Snapshot protected-path hashes after the preceding feature work; keep all prior changes and require subsequent UI-only changes to leave these hashes unchanged. |
| UI04 | 2026-09-29 | Keep the existing static backdrop until GPU/CPU cost of a live shader is measured. |
| UI05 | 2026-09-29 | The user's later direct request to repair the dwell pointer and Space grip supersedes the UI-only protected-path rule for those two faults. The original protected-path snapshot is retained as a historical baseline; this targeted exception is not represented as an empty protected diff. |
| UI06 | 2026-09-29 | Remove Space from the legacy uGUI Submit axis while retaining Enter for keyboard menu confirmation. Space remains a simulated squeeze only in explicit Keyboard Demo; camera and Phase 2 modes keep their separate provenance and do not silently substitute keyboard force. |
| UI07 | 2026-09-29 | The sandboxed Windows smoke-test process writes screenshots under `Logs/player-screenshots`; the test runner checks freshness and size. The user-opened Editor then imports the 12 fixed filenames into `Docs/screenshots/player-1280x720` via a fixed gate command. Missing screenshots fail the rendered check, and Editor images in `Docs/images` are preserved. |
| UI08 | 2026-09-30 | Use TMP from the installed uGUI package for new accessibility labels; preserve legacy Text references. Full Arabic shaping/RTL remains separate work. |
| UI09 | 2026-09-30 | Store only presentation preferences under `GripRehab.UI.v1.*` PlayerPrefs keys. No patient data is copied. The thin bridge configures the existing dwell cursor. QA isolates settings and profiles. |
| UI10 | 2026-09-30 | Font scale is a requested maximum with fitting, labelled FIT. Not every label renders at 150%; full enlarged-text reflow needs a separate layout audit. |
| UI11 | 2026-09-30 | Add dwell adjustment and mute buttons for patients unable to drag sliders. Modal scrim and actions CanvasGroup block background input. Feedback decorates controls without submitting gameplay keys. |
| UI12 | 2026-09-30 | Use three native shared-sprite glow layers instead of a new shader/blur dependency. Motion, alpha and tint come from the theme; low-VRAM devices use one layer, solid/high-contrast disables it. This supersedes UI04's static-only choice, without claiming isolated GPU budgets or hand parallax. |
| UI13 | 2026-09-30 | Add a separate saved English UI Gallery and eight component templates. It contains no patient, camera or force state. Only the explicit `-apartment-gallery-smoke` fixture navigates there from the Windows player; normal game flow is unchanged. The full library/AR requirement remains open. |
| UI14 | 2026-09-30 | Observe Button.onClick for activation sounds instead of PointerDown. Mouse, keyboard Submit and dwell share the existing event and do not add duplicate activation handlers. |
| UI15 | 2026-09-30 | Schedule subsequent Editor QA cycles from update after Edit-mode transition settles, with bounded timeout and a diagnostic heartbeat. Build/Verify preserve authored scene edits. |
| UI16 | 2026-09-30 | Preserve dwell progress and cursor position through at most 0.2 seconds of gesture flicker, counting qualified time only. Tracking loss resets immediately; longer gesture interruption, side changes and target changes reset. Prefer the active pointing side over a relaxed second hand and respect frontmost raycast blockers. This targeted repair follows the user's visible-pointer/no-click report and does not relax sensor freshness. |
