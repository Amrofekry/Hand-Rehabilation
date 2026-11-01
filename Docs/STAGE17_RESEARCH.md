# Stage 17 next-stage research proposal (2026-11-01)

These are recommendations only; nothing here is implemented. They come from desk research against well-known primary sources (listed), ranked by benefit for effort. "Now" means possible with the current hardware and data; otherwise the dependency is named.

| Rank | Idea | Benefit | Effort | Needs | Risk | Now? |
|---|---|---|---|---|---|---|
| 1 | Physical calibration and repeatability study of the grip (O6) | Turns force numbers into physical evidence | 1–2 wk | Assembled grip, certified masses | Low | Hardware |
| 2 | Real-webcam latency and accuracy benchmark (camera capture timestamp → applied pose; reference trajectories) | Measures what is only SYNTHETIC today | 1 wk | Webcam, a printed target board | Low | Yes |
| 3 | Depth from a depth camera or stereo (O5) | Removes the palm-size depth proxy, the largest jitter source | 2–3 wk | RealSense/Orbbec or 2 webcams | Medium | Hardware |
| 4 | Rehab metrics from the force stream: time-to-peak, sustained-hold steadiness (CV of force), rate of force development, fatigue index (decline over repeats) | Standard, interpretable outcomes for the doctor dashboard | 1–2 wk | Calibrated sensor | Low | Software now, data with hardware |
| 5 | Personalised difficulty from the patient's own baseline (per-patient MVC and ROM normalisation) before any learned model | Safer and more explainable than population models | 1 wk | Baseline protocol (4.1) | Low | Yes |
| 6 | Hybrid detector A/B (YOLO26-pose / RF-DETR vs MediaPipe) on recorded clips (O3) | Possibly fewer losses in poor light | 3–4 wk | GPU time, labelled clips | Medium | Partly |
| 7 | Grasp classification from landmarks (power/precision/lateral) feeding the 18-grip catalogue | Automatic grip choice from the real hand | 2 wk | Labelled poses | Medium | Yes (own data) |
| 8 | Digital twin of the grip: the same object stiffness in game and on a variable-stiffness grip | Matches what the hand feels to what the patient sees | 2+ mo | New hardware | High | No |
| 9 | Fatigue model on force and EMG with real labels (Borg scale) | Replaces the weak time labels | 3+ mo | Ethics approval, participants | High | No |

## Sources (primary)
- Casiez, Roussel, Vogel, "1€ Filter", CHI 2012: the filter used for the camera hand.
- Google MediaPipe Hand Landmarker documentation (ai.google.dev/edge/mediapipe): the tracker in use.
- Feix et al., "The GRASP Taxonomy of Human Grasp Types", IEEE THMS 2016: basis for grip types.
- Roboflow RF-DETR repository and paper (2025); Ultralytics YOLO pose documentation: candidates for O3.
- Mathiowetz et al., grip-strength norms (1985), and the ASHT clinical assessment recommendations: reference protocols for O6/O7.
- Ebied et al., Zenodo 10.5281/zenodo.5189275: the EMG data already used (weak labels; see `ai-engine/DATA_CARD.md`).

**Essential vs optional:** items 1 and 2 are essential before any claim about accuracy; items 3–7 are optional enhancements; items 8–9 are research needing funding, hardware or approvals.
