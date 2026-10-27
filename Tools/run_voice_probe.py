"""Stage 13 voice probe: recognizer start on this computer, then SYNTHETIC transcripts through the router and game actions.

  python Tools/run_voice_probe.py
SYNTHETIC transcripts in the rendered player; isolated acceptance storage, never a webcam. The Windows recognizer is started once on the real microphone (a platform check, no speech judged) and then turned off.
Writes Docs/voice-probe.json. Recognition of a real voice is a separate human check.
"""
from pathlib import Path
import json
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'Builds/Windows/GripRehab.exe'


def main():
    started = time.time()
    result_path = ROOT / 'Logs/voice-probe.json'
    command = [str(EXE), '-apartment-voice-probe', '-force-d3d11', '-screen-fullscreen', '0',
               '-screen-width', '1280', '-screen-height', '720', '-logFile', str(ROOT / 'Logs/voice-probe.log')]
    with subprocess.Popen(command, cwd=ROOT) as process:
        try:
            code = process.wait(timeout=300)
        except subprocess.TimeoutExpired:
            process.terminate()
            raise RuntimeError('Player hung during the voice probe')
    if not result_path.exists() or result_path.stat().st_mtime < started:
        raise RuntimeError('Player did not write fresh voice output; ensure current scripts were built')
    result = json.loads(result_path.read_text(encoding='utf-8-sig'))
    result['exit'] = code
    (ROOT / 'Docs/voice-probe.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    for check in result.get('checks') or []:
        print(f"{'PASS' if check['passed'] else 'FAIL'} {check['name']}: {check['value']}")
    if code or not result['passed']:
        raise RuntimeError(f'Voice probe failed: exit {code}, {result.get("detail", "")}')


if __name__ == '__main__':
    main()
