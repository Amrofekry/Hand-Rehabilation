"""Consent lifecycle checks without opening a camera or processing a face."""
import json
import sys
from collections import deque
from types import SimpleNamespace

import numpy as np
import pytest

from fatigue_ai import camera


def test_consent_is_fresh_per_worker_and_revoking_one_resets_history(tmp_path, monkeypatch):
    now = [100.0]
    monkeypatch.setattr(camera.time, 'time', lambda: now[0])
    # Exercise the real submit path deterministically without a background worker.
    monkeypatch.setattr(camera.threading, 'Thread', lambda **_: SimpleNamespace(start=lambda: None, join=lambda **_: None))
    worker = camera.ResearchCamera(tmp_path)
    worker.path.parent.mkdir()
    frame = np.zeros((2, 2, 3), dtype=np.uint8)

    def consent(updated, face=True, physiology=True):
        worker.path.write_text(json.dumps(dict(updatedUnix=updated, face=face, physiology=physiology)), encoding='utf-8')

    try:
        consent(99.9)  # A previous process's still-young file is not an opt-in.
        worker.submit(frame, 10)
        assert worker.opt == (False, False) and worker.pending is None
        now[0] = 100.1
        consent(100.1)
        worker.submit(frame, 11)
        assert worker.opt == (True, True) and worker.pending is not None
        assert worker.pending[0] is not frame
        worker.reset = False
        now[0] = 101
        consent(101, physiology=False)
        worker.submit(frame, 12)
        assert worker.opt == (True, False) and worker.reset
        now[0] = 105
        worker.submit(frame, 13)
        assert worker.opt == (False, False) and worker.pending is None
        consent(200)  # Future timestamps are not valid consent either.
        worker.submit(frame, 14)
        assert worker.opt == (False, False)
    finally:
        worker.close()


@pytest.mark.parametrize('mode', ['consent_reset', 'face_gap'])
def test_worker_reset_drops_cached_pulse_result(monkeypatch, mode):
    # Run one idle worker iteration; no image or model is involved.
    # OpenCV belongs to the existing camera interpreter, not the ML-only venv.
    monkeypatch.setitem(sys.modules, 'cv2', SimpleNamespace())
    worker = camera.ResearchCamera.__new__(camera.ResearchCamera)
    worker.closed = False
    worker.lock = camera.threading.Lock()
    worker.pending = (np.zeros((2, 2, 3), dtype=np.uint8), camera.time.monotonic(), (True, True)) if mode == 'face_gap' else None
    worker.reset = mode == 'consent_reset'
    worker.samples = deque([(1, 2, 3)])
    worker.baseline = [123]
    worker.previous = object()
    worker.last_ppg = {'valid': True, 'hr_bpm': 123}
    worker.opt = (True, True) if mode == 'face_gap' else (False, False)
    worker.detector = SimpleNamespace(setInputSize=lambda _: None, detect=lambda _: (None, None))
    worker.seq = 0
    worker.session_id = 'SYNTHETIC_reset'
    packets = []
    worker.sock = SimpleNamespace(sendto=lambda data, _: packets.append(json.loads(data)))
    worker.wake = SimpleNamespace(wait=lambda _: setattr(worker, 'closed', True), clear=lambda: None)
    worker._run()
    assert not worker.samples and not worker.baseline and worker.previous is None
    assert not hasattr(worker, 'last_ppg')
    if mode == 'face_gap':
        assert len(packets) == 1 and not packets[0]['physiologyValid']
