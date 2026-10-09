# Hand tracking, grasp and material response repair — 9 October 2026

Project: `E:\GP\codex\GripRehab-Unity`, Unity **6000.4.4f1**. Scope: WBS **3.2 / 3.3 / 3.4.1**. This review changes tracking stability, grasp/contact and the existing six-object deformation backend. It preserves the authored main scene, imported VirtualGrasp files, interface, modules, accounts and Phase 2 wire protocol. Existing unrelated working-tree changes were left intact. This is a research prototype, not a validated material model or medical device.

## Root causes and repairs

1. **Tracking jitter and delay:** repeated camera reads could queue work behind inference; a single palm-width depth estimate pumped when the silhouette changed; brief loss reset the tracking anchor. A one-frame camera mailbox now drops old captured frames, pose smoothing responds faster to intentional movement, depth uses both palm spans, and brief visual loss retains its reference. Invalid tracking still releases grasp and stops valid exercise input. The existing patient-facing mirror convention is preserved for both hands.
2. **Grip/release jitter:** the application released a grip inside the camera gate's hysteresis band. Holding now follows the gate's latched state. Thumb/index pinch intent is independent of the other fingers. Natural camera acquisition requires the thumb plus another distal finger within 35 mm of the object's surface, and preserves the observed object/hand offset. `G` remains explicit summon assistance; `E` remains explicit keyboard pickup.
3. **Inconsistent surface poses:** held Rigidbody interpolation and constraints ran at different points of the SDK frame. Held placement, constraint resolution, pressure and final refinement now occur in one ordered render transaction after the SDK pose. Fresh source bones are always separate from the constrained output. The player is never moved by this correction.
4. **Contact chatter and clipping:** the original direct VirtualGrasp skin is retained. Finger chains close along a tested arc from a verified open pose; thin cup walls use finer steps. Exact live mesh closest-surface/parity tests constrain the original joints, with bounded wrist clearance and 0.3 mm correction hysteresis. No dynamic hand, springs on the hand, fluid hand or second renderer is introduced.
5. **Jumping dents and lost pressure:** the deformation patch previously jumped to the nearest node. Patches now use the real contact point and surface normal, sharing one total force budget. If a compressed patch retreats beyond the fingertips, a bounded held-object fit can settle toward the visible finger surface at up to 40 mm/s, capped at 25 mm. Pressure is applied only after actual contact; no contact or invalid input means no added pressure. This fit is kinematic assistance, not frictional grasp simulation.
6. **Expensive exact queries:** world geometry/BVH/parity coefficients are cached by geometry revision. Existing Burst compiles the exact query batch. Zero-weight matrix multiplications are skipped; 15,354 native comparisons against the original four-weight skin sum found zero position difference. Every relevant visible skin vertex still sees the full triangle surface; cup cavities are not replaced by convex hulls. Owned native buffers are disposed with the object. A parallel-query candidate did not show a reliable gain and was rejected. An additional native-buffer skinning experiment was removed before delivery because it had not completed native verification.

## Material behavior

| Object | Response |
| --- | --- |
| Silicone ball | Local finger dents with limited bulging, then elastic recovery |
| Porcelain mug | Rigid walls; visible cracks and clipped-surface fragments, no rubber compression |
| Brittle cracker | Small bend, cracks and fracture under sustained excessive input |
| Kitchen sponge | Deep local compression and gradual recovery |
| Silicone cup | Local wall/rim bending; original cavity, UVs, submeshes and materials retained |
| Ripe tomato | Local indentation and accumulated damage with a retained pressure mark |

The baked spring network is capped at **96 nodes per soft object**. Nodes are not created while squeezing. The old dent backend does not also write meshes using this backend. Reset restores deformation, damage, fragments and collision proxies. Camera closure remains a **visual estimate**, never measured Newtons; only the Phase 2 source represents sensor force.

## Evidence and results

The adjacent JSON/text files contain the actual timestamps and individual results. `release-manifest.json` records source and binary hashes. Rejected experiments are retained as history and are not acceptance evidence.

