# Skills, plugins and connectors: checklist (2026-10-29)

Status:
- ✅ installed and working
- 🔁 used on demand (instruction-only skill, nothing to install)
- ⚠️ needs you
- ⛔ not applicable to this project

## Installed or set up in this session (everything on drive E)

| Tool | Status | Where / how |
|---|---|---|
| graphify | ✅ installed and run | `uv tool install graphifyy` into `.local/uv-tools`; exe in `.local/bin`. Code graph in `graphify-out/` (5183 nodes, 14360 edges, 255 communities); `graphify-out/graph.html` |
| playwright-cli | ✅ 0.1.22 | `npm i -g @playwright/cli` into `.local/npm` (npm prefix and cache moved to E; on your PATH) |
| Context7 (ctx7 CLI) | ✅ 0.8.2 | `.local/npm/ctx7`; the Context7 MCP connector is also connected |
| UnityMCP | ✅ stdio | `.mcp.json`; bridge on port 6400; checked by ConsoleZeroCheck |
| ck (Context Keeper) | ⚠️ blocked | The skill shipped without its scripts. The official installer (github.com/sreedhargs89/context-keeper, cloned to `.local/scratch/ck-repo`) was blocked: running external code needs your OK. To install it yourself: `bash E:/GP/codex/GripRehab-Unity/.local/scratch/ck-repo/install.sh` (it adds a SessionStart hook to `~/.claude/settings.json`) |
| archify | ⚠️ missing package | The skill needs its `bin/archify.mjs`, which is not shipped with the skill. The npm package named "archify" is an unrelated 0.0.4 project, so I did not install it. I need its real source from you |
| token-optimizer | ⚠️ missing package | Needs its `measure.py` plugin install; the source is not in the skill. I need its source or the plugin to be installed |
| paperclip | ⛔ | Needs a Paperclip company/server (PAPERCLIP_API_URL, agent id). There is no Paperclip workspace for this project |
| 21st (21st-ui) | ⚠️ | The plugin is enabled, but its MCP server is not loaded in this session. Enable or authorize it in the plugin settings, then restart. Free tier: 2 installs a day. It is web React UI, used as design reference only |
| Figma (figma-use) | ⚠️ | The connector failed to connect (needs authorization through /mcp) |
| tabbit | ⚠️ | Needs the Tabbit Browser app (`tabbit-cli.exe` under LocalAppData), which is not installed |

## Ready, used when their step comes (nothing to install)

| Tool | Used for |
|---|---|
| superpowers (using-superpowers, brainstorming, systematic-debugging, verification) | Process |
| unity-perf: csharp-zero-gc ✅ loaded, code-standards, unity-performance | Hand control, performance |
| unity: physics-3d-collision ✅ loaded, ui-ugui, ui, optimize-audio, audio-setup-mixers, urp-postprocessing, optimize-text-mesh-pro, unity-cli, unity-package-management, generate-editor-search-query, validate-urp-render-graph-renderer-feature, localization (Arabic) | Final polish and Stage 15A |
| Project skills: unity-scan, unity-debug, unity-feature, unity-optimize, unity-architecture, unity-save-state | Manual only (`disable-model-invocation`). You run them with `/unity-...` |
| code-review, security-review | Final review of the diff |
| docs, docx, pdf, frontend-design, deep-research, skill-creator | Guide, report and Stage 17 research |
| computer-use, chrome-browser, browser-use | Visual checks if needed |
| animation-studio, claude-skills-exporter, cowork-plugin-management | Available; not needed for the game |

## Not applicable to this game (not used)

- **Built for other kinds of games:**
  - unity:2d-pixel-perfect, sprite-editor, sprite-segment-3x3grid, manage-sprite-atlas, tilemap-*: 2D games
  - implement-in-app-purchases, levelplay-unity-integration: store purchases and ads
  - build-live-game, setup-multiplayer-services, setup-vivox-voice-chat: online services
  - initialize-ai-navigation: NPC navigation
  - shader-graph-create-custom-node
- **Not needed here:**
  - migrate-birp-to-urp: the project is already URP
  - new-unity-project: the project already exists
  - optimize-web: Windows build, not WebGL
- **Commands you run yourself:** /context, /doctor, /feedback, /plugin, /skills, /design.
