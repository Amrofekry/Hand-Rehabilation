# PROJECT_PLAN

**Project:** An Intelligent Adaptive Semi-Immersive VR Rehabilitation System
**Planning date:** 2026-09-12
**Status:** W1/W2 execution authorized by the user on 2026-09-12. See `docs/W1_W2_DELIVERY.md` for actual completion evidence and remaining physical checks. Original source calendar and acceptance criteria below are preserved.

## 1. Authority, evidence, and current workspace

This plan maps all **24 WBS rows across six phases**, including parent task 3.4 and the two GP2 tasks. Task identities, source status, and calendar bars come from the Gantt HTML. Architecture requirements come from the single-page PDF, inspected through both text extraction and a rendered page. Acceptance criteria, folder mappings, dependencies, and proposed gap ownership below are planning proposals, not claims made by the source documents or evidence of completed work.

The user's request governs execution: produce this plan now, stop before scaffolding, then work one WBS task at a time after approval and stop for review after each phase. Document annotations such as "Email to Library Developers," "Google Form/Sheet & Ethical Approval," and "Tax Invoice Required for All Purchases" are recorded as project requirements; they do not authorize sending messages, creating external forms, purchasing equipment, or submitting applications.

### Sources actually read

The supplied files are outside the repository. No `docs/` directory existed at inspection. These originals were read without modification; copying them into the planned `docs/` directory belongs to the approved scaffold step.

| Reference | Actual source | Intended repository location |
| --- | --- | --- |
| Architecture, page 1 | [Hand_Rehab_Pipeline.pdf](<E:/Downloads/Hand_Rehab_Pipeline.pdf>) | `docs/Hand_Rehab_Pipeline.pdf` |
| Authoritative WBS and calendar | [ganttchart.html](<E:/Downloads/ganttchart.html>) | `docs/ganttchart.html` |

SHA-256 fingerprints for the reviewed versions:

```text
Hand_Rehab_Pipeline.pdf  D23B3B8ED6FA476ACDB9DA0F0308589037AE82F3460566A2ADE883D45408199E
ganttchart.html          53456905BAA976E8D3C3CC54B4710073BDDCA71061B18E43EB8A9573CA90CC1C
```

At initial inspection the root was `C:/Users/lenovo/OneDrive/Documents/New project`, with an uncommitted `master` repository and unrelated projects. The current canonical repository is `E:/GP/codex`; its implementation and WBS commits are recorded in the delivery report. There is an existing `tests/` directory and a `vr_hand_rehab/` prototype. Its README describes a browser-based webcam prototype with estimated relative grip effort, rather than measured load-cell force. It is not evidence that the Arduino/Unity/temporal-model tasks are complete. No applicable `AGENTS.md` was found during inspection. Preserve existing files and avoid repository-wide staging; decide reuse or migration explicitly under issue G01.

## 2. Architecture to preserve

- **Physical device:** interchangeable Sphere, Cylinder, and Pencil shapes share a universal core and protective enclosure, Arduino Nano 33 IoT, HX711 ADC, 100 kg load cell, springs, step-up module, and battery. The PDF's illustrated component labels specify a 3.7 V Li-ion battery and a step-up converter with charger/power-bank module; these illustrations are not a wiring schematic or verified bill of materials.
- **Virtual world:** desktop semi-immersive Unity game with Living Room / Kitchen environment, virtual-hand and avatar controllers, and Doctor and Patient Dashboards. The source does not require a headset build.
- **Force-Object Interaction:** hardware force drives Sphere/Cylinder/Pencil deformation using Obi Softbody and Obi Fluid; MediaPipe supplies hand tracking. Metrics: force production, grip endurance, gesture stability.
- **Cognitive Performance, deferred to GP2 by the chart:** Gesture Sequence Memory (avatar) and Spatial Assembly (virtual hand); MediaPipe and YOLO; reaction time, trajectory, accuracy, velocity.
- **Visuomotor Coordination, deferred to GP2 by the chart:** Maze Navigation (avatar, collision score penalty) and Visual Tracing (virtual hand, green/red feedback); MediaPipe and YOLO; reaction time, trajectory, gesture stability, motion accuracy.
- **AI pipeline:** baseline maximum grip force, endurance, gesture stability, and range of motion (ROM); synchronized biomechanical force, kinematic ROM/tremor/speed/trajectory, facial seven-emotion features (SSD MobileNet / ExpressionNet), and physiological HR/HRV from rPPG; temporal LSTM/GRU fusion; adaptation or fatigue intervention.
- **PDF decision branches:** stable potential permits level increase/decrease; a single fatigue sign displays a warning with Resume/Quit; more than one fatigue sign forces a safety stop. Definitions of signs, timing, thresholds, and resumption conditions are unresolved, not inferred clinical rules.
- **Data flow to specify:** calibrated force over Bluetooth -> Unity force mapping and deformation; synchronized session features plus baseline -> temporal inference -> decision policy -> game difficulty/warning/stop; metrics and decision events -> patient results and doctor history/statistics/recommendations. ONNX export and Unity Sentis are explicit Gantt requirements.

