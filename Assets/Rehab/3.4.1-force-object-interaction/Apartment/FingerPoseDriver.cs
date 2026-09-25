using System;
using UnityEngine;
namespace GripRehab {
public sealed class FingerPoseDriver:MonoBehaviour {
 [Tooltip("Proximal, intermediate and distal joints for index/middle/ring/little, then thumb joints.")]
 public Transform[] joints;
 public Vector3 curlAxis=Vector3.right;
 public float curlSign=1;
 [Header("Tracking loss")] public bool holdLastTrackedPoseOnLoss=true;
 public Vector3 PalmDirection{get;private set;}
 public Vector3 PalmNormal{get;private set;}
 public float[] JointAngles=>_smoothed;
 private Quaternion[] _rest;private bool _everTracked;
 private readonly float[] _angles=new float[15];
 private readonly float[] _smoothed=new float[15];
 public bool TrackingUsed{get;private set;}
 public float Closure{get;private set;}
 public void UseDemoPose(){_everTracked=false;}
 private void Awake(){_rest=new Quaternion[joints.Length];for(int i=0;i<joints.Length;i++)_rest[i]=joints[i].localRotation;}
 public static float Bend(Vector3 a,Vector3 b,Vector3 c) {
  var first=b-a;var second=c-b;if(first.sqrMagnitude<1e-8f||second.sqrMagnitude<1e-8f)return 0;
  return Mathf.Clamp(Vector3.Angle(first,second),0,95);
 }
 public void Pose(LandmarkFrame frame,bool tracking,float demoClosure,float dt,float smoothing) {
  TrackingUsed=tracking&&HandKinematics.IsUsable(frame);
  if(TrackingUsed){_everTracked=true;PalmDirection=(frame.landmarks[9]-frame.landmarks[0]).normalized;PalmNormal=Vector3.Cross(frame.landmarks[5]-frame.landmarks[0],frame.landmarks[17]-frame.landmarks[0]).normalized;}
  if(!TrackingUsed&&_everTracked&&holdLastTrackedPoseOnLoss)return;
  float total=0;
  for(int finger=0;finger<5;finger++)for(int joint=0;joint<3;joint++) {
   int n=finger*3+joint;
   if(TrackingUsed) {
    int start=finger<4?5+finger*4:1;
    int center=start+joint;
    _angles[n]=Bend(frame.landmarks[joint==0?0:center-1],frame.landmarks[center],frame.landmarks[center+1]);
   } else _angles[n]=Mathf.Clamp01(demoClosure)*(joint==0?65:joint==1?85:55);
   _smoothed[n]=Mathf.Lerp(_smoothed[n],_angles[n],1-Mathf.Exp(-dt/Mathf.Max(.02f,smoothing)));
   if(n<joints.Length&&joints[n]!=null) joints[n].localRotation=_rest[n]*Quaternion.AngleAxis(_smoothed[n]*curlSign,curlAxis);
   total+=_smoothed[n]/90;
  }
  Closure=total/15;
 }
}
}
