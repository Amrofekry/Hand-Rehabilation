"""Model-free core of the hybrid hand tracker: One Euro filtering, per-joint fusion, outlier rejection and
two-hand track assignment. Pure numpy so it is unit-tested without a camera, GPU or model.

Coordinates are image pixels unless a name says otherwise. 21 joints in MediaPipe order
(0 wrist, 1-4 thumb, 5-8 index, 9-12 middle, 13-16 ring, 17-20 little).
"""
from __future__ import annotations

import math
from dataclasses import dataclass, field

import numpy as np

JOINTS = 21
BONES = ((0, 1), (1, 2), (2, 3), (3, 4), (0, 5), (5, 6), (6, 7), (7, 8), (5, 9), (9, 10), (10, 11), (11, 12),
         (9, 13), (13, 14), (14, 15), (15, 16), (13, 17), (17, 18), (18, 19), (19, 20), (0, 17))
PALM = (0, 5, 9, 13, 17)


def hand_scale(points: np.ndarray) -> float:
    """Hand size in the same units as points: wrist to middle-finger base plus palm width, robust to finger pose."""
    p = np.asarray(points, dtype=float)
    return float(np.linalg.norm(p[9, :2] - p[0, :2]) + np.linalg.norm(p[5, :2] - p[17, :2])) or 1.0


class OneEuro:
    """One Euro filter (Casiez, Roussel and Vogel, CHI 2012) over an array of values.

    Little smoothing when the signal moves fast (low lag), strong smoothing when it is still (low jitter).
    min_cutoff in Hz sets jitter at rest; beta raises the cutoff with speed; d_cutoff filters the speed estimate.
    """

    def __init__(self, min_cutoff: float = 1.2, beta: float = 0.02, d_cutoff: float = 1.0):
        self.min_cutoff, self.beta, self.d_cutoff = min_cutoff, beta, d_cutoff
        self.x = self.dx = None
        self.t = None

    @staticmethod
    def _alpha(cutoff, dt):
        tau = 1.0 / (2 * math.pi * cutoff)
        return 1.0 / (1.0 + tau / dt)

    def reset(self):
        self.x = self.dx = self.t = None

    def __call__(self, x: np.ndarray, t: float, scale: float = 1.0) -> np.ndarray:
        x = np.asarray(x, dtype=float)
        if self.x is None or self.t is None or t <= self.t or x.shape != self.x.shape:
            self.x, self.dx, self.t = x.copy(), np.zeros_like(x), t
            return x.copy()
        dt = min(t - self.t, 0.5)
        dx = (x - self.x) / dt
        a_d = self._alpha(self.d_cutoff, dt)
        self.dx = a_d * dx + (1 - a_d) * self.dx
        # Speed is normalised by the hand size so the same settings work near and far from the camera.
        cutoff = self.min_cutoff + self.beta * np.abs(self.dx) / max(scale, 1e-6) * 100
        a = self._alpha(cutoff, dt)
        self.x = a * x + (1 - a) * self.x
        self.t = t
        return self.x.copy()


@dataclass
class Estimate:
    """One model's view of one hand: (21, 2) pixels, per-joint confidence 0..1, optional (21,) relative depth."""
    xy: np.ndarray
    conf: np.ndarray
    z: np.ndarray | None = None
    source: str = ""


def fuse(estimates: list[Estimate], disagree_fraction: float = 0.12) -> tuple[np.ndarray, np.ndarray, np.ndarray | None, int]:
    """Per-joint confidence-weighted fusion of 2D joints from several models.

    Where two models disagree by more than disagree_fraction of the hand size, the joint takes only the most
    confident model (averaging two different answers gives a point that is on neither finger).
    Depth comes from the models that have it (MediaPipe). Returns xy, conf, z and the number of overruled joints.
    """
    usable = [e for e in estimates if e is not None and e.xy is not None and np.all(np.isfinite(e.xy))]
    if not usable:
        raise ValueError("no estimate to fuse")
    if len(usable) == 1:
        e = usable[0]
        return e.xy.astype(float).copy(), np.clip(e.conf, 0, 1).astype(float), None if e.z is None else e.z.astype(float).copy(), 0
    xy = np.stack([e.xy for e in usable]).astype(float)              # (m, 21, 2)
    conf = np.clip(np.stack([e.conf for e in usable]).astype(float), 1e-3, 1)  # (m, 21)
    scale = max(hand_scale(xy[int(np.argmax(conf.mean(axis=1)))]), 1.0)
    out = (xy * conf[..., None]).sum(axis=0) / conf.sum(axis=0)[:, None]
    out_conf = conf.max(axis=0)
    spread = np.linalg.norm(xy - out[None], axis=2).max(axis=0)       # (21,)
    overruled = spread > disagree_fraction * scale
    if np.any(overruled):
        best = np.argmax(conf, axis=0)
        out[overruled] = xy[best[overruled], overruled]
        # Disagreement lowers trust in that joint.
        out_conf[overruled] *= 0.7
    depths = [e.z for e in usable if e.z is not None]
    z = None if not depths else np.mean(np.stack(depths), axis=0)
    return out, out_conf, z, int(overruled.sum())


