# Stage 1 audit ledger — 23 October 2026

Audit only. No game code was changed in Stage 1. Evidence: `Docs/evidence/audit-2026-10/`. Severities: **S1** crash/data loss, **S2** wrong behaviour or broken check, **S3** degraded quality/missing scope, **S4** cosmetic. Owning stage refers to the [upgrade plan](superpowers/plans/2026-10-23-ultimate-upgrade-plan.md).

## 1. Automated baseline (Task 1.1)

| Suite | Result | Log |
|---|---|---|
| AI Python (`ai-engine/run.py test`) | **PASS 32/32** | `logs/ai-tests.log` |
| WBS tests (`pytest tests/`) | **PASS 45/45** | `logs/wbs-pytest.log` |
| Tools Python (`unittest discover -s Tools`) | **FAIL**: 66 run, 4 errors (A-003) | `logs/tools-unittest.log` |
| Vision/Phase 2 bridge contracts | **FAIL**: 1 failure, 5 errors (A-004) | `logs/bridges.log` |
| Offline verifier, metadata + assembly graph | PASS (840 metas, 4 assemblies) | `logs/offline-verify.log` |
| Offline verifier `--compile --tests` | **FAIL**: Unity 6000.4.4f1 path missing (A-005) | `logs/offline-verify-full.log` |
| Native Unity: FatigueAiVerification | **PASS 186 checks**, Sentis parity | `logs/unity-FatigueAiVerification.Run.log` |
| Native Unity: SceneOrganization.Verify | **PASS** | `logs/unity-SceneOrganization.Verify.log` |
| Native Unity: RehabDwellVerification | **PASS** | `logs/unity-RehabDwellVerification.Run.log` |
| Unity EditMode/PlayMode test assemblies | **None exist** for project code (A-012) | — |
| Windows player smoke / replay / journey (22 Oct build, 2022.3.20f1) | **PASS** 168 / 169 / 74 game checks, each with 10 model assertions; ONNX diff 7.45e-09 | `logs/player-summary.txt`, `Docs/ai-player-*.json` |
| Windows UI presentation | **PASS** 60 checks at 1280×720 and at 1600×900 | `Docs/ui-player-checks.json` |
| Windows UI gallery | **PASS** 87 checks at each of 1280×720, 1280×800, 1024×768 | `Docs/gallery-checks.json` |

These player suites close the STATUS.md note that UI/gallery results were only from Unity 6: they now pass on the 2022.3 build. **Coverage gap:** the UI/gallery suites test the HandMate components and gallery, not the research-portal pages (main menu, Prepare, Create account, Appearance), where the playthrough found layout and contrast defects P-001, P-004, P-018 and P-019.

## 2. Defects

