# Requirements checklist: Stages 8–17 (added 2026-10-25)

One row per requirement from your 2026-10-25 list. The plan is [Stages 8–17 in the upgrade plan](superpowers/plans/2026-10-23-ultimate-upgrade-plan.md).

**Status values:**
- Not Started
- In Progress
- Implemented (code exists)
- Tested (verified against its acceptance criteria)
- Blocked (with the reason)

**Evidence** links point to `Docs/evidence/...`. Physical and human checks are recorded separately from simulations, compilation and design checks.

## 1. Object deformation system → Stage 9 (WBS 3.3)

| ID | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| D-01 | Audit the existing deformation; rebuild or refactor where necessary | Not Started | | |
| D-02 | Realistic, physically plausible, visually convincing deformation | Not Started | | Measured quality only; no unvalidated "AAA" claim |
| D-03 | Elastic, plastic, rigid and breakable behaviour types | Not Started | | |
| D-04 | Configurable stiffness, elasticity, plasticity, damping, fracture thresholds, recovery speed | Not Started | | |
| D-05 | Deformation responds continuously to interaction forces (no canned animation or scaling) | Not Started | | |
| D-06 | Preserve volume, surface continuity, collision stability and material behaviour | Not Started | | |
| D-07 | Integrate with hand tracking, grip, force sensing and interaction | Not Started | | Depends on Stage 8 |
| D-08 | Real-time performance optimisation | Not Started | | Stage 7 budget |
| D-09 | Reusable components and material presets | Not Started | | |
| D-10 | Test deformation, release, recovery, repeated squeezing and fracture across shapes and materials | Not Started | | |

## 2. Hand–object interaction and contact → Stage 8 (WBS 3.4.1, 3.3)

| ID | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| C-01 | Audit and fix all existing interaction and collision problems | Tested | [stage8](evidence/stage8-2026-10-25/) | Baseline: 19–70 mm finger/palm penetration on every station object. S4-03 fixed for held objects: pads touch, 7.7–12.4 mm dent at 18 N |
| C-02 | Hand never passes through an object; object never penetrates the hand | Tested | [contact-probe.json](evidence/stage8-2026-10-25/contact-probe.json), [contact-probe-grips.json](evidence/stage8-2026-10-25/contact-probe-grips.json) | Station objects ≤ 5.7 mm (sponge palm) while holding, 18 chart grips ≤ 1.0 mm; ≤ 6 mm at ~1.1 m/s. Limit at ~2.8 m/s shake: see C-10 |
| C-03 | No hold gap (holding without touching) | Tested | [contact-probe.json](evidence/stage8-2026-10-25/contact-probe.json), [contact-probe-grips.json](evidence/stage8-2026-10-25/contact-probe-grips.json) | Mean pad gap 0–1.4 mm on station objects; chart grips 0–5.2 mm (pen, coin, USB and wrap grips leave some pads 2–7 mm short) |
| C-04 | Plausible contact between the outer hand surface and the outer object surface | Implemented | [stage8](evidence/stage8-2026-10-25/)/play/ | 9 mm finger capsules against the real colliders plus the visible dent; screenshots judged by eye by me, your by-eye check pending |
| C-05 | Robust collision detection and contact resolution (stable points, normals, response) | Tested | [contact-probe.json](evidence/stage8-2026-10-25/contact-probe.json) | Exact sphere/box/capsule signed distance, PhysX for mesh colliders; proximal-to-distal curl limit |
| C-06 | No floating away, sinking into the palm or wrong positioning while gripping or moving | Tested | [contact-probe.json](evidence/stage8-2026-10-25/contact-probe.json), [contact-probe-grips.json](evidence/stage8-2026-10-25/contact-probe-grips.json) | No drops, no furniture overlap > 2 mm (book: 3 frames while lifting); palm overlap ≤ 5.7 mm |
| C-07 | Stable grasp across object sizes, shapes, orientations and grip types | Tested | [contact-probe-grips.json](evidence/stage8-2026-10-25/contact-probe-grips.json) | 18 grip types from your chart on 18 code-built objects plus the 6 station objects; all held |
| C-08 | Hand deforms an object only through contact forces consistent with its material | Tested | Apartment acceptance (smoke/replay) | Only touching probes resolve force; non-touching probes resolve exactly 0 N |
| C-09 | No tunnelling, jitter, unstable collisions or sudden snapping | Tested | [contact-probe.json](evidence/stage8-2026-10-25/contact-probe.json), offline grasp checks | Hold jitter ≤ 0.1 mm after a 1 s settle; follow capped at 3.5 m/s; continuous speculative collision |
| C-10 | Stable during fast hand motion and large deformation | In Progress | [contact-probe.json](evidence/stage8-2026-10-25/contact-probe.json) | Large dents (12 mm) stay contact-correct. Fast shake (~2.8 m/s) still 13–45 mm transient overlap (S8-05) |
| C-11 | Mouse, keyboard, hand pointer and other working inputs preserved | Tested | smoke, replay, journey, UI, gallery suites | Inputs unchanged; the chart pose and presentation apply to keyboard/demo input only, a tracked camera hand keeps its pose |
| C-12 | Tested from multiple angles with different geometries | Tested | [contact-probe.json](evidence/stage8-2026-10-25/contact-probe.json) | Upright, turned 90° and tilted 45° for all six station objects |
| C-13 | Grip chart (added 2026-10-26): the 18 grips of your picture, each with its object, playable in the game | Tested | [contact-probe-grips.json](evidence/stage8-2026-10-25/contact-probe-grips.json), [stage8](evidence/stage8-2026-10-25/)/play/ | Grip Lab station; SYNTHETIC numbers in AUDIT_LEDGER §10a; your by-eye review pending |

