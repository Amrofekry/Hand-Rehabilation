---
name: unity-save-state
description: Update Docs/ai/STATE.md (done, in progress, next, bugs, decisions; ≤60 lines) and commit docs only
disable-model-invocation: true
model: sonnet
allowed-tools: Bash(git log:*), Bash(git status:*), Bash(git diff:*), Bash(git add Docs/ai/*), Bash(git commit:*), Read(./Docs/ai/**), Read(./Docs/STATUS.md), Edit(./Docs/ai/STATE.md), Write(./Docs/ai/STATE.md)
---

1. Read `Docs/ai/STATE.md`, run `git log --oneline -10`, and run `git status --short`.
2. Rewrite `STATE.md` in ≤ 60 lines with these sections:
   - Branch
   - Done (latest first, with short sha)
   - In progress
   - Next steps (numbered)
   - Open bugs (ID, one line)
   - Decisions (pointer to DECISIONS.md)
   - Pending user checks
3. Facts only, from git, STATUS.md and this session. Keep tested, by-eye and physical results separate.
4. Stage only `Docs/ai/` and commit `1.1 Save AI state` with the attribution line.
5. Output ≤ 5 lines: the sha and the top next step.
