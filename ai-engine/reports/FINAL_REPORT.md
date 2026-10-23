# Fatigue AI and dataset delivery report - 22 October 2026

## Delivery status

**Research software implemented and exercised in Unity 2022 and Windows. Biological fatigue/device validation is incomplete, and the requested recall target failed on the public proxy experiment. This is not a 100% validated fatigue detector or a medical device.**

Canonical home: `E:/GP/codex/GripRehab-Unity`. Unity 2022.3.20f1; Windows x64 Mono; Direct3D 11; Sentis 2.0.0 CPU. Original VirtualGrasp hand and apartment main scene were preserved. No new deformation, hand interaction, CAD, firmware, voice or eye/head work was introduced. Existing unrelated rollback/migration edits were preserved; this report does not claim a clean Git checkout or a new all-project commit.

## What is implemented

|WBS|Delivered software|Remaining acceptance|
|---|---|---|
|4.1|54-second software staged baseline: three 3-second grip trials with 5-second rests, 10-second visual motion, 10-second endurance, 10-second quiet stability. Fresh unique samples only; source/shape/calibration changes invalidate the comparison. Additive protected persistence and baseline summary.|Experimental grip reference, not an approved clinical MVC protocol. Camera values remain closure/visual-flexion estimates; force/endurance are unmeasured without measured force.|
|4.2|Publisher checksum/license acquisition, explicit units/channel audit, CSV/MAT/XDF adapters with explicit mappings, causal filtering/resampling/features, pseudonymous Phase 2 capture and synchronized derived telemetry. 40/40 synthetic live packets accepted.|Oxford authenticated download, real device collection and independent fatigue labels absent.|
|3.2/4.2|Local licensed YuNet detector and seven-class MobileFaceNet expression model on the existing camera worker; independent revocable per-session opt-ins; latest-frame bounded queue; no raw video storage or transmission.|No validated pain/fatigue meaning, expression accuracy or demographic robustness.|
|4.2/4.5|Original POS-style rPPG processing, signal-quality/motion/exposure/gap rejection, >=15 seconds pulse warm-up and >=60 seconds/high-quality HRV gate.|No reference pulse/ECG accuracy, lighting/skin-tone/generalization study.|
|4.3|Rules, logistic regression, gradient boosting, GRU/LSTM/CNN; train-only scaling and grouped CV, five neural seeds, class weighting, train-only augmentation, early stopping and validation calibration/seed selection. Subject-disjoint held-out evaluation, ablations, confusion matrices, calibration plot and event candidates.|Public weak-label recall target >=0.85 not achieved. Actual device/patient fatigue validity remains unknown.|
|4.3|Own-data independently labelled contract; separate scratch/fine-tuned checkpoints for all 3 neural models, public-only comparison and learning curves. Synthetic dry-runs executed without overwriting public model artifacts.|Actual force-labelled participant fine-tuning and prospective evaluation not performed.|
|4.4|Fixed[1, 50, 8] opset 15 ONNX exports for GRU/LSTM/CNN, PyTorch/ORT parity, supported CNN native Sentis/Windows parity and explicit deployment guards.|Sentis does not support the exported GRU operator; CNN is the tested runtime substitute. Public EMG weights are blocked on real hardware force.|
|4.5|Four independent categories, 3consecutive high windows, hysteresis, bounded difficulty/cooldown, explicit-resume warning after one persistent sign, latched stop after two independent signs, invalid-input hold/pause, persisted source/settings/reasons.|All thresholds are versioned research settings, not clinically validated limits.|
|6.2/6.3|Native validation, game/account/baseline lifecycle checks, Windows build and packaged model/game tests; source preservation, documentation and evidence.|Human camera, reference physiological sensing, actual hardware and full-game performance certification remain separate.|

## Actual dataset and model results

Downloaded Zenodo5189275, CC BY4.0, 15 healthy subjects, eight-channel elbow sEMG at200 Hz. ZIP10, 603, 743 bytes; publisher MD5 `a954b4eff5594fd8b1670b1a1d0eb864` matched. This is **not grip-force data**. RMS effort is tagged EMG_RMS_PROXY, with no conversion into Newtons. Early<=30% and late>=70% task-time labels are explicitly weak; ambiguous middle windows are excluded from binary training. The per-window ordinal label file and sensitivity counts preserve that uncertainty.

Final experiment: **931 windows**, binary counts[390, 541], 9 train/3 validation/3 test subjects, split seed20261022. All trials of a person remain in one split. The public early-reference interval2..10s must exist before prediction; windows before10s were excluded. The earlier1021-window experiment was superseded by this causal correction, using the same predeclared subjects/method. No test-set threshold tuning was used to improve results.

|Model|Held-out weak-target recall mean, 5 seeds|Macro-F1 mean|ONNX max error|ORT CPU p95 (ms)|
|---|---:|---:|---:|---:|
|GRU|0.4944|0.6267|7.45e-08|0.1445|
|LSTM|0.5111|0.6284|1.19e-07|0.1406|
|CNN|0.5241|0.6245|8.2e-08|0.0807|

