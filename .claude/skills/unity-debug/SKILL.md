---
name: unity-debug
description: Fix one Unity error from the Editor console with the smallest change, then verify it compiles
disable-model-invocation: true
model: opus
argument-hint: "[error text or blank for latest console error]"
allowed-tools: mcp__UnityMCP__read_console, mcp__UnityMCP__refresh_unity, mcp__UnityMCP__find_in_file, Grep, Read, Edit
---

Fix one error. No refactoring and no unrelated edits.

1. Get errors only:
   - `mcp__UnityMCP__read_console` with types=error, count ≤ 10.
   - If the Editor is not connected, use the argument text, or grep `Logs/*build*.log` for `error CS`.
2. Choose the target: the error matching the argument, or the first compile error.
3. Find the file and line in the stack or message. Read only that range (±20 lines) with Read offset/limit.
4. State the root cause in one line. If it is not clear, grep the symbol for usages first.
5. Apply the smallest Edit that fixes the cause, not the symptom.
6. Verify:
   - run `mcp__UnityMCP__refresh_unity` (compile), then read the console again;
   - with no Editor, run `Tools/offline_verify.py --compile`, or a batch build per CLAUDE.md.
7. Output ≤ 15 lines: error, cause, file:line changed, verification result.
   - If the fix needs more than about 20 changed lines or touches protected files, stop and ask.
