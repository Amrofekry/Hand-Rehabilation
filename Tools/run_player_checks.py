"""Run opt-in apartment acceptance checks in real Windows players.
Software-only transport fixtures never establish physical BLE/camera acceptance.
"""
from pathlib import Path
import json, subprocess
ROOT=Path(__file__).resolve().parents[1]
EXE=ROOT/'Builds/Windows/GripRehab.exe'

def run(name, extra, rendered=False):
    log=ROOT/'Logs'/f'{name}.log'
    flags=['-apartment-smoke','-logFile',str(log),'-force-d3d11']
    if rendered: flags+=['-screen-fullscreen','0','-screen-width','1280','-screen-height','720']
    else: flags+=['-batchmode','-nographics']
    process=subprocess.Popen([str(EXE),*flags,*extra],cwd=ROOT)
    try:
        # The rendered run also captures screenshots and exercises the full
        # camera hand workspace; slower integrated GPUs can take several minutes.
        code=process.wait(timeout=300 if rendered else 100)
        output=log.read_text(errors='replace')
        result=json.loads((ROOT/'Logs/apartment-runtime-checks.json').read_text(encoding='utf-8-sig'))
        if code!=0 or not result['passed'] or '[APARTMENT_TEST] PASS:' not in output:
            raise RuntimeError(f'{name}: exit {code}\n{result}\n{output[-2500:]}')
        for marker in ('NullReferenceException','MissingReferenceException','Unhandled Exception','[APARTMENT_TEST] FAIL:'):
            if marker in output: raise RuntimeError(f'{name}: {marker}')
        result.update(name=name,exit=code,rendered=rendered)
        (ROOT/'Docs'/f'{name}.json').write_text(json.dumps(result,indent=2))
        return result
    finally:
        if process.poll() is None: process.terminate(); process.wait(timeout=10)

if __name__=='__main__':
    results=[run('apartment-player-headless',[]),run('apartment-player-replay',['-rehab-force-replay']),run('apartment-player-rendered',[],True)]
    (ROOT/'Docs/player-checks.json').write_text(json.dumps(results,indent=2))
    print(json.dumps([{'test':r['name'],'passed':r['passed'],'checks':len(r['checks'])} for r in results],indent=2))
