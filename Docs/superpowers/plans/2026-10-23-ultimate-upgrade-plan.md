# GripRehab Ultimate Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **STATUS: PLAN ONLY. NOT AUTHORIZED FOR IMPLEMENTATION.** Do not start any task until the user explicitly says so. Each stage below ends with a stop-for-review gate (AGENTS.md / PROJECT_PLAN.md §3).

**Goal:** Take GripRehab from today's research prototype (uncommitted, Unity 2022.3.20f1, partial 23-task list) to a polished, stable, fully-traced "premium release" desktop rehabilitation game. That means three complete modules, a closed adaptive loop, full patient and doctor dashboards, presentation-grade visuals and audio, measured performance, an installer and graduation evidence. Physical and clinical claims stay honest throughout.

**Architecture:** Keep what works: the single authored scene `Assets/Scenes/GripRehab.unity`, the WBS-numbered `Assets/Rehab/*` modules, the Product/ProductIntegration assemblies, the Python MediaPipe/AI workers over loopback UDP 27817/27818/27819, and Sentis CNN inference. Upgrade in stages: secure → audit → fix → gameplay → presentation → closed loop → dashboards → performance → hardware readiness → release. Every stage is gated by automated checks plus a rendered/computer-use playthrough.

**Tech Stack:** Unity 2022.3.20f1 LTS (see Decision D1), URP 14.0.10, uGUI + TextMeshPro 3.0.6, Sentis 2.0.0, Gleechi VirtualGrasp, Brick Project Studio apartment, Python 3 (MediaPipe, OpenCV, PyTorch, ONNX Runtime), Arduino Nano 33 IoT firmware (BLE).

**Spec (sources this plan argues from):**
- `E:/GP/Pipeline/Hand_Rehab_Pipeline (1) (1).pdf`: architecture (real world / virtual world / AI loop / dashboards)
- `E:/GP/Pipeline/Blockdiagram (1).pdf`: patient journey 01–06 and doctor journey D1–D5
- `E:/GP/Pipeline/ganttchart (1).html`: WBS 1.1–6.3, W1 2026-09-12 → GP1 Done W22 2027-02-06
- `E:/GP/codex/PROJECT_PLAN.md`: WBS mapping, acceptance criteria, gaps G01–G17
- `Docs/SOFTWARE_TASKS_23.md` (22 Oct 2026): current status of the 23 software tasks
- `ai-engine/reports/FINAL_REPORT.md`: AI delivery and its limits

---

## Where the project actually stands (23 Oct 2026, today = Gantt W6)

| Area | Current state | Gap to "ultimate" |
|---|---|---|
| Repository | Branch `3.4.1-restore-before-interaction`. **~511 uncommitted paths** (192 M, 62 D, 257 ??), ≈77k lines deleted vs HEAD. HEAD = Unity 6000.4.4f1; working tree = Unity 2022.3.20f1 rollback + AI work. No remote. | **Highest risk.** One bad checkout loses weeks of work. Must checkpoint first. |
| Docs | ~45 top-level docs in `Docs/` with contradictory statements (README says Unity 6000.4.4f1; ProjectVersion says 2022.3.20f1; TASKS.md vs SOFTWARE_TASKS_23.md). | One status source of truth. |
| Scene / env | Furnished Brick apartment, Living Room + Kitchen stations, 6 material profiles. | Lighting/GI, material and audio polish, object variety tied to the PDF's Sphere/Cylinder/Pencil grips. |
| Hand | VirtualGrasp rig, MediaPipe whole-hand pointer, keyboard demo. "No baked grasps found" warning. Live human trial never done (B05). | Grasp DB baking, feel tuning, live trial protocol. |
| Deformation | Basic local deformation and breakage (soft-body replacement withdrawn). Obi not licensed (B04). | Decide Obi vs in-house; premium squash/recover/fracture feel. |
| Module 1 | Rounds and results exist. | A full game loop: objectives, progression, feedback, grip-shape mapping. |
| Modules 2/3 | Research prototypes (`SequenceMemoryTask`, `SpatialAssemblyTask`, `VisuomotorTasks`). | Full PDF scope: avatar sequence memory, spatial assembly, maze navigation with collision penalty, visual tracing with green/red feedback. |
| AI | 54 s baseline, 10 Hz windows, face/rPPG opt-ins, GRU/LSTM/CNN on EMG proxy (recall ≈0.52, target 0.85 failed), Sentis CNN, 4-category policy. | Wire adaptation into *all* modules with visible UX; never claim clinical accuracy. |
| Dashboards | Patient/doctor local previews, DPAPI-protected store. | Full D1–D5 doctor journey, charts, history, notes, export. |
| Perf | Menus only, 270–430 FPS on i7-11800H/RTX 3060. Gameplay with camera never measured. | 60 FPS p95 in full gameplay with camera, GC budget, 30-min soak. |
| Hardware | Firmware/BLE contracts and synthetic replay only (B02). | Calibration wizard and readiness. Physical tests recorded separately when hardware exists. |
| Release | `Builds/Windows/GripRehab.exe` (630 MB folder). | Installer, first-run, crash logging, demo video, final evidence matrix. |

## Global Constraints

- Canonical root `E:\GP\codex`; Unity project `E:\GP\codex\GripRehab-Unity`. Never work in the old OneDrive workspace.
- Preserve WBS numbering. Commit subjects start with the exact WBS ID (e.g. `3.4.1 Add round objectives`). New work maps to an existing WBS ID. No invented `0.x` IDs.
- Record **real physical testing separately** from simulation, compilation, synthetic replay and design checks. Never label camera estimates as Newtons or clinical ROM.
- Never claim clinical validation, medical-device status or validated fatigue detection.
- No purchases, emails to Obi developers, Google Form creation or ethics submissions without explicit user action. Prepare drafts locally only.
- Do not modify vendor code under `Assets/com.gleechi.unity.virtualgrasp` or `Assets/ThirdParty/*`. Wrap it instead.
- Keyboard/mouse must always remain a complete fallback input.
- English UI is primary. Arabic docs stay as they are. No Arabic UI unless D5 says so.
- Stage only named files (never `git add -A` across the tree) once the Stage 0 checkpoint exists.

## Decisions the user must make before implementation

