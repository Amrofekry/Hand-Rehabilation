"""Compares the current MediaPipe pipeline with the hybrid tracker on the same frames.

  .venv-cv/Scripts/python.exe Tools/Vision/hybrid/benchmark.py --clip CVTraining/clips/myhand.mp4
  .venv-cv/Scripts/python.exe Tools/Vision/hybrid/benchmark.py --dataset 500     # labelled hand-keypoints test images

Clip (no ground truth): per segment, hands found per frame, jitter (shake), bone-length steadiness, handedness swaps,
and time per frame. Dataset: PCK@0.05/0.1 (share of joints within 5 % / 10 % of the hand-box size of the label) per model.
The dataset labels were produced by MediaPipe, so the dataset score measures agreement with MediaPipe, not truth.
Writes CVTraining/benchmarks/<name>.json.
"""
import argparse
import json
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import env  # noqa: E402,F401
import cv2  # noqa: E402
import numpy as np  # noqa: E402

from core import bone_length_cv, jitter  # noqa: E402
from pipeline import DEFAULT_MP, HybridTracker  # noqa: E402

WORK = env.WORK


class MediaPipeOnly:
    """The current bridge's configuration: VIDEO mode, 2 hands, 0.65 thresholds, full frame."""

    def __init__(self):
        import mediapipe as mp
        self.mp = mp
        o = mp.tasks.vision.HandLandmarkerOptions(base_options=mp.tasks.BaseOptions(model_asset_path=str(DEFAULT_MP)),
                                                  running_mode=mp.tasks.vision.RunningMode.VIDEO, num_hands=2,
                                                  min_hand_detection_confidence=.65, min_hand_presence_confidence=.65, min_tracking_confidence=.65)
        self.lm = mp.tasks.vision.HandLandmarker.create_from_options(o); self.ts = 0

    def process(self, rgb, t):
        self.ts = max(int(t * 1000), self.ts + 1)
        r = self.lm.detect_for_video(self.mp.Image(image_format=self.mp.ImageFormat.SRGB, data=np.ascontiguousarray(rgb)), self.ts)
        h, w = rgb.shape[:2]
        return [(hd[0].category_name, np.array([[p.x * w, p.y * h] for p in hand])) for hand, hd in zip(r.hand_landmarks, r.handedness) if hd]


def run_clip(clip: Path, tracker, label: str, segments):
    cap = cv2.VideoCapture(str(clip)); fps = cap.get(cv2.CAP_PROP_FPS) or 30
    frames, hands, times, tracks = 0, [], [], {"Left": [], "Right": []}
    swaps = 0; last_side_at = {}
    while True:
        ok, frame = cap.read()
        if not ok:
            break
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB); t = frames / fps
        tick = time.perf_counter(); out = tracker.process(rgb, t); times.append((time.perf_counter() - tick) * 1000)
        if isinstance(tracker, HybridTracker):
            out = [(r.side, r.xy) for r in out]
        hands.append(len(out))
        for side, xy in out:
            # A swap: the same hand position reported with the other label from one frame to the next.
            other = "Left" if side == "Right" else "Right"
            if other in last_side_at and last_side_at[other][0] == frames - 1 and np.linalg.norm(last_side_at[other][1] - xy[0]) < 40:
                swaps += 1
            last_side_at[side] = (frames, xy[0])
            tracks[side].append((frames, xy))
        frames += 1
    cap.release()
    report = {"model": label, "frames": frames, "msMean": round(float(np.mean(times)), 2), "msP95": round(float(np.percentile(times, 95)), 2),
              "fps": round(1000 / float(np.mean(times)), 1), "handednessSwaps": swaps, "segments": {}}
    bounds = [(s["segment"], s["startFrame"], (segments[i + 1]["startFrame"] if i + 1 < len(segments) else frames)) for i, s in enumerate(segments)] or [("all", 0, frames)]
    for name, a, b in bounds:
        seg_hands = hands[a:b]
        main = max(tracks, key=lambda s: sum(1 for f, _ in tracks[s] if a <= f < b))
        seq = [xy for f, xy in tracks[main] if a <= f < b]
        runs = _contiguous([f for f, _ in tracks[main] if a <= f < b])
        steady = [s for r in runs for s in [[xy for f, xy in tracks[main] if f in r]] if len(s) >= 3]
        report["segments"][name] = {
            "framesWithHand": round(float(np.mean([n > 0 for n in seg_hands])) if seg_hands else 0, 3),
            "framesWithTwoHands": round(float(np.mean([n > 1 for n in seg_hands])) if seg_hands else 0, 3),
            "jitter": round(float(np.nanmean([jitter(s) for s in steady])) if steady else float("nan"), 5),
            "boneLengthCV": round(float(np.nanmean([bone_length_cv(s) for s in steady])) if steady else float("nan"), 4),
            "framesUsed": len(seq)}
    return report