## 3. Calendar and execution rules

Dates use **day/month/year**, confirmed by the sequence of Gantt headers. Week 1 begins **2026-09-12**. The chart labels Week 22 **2027-02-06**, which is that week's start, not an explicitly stated end-of-week deadline. Inclusive week intervals below are derived as seven-day periods. Task windows are decoded from the HTML's CSS grid: column 2 is W1; a bar `a / b` covers W(a-1) through W(b-2), because the ending grid line is exclusive.

| Week | Inclusive dates | Source availability / milestone |
| --- | --- | --- |
| W1 | 2026-09-12 - 2026-09-18 | Active |
| W2 | 2026-09-19 - 2026-09-25 | Active |
| W3 | 2026-09-26 - 2026-10-02 | Active; Google Form marker |
| W4 | 2026-10-03 - 2026-10-09 | Active |
| W5 | 2026-10-10 - 2026-10-16 | Active |
| W6 | 2026-10-17 - 2026-10-23 | Active |
| W7 | 2026-10-24 - 2026-10-30 | Active |
| W8 | 2026-10-31 - 2026-11-06 | Active; Paper Prep marker |
| W9 | 2026-11-07 - 2026-11-13 | Blocked: midterm break |
| W10 | 2026-11-14 - 2026-11-20 | Blocked: midterm break |
| W11 | 2026-11-21 - 2026-11-27 | Active |
| W12 | 2026-11-28 - 2026-12-04 | Active |
| W13 | 2026-12-05 - 2026-12-11 | Spare buffer |
| W14 | 2026-12-12 - 2026-12-18 | Spare buffer |
| W15 | 2026-12-19 - 2026-12-25 | Blocked: final exams |
| W16 | 2026-12-26 - 2027-01-01 | Blocked: final exams |
| W17 | 2027-01-02 - 2027-01-08 | Blocked: final exams |
| W18 | 2027-01-09 - 2027-01-15 | Blocked: final exams |
| W19 | 2027-01-16 - 2027-01-22 | Blocked: final exams |
| W20 | 2027-01-23 - 2027-01-29 | Blocked: final exams |
| W21 | 2027-01-30 - 2027-02-05 | Active; all Phase 5 and Phase 6 bars |
| W22 | 2027-02-06 - 2027-02-12 | GP1 Done marker; no task bars |

This is 22 calendar weeks, including eight blocked weeks and two spare weeks, leaving 12 ordinary available weeks; W22 has only a milestone. Do not silently allocate blocked time, consume spare weeks, or revise the source bars.

After plan approval, execution follows WBS row order within phases: `1.1 -> 1.2 -> 1.3`, then Phase 1 review; `2.1 -> 2.2 -> 2.3 -> 2.4`, then Phase 2 review; and so on. Parent 3.4 establishes the module framework before 3.4.1; GP2 children remain deferred. This sequential agent workflow is distinct from the overlapping calendar bars. Issue G03 records the conflict so no schedule is silently rewritten to make it look sequential.

**Git convention after approval:** phase branches `codex/1-planning`, `codex/2-hardware`, `codex/3-unity`, `codex/4-ai`, `codex/5-dashboards`, `codex/6-integration`; task branches, if needed, `codex/2.3-calibration`. The numeric portion is the canonical phase/task WBS ID. Task commit subjects begin with the exact task ID, e.g. `2.3 Calibrate load-cell output in Newtons` or `6.2 Verify the closed-loop rehabilitation pipeline`. Do not invent a `0.x` task for this plan. Record the approved plan and initial scaffold with 1.1 work. Stage only the named project files. No branches or commits are created in this planning step.

At every phase checkpoint, summarize task outcomes, changed modules, relevant test results, physical/manual evidence, unresolved issues, and the next phase's prerequisites. Wait for user approval before the next phase. A mock-only result or unavailable hardware is reported as partial/blocked verification, never as a completed physical acceptance test.

## 4. Proposed repository layout

All paths below are relative to the current repository root and are **planned, not created**. The six phases map to `docs/1.*`, hardware plus firmware `2.*`, Unity `3.*`, AI `4.*`, dashboards `5.*`, and integration/tests/documentation `6.*`. An additional `hardware/` root gives CAD and wiring a concrete home outside firmware source.

```text
PROJECT_PLAN.md
docs/
  Hand_Rehab_Pipeline.pdf
  ganttchart.html
  1.1-system-architecture/
  1.2-literature-components/
  1.3-events-calendar/
  3.3-library-correspondence/
  6.3-final-documentation/
hardware/                       # 2.1 CAD/printing and 2.2 electronics
firmware/                       # 2.3 acquisition/calibration and 2.4 wireless
unity-project/
  Assets/Rehab/                 # WBS-prefixed Unity modules, listed below
  Packages/                    # Unity-required conventional directory
  ProjectSettings/             # Unity-required conventional directory
ai-engine/                      # 4.1-4.5 protocol, data, training, export, policy
dashboards/                     # 5.1/5.2 specifications, contracts, fixtures
tests/                          # WBS-specific suites alongside existing tests
```

