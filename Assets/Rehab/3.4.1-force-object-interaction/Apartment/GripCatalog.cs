using System;
using UnityEngine;
namespace GripRehab {
/// <summary>The 18 grips of the user's 2026-10-25 grip chart (Stage 8 extension).</summary>
public enum GripType { Power, Precision, ThreeFinger, LateralPinch, Hook, Claw, Palm, Spherical, FingerTip, Cupped, TwoFinger, LateralThumb, PreciseThreeFinger, Fist, Side, Interlocking, Folded, Dynamic }

/// <summary>Where the hand holds the object: on the palm, between the finger tips, between the thumb and the
/// side of the index finger, or inside the hooked fingers.</summary>
public enum GripAnchor { Palm, Tips, Lateral, Hook }

/// <summary>How the held hand is presented to the first-person camera (keyboard/demo input only; a tracked
/// camera hand keeps its measured orientation). Matches the photographs of the grip chart.</summary>
public enum GripView {
 /// <summary>Fingers up, palm facing the body midline, thumb toward the camera: the object sits beside the palm.</summary>
 Side,
 /// <summary>Fingers pointing across, palm away and down, thumb underneath: the pinch is at the front.</summary>
 Pinch,
 /// <summary>Palm down over the object, fingers forward (claw and hook).</summary>
 Top,
 /// <summary>Thumb up, palm facing the midline, fingers forward: cylinders stand upright in the hand.</summary>
 Upright
}

/// <summary>
/// One grip: the hand shape at full closure (degrees per joint, index/middle/ring/little/thumb x
/// proximal/intermediate/distal, the same channels as FingerPoseDriver), finger spread, which finger
/// segments must touch the object, and where the object sits. Pure data: no Unity objects.
/// </summary>
public sealed class GripDefinition {
 public readonly int number;public readonly GripType type;public readonly string name,fingers,purpose;
 public readonly GripAnchor anchor;public readonly GripView view;public readonly bool twoHands;
 /// <summary>Curl at full closure, 15 joints.</summary>
 public readonly float[] curl;
 /// <summary>Finger spread in degrees (index, middle, ring, little, thumb); positive spreads away from the middle finger.</summary>
 public readonly float[] spread;
 /// <summary>Joint indices whose segments are expected to touch the object.</summary>
 public readonly int[] pads;
 public GripDefinition(int number,GripType type,string name,string fingers,string purpose,GripAnchor anchor,GripView view,float[] curl,float[] spread,int[] pads,bool twoHands=false){
  this.number=number;this.type=type;this.name=name;this.fingers=fingers;this.purpose=purpose;this.anchor=anchor;this.view=view;
  this.curl=curl;this.spread=spread;this.pads=pads;this.twoHands=twoHands;
 }
 /// <summary>Shape at a closure in [0,1]: a relaxed pre-shape (30 %) blending to the full grip.</summary>
 public float CurlAt(int joint,float closure){
  if(joint<0||joint>=15)return 0;
  closure=float.IsFinite(closure)?Math.Max(0,Math.Min(1,closure)):0;
  return curl[joint]*(.3f+.7f*closure);
 }
 public bool IsPad(int joint)=>Array.IndexOf(pads,joint)>=0;
 /// <summary>How far past the photographed shape the touching fingers may close before contact stops them.
 /// Only where measured to help (flat card, phone, folded cloth); elsewhere a finger that misses would over-curl.</summary>
 public float CloseFactor=>anchor==GripAnchor.Lateral||type==GripType.LateralThumb||type==GripType.Folded?1.35f:1f;
 /// <summary>Thumb against one finger: the hold may ease toward the finger that has not reached the object.</summary>
 public bool PinchPair=>(anchor==GripAnchor.Tips||anchor==GripAnchor.Lateral)&&pads.Length==2;
 /// <summary>Small pinches turn thin objects across the thumb-to-finger axis; the flat-hand side grip keeps the palm axis.</summary>
 public bool AlignsToPinch=>anchor==GripAnchor.Lateral||anchor==GripAnchor.Tips&&pads.Length<=3;
 /// <summary>Fingers (0-4) that wrap onto the object: any finger with a pad segment.</summary>
 public bool Wraps(int finger){for(int j=0;j<3;j++)if(IsPad(finger*3+j))return true;return false;}
}

public static class GripCatalog {
 // Joint indices: finger f, joint j -> f*3+j. Distal pads: 2,5,8,11,14. Intermediate: 1,4,7,10,13.
 static float[] C(float[] index,float[] middle,float[] ring,float[] little,float[] thumb){
  var c=new float[15];
  for(int j=0;j<3;j++){c[j]=index[j];c[3+j]=middle[j];c[6+j]=ring[j];c[9+j]=little[j];c[12+j]=thumb[j];}
  return c;
 }
 static float[] F(float a,float b,float c)=>new[]{a,b,c};
 static float[] S(float i,float m,float r,float l,float t)=>new[]{i,m,r,l,t};

