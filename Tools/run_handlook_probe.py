"""Hand look and walk probe: SYNTHETIC camera-hand frames over the loopback vision port; turning, walking, stopping and the setting.

  python Tools/run_handlook_probe.py
SYNTHETIC hand frames in the rendered player; isolated acceptance storage, never a webcam.
Writes Docs/handlook-probe.json. How it feels with your real hand is a separate human check.
"""
from pathlib import Path
import json
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'Builds/Windows/GripRehab.exe'


def main():
    started = time.time()
    result_path = ROOT / 'Logs/handlook-probe.json'
    command = [str(EXE), '-apartment-handlook-probe', '-force-d3d11', '-screen-fullscreen', '0',
               '-screen-width', '1280', '-screen-height', '720', '-logFile', str(ROOT / 'Logs/handlook-probe.log')]
    with subprocess.Popen(command, cwd=ROOT) as process:
        try:
            code = process.wait(timeout=300)
        except subprocess.TimeoutExpired:
            process.terminate()
            raise RuntimeError('Player hung during the hand-look probe')
    if not result_path.exists() or result_path.stat().st_mtime < started:
        raise RuntimeError('Player did not write fresh hand-look output; ensure current scripts were built')
    result = json.loads(result_path.read_text(encoding='utf-8-sig'))
    result['exit'] = code
    (ROOT / 'Docs/handlook-probe.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    for check in result.get('checks') or []:
        print(f"{'PASS' if check['passed'] else 'FAIL'} {check['name']}: {check['value']}")
    if code or not result['passed']:
        raise RuntimeError(f'Hand-look probe failed: exit {code}, {result.get("detail", "")}')


if __name__ == '__main__':
    main()