## 3. Six multi-step environmental activities → Stage 10 (WBS 3.1, 3.4.1)

| ID | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| A-01 | Living room: organise a bookshelf | Not Started | | |
| A-02 | Living room: set up a coffee table to a target layout | Not Started | | |
| A-03 | Living room: pack and organise a storage box | Not Started | | |
| A-04 | Kitchen: prepare a simple meal | Not Started | | |
| A-05 | Kitchen: cut and prepare ingredients | Not Started | | Plane-cut approximation, labelled as such |
| A-06 | Kitchen: prepare a meal and serve it | Not Started | | |
| A-07 | Add every missing object, tool, prop and environment element | Not Started | | Props built in code (D7) |
| A-08 | Instructions, objectives, progress, success conditions and completion feedback | Not Started | | |
| A-09 | Realistic manipulation, placement constraints and physical interactions | Not Started | | |
| A-10 | Difficulty levels and optional precision requirements | Not Started | | |
| A-11 | Scoring on accuracy, completion, handling and task performance | Not Started | | |
| A-12 | Integrated into the environment and UI without breaking current features | Not Started | | |
| A-13 | Replayable and completable with every supported input | Not Started | | |

## 4. Optional eye tracking → Stage 14 (WBS 3.2)

| ID | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| E-01 | Investigate engine, hardware, camera access and eye-tracking APIs first | Not Started | | No eye-tracking hardware present (D8) |
| E-02 | Eye tracking as an additional input for UI and gameplay | Not Started | | |
| E-03 | UI: look to move focus and select by dwell or a supported gesture | Not Started | | |
| E-04 | Clear visual feedback for the viewed or focused element | Not Started | | |
| E-05 | Accessible confirm and cancel to reduce accidental selections | Not Started | | |
| E-06 | In-game: gaze-driven camera left/right and up/down (configurable) | Not Started | | |
| E-07 | Look at an object to highlight, inspect or select it | Not Started | | |
| E-08 | Configurable sensitivity, calibration, dead zones, smoothing and thresholds | Not Started | | |
| E-09 | Prevent involuntary movement, jitter and unwanted selection | Not Started | | |
| E-10 | Recalibration and fallback when gaze data is unavailable or unreliable | Not Started | | |
| E-11 | Entirely optional through a settings toggle | Not Started | | Webcam estimate, accuracy stated |