| Check | Evidence | Coverage |
| --- | --- | --- |
| Native Unity compile, references and Windows build | `contact-review-build.log`, `burst-reference.json` | No Missing Scripts; 4,609 compiled/managed exact-query comparisons and 15,354 original/optimized skin comparisons |
| Native surface assertions | `native-surface-tests.json` | 76 assertions: geometry, scale/reflection, cavity and direct-hand architecture |
| Native spring solver | `solver-tests.json` | 78 assertions: pressure response, force budget, topology/seams, invalid force, recovery and reset |
| Production pose/domain source | `offline-tests.json` | Editor/player compilation and 148 pose assertions, including both-hand signs, independent joints, loss/reacquisition and synthetic noise/step response |
| Python | `python-tests.txt` | 68 tests, including latest-camera-frame behavior; no real camera input |
| Windows regression | `player-regression.json` | Normal, Phase 2 replay and rendered 1280×720 runs; controlled UDP acquisition, release, freshness and input/UI regressions |
| Six materials × both hands × three modes | `player-rendered.json`, `images/` | 36 synthetic cases: light/medium/maximum pressure, sustained pressure, fracture, release, drop and reset |
| Moving grasp | `moving-contact.json` | 12 cases with changing wrist translation/roll and pressure; independent managed skin-depth checks |
| Three cold Play/Stop cycles | `scene-play-result.json`, `scene-play-cycles.txt`, `deformation-mesh-cleanup.txt` | Single-instance receivers/camera/SDK and socket cleanup; zero owned deformation meshes and native query batches after Stop |

**Fixture correction:** the old camera approach grid widened only the fabricated palm. Scaling now applies to the complete hand. That grid still did not establish table alignment under the stricter thumb-plus-finger acquisition rule. It was replaced by an explicitly controlled aperture fixture: locate the measured thumb/index aperture, hold the test object there, then send closure through UDP without calling Grab. This verifies acquisition logic for each hand; it **does not** validate mapping a real hand to an authored table. The failed grid is not relabelled as a successful table-pickup test.

**Performance:** measured on the i7-11800H / RTX 3060 Laptop / 16 GB system in `machine.json`. Read the final per-object mean/p95/max in `moving-contact.json`; hollow-object contact remains the most expensive path. The 60 FPS budget is **not met for every moving cup frame**. The fixture-wide FPS in `player-rendered.json` includes periods without difficult contact and is not live-camera gameplay FPS. `solver-tests.json` measures only the warm spring loop: zero managed allocations there does not establish zero allocations across gameplay. Engine shutdown reports font/engine allocations separately; our mesh/query cleanup does not establish an engine-wide zero-leak claim.

## Files and first tuning parameters

| File / component | Purpose / first settings |
| --- | --- |
| `Tools/Vision/mediapipe_bridge.py` | Latest captured frame mailbox; sequential video replay preserved |
| `Apartment/FingerPoseDriver.cs` | Input skeleton; tracking smoothing, translation responsiveness 24, rotation responsiveness 0.045, visual loss hold 0.15 s |
| `3.2-vision/HandTracking.cs` | Validated 21-point geometry and separate pinch intent |
| `Apartment/ApartmentExperience.cs` | Input validity, acquisition hysteresis, natural anchor, ordered contact transaction and stage timings |
| `Apartment/SqueezableObject.cs` | Held/free Rigidbody state, real surface contacts, pressure, damage, fracture and owned query cleanup |
| `HandInteraction/HandSurfaceGuard.cs` | Original visual skeleton constraints; padding 0.5 mm, slop 0.3 mm, ordinary sweep 4°, hollow sweep 0.5°, eight binary steps, maximum wrist correction 150 mm; held fit 25 mm / 40 mm/s |
| `HandInteraction/HandSkinSurface.cs` | Weighted original skin vertices; independent managed reference for acceptance |
| `HandInteraction/MeshContactSurface.cs`, `BurstSurfaceBatch.cs` | Full live triangle BVH/parity, deterministic compiled batch and native buffer lifetime |
| `3.3-deformation/NodeSpringDeformation.cs` | Contact-local bounded spring response with one force budget |
| `Apartment/Profiles/*.asset` | Per-object radius, maximum depth, response/recovery, plasticity and experimental damage thresholds |

Paths above are under `Assets/Rehab/3.4.1-force-object-interaction/` unless specified otherwise. No manual scene migration or imported SDK edit is required. Turn on `HandSurfaceGuard.showGizmos` / the material's contact gizmos for inspection. Do not increase sweep steps or loosen padding to chase FPS without repeating the exact-surface checks: thin cup walls can be crossed between coarse samples.

