# Automatic camera hand selection and G summon — 26 September 2026 (WBS 3.2 / 3.4.1)

The reported live right hand worked while the left did not. Camera mode previously used only the manually selected side and the menu always initialized that choice to Right. The game now selects the sole fresh MediaPipe Right/Left stream automatically. If both streams are present, it keeps the selected side until that stream has stopped refreshing; switching sides releases a held object and rebinds the imported VirtualGrasp mesh and contact bones. The menu hand switch remains available. This change does not alter the Phase 2 force protocol.

The new **G** key moves the current room's selected object into the tracked hand while remaining in camera mode. It does not require matching the camera's estimated depth to the object's world position. The hand must be tracked and the menu resumed; the patient has about 1.25 seconds to close the fingers before an open hand drops the object. Tracking loss and changing sides release it immediately. **E** remains the explicit keyboard-demo pickup and is a separate control.

Unity Editor 6000.4.4f1 passed **91/91 checks in each of three Play/Stop cycles**, including automatic Right→Left and Left→Right switching, stability when both hands briefly appear, G placement at both hands, open-hand expiry, maintaining a G grip after the left fingers close, rejection without fresh tracking, existing grip/deformation/fracture cases, and release of UDP 27817/27818/27819 (`Logs/editor-qa.txt`, `Logs/auto-hand-editor-qa-final.log`). The final Windows build succeeded (`Logs/auto-hand-windows-build-final.log`). The built player passed **91/91** in headless, explicit Phase 2 replay, and rendered modes (`Docs/player-checks.json`). A local-camera smoke test of that final build opened MediaPipe and the preview at **30.5 FPS** with 17 preview packets (`Logs/vision-live-acceptance.json`, `Logs/auto-hand-vision-live-final.log`). No physical hand was in view in that smoke test, so **actual left-hand recognition, live left-finger motion, and live camera pickup still require an observed user trial**. No physical load cell was available.

---

# Camera and E-key regression repair — 26 September 2026 (WBS 3.2 / 3.4.1)

The reported failure was reproducible in code: camera-first Play ignored E on an object, tracking animation was suppressed while the start menu was open, and the vision receiver retained frame sequence/capture counters after the Python camera process restarted. An explicit E pickup now changes to keyboard demo and grabs the aimed object. Camera motion continues while the menu is open, but pickup is disabled until Resume. Restarting camera clears the old sequence. Real near-edge landmark coordinates and handedness scores above 0.5 are accepted; implausible points and stale/duplicate frames remain rejected. The HUD and annotated preview report the recognized Right/Left side, packet counts and last rejection reason.

The laptop webcam's raw read rate varied with auto exposure. One controlled read test measured about 15 FPS/66 ms per frame in automatic mode and 29.5 FPS/34 ms after requesting a fixed DirectShow exposure of -5; average image brightness remained about 62/255 in both measurements. The new `VisionCameraLauncher.lowLatencyCapture` switch requests 30 FPS and fixed exposure by default, and can be disabled in Inspector if an external camera or different lighting makes the image too dark. The preview is encoded at 256x192/5 FPS and is decoded only while shown; landmark inference remains on the full 640x480 camera frames. Camera mode also keeps Unity updating when the Game window loses focus.

Final verification: Unity Editor 6000.4.4f1 passed **82/82 runtime checks in each of three Play/Stop cycles** and released UDP 27817/27818/27819 each time (`Logs/editor-qa.txt`, `Logs/lag-final-editor-qa.log`). The final Windows build succeeded (`Logs/lag-final-windows-build.log`). Headless, explicit force replay, and rendered player each passed **82/82 checks** (`Docs/player-checks.json`), including keyboard E pickup from camera mode, visible pose while menu-paused, and acceptance of a restarted camera sequence beginning at one. Two Python bridge contract tests passed. A real webcam startup test of the final Windows player exited successfully with `camera_open`, **30.0 camera frames/s after stabilization** and 17 local preview packets (`Logs/vision-live-acceptance.json`, `Logs/lag-final-vision-live.log`). A separate rendered frame-time comparison without a tracked hand measured 535.8 FPS with camera preview, 565.9 with preview hidden, and 574.4 in keyboard demo at 1280x720 (`Logs/vision-performance.json`); this demonstrates rendering is not the principal bottleneck on this device, but it is not an end-to-end motion-latency measurement.

