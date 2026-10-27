---
name: unity-scan
description: Read-only project scan; writes or refreshes Docs/ai/PROJECT_MAP.md (incremental via git diff)
disable-model-invocation: true
model: sonnet
argument-hint: "[full]"
allowed-tools: Bash(git diff:*), Bash(git log:*), Bash(git ls-files:*), Bash(find:*), Bash(ls:*), Grep, Glob, Read(./Docs/ai/**), Read(./Packages/manifest.json), Read(./ProjectSettings/ProjectVersion.txt), Edit(./Docs/ai/PROJECT_MAP.md), Write(./Docs/ai/PROJECT_MAP.md)
---

Scan the Unity project and refresh `Docs/ai/PROJECT_MAP.md`. Read-only except that file.

1. Read the `Last scan:` commit at the top of `Docs/ai/PROJECT_MAP.md`.
   - If it exists and the argument is not `full`, list the changes with `git diff --name-status <commit>..HEAD -- Assets Packages ProjectSettings`. Update only the affected sections.
2. Collect, without reading full files:
   - the tree of `Assets/` to depth ≤ 3 (`find Assets -maxdepth 3 -type d`);
   - asmdefs (`git ls-files '*.asmdef'`), each with its `references` taken from a grep;
   - scenes (`git ls-files '*.unity'`) and the build scenes from a grep of `ProjectSettings/EditorBuildSettings.asset`;
   - packages from `Packages/manifest.json`, and the render pipeline (URP/HDRP/Built-in);
   - the `.cs` count per top-level folder (`git ls-files 'Assets/**/*.cs' | cut -d/ -f1-3 | sort | uniq -c`).
3. Never read `Library/`, `Temp/`, `Builds/` or `Logs/`.
4. Write `PROJECT_MAP.md` in ≤ 80 lines with these sections: `Last scan: <sha> <date>`, Pipeline/packages, Folders, Asmdefs, Scenes, Script counts, Entry points.
5. Output ≤ 15 lines: what changed since the last scan.
