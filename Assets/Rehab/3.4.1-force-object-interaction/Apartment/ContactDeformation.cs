using System;
using UnityEngine;
namespace GripRehab {
// Bounded contact-local visual approximation; not a FEM or clinical material model.
public static class ContactDeformation {
 public static float SafeDt(float dt)=>float.IsFinite(dt)&&dt>0?Math.Min(dt,.05f):0;
 public static float Depth(float force,float full,float maximum,int contacts){
  if(!float.IsFinite(force)||force<=0||!float.IsFinite(full)||full<=0||
     !float.IsFinite(maximum)||maximum<=0||contacts<=0)return 0;
  // Full force belongs to the grasp, not independently to all 15 phalanges.
  return Math.Min(.5f,maximum)*Math.Min(1,force/full)*Math.Min(1,5f/contacts);
 }
 public static Vector3 Indentation(Vector3 vertex,Vector3 surface,Vector3 outward,float depth,float radius,Vector3 seed,float bulge){
  if(!GraspMotionPolicy.Finite(vertex)||!GraspMotionPolicy.Finite(surface)||!GraspMotionPolicy.Finite(outward)||
     !GraspMotionPolicy.Finite(seed)||!float.IsFinite(depth)||depth<=0||!float.IsFinite(radius)||radius<=0||
     outward.sqrMagnitude<1e-10f||!float.IsFinite(bulge))return new Vector3();
  float distance=Vector3.Distance(vertex,surface);
  if(distance>=radius)return new Vector3();
  float t=1-distance/radius,falloff=t*t*(3-2*t);
  float irregular=.89f+.11f*(float)Math.Sin(seed.x*29+seed.y*17+seed.z*23);
  Vector3 normal=outward.normalized;
  Vector3 tangent=Vector3.ProjectOnPlane(vertex-surface,normal)/radius;
  Vector3 delta=(normal*(-depth)+tangent*(depth*Math.Max(0,Math.Min(.3f,bulge))))*(falloff*irregular);
  return GraspMotionPolicy.SafeVelocity(delta,depth);
 }
 public static float GripDamage(ref float dwell,bool eligible,float force,float dt,float threshold,float gate,float capacity){
  dt=SafeDt(dt);
  if(!eligible||!float.IsFinite(force)||force<=0||!float.IsFinite(threshold)||threshold<=0||
     !float.IsFinite(gate)||gate<=0||!float.IsFinite(capacity)||capacity<=0||force<threshold){dwell=0;return 0;}
  if(dt==0)return 0;
  float previous=float.IsFinite(dwell)&&dwell>=0?dwell:0;
  dwell=previous+dt;
  float active=Math.Max(0,dwell-gate)-Math.Max(0,previous-gate);
  return active/Math.Max(.01f,gate*.5f*capacity);
 }
 public static float ImpactDamage(float mass,float speedSquared,float threshold){
  if(!float.IsFinite(mass)||mass<=0||!float.IsFinite(speedSquared)||speedSquared<0||
     !float.IsFinite(threshold)||threshold<=0)return 0;
  double energy=.5*(double)mass*speedSquared;
  return energy<threshold?0:(float)Math.Min(1,energy/threshold-.5);
 }
}
public sealed class LocalDeformationState {
 public readonly Vector3[] Rest,Vertices;
 readonly Vector3[] targets,permanent;readonly bool[] touched;
 public LocalDeformationState(Vector3[] rest){
  if(rest==null||Array.Exists(rest,v=>!GraspMotionPolicy.Finite(v)))throw new ArgumentException("Finite rest vertices required");
  Rest=(Vector3[])rest.Clone();Vertices=(Vector3[])rest.Clone();
  targets=new Vector3[rest.Length];permanent=new Vector3[rest.Length];touched=new bool[rest.Length];
 }
 public void BeginStep(){Array.Clear(targets,0,targets.Length);Array.Clear(touched,0,touched.Length);}
 public void SetTarget(int index,Vector3 displacement){
  if(!GraspMotionPolicy.Finite(displacement))return;
  if(displacement.sqrMagnitude>targets[index].sqrMagnitude)targets[index]=displacement;
  touched[index]|=displacement.sqrMagnitude>0;
 }
 public bool Step(float dt,float recovery,float plasticity){
  dt=ContactDeformation.SafeDt(dt);if(dt==0)return false;
  recovery=float.IsFinite(recovery)?Math.Max(.01f,recovery):8;
  plasticity=float.IsFinite(plasticity)?Math.Max(0,Math.Min(1,plasticity)):0;
  float attack=1-(float)Math.Exp(-32*dt),relax=1-(float)Math.Exp(-recovery*dt);
  bool changed=false;
  for(int i=0;i<Rest.Length;i++){
   if(touched[i]&&plasticity>0)permanent[i]=Vector3.Lerp(permanent[i],targets[i]*(plasticity*.6f),attack);
   Vector3 offset=touched[i]?targets[i]:permanent[i];
   if(touched[i]&&offset.sqrMagnitude<permanent[i].sqrMagnitude)offset=permanent[i];
   Vector3 goal=Rest[i]+offset;
   Vector3 next=Vector3.Lerp(Vertices[i],goal,touched[i]?attack:relax);
   // Snap AND upload the final recovery step. Never stop short of the goal.
   if((next-goal).sqrMagnitude<1e-12f)next=goal;
   if((next-Vertices[i]).sqrMagnitude>0){Vertices[i]=next;changed=true;}
  }
  return changed;
 }
 public void Reset(){Array.Copy(Rest,Vertices,Rest.Length);Array.Clear(permanent,0,permanent.Length);BeginStep();}
}
}
