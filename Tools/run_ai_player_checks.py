"""Opt-in validation of the packaged research model and existing Windows game.

Rendered mode needs the player in the foreground, as real hand-pointer input
deliberately pauses when unfocused. This runner never forces desktop focus.
"""
import argparse
import json
import subprocess
import time
from pathlib import Path

from run_player_checks import ROOT, EXE, run


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('mode', choices=['smoke', 'replay', 'rendered', 'journey'])
    args = parser.parse_args()
    started = time.time()
    if args.mode == 'journey':
        target = ROOT / 'Logs/journey-acceptance.json'
        completed = subprocess.run([str(EXE), '-apartment-journey', '-research-ai-check',
            '-batchmode', '-nographics', '-logFile', str(ROOT/'Logs/ai-player-journey.log')],
            cwd=ROOT, timeout=180, check=True)
        if not target.exists() or target.stat().st_mtime < started:
            raise RuntimeError('Fresh isolated journey output is missing')
        result = json.loads(target.read_text(encoding='utf-8-sig'))
        if not result['passed']:
            raise RuntimeError('Packaged journey failed: '+result.get('detail', ''))
        (ROOT/'Docs/ai-player-journey.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    else:
        extra = ['-research-ai-check']
        if args.mode == 'replay':
            extra.append('-rehab-force-replay')
        result = run('ai-player-'+args.mode, extra, args.mode == 'rendered')
    target = ROOT / 'Logs/ai-player-checks.json'
    if not target.exists() or target.stat().st_mtime < started:
        raise RuntimeError('Fresh packaged AI acceptance output is missing')
    model = json.loads(target.read_text(encoding='utf-8-sig'))
    if not model['passed']:
        raise RuntimeError('Packaged model failed: '+model.get('error', ''))
    evidence = {'mode':args.mode, 'passed':True, 'game':result, 'packaged_model':model}
    (ROOT/'Docs'/('ai-player-'+args.mode+'-combined.json')).write_text(json.dumps(evidence, indent=2), encoding='utf-8')
    print(json.dumps({'mode':args.mode, 'passed':True, 'game_checks':result.get('count', len(result.get('checks', []))),
        'model_assertions':model['assertions'], 'onnx_difference':model['onnxDifference']}))


if __name__ == '__main__':
    main()