## 5. Voice commands and speech-to-text → Stage 13 (WBS 3.4)

| ID | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| V-01 | Voice as an additional input for UI and gameplay | Not Started | | Windows built-in offline (D9) |
| V-02 | UI commands: navigate, select, settings, screens, confirm, cancel, launch activities | Not Started | | |
| V-03 | In-game commands: move, stop, look at, select, release, open inventory and context actions | Not Started | | |
| V-04 | Restart, pause/resume, instructions and main menu commands | Not Started | | |
| V-05 | Speech-to-text for text fields (names, profile) | Not Started | | Passwords excluded |
| V-06 | Letter-by-letter name spelling into editable text | Not Started | | |
| V-07 | Show recognised text before submitting; allow correction | Not Started | | |
| V-08 | Feedback when a command is recognised, rejected or unavailable | Not Started | | |
| V-09 | Handle ambiguity, noise, recognition failure and commands invalid in the current state | Not Started | | |
| V-10 | Configurable activation, push-to-talk and microphone permission handling | Not Started | | |
| V-11 | Mouse, keyboard, hand pointer and existing controls fully functional | Not Started | | |

## 6. Audio and sound design → Stage 12 (WBS 3.1, 3.4)

| ID | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| S-01 | Audit missing, low-quality, repetitive, badly timed or wrongly triggered sounds | Not Started | | |
| S-02 | Upgrade ambience, UI, grab, release, collision, deformation, fracture and completion audio | Not Started | | |
| S-03 | Material-dependent sounds (wood, metal, glass, plastic, fabric, soft) | Not Started | | |
| S-04 | Variation so repeats do not sound identical | Not Started | | |
| S-05 | Synchronised with contact force, deformation intensity and fracture events | Not Started | | |
| S-06 | Spatial audio and distance attenuation | Not Started | | |
| S-07 | Volume balance, clarity, mixing and menu–gameplay transitions | Not Started | | AudioMixer asset |
| S-08 | Separate master, effects, ambience and voice-assistance volumes | Not Started | | |
| S-09 | Nothing loud, distracting, repetitive or misleading | Not Started | | Human listening recorded separately |
| S-10 | Licensed or original audio, new dependencies documented | Not Started | | |

## 7. UI modernization → Stage 11 (WBS 3.4, 5.1, 5.2)

| ID | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| U-01 | Audit and redesign UI, menus, buttons, type, icons, colour, spacing, layouts and states | Not Started | | |
| U-02 | Research 2024–2026 AAA and accessibility-focused UI/UX | Not Started | | Sources cited |
| U-03 | Analyse hierarchy, restrained effects, readable type, spacing, focus, motion and feedback | Not Started | | |
| U-04 | Use as inspiration only; no proprietary assets or exact interfaces | Not Started | | |
| U-05 | Consistent design system (colour, type, buttons, panels, icons, transitions, states) | Not Started | | |
| U-06 | Distinct hover, focus, pressed, disabled, selected and error states | Not Started | | |
| U-07 | Readability, contrast, text scaling, navigation clarity and hierarchy | Not Started | | |
| U-08 | Main menu, pause, settings, activity selection, progress and onboarding redesigned | Not Started | | |
| U-09 | Works with mouse, keyboard, hand pointer, gaze and voice | Not Started | | |
| U-10 | Indicators for input mode, gaze focus, voice recognition and assistant status | Not Started | | |
| U-11 | Clean menus without clutter, heavy gradients or distracting animation | Not Started | | |
| U-12 | Tested at the target resolutions and aspect ratios | Not Started | | |

## 8. Optional voice assistant → Stage 15 (WBS 3.4)

