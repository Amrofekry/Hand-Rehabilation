"""Subject-disjoint training, held-out evaluation and fixed-shape export, WBS 4.3/4.4.
EMG experiment uses time-in-task WEAK labels, never asserted as true fatigue.
"""
import argparse, copy, hashlib, json, platform, time
from pathlib import Path
import numpy as np
from scipy.special import expit
from sklearn.metrics import classification_report, confusion_matrix, roc_auc_score, average_precision_score, brier_score_loss
from sklearn.linear_model import LogisticRegression
from sklearn.ensemble import HistGradientBoostingClassifier
from sklearn.model_selection import GroupKFold
import torch
from .data import FEATURES,dump,zenodo_trials,windows,read_device_csv,preprocess
from .models import Temporal

def prepare(root,own=None,seconds=5):
 root=Path(root);xs=[];ys=[];groups=[];times=[];trials=[];summaries=[]
 source=list(read_device_csv(own)) if own else list(zenodo_trials(root))
 for d in source:
  t,x=d['t'],d['x'];t,x=preprocess(t,x)
  # MVC is calibrated pre-task or an early-task PROXY reference, never whole-trial maximum.
  if own and (d.get('mvc') is None or not np.isfinite(d['mvc']) or d['mvc']<=0):raise ValueError('Own-data MVC must come from a calibrated pre-task or game baseline')
  mvc=d.get('mvc') or float(np.percentile(x[(t>=2)&(t<10)],95))
  if not np.isfinite(mvc) or mvc<=0:raise ValueError('no usable baseline')
  validate_labels=d.get('labels')
  for end,f in windows(t,x,mvc,seconds):
   # Public early-reference normalization uses2..10s; do not predict before
   # that reference exists. Own MVC is measured before the trial.
   if not own and end<10:continue
   # Time-only weak prior is explicit; middle interval excluded from training.
   ratio=end/max(t[-1],1)
   if own:
    if validate_labels is None or not np.isfinite(validate_labels).all() or not set(validate_labels).issubset({-1,0,1}):raise ValueError('Own-data training requires explicit independent fatigue_label (-1 unknown, 0 stable, 1 fatigue); time labels are forbidden')
    label=int(validate_labels[np.searchsorted(d['t'],end,side='right')-1])
   else:label=0 if ratio<=.30 else 1 if ratio>=.70 else -1
   if label<0:continue
   xs.append(f);ys.append(label);groups.append(d['subject']);times.append(float(end));trials.append(d['trial'])
  summaries.append({'subject':d['subject'],'trial':d['trial'],'source':d['source'],'unit':d['unit'],'seconds':float(t[-1]),'samples':len(t),'baseline':mvc,'labels':'WEAK time-in-task early<=30%, late>=70%; not fatigue ground truth'})
 x=np.stack(xs);y=np.asarray(ys,dtype=np.int64);groups=np.asarray(groups)
 if len(set(groups))<6:raise ValueError('at least six separate subjects needed for train/validation/test')
 return x,y,groups,np.asarray(times),trials,summaries

def split(groups,seed=20261022):
 subjects=np.array(sorted(set(groups)));np.random.default_rng(seed).shuffle(subjects)
 n=max(1,int(len(subjects)*.2));test=subjects[:n];val=subjects[n:2*n];train=subjects[2*n:]
 ids=[np.flatnonzero(np.isin(groups,s)) for s in (train,val,test)]
 assert not(set(train)&set(val) or set(train)&set(test) or set(val)&set(test))
 return ids,{'train':train.tolist(),'validation':val.tolist(),'test':test.tolist(),'unit':'subject','seed':seed}

def metrics(y,p):
 pred=p>=.5;result={'report':classification_report(y,pred,output_dict=True,zero_division=0),'confusion_matrix':confusion_matrix(y,pred,labels=[0,1]).tolist(),'brier':float(brier_score_loss(y,p))}
 result.update(roc_auc=float(roc_auc_score(y,p)) if len(set(y))==2 else None,pr_auc=float(average_precision_score(y,p)) if len(set(y))==2 else None)
 return result

