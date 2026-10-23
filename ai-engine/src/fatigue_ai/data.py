"""Verified acquisition, strict trial schema and causal preprocessing (WBS 4.2)."""
import hashlib, io, json, os, time, urllib.request, zipfile
from pathlib import Path
import numpy as np
from scipy.signal import butter, sosfilt, resample_poly
SOURCES = {
 "oxford": {"url":"https://data.mrc.ox.ac.uk/data-set/investigating-motor-fatigue-using-force-emg-and-kinematic-data", "license":"CC-BY-SA-4.0", "kind":"force", "access":"registration required; no credentials requested"},
 "zenodo": {"url":"https://zenodo.org/api/records/5189275", "license":"CC-BY-4.0", "kind":"EMG-PROXY", "doi":"10.5281/zenodo.5189275"},
 "mendeley": {"url":"https://data.mendeley.com/datasets/8j2p29hnbv/1", "license":"CC-BY-4.0", "kind":"EMG-PROXY", "access":"adapter prepared; not included in this experiment"}}
FEATURES = ["pct_mvc", "mean", "slope", "std", "cv", "peak_decay", "work", "fatigue_index"]
def dump(path, obj):
 path=Path(path);path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(obj,indent=2,allow_nan=False),encoding="utf-8")
def fetch(url, path, log, expected_md5=None):
 os.environ.pop("SSLKEYLOGFILE",None);path=Path(path);path.parent.mkdir(parents=True,exist_ok=True)
 record={"url":url,"accessed_utc":time.strftime("%Y-%m-%dT%H:%M:%SZ",time.gmtime())}
 try:
  if not path.exists():
   with urllib.request.urlopen(url,timeout=90) as response,path.with_suffix(path.suffix+".part").open("wb") as out:
    while chunk:=response.read(1024*1024):out.write(chunk)
   path.with_suffix(path.suffix+".part").replace(path)
  b=path.read_bytes();record.update(status="verified_local_or_downloaded",bytes=len(b),sha256=hashlib.sha256(b).hexdigest())
  if expected_md5 and hashlib.md5(b).hexdigest()!=expected_md5:raise ValueError("dataset checksum mismatch")
 except Exception as e:record.update(status="failed",error=str(e));raise
 finally:
  with Path(log).open("a",encoding="utf-8") as out:out.write(json.dumps(record)+"\n")
 return path

def acquire(root):
 root=Path(root);raw=root/"data_raw";raw.mkdir(exist_ok=True);log=root/"reports/acquisition.jsonl";log.parent.mkdir(exist_ok=True)
 meta=json.loads(fetch(SOURCES["zenodo"]["url"],raw/"zenodo.json",log).read_text(encoding="utf-8"))
 if meta["metadata"]["license"]["id"]!="cc-by-4.0":raise ValueError("license changed; review before use")
 f=meta["files"][0];fetch(f["links"]["self"],raw/"zenodo5189275.zip",log,f["checksum"].split(":")[-1])
 fetch(SOURCES["oxford"]["url"],raw/"oxford-access.html",log)
 dump(root/"reports/sources.json",SOURCES)
 write_unified(root)

def validate_trial(t, signal, unit):
 t=np.asarray(t,dtype=float);x=np.asarray(signal,dtype=float)
 if unit not in {"N","EMG_RMS_PROXY","closure_ratio"}:raise ValueError("unknown unit")
 if t.ndim!=1 or x.shape!=t.shape or len(t)<2 or not np.isfinite(t).all() or not np.isfinite(x).all():raise ValueError("invalid trial dimensions/NaN")
 if np.any(np.diff(t)<=0) or np.any(x<0):raise ValueError("nonmonotonic time or negative effort")
 if unit=="closure_ratio" and np.any(x>1):raise ValueError("closure out of range")
 return t,x

def preprocess(t,x,hz=10):
 t,x=validate_trial(t,x,"N")
 dt=np.median(np.diff(t));fs=1/dt
 if np.max(np.diff(t))>.5:raise ValueError("sensor gap >=500 ms; split the trial")
 if fs<hz*.9:raise ValueError("insufficient sample rate")
 # Causal clipping/filtering; a future held-out observation never alters earlier data.
 smooth=np.empty_like(x);history=[]
 for i,v in enumerate(x):
  if len(history)>=5:
   med=np.median(history[-11:]);mad=np.median(np.abs(np.asarray(history[-11:])-med))
   v=np.clip(v,med-max(6*mad,1e-6),med+max(6*mad,1e-6)) if mad>0 else v
  smooth[i]=v;history.append(float(v))
 filt=sosfilt(butter(3,min(3,fs*.35),fs=fs,output="sos"),smooth) if fs>10.5 else smooth
 ts=np.arange(t[0],t[-1]+1e-8,1/hz)
 return ts,np.interp(ts,t,filt)

def feature_sequence(values,mvc,hz=10):
 x=np.asarray(values,dtype=float)
 if x.ndim!=1 or not np.isfinite(x).all() or np.any(x<0) or not np.isfinite(mvc) or mvc<=0:raise ValueError("invalid MVC/effort")
 q=x/mvc;out=np.empty((len(q),len(FEATURES)),dtype=np.float32)
 early=max(float(np.mean(q[:min(10,len(q))])),1e-6)
 for i in range(len(q)):
  early=max(float(np.mean(q[:min(i+1,10)])),1e-6)
  v=q[max(0,i-9):i+1];mean=float(v.mean());std=float(v.std());n=len(v)
  slope=0 if n<2 else float(np.dot(np.arange(n)-(n-1)/2,v-mean)/(n*(n*n-1)/12)*hz)
  out[i]=[q[i],mean,slope,std,std/max(mean,1e-6),max(0,1-float(v.max())/early),float(v.sum())/hz,max(0,1-mean/early)]
 return out

