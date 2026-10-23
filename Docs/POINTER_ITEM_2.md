# Item 2 — index pointer and dwell selection — 1 October 2026

**Software implementation complete; native uGUI and live-camera verification pending.**

Canonical project: `E:\GP\codex\GripRehab-Unity`. WBS **3.2 / 3.4**. No Unity, Hub, player, webcam or Computer Use was opened for this revision. Existing runtime reports remain historical.

## Behavior

In menus and paused in-game options, show either hand to the existing MediaPipe camera. Extend the index finger and curl at least two of the middle/ring/little fingers. The thumb is unrestricted. Move the index tip over a control and hold the gesture for **five qualified seconds** by default. The existing progress ring shows the countdown. The accessibility setting retains its two-to-eight-second range.

A short gesture flicker pauses progress and cursor position; prolonged loss clears progress. Missing/stale tracking, losing application focus, changing hands, replacing the EventSystem or changing screen dimensions clears the current dwell. After selection, move the pointer deliberately (32 screen pixels) or relax the gesture for more than 0.2 seconds before selecting again. This prevents a newly opened control from firing under a stationary hand. The next control still requires a full dwell.

Buttons, toggles, dropdowns and their option toggles use the same left-pointer click handler as mouse activation. Legacy and TMP dropdowns/input fields are recognized. Input fields receive focus; text entry and slider dragging still need keyboard/mouse or caregiver help. Existing shorter/longer dwell buttons remain available. No virtual keyboard or new menu system was added.

## Defects repaired

- Gesture detection used raw normalized image geometry even when MediaPipe supplied better estimated world geometry. It now uses world landmarks when available and aspect-corrected image geometry otherwise. Both hands share the same tested policy.
- A visible hand could carry old cursor smoothing into the opposite hand. Switching hands now clears position history and dwell.
- Dwell continued while the application was unfocused. Focus loss now hides/resets the pointer before any camera-start or dwell logic.
- A prior target could be retained when the raycast returned no hits. An empty hit list now cannot activate the old button. Frontmost graphics and disabled controls continue to block controls below them.
- Activation bypassed native pointer-click handlers by invoking callbacks/toggling values directly. It now dispatches one left-pointer click, after recording the rearm latch, so navigation callbacks cannot bypass it.
- A changed UI page could start another dwell underneath a stationary pointer. The explicit rearm latch prevents this.
- EventSystem replacement reused event data from the old system. It now resets and recreates the data. Singleton enforcement avoids duplicate cursor components; the shell reuses an existing authored instance.
- Invalid smoothing/duration/grace values could poison cursor state. Effective values are finite and bounded. The existing gate itself remains unchanged.
- The settings adapter could report the default rather than the live pointer setting. It now resolves the pointer on reads as well as writes.
- A disabled shell cannot drive the pointer; relaxed gestures cannot submit trace-pad samples. Disabling the pointer releases only a menu-owned camera worker, leaving camera gameplay ownership intact.

The saved scene, imported VirtualGrasp, hand pose mapping, keyboard/mouse behavior, force protocols, user profiles, grasp/deformation implementation and material profiles were not changed. Tests verify preservation hashes for item 5's owned runtime and test sources, except the verifier whose test list was extended.

## Evidence and limits

| Check | Result |
| --- | --- |
| New production pointer gesture/coordinate/rearm/dwell assertions with managed math | 106 passed |
| New static runtime binding contracts | 17 passed |
| Earlier tracking/input/grasp/deformation/domain/bridge regressions | 401 passed |
| Total unique positive checks across mixed software scopes | **524 passed** |
| Combined Python suites from project root | 51 passed, included in the total |
| Real cached Unity 6000.4.4f1 references | Five Editor / four player assemblies compiled |
| Metadata / project assembly graph | 795 GUIDs / five project assemblies passed |
| Same gesture/dwell subset against exact old production static methods | 57 passed / seven failed |
| Isolated mutations: ignore world geometry, bypass navigation rearm, bypass held-target latch | All three compiled and were rejected |

The baseline failures include six disagreements between image projection and estimated world finger shape, across both hands, and one malformed-frame selection. The old policy was copied verbatim into an isolated test wrapper; this is not a test of the old native UI.

Tests execute production pure-source decisions with managed math and fabricated frames. Static contracts check wiring but do not execute uGUI raycasts, native click callbacks, rendering, actual focus events or webcam operation. **No live usability, FPS/latency target, native Play/Stop or fresh Windows build is claimed.**

Evidence: [item2-pointer-checks.json](evidence/item2-pointer-checks.json), [item2-offline-checks.json](evidence/item2-offline-checks.json), [item2-negative-checks.json](evidence/item2-negative-checks.json). Logs: `Logs/offline/item2-pointer`, `Logs/offline/pointer-negative`, and standard `Logs/offline` test logs. Source backup: `Logs/item2-before-20261001-151614`.

Run from the project root:

```powershell
.\.venv\Scripts\python.exe Tools\offline_verify.py --compile --tests
.\.venv\Scripts\python.exe -m unittest Tools.test_pointer_bindings Tools.test_deformation_bindings Tools.test_grasp_bindings Tools.test_keyboard_bindings Tools.Vision.test_vision_bridge Tools.Phase2Bridge.test_unity_bridge
.\.venv\Scripts\python.exe Tools\Offline\verify_pointer_sensitivity.py
```

Negative tests change isolated generated copies under Logs only. No production source is replaced by a mutant. Existing unrelated Git changes are preserved; no commit is claimed.

## Native acceptance still required

Use the existing opt-in apartment acceptance runner and gesture verification command when Editor testing is authorized. Then test with a real right and left hand: Start, Continue, dropdown opening/option selection, settings, in-game pause, disabled actions and modal blocking. Verify the ring reaches the configured duration, brief gesture flicker pauses, tracking/focus loss resets, hand switching snaps correctly and stationary post-navigation pointing cannot auto-select. Check the same controls with mouse/keyboard, confirm Space remains grip, repeat Play/Stop and test a fresh Windows build at 1280×720 and higher resolutions.

Real camera stability, world-landmark quality, target accessibility and the 32-pixel rearm threshold require live usability measurement. Registration remains assisted for typed fields. Webcam/device/clinical verification is separate from the source checks.

## Checklist

Items **1–5** are software-complete, with applicable native/live verification pending. Items **6–15** remain pending review/completion. This checkpoint implements item 2 only.