def event_metrics(y,p,groups,times):
 delays=[];false_stops=0;stable_s=0
 for group in sorted(set(groups)):
  ids=np.flatnonzero(groups==group);ids=ids[np.argsort(times[ids])];run=0;triggered=False
  onset=min((times[i] for i in ids if y[i]),default=None)
  last=None
  for i in ids:
   if last is not None and times[i]-last>1.5:run=0;triggered=False
   run=run+1 if p[i]>=.7 else 0
   if y[i]==0:stable_s+=1
   if run>=3 and not triggered:
    triggered=True
    if y[i]==0:false_stops+=1
    elif onset is not None:delays.append(float(times[i]-onset))
   if run==0:triggered=False
   last=times[i]
 return {'false_stop_candidates_per_10min':false_stops/max(stable_s/600,1e-9),'detection_delay_seconds':delays,'detected_subjects':len(delays),'note':'single modality candidates; production stop needs >1 independent signs; weak-label onset'}

def fit_one(kind,seed,x,y,train,val,hidden=32,epochs=35,initial=None):
 torch.manual_seed(seed);rng=np.random.default_rng(seed);m=Temporal(kind,hidden)
 if initial:m.load_state_dict(torch.load(initial,map_location='cpu',weights_only=True))
 opt=torch.optim.Adam(m.parameters(),lr=.002,weight_decay=1e-4)
 counts=np.bincount(y[train],minlength=2);weight=torch.tensor(float(counts[0]/max(1,counts[1])))
 loss=torch.nn.BCEWithLogitsLoss(pos_weight=weight);xx=torch.tensor(x);yy=torch.tensor(y,dtype=torch.float32)
 best=None;best_loss=float('inf');stale=0;history=[]
 for epoch in range(epochs):
  m.train()
  for ids in np.array_split(rng.permutation(train),max(1,int(np.ceil(len(train)/64)))):
   batch=xx[ids].clone();batch+=torch.randn_like(batch)*.015
   batch*=float(rng.uniform(.97,1.03));batch+=float(rng.normal(0,.005))
   # Mild monotone time-warp on train only, preserving endpoints and chronology.
   if len(batch)>1 and rng.random()<.25:
    u=torch.linspace(0,1,batch.shape[1])**float(rng.uniform(.9,1.1));j=(u*(batch.shape[1]-1)).long();batch=batch[:,j,:]
   opt.zero_grad();l=loss(m(batch),yy[ids]);l.backward();torch.nn.utils.clip_grad_norm_(m.parameters(),1);opt.step()
  m.eval()
  with torch.no_grad():v=float(loss(m(xx[val]),yy[val]))
  history.append(v)
  if v<best_loss-1e-5:best_loss=v;best=copy.deepcopy(m.state_dict());stale=0
  else:stale+=1
  if stale>=6:break
 m.load_state_dict(best);m.eval();return m,history

def calibrate(logits,y):
 temperatures=np.geomspace(.25,4,60)
 return float(min(temperatures,key=lambda t:-(y*np.log(np.clip(expit(logits/t),1e-8,1))+(1-y)*np.log(np.clip(1-expit(logits/t),1e-8,1))).mean()))

