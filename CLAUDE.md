# GripRehab (Unity capstone)

Hand-grip rehabilitation game: the apartment and its activities, hand tracking by camera, force by BLE or keyboard demo, AI adaptation, and patient and doctor dashboards.

- **Unity** 2022.3.20f1, URP 14.0.10, uGUI, VirtualGrasp (free tier). Windows player.
- **Project rules:** `../AGENTS.md`, the WBS in `PROJECT_PLAN.md`, the upgrade plan in `Docs/superpowers/plans/2026-10-23-ultimate-upgrade-plan.md`.
- **Session start:** read `Docs/ai/STATE.md`. Read other docs on demand: `Docs/ai/PROJECT_MAP.md`, `ARCHITECTURE.md`, `DECISIONS.md`, `Docs/STATUS.md`.

## Commands
- **Build (batch, Editor closed):**
  `"/c/Program Files/Unity/Hub/Editor/2022.3.20f1/Editor/Unity.exe" -batchmode -nographics -quit -projectPath . -executeMethod GripRehab.Editor.RehabVerification.BuildWindows -logFile Logs/build.log`
  On a first compile, "could not be found" lines are harmless if the log ends in `Build Finished, Result: Success`.
- **Offline C#:** `ai-engine/.venv-ai/Scripts/python.exe Tools/offline_verify.py --meta --compile --tests`
- **Python:** `python -m unittest discover -s Tools -p "test_*.py"`. Run `cd ai-engine && .venv-ai/Scripts/python.exe -m pytest -q`.
- **Player suites** (one at a time; two players at once break the UDP ports):
  `Tools/run_ai_player_checks.py smoke|replay|journey`, `run_ui_checks.py`, `run_gallery_checks.py`, `run_perf_probe.py`, `run_contact_probe.py [--grip-lab|--material-lab]`, `run_activity_probe.py`
- **EditMode:** `Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml`
- **Console zero (Editor closed):** `Unity.exe -projectPath . -executeMethod GripRehab.Editor.ConsoleZeroCheck.Run -logFile Logs/console-zero.log`: opens the Editor, plays 25 s with voice on, passes only at 0 errors / 0 warnings / 0 logs and the MCP bridge up (`Logs/console-zero.json`).
- **Protected files:** after editing a hash-pinned file, record it with `python Docs/evidence/<stage>/update_review.py <path> "<reason>"`.

## Conventions
- Folders follow WBS IDs (`Assets/Rehab/3.4.1-...`). Pure logic is kept in Unity-free files so the offline harness can compile it.
- Match the surrounding style. Many apartment files are compact one-liners, so keep their density.
- Use no allocations per frame (NonAlloc queries, no LINQ in Update). The perf probe enforces this.
- Every input path must keep working: mouse, keyboard, hand pointer, camera. **The camera hand is the main input**: every new action needs a hand gesture or the hand pointer, voice for special actions, keys and mouse as options (`Docs/HAND_CONTROLS.md`).

## Rules
- **The Unity console stays at 0 errors, 0 warnings, 0 logs.** Fix the source; only known, documented info lines go to `Logs/editor-info.log` (`EditorConsoleHygiene`), never errors. Run the console-zero check after Unity changes.
- Everything lives on drive E (`.local/`, `CVTraining/`); nothing project-related on C.
- Grep before reading; read targeted line ranges; never scan `Library/`, `Temp/` or `Builds/`.
- Ask before editing `Packages/manifest.json`, `ProjectSettings/`, scenes or prefabs. Edit scenes and prefabs through UnityMCP, never raw YAML.
- Don't modify vendor code (VirtualGrasp, ThirdParty, Brick Project Studio).
- Never claim clinical or physical validation. Label SYNTHETIC, by-eye and physical results separately.
- No purchases, emails, pushes or destructive git without explicit approval.
- Use Context7 only when an API or version is uncertain, never by default.
- Use no subagents unless parallel read-only work is clearly cheaper.

## Git
- One branch per task: `claude/<feature>`, from the current work branch. Make small commits and a checkpoint before risky changes.
- Commit subjects start with the WBS ID and end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Stage named paths only.
- No remote is configured, so nothing is pushed.

## Tokens
- Stay terse: tables and bullets, and list paths instead of pasting files.
- Run long suites in the background; don't poll.
- `/clear` between unrelated tasks, `/compact` at about 60 % context, and `/context` to inspect usage.
- Use Sonnet for routine work and Opus for `/unity-architecture` and hard debugging. Skills set `model:`.
- Skills (manual only): `/unity-scan`, `/unity-debug`, `/unity-feature`, `/unity-architecture`, `/unity-optimize`, `/unity-save-state`.
