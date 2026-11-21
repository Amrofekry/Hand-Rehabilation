"""Hand depth from one camera, compared on real Hand Lab recordings (MEASURED landmarks, replayed offline).

  ai-engine/.venv-ai/Scripts/python.exe Tools/hand_depth_eval.py Logs/hand-lab/<session> [...]

For each recorded hand frame (bridge.jsonl, joined to the guided step in game.csv by capture time) it computes:
- palm: the old cue, aspect-corrected palm width and length in the image (changes when the hand tilts or rolls);
- scale: image bone lengths divided by the same bones' 3D length across the image plane (MediaPipe world landmarks), the
  median over palm and finger-base bones. A tilted hand shortens both, so the ratio follows distance only.
Both as ln(value / its median in the still step). A good depth cue is quiet when still, flat when the hand moves left/right,
up/down or makes a fist, and swings strongly on the toward/away step.
"""
from pathlib import Path
import csv
import json
import math
import statistics
import sys

BONES = ((0, 5), (0, 9), (0, 13), (0, 17), (5, 9), (9, 13), (13, 17), (5, 17), (5, 6), (9, 10), (13, 14), (17, 18))
HOLD_FROM, HOLD_FULL = .15, .45  # image heights per second: from here depth follows less, at full speed it holds
STEPS = {0: 'still', 1: 'left-right', 2: 'up-down', 3: 'toward-away', 4: 'quick', 5: 'fist'}


def palm_size(lm, aspect):
    def d(a, b):
        return math.hypot((lm[a]['x'] - lm[b]['x']) * aspect, lm[a]['y'] - lm[b]['y'])
    return .5 * (d(5, 17) + d(0, 9))


def bone_scale(lm, world, aspect):
    ratios = []
    for a, b in BONES:
        image = math.hypot((lm[a]['x'] - lm[b]['x']) * aspect, lm[a]['y'] - lm[b]['y'])
        plane = math.hypot(world[a][0] - world[b][0], world[a][1] - world[b][1])
        if plane > .01:
            ratios.append(image / plane)
    return statistics.median(ratios) if len(ratios) >= 4 else None


def load(path: Path):
    hands = {}
    for line in (path / 'bridge.jsonl').read_text(encoding='utf-8').splitlines():
        rec = json.loads(line)
        if rec.get('kind') == 'hand' and rec.get('world'):
            p = rec['payload']
            hands[round(p['captureSeconds'], 4)] = (p['landmarks'], rec['world'], rec['width'] / rec['height'])
    rows = []
    with open(path / 'game.csv', newline='', encoding='utf-8') as f:
        for r in csv.DictReader(f):
            key = round(float(r['capture']), 4) if r['capture'] else None
            if key in hands:
                rows.append((int(r['step']), key, *hands[key]))
    # One row per camera frame (the game logs several frames per camera frame).
    seen, unique = set(), []
    for row in rows:
        if row[1] not in seen:
            seen.add(row[1])
            unique.append(row)
    return unique


def analyse(path: Path):
    rows = load(path)
    out = {'session': path.name, 'frames': len(rows)}
    series = {'palm': [], 'scale': [], 'held': []}
    # 'held': both cues averaged (geometric mean), smoothed, and held while the hand travels across the image (a deliberate
    # toward/away move changes size with little image travel; a sweep left/right or up/down is mostly travel).
    level, last_t, last_xy, speed = None, None, None, 0.0
    for step, t, lm, world, aspect in rows:
        s = bone_scale(lm, world, aspect)
        if not s:
            continue
        p = palm_size(lm, aspect)
        series['palm'].append((step, p))
        series['scale'].append((step, s))
        cue = math.log(p) * .5 + math.log(s) * .5
        cx = statistics.fmean(lm[i]['x'] for i in (0, 5, 9, 13, 17)) * aspect
        cy = statistics.fmean(lm[i]['y'] for i in (0, 5, 9, 13, 17))
        if last_t is not None and t > last_t:
            dt = t - last_t
            v = math.hypot(cx - last_xy[0], cy - last_xy[1]) / dt
            speed += (v - speed) * (1 - math.exp(-dt / .15))
            hold = min(1.0, max(0.0, (speed - HOLD_FROM) / (HOLD_FULL - HOLD_FROM)))
            follow = (1 - math.exp(-dt / .12)) * (1 - hold)
            level += (cue - level) * follow
        else:
            level = cue
        last_t, last_xy = t, (cx, cy)
        series['held'].append((step, math.exp(level)))
    for name, values in series.items():
        ref = statistics.median([v for s, v in values if s == 0] or [v for _, v in values])
        logs = [(s, math.log(v / ref)) for s, v in values]
        stats = {}
        for step, label in STEPS.items():
            vals = [v for s, v in logs if s == step]
            if len(vals) > 10:
                vals.sort()
                p5, p95 = vals[int(.05 * (len(vals) - 1))], vals[int(.95 * (len(vals) - 1))]
                hp = statistics.pstdev([b - a for a, b in zip(vals, vals[1:])]) if False else None
                stats[label] = {'range90': round(p95 - p5, 3), 'std': round(statistics.pstdev(vals), 3)}
        # Fast jitter: frame-to-frame change in the still step.
        still = [v for s, v in logs if s == 0]
        if len(still) > 10:
            stats['still-jitter'] = round(statistics.pstdev([b - a for a, b in zip(still, still[1:])]), 4)
        # Separation: how much of the variation is the wanted toward/away move vs moves that should not change depth.
        if 'toward-away' in stats:
            false = max(stats.get(k, {'range90': 0})['range90'] for k in ('left-right', 'up-down', 'fist'))
            stats['signal-to-false'] = round(stats['toward-away']['range90'] / max(1e-6, false), 2)
        out[name] = stats
    return out


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    results = []
    for arg in sys.argv[1:]:
        r = analyse(Path(arg))
        results.append(r)
        print(f"== {r['session']} ({r['frames']} camera frames, MEASURED landmarks)")
        print(f"{'cue':6} {'still jitter':>12} {'still r90':>9} {'left-right':>10} {'up-down':>8} {'fist':>6} {'toward-away':>11} {'signal/false':>12}")
        for cue in ('palm', 'scale', 'held'):
            s = r[cue]
            g = lambda k: s.get(k, {}).get('range90', float('nan'))
            print(f"{cue:6} {s.get('still-jitter', float('nan')):12.4f} {g('still'):9.3f} {g('left-right'):10.3f} {g('up-down'):8.3f} {g('fist'):6.3f} {g('toward-away'):11.3f} {s.get('signal-to-false', float('nan')):12.2f}")
    Path('Logs/hand-depth-eval.json').write_text(json.dumps(results, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
