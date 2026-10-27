"""Camera or video -> hybrid hand tracker (RF-DETR + YOLO26-pose + MediaPipe) -> Unity loopback UDP.

Same command line, packet schema (LandmarkFrame v1) and VISION_STATUS lines as mediapipe_bridge.py, so Unity's
receiver and launcher work unchanged. Landmarks are image coordinates with MediaPipe's relative depth; this is not
metric depth, finger force or a clinically validated pose.

  .venv-cv/Scripts/python.exe Tools/Vision/hybrid/hybrid_bridge.py --camera 0 --low-latency
"""
import argparse
import json
import socket
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import env  # noqa: E402,F401  (caches on drive E, Windows certificate store)
import cv2  # noqa: E402

from pipeline import DEFAULT_MP, DEFAULT_RFDETR, DEFAULT_YOLO, HybridTracker, payload  # noqa: E402

BONES = ((0, 1), (1, 2), (2, 3), (3, 4), (0, 5), (5, 6), (6, 7), (7, 8), (5, 9), (9, 10), (10, 11), (11, 12),
         (9, 13), (13, 14), (14, 15), (15, 16), (13, 17), (17, 18), (18, 19), (19, 20), (17, 0))


def parse_args():
    p = argparse.ArgumentParser(description=__doc__)
    src = p.add_mutually_exclusive_group()
    src.add_argument("--camera", type=int, default=0); src.add_argument("--video", type=Path)
    p.add_argument("--model", type=Path, default=DEFAULT_MP, help="MediaPipe hand_landmarker.task")
    p.add_argument("--yolo", type=Path, default=DEFAULT_YOLO); p.add_argument("--rfdetr", type=Path, default=DEFAULT_RFDETR)
    p.add_argument("--port", type=int, default=27817); p.add_argument("--preview-port", type=int, default=0)
    p.add_argument("--low-latency", action="store_true"); p.add_argument("--exposure", type=float, default=-5)
    p.add_argument("--seconds", type=float, default=0); p.add_argument("--mirror", action="store_true")
    p.add_argument("--swap-handedness", action="store_true"); p.add_argument("--self-test", action="store_true")
    p.add_argument("--cpu", action="store_true", help="Run YOLO on the CPU (slow; for machines without CUDA)")
    return p.parse_args()


def main():
    args = parse_args()
    if not args.model.is_file():
        raise SystemExit(f"MediaPipe model missing: {args.model}")
    tracker = HybridTracker(args.yolo, args.rfdetr, args.model, device="cpu" if args.cpu else "cuda",
                            half=not args.cpu, swap_handedness=args.swap_handedness)
    if args.self_test:
        import numpy as np
        tracker.process(np.zeros((480, 640, 3), np.uint8), 0.0)
        print(f"MODEL_SELF_TEST_OK tracker={tracker.name} yolo={tracker.yolo is not None} rfdetr={tracker.rfdetr is not None}")
        return
    cap = cv2.VideoCapture(str(args.video)) if args.video else cv2.VideoCapture(args.camera, cv2.CAP_DSHOW if sys.platform == "win32" else cv2.CAP_ANY)
    if not cap.isOpened():
        print("VISION_STATUS " + json.dumps({"state": "camera_error", "camera": args.camera}), flush=True)
        raise SystemExit("Could not open camera/video. Check device access and index.")
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640); cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480); cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
    if args.low_latency and not args.video:
        cap.set(cv2.CAP_PROP_FPS, 30)
        if sys.platform == "win32":
            cap.set(cv2.CAP_PROP_AUTO_EXPOSURE, .25); cap.set(cv2.CAP_PROP_EXPOSURE, args.exposure)
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    preview = socket.socket(socket.AF_INET, socket.SOCK_DGRAM) if args.preview_port else None
    started = status_at = time.monotonic(); next_preview = 0.0
    frames = detections = sequence = interval_frames = interval_hands = 0
    print("VISION_STATUS " + json.dumps({"state": "camera_open", "camera": args.camera, "frames": 0, "detections": 0, "fps": 0, "tracker": tracker.name}), flush=True)
    try:
        while args.seconds <= 0 or time.monotonic() - started < args.seconds:
            ok, frame = cap.read()
            if not ok:
                if args.video:
                    break
                if time.monotonic() - status_at >= 1:
                    print("VISION_STATUS " + json.dumps({"state": "camera_stalled", "camera": args.camera, "frames": frames, "detections": detections, "fps": 0}), flush=True)
                    status_at = time.monotonic()
                time.sleep(0.02); continue
            if args.mirror:
                frame = cv2.flip(frame, 1)
            captured = time.monotonic()
            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            hands = tracker.process(rgb, captured)
            frames += 1; interval_frames += 1
            h, w = rgb.shape[:2]
            for hand in hands:
                sequence += 1
                packet = payload(hand, w, h, captured, sequence, tracker.name, max(0.0, (time.monotonic() - captured) * 1000))
                sock.sendto(json.dumps(packet, separators=(",", ":")).encode("utf-8"), ("127.0.0.1", args.port))
                detections += 1; interval_hands += 1
            now = time.monotonic()
            if preview is not None and now >= next_preview:
                small = cv2.resize(frame, (256, 192), interpolation=cv2.INTER_AREA); sx, sy = 256 / w, 192 / h
                for hand in hands:
                    pts = [(int(x * sx), int(y * sy)) for x, y in hand.xy]
                    for a, b in BONES:
                        cv2.line(small, pts[a], pts[b], (100, 235, 240), 2)
                    for p in pts:
                        cv2.circle(small, p, 2, (60, 255, 110), -1)
                    cv2.putText(small, hand.side.upper(), (max(0, min(205, pts[0][0])), max(15, min(180, pts[0][1]))), cv2.FONT_HERSHEY_SIMPLEX, .45, (80, 250, 250), 1, cv2.LINE_AA)
                ok_jpg, jpg = cv2.imencode(".jpg", small, [cv2.IMWRITE_JPEG_QUALITY, 50])
                if ok_jpg and len(jpg) < 60000:
                    preview.sendto(jpg.tobytes(), ("127.0.0.1", args.preview_port))
                next_preview = now + .2
            if now - status_at >= 1:
                print("VISION_STATUS " + json.dumps({"state": "camera_open", "camera": args.camera, "frames": frames, "detections": detections,
                                                     "fps": round(interval_frames / (now - status_at), 1), "hands": interval_hands,
                                                     "ms": round(tracker.timings.get("total_ms", 0), 1)}), flush=True)
                status_at = now; interval_frames = interval_hands = 0
    except KeyboardInterrupt:
        pass
    finally:
        cap.release(); sock.close(); tracker.close()
        if preview is not None:
            preview.close()
    elapsed = max(time.monotonic() - started, 1e-6)
    print(f"Processed {frames} frames ({frames / elapsed:.1f}/s), sent {detections} hand detections to loopback")


if __name__ == "__main__":
    main()
