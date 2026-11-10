"""The full regression: every software check of the project, in order, one game player at a time.

  ai-engine/.venv-ai/Scripts/python.exe Tools/run_full_regression.py [--skip-build]
Run with the Unity Editor closed. Writes Logs/full-regression.txt (progress) and Docs/full-regression.json (the result of
each suite, with the commit). Exits 1 if any suite fails. Input is SYNTHETIC throughout; no webcam, device or person.
Tools/test_repo_hygiene.py checks that every Tools/run_*.py suite and every tests/ folder is listed here.
"""
from pathlib import Path
import argparse
import json
import os
import re
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
UNITY = Path('C:/Program Files/Unity/Hub/Editor/2022.3.20f1/Editor/Unity.exe')
PY = str(ROOT / 'ai-engine/.venv-ai/Scripts/python.exe')
CAMERA_PY = str(ROOT / '.venv/Scripts/python.exe')
LOGS = ROOT / 'Logs'

# One rendered or headless player at a time: the suites share UDP ports 27817-27819.
PLAYER_SUITES = [
    'run_player_checks.py',
    'run_ai_player_checks.py smoke', 'run_ai_player_checks.py replay', 'run_ai_player_checks.py rendered', 'run_ai_player_checks.py journey',
    'run_ui_checks.py', 'run_gallery_checks.py', 'run_perf_probe.py',
    'run_contact_probe.py', 'run_contact_probe.py --grip-lab', 'run_contact_probe.py --material-lab',
    'run_activity_probe.py', 'run_activity_probe.py', 'run_activity_probe.py', 'run_activity_probe.py --camera',
    'run_handlook_probe.py', 'run_hand_benchmark.py after', 'run_voice_probe.py', 'run_audio_probe.py', 'run_fault_probe.py',
]
UNIT_SUITES = [  # (name, interpreter, arguments, working directory)
    ('offline C# (meta, compile, tests)', PY, ['Tools/offline_verify.py', '--meta', '--compile', '--tests'], ROOT),
    ('python: Tools', PY, ['-m', 'unittest', 'discover', '-s', 'Tools', '-p', 'test_*.py'], ROOT),
    ('python: tests/2.3-calibration', PY, ['-m', 'unittest', 'discover', '-s', 'tests/2.3-calibration', '-p', 'test_*.py'], ROOT),
    # Firmware C++ and 2.4, Phase2Bridge, CAD, tests/2.1, 4.2, 4.5, 6.1-bluetooth-integration, source fingerprints.
    ('workspace: verify_research_workspace.py', PY, ['Tools/verify_research_workspace.py', '--record'], ROOT),
    ('camera models: check_research_camera_models.py', CAMERA_PY, ['Tools/check_research_camera_models.py'], ROOT),
    ('pytest: ai-engine', PY, ['-m', 'pytest', '-q'], ROOT / 'ai-engine'),
]