The user reported that hand points appeared in the preview but the virtual hand remained still. No real hand was present during these final automated tests, so **live patient-hand motion and actual pickup remain unverified** and should be retested when the user is available. The HUD now distinguishes a detected side mismatch or rejected packet to make that trial diagnosable. The free VirtualGrasp package still emits its two transient startup controller diagnostics. No physical load cell or BLE hardware exists for a real force test.

---
# Live MediaPipe camera integration — 26 September 2026 (WBS 3.2)

The saved scene now starts in camera mode. The project-local Python worker opens webcam index 0 through DirectShow, runs MediaPipe Hand Landmarker on live frames, sends 21-point Right/Left frames over loopback UDP 27817, and sends a reduced annotated preview over UDP 27819. The imported Gleechi VirtualGrasp selected hand follows the accepted joint pose. A closed tracked hand near a chosen object picks it up; opening it, changing hands, or losing tracking releases it. Keyboard demo and Phase 2 force remain explicit modes via B. Camera "VISUAL GRIP" is an estimated closure percentage; any virtual contact pressure derived from it is not a load-cell measurement.

**Editor 6000.4.4f1:** three Play/Stop cycles passed **79/79** runtime assertions each, including right-ball and left-sponge gesture pickup/release, stale camera release, rejecting the unselected hand, independent finger motion, force protocol, deformation and fracture software fixtures. All three loopback ports 27817/27818/27819 rebound after each stop (`Logs/editor-qa.txt`, `Logs/vision-editor-qa6.log`). **Windows:** `Builds/Windows/GripRehab.exe` built successfully (`Logs/vision-windows-build2.log`); headless, explicit synthetic-force replay and rendered player each passed **79/79** checks (`Docs/player-checks.json`). The tests use deterministic, labelled synthetic landmark frames for grasp behavior.

**Real camera smoke test:** the rendered Windows player exited 0 with `camera_open`, 22.6 processed frames/s and 7 local preview packets (`Logs/vision-live-acceptance.json`, `Logs/vision-live-player-final.log`). No real hand was present during that run: both `rightTracked` and `leftTracked` were false. This confirms device opening and frame delivery, **not** real-person landmark fidelity, left/right correctness on this webcam, object pickup, depth accuracy, or measured latency. The user plans to put a hand before the camera after the game is ready; that supervised acceptance test remains pending. No Nano 33 IoT or load cell was available, so physical force/BLE testing also remains pending.

The camera-to-virtual-depth mapping estimates forward travel from apparent palm width relative to the first tracked frame. It is approximate and can drift with rotation or perspective; a single RGB camera does not provide metric 3D hand position or grip force. The free VirtualGrasp SDK still emits two transient `No controller for RehabVGController` startup diagnostics before its external controllers become active. Its vendor Grasp DB was not baked for these objects; the project handles proximity pickup and material effects. This is a development demonstration, not clinically validated rehabilitation software.

---
# Phase 3 apartment QA — 24 September 2026

The standalone Unity project was checked in Editor 6000.4.4f1 and in its Windows player. Every result below is a software result. Physical hardware does not exist yet.

## Selectable VirtualGrasp hand update � 26 September 2026 (WBS 3.4.1)

The scene now uses the imported Gleechi VirtualGrasp 1.7.0 hand model, with RIGHT/LEFT selection and exactly one rendered hand. Keyboard controls target each of five fingers and three joints; the external controller passes 16 wrist/joint poses to the SDK output. Phase 2 force packets and 21-point vision frames still feed the separate input rigs. Fifteen contact capsules switch to the selected vendor output hand. Local asymmetric mesh indentation and object-specific fracture were added to the six interaction objects. This replaces the hand implementation described in the 25 September historical sections below.

