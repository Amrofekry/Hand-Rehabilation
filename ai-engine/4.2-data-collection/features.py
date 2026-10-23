"""Local, privacy-preserving demo feature windows; no camera frames are stored."""

from __future__ import annotations

from collections import deque
from dataclasses import dataclass
from math import isfinite, sqrt
from typing import Deque


@dataclass(frozen=True)
class TelemetryPoint:
    session_id: str
    at_s: float                 # common receiver-monotonic clock
    measured_at_s: float        # age includes transport and queueing
    source: str                 # hardware, keyboard_demo or synthetic_replay
    synthetic: bool
    shape_id: int
    calibration_revision: int
    force_n: float | None
    closure: float | None       # dimensionless camera estimate
    rom_deg: float | None
    hand_valid: bool


@dataclass(frozen=True)
class FeatureWindow:
    session_id: str
    start_s: float
    end_s: float
    source: str
    synthetic: bool
    shape_id: int
    calibration_revision: int
    valid: bool
    reason: str
    force_mean_n: float | None
    force_peak_n: float | None
    closure_variability: float | None
    rom_span_deg: float | None


class FeatureWindowBuilder:
    def __init__(self, duration_s: float = 2.0, max_age_s: float = 0.5) -> None:
        if not isfinite(duration_s) or duration_s <= 0 or not isfinite(max_age_s) or max_age_s <= 0:
            raise ValueError("window settings must be finite and positive")
        self.duration_s = duration_s
        self.max_age_s = max_age_s
        self._points: Deque[TelemetryPoint] = deque()
        self._identity: tuple | None = None
        self._last_at = float("-inf")

    def add(self, point: TelemetryPoint) -> FeatureWindow | None:
        if not point.session_id or point.source not in {"hardware", "keyboard_demo", "synthetic_replay"}:
            raise ValueError("invalid session or source")
        if point.synthetic != (point.source != "hardware"):
            raise ValueError("synthetic provenance conflicts with source")
        if not isfinite(point.at_s) or point.at_s <= self._last_at or not isfinite(point.measured_at_s):
            raise ValueError("non-monotonic or non-finite timestamp")
        if point.measured_at_s > point.at_s:
            raise ValueError("capture lies in the future")
        if point.shape_id not in (1, 2, 3) or point.calibration_revision < 0:
            raise ValueError("invalid shape or calibration identity")
        for value in (point.force_n, point.closure, point.rom_deg):
            if value is not None and not isfinite(value):
                raise ValueError("non-finite feature")
        if point.force_n is not None and point.force_n < 0:
            raise ValueError("negative force")
        if point.closure is not None and not 0 <= point.closure <= 1:
            raise ValueError("closure outside [0,1]")

        identity = (point.session_id, point.source, point.synthetic,
                    point.shape_id, point.calibration_revision)
        if identity != self._identity:
            self._points.clear()  # never blend different session/shape/calibration
            self._identity = identity
        self._last_at = point.at_s
        self._points.append(point)
        while self._points and point.at_s - self._points[0].at_s > self.duration_s:
            self._points.popleft()
        if len(self._points) < 2 or point.at_s - self._points[0].at_s < self.duration_s * 0.9:
            return None

        points = tuple(self._points)
        if any(p.at_s - p.measured_at_s >= self.max_age_s for p in points):
            return self._window(points, "stale input")
        if any(b.at_s - a.at_s >= self.max_age_s for a, b in zip(points, points[1:])):
            return self._window(points, "sensor gap")
        if any(p.force_n is None or p.closure is None or p.rom_deg is None or not p.hand_valid
               for p in points):
            return self._window(points, "missing force or tracking")
        forces = [p.force_n for p in points]
        closures = [p.closure for p in points]
        rom = [p.rom_deg for p in points]
        mean_closure = sum(closures) / len(closures)
        variability = sqrt(sum((v - mean_closure) ** 2 for v in closures) / len(closures))
        return FeatureWindow(point.session_id, points[0].at_s, point.at_s, point.source,
                             point.synthetic, point.shape_id, point.calibration_revision,
                             True, "valid", sum(forces) / len(forces), max(forces),
                             variability, max(rom) - min(rom))

    @staticmethod
    def _window(points: tuple[TelemetryPoint, ...], reason: str) -> FeatureWindow:
        first, last = points[0], points[-1]
        return FeatureWindow(first.session_id, first.at_s, last.at_s, first.source,
                             first.synthetic, first.shape_id, first.calibration_revision,
                             False, reason, None, None, None, None)
