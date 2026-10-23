import json, math
from pathlib import Path
import numpy as np
import pytest
from fatigue_ai.data import validate_trial,preprocess,feature_sequence,windows,zenodo_trials,read_device_csv
from fatigue_ai.policy import Policy
from fatigue_ai.physiology import estimate_rppg
from fatigue_ai.train import split,prepare
ROOT=Path(__file__).resolve().parents[1]
@pytest.mark.parametrize('times,values,unit', [([0,0],[1,2],'N'),([0,1],[1,float('nan')],'N'),([0,1],[-1,1],'N'),([0,1],[1,2],'unknown'),([0,1],[1,1.1],'closure_ratio')])
def test_schema_rejects(times,values,unit):
 with pytest.raises(ValueError):validate_trial(times,values,unit)
def test_real_dataset():
 ds=list(zenodo_trials(ROOT));assert len(ds)==15
 assert all(d['unit']=='EMG_RMS_PROXY' and len(d['raw'].shape)==2 and d['raw'].shape[1]==8 for d in ds)
def test_causal_filter():
 t=np.arange(1000)/100;x=np.sin(t)*2+10;_,a=preprocess(t,x);x[700:]=1000;_,b=preprocess(t,x)
 np.testing.assert_allclose(a[:60],b[:60])
def test_gap():
 with pytest.raises(ValueError):preprocess([0,.1,1],[1,2,3])
def test_feature_physics():
 f=feature_sequence(np.full(50,10),20);assert f.shape==(50,8);assert f[-1,0]==.5
 assert abs(f[-1,2])<1e-6;assert f[-1,3]==0;assert f[-1,6]==.5
@pytest.mark.parametrize('mvc',[0,-1,float('nan'),float('inf')])
def test_bad_mvc(mvc):
 with pytest.raises(ValueError):feature_sequence(np.ones(50),mvc)
def test_subject_split():
 x,y,g,t,tr,summary=prepare(ROOT);ids,a=split(g)
 assert len(x)==len(y)==len(g)
 # The public early-reference interval must already exist at prediction time.
 assert t.min()>=10
 assert not(set(g[ids[0]])&set(g[ids[1]]) or set(g[ids[0]])&set(g[ids[2]]) or set(g[ids[1]])&set(g[ids[2]]))
 assert all(len(set(y[i]))==2 for i in ids)
def test_policy_warning_resume():
 p=Policy()
 for t in range(3):d=p.step(t,True,[.9,None,None,None])
 assert d['state']=='warning' and d['signs']==1
 with pytest.raises(ValueError):p.resume()
 for t in range(3,6):p.step(t,True,[.1,None,None,None])
 assert p.resume()['state']=='active'
def test_independence_latched_stop():
 p=Policy()
 for t in range(3):p.step(t,True,[.9,.9,None,None])
 assert p.state=='stopped'
 assert p.step(4,True,[.1]*4)['state']=='stopped'
 with pytest.raises(ValueError):p.resume()
@pytest.mark.parametrize('signals,valid',[([float('nan'),None,None,None],True),([.1]*4,False),([2,None,None,None],True)])
def test_invalid_fail_safe(signals,valid):
 assert Policy().step(0,valid,signals)['state']=='warning'
def test_low_quality_optional_ignored():
 p=Policy()
 for t in range(12):p.step(t,True,[.1,.1,None,None])
 assert p.state=='active' and p.level==2
@pytest.mark.parametrize('mutation',['motion','dropout','constant'])
def test_ppg_quality(mutation):
 t=np.arange(20*30)/30;v=np.sin(2*np.pi*1.2*t);rgb=np.column_stack([100+.1*v,120+.8*v,90+.05*v]);motion=np.zeros(len(t))
 if mutation=='motion':motion[:]=.1
 if mutation=='dropout':t[300:]+=1
 if mutation=='constant':rgb[:]=100
 assert not estimate_rppg(t,rgb,motion)['valid']
def test_SYNTHETIC_ppg_frequency():
 t=np.arange(30*30)/30;v=np.sin(2*np.pi*1.2*t);rgb=np.column_stack([100+.1*v,120+.8*v,90+.05*v]);out=estimate_rppg(t,rgb)
 assert out['valid'] and abs(out['hr_bpm']-72)<3 and out['rmssd_ms'] is None
def test_model_parity_and_leakage():
 p=json.loads((ROOT/'reports/training.json').read_text(encoding='utf-8'))
 assert p['provenance']=='PROTOTYPE-PROXY' and p['deployment_allowed_on_humans'] is False
 assert all(v['max_abs_diff']<1e-4 for v in p['exports'].values())
 assert 'GRU' in p['exports']['gru']['ops']
 assert not({'GRU','LSTM','Loop','Scan'} & set(p['exports']['cnn']['ops']))
 assert all(len(p['results'][k])==5 for k in ['gru','lstm','cnn'])


def test_labels_priority():
 from fatigue_ai.labels import label
 assert label(force_early=20,force_recent=10,borg=0,constant_target=True).binary==1
 assert not label(mvc_pre=30,mvc_post=29).weak
 assert label(borg=8).level==2
 assert label(time_ratio=.9).weak
 assert label().binary==-1

def test_nonconsecutive_windows_do_not_accumulate():
 p=Policy()
 for i in range(10):p.step(i,True,[.9 if i%2 else .5,None,None,None])
 assert p.state=='active'

def test_phase2_logger(tmp_path):
 from fatigue_ai.collect import convert
 rows=[{'schemaVersion':1,'kind':'sample','source':'synthetic','sequence':i,'sampleMs':i*100,'forceMilliNewtons':12345,'shapeId':1,'calibrationRevision':3,'connectionId':'synthetic','flags':3,'ageMs':20} for i in range(10)]
 raw=tmp_path/'SYNTHETIC_packets.jsonl';raw.write_text('\n'.join(json.dumps(row) for row in rows),encoding='utf-8');out=tmp_path/'SYNTHETIC_capture.csv'
 assert convert(raw,out,'SYNTHETIC_p1','trial','left',20)==10
 parsed=list(read_device_csv(out));assert parsed[0]['synthetic'] and parsed[0]['x'][0]==12.345
 with pytest.raises(ValueError):convert(raw,tmp_path/'bad.csv','SYNTHETIC_p1','trial','left',20)

def test_missing_device_labels_rejected(tmp_path):
 import pandas as pd
 frame=pd.DataFrame([{'subject_id':f's{i//10}','trial_id':'a','shape':'sphere','hand':'right','timestamp':(i%10)/10,'force_N':2,'synthetic':False,'calibration_revision':1,'mvc_N':10} for i in range(80)])
 p=tmp_path/'no_labels.csv';frame.to_csv(p,index=False)
 with pytest.raises(ValueError):prepare(ROOT,p)