| ID | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| G-01 | Assistant can be enabled or disabled at any time in settings | Not Started | | |
| G-02 | First-time onboarding (interface, controls, activities, mechanics) | Not Started | | |
| G-03 | Explains buttons, menus, settings, objectives and how to complete tasks | Not Started | | |
| G-04 | Contextual guidance from environment, activity, progress and interaction state | Not Started | | |
| G-05 | Answers spoken questions ("What should I do next?", "How do I grab this object?", and others) | Not Started | | |
| G-06 | Step-by-step guidance without taking control | Not Started | | |
| G-07 | Voice navigation with explicit confirmation | Not Started | | |
| G-08 | Clear, configurable voice output (volume and speed) | Not Started | | Windows TTS (D9) |
| G-09 | No needless interruption; repeat, pause, dismiss and disable | Not Started | | |
| G-10 | State-aware; never claims an action succeeded when it did not | Not Started | | |
| G-11 | Investigate offline and online speech/language options (latency, cost, privacy, platform) | Not Started | | Offline by default |
| G-12 | Handle microphone permission, network failure, unavailable service and errors | Not Started | | |
| G-13 | All controls preserved; fully playable with the assistant off | Not Started | | |

## 9. Pipeline, block diagram and Gantt audit → Stage 16 (WBS 1.1, 6.3)

| ID | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| P-01 | Review pipeline, architecture, block diagram, Gantt, requirements, status and source | Not Started | | |
| P-02 | Compare design with implementation; find gaps and inconsistencies | Not Started | | |
| P-03 | Verify end-to-end data flow (tracking → input → grasp → force → physics → deformation → collision → activity → feedback → logging) | Not Started | | |
| P-04 | Check the architecture supports eye, voice, assistant, audio, UI and activity systems | Not Started | | |
| P-05 | Updated block diagram (as-built) | Not Started | | |
| P-06 | Updated pipeline (implementation, integration, testing, optimisation, validation) | Not Started | | |
| P-07 | Updated Gantt (tasks, dependencies, milestones, priorities, durations, critical path) | Not Started | | |
| P-08 | Review requirements and acceptance criteria for missing deliverables | Not Started | | |
| P-09 | Audit stability, errors, configuration, assets, performance and hardware compatibility | Not Started | | |
| P-10 | Full start-up to shutdown test | Not Started | | |
| P-11 | New features coexist; no regressions | Not Started | | |
| P-12 | Prioritised gap analysis (blockers, important, optional, future) | Not Started | | |
| P-13 | Architecture docs, block diagram, Gantt, test checklist and final completion report | Not Started | | Never "100 %" on code alone |

## 10. Next-stage research proposal → Stage 17 (WBS 1.1)

| ID | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| R-01 | Brainstorm and research improvements after the Stage 16 audit | Not Started | | |
| R-02 | Gameplay, realism, UI/UX, models, vision, AI, assessment, data, accessibility, performance, maintainability | Not Started | | |
| R-03 | Digital twin, synchronisation, simulation fidelity, materials, sensor-to-virtual mapping | Not Started | | |
| R-04 | AI-assisted adaptation, personalised difficulty, feedback and progress tracking (where justified) | Not Started | | |
| R-05 | Hand tracking, grasp classification, force estimation, deformation, collision and latency | Not Started | | |
| R-06 | Rehabilitation metrics (time, grip force, steadiness, ROM where measurable, smoothness, repeatability) | Not Started | | |
| R-07 | Datasets, models, sensors and libraries with measurable benefit | Not Started | | |
| R-08 | Recent papers, official docs and credible examples, with links | Not Started | | |
| R-09 | Evaluate impact, complexity, cost, hardware, risk, scientific validity and compatibility | Not Started | | |
| R-10 | Separate essential fixes, optional enhancements and ambitious research | Not Started | | |
| R-11 | Ranked list with description, justification, benefit, effort, dependencies and order | Not Started | | |
| R-12 | Mark what is possible now versus what needs hardware, data, funding or architecture changes | Not Started | | |
| R-13 | Present for review; implement nothing automatically | Not Started | | |
