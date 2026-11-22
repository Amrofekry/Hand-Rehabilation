"""Hand check report (2026-11-22): does the virtual hand do what the real hand does? For one Hand Lab session.

  ai-engine/.venv-ai/Scripts/python.exe Tools/hand_check_report.py Logs/hand-lab/<session> [--json OUT]

Reads game.csv (the game's side, every frame) and bridge.jsonl (the landmarks the camera saw, numbers only) and reports:
  1. mapping: for the left/right, up/down and toward/away steps, the correlation between the real wrist (or the
     hand's apparent size) and the virtual hand on that axis, and how much depth moved during the left/right sweep;
  2. orientation: the angle between the real palm's turn and the virtual hand's turn, both measured from the
     still-hand step (so the rig's own rest pose does not matter), for the palm down / palm up / roll steps;
  3. grip: while holding, how far the object-led hand moved to sit on the object, finger depth into it, pad gap.
A live session is MEASURED (a real hand); a replay of one is MEASURED landmarks replayed; probes are SYNTHETIC.
"""
from __future__ import annotations

import argparse
import bisect
import csv
import json
import math
from pathlib import Path

STEP_STILL, STEP_LR, STEP_UD, STEP_DEPTH = 0, 1, 2, 3
STEP_GRABS = (6, 7, 8)
STEP_PALM = {9: 'palm down', 10: 'palm up', 11: 'wrist roll'}


def num(v):
    try:
        x = float(v)
        return x if math.isfinite(x) else None
    except (TypeError, ValueError):
        return None


def corr(a, b):
    pairs = [(x, y) for x, y in zip(a, b) if x is not None and y is not None]
    if len(pairs) < 10:
        return None
    n = len(pairs); mx = sum(p[0] for p in pairs) / n; my = sum(p[1] for p in pairs) / n
    sx = math.sqrt(sum((p[0] - mx) ** 2 for p in pairs)); sy = math.sqrt(sum((p[1] - my) ** 2 for p in pairs))
    return None if sx < 1e-9 or sy < 1e-9 else sum((p[0] - mx) * (p[1] - my) for p in pairs) / (sx * sy)


def spread(v):
    v = sorted(x for x in v if x is not None)
    return None if len(v) < 10 else v[int(.9 * (len(v) - 1))] - v[int(.1 * (len(v) - 1))]


def pct(v, q):
    v = sorted(x for x in v if x is not None)
    return None if not v else v[min(len(v) - 1, int(q * (len(v) - 1)))]


# ---- rotations as 3x3 matrices (columns: side, up, forward), the same basis FingerPoseDriver uses
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def unit(a):
    n = math.sqrt(dot(a, a)); return None if n < 1e-9 else (a[0] / n, a[1] / n, a[2] / n)


def palm_basis(world):
    """Patient-frame palm basis from world landmarks (x, -y, -z as HandKinematics.PosePoint): LookRotation(cross(side, up), up)."""
    p = [(w[0], -w[1], -w[2]) for w in world]
    up = unit(sub(p[9], p[0])); side = sub(p[5], p[17])
    if up is None: return None
    side = unit(sub(side, tuple(dot(side, up) * u for u in up)))
    if side is None: return None
    fwd = unit(cross(side, up))
    if fwd is None: return None
    right = cross(up, fwd)  # LookRotation's x axis
    return ((right[0], up[0], fwd[0]), (right[1], up[1], fwd[1]), (right[2], up[2], fwd[2]))


def quat_matrix(x, y, z, w):
    return ((1 - 2 * (y * y + z * z), 2 * (x * y - w * z), 2 * (x * z + w * y)),
            (2 * (x * y + w * z), 1 - 2 * (x * x + z * z), 2 * (y * z - w * x)),
            (2 * (x * z - w * y), 2 * (y * z + w * x), 1 - 2 * (x * x + y * y)))