Neural seeds11, 23, 37, 51, 71. These scores measure weak-target discrimination, **not real fatigue accuracy**. The selected runtime CNN seed is 37, selected using validation macro-F1 and operator compatibility. The plot in `PROTOTYPE_PROXY_evaluation.png` now uses that same seed for its held-out confusion matrix and validation calibration. Full per-class precision/recall/F1, ROC/PR-AUC, Brier, temperatures, candidate false-stop/delay results and grouped CV are in `training.json`; validation-only ablations in `ablations.json`. Synthetic comparison scores are not included as real performance results.

Oxford force/EMG/kinematics data require registration; no login was attempted. Mendeley candidates are recorded but not silently pooled. UBFC research-only wording is not treated as unrestricted commercial permission. Licenses/checksums/pretrained upstream commits are in DATA_CARD, sources, acquisition log, pretrained-model manifest and Docs/AI_THIRD_PARTY.md.

## Executed checks

|Check|Outcome|Evidence|
|---|---|---|
|Python AI|PASS 32 tests|`Logs/ai-python-final.log`: causal/data/split/labels/policy/ONNX/rPPG/logger/consent lifecycle.|
|Existing Python regressions|PASS 66 tests|`Logs/ai-existing-python-final.log`, including immutable298-file baseline and the narrow15-path reviewed extension.|
|Native Editor AI|PASS 186 assertions|Python/C# feature error 1.12e-07; Sentis/ORT difference 7.45e-09; isolated CPU p50 0.2938 ms, p95 0.4280 ms.|
|Native lifecycle|PASS|Three original game Play/Stop cycles, then three isolated account/baseline journey cycles; transport cleanup recorded in `editor-qa.txt` and `journey-editor-qa.txt`.|
|Windows build|PASS|build: Windows build completed; UTC 2026-10-22T07:18:57.5986088Z; actual exe and data directory in Builds/Windows.|
|Windows smoke|PASS|168 game/journey checks + 10 packaged-model assertions; ONNX error 7.45e-09|
|Windows replay|PASS|169 game/journey checks + 10 packaged-model assertions; ONNX error 7.45e-09|
|Windows rendered|PASS|168 game/journey checks + 10 packaged-model assertions; ONNX error 7.45e-09|
|Windows journey|PASS|74 game/journey checks + 10 packaged-model assertions; ONNX error 7.45e-09|

All player model checks load the packaged resource, execute the graph, compare against ORT, reject real-force use of proxy weights and require explicit synthetic opt-in. Journey checks use fabricated accounts and separate temporary storage; no real profile was overwritten. The rendered test is1280x720 on an NVIDIA GeForce RTX3060Laptop GPU(5996 MB reported VRAM). Isolated inference timings do not prove60FPS, 0BGC or end-to-end webcam latency. CPU model/RAM CIM queries were restricted; no machine-performance claim is inferred from unavailable fields.

In-app protected attempt records are bounded to 1800 metric rows and 500 policy events. The separate Phase 2 capture logger streams its CSV while recording; it is the collection path for longer approved research captures. These record limits must be considered before designing a longer participant protocol.

## Defects found and fixed

- Initial Sentis 2.1.3 dependency attempt encountered certificate resolution and incompatible stale NUnit references. Repaired through Unity's native PackageManager Client API to compatible Sentis 2.0.0/Collections2.2.1/TestFramework1.1.33/NUnit1.0.6/Burst1.8.17/Mathematics1.2.6/Cecil1.11.4. Original Library preserved under Logs/ai-2026-10-22. No TLS verification or Windows security policy was disabled; a process-local copy of already trusted Windows public roots was used for Node.
- Repeated account journeys exposed exact-string comparison after JSON deserialize/reserialize in the DPAPI save check. Now the parsed catalog is validated, then actual decrypted bytes are compared against the exact original UTF8content; buffers are cleared. Repeated native journeys subsequently passed.
- Public-reference causality and mismatched plot seed were corrected. Current final model/report/Unity resource hashes agree; old results remain superseded.
- Revoking either face or pulse consent clears cached signal history even if the other option stays enabled. Face loss, invalid crops and inference failures also reset pulse history. Results from a previous opt-in or face cannot be reused. Previous-process/future/expired consent is rejected; tests cover these checks without opening a camera.
- Native loopback tests cover the auxiliary face/pulse receiver, malformed/duplicate/negative-sequence/old/future packets, combined measurement/queue age, restart and port disposal. Inconsistent validity flags are rejected before UI expression indexing or pulse guidance, including a face-valid packet with emotion=-1 and a pulse-valid packet with HR=0.
- Missing/default force receiver samples previously looked like a calibration change. Identity comparison is now freshness-gated: loss pauses safely, recovery from the same identity retains its baseline, and a genuinely fresh changed calibration still latches the source fault. Native regression checks cover all three cases.
- The first rendered player attempt failed the existing focus-gated pointer test while unfocused. Re-running with the actual player foreground passed; the focus safeguard and test assertions were retained.
- The stock installed Unity 2022Mono player omitted its original x64 NVUnityPlugin.dll, producing a native startup warning. Including the exact installed, verified PE-x64 runtime removed that warning. An Editor post-build callback preserves this dependency and writes its checksum; no vendor hand code or downloaded replacement DLL is used.
- Headless NullGfxDevice runs can print unsupported-shader messages because no GPU renders there. Visual checks are based on the separately rendered Direct3D 11 run, not those headless logs. PyTorch's generic recurrent-export warning concerns other batch shapes; the validated deployed shape stays fixed[1, 50, 8].
- The existing OpenCV 5camera environment emits a backend-target availability warning while loading the expression model. Actual detector/7-output forward execution and the72 bpm synthetic signal check passed in that same interpreter; the environment was preserved. Camera HRV uses sample-grid peak intervals and is not ECG-grade timing.

