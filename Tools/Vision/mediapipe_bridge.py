"""Local camera/video -> MediaPipe's two 21-landmark hands -> Unity loopback UDP.

This bridge sends landmark coordinates only; it does not estimate metric depth,
individual finger force, or a clinically validated hand pose.
"""
import argparse
import json
import math
import socket
import time
from pathlib import Path

DEFAULT_MODEL = Path(__file__).resolve().parents[2] / "models" / "hand_landmarker.task"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group()
    source.add_argument("--camera", type=int, default=0, help="Local camera index (default: 0)")
    source.add_argument("--video", type=Path, help="Local video file instead of a camera")
    source.add_argument("--camera-name", help="Pick the camera whose Windows name contains this text, e.g. 'Windows Virtual Camera' (the phone through Phone Link)")
    parser.add_argument("--fallback-webcam", action="store_true", help="If no camera matches --camera-name, use --camera (the laptop webcam) instead of stopping")
    parser.add_argument("--fallback-camera", type=int, default=0, help="Webcam index used by --fallback-webcam")
    parser.add_argument("--camera-fps", type=int, default=30, help="Frame rate requested from a camera picked by name")
    source.add_argument("--phone", action="store_true", help="Android phone camera over USB (adb + scrcpy-server); see phone_camera.py")
    parser.add_argument("--phone-facing", default="back", choices=("back", "front", "external"))
    parser.add_argument("--phone-size", default="1280x720", help="Phone camera size WxH")
    parser.add_argument("--phone-fps", type=int, default=60)
    parser.add_argument("--phone-high-speed", action="store_true", help="Constrained high-speed mode (listed size/fps only)")
    parser.add_argument("--phone-zoom", type=float, default=0, help="Phone zoom; below 1 picks a wider lens where allowed")
    parser.add_argument("--no-world", action="store_true", help="Do not send MediaPipe world landmarks (depth and finger angles then use the image)")
    parser.add_argument("--max-hands", type=int, default=2, choices=(1, 2), help="1 is faster when only one hand plays")
    parser.add_argument("--model", type=Path, default=DEFAULT_MODEL)
    parser.add_argument("--port", type=int, default=27817)
    parser.add_argument("--preview-port", type=int, default=0, help="Optional local JPEG preview for Unity")
    parser.add_argument("--low-latency", action="store_true", help="Request 30 FPS and fixed webcam exposure when supported")
    parser.add_argument("--exposure", type=float, default=-5, help="DirectShow exposure used with --low-latency")
    parser.add_argument("--seconds", type=float, default=0, help="0 runs until Ctrl+C")
    parser.add_argument("--mirror", action="store_true", help="Mirror image before inference, for a selfie camera")
    parser.add_argument("--swap-handedness", action="store_true", help="Swap left/right labels if camera mounting reverses them")
    parser.add_argument("--self-test", action="store_true", help="Initialize model on blank image; no camera")
    parser.add_argument("--record", type=Path, help="Hand Lab: also write every frame and hand packet (no images) to this JSONL file")
    return parser.parse_args()


class HandednessFilter:
    """Steady left/right label for a single visible hand. MediaPipe flips the label on a few frames (a real-hand Hand Lab
    session, 2026-11-19: 41 'Left' among 2550 'Right' frames of one right hand), and every flip moved the game to the other
    hand. With one hand in view, a new label is taken only after it holds for `frames` frames in a row; when the hand is
    gone for `forget` seconds, or two hands are in view, the labels pass through unchanged."""

    def __init__(self, frames: int = 6, forget: float = 0.5):
        self.frames, self.forget = frames, forget
        self.label, self.pending, self.count, self.seen = None, None, 0, -1e9

    def update(self, label: str, now: float, hands: int = 1) -> str:
        if hands != 1 or self.label is None or now - self.seen > self.forget:
            self.label, self.pending, self.count = label, None, 0
        elif label != self.label:
            self.count = self.count + 1 if label == self.pending else 1
            self.pending = label
            if self.count >= self.frames:
                self.label, self.pending, self.count = label, None, 0
        else:
            self.pending, self.count = None, 0
        self.seen = now
        return self.label


def find_camera(name: str):
    """DirectShow index of the first camera whose name contains `name` (case-insensitive); None if none or unavailable."""
    try:
        from pygrabber.dshow_graph import FilterGraph
        for index, device in enumerate(FilterGraph().get_input_devices()):
            if name.lower() in device.lower():
                return index
    except Exception:
        return None
    return None