Use exact dotted WBS IDs in folder names, e.g. `3.4.1-force-object-interaction`. Language namespaces/package identifiers may use valid descriptive names, with the WBS recorded in their README; Unity-required folder names stay conventional. Dashboard runtime scenes/scripts are proposed inside Unity because the PDF places them within the Unity virtual world; `dashboards/` holds their contracts and fixtures, avoiding an implicit second web application. Confirm this under G10 before implementation.

## 5. Complete WBS mapping and acceptance criteria

**Legend:** source status is preserved separately from repository acceptance. The chart marks 1.1 and 1.3 in progress, 1.2 completed before W1, and 3.4.2/3.4.3 GP2; all other rows are not started. Every acceptance criterion below is proposed. Numerical tolerances, clinical protocol values, model metrics, and latency budgets must be specified and reviewed in the owning task before its verification; none is fabricated here.

### Phase 1 - Planning & System Architecture

| WBS / exact task title | Source window / status | Concrete folder or module | Acceptance criteria and evidence |
| --- | --- | --- | --- |
| 1.1 Finalize System Pipeline & Block Diagrams | W1; in progress | `docs/1.1-system-architecture/` | Versioned system diagram and interface specification trace physical core, Bluetooth, Unity, baseline, sensing, inference, policy, and dashboards. Define force units, timestamps, session/shape IDs, validity flags, and failure states. Distinguish GP1 from GP2 and assign every gap an owner/disposition. User reviews the architecture and this plan; this planning file alone does not complete 1.1. |
| 1.2 Literature Review & Component Selection (HW & AI) | Completed 2026-07-04 - 2026-08-30; no in-chart bar | `docs/1.2-literature-components/` | Import/index existing review evidence, bibliography, component/model selection rationale, BOM, and software/license decisions. Trace Arduino, HX711, load cell, power components, vision, physics, and temporal-model choices to available evidence. Record unavailable historical material without changing the chart's reported completion or pretending repo evidence exists. |
| 1.3 Events Calendar (Competitions, Conferences & Deadlines) | W1; in progress | `docs/1.3-events-calendar/` | Calendar records event, source, deadline, timezone, owner, and status; records the W3 Google Form, W8 Paper Prep, and W22 GP1 markers plus blocked/spare weeks. Unverified events/deadlines are explicitly pending. Calendar agrees with all 22 source headers. |

**Phase 1 checkpoint:** approved architecture/interfaces, evidence inventory, calendar, and gap decisions sufficient to start hardware work. Stop for review.

### Phase 2 - Real-World Hardware Setup

| WBS / exact task title | Source window / status | Concrete folder or module | Acceptance criteria and evidence |
| --- | --- | --- | --- |
| 2.1 3D Design & Printing (interchangeable handles + housing) | W2-W3; not started | `hardware/2.1-interchangeable-grips/` | Editable CAD, print exports/settings, dimensions and assembly guide cover Sphere/Cylinder/Pencil, shared core, protective housing, spring slots and load-cell mounting. Physical fit report demonstrates each grip uses the same core, transfers load correctly and permits assembly/service; dimensional tolerances and test loads are documented before validation. CAD files alone do not pass physical fit acceptance. |
| 2.2 Circuit Assembly (Arduino Nano 33 IoT, HX711, Step-Up, Battery) | W2; not started; annotation: Tax Invoice Required for All Purchases | `hardware/2.2-electronics/` | Schematic, pin map, BOM, power budget, battery/charger/protection decisions, assembly photographs, and bench measurements identify the actual parts and power/signal paths. Verify rails and readout with assembled hardware against selected component requirements. Procurement register links each purchase to a tax invoice or flags missing evidence. Purchases remain user-managed unless separately authorized. |
| 2.3 Calibration (100kg Load Cell, verify in Newtons) | W2; not started | `firmware/2.3-load-cell-calibration/`; `tests/2.3-calibration/` | HX711 acquisition, tare, calibration factor and persisted metadata produce force in Newtons. Unit tests cover conversion, tare, invalid readings and calibration persistence. Multi-point known-load bench report records loading/unloading, repeatability, drift, and approved error tolerances, including grip/load-path effects. Capacity is not used as a patient's exercise target. |
| 2.4 Wireless Firmware (Bluetooth / Wi-Fi streaming) | W2; not started | `firmware/2.4-wireless-streaming/`; `tests/2.4-wireless-streaming/` | Firmware streams calibrated force with a documented versioned message format, units, timestamps/sequence and health flags. Test serialization, ordering/loss detection, disconnect/reconnect and sensor faults; demonstrate streaming on the Nano. Decide BLE versus additional Wi-Fi scope under G08 and record measured rate and latency against agreed budgets. Unity end-to-end verification belongs to 6.1. |

