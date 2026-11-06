# GripRehab documentation index

Start with **[STATUS.md](STATUS.md)** (what is done, with evidence) and the **[Arabic guide](GUIDE_AR.md)**. Results are labelled SYNTHETIC, by-eye, measured or physical; nothing here is a clinical validation.

## Current: use these
| Topic | Documents |
|---|---|
| Status and audit | [STATUS](STATUS.md), [RELEASE_AUDIT](RELEASE_AUDIT.md) (plan vs build, v1.0.7), [STAGE16_AUDIT](STAGE16_AUDIT.md) (as-built diagrams, Gantt, gaps), [REQUIREMENTS_CHECKLIST](REQUIREMENTS_CHECKLIST.md), [DECISIONS](DECISIONS.md), [BLOCKERS](BLOCKERS.md) |
| Architecture | [architecture/index.html](architecture/index.html) (layers, pipelines, "where does X live"), [architecture/graph.html](architecture/graph.html) (dependency graph) |
| Camera hand and controls | [HAND_CONTROL_REPORT](HAND_CONTROL_REPORT.md) (measurements, fixes F1–F6), [HAND_CONTROLS](HAND_CONTROLS.md), [HAND_POINTER](HAND_POINTER.md), [HANDS_AND_MEDIAPIPE_AR](HANDS_AND_MEDIAPIPE_AR.md), [VOICE_COMMANDS](VOICE_COMMANDS.md) |
| Interaction and physics | [HAND_OBJECT_INTERACTION](HAND_OBJECT_INTERACTION.md), [CORE_INTERACTION_AR](CORE_INTERACTION_AR.md) |
| UI, audio, environment | [UI_DESIGN_SYSTEM](UI_DESIGN_SYSTEM.md) ([AR](UI_DESIGN_SYSTEM_AR.md)), [UI_UX_AR](UI_UX_AR.md), [AUDIO_DESIGN](AUDIO_DESIGN.md), [ENVIRONMENT](ENVIRONMENT.md), [APPEARANCE_AR](APPEARANCE_AR.md), [ASSET_SELECTION](ASSET_SELECTION.md) |
| Hardware | [BLE_PROTOCOL](BLE_PROTOCOL.md), [BLE_INTEGRATION](BLE_INTEGRATION.md), [PHYSICAL_TEST_PROTOCOL](PHYSICAL_TEST_PROTOCOL.md) (all physical checks OPEN) |
| Quality | [QA](QA.md), [PERFORMANCE](PERFORMANCE.md) |
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
- `history/unused-candidates.md`: dead code and unused assets, removed 2026-11-06 with your OK.
- `fault-probe.json`: release fault probe results (SYNTHETIC).
- `screenshots/`: UI gallery and player captures.

## History (superseded; kept for the record)
[PROGRESS](PROGRESS.md), [TASKS](TASKS.md), [SOFTWARE_TASKS_23](SOFTWARE_TASKS_23.md), [SOFTWARE_GRADUATION_PLAN](SOFTWARE_GRADUATION_PLAN.md), [REQUIREMENTS](REQUIREMENTS.md), [AUDIT](AUDIT.md), [AUDIT_LEDGER](AUDIT_LEDGER.md), [PROJECT_AUDIT](PROJECT_AUDIT.md), [CLOSEOUT_2026_10_06](CLOSEOUT_2026_10_06.md), [REPAIR_REPORT](REPAIR_REPORT.md), [UI_COMPLETION_CHECKLIST](UI_COMPLETION_CHECKLIST.md), [UI_RESEARCH](UI_RESEARCH.md), [HANDMATE_UI_DESIGN](HANDMATE_UI_DESIGN.md), [SCENE_ORGANIZATION](SCENE_ORGANIZATION.md), [CAMERA_DIRECTION_FIX](CAMERA_DIRECTION_FIX.md), [PHYSICS_HAND](PHYSICS_HAND.md) (retired), [REFERENCE_HANDS_INTEGRATION](REFERENCE_HANDS_INTEGRATION.md), item write-ups [POINTER_ITEM_2](POINTER_ITEM_2.md), [JOURNEY_ITEM_2](JOURNEY_ITEM_2.md), [KEYBOARD_ITEM_3](KEYBOARD_ITEM_3.md), [GRASP_ITEM_4](GRASP_ITEM_4.md), [DEFORMATION_ITEM_5](DEFORMATION_ITEM_5.md), [HAND_RIG_ITEM_6](HAND_RIG_ITEM_6.md).
