# Camera hand control report (final release, 2026-11-01)

Scope: the camera (CV) virtual hand in the apartment. Every number here is **SYNTHETIC**: 30 fps MediaPipe-shaped frames with Gaussian landmark noise (σ 0.004 of the image, about 2.6 px at 640 px) over the loopback vision port, with no camera and no person. Real-hand feel, latency on the user's webcam, and comfort remain **human checks (OPEN)**.

## 1. Root causes (confirmed in the original code)
| # | Problem | Effect |
|---|---|---|
| R1 | The palm-width depth reference was re-taken after every tracking gap, and depth used only palm width | Depth jumped and drifted, with 17.4 mm rest jitter, nearly all of it on the depth axis |
| R2 | The hold released on any frame with the score under the grab threshold (0.55); there was no hysteresis | A wobbling fist dropped the object |
| R3 | The grip score was four-finger flexion only, with no thumb | A pinch could not grab; a pointing hand (three fingers curled) scored high |
| R4 | There were two camera grab paths (HandLocomotion fist and ApartmentExperience gate) | They competed: double attempts and inconsistent results |
| R5 | The camera grab needed a fingertip within about 9 cm; the keyboard grab uses an aim assist | **Why the keyboard felt more reliable:** the camera could rarely reach activity items |
| R6 | Any tracking gap released the object at once and re-anchored the hand | A blink of occlusion dropped the object |
| R7 | Only one 45 ms exponential smoothing step | Either jitter or lag; no speed-adaptive behaviour |
| R8 | VisionUdpReceiver and ApartmentExperience shared an execution order | The hand could read the previous frame |
| R9 | Camera force was score × 80 N | An ordinary hold was about 60 N, above porcelain's 40 N, so cups broke silently |

## 2. Architecture (hybrid pipeline)
`MediaPipe bridge → VisionUdpReceiver (order −30) → FingerPoseDriver (One Euro position + depth, references kept through short gaps, 0.6 s pose grace) → HandGrasp.Score (max(power, pinch), Point = 0) → CameraGripGate (close .55 / open .28 / dwell .12 s) → GraspHoldRule (hysteresis + 0.6 s loss grace) → ApartmentExperience.TryGrab / Release (same entry points as the keyboard) → HandInteractionMachine (HUD state)`

- **Look and walk:** `HandLocomotionPolicy` (edge band turns the view, a held point walks).
- **Actions:** `HandActionPolicy` (held pinch opens options, twist turns the placement).
- **Rehab metrics stay unfiltered:** ROM and the force estimate read raw frames (`HandKinematics.GripScore`). Only the presented pose is filtered.

## 3. Keyboard integration
Camera, keyboard, mouse and voice call the **same** `ApartmentExperience` methods: `TryGrab` (proximity, then aim via `KeyboardTarget`), `Release`, `RecenterTracking`, and `ActivityRunner.SetPlacementYaw`. The camera fallback to the keyboard aim target is the **aim assist** (on by default). It is counted separately as `AssistedGrabs`, so analysis can tell physical reach from assisted grabs. A separate `RehabControlIntent` layer was **not** added: the shared entry points already give one code path, and an extra layer would mean editing more pinned files for no measured gain.

## 4. Files
| File | Change |
|---|---|
| `Assets/Rehab/3.4.1-.../Apartment/HandRigRules.cs` | `OneEuroFilter` struct (pinned, recorded) |
| `Assets/Rehab/3.2-vision/HandGrasp.cs` | **new:** pinch score, grasp score, camera force curve |
| `Assets/Rehab/3.2-vision/HandLocomotionPolicy.cs` | Wider work zone; `GraspHoldRule`; `HandInteractionMachine`; `HandControlProfile` |
| `.../Apartment/ApartmentExperience.cs` | Hysteresis hold, loss grace, single camera grab path, aim assist, `AssistedGrabs`, force curve, no hand swap while holding (pinned, recorded) |
| `.../Apartment/FingerPoseDriver.cs` | One Euro, two-measure depth, references that survive gaps, pose grace (pinned, recorded) |
| `Assets/Rehab/3.2-vision/VisionUdpReceiver.cs` | `[DefaultExecutionOrder(-30)]` (pinned, recorded) |
| `.../Apartment/HandLocomotion.cs` | Settings statics, HUD interaction state, profile apply on change, single grab path, pinch-vs-options guard |
| `Assets/ProductIntegration/ResearchPortalPages.cs` | Settings: HAND REACH, HAND STEADINESS, AIM ASSIST, RECALIBRATE CAMERA HAND (pinned, recorded) |
| `.../Apartment/RehabHandBenchmark.cs`, `Tools/run_hand_benchmark.py` | **new:** SYNTHETIC benchmark (`-apartment-handbench-probe`) |
| `Tools/Offline/HandLocomotionTests.cs`, `Tools/offline_verify.py` | 53 offline tests (hold rule, state machine, One Euro, profile) |
| `Docs/evidence/final-2026-10-29/*` | Benchmark before/after, protected-source review link |

