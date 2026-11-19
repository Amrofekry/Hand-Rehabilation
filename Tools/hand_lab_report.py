"""Hand Lab report: how a real camera hand mapped into the game, in numbers.

  ai-engine/.venv-ai/Scripts/python.exe Tools/hand_lab_report.py Logs/hand-lab/<session> [<replay dir> ...]
Reads game.csv (and bridge.jsonl when present) and writes report.json and report.md next to it. MEASURED on a real hand
for a live session; a replay re-measures the game side on the same recorded hand.
"""
from pathlib import Path
import csv
import json
import math
import statistics
import sys

ROOT = Path(__file__).resolve().parents[1]
STEP = {'still': 0, 'leftright': 1, 'updown': 2, 'depth': 3, 'quick': 4, 'fist': 5, 'grabs': (6, 7, 8)}
REACH_X, REACH_Y = .30, .26  # NORMAL hand reach (HandControlProfile)


def num(v):
    try:
        x = float(v)
        return x if math.isfinite(x) else None
    except (TypeError, ValueError):
        return None


def load(path: Path):
    with open(path / 'game.csv', newline='', encoding='utf-8') as f:
        rows = list(csv.DictReader(f))
    for r in rows:
        for k in ('t', 'capture', 'ageMs', 'wristX', 'wristY', 'palm', 'targetX', 'targetY', 'targetZ', 'handX', 'handY', 'handZ', 'size', 'score', 'targetDist', 'fps'):
            r[k] = num(r.get(k))
        for k in ('step', 'seq', 'valid', 'tracked', 'gate'):
            r[k] = int(r[k]) if r.get(k) not in (None, '') else 0
    return rows


def pct(values, q):
    values = sorted(v for v in values if v is not None)
    if not values:
        return None
    i = min(len(values) - 1, max(0, int(round(q / 100 * (len(values) - 1)))))
    return values[i]


def resample(rows, key, dt=.01):
    """Signal on an even 100 Hz grid (sample and hold), for cross-correlation."""
    pts = [(r['t'], r[key]) for r in rows if r['t'] is not None and r[key] is not None and r['tracked']]
    if len(pts) < 20:
        return []
    t0, t1 = pts[0][0], pts[-1][0]
    out, j, n = [], 0, int((t1 - t0) / dt)
    for i in range(n):
        t = t0 + i * dt
        while j + 1 < len(pts) and pts[j + 1][0] <= t:
            j += 1
        out.append(pts[j][1])
    return out


def lag_ms(rows, a, b, max_ms=600):
    """Delay of b behind a (ms), from the peak of their cross-correlation."""
    x, y = resample(rows, a), resample(rows, b)
    n = min(len(x), len(y))
    if n < 50:
        return None
    x, y = x[:n], y[:n]
    mx, my = statistics.fmean(x), statistics.fmean(y)
    x = [v - mx for v in x]
    y = [v - my for v in y]
    best, best_k = -1e30, 0
    for k in range(0, int(max_ms / 10) + 1):
        s = sum(x[i] * y[i + k] for i in range(n - k))
        if s > best:
            best, best_k = s, k
    return best_k * 10


def step_rows(rows, s):
    return [r for r in rows if r['step'] == s]


