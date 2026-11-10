## 2026-10-06 Unity repair and native closeout (WBS 3.4 / 6.2 / 6.3)

[Current closeout](CLOSEOUT_2026_10_06.md) supersedes the pending native/build statements in the historical entries below. The repaired Windows build passes 168/169/168 gameplay checks, 60 UI checks at each of two resolutions and 87 gallery checks at each of three resolutions. Three Play/Stop cycles pass after the TMP font fix. Live human-camera and physical hardware trials remain unverified. Voice remains future task 7; UI remains English.

## 2026-10-03 HandMate UI design adaptation (WBS 3.4)

[HANDMATE_UI_DESIGN.md](HANDMATE_UI_DESIGN.md) records the navy/cyan palette, source fonts and artwork across saved and runtime UI. Source compilation and 65 Python tests passed; scene logic and protected assets are unchanged. Native import/visual verification and a fresh Windows build remain pending because the automated Editor preflight failed licensing before import. Voice remains future task 7.

## 2026-10-02 menu input status

Mouse, keyboard and the whole-hand MediaPipe pointer remain available. Five-second palm dwell and the managed-event cache repair are retained. Voice commands are not implemented; task 7 in [TASKS.md](TASKS.md) is future work. Earlier native/build evidence predates this removal; current file compilation and source-test evidence is separate.

# Product progress — 2026-09-28

## 2026-10-01 item 6 VirtualGrasp joints and poses (WBS 3.2 / 3.4.1)

[HAND_RIG_ITEM_6.md](HAND_RIG_ITEM_6.md) records independent joint bounds/rest recovery, complete atomic SDK input frames, explicit both-hand input/contact readiness and cached selected-side bindings. 107 new source assertions, ten static contracts and 524 earlier regressions passed (641 unique mixed-scope positive checks; 61 Python tests included). Five Editor/four player assemblies compiled against cached references. The exact prior source failed 22 checks, with old exception-aborted groups; four isolated mutations were rejected. All 586 captured VirtualGrasp/hand/profile assets and the captured scene/prior repair sources matched preservation hashes. No Unity, player, webcam or Computer Use was launched. Native SDK/rendering/contact, live accuracy/performance and a fresh Windows build remain pending. Items 1–6 are software-complete; items 7–15 remain pending. Earlier runtime results below are historical.

## 2026-10-01 item 2 index-pointer/dwell source repair (WBS 3.2 / 3.4)

[POINTER_ITEM_2.md](POINTER_ITEM_2.md) records world/aspect-corrected gesture detection, both-hand selection, focus/side/EventSystem/resolution resets, frontmost-hit gating, native left-click dispatch and deliberate rearming after navigation. 106 new source assertions, 17 static contracts and 401 earlier regressions passed (524 unique mixed-scope positive checks). Five Editor/four player assemblies compiled against cached Unity references. The exact old policy failed seven gesture/selection checks; all three isolated mutations were rejected. No Unity, webcam, player or Computer Use was launched. Native uGUI, live usability/performance, Play/Stop and a fresh Windows build remain pending. Items 1–5 are software-complete; items 6–15 remain pending. Earlier runtime results below do not verify this revision.

## 2026-10-01 item 5 deformation/fracture source repair (WBS 3.4.1)

[DEFORMATION_ITEM_5.md](DEFORMATION_ITEM_5.md) records completed canonical application, actual penetration-driven local dents, final recovery upload, bounded plastic retention, sustained damage/impact gates, source-shaped fracture fragments, inactive fragment caching and capped scatter. Cached-reference compilation passed five Editor/four player assemblies; 401 unique mixed-scope software checks passed and three isolated negative mutations were rejected. No Unity/Hub/player, camera or Computer Use was launched. Native appearance/PhysX/performance, Play/Stop and a fresh Windows build remain pending. Items 1, 3, 4 and 5 are software-complete; other checklist items remain pending. The earlier item 5 write blocker is resolved; historical results below retain their original scope.

## 2026-10-01 item 4 grasp/release source repair (WBS 3.4.1)