def windows(t,x,mvc,seconds=5,stride=1):
 f=feature_sequence(x,mvc);size=int(seconds*10)
 return [(t[i+size-1],f[i:i+size]) for i in range(0,len(f)-size+1,int(stride*10))]

def zenodo_trials(root):
 """No fabricated force: raw ADC counts -> causal RMS envelope, arbitrary units."""
 with zipfile.ZipFile(Path(root)/"data_raw/zenodo5189275.zip") as z:
  for name in sorted(z.namelist()):
   if "/sub" not in name or not name.endswith(".txt"):continue
   raw=np.loadtxt(io.BytesIO(z.read(name)))
   if raw.ndim!=2 or raw.shape[1]!=8 or not np.isfinite(raw).all():raise ValueError("Unexpected Zenodo layout")
   signal=raw.astype(float);fs=200
   # The input is already notch filtered according to the dataset's README.
   rect=np.mean(signal**2,axis=1)
   energy=sosfilt(butter(3,3,fs=fs,output="sos"),rect)
   effort=np.sqrt(np.maximum(energy,0))[::20];t=np.arange(len(effort))/10
   yield {"subject":Path(name).stem,"trial":"6kg-elbow","source":"Zenodo-5189275","unit":"EMG_RMS_PROXY","t":t,"x":effort,"raw":raw,"fs":fs}

def read_device_csv(path):
 import pandas as pd
 df=pd.read_csv(path)
 required={"subject_id","trial_id","shape","hand","timestamp","force_N","synthetic","calibration_revision"}
 if not required.issubset(df.columns):raise ValueError("missing device schema columns: "+str(required-set(df.columns)))
 for (subject,trial),g in df.groupby(["subject_id","trial_id"],sort=False):
  if not str(subject).strip() or g["calibration_revision"].nunique()!=1 or g["shape"].nunique()!=1 or g["hand"].nunique()!=1:raise ValueError("identity changes within trial")
  t,x=validate_trial(g.timestamp.to_numpy(),g.force_N.to_numpy(),"N")
  synth=g.synthetic.astype(str).str.lower().isin(["true","1"]).any()
  if synth and not Path(path).name.startswith("SYNTHETIC_"):raise ValueError("Synthetic files must be prefixed SYNTHETIC_")
  yield {"subject":str(subject),"trial":str(trial),"source":"own-device" if not synth else "SYNTHETIC_OWN","unit":"N","t":t,"x":x,"synthetic":bool(synth),"mvc":float(g.mvc_N.iloc[0]) if "mvc_N" in g and str(g.mvc_N.iloc[0]) not in ("nan", "") else None,"labels":g.fatigue_label.to_numpy(dtype=float) if "fatigue_label" in g else None}

def read_oxford_xdf(path,force_channel,scale_newtons,subject,trial):
 # Channel and scale MUST come from the dataset's channel/calibration files.
 import pyxdf
 streams,header=pyxdf.load_xdf(str(path));stream=next(s for s in streams if s["info"]["name"][0]==force_channel[0])
 t=np.asarray(stream["time_stamps"]);x=np.asarray(stream["time_series"])[:,force_channel[1]]*scale_newtons
 validate_trial(t,x,"N");return {"subject":subject,"trial":trial,"source":"Oxford","unit":"N","t":t-t[0],"x":x}


def read_calibrated_mat(path,mapping,subject,trial):
 """External MAT layouts MUST supply explicit keys and unit conversion; no unit guessing."""
 from scipy.io import loadmat
 required={'time_key','force_key','scale_to_newtons','scale_time_to_seconds'}
 if not required.issubset(mapping):raise ValueError('explicit calibration and keys required')
 try:m=loadmat(path,squeeze_me=True)
 except NotImplementedError:
  import mat73;m=mat73.loadmat(path)
 t=np.asarray(m[mapping['time_key']],dtype=float).reshape(-1)*mapping['scale_time_to_seconds']
 x=np.asarray(m[mapping['force_key']],dtype=float).reshape(-1)*mapping['scale_to_newtons']
 validate_trial(t,x,'N');return {'subject':subject,'trial':trial,'source':'calibrated-public-MAT','unit':'N','t':t-t[0],'x':x}


def write_unified(root):
 import csv
 root=Path(root);summary=[]
 with (root/'data_interim/PROTOTYPE_PROXY_unified.csv').open('w',encoding='utf-8',newline='') as out:
  w=csv.writer(out);w.writerow(['subject_id','trial_id','source','intensity_pctMVC','t','signal_raw','signal_unit','pct_MVC','phase','self_report','quality_flag','effort_reference_ratio'])
  for d in zenodo_trials(root):
   base=float(np.percentile(d['x'][20:100],95))
   for t,v in zip(d['t'],d['x']):w.writerow([d['subject'],d['trial'],d['source'],'',t,v,'EMG_RMS_PROXY','','fatiguing_task','','PROXY_NO_GROUND_TRUTH',v/base])
   raw=d['raw'];summary.append(dict(subject=d['subject'],channels=8,input_hz=200,output_hz=10,samples_raw=len(raw),raw_min=raw.min(0).tolist(),raw_max=raw.max(0).tolist(),nan_count=int(np.isnan(raw).sum()),mvc_known=False))
 dump(root/'reports/channel-audit.json',summary)
