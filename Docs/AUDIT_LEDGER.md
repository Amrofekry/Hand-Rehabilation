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
| S4-03 | S4 | While squeezing, the HUD showed "Silicone ball · 0.0 mm" in one frame: the fist had closed beside the ball, not around it, so no finger contact dented the mesh | **Fixed in Stage 8** (§10a): fingers close onto the held ball, 5 pads touch, 7.7 mm dent at 18 N (SYNTHETIC). |

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

### Hand-only play for Spatial Assembly and Maze (24 Oct 2026, requested after the Stage 4 review)

Both exercises can now be played with the camera hand pointer alone, like the menus and Visual Tracing. Mouse, tap and keyboard stay as alternatives.

- **Spatial Assembly:**
  - Hold the hand pointer on a piece for the pointer's dwell time (5 s, the same as buttons) and the piece lifts and follows the hand.
  - Hold it still on an outline for 2 s to place it. The scoring is the same as tapping: a wrong outline counts one error and sends the piece back.
  - If the hand is lost for 1.5 s, the piece returns to the tray without an error.
- **Maze Navigation:**
  - Move the hand onto the blue marker to take hold of it. It then follows the hand one cell at a time along open corridors, one step every 0.16 s.
  - Pushing into a wall counts one wall contact for each new hand cell, not one per frame.
  - Losing the hand for 1.5 s lets go of the marker. A mouse drag on the board works the same way.
- **The pointer ring and status line show what to do:**
  - "HOLD 4s"
  - "CARRYING 2"
  - "HOLD TO PLACE"
  - "TOUCH THE MARKER"
- **Code:** `RehabHandSurface` and `HandFeedback` in `RehabExerciseSuite.cs`, `AssemblyHand` / `MazeHand` / `ReleaseHand`, and a captured-surface hook in `RehabDwellPointer`. Instructions and tutorial cards were rewritten for hand play.
- **Checks:** `RehabExerciseVerification` (EditMode) covers:
  - no pick-up before 5 s, then pick-up at 5 s;
  - the piece follows the hand;
  - a wrong outline gives one error and the piece returns;
  - a lost hand returns the piece without an error;
  - all pieces placed by hand (score 90 with the one error);
  - the maze ignores the hand until the marker is touched;
  - a lost hand releases the marker;
  - following the corridors by hand reaches the goal with no contacts (score 100).

**Played in the Windows build with a synthetic hand.** Frames were sent over loopback UDP by `evidence/stage4-2026-10-24/hand_driver.py`, the same 21-landmark format the acceptance runner uses. No camera and no person were involved, so this is not a physical or human test.