| ID | Sev | WBS | Finding | Repro / evidence | Stage |
|---|---|---|---|---|---|
| A-001 | S2 | 3.2 | After a camera-worker restart (auto-restarted every 5 s by `RehabDwellPointer.cs:169`), `VisionUdpReceiver.cs:110` rejects every packet as "duplicate sequence" until the new sequence passes the old one. Tracking can be dead for minutes. | Code trace: `EnsureStarted()` without `vision.BeginNewSession()`; the worker's sequence restarts at 1 | 2 |
| A-002 | S1 | 5.x | `RehabProfileStore` constructor: any read error on `profiles.dat` (e.g. an antivirus lock) overwrites the newer primary with the older `.bak`, silently losing the last save. | `RehabProfileStore.cs:57-65`; `Save()` keeps the previous version in `.bak` | 2 |
| A-003 | S2 | 6.3 | **Stage 0 regression (mine):** moving `Assets/_Recovery` broke the protected-asset hash test. | `test_handmate_design` 4 errors; commit `a19d2dd` | 2 (restore files or re-baseline) |
| A-004 | S2 | 3.2 | Vision bridge tests expect `world_hand` / `image_width` payload extensions that the 12 Oct rollback removed from `mediapipe_bridge.py`. | `TypeError: make_payload() got an unexpected keyword argument` | 2 |
| A-005 | S2 | 6.2 | `Tools/offline_verify.py:10` hard-codes the Unity 6000.4.4f1 editor, so the offline C# domain/grasp/deformation/pointer tests can't run on 2022.3.20f1. | `[WinError 2]` | 2 |
| A-006 | S2 | 3.1 | `Packages/manifest.json` uses absolute `file:E:/GP/codex/GripRehab-Unity/...` package paths. The project won't open from another folder or machine. | manifest | 2 |
| A-007 | S3 | 3.2 | `mediapipe_bridge.py:108` busy-spins a CPU core while the camera is stalled (no sleep on failed `read()`). | Code trace | 7 |
| A-008 | S3 | 5.2 | Profile results capped at 100; oldest are silently dropped, losing long-term history for the doctor dashboard. | `RehabProfileStore.cs:260` | 6 |
| A-009 | S3 | 5.x | Autosave failure dialog says "Practice is paused" but the session keeps running and the dialog is rebuilt every 5 s. | `ResearchSessionRuntime.cs:133-138` | 5 |
| A-010 | S3 | 6.1 | Phase 2 bridge relays force only every 200 ms (metadata confirmation), adding up to 200 ms latency in 5 Hz bursts. | `unity_bridge.py:105` | 7/8 |
| A-011 | S4 | 3.2 | Launcher reads `readMs` / `inferenceMs` that the bridge never sends, so they always show 0. | `VisionCameraLauncher.cs:105` | 7 |
| A-012 | S3 | 6.2 | No Unity Test Framework assemblies for project code. Native checks are custom editor/player gates only, with no `-runTests` coverage. | Only VirtualGrasp's sample test asmdef exists | 2/3 |
| A-013 | S3 | 6.3 | `ROLLBACK_2026_10_12.md` is linked from `TASKS.md` and `SOFTWARE_GRADUATION_PLAN.md` but missing. | `Docs/` | 2 |
| A-014 | S3 | 3.4.1 | VirtualGrasp "No baked grasps found" (object Grasp DB not baked). | Known since 30 Sep | 2 (Task 2.2) |
| A-015 | S4 | 3.4.2 | Sequence Memory seed is `DayOfYear + 2048`, so the same sequence repeats all day. | `RehabExerciseSuite.cs:137` | 3 |
| A-016 | S3 | 3.2 | Camera launcher needs project-root `.venv`, `Tools/Vision` and `models` next to the build, so the build is not standalone. | `VisionCameraLauncher.cs:51,139` | 9 |

| A-017 | S4 | 3.4 | `RehabApp.cs` (668 lines) is not in the main scene; only `RehabAction` references it. It's legacy controller code beside `RehabAppShell` / `ApartmentExperience`. | GUID `40d584fa…` absent from `GripRehab.unity` | 2 |
| A-018 | S3 | 3.4.3 | Tracing accuracy counts correct on-path tracing as off-path: off-path = distance to the *next waypoint* > 0.2, but every segment is 0.206–0.25 long, so the first part of each segment is always penalised. | `VisuomotorTasks.cs:25-27`; segment lengths 0.247/0.206/0.23/0.25 | 3 |

| A-019 | S2 | 3.2 | Whenever a menu is open and the game is focused, the hand pointer starts the webcam worker automatically (retried every 5 s), with no opt-in step and no camera indicator beyond a status line. | `RehabDwellPointer.cs:166-170` | 4 (UX) / 5 (consent) |
| A-020 | S3 | 5.x | `WindowsProductStore.Load` uses the same restore-from-`.bak` pattern as A-002. The real data folder had `research-accounts.dat.bak` with no primary file, a state that rollback produces silently. | `WindowsProductStore.cs:16-22`; observed in LocalLow | 2 |

### Playthrough defects (Windows build, 1600×900 windowed, keyboard/mouse, camera deliberately unavailable)

