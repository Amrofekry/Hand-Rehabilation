# Architecture (short)

## Layers
1. **Input:** keyboard/mouse demo force, BLE force sensor (6.1), camera hand frames over loopback UDP (3.2), hand pointer dwell.
2. **Interaction:** `ApartmentExperience`, which handles grab/hold/release, reach and presentation. Below it sit the grasp policy, the contact rig, the deformation (`ContactDeformation`, `SqueezableObject`, material profiles) and the Stage 10 activities (`ActivityRunner`, `Activities`, `ActivityProps`).
3. **Game modules (3.4):** exercises, `ExerciseTuning` levels, the research flow (`ModuleStarted`, `ExerciseCompleted`).
4. **Product:** accounts, sessions with outcomes, repository with backups, PDF/CSV export, and the patient and doctor dashboards.
5. **AI (ai-engine):** adaptation model (ONNX), safety policy.

## Rules
- Keep pure logic Unity-free (`*Core.cs`, `PlaneCutter`, policies). The offline harness compiles it with `TrackingMathShim.cs`.
- MonoBehaviours stay thin and call into the pure rules.
- Per-frame code makes no allocations. It uses NonAlloc physics and cached components.
- Probes and diagnostics are opt-in by command-line flag and write JSON under `Logs/`, which is copied to `Docs/`.
- Protected files are hash-pinned (`Tools/test_handmate_design.py`). Each change gets a stage review link with a reason.
- Vendor code is read-only.

## Known structure debts
- Game code lives mostly in Assembly-CSharp; there are no Rehab asmdefs. Compile time and coupling are not enforced.
- Several apartment files use a dense one-line style. Keep it when editing them.
