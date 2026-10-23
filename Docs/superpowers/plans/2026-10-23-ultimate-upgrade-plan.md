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

## Stage 8: Hardware readiness (WBS 2.3, 2.4, 6.1)

### Task 8.1: Calibration wizard in-game
- [ ] Tare → known-weight steps → fit → save calibration metadata (`calibration_id`) → verify-load step. Works against synthetic replay now. It is marked as physically unverified until real hardware is used.

### Task 8.2: Device connection UX
- [ ] Device status panel: BLE bridge state, packet rate, loss %, battery, last sample age. Reconnect handling.

### Task 8.3: Physical test protocol (executed only when hardware exists)
- [ ] `Docs/PHYSICAL_TEST_PROTOCOL.md`: known loads for each grip, BLE rate/latency, closed loop. Results go to a **separate** `Docs/evidence/physical/` record.

**Gate 8:** software readiness reviewed. Physical items stay OPEN until real tests are done.

---

## Stage 9: Release and graduation package (WBS 6.3)

### Task 9.1: Installer and first run
- [ ] IL2CPP Release build (if Sentis/VG are compatible, otherwise Mono). Inno Setup installer bundling the Python workers' venv or an embedded runtime. A first-run check covers camera, ports and model files.

### Task 9.2: Clean-machine verification
- [ ] Install on a clean Windows user account and run the full Stage 1 suite plus a playthrough from the installed build.

### Task 9.3: Graduation evidence
- [ ] Final WBS evidence matrix (software / native / physical / human columns), model and data cards, limitations, credits/licences, a 3-minute demo video, and presentation slides.

**Gate 9:** final review. GP1 Done (W22, 2027-02-06).

---

## Suggested calendar (respects Gantt blocked weeks)

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
