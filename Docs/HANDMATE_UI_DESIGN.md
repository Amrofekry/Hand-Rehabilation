## Native acceptance — 6 October 2026

[Current closeout](CLOSEOUT_2026_10_06.md) supersedes the pending native/build statements in the historical entries below. The repaired Windows build passes 168/169/168 gameplay checks, 60 UI checks at each of two resolutions and 87 gallery checks at each of three resolutions. Three Play/Stop cycles pass after the TMP font fix. Live human-camera and physical hardware trials remain unverified. Voice remains future task 7; UI remains English.

# HandMate presentation adaptation — 2026-10-03

WBS: 3.4 (game presentation), including presentation of the existing 3.4.1–3.4.3 modules. The original WBS, timelines and gameplay scope are unchanged.

## Source and scope

Source: `E:\Downloads\HandMate-Rehab-System-main.zip`. Its `ModuleSelectionUI` and `EnvironmentSelectionScene` use navy `#1A2230`, cyan `#00F0FF`, white labels, Orbitron headings, Montserrat supporting text, a rehabilitation-room background and illustrated module cards.

Only nine images and three font files were copied into `Assets/UI/HandMate`. No source scene, script, environment, package manifest, project setting, hardware implementation or account system was imported. Source artwork hashes and archive provenance are in `evidence/handmate-design-import.json`.

The supplied Montserrat variable font defaults to weight 100. Body text uses a static weight-400 Regular instance made from that same font, with its name table updated to Regular; the original font remains available. Orbitron is unchanged. Embedded notices and the full SIL OFL 1.1 are included in `Assets/UI/HandMate/Fonts/OFL.txt`. Legacy text uses these embedded TTFs; TMP labels use cached dynamic atlases created once per font, with Montserrat as a fallback for heading symbols.

## Screen coverage

| Surface | Implementation |
| --- | --- |
| Main menu, profile form, saved-profile picker | Saved `Assets/Scenes/GripRehab.unity`; Montserrat body, Orbitron headings, photo backdrop, distinct navy fields and secondary buttons |
| Module selection | Same saved scene; three original source illustrations above existing titles and actions; existing playable modules remain enabled |
| Patient/doctor screens and research workspace | Saved screen styling plus `Assets/UI/ResearchPortalView.cs`; no access, account or storage logic changed |
| In-game HUD, force values, progress, shortcuts and camera panel | Existing saved Graphics restyled; values, units and data sources unchanged; raw camera image is excluded from recoloring |
| Room/object/session options | `ApartmentChoices.cs` resolves selected/unselected fills and labels from the current theme, including after theme transitions |
| Modules 2/3 exercise pages and tracing markers | Existing runtime page explicitly registers with the presentation controller; marker backgrounds and numbers use matching theme foregrounds |
| Hand pointer | Existing overlay registers its Graphics once; the bright centre and cyan dwell ring retain their existing movement and activation logic |
| Dropdown lists, sliders, toggles, inputs and reusable components | Existing templates styled; source arrow icon used for dropdown chevrons; slider handles and focus accents remain visible; eight UI component prefabs and the gallery updated |

The source module images are illustrations. Their baked pictures/numbers are not live force readings, measured rehabilitation outcomes or newly implemented gameplay.

## Tokens and editing

`Assets/UI/Themes/Dark.asset` is the default navy/cyan design. Light and HighContrast retain the existing appearance alternatives using compatible foreground/background pairs. Photo tint `#586A80` keeps white headings readable even over the brightest source photo pixels. Foregrounds and control backgrounds have tested contrast of at least 4.5:1. More opaque introductory cards keep body text separate from the photo.

Edit shared colors and fonts in the theme assets. Edit positions and labels in the saved main scene. Build and Verify do not regenerate the scene. Original navigation listeners, input shortcuts, hand tracking, grip/physics logic, profile storage and Phase 2 contracts remain intact. No voice command implementation has been reintroduced.

`GripUiThemeController` caches semantic bindings, includes inactive authored screens, accepts explicitly registered runtime pages, prunes destroyed page bindings and retains artwork separately for each backdrop. It never scans the hierarchy in Update or subscribes to gameplay input. Runtime font atlases are released with the controller. Theme authoring and accessibility tools use the new palette.

## Verification and remaining work

- Five Editor and four player assemblies compiled successfully against the installed Unity cached references. This is file compilation, not a fresh native Unity import.
- The existing managed domain/hand/pointer/grip/deformation tests passed.
- 61 root Python tests and four vision bridge tests passed, including six new saved-design tests covering source hashes, foreground contrast, UI GUIDs/IDs, decorative raycast exclusion, illustration containment, visible arrows/handles and protected-file/scene-logic preservation.
- 298 protected pre-existing source/assets and 91 serialized scene logic components match their before-change hashes. Only presentation scripts/fields and the specified UI documents are permitted differences.
- Static review images for Home/Profile/Modules at 1600×900 and 1280×720 are in `screenshots/handmate-static`. They are explicitly labelled **not Unity runtime screenshots**; they approximate saved layout and typography and do not establish native font/rendering or interaction behavior.
- Native import/rendering, Play/Stop, runtime TMP atlas behavior, popup interaction and a fresh Windows player remain unverified for this revision. The headless Editor preflight exited 198 before importing, reporting unavailable licensing in the automated session. This does not prove that an interactive user session has no license. No settings/authentication were changed.
- Existing Windows builds predate this source revision. They must not be used to judge the new design or as evidence that retired voice code is absent.

Results and current file hashes are in `evidence/handmate-design-verification.json`. Existing unrelated Git changes were preserved; no mixed-work commit was created.

For a fresh native acceptance run when the Editor is available: import this project, inspect Home/Profile/Modules/HUD/Options/exercises and both palettes at 1280×720 and 1600×900; open each dropdown, move sliders, navigate by mouse/keyboard/hand dwell; verify populated results and actual input-state labels; repeat Play/Stop; build Windows and inspect the same player screens. These steps remain pending rather than being reported as passed.
