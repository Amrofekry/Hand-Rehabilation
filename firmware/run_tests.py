"""Run only capstone W2 tests; preserve other projects' test environments."""
from pathlib import Path
import argparse
import shutil
import subprocess
import sys

def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--compiler'); args=parser.parse_args()
    root=Path(__file__).resolve().parent.parent; build=root/'firmware/.build'; build.mkdir(exist_ok=True)
    compiler=args.compiler or shutil.which('clang++') or shutil.which('g++')
    if not compiler: parser.error('A C++11 compiler is required for actual firmware-domain tests')
    include=root/'firmware/2.3-load-cell-calibration/libraries/RehabCore/src'
    extra=['-fuse-ld=lld'] if 'clang' in Path(compiler).name and sys.platform=='win32' else []
    for source in ['tests/2.3-calibration/test_core.cpp','tests/2.3-calibration/test_hx711_ready.cpp','tests/2.4-wireless-streaming/test_telemetry.cpp']:
        target=build/(Path(source).stem+('.exe' if sys.platform=='win32' else ''))
        subprocess.run([compiler,*extra,'-std=c++11','-Wall','-Wextra','-Werror','-I'+str(include),'-I'+str(root/'firmware/2.4-wireless-streaming/grip_stream'),str(root/source),'-o',str(target)],check=True)
        subprocess.run([str(target)],check=True)
    for directory in ['tests/2.3-calibration','tests/2.4-wireless-streaming']:
        subprocess.run([sys.executable,'-m','unittest','discover','-s',str(root/directory),'-v'],check=True)
    print('PASS: C++ domain/protocol executables and Python validation/receiver suites')
if __name__=='__main__': main()
