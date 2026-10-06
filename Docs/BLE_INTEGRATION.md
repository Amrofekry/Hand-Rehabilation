# 6.1 Phase 2 connection — standalone apartment project

Target: Nano 33 IoT + HX711 and existing BLE v1 packets, documented in BLE_PROTOCOL.md. `Tools/Phase2Bridge` contains byte-identical copies of `unity_bridge.py`, `monitor.py`, and `protocol.py` from Phase 2; SOURCE_MANIFEST.json records hashes. No runtime file depends on the old project path.

## Starting Module 01 and testing without hardware
Press Play to open the Main Menu. Start can open guest practice; creating a local profile is optional. Choose Module 01 and the game starts in CAMERA mode. The menu's whole-hand pointer can also start the camera worker before entering the module. Press B during gameplay to cycle Camera → Keyboard Demo → Phase 2 → Camera; B does nothing while a menu is open. Alternatively, E on an aimed object in Camera mode explicitly selects Keyboard Demo and grabs it; E in Phase 2 retains the measured source. In Demo, walk with WASD, aim with the mouse, E grabs, Q drops, F sits/stands, and Space or left mouse squeezes. Mouse wheel or +/- changes the simulated force. Enter confirms focused menu actions; Space is not a menu Submit key. There is no automatic fallback from missing camera or Phase 2 input.

Escape or Tab toggles session options. After Resume, regaining window focus, or changing input mode, release any held Space/mouse button and press it again to squeeze. Both Shift keys select maximum simulated grip; keypad +/- also changes the demo setpoint. Apartment shortcuts are blocked while editing a profile or navigating shell screens. See [item 3 verification](KEYBOARD_ITEM_3.md) for the original files-only evidence; native keyboard and transport regressions are now recorded in [the 6 October closeout](CLOSEOUT_2026_10_06.md).

## Software BLE replay in Editor
1. Stop Play. Select **03 · First person patient** in Hierarchy.
2. In **Live Force Receiver**, enable **Allow Synthetic In Editor** temporarily.
3. Press Play, select a profile and Module 01, then press **B** twice during gameplay to select Phase 2 input.
4. Run `.\.venv\Scripts\python.exe Tools/Phase2Bridge/unity_bridge.py --demo --seconds 30` from this project folder.
5. Both the in-game input card and the detailed HUD must show **SYNTHETIC REPLAY**, then 18 N. Aim at an object and press E.
6. Stop the sender: within the 500 ms freshness budget plus a frame the object releases, force becomes invalid and hold progress stops. Stop Play, disable replay, then save.

A Windows player rejects synthetic packets unless launched with `-rehab-force-replay`; the Editor checkbox is not compiled into the player. Synthetic replay is a transport test, separate from the explicitly selected Keyboard Demo.

## Real BLE when hardware exists
Create a local Python 3.12 environment: `python -m venv .venv`. Install `.venv/Scripts/python.exe -m pip install -r requirements-ble.txt`. Run `.venv/Scripts/python.exe Tools/Phase2Bridge/unity_bridge.py --scan`, then use `--address DEVICE_ADDRESS`. Press B in the game to select Phase 2 input.

Firmware must have the selected grip shape calibrated and capture started via USB `start`. The bridge does not calibrate or restart firmware. Do not change shape/calibration while capturing. BLE reconnect requires the firmware capture-start procedure again.

Force: UDP **127.0.0.1:27818**. Vision: **27817**. Only one running Editor Play session or player can own these ports. Unity validates version/source/shape/flags/limits/sequence/sample timestamps/calibration/connection. milliNewtons are converted to Newtons; socket queue delay consumes the 500 ms freshness budget. Repeated packets cannot earn hold progress. Lost input releases the held object. Connection/calibration changes end the previous grip.

## Camera mode
Selecting Module 01 in the menu starts the project-local MediaPipe worker automatically when `.venv`, its model, and webcam 0 are available. The bridge sends 21 landmarks to UDP 27817 and an annotated preview to 27819. The sole detected physical hand selects the matching VirtualGrasp side. Frames expire after 500 ms including processing and Unity queue time; an invalid or missing frame releases a held object and the HUD shows `--` rather than a live closure measurement. Press G to summon the selected object to a freshly tracked hand, then close the hand to keep it. See HANDS_AND_MEDIAPIPE_AR.md. A single RGB camera estimates pose and depth; it cannot measure force or guarantee exact 3D alignment. Keyboard Demo does not require a camera.

No physical BLE device was tested. R4 uses different contact-force sensors and requires different sensor firmware/calibration. This integration does not establish R4 compatibility or clinical safety. BLE v1 lacks a per-packet calibration epoch; the bridge confirms metadata before releasing buffered samples, but changing calibration during capture remains prohibited.
