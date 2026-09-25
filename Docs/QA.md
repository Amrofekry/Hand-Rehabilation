# Phase 3 apartment QA — 24 September 2026

The standalone Unity project was checked in Editor 6000.4.4f1 and in its Windows player. Every result below is a software result. Physical hardware does not exist yet.

## Results

| Check | Actual result |
| --- | --- |
| Serialized scene and imports | PASS: one furnished apartment, one player camera, one force receiver, one vision receiver, six selectable force objects, no missing scripts/materials, keyboard demo and replay-off defaults. |
| Unity Play/Stop cycles | PASS: three cycles; each ran the apartment interaction suite and released both UDP ports. The final cycle results are in `Logs/editor-qa.txt` and `Logs/apartment-release-editor-qa.log`. |
| Phase 2 logic | PASS: calibrated force conversion, packet flags, ordering, duplicates, queued transport age within 500 ms, reconnect/calibration/shape changes, explicit synthetic replay, and 21-landmark expiry. |
| Python bridge | PASS: four standalone bridge contract tests, including calibrated sample relay and rejecting expired/repeated/invalid force. |
| Windows build | PASS: `Builds/Windows/GripRehab.exe`, Direct3D 11. `Logs/apartment-final-release-build.log` records the build and byte-identical authored scene after an edit/save/reopen/build check. |
| Windows runtime | PASS: `Tools/run_player_checks.py` ran headless default, opt-in synthetic replay, and rendered gameplay. Each run passed **40** checks, recorded in `Docs/player-checks.json`. |
| Visual inspection | PASS: final rendered screenshots show rigged first-person hands, seated ball grip, kitchen sponge grip and station/object menu. See `Docs/images/`. Two overly bright table candles were reduced and checked again in the rendered build. |
| Unity Hub | Registered as `E:\GP\codex\GripRehab-Unity` with Editor 6000.4.4f1 (URP). |

The user-supplied Apartment_02 source uses 125,618 triangles. Dependency-based pruning removed 240,803,368 bytes of unused Brick Project Studio assets from this copy; 123,191,834 bytes of vendor source dependencies remain. The original ZIP remains unchanged in Downloads. Graphics screenshots were captured at 1280×720 on this machine; no FPS or minimum hardware specification was established.

## Practical limits

No real Nano 33 IoT, HX711, load-cell calibration, BLE radio, MediaPipe camera tracking or clinical session was tested. The Windows suite sends simulated loopback envelopes; its `hardware` source fixture is protocol-shaped test data, not proof of a device connection. The load cell measures aggregate force only. Individual finger joints in demo mode are animated; camera landmarks can drive an approximate pose but need physical calibration/validation. Silicone compression and ceramic fracture are visual meshes and simple physics approximations, not material measurements. Licensed Obi, trained YOLO gesture inference and an AI fatigue model are not included in this playable Phase 3 module.


## Core hand–object upgrade — 25 September 2026 (WBS 3.4.1)

The same standalone project now has 15 saved capsule-trigger probes on the right-hand phalanges, six serialized physical material profiles, per-contact force distribution, localized mesh displacement/recovery, filtered Phase 2 grip force, cumulative damage states, pooled fracture debris, and seated grip clearance that keeps the object above the table. The authored apartment, menus and Phase 2 packet protocol remain in place.

**Editor pass 1:** Unity 6000.4.4f1 compiled with no C# errors. `CoreInteractionAuthoring.Verify` found all 15 probes with colliders/kinematic bodies and all six profiles, and the apartment scene verifier found no missing scripts/materials. The existing three-cycle test completed with **48/48 checks per cycle**, releasing UDP ports 27817 and 27818 each time.

**Senior review pass 2:** The first run caught a real 30 cm separation between the seated hand and held ball caused by the table-clearance sphere cast; hand and hold positions were aligned above the tabletop. A serialization fault in the initial probe definition was fixed by giving `FingerContactProbe` its own script asset. Later checks confirmed 6/15 probes contact the ball, every noncontacting probe resolves exactly 0 N, and **179 mesh vertices moved while 0 of 227 distant vertices moved** in the seated ball grip. The mug broke from both sustained force and an energetic drop; shard reuse was exercised by resetting and breaking it again. A one-sample 500 N Phase 2-shaped spike was rejected by the contact-force filter. All 48 checks passed in three repeat Play/Stop cycles after the fixes.

**Windows player:** `Builds/Windows/GripRehab.exe` built successfully. `Tools/run_player_checks.py` passed **48/48 checks** in each of headless default, explicit synthetic replay, and rendered Direct3D 11 modes. Rendered screenshots were refreshed in `Docs/images/`. Four Python bridge tests passed separately.

| Measured contact-step duration | Mean | Maximum |
| --- | ---: | ---: |
| Editor batch (last Play cycle) | 0.62 ms | 19.57 ms |
| Windows headless | 0.48 ms | 5.36 ms |
| Windows opt-in replay | 0.55 ms | 6.42 ms |
| Windows rendered | 0.58 ms | 6.77 ms |

These are in-process elapsed times around the contact/deformation loop, recorded in `Logs/apartment-runtime-checks.json` and `Docs/player-checks.json`; the `GripRehab.FingerContacts` profiler marker is available for deeper inspection. They are not FPS measurements or guarantees on other PCs.

The available test sources are software fixtures. A real camera, Nano 33 IoT, HX711, load-cell calibration, finger-surface accuracy, haptic force and clinical safety remain unverified. The object collider remains rigid while the mesh deforms visually; contact force is an aggregate reading distributed only among contacting fingers. There is no FEM/Obi simulation, soft-body collider remeshing, fluid or real fracture mesh generation. The local approximation is suitable for continued development, not patient use.