| ID | Decision | Recommendation |
|---|---|---|
| D1 | Engine: stay on **2022.3.20f1** (working tree, AI/Sentis verified) or return to **6000.4.4f1** (HEAD)? | **Stay on 2022.3.20f1** through GP1. You requested the rollback, and the AI stack is verified there. Revisit Unity 6 after GP1. |
| D2 | Deformation: buy **Obi Softbody/Fluid** licences, or finish the in-house deformer? | In-house for GP1, with Obi as an optional adapter if you buy it. The Fluid role then becomes a squeeze-liquid VFX on sponge/tomato. |
| D3 | Modules 2 and 3: full build now (scope variance vs Gantt GP2) or polish the existing prototypes only? | Full build. It was already moved forward on 2026-09-23. |
| D4 | Extras: voice commands (task 22) and eye/head pointer (task 23)? | Defer to post-GP1. Keep them out of this plan's critical path. |
| D5 | Arabic in-game UI? | Defer. Use Unity Localization tables only if you want it. |
| D6 | Git remote (private GitHub) for off-machine backup? | Yes. A private repo with LFS for large binaries. |
| D4 (revised 2026-10-25) | Voice commands and eye tracking | **In scope** as Stages 13 and 14 (your 2026-10-25 request). This supersedes the earlier deferral. |
| D7 (2026-10-25) | Props for the new activities | **Built in code:** procedural meshes and materials (books, shelf slots, plates, cups, tray, box, knife, board, vegetables). No downloads or licences. |
| D8 (2026-10-25) | Eye tracking without hardware | **Webcam gaze estimate** from MediaPipe face/iris landmarks. Coarse, optional, off by default, with stated accuracy limits. |
| D9 (2026-10-25) | Speech engine | **Windows built-in, offline:** Unity keyword and dictation recognizers plus Windows text-to-speech. *Revised 2026-10-27 (Stage 13): Windows free dictation is an online service, so you chose offline keyword commands and letter-by-letter spelling only; free dictation is not offered.* |
| D10 (2026-10-25) | Order of the new stages | **Dependency order:** contact, deformation, activities, UI, audio, voice, eye, assistant, audit, research. |

---

## Stage 0: Safeguard and single source of truth (WBS 1.1 / 6.3)

### Task 0.1: Checkpoint the uncommitted working tree
**Files:** `.gitignore`, `.gitattributes` (create; LFS rules for `*.fbx *.png *.psd *.onnx *.wav *.tgz`), all current changes.
- [ ] Back up first: zip the project, excluding `Library/` and `Builds/`, to `E:\GP\backups\GripRehab-2026-10-23.zip`. Verify the zip lists `Assets/Scenes/GripRehab.unity` and `ai-engine/models`.
- [ ] Create branch `3.4-upgrade-baseline` from the current state (no checkout of other branches).
- [ ] Commit in logical WBS groups, staging named paths only: `3.1 Restore Unity 2022.3 project settings and packages`, `3.3 Withdraw soft-body/contact replacement`, `4.1-4.5 Add fatigue AI, baseline and policy`, `3.4 Patient journey and product store`, `6.3 Evidence and reports`.
- [ ] Verify: `git status --short | wc -l` → `0` (except ignored), and `git log --oneline -8` shows the WBS-prefixed commits.
- [ ] Tag `v0.9-prototype-2026-10-23`.

### Task 0.2: Repository hygiene
**Files:** `Assets/_Recovery/*`, `Assets/SceneBackups/*`, `Tools/__pycache__`, `.gitignore`.
- [ ] Move `_Recovery` and `SceneBackups` scenes out of `Assets/` into `Docs/archive/scenes/` (they bloat import and confuse the editor). Verify that Unity opens `GripRehab.unity` with 0 missing-script warnings.
- [ ] Ignore `__pycache__/`, `Logs/`, `Builds/`, `UserSettings/`, `.venv*/`.
- [ ] Commit `6.3 Archive recovery scenes and tighten ignores`.

### Task 0.3: One status document
**Files:** Create `Docs/STATUS.md`; modify `README.md`; move superseded docs into `Docs/archive/`.
- [ ] `STATUS.md` = one table of the 23 tasks plus WBS rows, with columns *Software*, *Native test*, *Physical/human evidence*, *Evidence link*.
- [ ] Fix the README engine version (2022.3.20f1) and remove contradictory "current delivery" headers.
- [ ] Verify: `grep -rn "6000.4.4f1" README.md Docs/STATUS.md` returns nothing, unless D1 chose Unity 6.

**Gate 0:** a clean tree, a tag and a single status doc. Stop for review.

---

## Stage 1: Full review and audit (no behaviour changes)

### Task 1.1: Automated baseline
- [ ] Run and record pass counts in `Docs/evidence/audit-2026-10/baseline.md`:
  - `python Tools/offline_verify.py`
  - `python -m unittest Tools.Vision.test_vision_bridge Tools.Phase2Bridge.test_unity_bridge`
  - `.\ai-engine\Run-AI.cmd test` (expect 32 pass)
  - Unity batch EditMode/PlayMode tests: `Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml`
  - `python Tools/run_player_checks.py`, `Tools/run_ui_checks.py`, `Tools/run_gallery_checks.py`, `Tools/run_ai_player_checks.py smoke|replay|journey|rendered`
- [ ] Any failure becomes a ledger item in Task 1.4. Do not fix it here.

### Task 1.2: Code review per assembly
- [ ] Run `/code-review high` per area: `Assets/Rehab/3.2-vision`, `3.3-deformation`, `3.4-game-modules`, `3.4.1-*`, `3.4.2-*`, `3.4.3-*`, `6.1-*`, `Assets/Product*`, `Assets/UI`, `Tools/Vision`, `ai-engine/src`.
- [ ] Focus on UDP lifecycle and port release, thread safety in receivers, null and stale-sample handling, DPAPI store failure paths, per-frame allocations, and dead editor tooling left by the withdrawn soft-body work (references to the deleted `SoftBodyTopology` and `HandSkinSurface`).

### Task 1.3: Playthrough audit with computer use
- [ ] Use computer use (Unity Editor + built player) to play every route: Guest → each of the 3 modules; Sign-in → baseline → Module 1 → results → dashboard; Doctor → patient → history; Appearance/high-contrast; camera loss/recovery; keyboard-only. Capture screenshots to `Docs/evidence/audit-2026-10/play/`.
- [ ] Score each screen on a rubric (readability, feedback clarity, visual polish, responsiveness, bugs) from 1 to 5.

### Task 1.4: Defect and gap ledger
**Files:** Create `Docs/AUDIT_LEDGER.md`.
- [ ] One row per finding: ID (`A-###`), WBS, severity (S1 crash/data loss … S4 cosmetic), repro, evidence, owning stage.
- [ ] Map every PDF and block-diagram requirement to a status (journey 01–06, D1–D5, measured parameters per module).

**Gate 1:** ledger reviewed with you; severities agreed. Stop.

