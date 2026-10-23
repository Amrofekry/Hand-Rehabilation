# GripRehab repair handover — 30 September 2026

Project: `E:\GP\codex\GripRehab-Unity`, Unity Editor **6000.4.4f1**.
Scope: repair and repeated tests of the existing English game. No new gameplay, redesign, research portal, CAD, AI or localization features were introduced. Earlier unfinished expansion files and the user's working tree remain on disk.

## Problems reproduced and repaired

| Finding | Repair | Evidence |
| --- | --- | --- |
| Ordinary Play failed with `MissingComponentException` while creating `Research portal`; an ordinary Transform cannot become a RectTransform using the old fallback. | Create the view with RectTransform at construction and require that component. | Native Editor subsequently initialized the view without that exception; C# compilation passed. |
| Recent expansion automatically replaced the saved, previously working Home/profile/module menu at every startup. | Removed automatic research-host creation. An explicitly attached host remains supported, but the saved scene uses the original menu. | Separate ordinary Editor Play showed Home; Start opened Modules; Back returned Home; no red Console errors. |
| Imported apartment components were deleted at runtime because the built-in particle-system package was disabled. | Restored `com.unity.modules.particlesystem@1.0.0` through Unity PackageManager Client; kept existing package versions. | Registered-package check after reload, native scene verification, Windows build and repeated runtime tests passed. |
| Project-owned calls produced Unity 6.4 obsolete-API warnings. | Removed unnecessary sorted FindObjectsByType overloads; the dwell cursor now uses a stable token for each target transition instead of GetInstanceID. | Native compile, Editor dwell checks and five-second bilateral loopback pointer tests passed. |
| Canceling the scene-save dialog could let the fixed build gate falsely report success. | Build cancellation now throws and produces a failed gate result. | Source review and native compilation; a normal completed build passed. Interactive cancellation was not separately exercised. |
| Windows regression-log scan missed MissingComponentException; offline evidence incorrectly described the user's old files-only restriction. | Added the exception to fatal log markers and made offline evidence describe only that tool's scope. Corrected camera/menu/force instructions. | Fresh player checks, updated evidence descriptions and BLE guide. |

During the package-repair implementation, a namespace collision with the gameplay SessionState enum briefly caused compilation errors. The editor calls now explicitly use UnityEditor.SessionState; all native compilation and runtime results below were obtained after that correction.

## Fresh verification

| Verification | Result | Evidence |
| --- | --- | --- |
| Unity Editor Play/Stop | **3 cycles**, 137 runtime checks per cycle; all UDP ports 27817/27818/27819 released after each Stop. Completed **16:08:04 UTC**. | `evidence/repair-editor-qa.txt`, `repair-editor-runtime.json`, `repair-editor-qa.json` |
| Native scene/logic/reference verification | PASS, **16:14:35 UTC**; no scene regeneration. | `evidence/repair-scene-verify.json` |
| Editor pointer fixtures | PASS, **16:14:00 UTC**. | `evidence/repair-dwell-check.json` |
| Windows build | PASS, **16:13:15 UTC**. | `evidence/repair-windows-build.json`; managed game-assembly hash in `repair-summary.json` |
| Windows normal / explicit Synthetic Phase 2 replay / rendered player | **137 / 138 / 137 PASS**, fresh output and successful exit. Rendered at 1280×720. | `player-checks.json` |
| Windows UI presentation | **60 checks each** at 1280×720 and 1600×900. | `ui-player-checks.json`, fresh theme screenshots |
| Existing UI gallery | **79 checks each** at 1280×720, 1280×800 and 1024×768. | `gallery-checks.json` |
| Offline C# checks | 788 metadata checks, five-assembly dependency graph, five Editor/four Player assemblies compiled; **37 domain tests passed**. | `evidence/files-only-checks.json`, `Logs/offline/product-tests.log` |
| Python hand/force bridge contracts | **6 tests passed**, repeated twice in this repair session. | `python -m unittest Tools.Vision.test_vision_bridge Tools.Phase2Bridge.test_unity_bridge` |
| Ordinary production Editor startup, not acceptance mode | Home → Start → Modules → Back → Home worked; pressing Space on the module selector did not submit or exit. Existing profile remained readable. Camera-pointer status reached CAMERA READY. **0 Console errors**, one known vendor warning. | Computer Use observation in this session; final Editor screenshot contains no profile data. |

Runtime fixtures cover right/left selection, joints, synthetic landmark direction/freshness, five-second pointer activation with brief gesture flicker, lost tracking, E/G input paths, squeeze/release, local deformation, brittle fracture, player collision isolation, saved profiles and the existing prototype exercises. Tests use isolated fabricated records and explicit synthetic inputs; they are not a supervised physical-hand trial. The tool-specific offline result does not claim to run Unity; native Editor and player evidence is recorded separately above.

Visual review of the fresh Home, HUD/deformation and high-contrast enlarged Appearance screenshots found readable foreground text within the relevant panels. Existing themes and English UI were preserved. Menu-only frame samples averaged about **270–407 FPS** on this machine; see PERFORMANCE.md for measurement conditions. This is not a live MediaPipe latency or zero-allocation measurement.

## Handover and remaining limits

- Open the same project in Unity Hub. Main scene: `Assets/Scenes/GripRehab.unity`. Windows application: `Builds/Windows/GripRehab.exe`. The launcher EXE may retain its original timestamp; the freshly built managed assemblies are the game-code evidence.
- The Editor is left outside Play on the main scene. Stop any other running player before pressing Play so it does not own the UDP ports. Keyboard Demo remains available through B; E on an aimed object in Camera explicitly enters demo. Space squeezes in Demo; Enter confirms menus. G is the Camera pickup assist.
- VirtualGrasp still warns **No baked grasps found**: the imported rig/native external-controller path initializes, but object-specific Grasp DB baking is absent. No vendor files were modified and the warning was not hidden.
- Live human left/right tracking, five-second pointing usability and end-to-end camera latency still need a supervised trial. CAMERA READY only establishes worker startup, not accurate motion/grasp for a real hand. Physical Nano/HX711 and clinical validation remain unverified.
- Git still rejects creation of `.git/index.lock` with Permission denied. No new commit or clean-tree claim is made; pre-existing changes were preserved. A full-tree whitespace check flags historical Unity-generated empty YAML fields; these are not runtime faults and were not rewritten.
- Historical product/redesign requirements are not completion claims or pending authorization for new features. This handover closes the repair checks listed above, with the stated physical/vendor limits.
