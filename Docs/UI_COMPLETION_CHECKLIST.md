# Current English UI completion checklist

Updated 2026-10-06. This is the current repair/design scope, superseding the broader historical redesign request. The user removed Arabic UI and voice and stopped new feature expansion.

| Item | State / evidence |
| --- | --- |
| HandMate navy/cyan colors, original artwork and fonts | Applied across saved menus/HUD and registered runtime exercise/dashboard views; source asset hashes preserved |
| Main menu, profile fields, module selection and session HUD | Native 168-check rendered acceptance and fresh screenshots |
| Top-bar text containment and control contrast | Reviewed in native HUD/menu screenshots; theme foreground contrast checked |
| Dark / Light / High Contrast and display controls | 60 native tests at each of 1280x720 and 1600x900; motion, solid panels, fitted text, dwell, volume/mute and focus/modal behavior |
| TMP initialization and scene cleanup | Reproduced fallback-list fault fixed; gallery reload/atlas reuse checks pass |
| English component gallery | Eight saved templates; 87 native checks at each of three resolutions, including text overflow and raycast reachability |
| Whole-hand pointer / five-second dwell | Native synthetic frame-to-Start activation, left-hand stability and event cache recovery pass; human trial remains task 9 |
| Keyboard / mouse and Space isolation | Native regressions pass; Enter confirms menus, Space squeezes in explicit Keyboard Demo |
| Scene save / reopen / Build preservation | Native persistence gate passes; original scene-logic hashes preserved |
| Performance | Short menu measurements in PERFORMANCE.md; live camera latency and isolated GPU/GC budgets unverified |
| Voice / Arabic interface | Not part of delivery; voice is future task 7, English is the active interface |
| Doctor portal / baseline / fatigue system expansion | Future scope; existing doctor screen is explicitly a local preview |

These checks cover the existing application, not every possible resolution, clinical usability or a complete new component/state library. No new blur/parallax/music/mixer feature was added during the repair pass. See CLOSEOUT_2026_10_06.md and TASKS.md for current evidence and the remaining live/device trials.

Run the Windows suites sequentially from the project root: `python Tools/run_player_checks.py`, `python Tools/run_ui_checks.py`, `python Tools/run_gallery_checks.py`. All use isolated test profiles/preferences. Historical `verify_ui_boundary.py` compares an older scope; its remaining differences are documented, not hidden.