---

## Stage 2: Fix and stabilise (ledger S1/S2 first)

### Task 2.1: S1/S2 defects
- [ ] For each ledger item: write a failing EditMode/PlayMode or Python test reproducing it, fix it, re-run, then commit `<WBS> Fix A-###: <summary>`. Use superpowers:systematic-debugging.

### Task 2.2: VirtualGrasp Grasp DB baking (WBS 3.4.1)
**Files:** `Assets/Editor/VirtualGraspAuthoring.cs`, VG project settings.
- [ ] Bake grasps for all 6 objects plus the Sphere/Cylinder/Pencil grip proxies using the VG SDK bake flow.
- [ ] Verify: Play-mode console has **no** `No baked grasps found`, and the native grasp check passes for both hands.

### Task 2.3: Dead code and editor tooling cleanup
- [ ] Remove editor menus and scripts that target deleted soft-body/contact systems. Verify that compilation has 0 warnings from project assemblies (vendor warnings listed separately).

**Gate 2:** all S1/S2 closed and the full Stage 1 suite green. Stop.

---

## Stage 3: Gameplay "premium" pass (WBS 3.4, 3.4.1, 3.4.2, 3.4.3)

Shared interfaces (from `Assets/Rehab/3.4-game-modules/RehabExerciseSuite.cs` / `RehabApp.cs`). Every module implements:
`IRehabModule { string Id; void Begin(SessionContext ctx); void ApplyDifficulty(DifficultyLevel level); void Pause(PauseReason r); void Resume(); ModuleResult End(EndReason r); event Action<MetricSample> Metric; }`
If an equivalent already exists, adopt it instead of adding a parallel one (decide in Task 3.0).

### Task 3.0: Module framework contract
- [ ] Unify the three modules behind one lifecycle, one difficulty scale (L1–L10), one metric event stream feeding the 10 Hz window builder and one results schema.
- [ ] Tests: PlayMode `ModuleLifecycleTests` covering begin→pause(warning)→resume→end, latched stop rejects resume, and difficulty clamped to 1..10.

### Task 3.1: Module 1, Force-Object Interaction, full loop
- [ ] Session = warm-up → N rounds → cool-down. Round types: *reach target force band and hold* (endurance), *squeeze without breaking* (control), *pulse rhythm* (stability).
- [ ] Map grip shape → object family: Sphere → ball/tomato/sponge, Cylinder → cup/mug/bottle, Pencil → pen/brush/cracker. Shape comes from the Phase 2 envelope `shape` field, or from a selector in keyboard/camera mode.
- [ ] Juice: force ring HUD, target band, combo/streak, star rating, object squash-recover curves, crack/fracture VFX, success/fail audio.
- [ ] Metrics per PDF: force production, grip endurance, gesture stability. Units shown honestly: N only when the source is calibrated Phase 2, otherwise "% effort (estimated)".
- [ ] Tests: replay fixtures for target attainment, hold timeout, break, release/reset, invalid input → pause.

### Task 3.2: Module 2, Cognitive Performance
- [ ] Gesture Sequence Memory: an avatar demonstrates a gesture sequence (open/fist/pinch/point …), then the player observes, memorises and executes. Length grows with difficulty.
- [ ] Spatial Assembly: virtual hand moves pieces to matching slots.
- [ ] Metrics: reaction time, accuracy, trajectory, velocity.
- [ ] Gesture recognition: the MediaPipe landmark classifier now. YOLO only if a project-trained model exists (G06); otherwise document the substitution.
- [ ] Tests: deterministic sequence seeds, scoring fixtures, timeout and wrong-gesture paths.

### Task 3.3: Module 3, Visuomotor Coordination
- [ ] Maze Navigation: avatar moves through a maze by hand gesture. Shortest-path reference (A*), collision → score reduction.
- [ ] Visual Tracing: trace a dashed shape. Within tolerance = green, outside = red. Sync accuracy score.
- [ ] Metrics: reaction time, trajectory, gesture stability, motion accuracy.
- [ ] Tests: path-deviation math, collision penalty, tracing tolerance boundaries.

### Task 3.4: Deformation upgrade (WBS 3.3), per D2
- [ ] In-house: GPU/Burst vertex deformer with per-material stiffness, damping, plastic memory and break threshold. Squeeze-liquid particles for sponge/tomato fill the "Fluid" role.
- [ ] If Obi is licensed: `IDeformationBackend` adapter, switchable per object.
- [ ] Tests: bounded displacement, recovery to rest within tolerance after release, no NaN vertices over a 10k-frame replay.

**Gate 3:** all 5 tasks (2 + 2 + 1 exercises) playable end-to-end in the built player, with your playtest sign-off. Stop.

---

## Stage 4: Visual, audio and UX presentation pass (WBS 3.1, 3.4, 5.x)

### Task 4.1: Lighting and rendering
**Files:** `Assets/Settings/PC_RPAsset.asset`, `PC_Renderer.asset`, `DefaultVolumeProfile.asset`, scene lighting.
- [ ] Baked GI plus light probes for the apartment, reflection probes per room, mixed lighting for the hand, SSAO, tonemapping (ACES), colour grading per room, subtle DoF on the station focus. Use the `unity:urp-postprocessing` skill.
- [ ] Quality tiers Low/Medium/High/Ultra in `RehabSettings`, auto-selected by GPU.
- [ ] Verify: rendered screenshots before/after at 1920×1080, and the rubric score improves in the Task 1.3 re-run.

### Task 4.2: Hand and object materials
- [ ] Skin shading on the VG hand (subsurface approximation), contact highlight, per-material PBR review of the 6 objects plus new grip-family objects.

### Task 4.3: Audio
**Files:** create `Assets/Audio/` with an `AudioMixer` (Master/Music/SFX/UI/Voice-guide).
- [ ] Ambient room tone per room, squeeze/creak/crack/splash SFX per material, UI clicks, success stingers, calm music bed with ducking during instructions. Use the `unity:audio-setup-mixers` and `unity:optimize-audio` skills. Licence-clean sources only, recorded in `Docs/CREDITS.md`.

### Task 4.4: UI/UX motion and onboarding
- [ ] Animated transitions, a first-run tutorial per module, an always-visible input/source badge (CAMERA / KEYBOARD / PHASE 2 / SYNTHETIC), toast system, and a pause menu reachable with dwell.
- [ ] Accessibility: text scale, high contrast, reduced motion, colour-blind-safe green/red (add shape/icon cues to tracing), dwell time setting.
- [ ] Sync with the Figma board (if the Figma plugin is enabled).

