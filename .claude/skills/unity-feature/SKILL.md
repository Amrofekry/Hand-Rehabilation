---
name: unity-feature
description: Plan (≤10 lines), wait for OK, then branch, implement minimally, compile-check and commit
disable-model-invocation: true
model: sonnet
argument-hint: "<feature description>"
allowed-tools: Bash(git switch:*), Bash(git status:*), Bash(git add:*), Bash(git commit:*), Bash(git diff:*), Grep, Glob, Read, Edit, Write, mcp__UnityMCP__refresh_unity, mcp__UnityMCP__read_console
---

1. Read `Docs/ai/ARCHITECTURE.md` and the relevant lines of `Docs/ai/PROJECT_MAP.md`. Grep before reading code.
2. Post a plan of ≤ 10 lines: files to touch, approach, and test or verification. **Stop and wait for the user's OK.**
3. After the OK:
   - if the tree is dirty, ask for a checkpoint commit;
   - otherwise run `git switch -c claude/<short-feature>`.
4. Implement in the fewest files. Follow ARCHITECTURE.md conventions and match the surrounding code style.
5. Edit scenes and prefabs through UnityMCP tools, never raw YAML. Ask before editing `Packages/manifest.json`, `ProjectSettings/`, scenes or prefabs.
6. Compile check:
   - `mcp__UnityMCP__refresh_unity`, then `read_console` for errors;
   - with no Editor, use the batch build from CLAUDE.md.
7. Commit named paths only. The subject starts with the WBS ID (see PROJECT_PLAN.md) and ends with the attribution line.
8. Output ≤ 15 lines: branch, files, verification, commit sha.