def make_payload(hand, category, captured: float, sequence: int, version: str, swap: bool,
                 age_ms: float = 0, *, world_hand=None, image_width: int = 0, image_height: int = 0) -> dict:
    label = category.category_name
    if label not in ("Left", "Right"):
        raise ValueError(f"Unknown hand label: {label}")
    if swap:
        label = "Left" if label == "Right" else "Right"
    # Reject values that would poison Unity's pose before they reach the socket.
    if len(hand) != 21 or any(not math.isfinite(v) for p in hand for v in (p.x, p.y, p.z)):
        raise ValueError("Expected 21 finite image landmarks")
    if not math.isfinite(age_ms) or age_ms < 0 or not math.isfinite(category.score):
        raise ValueError("Invalid frame age or handedness score")
    if (image_width or image_height) and not (1 <= image_width <= 8192 and 1 <= image_height <= 8192):
        raise ValueError("Invalid image dimensions")
    payload = {
        "schemaVersion": 1,
        "source": f"mediapipe-{version}",
        "captureSeconds": captured,
        "ageMs": age_ms,
        "sequence": sequence,
        "handedness": label,
        # Handedness classification score is not a per-joint tracking accuracy guarantee.
        "confidence": max(0.0, min(1.0, category.score)),
        "gesture": "unknown",
        "gestureConfidence": 0.0,
        "landmarks": [{"x": p.x, "y": p.y, "z": p.z} for p in hand],
    }
    # Optional schema-v1 extensions; Unity's LandmarkFrame already accepts both.
    if image_width and image_height:
        payload.update(imageWidth=image_width, imageHeight=image_height)
    if world_hand is not None:
        if len(world_hand) != 21 or any(not math.isfinite(v) or abs(v) > 2
                                       for p in world_hand for v in (p.x, p.y, p.z)):
            raise ValueError("Expected 21 finite estimated world landmarks")
        payload["worldLandmarks"] = [{"x": p.x, "y": p.y, "z": p.z} for p in world_hand]
    return payload


