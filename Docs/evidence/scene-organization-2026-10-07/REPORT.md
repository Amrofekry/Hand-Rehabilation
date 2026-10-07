# Scene organization verification أ¢â‚¬â€‌ 2026-10-07

Scope: split the existing authored Unity scene into focused additive scenes. This is separate from the numbered graduation backlog. No game modes, tracking equations, imported VirtualGrasp source or patient data were changed.

## Saved structure

- Entry: Assets/Scenes/GripRehab.unity (same asset GUID as before).
- Six content scenes: apartment; lighting; player/sensors; visible VirtualGrasp hands; interaction; interface.
- Cross-scene manifest: 26 existing links, stable object IDs, bind before activation.
- Original monoscene: Assets/Scenes/Archive/GripRehab-BeforeSceneSplit.unity, excluded from builds.
- Global/active build lists include all content scenes; build reads manifest directly.
- Individual content scenes show an Edit preview. Play from startup/full workspace.

## Evidence collected

- Native Unity source import and reference verification passed: Logs/scene-composition-checks.json.
- 66 Python root tests passed, including the new scene-layout checks and unchanged 91 pinned authored script blocks after in-memory reference reconstruction.
- Offline real-reference Editor/player compilation and managed source suites passed: Logs/scene-offline-all.json. This is not a native camera/hardware test.
- Native cold startup from the bootstrap alone: three Play/Stop cycles passed, all six parts loaded, SDK/hand and room bindings checked, no duplicate camera/EventSystem/receivers, and all three UDP ports released after each cycle (scene-play-result.json).
- A 1 cm hold-anchor edit was saved to its owning PlayerAndSensors scene, survived reopening, then was restored. All seven scene files remained byte-identical through the successful Windows build (scene-persistence-checks.json).
- Windows player acceptance passed 168 headless / 169 synthetic replay / 168 rendered checks. The rendered run includes the full dwell/hand/object suite and fresh 1280أ—720 screenshots; this does not establish live human or hardware tracking.
- Vision bridge 4 tests passed; Phase 2 loopback bridge 4 tests passed outside the filesystem/network sandbox. Inside the sandbox two loopback packets timed out; no bridge production code was changed.
- Native menu screenshot and four fresh object/room screenshots are included beside this report.
- Compiled scene assembly and build content are current. The player executable stub keeps its Unity-distribution timestamp; fresh acceptance outputs and built scene data establish the tested build.

## Limits and separate findings

The existing full gameplay acceptance passed 165 assertions before its focused hand-pointer dwell assertion failed with Application.isFocused=false. Its error identifies focus loss, not a missing EventSystem or invalid button geometry. Focus guards and assertions were not disabled. Desktop automation could not initialize (kernel-assets OS error 3), so the Editor focused-pointer check is not claimed. The subsequent rendered Windows player full pointer suite passed. Live camera/human verification is still not claimed.

The separately attempted account journey reached 37 assertions then failed its Module 2 completed-session synthetic-source check. That is a separate account/backlog concern, not addressed by changing account logic in this scene-only task. It remains recorded in Logs/journey-acceptance.json.

Initial migration error while creating successive untitled additive scenes was fixed by saving each scene before creating the next. The original scene was restored before retry. A play-transition serialization exception was fixed by clearing preview links with direct field assignment outside the save serializer; actual disk saves still use Unity serialized properties.

Existing imported VirtualGrasp warning about no baked grasp database remains. Real participant tracking and physical Nano/HX711 validation are outside this task.

## Delivery state

Unity was left outside Play on the complete workspace (all seven scenes), using the existing beginner-workspace command. The numbered graduation backlog was not changed. Only the scene organization files, related verification adapters and evidence are staged; unrelated existing work remains in the workspace.
