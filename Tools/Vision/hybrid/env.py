"""Keeps every cache, setting, download and run of the hybrid tracker inside the project on drive E.

Import this first, before torch, ultralytics, rfdetr or matplotlib, so nothing is written under C:\\Users.
"""
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
WORK = ROOT / "CVTraining"
CACHE = WORK / "cache"

_PATHS = {
    "YOLO_CONFIG_DIR": WORK / "ultralytics-config",   # Ultralytics settings.json
    "TORCH_HOME": CACHE / "torch",                    # torch.hub checkpoints
    "HF_HOME": CACHE / "huggingface",                 # Hugging Face hub (RF-DETR backbone)
    "XDG_CACHE_HOME": CACHE / "xdg",                  # libraries that use ~/.cache
    "MPLCONFIGDIR": CACHE / "matplotlib",             # Ultralytics training plots
    "CUDA_CACHE_PATH": CACHE / "cuda",                # CUDA kernel cache
    "UV_CACHE_DIR": CACHE / "uv",                     # package downloads
    "PIP_CACHE_DIR": CACHE / "pip",
    "TMP": CACHE / "tmp", "TEMP": CACHE / "tmp",      # temporary files of training workers
}
for key, path in _PATHS.items():
    path.mkdir(parents=True, exist_ok=True)
    os.environ[key] = str(path)
os.chdir(WORK)  # downloads that use the working directory (pretrained weights) land here too

# This PC re-signs HTTPS (antivirus or proxy), so Python's bundled CA list rejects downloads (GitHub, Hugging Face).
# truststore makes Python verify against the Windows certificate store instead; verification stays on.
try:
    import truststore
    truststore.inject_into_ssl()
except ImportError:
    pass
