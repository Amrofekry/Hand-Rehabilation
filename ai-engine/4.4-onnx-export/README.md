# 4.4-onnx-export

ONNX export and Unity Sentis integration (CNN deployed; GRU operator unsupported in Sentis).

**Status:** implemented (2026-10-22; see `../reports/FINAL_REPORT.md`). Results are on public/weak-label or SYNTHETIC data, not a clinical fatigue validation.

**Where it lives:** `../export_unity.py`; Unity runtime `Assets/ProductIntegration/ResearchAiRuntime.cs`, model `Assets/Resources/ResearchAi/PROTOTYPE_PROXY_cnn.onnx`; parity in `../models/parity-fixture.json`.

WBS scope and acceptance criteria: root `PROJECT_PLAN.md`.
