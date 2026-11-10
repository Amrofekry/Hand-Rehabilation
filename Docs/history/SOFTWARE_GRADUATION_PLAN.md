> 12 October 2026: restored the earlier VirtualGrasp prototype for Unity 2022.3.0f1 at the user's request. Later hand/contact/soft-body evidence below is historical. See [restoration report](ROLLBACK_2026_10_12.md) and [current 23 tasks](SOFTWARE_TASKS_23.md).

# Software graduation completion plan

Reviewed: 2026-10-06. Item 2 implementation update: 2026-10-06. Project: `E:\GP\codex\GripRehab-Unity`.

## Scope and authority

The original source-review pass changed documentation only. The subsequent user request authorizes item 2: integrating the existing local accounts and patient journey. The implementation uses the current authored scene; packages, vendor code, firmware, CAD, real records and the original schedule remain unchanged. Other numbered work is not being implemented by this item.

Target: a complete desktop semi-immersive Unity research application, with all three modules, patient/doctor journeys and the documented AI pipeline. This does not require a headset. Hardware fabrication/firmware changes and physical, human or clinical trials are outside this software checklist. Their exclusion does not make their evidence complete.

English remains the application language. Voice commands remain removed and optional future work. No scheduled follow-up is authorized. Document annotations about emails, forms, ethics and invoices are context, not authorization for external actions.

The 6 October repair closeout covers the existing game. It does **not** close all architecture requirements. Existing implementations and regression evidence must be reused; do not replace working profiles, hands, apartment or menus simply to satisfy a new folder layout.

## Sources read and checked

The Pipeline and Blockdiagram each have one page, read as extracted text and rendered images. All 24 Gantt task rows and all 22 week headers were read. Project copies match the parent source hashes. The original `PROJECT_PLAN.md` is historical traceability, not the current runtime inventory.

| Source | SHA-256 | Use |
| --- | --- | --- |
| `Source/Hand_Rehab_Pipeline.pdf` / parent `docs/Hand_Rehab_Pipeline.pdf` | `d23b3b8ed6fa476acdb9da0f0308589037ae82f3460566a2ade883d45408199e` | Modules, controllers, measurements, multimodal AI and dashboards |
| `Source/ganttchart.html` / parent `docs/ganttchart.html` | `53456905baa976e8d3c3cc54b4710073bddca71061b18e43eb8a9573ca90cc1c` | Canonical WBS, GP1/GP2 distinction, calendar and named integrations |
| `Source/Blockdiagram.pdf` | `9dfc8cb44a41d71b35517ab20cc6d64bce49e7557c20a3fe39f4da3100eecf80` | Patient steps 01-06 and doctor steps D1-D5; supplemental previously supplied source |

Week 1 is 12 September 2026; W22 starts 6 February 2027. W9-W10 are blocked, W13-W14 buffers, W15-W20 paused. Original dates, bars and WBS IDs are unchanged. This backlog is an implementation order, not a new dated schedule.

## Source-review findings (before item 2 integration)

- `Assets/Scenes/GripRehab.unity`, the apartment, English HandMate menus, whole-hand dwell pointer, keyboard controls, imported VirtualGrasp driven rig, bounded contact deformation and BLE bridge have current repair evidence in `CLOSEOUT_2026_10_06.md`. Reuse these foundations.
- `RehabExerciseSuite.cs` exposes all four Module 2/3 demo tasks. Memory uses numeric tokens; assembly is a fixed numeric order puzzle; maze is a text grid with buttons/arrows; tracing follows five waypoints. These are playable game prototypes, not yet the full gesture/avatar/virtual-hand tasks described in the Pipeline.
- `ResearchPortalHost.cs`, `ResearchPortalPages.cs` and `ResearchSessionRuntime.cs` contain local login, baseline, history, notes, export and statistical guidance code. `RehabAppShell.Awake` only obtains an explicitly attached host. Its GUID `ec72363e6a22ee84081de6d5cc2b7a5f` occurs in **no saved scene or prefab** in the inspected Assets tree. Do not advertise the expanded portal as the active delivered journey. Integrate and test it before enabling it.
- `CapabilityTrial.cs` currently collects 12 seconds of accepted input with peak/mean force, closure variation and motion statistics. It does not provide the complete staged maximum-force/endurance/ROM assessment specified in the Pipeline. The portal passes consent=true directly; add an actual explicit consent/withdrawal step rather than treating that argument as user consent.
- `Assets/Product` has reusable role-gated repository, password verifier, record and export code. `WindowsProductStore` and the active `RehabProfileStore` contain protected persistence/recovery paths. Expand and verify these; do not recreate them from zero or copy real patient data into fixtures.
- `AI-Engine/4.2-data-collection/features.py` has limited local feature windows; C# has statistical fallback policies. `AI-Engine/4.3-fatigue-model/README.md` and `4.4-onnx-export/README.md` explicitly say reserved/not implemented. No owned ONNX model was found and `Packages/manifest.json` has no Sentis dependency.
- The inspected owned vision/runtime tree contains no implemented YOLO, facial seven-emotion or rPPG adapter. The existing non-Obi deformation identifies itself as a bounded fallback. Missing named integrations cannot be marked complete merely because a substitute works.

