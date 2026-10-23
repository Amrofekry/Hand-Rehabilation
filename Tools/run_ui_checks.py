"""Opt-in display tests: use isolated profiles, never open a physical webcam."""
from pathlib import Path
import json
import shutil
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
EXE = ROOT / 'Builds/Windows/GripRehab.exe'

def run(width, height):
    started = time.time()
    log = ROOT / f'Logs/ui-player-{width}x{height}.log'
    result_path = ROOT / 'Logs/ui-presentation-checks.json'
    command = [str(EXE), '-apartment-ui-smoke', '-force-d3d11', '-screen-fullscreen', '0',
               '-screen-width', str(width), '-screen-height', str(height), '-logFile', str(log)]
    with subprocess.Popen(command, cwd=ROOT) as process:
        try:
            code = process.wait(timeout=100)
        except subprocess.TimeoutExpired:
            process.terminate()
            raise
    if not result_path.exists() or result_path.stat().st_mtime < started:
        raise RuntimeError('Player did not write fresh UI QA output; ensure current scripts were built')
    result = json.loads(result_path.read_text(encoding='utf-8-sig'))
    output = log.read_text(errors='replace')
    if code or not result['passed'] or '[UI_PRESENTATION] PASS' not in output:
        raise RuntimeError(f'UI check failed: {code}, {result.get("error", "")}')
    for marker in ('Exception', '[PROFILE]', 'Failed to store screen shot'):
        if marker in output:
            raise RuntimeError(f'Unexpected player log marker: {marker}')
    names = [f'theme-{i}-{view}.png' for i in range(3)
             for view in ('home', 'appearance', 'appearance-large')]
    source = ROOT / 'Logs/ui-screenshots'
    destination = ROOT / f'Logs/ui-screenshots-{width}x{height}'
    destination.mkdir(parents=True, exist_ok=True)
    for name in names:
        path = source / name
        if not path.exists() or path.stat().st_mtime < started or path.stat().st_size < 1024:
            raise RuntimeError(f'Fresh screenshot missing: {name}')
        shutil.copy2(path, destination / name)
    result['exit'] = code
    result['screenshots'] = [str(destination / name) for name in names]
    (ROOT / f'Docs/ui-player-{width}x{height}.json').write_text(json.dumps(result, indent=2))
    return result

if __name__ == '__main__':
    results = [run(1280, 720), run(1600, 900)]
    (ROOT / 'Docs/ui-player-checks.json').write_text(json.dumps(results, indent=2))
    print(json.dumps([{'size': f'{r["width"]}x{r["height"]}', 'checks': len(r['checks']),
                       'passed': r['passed']} for r in results], indent=2))