**Gate 4:** visual and audio review via screenshots and a short capture. Stop.

---

## Stage 5: Closed adaptive loop and safety UX (WBS 4.1–4.5, 6.2 software)

### Task 5.1: Baseline flow polish
- [ ] Guided 54 s baseline with animated coach, countdowns and a per-stage quality indicator. Re-baseline prompt when shape or calibration changes.

### Task 5.2: Adaptation in every module
- [ ] The policy's `DifficultyLevel` drives Module 1 force band/hold time, Module 2 sequence length/time, and Module 3 maze complexity/tolerance. Bounded steps and cooldown, as already specified.
- [ ] Tests: replay a policy fixture into each module and assert parameter changes stay in bounds.

### Task 5.3: Fatigue warning and safety stop UX
- [ ] One sign → full-screen calm warning with Resume/Quit (dwell and keyboard). Two signs → latched stop screen, session saved, no auto-restart. Reasons are shown in plain language and labelled "research indicator".
- [ ] Tests: simultaneous signals, stale prediction, bad-input hold, user quit during warning.

### Task 5.4: Model runtime decision
- [ ] Keep the Sentis CNN (GRU is unsupported). Re-export only if real-device data arrives. The UI never shows proxy-model output as real fatigue when the force source is real (existing guard), verified by test.

**Gate 5:** a closed loop demonstrated with synthetic replay (labelled SYNTHETIC). Stop.

---

## Stage 6: Dashboards (WBS 5.1, 5.2)

### Task 6.1: Patient dashboard (journey step 05)
- [ ] Session summary cards (force, endurance, stability, reaction time, accuracy), trend charts across sessions, baseline comparison, stopped/incomplete sessions clearly marked, missing data shown as "—", never 0.

### Task 6.2: Doctor dashboard (D1–D5)
- [ ] D1 sign-in, D2 patient list/search, D3 progress statistics by module with fatigue/stop events, D4 notes and recommendations (free text plus templated, clearly clinician-authored), D5 save/sign-out with audit trail.
- [ ] Export: CSV (existing) plus a PDF session report.
- [ ] Tests: fixture patients reproduce exact numbers, no cross-patient mixing, permission checks.

**Gate 6:** both journeys walked through with computer use against fixtures. Stop.

---

## Stage 7: Performance, stability and robustness (WBS 6.2)

### Task 7.1: Budgets and profiling
- [ ] Budgets: 60 FPS p95 at 1920×1080 High with camera worker running on the reference laptop (i7-11800H/RTX 3060). GC alloc < 1 KB/frame in gameplay. Camera-to-hand latency measured and reported.
- [ ] Development build plus Profiler/Memory Profiler captures per module. Fix the hotspots (the `unity-perf` plugin helps here).

### Task 7.2: Soak and fault injection
- [ ] 30-minute automated soak per module (synthetic input): no leak growth > 10%, no exceptions.
- [ ] Fault injection: kill the camera worker, malformed UDP, port already in use, disk full on save, corrupt profile file. Each produces a graceful message and no data loss.

### Task 7.3: Crash and log capture
- [ ] Rolling log file in `%LOCALAPPDATA%/GripRehab/logs`, an unhandled-exception handler, and a "copy diagnostics" button.

**Gate 7:** budgets met, or misses documented with numbers. Stop.

---

## Stages 8–17: requested upgrades (added 2026-10-25, run before Stages 18–19)

You added ten requirements after Stage 7. They run first, in **dependency order** (decision D10), and the original hardware and release stages follow as Stages 18–19.

Each stage starts only when you authorize it ("Start Stage N, then stop for review") and ends at its Gate. Every checkbox has a row in [REQUIREMENTS_CHECKLIST.md](../../REQUIREMENTS_CHECKLIST.md) with one of these statuses:
- Not Started
- In Progress
- Implemented
- Tested
- Blocked

| Stage | Requirement (your list) | WBS | Why this position |
|---|---|---|---|
| 8 | 2. Hand–object interaction and contact | 3.4.1, 3.3 | Deformation needs stable contact points and forces first |
| 9 | 1. Object deformation system | 3.3 | Builds on Stage 8 contacts |
| 10 | 3. Six multi-step activities | 3.1, 3.4.1 | Needs good contact and deformation (packing, cutting) |
| 11 | 7. UI modernization and design system | 3.4, 5.1, 5.2 | One design system before the voice, gaze and assistant screens |
| 12 | 6. Audio and sound design | 3.1, 3.4 | Material sounds need the Stage 9 materials and Stage 10 objects |
| 13 | 5. Voice commands and speech-to-text | 3.4 | Uses Stage 11 indicators; feeds Stage 15 |
| 14 | 4. Optional eye tracking | 3.2 | Shares the dwell/focus pipeline and Stage 11 focus states |
| 15 | 8. Optional voice assistant | 3.4 | Needs Stage 13 speech and the Stage 10 activity state |
| 16 | 9. Pipeline, block diagram and Gantt audit | 1.1, 6.3 | Audits everything above |
| 15A | Environment upgrade, kitchen and living room (added 2026-10-26) | 3.1 | After the activities, audio and assistant use the rooms; before the Stage 16 audit so it is audited. Lettered to keep every other stage number |
| 17 | 10. Next-stage research proposal | 1.1 | Follows the Stage 16 gap analysis; recommendations only |

### Global rules for Stages 8–17

- **Preserve what works:** the project, source, assets, scenes, working systems and **every current input method** (mouse, keyboard, hand pointer). These are additions and upgrades, not a replacement game.
- **Inspect first:** read the repository and verify the engine version, APIs and hardware before editing. Reuse sound components; rebuild only what genuinely needs it. No duplicated systems, incompatible dependencies or unrelated changes.
- **No placeholders or overclaiming:**
  - Nothing is called complete when it is partial.
  - "AAA" quality is never promised without measured feasibility.
  - Physical and human tests are recorded separately from simulations, compilation and design checks.
- **Blockers are documented:** hardware, engine, assets, licensing or services, each with the most practical alternative.
- **Robustness:** error handling, configuration options and performance safeguards (the Stage 7 budget is ≥ 60 FPS at p95 and < 1 KB/frame GC) are part of every feature.
- **Tests:** add or update tests for each major feature. After significant changes, re-run every suite:
  - smoke, replay and journey
  - UI and gallery
  - perf probe
  - EditMode
  - offline C#
  - Python (Tools, bridges, WBS, AI)
