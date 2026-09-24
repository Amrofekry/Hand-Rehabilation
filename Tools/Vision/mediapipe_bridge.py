"""Local webcam/video -> MediaPipe hand landmarks -> Unity loopback UDP.

No images leave this process. This bridge does not perform YOLO gesture recognition.
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
    source.add_argument("--camera", type=int, default=0, help="Open a local camera by index (default: 0)")
    source.add_argument("--video", type=Path, help="Read a local video file instead of a camera")
    parser.add_argument("--model", type=Path, default=DEFAULT_MODEL)
    parser.add_argument("--port", type=int, default=27817)
    parser.add_argument("--seconds", type=float, default=0, help="Stop after this many seconds; 0 runs until Ctrl+C")
    parser.add_argument("--self-test", action="store_true", help="Initialize model and infer on a blank image; no camera")
    return parser.parse_args()


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
        num_hands=1,
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

        capture = cv2.VideoCapture(str(args.video) if args.video else args.camera)
        if not capture.isOpened():
            raise SystemExit("Could not open the selected camera/video source. Check device access and index.")
        capture.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        capture.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        started = time.monotonic()
        last_timestamp_ms = -1
        frames = detections = 0
        try:
            while args.seconds <= 0 or time.monotonic() - started < args.seconds:
                ok, frame = capture.read()
                if not ok:
                    if args.video:
                        break
                    continue
                captured = time.monotonic()
                timestamp_ms = max(int((captured - started) * 1000), last_timestamp_ms + 1)
                last_timestamp_ms = timestamp_ms
                rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                image = mp.Image(image_format=mp.ImageFormat.SRGB, data=np.ascontiguousarray(rgb))
                result = landmarker.detect_for_video(image, timestamp_ms)
                frames += 1
                if not result.hand_landmarks:
                    continue  # Unity marks the last detection stale after 500 ms.
                detections += 1
                hand = result.hand_landmarks[0]
                payload = {
                    "schemaVersion": 1,
                    "source": f"mediapipe-{mp.__version__}",
                    "captureSeconds": captured,
                    # The task does not expose a calibrated per-hand probability here.
                    # 1 means it passed the configured detection/presence thresholds.
                    "confidence": 1.0,
                    "gesture": "unknown",
                    "gestureConfidence": 0.0,
                    "landmarks": [
                        {"x": point.x, "y": point.y, "z": point.z} for point in hand
                    ],
                }
                sock.sendto(json.dumps(payload, separators=(",", ":")).encode("utf-8"),
                            ("127.0.0.1", args.port))
        except KeyboardInterrupt:
            pass
        finally:
            capture.release()
            sock.close()
        elapsed = max(time.monotonic() - started, 1e-6)
        print(f"Processed {frames} frames ({frames / elapsed:.1f}/s), detected {detections}; sent landmarks only to loopback")


if __name__ == "__main__":
    main()
