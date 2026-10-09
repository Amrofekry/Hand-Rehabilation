"""Native Unity checks for the hand/contact repair; run with the Editor closed.

No scene generation, package installation, webcam capture or user records.
"""
import argparse
import json
import os
import re
from pathlib import Path
import shutil
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / 'Docs/evidence/interaction-2026-10-09'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--cycles', action='store_true', help='Three cold scene-load Play/Stop cycles')
    parser.add_argument('--unity', type=Path, default=Path(os.environ.get('ProgramFiles', 'C:/Program Files')) / 'Unity/Hub/Editor/6000.4.4f1/Editor/Unity.exe')
    args = parser.parse_args()
    if not args.unity.is_file():
        raise FileNotFoundError('Unity 6000.4.4f1 not found; pass --unity with its executable path')
    log = ROOT / 'Logs' / ('contact-play-cycles.log' if args.cycles else 'contact-review-build.log')
    log.parent.mkdir(parents=True, exist_ok=True)
    method = ('GripRehab.Editor.SoftBodyVerification.VerifyAndCycles' if args.cycles
              else 'GripRehab.Editor.HandInteractionCorrection.Verify')
    command = [str(args.unity), '-batchmode', '-nographics', '-projectPath', str(ROOT),
               '-executeMethod', method, '-logFile', str(log)]
    command += ['-apartment-deformation-cycles'] if args.cycles else ['-quit']
    started = time.time()
    process = subprocess.Popen(command, cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.STDOUT)
    try:
        code = process.wait(timeout=360)
        if not log.is_file() or log.stat().st_mtime < started:
            raise RuntimeError('Unity did not produce a fresh log')
        output = log.read_text(encoding='utf-8-sig', errors='replace')
        if code:
            raise RuntimeError(f'Unity exited {code}; inspect {log}\n{output[-3000:]}')
        EVIDENCE.mkdir(parents=True, exist_ok=True)
        if args.cycles:
            paths = [ROOT/'Logs'/name for name in ('scene-play-result.json', 'scene-play-cycles.txt', 'deformation-mesh-cleanup.txt')]
            result = json.loads(paths[0].read_text(encoding='utf-8-sig'))
            if not result.get('passed') or result.get('cycles') != 3:
                raise RuntimeError('Play/Stop cycle checks failed')
        else:
            if '[REHAB_BUILD] PASS:' not in output:
                raise RuntimeError('Windows build did not pass')
            paths = [ROOT/'Docs/evidence/deformation-2026-10-08/solver-tests.json',
                     ROOT/'Docs/evidence/hand-correction-2026-10-08/native-surface-tests.json',
                     EVIDENCE/'burst-reference.json']
            result = {'passed': True, 'scope': 'Native surface, solver, scene references and Windows build'}
        for path in paths:
            if not path.is_file() or path.stat().st_mtime < started:
                raise RuntimeError('Missing or stale native evidence: '+str(path))
            if path.parent != EVIDENCE:
                shutil.copyfile(path, EVIDENCE/path.name)
        # Preserve diagnostic markers without retaining licensing identifiers.
        redacted = re.sub(r'(?im)^(.*(?:Machine Id|Session Id|Correlation Id|External correlation Id|hubSessionId|accessToken):).*$', r'\1 [redacted]', output)
        (EVIDENCE/log.name).write_text(redacted, encoding='utf-8')
        print(json.dumps(result))
    finally:
        if process.poll() is None:
            process.terminate()
            process.wait(timeout=15)


if __name__ == '__main__':
    main()