- **Keep the project runnable throughout.** Document new dependencies, setup, configuration, known limits and test results.
- **Research:** use official documentation and credible sources, give links, and separate verified facts from assumptions.
- **Process:**
  - Keep the requirements checklist statuses current.
  - Add a protected-file review link per stage.
  - Start commit subjects with existing WBS IDs.
  - Stop for review after every stage.
- **Final acceptance:** the existing game still works, each requirement is verified against explicit criteria, all blockers are documented, and the final report separates tested functionality from remaining work.

---

## Stage 8: Hand–object interaction and contact (WBS 3.4.1, 3.3)

**Reuse and extend** (in `Assets/Rehab/3.4.1-force-object-interaction/Apartment/`):
- `FingerContactRig` / `FingerContactProbe` (per-phalanx probes)
- `GraspCollisionShield`
- `GraspMotionPolicy` (pickup orientation, damped release, speed caps)
- `RehabVGController`
- `HandRigRules`
- `ApartmentExperience.TryGrab / CanReach / BeginGrab`

### Task 8.1: Contact audit (C-01)
- [x] Reproduce and measure every current defect per object and angle:
  - **S4-03:** the fist closes beside the ball and the dent reads 0 mm
  - hand passing through objects
  - an object penetrating the palm
  - a floating hold gap
  - sinking
  - tunnelling at speed
  - jitter
  - sudden snapping
- [x] Record the numbers as the baseline.

### Task 8.2: Contact solver (C-02 to C-05)
- [x] Closest-surface contact points and normals per probe.
- [x] Finger curl stops at first surface contact, so there is no hand-through-object and no object-in-palm.
- [x] Palm-anchored hold keeps the measured contact offset, so there is no floating gap and no sinking.

### Task 8.3: Stability (C-06 to C-10)
- [x] Grip-shape presets per object: power, pinch, cylindrical (superseded by Task 8.5).
- [x] Stable grasp across sizes, shapes and orientations.
- [ ] Continuous collision for the held object, with speed-limited follow: no tunnelling, jitter or snapping during fast hand motion or large deformation. *Partly:* holds and ~1 m/s reach are clean; a ~2.7 m/s shake still shows transient overlap (C-10).
- [x] Only probes that are in contact can drive deformation pressure, consistent with the material.

### Task 8.4: Preserve inputs and test (C-11, C-12)
- [x] Mouse, keyboard and hand pointer still work.
- [x] Offline contact-maths tests: penetration ≤ tolerance, gap ≤ tolerance, no tunnelling at a stated hand speed.
- [x] An apartment acceptance case per object and approach angle.

### Task 8.5: Grip chart (added 2026-10-26 at your request, before Stage 9)
Your chart of 18 grips ("Hand Grips & Object Examples": power, precision, three-finger, lateral pinch, hook, claw,
palm, spherical, finger tip, cupped, two-finger, lateral/thumb, precision 3-finger, fist, side, interlocking, folded,
dynamic). Interactions and the game should look like it.
- [x] `GripCatalog`: the 18 grips as data (hand shape per joint, spread, touching segments, anchor, viewing angle; interlocking uses both hands).
- [x] `GripLab`: one code-built prop per grip (D7), including ball, cube, pen, card, kettlebell, basketball, bottle, coin, cup, USB drive, phone, screw, grip tool, book, large ball, cloth and hammer.
- [x] **Grip Lab** station (third station button; `[` / `]` or the options buttons step through the chart).
- [x] Every object, the six station objects included, is held in its chart grip: hand shaped, object placed at the grip anchor, contact solver closes the touching fingers, hand turned and brought into view like the photograph (keyboard/demo; a tracked camera hand keeps its own pose).
- [x] Grip Lab probe (`run_contact_probe.py --grip-lab`): penetration, pad gap, touching segments, palm overlap, jitter and dent per grip, plus a screenshot per grip.

**Gate 8:** a penetration / gap / jitter table per object and angle, before and after, plus screenshots. Stop.

---

## Stage 9: Object deformation system (WBS 3.3)

**Reuse:**
- `ContactDeformation` (bounded local dents, rotation-following, recovery, plasticity)
- `SqueezableObject`
- `PhysicalMaterialProfile` and its presets
- `FracturePartitioner` / `ObjectFractureVisuals`
- `SqueezeLiquid`
- the offline deformation and fracture tests (61)

### Task 9.1: Audit (D-01)
- [x] Audit the existing deformer and decide part by part what to keep or rebuild. Record the reasoning.

### Task 9.2: Physical model (D-02, D-05, D-06)
- [x] A per-vertex spring–damper driven continuously by contact force, not by animations or scaling.
- [x] Neighbour (Laplacian) smoothing for surface continuity.
- [x] An approximate volume-preservation bulge.
- [x] Collision stays stable while deformed.

### Task 9.3: Material behaviours and presets (D-03, D-04, D-09)
- [x] Elastic: deforms and recovers.
- [x] Plastic: keeps some or all of the dent.
- [x] Rigid: resists normal forces.
- [x] Breakable: cracks, then fractures and splits past thresholds.
- [x] Configurable stiffness, elasticity, plasticity, damping, fracture threshold and recovery speed.
- [x] Reusable components and presets, so new objects need no rebuild.

### Task 9.4: Integration and performance (D-07, D-08)
- [x] Driven by hand tracking, grip and force sensing through Stage 8 contacts.
- [x] Job/Burst-friendly with no per-frame GC; the perf probe stays within the Stage 7 budget.

### Task 9.5: Your deformation chart (added 2026-10-26)
Your chart "Types and Forms of Deformation" sets the target:
- [x] **Material classes** with presets from the chart: rigid (steel, glass, concrete), semi-rigid (wood, ABS plastic, dense foam), flexible (rubber, silicone, fabric) and soft / very soft (gel, sponge, clay). Bone is in the chart's summary; it is not added as a game object.
- [x] **Forms of deformation** driven by grip force: compression, tension, bending, shear, torsion and buckling. Volume change in the chart is thermal (heating/cooling); grip force cannot cause it, so it is documented as out of scope, while the squeeze-volume change of soft materials is measured.
- [x] **Results**: elastic (returns), plastic (stays deformed) and fracture (crack, then break).
- [x] A **Material Lab** with code-built examples (D7), playable with mouse, keyboard and hand pointer.

### Task 9.6: Tests (D-10)
- [x] Squeeze, release, recovery, repeated squeeze and fracture across ball, cylinder and box shapes for each material.

**Gate 9:** a material matrix of measured dent, recovery time, fracture threshold and frame time. No "AAA" claim without these numbers. Stop.

---

## Stage 10: Six multi-step environmental activities (WBS 3.1, 3.4.1)