| ID | Sev | Screen | Finding | Evidence |
|---|---|---|---|---|
| P-001 | S3 | Research portal footer | "CAMERA UNAVAILABLE / USE MOUSE OR KEYBOARD" status text overlaps the SCROLL UP button label. | `play/01-main-menu.jpg` |
| P-002 | S3 | Prepare to train | Camera is preselected even when the camera is unavailable. | playthrough |
| P-003 | S4 | Prepare / menus | Several buttons show cyan focus borders at once (selection, keyboard focus and dwell hover look identical). | playthrough |
| P-004 | **S2** | Prepare to train | The primary CHOOSE A MODULE button stays half-hidden behind the footer even at maximum scroll. The mouse wheel doesn't scroll, the SCROLL DOWN step is tiny, and scroll position varies, which made a scripted click toggle SYNTHETIC MODEL DEMO by mistake. | `play/05-prepare-cta-hidden.jpg` |
| P-005 | S3 | All | Two visual languages: flat research-portal pages (menu, prepare, results, account) vs. polished HandMate pages (module select, profile form). | screenshots |
| P-006 | S4 | Module 1 HUD | Force readout uses a slashed-zero display font, so "0.0" reads like "Ø.Ø". It didn't update visibly while squeezing in Keyboard Demo. | `play/02-module1-living-room.jpg` |
| P-007 | **S2** | Module 1 results | "Valid input 92.2 s" counted idle time: Keyboard Demo samples are always "fresh", so the whole session counts as valid input. | results screen |
| P-008 | S3 | Module 1 | The session ends after one 3 s hold with score 100/100 and no rounds, objectives or feedback. | results screen |
| P-009 | S3 | Results → menu | BACK TO MENU returns to the root menu. Trying another module means repeating Guest → Prepare → scroll → Choose a module. | playthrough |
| P-010 | S3 | Module 2 Memory | No "show pattern again" control. | playthrough |
| P-011 | S3 | Startup | A second instance starts silently with all UDP ports unavailable; the user only sees "CAMERA UNAVAILABLE". No single-instance guard or clear message. | Player.log of second instance |
| P-012 | **S1** | Module 2 Sequence Memory | **Pattern never shown.** The screen goes straight to "ENTER PATTERN 0/4" on start and after RESTART (no "MEMORISE …" text, no "Pattern hides shortly"), so the task can't be played; guesses count as retries. Code looks correct (`RehabExerciseSuite.cs:141,242`), so the root cause is runtime. Needs debugging. | `play/03-module2-memory-no-pattern.jpg` |
| P-013 | S4 | Logs | `[VISION_CAMERA] Tools/Vision not found` is logged as an error every 5 s while the camera is unavailable. | Player.log |
| P-014 | S3 | Module 2/3 results | Results show only score and valid seconds, not the PDF's metrics (reaction time, accuracy, trajectory, velocity). | results screen |
| P-015 | **S2** | Module 3 Tracing | Dragging exactly through waypoints 1→5 never registers waypoint 5 (a separate click does), and accuracy falls 54% → 38% while on the path (confirms A-018). No dashed guide line or green/red feedback as the PDF specifies. | `play/04-module3-tracing.jpg`, `play/08-tracing-accuracy-38.jpg` |
| P-016 | S3 | How to play | A wall of text; no images, animation or per-module tutorial. | playthrough |
| P-017 | S3 | Appearance | The active theme isn't indicated (Dark/Light/High contrast buttons look the same). | playthrough |
| P-018 | **S2** | Appearance → Light | In the research portal, Light theme keeps the dark background but turns some buttons white with pale text ("HIGH CONTRAST" nearly unreadable). This is an accessibility contrast failure on the accessibility page itself. | `play/06-light-theme-contrast.jpg` |
| P-019 | **S2** | Create account | Text input fields have no visible border or contrast; empty fields are almost invisible. | `play/07-account-fields-invisible.jpg` |
| P-020 | S4 | Profile form | Asks for the name again right after "Display name". | playthrough |
| P-021 | S3 | Baseline | Thin bar plus one line of text; no coach, countdown, per-stage timer or "paused" message. The game pauses silently when the window loses focus (progress froze while another app was in front). | playthrough |
| P-022 | S4 | Patient home | "Continue practice" is shown disabled with no explanation. | playthrough |
| P-023 | **S2** | Baseline summary | Endurance reads **0.0 s** after holding the grip about 10 s during the endurance stage (peak 18.0 N recorded). | `play/09-baseline-endurance-zero.jpg` |
| P-024 | S4 | Patient home | SIGN OUT is only one level up in the root menu, not on the patient home. | playthrough |
| P-025 | S4 | Session history | Developer codes "D3–D4" shown in patient-facing subtitle. | playthrough |

