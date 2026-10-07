# Single game scene restored — 2026-10-07

User requested undoing the scene partition and returning to one working scene.
The current authored content was merged through native Unity APIs rather than
restoring the older backup or reverting unrelated source changes.

Assets/Scenes contains only GripRehab.unity and GripRehab.unity.meta. Its original
GUID, 0d609e8f6ef162e45a37a4c467a3d5bf, is preserved. All 972 authored GameObjects
were retained. The six generated wrappers, bootstrap object and reference markers
were removed. The composition manifest and six content scene assets were retired.
All 26 former cross-scene links are ordinary saved native references now; they
were verified against their actual object identities after closing/reopening.
Prefab overrides and lighting sun reference are retained. The optional gallery
outside Scenes and the older recovery copy outside Scenes are unchanged.

The original split layout and build settings are preserved in before-merge.zip
outside Assets. Startup constants, authoring tools, active/global build lists,
file inspection tests and live editing instructions point to GripRehab.unity.
The scene-loading helpers also support normal single-scene verification.

## Actual verification

- Native Unity 6000.4.4f1 import/compilation, merge and reload: PASS.
- Native scene script/material and apartment/UI reference validation: PASS.
- Root Python suite: 66 tests passed, including immutable vendor/source checks
  and all 91 originally pinned authored gameplay component blocks.
- Native Windows build: PASS; Builds/Windows/GripRehab.exe rebuilt.
- Fresh headless Windows player: 168 assertions passed at 14:22:23Z.
- Native Editor: three cold Play/Stop cycles passed at 14:23:54Z, covering
  one camera/EventSystem/input receiver per type, room/hand bindings, VirtualGrasp
  initialization, deformation initialization, usable UI and UDP socket cleanup.
- One working scene with no nested folders confirmed. Final scene hash matches
  9545f4e28977e082765ef7f949fd47ad56857f1d1231a049e43baf58e63d4bd2
  recorded after the build and before the Play/Stop tests.

Gameplay equations, patient data, hardware contracts and imported SDK code were
not changed by this task. No human camera trial or real hardware test was claimed.
No rendered visual QA was repeated in this organization-only reversal. Existing
account/journey limitations documented before this task remain outside its scope.
Native Editor runs were hidden batch sessions and are closed, with no pending gate.