This was a read-only source/code audit plus documentation update. No new Unity run, camera trial, performance capture or model evaluation was conducted in this review. Existing native/software results remain dated and scoped as recorded in the closeout.

## Updated numbered work list

These **new numbers 1-21** identify graduation completion work. They are distinct from the previous repair list 1-11. Item 1 is closed. Item 2 now has implemented source and passing file/domain checks, with native Editor/player acceptance blocked by the Software Terms dialog; it is not yet checked as DONE. PARTIAL means usable foundations exist, not that the full acceptance criterion is met. WBS IDs remain the canonical commit/task identifiers.

| No. / status | Work and WBS | Existing foundation | Software acceptance criterion |
| --- | --- | --- | --- |
| **1 DONE** | Source requirements, gaps and completion scope - 1.1 | Original plan, requirements and current evidence | All 24 source WBS rows accounted for below; source hashes verified; named tasks/metrics and conflicts recorded; hardware/external work separated; future list and acceptance defined |
| **2 IMPLEMENTED; NATIVE VERIFICATION PENDING** | Connect the complete patient journey and local accounts - 1.1, 3.4, 5.1, 5.2 | Active runtime account portal around authored profile/module panels; consent, protected sessions, linked Continue and isolated native test fixture | Real menu route reaches patient sign-in/profile, preparation, baseline, each module and results. Guest/account ownership, sign-out and doctor access tests pass. Preserve legacy Continue semantics; no duplicate hosts/listeners or scene regeneration |
| **3 PARTIAL** | Staged baseline and capability profile - 4.1 | `CapabilityTrial`, profile models | Explicit consent and cancellation; distinct peak measured force, endurance, documented joint ROM and gesture-stability stages. Each result carries unit, side, shape, calibration, source, version and validity. Camera-only mode leaves measured force missing; source/calibration change restarts the relevant stage. Deterministic fixtures verify reductions and saved starting level |
| **4 PARTIAL / DEPENDENCY** | Complete vision metrics and gesture recognition - 3.2 | MediaPipe 21-landmark bridge, coordinate mapping and whole-hand pointer | Preserve both-hand tracking; add documented ROM/tremor/speed/trajectory definitions and gesture events for modules. Specify classes, model provenance and a versioned YOLO adapter. Supplied compatible model must pass labelled held-out evaluation before a YOLO claim; geometric fallback remains identified. Frame loss, reacquisition, occlusion and side/calibration changes have software regressions |
| **5 PARTIAL / DEPENDENCY** | Resolve physics package requirements and object grasp profiles - 3.3 | Contact mesh deformation, fracture, VirtualGrasp rig | Document separate Softbody/Fluid roles, package compatibility/license and bounded force-response tests. Integrate supplied licensed packages if available; otherwise retain the labelled mesh substitute and explicitly document the architecture deviation. Complete object-specific grasp setup supported by imported SDK, or document its replacement; no missing baked DB claim. Recovery/reset, release, penetration and no-player-recoil tests pass |
| **6 PARTIAL** | Complete Force-Object Interaction exercise - 3.4.1 | Apartment grip/deformation activity | Explicit Sphere/Cylinder/Pencil exercise variants linked to the shared input shape contract; retain everyday props as additional objects. Defined target/hold/release rounds and source-specific force production, grip endurance and gesture stability results. Invalid or changed input stops scored progress; record completion, interruption and reset with units and versioned demo targets |
| **7 PARTIAL** | Complete Cognitive Performance - 3.4.2 | Memory and numeric assembly demos | Gesture Sequence Memory: observe, memorise and reproduce gesture sequence through the avatar/controller. Spatial Assembly: virtual-hand piece pickup, move, align and placement rather than numeric order alone. Save per-task reaction time, accuracy/errors, trajectory and velocity with documented units; keyboard alternatives, restart and reproducible difficulty fixtures work |
| **8 PARTIAL** | Complete Visuomotor Coordination - 3.4.3 | Grid maze and waypoint tracing demos | Maze: gestures drive an avatar/controller, collisions reduce score and shortest-path efficiency is defined. Tracing: virtual-hand-driven dashed paths show correct/incorrect green/red feedback. Save reaction time, trajectory, gesture stability and motion accuracy; score cannot advance through invalid tracking; keyboard alternatives and reproducible path fixtures work |
| **9 PARTIAL** | Versioned collection and synchronized feature windows - 4.2 | Python/C# force-motion windows and metric records | Specify CSV/session schema and monotonic alignment for force, time, strain, motion, facial and physiological features. Resolve strain meaning; store unknown/invalid as missing with quality flags. Session/side/shape/calibration boundaries, retention, explicit consent and synthetic provenance are tested. Window assembler is shared by training and Unity inference; no raw camera images retained |
| **10 NOT IMPLEMENTED** | Facial seven-emotion sensing - 3.2, 4.2, 4.3 | Requirement in Pipeline only | Local opt-in face adapter using supplied compatible SSD MobileNet/ExpressionNet assets or an explicitly documented alternative. Output seven-class probabilities, capture timing, confidence and missing-state flags into feature windows. No-face/model faults pause that modality. Emotion labels alone are not fatigue ground truth; no raw-frame saving |
| **11 NOT IMPLEMENTED** | rPPG physiological sensing - 3.2, 4.2, 4.3 | Requirement in Pipeline only | Local opt-in rPPG adapter outputs HR in bpm, documented HRV metric/unit, signal quality and acquisition-window state. Low light/motion/insufficient windows return unavailable, not zero or fabricated values. Synthetic signal and recorded authorized fixture tests validate the algorithm path; no clinical accuracy claim or raw-frame retention |
| **12 NOT IMPLEMENTED** | Temporal fatigue training pipeline - 4.3 | Statistical fallback, reserved training directory | Reproducible GRU or LSTM training/evaluation with baseline context, multimodal masks, participant-separated train/validation/test and reproducible configuration. Define labels/provenance and report sensitivity/specificity or agreed metrics, model card and limitations. Synthetic fixtures validate code only; a participant-trained performance claim requires an appropriate dataset |
| **13 NOT IMPLEMENTED / DEPENDENCY** | ONNX export and Unity inference - 4.4 | Reserved export directory, no pinned Sentis/model | Export model with declared inputs/windows/normalization and test parity against training runtime. Pin compatible Sentis package and implement loading/warm-up/inference/model-error handling. Compare Unity inference to known vectors, profile allocations/latency and identify model vs statistical fallback in UI/results. No substitute is described as Sentis inference |
| **14 PARTIAL** | Integrate adaptation and warning/stop decisions - 4.5 | Versioned research statistical/state policies | Baseline-relative bounded level up/down across modules requires valid stable input and cooldown. One confirmed independent sign offers Resume/Quit; more than one latches stop with no automatic resume. Missing inputs/model faults block escalation. Correlated features do not count as independent signs. Deterministic boundary/fault tests and persisted policy events match a versioned, unvalidated research specification |
| **15 PARTIAL** | Complete Patient Dashboard - 5.1 | Current summary, dormant history/chart code | Show actual saved per-module metrics with source/unit/validity, baseline comparison and progression charts. Distinguish guest, incomplete, synthetic and safety-stopped sessions. Numeric reductions reproduce known fixtures; patient cannot read another patient's records; results survive restart |
| **16 PARTIAL** | Complete Doctor Dashboard D1-D5 - 5.2 | Current preview, dormant role-gated search/history/notes | Sign in, select patient, review history/statistics/fatigue-stop events, save attributable recommendations and sign out. Compare the required metrics rather than best score alone. Notes retain author/time and are research guidance; apply any doctor-configured game plan with validation/audit. Cross-patient access, save failure and restart tests pass |
| **17 PARTIAL** | Integrate protected records and CSV/PDF export - 4.2, 5.1, 5.2, 6.3 | DPAPI stores, PBKDF2/repository, CSV/PDF writers | Account/profile/session IDs link consistently through migration without data loss. Atomic read-back/recovery, corruption, interrupted saves and storage limits tested. Explicit export includes complete baseline/module/quality/policy data, missing values, source and units; generated PDF pages and CSV formula escaping reviewed. Use fabricated fixtures; no real records in Git |
| **18 PARTIAL; CURRENT V1 SOFTWARE VERIFIED** | Unify the force/hand input boundary - 6.1, 3.4 | Existing BLE v1 bridge/UDP, freshness and domain interfaces | Keep Nano 33 IoT/HX711 BLE v1 IDs/milliNewton-to-Newton mapping, ports 27818 force and 27817 vision. One canonical force and hand contract must be wired to baseline, gameplay, features and policy; reconcile current overlapping force interfaces. Software replay covers sequence/session/shape/calibration, full 500 ms age budget, reconnect and faults, without silent hardware-to-demo fallback. USB/v2/Wi-Fi are not mandatory additions from this source review |
| **19 PARTIAL; CURRENT DESIGN VERIFIED** | Finish English UI/UX for expanded routes - 3.1, 3.4, 5.1, 5.2 | HandMate saved presentation, mouse/keyboard/palm-dwell/accessibility | Reuse current design on login, baseline, all task states, dashboards, consent, warnings and export/recovery. Instructions, units, loading/error/empty states and start/pause/quit controls are consistent. Whole-hand dwell and keyboard/mouse work on every enabled route, with focus/modal protection. No Arabic or voice reintroduction |
| **20 PARTIAL; REPAIR QA PASSED** | Full software closed-loop and performance - 6.2 | Current 69 Python, native player/UI/gallery and three-cycle QA evidence | Replay full baseline -> force/deformation -> multimodal windows -> temporal inference -> difficulty/warning/stop -> stored dashboards. Cover all modules/grip variants, lost/stale/duplicate input, corrupted storage and exit/restart. Repeat applicable Editor and built-player journeys with isolated records. Measure camera-enabled workload/performance where available, state hardware specs and unavailable measurements; 60 FPS and hot-loop allocation targets are goals until captured. Existing UI-only samples do not close expanded runtime performance |
| **21 PARTIAL** | Final software packaging/documentation - 6.3 | Current Windows repair build, guides and credits | New verified Windows release after items 2-20, setup/build/run/developer guide, as-built architecture, dataset/model cards, dependency/asset license inventory, requirements-to-current-evidence matrix, report and presentation/demo material. Clean local setup reproduces results; limitations and external evidence remain explicit. Do not use the earlier repair build as evidence of newly integrated features |

