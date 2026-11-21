# Phone camera (Android): through Windows Phone Link, or over USB

## Through Windows Phone Link (no cable, no developer mode), 2026-11-21
**What it is:** Phone Link's *connected camera* shows the phone's camera to Windows as a normal camera, named "<phone> (Windows Virtual Camera)". The game finds it by name.
- **In the game:** Settings → CAMERA: PHONE (PHONE LINK).
- **From the command line:** `mediapipe_bridge.py --camera-name "Windows Virtual Camera"`.
- **Requires:** the small `pygrabber` package (in `requirements-vision.txt`), which lists the camera names.

**MEASURED on this laptop (no hand in view):** 1280x720 at 30.3-30.5 fps, on both the DirectShow and Media Foundation backends. The laptop webcam gives 640x480. The delay from a screen flip to the PC is **not measured yet**: it needs `Tools/Vision/camera_latency.py` with the phone pointed at the screen.

**Settings that are yours to set** (they are phone or Windows settings, not the game's):
- **In Phone Link's camera panel on the PC:**
  - the **back** camera;
  - the widest lens that still shows your hand large;
  - effects and background blur **off**.
- **On the phone:** keep the screen on while it is the camera.
- **Windows Settings → Bluetooth & devices → Cameras → the phone camera:**
  - brightness and contrast at default;
  - Video HDR **off**;
  - Windows Studio Effects (background blur, eye contact, auto framing) **off**. They move or crop the picture under the hand.

**For hand tracking:** a steady exposure, no auto framing, and the phone placed so your resting hand is in the middle of the picture.

# Phone camera over USB (Android)

USB status (2026-11-21): **available, not the default**. The laptop webcam remains the main camera, because on the tested phone the USB camera stream is too slow to steer a hand.

## How it works
- **Transport:** `Tools/Vision/phone_camera.py` starts `scrcpy-server` v5.0.1 (Apache-2.0) on the phone over adb in camera mode (Android 12+). It streams raw H.264 over an `adb forward` socket, and OpenCV's FFmpeg decodes it single-threaded.
  - Nothing is installed on the phone.
  - USB debugging must be on.
- **Tools:** in `E:\GP\codex\.local\tools\scrcpy\scrcpy-win64-v5.0.1` (checksum verified). Override the location with `GRIPREHAB_SCRCPY_DIR`.
- **Bridge:** `mediapipe_bridge.py --phone [--phone-facing back] [--phone-size 1280x720] [--phone-fps 30]`.
- **Game:** Settings → `CAMERA: LAPTOP WEBCAM / PHONE (USB)` (saved), or `-vision-phone`.
- **IP Webcam:** the free app is also supported (`IPWebcamCamera`: its settings over HTTP through the same cable).
- **Command-line tools:**
  - `phone_camera.py --list` lists the cameras.
  - `phone_camera.py --probe 5` measures fps.
  - `camera_latency.py` measures the delay from a screen flip to the PC.

## Measured on an OPPO CPH2625 (Android 16), MEASURED
| Camera | FOV (h × v) | Frame rate | Delay from screen flip to PC |
|---|---|---|---|
| 0 main (auto focus) | 68° × 54° | 30.2 fps (high-speed 120/240 requested: 23-30 fps, not passed through) | ≈ 610 ms |
| 2 ultrawide (fixed focus) | 95° × 79° | 30.2 fps | ≈ 610 ms |
| Test window alone (no camera) | — | — | ≈ 60 ms |

- **Where the delay is:** inside the phone. Raw H.264 frames already arrive 300-640 ms late, and decoding takes about 2 ms per frame.
- **What did not help:** changing size, bitrate, H.265 or encoder options.
- **The phone's own Camera app:** it cannot be mirrored instead, because it is blacked out in screen capture.
- **Built-in webcam mode:** Android's own USB "Webcam" mode (`DeviceAsWebcam`) is not offered on this model (`ro.usb.uvc.enabled` unset).
- **IP Webcam:** its server did not answer over USB in this session. Not measured yet.

## Settings worth using when a phone path is fast enough
| Setting | Choice | Why |
|---|---|---|
| Frame rate | Highest the path really delivers | Less camera delay |
| Exposure | Short | No motion blur |
| Focus | Fixed or locked at arm's length | No focus hunting |
| Stabilization | Off | It adds delay and moves the image under the hand |
| Lens | The widest that keeps the hand at least 15 % of the frame | The hand stays in frame when reaching |
| Filters / beauty | Off | They change the landmarks |

- **Resolution:** 1280x720 is enough. MediaPipe works at 192/224 px, so higher resolution adds delay, not accuracy.
