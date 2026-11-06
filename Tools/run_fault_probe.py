"""Release QA fault probe: injected faults in the rendered player must be explained to the player, never crash, never log.

  python Tools/run_fault_probe.py
Cases: camera that cannot open (absent index 9, so no webcam is used), hand stream lost and back, force sensor stale,
disconnected and reconnected (SYNTHETIC replay frames), no microphone (simulated), missing fatigue model, and damaged
saved profiles in a throwaway folder. Writes Docs/fault-probe.json. All results are SYNTHETIC.
"""
from pathlib import Path
import json
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'Builds/Windows/GripRehab.exe'


def main():
    started = time.time()
    result_path = ROOT / 'Logs/fault-probe.json'
    command = [str(EXE), '-apartment-faults', '-rehab-force-replay', '-force-d3d11', '-screen-fullscreen', '0',
               '-screen-width', '1280', '-screen-height', '720', '-logFile', str(ROOT / 'Logs/fault-probe.log')]
    with subprocess.Popen(command, cwd=ROOT) as process:
        try:
            code = process.wait(timeout=240)
        except subprocess.TimeoutExpired:
            process.terminate()
            raise RuntimeError('Player hung during the fault probe')
    if not result_path.exists() or result_path.stat().st_mtime < started:
        raise RuntimeError('Player did not write fresh fault output; ensure current scripts were built')
    result = json.loads(result_path.read_text(encoding='utf-8-sig'))
    result['exit'] = code
    (ROOT / 'Docs/fault-probe.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    for check in result.get('checks') or []:
        print(f"{'PASS' if check['passed'] else 'FAIL'} {check['name']}: {check['value']}")
    for line in result.get('logLines') or []:
        print('LOG', line)
    if code or not result['passed']:
        raise RuntimeError(f'Fault probe failed: exit {code}, {result.get("detail", "")}')


if __name__ == '__main__':
    main()
