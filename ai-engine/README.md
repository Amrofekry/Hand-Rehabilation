# GripRehab fatigue AI and dataset pipeline (WBS 4.1-4.5)
**RESEARCH PROTOTYPE. Public EMG data are not this device.** The delivered public models learn weak early/late-task labels from an elbow sEMG dataset. They are **not validated fatigue detectors** and must not control human treatment. Unity rejects these weights on measured hardware force. Model inference is available only in explicitly labelled synthetic demonstration mode and native acceptance tests. The ordinary camera/force game uses the named statistical fallback.

## Reproduce from the repository root
```powershell
.\ai-engine\Run-AI.cmd test
.\ai-engine\Run-AI.cmd all
```
Alternatively, run the explicit Python commands below in your existing shell.
```powershell
$env:PYTHONPATH = "$PWD\ai-engine\src"
.\ai-engine\.venv-ai\Scripts\python.exe -m pytest ai-engine\tests -q --basetemp Logs\ai-pytest-reproduce
.\ai-engine\.venv-ai\Scripts\python.exe -m fatigue_ai.train --root ai-engine
.\ai-engine\.venv-ai\Scripts\python.exe ai-engine\fine_tune.py ai-engine\data_own\SYNTHETIC_device.csv --epochs 3
```
On a fresh machine, create `ai-engine/.venv-ai` with Python 3.10+ and install `requirements-lock.txt`. `python ai-engine/run.py setup` performs this using the existing camera Python environment as the bootstrap. Hardware, CAD, existing firmware and BLE v1 remain unchanged.

## Unity
Unity **2022.3.20f1**, Sentis **2.0.0**, CPU backend. The initial 2.1.3 dependency attempt exposed stale NUnit references; a clean native-resolved 2.0.0 graph is used. Package changes were made through Unity's Client API, with verified official local archives when network certificates blocked UPM. No TLS verification was disabled. `Tools/AI/Unity2022Packages` plus the Burst/Cecil archives are reproducibility inputs; the path is currently the user's canonical E:/GP/codex/GripRehab-Unity.

Patient: Prepare to train -> COLLECT BASELINE -> explicit consent -> three grip/rest trials -> motion -> endurance -> quiet stability -> saved summary. Camera baseline contains visual motion only. All stages count fresh unique samples; a gap, source/shape/calibration change cannot earn time. The measured grip reference is an experimental reference, not a clinical MVC assessment.

Prepare to train -> OPTIONAL FACE / PULSE SENSING: separate per-app-session opt-ins. Both default OFF and expire when the session process exits. Revoke from the same screen. Existing MediaPipe capture is reused on a bounded latest-frame worker; no second camera, raw frames or facial identity records. Face emotion is seven-class pretrained OpenCV inference, not pain or fatigue ground truth. Pulse is original POS-style processing, quality gated. HRV is withheld until >=60s and high quality. Real accuracy and skin-tone/lighting robustness remain unverified.

Guidance is OFF by default. When enabled: 1Hz decisions, three consecutive high windows per independent modality, hysteresis, explicit-resume warning for one sign or missing required input, latched session stop for two independent signs. Four modalities do not mean four force features. Optional unavailable/low-quality modalities are excluded. User stop always ends the session; no automatic restart. Decisions and reasons are retained in protected session records; added baseline fields preserve old records.

## Map
- `src/fatigue_ai/data.py`: checksummed acquisition, explicit units, CSV/MAT/XDF adapters, causal features.
- `labels.py`: objective force/MVC > Borg > time-only weak prior. Unknown labels remain unknown.
- `train.py`, `models.py`: rules/logistic/boosting/GRU/LSTM/CNN, train-only normalization, grouped evaluation, five seeds, calibration and ablations.
- `policy.py`: deterministic four-category reference; replay matches C#.
- `physiology.py`, `camera.py`: local consent-gated expression/rPPG; no model/code copied from GPL pyVHR.
- `collect.py`: existing Phase-2 envelope -> pseudonymous CSV, rejects age/invalid calibration/duplicates/identity changes.
- `fine_tune.py`, `learning_curve.py`: own-data labels required, synthetic dry-run isolated from public-model files.
- `Assets/Product/{StagedCapabilityTrial,TemporalForceWindow,FatigueSafetyPolicy}.cs`: deterministic research domain.
- `Assets/ProductIntegration/Research{AiRuntime,AuxiliaryReceiver,Sensing}.cs`: adapters and live UI integration.
- `Assets/Resources/ResearchAi`: fixed `[1,50,8]` ONNX CNN, scaler, acceptance fixtures.
- `Assets/Editor/FatigueAiVerification.cs`: actual native Sentis/feature/replay/baseline validation.

## Gates and evidence
See `reports/FINAL_REPORT.md`, `DATA_CARD.md`, `MODEL_CARD.md`, `RISKS.md`, `DECISIONS.md`, `tests/ACCEPTANCE.md`. Do not replace missing physical, physiological or participant validation with synthetic success. Synthetic logs/plots are engineering checks only; no claimed clinical recall or device reliability follows.

The direct Python/CMD entry point was used because this machine blocks PowerShell scripts. No execution policy or Windows security setting was changed. The optional PowerShell script is equivalent but not required.

## Validate the delivered player and camera environment
```powershell
.\ai-engine\.venv-ai\Scripts\python.exe Tools\run_ai_player_checks.py smoke
.\ai-engine\.venv-ai\Scripts\python.exe Tools\run_ai_player_checks.py replay
.\ai-engine\.venv-ai\Scripts\python.exe Tools\run_ai_player_checks.py journey
.\ai-engine\.venv-ai\Scripts\python.exe Tools\run_ai_player_checks.py rendered
.\.venv\Scripts\python.exe Tools\check_research_camera_models.py
```
Keep the rendered player in the foreground for its existing hand-pointer tests. The camera-model check uses blank/synthetic inputs and never opens a webcam. Player acceptance uses fabricated accounts and temporary storage. Packaged-model checks also exercise the derived face/pulse UDP receiver, reject inconsistent validity flags, and confirm that disposal releases its port.

Tunable research guidance lives in `Assets/Resources/ResearchGuidance.asset`. Guidance and the public-model synthetic demo default OFF; optional expression/pulse consent is separate and revocable. A missing input pauses comparison; a genuinely fresh change of source/shape/calibration requires a matching new baseline. Human/device/physiology validation remains separate from these software checks.
