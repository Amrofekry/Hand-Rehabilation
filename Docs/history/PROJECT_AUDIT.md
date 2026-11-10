# UI redesign baseline — 2026-09-29

The working project is `E:\GP\codex\GripRehab-Unity`. The Git tree already contained extensive uncommitted feature work before this UI-only pass. Its tested software baseline is documented in `QA.md`; the previous 3 Play/Stop cycles and Windows player checks passed. No physical sensor or supervised live hand usability result is implied.

| Item | Observed state |
| --- | --- |
| Editor | Unity 6000.4.4f1 |
| Render | URP 17.0.1 |
| Target | Windows desktop; 1280×720 default, 1600×900 Canvas reference |
| UI | uGUI Canvas, `Text`, `Dropdown`, `InputField`; editable saved scene |
| Input | Legacy Input Manager, mouse/keyboard, custom MediaPipe dwell cursor |
| Packages | Unity uGUI 2.0.0 (includes TMP runtime), Test Framework 1.4.2, URP 17.0.1; no separate DOTween or Unity Localization package. TMP essentials imported 2026-09-30 for new labels |
| App scene | `Assets/Scenes/GripRehab.unity` |
| Other scene | `Assets/_Recovery/0.unity` is a recovery artifact, not an app route |
| Vendor scenes | VirtualGrasp sample/tutorial scenes are third-party demonstrations, not app routes |

## Existing screens and bindings

| Screen | Reads | Raises |
| --- | --- | --- |
| Home | Active profile, saved-session flag, local store availability | Start guest/profile, Continue, New Profile, Choose Profile, Patient, Doctor, Quit |
| Profile | Current form values and validation error | Save or cancel; local profile store |
| Modules | Active profile or guest state | Start Module 01, 02, 03; return Home |
| Patient | Active profile, goal, hand, saved result count | Return Home |
| Doctor research preview | Active profile, game scores and last activity | Return Home |
| Module 01 HUD | Room, hand, input source, force/closure, held object, progress | Home, room/object/mode controls through existing game UI |
| Module 02/03 Activity | Local task state, timer and game score | Task inputs, next/retry/back through existing exercise suite |
| Session options | Existing station, object, mode and resume state | Existing `ApartmentChoices` controls |

The current app does not contain independent Splash, Login, Role Select, Settings, Baseline, Fatigue, Force-Quit, Session History or Chart routes. Designing functional versions would require behavior/data changes outside this visual-only request. They can be represented as clearly labeled concept/gallery views, but cannot be advertised as implemented therapy flows. `Docs/DECISIONS.md` records this boundary.

An Appearance modal now supplies display/accessibility settings within Home: themes, fitted text size, reduced motion, solid panels, dwell time and UI volume/mute. It does not add account/clinical routes. Native uGUI references and current profile bindings remain intact; new labels use TMP. The live Console observed on 2026-09-30 showed zero errors and one existing Input Manager deprecation warning before the additional UI tests. A complete before/after performance baseline was not captured.

## Baseline evidence and limits

- Visual snapshots available before this pass: `Docs/images/ui-main-menu.png`, `ui-new-profile.png`, `ui-module-selection.png`, `apartment-overview.png`, `apartment-options.png`. Copies in `Docs/screenshots/before/` preserve these as historical visual references, not a fresh capture of every screen.
- Existing `Logs/editor-qa.txt`, `Logs/editor-gate-result.json`, and `Docs/player-checks.json` show previous successful software checks. Phase-0 live Editor Console error/warning count and menu/HUD frame times have **not yet been measured**; these are explicit gaps rather than fabricated zeroes.
- Protected paths are in `PROTECTED_PATHS.txt`; `Docs/evidence/ui-protected-baseline.json` records hashes after the preceding feature implementation. Later UI-only work is checked against those hashes.
- No `Docs/ui_references/` directory was present. The prior Figma board and saved screenshots are the local reference.
