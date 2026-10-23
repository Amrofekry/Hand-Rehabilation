"""Run grouped own-data learning curves without including test subjects in fitting."""
import argparse,json,sys
from pathlib import Path
import pandas as pd
sys.path.insert(0,str(Path(__file__).parent/'src'))
from fatigue_ai.data import dump
from fatigue_ai.train import prepare,split,fit_one,metrics
import torch,numpy as np
p=argparse.ArgumentParser();p.add_argument('csv',type=Path);args=p.parse_args();root=Path(__file__).parent
x,y,g,t,_,_=prepare(root,args.csv);(tr,va,te),ids=split(g);rows=[]
for count in range(2,len(ids['train'])+1):
 train=tr[np.isin(g[tr],ids['train'][:count])];mu=x[train].reshape(-1,8).mean(0);sd=np.maximum(x[train].reshape(-1,8).std(0),1e-6);z=((x-mu)/sd).astype('float32')
 m,h=fit_one('cnn',11,z,y,train,va,epochs=25)
 with torch.no_grad():p=m(torch.tensor(z[te])).sigmoid().numpy()
 rows.append({'training_subjects':count,'test':metrics(y[te],p)})
dump(root/'reports'/('SYNTHETIC_learning_curve.json' if args.csv.name.startswith('SYNTHETIC_') else 'own_learning_curve.json'),rows)
