"""Local camera/video -> MediaPipe's two 21-landmark hands -> Unity loopback UDP.

This bridge sends landmark coordinates only; it does not estimate metric depth,
individual finger force, or a clinically validated hand pose.
"""
import argparse
import json
import socket
import time
from pathlib import Path

DEFAULT_MODEL = Path(__file__).resolve().parents[2] / "models" / "hand_landmarker.task"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group()
    source.add_argument("--camera", type=int, default=0, help="Local camera index (default: 0)")
    source.add_argument("--video", type=Path, help="Local video file instead of a camera")
    parser.add_argument("--model", type=Path, default=DEFAULT_MODEL)
    parser.add_argument("--port", type=int, default=27817)
    parser.add_argument("--preview-port", type=int, default=0, help="Optional local JPEG preview for Unity")
    parser.add_argument("--seconds", type=float, default=0, help="0 runs until Ctrl+C")
    parser.add_argument("--mirror", action="store_true", help="Mirror image before inference, for a selfie camera")
    parser.add_argument("--swap-handedness", action="store_true", help="Swap left/right labels if camera mounting reverses them")
    parser.add_argument("--self-test", action="store_true", help="Initialize model on blank image; no camera")
    return parser.parse_args()


def make_payload(hand, category, captured: float, sequence: int, version: str, swap: bool, age_ms: float = 0) -> dict:
    label = category.category_name
    if label not in ("Left", "Right"):
        raise ValueError(f"Unknown hand label: {label}")
    if swap:
        label = "Left" if label == "Right" else "Right"
    return {
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
        num_hands=2,
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

        # DirectShow avoids the long MSMF startup/stall seen on some Windows webcams.
        capture = (cv2.VideoCapture(str(args.video)) if args.video else
                   cv2.VideoCapture(args.camera, cv2.CAP_DSHOW if __import__("sys").platform == "win32" else cv2.CAP_ANY))
        if not capture.isOpened():
            print('VISION_STATUS '+json.dumps({'state':'camera_error','camera':args.camera}), flush=True)
            raise SystemExit("Could not open camera/video. Check device access and index.")
        capture.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        capture.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        preview_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM) if args.preview_port else None
        next_preview = 0.0
        started = time.monotonic()
        last_timestamp_ms = -1
        frames = detections = sequence = 0
        status_at = started
        interval_frames = interval_detections = 0
        print('VISION_STATUS '+json.dumps({'state':'camera_open','camera':args.camera,'frames':0,'detections':0,'fps':0}), flush=True)
        try:
            while args.seconds <= 0 or time.monotonic() - started < args.seconds:
                ok, frame = capture.read()
                if not ok:
                    if args.video:
                        break
                    if time.monotonic() - status_at >= 1:
                        print('VISION_STATUS '+json.dumps({'state':'camera_stalled','camera':args.camera,'frames':frames,'detections':detections,'fps':0}), flush=True)
                        status_at = time.monotonic()
                    continue
                if args.mirror:
                    frame = cv2.flip(frame, 1)
                captured = time.monotonic()
                timestamp_ms = max(int((captured - started) * 1000), last_timestamp_ms + 1)
                last_timestamp_ms = timestamp_ms
                rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                image = mp.Image(image_format=mp.ImageFormat.SRGB, data=np.ascontiguousarray(rgb))
                result = landmarker.detect_for_video(image, timestamp_ms)
                frames += 1
                interval_frames += 1
                # No detection packet: Unity expires that hand independently after 500 ms.
                for hand, handedness in zip(result.hand_landmarks, result.handedness):
                    if len(hand) != 21 or not handedness:
                        continue
                    sequence += 1
                    payload = make_payload(hand, handedness[0], captured, sequence,
                                           mp.__version__, args.swap_handedness,
                                           max(0.0, (time.monotonic() - captured) * 1000))
                    sock.sendto(json.dumps(payload, separators=(",", ":")).encode("utf-8"),
                                ("127.0.0.1", args.port))
                    detections += 1
                    interval_detections += 1
                now = time.monotonic()
                if preview_sock is not None and now >= next_preview:
                    preview = cv2.resize(frame, (320, 240), interpolation=cv2.INTER_AREA)
                    for hand in result.hand_landmarks:
                        for a, b in ((0,1),(1,2),(2,3),(3,4),(0,5),(5,6),(6,7),(7,8),
                                     (5,9),(9,10),(10,11),(11,12),(9,13),(13,14),(14,15),(15,16),
                                     (13,17),(17,18),(18,19),(19,20),(17,0)):
                            start = (int(hand[a].x*320), int(hand[a].y*240))
                            end = (int(hand[b].x*320), int(hand[b].y*240))
                            cv2.line(preview, start, end, (100, 235, 240), 2)
                        for point in hand:
                            cv2.circle(preview, (int(point.x*320), int(point.y*240)), 3, (60, 255, 110), -1)
                    encoded, jpeg = cv2.imencode('.jpg', preview, [cv2.IMWRITE_JPEG_QUALITY, 58])
                    if encoded and len(jpeg) < 60000:
                        preview_sock.sendto(jpeg.tobytes(), ("127.0.0.1", args.preview_port))
                    next_preview = now + .125
                if now - status_at >= 1:
                    print('VISION_STATUS '+json.dumps({'state':'camera_open','camera':args.camera,'frames':frames,'detections':detections,'fps':round(interval_frames/(now-status_at),1),'hands':interval_detections}), flush=True)
                    status_at = now
                    interval_frames = interval_detections = 0
        except KeyboardInterrupt:
            pass
        finally:
            capture.release()
            sock.close()
            if preview_sock is not None:
                preview_sock.close()
        elapsed = max(time.monotonic() - started, 1e-6)
        print(f"Processed {frames} frames ({frames / elapsed:.1f}/s), sent {detections} hand detections to loopback")


if __name__ == "__main__":
    main()