def analyse(path: Path):
    rows = load(path)
    report = {'session': str(path), 'frames': len(rows)}
    bridge = path / 'bridge.jsonl'
    if not bridge.exists() and (path.parent / 'bridge.jsonl').exists():
        bridge = path.parent / 'bridge.jsonl'
    if bridge.exists():
        frames, hands = [], []
        for line in bridge.read_text(encoding='utf-8').splitlines():
            rec = json.loads(line)
            (frames if rec.get('kind') == 'frame' else hands).append(rec)
        if len(frames) > 2:
            span = frames[-1]['captured'] - frames[0]['captured']
            report['camera'] = {
                'fps': round((len(frames) - 1) / span, 1) if span > 0 else None,
                'frameSize': f"{frames[0]['width']}x{frames[0]['height']}",
                'inferenceMsMedian': round(pct([f['inferMs'] for f in frames], 50), 1),
                'inferenceMsP95': round(pct([f['inferMs'] for f in frames], 95), 1),
                'handSeenPercent': round(100 * sum(1 for f in frames if f['hands'] > 0) / len(frames), 1),
            }
    # Steady hand: jitter of the virtual hand and of the depth estimate while holding still.
    still = [r for r in step_rows(rows, STEP['still']) if r['tracked']]
    if still:
        report['still'] = {
            'handJitterMm': {ax: round(1000 * statistics.pstdev([r['hand' + ax] for r in still]), 2) for ax in ('X', 'Y', 'Z')},
            'palmSizeNoisePercent': round(100 * statistics.pstdev([r['size'] for r in still if r['size']]) / max(1e-6, statistics.fmean([r['size'] for r in still if r['size']])), 2),
            'restTarget': [round(statistics.median([r['target' + ax] for r in still]), 4) for ax in ('X', 'Y', 'Z')],
        }
    rest = report.get('still', {}).get('restTarget', [0, 0, 0])
    # Per-axis filter delay (mapped target -> virtual hand) and how much of the move was clamped at the reach limit.
    axes = {}
    for name, s, ax, reach, img in (('leftRight', STEP['leftright'], 'X', REACH_X, 'wristX'), ('upDown', STEP['updown'], 'Y', REACH_Y, 'wristY'),
                                    ('depth', STEP['depth'], 'Z', None, 'size')):
        rs = [r for r in step_rows(rows, s) if r['tracked']]
        if len(rs) < 30:
            continue
        info = {'filterLagMs': lag_ms(rs, 'target' + ax, 'hand' + ax),
                'handRangeCm': round(100 * (max(r['hand' + ax] for r in rs) - min(r['hand' + ax] for r in rs)), 1)}
        vals = [r[img] for r in rs if r[img] is not None]
        if vals:
            info['imageRange'] = round(max(vals) - min(vals), 3)
        if reach is not None:
            i = 'XYZ'.index(ax)
            info['clampedPercent'] = round(100 * sum(1 for r in rs if abs(r['target' + ax] - rest[i]) >= reach - .002) / len(rs), 1)
        axes[name] = info
    report['axes'] = axes
    report['trackingLostPercent'] = {str(s): round(100 * sum(1 for r in step_rows(rows, s) if not r['tracked']) / max(1, len(step_rows(rows, s))), 1) for s in range(10) if step_rows(rows, s)}
    ages = [r['ageMs'] for r in rows if r['ageMs'] is not None and r['tracked']]
    if ages:
        report['bridgeAgeMsMedian'] = round(pct(ages, 50), 1)
    lag = [a.get('filterLagMs') for a in axes.values() if a.get('filterLagMs') is not None]
    if lag and ages:
        report['estimatedEndToEndMs'] = {'positionMedian': round(pct(ages, 50) + 17 + statistics.median([axes[k]['filterLagMs'] for k in ('leftRight', 'upDown') if k in axes and axes[k].get('filterLagMs') is not None] or [0])),
                                         'depth': round(pct(ages, 50) + 17 + (axes.get('depth', {}).get('filterLagMs') or 0)),
                                         'note': 'camera exposure (about half a frame) not included; 17 ms = half a camera frame waiting'}
    # Grabs.
    grabs = []
    for s in STEP['grabs']:
        rs = step_rows(rows, s)
        if not rs:
            continue
        want = rs[0]['targetName']
        held_times = [r['t'] for r in rs if r['held'] == want and want]
        wrong = sorted({r['held'] for r in rs if r['held'] and r['held'] != want})
        closes = sum(1 for a, b in zip(rs, rs[1:]) if b['gate'] and not a['gate'])
        dists = [r['targetDist'] for r in rs if r['targetDist'] is not None]
        grabs.append({'target': want, 'grabbed': bool(held_times), 'secondsToGrab': round(held_times[0] - rs[0]['t'], 2) if held_times else None,
                      'wrongObjects': wrong, 'handClosings': closes, 'closestCm': round(100 * min(dists), 1) if dists else None,
                      'maxScore': round(max((r['score'] or 0) for r in rs), 2), 'trackingLostPercent': round(100 * sum(1 for r in rs if not r['tracked']) / len(rs), 1)})
    report['grabs'] = grabs
    report['grabSuccess'] = f"{sum(g['grabbed'] for g in grabs)}/{len(grabs)}"
    return report


def markdown(rep):
    lines = [f"# Hand Lab report: {Path(rep['session']).name}", '']
    cam = rep.get('camera')
    if cam:
        lines += [f"- Camera: {cam['fps']} fps at {cam['frameSize']}; MediaPipe {cam['inferenceMsMedian']} ms median ({cam['inferenceMsP95']} p95); hand seen in {cam['handSeenPercent']} % of frames"]
    if 'estimatedEndToEndMs' in rep:
        e = rep['estimatedEndToEndMs']
        lines += [f"- Estimated delay, camera frame to virtual hand: about {e['positionMedian']} ms sideways/up-down, {e['depth']} ms toward/away ({e['note']})"]
    if 'still' in rep:
        s = rep['still']
        lines += [f"- Holding still: hand jitter X {s['handJitterMm']['X']} / Y {s['handJitterMm']['Y']} / Z {s['handJitterMm']['Z']} mm; palm-size noise {s['palmSizeNoisePercent']} %"]
    lines += ['', '| Axis | Filter delay (ms) | Hand range (cm) | Image range | Clamped at limit (%) |', '|---|---|---|---|---|']
    for name, a in rep.get('axes', {}).items():
        lines.append(f"| {name} | {a.get('filterLagMs')} | {a.get('handRangeCm')} | {a.get('imageRange')} | {a.get('clampedPercent', '-')} |")
    lines += ['', f"Grabs: {rep.get('grabSuccess')}", '', '| Target | Grabbed | Seconds | Wrong objects | Hand closings | Closest (cm) | Max grip score | Tracking lost (%) |', '|---|---|---|---|---|---|---|---|']
    for g in rep.get('grabs', []):
        lines.append(f"| {g['target']} | {g['grabbed']} | {g['secondsToGrab']} | {', '.join(g['wrongObjects']) or '-'} | {g['handClosings']} | {g['closestCm']} | {g['maxScore']} | {g['trackingLostPercent']} |")
    return '\n'.join(lines) + '\n'


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    for arg in sys.argv[1:]:
        path = Path(arg) if Path(arg).is_absolute() else ROOT / arg
        rep = analyse(path)
        (path / 'report.json').write_text(json.dumps(rep, indent=2) + '\n', encoding='utf-8')
        (path / 'report.md').write_text(markdown(rep), encoding='utf-8')
        print(markdown(rep))


if __name__ == '__main__':
    main()
