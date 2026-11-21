"""Glass-to-PC camera delay: a window flips black/white, the camera looks at it, and the time from the flip to the
first frame that sees the change is measured (includes the screen's own refresh, about one frame).

  .venv/Scripts/python.exe Tools/Vision/camera_latency.py --phone --camera-id 2      # phone pointed at the screen
  .venv/Scripts/python.exe Tools/Vision/camera_latency.py --camera 1                 # a second webcam pointed at it
  .venv/Scripts/python.exe Tools/Vision/camera_latency.py --camera-name "Windows Virtual Camera"   # Phone Link camera

MEASURED on this computer; no image is saved (only the brightness of the frame's centre is read).
"""
from __future__ import annotations

import argparse
import statistics
import time

import cv2
import numpy as np

from phone_camera import IPWebcamCamera, LatestFrameReader, PhoneCamera, PhoneCameraConfig


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--phone", action="store_true")
    parser.add_argument("--ipwebcam", action="store_true", help="The IP Webcam app over USB (port 8080)")
    parser.add_argument("--camera-id", default="")
    parser.add_argument("--camera", type=int, default=0)
    parser.add_argument("--camera-name", default="", help="Pick the camera by (part of) its name, e.g. the Phone Link camera")
    parser.add_argument("--size", default="1280x720")
    parser.add_argument("--fps", type=int, default=30)
    parser.add_argument("--extra", nargs="*", default=[], help="More scrcpy-server key=value options")
    parser.add_argument("--flips", type=int, default=20)
    parser.add_argument("--jump", type=float, default=12, help="Brightness change (0-255) that counts as seeing the flip")
    args = parser.parse_args()
    if args.ipwebcam:
        source = IPWebcamCamera().open()
    elif args.phone:
        source = PhoneCamera(PhoneCameraConfig(camera_id=args.camera_id, size=args.size, fps=args.fps, extra=tuple(args.extra))).open()
    else:
        index = args.camera
        if args.camera_name:
            from mediapipe_bridge import find_camera
            found = find_camera(args.camera_name)
            if found is None:
                raise SystemExit(f"No camera named like {args.camera_name!r}")
            index = found
        source = cv2.VideoCapture(index, cv2.CAP_DSHOW)
        width, height = (int(v) for v in args.size.split("x"))
        source.set(cv2.CAP_PROP_FRAME_WIDTH, width); source.set(cv2.CAP_PROP_FRAME_HEIGHT, height); source.set(cv2.CAP_PROP_FPS, args.fps)
    reader = LatestFrameReader(source)
    window = "GripRehab camera delay"
    cv2.namedWindow(window, cv2.WINDOW_NORMAL)
    cv2.setWindowProperty(window, cv2.WND_PROP_FULLSCREEN, cv2.WINDOW_FULLSCREEN)
    black, white = np.zeros((360, 640, 3), np.uint8), np.full((360, 640, 3), 255, np.uint8)

    def grab():
        ok, frame, at = reader.read(timeout=.5)
        if not ok:
            return None
        small = cv2.resize(cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY), (64, 36), interpolation=cv2.INTER_AREA)
        return small.astype(np.float32), at

    def flip_and_wait(show_white, threshold=None):
        """Flip, then return (delay ms of the first frame that changed by threshold, the largest change seen)."""
        settle = time.monotonic() + .45 + np.random.rand() * .3  # irregular, so a flip never lines up with the camera clock
        while time.monotonic() < settle:
            cv2.waitKey(5)
        frames = [f for f in (grab() for _ in range(3)) if f]
        if not frames:
            return None, 0.0
        before = np.median(np.stack([f[0] for f in frames]), axis=0)
        cv2.imshow(window, white if show_white else black)
        cv2.waitKey(1)
        flipped = time.monotonic()
        first, peak = None, 0.0
        while time.monotonic() < flipped + .9:
            cv2.waitKey(1)  # Windows repaints an OpenCV window only inside waitKey
            got = grab()
            if not got or got[1] <= flipped:
                continue
            # The screen part of the picture changes most: the mean of the top 20 % of per-pixel changes.
            change = np.abs(got[0] - before).ravel()
            score = float(np.mean(np.partition(change, int(.8 * change.size))[int(.8 * change.size):]))
            peak = max(peak, score)
            if threshold is not None and first is None and score >= threshold:
                first = (got[1] - flipped) * 1000
        return first, peak

    # Auto-exposure re-balances a steady screen within a second, so each flip is seen as a jump from the frames just
    # before it. Four warm-up flips learn how big a real flip looks; only changes of at least half that count.
    cv2.imshow(window, black); cv2.waitKey(1); time.sleep(1.0)
    state, peaks = False, []
    for _ in range(4):
        state = not state
        peaks.append(flip_and_wait(state)[1])
    full = float(np.median(peaks))
    if full < 15:
        print(f"CAMERA_DELAY failed: the flips barely show in the picture (change {full:.0f}/255); point the camera at the screen")
        cv2.destroyAllWindows(); reader.stop(); source.release()
        return
    delays = []
    for _ in range(args.flips):
        state = not state
        delay, _ = flip_and_wait(state, threshold=full * .5)
        if delay is not None:
            delays.append(delay)
    print(f"flip size {full:.0f}/255 (warm-up peaks {[round(x) for x in peaks]})")
    cv2.destroyAllWindows()
    reader.stop()
    source.release()
    if not delays:
        print("CAMERA_DELAY failed: no flip was seen")
        return
    delays.sort()
    print(f"CAMERA_DELAY median={statistics.median(delays):.0f} ms p90={delays[int(.9 * (len(delays) - 1))]:.0f} ms "
          f"min={delays[0]:.0f} max={delays[-1]:.0f} n={len(delays)}/{args.flips} (includes the screen refresh)")


if __name__ == "__main__":
    main()
