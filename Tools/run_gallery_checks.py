"""Rendered UI-only sandbox checks, isolated from camera and profile data."""
import json
import shutil
import subprocess
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def run(width, height):
    stamp = time.time()
    size = f"{width}x{height}"
    log = ROOT / "Logs" / f"gallery-{size}.log"
    process = subprocess.Popen([str(ROOT / "Builds/Windows/GripRehab.exe"),
        "-apartment-gallery-smoke", "-force-d3d11", "-screen-fullscreen", "0",
        "-screen-width", str(width), "-screen-height", str(height), "-logFile", str(log)], cwd=ROOT)
    try:
        code = process.wait(timeout=100)
        path = ROOT / "Logs/gallery-checks.json"
        if not path.exists() or path.stat().st_mtime < stamp or not log.exists() or log.stat().st_mtime < stamp:
            raise RuntimeError(f"{size}: no fresh gallery result")
        result = json.loads(path.read_text(encoding="utf-8-sig"))
        output = log.read_text(errors="replace")
        if code or not result["passed"] or "[UI_GALLERY] PASS:" not in output:
            raise RuntimeError(f"{size}: {result.get('error', '')}\n{output[-2000:]}")
        for marker in ("NullReferenceException", "MissingReferenceException", "Unhandled Exception"):
            if marker in output: raise RuntimeError(f"{size}: {marker}")
        if (result["width"], result["height"]) != (width, height): raise RuntimeError("Unexpected player size")
        for i, theme in enumerate(("dark", "light", "contrast")):
            source = ROOT / f"Logs/gallery-screenshots/gallery-theme-{i}.png"
            if not source.exists() or source.stat().st_mtime < stamp or source.stat().st_size < 1024:
                raise RuntimeError(f"{size}: missing fresh image {source}")
            target = ROOT / f"Docs/screenshots/{theme}/en/{size}/gallery.png"
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
        return result
    finally:
        if process.poll() is None: process.terminate(); process.wait(timeout=10)

if __name__ == "__main__":
    results = [run(w, h) for w, h in ((1280, 720), (1280, 800), (1024, 768))]
    (ROOT / "Docs/gallery-checks.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(json.dumps([{"size": f"{r['width']}x{r['height']}", "passed": r["passed"], "checks": len(r["checks"])} for r in results], indent=2))
