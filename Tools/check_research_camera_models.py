"""Execute optional models in the existing camera Python; never open a webcam."""
import json
import sys
import tempfile
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT/'ai-engine/src'))
import cv2
import numpy as np
from fatigue_ai.camera import ResearchCamera
from fatigue_ai.physiology import estimate_rppg

blank = np.zeros((240, 320, 3), dtype=np.uint8)
detector = cv2.FaceDetectorYN.create(str(ROOT/'models/face_detection_yunet_2023mar.onnx'), '', (320, 240), .85, .3, 1000)
_, faces = detector.detect(blank)
assert faces is None or len(faces) == 0
model = cv2.dnn.readNet(str(ROOT/'models/facial_expression_recognition_mobilefacenet_2022july.onnx'))
model.setInput(np.zeros((1, 3, 112, 112), dtype=np.float32))
started = time.perf_counter()
output = model.forward()
elapsed = (time.perf_counter()-started)*1000
assert output.shape == (1, 7) and np.isfinite(output).all()
temp_root = ROOT/'Logs/ai-temp'
temp_root.mkdir(exist_ok=True)
with tempfile.TemporaryDirectory(prefix='SYNTHETIC_consent_', dir=temp_root) as directory:
    worker = ResearchCamera(directory)
    try:
        worker.submit(blank, time.monotonic())
        assert worker.opt == (False, False) and worker.pending is None
    finally:
        worker.close()
t = np.arange(30*30)/30
pulse = np.sin(2*np.pi*1.2*t)
rgb = np.column_stack([100+.1*pulse, 120+.8*pulse, 90+.05*pulse])
result = estimate_rppg(t, rgb)
assert result['valid'] and abs(result['hr_bpm']-72)<3 and result['rmssd_ms'] is None
report = dict(passed=True, utc=time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
    consent_off_prevents_processing=True, blank_frame_has_no_face=True,
    expression_output_shape=list(output.shape), expression_ms=elapsed, opencv=cv2.__version__, numpy=np.__version__,
    SYNTHETIC_rppg_bpm=result['hr_bpm'], actual_camera_accuracy_validated=False,
    scope='Existing camera Python, model execution and synthetic signal; no webcam or human frames')
(ROOT/'ai-engine/reports/SYNTHETIC_camera-checks.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report))
