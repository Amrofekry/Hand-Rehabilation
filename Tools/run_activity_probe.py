"""Stage 10 activity probe (all six activities at levels 2 and 8) plus the Stage 11 UI checks; SYNTHETIC input.

  python Tools/run_activity_probe.py              # activities and UI checks at 1600x900
  python Tools/run_activity_probe.py --ui-only    # UI checks only, at 1024x768, 1280x720, 1280x800 and 1600x900
Isolated acceptance storage, never a webcam or device. Writes Docs/activity-probe.json (or Docs/activity-ui-checks.json)
and screenshots under Logs/activity-shots/.
"""
from pathlib import Path
import json
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'Builds/Windows/GripRehab.exe'
SIZES = [(1024, 768), (1280, 720), (1280, 800), (1600, 900)]


def play(width, height, ui_only):
    started = time.time()
    result_path = ROOT / 'Logs/activity-probe.json'
    command = [str(EXE), '-apartment-activity-probe', '-force-d3d11', '-screen-fullscreen', '0',
               '-screen-width', str(width), '-screen-height', str(height), '-logFile', str(ROOT / 'Logs/activity-probe.log')]
    if ui_only:
        command.append('-activity-ui-only')
    command.extend(a for a in sys.argv[1:] if a.startswith('-no-'))  # diagnostics switches pass through
    with subprocess.Popen(command, cwd=ROOT) as process:
        try:
            code = process.wait(timeout=900)
        except subprocess.TimeoutExpired:
            process.terminate()
            raise RuntimeError('Player hung during the activity probe')
    if not result_path.exists() or result_path.stat().st_mtime < started:
        raise RuntimeError('Player did not write fresh activity output; ensure current scripts were built')
    result = json.loads(result_path.read_text(encoding='utf-8-sig'))
    result['exit'] = code
    return result


def show(result):
    for check in result.get('uiChecks') or []:
        print(f"UI {'PASS' if check['passed'] else 'FAIL'} {check['name']}: {check['value']}")
    for run in result.get('runs') or []:
        outcomes = {o['key']: o['value'] for o in run.get('outcomes') or []}
        print(f"L{run['level']} {run['activity'][:26]:26} done={run['completed']!s:5} score={run['score']:3} steps={run['stepsDone']}/{run['stepsTotal']} "
              f"errors={run['errors']} misses={run['misses']} grabsFailed={run['failedGrabs']} t={run['seconds']:.0f}s "
              f"acc={outcomes.get('activity.accuracy', '-')} posMm={outcomes.get('activity.positionErrorMm', '-')}")


def main():
    if '--ui-only' in sys.argv:
        results = []
        for width, height in SIZES:
            result = play(width, height, True)
            print(f'== {width}x{height}: {"PASS" if result["passed"] else "FAIL"}')
            show(result)
            results.append(result)
        (ROOT / 'Docs/activity-ui-checks.json').write_text(json.dumps(results, indent=2) + '\n', encoding='utf-8')
        failed = [r for r in results if r['exit'] or not r['passed']]
        if failed:
            raise RuntimeError('UI checks failed: ' + '; '.join(f"{r['resolution']}: {r.get('detail', '')}" for r in failed))
        return
    result = play(1600, 900, False)
    (ROOT / 'Docs/activity-probe.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    show(result)
    if result['exit'] or not result['passed']:
        raise RuntimeError(f'Activity probe failed: exit {result["exit"]}, {result.get("detail", "")}')


if __name__ == '__main__':
    main()
