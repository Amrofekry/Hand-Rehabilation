# M0 baseline audit

Date: 2026-09-28. Canonical product directory: `E:\GP\codex\GripRehab-Unity` (an independent nested Git repository). Parent `E:\GP\codex` holds the original Phase 1/2 files and separate Git history. Neither was deleted.

| Area | Observed | Disposition |
| --- | --- | --- |
| Working tree | 29 previously modified tracked paths, including 6 C# files, tests/docs and screenshots. `git diff --check` passed. | Preserve; hash and text-patch snapshot in `evidence/`. No reset. |
| Unity | `6000.4.4f1` project, URP `17.0.1`, uGUI `2.0.0`, Test Framework `1.4.2`; one editable saved main scene. | Verify current Editor/build before M1 closure. Do not hand-edit vendor package. |
| Vendor hand | `Assets/com.gleechi.unity.virtualgrasp` imported, with its own assembly definitions and samples. | Keep isolated and audit license/provenance before redistribution. |
| Force/vision | UDP 27818 force, 27817 landmarks, 27819 preview; Phase 2 BLE v1 bridge already copied in `Tools/Phase2Bridge`. | Preserve packet compatibility, add explicit source contracts and replay. |
| Existing profile | `RehabProfileStore` writes schema-1 plaintext `profiles.json` and `.bak` into local application data; no password. | M2 migration must be transactional and recovery tested before any plaintext removal. |
| Module status | Module 1 interactive. Module 2/3 and doctor screens are previews. `RehabApp.cs` contains older exercise routines, but they do not establish current product-flow acceptance. | Build actual journeys and metrics before calling GP2 complete. |
| AI packages | No Sentis or localization UPM dependency in current manifest. Parent `ai-engine` directories are mostly specifications/placeholders. | Pin compatible packages only after a minimal compile/probe. No model accuracy claim. |
| Hardware | Parent has firmware and CAD sources, and a modified CAD file. No actual Nano/HX711 was available in earlier QA. | Copy sources with provenance; retain H tests as pending. |
| Tests | Previous `QA.md` reports GUI Editor and Windows player software passes, with synthetic bridge fixtures. | Re-run against new snapshot and record logs. |
| Git metadata | Branch and commit operations fail to create `.git/index.lock`; an attempted new `codex/full-product` branch could not create its ref. | Track as blocker; do not claim a commit or rewrite Git metadata manually. |

The source diagrams describe capabilities; they are not evidence that those capabilities already run. A screenshot, synthetic replay or blank-frame MediaPipe smoke test does not establish human-hand accuracy, real grip force or clinical safety.
