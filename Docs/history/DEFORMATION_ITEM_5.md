# Item 5 — deformation and fracture — 1 October 2026

**Status: software implementation complete in the canonical project; native verification pending.**

Project: `E:\GP\codex\GripRehab-Unity`. WBS: **3.4.1**. No Unity, Hub, player, webcam or Computer Use was launched for this revision. Historical native acceptance results do not verify it.

## Changes and resolved defects

- Contact-local deformation produces bounded, irregular dents with a small rubber bulge. Actual capsule/object penetration is confirmed every physics step; old trigger callbacks cannot manufacture contact. Pressure is distributed across confirmed contacts. Geometry/scratch arrays are cached, and source mesh assets remain unchanged.
- Rubber recovers after release, including the final mesh upload. Plastic materials retain a bounded dent. Reset clears permanent/temporary offsets and displayed metrics. Distant vertices stay unchanged; there is no whole-object scale trick.
- Porcelain remains rigid. Above-threshold sustained pressure produces damage seams and fracture; energetic impacts can also fracture it. A single pressure peak, lost contact or a frame hitch cannot bypass the continuous-pressure gate.
- Mug/cracker debris consists of partitions of the original mesh triangles with UV/material references preserved. Inactive fragment bodies/colliders are cached explicitly: the preliminary version missed them. Pieces are precomputed, their count is bounded, player collision protection is retained, and modest capped scatter replaces the former impulse that could fling lightweight pieces.
- Camera maximum visual stimulus is 80 rather than 38, so closure can reach the existing brittle thresholds of 55/60. Camera output remains labelled **visual grip / no measured force**. Phase 2 protocol and measured-force input are unchanged. Keyboard input, hand direction mapping, imported VirtualGrasp and material-profile values are preserved.

The six existing objects retain distinct responses: recovering silicone ball/cup, soft sponge, brittle cracker, rigid brittle porcelain mug, and a tomato with retained dents. The mug is porcelain; no glass asset is claimed.

## Fresh files-only verification

The tests ran against the saved canonical files, without a staging override:

| Check | Result |
| --- | --- |
| Compilation against cached Unity 6000.4.4f1 references | Five Editor and four player assemblies passed |
| Metadata / project assembly dependency closure | 794 GUIDs / five project assemblies passed |
| Production deformation/state/damage/partition assertions with managed math | 61 passed |
| New static deformation integration contracts | Nine passed |
| Earlier pose, keyboard, grasp, product and Python regressions | 331 passed |
| Combined Python integration/bridge suites from project root | 34 passed; included in the 401 total |
| Isolated negative mutations: outward dent, omitted final recovery upload, missing source triangle | All three compiled and were rejected |

**401 unique positive checks** passed across different software scopes. Repeated runs are not counted again. The tests exercise production pure-source logic with managed math/physics substitutes and static wiring, not Unity rendering, native PhysX, SDK behavior or physical sensors. New native acceptance fixtures compiled but were not executed.

Evidence: [item5-deformation-checks.json](../evidence/item5-deformation-checks.json), [item5-offline-checks.json](../evidence/item5-offline-checks.json), [item5-negative-checks.json](../evidence/item5-negative-checks.json). Logs are under `Logs/offline`.

Run from the project root:

```powershell
.\.venv\Scripts\python.exe Tools\offline_verify.py --compile --tests
.\.venv\Scripts\python.exe -m unittest Tools.test_deformation_bindings Tools.test_grasp_bindings Tools.test_keyboard_bindings Tools.Vision.test_vision_bridge Tools.Phase2Bridge.test_unity_bridge
.\.venv\Scripts\python.exe Tools\Offline\verify_deformation_sensitivity.py
```

Negative tests only change isolated generated copies under `Logs/offline/deformation-negative`; production files are never replaced by a mutation.

## Application and preservation

The previous Windows file-tool write failure is resolved for this checkpoint. All eight pending files were applied using exact-hash validation, recovery copies and verified readback. A fresh canonical run also passed the Product PDF regression that previously failed on fixture output permissions. Original failure evidence remains in the task workspace.

Backups: `Logs/item5-before-20261001` and `Logs/item5-pending-before-20261001-134136`. Hand-direction, keyboard and grasp-policy sources were checked against earlier evidence hashes. Material profiles, contact-probe source and authoring source were checked against the item 5 backup. Saved scenes, source meshes, imported SDK and user-profile storage were not edited. Existing unrelated Git changes are preserved; no commit is claimed.

## Native checks still required

Check all six objects, both hands and standing/seated stations. Verify normal/max/zero pressure, local dents, release recovery, retained tomato dent, rigid mug seams/fracture, cracker breakup, impact fracture and reset. Test tracking loss and Phase 2 stale/duplicate/calibration changes. Inspect mesh-piece appearance, contact distribution, debris/player/furniture collisions and repeated Play/Stop, then build/run Windows.

This is a visual material-response approximation using primitive collision proxies and surface fragments, not volumetric fracture or FEM. Collider geometry remains undeformed. Debris settling, crack appearance, deformation performance and real camera usability require native testing. Force thresholds are prototype values, not material measurements or clinical limits.

## Checklist status

Items **1, 3, 4 and 5** are software-complete with native testing pending. Items **2 and 6–15** remain pending review/completion. No other numbered item was implemented in this checkpoint.