def _contiguous(frames):
    runs, cur = [], []
    for f in frames:
        if cur and f != cur[-1] + 1:
            runs.append(set(cur)); cur = []
        cur.append(f)
    if cur:
        runs.append(set(cur))
    return runs


def run_dataset(n: int):
    """PCK on the held-out test half (images never used for training either model)."""
    from pipeline import HybridTracker as H
    images = sorted((WORK / "datasets" / "hand-boxes-coco" / "test").glob("*.jpg"))[:n] or sorted((WORK / "datasets" / "hand-keypoints" / "images" / "val").glob("*.*"))[-n:]
    labels_dir = WORK / "datasets" / "hand-keypoints" / "labels" / "val"
    modes = {"mediapipe-only": H(use_yolo=False, use_rfdetr=False), "yolo26-only": None, "hybrid": H()}
    scores = {m: {"pck05": [], "pck10": []} for m in modes}
    for path in images:
        label = labels_dir / (path.stem + ".txt")
        if not label.exists():
            continue
        img = cv2.imread(str(path)); h, w = img.shape[:2]; rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
        truths = []
        for line in label.read_text().split("\n"):
            v = [float(x) for x in line.split()]
            if len(v) >= 5 + 63:
                k = np.array(v[5:5 + 63]).reshape(21, 3); truths.append((k[:, :2] * [w, h], max(v[3] * w, v[4] * h), k[:, 2] > 0))
        if not truths:
            continue
        for mode, tracker in modes.items():
            if mode == "yolo26-only":
                preds = [e.xy for _, _, e in modes["hybrid"]._yolo(rgb)]
            else:
                tracker.tracker.tracks.clear(); [setattr(tracker, "frame_index", 2)]  # fresh image: no tracking history
                preds = [r.xy for r in tracker.process(rgb, time.monotonic())]
            for xy_true, size, visible in truths:
                best = min(preds, key=lambda p: np.linalg.norm(p - xy_true, axis=1).mean(), default=None)
                d = np.full(21, np.inf) if best is None else np.linalg.norm(best - xy_true, axis=1)
                scores[mode]["pck05"].append(float(np.mean(d[visible] < .05 * size))); scores[mode]["pck10"].append(float(np.mean(d[visible] < .10 * size)))
    return {m: {k: round(float(np.mean(v)), 4) if v else None for k, v in s.items()} | {"hands": len(s["pck05"])} for m, s in scores.items()}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--clip", type=Path); p.add_argument("--dataset", type=int, default=0)
    args = p.parse_args()
    out_dir = WORK / "benchmarks"; out_dir.mkdir(exist_ok=True)
    if args.clip:
        clip = args.clip if args.clip.is_absolute() else env.ROOT / args.clip
        meta = clip.with_suffix(".json"); segments = json.loads(meta.read_text())["segments"] if meta.exists() else []
        result = {"clip": clip.name, "runs": [run_clip(clip, MediaPipeOnly(), "mediapipe-only (current)", segments),
                                             run_clip(clip, HybridTracker(), "hybrid", segments)]}
        (out_dir / f"{clip.stem}.json").write_text(json.dumps(result, indent=2)); print(json.dumps(result, indent=2))
    if args.dataset:
        result = run_dataset(args.dataset)
        (out_dir / "dataset-pck.json").write_text(json.dumps(result, indent=2)); print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
