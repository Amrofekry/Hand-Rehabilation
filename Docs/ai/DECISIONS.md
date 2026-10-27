# Decisions (one-line ADRs)

Full project decisions: `Docs/DECISIONS.md` and the decisions table in the upgrade plan (D1–D10).

- 2026-10-27 ADR-AI-1: Claude Code setup is project-scoped (`.mcp.json`, `.claude/`). One Unity MCP (CoplayDev MCP for Unity 10.0.0, HTTP 127.0.0.1:8080) plus the official Unity Agent Plugin skills.
- 2026-10-27 ADR-AI-2: Context7 runs on the free tier with no key, only when an API or version is uncertain.
- 2026-10-27 ADR-AI-3: Work goes on `claude/<feature>` branches from the current work branch. WBS commit prefixes are kept, and merges happen only on the user's approval.
- 2026-10-27 ADR-AI-4: `Logs/` stays readable, because it holds build and probe evidence. Library/Temp/Obj/Builds/UserSettings are denied to Read.
- 2026-10-27 ADR-AI-5: Skills are manual-only (`disable-model-invocation`), with Sonnet for routine work and Opus for architecture and debugging.