**Phase 2 checkpoint:** physical assembly, calibration and wireless bench evidence are reviewable; distinguish completed software tests from unperformed hardware checks. Stop for review.

### Phase 3 - Virtual World - Unity VR & Physics Development

| WBS / exact task title | Source window / status | Concrete folder or module | Acceptance criteria and evidence |
| --- | --- | --- | --- |
| 3.1 Environment Setup (Living Room / Kitchen scene) | W3; not started | `unity-project/Assets/Rehab/3.1-environment/` | Pin Unity version, desktop target and dependencies; project opens and builds on the agreed machine. Scene inventory and walkthrough cover the agreed Living Room/Kitchen interpretation, camera, virtual hand and input provider interface. A labelled fixture input allows scene verification before 6.1 live transport. |
| 3.2 Vision Algorithms (MediaPipe Hand Tracking + YOLO) | W4-W5; not started | `unity-project/Assets/Rehab/3.2-vision/`; `tests/3.2-vision/` | MediaPipe landmarks map consistently into Unity coordinates; derive documented kinematic metrics and explicit tracking confidence/invalid states. Define YOLO gesture taxonomy, model/data provenance and integration contract; perform agreed gesture evaluation on held-out examples if retained in GP1. Exercise hand loss, reacquisition, occlusion and timestamp alignment. G06 decides GP1 YOLO use and G07 assigns facial/rPPG work before claiming multimodal coverage. |
| 3.3 Physics & Deformation (Obi Softbody + Obi Fluid) | W4-W5; not started; annotation: Email to Library Developers | `unity-project/Assets/Rehab/3.3-deformation/`; `tests/3.3-deformation/`; `docs/3.3-library-correspondence/` | Version/license record and representative scenes demonstrate the agreed roles of both Obi packages. Force replay produces bounded deformation, release recovery and reset for supported objects without invalid physics state. Record frame-time results on the selected machine and mapping parameters. Prepare developer questions/correspondence draft; actual sending needs explicit user authorization. Resolve package availability and Fluid use case under G09. |
| 3.4 Game Modules Development | W6-W7; not started; parent task | `unity-project/Assets/Rehab/3.4-game-modules/`; `tests/3.4-game-modules/` | Shared module lifecycle, session context, metric events, difficulty interface and warning/stop hooks are defined and exercised. Parent GP1 acceptance requires 3.4.1; 3.4.2/3.4.3 remain explicitly deferred. Do not count the parent bar as an additional independent module or call all three games complete. |
| 3.4.1 Module 1: Force-Object Interaction Task | W6-W7; not started | `unity-project/Assets/Rehab/3.4.1-force-object-interaction/`; `tests/3.4.1-force-object-interaction/` | Playable virtual-hand task connects the force provider to Sphere/Cylinder/Pencil deformation. Session output measures force production, grip endurance and gesture stability using documented definitions. Replay verifies target attainment, hold/release, reset, invalid input and policy stop behavior. Identify simulated versus measured input in evidence; physical closed-loop sign-off occurs in 6.2. |
| 3.4.2 Module 2: Cognitive Performance Tasks | GP2 future; explicitly outside current scope; no scheduled bar | Reserved `unity-project/Assets/Rehab/3.4.2-cognitive-performance/`; future `tests/3.4.2-cognitive-performance/` | Current GP1 acceptance: record deferred scope and interface requirements only. Future GP2 acceptance: Gesture Sequence Memory with avatar and Spatial Assembly with virtual hand; sequence/matching scoring and reaction time, trajectory, accuracy, velocity verified with fixtures and playthroughs using agreed MediaPipe/YOLO inputs. No GP1 implementation or invented dates. |
| 3.4.3 Module 3: Visuomotor Coordination Tasks | GP2 future; explicitly outside current scope; no scheduled bar | Reserved `unity-project/Assets/Rehab/3.4.3-visuomotor-coordination/`; future `tests/3.4.3-visuomotor-coordination/` | Current GP1 acceptance: record deferred scope and interface requirements only. Future GP2 acceptance: avatar maze navigation with shortest-path objective/collision penalties and virtual-hand tracing with green/red correctness feedback; reaction time, trajectory, gesture stability and motion accuracy verified using deterministic paths and playthroughs. No GP1 implementation or invented dates. |

**Phase 3 checkpoint:** desktop build and Force-Object task demonstrated; package/vision evidence, performance limits and GP2 deferrals recorded. Stop for review.

### Phase 4 - AI Engine & Adaptive Difficulty

