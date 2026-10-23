# Research risk register
|ID|Risk|Likelihood / impact|Mitigation|Owner|
|---|---|---|---|---|
|R1|No hardware before collection window|High / high|Public proxy engineering only; no clinical deployment|Project team|
|R2|Dynamometer/EMG/device domain gap|Certain / high|Per-source units, MVC normalization, device fine-tuning and unseen participants|ML lead|
|R3|Weak time labels / circular labels|High / high|Explicit weak tag, no true-fatigue performance claim; obtain objective decline/Borg|Research supervisor|
|R4|Small subject count|High / high|Grouped splits, five seeds, confidence limitations; larger approved collection|Research supervisor|
|R5|Unity operator/dependency incompatibility|Observed / high|Real native inference parity; CNN fallback; pinned compatible archives|Unity lead|
|R6|False stop or missed fatigue|High / high|Independent modalities, hysteresis, explicit resume, invalid-input warning, supervised validation|Clinical supervisor|
|R7|W9-10/W15-20 exam pause|Planned / medium|W13-14 buffer and reproducible automated commands|Project lead|
|R8|Ethics / consent delay|High / high|Prepare templates; no recruitment or collection automatically|Principal investigator|
|R9|rPPG affected by light/motion/skin tone|High / high|Quality gating, no HRV before60s, OFF default, compare reference sensor across participants|Vision lead|
|R10|Emotion mistaken for pain/fatigue|High / high|Explicit unvalidated label and consent; no diagnostic interpretation|Research supervisor|
|R11|Model or sensor loss|Medium / high|No escalation; pause with explicit resume; stop latched on multiple signs|Software lead|
|R12|Privacy/export leakage|Medium / high|Local DPAPI store, no frames, pseudonymous opt-in export; private directories ignored|Data custodian|