## Reproduce from the project root

Close the Editor for batch tests; do not forcibly close an unsaved scene.

```powershell
python Tools/run_contact_editor_checks.py
python Tools/run_contact_editor_checks.py --cycles
python Tools/offline_verify.py --compile --tests
python -m unittest discover -s Tools -p 'test_*.py'
python Tools/run_player_checks.py
python Tools/run_contact_checks.py
python Tools/run_deformation_checks.py --rendered --evidence-directory Docs/evidence/interaction-2026-10-09
```

The native Editor command only verifies/opens the authored scene and builds; it does not run scene generation. Player checks run sequentially because their test receivers use the same local UDP ports. Synthetic fixtures are opt-in command-line modes and do not establish physical BLE/webcam acceptance.

## Reference use and licenses

- [Unity-SoftBody-physics](https://github.com/ehsanwwe/Unity-SoftBody-physics), pinned `d9f6d89edcc52517f65d50149e3265b1e30db9e3`, **Apache-2.0**. The existing rewritten surface-node/spring adaptation remains in use. License, unmodified upstream generator and the adaptation notice are in `Docs/ThirdParty/UnitySoftBody/`. This is not its unmodified demo and is not Obi/FEM/XPBD.
- [PhysicsHandsPlayground](https://github.com/ultraleap/PhysicsHandsPlayground), **Apache-2.0**, studied for contact/grab separation. It depends on Ultraleap/native tracking and was not imported as a drop-in webcam hand. The dynamic hand design rejected by the user was not restored.
- [HandTrackingWithUnity-Mediapipe](https://github.com/xinyang-sun/HandTrackingWithUnity-Mediapipe), `Unity` branch reference `6c9a9a2cbecc5bd70f29b45e710cd5cbf2bbd42d`, **MIT**. Studied its 21-point retargeting and object control; its fixed offsets and per-frame logging were not copied.
- The user's already imported VirtualGrasp package remains unchanged. No new third-party hand package or package-version upgrade was made.

## Explicit remaining limits

- Real webcam jitter/latency, occluded fingers, wrist flips, table alignment and both real hands have **not** been validated by these synthetic tests. Monocular depth remains approximate. The measured 80 ms pose response is relative to a received fabricated sample, not capture-to-display camera latency.
- Pinch intent and natural anchor preservation are implemented. A stable physical two-finger/tripod grip on all shapes is **not established**; the moving-contact suite uses synthetic whole-grasp poses. There is no frictional force equilibrium or full skeletal IK guaranteeing exact reconstruction of arbitrary hand gestures.
- Constraints test all relevant visible **vertices** against the object's triangle surface. The measured sub-millimetre residual is not a proof of zero continuous mesh/edge intersection in every pose. A bounded wrist correction can visibly diverge from the target near an oversized object. Cup contact performance still needs improvement before a universal 60 FPS claim.
- Material constants, crack thresholds and tomato damage are presentation parameters, not measured constitutive properties. Free-object collision proxies approximate the deformed mesh; crushing/tearing/fluid contents are not simulated.
- The SDK reports that no baked grasp database is assigned. This integration uses the verified external-controller bone bindings; it does not claim a supplied library of pretrained object grasps. Run the Windows player under this project's `Builds/Windows` folder: the existing launcher locates `Tools/Vision` and its Python runtime through the project. A separate portable installer/runtime bundle was not created in this repair.
- Physical Nano 33 IoT/HX711 input, trained fatigue-model validity and clinical effectiveness remain unverified. No raw camera/audio recording or real user records are included in this evidence.

## Saved work

- `661fa5c` — WBS 3.2: camera mailbox, pose stability, pinch intent and production source tests.
- `cf974ac` — WBS 3.3 / 3.4.1: ordered direct-hand constraints, contact-local material response, exact query backend and verification tools.

Commits preserve the current approved state of these touched core files, including earlier same-scope work that was pending. Other earlier working-tree changes remain outside these commits; a pristine standalone clone of this repair alone is not the delivery target. The canonical local folder and its tested nested Windows build are the delivery artifacts.
