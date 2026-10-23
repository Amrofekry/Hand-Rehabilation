"""Four-category, persisted-consecutive-window safety state machine (Python/C# parity)."""
from dataclasses import dataclass
import math
@dataclass
class Config:
 consecutive:int=3
 clear_windows:int=3
 high:float=.7
 low:float=.4
 stable_seconds:float=8
 cooldown:float=10
 max_level:int=5
 version:str="research-ai-1"
class Policy:
 def __init__(self,config=None):
  self.c=config or Config();self.state="active";self.level=1;self.since=None;self.changed=-1e30;self.last=-1e30;self.count=[0]*4;self.clear=0;self.signs=0;self.valid=False
 def step(self,now,valid,signals,stable=True):
  if not math.isfinite(now) or now<=self.last:raise ValueError("clock must increase")
  self.last=now
  if self.state=="stopped":return self.result("stop latched")
  self.valid=bool(valid) and len(signals)==4 and all(p is None or (math.isfinite(p) and 0<=p<=1) for p in signals)
  if not self.valid:
   self.count=[0]*4;self.clear=0;self.signs=0;self.since=None;self.level=max(1,self.level-1) if self.state!="warning" else self.level;self.state="warning";return self.result("invalid input or model; explicit resume required")
  for i,p in enumerate(signals):
   self.count[i]=0 if p is None or p<=self.c.low else min(self.c.consecutive,self.count[i]+1) if p>=self.c.high else self.count[i] if self.count[i]>=self.c.consecutive else 0
  self.signs=sum(1<<i for i,n in enumerate(self.count) if n>=self.c.consecutive)
  all_clear=all(p is None or p<=self.c.low for p in signals)
  self.clear=self.clear+1 if all_clear else 0
  if self.signs.bit_count()>1:self.state="stopped";self.since=None;return self.result("multiple independent signs")
  if self.signs:
   if self.state!="warning":self.level=max(1,self.level-1);self.changed=now
   self.state="warning";self.since=None;return self.result("one persistent sign")
  if self.state=="warning":return self.result("explicit resume required")
  if not stable or not all_clear:self.since=None;return self.result("difficulty held")
  if self.since is None:self.since=now
  if now-self.since>=self.c.stable_seconds and now-self.changed>=self.c.cooldown:
   self.level=min(self.c.max_level,self.level+1);self.since=self.changed=now
  return self.result("active")
 def resume(self):
  if self.state!="warning" or not self.valid or self.signs or self.clear<self.c.clear_windows:raise ValueError("fresh cleared windows required")
  self.state="active";self.changed=self.last;self.since=None;return self.result("resumed")
 def result(self,reason):return {"state":self.state,"level":self.level,"signs":self.signs,"reason":reason}
