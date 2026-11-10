# Retired tools (kept for the record, not run)

| Tool | Retired | Why |
|---|---|---|
| `verify_ui_boundary.py` with `ui-protected-baseline.json` and its last output `ui-boundary-check.json` | 2026-11-10 | A one-time UI05 check that compared 992 files with a frozen 2026-09-29 snapshot and allowed 5 edits. It already reported 12 other changed files on 2026-10-06, and every later stage changed more (70 on 2026-11-10), so it can never pass again. Protected files are now guarded one by one, each change with a reason, by `Tools/test_handmate_design.py` and `Docs/evidence/final-2026-10-29/protected-source-review.json`. |

The script's paths assume its old place in `Tools/`; it is here to read, not to run.
