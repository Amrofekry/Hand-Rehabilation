"""GripRehab first-run check (Stage 19): the camera worker's Python, packages, model, UDP ports and camera.

Run from the install folder: .venv\\Scripts\\python.exe Tools\\release\\first_run_check.py [--no-camera]
Prints one line per check and exits 0 only when every check passes. Nothing is saved; no image is kept.
"""
import argparse
import importlib
import socket
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def check(name, fn):
    try:
        detail = fn() or ''
        print(f'PASS {name} {detail}'.rstrip()); return True
    except Exception as e:  # report every failure, keep going
        print(f'FAIL {name}: {type(e).__name__}: {e}'); return False


def files():
    need = [ROOT / 'Game' / 'GripRehab.exe', ROOT / 'Tools' / 'Vision' / 'mediapipe_bridge.py', ROOT / 'models' / 'hand_landmarker.task']
    missing = [str(p.relative_to(ROOT)) for p in need if not p.exists()]
    if missing: raise FileNotFoundError(', '.join(missing))


def packages():
    versions = []
    for m in ('numpy', 'cv2', 'mediapipe'):
        mod = importlib.import_module(m); versions.append(f'{m} {getattr(mod, "__version__", "?")}')
    return '; '.join(versions)


def ports():
    busy = []
    for port in (27817, 27818, 27819):
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        try: s.bind(('127.0.0.1', port))
        except OSError: busy.append(port)
        finally: s.close()
    if busy: raise OSError(f'UDP ports in use (close the game or another copy first): {busy}')
    return '27817-27819 free'


def camera(index):
    import cv2
    cap = cv2.VideoCapture(index, cv2.CAP_DSHOW)
    try:
        if not cap.isOpened(): raise RuntimeError(f'camera {index} could not open (privacy settings or another app?)')
        ok, frame = cap.read()
        if not ok or frame is None: raise RuntimeError('camera opened but returned no frame')
        return f'camera {index} {frame.shape[1]}x{frame.shape[0]}'
    finally: cap.release()


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument('--no-camera', action='store_true'); p.add_argument('--camera', type=int, default=0)
    a = p.parse_args(argv)
    results = [check('files', files), check('python ' + sys.version.split()[0], lambda: None), check('packages', packages), check('ports', ports)]
    if not a.no_camera: results.append(check('camera', lambda: camera(a.camera)))
    print('FIRST RUN CHECK ' + ('PASSED' if all(results) else 'FAILED'))
    return 0 if all(results) else 1


if __name__ == '__main__':
    raise SystemExit(main())