Unity Editor 6000.4.4f1 passed **69/69 runtime checks on each of three Play/Stop cycles**, releasing ports 27817 and 27818 each time (`Logs/editor-qa.txt`, `Logs/vg-final-editor-qa4.log`). The final Windows build passed (`Logs/vg-final-windows-build3.log`). Its headless demo, opt-in synthetic replay and rendered Direct3D runs each passed **69/69 checks** (`Docs/player-checks.json`). The rendered screenshots include `Docs/images/vg-ball-before.png`, `vg-ball-deformed.png`, `vg-mug-fractured.png`, and `vg-left-kitchen-grip.png`. The selected-hand HUD and menu button are asserted in both directions. The final rendered player measured a 0.51 ms mean and 3.67 ms maximum around the contact loop. These local code-loop timings are not FPS or device performance guarantees.

Known SDK diagnostic: at the beginning of each Play run the imported VirtualGrasp free SDK emits two `No controller for RehabVGController` errors, one per hand, before its external controllers become active. Subsequent 16-bone motion and every acceptance test pass. This startup log remains unresolved and should not be described as a clean Console. The purchased/test asset remains in this local project, and its original source was not modified except repair of one malformed `.meta` GUID line. The contact simulation uses the vendor output bones but keeps the project's E-key pickup; the vendor automatic grasp database has not been baked for the six objects. In the head-on first-person view a held ball can hide much of the hand; the pre-grab screenshot shows its complete visible model.

Synthetic force/vision fixtures do not validate a real load cell, BLE device, camera accuracy, grasp accuracy, material strain, or clinical use.

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

## Two-hand core update — 25 September 2026 (WBS 3.2)

The saved player prefab now contains only two skinned hand renderers. The torso, legs, shoes, cuffs, and separate forearm meshes were removed in Unity's prefab authoring API. The first-person camera/controller, seated station, six objects, right-hand contact probes and Phase 2 force receiver remain connected. A runtime assertion confirms that no patient renderer sits outside the two hand hierarchies.

MediaPipe transport now carries independent Left/Right 21-landmark frames and their sequence, capture time, and capture-to-send age. Unity rejects malformed, duplicate, out-of-order, or already-stale frames; expiry is independent per hand and includes Python processing and Unity queue latency in the 500 ms budget. Finger MCP/PIP/DIP curl, spread, palm orientation and relative wrist travel drive both skinned rigs. Demo mode additionally moves the selected hand with T, arrows, PageUp/PageDown and 0; the held object follows the right hand. The load cell still supplies only aggregate right-grip force.

Final verification: Unity Editor 6000.4.4f1 completed **three Play/Stop cycles with 59/59 runtime checks each** and released UDP ports 27817/27818 every time (`Logs/editor-qa.txt`). Windows build succeeded with the unmodified user-imported VirtualGrasp FREE 1.7.0 present (`Logs/bimanual-windows-build-final.log`). The Windows player passed **59/59 checks in each of headless demo, opt-in force replay, and rendered modes** (`Docs/player-checks.json`). Two Python bridge contract tests passed; the bundled MediaPipe 1.0.1 model initialized and inferred on a blank local image with no detected hands. The model self-test did not use a camera. The rendered synthetic-tracking image is `Docs/images/apartment-two-hands-tracked.png`, explicitly labelled SYNTHETIC in the HUD.

The imported VirtualGrasp meshes include shoulder/elbow or both arms, so the existing licensed Unity XR Hands meshes remain the active hand-only geometry and the VirtualGrasp grasp solver is not stacked over the existing contact engine. The SDK's original build callback requires Android ARM64 and physics faster than 72 Hz even for Windows; this project's saved settings select ARM64 and 75 Hz, leaving vendor files untouched. The SDK was supplied locally and is intentionally untracked by Git; another checkout must import its own licensed copy for identical local setup.

No live camera, actual two-hand landmark accuracy measurement, wrist-depth calibration, independent finger-force sensor, BLE hardware or patient assessment was performed. The screenshot and numerical tests use synthetic frames. The single-camera mapping is approximate because normalized landmark z is relative and not metric. This build is a development base, not a validated clinical device.