Recommended order: keep this numbering for requests; establish items 2-3 and the shared contracts of 9/18 first, develop 4-8, then 10-14, integrate 15-19 and finish 20-21. Dependency-free work may continue while models/licenses/datasets are unavailable, but a missing named integration must retain its open/deviation status. No original calendar allocation is silently changed.

## All Gantt rows accounted for

This is a coverage map, not a claim of completion of every WBS row.

| Canonical WBS | Software list disposition |
| --- | --- |
| 1.1 | Item 1 source review completed; item 2 implements the final runtime architecture |
| 1.2 | Software library/model/license rationale in 4, 5, 12, 13, 21; hardware review excluded |
| 1.3 | Source calendar/milestones preserved by item 1; competitions/conferences scheduling is outside game implementation |
| 2.1 | CAD/printing excluded |
| 2.2 | Circuit assembly/purchases excluded |
| 2.3 | Physical calibration and firmware changes excluded; consume calibration identity/units in item 18 |
| 2.4 | Firmware/wireless bench work excluded; preserve existing BLE contract in item 18 |
| 3.1 | Existing scene foundation; expanded UI and final release in 19, 21 |
| 3.2 | 4, 10, 11 |
| 3.3 | 5 |
| 3.4 | 2, 18, 19 |
| 3.4.1 | 6 |
| 3.4.2 | 7; full-project extension to the chart's GP2 label |
| 3.4.3 | 8; full-project extension to the chart's GP2 label |
| 4.1 | 3 |
| 4.2 | 9, 10, 11, 17; participant collection/forms/approval are external |
| 4.3 | 12, consuming 9-11 |
| 4.4 | 13 |
| 4.5 | 14 |
| 5.1 | 2, 15, 17, 19 |
| 5.2 | 2, 16, 17, 19 |
| 6.1 | 18 software contract/replay; actual Bluetooth device validation external |
| 6.2 | 20 software closed loop; physical and clinical sign-off external |
| 6.3 | 17, 21; software handover only |

