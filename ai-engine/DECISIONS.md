# Engineering decisions
|Decision|Reason|Alternatives / risk|
|---|---|---|
|Retain existing Unity scene/hand/BLE v1|AI work must not repeat interaction regressions|No environment, deformation or tracking coordinate changes|
|Use public EMG as PROTOTYPE-PROXY fallback|Oxford requires authenticated access; licensed real EMG is downloadable|Time labels are weak; cannot establish actual fatigue accuracy|
|Block proxy ONNX on real force|Different sensing domain and units|Real device-trained models need separate approved promotion|
|Keep three temporal architectures, deploy CNN demo|Sentis GRU operator unsupported; fixed CNN graph is small|LSTM exported/parity-tested in ORT; direct runtime support not assumed|
|Sentis 2.0.0 on Unity2022.3.20f1|2.1.3 dependencies exposed incompatible cached NUnit layout; compatible native-resolved package set used|Native rebuild required; old generated cache preserved under Logs|
|No TLS bypass|Official archives verified, process-scoped existing Windows root certificates used|No global trust/settings/antivirus changes|
|Four independent safety categories|Two force-derived features do not constitute two physiological signs|Face/HR are confounded estimates; OFF by default and opt-in|
|Reuse hand capture with latest-frame worker|No competing camera capture or inference backlog|Optional face processing still consumes CPU; real capture latency needs live measurement|
|Additive profile fields|Preserve existing encrypted user records|Legacy 12s baselines remain readable; new staged baseline explicitly versioned|
|Clinical/device validation remains incomplete|No load cell or approved participants/data available|Never turn a synthetic PASS into a clinical claim|

Baseline-time audit: public-data predictions before10s were removed because the early RMS normalization reference uses2..10s. The pipeline was rerun using the same predeclared subject split and validation-only selection. This correction was based on timestamp causality, not test performance. Earlier metrics are superseded.