Not played by hand: doctor journey D1–D5 (covered by the native 74-check journey suite), Phase 2 live input (no hardware), live camera (deliberately disabled to avoid opening your webcam). Test data: one throwaway patient account; the LocalLow data folder was backed up first and restored afterwards (10/10 SHA-256 match). Preferences remained at Dark / 100% / 5 s.

## 3. Requirement map (PDF pipeline + block diagram)

| Requirement | Status | Gap → stage |
|---|---|---|
| 01 Patient: power on, open app, open profile | Implemented (local accounts, guest) | — |
| 02 Prepare and assess: connect grip and camera, starting trial | Implemented (54 s staged baseline); real grip absent | Physical test → 8 |
| 03 Select and start a module | Implemented (3 modules) | — |
| 04 Adaptive guidance: level changes, fatigue warning Resume/Quit, automatic stop | Policy implemented; difficulty mapped to Module 1 only | Adaptation in Modules 2/3 → 5 |
| 05 Review results: patient dashboard (force, endurance, stability) | Partial: score, valid seconds, peak N; no trend charts | 6 |
| 06 Save and switch off | Implemented (protected local store) | A-002 → 2 |
| D1 Doctor sign-in | Implemented (local role) | — |
| D2 Select patient: list, search, sort, paging | Implemented | — |
| D3 Review progress: compare force, endurance, stability, reaction time, accuracy | Partial: session history; no cross-module statistics/charts | 6 |
| D4 Recommendations / notes | Partial (notes) | 6 |
| D5 Save and sign out | Implemented | — |
| Module 1 Force-Object: Sphere/Cylinder/Pencil, Obi deformation, force/endurance/stability | Partial: 6 everyday objects, in-house deformation, no grip-shape variants, no Obi | 3 |
| Module 2: Gesture Sequence Memory (avatar), Spatial Assembly (virtual hand) | Prototype: numeric tokens and fixed order puzzle in a UI card | 3 |
| Module 3: Maze (avatar, collision penalty), Visual Tracing (green/red) | Prototype: text grid with buttons; 5-waypoint trace pad | 3 |
| Vision: MediaPipe hand tracking | Implemented; live human trial pending (B05) | Human trial |
| YOLO gesture recognition | Not implemented (no model) | 3 (document substitute) |
| Facial 7-emotion, rPPG HR/HRV | Implemented (opt-in), accuracy not validated | — |
| LSTM/GRU temporal model → ONNX → Sentis | CNN in Sentis (GRU unsupported); EMG proxy only | 5 |
| Force mapping from load cell over BLE | Software path + synthetic replay; no hardware (B02) | 8 |

## 4. Playthrough rubric (Task 1.3)

Scores 1–5 (readability · feedback clarity · visual polish · responsiveness · bugs, where 5 = no bugs). Subjective, from one session at 1600×900.

| Screen | Read | Feedback | Polish | Resp. | Bugs | Notes |
|---|---|---|---|---|---|---|
| Main menu (research portal) | 4 | 3 | 2 | 4 | 3 | Flat list, footer overlap |
| Prepare to train | 3 | 2 | 2 | 3 | 2 | Hidden CTA, no wheel scroll |
| Module select (HandMate) | 4 | 4 | 4 | 4 | 5 | Best screen |
| Module 1 gameplay | 4 | 2 | 2.5 | 4 | 3 | Blue glove hand, marble table fills view, HUD doesn't react |
| Module 2 Memory | 4 | 1 | 2 | 4 | 1 | Pattern never shown |
| Module 2 Assembly | 4 | 3 | 2 | 4 | 4 | Copy-the-numbers, not spatial |
| Module 3 Tracing | 4 | 2 | 2 | 3 | 2 | Accuracy bug, no guide line |
| Results | 4 | 2 | 1.5 | 4 | 3 | No metrics, always 100 |
| Appearance | 3 | 2 | 2 | 4 | 2 | Light theme contrast failure |
| Create account / profile | 2 / 4 | 3 | 2 / 4 | 4 | 3 | Invisible fields; profile form good |
| Baseline | 3 | 2 | 1.5 | 3 | 2 | Endurance 0.0 s |
| **Overall** | **3.6** | **2.4** | **2.3** | **3.8** | **2.7** | Biggest wins: unify on HandMate design, fix Module 2/3 logic, real game loop for Module 1 |

