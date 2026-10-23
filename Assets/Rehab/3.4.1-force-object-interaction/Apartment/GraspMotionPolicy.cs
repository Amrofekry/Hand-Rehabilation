using UnityEngine;
namespace GripRehab {
// Geometry/release policy shared by gameplay and files-only tests.
// Monocular pose changes are not calibrated throw measurements.
public static class GraspMotionPolicy {
 public static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
 public static Vector3 SafeVelocity(Vector3 velocity,float maximumSpeed){
  if(!Finite(velocity)||!float.IsFinite(maximumSpeed)||maximumSpeed<=0)return new Vector3();
  double length=System.Math.Sqrt((double)velocity.x*velocity.x+(double)velocity.y*velocity.y+(double)velocity.z*velocity.z);
  return length>maximumSpeed?velocity*(float)(maximumSpeed/length):velocity;
 }
 public static Vector3 ReleaseVelocity(Vector3 previous,Vector3 current,float dt,float gain,float maximumSpeed,float maximumStep){
  if(!Finite(previous)||!Finite(current)||!float.IsFinite(dt)||dt<=0||!float.IsFinite(gain)||gain<0||
     !float.IsFinite(maximumStep)||maximumStep<=0)return new Vector3();
  Vector3 delta=current-previous;
  if(!Finite(delta)||delta.sqrMagnitude>maximumStep*maximumStep)return new Vector3();
  return SafeVelocity(delta*(gain/dt),maximumSpeed);
 }
 public static float AimScore(Vector3 origin,Vector3 forward,Vector3 surface,float reach,float coneDegrees){
  if(!Finite(origin)||!Finite(forward)||!Finite(surface)||forward.sqrMagnitude<1e-10f||
     !float.IsFinite(reach)||reach<=0||!float.IsFinite(coneDegrees)||coneDegrees<=0)return float.PositiveInfinity;
  Vector3 delta=surface-origin;float distance=Vector3.Distance(origin,surface);
  float angle=Vector3.Angle(forward,delta);
  if(!float.IsFinite(distance)||distance>reach||Vector3.Dot(delta,forward)<=0||angle>coneDegrees)return float.PositiveInfinity;
  return angle/coneDegrees+distance/reach*.05f;
 }
 public static bool Separated(Vector3 aMin,Vector3 aMax,Vector3 bMin,Vector3 bMax,float margin){
  if(!Finite(aMin)||!Finite(aMax)||!Finite(bMin)||!Finite(bMax)||!float.IsFinite(margin)||margin<0)return false;
  if(aMin.x>aMax.x||aMin.y>aMax.y||aMin.z>aMax.z||bMin.x>bMax.x||bMin.y>bMax.y||bMin.z>bMax.z)return false;
  return aMax.x+margin<bMin.x||aMin.x-margin>bMax.x||aMax.y+margin<bMin.y||aMin.y-margin>bMax.y||aMax.z+margin<bMin.z||aMin.z-margin>bMax.z;
 }
 public static Quaternion RelativeRotation(Quaternion palm,Quaternion item)=>Quaternion.Inverse(palm)*item;
 public static Quaternion HeldRotation(Quaternion palm,Quaternion relative)=>palm*relative;
}
}
