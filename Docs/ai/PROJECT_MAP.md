Last scan: 2026-11-06 (v1.0.7)

## Pipeline and packages
- URP 14.0.10, uGUI, TextMesh Pro 3.0.6, Sentis 2.0.0, Burst 1.8.17, Collections 2.2.1, Mathematics 1.2.6, XR Management 4.6.1. Input is the legacy Input Manager (the Input System package is not installed).
- Test Framework 1.1.33 and Performance 3.0.2 (local tgz under `Tools/AI/`).
- MCP for Unity 10.0.0 (git, pinned).

## Folders (Assets)
| Path | What |
|---|---|
| `Rehab/3.1-environment` | Apartment environment, presentation |
| `Rehab/3.2-vision` | Camera hand: UDP receiver, filters, gestures, camera worker launcher |
| `Rehab/3.3-deformation` | `GripDeformation.cs`; the Stage 9 solver sits beside the apartment in 3.4.1 |
| `Rehab/3.4-game-modules` | App shell, profiles, voice, diagnostics, probes (incl. the fault probe) |
| `Rehab/3.4.1-force-object-interaction` | Apartment, grasp, contact, deformation, the six activities (`Apartment/`) |
| `Rehab/3.4.2-cognitive-performance` | Memory and assembly tasks |
| `Rehab/3.4.3-visuomotor-coordination` | Maze and tracing tasks |
| `Rehab/6.1-bluetooth-integration` | BLE force state, receiver, device health panel |
| `Product/` | Assembly `GripRehab.Product` (Unity-free domain): accounts, sessions, baseline, fatigue policy, export |
| `ProductIntegration/` | Binds the product layer to the game: research portal, sessions, sensing, Sentis runtime |
| `UI/`, `UIBridge/` | Assembly `GripRehab.UI`: theme, portal view, charts, gallery scene; bridge to dwell settings |
| `_Project/Core` | Assembly `GripRehab.Core`: input contracts, session, adaptive policy |
| `Editor/` | Build, authoring and verification tools (`-executeMethod`) |
| `com.gleechi.unity.virtualgrasp`, `ThirdParty/` | Vendor code: do not modify |
| `SceneBackups/` | Pre-split scene backup, used by the scene-composition tests |

## WBS → code (decision ADR-WBS-1)
Game code lives in `Assets/Rehab/<WBS>`. The four assembly layers (`Product`, `ProductIntegration`, `UI`, `_Project/Core`) span several WBS IDs, so they stay as layers; this table is the WBS index for them.

| WBS | Where |
|---|---|
| 2.3 / 2.4 / 6.1 | `firmware/2.3-*`, `firmware/2.4-*`, `Tools/Phase2Bridge/`, `Rehab/6.1-bluetooth-integration/` |
| 3.1 | `Rehab/3.1-environment/`, `Scenes/GripRehab.unity` |
| 3.2 | `Rehab/3.2-vision/`, `Tools/Vision/mediapipe_bridge.py`, `ProductIntegration/ResearchAuxiliaryReceiver.cs`, `ResearchSensing.cs` (opt-in face/rPPG) |
| 3.3 | `Rehab/3.3-deformation/`, deformation files in `Rehab/3.4.1-force-object-interaction/Apartment/` |
| 3.4 | `Rehab/3.4-game-modules/`, `_Project/Core/RehabSession.cs`, `InputContracts.cs`, `UI/`, `UIBridge/` |
| 3.4.1 | `Rehab/3.4.1-force-object-interaction/`, `Product/ForceRoundSession.cs`, `ExerciseTuning.cs`, `ProductIntegration/ResearchSessionRuntime.cs` |
| 3.4.2 / 3.4.3 | `Rehab/3.4.2-cognitive-performance/`, `Rehab/3.4.3-visuomotor-coordination/` |
| 4.1 | `Product/StagedCapabilityTrial.cs`, `CapabilityTrial.cs`, `ai-engine/4.1-*` |
| 4.2 | `Product/TemporalForceWindow.cs`, `ResearchExport.cs`, `ai-engine/4.2-*` |
| 4.3 / 4.4 | `ai-engine/4.3-*`, `ai-engine/4.4-*`, `ProductIntegration/ResearchAiRuntime.cs`, `Resources/ResearchAi/` |
| 4.5 | `Product/FatigueSafetyPolicy.cs`, `AdaptiveDifficulty.cs`, `ResearchGuidance.cs`, `SafetyMessages.cs`, `_Project/Core/ResearchAdaptivePolicy.cs` |
| 5.1 / 5.2 | `Product/ProductRepository.cs`, `ProductModels.cs`, `ProgressSummary.cs`, `PasswordKdf.cs`, `ProtectedFileRecovery.cs`, `ProductIntegration/ResearchPortalHost.cs`, `ResearchPortalPages.cs`, `WindowsProductStore.cs`, `UI/ResearchPortalView.cs`, `ResearchChart.cs` |
| 6.2 | `Tools/run_*.py`, probes in `Rehab/`, `ProductIntegration/*Acceptance.cs`, `tests/6.2-*` |
| 6.3 | `Docs/`, `Tools/release/` |

## Asmdefs
- `GripRehab.Product`, `GripRehab.UI`, `GripRehab.Core`.
- Gleechi VirtualGrasp (Runtime, Editor, Build, Samples).
- Most game code is in Assembly-CSharp (no asmdef under `Assets/Rehab`).

## Scenes (build)
- `Assets/Scenes/GripRehab.unity`: the main scene, holding the app shell and apartment.
- `Assets/UI/GripUiGallery.unity`: UI gallery.

## Script counts (.cs, tracked, 2026-11-06)
| Folder | Count |
|---|---|
| 3.4.1-force-object-interaction | 44 |
| Editor | 41 |
| 3.4-game-modules / Product | 18 / 18 |
| UI + UIBridge | 14 |
| 3.2-vision / ProductIntegration | 10 / 10 |
| 3.1-environment | 7 |
| 6.1-bluetooth | 5 |
| 3.4.2 / _Project/Core | 3 / 3 |
| 3.3 / 3.4.3 | 1 / 1 |
| VirtualGrasp (vendor) | 43 |

## Outside Assets
- `Tools/`: offline harness, player-suite runners, Python tests, vision worker.
- `ai-engine/`: ML model and its pytest suite.
- `Docs/`: STATUS, AUDIT_LEDGER, REQUIREMENTS_CHECKLIST, plans, evidence.

## Entry points
- `RehabAppShell`: home, modules, research identity.
- `ApartmentExperience`: grab, hold and release.
- `ApartmentChoices`: options panel.
- `ActivityRunner`: Stage 10 activities.
- `RehabVerification.BuildWindows`: build.
- Probes are opt-in by command-line flag (`-apartment-activity-probe` and others).
