# Decisions (one-line ADRs)

Full project decisions: `Docs/DECISIONS.md` and the decisions table in the upgrade plan (D1–D10).

- 2026-10-27 ADR-AI-1: Claude Code setup is project-scoped (`.mcp.json`, `.claude/`). One Unity MCP (CoplayDev MCP for Unity 10.0.0, HTTP 127.0.0.1:8080) plus the official Unity Agent Plugin skills.
- 2026-10-27 ADR-AI-2: Context7 runs on the free tier with no key, only when an API or version is uncertain.
- 2026-10-27 ADR-AI-3: Work goes on `claude/<feature>` branches from the current work branch. WBS commit prefixes are kept, and merges happen only on the user's approval.
- 2026-10-27 ADR-AI-4: `Logs/` stays readable, because it holds build and probe evidence. Library/Temp/Obj/Builds/UserSettings are denied to Read.
- 2026-10-27 ADR-AI-5: Skills are manual-only (`disable-model-invocation`), with Sonnet for routine work and Opus for architecture and debugging.
- 2026-10-27 ADR-AU-1: All game audio is synthesised in code (`RehabSoundBank`): six material families, variant pools, loudness targets with a −1 dBFS ceiling. No recordings, no new dependencies.
- 2026-10-27 ADR-AU-2: `GripRehabMixer` (Master, SFX, Ambience, Voice) is generated once by `RehabAudioMixerBuilder`, which calls Unity's editor `AudioMixerController` by reflection (there is no public API). The asset is committed; the runtime falls back to per-source volumes without it.
- 2026-10-27 ADR-AU-3: Impacts are detected in both `OnCollisionEnter` and `OnCollisionStay` (with a firmer threshold), from the fastest contact point, because a landing on a contact the engine kept alive sends no Enter.
- 2026-10-27 ADR-VO-1: Voice uses only Windows on-device keyword recognition (commands and NATO/letter spelling). Free dictation is not offered because Windows dictation is an online service; the user chose offline spelling (D9 revised).
- 2026-10-27 ADR-VO-2: The voice router is Unity-free and tested from transcripts; voice never produces grip force, losing-progress commands always need "confirm", and low-confidence results ask before acting.
- 2026-11-06 ADR-WBS-1: Game code lives in `Assets/Rehab/<WBS>`. The assembly layers `Product`, `ProductIntegration`, `UI`/`UIBridge` and `_Project/Core` span several WBS IDs, so they stay as layers rather than being split by WBS (the user's choice). `Docs/ai/PROJECT_MAP.md` § "WBS → code" is the index for them.
