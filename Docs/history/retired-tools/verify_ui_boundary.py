"""Check historical protected hashes without hiding authorized repair differences."""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
AUTHORIZED = {
    "Assets/Rehab/3.4-game-modules/RehabAppShell.cs",
    "Assets/Rehab/3.4-game-modules/RehabDwellPointer.cs",
    "Assets/Rehab/3.4-game-modules/RehabProfileStore.cs",
    "Assets/Rehab/3.4.1-force-object-interaction/Apartment/ApartmentAcceptance.cs",
    "Assets/Rehab/3.4.1-force-object-interaction/Apartment/ApartmentExperience.cs",
}

if __name__ == "__main__":
    baseline = json.loads((ROOT / "Docs/evidence/ui-protected-baseline.json").read_text(encoding="utf-8-sig"))
    changed = []
    for entry in baseline["entries"]:
        path = ROOT / entry["path"]
        if not path.exists() or hashlib.sha256(path.read_bytes()).hexdigest() != entry["sha256"]:
            changed.append(entry["path"])
    unexpected = sorted(set(changed) - AUTHORIZED)
    result = {"utc": datetime.now(timezone.utc).isoformat(), "baselineUtc": baseline["capturedUtc"],
              "filesCompared": len(baseline["entries"]), "emptyDiff": not changed,
              "authorizedRepairDifferences": sorted(set(changed) & AUTHORIZED),
              "unexpectedDifferences": unexpected, "passed": not unexpected,
              "note": "UI05 and direct interaction/profile replacement repairs; vendor package unchanged."}
    (ROOT / "Docs/evidence/ui-boundary-check.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result, indent=2))
    raise SystemExit(1 if unexpected else 0)