## 5. Stage 2 results (23 Oct 2026)

Branch `3.4-upgrade-baseline`, commits `d97f0c1`..`ee06fd5`. Evidence: `Docs/evidence/stage2-2026-10-23/`. "Verified" means an automated test and/or a fresh look at the rebuilt Windows player; no physical or human trial is implied.

| ID | Outcome | How it was verified |
|---|---|---|
| A-003 | **Fixed**: recovery scenes restored to `Assets/_Recovery` | Tools 66/66 |
| A-002 / A-020 | **Fixed**: `ProtectedFileRecovery` retries busy reads, never rolls back to `.bak`, copies an unreadable primary aside first | 6 new offline tests; product suite 48/48 |
| P-012 | **Not a game defect; hardening kept.** Instrumented build logs show "MEMORISE …" shown for exactly 3.5 s every time. My computer-use screenshots landed 5–7 s after each click (game clock vs screenshot clock), so they always missed the reveal. Countdown now runs only in visible, focused frames (capped 0.1 s per tick), so a hitch or focus loss can't use it up. | New EditMode `RehabExerciseVerification`; Player.log timings |
| A-001 | **Fixed**: `HandStreamOrder` accepts a restarted worker after 0.5 s stale | 6 offline cases; tracking 148/148 |
| P-023 | **Not a calculation defect**: endurance maths is correct (timed-hold test passes); my hold landed in the wrong stage | New offline test |
| P-021 | **Partly fixed**: baseline status shows "Stage n of 9 · task · s left"; animated coach still Stage 5 | Code + native compile |
| P-007 | **Fixed**: `activeSeconds` (≥1 N or closure ≥0.6) recorded and shown beside "input available" | Offline test; seen on results screen |
| A-018 / P-015 | **Fixed**: swept-segment waypoint hits; accuracy is distance to the route segment | 4 offline cases; one drag completed the trace at 100/100 in the player |
| P-004 | **Fixed**: wheel scrolls; footer scrolls by 80 % of a page; 48 px end padding | Player: one Scroll Down shows CHOOSE A MODULE fully |
| P-001 | **Fixed**: footer raised above a 44 px status strip | Player screenshot |
| P-018 | **Fixed** (root cause: theme blend used positional start colours while the page rebuilt) | Player: full Light page. Residual P-018b below |
| P-017 | **Fixed**: active theme marked `[Selected]` | Player screenshot |
| P-019 | **Fixed**: fields use Surface plus a border | Player screenshot |
| A-004 | **Fixed**: validated v1 payload contract restored | Bridges 8/8 |
| A-005 | **Fixed**: offline verifier follows `ProjectVersion.txt`; revealed and fixed 2 stale tests (A-021, A-022) | All 16 offline checks pass |
| A-006 | **Fixed**: package tarballs use `file:../Tools/...`; Unity re-resolved and rewrote the lock file | Native compile, 0 errors |
| A-007 | **Fixed**: stalled camera sleeps 20 ms per retry | Code review; bridges 8/8 |
| A-012 | **Fixed**: `RehabEditModeTests` runs 4 native verifications through Unity Test Framework | `-runTests` EditMode 4/4 |
| A-014 | **Blocked on you**: VirtualGrasp 1.7.0-free bakes grasps through Gleechi's online service (uploads object meshes, needs your Gleechi account) | — |
| A-017 | **Left open**: `RehabApp.cs` is on the protected-file list; retiring it needs your explicit OK | — |

New items found during Stage 2:

