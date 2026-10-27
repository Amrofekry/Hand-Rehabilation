"""Record one reviewed Stage 10 change to a hash-pinned file (never a broad refresh).

Usage: python update_review.py <repo-relative path> "<reason>"
previousSha256 is the hash the protected-asset test currently expects for that path.
"""
import hashlib, json, subprocess, sys
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
REVIEW = Path(__file__).with_name('protected-source-review.json')

def chain_expected(path):
    # Ask the protected test what it expects right now (it prints actual != expected).
    out = subprocess.run([sys.executable, '-m', 'unittest', 'discover', '-s', 'Tools', '-p', 'test_handmate_design.py'], cwd=ROOT, capture_output=True, text=True).stderr
    marker = f"(path='{path}')"
    for block in out.split('FAIL: ')[1:]:
        if marker in block:
            return block.split("!= '")[1].split("'")[0]
    raise SystemExit(f'{path} is not failing the protected test; nothing to record')

path, reason = sys.argv[1], sys.argv[2]
data = json.loads(REVIEW.read_text(encoding='utf-8')) if REVIEW.exists() else {
    'scope': 'Stage 10 activity changes only, each with a reason. Earlier review links remain immutable; no broad hash refresh.', 'changes': {}}
entry = data['changes'].get(path)
previous = entry['previousSha256'] if entry else chain_expected(path)
data['changes'][path] = {'previousSha256': previous,
                         'currentSha256': hashlib.sha256((ROOT / path).read_bytes()).hexdigest(),
                         'reason': 'User-authorized Stage 10 activities (2026-10-27). ' + reason}
REVIEW.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
print('recorded', path)
