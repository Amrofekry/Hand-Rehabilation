"""Measure moving contact in the actual Windows player. No live camera or user data."""
import argparse
import json
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--evidence-directory', type=Path, default=ROOT/'Docs/evidence/interaction-2026-10-09')
    args = parser.parse_args()
    result = ROOT/'Logs/moving-contact.json'
    started = time.time()
    process = subprocess.Popen([str(ROOT/'Builds/Windows/GripRehab.exe'), '-batchmode', '-nographics', '-apartment-moving-contact', '-logFile', str(ROOT/'Logs/moving-contact.log')], cwd=ROOT)
    try:
        code = process.wait(timeout=180)
        if not result.exists() or result.stat().st_mtime < started:
            raise RuntimeError('No fresh moving-contact result; exit='+str(code))
        data = json.loads(result.read_text(encoding='utf-8-sig'))
        out = args.evidence_directory.resolve()
        out.mkdir(parents=True, exist_ok=True)
        (out/'moving-contact.json').write_text(json.dumps(data, indent=2), encoding='utf-8')
        print(json.dumps({'passed':data['passed'], 'cases':len(data['rows']), 'peakPenetrationMm':max(row['peakPenetrationMm'] for row in data['rows']), 'contactMeansMs':[round(row['meanMs'],2) for row in data['rows']]}))
        if code or not data['passed']:
            raise RuntimeError(data.get('error', 'Moving contact failed'))
    finally:
        if process.poll() is None:
            process.terminate()
            process.wait(timeout=15)

if __name__ == '__main__':
    main()