## Run and inspect

From the project root:
```powershell
.\ai-engine\Run-AI.cmd test
.\ai-engine\Run-AI.cmd all
.\ai-engine\.venv-ai\Scripts\python.exe ai-engine\fine_tune.py ai-engine\data_own\SYNTHETIC_device.csv --epochs 3
.\ai-engine\.venv-ai\Scripts\python.exe Tools\run_ai_player_checks.py smoke
.\ai-engine\.venv-ai\Scripts\python.exe Tools\run_ai_player_checks.py replay
.\ai-engine\.venv-ai\Scripts\python.exe Tools\run_ai_player_checks.py journey
.\ai-engine\.venv-ai\Scripts\python.exe Tools\run_ai_player_checks.py rendered
```
The rendered player's window must remain foreground. The existing all command actually ran acquisition/unification->training/export->tests; the final expanded suite was then run separately. No PowerShell execution-policy change is needed. See README and requirements-lock for a separate ML environment; existing camera Python was not upgraded. Unity package file references currently use the permanent canonical project path and official local archives; moving the repo requires re-resolving those native package inputs.

In the game: Prepare to train -> COLLECT BASELINE. Optional face and pulse consent are separate and defaultOFF. Research guidance and SYNTHETIC MODEL DEMO are explicit options. Demo weights cannot become hardware weights through UI toggles. Revoke sensing in the same preparation screen. See session CSV for source/quality/consent/derived values and policy decisions; unavailable fields remain unavailable.

## Remaining gates and next10actions (unchanged Gantt)

1. W6(17-23October2026): review these requirements, weak labels, unit contracts, experiment and unresolved acceptance with the supervisor.
2. W7(24-30 October): obtain authorized Oxford data or inspect a licensed force candidate; provide actual channel/calibration maps before using its adapters.
3. W7: approve the human-data protocol, consent/retention/pseudonym handling, independent labels and stop procedures; no application was submitted automatically.
4. W7-8(24 October-6 November): collect approved participant/device force and repeat baselines by hand/shape/calibration; hardware collection depends on actual equipment.
5. W8: synchronize optional camera-derived values and compare pulse/HRV against a reference sensor; assess lighting/motion/skin-tone and expression robustness. No raw video is stored by the ordinary runtime.
6. W9-10(7-20 November): preserve the source Gantt blocked/exam weeks. Do not fabricate collection or change their dates.
7. W11(21-27 November): run independently labelled public-only/scratch/fine-tuned GRU/LSTM/CNN comparisons, grouped validation and learning curves on real device subjects.
8. W12(28 November-4 December): lock thresholds/methods before a new held-out prospective evaluation, then assess recall, actual false stops/missed fatigue and closed-loop behavior. Current0.85 recall gate is unmet.
9. W13-14(5-18 December buffer): fix measured failures; validate real camera latency/physiology/device fault behavior and full-game frame/GC budgets. W15-20 exam blocks stay as recorded.
10. W21-22(30 January-12February2027; W22starts6 February): repeat end-to-end target-device acceptance and package final study/report artifacts. Human/clinical deployment requires its separate validation/approval, never automatic promotion of these proxy weights.

## Sources, backups and delivery map

Read the supplied master prompt/diagrams/Gantt before implementation. Their Desktop paths later disappeared before copying; exact copies of those vanished attachments are not claimed. Existing Docs/Source architecture/Gantt remain present and were used for the retained source map; source WBS/dates were not rewritten. Architecture-named SSDMobileNet/ExpressionNet are explicitly substituted by the licensed working seven-class model; this is not presented as the exact named architecture.

Backup: Logs/ai-2026-10-22/source-before.zip. Older user-requested hand rollback history/backups remain untouched. `Docs/SOFTWARE_TASKS_23.md` is updated with implemented research software versus pending external validation; voice and eye/head extras remain future work. `REQUIREMENTS.md`, DATA_CARD, MODEL_CARD, protocol/OWN_DATA_PROTOCOL, tests/ACCEPTANCE, reports/METHODS, DECISIONS and RISKS explain contracts and boundaries. Sanitized non-secret acceptance copies and delivery hashes are in Docs/evidence/ai-2026-10-22. Raw Unity/license logs and trusted-root files are not copied into public-facing evidence.