| WBS / exact task title | Source window / status | Concrete folder or module | Acceptance criteria and evidence |
| --- | --- | --- | --- |
| 4.1 Baseline Protocol (Baseline Assessment setup) | W8; not started | `ai-engine/4.1-baseline-protocol/`; `unity-project/Assets/Rehab/4.1-baseline-assessment/`; `tests/4.1-baseline/` | Reviewed trial protocol specifies maximum grip force, endurance, stability and ROM, trial/rest rules, units, quality checks and abort criteria. Baseline profile includes patient/session/shape provenance. Tests cover normalization, absent/invalid baseline and aborted assessment. Protocol review precedes dependent participant collection; resolve G04 rather than collecting against an undefined baseline. |
| 4.2 Data Collection (CSV: force, time, strain) | W3-W11 continuous source bar; not started; annotation: Google Form/Sheet & Ethical Approval | `ai-engine/4.2-data-collection/` with `schema/`, `protocol/`, `fixtures/`, `approvals/`; `tests/4.2-data-schema/` | Versioned CSV schema/data dictionary defines force, time and strain plus IDs, baseline reference, shape, kinematic/facial/physiological fields as approved, quality/missingness flags, labels and decision events. Schema validator and synchronized fixture/replay checks reject malformed units/timestamps and prevent cross-session mixing. Define strain and fatigue ground truth, participant/session split policy, collection protocol, retention/access and consent/ethical-approval records before participant collection. Synthetic fixtures remain labelled and cannot establish model effectiveness. Form/Sheet content is prepared locally; creation/submission is separate authorization. Resolve G03-G07 before asserting full collection completion. |
| 4.3 Model Training (GRU/LSTM - fatigue detection) | W11-W12; not started | `ai-engine/4.3-fatigue-model/`; `tests/4.3-fatigue-model/` | Reproducible training/evaluation configuration documents temporal windows, preprocessing, missing modalities, baseline normalization, fusion and output meaning. Select GRU or LSTM with rationale. Use participant-separated evaluation and fit transforms only on training data; report class support, held-out metrics, failure cases, and results against a stated baseline and pre-agreed criteria. Bundle model card, dataset provenance and artifacts. Insufficient real labelled data blocks efficacy acceptance; synthetic smoke tests verify execution only. |
| 4.4 ONNX Export & Unity Sentis Integration | W11-W12; not started | `ai-engine/4.4-onnx-export/`; `unity-project/Assets/Rehab/4.4-sentis-inference/`; `tests/4.4-model-parity/` | Export selected model with documented feature order, tensor shapes, temporal state/reset and preprocessing. Fixed evaluation fixtures compare training-runtime, ONNX and Unity Sentis outputs within a declared tolerance. Measure inference latency and test unsupported/missing model, invalid inputs and session reset. Record actual compatible versions/operator support; do not assume compatibility. |
| 4.5 Decision Logic (adaptive difficulty + safety stop) | W11-W12; not started | `ai-engine/4.5-decision-policy/`; `unity-project/Assets/Rehab/4.5-adaptive-difficulty/`; `tests/4.5-decision-policy/` | Reviewed state table implements PDF branches: stable potential -> bounded adaptation; one sign -> Resume/Quit warning; multiple signs -> force quit. Specify distinct signs, thresholds, persistence/hysteresis, missing-input handling, stale prediction behavior and resumption authority. Deterministic replay tests cover each branch, simultaneous signals, boundary transitions, user quit and latched stop without automatic restart. Compare Unity behavior with policy fixtures. Clinical adequacy is not established by passing software tests. |

**Phase 4 checkpoint:** baseline/data provenance, model evidence, export parity and decision-policy replay are reviewed. Unresolved sensing, labels or thresholds remain explicit blockers to the relevant acceptance claim. Stop for review.

### Phase 5 - Dashboards Development

| WBS / exact task title | Source window / status | Concrete folder or module | Acceptance criteria and evidence |
| --- | --- | --- | --- |
| 5.1 Patient Dashboard (results + visual feedback) | W21; not started | `dashboards/5.1-patient/`; `unity-project/Assets/Rehab/5.1-patient-dashboard/`; `tests/5.1-patient-dashboard/` | Agreed patient view exposes GP1 task access, visual session feedback and results with units and clear session context. Fixtures verify summaries agree with stored metrics, incomplete/stopped sessions are distinguished and deferred games are not presented as playable. Warning/quit interactions use 4.5; missing data is not presented as a valid zero. |
| 5.2 Doctor Dashboard (history, stats, recommendations) | W21; not started | `dashboards/5.2-doctor/`; `unity-project/Assets/Rehab/5.2-doctor-dashboard/`; `tests/5.2-doctor-dashboard/` | Patient-specific history, progression statistics and traceable recommendations reproduce known session fixtures without patient mixing. Recommendation basis and limits are visible; policy/configuration changes and stop events retain provenance. Confirm storage, identity/access scope and recommendation rules under G10; verify them at the agreed prototype level. |