[GRASP_ITEM_4.md](GRASP_ITEM_4.md) records object-owned collision exclusion until separation, surface-based assisted E pickup, shared HUD target, selected-palm rotation, bounded release velocity, nonalloc furniture clearance and camera re-grab gating. Cached-reference compilation passed five Editor/four player assemblies; 67 grasp-source and eight static integration checks plus earlier regressions passed (331 positive checks across mixed software scopes). Both isolated negative mutations were rejected. Native PhysX/clearance/contact testing, live camera and a new Windows build remain pending under the user's files-only instruction. No Editor/Hub/player/Computer Use was launched; prior hand/key sources and deformation algorithms were preserved. Item 5 was not implemented.


## 2026-10-01 item 3 keyboard/mouse source repair (WBS 3.4 / 3.4.1)

Item 3 is implemented and verified at the files-only/software-policy level. [KEYBOARD_ITEM_3.md](KEYBOARD_ITEM_3.md) records the single Escape/Tab owner, same-frame and held-input barriers, focus/text/shell gating, immediate Demo pose isolation from lingering camera frames, E pause ordering, G geometry validation and Shift/numpad support. Cached-reference compilation passed five Editor/four player assemblies. Tests passed 72 keyboard-policy assertions, nine static binding checks, 130 pose assertions, 37 domain tests and eight vision/force contracts (256 positive checks total). Both negative rearming/Space-Submit mutations were rejected. Native keyboard/mouse behavior, Play/Stop, rendering and a fresh Windows build are pending: the user requested files only. No other numbered checklist item was implemented.

## 2026-10-01 file-only camera direction repair (WBS 3.2 / 3.4.1)

The user's observed opposite horizontal travel and wrist turns were corrected by removing the second X reflection from both wrist movement and palm geometry. Camera neutral facing now turns the upright palm over around Y for both hands. Depth/vertical signs, keyboard/demo pose, handedness selection and Phase 2 remain unchanged. See [CAMERA_DIRECTION_FIX.md](CAMERA_DIRECTION_FIX.md). Cached-reference compilation passed five Editor/four player assemblies; 130 production pose-source assertions with managed test math, 37 domain assertions and 8 Python contracts passed. The previous driver failed 48 direction/facing assertions under the same harness. No Unity/Hub/player or Computer Use was launched for this revision. Native rendering, live-hand accuracy and a fresh Windows build remain pending; earlier runtime counts are historical, not verification of this change.

## 2026-09-30 repair-only handover (WBS 3.2 / 3.4 / 3.4.1 / 6.2)

The user's latest instruction stops feature expansion and restores the existing English game. [REPAIR_REPORT.md](REPAIR_REPORT.md) records the actual startup RectTransform fault, restored authored-menu default, required built-in particle package, Unity 6.4 compatibility fixes and truthful test/build reporting. Fresh native QA passed three Play/Stop cycles (137 checks each, UDP cleanup), then a Windows build at 16:13:15 UTC. Fresh Windows normal/replay/rendered runs passed 137/138/137 checks; UI tests passed 60 checks at each of two resolutions; gallery tests passed 79 checks at each of three resolutions. A separate ordinary Editor startup and menu navigation showed zero Console errors. Existing profiles and unfinished expansion sources were preserved; the research portal is no longer attached automatically. No new module, design, CAD, localization or AI features were added. Known vendor baked-grasp warning, live human trial and physical-device limitations remain documented. Git metadata lock writes remain denied, so no commit is claimed.

## 2026-09-30 visible pointer / no activation repair (WBS 3.2 / 3.4)

The dwell gate now preserves qualified progress through at most 0.2 seconds of gesture flicker, while counting no unqualified time and making no activation during the interruption. Its cursor position also pauses during that grace interval to prevent a briefly curled index from jumping onto an adjacent button. Freshness loss, side change and target change reset progress. Palm-relative gesture thresholds accept smaller distant hands, and the chosen left/right side remains stable beside a relaxed second hand. Frontmost raycast controls receive hover feedback; blocking graphics and disabled controls cannot be clicked through. The status explicitly distinguishes pointing, paused input, dwell and selection.

