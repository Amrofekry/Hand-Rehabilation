"""Reproduce Phase 1/2 checks and package the current tracked working files."""
import argparse
import csv
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parent
SOURCES = {'Hand_Rehab_Pipeline.pdf': 'd23b3b8ed6fa476acdb9da0f0308589037ae82f3460566a2ade883d45408199e',
           'ganttchart.html': '53456905baa976e8d3c3cc54b4710073bddca71061b18e43eb8a9573ca90cc1c'}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def document_links(root):
    documents = [root/'README.md', root/'PROJECT_PLAN.md']
    for folder in ['docs', 'hardware', 'firmware', 'unity-project', 'ai-engine', 'dashboards']:
        documents += [f for f in (root/folder).rglob('*.md') if not any(s.startswith('.') for s in f.relative_to(root).parts)]
    for file in documents:
        for target in re.findall(r'\]\(([^)]+)\)', file.read_text(encoding='utf-8-sig')):
            target = target.strip('<>').split('#')[0]
            if not target or re.match(r'(https?|mailto):', target):
                continue
            # The plan records historical source locations; a portable copy must
            # use docs/ source copies, not require another PC's Downloads folder.
            if re.match(r'^[A-Za-z]:[/\\]', target) or target.startswith('/'):
                continue
            require((file.parent/target).exists(), f'Broken link: {file}: {target}')


def calendar_check(root):
    path = root/'docs/1.3-events-calendar/generate_calendar.py'
    spec = importlib.util.spec_from_file_location('source_calendar', path)
    calendar = importlib.util.module_from_spec(spec); spec.loader.exec_module(calendar)
    calendar.ROOT = root
    expected = calendar.source_rows()
    with (path.parent/'calendar.csv').open(encoding='utf-8', newline='') as stream:
        actual = list(csv.DictReader(stream))
    require(actual == expected, 'Calendar rows differ from the authoritative Gantt')
    text = (path.parent/'calendar.ics').read_text(encoding='utf-8')
    require(re.findall(r'DTSTART;VALUE=DATE:(\d+)', text) == [r['start'].replace('-', '') for r in expected], 'ICS start dates differ')
    from datetime import date, timedelta
    ends = [(date.fromisoformat(r['end'])+timedelta(days=1)).strftime('%Y%m%d') for r in expected]
    require(re.findall(r'DTEND;VALUE=DATE:(\d+)', text) == ends, 'ICS exclusive end dates differ')
    require(re.findall(r'UID:([^\r\n]+)', text) == [f"griprehab-{r['week']}@local" for r in expected], 'ICS event IDs differ')


def write_archive(root, files, receipt, archive):
    manifest = {}
    temporary = archive.with_suffix('.zip.tmp')
    try:
        with zipfile.ZipFile(temporary, 'w', compression=zipfile.ZIP_DEFLATED) as z:
            for name in sorted(files):
                path = root/name
                require(path.resolve().is_relative_to(root.resolve()) and not path.is_symlink(), f'Unsafe archive path: {name}')
                require(path.is_file(), f'Tracked file is missing: {name}')
                data = path.read_bytes()
                z.writestr(name, data)
                manifest[name] = hashlib.sha256(data).hexdigest()
            z.writestr('delivery/verification.json', json.dumps(receipt, indent=2)+'\n')
            z.writestr('delivery/file-hashes.json', json.dumps(manifest, indent=2)+'\n')
        with zipfile.ZipFile(temporary) as z:
            require(z.testzip() is None, 'Archive CRC failure')
            for name, digest in manifest.items():
                require(hashlib.sha256(z.read(name)).hexdigest() == digest, f'Archive hash mismatch: {name}')
        temporary.replace(archive)
    finally:
        if temporary.exists():
            temporary.unlink()
    return {'path': str(archive), 'files': len(manifest), 'sha256': hashlib.sha256(archive.read_bytes()).hexdigest()}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--compiler'); parser.add_argument('--package', action='store_true')
    args = parser.parse_args()
    out = ROOT/'delivery'; out.mkdir(exist_ok=True)
    receipt = {'scope': 'Phase 1 and Phase 2 local delivery', 'generated_utc': datetime.now(timezone.utc).isoformat(),
               'workspace': str(ROOT), 'status': 'RUNNING', 'checks': [],
               'physical_acceptance': 'PENDING: mechanical/ergonomic review, procurement, assembly and real-device measurements',
               'board_builds': 'Separate firmware/build.ps1 check; not inferred from host tests', 'archive': None}
    def save():
        temporary = out/'verification.json.tmp'
        temporary.write_text(json.dumps(receipt, indent=2)+'\n', encoding='utf-8')
        temporary.replace(out/'verification.json')
    def run(label, command):
        result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True, encoding='utf-8', errors='replace')
        (out/(label+'.log')).write_text(result.stdout+result.stderr, encoding='utf-8')
        receipt['checks'].append({'check': label, 'result': 'PASS' if result.returncode == 0 else 'FAIL'})
        require(result.returncode == 0, label+' failed; see delivery log')
        print(label, 'PASS', flush=True)
    save()
    try:
        run('firmware-tests', [sys.executable, 'firmware/run_tests.py', *(['--compiler', args.compiler] if args.compiler else [])])
        run('cad-r1', [sys.executable, 'hardware/2.1-interchangeable-grips/export_parts.py', '--verify-only'])
        run('cad-r2', [sys.executable, 'hardware/2.1-interchangeable-grips/rev2/build.py', '--verify-only'])
        run('cad-input-tests', [sys.executable, '-m', 'unittest', 'discover', '-s', 'tests/2.1-mechanical', '-v'])
        run('synthetic-monitor', [sys.executable, 'firmware/2.4-wireless-streaming/host/monitor.py', '--demo'])
        for name, digest in SOURCES.items():
            require(hashlib.sha256((ROOT/'docs'/name).read_bytes()).hexdigest() == digest, 'Source mismatch: '+name)
        calendar_check(ROOT); document_links(ROOT)
        receipt['checks'].extend([{'check': c, 'result': 'PASS'} for c in ['source-fingerprints', 'calendar', 'local-document-links']])
        receipt['status'] = 'PASS'
        if args.package:
            require((ROOT/'.git').exists(), 'Packaging requires the project Git repository; verification alone works from an extracted ZIP')
            git = ['git', '-c', 'safe.directory='+ROOT.as_posix()]
            actual = subprocess.check_output([*git, 'rev-parse', '--show-toplevel'], cwd=ROOT, text=True).strip()
            require(Path(actual).resolve() == ROOT.resolve(), 'Refusing to package a parent repository')
            files = list(filter(None, subprocess.check_output([*git, 'ls-files', '-z'], cwd=ROOT).decode().split('\0')))
            receipt['git_head'] = subprocess.check_output([*git, 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
            receipt['archive'] = write_archive(ROOT, files, receipt, out/'hand-rehab-phase1-2.zip')
            print('Verified archive:', receipt['archive']['path'], flush=True)
        save()
        print('PASS: local checks; physical acceptance remains pending', flush=True)
        return 0
    except Exception as error:
        receipt['status'] = 'FAIL'; receipt['error'] = str(error); save()
        print('FAIL:', error, file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
