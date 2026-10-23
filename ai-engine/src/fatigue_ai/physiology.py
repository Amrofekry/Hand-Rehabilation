"""Original POS-style rPPG implementation. No pyVHR code copied (GPL).
Wang et al., IEEE TBME 2017, doi:10.1109/TBME.2016.2609282.
Quality gating does not establish physiological accuracy. HRV is experimental.
"""
import numpy as np

def estimate_rppg(t,rgb,motion=None):
 t=np.asarray(t,dtype=float);rgb=np.asarray(rgb,dtype=float)
 bad={"valid":False,"hr_bpm":None,"rmssd_ms":None,"quality":0.,"reason":"insufficient/invalid signal"}
 if len(t)<200 or rgb.shape!=(len(t),3) or not np.isfinite(rgb).all() or not np.isfinite(t).all() or np.any(np.diff(t)<=0) or t[-1]-t[0]<15:return bad
 dt=np.diff(t);fs=1/np.median(dt)
 if fs<12 or dt.max()>.2 or np.std(dt)/np.mean(dt)>.3:return {**bad,"reason":"irregular capture"}
 if np.min(rgb)<5 or np.max(rgb)>250:return {**bad,"reason":"exposure/clipping"}
 if motion is not None and (not np.isfinite(motion).all() or np.mean(motion)>.04):return {**bad,"reason":"face motion"}
 uniform=np.arange(t[0],t[-1],1/fs);c=np.array([np.interp(uniform,t,rgb[:,i]) for i in range(3)]).T
 pulse=np.zeros(len(c));weight=np.zeros(len(c));size=int(1.6*fs)
 for start in range(0,len(c)-size+1):
  block=c[start:start+size];cn=block/np.maximum(block.mean(0),1e-6)-1
  s1=cn[:,1]-cn[:,2];s2=-2*cn[:,0]+cn[:,1]+cn[:,2]
  h=s1+np.std(s1)/max(np.std(s2),1e-8)*s2;pulse[start:start+size]+=h-h.mean();weight[start:start+size]+=1
 pulse/=np.maximum(weight,1);pulse-=pulse.mean();f=np.fft.rfftfreq(len(pulse),1/fs);spectrum=np.abs(np.fft.rfft(pulse*np.hanning(len(pulse))))**2
 band=(f>=.7)&(f<=3);total=spectrum[band].sum()
 if total<1e-12:return {**bad,"reason":"no pulsatile signal"}
 peak=f[np.flatnonzero(band)[np.argmax(spectrum[band])]];mask=band&(np.abs(f-peak)<.12);quality=float(spectrum[mask].sum()/total)
 if quality<.55:return {**bad,"quality":quality,"reason":"low spectral concentration"}
 # Sample-grid peak intervals, only with sufficient history. This is not
 # ECG-grade timing; camera frame rate limits the experimental HRV estimate.
 filtered=np.fft.irfft(np.fft.rfft(pulse)*band,len(pulse));peaks=[];minimum=int(fs*.30)
 for i in range(1,len(filtered)-1):
  if filtered[i]>filtered[i-1] and filtered[i]>=filtered[i+1] and (not peaks or i-peaks[-1]>=minimum):peaks.append(i)
 ibi=np.diff(peaks)/fs;ibi=ibi[(ibi>=.33)&(ibi<=1.5)]
 hrv=float(np.sqrt(np.mean(np.diff(ibi)**2))*1000) if t[-1]-t[0]>=60 and len(ibi)>=30 and quality>=.8 else None
 return {"valid":True,"hr_bpm":float(peak*60),"rmssd_ms":hrv,"quality":quality,"reason":"experimental POS estimate; not validated against ECG"}