**Shared framework:**
- An `ActivityDefinition`: steps, target layout, tolerances, scoring, and difficulty on `ExerciseTuning`.
- Snap targets that report position and orientation error.
- Outcomes go into `SessionRecord.outcomes`, so they appear in the Stage 6 dashboards automatically.

**Props are built in code (decision D7):** a `PropFactory` makes books of several heights, shelf slots, cups, plates, a tray, a storage box, a knife, a cutting board and vegetables.

### Task 10.1: Living room (A-01 to A-03)
- [x] **Bookshelf:** pick up, rotate and place books in order by height or category; wrong placements are detected and correct organisation is rewarded.
- [x] **Coffee table:** arrange cups, plates, a tray and other items to a target layout, scored on position, orientation and spacing.
- [x] **Storage box:** choose items, grip them suitably, and fit them inside the volume without excessive overlap or instability.

### Task 10.2: Kitchen (A-04 to A-06)
- [x] **Simple meal:** ingredients to the preparation area, utensils, and a sequence of safe, simulated cooking steps.
- [x] **Cutting:** a knife stroke cuts compatible vegetables into pieces. It uses a plane cut through the existing partitioner, a technically feasible approximation that is labelled as such.
- [x] **Prepare and serve:** collect ingredients and utensils, prepare in several steps, plate the food and serve it at the right place.

### Task 10.3: Requirements for every activity (A-07 to A-13)
- [x] Every missing prop and environment element is added.
- [x] Instructions, objectives, a progress indicator, success conditions and completion feedback.
- [x] Realistic manipulation and placement constraints.
- [x] Difficulty levels and optional precision requirements.
- [x] Scoring on accuracy, completion, handling and task-specific measures.
- [x] Integrated into the existing environment and UI without breaking current features.
- [ ] Replayable, and completable with every supported input method. *(Replay and keyboard/mouse done; camera hand not tested end-to-end: A-13 In Progress.)*

### Task 10.4: Tests
- [x] ~~An EditMode completion test per activity using synthetic input.~~ Replaced by a player completion probe (`Tools/run_activity_probe.py`: all six activities at levels 2 and 8 through the real grab/aim/release path, SYNTHETIC input), because the activities need the rendered apartment, physics and UI; the pure rules are in the offline activity checks.
- [ ] A by-eye playtest. *(Claude checked the probe screenshots; your hands-on playtest is pending.)*
- [x] Results visible on the patient and doctor dashboards.

**Gate 10:** all six activities playable start to finish, with outcomes saved. Stop.

**Result (2026-10-27):** 12 of 12 probe runs complete (levels 2 and 8), scores 88–100; three repeated runs all pass. Details in `Docs/AUDIT_LEDGER.md` §10c.

---

## Stage 11: UI modernization and design system (WBS 3.4, 5.1, 5.2)

**Extend:**
- `GripUiTheme` (Dark / Light / HighContrast)
- `GripUiThemeController`
- `GripUiAccessibility`
- `ResearchPortalView`

### Task 11.1: Research (U-01 to U-04)
- [x] Audit the current UI.
- [x] Research 2024–2026 AAA and accessibility-focused interfaces (clean hierarchies, restrained effects, readable type, consistent spacing, clear focus, subtle motion, strong feedback). Cite sources.
- [x] Use the findings as inspiration only; no proprietary assets or exact interfaces are copied.

### Task 11.2: Design system (U-05 to U-07, U-11)
- [x] Tokens for colour, type scale, spacing, panels, icons and transitions.
- [x] Distinct hover, focus, pressed, disabled, selected and error states.
- [x] Better readability, contrast, text scaling and hierarchy.
- [x] No clutter, heavy gradients or distracting animation.
- [x] Reduce Motion respected.

### Task 11.3: Screens and indicators (U-08 to U-10)
- [x] A modern main menu, pause menu, settings, activity selection, progress display and onboarding. *(Your by-eye review pending.)*
- [ ] Every screen works with mouse, keyboard, hand pointer, gaze and voice. *(Mouse, keyboard and hand pointer done; gaze and voice arrive in Stages 13–14.)*
- [ ] Indicators for the active input mode, gaze focus, voice listening and assistant status. *(Strip built; input and pointer live; gaze, voice and assistant read OFF until Stages 13–15 report.)*

### Task 11.4: Tests (U-12)
- [x] UI and gallery suites extended to the new screens at 1024×768, 1280×720, 1280×800 and 1600×900.
- [x] Contrast checks.

**Gate 11:** before/after gallery. Stop.

**Result (2026-10-27):** before/after gallery in `Docs/evidence/stage11-2026-10-27/`; UI checks pass at 4 resolutions × 3 themes; full regression passes. Details in `Docs/AUDIT_LEDGER.md` §10d.

---

## Stage 12: Audio and sound design (WBS 3.1, 3.4)

**Start from:**
- `RehabAudio` (synthesised effects, creak and room loops, mute, effects and ambience volumes)
- `GripUiAccessibility.PlayCue`

### Task 12.1: Audit (S-01)
- [x] List missing, low-quality, repetitive, badly timed or wrongly triggered sounds.

### Task 12.2: Sound families and events (S-02 to S-05)
- [x] Ambience, UI, grab, release, collision, deformation, fracture and activity-completion sounds.
- [x] Material families: wood, metal, glass, plastic, fabric and soft.
- [x] Variant pools and pitch variation.
- [x] Triggered by real contact force, deformation rate and fracture events from Stages 8–9.

### Task 12.3: Space and mix (S-06 to S-09)
- [x] 3D spatial sources with distance attenuation.
- [x] An AudioMixer asset with Master, SFX, Ambience and Voice groups, replacing the code-only volumes from Stage 4.
- [x] Balanced menu-to-gameplay transitions.
- [x] Separate settings for each group.
- [x] Nothing loud, distracting or misleading. *(Measured: peaks ≤ -1 dBFS, rate-driven strain, no sound at rest. Your listening check pending.)*

### Task 12.4: Licensing (S-10)
- [x] Original, synthesised audio or documented-licence assets only, recorded in the credits.

**Tests:** EditMode audio checks (levels, no clipping, no NaN, seamless loops). Your own listening session is recorded as human-verified only when you do it.

**Gate 12:** event-to-sound map and loudness table. Stop.

**Result (2026-10-27):** event-to-sound map and loudness table in `Docs/AUDIO_DESIGN.md`; evidence in `Docs/evidence/stage12-2026-10-27/`; full regression in `Docs/AUDIT_LEDGER.md` §10e.

---

## Stage 13: Voice commands and speech-to-text (WBS 3.4)

