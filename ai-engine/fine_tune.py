"""Own-data comparison. Requires independent per-sample labels; synthetic is dry-run only."""
import argparse,json,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'src'))
from fatigue_ai.train import run,prepare,split,metrics
from fatigue_ai.models import Temporal
import numpy as np,torch
from scipy.special import expit
p=argparse.ArgumentParser();p.add_argument('csv',type=Path);p.add_argument('--epochs',type=int,default=20);args=p.parse_args()
root=Path(__file__).parent
# Isolate outputs so synthetic dry-runs can never overwrite public-model artifacts.
work=root/'reports'/('SYNTHETIC_finetune' if args.csv.name.startswith('SYNTHETIC_') else 'own_finetune');work.mkdir(exist_ok=True)
for experiment in ['from_scratch','fine_tuned']:
 for d in ['models','reports','data_processed']:(work/experiment/d).mkdir(parents=True,exist_ok=True)
public=root/'models/PROTOTYPE_PROXY_cnn.pt'
public_checkpoints={kind:root/f'models/PROTOTYPE_PROXY_{kind}.pt' for kind in ('gru','lstm','cnn')}
if not all(path.is_file() for path in public_checkpoints.values()):raise FileNotFoundError('Train the public research artifacts before the own-data comparison')
from_scratch=run(work/'from_scratch',args.csv,args.epochs)
fine_tuned=run(work/'fine_tuned',args.csv,args.epochs,initial=public_checkpoints)
x,y,g,t,_,_=prepare(root,args.csv);(_,_,test),_=split(g)
scaler=json.loads((root/'models/scaler.json').read_text(encoding='utf-8'));z=((x-np.asarray(scaler['mean']))/np.asarray(scaler['std'])).astype('float32')
report=json.loads((root/'reports/training.json').read_text(encoding='utf-8'));public_only={}
for kind,path in public_checkpoints.items():
 model=Temporal(kind);model.load_state_dict(torch.load(path,map_location='cpu',weights_only=True));model.eval()
 seed=report['exports'][kind]['seed'];temperature=next(q['temperature'] for q in report['results'][kind] if q['seed']==seed)
 with torch.no_grad():prob=expit(model(torch.tensor(z[test])).numpy()/temperature)
 public_only[kind]=metrics(y[test],prob)
(work/'comparison.json').write_text(json.dumps({'provenance':'SYNTHETIC' if args.csv.name.startswith('SYNTHETIC_') else 'RESEARCH_DEVICE','from_scratch':from_scratch,'fine_tuned':fine_tuned,'public_only':public_only['cnn'],'public_only_by_model':public_only,'learning_curve_command':'python learning_curve.py '+str(args.csv)},indent=2),encoding='utf-8')
