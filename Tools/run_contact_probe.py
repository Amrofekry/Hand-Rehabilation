"""Stage 8 hand-object contact audit: same build, solver off (baseline) then on.

SYNTHETIC keyboard-demo force only, isolated acceptance storage, never a webcam or device.
  python Tools/run_contact_probe.py            # both runs plus a comparison table
  python Tools/run_contact_probe.py --solver-only
  python Tools/run_contact_probe.py --grip-lab     # the 18 chart grips (Grip Lab), solver on
"""
from pathlib import Path
import argparse
import json
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'Builds/Windows/GripRehab.exe'


def run(solver, grip_lab=False):
    started = time.time()
    name = ('contact-probe-grips' if grip_lab else 'contact-probe') + ('' if solver else '-baseline')
    log = ROOT / f'Logs/{name}.log'
    result_path = ROOT / f'Logs/{name}.json'
    command = [str(EXE), '-apartment-contact-probe', '-force-d3d11', '-screen-fullscreen', '0',
               '-screen-width', '1600', '-screen-height', '900', '-logFile', str(log)]
    if not solver:
        command.append('-contact-solver-off')
    if grip_lab:
        command.append('-grip-lab')
    with subprocess.Popen(command, cwd=ROOT) as process:
        try:
            code = process.wait(timeout=600)
        except subprocess.TimeoutExpired:
            process.terminate()
            raise RuntimeError('Player hung during the contact probe')
    if not result_path.exists() or result_path.stat().st_mtime < started:
        raise RuntimeError('Player did not write fresh contact output; ensure current scripts were built')
    result = json.loads(result_path.read_text(encoding='utf-8-sig'))
    (ROOT / f'Docs/{name}.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    if code or not result['passed']:
        raise RuntimeError(f'Contact probe failed: exit {code}, {result.get("detail", "")}')
    return result


def summary(result):
    rows = {}
    for c in result['cases']:
        if c['phase'] == 'grab failed':
            rows.setdefault(c['item'], {}).setdefault('grabFailed', 0)
            rows[c['item']]['grabFailed'] += 1
            continue
        r = rows.setdefault(c['item'], {'holdPenMax': 0, 'reachPenMax': 0, 'shakePenMax': 0, 'gapMean': [], 'holdPalmMax': 0,
                                        'jitter': 0, 'dent18': 0, 'furniture': 0, 'dropped': 0, 'reachMs': 0, 'shakeMs': 0})
        key = {'hold': 'hold', 'reach move': 'reach', 'fast shake': 'shake'}[c['phase']]
        r[key + 'PenMax'] = max(r[key + 'PenMax'], c['penetrationMaxMm'])
        if key != 'hold':
            r[key + 'Ms'] = max(r[key + 'Ms'], c.get('handPeakMs', 0))
        else:
            r['holdPalmMax'] = max(r['holdPalmMax'], c['palmDepthMaxMm'])
        r['furniture'] += c['furnitureOverlapFrames']
        r['dropped'] += 0 if c['held'] else 1
        if c['phase'] == 'hold':
            r['gapMean'].append(c['padGapMeanMm'])
            r['jitter'] = max(r['jitter'], c['jitterMm'])
            if c['force'] == 18:
                r['dent18'] = max(r['dent18'], c['dentMm'])
    for r in rows.values():
        if isinstance(r.get('gapMean'), list):
            r['gapMean'] = round(sum(r['gapMean']) / max(1, len(r['gapMean'])), 2)
        for k in ('holdPenMax', 'reachPenMax', 'shakePenMax', 'holdPalmMax', 'jitter', 'dent18', 'reachMs', 'shakeMs'):
            if k in r:
                r[k] = round(r[k], 2)
    return rows


def grip_summary(result):
    rows = {}
    for c in result['cases']:
        key = f"{c['gripNumber']:02d} {c['grip']} / {c['item']}"
        if c['phase'] == 'grab failed':
            rows[key] = {'grabFailed': True}
            continue
        r = rows.setdefault(key, {'holdPenMax': 0, 'reachPenMax': 0, 'gapMean': [], 'touching18': 0, 'pads': c['padCount'],
                                  'holdPalmMax': 0, 'jitter': 0, 'dent18': 0, 'furniture': 0, 'dropped': 0})
        if c['phase'] == 'hold':
            r['holdPenMax'] = max(r['holdPenMax'], c['penetrationMaxMm'])
            r['holdPalmMax'] = max(r['holdPalmMax'], c['palmDepthMaxMm'])
            r['gapMean'].append(c['padGapMeanMm'])
            r['jitter'] = max(r['jitter'], c['jitterMm'])
            if c['force'] == 18:
                r['touching18'] = round(c['touchingPadsMean'], 1)
                r['dent18'] = round(c['dentMm'], 1)
        else:
            r['reachPenMax'] = max(r['reachPenMax'], c['penetrationMaxMm'])
        r['furniture'] += c['furnitureOverlapFrames']
        r['dropped'] += 0 if c['held'] else 1
    for r in rows.values():
        if 'gapMean' in r:
            r['gapMean'] = round(sum(r['gapMean']) / max(1, len(r['gapMean'])), 1)
            for k in ('holdPenMax', 'reachPenMax', 'holdPalmMax', 'jitter'):
                r[k] = round(r[k], 1)
    return rows


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--solver-only', action='store_true')
    parser.add_argument('--grip-lab', action='store_true')
    args = parser.parse_args()
    if args.grip_lab:
        print(json.dumps(grip_summary(run(True, grip_lab=True)), indent=1))
        return
    out = {}
    if not args.solver_only:
        out['baseline (solver off)'] = summary(run(False))
    out['solver on'] = summary(run(True))
    print(json.dumps(out, indent=1))


if __name__ == '__main__':
    main()
