"""Opt-in native PhysX/VirtualGrasp fixtures. Never opens the real camera."""
from pathlib import Path
import argparse, json, shutil, subprocess, time

ROOT = Path(__file__).resolve().parents[1]

def run(rendered, profiled=False):
    mode = 'rendered' if rendered else 'headless'
    suffix = '-profiled' if profiled else ''
    exe = ROOT / ('Builds/PhysicsHandProfile/GripRehab.exe' if profiled else 'Builds/Windows/GripRehab.exe')
    log = ROOT / f'Logs/physics-hand-{mode}{suffix}.log'
    result = ROOT / 'Logs/physics-hand-player.json'
    evidence = ROOT / 'Docs/evidence/physics-hand-2026-10-08'
    command = [str(exe), '-apartment-physics-hand', '-logFile', str(log)]
    command += ['-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720', '-force-d3d11'] if rendered else ['-batchmode', '-nographics']
    started = time.time()
    process = subprocess.Popen(command, cwd=ROOT)
    try:
        code = process.wait(timeout=120)
        if not result.exists() or result.stat().st_mtime < started:
            raise RuntimeError(f'No fresh physical-hand result; exit={code}; see {log}')
        data = json.loads(result.read_text(encoding='utf-8-sig'))
        evidence.mkdir(parents=True, exist_ok=True)
        destination = evidence / f'player-{mode}{suffix}.json'
        if code or not data['passed']:
            destination = evidence / f'failed-{mode}{suffix}-{int(started)}.json'
        destination.write_text(json.dumps(data, indent=2), encoding='utf-8')
        if code or not data['passed']:
            raise RuntimeError(data.get('error', f'Player exit={code}'))
        if rendered:
            images = evidence / 'images'; images.mkdir(exist_ok=True)
            for image in (ROOT / 'Logs/physics-hand-images').glob('*.png'):
                if image.stat().st_mtime >= started:
                    shutil.copy2(image, images / image.name)
        print(json.dumps(dict(mode=mode, profiled=profiled, passed=True,
            peakPenetrationMm=data['peakPenetrationMm'], peakSkinGapMm=data['peakSkinGapMm'],
            physicsRecorderAvailable=data['physicsRecorderAvailable'])))
    finally:
        if process.poll() is None:
            process.terminate(); process.wait(timeout=15)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--profiled', action='store_true')
    args = parser.parse_args()
    run(False, args.profiled); run(True, args.profiled)
