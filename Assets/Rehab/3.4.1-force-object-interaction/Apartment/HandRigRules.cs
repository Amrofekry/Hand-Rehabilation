using System;
using UnityEngine;
namespace GripRehab {
// Gleechi rig: SDK 0=thumb; our channels are index/middle/ring/little/thumb.
public static class HandRigRules {
 public const int JointCount=15, SdkBoneCount=16;
 static readonly string[] fingers={"Thumb","Index","Middle","Ring","Pinky"};
 public static int SdkFinger(int channel){if(channel<0||channel>=5)throw new ArgumentOutOfRangeException(nameof(channel));return (channel+1)%5;}
 public static string BoneName(int sdkBone,bool left){
  if(sdkBone<0||sdkBone>=SdkBoneCount)throw new ArgumentOutOfRangeException(nameof(sdkBone));
  var suffix=left?"_L":"_R";
  return sdkBone==0?"Wrist"+suffix:fingers[(sdkBone-1)/3]+(char)('A'+(sdkBone-1)%3)+suffix;
 }
 public static float Unit(float value)=>float.IsFinite(value)?Mathf.Clamp01(value):0;
 public static float DemoLimit(int finger,int joint)=>joint==0?(finger==4?45:65):joint==1?85:55;
 public static float SafeDt(float dt)=>float.IsFinite(dt)&&dt>0?Math.Min(.1f,dt):0;
 public static bool Finite(Vector3 value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z);
 public static bool TryRotation(Quaternion value,out Quaternion unit){
  unit=Quaternion.identity;
  if(!float.IsFinite(value.x)||!float.IsFinite(value.y)||!float.IsFinite(value.z)||!float.IsFinite(value.w))return false;
  double length=(double)value.x*value.x+(double)value.y*value.y+(double)value.z*value.z+(double)value.w*value.w;
  if(length<1e-12)return false;
  double inverse=1/Math.Sqrt(length);
  unit=new Quaternion((float)(value.x*inverse),(float)(value.y*inverse),(float)(value.z*inverse),(float)(value.w*inverse));return true;
 }
 public static Vector3 Axis(Vector3 value,Vector3 fallback)=>Finite(value)&&value.sqrMagnitude>1e-8f&&float.IsFinite(value.sqrMagnitude)?value.normalized:fallback;
}

/// <summary>
/// One Euro filter (Casiez, Roussel and Vogel, CHI 2012) for one value: strong smoothing when the hand is still (low
/// jitter), little when it moves fast (low lag). minCutoff (Hz) sets jitter at rest; beta raises the cutoff with speed
/// (units per second); dCutoff filters the speed estimate. No allocation; a bad sample or time step resets it.
/// </summary>
public struct OneEuroFilter {
 public float minCutoff,beta,dCutoff;float x,dx;bool primed;
 public OneEuroFilter(float minCutoff,float beta,float dCutoff=1f){this.minCutoff=minCutoff;this.beta=beta;this.dCutoff=dCutoff;x=dx=0;primed=false;}
 public bool Primed=>primed;
 public void Reset(){primed=false;x=dx=0;}
 static float Alpha(float cutoff,float dt){float tau=1f/(2f*(float)Math.PI*Math.Max(1e-3f,cutoff));return 1f/(1f+tau/dt);}
 public float Step(float value,float dt){
  if(!float.IsFinite(value))return primed?x:0;
  if(!primed||!(dt>0)||!float.IsFinite(dt)){x=value;dx=0;primed=true;return x;}
  dt=Math.Min(dt,.1f);
  float speed=(value-x)/dt;dx+=Alpha(dCutoff,dt)*(speed-dx);
  x+=Alpha(minCutoff+beta*Math.Abs(dx),dt)*(value-x);
  return x;
 }
}
}
