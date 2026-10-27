"""Stage 12 audio probe: mixer routing and volumes, menu-to-gameplay fade, grab, strain, release, impacts, rest, fracture.

  python Tools/run_audio_probe.py
SYNTHETIC keyboard-demo input in the rendered player; isolated acceptance storage, never a webcam or device.
Writes Docs/audio-probe.json. It measures events and levels, not how the sounds feel (that is a human listening check).
"""
from pathlib import Path
import json
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'Builds/Windows/GripRehab.exe'


def main():
    started = time.time()
    result_path = ROOT / 'Logs/audio-probe.json'
    command = [str(EXE), '-apartment-audio-probe', '-force-d3d11', '-screen-fullscreen', '0',
               '-screen-width', '1280', '-screen-height', '720', '-logFile', str(ROOT / 'Logs/audio-probe.log')]
    with subprocess.Popen(command, cwd=ROOT) as process:
        try:
            code = process.wait(timeout=300)
        except subprocess.TimeoutExpired:
            process.terminate()
            raise RuntimeError('Player hung during the audio probe')
    if not result_path.exists() or result_path.stat().st_mtime < started:
        raise RuntimeError('Player did not write fresh audio output; ensure current scripts were built')
    result = json.loads(result_path.read_text(encoding='utf-8-sig'))
    result['exit'] = code
    (ROOT / 'Docs/audio-probe.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    for check in result.get('checks') or []:
        print(f"{'PASS' if check['passed'] else 'FAIL'} {check['name']}: {check['value']}")
    if code or not result['passed']:
        raise RuntimeError(f'Audio probe failed: exit {code}, {result.get("detail", "")}')


if __name__ == '__main__':
    main()
