# Preparation template - not an approved human-study protocol
Do not perform fatiguing trials on patients without an approved protocol and supervision. This document prepares the data path; it does not authorize recruitment, prescribe force targets, or replace a clinical assessment.

1. Institutional ethics review and supervisor define population, exclusions, force limits, recovery periods, session duration, stop criteria and emergency procedures. Begin validation with approved healthy volunteers, then consider patients only under appropriate oversight.
2. Explain separate game, hand camera, optional expression and optional pulse consent. No face/raw video storage in ordinary game runtime. Participants may stop/revoke without penalty. Pseudonymous ID assignment and consent contact/key file are held separately outside this repository.
3. Calibrate the existing Phase2 HX711 pipeline with known masses and Newton conversion; record device, calibration revision/CRC, shape and hand. Verify unloaded zero, span, noise and saturation. Never derive force from camera closure.
4. Supervisor approves maximum-comfortable/MVC trials x3 and adequate rest, followed by approved submaximal sustained/repeated tasks and Borg CR10 prompts. The game's staged54s trial is a software demonstration, not a substitute for that approved protocol.
5. Record time-synchronization method, force units, source, sequence, quality, calibration, MVC-before/after where approved, target intensity, rest/assessment/fatigue phases, optional kinematics, and independently justified labels. Timing alone is weak evidence.
6. Each hand/shape/calibration/task/connection is a separate trial. Stop on discomfort/request, invalid sensor, unsafe movement, or supervisor criteria; no automatic restart after a stop. Record missing data rather than inventing values.
7. Log JSONL from the existing Phase2 bridge, then `python -m fatigue_ai.collect input.jsonl output.csv --subject S001 --trial T001 --hand right --mvc <approved measured reference>`. Output must be SYNTHETIC_-prefixed for replay.
8. Review labels blinded to model predictions. Keep all trials of one subject in one split. Hold final test subjects out until methods are fixed; assess recall, false-stop candidates and actual closed-loop safety separately.
9. Fine-tune with independent labels and run public-only/fine-tuned/from-scratch and learning-curve comparisons. Do not promote a model to device use from dry-run success alone.

## Ethics-application checklist
Purpose, investigators, recruitment and inclusion/exclusion criteria, consent and capacity, risk/benefit, supervision and stop procedures, data controller/retention/deletion/export policy, pseudonym key separation, demographic fairness, compensation, withdrawal process, adverse event reporting, and separate optional sensing permissions. Obtain approval/version/date before data collection.

## Form template (copy into an approved local form; nothing is submitted online)
Study ID; consent version/date; game consent yes/no; hand-camera consent yes/no; expression consent yes/no; pulse consent yes/no; age band; self-described sex/gender optional; dominant/affected hand; shape; task; baseline ID; calibration revision; trial start/end; pre/post Borg CR10; discomfort/stop reason; operator; data-retention acknowledgement. Avoid names, addresses, account passwords and free-text clinical identifiers in training CSVs.
