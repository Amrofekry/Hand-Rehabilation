# GripRehab documentation index

Start with **[STATUS.md](STATUS.md)** (what is done, with evidence) and the **[Arabic guide](GUIDE_AR.md)**. Results are labelled SYNTHETIC, by-eye, measured or physical; nothing here is a clinical validation.

## Current: use these
| Topic | Documents |
|---|---|
| Status and audit | [REVIEW_2026-11-23](REVIEW_2026-11-23.md) (latest review, size, task list), [STATUS](STATUS.md), [RELEASE_AUDIT](RELEASE_AUDIT.md) (plan vs build, v1.0.8), [STAGE16_AUDIT](STAGE16_AUDIT.md) (as-built diagrams, Gantt, gaps), [REQUIREMENTS_CHECKLIST](REQUIREMENTS_CHECKLIST.md), [DECISIONS](DECISIONS.md), [BLOCKERS](BLOCKERS.md) |
| Architecture | [architecture/index.html](architecture/index.html) (layers, pipelines, "where does X live"), [architecture/graph.html](architecture/graph.html) (dependency graph) |
| Camera hand and controls | [HAND_CONTROL_REPORT](HAND_CONTROL_REPORT.md) (measurements, fixes F1–F6), [HAND_CONTROLS](HAND_CONTROLS.md) (gestures, movement assist, hand depth), [PHONE_CAMERA](PHONE_CAMERA.md) (phone via Phone Link or USB, measured), [HAND_POINTER](HAND_POINTER.md), [HANDS_AND_MEDIAPIPE_AR](HANDS_AND_MEDIAPIPE_AR.md), [VOICE_COMMANDS](VOICE_COMMANDS.md) |
| Interaction and physics | [HAND_OBJECT_INTERACTION](HAND_OBJECT_INTERACTION.md), [CORE_INTERACTION_AR](CORE_INTERACTION_AR.md) |
| UI, audio, environment | [UI_DESIGN_SYSTEM](UI_DESIGN_SYSTEM.md) ([AR](UI_DESIGN_SYSTEM_AR.md)), [UI_UX_AR](UI_UX_AR.md), [AUDIO_DESIGN](AUDIO_DESIGN.md), [ENVIRONMENT](ENVIRONMENT.md), [APPEARANCE_AR](APPEARANCE_AR.md), [ASSET_SELECTION](ASSET_SELECTION.md) |
| Hardware | [BLE_PROTOCOL](BLE_PROTOCOL.md), [BLE_INTEGRATION](BLE_INTEGRATION.md), [PHYSICAL_TEST_PROTOCOL](PHYSICAL_TEST_PROTOCOL.md) (all physical checks OPEN) |
| Quality | [full-regression.json](full-regression.json) (the latest full regression, written by `Tools/run_full_regression.py`), [AUDIT_LEDGER](AUDIT_LEDGER.md) (findings and how each was closed), [QA](QA.md) (dated QA log), [PERFORMANCE](PERFORMANCE.md) |
| Next steps | [OPTIONAL_PLAN](OPTIONAL_PLAN.md), [STAGE17_RESEARCH](STAGE17_RESEARCH.md) |
| Delivery | [delivery/GripRehab_Final.pptx](delivery/GripRehab_Final.pptx), [GUIDE_AR](GUIDE_AR.md) ([PDF](GUIDE_AR.pdf), [Word](GUIDE_AR.docx)), [FINAL_REPORT](FINAL_REPORT.md) |
| Licences and credits | [CREDITS](CREDITS.md), [AI_THIRD_PARTY](AI_THIRD_PARTY.md) |

## Source plans
`Source/`: the original pipeline (`Hand_Rehab_Pipeline.pdf`), block diagram (`Blockdiagram.pdf`), Gantt (`ganttchart.html`) and project plan.

## Evidence and results
- `evidence/<stage>/`: per-stage evidence. `evidence/final-2026-10-29/` holds the final-release benchmark, latency, camera activity run, protected-file reviews and the Stage 15A pictures.
- `evidence/physical/`: physical test templates (empty until run on hardware).
- `*.json` here: the latest output of each test runner (`Tools/run_*.py` writes them).
- `history/results/`: outputs of retired runners, kept for the record.
- `history/retired-tools/`: tools that no longer apply, with the reason.
- `history/unused-candidates.md`: dead code and unused assets, removed 2026-11-06 with your OK.
- `fault-probe.json`: release fault probe results (SYNTHETIC).
- `screenshots/`: UI gallery and player captures.

## History (superseded; in `history/`, kept for the record)
[PROGRESS](history/PROGRESS.md), [TASKS](history/TASKS.md), [SOFTWARE_TASKS_23](history/SOFTWARE_TASKS_23.md), [SOFTWARE_GRADUATION_PLAN](history/SOFTWARE_GRADUATION_PLAN.md), [REQUIREMENTS](history/REQUIREMENTS.md), [AUDIT](history/AUDIT.md), [PROJECT_AUDIT](history/PROJECT_AUDIT.md), [CLOSEOUT_2026_10_06](history/CLOSEOUT_2026_10_06.md), [REPAIR_REPORT](history/REPAIR_REPORT.md), [UI_COMPLETION_CHECKLIST](history/UI_COMPLETION_CHECKLIST.md), [UI_RESEARCH](history/UI_RESEARCH.md), [HANDMATE_UI_DESIGN](history/HANDMATE_UI_DESIGN.md), [SCENE_ORGANIZATION](history/SCENE_ORGANIZATION.md), [CAMERA_DIRECTION_FIX](history/CAMERA_DIRECTION_FIX.md), [PHYSICS_HAND](history/PHYSICS_HAND.md) (retired), [REFERENCE_HANDS_INTEGRATION](history/REFERENCE_HANDS_INTEGRATION.md), item write-ups [POINTER_ITEM_2](history/POINTER_ITEM_2.md), [JOURNEY_ITEM_2](history/JOURNEY_ITEM_2.md), [KEYBOARD_ITEM_3](history/KEYBOARD_ITEM_3.md), [GRASP_ITEM_4](history/GRASP_ITEM_4.md), [DEFORMATION_ITEM_5](history/DEFORMATION_ITEM_5.md), [HAND_RIG_ITEM_6](history/HAND_RIG_ITEM_6.md).
