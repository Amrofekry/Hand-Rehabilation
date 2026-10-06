# Graduation item 2: integrated local patient journey

Updated 2026-10-06. WBS: 3.4, 5.1, 5.2 (requirements mapping 1.1).
State: **SOURCE IMPLEMENTED; NATIVE ACCEPTANCE PENDING**.

## Current routes

- Main menu: Sign In, Create Patient Account, explicit first local Doctor Setup, Guest Practice, Appearance, How To Play and Quit confirmation.
- Patient: create an account with a PBKDF2 verifier, complete the existing hand-profile form, open the dashboard and Prepare To Train.
- Preparation: choose Camera (visual closure), Keyboard Demo (synthetic force), or Phase 2 (calibrated measured force). No automatic source fallback is added.
- Optional baseline: explicit consent, cancel/decline, waiting/invalid-input state, then a saved summary. This reuses the existing 12-second quick research trial. It is **not** the staged maximum-force/endurance/ROM baseline in graduation item 3.
- Choose a module: all three existing module demos remain accessible. This item connects their session/result navigation; it does not claim the richer task specifications in items 6-8 are finished.
- Results: completed/interrupted/safety-stopped records keep ownership, source and synthetic provenance. Returning home, signing out or leaving destroys the current attempt safely; a save failure shows Retry before account/session switching.
- Continue: available only for the signed-in patient's linked profile with a checkpoint. Module 1 restores room, object, side and explicitly chosen source. Modules 2/3 restore the task selection and start a fresh round. Previously unfinished scores are not recreated.
- Doctor: the local role opens its existing research workspace and can explicitly assign an existing legacy hand profile to a patient. It cannot start an owned patient session or create a patient profile through hidden legacy buttons. A doctor can still deliberately choose unowned Guest Practice. Local role selection does not verify professional credentials.

## Preservation and lifecycle

The authored main scene, serialized profile/module panels, imported VirtualGrasp package, input protocol and assets are retained. One runtime host and one theme-driven view compose the account routes; there is no scene generation on import, verification or build. Existing button subscriptions continue to unbind on disable.

Real profiles remain in the existing DPAPI-protected `GripRehab/profiles.dat` store under Unity's persistent-data directory. Account/session records use `research-accounts.dat` in the same directory with the existing protected backup/read-back behavior. No real records were copied into the repository or loaded by tests. Legacy profiles are not silently assigned to a new account: a local doctor explicitly links the correct preserved profile, after which its Continue checkpoint is available. Older exercise results remain identified as legacy data whose source was not recorded.

At patient sign-in, saved InProgress attempts from an earlier process become Interrupted in an owner-scoped transaction. Another patient's records and terminal results are untouched. Failed recovery preserves the saved snapshot; retry is idempotent. Failed account-linking keeps the already-created profile and offers Retry Profile Link rather than creating duplicates.

## File map

| File | Responsibility |
| --- | --- |
| `Assets/Rehab/3.4-game-modules/RehabAppShell.cs` | Activates one portal; preserves authored panels, profile form and checkpoint navigation |
| `Assets/ProductIntegration/ResearchPortalHost.cs` | Account routes, preparation, consent, profile linking and role guards |
| `Assets/ProductIntegration/ResearchSessionRuntime.cs` | Session ownership, results, interruption, safe save retry and existing research sampling |
| `Assets/ProductIntegration/ResearchPortalPages.cs` | Reused local dashboard/history/doctor/profile-assignment pages |
| `Assets/Product/ProductRepository.cs` | Transactional owner-scoped crash recovery and profile-linked session requirement |
| `Assets/UI/ResearchPortalView.cs` | Themed English scrollable account pages, readable input controls and keyboard focus |
| `Assets/ProductIntegration/ResearchJourneyAcceptance.cs` | Opt-in native UI route fixture using fabricated isolated records |
| `Assets/Editor/ResearchJourneyQA.cs` | Explicit three-cycle Play/Stop journey controller and socket cleanup checks |
| `Tools/Offline/ProductTests.cs` | Account permissions, storage faults, recovery and existing domain regressions |

## Actual verification

- File-only Roslyn compilation against the project's cached Unity 6000.4.4f1 references: five Editor assemblies and four player assemblies pass. This does not confirm a fresh Unity import or native build.
- 40 managed product tests pass, including three added recovery/profile-link tests. DPAPI roundtrip/tamper, ownership, transaction failures, baseline source and terminal-state contracts are covered by the existing and new fixtures.
- Existing managed tracking, keyboard, grasp, deformation, SDK-adapter and palm-pointer suites pass. These use managed doubles where documented; they do not run native PhysX, VirtualGrasp or webcam inference.
- 69 Python tests pass: 61 root Tools tests, four vision tests and four Phase 2 bridge tests.
- The original HandMate immutable design baseline remains unchanged. Its protected-source check initially flagged six intentional integration edits. A separate narrow review pins their original and current hashes with reasons; all other protected source, source artwork and scene logic remain checked against the original baseline. See `evidence/journey-item2-2026-10-06/protected-source-review.json`.
- During test preparation a new Editor script initially collided with the project's `SessionState` type. It was corrected to `UnityEditor.SessionState`; the subsequent Editor/player compilation passes.

Current detailed results/hashes: `evidence/files-only-checks.json` and `evidence/journey-item2-2026-10-06/verification.json`.

## Native gate not performed

The launched Editor reached **Unity Editor Software Terms** before loading the current project. The user is unavailable to review that dialog and explicitly requested continuing file tests and recording the limitation. No terms were accepted, no queued request or automatic Play was left, and no current native result/build is claimed. The existing Windows executable belongs to the earlier repair release; its historical test results do not validate this new journey.

After the user handles the dialog, open the saved main scene, let import/compilation finish and invoke **Grip Rehab > Run Account Journey Tests (3 cycles)**. The prepared fixture uses a fresh `GripRehab-Journey-<guid>` directory under Unity temporary cache for every cycle, never real accounts. It traverses guest practice, registration/profile, consent/decline/quick baseline, Modules 1-3, results, interruption/Continue, doctor access/legacy assignment, sign-out/sign-in and repeated navigation. It records `Logs/journey-cycle-1.json` through `-3.json`, `journey-editor-qa.txt` and gate status; UDP 27817/27818/27819 must be released after each stop.

Build a fresh Windows player using the existing non-regenerating build command, then run it with `-apartment-journey` from the project root. Read `Logs/journey-acceptance.json` and exit status, then exercise the normal menu and inspect layouts at the target resolutions. Do not run the old executable as evidence of this feature or replace its DLLs with offline compiler output.

Pending: native journey passes, current build/player pass, visual inspection of the new routes and preservation during save/reopen/build. The user's live hand and physical-device trials remain separate. Item 2 cannot be checked DONE until its native gate is met. Next implementation is item 3; voice, Arabic, CAD and hardware changes remain excluded.
