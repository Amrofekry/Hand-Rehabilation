---
name: unity-architecture
description: Ranked architecture review (asmdefs, coupling, singletons, cycles, Update misuse); updates ARCHITECTURE.md and DECISIONS.md only
disable-model-invocation: true
model: opus
argument-hint: "[folder or asmdef]"
allowed-tools: Bash(git ls-files:*), Grep, Glob, Read, Edit(./Docs/ai/ARCHITECTURE.md), Edit(./Docs/ai/DECISIONS.md), Write(./Docs/ai/ARCHITECTURE.md), Write(./Docs/ai/DECISIONS.md)
---

Review only. No code changes without explicit approval.

1. Read `Docs/ai/PROJECT_MAP.md` and `Docs/ai/ARCHITECTURE.md`. Scope to the argument if one is given.
2. Grep, without full reads:
   - asmdef `references`, to build the dependency graph and check for cycles;
   - `static .* Instance`, `FindObjectOfType|FindAnyObjectByType|GameObject.Find`, `GetComponent` inside `Update`/`FixedUpdate`/`LateUpdate`;
   - empty `Update()` methods;
   - cross-folder `using` directives (coupling);
   - folder naming against WBS conventions.
3. Read only the specific line ranges that confirm a finding.
4. Rank the findings by impact. Each one gets file:line, the issue, the risk and a suggested fix (≤ 2 lines).
5. Update `Docs/ai/ARCHITECTURE.md` (graph, layers, rules) and add one-line ADRs to `Docs/ai/DECISIONS.md` for each decision taken.
6. Output ≤ 15 lines: the top findings, then a request for approval before any fix.
