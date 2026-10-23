# Acceptance contract
- Dataset checksum matches publisher; unknown license or unit fails closed.
- Monotonic per-trial time, finite nonnegative force, no stale/calibration/identity mixing; synthetic naming enforced.
- Subject-disjoint splits before fitting any scaler; grouped fold scalers train-only; test never used for selection.
- Rules, logistic/boosting, GRU/LSTM/CNN trained with saved seeds; label weakness and source recorded.
- ONNX checker and PyTorch/ORT max absolute error <1e-4; fixed1x50x8 shape; CPU latency measured.
- Native Sentis CNN vs ORT <1e-4 and Python/C# causal-feature + safety replay parity.
- Baseline valid unique samples only, three grip/rest trials, motion, endurance and quiet stability, additive protected persistence.
- Unavailable real-force weights cannot silently use EMG proxy; model demo requires synthetic opt-in.
- Three high windows for one category warn; two categories latch stop; clear windows + explicit action needed to resume; stale/NaN/model failure cannot increase level.
- Camera expression/pulse individually OFF by default; consent expires and revokes; same capture/latest frame only; no raw image storage; low signal quality excluded.
- Synthetic pulse frequency and motion/dropout rejection tests pass; these are NOT real HR/HRV accuracy tests.
- Three Play/Stop cycles and current account/Module1 regressions; Windows build if supported locally; report each actual outcome.
- Human camera, reference pulse/ECG and Nano/HX711 validation remain separate external evidence, never inferred from fixtures.

- Packaged Windows CNN parity/real-force guard/opt-in guard; smoke, replay, rendered and isolated account/baseline journey checks.
- Own-data fine-tuning initializes each GRU/LSTM/CNN from its matching checkpoint; synthetic comparisons stay isolated.