| ID | Sev | Finding | Status |
|---|---|---|---|
| A-021 | S4 | Product test compared `12 * 0.1` with literal `1.2` (float), so it always failed once the harness ran | Fixed in test |
| A-022 | S3 | Rollback removed three pose-stability behaviours that tests still described: hold visuals on a lost sample, keep calibration on reacquisition, 90 % response in 80 ms | Checks marked WITHDRAWN in `TrackingTests.cs`; restore in Stage 3/7 |
| P-018b | S3 | In Light theme, portal button labels render mid-grey (about 4:1 contrast) instead of the palette's near-black | Stage 4 |
| P-026 | S4 | Module 2/3 results show "Active grip 0.0 s", which isn't meaningful for non-grip tasks | Stage 6 |
| T-001 | process | Computer-use screenshots can land several seconds after an action. Timed UI states (reveals, countdowns) must be checked with logs or a long-lived state, not one screenshot | Noted for Stage 3+ audits |

## 6. Stage 3 gameplay pass (24 Oct 2026)

Commits `ab48c0a`..`108ec59`. Evidence: `Docs/evidence/stage3-2026-10-24/` (logs, per-file protected-change record and the `update_review.py` recorder). Level 1 of every exercise is the original content, so earlier checks and checkpoints keep their meaning.

| Plan task | Delivered | Verified by |
|---|---|---|
| 3.0 Shared contract | `ExerciseTuning`: one 1–10 level per exercise plus between-session progression (≥85 → up, <50 → down). Sessions store `level` and a validated list of named `outcomes`. | 7 offline tests |
| 3.1 Module 1 | `ForceRoundSession`: Hold, Control (over-squeeze cracks and restarts the round) and Pulse rounds on effort ÷ reference (baseline peak, else labelled 40 N keyboard / 50 N hardware demo, or camera closure); 30 s round timeout; score, stars and the PDF metrics (peak effort, time in band, in-band CV, time to target, over-squeezes). HUD shows round, type, effort and instruction; results show level, stars and metrics. | 5 offline tests; native compile; player suites |
| 3.2 Module 2 | Sequence Memory: stylised hand avatar demonstrates each gesture (Open/Fist/Point/Pinch); answers by camera gesture (`GestureClassifier` + 0.4 s `GestureStabilizer`), gesture buttons or keys 1–4; length 4–8 by level; fresh seed per attempt (A-015 fixed). Spatial Assembly: drag pieces onto matching outlines or tap piece then outline; placement error, path efficiency and speed. | Offline gesture and assembly tests; EditMode run completes memory by camera gestures and assembly by tapping |
| 3.3 Module 3 | Maze: tile board, gliding avatar, green goal, red flash on wall contact; seeded generated mazes from level 2 (5×5 to 13×13) scored on route efficiency. Tracing: dashed route, numbered waypoints, green/red trail; routes and tolerance by level; mean deviation recorded. | Offline maze/trace tests; EditMode solves a level-5 maze at 100 |
| 3.4 Deformation | `SqueezeLiquid`: sponge drips water and tomato drips juice in proportion to compression, and the tomato bursts when broken. In-house stand-in for the Obi Fluid role, visual only. | EditMode guard on the authored URP particle material |

**Results on the Stage 3 build:**
- **Windows player:** smoke / replay / journey 168 / 169 / 74 checks, plus 10 model checks each; UI 60 checks at each of 2 sizes; gallery 87 at each of 3.
- **Unity test runner:** EditMode 5/5.
- **Offline C#:** product 57, tracking 177, all 16 offline checks.
- **Python:** Tools 66, bridges 8, WBS 45, AI 32.
- **P-026** (Module 2/3 "Active grip 0.0 s") is fixed: those results now show time instead.

**Not yet done:**
- **Hands-on playtest of the new screens is PENDING.** Computer use was held by another Claude session at the end of Stage 3. Layout, readability and feel of the new Module 1 HUD and the Module 2/3 boards have been checked only by code and automated runs, not by eye. Do this first in the Stage 3 review or at the start of Stage 4.
- **Grip-shape mapping (Sphere/Cylinder/Pencil → objects)** is not implemented: the apartment has no pencil-like object asset. The six objects remain selectable for every grip.
- **No YOLO model:** gestures come from the landmark classifier (G06 substitute, documented).
- **No Obi Softbody/Fluid:** in-house deformation plus `SqueezeLiquid`.
- **The avatar is a 2D stylised hand on the exercise card**, not a 3D character.