| Check | Result |
|---|---|
| Assembly, all 4 pieces placed by hand | 100/100, 3 stars, 0 wrong outlines, path efficiency 98.7% |
| Maze tutorial GOT IT pressed by hand dwell | Pass |
| Maze, first run | Goal reached by hand, but 12 moves against a shortest route of 10 (96/100). The hand came in diagonally and took hold of the marker from the next square, which stepped the marker. |
| Maze, after the fix (hand must be on the marker's own square) | 10 moves, 0 contacts, 100/100 |
| Focus | Once, the hand did nothing because the window had lost focus (T-003); it worked after the window was brought forward |

Screenshots are in `evidence/stage4-2026-10-24/hand-play/`.

**Not tested:**
- A real webcam hand. Real-hand tremor, tracking loss and comfort of the 5 s pick-up and 2 s placement still need a hands-on session with the camera.

**Final build after the hand-play change:**
- **Windows player:** smoke / replay / journey 168 / 169 / 74, each with 10 model checks.
- **Other player suites:** UI passed at 2 sizes; gallery passed.
- **Unity test runner:** EditMode 6/6, including the hand-play checks.
- **Offline C#:** all jobs pass (product 58, tracking 177).
- **Python:** Tools 66, bridges 8, WBS 45, AI 32.

Your LocalLow data matched the backup (11/11 SHA-256) after the session. The review-created tutorial preferences were deleted.

## 8. Stage 5 closed adaptive loop and safety UX (24 Oct 2026)

Plan tasks 5.1–5.4. Everything here is software, checked with synthetic or fixture input only. No device, live person or validated fatigue measure is involved, and all thresholds remain unvalidated research settings.

| Task | Delivered | Checked by |
|---|---|---|
| 5.1 Baseline flow | Big coach cue for each stage (SQUEEZE, RELEASE, OPEN/CLOSE alternating, HOLD, STAY STILL) with a countdown and a gentle pulse that is off under Reduce Motion. A per-stage signal-quality line (GOOD / FAIR / POOR) measures fresh-sample time against wall time. Prepare page says when there is no baseline yet, or when the saved baseline came from a different input, and asks for a new one. A mid-session source, shape or calibration change still stops comparison (existing guard) and now says so in plain language. | Product tests (quality and cues, baseline match); the player journey runs the full baseline with the coach |
| 5.2 Adaptation in every module | `AdaptiveDifficulty` carries the policy's in-session level onto the 1–10 exercise scale: one step per decision, at most 3 levels from the session's start, inside 1–10. Module 1 retunes the band, hold and pulses of rounds not yet started (a round in progress keeps its target, and the round count never changes). The policy now runs in Modules 2 and 3 on hand motion (and opt-in face or pulse). Keyboard or mouse alone never adapts them, and force and the proxy model stay Module 1 only. The next attempt starts from the adapted level and then follows the score. A safety stop starts the next attempt one level lower; an interrupted attempt keeps its level. Results show "level 3 to 4 (adapted during play)". | Product tests (bounded steps, retune, next-session rule, 120 s policy replay across all module settings); EditMode `ClosedAdaptiveLoop` |
| 5.3 Warning and stop UX | Plain-language pause and stop screens, each labelled "RESEARCH INDICATOR · unvalidated estimate, not a medical assessment". Signal loss shows "Paused: signal lost … not counted as tiredness". One sign shows "Time for a short rest" naming the sign in everyday words. Two signs show "Session stopped for today", saved as a safety stop with no automatic restart. Resume is accepted only after fresh, settled windows, and the screen says so. Finish Session latches the stop (`FatigueSafetyPolicy.Quit`). Pauses cover Modules 2 and 3 too; their timers wait because the shell leaves the exercise page. | Product tests (simultaneous signs, stale/NaN prediction, bad-input hold, quit during warning, wording); seen in the player |
| 5.4 Model runtime | Kept the Sentis CNN. `ProxyModelGuard`: the public-EMG proxy reads only synthetic Newton input, never real device force or camera closure. | Product test; packaged-player model guards unchanged (10 checks per run) |

**Gate 5 (SYNTHETIC closed loop):** `ClosedLoopVerification` replays all 5 cases of `Resources/ResearchAi/SYNTHETIC_policy-fixture.json` through the policy, the adaptive level and every module's settings.
- **Steady play:** raises difficulty by two bounded steps.
- **One sign:** pauses.
- **Signal dropout:** pauses without counting a sign.
- **Two signs:** stop and latch without raising difficulty.

Every pause shows plain wording, never policy codes.

**Seen in the Windows build** (camera-free copy, guest, Camera input, guidance on):
- Module 1 opened on "Paused: signal lost" with the research-indicator label.
- Resume was refused while no signal arrived.
- Finish Session ended on "Session finished" with no restart.

Screenshots are in `evidence/stage5-2026-10-24/play/`.

| ID | Sev | Found | Fix and check |
|---|---|---|---|
| S5-01 | S2 | First Stage 5 build failed the player journey: TextMeshPro threw during layout because the new coach line ("·") and results line ("→") used glyphs missing from the heading font | Coach, results and pause titles use plain characters. Journey 74/74 on rebuild. |
| S5-02 | process | One gallery run timed out on the first build (T-003 pattern) | Passed on the rebuild |

**Not done or not verified:**
- No real webcam, device or person.
- The face and pulse signs were exercised only through the fixture.
- The baseline coach's pulse animation was not watched by eye.
- Adaptation in Modules 2 and 3 needs camera hand motion and a matching baseline, so it was checked by test only.

**Final suites:**
- **Windows player:** smoke / replay / journey 168 / 169 / 74, each with 10 model checks.
- **Other player suites:** UI passed at 2 sizes; gallery passed.
- **Unity test runner:** EditMode 7/7.
- **Offline C#:** product 68 (was 58), tracking 177.
- **Python:** Tools 66, bridges 8, WBS 45, AI 32.

Protected changes, each with a reason, are in `evidence/stage5-2026-10-24/protected-source-review.json`:
- `ResearchPortalHost`
- `ResearchSessionRuntime`
- `ProductModels`
- `CatalogValidation`

Your LocalLow data was restored and verified (11/11 SHA-256).

## 9. Stage 6 dashboards (24–25 Oct 2026)

Plan tasks 6.1 and 6.2 (WBS 5.1, 5.2). All numbers below come from fabricated, clearly labelled fixtures. No real patient or clinician used these screens.

| Task | Delivered | Checked by |
|---|---|---|
| 6.1 Patient dashboard | **My Progress** has five summary cards (force, endurance, stability, reaction time, accuracy) from the latest completed attempts. Each card names its measure and shows "—" when not measured, never 0. A baseline comparison appears only for measured force from the same input, otherwise "—" with the reason. Per-module statistics, plus score trends from completed attempts only. Session history marks "STOPPED · research safety" and "INCOMPLETE" with no score, and its chart leaves unfinished attempts out instead of drawing 0. | Product tests (exact fixture numbers, dash rules); walked in the build |
| 6.2 Doctor dashboard | **D1** sign-in and **D2** search and sort were kept. **D3** Progress by Module shows the same statistics plus research pause/stop events in plain words. **D4** notes show "written by <doctor>" and offer five editable templated recommendations. **D5** audit trail: doctor sign-in and sign-out, record views, notes and every export, each with author and time and no record content copied. A doctor's own sign-in log is available, with Sign Out on the workspace. Exports: CSV (existing), the full PDF report (unfinished attempts now show "--" instead of 0) and a new one-session PDF with outcomes and research events. | Product tests (exact numbers, no cross-patient mixing, role checks, audit contents); EditMode `DashboardFixturesOnDisk`; walked in the build |

**Gate 6 walkthrough** (Windows build with `-dashboard-fixtures`, which uses a throwaway folder seeded with fabricated accounts patient.alpha, patient.beta and doctor.demo, behind a visible "FABRICATED DEMO DATA" banner):
- **Patient:** My Progress showed exactly the tested values (55 % ref, 22.7 s, 0.041 CV, 1.80 s, 83 %, and "22.0 N today vs 20.0 N baseline (110%) · SYNTHETIC"). Module 1–3 statistics and the completed-only trends matched. History showed "INCOMPLETE · —".
- **Doctor:**
  - D1/D2 listed both patients.
  - D3 showed the PAUSE at 30 s and STOP at 40 s in plain words.
  - D4 saved a templated note with its author.
  - D5 listed every view and note.

The fixture folders were deleted afterwards. Screenshots are in `evidence/stage6-2026-10-24/play/`.

| ID | Sev | Found | Fix and check |
|---|---|---|---|
| S6-01 | **S2** (pre-existing) | Unity's JSON stores a null id as "", so `SaveSession` saw a changed `baselineId` and refused to save **any session for a patient without a baseline**. The journey test missed it because its patient always collects a baseline first. Found when the fixture seeding failed in the real encrypted store. | `ProductRepository` compares null and "" as equal. The new EditMode test seeds through the real encrypted store and reloads. |
| S6-02 | S3 | The doctor's sign-in log filtered on a null subject, which the same JSON round-trip turns into "" | Filter accepts empty; covered by the same EditMode test |
| S6-03 | process | Fixture seeding errors were swallowed, leaving a half-seeded demo | Fixture banner now shows any seeding problem and signs out |

**Not done:**
- No real clinician review of wording or layout.
- The cards and statistics are game measures, not clinical scores.
- Exported PDFs were checked by test (structure and labels), not opened by eye.

**Final suites:**
- **Windows player:** smoke / replay / journey 168 / 169 / 74, each with 10 model checks.
- **Other player suites:** UI passed; gallery passed.
- **Unity test runner:** EditMode 8/8.
- **Offline C#:** product 74 (was 68), tracking 177.
- **Python:** Tools 66, bridges 8, WBS 45, AI 32.

Protected changes, each with a reason, are in `evidence/stage6-2026-10-24/protected-source-review.json`:
- `ResearchPortalHost`
- `ResearchPortalPages`
- `RehabAppShell`
- `ProductRepository`
- `ProductModels`
- `ResearchExport`

Your LocalLow data was restored (11/11 SHA-256).

## 10. Stage 7 performance, stability and robustness (25 Oct 2026)

Plan tasks 7.1–7.3 (WBS 6.2). Everything was measured in the rendered Windows player on the reference laptop: i7-11800H, RTX 3060 Laptop GPU, 16 GB, 1920×1080 windowed, "PC" quality. Input was SYNTHETIC only, through the new `-apartment-perf-probe` (isolated acceptance storage, no camera, device or player records), run by `Tools/run_perf_probe.py`.

### 7.1 Budgets

| Phase (20 s each) | Mean FPS | Frame p95 | FPS at p95 (budget 60) | Managed alloc / frame (budget < 1 KB) | Errors |
|---|---|---|---|---|---|
| Menu | 334 | 3.9 ms | 257 | 209 B | 0 |
| Module 1 (held ball, synthetic squeeze) | 256 | 6.8 ms | 146 | 255 B | 0 |
| Module 2 (memory, then hand-played assembly) | 335 | 3.9 ms | 257 | 307 B | 0 |
| Module 3 (tracing, then maze) | 324 | 4.0 ms | 250 | 458 B | 0 |

- **Loopback hand-packet latency** (send to the frame that sees it): p50 2.3 ms, p95 4.2 ms. This excludes camera capture and MediaPipe inference.
- **Final rebuild re-run** (after the S7-01 log fix): FPS at p95 was 241 / 128 / 235 / 236 and allocation 220 / 283 / 341 / 514 B per frame, with 0 errors. Run-to-run variation on the same laptop; still inside both budgets.
- **Budgets met** for the synthetic, camera-off case.
- **Allocation method:** release players have no per-frame GC counter, so allocation is managed-heap growth per frame. The heap grows in 4 KB blocks, so the mean is the fair figure. The probe was changed to cache its own reflection and maze solve, so its garbage isn't counted.

**Not measured:**
- **FPS with the camera worker running, and camera-to-hand latency.** Both need your webcam and consent.
- **Profiler or Memory Profiler captures from a development build.** Because every budget is met, no hotspot fixes were needed this stage.

### 7.2 Soak and fault injection

| Fault | Result |
|---|---|
| Malformed UDP (10 kinds: empty, truncated, wrong schema, NaN, 60 kB, wrong handedness and others) sent to ports 27817, 27818 and 27820 | 30 sent, 9 rejected and counted by the hand receiver, **0 errors**, game kept running |
| Ports 27817/27818 already in use (review focus 2) | Runner holds both ports before launch. The game says "Hand-tracking port 27817 is already in use. Close any other GripRehab window and restart." and "Force bridge port unavailable: AddressAlreadyInUse", keeps running at 340 FPS, no hang, 0 errors. **New:** the hand-pointer status line shows this message (it was only a log warning before). |
| Disk full during a session save | New product test: the last saved copy is kept, and a retry succeeds once space returns. The game's "Saving needs attention" path was already present. |
| Corrupt profile or catalog | Existing tests: corrupt primary is preserved before backup recovery; both copies unreadable fails closed; missing primary is restored from backup; older catalogs load (Stage 6). |
| Camera worker killed | **Not run.** It needs the webcam worker running. Camera loss mid-grip pauses safely (Stage 5). |

**30-minute soak per phase** (2 hours in one run, SYNTHETIC input; `evidence/stage7-2026-10-25/logs/perf-soak.json`):

| Phase (1800 s) | Mean FPS | Frame p95 | Alloc / frame | Total Unity memory start → end | Managed heap at end | Iterations | Errors |
|---|---|---|---|---|---|---|---|
| Menu | 330 | 4.5 ms | 188 B | 99 → 100 MB (−0.0 %) | 17.5 MB | — | 0 |
| Module 1 | 220 | 7.4 ms | 240 B | 100 → 100 MB (+0.2 %) | 15.0 MB | ball held throughout | 0 |
| Module 2 | 315 | 4.4 ms | 243 B | 101 → 101 MB (+0.5 %) | 16.4 MB | 125 restarts | 0 |
| Module 3 | 316 | 4.4 ms | 452 B | 101 → 101 MB (+0.0 %) | 15.8 MB | 3,956 restarts | 0 |

- **Pass:** no leak growth over 10 %, 0 errors and 0 exceptions in 2 hours.
- **How to read the managed-heap column:** single readings sit on the garbage collector's normal saw-tooth, so start-to-end percentages swing either way (for example +144 % in the menu, −37 % in Module 1). The ending values (15–17.5 MB) and the flat total show nothing accumulates.
- **After the soak:** hand latency was p50 2.7 ms and p95 4.3 ms; the malformed-packet test passed again.

### 7.3 Crash and log capture

- **Rolling log:** `%LOCALAPPDATA%/GripRehab/logs/griprehab-<time>.log`, newest 5 kept, 1 MB cap each. Every Unity log line is written with level and time, plus unhandled and unobserved-task exceptions.
- **No personal data:** password-like text and user-profile paths are scrubbed. No names, records or the computer's name are written. A sample is in `evidence/stage7-2026-10-25/logs/`.
- **Diagnostics for support:** a new Settings page that shows device facts, health counters and recent log lines, then copies them on request.
- **Tests:** product tests cover rotation, size cap, scrubbing and counters.

**Seen in the build:** the Diagnostics page shows device facts, counters and recent lines, with every path as `%USERPROFILE%\…`. Screenshot: `evidence/stage7-2026-10-25/play/`.

| ID | Sev | Found | Fix and check |
|---|---|---|---|
| S7-01 | S3 | The page counted 7 errors in 36 s: the camera launcher logs the same "Tools/Vision not found" error every 5 s (expected in the camera-free copy, which has no camera tools) and would fill the 1 MB log in a few hours | The log writes a repeated line once and then "previous line repeated N more times"; counters still count every occurrence. Product test added. |
| S7-02 | process | First probe build reported 0 B/frame (the per-frame GC counter does not exist in release players), working set 0 (not reported by Mono) and Module 1 never grabbing (camera mode, and a teleport moved the player out of reach) | Heap-growth fallback, Unity memory totals, keyboard mode from the seated start; the probe fails loudly if Module 1 never holds the ball |

**Suites:**
- **Windows player:** smoke / replay / journey 168 / 169 / 74, each with 10 model checks.
- **Other player suites:** UI and gallery passed; busy-ports probe passed.
- **Unity test runner:** EditMode 8/8.
- **Offline C#:** product 76 (was 74), tracking 177.
- **Python:** Tools, bridges 8, WBS 45, AI 32.

Protected changes, each with a reason, are in `evidence/stage7-2026-10-25/protected-source-review.json`:
- `ResearchPortalPages`
- `VisionUdpReceiver`

## 10a. Stage 8 hand–object contact and the grip chart (25–26 Oct 2026)

Plan tasks 8.1–8.5 (WBS 3.4.1, 3.3). All numbers below are **SYNTHETIC**: rendered Windows player on the reference laptop, keyboard-demo force (0 / 18 / 35 N), scripted hand motion, isolated acceptance storage, no camera, device or person. Probe: `-apartment-contact-probe` (`Tools/run_contact_probe.py`); `-contact-solver-off` runs the same build with the pre-Stage-8 behaviour as the baseline. Data: `evidence/stage8-2026-10-25/contact-probe*.json`.

**What changed:**
- **Contact solver** (`GraspContactSolver`, `ContactGeometry`): after the hand pose is set, each finger joint curls only as far as the object surface plus 2 mm skin and the visible dent (binary search per joint, proximal to distal). Depth is exact for sphere, box and capsule colliders.
- **Hold placement:** the object rests on the palm, at the fingertips or in the hooked fingers, per grip, with a bounded back-off while anything overlaps. Thumb-against-one-finger pinches ease the object toward a finger that has not reached it. Held-object follow is capped at 3.5 m/s.
- **Grip chart** (your 2026-10-26 picture): `GripCatalog` holds the 18 grips as data (hand shape per joint, spread, touching segments, anchor, viewing angle). `GripLab` builds one prop per grip in code (D7). A new **GRIP LAB** station has `[` / `]` or the options buttons to step through the chart. The six station objects also use chart grips: ball and sponge power, mug and silicone cup cupped, cracker precision, tomato spherical.
- **Presentation (keyboard/demo input only):** the hand takes the grip's shape and turns to the chart's viewing angle. The held object is brought to the middle of the view, raised only to clear the surface under it. The interlocking grip brings in and draws the second hand. A tracked camera hand keeps its own measured pose.
- **Placement safety (solver on):** the path from the eye is swept with at most 5 cm. The landing check tests the object's own colliders (> 2 mm overlap). The old full-size bounding sphere grazed the table and held large objects short.
- **Allocation fix:** the solver first cost 16 KB/frame (Module 1 over budget). The cause was a lambda capturing a parameter, which allocates its closure on every call (about 500 calls a frame). The fix moved the lookup out and replaced delegates with struct callbacks.

### Station objects, before → after (6 objects × upright / turned 90° / tilted 45°)

| Object | Finger/palm penetration while holding (0–35 N) | Mean pad gap | Hold jitter (RMS) | Reach move ~1.1 m/s | Fast shake ~2.8 m/s |
|---|---|---|---|---|---|
| Silicone ball (14 cm) | 69.8 → **0** mm (palm 5.7) | 0 → 0 mm | 0.0 → 0.0 mm | 62.5 → **0** mm | 72.6 → 17.7 mm |
| Porcelain mug | 49.7 → **1.0** | 4.0 → 1.0 | 0.0 → 0.0 | 51.8 → 2.1 | 51.9 → 27.8 |
| Brittle cracker | 19.0 (palm 27.2) → **0** | 0.2 → 0.0 | 0.0 → 0.0 | 19.8 → 1.2 | 19.8 → 13.3 |
| Kitchen sponge | 35.6 → **5.7** | 8.7 → 1.4 | 0.0 → 0.0 | 28.0 → 0 | 33.4 → 32.2 |
| Silicone cup | 47.7 → **1.0** | 3.1 → 0.0 | 0.0 → 0.0 | 46.6 → 6.1 | 47.6 → 44.6 |
| Ripe tomato | 26.3 → **0** | 8.8 → 0.3 | 0.0 → 0.1 | 23.8 → 0.9 | 24.5 → 32.6 |

- **No drops, no furniture overlap (> 2 mm) in any case.** The dent at 18 N is 7.7–12.4 mm on soft objects, so S4-03 (the fist closing beside the ball, 0 mm dent) is fixed for held objects.
- **Settling:** after pickup the hand takes about 1 s to turn and come into view. Hold measurements start after a 1 s settle; during that settle, the first run measured 4.5–7.4 mm of movement.
- **Known limit (C-10):** a fast shake at about 2.8 m/s peak (far faster than rehab movement) still shows 13–45 mm transient overlap. The held body follows one physics step behind the hand. Reach-speed motion (~1.1 m/s) stays within 6 mm.

### Grip chart: 18 grips (Grip Lab), solver on

| # | Grip | Object | Penetration hold / reach | Pads touching at 18 N | Mean pad gap | Jitter |
|---|---|---|---|---|---|---|
| 1 | Power | Blue rubber ball | 0 / 0 mm (palm 3.6) | 5 of 5 | 0.0 mm | 0.0 mm |
| 2 | Precision | Small red cube | 0 / 0 | 2 of 2 | 0.0 | 0.0 |
| 3 | Three-finger | Ballpoint pen | 0 / 0 | 0 of 3 | 2.7 | 0.0 |
| 4 | Lateral pinch | Plastic card | 0 / 0 | 2 of 2 | 0.0 | 0.0 |
| 5 | Hook | Kettlebell | 0 / 0 | 4 of 4 | 0.0 | 0.0 |
| 6 | Claw | Basketball | 1.0 / 0 | 5 of 5 | 0.4 | 0.0 |
| 7 | Palm | Water bottle | 1.0 / 2.4 | 4 of 7 | 5.2 | 0.1 |
| 8 | Spherical | Stress ball | 0 / 0 (palm 2.7) | 5 of 5 | 0.0 | 0.0 |
| 9 | Finger tip | Coin | 0 / 0 | 1 of 2 | 3.5 | 0.0 |
| 10 | Cupped | Porcelain cup | 0 / 2.3 | 4 of 7 | 2.7 | 0.0 |
| 11 | Two-finger | USB flash drive | 0 / 0 | 1 of 2 | 2.5 | 0.0 |
| 12 | Lateral/thumb | Smartphone | 0 / 1.4 | 2 of 4 | 1.1 | 0.0 |
| 13 | Precision 3-finger | Screw | 0 / 0 | 3 of 3 | 0.0 | 0.0 |
| 14 | Fist | Grip strength tool | 0.2 / 2.2 | 6 of 9 | 3.9 | 0.0 |
| 15 | Side | Hardcover book | 0 / 0 | 4 of 4 | 0.0 | 0.0 |
| 16 | Interlocking (2 hands) | Large rubber ball | 1.0 / 1.2 | 5 of 5 | 0.0 | 0.0 |
| 17 | Folded | Cleaning cloth | 0 / 0 (palm 4.7) | 7 of 7 | 2.1 | 0.0 |
| 18 | Dynamic | Claw hammer | 0.2 / 2.5 | 5 of 9 | 3.3 | 0.0 |

- **Across all 18:** finger penetration ≤ 1.0 mm while holding and ≤ 2.5 mm at reach speed; palm overlap ≤ 4.7 mm; hand-relative jitter ≤ 0.1 mm; no drops.
- **"Touching" means within 2 mm.** The open gaps are small: the pen's three pads sit 2–3 mm off it, and one pad stays 3–7 mm off the coin and USB drive. Larger wrap grips (bottle, cup, fist, hammer) also leave 2–5 pads short.
- **Book:** 3 frames of furniture overlap while it was being lifted into view.
- **Screenshots, judged by eye by me:**
  - `evidence/stage8-2026-10-25/play/grip-chart-01-09.jpg`
  - `grip-chart-10-18.jpg`
  - `station-objects-before-after.jpg`

  Your own look in the game is the real check.

**Not measured:** a real hand through the camera holding each grip, and any clinical or physical validity of the grip shapes. The shapes are modelled on your chart, not measured from a person.

### Performance after Stage 8 (reference laptop, 1920×1080, SYNTHETIC)

| Phase | FPS at p95 (budget 60) | Alloc / frame (budget < 1 KB) | Errors |
|---|---|---|---|
| Menu | 241 | 221 B | 0 |
| Module 1 (held ball, solver on) | 170 | 246 B | 0 |
| Module 2 | 258 | 289 B | 0 |
| Module 3 | 252 | 482 B | 0 |

- **Hand-packet latency:** p50 2.5 ms, p95 4.2 ms. The malformed-packet test passed: 30 sent, 9 rejected, 0 errors.
- **Run-to-run variation:** another run on the same build gave Module 1 at 87 FPS (p95) and 368 B/frame, still inside budget.
- **Solver off (A/B on an earlier Stage 8 build):** Module 1 at 257 FPS (p95) and 187 B/frame.

| ID | Sev | Found | Fix and check |
|---|---|---|---|
| S8-01 | S2 | Solver allocated 16 KB/frame (lambda closure in the collider lookup) | Lookup split out, struct callbacks; Module 1 back to 246–368 B/frame |
| S8-02 | S3 | Large objects held short of the hand: a full-size bounding-sphere sweep from the eye grazed the table | 5 cm path sweep, exact landing check (solver on); baseline keeps the old checks |
| S8-03 | S3 | A one-way palm back-off ratcheted to 4 cm while the hand turned, leaving the hammer 5 cm off the fist | Back-off settles back when clear and not touching |
| S8-04 | S4 | Second hand of the interlocking grip invisible (only the selected hand's mesh drawn) | `RehabVirtualGrasp.ShowBoth` while a two-handed grip is held |
| S8-05 | S4 | C-10: transient 13–45 mm overlap at about 2.8 m/s shake | **Open**, documented; reach speed is clean |

**Suites (final build):**
- **Windows player:** smoke / replay / journey 168 / 169 / 74, each with 10 model checks.
- **Other player suites:** UI passed; gallery 87 checks.
- **Unity test runner:** EditMode 8/8.
- **Offline C#:** product 76, tracking 177, grasp 235 (was 67; contact geometry and grip chart checks added), deformation 61, hand rig 107, pointer 120.
- **Python:** Tools unit tests, bridges, WBS 45, AI 32.

**One acceptance check was revised:**
- "Rigged fingers close onto a held object" now requires closure > 0.1 and at least 3 pads touching. Measured: closure 0.14, 5 pads, 0.0 mm.
- The old check (mean curl > 0.4) could only pass with the fist inside the 14 cm ball.
- Two binding-test assertions were updated for the new landing check.

**Protected changes, each with a reason**, are in `evidence/stage8-2026-10-25/protected-source-review.json`:
- `FingerPoseDriver`
- `ApartmentExperience`
- `ApartmentAcceptance`
- `RehabVirtualGrasp`

## 10b. Stage 9 deformation and the deformation chart (26 Oct 2026)

Plan tasks 9.1–9.6 (WBS 3.3). The target is your chart "Types and Forms of Deformation". All numbers are **SYNTHETIC**: rendered Windows player on the reference laptop, keyboard-demo force, isolated acceptance storage, no camera, device or person. Probe: `run_contact_probe.py --material-lab`. Data: `evidence/stage9-2026-10-26/material-probe.json`.

**The material values are a game-scale model**, tuned so a 0–45 N grip shows each material's character. They are not laboratory constants, and the deformation is not a finite-element simulation.

### 9.1 Audit of the existing deformer (D-01)

| Part | Decision | Why |
|---|---|---|
| Contact-local dents (`ContactDeformation.Indentation`, probe contacts) | Kept | Bounded, rotation-following, contact-only; 61 tests |
| Damage dwell, impact damage, crack seams, fracture pieces (`FracturePartitioner`, `ObjectFractureVisuals`) | Kept and reused | Brittle materials now break through this path from their own force threshold |
| First-order recovery (`LocalDeformationState.Step`) | Kept for objects without a material | It has no springiness, so a gel cannot wobble, and no yield point (any dent could stay) |
| Per-dent tangent "bulge" | Replaced (with a material) | It was not volume preservation |
| Compression-only response | Extended | The chart also shows tension, bending, shear, torsion and buckling |

### What was built
- **`DeformationMaterial`:** class (rigid, semi-rigid, flexible, soft), result (elastic, plastic, brittle) and these settings:
  - stiffness and maximum dent
  - spring frequency and damping
  - yield point and plasticity
  - Poisson ratio, which sets how much volume is kept
  - smoothing
  - fracture force
  - whole-object form

  It has presets for the chart's 12 materials (steel, glass, concrete, wood, ABS, dense foam, rubber, silicone, fabric, gel, sponge, clay), the form examples, and the station objects.
- **Surface dynamics (`LocalDeformationState.StepDynamic`):**
  - a damped spring per vertex;
  - neighbour smoothing across welded UV seams and poles, so there is no tearing;
  - permanent set past yield;
  - an outward bulge within half a dent radius of each dent, sized by the exact enclosed volume, so rubber, gel and clay keep their volume and a sponge loses it;
  - normals oriented for either triangle winding;
  - no allocation per step.
- **Forms (`DeformationDynamics.Form`):** compression, tension (exact Poisson thinning), bending, shear, torsion and buckling (no buckling below the critical load), driven by grip force. Each form springs, keeps its plastic part and stays fixed at the held end.
- **Material Lab:** a new station with the chart's 12 materials plus a steel strip (torsion), a paper stack (shear) and a plastic straw (buckling), each held in a chart grip (`[` / `]` or the options buttons step through it). The status line names the material, its class and its result.
- **Existing objects:**
  - The Grip Lab props and the six station objects now have chart materials.
  - The station objects keep their authored force and depth calibration.
  - Volume change through heating (chart form 7) is thermal; grip force cannot cause it. It is documented, not modelled. Squeeze volume change is measured instead.

### Material matrix (Material Lab, 18 and 35 N, then release; 3 squeeze cycles at 35 N; 2 s at 45 N)

| Object | Material | Class / result | Dent 18 / 35 N (mm) | Form at 35 N (kept after release) | Volume change at 35 N | Recovery to 10 % | Set after release / after 3 cycles (mm) | Breaks at 45 N | Frame mean / worst (ms) |
|---|---|---|---|---|---|---|---|---|---|
| Steel block | Steel | Rigid / Elastic | 0.0 / 0.0 | — | 0.0 % | instant | 0.0 / 0.0 | no | 2.3 / 3.6 |
| Glass tumbler | Glass | Rigid / Brittle | 0.0 / 0.0 | — | 0.0 % | instant | 0.0 / 0.0 | **yes** | 2.6 / 4.7 |
| Concrete block | Concrete | Rigid / Elastic | 0.0 / 0.0 | — | 0.0 % | instant | 0.0 / 0.0 | no | 2.4 / 4.9 |
| Wooden plank | Wood | Semi-rigid / Plastic | 23.4 / 45.5 | bends 20° (keeps 3°) | 0.0 % | — | 6.0 / 6.0 | no | 4.3 / 11.1 |
| ABS plastic box | ABS plastic | Semi-rigid / Plastic | 4.1 / 8.1 | — | −1.3 % | — | 1.0 / 1.0 | no | 2.5 / 5.2 |
| Dense foam block | Dense foam | Semi-rigid / Elastic | 3.6 / 7.3 | — | −4.5 % | 0.6 s (slow creep) | 0.0 / 0.0 | no | 2.5 / 4.5 |
| Rubber ball | Rubber | Flexible / Elastic | 12.9 / 22.0 | — | −0.3 % | 0.5 s | 0.0 / 0.0 | no | 2.7 / 8.9 |
| Silicone band | Silicone | Flexible / Elastic | 43.2 / 84.0 | stretches 70 % (keeps 0) | −5.8 % | 0.4 s | 0.0 / 0.0 | no | 2.4 / 4.7 |
| Fabric | Fabric | Flexible / Plastic | 11.3 / 8.9 | — | −8.6 % | — | 2.8 / 2.8 | no | 2.9 / 8.1 |
| Gel blob | Gel | Soft / Elastic | 35.5 / 32.0 | — | −0.7 % | 0.8 s (wobbles) | 0.0 / 1.0 | no | 3.0 / 8.3 |
| Sponge | Sponge | Soft / Elastic | 18.0 / 21.8 | squashes 52 % (keeps 0) | −62.4 % | 0.5 s | 0.0 / 0.0 | no | 2.9 / 7.3 |
| Clay | Clay | Soft / Plastic | 18.7 / 18.7 | — | −2.2 % | — | 17.9 / 19.1 | no | 3.0 / 8.0 |
| Steel strip | Steel strip | Semi-rigid / Plastic | 8.7 / 14.5 | twists 126° (keeps 66°) | −2.0 % | — | 8.8 / 8.8 | no | 2.5 / 6.4 |
| Paper stack | Paper stack | Semi-rigid / Plastic | 18.0 / 35.0 | shears 35 mm (keeps 15 mm) | −0.3 % | — | 15.0 / 15.0 | no | 2.5 / 5.9 |
| Plastic straw | Plastic straw | Semi-rigid / Plastic | 15.7 / 35.0 | buckles 34 mm (keeps 8 mm) | −7.2 % | — | 8.1 / 8.1 | no | 2.3 / 4.8 |

**How to read it:**
- The "dent" column is the largest surface displacement, so for the form objects it includes the whole-object form.
- Elastic materials return to 0 mm. Plastic ones keep a set (clay 18–19 mm, ABS 1 mm, wood 3°).
- Brittle glass breaks at 45 N. Rigid steel and concrete do not move.
- Volume:
  - kept: rubber −0.3 %, gel −0.7 %, clay −2.2 %;
  - lost: sponge −62 % (by design: air leaves);
  - partly lost: fabric −9 % and dense foam −4.5 %.
- **Worst frame across all 15 materials:** 11.1 ms (about 90 FPS).
- **By eye (screenshots `evidence/stage9-2026-10-26/play/material-chart-01-08.jpg`, `-09-15.jpg`):**
  - The bend, stretch, squash, buckle and twist are visible.
  - The paper-stack shear is measured but hard to see from the seated view.
  - Your own look in the game is the real check.

**Offline proof (production code, 105 deformation checks, was 61):**
- material classes in chart order;
- spring stability at a 50 ms hitch, gel overshoot, foam creep;
- yield and permanent set;
- seam welding with no tearing;
- sphere volume, both mesh windings;
- volume kept under five fingers (−0.25 %);
- sponge volume lost;
- elastic return within 0.0001 mm and plastic clay kept;
- exact form geometry for all six forms;
- Poisson 0.5 keeps volume when stretched or squashed;
- no buckling below the critical load;
- bad input rejected.

### Performance after Stage 9 (reference laptop, 1920×1080, SYNTHETIC)

| Phase | FPS at p95 (budget 60) | Alloc / frame (budget < 1 KB) | Errors |
|---|---|---|---|
| Menu | 228 | 210 B | 0 |
| Module 1 (held silicone ball, now with the chart material) | 217 | 249 B | 0 |
| Module 2 | 228 | 308 B | 0 |
| Module 3 | 230 | 473 B | 0 |

- **Hand-packet latency:** p50 2.5 ms, p95 5.0 ms. Malformed packets: 30 sent, 9 rejected, 0 errors.
- **Burst:** Burst 1.8.17 is installed but not needed; the plain allocation-free code stays inside budget (D-08).

| ID | Sev | Found | Fix and check |
|---|---|---|---|
| S9-01 | S2 | Unity meshes are wound opposite to the test sphere, so derived normals pointed inward and dents lost 21 % volume in the game | Normals oriented by signed volume; offline test with both windings |
| S9-02 | S3 | First-order dent volume undercounted deep multi-finger dents | Exact enclosed volume, two bulge passes |
| S9-03 | S3 | Bulge spread four vertex rings, which on the coarse 14 cm station ball moved "distant" surface (apartment acceptance failed: 11 of 117) | Bulge limited by surface distance to half a dent radius |
| S9-04 | S4 | 3 cm dent radius on a 3.5 cm ball left no surface to bulge | Fingertip-sized dent radii (1.6–2.5 cm) on small lab props |
| S9-05 | S4 | Twisting a round wire is invisible; plank bending pointed at the camera | Flat steel strip; plank turned to bend sideways |
| S9-06 | S3 | Dense 3,750-vertex strip: 8.8 ms mean, 29.7 ms worst frame | 12 subdivisions: 2.5 / 6.4 ms |

**Station objects (contact probe, solver on):**
- Penetration while holding ≤ 5.7 mm (sponge palm), jitter ≤ 1.6 mm (tomato), 0 drops, 0 furniture overlap.
- 18 chart grips: penetration ≤ 1.0 mm, jitter ≤ 0.4 mm, 0 drops.

**Suites (final build):**
- **Windows player:** smoke / replay / journey 168 / 169 / 74, each with 10 model checks.
- **Other player suites:** UI passed; gallery passed.
- **Unity test runner:** EditMode 8/8.
- **Offline C#:** deformation 105 (was 61), grasp 235, product 76, tracking 177, hand rig 107, pointer 120.
- **Python:** Tools unit tests.

**Protected changes, each with a reason**, are in `evidence/stage9-2026-10-26/protected-source-review.json`:
- `ContactDeformation`
- `SqueezableObject`
- `ApartmentExperience` (status line only)

**Not measured:** a real hand squeezing these objects, any match to real material constants, and any clinical meaning of the dents.

## 10c. Stage 10 six multi-step activities (27 Oct 2026)

**What was built:**
- Six activities, Module 1 tasks 2–7, started from the options panel (ACTIVITY / START ACTIVITY) or with K; N picks the next one in the room, K replays.
- **Living room (seated):** organise the bookshelf, set the coffee table, pack the storage box.
- **Kitchen (standing):** cook a simple meal, cut the vegetables, prepare and serve.
- **How you play:** grab (E or the camera hand), look where it goes, release (Q). The item is put upright on the aimed surface and checked against its target after 0.35 s; `,` and `.` turn it by 15°. Tools act where you look while you squeeze: the knife cuts, the spoon stirs.
- **Props are built in code (D7):** shelf, books, cups, plates, bowl, tray, napkin, box, ball, block, can, sponge, pan, spoon, knife, board, cucumber, tomato, bread and mats.
- **Cutting** is a mesh plane cut (`PlaneCutter`), an approximation of cutting, not a simulation of a blade.
- **Levels 1–10:** position tolerance 6 → 2 cm, angle 35 → 12°, 3 → 5 items, 2 → 4 cuts, stirring 2 → 5 s at 10 → 20 N, chop force 12 → 22 N.
- **Score** = 60 × accuracy + 25 × completion + 15 × handling (errors), with 1–3 stars. Outcomes (position mm, angle °, errors, retries, time and per-activity measures) are saved in the session and appear in the PDF and module dashboards.

**SYNTHETIC player probe** (`Tools/run_activity_probe.py`, keyboard-demo input through the real grab/aim/release path, isolated storage, no camera or person):

| Activity | Level 2 score | Level 8 score | Level 8 detail |
|---|---|---|---|
| Organise the bookshelf | 94 | 88 | 5 books, 8.4 mm position error, all first try |
| Set the coffee table | 97 | 95 | 3.5 mm, 0° angle error |
| Pack the storage box | 100 | 100 | 5 items inside, 0 overlaps |
| Cook a simple meal | 100 | 100 | 4 ingredients, stirred 4.3 s |
| Cut the vegetables | 98 | 97 | 4 cuts, 94 % even |
| Prepare and serve | 99 | 98 | 5 steps, 5.6 mm |

12 of 12 runs completed, 0 errors logged; the probe passed 3 times in a row on the final build.

| ID | Sev | Found | Fix and check |
|---|---|---|---|
| S10-01 | S2 | Round props rolled off the counter before first pickup | Props rest kinematic until picked up |
| S10-02 | S2 | Targets and placements followed the current look, giving 33° angle errors; kitchen props spawned in the living-room direction | Room facing fixed when the activity starts |
| S10-03 | S2 | Cooked food could not be picked out of the pan (the pan blocked the reach ray) | `CanReach` ignores a blocker within 3 cm of the item |
| S10-04 | S2 | The pan sank into the stove while physics settled | Assisted placements stay at rest |
| S10-05 | S3 | Items stacked on each other in the storage box and stuck out over the rim | Free floor spot search inside the box; stacking kept inside the walls; box enlarged to 30 × 22 cm for 5 items |
| S10-06 | S3 | Releasing the knife counted as a miss | Items with no target are ignored |
| S10-07 | S2 | Signed-in users could not start activities (task range 0–1) | Module 1 accepts tasks 0–7 (`MaxTask`), offline product test |
| S10-08 | S3 | 1 in 3 probe runs missed every knife chop | The knife finds the nearest food along the whole look ray, or within 2.5 cm of the look point; re-armed per activity |
| S10-09 | S4 | The activity row covered the Resume button and its label was cut off | Row placed under input/hand, Resume moved down, label fits |

**Suites (final build):**
- **Windows player:** smoke / replay / journey 168 / 169 / 74, each with 10 model checks; UI and gallery (87 checks) passed; contact, grip-lab and material-lab probes passed.
- **Performance probe:** passed (mean 195–310 FPS across scenes, worst p95 frame 8.8 ms).
- **Unity test runner:** EditMode 8/8.
- **Offline C#:** activity and deformation checks, product 77 (with a new activity-session test), grasp 235, tracking 177, keyboard 72.
- **Python:** Tools unit tests 66, ai-engine 32.

**The plan asked for an EditMode completion test per activity.** It is replaced by the player probe, because the activities need the rendered apartment, physics and UI. The pure rules (tuning, placement, book order, evenness, score, steps, plane cut) are covered by offline checks.

**Protected changes, each with a reason**, are in `evidence/stage10-2026-10-27/protected-source-review.json`:
- `ApartmentExperience` (grab/release events, held-tool offset, reach tolerance)
- `CatalogValidation` and `ProductRepository` (Module 1 task range)

**Not measured:**
- your hands-on playtest;
- a real camera hand completing an activity;
- any clinical meaning of the scores.

**Seen but left for Stage 11:** the options-panel section labels touch the room and object buttons. This predates Stage 10.

## 10d. Stage 11 UI modernization and design system (27 Oct 2026)

**Design system:** `Docs/UI_DESIGN_SYSTEM.md`, §Stage 11. Research sources are cited there: Xbox Accessibility Guidelines 101/102, Game Accessibility Guidelines, WCAG 2.2 and Material states.

**What was built:**
- **Tokens:** spacing, state, focus and error tokens, plus `GripUiContrast` (the WCAG formula).
- **Contrast-aware focus ring:** the selected marker bar means selection is no longer shown by colour alone; there is also an error flash, and labels on filled controls are bold.
- **Pause panel:** four numbered sections, with one-press activity selection (three per room).
- **Themed activity panel:** follows the theme's colours, font and text size.
- **Onboarding:** five steps, each completed by a real action; it never blocks play.
- **Input-mode strip:** shows input and pointer live; gaze, voice and assistant read OFF until Stages 13–15.
- **4:3 fit:** `GripUiAspectFit`.
- **Appearance modal:** the current theme is labelled, and the text-size note no longer collides with the buttons.

**Measured before → after:**

| Check | Before | After |
|---|---|---|
| Focus ring on the selected cyan button (Dark) | 1.0:1 (invisible) | ≥ 7.4:1 on every panel control |
| Focus ring on white buttons (High Contrast) | 1.1:1 (invisible) | ≥ 19.6:1 |
| Focus ring, worst panel control (Light) | 1.2:1 (teal on teal) | 4.7:1 |
| Options-panel overlaps | Labels on the room/object rows, clipped chips | 0 at 4 resolutions |
| HUD at 1024×768 | Breadcrumb, MAIN MENU and force card cut off | All text and buttons on screen |
| Selection cue | Fill colour only | Fill + marker bar (≥ 4.5:1) |

The "before" ratios are computed from the theme colours with the same formula; the "after" ratios are measured in the running player.

**SYNTHETIC UI checks** (`run_activity_probe.py --ui-only`, 1024×768, 1280×720, 1280×800, 1600×900): 11 checks per resolution, all passed:
- no overlaps; panel and HUD on screen; the strip covers no text;
- focus ring ≥ 3:1 in 3 themes; selected marker;
- strip states;
- onboarding advanced 1, 2, 3, 4, then done.

`run_ui_checks.py` now covers the same four resolutions: 60 checks each, passed.

| ID | Sev | Found | Fix and check |
|---|---|---|---|
| S11-01 | S2 | Focus ring invisible on primary (Dark, Light) and on white (High Contrast) controls | Colour picked per control for ≥ 3:1; measured in the player and in `test_ui_contrast.py` |
| S11-02 | S3 | Options panel: section labels on top of rows, clipped room chips | `ApartmentOptionsLayout` grid; overlap check |
| S11-03 | S3 | 4:3: the 16:9 HUD was cut off at both sides | Canvas matches width below 3:2; on-screen check |
| S11-04 | S4 | Thin labels on filled buttons read weakly (text colour itself 9.5–11.5:1) | Bold on filled controls and options |
| S11-05 | S4 | Appearance: note squeezed against the buttons, bare theme name floating | Moved; "CURRENT THEME: …" |
| S11-06 | S3 | First strip placement covered the breadcrumb at 4:3 and 16:10 | Strip measured above the bottom cards; "covers no HUD text" check |

**Open (pre-existing, intermittent):** A-OB1. The level-8 "Cook a simple meal" probe run failed once in 3 full runs: 3 failed grabs and 2 misses. It passed in 2 reruns and in all four Stage 10 runs. No cause is identified yet; for the next activity work.

**Suites (final build):**
- **Windows player:** smoke / replay / journey, UI (4 resolutions), gallery, perf, contact, grip-lab and material-lab all pass.
- **Perf:** mean 192–267 FPS; worst p95 frame 8.1 ms.
- **Activity probe:** 12/12 in 2 of 3 runs (see A-OB1).
- **Unity test runner:** EditMode 8/8.
- **Offline C#:** all 17 checks pass.
- **Python:** Tools unit tests 70 (4 new contrast tests); ai-engine 32.

**Protected files:** none changed in Stage 11, so the hash test passes without a new review link.

**Not measured:**
- your by-eye review;
- gaze and voice input (not built yet);
- real hand-pointer use on the new controls;
- an Arabic/RTL layout.

## 11. Severity summary and Stage 2 entry list (Stage 1)

- **S1 (2):** A-002 profile rollback on transient read error · P-012 Sequence Memory pattern never shown.
- **S2 (12):** A-001, A-003 (my Stage 0 regression), A-004, A-005, A-006, A-019, P-004, P-007, P-015 (with A-018), P-018, P-019, P-023. A-020 (S3) is fixed together with A-002.
- **S3/S4:** remaining items, owned by Stages 3–9 as listed.

Recommended Stage 2 order: A-003 → A-002/A-020 → P-012 → A-001 → P-023 → P-007 → A-018/P-015 → P-004/P-001 → P-018/P-019 → A-004/A-005/A-006 → A-014 (Grasp DB) → A-012 (test assemblies) → A-017 cleanup.
