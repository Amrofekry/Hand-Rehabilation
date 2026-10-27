"""Records the local test clip for the hybrid hand tracker (2 minutes, guided). Stays on this computer.

  .venv/Scripts/python.exe Tools/Vision/hybrid/record_clip.py [--camera 0] [--name myclip]

A window shows what to do in each 15 s segment; press SPACE to start, Q to stop early. The saved video has no
overlay. Writes CVTraining/clips/<name>.mp4 and <name>.json (segment times). CVTraining/ is git-ignored.
"""
import argparse
import json
import sys
import time
from pathlib import Path

import cv2

ROOT = Path(__file__).resolve().parents[3]
SEGMENTS = [
    ("still", "Hold your RIGHT hand still, fingers open, palm to the camera"),
    ("slow-open-close", "Slowly open and close your hand into a fist"),
    ("fast-open-close", "Open and close your hand quickly"),
    ("pinch", "Touch your thumb to each fingertip in turn"),
    ("rotate", "Turn your wrist: palm, side, back of the hand, and back"),
    ("near-far", "Move your hand slowly toward the camera, then away"),
    ("two-hands", "Show BOTH hands, open and close them"),
    ("low-light", "Dim the light (lamp off or screen down), keep moving the hand"),
]
SECONDS = 15


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--camera", type=int, default=0)
    parser.add_argument("--name", default=time.strftime("clip-%Y%m%d-%H%M%S"))
    args = parser.parse_args()
    out_dir = ROOT / "CVTraining" / "clips"; out_dir.mkdir(parents=True, exist_ok=True)
    cap = cv2.VideoCapture(args.camera, cv2.CAP_DSHOW if sys.platform == "win32" else cv2.CAP_ANY)
    if not cap.isOpened():
        raise SystemExit("Could not open the camera. Close other apps using it and check Windows camera permission.")
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640); cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480); cap.set(cv2.CAP_PROP_FPS, 30)
    w, h = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH)), int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    writer = None; started = None; marks = []; frames = 0
    print("Press SPACE in the video window to start recording, Q to stop.")
    while True:
        ok, frame = cap.read()
        if not ok:
            continue
        view = frame.copy()
        now = time.monotonic()
        if started is None:
            cv2.putText(view, "SPACE = start (2 min guided)   Q = quit", (12, 30), cv2.FONT_HERSHEY_SIMPLEX, .65, (0, 255, 255), 2)
        else:
            elapsed = now - started; index = int(elapsed // SECONDS)
            if index >= len(SEGMENTS):
                break
            if len(marks) <= index:
                marks.append({"segment": SEGMENTS[index][0], "startFrame": frames, "startSeconds": round(elapsed, 3)})
            writer.write(frame); frames += 1
            left = SECONDS - (elapsed % SECONDS)
            cv2.rectangle(view, (0, 0), (w, 64), (0, 0, 0), -1)
            cv2.putText(view, f"{index + 1}/{len(SEGMENTS)}  {SEGMENTS[index][1]}", (10, 26), cv2.FONT_HERSHEY_SIMPLEX, .55, (0, 255, 255), 2)
            cv2.putText(view, f"REC  {left:4.1f}s", (10, 54), cv2.FONT_HERSHEY_SIMPLEX, .6, (0, 0, 255), 2)
        cv2.imshow("GripRehab test clip", view)
        key = cv2.waitKey(1) & 0xFF
        if key == ord("q"):
            break
        if key == ord(" ") and started is None:
            fps = cap.get(cv2.CAP_PROP_FPS) or 30
            writer = cv2.VideoWriter(str(out_dir / f"{args.name}.mp4"), cv2.VideoWriter_fourcc(*"mp4v"), fps, (w, h))
            started = time.monotonic()
    duration = time.monotonic() - started if started else 0
    cap.release(); cv2.destroyAllWindows()
    if writer is None:
        print("Nothing recorded."); return
    writer.release()
    meta = {"name": args.name, "width": w, "height": h, "frames": frames, "seconds": round(duration, 2),
            "measuredFps": round(frames / duration, 2) if duration else 0, "segments": marks,
            "note": "Local test clip recorded with the user's consent; not committed to git."}
    (out_dir / f"{args.name}.json").write_text(json.dumps(meta, indent=2), encoding="utf-8")
    print(f"Saved {out_dir / (args.name + '.mp4')}: {frames} frames in {duration:.1f}s")


if __name__ == "__main__":
    main()
