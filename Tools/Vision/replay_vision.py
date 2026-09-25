"""Deterministic two-hand vision transport fixture. This is not camera inference."""
import argparse
import json
import math
import socket
import time


def hand_points(cx: float, phase: float, left: bool) -> list[dict]:
    spread = -1 if left else 1
    wrist = (cx, 0.79, 0.02)
    points = [wrist]
    # Thumb, index, middle, ring, little in MediaPipe order.
    for finger, lateral in enumerate((-0.09, -0.046, 0, 0.043, 0.079)):
        base_y = 0.71 if finger == 0 else 0.62 + abs(finger - 2) * 0.015
        bend = 0.11 * (0.5 + 0.5 * math.sin(phase + finger * 0.32))
        for joint in range(4):
            fraction = (joint + 1) / 4
            x = cx + spread * (lateral + (0.045 * fraction if finger == 0 else 0))
            y = base_y - fraction * (0.13 if finger == 0 else 0.22) + bend * fraction * fraction
            z = -0.02 - bend * fraction
            points.append((x, y, z))
    return [{"x": max(0, min(1, x)), "y": max(0, min(1, y)), "z": z}
            for x, y, z in points]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seconds", type=float, default=10)
    parser.add_argument("--port", type=int, default=27817)
    args = parser.parse_args()
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    start = time.monotonic()
    next_frame = start
    sequence = 0
    try:
        while time.monotonic() - start < args.seconds:
            now = time.monotonic()
            phase = (now - start) * 2
            for left, center in ((True, 0.34), (False, 0.66)):
                sequence += 1
                payload = {
                    "schemaVersion": 1,
                    "source": "synthetic",
                    "captureSeconds": now,
                    "ageMs": 0,
                    "sequence": sequence,
                    "handedness": "Left" if left else "Right",
                    "confidence": 0.95,
                    "gesture": "unknown",
                    "gestureConfidence": 0.0,
                    "landmarks": hand_points(center + 0.03 * math.sin(phase), phase, left),
                }
                sock.sendto(json.dumps(payload, separators=(",", ":")).encode("utf-8"),
                            ("127.0.0.1", args.port))
            next_frame += 1 / 30
            time.sleep(max(0, next_frame - time.monotonic()))
    finally:
        sock.close()
    print(f"Sent {sequence} synthetic hand frames to 127.0.0.1:{args.port}")


if __name__ == "__main__":
    main()
