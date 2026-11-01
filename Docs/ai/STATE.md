# State (2026-11-01)

**Branch:** `claude/final-release` (linear chain from `3.4-upgrade-baseline`). Not merged; nothing pushed.

## Resume here (2026-11-01, final release run)
- **Plan:** `C:/Users/lenovo/.claude/plans/alaways-use-graphify-of-generic-yeti.md`. **Branch:** `claude/final-release`.
- **Done:**
  - **A, CV hybrid hand control:** `Docs/HAND_CONTROL_REPORT.md`. SYNTHETIC benchmark: rest jitter 17.5 → 9.9 mm, settle 62 → 20 ms; 0.7 s gap, activity and pinch grabs now hold.
  - **B, A-OB1:** not reproduced in 3 activity runs × 12 (2026-11-01). Root cause unproven; it stays in regression.
  - **D, Stage 18:** calibration wizard, device status panel, `Docs/PHYSICAL_TEST_PROTOCOL.md`.
  - **E, review:** code review found 10 issues and 7 are fixed; the security pass was done by hand.
  - **F, Stages 16/17:** `Docs/STAGE16_AUDIT.md`, `STAGE17_RESEARCH.md`.
  - **H, guide:** `Docs/GUIDE_AR.md/.pdf/.docx`, `Docs/architecture/`.
  - **I, optional plan:** `Docs/OPTIONAL_PLAN.md`.
  - **G, release:** `Tools/release/` (stager, first-run check, Inno Setup script; Inno in `.local/innosetup`).
- **Full regression (2026-11-01):** build, 16 player suites, EditMode 10/10, offline, Python, pytest 32, console 0/0/0 with MCP. All green.
- **Deferred:** C, Stage 15A environment polish (cosmetic; risks the perf/gallery baselines).
- **J (2026-11-01):** merged (baseline fast-forwarded) and tagged v1.0.0. The outer folders now sit in `02_Hardware`, `03_Docs`, `04_Delivery` and `99_Archive`. The Arduino caches moved into `firmware/`.
- **Waiting on you:**
  - Run `.local/scratch/block-j-cleanup.ps1` (about 14 GB; the classifier blocked me from running it).
  - With Claude and Unity closed, run `E:\GP\codex\Move-GameTo01.ps1`. It moves the game to `01_Game` and fixes the paths.

## Done
- Hand-first controls (2026-10-28): edge look, point to walk, fist grab, pinch options, twist placement; voice for special actions; console zero with MCP over stdio.
- Stage 13 voice: offline keyword commands, spelling with review, confirmations, feedback; off by default. Offline router tests 140, voice probe 28/28 (SYNTHETIC). Free dictation Blocked (online-only; your choice).
- Stage 12 audio: synthesised material sounds from real events, 3D, GripRehabMixer with 4 group settings, menu fade, seamless loops. EditMode 10/10, audio probe 17/17 (SYNTHETIC).
- Stage 11 UI design system: tokens, contrast-aware focus, pause panel, onboarding, input strip, 4:3 fit. UI checks at 4 resolutions × 3 themes pass.
- a8ff3df: Claude Code setup.
- 0f6e5e2: Stage 10 six activities. SYNTHETIC probe 12/12, full regression passes.
- 0d47b17: Stage 9 deformation.
- 5b44747: Stage 8 contact and the 18-grip chart.
- Claude Code setup: unity-mcp 10.0.0, uv, `.mcp.json` (UnityMCP and context7), deny rules, 6 skills, CLAUDE.md, Docs/ai.

## In progress
- None. Stages 11, 12 and 13 are waiting for review.

## Next steps
1. Your Stage 11 to 13 reviews: a listening session (S-07, S-09) and a microphone session (V-01, V-09, V-10).
2. Stage 14 eye tracking (needs authorization).
3. Merge `claude/*` branches into `3.4-upgrade-baseline` when you approve.

## Open bugs
- A-OB1: intermittent activity grab failures (4 of 6 runs on 2026-10-28). Not reproduced in 3 runs on 2026-11-01. Kept under watch.

## Pending user checks
- Hands-on playtest of the Stage 10 activities.
- Camera-hand run of an activity (A-13).
- By-eye review of Stage 8 grips and Stage 9 D-02.
- Unity MCP live test: open the Editor, start the server, restart Claude Code.

## Decisions
See `Docs/ai/DECISIONS.md`.
