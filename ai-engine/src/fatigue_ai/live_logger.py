"""Consented pseudonymous collection adapter, no firmware changes.
Run existing Phase2 unity_bridge.py --port27821, then this listener.
Optional forwarding preserves the game input on27818; replay stays synthetic.
"""
import argparse,csv,json,math,socket,time
from pathlib import Path
from .collect import FIELDS

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('output',type=Path);p.add_argument('--subject',required=True);p.add_argument('--trial',required=True);p.add_argument('--hand',choices=['left','right'],required=True);p.add_argument('--mvc',type=float,required=True);p.add_argument('--port',type=int,default=27821);p.add_argument('--seconds',type=float,default=60);p.add_argument('--forward-unity',action='store_true');args=p.parse_args()
 if args.mvc<=0 or not math.isfinite(args.mvc) or args.seconds<=0 or not 1024<=args.port<=65535:raise ValueError('invalid capture settings')
 if args.output.exists():raise FileExistsError('refusing to overwrite captured data')
 args.output.parent.mkdir(parents=True,exist_ok=True);started=time.monotonic();last=-1;identity=None;stamp=-1;accepted=rejected=0
 with socket.socket(socket.AF_INET,socket.SOCK_DGRAM) as s,args.output.open('w',encoding='utf-8',newline='') as out:
  s.bind(('127.0.0.1',args.port));s.settimeout(.25);writer=csv.writer(out);writer.writerow(FIELDS)
  while time.monotonic()-started<args.seconds:
   try:raw,addr=s.recvfrom(8192)
   except socket.timeout:continue
   try:
    b=json.loads(raw)
    if addr[0]!='127.0.0.1' or b.get('schemaVersion')!=1:raise ValueError('unsupported local bridge envelope')
    if b.get('kind')!='sample':continue
    seq=int(b['sequence']);at=b['sampleMs']/1000.;force=b['forceMilliNewtons']/1000.;synthetic=b.get('source')!='hardware'
    key=(b['connectionId'],b['calibrationRevision'],b['shapeId'])
    if seq<=last or at<=stamp or not math.isfinite(force) or force<0 or b.get('flags',0)&3!=3 or not 0<=b.get('ageMs',1000)<500:raise ValueError('invalid force, sequence, calibration or age')
    if synthetic and not args.output.name.startswith('SYNTHETIC_'):raise ValueError('SYNTHETIC_ output required')
    if identity is not None and identity!=key:raise RuntimeError('connection/calibration/shape changed; split the trial')
    shape={1:'sphere',2:'cylinder',3:'pencil'}[b['shapeId']]
    writer.writerow([args.subject,args.trial,shape,args.hand,at,force,'','','',synthetic,b['calibrationRevision'],args.mvc]);out.flush()
    last=seq;stamp=at;identity=key;accepted+=1
    if args.forward_unity:s.sendto(raw,('127.0.0.1',27818))
   except (ValueError,KeyError,TypeError):rejected+=1
 args.output.with_suffix('.capture.json').write_text(json.dumps({'subject_id':args.subject,'trial_id':args.trial,'accepted':accepted,'rejected':rejected,'elapsed_s':time.monotonic()-started,'labels':'unlabelled; obtain independent evidence before training'},indent=2),encoding='utf-8')
if __name__=='__main__':main()
