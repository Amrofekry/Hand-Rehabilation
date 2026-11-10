"""Keeps the full regression complete and the documents linked: nothing can be left out or left dangling silently."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]


class RepoHygieneTests(unittest.TestCase):
    def test_full_regression_lists_every_suite(self):
        runner = (ROOT / 'Tools/run_full_regression.py').read_text(encoding='utf-8')
        covered = runner + (ROOT / 'Tools/verify_research_workspace.py').read_text(encoding='utf-8') + (ROOT / 'firmware/run_tests.py').read_text(encoding='utf-8')
        for script in sorted((ROOT / 'Tools').glob('run_*.py')):
            if script.name != 'run_full_regression.py':
                self.assertTrue(script.name in runner, f'{script.name} is not in the full regression')
        modes = re.search(r"choices=\[([^\]]+)\]", (ROOT / 'Tools/run_ai_player_checks.py').read_text(encoding='utf-8'))[1]
        for mode in re.findall(r"'(\w+)'", modes):
            self.assertTrue('run_ai_player_checks.py ' + mode in runner, f'mode {mode} is not in the full regression')
        for flag in ('--grip-lab', '--material-lab', '--camera'):
            self.assertTrue(flag in runner, f'{flag} is not in the full regression')
        for folder in sorted({p.parent for p in (ROOT / 'tests').glob('*/test_*.*') if p.suffix in ('.py', '.cpp')}):
            self.assertTrue('tests/' + folder.name in covered, f'tests/{folder.name} is run by no regression step')
        for tool in ('offline_verify.py', 'verify_research_workspace.py', 'check_research_camera_models.py', 'ConsoleZeroCheck', 'EditMode', 'pytest'):
            self.assertTrue(tool in runner, f'{tool} is not in the full regression')

    def test_document_links_resolve(self):
        link = re.compile(r'\]\(([^)#\s]+)(?:#[^)]*)?\)')
        broken = []
        for folder in ('', 'ai', 'history', 'history/retired-tools'):
            for doc in sorted((ROOT / 'Docs' / folder).glob('*.md')):
                for target in link.findall(doc.read_text(encoding='utf-8', errors='replace')):
                    if not re.match(r'^[a-z]+:', target) and not (doc.parent / target).exists():
                        broken.append(f'{doc.relative_to(ROOT).as_posix()} -> {target}')
        self.assertEqual(broken, [])


if __name__ == '__main__':
    unittest.main()
