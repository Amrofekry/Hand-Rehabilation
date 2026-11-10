# Item 6 — VirtualGrasp finger joints and poses — 1 October 2026

**Software implementation complete; native SDK, rendering and live-camera verification pending.**

Canonical project: `E:\GP\codex\GripRehab-Unity`. WBS **3.2 / 3.4.1**. This revision was completed using files and managed tests. No Unity Editor, Hub, player, Computer Use or webcam was opened. Earlier native results do not validate this revision.

## Repaired behavior

The existing imported Gleechi VirtualGrasp hand remains the output hand. Project-owned input adapters now require a complete, unambiguous wrist-plus-15-joint skeleton for both sides. SDK output bone names and sides must match the saved rig. Missing, duplicate, wrong-side or unavailable mappings fail explicitly instead of silently accepting an incomplete hand.

Each source frame is validated completely before any SDK pose write. Non-finite positions and invalid quaternions reject that frame; valid rotations are normalized. A bad final bone cannot leave a partly updated hand. A subsequent valid frame can recover. Pose buffers and contact-bone arrays are reused.

SDK avatar registration alone no longer sets `Ready`. Both project input controllers and all 15 distinct SDK contact bones on each side must be available. Registration is retried at a bounded rate; the first two seconds allow normal startup, followed by an actionable error logged once per distinct persistent failure. Missing required mesh/skeleton assets stop SDK lifecycle calls cleanly. Switching hands uses the selected mesh and cached corresponding contact array. Unready contact binding clears earlier bindings rather than silently using another hand.

The driver validates all 15 distinct input joints before posing. Invalid selection indices, manual/demo values, delta time, axes and smoothing settings are bounded or rejected. A replaced joint captures its own rest pose. Invalid smoothing history can recover. Independent thumb-base bending now uses the same 45-degree prototype limit as whole-thumb bending. Entering Demo or losing tracking clears live-frame metadata immediately, including when holding the last visible pose.

The camera coordinate conventions, palm-facing correction, horizontal/vertical/depth directions and existing calibration are preserved. No new menu, exercise, hand mesh, SDK package edit or force protocol was introduced.

## Finger channels and controls

| Project channel / key | Finger | SDK finger index |
| --- | --- | --- |
| 0 / 1 | Index | 1 |
| 1 / 2 | Middle | 2 |
| 2 / 3 | Ring | 3 |
| 3 / 4 | Little | 4 |
| 4 / 5 | Thumb | 0 |

In **Keyboard Demo**, keys **1–5** select a finger. **J** cycles all joints → base → middle → tip → all joints. Hold **X** to bend and **Z** to open the selected channel; **C** resets manual finger input. **Space / mouse squeeze**, existing pickup/release keys and hand selection remain unchanged. Camera mode continues to derive pose from 21 landmarks; **C** recenters camera tracking there. Keyboard input is an explicit fallback, not measured force.

Non-thumb demo limits are 65/85/55 degrees at base/middle/tip; thumb limits are 45/85/55. These are bounded prototype animation settings, not patient range-of-motion measurements or clinical limits. The five whole-finger channels and all 15 individual channels were checked on both authored sides, including their opposite curl signs.

MediaPipe supplies estimated pose. The load cell supplies aggregate grip force, not separate forces or bends for each joint. Existing unsigned flexion, generic rig axes, thumb opposition, monocular depth and occlusion remain approximation limits. This work does not establish exact anatomical motion or physical material behavior.

## Evidence

| Check | Result / scope |
| --- | --- |
| Production hand-rig/SDK-adapter source | 107 assertions passed, using managed math and SDK doubles |
| Static scene, profile, mapping and integration contracts | 10 tests passed |
| Earlier tracking, pointer, keyboard, grasp, deformation, product and bridge regressions | 524 unique checks passed |
| Total unique positive software checks | **641**; heterogeneous source/static/bridge scopes, not 641 native gameplay tests |
| Combined Python suites | 61 passed, **already included** in the total |
| Cached real Unity/VirtualGrasp-reference compilation | Five Editor and four player assemblies passed; not a Unity import or Windows build |
| Metadata / project assembly graph | 796 metadata GUIDs and five assembly definitions checked |
| Exact pre-item source comparison | 67 passed / 22 failed; exception-aborted old groups ran 89 checks, not the full revised 107 |
| Isolated deliberately broken variants | Four compiled and were rejected: wrong contact-channel order, unsafe delta time, false readiness and partial SDK writes |
| Preservation hashes | All 586 captured vendor/hand/profile assets, saved main scene and captured earlier repair sources matched |

The baseline exposed inconsistent independent thumb-base bending, out-of-range input failures, incomplete/ambiguous skeleton acceptance, invalid partial writes, false readiness and contact-array reuse failures. Mutation tests used copies under `Logs`; they never replaced production files.

Reproduction from the project root:

```powershell
.\.venv\Scripts\python.exe Tools/offline_verify.py --compile --tests
.\.venv\Scripts\python.exe -m unittest Tools.test_hand_rig_bindings Tools.test_pointer_bindings Tools.test_deformation_bindings Tools.test_grasp_bindings Tools.test_keyboard_bindings Tools.Vision.test_vision_bridge Tools.Phase2Bridge.test_unity_bridge
.\.venv\Scripts\python.exe Tools/Offline/verify_hand_rig_sensitivity.py
```

Evidence snapshots: `evidence/item6-hand-rig-checks.json`, `item6-offline-checks.json`, `item6-negative-checks.json` and `item6-preserved-files.json`. Detailed logs are in `Logs/offline`, with the exact old/new comparison under `Logs/offline/item6-rig`. Managed SDK doubles do not execute the native VirtualGrasp solver or skin the rendered hand.

## Remaining runtime verification

- Import and native SDK registration/controller discovery in Unity; confirm both visible skinned hands and each joint follow the corresponding input skeleton.
- Live right/left camera trial: open/close each finger, rotate the wrist, recenter, lose/recover tracking and change sides. Confirm thumb appearance and calibration against the actual model.
- Native hand-object contact/grasp/drop, material appearance, repeated Play/Stop and a fresh Windows build.
- Frame timing, allocations and camera latency on the target laptop. Buffer reuse is a source change, not measured zero-GC or FPS proof.
- Physical Nano/HX711 force validation remains separate until hardware exists.

Items **1–6 are software-complete** with their documented native/live limits. Items **7–15 remain pending**; item 7 was not started. The previous dirty Git tree and existing profiles were preserved. No new commit is claimed.

Recovery copies of the pre-item project-owned files: `E:\GP\codex\GripRehab-Unity\Logs\item6-before-20261001-160056`.