## Conflicts, gaps and declared handling

1. **GP1 versus full project:** Gantt and Blockdiagram defer Modules 2/3 to GP2; Pipeline describes them. The user now requests a full-project backlog, so items 7/8 are included as extensions without changing source dates/labels.
2. **Playable versus architecture-complete:** Current numeric/grid/waypoint demos and doctor summary are not the full controllers, measurements or doctor journey. Existing portal source is not a saved runtime binding. Integrate reusable work and verify the actual user route, not just domain fixtures.
3. **YOLO placement:** Gantt includes YOLO in 3.2, whereas Pipeline places it under cognitive/visuomotor modules. Item 4 is a shared recognizer consumed by 7/8; retain geometric fallback labels until a compatible supplied model has evaluation evidence.
4. **Facial/rPPG allocation:** Pipeline specifies both; Gantt has no separate row. Assign adapters to existing 3.2, synchronized features to 4.2 and fusion to 4.3. They are not silently removed from full-architecture scope or given invented WBS numbers.
5. **Obi Fluid role:** Both Obi packages are named, but no fluid task is defined and availability is unresolved. Item 5 requires a concrete representative role or an explicit documented substitution/deferment. Do not purchase or email automatically.
6. **Data terminology:** Gantt's force/time/strain is narrower than Pipeline's multimodal features, and strain has no definition. Item 9 must specify it and label methods; do not synthesize a clinical label or confuse it with hand closure.
7. **Baseline/policy definitions:** MVC, endurance, ROM, stable potential and fatigue signs have no operational protocol/thresholds in the source. Items 3/14 need versioned research definitions and fixture tests. Seven emotions and multiple features of one sensor do not automatically constitute independent fatigue signs.
8. **Transport and added requirements:** Pipeline/6.1 select Bluetooth; 2.4 also mentions Wi-Fi. Current BLE v1 remains the target. Prior USB/v2 plans, password accounts, encryption and CSV/PDF exports have user/engineering provenance, not an invented Gantt row; do not require a transport rewrite to close the game's existing bridge.
9. **Schedule feasibility:** Collection overlaps baseline/training and blocked weeks; Phase 5/6 are in W21 despite a W22 completion marker. Preserve those historical facts. No new deadline or blocked-week work is assumed by this audit.
10. **Current language/scope:** Earlier broad requirements mention Arabic. The user's later request removes Arabic and voice. English-only is current scope; review does not revive CAD, firmware, subscriptions, external approvals or clinical certification.

