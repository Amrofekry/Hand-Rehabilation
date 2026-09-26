# Grip Rehab — Unity apartment and live hand tracking

Open `E:\GP\codex\GripRehab-Unity` in Unity Hub with Editor **6000.4.4f1**. The saved main scene is `Assets/Scenes/GripRehab.unity`. For first-time instructions, read [START_HERE_AR.html](START_HERE_AR.html) and the [Arabic camera/hand guide](Docs/HANDS_AND_MEDIAPIPE_AR.md).

**Default Play mode is now live MediaPipe camera.** The project-local Python worker opens webcam index 0, estimates 21 landmarks, and streams them on loopback UDP 27817. The annotated local preview is shown in Game view via UDP 27819. Select RIGHT or LEFT in the menu; exactly one imported Gleechi VirtualGrasp hand is rendered. Approach a chosen object with the tracked hand, close the fingers briefly to pick it up, and open them to release. Tracking loss also releases it. The visual grip percentage and object indentation are demonstrations, not measured force or clinical validation.

Press **B** to cycle Camera → Keyboard Demo → Phase 2 → Camera. The keyboard demo works without camera or hardware: WASD/mouse navigate, E picks up, Q releases, Space/click squeezes, 1–5 selects fingers, J chooses a joint, X/Z bends/opens, C resets, arrows/PageUp/PageDown move the hand, F sits or stands, and V toggles the preview. Phase 2 expects Nano 33 IoT + HX711 through the supplied bridge on UDP 27818; no such physical hardware has been tested. There is no automatic fallback from an invalid Phase 2 sample to demo force.

The single furnished apartment from Brick Project Studio has seated living-room and kitchen stations with three selectable objects each. Their local deformation and breakage are approximations. The imported VirtualGrasp package supplies the visible rig, output bones and controller; automatic object-specific Grasp DB baking has not been performed. Older screenshots in `Docs/images` may show previous modes or synthetic tracking and are labelled as such.

- Scene settings: `Assets/Rehab/3.1-environment/Apartment/ApartmentSettings.asset`.
- Camera bridge: `Tools/Vision/mediapipe_bridge.py`; Unity lifecycle and preview: `Assets/Rehab/3.2-vision/`.
- Hand interaction: `Assets/Rehab/3.4.1-force-object-interaction/Apartment/`.
- Phase 2 protocol: [Docs/BLE_INTEGRATION.md](Docs/BLE_INTEGRATION.md).
- Verification and remaining limits: [Docs/QA.md](Docs/QA.md).

Build and Verify preserve the authored scene. Edit scene assets outside Play and save. This is the standalone Unity working folder; the former `../unity-phase3` and Phase 2 firmware remain separate, with no automatic synchronization. A real-person live-camera landmark/grasp trial and real load-cell validation remain outstanding.
