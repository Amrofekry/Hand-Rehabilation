"""Stage 19: assemble the GripRehab release folder, portable zip and installer from an existing player build.

Layout (what VisionCameraLauncher expects: Tools/Vision and models next to the build's parent):
  Game/                 Builds/Windows (GripRehab.exe)
  python/               pinned CPython 3.12 (python-build-standalone) with the worker's packages
  Tools/Vision, Tools/Phase2Bridge, Tools/release/first_run_check.py
  firmware/2.3-load-cell-calibration (calibration wizard and verifier)
  models/hand_landmarker.task, Docs (user guides), LICENSES
The installer creates .venv (--system-site-packages) from python/ on the target machine, offline.
Usage: python Tools/release/stage_release.py [--version 1.0.0] [--no-installer]
Output goes to .local/release (drive E). Nothing is uploaded.
"""
import argparse
import shutil
import subprocess
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / '.local' / 'release'
PY = ROOT / '.local' / 'python' / 'cpython-3.12.16-windows-x86_64-none'
SITE = ROOT / '.venv' / 'Lib' / 'site-packages'
ISCC = ROOT / '.local' / 'innosetup' / 'ISCC.exe'
SKIP = shutil.ignore_patterns('__pycache__', '*.pyc', '.pytest_cache', 'test_*.py')  # project sources only
RAW = shutil.ignore_patterns('__pycache__', '*.pyc', '*_BurstDebugInformation_DoNotShip')  # build and site-packages: keep every module
DOCS = ['GUIDE_AR.md', 'GUIDE_AR.pdf', 'HAND_CONTROLS.md', 'PHONE_CAMERA.md', 'HAND_CONTROL_REPORT.md', 'VOICE_COMMANDS.md', 'PHYSICAL_TEST_PROTOCOL.md', 'HANDS_AND_MEDIAPIPE_AR.md', 'OPTIONAL_PLAN.md']


def copy(src, dst, ignore=SKIP):
    if src.is_dir(): shutil.copytree(src, dst, ignore=ignore, dirs_exist_ok=True)
    else: dst.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(src, dst)


def stage(version):
    app = OUT / f'GripRehab-{version}'
    if app.exists(): shutil.rmtree(app)
    build = ROOT / 'Builds' / 'Windows'
    if not (build / 'GripRehab.exe').exists(): raise SystemExit('Build the player first (RehabVerification.BuildWindows)')
    copy(build, app / 'Game', RAW)
    copy(PY, app / 'python', RAW)
    copy(SITE, app / 'python' / 'Lib' / 'site-packages', RAW)
    for pth in (app / 'python' / 'Lib' / 'site-packages').glob('_virtualenv.*'): pth.unlink()  # uv venv shim, not needed outside a venv
    for rel in ('Tools/Vision/mediapipe_bridge.py', 'Tools/Vision/__init__.py', 'Tools/Vision/replay_vision.py', 'Tools/Phase2Bridge', 'Tools/release/first_run_check.py',
                'firmware/2.3-load-cell-calibration/calibration_wizard.py', 'firmware/2.3-load-cell-calibration/verify_calibration.py',
                'firmware/2.3-load-cell-calibration/README.md', 'models/hand_landmarker.task', 'models/LICENSE-2.0.txt', 'models/README.md'):
        copy(ROOT / rel, app / rel)
    for d in DOCS:
        if (ROOT / 'Docs' / d).exists(): copy(ROOT / 'Docs' / d, app / 'Docs' / d)
    copy(ROOT / 'Docs' / 'CREDITS.md', app / 'LICENSES' / 'CREDITS.md')
    copy(ROOT / 'Docs' / 'AI_THIRD_PARTY.md', app / 'LICENSES' / 'AI_THIRD_PARTY.md')
    copy(PY / 'LICENSE.txt', app / 'LICENSES' / 'PYTHON_LICENSE.txt')
    (app / 'README.txt').write_text(README.format(version=version), encoding='utf-8')
    (app / 'Setup-Portable.cmd').write_text(PORTABLE, encoding='utf-8')
    return app


def zip_dir(app):
    z = OUT / f'{app.name}-portable.zip'
    with zipfile.ZipFile(z, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as f:
        for p in app.rglob('*'):
            if p.is_file(): f.write(p, Path(app.name) / p.relative_to(app))
    return z


README = """GripRehab {version} - hand-grip rehabilitation game (research prototype; not a medical device)

Installed copy: start GripRehab from the Start menu.
Portable zip: run Setup-Portable.cmd once (creates .venv for the camera worker), then Game\\GripRehab.exe.
Check the camera worker: .venv\\Scripts\\python.exe Tools\\release\\first_run_check.py
Force sensor (optional): .venv\\Scripts\\python.exe Tools\\Phase2Bridge\\unity_bridge.py   (needs the BLE grip; see Docs\\PHYSICAL_TEST_PROTOCOL.md)
Controls: Docs\\HAND_CONTROLS.md. Arabic guide: Docs\\GUIDE_AR.pdf. Licences: LICENSES\\.
No video is saved. Results are SYNTHETIC or by-eye unless a physical report says otherwise.
"""
PORTABLE = '@echo off\r\ncd /d "%~dp0"\r\npython\\python.exe -m venv --without-pip --system-site-packages .venv && .venv\\Scripts\\python.exe Tools\\release\\first_run_check.py --no-camera\r\npause\r\n'


def main():
    p = argparse.ArgumentParser(); p.add_argument('--version', default='1.0.0'); p.add_argument('--no-installer', action='store_true'); a = p.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    app = stage(a.version); print('staged', app)
    print('zip', zip_dir(app))
    if not a.no_installer:
        if not ISCC.exists(): raise SystemExit('Inno Setup not found at ' + str(ISCC))
        subprocess.run([str(ISCC), f'/DAppVersion={a.version}', f'/DSourceDir={app}', f'/O{OUT}', str(ROOT / 'Tools' / 'release' / 'GripRehab.iss')], check=True)
        print('installer', OUT / f'GripRehab-Setup-{a.version}.exe')


if __name__ == '__main__':
    main()
