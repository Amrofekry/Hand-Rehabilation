"""Hybrid hand tracker: RF-DETR finds hands, YOLO26-pose gives 2D joints, MediaPipe gives depth and handedness.

Per frame:
1. YOLO26-pose (GPU) on the full frame: hand boxes and 21 2D joints with per-joint confidence.
2. RF-DETR Nano (GPU), every few frames or whenever YOLO lost a hand: confirms boxes (drops weak YOLO false
   positives) and re-acquires hands YOLO missed.
3. MediaPipe Hand Landmarker (CPU) on a square crop around each hand: 21 joints with relative depth, world
   landmarks and handedness.
4. Per-joint confidence fusion of YOLO and MediaPipe 2D, depth from MediaPipe, jump rejection, a Left/Right track
   per hand, One Euro smoothing. MediaPipe-only is the fallback when a model or GPU is missing.
"""
from __future__ import annotations

import time
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np

from core import Estimate, HandTracker, OneEuro, box_iou, box_of, fuse, hand_scale, reject_jumps

ROOT = Path(__file__).resolve().parents[3]
DEFAULT_YOLO = ROOT / "CVTraining" / "weights" / "yolo26n-hand.pt"
DEFAULT_RFDETR = ROOT / "CVTraining" / "weights" / "rfdetr-nano-hand.pth"
DEFAULT_MP = ROOT / "models" / "hand_landmarker.task"


@dataclass
class HandResult:
    side: str
    xy: np.ndarray            # (21, 2) pixels, smoothed
    z: np.ndarray             # (21,) MediaPipe relative depth in full-frame width units
    conf: np.ndarray          # (21,) fused confidence
    world: np.ndarray | None  # (21, 3) MediaPipe world landmarks (metres, estimated), if any
    box: tuple
    side_score: float
    sources: list[str] = field(default_factory=list)
    overruled: int = 0
    held: int = 0


