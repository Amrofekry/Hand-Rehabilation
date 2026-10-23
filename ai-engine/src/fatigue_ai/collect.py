"""Explicit pseudonymous Phase-2 JSON replay -> own-data CSV. No camera/images.
Input JSONL is produced by the existing BLE bridge/monitor; firmware is unchanged.
"""
import argparse,csv,json,math
from pathlib import Path
FIELDS=['subject_id','trial_id','shape','hand','timestamp','force_N','strain','fatigue_rating','fatigue_label','synthetic','calibration_revision','mvc_N']
def convert(source,out,subject,trial,hand,mvc=None):
 if not subject or not trial or hand not in ('left','right'):raise ValueError('pseudonymous identity required')
 last=-1;stamp=-1;identity=None;rows=[]
 for line in Path(source).read_text(encoding='utf-8').splitlines():
  p=json.loads(line)
  if p.get('schemaVersion')==1:
   if p.get('kind')!='sample':continue
   if p.get('flags',0)&3!=3 or p.get('ageMs',1000)>=500 or p.get('ageMs',-1)<0:raise ValueError('uncalibrated or stale bridge sample')
   p={**p,'timestamp':p['sampleMs']/1000,'force_N':p['forceMilliNewtons']/1000,'shape':{1:'sphere',2:'cylinder',3:'pencil'}.get(p['shapeId'],'invalid'),'calibration_revision':p['calibrationRevision'],'synthetic':p['source']!='hardware'}
  seq=p['sequence'];t=float(p['timestamp']);f=float(p['force_N']);key=(p['shape'],p['calibration_revision'],p.get('connectionId',''))
  if not(math.isfinite(t) and math.isfinite(f)) or f<0 or t<=stamp or seq<=last:raise ValueError('invalid, duplicate or stale sequence')
  if identity and key!=identity:raise ValueError('split trials on shape/calibration change')
  if p.get('synthetic') and not Path(out).name.startswith('SYNTHETIC_'):raise ValueError('synthetic output prefix required')
  rows.append([subject,trial,p['shape'],hand,t,f,p.get('strain',''),p.get('borg_cr10',''),p.get('fatigue_label',''),bool(p.get('synthetic')),p['calibration_revision'],mvc or ''])
  identity=key;stamp=t;last=seq
 with Path(out).open('w',newline='',encoding='utf-8') as stream:
  writer=csv.writer(stream);writer.writerow(FIELDS);writer.writerows(rows)
 return len(rows)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('input');p.add_argument('output');p.add_argument('--subject',required=True);p.add_argument('--trial',required=True);p.add_argument('--hand',choices=['left','right'],required=True);p.add_argument('--mvc',type=float);args=p.parse_args();convert(args.input,args.output,args.subject,args.trial,args.hand,args.mvc)