def main() -> None:
    args = parse_args()
    if not args.model.is_file():
        raise SystemExit(f"MediaPipe model missing: {args.model}")
    import cv2
    import mediapipe as mp
    import numpy as np

    options = mp.tasks.vision.HandLandmarkerOptions(
        base_options=mp.tasks.BaseOptions(model_asset_path=str(args.model)),
        running_mode=mp.tasks.vision.RunningMode.IMAGE if args.self_test else mp.tasks.vision.RunningMode.VIDEO,
        num_hands=args.max_hands,
        min_hand_detection_confidence=0.65,
        min_hand_presence_confidence=0.65,
        min_tracking_confidence=0.65,
    )
    with mp.tasks.vision.HandLandmarker.create_from_options(options) as landmarker:
        if args.self_test:
            blank = np.zeros((256, 256, 3), dtype=np.uint8)
            result = landmarker.detect(mp.Image(image_format=mp.ImageFormat.SRGB, data=blank))
            print(f"MODEL_SELF_TEST_OK hands={len(result.hand_landmarks)} mediapipe={mp.__version__}")
            return

        if args.phone:
            from phone_camera import PhoneCamera, PhoneCameraConfig
            try:
                capture = PhoneCamera(PhoneCameraConfig(facing=args.phone_facing, size=args.phone_size, fps=args.phone_fps,
                                                        high_speed=args.phone_high_speed, zoom=args.phone_zoom)).open()
            except Exception as error:  # a plain reason for the game's camera message (no phone, not authorised, ...)
                print('VISION_STATUS '+json.dumps({'state':'camera_error','camera':-1,'detail':str(error)}), flush=True)
                raise SystemExit(f"Phone camera: {error}")
        elif args.camera_name and (index := find_camera(args.camera_name)) is not None:
            # A phone app's camera (Camo, Phone Link) by name, so the index never has to be guessed.
            capture = cv2.VideoCapture(index, cv2.CAP_DSHOW)
            capture.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
            capture.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
            capture.set(cv2.CAP_PROP_FPS, max(15, min(60, args.camera_fps)))
        elif args.camera_name and not args.fallback_webcam:
            print('VISION_STATUS '+json.dumps({'state':'camera_error','camera':-1,'detail':f"No camera named '{args.camera_name}': start the phone camera app (Camo or Phone Link) on the phone and the PC."}), flush=True)
            raise SystemExit(f"No camera named {args.camera_name}")
        else:
            if args.camera_name:  # not connected: the laptop webcam keeps the game playable
                print(f"VISION_NOTE no camera named '{args.camera_name}': using the laptop webcam", flush=True)
                args.camera_name = None; args.camera = args.fallback_camera
            # DirectShow avoids the long MSMF startup/stall seen on some Windows webcams.
            capture = (cv2.VideoCapture(str(args.video)) if args.video else
                       cv2.VideoCapture(args.camera, cv2.CAP_DSHOW if __import__("sys").platform == "win32" else cv2.CAP_ANY))
        if not capture.isOpened():
            print('VISION_STATUS '+json.dumps({'state':'camera_error','camera':args.camera}), flush=True)
            raise SystemExit("Could not open camera/video. Check device access and index.")
        if not args.phone and not args.camera_name:
            capture.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
            capture.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
            capture.set(cv2.CAP_PROP_BUFFERSIZE, 1)
        if args.low_latency and not args.video and not args.phone and not args.camera_name:
            capture.set(cv2.CAP_PROP_FPS, 30)
            if __import__("sys").platform == "win32":
                # A fixed exposure avoids the 15 FPS auto-exposure mode on this laptop.
                # Unsupported webcams simply ignore these requests.
                capture.set(cv2.CAP_PROP_AUTO_EXPOSURE, .25)
                capture.set(cv2.CAP_PROP_EXPOSURE, args.exposure)
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        preview_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM) if args.preview_port else None
        import sys
        sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "ai-engine" / "src"))
        from fatigue_ai.camera import ResearchCamera
        research_camera = ResearchCamera(Path(__file__).resolve().parents[2])
        next_preview = 0.0
        started = time.monotonic()
        last_timestamp_ms = -1
        frames = detections = sequence = 0
        status_at = started
        interval_frames = interval_detections = 0
        print('VISION_STATUS '+json.dumps({'state':'camera_open','camera':args.camera,'frames':0,'detections':0,'fps':0}), flush=True)
        # Hand Lab recording: landmark numbers and timings only, never an image (the face is not stored).
        record = open(args.record, "w", encoding="utf-8") if args.record else None
        handedness_filter = HandednessFilter()
        # Live sources are read on their own thread and only the newest frame is tracked (2026-11-20): a frame never
        # waits behind an older one, and "captured" is when the frame arrived, not when tracking started on it.
        from phone_camera import LatestFrameReader
        reader = None if args.video else LatestFrameReader(capture)
        try:
            while args.seconds <= 0 or time.monotonic() - started < args.seconds:
                if reader is not None:
                    ok, frame, captured = reader.read()
                else:
                    ok, frame = capture.read()
                    captured = time.monotonic()
                if not ok:
                    if args.video:
                        break
                    if time.monotonic() - status_at >= 1:
                        print('VISION_STATUS '+json.dumps({'state':'camera_stalled','camera':args.camera,'frames':frames,'detections':detections,'fps':0}), flush=True)
                        status_at = time.monotonic()
                    time.sleep(0.02)  # A stalled camera must not spin a CPU core (audit A-007).
                    continue
                if args.mirror:
                    frame = cv2.flip(frame, 1)
                research_camera.submit(frame, captured)
                timestamp_ms = max(int((captured - started) * 1000), last_timestamp_ms + 1)
                last_timestamp_ms = timestamp_ms
                rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                image = mp.Image(image_format=mp.ImageFormat.SRGB, data=np.ascontiguousarray(rgb))
                result = landmarker.detect_for_video(image, timestamp_ms)
                detected = time.monotonic()
                frames += 1
                if record is not None:
                    record.write(json.dumps({"kind": "frame", "frame": frames, "captured": captured, "inferMs": (detected - captured) * 1000,
                                             "width": int(frame.shape[1]), "height": int(frame.shape[0]), "hands": len(result.hand_landmarks)},
                                            separators=(",", ":")) + "\n")
                interval_frames += 1
                # No detection packet: Unity expires that hand independently after 500 ms.
                for hand, handedness in zip(result.hand_landmarks, result.handedness):
                    if len(hand) != 21 or not handedness:
                        continue
                    sequence += 1
                    # World landmarks (metres, about the hand's centre) let the game tell distance from tilt (HandDepth).
                    hand_index = list(result.hand_landmarks).index(hand)
                    world_hand = None if args.no_world else result.hand_world_landmarks[hand_index] if hand_index < len(result.hand_world_landmarks) else None
                    try:
                        payload = make_payload(hand, handedness[0], captured, sequence, mp.__version__, args.swap_handedness,
                                               max(0.0, (time.monotonic() - captured) * 1000), world_hand=world_hand,
                                               image_width=int(frame.shape[1]), image_height=int(frame.shape[0]))
                    except ValueError:  # an implausible world pose: send the image landmarks alone
                        payload = make_payload(hand, handedness[0], captured, sequence, mp.__version__, args.swap_handedness,
                                               max(0.0, (time.monotonic() - captured) * 1000),
                                               image_width=int(frame.shape[1]), image_height=int(frame.shape[0]))
                    payload["handedness"] = handedness_filter.update(payload["handedness"], captured, len(result.hand_landmarks))
                    sock.sendto(json.dumps(payload, separators=(",", ":")).encode("utf-8"),
                                ("127.0.0.1", args.port))
                    if record is not None:
                        index = list(result.hand_landmarks).index(hand)
                        world = result.hand_world_landmarks[index] if index < len(result.hand_world_landmarks) else None
                        record.write(json.dumps({"kind": "hand", "sent": time.monotonic(), "payload": payload,
                                                 "width": int(frame.shape[1]), "height": int(frame.shape[0]),
                                                 "world": [[p.x, p.y, p.z] for p in world] if world is not None else None},
                                                separators=(",", ":")) + "\n")
                    detections += 1
                    interval_detections += 1
                now = time.monotonic()
                if preview_sock is not None and now >= next_preview:
                    preview = cv2.resize(frame, (256, 192), interpolation=cv2.INTER_AREA)
                    for hand_index, hand in enumerate(result.hand_landmarks):
                        for a, b in ((0,1),(1,2),(2,3),(3,4),(0,5),(5,6),(6,7),(7,8),
                                     (5,9),(9,10),(10,11),(11,12),(9,13),(13,14),(14,15),(15,16),
                                     (13,17),(17,18),(18,19),(19,20),(17,0)):
                            start = (int(hand[a].x*256), int(hand[a].y*192))
                            end = (int(hand[b].x*256), int(hand[b].y*192))
                            cv2.line(preview, start, end, (100, 235, 240), 2)
                        for point in hand:
                            cv2.circle(preview, (int(point.x*256), int(point.y*192)), 2, (60, 255, 110), -1)
                        if hand_index < len(result.handedness) and result.handedness[hand_index]:
                            side = result.handedness[hand_index][0].category_name
                            if args.swap_handedness:
                                side = "Left" if side == "Right" else "Right"
                            cv2.putText(preview, side.upper(), (max(0, min(205, int(hand[0].x*256))),
                                        max(15, min(180, int(hand[0].y*192)))),
                                        cv2.FONT_HERSHEY_SIMPLEX, .45, (80, 250, 250), 1, cv2.LINE_AA)
                    encoded, jpeg = cv2.imencode('.jpg', preview, [cv2.IMWRITE_JPEG_QUALITY, 50])
                    if encoded and len(jpeg) < 60000:
                        preview_sock.sendto(jpeg.tobytes(), ("127.0.0.1", args.preview_port))
                    next_preview = now + .2
                if now - status_at >= 1:
                    print('VISION_STATUS '+json.dumps({'state':'camera_open','camera':args.camera,'frames':frames,'detections':detections,'fps':round(interval_frames/(now-status_at),1),'hands':interval_detections}), flush=True)
                    status_at = now
                    interval_frames = interval_detections = 0
        except KeyboardInterrupt:
            pass
        finally:
            if record is not None:
                record.close()
            research_camera.close()
            if reader is not None:
                reader.stop()
            capture.release()
            sock.close()
            if preview_sock is not None:
                preview_sock.close()
        elapsed = max(time.monotonic() - started, 1e-6)
        print(f"Processed {frames} frames ({frames / elapsed:.1f}/s), sent {detections} hand detections to loopback")


if __name__ == "__main__":
    main()
