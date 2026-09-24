# Grip Rehab — apartment interaction project

Open this folder using Unity Hub → Projects → Add → Add project from disk, with Editor **6000.4.4f1**. Open `Assets/Scenes/GripRehab.unity` and press Play.

**ابدأ من [الدليل العربي المصوّر](START_HERE_AR.html).**

The current playable scene contains one furnished apartment from the supplied Brick Project Studio asset, a first-person patient with rigged hands, a seated dining station and a kitchen station. The current iteration focuses on force/object interaction. The saved menu chooses living room or kitchen and three objects at each station. Previous cognitive and coordination task logic remains in its WBS folders for later integration; those tasks are not presented as playable apartment activities yet.

- **Default: keyboard + mouse demo**, no hardware or Python required. WASD walk, mouse look, E grab/interact, Q drop, F sit/stand, Space or left mouse squeeze, wheel or +/- change demo force, R reset objects, B explicitly switch input source, Esc pause/release cursor.
- Therapy ball and sponge deform; the virtual cracker fractures under sustained force above its configured threshold. These are visual demonstrations, not measured material models.
- Settings: `Assets/Rehab/3.1-environment/Apartment/ApartmentSettings.asset`.
- Runtime: `Assets/Rehab/3.4.1-force-object-interaction/Apartment/`.
- Phase 2 bridge: [Docs/BLE_INTEGRATION.md](Docs/BLE_INTEGRATION.md).
- Verification and known limits: [Docs/QA.md](Docs/QA.md).
- Build and Verify preserve the authored scene. Edit Scene/Prefabs normally and save outside Play.
- This is the new Unity working folder. Original `../unity-phase3` and Phase 2 hardware files are preserved; there is no automatic synchronization.

Force input targets Nano 33 IoT + HX711 BLE v1. Real hardware testing remains pending. A load cell measures aggregate grip force, not individual finger angles; camera landmarks optionally drive finger pose. Demo hand motion, approximate camera retargeting, mesh deformation and fracture are not clinically validated. No licensed Obi asset, trained YOLO gesture classifier or fatigue model is included.