**Phase 5 checkpoint:** patient/doctor walkthroughs reconcile with session data, including aborted and missing-data cases. Stop for review.

### Phase 6 - System Integration, Testing & Documentation

| WBS / exact task title | Source window / status | Concrete folder or module | Acceptance criteria and evidence |
| --- | --- | --- | --- |
| 6.1 Hardware-Software Bluetooth Integration | W21 only; not started | `unity-project/Assets/Rehab/6.1-bluetooth-integration/`; `tests/6.1-bluetooth-integration/` | Actual Nano/HX711/load-cell stream reaches the desktop Unity force provider using the 2.4 contract. Report units, timestamps, rate, loss and latency; exercise reconnect, stale packets, sensor failure and session isolation. Verify invalid data reaches 4.5's defined handling and cannot silently drive adaptation. Hardware test evidence is required beyond a simulator. |
| 6.2 Closed-Loop Testing (force -> deformation -> AI -> difficulty) | W21 only; not started | `tests/6.2-closed-loop/` | Versioned scripts/protocol replay and physically demonstrate force -> object deformation -> features -> temporal inference -> difficulty/warning/stop -> dashboard log. Trace one session across all components. Cover all three grips, baseline handling, ordinary adaptation, one/multiple signs, dropout, invalid data and restart. Publish measured performance against agreed budgets, results, known defects and distinction between bench/software evidence and participant evaluation. |
| 6.3 Final Documentation & Presentation Preparation | W21 only; not started | `docs/6.3-final-documentation/` | Reproducible setup/build/calibration/run guide, as-built architecture/BOM, WBS evidence matrix, model/data cards, test report, limitations, licensing/invoice index and presentation/demo material match the actual implementation. Document GP2 deferrals and unresolved gaps. Verify a clean setup/demo walkthrough on the agreed target and record the final user review; clarify W22 milestone meaning under G11. |

**Phase 6 checkpoint:** review complete GP1 evidence and remaining limitations. GP2 development requires a separate scope/calendar approval; it does not start automatically.

## 6. Gaps, conflicts, and decisions requiring disposition

No issue below silently adds a new canonical WBS task or changes the source schedule. Proposed owners use existing IDs. Plan approval authorizes later agreed work; it does not supply missing measurements, clinical definitions, records or external permissions.

