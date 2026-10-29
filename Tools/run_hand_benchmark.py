"""Camera-hand control benchmark (SYNTHETIC noisy frames over the real vision port; no camera or person).

  python Tools/run_hand_benchmark.py <label>      # e.g. before / after
Writes Docs/evidence/final-2026-10-29/hand-benchmark-<label>.json and prints the metrics.
"""
from pathlib import Path
import json
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'Builds/Windows/GripRehab.exe'


def main():
    label = sys.argv[1] if len(sys.argv) > 1 else 'run'
    started = time.time(); out = ROOT / 'Logs/hand-benchmark.json'
    cmd = [str(EXE), '-apartment-handbench-probe', '-force-d3d11', '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720',
           '-logFile', str(ROOT / 'Logs/hand-benchmark.log')]
    with subprocess.Popen(cmd, cwd=ROOT) as p:
        try:
            code = p.wait(timeout=300)
        except subprocess.TimeoutExpired:
            p.terminate(); raise RuntimeError('Player hung during the hand benchmark')
    if not out.exists() or out.stat().st_mtime < started:
        raise RuntimeError('No fresh benchmark output; rebuild the player')
    result = json.loads(out.read_text(encoding='utf-8-sig')); result['exit'] = code
    dest = ROOT / 'Docs/evidence/final-2026-10-29' / f'hand-benchmark-{label}.json'; dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    for k, v in result.items():
        if k not in ('scope', 'detail'):
            print(f'{k}: {v}')
    if result.get('detail'):
        print('detail:', result['detail'][:400])


if __name__ == '__main__':
    main()
