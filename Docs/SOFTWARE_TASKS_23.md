# Software task list - 22 October 2026

Same 23-task scope. Canonical project E:/GP/codex/GripRehab-Unity; Unity 2022.3.20f1. Status distinguishes implemented software from missing scientific/device validation. Original VirtualGrasp hand and single scene remain.

|No.|Status|Task and remaining acceptance|
|---|---|---|
|1|Completed|Pipeline/Gantt review and software requirements; new AI evidence mapped to WBS 4.1-4.5.|
|2|Partial|Accounts/profile/results/Continue: three native journey cycles passed; whole product scope is not newly certified.|
|3|Implemented; validation pending|54-second staged force/endurance/visual-ROM/stability baseline, freshness and identity checks. Camera never supplies measured Newtons or clinical joint ROM.|
|4|Partial|Existing MediaPipe/gesture path preserved. YOLO and human tracking-quality validation remain separate.|
|5|Partial - earlier prototype preserved|Object grip/basic localized deformation/breakage unchanged by AI work; later soft-body/contact replacements remain withdrawn.|
|6|Partial|Module 1 rounds/results retained and regression checked; physical rehabilitation acceptance remains unverified.|
|7|Partial|Module 2 Cognitive Performance retained; no new module gameplay added by AI work.|
|8|Partial|Module 3 Visuomotor Coordination retained; no new module gameplay added by AI work.|
|9|Implemented; device validation pending|Causal 10 Hz/5 second windows, timestamps, source/calibration, optional derived sensing metrics and explicit CSV export.|
|10|Implemented; accuracy pending|Local seven-class facial model, separate explicit/revocable per-session consent, no raw-frame storage. Not a validated pain/fatigue detector.|
|11|Implemented; accuracy pending|POS-style rPPG, quality rejection and >=60 seconds HRV gate. Synthetic frequency/dropout/motion tests passed; reference-sensor validation absent.|
|12|Partial|Reproducible GRU/LSTM/CNN + classical training/evaluation completed on licensed weak-label elbow-EMG proxy. Device data, independent fatigue truth and target recall remain unmet.|
|13|Implemented for research demo|Three ONNX exports; native Sentis CNN parity/inference passed. GRU operator unsupported in Sentis; public weights blocked on real force.|
|14|Implemented; thresholds experimental|Four independent categories, hysteresis, three-window confirmation, explicit-resume warning, latched two-sign stop, bad-input hold and saved decision version/source.|
|15|Partial|Patient Dashboard/baseline comparison retained; additional source/consent/model metrics persisted. Full dashboard scope not newly completed.|
|16|Partial|Doctor Dashboard/permissions retained; full clinician workflow outside this AI change.|
|17|Partial|Protected local records/recovery/CSV retained; exact-content DPAPI readback fixed and regression checked. Full data-governance/PDF validation remains separate.|
|18|Partial|Phase 2 envelope collection, age/sequence/shape/calibration validation and synthetic logger/replay passed. Physical Nano/HX711 absent.|
|19|Partial|Existing English UI/controls/hand pointer retained; optional sensing and research-demo controls added. Human accessibility acceptance remains separate.|
|20|Partial|AI 32 tests, native 186 assertions, three game and three account/baseline cycles passed. Full-game FPS/GC and human-camera latency are not certified.|
|21|Partial|AI documentation/model/data cards/reproducibility and current Windows build/player evidence in ai-engine/reports/FINAL_REPORT.md. Final graduation evidence still includes unresolved validation.|
|22|Future extra|Voice commands for menus/HUD and player movement; not reintroduced.|
|23|Future extra|Eye/head pointer and movement, eye-only/head-only/fusion, independent toggles and hand/eye-head selection; five-second menu dwell. Not implemented.|

The AI work does not complete every graduation task. See ../ai-engine/reports/FINAL_REPORT.md for measured results, public-data limitations and next actions. Keyboard/mouse remain available. Voice and eye/head extras are future scope.
