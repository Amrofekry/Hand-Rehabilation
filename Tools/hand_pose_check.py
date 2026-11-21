"""Hand pose check (2026-11-22): does the virtual hand look like the real hand, seen from behind it?

  ai-engine/.venv-ai/Scripts/python.exe Tools/hand_pose_check.py [--out DIR]

Starts the built player in Hand Lab replay mode (no webcam) and sends fabricated (SYNTHETIC) right-hand poses, each held
3 s, then the game saves its own view per pose (never the desktop). The poses are built in the patient's frame (x right, y up, z forward,
toward the camera) and converted to what the bridge sends for a mirrored camera image, so the game must show the hand
as the patient sees their own hand: 'upright' = fingers up, back of the hand toward the eyes, thumb on the left.
"""
from pathlib import Path
import argparse
import json
import math
import socket
import subprocess
import time


ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'Builds/Windows/GripRehab.exe'
W, H = 1280, 720


def right_hand():
    """21 points (m) of an open right hand, patient's frame, fingers up, palm toward the camera (+z), thumb on the left."""
    pts = [(0.0, 0.0, 0.0)]
    pts += [(-0.03, 0.03, 0.0), (-0.05, 0.05, 0.0), (-0.065, 0.07, 0.0), (-0.075, 0.09, 0.0)]  # thumb
    for x in (-0.028, -0.009, 0.01, 0.028):  # index, middle, ring, little: index on the left
        pts += [(x, 0.09, 0.0), (x, 0.125, 0.0), (x, 0.15, 0.0), (x, 0.17, 0.0)]
    return pts


def rot(axis, deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    if axis == 'x':
        return ((1, 0, 0), (0, c, -s), (0, s, c))
    if axis == 'y':
        return ((c, 0, s), (0, 1, 0), (-s, 0, c))
    return ((c, -s, 0), (s, c, 0), (0, 0, 1))


def mul(a, b):
    return tuple(tuple(sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)) for i in range(3))


def apply(m, p):
    return tuple(sum(m[i][k] * p[k] for k in range(3)) for i in range(3))


I3 = ((1, 0, 0), (0, 1, 0), (0, 0, 1))


def quat(m):
    """Rotation matrix -> unit quaternion (w, x, y, z)."""
    t = m[0][0] + m[1][1] + m[2][2]
    if t > 0:
        r = math.sqrt(1 + t) * 2
        return (r / 4, (m[2][1] - m[1][2]) / r, (m[0][2] - m[2][0]) / r, (m[1][0] - m[0][1]) / r)
    i = max(range(3), key=lambda k: m[k][k])
    j, k = (i + 1) % 3, (i + 2) % 3
    r = math.sqrt(1 + m[i][i] - m[j][j] - m[k][k]) * 2
    q = [0.0, 0.0, 0.0, 0.0]
    q[0] = (m[k][j] - m[j][k]) / r
    q[1 + i] = r / 4
    q[1 + j] = (m[j][i] + m[i][j]) / r
    q[1 + k] = (m[k][i] + m[i][k]) / r
    return tuple(q)


def matrix(q):
    w, x, y, z = q
    return ((1 - 2 * (y * y + z * z), 2 * (x * y - w * z), 2 * (x * z + w * y)),
            (2 * (x * y + w * z), 1 - 2 * (x * x + z * z), 2 * (y * z - w * x)),
            (2 * (x * z - w * y), 2 * (y * z + w * x), 1 - 2 * (x * x + y * y)))


def slerp(a, b, t):
    d = sum(p * q for p, q in zip(a, b))
    if d < 0:
        b, d = tuple(-v for v in b), -d
    if d > .9995:
        r = tuple(p + (q - p) * t for p, q in zip(a, b))
    else:
        th = math.acos(d)
        r = tuple((math.sin((1 - t) * th) * p + math.sin(t * th) * q) / math.sin(th) for p, q in zip(a, b))
    n = math.sqrt(sum(v * v for v in r))
    return tuple(v / n for v in r)
POSES = [
    ('1-upright', 'fingers up, back of the hand toward the eyes, thumb on the left', I3),
    ('2-palm-down-forward', 'fingers forward, palm down (reaching over a table)', rot('x', 90)),
    ('3-palm-up-forward', 'fingers forward, palm up (holding a tray)', mul(rot('z', 180), rot('x', 90))),
    ('4-palm-turned-right', 'fingers up, palm turned to the right (pouring roll)', rot('y', 90)),
    ('5-lean-left', 'fingers leaning 45 degrees to the left', rot('z', 45)),
    ('6-upright-again', 'back to upright (no drift)', I3),
]


def payload(points, seq):
    world = [{'x': x, 'y': -y, 'z': -z} for x, y, z in points]  # patient frame -> mirrored image frame (y down, z away)
    k = 2.2
    image = [{'x': 0.5 + x * k * H / W, 'y': 0.72 - y * k, 'z': -z * k} for x, y, z in points]
    return {'schemaVersion': 1, 'source': 'pose-check-synthetic', 'captureSeconds': time.monotonic(), 'ageMs': 0,
            'sequence': seq, 'handedness': 'Right', 'confidence': 0.99, 'gesture': 'unknown', 'gestureConfidence': 0.0,
            'landmarks': image, 'worldLandmarks': world, 'imageWidth': W, 'imageHeight': H}


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--out', type=Path, default=ROOT / 'Logs' / 'hand-pose-check')
    parser.add_argument('--hold', type=float, default=3.0)
    args = parser.parse_args()
    out = args.out if args.out.is_absolute() else ROOT / args.out
    out.mkdir(parents=True, exist_ok=True)
    (out / 'ready').unlink(missing_ok=True)
    command = [str(EXE), '-apartment-hand-lab', '-hand-lab-replay', '-hand-lab-out=' + str(out), '-force-d3d11',
               '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900', '-logFile', str(out / 'player.log')]
    base = right_hand()
    seq = 0
    player = subprocess.Popen(command, cwd=ROOT)
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
            deadline = time.monotonic() + 120
            while not (out / 'ready').exists():
                if time.monotonic() > deadline or player.poll() is not None:
                    raise SystemExit('The player did not reach the Hand Lab start')
                time.sleep(.05)
            last = quat(I3)
            for name, label, m in POSES:
                # Turn there over 1 s, as a real hand does (an instant 180-degree jump has no single shortest path).
                goal = quat(m)
                start = time.monotonic()
                while time.monotonic() - start < 1.0:
                    seq += 1
                    q = matrix(slerp(last, goal, (time.monotonic() - start) / 1.0))
                    sock.sendto(json.dumps(payload([apply(q, p) for p in base], seq), separators=(',', ':')).encode(), ('127.0.0.1', 27817))
                    time.sleep(1 / 30)
                last = goal
                pts = [apply(m, p) for p in base]
                end = time.monotonic() + args.hold
                shot = False
                while time.monotonic() < end:
                    seq += 1
                    sock.sendto(json.dumps(payload(pts, seq), separators=(',', ':')).encode(), ('127.0.0.1', 27817))
                    if not shot and end - time.monotonic() < .5:
                        (out / (name + '.shot')).write_text('')  # the game saves its own view (never the desktop)
                        shot = True
                    time.sleep(1 / 30)
                time.sleep(.6)  # let the game write the picture
                print(name, '-', label)
    finally:
        player.kill()
    print(json.dumps({'out': str(out), 'poses': len(POSES), 'synthetic': True}))


if __name__ == '__main__':
    main()
