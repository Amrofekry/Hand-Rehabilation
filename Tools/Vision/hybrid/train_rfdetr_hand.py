"""Fine-tunes RF-DETR Nano (Apache 2.0) as a one-class hand detector.

  .venv-cv/Scripts/python.exe Tools/Vision/hybrid/train_rfdetr_hand.py [--epochs 20]

RF-DETR trains on COCO JSON (train/valid/test, each with _annotations.coco.json). The boxes come from the same Hand
Keypoints dataset as the YOLO model (its YOLO-format labels hold one box per hand); the validation split is halved
into valid and test. Images are hard-linked, not copied. Output: CVTraining/runs/rfdetr-nano-hand/.
"""
import argparse
import json
import os
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
WORK = ROOT / "CVTraining"
SOURCE = WORK / "datasets" / "hand-keypoints"
COCO = WORK / "datasets" / "hand-boxes-coco"
import env  # noqa: E402,F401  - every cache, setting and download stays in CVTraining/ on drive E


def link(src: Path, dst: Path):
    if dst.exists():
        return
    try:
        os.link(src, dst)
    except OSError:
        shutil.copy2(src, dst)


def convert() -> Path:
    from PIL import Image
    if (COCO / "test" / "_annotations.coco.json").exists():
        return COCO
    splits = {"train": sorted((SOURCE / "images" / "train").glob("*.*")), "val": sorted((SOURCE / "images" / "val").glob("*.*"))}
    if not splits["train"]:
        raise SystemExit(f"Dataset missing at {SOURCE}; run train_yolo_hand.py first (it downloads it).")
    half = len(splits["val"]) // 2
    plan = {"train": splits["train"], "valid": splits["val"][:half], "test": splits["val"][half:]}
    for split, images in plan.items():
        out = COCO / split; out.mkdir(parents=True, exist_ok=True)
        coco = {"images": [], "annotations": [], "categories": [{"id": 0, "name": "hand", "supercategory": "none"}]}
        for image_id, path in enumerate(images):
            label = SOURCE / "labels" / path.parent.name / (path.stem + ".txt")
            with Image.open(path) as im:
                w, h = im.size
            link(path, out / path.name)
            coco["images"].append({"id": image_id, "file_name": path.name, "width": w, "height": h})
            if label.exists():
                for line in label.read_text().split("\n"):
                    v = line.split()
                    if len(v) < 5:
                        continue
                    cx, cy, bw, bh = (float(x) for x in v[1:5])
                    box = [(cx - bw / 2) * w, (cy - bh / 2) * h, bw * w, bh * h]
                    coco["annotations"].append({"id": len(coco["annotations"]), "image_id": image_id, "category_id": 0,
                                                "bbox": [round(b, 2) for b in box], "area": round(box[2] * box[3], 2), "iscrowd": 0})
        (out / "_annotations.coco.json").write_text(json.dumps(coco), encoding="utf-8")
        print(split, len(coco["images"]), "images", len(coco["annotations"]), "hands")
    return COCO


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--epochs", type=int, default=20)
    parser.add_argument("--batch", type=int, default=4)
    parser.add_argument("--accum", type=int, default=4, help="batch x accum = 16, as the RF-DETR docs advise")
    args = parser.parse_args()
    dataset = convert()
    from rfdetr import RFDETRNano
    out = WORK / "runs" / "rfdetr-nano-hand"; out.mkdir(parents=True, exist_ok=True)
    RFDETRNano().train(dataset_dir=str(dataset), epochs=args.epochs, batch_size=args.batch, grad_accum_steps=args.accum,
                       lr=1e-4, output_dir=str(out), early_stopping=True)
    best = out / "checkpoint_best_total.pth"
    if best.exists():
        target = WORK / "weights" / "rfdetr-nano-hand.pth"; target.parent.mkdir(exist_ok=True); shutil.copy2(best, target)
        print("BEST", target)


if __name__ == "__main__":
    main()
