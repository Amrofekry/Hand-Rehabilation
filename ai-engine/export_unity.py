"""Copy validated public engineering artifacts into the existing Unity Resources folder.
Stop Play before importing model changes. No scene is generated or overwritten.
"""
import hashlib,json,shutil
from pathlib import Path
root=Path(__file__).resolve().parent;report=json.loads((root/'reports/training.json').read_text(encoding='utf-8'))
if report['provenance']!='PROTOTYPE-PROXY' or not report['exports']['cnn']['parity_pass']:raise ValueError('Only validated public-proxy artifacts may use this demo integration')
source=root/'models/PROTOTYPE_PROXY_cnn.onnx'
if hashlib.sha256(source.read_bytes()).hexdigest()!=report['exports']['cnn']['sha256']:raise ValueError('model hash mismatch')
destination=root.parent/'Assets/Resources/ResearchAi';destination.mkdir(exist_ok=True)
for name in ['PROTOTYPE_PROXY_cnn.onnx','scaler.json','parity-fixture.json']:shutil.copy2(root/'models'/name,destination/name)
print('Validated PROTOTYPE-PROXY demo model/scaler copied to Unity. Real-device inference remains blocked.')