def mul(a, b): return tuple(tuple(sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)) for i in range(3))
def tr(a): return tuple(tuple(a[j][i] for j in range(3)) for i in range(3))
def angle(m): return math.degrees(math.acos(max(-1.0, min(1.0, (m[0][0] + m[1][1] + m[2][2] - 1) / 2))))


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument('session', type=Path)
    ap.add_argument('--json', type=Path)
    a = ap.parse_args()
    session = a.session
    with open(session / 'game.csv', encoding='utf-8') as f:
        rows = list(csv.DictReader(f))
    by_step = {}
    for r in rows:
        by_step.setdefault(int(r['step']), []).append(r)
    report = {'session': str(session), 'frames': len(rows)}

    # 1. mapping
    def col(step, name): return [num(r.get(name)) for r in by_step.get(step, []) if r.get('tracked') == '1']
    lr_x, lr_hand = col(STEP_LR, 'wristX'), col(STEP_LR, 'handX')
    ud_y, ud_hand = col(STEP_UD, 'wristY'), col(STEP_UD, 'handY')
    dp_size = [math.log(v) if v and v > 0 else None for v in col(STEP_DEPTH, 'palm')]
    dp_hand = col(STEP_DEPTH, 'handZ')
    report['mapping'] = {
        'leftRight': {'correlation': corr(lr_x, lr_hand), 'virtualSpreadM': spread(lr_hand)},
        'upDown': {'correlation': corr([-y if y is not None else None for y in ud_y], ud_hand), 'virtualSpreadM': spread(ud_hand)},
        'towardAway': {'correlation': corr(dp_size, dp_hand), 'virtualSpreadM': spread(dp_hand)},
        'depthDuringLeftRightM': spread(col(STEP_LR, 'handZ')),
    }

    # 2. orientation: join bridge hands to game frames by capture time
    hands = []
    bridge = session / 'bridge.jsonl'
    if bridge.exists():
        for line in bridge.read_text(encoding='utf-8').splitlines():
            rec = json.loads(line)
            if rec.get('kind') == 'hand' and rec.get('world'):
                hands.append((rec['payload']['captureSeconds'], rec['world']))
    hands.sort(key=lambda h: h[0]); times = [h[0] for h in hands]

    def real_basis(r):
        t = num(r.get('capture'))
        if t is None or not times: return None
        i = bisect.bisect_left(times, t)
        best = min((j for j in (i - 1, i) if 0 <= j < len(times)), key=lambda j: abs(times[j] - t), default=None)
        return palm_basis(hands[best][1]) if best is not None and abs(times[best] - t) < .02 else None

    def virt(r):
        q = [num(r.get(k)) for k in ('rotX', 'rotY', 'rotZ', 'rotW')]
        return None if None in q else quat_matrix(*q)

    ref_real = ref_virt = None
    for r in by_step.get(STEP_STILL, [])[len(by_step.get(STEP_STILL, [])) // 2:]:
        b, v = real_basis(r), virt(r)
        if b and v: ref_real, ref_virt = b, v; break
    orientation = {}
    if ref_real and ref_virt:
        for step, name in STEP_PALM.items():
            errs, turns = [], []
            for r in by_step.get(step, []):
                b, v = real_basis(r), virt(r)
                if not (b and v): continue
                real_turn = mul(b, tr(ref_real)); virt_turn = mul(v, tr(ref_virt))
                errs.append(angle(mul(real_turn, tr(virt_turn)))); turns.append(angle(real_turn))
            orientation[name] = {'frames': len(errs), 'realTurnDegMedian': pct(turns, .5), 'errorDegMedian': pct(errs, .5), 'errorDegP90': pct(errs, .9)}
    else:
        orientation['note'] = 'no world landmarks or no rotation columns (record a new session with the 2026-11-22 build)'
    report['orientation'] = orientation

    # 3. grip
    held = [r for s in STEP_GRABS for r in by_step.get(s, []) if r.get('held')]
    report['grip'] = {
        'heldFrames': len(held),
        'handCorrectionMmP90': pct([num(r.get('holdCorrection')) for r in held], .9),
        'penetrationMmMax': pct([num(r.get('penetration')) for r in held], 1.0),
        'padGapMmMedian': pct([num(r.get('padGap')) for r in held], .5),
        'grabbed': sorted({r['held'] for r in held}),
    }
    text = json.dumps(report, indent=2)
    print(text)
    if a.json:
        a.json.write_text(text + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
