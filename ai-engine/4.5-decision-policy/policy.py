"""Deterministic research-demo adaptation policy; not a clinical decision rule.

Inputs are feature *signs* from independent sensing categories. A camera's
closure estimate is not a force sign, and duplicate model features from the
same category do not count as separate fatigue evidence.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from math import isfinite
from typing import FrozenSet


class Signal(str, Enum):
    FORCE = "force"
    KINEMATIC = "kinematic"
    FACIAL = "facial"
    PHYSIOLOGICAL = "physiological"


class SessionStatus(str, Enum):
    ACTIVE = "active"
    WARNING = "warning_resume_or_quit"
    STOPPED = "safety_stopped"


@dataclass(frozen=True)
class DemoPolicySettings:
    version: str = "research-demo-1"
    min_level: int = 1
    max_level: int = 5
    initial_level: int = 1
    sign_persistence_s: float = 1.5
    stable_window_s: float = 8.0
    cooldown_s: float = 10.0
    input_max_age_s: float = 0.5

    def __post_init__(self) -> None:
        if not self.version or not (1 <= self.min_level <= self.initial_level <= self.max_level):
            raise ValueError("invalid demo difficulty settings")
        for name in ("sign_persistence_s", "stable_window_s", "cooldown_s", "input_max_age_s"):
            value = getattr(self, name)
            if not isfinite(value) or value <= 0:
                raise ValueError(f"{name} must be finite and positive")


@dataclass(frozen=True)
class Observation:
    at_s: float
    measured_at_s: float
    signs: FrozenSet[Signal]
    force_valid: bool
    hand_valid: bool
    model_valid: bool
    stable_potential: bool


@dataclass(frozen=True)
class Decision:
    status: SessionStatus
    level: int
    reason: str
    confirmed_signs: FrozenSet[Signal]
    settings_version: str


class AdaptivePolicy:
    def __init__(self, settings: DemoPolicySettings | None = None) -> None:
        self.settings = settings or DemoPolicySettings()
        self.status = SessionStatus.ACTIVE
        self.level = self.settings.initial_level
        self._last_at = float("-inf")
        self._last_change_at = float("-inf")
        self._stable_since: float | None = None
        self._sign_since: dict[Signal, float] = {}
        self._confirmed: FrozenSet[Signal] = frozenset()
        self._last_input_valid = False

    def step(self, observation: Observation) -> Decision:
        s = self.settings
        now = observation.at_s
        if not isfinite(now) or now <= self._last_at:
            raise ValueError("observation clock must increase monotonically")
        if not isfinite(observation.measured_at_s) or observation.measured_at_s > now:
            raise ValueError("invalid capture clock")
        if any(not isinstance(sign, Signal) for sign in observation.signs):
            raise ValueError("unknown signal category")
        self._last_at = now
        if self.status == SessionStatus.STOPPED:
            return self._result("safety stop latched")

        valid = (observation.force_valid and observation.hand_valid and
                 observation.model_valid and now - observation.measured_at_s < s.input_max_age_s)
        self._last_input_valid = valid
        if not valid:
            self._stable_since = None
            self._sign_since.clear()
            self._confirmed = frozenset()
            return self._result("missing, invalid or stale input; no escalation")

        for sign in tuple(self._sign_since):
            if sign not in observation.signs:
                del self._sign_since[sign]
        for sign in observation.signs:
            self._sign_since.setdefault(sign, now)
        self._confirmed = frozenset(
            sign for sign, since in self._sign_since.items()
            if now - since >= s.sign_persistence_s
        )

        if len(self._confirmed) >= 2:
            self.status = SessionStatus.STOPPED
            self._stable_since = None
            return self._result("two independent persistent fatigue signs")
        if self.status == SessionStatus.WARNING:
            return self._result("waiting for explicit Resume or Quit")
        if self._confirmed:
            self.status = SessionStatus.WARNING
            self._stable_since = None
            return self._result("one persistent fatigue sign")

        if not observation.stable_potential or observation.signs:
            self._stable_since = None
            return self._result("difficulty held: potential not stable")
        if self._stable_since is None:
            self._stable_since = now
        if (now - self._stable_since >= s.stable_window_s and
                now - self._last_change_at >= s.cooldown_s and
                self.level < s.max_level):
            self.level += 1
            self._last_change_at = now
            self._stable_since = now
            return self._result("difficulty increased after stable window")
        return self._result("difficulty held")

    def resume(self) -> Decision:
        if self.status != SessionStatus.WARNING or not self._last_input_valid or self._confirmed:
            raise ValueError("cannot resume without valid, cleared inputs")
        self.status = SessionStatus.ACTIVE
        self._stable_since = None
        self._last_change_at = self._last_at
        return self._result("resumed explicitly; cooldown restarted")

    def quit(self) -> Decision:
        self.status = SessionStatus.STOPPED
        self._stable_since = None
        return self._result("user quit; stop latched")

    def _result(self, reason: str) -> Decision:
        return Decision(self.status, self.level, reason, self._confirmed, self.settings.version)
