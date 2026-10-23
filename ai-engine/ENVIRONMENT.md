# Tested environment

- Canonical project E:/GP/codex/GripRehab-Unity.
- Unity 2022.3.20f1 (61c2feb0970d), Windowsx64 Mono, Direct3D 11, Sentis 2.0.0 CPU.
- Windows 11 build26300; Python 3.12.14; Torch 2.5.1+cpu; NumPy 1.26.4 in isolated ai-engine/.venv-ai; exact requirements-lock retained.
- Existing camera interpreter is separate: Python 3.12.14, MediaPipe 1.0.1, OpenCV-contrib5.0.0.93, NumPy 2.5.3; unchanged by the ML installation.
- Rendered GPU: NVIDIA GeForce RTX3060Laptop GPU, reported5996 MB VRAM. CPU/RAM CIM fields were unavailable to the sandbox; do not infer whole-game throughput.
- Native package resolution pins compatible official archives in Tools/AI. Absolute file links target the permanent canonical location. Process-local NODE_EXTRA_CA_CERTS used existing Windows trusted public roots; no TLS bypass or persistent security-setting change.
- Windows post-build callback verifies/copies the matching original Unity 2022x64 NVUnityPlugin runtime. SHA and Editor version retained in Logs/windows-runtime-dependency.json.
- The machine blocks PowerShell scripts; tested Run-AI.cmd/Python paths need no execution-policy changes.
