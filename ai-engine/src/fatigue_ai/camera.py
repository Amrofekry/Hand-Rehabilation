"""Optional local face/physiology processing on the EXISTING hand-camera frames.
A single latest-frame slot prevents backlog. No raw frame is written or transmitted.
Independent per-session opt-ins are read from a local consent file; absent = OFF.
"""
import json,math,socket,threading,time,uuid
from collections import deque
from pathlib import Path
import numpy as np
from .physiology import estimate_rppg
class ResearchCamera:
 def __init__(self,root):
  self.root=Path(root);self.startedUnix=time.time();self.path=self.root/'Logs/ai-camera-consent.json';self.pending=None;self.closed=False;self.reset=False;self.lock=threading.Lock();self.wake=threading.Event();self.samples=deque(maxlen=2000);self.detector=None;self.emotion=None;self.baseline=[];self.opt=(False,False);self.previous=None;self.next_config=0;self.next_frame=0;self.seq=0;self.session_id=uuid.uuid4().hex;self.sock=socket.socket(socket.AF_INET,socket.SOCK_DGRAM);self.thread=threading.Thread(target=self._run,daemon=True,name='Local consented face/rPPG');self.thread.start()
 def submit(self,frame,captured):
  if captured<self.next_frame:return
  self.next_frame=captured+1/15
  now=time.time()
  previous_opt=self.opt
  if captured>=self.next_config:
   self.next_config=captured+.5
   try:
    m=json.loads(self.path.read_text(encoding='utf-8'));self.opt=(m.get('face') is True,m.get('physiology') is True) if now-m.get('updatedUnix',0)<3 and now>=m.get('updatedUnix',0)>=self.startedUnix else (False,False)
   except (OSError,ValueError,TypeError):self.opt=(False,False)
  # Revoking either modality invalidates its previous sampling history, even
  # when the other consent remains ON. The worker owns all signal state.
  if self.opt!=previous_opt:
   with self.lock:self.pending=None;self.reset=True
  if not any(self.opt):
   with self.lock:self.pending=None;self.reset=True
   self.wake.set();return
  with self.lock:self.pending=(frame.copy(),captured,self.opt)
  self.wake.set()
 def close(self):
  self.closed=True;self.wake.set();self.thread.join(timeout=2);self.sock.close()
 def _clear_signal_history(self):
  # Worker-owned: a new face or opt-in must earn its own pulse warm-up.
  self.samples.clear();self.baseline.clear();self.previous=None
  if hasattr(self,'last_ppg'):del self.last_ppg
 def _run(self):
  import cv2
  while not self.closed:
   self.wake.wait(.25);self.wake.clear()
   with self.lock:item=self.pending;self.pending=None;reset=self.reset;self.reset=False
   if reset:self._clear_signal_history()
   if item is None or not any(self.opt):continue
   frame,captured,opt=item
   if opt!=self.opt:continue
   try:
    if self.detector is None:
     self.detector=cv2.FaceDetectorYN.create(str(self.root/'models/face_detection_yunet_2023mar.onnx'),'',(frame.shape[1],frame.shape[0]),.85,.3,1000)
    self.detector.setInputSize((frame.shape[1],frame.shape[0]));_,faces=self.detector.detect(frame)
    packet={'version':'aux-1','sessionId':self.session_id,'sequence':self.seq,'ageMs':0,'faceValid':False,'physiologyValid':False,'emotion':-1,'emotionConfidence':0.,'strain':0.,'hr':0.,'rmssd':0.,'hrvValid':False,'quality':0.};self.seq+=1
    if faces is None or len(faces)!=1:
     self._clear_signal_history()
    else:
     face=faces[0];x,y,w,h=face[:4].astype(int);height,width=frame.shape[:2]
     if x<0 or y<0 or x+w>width or y+h>height or w<70 or h<70:
      self._clear_signal_history();continue
     center=np.array([(x+w/2)/width,(y+h/2)/height]);motion=0 if self.previous is None else float(np.linalg.norm(center-self.previous));self.previous=center
     if opt[0]:
      if self.emotion is None:self.emotion=cv2.dnn.readNet(str(self.root/'models/facial_expression_recognition_mobilefacenet_2022july.onnx'))
      # Similarity alignment to the model's documented five-point face template.
      points=face[4:14].reshape(5,2);target=np.array([[38.2946,51.6963],[73.5318,51.5014],[56.0252,71.7366],[41.5493,92.3655],[70.7299,92.2041]],np.float32)
      transform,_=cv2.estimateAffinePartial2D(points.astype(np.float32),target,method=cv2.LMEDS)
      if transform is not None:
       image=cv2.warpAffine(frame,transform,(112,112));blob=cv2.dnn.blobFromImage(image,1/127.5,(112,112),(127.5,127.5,127.5),swapRB=True)
       self.emotion.setInput(blob);raw=self.emotion.forward().reshape(-1).astype(float)
       if len(raw)!=7 or not np.isfinite(raw).all():raise ValueError('bad expression output')
       probabilities=np.exp(raw-raw.max());probabilities/=probabilities.sum();label=int(probabilities.argmax());confidence=float(probabilities[label]);strain=float(probabilities[[0,1,2,5]].sum())
       packet.update(faceValid=confidence>=.65 and motion<.05,emotion=label,emotionConfidence=confidence,strain=strain)
     if opt[1]:
      # Inner cheek/forehead patch avoids eyes and mouth; no facial identity stored.
      roi=frame[y+int(.18*h):y+int(.48*h),x+int(.25*w):x+int(.75*w)]
      mean=roi.mean((0,1))[::-1];self.samples.append((captured,mean,motion))
      # Estimation 1Hz, broadcast latest result 5Hz with current frame freshness.
      if not hasattr(self,'last_ppg') or captured-getattr(self,'ppg_at',0)>=1:
       self.ppg_at=captured;rows=list(self.samples);self.last_ppg=estimate_rppg([q[0] for q in rows],np.asarray([q[1] for q in rows]),[q[2] for q in rows])
      p=self.last_ppg;packet.update(physiologyValid=p['valid'],hr=p['hr_bpm'] or 0,rmssd=p['rmssd_ms'] or 0,hrvValid=p['rmssd_ms'] is not None,quality=p['quality'])
    packet['sentUnix']=time.time()
    packet['ageMs']=max(0,(time.monotonic()-captured)*1000)
    if packet['ageMs']<500:self.sock.sendto(json.dumps(packet,allow_nan=False).encode(),('127.0.0.1',27820))
   except Exception:
    self._clear_signal_history()
    # Silent unavailable modality cannot become a positive/negative fatigue sign.
    # Status is surfaced as unavailable by Unity; no repeated log flood.