| ID | Evidence and conflict/gap | Proposed disposition / owning WBS | Needed before |
| --- | --- | --- | --- |
| G01 | Files are in `E:/Downloads`, not `docs/`; repo is a mixed workspace with no commits and an existing webcam prototype. | Map to the requested root for now. During approved scaffolding, copy the two exact sources and preserve hashes. Confirm whether to retain this root or isolate the capstone; evaluate prototype reuse separately, without treating estimated effort as measured force. Owner 1.1. | Scaffolding / initial staging |
| G02 | PDF describes all three training modules, but Gantt explicitly places 3.4.2 and 3.4.3 in GP2 outside current scope. | Keep complete architecture traceability and reserve paths; implement only 3.4.1 in GP1. Seek a revised schedule if the user wants all three now. Owner 1.1 / 3.4. | Phase 3 scope sign-off |
| G03 | The user requests one task at a time and phase reviews; Gantt overlaps phases/tasks. 2.1 runs through W3 while 2.2-2.4 are W2; 4.2 begins W3 despite belonging to Phase 4; Phase 5 and 6 share W21. | Preserve bars as the authoritative baseline and follow sequential WBS execution. Record schedule variance; any early collection preparation or reordered work needs an explicit revision to the execution sequence. Do not claim both schedules are simultaneously satisfied. Owner 1.1 / 1.3. | Agreed execution schedule |
| G04 | 4.2 collection is W3-W11, before 4.1 baseline in W8, and its continuous bar includes blocked W9-W10. Training 4.3 begins W11 while collection is still scheduled. | Distinguish form/protocol preparation, bench/fixture capture and participant collection. Resolve baseline availability, ethical approval, pauses and dataset freeze timing. Proposed pause during W9-W10, with any revised active work dates explicitly approved rather than editing the chart implicitly. Owners 4.1-4.3 / 1.3. | Collection and training |
| G05 | Gantt's `force, time, strain` does not define strain or fatigue labels and does not cover the PDF's full feature set. | Define whether strain is self-report, mechanical strain, or another measure; define units, source and annotation timing. Review label method, sample needs and synchronized schema; keep missing modalities explicit. No default numeric strain or invented fatigue ground truth. Owner 4.2. | Schema freeze / collection |
| G06 | YOLO appears in GP1 task 3.2, but PDF only names it under GP2 cognitive/visuomotor games; GP1 force task names MediaPipe. | Decide whether GP1 delivers a tested reusable YOLO adapter or defers its runtime implementation. Preserve task title and report any approved scope reduction. Gesture classes/data are unspecified. Owners 3.2 / 3.4. | Vision implementation |
| G07 | PDF requires facial seven-emotion monitoring (SSD MobileNet / ExpressionNet) and HR/HRV via rPPG, but no Gantt row explicitly implements them. Their mapping to fatigue is also unspecified. | Proposed ownership: sensing adapters under `3.2-vision/facial/` and `3.2-vision/rppg/`, synchronization/schema in 4.2, fusion in 4.3, policy in 4.5. Confirm feasibility and time allocation or explicitly approve a reduced-modality GP1 scope. Do not advertise full multimodal delivery while these are absent. | Phase 3/4 scope and estimates |
| G08 | PDF labels wireless Bluetooth; 2.4 says Bluetooth / Wi-Fi; 6.1 explicitly tests Bluetooth. No BLE service/characteristic, payload or rate is defined. | Proposed GP1 transport is BLE per the user's request, with Wi-Fi pending clarification. Specify transport, desktop adapter/OS, timing and reconnection contract in 1.1/2.4; validate actual integration in 6.1. | Firmware contract |
| G09 | Both Obi packages are named, but Fluid's gameplay role, licenses/availability and Unity compatibility are unspecified. "Email to Library Developers" is only a chart annotation. | Define separate demonstrations/use cases for both packages or record an approved substitution/deferment. Inventory authorized packages and versions; draft questions locally. Sending correspondence requires explicit user instruction. Owners 1.2 / 3.3. | Physics implementation |
| G10 | PDF places dashboards inside Unity; user also requests a `/dashboards` root. Neither document specifies storage, patient identity/access, deployment or recommendation generation. | Proposed Unity runtime plus dashboard contracts/fixtures at root, backed by an agreed local session store. Decide UI location, persistence and access scope; document recommendation rules without inventing clinical prescriptions. Owners 1.1 / 5.1 / 5.2. | Data contracts and dashboard work |
| G11 | Chart metadata says End Date: W22 - 6/2/2027 and marks GP1 Done at W22, but 5.1, 5.2 and all Phase 6 bars cover only W21. W13-W14 are spare; W15-W20 paused. | Preserve W21 bars and W22 marker. Clarify whether 2027-02-06 is a hand-in milestone or W22 work is intended; do not move documentation/testing to W22 or consume buffer automatically. Owners 1.3 / 6.3. | Calendar commitment |
| G12 | Source calls 1.2 completed, but the two reference files do not contain its full literature review, BOM or selection evidence. | Preserve historical completion as reported; reconcile/import evidence under 1.2 and distinguish evidence gaps from task status. Do not fabricate bibliography or mark unrelated prototype files as completion evidence. | Phase 1 acceptance |
| G13 | W3 Google Form and W8 Paper Prep are milestone labels without their own WBS IDs. | Proposed ownership: form preparation under 4.2; paper preparation under 6.3 with scheduling coordination in 1.3. This conflicts with phase-sequential execution and needs explicit early-preparation authorization or a revised schedule. No invented task numbers and no automatic external submission. | Milestone planning |
| G14 | PDF is conceptual: wiring arrows, spring illustrations and 100 kg capacity do not define circuit rails, exact load-cell part, load transfer, charger/protection details or print tolerances. | Produce verified electrical/mechanical specifications and calibrated assembly measurements in 2.1-2.3. Record component substitutions and invoice evidence. Capacity alone is neither an accuracy guarantee nor an exercise limit. | Physical build validation |
| G15 | "Stable potential," single/multiple fatigue signs, baseline procedures and stop/resume thresholds lack operational definitions or validation evidence. | Review a concrete state/threshold specification in 4.1/4.5 with the project supervisor or relevant clinician before participant-facing use. Define distinct signals, correlation handling, invalid-input fallback and permitted resumption. Software tests demonstrate conformance to that specification only. | Policy acceptance / participant use |
| G16 | No versions, target performance budgets, training dataset size/class balance, camera conditions or calibration/model pass thresholds are supplied. | Set measurable criteria and resource constraints in 1.1/1.2 and the relevant owning tasks before implementation validation. Resolve Sentis/ONNX compatibility in 4.4. Record physical checks and participant evidence separately from simulated tests. | Task-level acceptance |

## 7. Initial review gate (historical; superseded by sections 8–9)

**Current deliverable:** this planning document only. Sources remain at their supplied paths; no capstone directories, feature code, branches or commits were created in this step. Existing projects are unchanged.

For review, the most consequential decisions are G01 (repository location), G02 (GP1/GP2 boundary), G03-G04/G13 (sequential execution versus early/overlapping calendar work), and G07 (unallocated sensing scope). Other open issues can be resolved in their owning tasks before dependent work begins. Approval of this plan does not automatically resolve an issue marked pending.

**After the user's go-ahead:** scaffold the approved layout, copy/reference the reviewed source files, then begin 1.1 and continue Phase 1 tasks in order. Preserve the historical 1.2 status while reconciling its evidence. Stop with the Phase 1 summary before starting Phase 2. Do not begin the scaffold or any feature implementation until the user confirms.