The first new production-duration player fixture failed because a temporarily curled tip moved from Start to Continue; `evidence/pointer-flicker-first-failure.json` preserves that failure. After the position-grace fix, the rebuilt Windows player passed **137/138/137** normal/replay/rendered checks at **11:41:13–11:42:45 UTC**, including a five-second left-hand dwell with repeated brief classifier dropouts and a visible relaxed right hand. `player-checks.json` contains fresh results. Three Editor Play/Stop cycles then passed **137 checks each** with all three UDP sockets released, completed **11:45:30 UTC** (`evidence/pointer-editor-qa.txt`). Editor gesture/state-machine fixtures and six Python vision/force contracts passed; 992 historical protected hashes had no unexpected differences. This checkpoint supersedes older player counts below. The main scene is restored outside Play; consolidated evidence is in `evidence/pointer-repair-checks.json`. Live hand usability remains unverified.

## 2026-09-30 Unity interaction and pointer checkpoint (WBS 3.2 / 3.4 / 3.4.1)

The saved project now keeps the uGUI Submit mapping on Enter/Return and removes Space, so simulated grip cannot activate a previously focused menu action. During gameplay the EventSystem navigation/selection is disabled. The MediaPipe menu cursor chooses an actually pointing hand over a merely visible hand, and a new end-to-end loopback acceptance check sends 21 landmarks to the runtime cursor, dwells on Start, and confirms navigation to Modules without opening a physical webcam. The visual test exposed a separate intermittent failure: the held ball could miss every finger probe, or envelop all 15. Its target now follows the selected VirtualGrasp finger-pad cluster with an index-side offset; contact remains local on both hands. A transient Windows encrypted-profile replacement error now gets bounded retries while retaining the prior copy.

The latest Unity Windows build passed 136 headless, 137 explicit synthetic Phase 2 replay, and 136 rendered checks (`Docs/player-checks.json`). Three Editor Play/Stop cycles passed with UDP cleanup (`Logs/editor-qa.txt`); 51 Python contract tests passed across the repository. Twelve fresh 1280×720 screenshots are in `Docs/screenshots/player-1280x720`. These are software fixtures. Real-hand five-second usability, actual Nano/HX711 input, treatment efficacy, and clinical safety remain unverified. The earlier checkpoints below remain historical and are not claims for the current build.

## 2026-09-29 Unity checkpoint (WBS 3.2 / 3.4 / 3.4.2 / 3.4.3)

The saved scene now has the repaired high-contrast dropdown/top bar and playable research exercises in Modules 02/03. MediaPipe index-pointing menu input with a five-second dwell ring and guest practice are implemented without removing mouse/keyboard. The memory pattern timer was corrected after visual inspection. A saved test-only acceptance object was removed from the scene and future verification rejects it. Three Editor Play/Stop cycles and the final Windows player checks passed (134/135/134). Physical hand-dwell usability, Nano hardware and clinical validation remain open. The subsequent UI-only redesign starts from this tested baseline; gameplay/vision/force/data paths are protected. `ui-redesign` branch creation and commits are blocked by `.git` lock permissions, even with scoped filesystem permission.

The requested product expansion includes GP2 Modules 2/3 and doctor/AI work beyond the original GP1 Gantt. The source WBS and dates are preserved. This file tracks *current verification*, not aspirations.