## 5. Configuration (no code changes needed)
| Setting | Where | Values |
|---|---|---|
| Hand reach (sensitivity) | Settings → HAND REACH | LOW .24 / NORMAL .30 / HIGH .36 of the image |
| Steadiness (One Euro cutoff) | Settings → HAND STEADINESS | RESPONSIVE 2.0 Hz / BALANCED 1.2 / STEADY 0.7 (depth .6 / .3 / .2) |
| Aim assist | Settings → AIM ASSIST | ON (default) / OFF = physical reach only |
| Recalibrate | Settings → RECALIBRATE CAMERA HAND, C key, or voice "recenter hand" | Resets the depth and position references |
| Gate thresholds | `ApartmentSettings.cameraCloseAt/OpenAt` (Inspector) | .55 / .28, dwell .12 s |
| Loss grace | `FingerPoseDriver.lossGraceSeconds`, `GraspHoldRule.LossGraceSeconds` | 0.6 s |
| Look band | `HandLocomotionPolicy.deadZoneX/Y`, `fullAt` | .30 / .26 / .46 |

## 6. Test results (SYNTHETIC; `Tools/run_hand_benchmark.py before|after`)
| Metric (definition) | Original | Final |
|---|---|---|
| Rest jitter: RMS of the hand position about its mean, still hand, 3 s | 17.5 mm | **9.9–10.4 mm** |
| Of which depth axis | 17.4 mm | 9.7–10.2 mm |
| Of which image plane (XY) | 2.4 mm | 2.1–2.2 mm |
| Rest step: mean frame-to-frame movement at rest | 0.95 mm | 0.36–0.4 mm |
| Sweep RMS: error against the noise-free trajectory, repeated horizontal sweep | 17.2 mm | **9.9–11.6 mm** |
| Settle: time to within 5 mm after a step | 62 ms | **20–28 ms** |
| Object held through a 0.7 s tracking gap | no | **yes** |
| Camera grab of an activity item | no | **yes** |
| Pinch grab of a small item | no | **yes** |
| False grab while pointing | no | no |
| Drops during a 3 s noisy hold | 0 | 0 |

Final values are the range over two recorded runs (2026-11-01, before and after the review fixes); the original is one run. The latest file is `Docs/evidence/final-2026-10-29/hand-benchmark-after.json`.

The offline harness shows the old hold rule dropping a noisy hold on many frames, while the new rule drops none, using the same score sequence (`HandLocomotionTests.cs`). One Euro cuts rest variance to under a quarter of raw, with under 2 cm lag at 0.5 m/s.

**Scenario coverage (master prompt §13):**
| # | Scenario | Check |
|---|---|---|
| 1 | Stationary hand | Benchmark rest jitter |
| 2–3 | Slow horizontal / vertical | Benchmark sweep; hand-look probe (edge band) |
| 4 | Forward/back | Benchmark turn-depth (depth stays put while rotating); real depth is a human check |
| 5–6 | Rapid moves, direction changes | Benchmark settle; One Euro lag test |
| 7 | Repeated path | Benchmark sweep (repeated) |
| 8 | Pinch / grasp recognition | Benchmark pinch grab, point false grab; offline score tests |
| 9–10 | Grab, hold, release | Offline hold-rule and state-machine tests; benchmark hold drops |
| 11 | Pickup and placement | Activity probe (6 activities); benchmark activity camera grab |
| 12 | Deformable objects | Contact probes (grip lab, material lab); camera force curve keeps porcelain intact |
| 13–14 | Occlusion, loss and recovery | Benchmark 0.7 s gap; state machine recovery test |
| 15 | Varying frame rates | Filters use real dt (clamped to 0.1 s); offline hitch test. A real low-fps camera is a human check |
| 16 | Prolonged activity | Perf soak probe; state machine never sticks |
| 17 | Keyboard fallback | Activity, smoke and UI probes (keyboard paths unchanged) |
| 18 | Tracking + rendering + physics together | Perf probe (fps p95, allocation per frame) |

