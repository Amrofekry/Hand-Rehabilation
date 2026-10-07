# Flat scene folder — 2026-10-07

The user requested scene files directly in Assets/Scenes, with no nested Archive/Workspace folders.

## Delivered layout

- Assets/Scenes/3.1-Bootstrap.unity: renamed startup scene, same GUID.
- Assets/Scenes/3.1-Apartment.unity
- Assets/Scenes/3.1-Lighting.unity
- Assets/Scenes/3.2-PlayerAndSensors.unity
- Assets/Scenes/3.3-VirtualHands.unity
- Assets/Scenes/3.4.1-Interaction.unity
- Assets/Scenes/3.4-Interface.unity

The recovery scene and scene guide moved to Assets/SceneBackups, outside Scenes. The two old nested folders were removed only after verifying they were empty. All .meta identities were preserved using AssetDatabase.MoveAsset. The seven scene payloads remain byte-identical to their prior locations, verified after the Windows build. Gameplay/hand tracking/interaction were not changed.

The composition manifest, startup constants, active/global build scene lists, existing editor path references, tests and live editing instructions were updated. Historical evidence still records its original paths.

## Verification

- Native asset migration and all 26 cross-scene reference bindings: passed (scene-flatten-checks.json, scene-composition-checks.json).
- Native Unity import/compilation and Windows build: succeeded with Unity 6000.4.4f1; flat-build-batch.log contains REHAB_BUILD PASS.
- Root Python tests: 67 passed, including the direct-files-only directory check and preserved authored wiring.
- Byte hashes: all seven scene files match the pre-move payloads (scene-bytes-preserved.json).
- Updated Windows cold-start acceptance: 168 native player assertions passed at 2026-10-07T13:44:09.5658724Z (player-smoke.json).

The first move attempt rolled back after closing scenes unloaded the Resources manifest. Reacquiring the manifest through AssetDatabase after the moves fixed it. The interactive Editor was subsequently closed; successful migration and build used hidden native Unity batch sessions. No pending Editor gate request remains.

This folder organization change does not update the graduation backlog or claim live participant/hardware verification. The previously documented separate account/journey limitation remains in the earlier scene-organization report.
