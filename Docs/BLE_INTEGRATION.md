# 6.1 Phase 2 connection — standalone apartment project

Target: Nano 33 IoT + HX711 and existing BLE v1 packets, documented in BLE_PROTOCOL.md. `Tools/Phase2Bridge` contains byte-identical copies of `unity_bridge.py`, `monitor.py`, and `protocol.py` from Phase 2; SOURCE_MANIFEST.json records hashes. No runtime file depends on the old project path.

## Keyboard and mouse — no setup
Press Play. The source starts as KEYBOARD + MOUSE DEMO. Walk with WASD, aim with the mouse, E grabs, Q drops, F sits/stands. Hold Space or left mouse to squeeze. Mouse wheel or +/- changes demo force. B is an explicit input-mode switch. No automatic fallback exists.

## Software BLE replay in Editor
1. Stop Play. Select **03 · First person patient** in Hierarchy.
2. In **Live Force Receiver**, enable **Allow Synthetic In Editor** temporarily.
3. Press Play, then **B** to select hardware input.
4. Run `.\.venv\Scripts\python.exe Tools/Phase2Bridge/unity_bridge.py --demo --seconds 30` from this project folder.
5. The HUD must show **SYNTHETIC REPLAY**, then 18 N. Aim at an object and press E.
6. Stop the sender: within the 500 ms freshness budget plus a frame the object releases, force becomes invalid and hold progress stops. Stop Play, disable replay, then save.

A Windows player rejects synthetic packets unless launched with `-rehab-force-replay`; the Editor checkbox is not compiled into the player. Synthetic replay is a transport test, separate from the default keyboard demo.

## Real BLE when hardware exists
Create a local Python 3.12 environment: `python -m venv .venv`. Install `.venv/Scripts/python.exe -m pip install -r requirements-ble.txt`. Run `.venv/Scripts/python.exe Tools/Phase2Bridge/unity_bridge.py --scan`, then use `--address DEVICE_ADDRESS`. Press B in the game to select Phase 2 input.

Firmware must have the selected grip shape calibrated and capture started via USB `start`. The bridge does not calibrate or restart firmware. Do not change shape/calibration while capturing. BLE reconnect requires the firmware capture-start procedure again.

Force: UDP **127.0.0.1:27818**. Vision: **27817**. Only one running Editor Play session or player can own these ports. Unity validates version/source/shape/flags/limits/sequence/sample timestamps/calibration/connection. milliNewtons are converted to Newtons; socket queue delay consumes the 500 ms freshness budget. Repeated packets cannot earn hold progress. Lost input releases the held object. Connection/calibration changes end the previous grip.

## Optional camera
Use a separate Python environment with `requirements-vision.txt`, then `Tools/Vision/mediapipe_bridge.py --camera 0`. The bundled model path is relative to this project. Independent 21-point frames animate the two hidden input rigs using handedness labels, while only the selected vendor hand is visible; each input expires separately after 500 ms including Unity queue time, then blends to explicitly labelled demo animation. See HANDS_AND_MEDIAPIPE_AR.md. The mapping is approximate and needs calibration/validation on an actual camera. Monocular image landmarks do not provide a validated metric hand pose. No camera is required for the keyboard demo.

No physical BLE device was tested. R4 uses different contact-force sensors and requires different sensor firmware/calibration. This integration does not establish R4 compatibility or clinical safety. BLE v1 lacks a per-packet calibration epoch; the bridge confirms metadata before releasing buffered samples, but changing calibration during capture remains prohibited.
