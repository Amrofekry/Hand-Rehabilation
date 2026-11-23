"""Console zero (2026-11-23): Unity warns "Setting linear/angular velocity of a kinematic body is not supported" when code
sets a kinematic Rigidbody's velocity. Every velocity write in the game code must be guarded by an isKinematic check or
follow a write that makes the body dynamic (isKinematic=false) within the three lines before it."""
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WRITE = re.compile(r'\b\w+\.(velocity|angularVelocity)\s*=(?!=)')
# A real guard: a "not kinematic" check, or making the body dynamic first ("isKinematic=true" before the write is the bug).
GUARD = re.compile(r'!\s*[\w.]*isKinematic|isKinematic\s*=\s*false')


class KinematicVelocityTests(unittest.TestCase):
    def test_velocity_writes_are_guarded(self):
        problems = []
        for path in (ROOT / 'Assets' / 'Rehab').rglob('*.cs'):
            lines = path.read_text(encoding='utf-8', errors='replace').splitlines()
            for i, line in enumerate(lines):
                if not WRITE.search(line) or 'emit.velocity' in line:  # particle emit parameters, not bodies
                    continue
                window = '\n'.join(lines[max(0, i - 3):i + 1])
                if not GUARD.search(window):
                    problems.append(f'{path.relative_to(ROOT)}:{i + 1}: {line.strip()[:100]}')
        self.assertEqual(problems, [], 'velocity set without an isKinematic guard:\n' + '\n'.join(problems))


if __name__ == '__main__':
    unittest.main()
