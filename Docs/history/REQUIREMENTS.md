# GripRehab requirements and acceptance trace

Status: working research specification, 2026-09-28. The canonical WBS IDs and dates remain in `Source/ganttchart.html` and the original `E:\GP\codex\PROJECT_PLAN.md`. This document adds the explicitly requested GP2 product scope; it does not revise the historical schedule. `Source/Hand_Rehab_Pipeline.pdf` and `Source/Blockdiagram.pdf` are architecture inputs, not instructions to operate external services or claim clinical validation.

Each row needs evidence from the **current** checkout before it can be marked complete. `S` means a software test, `R` a rendered/manual UI review, `H` a physical bench test, `C` a consented human/clinical study. An S pass never substitutes for H or C.

| ID | WBS | Requirement / acceptance evidence | Needed evidence |
| --- | --- | --- | --- |
| R01 | 1.1 | Diagram and versioned interface contracts connect Nano, BLE/USB, Unity, vision, policy and dashboards; units, timestamps and failure states are explicit. | S, review |
| R02 | 1.2 | Component, model and library choices have provenance, licenses, alternatives and a bill of materials. | review |
| R03 | 1.3 | The 22 source weeks, blocked/spare weeks and event markers remain traceable without inventing deadlines. | review |
| R04 | 2.1 | Editable interchangeable Sphere/Cylinder/Pencil CAD fits one protected core; physical fit and load path have measured tolerances. | S, H |
| R05 | 2.2 | Nano 33 IoT, HX711, 100 kg cell and power path have schematic, safe assembly and measured rails; purchasing evidence is user-managed. | H |
| R06 | 2.3 | Tare, calibration persistence and Newton conversion have unit and multi-point physical verification, including drift and unloading. | S, H |
| R07 | 2.4 | BLE v1 stays compatible; negotiated v2 adds integrity, sequence/calibration identity and confirmed commands without silently changing v1. | S, H |
| R08 | 3.1 | Living Room/Kitchen scene opens, remains editable and builds on pinned Unity; current Module 1 remains playable. | S, R |
| R09 | 3.2 | Each handed 21-point frame has capture/receive time, confidence and validity; loss/reacquisition and coordinate mapping are tested. Geometric gestures are the fallback; YOLO requires a proven compatible model. | S, R, C |
| R10 | 3.3 | Object-specific deformation, release and fracture are bounded and measured. The current mesh approximation is labelled as such; Obi requires a compatible licensed package. | S, R |
| R11 | 3.4 | Bootstrap, profile/session lifecycle, common module/metric events and source contracts run without duplicated listeners or regression of Module 1. | S |
| R12 | 3.4.1 | Module 1 records force production, hold/endurance and gesture stability with source, units and incomplete-session state; stale input stops progress. | S, R, H for hardware path |
| R13 | 3.4.2 | Sequence Memory and Spatial Assembly are playable with repeatable reaction, error and trajectory metrics, replay and keyboard accessibility. | S, R |
| R14 | 3.4.3 | Maze and Tracing are playable with collision/accuracy, reaction and trajectory metrics, replay and keyboard accessibility. | S, R |
| R15 | 4.1 | Consented baseline captures MVC, endurance, ROM and stability with validity and session/shape identifiers; missing values remain missing. | S, C for validity |
| R16 | 4.2 | Versioned local feature windows align force, motion and optional facial/rPPG inputs; no raw camera image is retained; synthetic records are marked. | S, privacy review |
| R17 | 4.3 | Train/validation leakage is prevented by participant split; reproducible temporal GRU/LSTM baseline, metrics, model card and fallback exist. No diagnostic claim without data. | S, C for clinical validity |
| R18 | 4.4 | ONNX export parity is tested against training runtime, and Unity inference is verified with a pinned compatible Sentis package; otherwise statistical fallback is explicit. | S |
| R19 | 4.5 | Bounded adaptation requires valid stable input and cooldown. One independent fatigue sign offers Resume/Quit; two latch safety stop. Missing/stale/model failure prevents escalation. | S, C for safety validity |
| R20 | 5.1 | Patient flow covers login, consent, baseline, available modules, live units/source, saved results and clear incomplete/synthetic states. | S, R |
| R21 | 5.2 | Doctor D1–D5 is role gated and shows attributable history, statistics and clearly non-diagnostic recommendations; CSV/PDF export is explicit. | S, R, privacy review |
| R22 | 6.1 | `IForceSource` contract accepts USB/BLE/simulator; sequence, shape, calibration, source and 500 ms freshness are enforced without silent hardware→keyboard fallback. | S, H |
| R23 | 6.2 | Fault-injected closed-loop tests cover camera/force loss, corrupt/duplicate/old packets, storage failure, profile migration, restart, languages and all journeys. Profile FPS/GC and record actual hardware. | S, R; H/C separately |
| R24 | 6.3 | Windows release, reproducible test commands, user/developer guides, asset credits, requirement-to-evidence matrix and unresolved limitations are packaged. | S, R, review |

## Cross-cutting contracts

- A force sample includes source, measurement/receive timestamps, sequence, shape, calibration revision, Newton value, validity and `Synthetic`. The 500 ms budget includes all transport/queue age. Hardware failure never selects keyboard force automatically.
- A hand frame includes 21 landmarks, side, confidence, capture/receive timestamps and validity. Loss of tracking releases or freezes grasp safely and stops scored progress. RGB monocular depth and closing fingers are not measured force.
- Identity is local Windows only. Existing schema-1 `profiles.json` is migrated with an authenticated, recoverable encrypted copy and read-back before deleting plaintext. Passwords use a per-user salt and PBKDF2; test fixtures contain fabricated identities only.
- Face/rPPG processing requires explicit opt-in and remains local; no raw frames are saved. Consent may be withdrawn. The UI identifies unvalidated model and sensor estimates.
- Therapeutic thresholds are versioned demo research settings, not clinician prescriptions. Physical hardware performance, participant-model validity and clinical approval are independent unsatisfied evidence categories until actually tested.

## Gate rule

For M0–M10, a gate closes only after applicable tests, Unity compilation and Windows player run are recorded in `PROGRESS.md`. If an external dependency prevents a gate, record the exact block in `BLOCKERS.md` and continue only independent work. A fixture or synthetic pass cannot close physical/clinical acceptance.