**Engine (decision D9):** Windows built-in, offline. Unity's Windows keyword and dictation recognizers, plus Windows text-to-speech for feedback.

### Task 13.1: Investigate (V-01)
- [x] Check speech-service availability, the Windows privacy setting and microphone permission on this machine.
- [x] If speech is unavailable, the stage is marked **Blocked** with the reason; it is never faked.

### Task 13.2: Commands (V-02 to V-04, V-08, V-09)
- [x] A `VoiceCommandRouter` (keyword grammar) mapping commands to existing UI and game actions:
  - **UI:** navigate, select, open settings, back, confirm, cancel, launch activity
  - **In-game:** move forward, back, left and right; stop; look at, select and release the object; open inventory
  - **Session:** restart, pause/resume, instructions, main menu
- [x] State-aware: commands that are invalid in the current state are rejected.
- [x] Recognized, rejected and unavailable feedback, visual and audio.
- [x] Ambiguity and background-noise handling.

### Task 13.3: Speech-to-text (V-05 to V-07)
- [ ] Dictation into text fields (names, profile). *(Blocked: Windows dictation is online; you chose offline spelling, which fills names and profile text.)*
- [x] **Letter-by-letter spelling** becomes editable text.
- [x] The recognized text is shown for review and correction before submit.
- [x] **Password fields are excluded.**

### Task 13.4: Control (V-10, V-11)
- [x] Activation toggle and push-to-talk.
- [x] Microphone-permission handling.
- [x] All existing inputs unchanged.

**Tests:**
- Router unit tests from transcripts (no microphone needed).
- A microphone run with you, recorded as human-verified.

**Gate 13:** command coverage table. Stop.

**Result (2026-10-27):** coverage table in `Docs/VOICE_COMMANDS.md`; evidence in `Docs/evidence/stage13-2026-10-27/`; full regression in `Docs/AUDIT_LEDGER.md` §10f. Microphone session with you pending.

---

## Stage 14: Optional eye tracking (WBS 3.2)

**Approach (decision D8):** a webcam gaze estimate. No eye-tracking hardware is present, and a standard webcam is **not** hardware-level eye tracking.

### Task 14.1: Investigate (E-01)
- [ ] Confirm the engine, camera and API options.
- [ ] Measure what MediaPipe face/iris landmarks from the existing vision worker can achieve.

### Task 14.2: Gaze pipeline (E-02, E-08, E-09, E-10)
- [ ] The vision worker sends gaze packets with confidence on a new loopback port.
- [ ] A `GazeReceiver` with 5-point calibration and recalibration.
- [ ] Sensitivity, dead zones, smoothing and activation thresholds, to suppress involuntary movement and jitter.
- [ ] Fall back to other inputs when confidence is low.

### Task 14.3: UI gaze (E-03 to E-05)
- [ ] Look at menus and buttons to move focus.
- [ ] Select by dwell (reusing `RehabDwellGate` / `RehabPointerPolicy`) or a supported gesture.
- [ ] Clear focus feedback.
- [ ] Accessible confirm and cancel to avoid accidental selections.

### Task 14.4: In-game gaze (E-06, E-07)
- [ ] Optional camera turn left/right and up/down, with a dead zone and rate limit.
- [ ] Look at an object to highlight, inspect or select it.

### Task 14.5: Optional (E-11)
- [ ] A settings toggle, off by default.
- [ ] Measured accuracy (degrees and screen regions) reported plainly.

**Tests:**
- Synthetic gaze-packet replay.
- A camera session only with your consent, recorded separately.

**Gate 14:** accuracy and stability table. Stop.

---

## Stage 15: Optional in-game voice assistant (WBS 3.4)

### Task 15.1: Engine (G-01, G-11, G-12)
- [ ] Offline by default (decision D9): an intent layer over the Stage 13 recognition, with Windows text-to-speech for output.
- [ ] Compare any online option for latency, cost and privacy, and only adopt one if you approve it.
- [ ] Handle microphone, network, service and assistant errors cleanly.

### Task 15.2: Guidance (G-02 to G-05)
- [ ] Onboarding for first-time users.
- [ ] Explains buttons, menus, settings, objectives and how to complete tasks.
- [ ] Contextual help from the live state (page, activity step, Module 1 rounds, exercise suite, safety pause).
- [ ] Answers "What should I do next?", "How do I grab this object?", "Repeat the instructions" and "How do I complete this task?".

### Task 15.3: Behaviour (G-06 to G-10, G-13)
- [ ] Step-by-step help without taking control.
- [ ] Navigation only after confirmation.
- [ ] Clear voice with volume and speed settings.
- [ ] No needless interruptions: repeat, pause, dismiss and disable are available.
- [ ] State-aware: never claims an action succeeded unless the game state confirms it.
- [ ] The game stays fully playable with the assistant off.

**Tests:** intent tests from transcripts against fixture game states.

**Gate 15:** answer table by game state. Stop.

---

## Stage 15A: Environment upgrade, kitchen and living room (WBS 3.1, added 2026-10-26)

You asked for this "in next stages later". Details are fixed when you authorize it; the starting scope:
- [ ] Audit both rooms (lighting, materials and textures, props, clutter around the stations, scale, readability from the seated and standing views) and record findings with screenshots.
- [ ] Improve lighting and materials (URP), keep stations clear for the grip and material labs, and dress both rooms with code-built or licence-documented props.
- [ ] Keep station positions, colliders, teleports and every input method working; re-run the apartment acceptance and contact probes.
- [ ] Stay inside the Stage 7 budget (≥ 60 FPS at p95, < 1 KB/frame GC) on the reference laptop.

**Gate 15A:** before/after screenshots of both rooms and the perf numbers. Stop.

---

## Stage 16: Pipeline, block diagram and Gantt audit (WBS 1.1, 6.3)

### Task 16.1: Plan versus code (P-01 to P-04)
- [ ] Review the pipeline, architecture, block diagram, Gantt, requirements, status and source.
- [ ] Find missing components, incomplete features, inconsistencies and integration gaps.
- [ ] Verify end-to-end data flow: hand tracking → input → grasp → force → physics and deformation → collision → activity logic → feedback → logging.
- [ ] Check that the architecture supports the new eye, voice, assistant, audio, UI and activity systems.

### Task 16.2: Updated documents (P-05 to P-08)
- [ ] An as-built block diagram.
- [ ] An updated pipeline: implementation, integration, testing, optimisation and validation stages.
- [ ] An updated Gantt: remaining tasks, dependencies, milestones, priorities, realistic durations and critical-path risks.
- [ ] Requirements and acceptance-criteria review.

