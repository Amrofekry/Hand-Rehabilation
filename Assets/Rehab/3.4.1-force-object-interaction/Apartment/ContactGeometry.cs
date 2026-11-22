using System;
using UnityEngine;
namespace GripRehab {
// Stage 8: pure contact geometry for the hand-object solver. Only Vector3/Mathf, so the
// files-only harness can test it without PhysX. Distances are metres; positive depth means
// the finger capsule overlaps the object, negative depth is the remaining gap.
/// <summary>Overlap (positive) or gap (negative) of a sphere with an object. A struct implementation keeps the per-frame solver allocation-free.</summary>
public interface ISphereDepth { float Depth(Vector3 centre,float radius); }
/// <summary>Places a joint at the angle and returns the resulting depth.</summary>
public interface IAngleDepth { float Depth(float angle); }

public static class ContactGeometry {
 struct SphereFunc:ISphereDepth{public Func<Vector3,float,float> f;public float Depth(Vector3 c,float r)=>f(c,r);}
 struct AngleFunc:IAngleDepth{public Func<float,float> f;public float Depth(float a)=>f(a);}

 public const float SkinTolerance=.002f, MaxHoldCorrection=.07f, DeadBand=.0015f;

 /// <summary>Deepest overlap (positive) or smallest gap (negative) of a capsule a-b of the given radius.</summary>
 /// <param name="sphereDepth">Overlap (positive) or gap (negative) of a sphere (centre, radius) with the object;
 /// for a centre inside the object it must return the true depth, not a capped value.</param>
 public static float CapsuleDepth(Vector3 a,Vector3 b,float radius,Func<Vector3,float,float> sphereDepth,int samples=3){
  if(sphereDepth==null)return float.NegativeInfinity;
  var depth=new SphereFunc{f=sphereDepth};return CapsuleDepth(a,b,radius,ref depth,samples);
 }
 public static float CapsuleDepth<T>(Vector3 a,Vector3 b,float radius,ref T sphereDepth,int samples=3) where T:struct,ISphereDepth{
  if(!GraspMotionPolicy.Finite(a)||!GraspMotionPolicy.Finite(b)||!float.IsFinite(radius)||radius<=0)return float.NegativeInfinity;
  samples=Math.Max(2,Math.Min(8,samples));
  float deepest=float.NegativeInfinity;
  for(int i=0;i<samples;i++){
   float depth=sphereDepth.Depth(Vector3.Lerp(a,b,i/(float)(samples-1)),radius);
   if(float.IsFinite(depth)&&depth>deepest)deepest=depth;
  }
  return deepest;
 }

 /// <summary>
 /// Largest curl angle in [0, target] whose overlap stays within tolerance. depthAt(angle)
 /// must place the joint at that angle and return the resulting depth. When even the open
 /// joint overlaps (the object was placed inside the finger), the open angle is returned.
 /// </summary>
 /// <param name="minimum">Most extended angle allowed (negative = extension), so a finger already inside can back out.</param>
 public static float LimitAngle(float target,Func<float,float> depthAt,float tolerance,int iterations=7,float minimum=0){
  if(depthAt==null)return 0;
  var depth=new AngleFunc{f=depthAt};return LimitAngle(target,ref depth,tolerance,iterations,minimum);
 }
 public static float LimitAngle<T>(float target,ref T depthAt,float tolerance,int iterations=7,float minimum=0) where T:struct,IAngleDepth{
  if(!float.IsFinite(minimum)||minimum>0)minimum=0;
  if(!float.IsFinite(target))return 0;
  float low=Math.Min(minimum,target),high=target;
  if(depthAt.Depth(low)>tolerance)return low;
  // The first contact along the closing sweep (2026-11-22): a finger closing on a thin object (a book) could pass it and
  // end clear of it behind; checking only the final angle accepted that, so the pads never touched. Six steps, then bisect.
  bool hit=false;float previous=low;
  for(int k=1;k<=6;k++){float a=low+(target-low)*k/6f;if(depthAt.Depth(a)>tolerance){low=previous;high=a;hit=true;break;}previous=a;}
  if(!hit)return target;
  for(int i=0;i<Math.Max(1,iterations);i++){
   float middle=(low+high)*.5f;
   if(depthAt.Depth(middle)<=tolerance)low=middle;else high=middle;
  }
  return low;
 }

 /// <summary>
 /// Moves the held object's offset toward the finger pads when they could not reach it
 /// (gap) and out of the palm when it overlaps it. Bounded and rate-limited, so it never jumps.
 /// </summary>
 public static Vector3 HoldCorrection(Vector3 current,float padGap,Vector3 towardPads,float palmDepth,Vector3 outOfPalm,float dt,float metresPerSecond=.08f){
  if(!GraspMotionPolicy.Finite(current))current=Vector3.zero;
  if(!float.IsFinite(dt)||dt<=0)return current;
  Vector3 wanted=Vector3.zero;
  // A dead band stops the hold hunting back and forth around the target (visible wobble).
  if(float.IsFinite(padGap)&&padGap>SkinTolerance+DeadBand&&GraspMotionPolicy.Finite(towardPads)&&towardPads.sqrMagnitude>1e-8f)
   wanted+=towardPads.normalized*(padGap-SkinTolerance);
  if(float.IsFinite(palmDepth)&&palmDepth>DeadBand&&GraspMotionPolicy.Finite(outOfPalm)&&outOfPalm.sqrMagnitude>1e-8f)
   wanted+=outOfPalm.normalized*palmDepth;
  var next=current+Vector3.ClampMagnitude(wanted,metresPerSecond*dt);
  return Vector3.ClampMagnitude(next,MaxHoldCorrection);
 }

 /// <summary>Speed-limited follow for a held object, so a fast hand cannot make it tunnel or snap.</summary>
 public static Vector3 Follow(Vector3 last,Vector3 requested,float maximumSpeed,float dt){
  if(!GraspMotionPolicy.Finite(requested))return last;
  if(!GraspMotionPolicy.Finite(last)||!float.IsFinite(dt)||dt<=0||!float.IsFinite(maximumSpeed)||maximumSpeed<=0)return requested;
  return Vector3.MoveTowards(last,requested,maximumSpeed*dt);
 }
}
}