 public static readonly GripDefinition[] All={
  new GripDefinition(1,GripType.Power,"Power grip","All 5 fingers (full hand grip)","Ball (spherical)",GripAnchor.Palm,GripView.Side,
   C(F(58,68,42),F(58,68,42),F(58,68,42),F(58,68,42),F(40,30,20)),S(-4,0,4,8,18),new[]{2,5,8,11,14}),
  new GripDefinition(2,GripType.Precision,"Precision grip","Thumb + index (pinch)","Small cube",GripAnchor.Tips,GripView.Pinch,
   C(F(42,38,18),F(50,62,40),F(56,70,45),F(60,74,45),F(32,22,14)),S(0,0,2,4,22),new[]{2,14}),
  new GripDefinition(3,GripType.ThreeFinger,"Three-finger grip","Thumb + index + middle (tripod)","Pen (cylindrical)",GripAnchor.Tips,GripView.Pinch,
   C(F(40,34,20),F(46,40,24),F(60,80,50),F(64,84,50),F(30,20,12)),S(-2,2,4,6,20),new[]{2,5,14}),
  new GripDefinition(4,GripType.LateralPinch,"Lateral pinch","Thumb + side of index (lateral pinch)","Card (flat)",GripAnchor.Lateral,GripView.Pinch,
   C(F(55,75,45),F(60,84,54),F(64,85,55),F(64,85,55),F(14,10,10)),S(0,0,0,0,-6),new[]{1,14}),
  new GripDefinition(5,GripType.Hook,"Hook grip","Fingers (hook), thumb on top","Handle (curved)",GripAnchor.Hook,GripView.Top,
   C(F(16,100,62),F(16,100,62),F(16,100,62),F(16,100,62),F(6,2,0)),S(0,0,0,2,26),new[]{1,4,7,10}),
  new GripDefinition(6,GripType.Claw,"Claw grip","Fingers spread (claw)","Basketball (large)",GripAnchor.Palm,GripView.Top,
   C(F(48,72,52),F(48,72,52),F(48,72,52),F(48,72,52),F(36,34,24)),S(-14,-4,6,16,34),new[]{2,5,8,11,14}),
  new GripDefinition(7,GripType.Palm,"Palm grip","Whole palm + fingers (wrapped)","Water bottle (cylindrical)",GripAnchor.Palm,GripView.Upright,
   C(F(55,65,40),F(55,65,40),F(55,65,40),F(55,65,40),F(45,35,20)),S(-2,0,2,4,14),new[]{1,2,4,5,7,8,14}),
  new GripDefinition(8,GripType.Spherical,"Spherical grip","All fingers + palm (spherical)","Stress ball (spherical)",GripAnchor.Palm,GripView.Side,
   C(F(50,56,36),F(50,56,36),F(50,56,36),F(50,56,36),F(40,26,20)),S(-8,-2,4,10,22),new[]{2,5,8,11,14}),
  new GripDefinition(9,GripType.FingerTip,"Finger tip grip","Finger tips (index + thumb), delicate","Coin (small)",GripAnchor.Tips,GripView.Pinch,
   C(F(40,30,24),F(34,32,22),F(36,36,24),F(38,40,26),F(30,15,10)),S(0,2,4,6,22),new[]{2,14}),
  new GripDefinition(10,GripType.Cupped,"Cupped grip","Fingers curved + palm (cupping)","Cup (fragile)",GripAnchor.Palm,GripView.Upright,
   C(F(44,50,34),F(44,50,34),F(44,50,34),F(44,50,34),F(30,20,10)),S(-2,0,2,4,16),new[]{1,2,4,5,7,8,11}),
  new GripDefinition(11,GripType.TwoFinger,"Two-finger grip","Thumb + middle (two-finger)","USB flash drive (small)",GripAnchor.Tips,GripView.Pinch,
   C(F(28,18,10),F(46,40,24),F(56,70,45),F(60,74,45),F(30,20,14)),S(-6,0,2,4,22),new[]{5,14}),
  new GripDefinition(12,GripType.LateralThumb,"Lateral/thumb grip","Thumb + fingers (side support)","Smartphone (large)",GripAnchor.Palm,GripView.Upright,
   C(F(55,62,40),F(55,62,40),F(55,62,40),F(55,62,40),F(26,16,10)),S(0,0,0,0,10),new[]{2,5,8,14}),
  new GripDefinition(13,GripType.PreciseThreeFinger,"Precision 3-finger","Thumb + index + middle (fine control)","Screw (small)",GripAnchor.Tips,GripView.Pinch,
   C(F(44,44,24),F(50,44,24),F(60,80,50),F(64,84,50),F(34,24,14)),S(-2,2,4,6,22),new[]{2,5,14}),
  new GripDefinition(14,GripType.Fist,"Fist grip","All fingers + thumb (fist)","Grip strength tool",GripAnchor.Palm,GripView.Upright,
   C(F(64,85,55),F(64,85,55),F(64,85,55),F(64,85,55),F(45,40,30)),S(0,0,0,0,10),new[]{1,2,4,5,7,8,10,11,14}),
  new GripDefinition(15,GripType.Side,"Side grip","Fingers on one side + thumb (side support)","Book (rectangular)",GripAnchor.Tips,GripView.Pinch,
   C(F(20,14,8),F(20,14,8),F(20,14,8),F(20,14,8),F(10,6,4)),S(-2,0,2,4,-4),new[]{2,5,8,14}),
  new GripDefinition(16,GripType.Interlocking,"Interlocking grip","Both hands, interlocked (stability)","Ball (large)",GripAnchor.Palm,GripView.Side,
   C(F(44,54,34),F(44,54,34),F(44,54,34),F(44,54,34),F(30,24,14)),S(-8,-2,4,10,20),new[]{2,5,8,11,14},true),
  new GripDefinition(17,GripType.Folded,"Folded grip","Fingers folded (grasping fabric)","Cloth (flexible)",GripAnchor.Palm,GripView.Side,
   C(F(64,80,50),F(64,80,50),F(64,80,50),F(64,80,50),F(40,35,25)),S(0,0,0,0,10),new[]{1,2,4,5,7,8,14}),
  new GripDefinition(18,GripType.Dynamic,"Dynamic grip","Full hand (adjustable)","Hammer (tool)",GripAnchor.Palm,GripView.Upright,
   C(F(62,80,50),F(62,80,50),F(62,80,50),F(62,80,50),F(45,35,25)),S(0,0,0,0,10),new[]{1,2,4,5,7,8,10,11,14}),
 };
 public static GripDefinition Get(GripType type)=>All[(int)type];

 /// <summary>Wanted finger direction (wrist to knuckles) and palm-facing direction in camera space
 /// (x right, y up, z forward) for a right hand; a left hand mirrors x. Not orthonormal: the caller orthonormalises.</summary>
 public static void ViewBasis(GripView view,bool left,out Vector3 along,out Vector3 palm){
  switch(view){
   // Pinch: fingers to the side, palm down, so the camera looks along the thumb and sees the pinch in profile.
   case GripView.Pinch:along=new Vector3(-.95f,.2f,.1f);palm=new Vector3(0,-1,.25f);break;
   // Top: palm down over the object, fingers to the side and slightly away.
   case GripView.Top:along=new Vector3(-.8f,-.15f,.55f);palm=new Vector3(0,-1,0);break;
   // Upright: thumb up, palm toward the midline, fingers forward (how one sees one's own hand holding a bottle).
   case GripView.Upright:along=new Vector3(-.25f,0,1);palm=new Vector3(-1,0,-.3f);break;
   // Side: fingers up, palm toward the midline, thumb toward the camera.
   default:along=new Vector3(-.2f,.95f,.1f);palm=new Vector3(-1,0,.15f);break;
  }
  if(left){along.x=-along.x;palm.x=-palm.x;}
 }
}
}
