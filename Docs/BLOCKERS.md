# Open blockers and external evidence

| ID | Status | Effect / next action |
| --- | --- | --- |
| B01 Git metadata | Resolved through reviewed elevated Git | Default sandbox writes still deny `.git/index.lock`; the 6 October reviewed elevated command stages the narrow verification checkpoint successfully. Existing branch and unrelated changes are preserved. Commit identity is in the current closeout evidence. |
| B02 physical device | Open | No Nano 33 IoT/HX711/load-cell assembly has been available for real calibration, BLE throughput or closed-loop tests. Synthetic tests may validate contracts only. |
| B03 clinical data | Open | No consented participant dataset, clinician-approved thresholds or regulatory evidence. Fatigue model and safety policy can be software-tested but cannot be asserted clinically accurate. |
| B04 proprietary integration | Open | Availability/license compatibility of Obi Softbody/Fluid, YOLO trained model and any additional commercial assets must be established before enabling them. Fallbacks must be named. |
| B05 live tracking | Open | Previous GUI camera preview opened, but supervised left/right human-hand mapping, grip pickup and end-to-end latency have not been measured after the last code changes. |
| B06 current Unity build | Resolved for current software fixtures | User-session Unity 6000.4.4f1 builds successfully; 6 October repaired build passes 168/169/168 player checks, 60 UI checks at two resolutions and 87 gallery checks at three resolutions, plus three Play/Stop cycles. Headless session licensing exit 198 is a separate observation. See CLOSEOUT_2026_10_06.md; B02/B03/B05 remain open. |
| B07 historical UI branch | Historical attempt; no new branch required for this pass | Earlier sandbox branch creation was denied. This repair pass preserves `3.2-bimanual-tracking` and records a scoped checkpoint using reviewed Git metadata access; no `ui-redesign` branch or tag is claimed. |


## 2026-10-06 graduation item 2 update

B08 graduation item 2 native acceptance: OPEN. Editor launch reached Unity Editor Software Terms. User unavailable and requested file-only tests; no terms accepted or queued automatic Play/build. Earlier B06 closure applies to the repair release only. Newly integrated journey must pass native three-cycle QA and fresh build/player before DONE.
