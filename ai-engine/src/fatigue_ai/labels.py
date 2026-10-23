"""Reproducible research labels, with evidence and uncertainty retained (WBS 4.3)."""
from dataclasses import dataclass
import math
@dataclass(frozen=True)
class Label:
 binary:int
 level:int
 evidence:str
 weak:bool

def label(force_early=None,force_recent=None,mvc_pre=None,mvc_post=None,borg=None,time_ratio=None,constant_target=False,decline=.30):
 if not 0<decline<1:raise ValueError('invalid decline threshold')
 for v in [force_early,force_recent,mvc_pre,mvc_post,borg,time_ratio]:
  if v is not None and (not math.isfinite(v) or v<0):raise ValueError('invalid label evidence')
 if borg is not None and borg>10:raise ValueError('Borg CR10 outside 0..10')
 objective=[]
 if constant_target and force_early is not None and force_recent is not None and force_early>0:objective.append(force_recent/force_early)
 if mvc_pre is not None and mvc_post is not None and mvc_pre>0:objective.append(mvc_post/mvc_pre)
 if objective:
  ratio=min(objective);level=2 if ratio<=1-decline else 1 if ratio<.85 else 0
  return Label(1 if level else 0,level,'objective force/MVC decline; task-demand confounding must be reviewed',False)
 if borg is not None:return Label(1 if borg>=5 else 0,2 if borg>=7 else 1 if borg>=5 else 0,'Borg CR10 self-report',False)
 if time_ratio is not None:
  if time_ratio>1:raise ValueError('invalid time fraction')
  if .3<time_ratio<.7:return Label(-1,1,'WEAK intermediate ordinal target; binary interval excluded',True)
  return Label(int(time_ratio>=.7),2 if time_ratio>=.7 else 0,'WEAK time-in-task proxy; not ground truth',True)
 return Label(-1,-1,'no admissible fatigue evidence',True)


def write_weak_window_labels(root):
 """Ordinal labels are an engineering prior, never a command to force-stop a patient."""
 import csv
 import numpy as np
 from pathlib import Path
 from .data import zenodo_trials,dump
 root=Path(root);counts=[0,0,0];sensitivity=[];trials=list(zenodo_trials(root))
 with (root/'data_interim/PROTOTYPE_PROXY_window_labels.csv').open('w',newline='',encoding='utf-8') as f:
  w=csv.writer(f);w.writerow(['subject_id','trial_id','window_end_s','binary','ordinal_weak','label_evidence'])
  for d in trials:
   for end in d['t'][49::10]:
    if end<10:continue
    ratio=float(end/d['t'][-1]);q=label(time_ratio=ratio);counts[q.level]+=1;w.writerow([d['subject'],d['trial'],end,q.binary,q.level,q.evidence])
 for early,late in [(.2,.8),(.25,.75),(.3,.7)]:
  n=[0,0,0]
  for d in trials:
   for end in d['t'][49::10]:
    if end<10:continue
    ratio=end/d['t'][-1];n[0 if ratio<=early else 2 if ratio>=late else 1]+=1
  sensitivity.append(dict(early_ratio=early,late_ratio=late,stable=n[0],ambiguous_warning=n[1],late=n[2],weak=True))
 dump(root/'reports/label-sensitivity.json',sensitivity);dump(root/'reports/ordinal-label-balance.json',{'counts':counts,'weak':True,'interpretation':'ordinal time prior; no biological fatigue or clinical stop truth'})
