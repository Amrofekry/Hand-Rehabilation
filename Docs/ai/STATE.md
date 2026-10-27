# State (2026-10-27)

**Branch:** `claude/stage13-voice`, from `claude/stage12-audio`, from `claude/stage11-ui`, from `claude/setup`, from `3.4-upgrade-baseline`. Not merged.

## Done
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
- A-OB1: the level-8 simple-meal probe run fails intermittently (1 of 3 full runs).

## Pending user checks
- Hands-on playtest of the Stage 10 activities.
- Camera-hand run of an activity (A-13).
- By-eye review of Stage 8 grips and Stage 9 D-02.
- Unity MCP live test: open the Editor, start the server, restart Claude Code.

## Decisions
See `Docs/ai/DECISIONS.md`.
