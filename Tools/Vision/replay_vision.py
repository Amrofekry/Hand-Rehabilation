"""Send deterministic synthetic hand landmarks to the Unity loopback adapter.

This is a transport/UI fixture, not a MediaPipe or YOLO inference implementation.
"""

import argparse
import json
import math
import socket
import time


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--seconds", type=float, default=10)
    parser.add_argument("--port", type=int, default=27817)
    args = parser.parse_args()
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    start = time.monotonic()
    next_frame = start
    count = 0
    try:
        while time.monotonic() - start < args.seconds:
            now = time.monotonic()
            center_x = 0.5 + 0.15 * math.sin((now - start) * 1.3)
            points = [
                {"x": max(0, min(1, center_x + 0.008 * (i % 5))),
                 "y": 0.5 + 0.016 * (i // 5), "z": 0.0}
                for i in range(21)
            ]
            frame = {
                "schemaVersion": 1,
                "source": "synthetic",
                "captureSeconds": now,
                "confidence": 0.95,
                "gesture": "unknown",
                "gestureConfidence": 0.0,
                "landmarks": points,
            }
            sock.sendto(json.dumps(frame, separators=(",", ":")).encode("utf-8"),
                        ("127.0.0.1", args.port))
            count += 1
            next_frame += 1 / 30
            time.sleep(max(0, next_frame - time.monotonic()))
    finally:
        sock.close()
    print(f"Sent {count} synthetic frames to 127.0.0.1:{args.port}")


if __name__ == "__main__":
    main()