**Pipeline latency (MEASURED on this PC, 2026-11-03):** capture to the frame the pose is applied in: **median 49.6 ms, p95 67.2 ms** (436 frames). Bridge (capture to send, mostly MediaPipe inference): median 47 ms, p95 63 ms. Unity (receive to applied): median 3.1 ms, p95 5.7 ms.
- **Input:** real hand photos from a dataset, replayed as a video through the real bridge and port (`-vision-latency`, `Docs/evidence/final-2026-10-29/vision-latency-replay.json`).
- **Why this is conservative:** every image is new, so MediaPipe runs full detection on each one; live tracking is usually faster.
- **Not included:** camera exposure and display time.

**Still not measured:** latency with a live hand on the webcam, and position error against a physical reference. Both need a person and a reference rig, so they stay OPEN.

## 7. Build status
The batch Windows build succeeded. The offline compile and tests pass (53 hand tests), and the Python suite is OK. EditMode, probes and ConsoleZeroCheck results are in `Logs/final-a-regression.txt`, summarised in `Docs/ai/STATE.md`.

## 8. Limitations
- A monocular webcam has no true depth. Depth is a proxy from palm size and is about 4× noisier than XY.
- The aim assist makes grabs easier than physical reach. Analysis must read `AssistedGrabs`.
- Camera force is a **visual estimate**, not a measured force.
- MediaPipe remains the tracker. The YOLO26/RF-DETR hybrid was not trained to completion (optional plan O3).
- Lighting, skin tone, sleeves and camera angle effects are untested (human check).

## 9. Usage
1. Start the game and choose camera mode. The MediaPipe bridge starts with the camera launcher.
2. Hold the hand in the centre and press RECALIBRATE (or C, or say "recenter").
3. **Look:** move to the edge band. **Walk:** hold a point. **Grab:** close the hand. **Release:** open it. **Options:** hold a pinch. **Rotate placement:** twist the hand.
4. To re-run the benchmark: `ai-engine/.venv-ai/Scripts/python.exe Tools/run_hand_benchmark.py <label>`.

## 10. Camera-hand activity run (2026-11-02, SYNTHETIC)
`Tools/run_activity_probe.py --camera` plays all six activities at levels 2 and 8 with camera-hand frames instead of keyboard calls:
- **Grab:** the hand closes until the real gate closes, gently.
- **Place:** the hand opens.
- **Squeeze:** a closed loop on the camera force.
- **Cuts:** the hand relaxes between cuts.

The first run completed **3 of 12** (keyboard: 12 of 12). It exposed five real problems:

| # | Problem (affects) | Fix |
|---|---|---|
| F1 | In a cluster, a fingertip brushing a neighbour grabbed it, not the looked-at object (camera) | With aim assist on, the looked-at object wins; it counts as assisted only if no fingertip reached it |
| F2 | The look target was the first collider the ray met: knife on a plate gave the plate (camera and keyboard E) | Among reachable objects in the aim cone, the one whose centre is nearest the view direction wins |
| F3 | Reach needed a clear line of sight to the single closest point: bread behind a board edge was unreachable (camera and keyboard) | Reachable if the closest point or the top of the object is visible (distance limit unchanged) |
| F4 | The first cut could never arm with a camera grip, and squeezing while looking at the board spent the cut (camera; keyboard for the second) | Picking up the knife arms the first cut; only a squeeze aimed at food uses it |
| F5 | 25 N needed grip score 0.85, more than a real fist gives (benchmark mean 0.77), so level-8 tasks were out of reach (camera) | 25 N at score 0.80; an ordinary hold is still about 18 N; porcelain (40 N) only breaks on a deliberate hard squeeze |

**Result after the fixes: 11 of 12.** Keyboard is still 12 of 12. Evidence: `Docs/evidence/final-2026-10-29/activity-probe-camera.json`.

**Open finding:** level 8 "Pack the storage box" (5 items) with the camera hand. A camera release happens as the hand opens (the gate confirms over about 0.12 s), so the largest item (a 12 cm book) lands about 1.5 cm over the wall, even after two pick-up-and-replace corrections. The keyboard's instant release fits. Next step: snap a camera release to the placement preview the way the keyboard path does, or give the box a wider tolerance at high levels.

## 11. Next improvements (justified by the limits above)
1. Run `-vision-latency` with a live hand on the webcam, and add camera exposure and display time with a photodiode or high-speed video.
2. A depth camera or stereo option to replace the palm-size proxy (optional plan O5).
3. A hybrid detector only if an A/B against MediaPipe on recorded clips shows lower jitter or fewer losses (O3).
4. A per-user automatic reach calibration from a 5 s range-of-motion sweep.
