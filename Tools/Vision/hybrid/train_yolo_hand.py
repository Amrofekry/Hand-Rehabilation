"""Trains YOLO26-pose on the Ultralytics Hand Keypoints dataset (21 keypoints, 26,768 images; AGPL-3.0, accepted 2026-10-27).

  .venv-cv/Scripts/python.exe Tools/Vision/hybrid/train_yolo_hand.py [--model yolo26n-pose.pt] [--epochs 80]

Everything stays under CVTraining/ (git-ignored): the dataset, the Ultralytics settings (YOLO_CONFIG_DIR), the
pretrained checkpoint and the runs. The labels of this dataset were produced by MediaPipe, so the trained model
learns MediaPipe-like keypoints; its value here is robust detection and a second, independent 2D opinion.
"""
import argparse
import os
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
WORK = ROOT / "CVTraining"
import env  # noqa: E402,F401  - every cache, setting and download stays in CVTraining/ on drive E


def dataset_yaml() -> Path:
    """A project copy of the official hand-keypoints.yaml whose dataset path points inside CVTraining/."""
    import ultralytics
    official = Path(ultralytics.__file__).parent / "cfg" / "datasets" / "hand-keypoints.yaml"
    text = official.read_text(encoding="utf-8")
    lines = [f"path: {(WORK / 'datasets' / 'hand-keypoints').as_posix()}" if line.startswith("path:") else line for line in text.splitlines()]
    out = WORK / "hand-keypoints.yaml"; out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model", default="yolo26n-pose.pt")
    parser.add_argument("--epochs", type=int, default=80)
    parser.add_argument("--imgsz", type=int, default=640)
    parser.add_argument("--batch", type=int, default=-1, help="-1 picks the largest batch that fits the GPU")
    parser.add_argument("--name", default=None)
    parser.add_argument("--fraction", type=float, default=1.0, help="share of the training images (smoke tests)")
    args = parser.parse_args()
    WORK.mkdir(parents=True, exist_ok=True); os.chdir(WORK)  # pretrained weights download here
    from ultralytics import SETTINGS, YOLO
    SETTINGS.update({"datasets_dir": str(WORK / "datasets"), "weights_dir": str(WORK / "weights"), "runs_dir": str(WORK / "runs")})
    model = YOLO(args.model)
    name = args.name or Path(args.model).stem.replace("-pose", "") + "-hand"
    model.train(data=str(dataset_yaml()), epochs=args.epochs, imgsz=args.imgsz, batch=args.batch, device=0,
                project=str(WORK / "runs"), name=name, exist_ok=True, patience=15, workers=4, seed=0, plots=True,
                fraction=args.fraction)
    best = WORK / "runs" / name / "weights" / "best.pt"
    if best.exists():
        target = WORK / "weights" / f"{name}.pt"; target.parent.mkdir(exist_ok=True); shutil.copy2(best, target)
        print("BEST", target)


if __name__ == "__main__":
    main()
