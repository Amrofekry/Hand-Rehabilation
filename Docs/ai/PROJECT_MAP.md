Last scan: 0f6e5e2 2026-10-27

## Pipeline and packages
- URP 14.0.10, uGUI, TextMesh Pro, Input System actions, Burst 1.8.17, Collections 2.2.1.
- Test Framework 1.1.33 (local tgz under `Tools/AI/`).
- MCP for Unity 10.0.0 (git, pinned).

## Folders (Assets)
| Path | What |
|---|---|
| `Rehab/3.1-environment` | Apartment environment |
| `Rehab/3.2-vision` | Camera hand-tracking receiver |
| `Rehab/3.4-game-modules` | Exercise modules, tuning, research flow |
| `Rehab/3.4.1-force-object-interaction` | Apartment, grasp, contact, deformation, Stage 10 activities (`Apartment/`) |
| `Rehab/3.4.2-cognitive-performance` | Memory and assembly tasks |
| `Rehab/6.1-bluetooth-integration` | BLE force sensor |
| `Product/` | Accounts, sessions, repository, PDF/CSV, dashboards domain |
| `UI/`, `UIBridge/` | Theme, research portal, gallery scene |
| `_Project/Core` | Core asmdef |
| `com.gleechi.unity.virtualgrasp`, `ThirdParty/` | Vendor code: do not modify |
| `_Recovery/`, `SceneBackups/` | Old scene backups |

## Asmdefs
- `GripRehab.Product`, `GripRehab.UI`, `GripRehab.Core`.
- Gleechi VirtualGrasp (Runtime, Editor, Build, Samples).
- Most game code is in Assembly-CSharp (no asmdef under `Assets/Rehab`).

## Scenes (build)
- `Assets/Scenes/GripRehab.unity`: the main scene, holding the app shell and apartment.
- `Assets/UI/GripUiGallery.unity`: UI gallery.

## Script counts (.cs, tracked)
| Folder | Count |
|---|---|
| 3.4.1-force-object-interaction | 35 |
| 3.4-game-modules | 16 |
| 3.1-environment | 7 |
| 3.2-vision | 6 |
| 6.1-bluetooth / 3.4.2 | 3 / 3 |
| VirtualGrasp | 43 |

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
