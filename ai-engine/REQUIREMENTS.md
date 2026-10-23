# AI requirement-to-evidence map
The source Gantt WBS and dates are retained unchanged. Current source diagrams identify baseline -> multimodal sensing -> temporal comparison -> adaptive warning/stop -> saved results. Module2/3 control and object/hand geometry are outside this change.

|Requirement|WBS|Implementation/evidence|External boundary|
|---|---|---|---|
|Source/consent/unit-specific baseline|4.1|StagedCapabilityTrial; native acceptance; additive CapabilityProfile|Not validated clinical MVC/ROM protocol|
|Force/time/strain collection|4.2|Existing BLE-v1 bridge, collect/live_logger, unified CSV, schema tests|No Nano/HX711 physically present|
|Dataset acquisition/license|4.2|sources, acquisition JSONL, publisher checksum, data card|Oxford requires login; no account submitted|
|Subject grouping and normalization|4.2/4.3|Split audit, per-fold train-only scalers, tests|No device participants|
|Labels and sensitivity|4.3|labels.py, label-sensitivity JSON, explicit weak/public labels|Independent fatigue truth absent for EMG experiment|
|GRU/LSTM + alternatives|4.3|Five seeds, classical/rule baselines, calibration, ablations, held-out test|Target recall not met; no fatigue accuracy claimed|
|ONNX parity and Unity inference|4.4|PyTorch/ORT export report, native Sentis JSON, fixed CNN fallback|Public weights blocked on real force|
|Four-category fusion|4.5|Python/C# replay, hysteresis, explicit-resume warning, latched stop|Research thresholds, not prescriptions|
|Facial seven emotions|3.2/4.2|Licensed pretrained OpenCV model, worker, separate revocable consent, synthetic execution check|No real expression/fatigue accuracy established|
|rPPG/HRV with quality|4.2/4.5|POS-style algorithm, pulse/motion/dropout tests,60s HRV gate|No reference ECG/pulse validation|
|Session metrics and decision history|4.2/5.1/5.2|Protected additive metrics, per-event version/source, explicit CSV export|No raw video retention; local accounts preserved|
|Own-data readiness|4.2/4.3|Logger dry-run, independently labelled CSV contract, fine-tune/scratch/public comparison, learning curve|Synthetic success is plumbing only|
|Closed-loop and release checks|6.2/6.3|Native game/account QA, model parity, Windows build result in final report|Live hand/hardware/clinical tests remain separate|

## Source conflicts resolved explicitly
- Pipeline names SSD MobileNet/ExpressionNet; a licensed working7-class OpenCV MobileFaceNet/Progressive Teacher substitute is named, without claiming those exact architectures.
- Pipeline names LSTM/GRU; both are trained/exported. Sentis GRU operator is unsupported, so CNN is the tested runtime fallback. A converted graph is never claimed as a GRU operator implementation.
- Master prompt says Unity cannot be run; this workstation has an installed licensed Editor, so actual native tests are performed and supersede that environment assumption.
- Master prompt includes firmware/CAD; prior user scope restricts this work to software/Unity. Existing firmware/protocol are reused without hardware modifications.
- The public source is elbow EMG, not device grip force; `EMG_RMS_PROXY` units and weak labels are mandatory throughout. No Newton conversion or biological fatigue accuracy is fabricated.