## 8. W1/W2 authorization update

The user requested "make full complete W1 & W2" after reviewing the plan. This authorizes scaffolding, root-layout use and sequential 1.1-2.4 delivery, including continuing beyond the Phase 1 checkpoint for this request. Historical planning-only statements above describe the initial inspection, not the current implementation status. BLE is selected per the original request. All later phase gates remain. Physical evidence is never inferred from code, CAD or fixtures.

The user subsequently clarified that they own no hardware or CAD and requested full Phase 1 and Phase 2. Scope now includes the complete local deliverable package for 1.1-2.4, including 2.1 design work extending into W3. This does not create procurement, printing, assembly or physical measurements remotely. See the delivery evidence matrix for the distinction between implemented software/design files and remaining acceptance gates.

## 9. Phase 2 finishing revision and permanent location

The canonical workspace is `E:\GP\codex`. Phase 1/2 software and local design work are implemented; the planning-only gate in section 7 is historical. Phase 3 was subsequently authorized by the user on 2026-09-23; see the execution note below.

WBS 2.1-R2 now supplies a compact TAL220 configuration, a larger nominally non-overlapping electronics layout, metal force-transfer plates/spacers, common shell mounts, stop/guide geometry, 13 closed meshes and four DXF outlines. This supersedes the R1 nominal sensor/battery mismatch without claiming a fabricated assembly. WBS 2.2-R2 documents separate bridge excitation because the stock HX711 excitation output is below the selected sensor's specified minimum.

**G17 — ergonomic and mechanical acceptance remains open:** R2's oversized bench shells are not suitable evidence of a handheld rehabilitation product. The PDF's compact universal Sphere/Cylinder/Pencil packaging is still unproven. The initial R2 build omits auxiliary springs and relies on sensor elastic return to avoid a parallel force path; this differs from the illustrated architecture. Final handheld geometry, materials, fastener/guide selection, stop setting from measured sensor deflection, strength, and physical comfort need mechanical/ergonomic review and bench evidence. These are not silently marked complete.

The local receipt and reproducible archive are generated by `verify_phase2.py`. Firmware host tests, nominal geometry checks and synthetic traffic do not establish physical or clinical acceptance. Continue with real procurement, fabrication and bench measurements when equipment is available. The later 2026-09-23 Phase 3 authorization is recorded below.


## Phase 3 scope authorization and separate workspace (2026-09-23)

The user explicitly authorized all Phase 3 Unity work in a **new separate folder**. The implementation lives in `unity-phase3/` on branch `codex/3-unity`, while the earlier `unity-project/` scaffold and Phase 1/2 source remain intact. WBS IDs still govern commits and documentation. This new instruction also moves 3.4.2 and 3.4.3 forward from the Gantt's GP2 deferral into the current Unity demo; the source Gantt dates and GP2 labels have not been edited. This is a recorded scope variance under G02, not a claim that the historical timeline changed.

Current Phase 3 acceptance evidence is in `unity-phase3/docs/QA.md` and `unity-phase3/docs/PHASE3_STATUS.md`. A Unity 6 Windows desktop demo builds and runs with all five tasks, simulated grip force, procedural rooms and objects, a real optional MediaPipe camera bridge, and a validated localhost landmark contract. 3.2 is not fully accepted without a project-specific YOLO model, gesture evaluation and observed real-hand detection. 3.3 is not fully accepted without legitimately supplied Obi Softbody/Fluid packages, a defined Fluid use case and benchmarks. The demo does not complete the hardware/BLE, fatigue AI, dashboard or clinical acceptance of later phases.

## WBS 6.1 software integration advance (2026-09-23)

The user requested that Phase 3 Unity connect to the existing Phase 2 implementation before physical Phase 6 work. Branch `codex/6-integration` adds a Phase 2 decoder/metadata-confirmed BLE-to-loopback bridge and a selectable Unity live-force provider under `unity-phase3/Assets/Rehab/6.1-bluetooth-integration/`. The source Gantt still places 6.1 in W21. Software acceptance is supported by protocol tests and a built-player synthetic stream; the actual Nano/HX711/load-cell and radio acceptance in the 6.1 row remains open because no hardware exists. This is a scope/timing advance, not a claim that the original timeline or clinical pipeline is complete.

## 3.1/3.3 visual refinement (2026-09-23)

The Unity project gained reproducible PBR-style surface maps, detailed Living Room/Kitchen furniture and rehabilitation station props, soft lighting, URP post-processing, an articulated force-responsive hand and mode-specific 3D boards. These refine the previously authorized Phase 3 demonstration under WBS 3.1, 3.3 and 3.4 without changing the source Gantt dates. Visual QA and limitations are recorded in `unity-phase3/docs/VISUALS.md`; procedural geometry and bounded scale deformation are still not licensed Obi simulation or photogrammetry.
