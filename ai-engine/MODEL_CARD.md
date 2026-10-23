# Model card - public effort prototype

**PROTOTYPE — requires independently labelled device data and validation before human use.**

Intended use: local engineering of data, inference and safety integration. Not for diagnosis, treatment decisions, fatigue screening or validated HR/HRV monitoring. Unity explicitly blocks this EMG model on real measured force.

Final experiment: 15 healthy-subject licensed elbow-sEMG development data, RMS effort proxy, weak early/late-task targets, no patient or load-cell force. Subject split: 9 train, 3 validation, 3 untouched test (seed 20261022). Final dataset has **931 windows**, class counts [390, 541]. Public windows start at >=10 seconds, after the early-reference interval exists; the earlier 1021-window experiment is superseded. Training normalization and each CV fold use train subjects only. Neural seeds: 11, 23, 37, 51, 71. Calibration and checkpoint selection use validation data only.

|Model|Test weak-target recall mean, 5 seeds|Test macro-F1 mean|ONNX parity max abs error|ORT CPU p95 ms|
|---|---:|---:|---:|---:|
|GRU|0.4944|0.6267|7.45e-08|0.1445|
|LSTM|0.5111|0.6284|1.19e-07|0.1406|
|CNN|0.5241|0.6245|8.2e-08|0.0807|

The requested recall >=0.85 was **not met**. These are weak-target discrimination scores, not biological fatigue accuracy. Logistic regression weak-target recall is 0.7963, macro-F1 0.7748; it is not valid to select a replacement using held-out-test performance. Small N, elbow/hand mismatch, unknown fatigue labels and healthy/patient domain shift remain unresolved.

Deployment graph: fixed [1, 50, 8] float32 input, opset 15, CNN with calibration temperature. GRU/LSTM are both trained and exported as research artifacts. Sentis GRU operator is unsupported; CNN is the tested runtime fallback. Inputs and scaler provenance must match. CNN SHA256: `7ccc6831cc9ae9affb1e0b730447b299d68aff25bcdfb2baebf545db10e744bc`.

Actual Unity 2022.3.20f1/Sentis 2.0.0 CPU execution: 186 assertions passed; feature max error 1.12e-07; ONNX output difference 7.45e-09; p50 0.2938 ms, p95 0.4280 ms. This measures isolated inference, not full-game FPS or camera latency.

Own-data experiments require an explicit measured pre-task MVC, source/calibration provenance and independent labels. `reports/SYNTHETIC_finetune/from_scratch` and `fine_tuned` contain separate dry-run checkpoints; `comparison.json` and `SYNTHETIC_learning_curve.json` validate plumbing only. They are never device-validation evidence.

See `reports/training.json`, `reports/ablations.json`, `reports/split-audit.json` and `DATA_CARD.md`. No clinical population confidence interval can be inferred from three proxy test subjects. Face classes and camera pulse estimates do not establish true fatigue.
