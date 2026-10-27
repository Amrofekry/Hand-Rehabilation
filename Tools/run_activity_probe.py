"""Stage 10 activity probe: all six activities at levels 2 and 8, SYNTHETIC keyboard-demo input.

  python Tools/run_activity_probe.py
Isolated acceptance storage, never a webcam or device. Writes Docs/activity-probe.json and screenshots
under Logs/activity-shots/.
"""
from pathlib import Path
import json
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'Builds/Windows/GripRehab.exe'


def main():
    started = time.time()
    result_path = ROOT / 'Logs/activity-probe.json'
    command = [str(EXE), '-apartment-activity-probe', '-force-d3d11', '-screen-fullscreen', '0',
               '-screen-width', '1600', '-screen-height', '900', '-logFile', str(ROOT / 'Logs/activity-probe.log')]
    with subprocess.Popen(command, cwd=ROOT) as process:
        try:
            code = process.wait(timeout=900)
        except subprocess.TimeoutExpired:
            process.terminate()
            raise RuntimeError('Player hung during the activity probe')
    if not result_path.exists() or result_path.stat().st_mtime < started:
        raise RuntimeError('Player did not write fresh activity output; ensure current scripts were built')
    result = json.loads(result_path.read_text(encoding='utf-8-sig'))
    (ROOT / 'Docs/activity-probe.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    for run in result['runs']:
        outcomes = {o['key']: o['value'] for o in run.get('outcomes') or []}
        print(f"L{run['level']} {run['activity'][:26]:26} done={run['completed']!s:5} score={run['score']:3} steps={run['stepsDone']}/{run['stepsTotal']} "
              f"errors={run['errors']} misses={run['misses']} grabsFailed={run['failedGrabs']} t={run['seconds']:.0f}s "
              f"acc={outcomes.get('activity.accuracy', '-')} posMm={outcomes.get('activity.positionErrorMm', '-')}")
    if code or not result['passed']:
        raise RuntimeError(f'Activity probe failed: exit {code}, {result.get("detail", "")}')


if __name__ == '__main__':
    main()
