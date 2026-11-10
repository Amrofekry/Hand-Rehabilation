# Item 4 — grasp and release — 1 October 2026

WBS 3.4.1. Software implementation and files-only checks complete in the existing project. Native PhysX, scene clearance/contact behavior, live camera and a new Windows build remain pending. No Unity, Hub, player, camera or Computer Use was launched. Earlier native results do not verify this revision; the previous EXE remains unchanged.

## Repairs

- Player collision exclusion is owned by each object, rather than the current Held reference. Release makes the object dynamic with gravity but retains exclusion while its bounds overlap the player's bounds plus 15 mm. Separation restores each pair's original IgnoreCollision state. A timeout cannot restore an overlapping pair. Disabled/re-enabled controllers retain the bookkeeping; object disable/destruction restores its pairs.
- Root and child colliders attached to the object's Rigidbody are used for nearest-surface distance. Finger triggers remain untouched; player exclusion covers all cached solid player colliders. Existing fragment colliders get the same exclusion when spawned; fracture thresholds/effects are unchanged.
- E keeps exact-ray object/door priority, then offers a small bounded aim cone with range and visibility checks. The HUD and E use the same target selection. Camera near-grab uses actual collider surfaces. G remains an explicit tracked-hand summon with its existing 1.25-second grace and without changing Camera mode.
- Ordinary pickup retains object orientation and follows subsequent selected-palm rotation via a relative quaternion. G initially uses the existing anchor orientation, then follows the palm. Existing finger-pad positioning is retained.
- Collider bounds determine a conservative clearance sphere. Reusable nonalloc sweep/overlap queries check furniture/walls, including endpoint overlap, and ignore grippable child colliders regardless of their layer. Saturated query buffers reject placement. A blocked target is moved toward a clear camera-side position; completely blocked pickup is rejected.
- Pickup relocation never contributes throw momentum. Release velocity is damped, finite-checked, capped at 1.2 m/s by default, and zeroed on pose jumps over 0.25 m per physics step or a stale/paused motion sample. Rigidbody interpolation/collision settings are restored after release/reset.
- Explicit camera Q/E release cannot immediately re-grab while the hand remains closed. A fully opened valid gesture re-arms acquisition; G remains an explicit override.

## Configuration and controls

ApartmentSettings.asset preserves 1.6 m reach and existing camera/finger settings. Added values: 9-degree pickup assistance, 0.15 release-velocity gain, 1.2 m/s maximum release speed, 0.25 m maximum release step. These are usability/simulation settings, not physical force or throw measurements.

E picks up/releases (Camera pickup explicitly selects Demo); G summons the selected object only with valid Camera tracking; Q releases; opening the tracked hand releases; R restores station objects. Space/mouse behavior remains from item 3. Re-grip after camera Q/E release by opening then closing the hand, or use G.

## Files-only evidence

- Cached real-Unity-reference compilation passed five Editor and four player assemblies; this is not a native Editor import or Windows build.
- 67 assertions execute the production motion policy/collision guard with managed math, bounds and pair-state substitutes. They check both rotation signs, aim bounds, invalid velocity/timing, teleport rejection, clearance margins, original pair preservation, overlapping release, re-grab, disabled controllers, child colliders, trigger/floor exclusion and cleanup. No PhysX is simulated.
- Eight static grasp integration tests passed.
- Regression checks passed: 72 keyboard-policy assertions, nine static keyboard bindings, 130 managed pose assertions, 37 product-domain assertions and eight Python vision/Phase 2 contracts. Total positive checks: 331 across different evidence scopes.
- Two isolated negative mutations were rejected: immediate collision restoration on overlap, and removing teleport rejection. No mutation replaced a production source.
- Metadata (791 GUIDs), five assembly definitions and whitespace checks passed. Item 1 hand mapping and item 3 player/input/menu sources were hash-checked as unchanged. The deformation/damage algorithm block was compared to the pre-edit backup and is unchanged.

Evidence: evidence/item4-grasp-checks.json, evidence/item4-offline-checks.json, evidence/item4-negative-checks.json; logs in Logs/offline and Logs/item4-negative. Pre-edit files are preserved under Logs/item4-before-20261001. The existing dirty Git tree was preserved; no new commit is claimed.

Reproduce without opening Unity:

    .\.venv\Scripts\python.exe Tools\offline_verify.py --compile --tests
    .\.venv\Scripts\python.exe -m unittest Tools.test_grasp_bindings Tools.test_keyboard_bindings Tools.Vision.test_vision_bridge Tools.Phase2Bridge.test_unity_bridge
    .\.venv\Scripts\python.exe Tools\Offline\VerifyGraspSensitivity.py

## Native acceptance still required

The updated ApartmentAcceptance.cs includes dynamic/gravity release, overlapping-drop recoil and separation restoration assertions. They compile but were not executed this turn.

When native testing is authorized, test both hands standing/seated in both stations, all object sizes and E's exact/assisted edge aim. Check occluded/out-of-range rejection, doors, G with/without tracking and wall clearance, palm turning while held, floor/table drops, repeated release/re-grab/reset, Q/E while a camera fist remains closed, tracking loss, camera jumps, controller disable/re-enable, fragment overlap and repeated Play/Stop. Verify finger contacts remain local after rotation and conservative sphere clearance is not too restrictive for mugs/elongated objects. AABB separation can delay restoring player contact beyond actual geometric separation; gravity and furniture collision remain active during that interval.

Native friction, settling, tunnelling, visible hand-object alignment, recoil and live-hand usability cannot be established by the managed recorder. Deformation/fracture quality remains item 5; VirtualGrasp grasp-pose refinement remains item 6.
