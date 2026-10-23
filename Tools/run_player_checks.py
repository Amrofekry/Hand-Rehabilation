"""Run opt-in apartment acceptance checks in real Windows players.
Software-only transport fixtures never establish physical BLE/camera acceptance.
"""
from pathlib import Path
import json, subprocess, time
ROOT=Path(__file__).resolve().parents[1]
EXE=ROOT/'Builds/Windows/GripRehab.exe'
SCREENSHOTS=(
    'apartment-options', 'vg-ball-before', 'vg-ball-deformed',
    'apartment-seated-grip', 'vg-mug-fractured', 'apartment-kitchen-grip',
    'apartment-two-hands-tracked', 'vg-left-kitchen-grip',
    'apartment-overview', 'ui-main-menu', 'ui-new-profile',
    'ui-module-selection',
)

def run(name, extra, rendered=False):
    log=ROOT/'Logs'/f'{name}.log'
    result_file=ROOT/'Logs/apartment-runtime-checks.json'
    flags=['-apartment-smoke','-logFile',str(log),'-force-d3d11']
    if rendered: (ROOT/'Logs/player-screenshots').mkdir(parents=True,exist_ok=True)
    if rendered: flags+=['-screen-fullscreen','0','-screen-width','1280','-screen-height','720']
    else: flags+=['-batchmode','-nographics']
    started=time.time()
    process=subprocess.Popen([str(EXE),*flags,*extra],cwd=ROOT)
    try:
        # The rendered run also captures screenshots and exercises the full
        # camera hand workspace; slower integrated GPUs can take several minutes.
        code=process.wait(timeout=300 if rendered else 180)
        if not log.exists() or log.stat().st_mtime < started or not result_file.exists() or result_file.stat().st_mtime < started:
            raise RuntimeError(f'{name}: player exited {code} without fresh acceptance output; check Windows launch and {log}')
        output=log.read_text(errors='replace')
        result=json.loads(result_file.read_text(encoding='utf-8-sig'))
        if code!=0 or not result['passed'] or '[APARTMENT_TEST] PASS:' not in output:
            raise RuntimeError(f'{name}: exit {code}; passed={result["passed"]}; error={result.get("error", "")}\n{output[-2500:]}')
        for marker in ('NullReferenceException','MissingReferenceException','MissingComponentException','Unhandled Exception',
                       'UnauthorizedAccessException','Failed to store screen shot','[APARTMENT_TEST] FAIL:'):
            if marker in output: raise RuntimeError(f'{name}: {marker}')
        if rendered:
            for screenshot in SCREENSHOTS:
                source=ROOT/'Logs/player-screenshots'/f'{screenshot}.png'
                if not source.exists() or source.stat().st_mtime < started or source.stat().st_size < 1024:
                    raise RuntimeError(f'{name}: fresh screenshot missing: {screenshot}')
            result['screenshots']=[str(ROOT/'Logs/player-screenshots'/f'{name}.png') for name in SCREENSHOTS]
        result.update(name=name,exit=code,rendered=rendered)
        (ROOT/'Docs'/f'{name}.json').write_text(json.dumps(result,indent=2))
        return result
    finally:
        if process.poll() is None: process.terminate(); process.wait(timeout=10)

if __name__=='__main__':
    results=[run('apartment-player-headless',[]),run('apartment-player-replay',['-rehab-force-replay']),run('apartment-player-rendered',[],True)]
    (ROOT/'Docs/player-checks.json').write_text(json.dumps(results,indent=2))
    print(json.dumps([{'test':r['name'],'passed':r['passed'],'checks':len(r['checks'])} for r in results],indent=2))
