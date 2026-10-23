using UnityEngine;
namespace GripRehab {
public sealed class GripForceFilter {
 public float Value{get;private set;}
 float candidate;int candidateCount;bool initialized;
 public void Reset(){Value=candidate=0;candidateCount=0;initialized=false;}
 // A single isolated jump cannot reach damage; a repeated reading can.
 public float Push(float sample,float dt,float smoothingSeconds,float spikeNewtons,float maxSlewPerSecond){
  if(!float.IsFinite(sample)||sample<0){Reset();return 0;}
  sample=Mathf.Clamp(sample,0,981);
  if(!initialized){initialized=true;Value=sample;return Value;}
  if(Mathf.Abs(sample-Value)>spikeNewtons){
   if(candidateCount==0||Mathf.Abs(sample-candidate)>spikeNewtons*.25f){candidate=sample;candidateCount=1;return Value;}
   if(++candidateCount<2)return Value;
  }
  candidateCount=0;
  float alpha=1-Mathf.Exp(-Mathf.Max(.001f,dt)/Mathf.Max(.005f,smoothingSeconds));
  Value=Mathf.MoveTowards(Value,Mathf.Lerp(Value,sample,alpha),Mathf.Max(1,maxSlewPerSecond)*Mathf.Max(.001f,dt));
  return Value;
 }
}
}
