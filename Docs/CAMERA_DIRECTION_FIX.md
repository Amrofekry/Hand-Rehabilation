# Camera perspective direction repair — 1 October 2026

WBS 3.2 / 3.4.1. Scope: correct the existing camera-driven hand on both sides using files only. No Unity Editor, Hub, player or Computer Use was launched for this revision. Imported VirtualGrasp, the scene, handedness calibration, keyboard mode and Phase 2 contracts were preserved.

## Changes

`FingerPoseDriver.cs` now uses the worker's received image X without a second negation. Palm geometry uses the same convention, removing the extra X reflection that inverted signed Y/Z turns. Vertical motion and the existing palm-width depth mapping retain their signs, scales and limits.

The camera rest pose keeps the upright correction and adds a 180-degree turn around upright Y to exchange palm/back facing. This is a neutral orientation correction; subsequent measured rotations retain their signed direction. It applies to both selected hands. Finger bend calculations, per-finger keyboard controls and the authored demo rest remain unchanged. C still recalibrates camera pose.

World landmarks remain preferred for local hand geometry; dimension-aware image-only packets remain compatible. Hand-centred world landmarks do not provide the global position of the wrist. The existing single-camera depth estimate and 160-degree palm-turn limit remain.

## Executed checks

| Check | Result | Evidence scope |
| --- | --- | --- |
| Cached Unity-reference C# compilation | Five Editor and four player assemblies passed | Compiler only; no native import/build |
| Production pose-source tests | 130 passed | Actual HandTracking/FingerPoseDriver sources with test-only managed math/Transform substitutes |
| Existing product domain tests | 37 passed | Fabricated software fixtures |
| Python vision / Phase 2 bridge tests | 8 passed | Packet contracts; no physical device |
| Regression sensitivity | Previous driver rejected by 48 direction/facing assertions | Backup source compiled separately; production files never swapped |
| Metadata / assembly graph | 788 GUIDs and 5 asmdefs passed | Static inspection |

Tracking assertions cover both handedness labels and two authored rest orientations: horizontal/vertical direction, unchanged near/far depth, positive/negative turns on all three axes, opposite neutral facing, each independent finger, recalibration, invalid palm rejection, tracking loss/recovery, world/image-only compatibility and keyboard rest. The managed math substitute uses System.Numerics, has independent known-axis checks, lives outside Assets and is never part of the game.

`ApartmentAcceptance.cs` was updated to reject the former reflected directions on future native runs. That suite was compiled but not executed for this revision. The earlier 163/164/163 fixture runs precede this direction repair and are not acceptance evidence for it.

## Reproduce without opening Unity

Run from the project root:

```powershell
.\.venv\Scripts\python.exe Tools\offline_verify.py --compile --tests
.\.venv\Scripts\python.exe -m unittest Tools.Vision.test_vision_bridge Tools.Phase2Bridge.test_unity_bridge
```

The first command needs existing cached compiler references and Unity's bundled .NET/Mono tools; it never launches the Editor. Detailed results: `Docs/evidence/camera-direction-fix.json`, `Docs/evidence/files-only-checks.json`, `Docs/evidence/camera-direction-old-code-rejection.json`, and `Logs/offline/tracking-tests.log`.

## Remaining observation

No physical-camera movement, rendered mesh-facing check, native Editor Play test or new Windows build was performed for this revision, as requested. The files are ready for the next import; the previously built EXE is unchanged and does not contain this fix.

When a live trial is available, check right then left: palm toward the webcam should present the expected back of the hand to the player; move right/left and up/down; rotate clockwise/counterclockwise and turn the palm sideways; approach/retreat; bend each finger; use C; then test grabbing/releasing and tracking loss. Record any opposite axis and the selected hand. These observations remain pending rather than being inferred from synthetic checks.

Before-edit source copies are in `Logs/camera-perspective-before-20261001`. Existing work was preserved without reset or checkout.