| Gate | State | Evidence and next exit work |
| --- | --- | --- |
| M0 foundation audit | In progress | Source PDFs/Gantt copied to `Source/`; baseline dirty-tree hashes and text patch in `evidence/`; requirements and audit established. The user's GUI Editor now resolves its Unity entitlement and compiled the current changes; Git metadata write still fails. |
| M1 architecture | In progress | `RehabSession` moved with its `.meta` to `_Project/Core` asmdef; force/hand contracts and current adapters added. Unity Editor scene/logic verification passed on 2026-09-28. Bootstrap and incremental migration remain. User narrowed ongoing work to Unity only; no further CAD work is planned. |
| M2 patient path / protected storage | In progress | Windows DPAPI codec and schema-1 encrypted migration/backup code added. A 2026-09-28 Editor Play acceptance cycle passed encrypted primary/backup, migration, tamper recovery and profile navigation fixtures. Password hash exists but login, baseline and results remain. |
| M3 firmware/force | Not started | Import canonical Phase 2 without losing original; v1 compatibility/v2 negotiation; USB/BLE/simulator contract. Physical bench evidence pending. |
| M4 vision | Not started | Tracking quality/calibration, geometric gesture fallback, optional ONNX model. Human-hand verification pending. |
| M5 adaptation | In progress independently | Python research-demo safety policy and six deterministic tests pass. Feature windows, model/fallback and Unity integration remain; clinical data pending. |
| M6 Modules 1–3 | Not started | Metric-complete three modules and replay checks. |
| M7 patient/doctor | Not started | D1–D5, role guard, charts, explicit exports. |
| M8 UX/localization | Not started | EN/AR, RTL shaping, access/sound/onboarding and visual review. |
| M9 hardening | Not started | Fault injection, performance/GC, resolutions, all journeys. |
| M10 delivery | Not started | Final Windows release, evidence matrix, documentation and limitations. |

## Existing baseline carried forward

The pre-M0 Unity checkout already has a rendered apartment, imported VirtualGrasp, Module 1 gameplay, camera/Phase 2 bridges, local plaintext profiles, and UI previews for future modules and doctor. `QA.md` records an earlier GUI Editor result of 127 checks in each of three Play/Stop cycles and Windows player checks of 127/128/127. These are historical software results for the preserved dirty tree, **not** M0–M10 acceptance and not physical/clinical validation. Re-run after product changes.

M0 observed Git HEAD `7af9546` on `3.2-bimanual-tracking` with 29 modified paths before new work. `evidence/pre-m0-working-tree.json` contains SHA-256 of those files and `pre-m0-text.patch` captures their text diff. No reset/checkout was performed. Git cannot currently create `.git/index.lock` even after a scoped permission grant; no new commit is claimed.

Earlier independent checks: imported Firmware C++ suites and 18 Python tests passed; original R1 CAD, committed R2 CAD and eight mechanical tests passed before the Unity-only scope change. The parent's uncommitted R2 exploded-assembly edit is preserved in `evidence/assembly-parent-uncommitted.scad`; the unified delivery uses the last verified committed R2 source matching its 13 exports. Python vision/bridge tests passed (2 + 4). Core and adapter C# subsets compiled with Unity's bundled Roslyn compiler.

Current Unity GUI evidence: `Logs/editor-gate-result.json` records a passed scene/logic/Phase 2 check after successful Editor import. A defect in VirtualGrasp profile discovery was found from `Editor.log` and repaired with a project-owned `Resources/VG_ControllerProfiles/VG_CP_Rehab.Input.asset` bound to the scene. One full Editor Play cycle then passed **130 runtime checks**, including independent right/left SDK finger motion, encrypted profile migration/recovery and input contracts; it also released UDP 27817/27818/27819. The three-cycle run was not yet completed: cycle 2 stalled in the user Editor, so Windows build/player evidence for these changes remains pending. Historical 127-check evidence is not substituted for it.


## 2026-10-06 graduation item 2 update

Graduation item 2 source now integrates account/profile/preparation/explicit quick-baseline consent/module/results/linked Continue around authored panels. Owner-scoped crash recovery and save guards added. Five Editor/four player assemblies compile offline; 40 product and 69 Python tests pass. Native three-cycle journey and new Windows build NOT RUN: Editor Software Terms requires the unavailable user. See JOURNEY_ITEM_2.md; original M0-M10 rows above are historical, not current item-2 completion claims.


## WBS 4 AI work - 2026-10-22
See [AI progress](../../ai-engine/PROGRESS.md) and [measured report](../../ai-engine/reports/FINAL_REPORT.md). Public EMG proxy models are not validated device-fatigue models. Existing hand, objects and scene are preserved.