### Stage 3 hands-on playtest (24 Oct 2026, camera-free build copy, keyboard/mouse)

All five exercises were played end to end in the Windows build. Your LocalLow data was backed up and restored byte-for-byte (11/11 SHA-256), and the camera stayed off.

| ID | Sev | Found | Fix and check |
|---|---|---|---|
| S3-01 | **S2** | Module 1 Control round counted a crack on every 10 Hz sample while over the limit ("cracked 13x" from one 1.2 s slip, −15 each) | One crack per crossing; new offline test (product 58/58) |
| S3-02 | S3 | Memory cue "MEMORISE 3 3 4 3" spelled the answer as numbers over the gesture avatar | Cue reads "MEMORISE THE GESTURES" |
| S3-03 | S4 | Gesture strip left-aligned for short patterns | Centred on the pattern length (seen in player) |
| S3-04 | S4 | White glyph on the bright active tile was hard to read | Glyph turns dark on highlighted tiles |
| S3-05 | S3 | Results showed "*** of 3 stars", "Level" twice and a large gap | "n of 3 stars", level once, tighter spacing (seen in player) |
| S3-06 | **S2** | Assembly outlines rendered as solid blocks, and placed pieces vanished behind them. Unity's UI `Outline` effect copies the whole image. | Outlines drawn as real frames; placed piece on top (seen in player) |
| S3-07 | S3 | Assembly, trace and maze boards were invisible: inferred theming painted them the card colour | Boards tagged with the Secondary theme role (seen in player) |
| S3-08 | S3 | Cue line clipped; boards overlapped it | Taller cue, boards lowered (seen in player) |
| S3-09 | S3 | Maze corridors were the card colour, so only walls showed; then direction buttons vanished on the new board colour | Corridor colour plus darker button surface (seen in player) |
| S3-10 | S3 | Results page replaced the board the instant an exercise finished | 1.4 s visible completion hold; EditMode verification checks it |
| S3-11 | S4 | Module card copy still says "accessible buttons and keys" | **Open**: text lives in the protected saved scene; change with Stage 4 UI work |
| S3-12 | S4 | Module 1 HUD has no visual target-band gauge, and the force digits still use the slashed-zero font (P-006) | **Open**: Stage 4 presentation |
| T-002 | process | Computer use warps the pointer and clicks within one frame; Unity sometimes misses the first click on a newly shown page | Hover before clicking in future playtests; real mice are unaffected |
| T-003 | process | Focus thieves on this machine (ScreenRec, PC Manager) pause the game's focused-only timers; one gallery run stalled and passed on rerun | Noted; not a game defect |

**Played and confirmed:**
- **Module 1:** three rounds (Hold → Control → Pulse) with HUD instructions, and a results page with all metrics.
- **Sequence Memory:** avatar demonstration, then button answers, then results.
- **Assembly:** completed both by dragging and by tapping.
- **Tracing:** 100% when following the line; a deliberate detour lowers accuracy.
- **Maze:** moves and wall contacts scored.

**Final build:**
- **Windows player:** smoke / replay / journey 168 / 169 / 74 checks, plus 10 model checks each; UI 60 checks at each of 2 sizes; gallery 87 at each of 3 (on rerun).
- **Unity test runner:** EditMode 5/5.
- **Offline C#:** product 58, tracking 177.
- **Python:** Tools 66, bridges 8, WBS 45, AI 32.

## 7. Stage 4 presentation pass (24 Oct 2026)

Commit `cea20a8` plus the review fixes below. Plan tasks 4.1–4.4.

