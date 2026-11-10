> 12 October 2026: restored the earlier VirtualGrasp prototype for Unity 2022.3.0f1 at the user's request. Later hand/contact/soft-body evidence below is historical. See [restoration report](evidence/rollback-2026-10-12/) and [current 23 tasks](history/SOFTWARE_TASKS_23.md).

# GripRehab current software delivery


Project: `E:\GP\codex\GripRehab-Unity`. Editor: **6000.4.4f1**. Current Windows build: `Builds/Windows/GripRehab.exe`, rebuilt at **17:17:57 UTC** through the persistence gate. This closes actionable repairs and verification of the existing English game. Voice remains future task 7; no CAD or new research-product expansion was performed.

## Changes and findings

- Fixed a reproduced `NullReferenceException` in `GripUiThemeController.RuntimeFont` after loading the UI Gallery. TMP runtime-created fonts have a null fallback list; the controller now initializes that list before attaching the body font. The imported VirtualGrasp package was not edited.
- Extended native gallery acceptance to check actual heading/body atlas creation, cache reuse, one body fallback and a second scene load after cleanup. Runtime errors now fail the gallery's own result, rather than allowing a misleading PASS alongside a logged exception. The original failure log/result remain in `evidence/closeout-2026-10-06/gallery-first-*`.
- Current main menu/profile/modules/HUD use the supplied HandMate presentation. Fresh native views show the top-bar text inside its panel and readable controls in Dark, Light and High Contrast. Keyboard, mouse, five-second whole-hand dwell, G summon and Space input regressions passed.
- Preserved existing profiles, unrelated working-tree edits and vendor source. Tests used isolated profiles/preferences and synthetic inputs. The VirtualGrasp tutorial popup was closed and its startup display disabled using the Editor UI.
- Unity save normalized scene ordering, whitespace and numeric formatting. All 1,513 serialized object IDs and field structures remain, with changed numbers equivalent as float32; the protected 91 scene-logic components still match. The test edit was restored. Build then preserved the saved scene bytes. See `scene-preservation.json` and `scene-save-delta.patch` in the current evidence folder.

## Current verification

| Check | Actual result |
| --- | --- |
| Cached-reference compilation / metadata | Five Editor and four player assemblies compile; dependency and metadata checks pass. This alone is not native verification |
| Python | **69 PASS**: 61 root Tools, four Vision, four Phase2Bridge tests |
| Native Editor scene, contrast and gallery references | PASS in the graphical user session |
| Editor lifecycle after font fix | Three Play/Stop cycles, **168 checks each**, UDP 27817/27818/27819 released; completed **2026-10-06T17:26:25.1858868Z** |
| Windows ordinary / Phase 2 replay / rendered | **168 / 169 / 168 PASS** after the repaired build; `player-checks.json` |
| Main menu / Appearance | **60 PASS** at 1280x720 and **60 PASS** at 1600x900; `ui-player-checks.json` |
| UI Gallery | **87 PASS** at 1280x720, 1280x800 and 1024x768; no native runtime errors; `gallery-checks.json` |
| Edit / save / reopen / build | PASS at 17:17:57 UTC; `evidence/closeout-2026-10-06/persistence.json` |
| HandMate design preservation | Six saved-design tests pass, including 298 protected files and 91 serialized scene-logic components |

Native gameplay checks cover both imported SDK hands, finger independence and wrist direction using fabricated 21-point frames, whole-hand dwell activation, menu/profile persistence and recovery, keyboard Submit isolation, object deformation/breakage/drop, no player recoil, invalid/duplicate/stale sensor messages, calibration changes and explicit synthetic provenance. They also exercise the existing playable Modules 2 and 3. They do not reproduce a human hand through a physical camera.

An additional ordinary Editor menu review showed zero Console errors: the actual New Profile button opened the form, popup options remained visible, and Male/Left selections updated their captions. No profile was saved. Evidence: `evidence/closeout-2026-10-06/manual-ui-review.json`.

Fresh gameplay, UI and gallery logs contain no exception/error markers rejected by their runners. The known vendor warning about an absent baked Grasp DB remains: the project uses its visible rig and driven joints, without an object-specific baked grasp database. This report does not claim zero warnings or clinical correctness.

## Evidence and measurement limits

Current screenshots: `screenshots/player-1280x720`, plus `screenshots/{dark,light,contrast}/en/{resolution}`. The screenshots are native player captures with preview/synthetic data, not real patient records. [PERFORMANCE.md](PERFORMANCE.md) records current short menu samples and explicitly unavailable GC/batch counters; no 0 B GC or live-camera 60 FPS claim is made.

An isolated headless Editor preflight exited 198 before import because that session could not resolve licensing. The graphical Editor successfully compiled, played and built. No login/security configuration was changed. One initial lifecycle attempt was interrupted and was not counted as passed; subsequent fresh cycles succeeded.

The historical September 29 boundary checker still reports 16 later authorized October repair differences outside its old five-file exception list. Its failure remains visible, not silently whitelisted/rebased. The October HandMate preservation tests pass independently. Historical reports and screenshots are not substituted for current results.

## Remaining checklist

- **9:** supervised live-camera right/left hand, pointer dwell, orientation, recentering, grasp/release and latency trial when the user is available.
- **10:** real Nano 33 IoT/HX711 calibration/BLE/force testing when equipment exists.
- **7:** voice commands are future work by explicit request; there is no active voice implementation.

The game is a research prototype. Camera closure is an estimate, not measured force; physical material fidelity and clinical efficacy are unverified. The doctor screen remains a local preview and adaptive AI/product-portal ambitions remain separate future scope. Original Gantt/WBS dates were not changed.

Default sandbox Git writes still failed; the reviewed elevated Git command successfully staged a narrow checkpoint. Only the owned UI font fix, its gallery regression and the current task/report documents are included; unrelated existing changes remain in the working tree. The verified commit identity is recorded separately after committing. [TASKS.md](history/TASKS.md) is the current numbered list; older progress entries remain historical.