class HybridTracker:
    def __init__(self, yolo=DEFAULT_YOLO, rfdetr=DEFAULT_RFDETR, mp_model=DEFAULT_MP, device="cuda", half=True,
                 detector_every=3, yolo_conf=0.35, rfdetr_conf=0.45, swap_handedness=False, use_yolo=True, use_rfdetr=True):
        import mediapipe as mp
        self.mp = mp
        options = mp.tasks.vision.HandLandmarkerOptions(
            base_options=mp.tasks.BaseOptions(model_asset_path=str(mp_model)),
            running_mode=mp.tasks.vision.RunningMode.IMAGE, num_hands=1,
            min_hand_detection_confidence=0.5, min_hand_presence_confidence=0.5)
        self.landmarker = mp.tasks.vision.HandLandmarker.create_from_options(options)
        self.yolo = self.rfdetr = None
        self.device, self.half = device, half
        if use_yolo and Path(yolo).exists():
            from ultralytics import YOLO
            self.yolo = YOLO(str(yolo))
        if use_rfdetr and Path(rfdetr).exists():
            from rfdetr import RFDETRNano
            self.rfdetr = RFDETRNano(pretrain_weights=str(rfdetr))
            try:
                self.rfdetr.optimize_for_inference()
            except Exception:  # older or newer rfdetr without it: plain inference still works
                pass
        self.detector_every, self.yolo_conf, self.rfdetr_conf, self.swap = detector_every, yolo_conf, rfdetr_conf, swap_handedness
        self.tracker = HandTracker()
        self.frame_index = 0
        self.timings: dict[str, float] = {}
        self.counters = {"rfdetr_runs": 0, "rfdetr_added": 0, "rfdetr_dropped": 0, "mp_misses": 0, "jumps_held": 0, "overruled": 0}

    @property
    def name(self) -> str:
        parts = ["mediapipe-hybrid-" + self.mp.__version__]
        if self.yolo: parts.append("yolo26")
        if self.rfdetr: parts.append("rfdetr")
        return "+".join(parts)

    # ---------- models ----------
    def _yolo(self, rgb):
        if self.yolo is None:
            return []
        r = self.yolo.predict(rgb[..., ::-1], imgsz=640, conf=self.yolo_conf, device=0 if self.device == "cuda" else "cpu",
                              half=self.half, verbose=False, max_det=4)[0]
        out = []
        if r.keypoints is None or r.boxes is None:
            return out
        kxy = r.keypoints.xy.cpu().numpy(); kc = r.keypoints.conf.cpu().numpy() if r.keypoints.conf is not None else np.ones(kxy.shape[:2])
        for i, b in enumerate(r.boxes.xyxy.cpu().numpy()):
            out.append((tuple(float(v) for v in b), float(r.boxes.conf[i]), Estimate(kxy[i].astype(float), kc[i].astype(float), source="yolo26")))
        return out

    def _rfdetr(self, rgb):
        from PIL import Image
        d = self.rfdetr.predict(Image.fromarray(rgb), threshold=self.rfdetr_conf)
        return [(tuple(float(v) for v in b), float(c)) for b, c in zip(d.xyxy, d.confidence)]

    def _mediapipe(self, rgb, box, w, h):
        """MediaPipe on a square crop 1.5x the box; returns an Estimate in full-frame pixels, world points and handedness."""
        x0, y0, x1, y1 = box
        cx, cy, s = (x0 + x1) / 2, (y0 + y1) / 2, max(x1 - x0, y1 - y0) * 1.5
        ax0, ay0 = int(max(0, cx - s / 2)), int(max(0, cy - s / 2)); ax1, ay1 = int(min(w, cx + s / 2)), int(min(h, cy + s / 2))
        if ax1 - ax0 < 16 or ay1 - ay0 < 16:
            return None
        crop = np.ascontiguousarray(rgb[ay0:ay1, ax0:ax1])
        res = self.landmarker.detect(self.mp.Image(image_format=self.mp.ImageFormat.SRGB, data=crop))
        if not res.hand_landmarks:
            return None
        lm = res.hand_landmarks[0]; cw, ch = ax1 - ax0, ay1 - ay0
        xy = np.array([[ax0 + p.x * cw, ay0 + p.y * ch] for p in lm])
        z = np.array([p.z for p in lm]) * cw / w           # MediaPipe z is in crop-width units; express in frame width
        presence = res.handedness[0][0].score if res.handedness else 0.5
        world = np.array([[p.x, p.y, p.z] for p in res.hand_world_landmarks[0]]) if res.hand_world_landmarks else None
        side = res.handedness[0][0].category_name if res.handedness else None
        if side and self.swap:
            side = "Left" if side == "Right" else "Right"
        return Estimate(xy, np.full(21, float(presence)), z, "mediapipe"), world, side, float(presence)

    # ---------- per frame ----------
    def process(self, rgb: np.ndarray, t: float) -> list[HandResult]:
        h, w = rgb.shape[:2]; tick = time.perf_counter(); self.frame_index += 1
        yolo = self._yolo(rgb); t_yolo = time.perf_counter()
        boxes = [(b, c, e) for b, c, e in yolo]
        lost = len(self.tracker.tracks) > len(boxes)
        if self.rfdetr is not None and (self.frame_index % self.detector_every == 0 or lost or not boxes):
            self.counters["rfdetr_runs"] += 1
            found = self._rfdetr(rgb)
            # Weak YOLO boxes that RF-DETR does not see are dropped; confident ones are kept.
            kept = [(b, c, e) for b, c, e in boxes if c >= 0.6 or any(box_iou(b, f) > 0.3 for f, _ in found)]
            self.counters["rfdetr_dropped"] += len(boxes) - len(kept); boxes = kept
            for f, c in found:
                if all(box_iou(f, b) <= 0.3 for b, _, _ in boxes):
                    boxes.append((f, c, None)); self.counters["rfdetr_added"] += 1
        elif self.yolo is None and self.rfdetr is None or not boxes:
            # Fallback: continue known tracks from their last box (MediaPipe re-finds the hand inside it).
            boxes = [(tr.box, 0.5, None) for tr in self.tracker.tracks.values()]
            if not boxes:
                boxes = [((0, 0, w, h), 0.3, None)]
        t_det = time.perf_counter()
        boxes = sorted(boxes, key=lambda x: -x[1])[:2]
        candidates = []
        for box, score, yest in boxes:
            m = self._mediapipe(rgb, box, w, h)
            if m is None:
                self.counters["mp_misses"] += 1
                if yest is None:
                    continue
            ests = [e for e in (yest, m[0] if m else None) if e is not None]
            xy, conf, z, overruled = fuse(ests)
            self.counters["overruled"] += overruled
            candidates.append(dict(box=box_of(xy, 0.15), xy=xy, conf=conf, z=z, world=m[1] if m else None,
                                   side=m[2] if m else None, side_score=m[3] if m else 0.0, sources=[e.source for e in ests], overruled=overruled))
        t_mp = time.perf_counter()
        sides = self.tracker.assign([c["box"] for c in candidates], [c["side"] for c in candidates], [c["side_score"] for c in candidates], t, w)
        results = []
        for c, side in zip(candidates, sides):
            if side is None:
                continue
            track = self.tracker.tracks[side]
            prev_t = getattr(track, "t", None)
            xy, held = reject_jumps(track.xy, c["xy"], (t - prev_t) if prev_t else 0)
            self.counters["jumps_held"] += held
            smooth = track.filter(xy, t, hand_scale(xy))
            z = c["z"] if c["z"] is not None else getattr(track, "z", np.zeros(21))
            zs = track.z_filter(z, t, 1.0)
            track.xy, track.t, track.z = smooth, t, zs
            results.append(HandResult(side, smooth, zs, c["conf"], c["world"], c["box"], c["side_score"], c["sources"], c["overruled"], held))
        done = time.perf_counter()
        self.timings = {"yolo_ms": (t_yolo - tick) * 1000, "detector_ms": (t_det - t_yolo) * 1000, "mediapipe_ms": (t_mp - t_det) * 1000,
                        "fuse_ms": (done - t_mp) * 1000, "total_ms": (done - tick) * 1000}
        return results

    def close(self):
        self.landmarker.close()


def payload(r: HandResult, w: int, h: int, captured: float, sequence: int, source: str, age_ms: float) -> dict:
    """Unity LandmarkFrame schema v1, the same fields the MediaPipe bridge sends."""
    lm = [{"x": float(x / w), "y": float(y / h), "z": float(z)} for (x, y), z in zip(r.xy, r.z)]
    if any(not np.isfinite(v) for p in lm for v in p.values()):
        raise ValueError("non-finite landmark")
    out = {"schemaVersion": 1, "source": source, "captureSeconds": captured, "ageMs": age_ms, "sequence": sequence,
           "handedness": r.side, "confidence": float(np.clip(r.conf.mean(), 0, 1)), "gesture": "unknown", "gestureConfidence": 0.0,
           "landmarks": lm, "imageWidth": w, "imageHeight": h}
    if r.world is not None and np.all(np.isfinite(r.world)) and np.abs(r.world).max() <= 2:
        out["worldLandmarks"] = [{"x": float(a), "y": float(b), "z": float(c)} for a, b, c in r.world]
    return out