## Closure rules

- A numbered item is checked only after its stated software acceptance and current relevant tests/build evidence exist. Repository file presence or a menu placeholder is not sufficient.
- A missing model/package/dataset remains a dependency or explicit architecture deviation; never label an alternative as the absent named implementation. Synthetic training/parity can verify plumbing, not participant predictive accuracy.
- Supervised live-hand trials, physical Nano/HX711 tests and clinical validation are maintained separately, not inserted as work for the user in this software checklist. They remain unverified unless actually performed.
- Update this plan and the requirement evidence when an item is completed. Keep the original repair task numbers and historical reports intact. Item 2 implementation and its outstanding native gate are recorded in [JOURNEY_ITEM_2.md](JOURNEY_ITEM_2.md). Historical repair/source-review results do not validate the new journey.

## Item 2 current evidence and next task

The new default entry composes one `ResearchPortalHost`/themed view at runtime. Patient registration uses the existing hand-profile form; preparation exposes Camera, Keyboard Demo and Phase 2 explicitly. Optional quick-baseline consent, all three existing module demos, source-attributed results, sign-out and linked Continue use protected local records. A doctor can explicitly assign a preserved legacy profile; unauthenticated users cannot claim another patient's profile. Continue restores checkpoint choices and starts a new attempt; it does not fabricate unfinished exercise progress.

File-only compilation of the five Editor and four player assemblies and 40 managed product tests passed. The native three-cycle account journey is prepared but NOT run. The existing Windows executable is the earlier repair release, not this feature. User unavailable to handle Unity Software Terms; no acceptance or queued automatic Play was attempted. [Detailed implementation, tests and limitations](JOURNEY_ITEM_2.md).

Next requested implementation remains **3: staged baseline and capability profile**. Items 4-21 keep their statuses above. Item 2 native verification must be completed when the Editor is available; no item is silently checked off because source files exist.