def reject_jumps(previous: np.ndarray | None, current: np.ndarray, dt: float, max_speed_hands_per_s: float = 12.0) -> tuple[np.ndarray, int]:
    """Holds any joint that moved faster than a hand can (in hand sizes per second) since the last frame.

    A real hand rarely exceeds a few hand-lengths per second; a single-frame leap of a joint across the image is a
    model error (a finger snapping to the other hand, a background false positive).
    """
    if previous is None or dt <= 0:
        return current, 0
    scale = hand_scale(previous)
    limit = max_speed_hands_per_s * scale * min(dt, 0.2)
    step = np.linalg.norm(current - previous, axis=1)
    # A whole-hand move (every joint moving together) is real motion, not an outlier.
    whole = np.median(step)
    bad = (step - whole) > limit
    out = current.copy()
    out[bad] = previous[bad] + (current[bad] - previous[bad]) * (limit / np.maximum(step[bad], 1e-6))[:, None]
    return out, int(bad.sum())


def box_iou(a, b) -> float:
    ax0, ay0, ax1, ay1 = a; bx0, by0, bx1, by1 = b
    iw, ih = max(0.0, min(ax1, bx1) - max(ax0, bx0)), max(0.0, min(ay1, by1) - max(ay0, by0))
    inter = iw * ih
    union = (ax1 - ax0) * (ay1 - ay0) + (bx1 - bx0) * (by1 - by0) - inter
    return inter / union if union > 0 else 0.0


def box_of(xy: np.ndarray, pad: float = 0.25) -> tuple[float, float, float, float]:
    x0, y0 = xy.min(axis=0); x1, y1 = xy.max(axis=0)
    s = max(x1 - x0, y1 - y0) * (1 + 2 * pad)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    return cx - s / 2, cy - s / 2, cx + s / 2, cy + s / 2


@dataclass
class Track:
    side: str
    box: tuple
    last_seen: float
    xy: np.ndarray | None = None
    filter: OneEuro = field(default_factory=OneEuro)
    z_filter: OneEuro = field(default_factory=lambda: OneEuro(1.0, 0.01))
    side_votes: float = 0.0


class HandTracker:
    """Keeps at most one Left and one Right track. New boxes join the track they overlap most; handedness comes from
    MediaPipe when it is confident, and otherwise from the track, so a hand does not swap sides mid-motion."""

    def __init__(self, lost_after_s: float = 0.5, min_iou: float = 0.15):
        self.tracks: dict[str, Track] = {}
        self.lost_after_s, self.min_iou = lost_after_s, min_iou
        self.swaps_prevented = 0

    def expire(self, now: float):
        for side in [s for s, t in self.tracks.items() if now - t.last_seen > self.lost_after_s]:
            del self.tracks[side]

    def assign(self, boxes: list[tuple], sides: list[str | None], side_scores: list[float], now: float, frame_width: float) -> list[str]:
        """Returns a side ('Left'/'Right') for each box, at most one box per side."""
        self.expire(now)
        result: list[str | None] = [None] * len(boxes)
        taken = set()
        # 1. Continue existing tracks by overlap (strongest first).
        pairs = sorted(((box_iou(b, t.box), i, s) for i, b in enumerate(boxes) for s, t in self.tracks.items()), reverse=True)
        for iou, i, side in pairs:
            if iou < self.min_iou or result[i] is not None or side in taken:
                continue
            claimed = sides[i]
            if claimed and claimed != side and side_scores[i] >= 0.95 and claimed not in self.tracks:
                side = claimed  # a confident, uncontested label corrects an earlier wrong guess
            elif claimed and claimed != side:
                self.swaps_prevented += 1
            result[i] = side; taken.add(side)
        # 2. New hands take their claimed side; with no claim, the free side by image position
        #    (in an unmirrored camera the person's right hand appears on the image's left).
        for i, b in enumerate(boxes):
            if result[i] is not None:
                continue
            free = [s for s in ("Left", "Right") if s not in taken]
            if not free:
                continue
            side = sides[i] if sides[i] in free else free[0] if len(free) == 1 else ("Right" if (b[0] + b[2]) / 2 < frame_width / 2 else "Left")
            result[i] = side; taken.add(side)
        for i, side in enumerate(result):
            if side is None:
                continue
            t = self.tracks.get(side)
            if t is None:
                self.tracks[side] = Track(side=side, box=boxes[i], last_seen=now)
            else:
                t.box, t.last_seen = boxes[i], now
        return [s for s in result]


def bone_length_cv(sequence: list[np.ndarray]) -> float:
    """Mean coefficient of variation of each bone's length over a sequence (lower = steadier skeleton)."""
    if len(sequence) < 3:
        return float("nan")
    lengths = np.array([[np.linalg.norm(p[a] - p[b]) for a, b in BONES] for p in sequence])
    mean = lengths.mean(axis=0)
    return float(np.mean(lengths.std(axis=0) / np.maximum(mean, 1e-6)))


def jitter(sequence: list[np.ndarray]) -> float:
    """Median joint acceleration (second difference) in hand sizes per frame squared: high-frequency shake."""
    if len(sequence) < 3:
        return float("nan")
    s = np.stack(sequence)
    acc = np.linalg.norm(s[2:] - 2 * s[1:-1] + s[:-2], axis=2)
    scale = np.array([hand_scale(p) for p in s[1:-1]])[:, None]
    return float(np.median(acc / scale))
