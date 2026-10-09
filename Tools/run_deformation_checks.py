"""Run six-object fixtures in the real Windows player, without live CV/BLE."""
from pathlib import Path
import argparse, json, subprocess, time, shutil
ROOT=Path(__file__).resolve().parents[1]
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--rendered',action='store_true');parser.add_argument('--quick',action='store_true');parser.add_argument('--mode',choices=['Keyboard','Phase2Replay','CameraReplay']);parser.add_argument('--evidence-directory',type=Path,default=ROOT/'Docs/evidence/deformation-2026-10-08');args=parser.parse_args()
    kind=('rendered' if args.rendered else 'headless')+('-quick' if args.quick else '')+('-'+args.mode if args.mode else '')
    log=ROOT/f'Logs/deformation-{kind}.log';result=ROOT/'Logs/deformation-player.json'
    command=[str(ROOT/'Builds/Windows/GripRehab.exe'),'-apartment-deformation','-rehab-force-replay','-logFile',str(log)]
    if args.rendered:command+=['-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-force-d3d11']
    else:command+=['-batchmode','-nographics']
    if args.quick:command += ['-deformation-quick']
    if args.mode:command += ['-deformation-mode',args.mode]
    started=time.time();p=subprocess.Popen(command,cwd=ROOT)
    try:
        code=p.wait(timeout=420)
        if not result.exists() or result.stat().st_mtime<started:raise RuntimeError('No fresh player result; exit='+str(code))
        data=json.loads(result.read_text(encoding='utf-8-sig'))
        out=args.evidence_directory.resolve();out.mkdir(exist_ok=True,parents=True)
        (out/f'player-{kind}.json').write_text(json.dumps(data,indent=2))
        if code or not data['passed']:raise RuntimeError(data.get('error','Player failed'))
        if args.rendered:
            images=out/'images';images.mkdir(exist_ok=True)
            for image in (ROOT/'Logs/deformation-images').glob('*.png'):
                if image.stat().st_mtime>=started:shutil.copy2(image,images/image.name)
        print(json.dumps(dict(mode=kind,passed=data['passed'],cases=len(data['rows']),checks=data['checks'],fps=data['measuredFps'],contactMeanMs=data['contactMeanMs'])))
    finally:
        if p.poll() is None:p.terminate();p.wait(timeout=15)
if __name__=='__main__':main()
