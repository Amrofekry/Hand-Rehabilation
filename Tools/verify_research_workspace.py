"""Re-run software-only repository checks without implying device or clinical QA."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import platform
import re
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[1]
EXPECTED_SOURCES = {
    "Hand_Rehab_Pipeline.pdf": "d23b3b8ed6fa476acdb9da0f0308589037ae82f3460566a2ade883d45408199e",
    "ganttchart.html": "53456905baa976e8d3c3cc54b4710073bddca71061b18e43eb8a9573ca90cc1c",
    "Blockdiagram.pdf": "9dfc8cb44a41d71b35517ab20cc6d64bce49e7557c20a3fe39f4da3100eecf80",
}


def hash_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def source_check() -> None:
    for name, expected in EXPECTED_SOURCES.items():
        actual = hash_file(ROOT / "Docs/Source" / name)
        if actual != expected:
            raise AssertionError(f"source fingerprint mismatch: {name}")
    imported = json.loads((ROOT / "Docs/Source/IMPORT_MANIFEST.json").read_text(encoding="utf-8-sig"))
    for entry in imported["files"]:
        path = ROOT / entry["path"]
        if not path.is_file() or hash_file(path) != entry["sha256"]:
            raise AssertionError(f"import changed without provenance update: {entry['path']}")


def meta_check() -> None:
    ids: set[str] = set()
    for meta in (ROOT / "Assets").rglob("*.meta"):
        match = re.search(r"^guid:\s*([0-9a-f]{32})", meta.read_text(encoding="utf-8-sig"), re.M)
        if not match or match.group(1) in ids:
            raise AssertionError(f"missing/duplicate GUID: {meta}")
        ids.add(match.group(1))
    for path in (ROOT / "Assets/_Project").rglob("*"):
        if path.suffix == ".meta" or path.name.endswith(".meta"):
            continue
        if not Path(str(path) + ".meta").exists():
            raise AssertionError(f"missing owned Unity metadata: {path}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--record", action="store_true", help="write result to Docs/evidence")
    args = parser.parse_args()
    checks: list[dict[str, str]] = []
    commands = [
        ("vision", [sys.executable, "-m", "unittest", "discover", "-s", "Tools/Vision", "-p", "test_*.py"]),
        ("phase2-bridge", [sys.executable, "-m", "unittest", "discover", "-s", "Tools/Phase2Bridge", "-p", "test_*.py"]),
        ("firmware", [sys.executable, "firmware/run_tests.py"]),
        ("cad-r1", [sys.executable, "hardware/2.1-interchangeable-grips/export_parts.py", "--verify-only"]),
        ("cad-r2", [sys.executable, "hardware/2.1-interchangeable-grips/rev2/build.py", "--verify-only"]),
        ("mechanical", [sys.executable, "-m", "unittest", "discover", "-s", "tests/2.1-mechanical"]),
        ("feature-windows", [sys.executable, "-m", "unittest", "discover", "-s", "tests/4.2-data-schema"]),
        ("decision-policy", [sys.executable, "-m", "unittest", "discover", "-s", "tests/4.5-decision-policy"]),
        ("bluetooth-integration", [sys.executable, "-m", "unittest", "discover", "-s", "tests/6.1-bluetooth-integration"]),
    ]
    for name, command in commands:
        result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True, errors="replace")
        checks.append({"name": name, "status": "PASS" if result.returncode == 0 else "FAIL",
                       "exit_code": str(result.returncode), "last_output": (result.stdout + result.stderr)[-1200:]})
    for name, check in (("source-provenance", source_check), ("unity-metadata", meta_check)):
        try:
            check()
            checks.append({"name": name, "status": "PASS", "exit_code": "0", "last_output": ""})
        except Exception as error:
            checks.append({"name": name, "status": "FAIL", "exit_code": "1", "last_output": str(error)})
    result = {
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "project": str(ROOT),
        "python": platform.python_version(),
        "platform": platform.platform(),
        "scope": "software-only; Unity Editor/player, hardware and clinical tests not included",
        "passed": all(c["status"] == "PASS" for c in checks),
        "checks": checks,
    }
    if args.record:
        destination = ROOT / "Docs/evidence/software-checks.json"
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"passed": result["passed"], "checks": [
        {"name": c["name"], "status": c["status"]} for c in checks]}, indent=2))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
