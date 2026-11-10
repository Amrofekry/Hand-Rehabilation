# Item 3 — keyboard and mouse repair — 1 October 2026

WBS 3.4 / 3.4.1. Software implementation and files-only verification complete. Native keyboard/mouse input, Unity EventSystem behavior, Play/Stop and a fresh Windows build remain unverified for this revision. No Editor, Hub, player, camera or Computer Use was launched. Existing scene, imported VirtualGrasp, tracked-hand geometry, profiles and BLE protocol were preserved.

## Defects addressed

- Escape and Tab had separate owners with no frame boundary shared by movement and grip. ApartmentPlayer now owns both keys at execution order -80, before normal gameplay consumers. A pause/resume transition blocks input for that frame.
- A held Space or mouse button could carry across Resume, focus loss or an explicit input-mode change. The shared gate requires each held input to be released before it can squeeze again. Other movement/actions become available on the next eligible frame; they do not wait for Space release.
- Gameplay consumers previously checked only player pause state. The gate also rejects shell screens, focused text fields, unfocused windows and disabled players. Opt-in acceptance fixtures alone may bypass real window focus/component enable checks.
- Keyboard Demo still consumed the last valid camera pose for up to the tracking expiry interval, hiding keyboard curls after E selected Demo. Demo now exclusively drives the demo pose; Camera and Phase 2 keep their existing tracking paths.
- TryKeyboardAction released a held object before checking pause. Its pause guard now precedes pickup/release.
- G now rejects a collapsed palm geometry before bringing the selected object to the camera hand, while retaining Camera mode.
- Both Shift keys and numeric keypad +/- are supported. Simultaneous +/- cancels rather than favoring an increase.
- Existing Enter/Return-only menu Submit and clearing of selected uGUI controls during gameplay are retained and covered by static contracts. No Space-to-Quit binding was added.

## Controls retained

| Control | Context / action |
| --- | --- |
| WASD + mouse | Move/look in active apartment gameplay |
| Escape or Tab | Toggle session options; no apartment toggle on Home/profile/activity or during text entry |
| B | Camera → Keyboard Demo → Phase 2 → Camera, during gameplay only |
| E | Pick up / release, or toggle an aimed door; camera object pickup explicitly selects Demo; Phase 2 retains its source |
| G | Bring selected object to a valid camera hand; does not select Demo |
| Space or left mouse | Hold to squeeze in Keyboard Demo; never submit a menu action |
| Left or right Shift while squeezing | Maximum 80 N simulated grip |
| Wheel / +/- / keypad +/- | Adjust demo setpoint between 1 and 80 N |
| Q / R / F | Release / reset objects / sit or stand |
| 1–5 / J / X / Z / C / 0 | Demo finger / joint / bend / open / reset fingers / reset hand offset |
| Arrows / PageUp / PageDown | Demo hand translation |
| C in Camera | Release and recalibrate tracked palm |
| Enter/Return in menus | Confirm the selected menu control |

After resuming, regaining focus or changing input mode, release any held Space/mouse button and press again to squeeze. Grip in Camera/Phase 2 still uses its corresponding source; keyboard squeeze is explicitly simulated.

## Verification

- Cached Unity-reference compiler: five Editor assemblies and four player assemblies passed. This is not a native Unity import/build.
- 72 production keyboard-policy assertions passed, using fabricated frame/key states with no Unity substitute required for the policy itself.
- Nine static integration checks passed: Submit mapping, single pause owner, shortcut guards, focus/text gates, both Shift/numpad keys, Demo pose isolation, E pause ordering, G mode preservation/geometry and menu navigation.
- Existing regression checks: 130 hand-pose assertions with managed math, 37 product tests and eight Python vision/force contracts passed. Total positive checks: 256; they have different evidence scopes.
- Negative sensitivity checks rejected a gate with held-input rearming removed and an in-memory InputManager with Space added to Submit. Production files were never replaced by either mutation.
- Metadata/assembly inspection passed 789 GUIDs and five asmdefs.

The first static binding check incorrectly treated newlines as whitespace after empty fields and missed the following Enter value. The test parser was corrected to use horizontal whitespace; final integrated and separate bridge runs passed. No test failure is hidden by reusing an older native result.

Reproduce from the project root without opening Unity:

```powershell
.\.venv\Scripts\python.exe Tools\offline_verify.py --compile --tests
.\.venv\Scripts\python.exe -m unittest Tools.test_keyboard_bindings Tools.Vision.test_vision_bridge Tools.Phase2Bridge.test_unity_bridge
```

Evidence: `Docs/evidence/item3-keyboard-checks.json`, `Docs/evidence/files-only-checks.json`, `Docs/evidence/item3-negative-checks.json`, and `Logs/offline/keyboard-tests.log`. The input gate is a project-owned non-component class with its .meta; the imported SDK is untouched.

## Pending native acceptance

When native testing is authorized, import the sources and test: Home/profile typing → Module 01 → E pickup → repeated Space/mouse press and release → Q/E release → G in Camera with/without valid tracking → Escape/Tab and Resume while keys are held → B through all modes → focus loss/return → fingers/joints/hand movement → F/R. Check Enter in menus, Space cannot activate Main Menu/Quit, camera frames cannot override Demo, and activity number/arrow keys never move the apartment player. Repeat Play/Stop and build/run Windows before making a runtime completion claim.

The previous EXE does not contain this repair. Backups are in `Logs/item3-before-20261001`; the dirty working tree was preserved. Checklist numbering stays in the chat, not a new in-game roadmap.
