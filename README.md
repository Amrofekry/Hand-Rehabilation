## Current hand controls

The current scene uses the imported Gleechi VirtualGrasp hand model and a single visible selected hand. See [Arabic hand/keyboard guide](Docs/HANDS_AND_MEDIAPIPE_AR.md). Older bimanual screenshots below describe previous versions. Native automatic object-specific grasp synthesis still requires a baked Grasp DB; the project uses its own pickup/contact/material simulation.

# Grip Rehab — apartment interaction project

Open this folder using Unity Hub → Projects → Add → Add project from disk, with Editor **6000.4.4f1**. Open `Assets/Scenes/GripRehab.unity` and press Play.

**ابدأ من [الدليل العربي المصوّر](START_HERE_AR.html).**

Hand-only rig, two-hand MediaPipe stream and setup: [Docs/HANDS_AND_MEDIAPIPE_AR.md](Docs/HANDS_AND_MEDIAPIPE_AR.md).

The current playable scene contains one furnished apartment from the supplied Brick Project Studio asset, a first-person patient with one selectable rigged vendor hand, a seated dining station and a kitchen station. The current iteration focuses on force/object interaction. The saved menu chooses living room or kitchen and three objects at each station. Previous cognitive and coordination task logic remains in its WBS folders for later integration; those tasks are not presented as playable apartment activities yet.

- **Default: keyboard + mouse demo**, no hardware or Python required. Choose RIGHT or LEFT in the opening menu; only that vendor hand appears. WASD walk, mouse look, E grab, Q drop, F sit/stand, Space or left mouse squeeze, Shift+Space 80 N demo force, 1�5 choose a finger, J choose a joint, X/Z bend/open, C reset fingers, arrows/PageUp/PageDown move the selected hand, 0 recenter, wheel or +/- set demo force, R reset objects, B explicitly switch to Phase 2, Esc/Tab open the menu.
- Therapy ball and sponge deform; the virtual cracker fractures under sustained force above its configured threshold. These are visual demonstrations, not measured material models.
- Settings: `Assets/Rehab/3.1-environment/Apartment/ApartmentSettings.asset`.
- Runtime: `Assets/Rehab/3.4.1-force-object-interaction/Apartment/`.
- Phase 2 bridge: [Docs/BLE_INTEGRATION.md](Docs/BLE_INTEGRATION.md).
- Verification and known limits: [Docs/QA.md](Docs/QA.md).
- Build and Verify preserve the authored scene. Edit Scene/Prefabs normally and save outside Play.
- This is the new Unity working folder. Original `../unity-phase3` and Phase 2 hardware files are preserved; there is no automatic synchronization.

Force input targets Nano 33 IoT + HX711 BLE v1. Real hardware testing remains pending. A load cell measures aggregate grip force, not individual finger angles; camera landmarks optionally drive finger pose. Demo hand motion, approximate camera retargeting, mesh deformation and fracture are not clinically validated. No licensed Obi asset, trained YOLO gesture classifier or fatigue model is included.
