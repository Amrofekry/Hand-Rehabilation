# Optional stages (after v1.0.0)

These stages are not needed for the graduation release. Each one is optional because v1.0.0 already meets its requirement another way, or because it needs hardware, people or approvals the project does not have. Effort is for one developer.

| ID | Stage | Value | Effort | Depends on | Why optional |
|---|---|---|---|---|---|
| O1 | Gaze input (old Stage 14): webcam eye/head gaze picks the target, the hand confirms | Hands-busy selection; less arm fatigue | 2–3 wk | MediaPipe Face Landmarker, calibration UI | Aim assist and the hand pointer already select targets |
| O2 | Voice assistant (old Stage 15): spoken coaching and Q&A ("how do I…", "how am I doing") on top of the command router | Guidance for patients who cannot read the HUD | 2 wk | Local speech-to-text and text-to-speech, a small on-device LLM or scripted dialogue | Commands already work; open dialogue raises privacy and safety review |
| O3 | Hybrid YOLO26 + RF-DETR hand tracker, A/B against MediaPipe on recorded clips | Possibly fewer losses in poor light | 3–4 wk + GPU time | `CVTraining/`, labelled clips, Sentis/ONNX export | Training was not finished. Adopt only if the A/B shows lower jitter or loss (`Docs/HAND_CONTROL_REPORT.md` §10) |
| O4 | Arabic localisation and RTL layout for every screen | Native-language patients | 2 wk | Unity Localization, an Arabic font with shaping | English UI is complete; Arabic guides exist in `Docs/*_AR.md` |
| O5 | Depth camera support (RealSense / Orbbec / iPhone LiDAR) | Real depth instead of the palm-size proxy (about 4× noisier than XY) | 2–3 wk | Device SDK, a second landmark source behind `VisionUdpReceiver` | Needs hardware the project does not have |
| O6 | Physical hardware trials (R5/R6 grips, BLE) | Turns the OPEN items into physical evidence | 1–2 wk bench time | `Docs/PHYSICAL_TEST_PROTOCOL.md`, certified masses | Needs the assembled hardware |
| O7 | Clinical pilot study design (protocol, ethics, outcome measures) | Path to clinical evidence | 2 mo + approvals | Ethics board, clinical supervisor, O6 done | No clinical claim is made, so it is outside the capstone |
| O8 | UI extras (21st/archify components, motion polish, dashboards v2) | Polish | 1–2 wk | 21st MCP / archify available in the session | The current uGUI design system passes the UI checks |

**Suggested order if continued:** O6, then O5 or O3 (whichever the O6 data shows matters more), then O4, O1, O2, O8. O7 comes last and is not engineering work.