def call(command, cwd=ROOT, timeout=1800, log=None, env=None):
    started = time.time()
    try:
        done = subprocess.run(command, cwd=cwd, timeout=timeout, capture_output=True, text=True, errors='replace', env=env)
        code, text = done.returncode, done.stdout + done.stderr
    except subprocess.TimeoutExpired as error:
        code, text = 124, f'timed out after {timeout} s\n{error.stdout or ""}'
    if log:
        (LOGS / log).write_text(text if isinstance(text, str) else str(text), encoding='utf-8', errors='replace')
    return code, text, round(time.time() - started, 1)


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--skip-build', action='store_true', help='reuse the existing Windows build (not a release-grade run)')
    args = parser.parse_args()
    LOGS.mkdir(exist_ok=True)
    progress = LOGS / 'full-regression.txt'
    progress.write_text('', encoding='utf-8')
    results = []
    commit = subprocess.run(['git', 'rev-parse', '--short', 'HEAD'], cwd=ROOT, capture_output=True, text=True).stdout.strip()
    dirty = [line[3:] for line in subprocess.run(['git', 'status', '--short'], cwd=ROOT, capture_output=True, text=True).stdout.splitlines()
             if not line[3:].startswith(('Docs/', 'ai-engine/reports/'))]

    def save(complete):
        summary = {'passed': complete and all(r['passed'] for r in results), 'complete': complete,
                   'utc': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
                   'commit': commit, 'uncommittedSourceChanges': dirty, 'buildSkipped': args.skip_build,
                   'scope': 'SYNTHETIC input in the Windows build and offline tests; no webcam, device or person',
                   'suites': len(results), 'results': results}
        (ROOT / 'Docs/full-regression.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
        return summary

    def record(name, passed, seconds, detail=''):
        results.append({'suite': name, 'passed': bool(passed), 'seconds': seconds, 'detail': detail})
        save(False)
        with progress.open('a', encoding='utf-8') as stream:
            stream.write(f"{'PASS' if passed else 'FAIL'} {name} ({seconds} s) {detail}\n")
        print(f"{'PASS' if passed else 'FAIL'} {name} {detail}", flush=True)

    env = dict(os.environ)
    env['PATH'] = os.pathsep.join(['C:\\msys64\\mingw64\\bin', str(ROOT / '.local/bin'), env.get('PATH', '')])
    env.update(UV_SYSTEM_CERTS='1', UV_CACHE_DIR=str(ROOT / '.local/cache/uv'), UPM_CACHE_ROOT=str(ROOT / '.local/cache/unity-upm'))
    project = ['-projectPath', str(ROOT)]

    if args.skip_build:
        record('build', True, 0, 'SKIPPED (--skip-build): existing build reused')
    else:
        log = LOGS / 'full-build.log'
        _, _, seconds = call([str(UNITY), '-batchmode', '-nographics', '-quit', *project, '-executeMethod',
                              'GripRehab.Editor.RehabVerification.BuildWindows', '-logFile', str(log)], timeout=3600)
        text = log.read_text(encoding='utf-8', errors='replace') if log.exists() else ''
        found = re.findall(r'Build Finished, Result: (\w+)', text)
        record('build', bool(found) and found[-1] == 'Success', seconds, found[-1] if found else 'no build result in the log')

    if results[-1]['passed']:
        for suite in PLAYER_SUITES:
            code, text, seconds = call([PY, *('Tools/' + suite).split()], log='full-' + re.sub(r'[^a-z0-9]+', '-', suite.lower()).strip('-') + '.out')
            record(suite, code == 0, seconds, '' if code == 0 else text.strip().splitlines()[-1][:200] if text.strip() else f'exit {code}')

        xml = LOGS / 'editmode.xml'
        if xml.exists(): xml.unlink()
        _, _, seconds = call([str(UNITY), '-batchmode', '-nographics', *project, '-runTests', '-testPlatform', 'EditMode',
                              '-testResults', str(xml), '-logFile', str(LOGS / 'full-editmode.log')], timeout=1800)
        counts = re.search(r'total="(\d+)" passed="(\d+)" failed="(\d+)"', xml.read_text(encoding='utf-8', errors='replace')) if xml.exists() else None
        record('EditMode', bool(counts) and int(counts[1]) > 0 and counts[1] == counts[2] and counts[3] == '0', seconds,
               f'{counts[2]}/{counts[1]} passed' if counts else 'no result file')

        zero = LOGS / 'console-zero.json'
        if zero.exists(): zero.unlink()
        _, _, seconds = call([str(UNITY), *project, '-executeMethod', 'GripRehab.Editor.ConsoleZeroCheck.Run',
                              '-logFile', str(LOGS / 'console-zero.log')], timeout=900, env=env)
        report = json.loads(zero.read_text(encoding='utf-8-sig')) if zero.exists() else {}
        record('console zero', report.get('passed') is True and report.get('startup') == '0,0,0' and report.get('afterPlay') == '0,0,0', seconds,
               f"startup {report.get('startup')}, after play {report.get('afterPlay')}, MCP {report.get('mcpServer')} {report.get('problem', '')}".strip())

    for name, interpreter, arguments, cwd in UNIT_SUITES:
        code, text, seconds = call([interpreter, *arguments], cwd=cwd, env=env, log='full-' + re.sub(r'[^a-z0-9]+', '-', name.lower()).strip('-') + '.out')
        last = text.strip().splitlines()[-1][:160] if text.strip() else ''
        record(name, code == 0, seconds, last if code else last if 'passed' in last or last == 'OK' else '')

    summary = save(True)
    with progress.open('a', encoding='utf-8') as stream:
        stream.write(('ALL PASSED' if summary['passed'] else 'FAILED') + f" {sum(r['passed'] for r in results)}/{len(results)} at {commit}\n")
    return 0 if summary['passed'] else 1


if __name__ == '__main__':
    sys.exit(main())