### Task 16.3: Whole-product audit (P-09 to P-11)
- [ ] Stability, error handling, configuration, assets, performance and hardware compatibility.
- [ ] A full run from start-up and onboarding through activity selection, play, completion and settings to shutdown.
- [ ] A regression check of all existing features.

### Task 16.4: Report (P-12, P-13)
- [ ] A prioritised gap analysis: critical blockers, important improvements, optional features and future ideas.
- [ ] Architecture documentation, revised block diagram, revised Gantt, test checklist and a final completion report.
- [ ] Never "100 % complete" on code alone; every requirement is verified by test against acceptance criteria.

**Gate 16:** you review the audit and the re-planned Gantt. Stop.

---

## Stage 17: Next-stage research proposal (WBS 1.1). Recommendations only.

### Task 17.1: Research (R-01 to R-08)
- [ ] After the Stage 16 audit, brainstorm and research improvements across:
  - gameplay, physical realism and UI/UX
  - object models, computer vision and AI
  - rehabilitation assessment and data processing
  - accessibility, performance and maintainability
- [ ] Digital twin and real-time synchronisation, simulation fidelity, material modelling and sensor-to-virtual mapping.
- [ ] AI-assisted adaptation and personalised difficulty, only where scientifically justified.
- [ ] Hand tracking, grasp classification, force estimation, deformation, collision robustness and latency.
- [ ] Rehabilitation metrics: completion time, grip force, force steadiness, ROM where measurable, smoothness and repeatability.
- [ ] Datasets, models, sensors and libraries with measurable benefit.
- [ ] Recent papers, official engine documentation and credible demonstrations, with links that separate facts from assumptions.

### Task 17.2: Ranked proposal (R-09 to R-13)
- [ ] Each idea scored on impact, complexity, cost, hardware, risk, scientific validity and compatibility.
- [ ] Essential fixes, optional enhancements and ambitious research kept separate.
- [ ] A ranked list with description, justification, benefit, effort, dependencies and order.
- [ ] Ideas that are possible now marked apart from those that need hardware, data, funding or architecture changes.
- [ ] **Nothing is implemented automatically.** The proposal is presented for your review and prioritisation.

**Gate 17:** you prioritise the next development stage. Stop.

---

## Stage 18: Hardware readiness (formerly Stage 8) (WBS 2.3, 2.4, 6.1)

### Task 18.1: Calibration wizard in-game
- [ ] Tare → known-weight steps → fit → save calibration metadata (`calibration_id`) → verify-load step. Works against synthetic replay now. It is marked as physically unverified until real hardware is used.

### Task 18.2: Device connection UX
- [ ] Device status panel: BLE bridge state, packet rate, loss %, battery, last sample age. Reconnect handling.

### Task 18.3: Physical test protocol (executed only when hardware exists)
- [ ] `Docs/PHYSICAL_TEST_PROTOCOL.md`: known loads for each grip, BLE rate/latency, closed loop. Results go to a **separate** `Docs/evidence/physical/` record.

**Gate 18:** software readiness reviewed. Physical items stay OPEN until real tests are done.

---

## Stage 19: Release and graduation package (formerly Stage 9) (WBS 6.3)

### Task 19.1: Installer and first run
- [ ] IL2CPP Release build (if Sentis/VG are compatible, otherwise Mono). Inno Setup installer bundling the Python workers' venv or an embedded runtime. A first-run check covers camera, ports and model files.

### Task 19.2: Clean-machine verification
- [ ] Install on a clean Windows user account and run the full Stage 1 suite plus a playthrough from the installed build.

### Task 19.3: Graduation evidence
- [ ] Final WBS evidence matrix (software / native / physical / human columns), model and data cards, limitations, credits/licences, a 3-minute demo video, and presentation slides.

**Gate 19:** final review. GP1 Done (W22, 2027-02-06).

---

## Suggested calendar (respects Gantt blocked weeks)

> **2026-10-25 update:** Stages 8–17 add many weeks of work before hardware (18) and release (19). The table below is the original 23 Oct estimate. Stage 16 re-plans the Gantt with realistic durations, dependencies and the critical path.

| Weeks | Dates | Stages |
|---|---|---|
| W6–W7 | 23 Oct – 30 Oct | Stage 0, Stage 1 |
| W8 | 31 Oct – 6 Nov | Stage 2 (and Paper Prep marker) |
| W9–W10 | 7 – 20 Nov | **Midterm break, no planned work** |
| W11–W12 | 21 Nov – 4 Dec | Stage 3 |
| W13–W14 | 5 – 18 Dec | Spare buffer. Use only if you approve (Stage 4) |
| W15–W20 | 19 Dec – 29 Jan | **Final exams, paused** |
| W21 | 30 Jan – 5 Feb | Stages 5–9 compressed. **This is unrealistic as one week.** Recommend you approve pulling Stage 4–6 into W13–W14 |

## Review Focus (failure modes no single task's tests cover)

1. **Camera lost mid-grip:** the held object releases safely, the session pauses and doesn't count as a fatigue sign. Test owner: Task 5.3.
2. **Ports 27817–27819 already in use** (a second player instance): a clear error and no silent hang. Test owner: Task 7.2.
3. **Switching grip shape or calibration mid-session:** the baseline is invalidated and a re-baseline is prompted, not a silent mix. Test owner: Task 5.1.
4. **Left-handed or both-hands-visible users:** hand selection is stable and dwell still works. Test owner: Task 1.3 playthrough plus the existing pointer tests.
5. **Corrupt or old profile store after an upgrade:** migration or recovery without data loss. Test owner: Task 7.2.

## What this plan cannot deliver (stated up front)

- A real physical Nano/HX711 test, a live supervised human trial and clinical/fatigue validation. These need hardware, participants and ethics approval (B02, B03, B05). The plan gets the software ready and records them as OPEN.
- "AAA" here means presentation-grade polish within a capstone budget: no new licensed art packs, Obi or YOLO models unless you supply or buy them.

## Tooling to use during execution

- **Computer use** (offered by you) for Unity Editor and built-player playthroughs (Tasks 1.3, 4.x, 6.x gates).
- Skills: `unity:urp-postprocessing`, `unity:audio-setup-mixers`, `unity:optimize-audio`, `unity:optimize-text-mesh-pro`, `unity:ui-ugui`, `unity:physics-3d-collision`, `unity:unity-cli`, `superpowers:systematic-debugging`, `superpowers:test-driven-development`, `code-review`.
- Optional plugins: **Figma** (sync with the existing HUD board) and **unity-perf** (Stage 7).
