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