def run(root,own=None,epochs=35,seeds=(11,23,37,51,71),initial=None):
 root=Path(root);report=root/'reports';report.mkdir(exist_ok=True);models=root/'models';models.mkdir(exist_ok=True)
 torch.set_num_threads(2);x,y,g,t,trials,summary=prepare(root,own);(tr,va,te),splits=split(g)
 # TRAIN ONLY fit: validation/test never affect scaling, augmentation or early stopping.
 mean=x[tr].reshape(-1,8).mean(0);std=x[tr].reshape(-1,8).std(0);std=np.maximum(std,1e-6);scaled=((x-mean)/std).astype('float32')
 provenance='SYNTHETIC' if own and Path(own).name.startswith('SYNTHETIC_') else 'RESEARCH_DEVICE' if own else 'PROTOTYPE-PROXY'
 dump(models/'scaler.json',{'version':'features-1','features':FEATURES,'mean':mean.tolist(),'std':std.tolist(),'input_shape':[1,50,8],'provenance':provenance,'training_subjects':splits['train']})
 np.savez_compressed(root/'data_processed'/('SYNTHETIC_windows.npz' if provenance=='SYNTHETIC' else 'PROTOTYPE_PROXY_windows.npz'),x=x,y=y,subjects=g,t=t)
 dump(report/'inspection.json',summary);dump(report/'split-audit.json',splits)
 dump(report/'label-sensitivity.json',[{'early_ratio':early,'late_ratio':late,'stable':sum(int((d['seconds']*early-5)//1+1) for d in summary),'late':sum(int(d['seconds']*(1-late)//1+1) for d in summary),'weak':True} for early,late in [(0.2,.8),(.25,.75),(.3,.7)]])
 results={};tab=scaled.mean(1)
 for name,m in [('logistic',LogisticRegression(class_weight='balanced',random_state=11,max_iter=500)),('gradient_boosting',HistGradientBoostingClassifier(max_iter=80,max_leaf_nodes=7,random_state=11))]:
  m.fit(tab[tr],y[tr]);p=m.predict_proba(tab[te])[:,1];results[name]={'validation':metrics(y[va],m.predict_proba(tab[va])[:,1]),'test':metrics(y[te],p)}
 cv=[]
 for train_fold,val_fold in GroupKFold(3).split(x[tr],y[tr],g[tr]):
  aa,bb=tr[train_fold],tr[val_fold];mu=x[aa].reshape(-1,8).mean(0);sd=np.maximum(x[aa].reshape(-1,8).std(0),1e-6)
  model=LogisticRegression(class_weight='balanced',max_iter=500).fit(((x[aa]-mu)/sd).mean(1),y[aa]);cv.append(metrics(y[bb],model.predict_proba(((x[bb]-mu)/sd).mean(1))[:,1]))
 results['rule']={'test':metrics(y[te],np.clip(x[te,-1,7],0,1)),'note':'force-decay rule evaluated on EMG effort proxy; expected domain mismatch'}
 best_models={};best_val={};latency={}
 for kind in ('gru','lstm','cnn'):
  records=[];best_score=-1
  for seed in seeds:
   checkpoint=initial.get(kind) if isinstance(initial,dict) else (initial if kind=='cnn' else None)
   model,history=fit_one(kind,seed,scaled,y,tr,va,epochs=epochs,initial=checkpoint)
   with torch.no_grad():logits=model(torch.tensor(scaled)).numpy()
   temp=calibrate(logits[va],y[va]);prob=expit(logits/temp);validation=metrics(y[va],prob[va]);score=validation['report']['macro avg']['f1-score']
   records.append({'seed':seed,'epochs':len(history),'validation_loss':history,'temperature':temp,'validation':validation,'test':metrics(y[te],prob[te]),'events':event_metrics(y[te],prob[te],g[te],t[te])})
   if score>best_score:best_score=score;best_models[kind]=(model,temp,seed);best_val[kind]=prob[va].tolist()
  results[kind]=records
 # Sentis GRU operator is unsupported. CNN is the fixed-shape supported fallback;
 # selection of seeds is validation-only, not selected from held-out test numbers.
 exports={}
 for kind,(model,temp,seed) in best_models.items():
  class Calibrated(torch.nn.Module):
   def __init__(self,base,temperature):super().__init__();self.base=base;self.temp=temperature
   def forward(self,x):return torch.sigmoid(self.base(x)/self.temp)
  wrap=Calibrated(model,temp).eval();example=torch.tensor(scaled[va[:1]])
  p=models/(('SYNTHETIC_' if provenance=='SYNTHETIC' else 'PROTOTYPE_PROXY_')+kind+'.onnx');torch.save(model.state_dict(),p.with_suffix('.pt'))
  torch.onnx.export(wrap,example,str(p),input_names=['features'],output_names=['fatigue_probability'],opset_version=15,do_constant_folding=True)
  import onnx,onnxruntime as ort
  graph=onnx.load(str(p));onnx.checker.check_model(graph);session=ort.InferenceSession(str(p),providers=['CPUExecutionProvider']);diff=0
  for i in va[:20]:
   ex=scaled[i:i+1];actual=session.run(None,{'features':ex})[0]
   with torch.no_grad():expected=wrap(torch.tensor(ex)).numpy()
   diff=max(diff,float(np.abs(actual-expected).max()))
  durations=[]
  for i in range(110):
   start=time.perf_counter();session.run(None,{'features':scaled[va[:1]]})
   if i>=10:durations.append((time.perf_counter()-start)*1000)
  exports[kind]={'file':p.name,'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'max_abs_diff':diff,'parity_pass':diff<1e-4,'p50_ms':float(np.percentile(durations,50)),'p95_ms':float(np.percentile(durations,95)),'ops':sorted({n.op_type for n in graph.graph.node}),'seed':seed}
 if not own:
  from .labels import write_weak_window_labels
  write_weak_window_labels(root)
 # Feature/window/model-size ablations use validation only, never test tuning.
 ablations=[]
 for name,idx in [('amplitude_only',[0,1]),('without_slope',[0,1,3,4,5,6,7]),('all',list(range(8)))]:
  m=LogisticRegression(class_weight='balanced',max_iter=500).fit(tab[tr][:,idx],y[tr]);ablations.append({'feature_set':name,'validation':metrics(y[va],m.predict_proba(tab[va][:,idx])[:,1])})
 for seconds in (5,10):
  wx,wy,wg,wt,_,_=prepare(root,own,seconds);(aa,bb,_),_=split(wg);mu=wx[aa].reshape(-1,8).mean(0);sd=np.maximum(wx[aa].reshape(-1,8).std(0),1e-6);v=((wx-mu)/sd).mean(1)
  m=LogisticRegression(class_weight='balanced',max_iter=500).fit(v[aa],wy[aa]);ablations.append({'window_seconds':seconds,'validation':metrics(wy[bb],m.predict_proba(v[bb])[:,1])})
 for hidden in (16,64):
  m,h=fit_one('cnn',11,scaled,y,tr,va,hidden=hidden,epochs=min(20,epochs))
  with torch.no_grad():p=expit(m(torch.tensor(scaled[va])).numpy())
  ablations.append({'cnn_hidden':hidden,'validation':metrics(y[va],p),'epochs':len(h)})
 dump(report/'ablations.json',ablations)
 output={'provenance':provenance,'label_quality':'Explicit own-data labels; review protocol and ethics' if own else 'WEAK time-in-task, not independent fatigue ground truth','deployment_allowed_on_humans':False,'samples':len(y),'class_balance':np.bincount(y).tolist(),'split':splits,'results':results,'group_cv_train_only':cv,'exports':exports,'selected_deployment':'cnn','selection_reason':'Sentis fallback; best seed by validation macro F1','environment':{'python':platform.python_version(),'torch':torch.__version__,'numpy':np.__version__,'platform':platform.platform(),'threads':2}}
 if initial:
  paths=initial if isinstance(initial,dict) else {'cnn':initial}
  output['initial_checkpoints']={kind:{'file':Path(path).name,'sha256':hashlib.sha256(Path(path).read_bytes()).hexdigest()} for kind,path in paths.items()}
 dump(report/('SYNTHETIC_training.json' if provenance=='SYNTHETIC' else 'training.json'),output)
 # Unity replay inputs use the SAME exported scaler and feature ordering.
 dump(models/'parity-fixture.json',{'features':scaled[va[:1]].reshape(-1).tolist(),'expected':float(ort.InferenceSession(str(models/exports['cnn']['file']),providers=['CPUExecutionProvider']).run(None,{'features':scaled[va[:1]]})[0][0])})
 figure_report(root,output,y[te],np.asarray(best_val['cnn']),y[va])
 return output

def figure_report(root,output,y,p,val_y):
 import matplotlib;matplotlib.use('Agg')
 import matplotlib.pyplot as plt
 from sklearn.calibration import calibration_curve
 selected=output['exports']['cnn']['seed'];record=next(q for q in output['results']['cnn'] if q['seed']==selected)
 fig,ax=plt.subplots(1,2,figsize=(10,4));cm=np.asarray(record['test']['confusion_matrix']);ax[0].imshow(cm,cmap='Blues');ax[0].set(title=f'CNN seed{selected} / unseen subjects',xlabel='Predicted weak target',ylabel='Observed weak target',xticks=[0,1],yticks=[0,1],xticklabels=['Early','Late'],yticklabels=['Early','Late'])
 for (i,j),n in np.ndenumerate(cm):ax[0].text(j,i,str(n),ha='center',va='center')
 prob,mean=calibration_curve(val_y,p,n_bins=6);ax[1].plot(mean,prob,'o-');ax[1].plot([0,1],[0,1],'--');ax[1].set(title='Validation calibration (proxy)',xlabel='Probability',ylabel='Observed weak-label fraction');fig.tight_layout()
 for suffix in ('png','svg'):fig.savefig(root/'reports'/(('SYNTHETIC_evaluation.' if output['provenance']=='SYNTHETIC' else 'PROTOTYPE_PROXY_evaluation.')+suffix),dpi=170)
 plt.close(fig)

if __name__=='__main__':
 parser=argparse.ArgumentParser();parser.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]);parser.add_argument('--own',type=Path);parser.add_argument('--epochs',type=int,default=35);args=parser.parse_args();run(args.root,args.own,args.epochs)