| Task | Delivered | Evidence |
|---|---|---|
| 4.1 Lighting / rendering | Realtime reflection probes at the seats, warm grade on a runtime copy of the post-process profile (white balance, saturation, exposure, bloom threshold) | Seen in player; reflections and grade judged by eye only |
| 4.2 Hand / object materials | Virtual hand tone setting: GLOVE, LIGHT, MEDIUM (default) or DEEP, tinting a runtime copy of the hand material | DEEP seen in Module 1 |
| 4.3 Audio | `RehabAudio`: six synthesised effects (success, error, crack, bump, chime, squish), step tick, squeeze creak loop and room tone. Hooked to rounds, answers, waypoints, maze, assembly and breaks. Mute, effects volume and ambience settings are stored per computer. | `RehabAudioVerification` (EditMode): every sound present, audible, unclipped, no NaN, loops start and end silent |
| 4.4 UI/UX | Module 1 effort gauge with target band and red over-limit marker, readable force digits (closes S3-12 / P-006), new module card copy (closes S3-11), first-run HOW TO PLAY card for each Module 2/3 exercise, larger off-path trace dots, maze snaps under Reduce Motion | Seen in player (screenshots in `evidence/stage4-2026-10-24/play/`) |

### Stage 4 by-eye review (24 Oct 2026, camera-free build copy, keyboard/mouse)

Your LocalLow data was backed up and restored byte-for-byte (11/11 SHA-256, identical file list). Four game preferences created during the review (hand tone and three tutorial flags) were deleted afterwards.

| ID | Sev | Found | Fix and check |
|---|---|---|---|
| S4-01 | S3 | HOW TO PLAY card let the board text show through at about 20% brightness. The card was 97% opaque, and in linear colour space a 3% leak of white text is clearly visible. | Card made fully opaque. Rebuilt; EditMode and player suites pass. Not re-seen by eye. |
| S4-02 | S3 | Every Appearance toggle rebuilt the page scrolled to the top, so the new rows at the bottom needed scrolling again after each click | Settings keep their scroll position (`ResearchPortalView.ScrollPosition`, `SettingsAt`). Rebuilt; suites pass. Not re-seen by eye. |
| S4-03 | S4 | While squeezing, the HUD showed "Silicone ball · 0.0 mm" in one frame: the fist had closed beside the ball, not around it, so no finger contact dented the mesh | **Open** (grip-contact behaviour, not presentation). Apartment acceptance still proves >3 mm dents at demo force. Revisit with Stage 5/7 grip work. |

**Seen and confirmed in the player:**
- Appearance rows for game sounds, effects volume, room ambience and virtual hand; the hand tone cycles.
- Module cards show the new copy.
- Module 1: readable digits, gauge marker inside the green band at 45% effort, DEEP hand tone, three rounds through to results (2 of 3 stars, outcomes listed).
- The tutorial card appears on first entry to Sequence Memory, Visual Tracing and Maze, pauses the reveal and timers, and GOT IT dismisses it.
- Tracing: small green on-path dots, larger red off-path dots.
- Maze: moves and a wall contact counted.

**Not verified by eye or ear:**
- Sounds were not listened to; only the automated synthesis check covers them.
- The Spatial Assembly tutorial was not opened.
- Reflections and grade were judged on one station only.

**Deviations from the plan:**
- No AudioMixer asset; volumes are set in code.
- No baked GI, light probes or quality tiers; that work moves to Stage 7 (performance).

**Final build after the fixes:**
- **Windows player:** smoke / replay / journey 168 / 169 / 74, each with 10 model checks.
- **Other player suites:** UI passed at 2 sizes; gallery 87 at each of 3 sizes.
- **Unity test runner:** EditMode 6/6.
- **Offline C#:** product 58, tracking 177.
- **Python:** Tools 66, bridges 8, WBS 45, AI 32.

All of these use synthetic or fixture input only. No physical hardware, camera or participant was used.

## 8. Severity summary and Stage 2 entry list (Stage 1)

- **S1 (2):** A-002 profile rollback on transient read error · P-012 Sequence Memory pattern never shown.
- **S2 (12):** A-001, A-003 (my Stage 0 regression), A-004, A-005, A-006, A-019, P-004, P-007, P-015 (with A-018), P-018, P-019, P-023. A-020 (S3) is fixed together with A-002.
- **S3/S4:** remaining items, owned by Stages 3–9 as listed.

Recommended Stage 2 order: A-003 → A-002/A-020 → P-012 → A-001 → P-023 → P-007 → A-018/P-015 → P-004/P-001 → P-018/P-019 → A-004/A-005/A-006 → A-014 (Grasp DB) → A-012 (test assemblies) → A-017 cleanup.
