using System;
using GripRehab;
using UnityEngine;
// Production policies/guard with managed math and a collider-pair recorder.
// NO native PhysX, Unity scene, SDK, camera or OS input is executed.
class GraspTests {
 static int passed;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
 static bool Near(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<.0001f;
 static Collider Box(float x)=>new Collider{bounds=new Bounds(new Vector3(x-.1f,-.1f,-.1f),new Vector3(x+.1f,.1f,.1f))};
 static void Main(){
  var zero=new Vector3();var forward=Vector3.forward;
  foreach(float side in new[]{-1f,1f}){
   var surface=new Vector3(.08f*side,0,1);
   Check(!float.IsInfinity(GraspMotionPolicy.AimScore(zero,forward,surface,1.6f,9)),"Slightly off-centre "+side+" side is reachable");
   Check(float.IsPositiveInfinity(GraspMotionPolicy.AimScore(zero,forward,new Vector3(side*.5f,0,1),1.6f,9)),"Far off-centre "+side+" is rejected");
   Check(float.IsPositiveInfinity(GraspMotionPolicy.AimScore(zero,forward,surface,1.6f,2)),"Inspector cone reduces assistance "+side);
   var rest=Quaternion.AngleAxis(side*40,Vector3.up);var item=Quaternion.AngleAxis(15,Vector3.right);
   var offset=GraspMotionPolicy.RelativeRotation(rest,item);
   Check(Quaternion.Angle(GraspMotionPolicy.HeldRotation(rest,offset),item)<.1f,"Pickup orientation preserved "+side);
   var turn=Quaternion.AngleAxis(side*30,Vector3.forward);
   Check(Quaternion.Angle(GraspMotionPolicy.HeldRotation(turn*rest,offset),turn*item)<.1f,"Held item follows palm rotation "+side);
   var motion=GraspMotionPolicy.ReleaseVelocity(zero,new Vector3(.02f*side,0,0),.02f,.15f,1.2f,.25f);
   Check(Near(motion,new Vector3(.15f*side,0,0)),"Small motion has damped release velocity "+side);
   Check(Near(GraspMotionPolicy.ReleaseVelocity(zero,new Vector3(2*side,0,0),.02f,.15f,1.2f,.25f),zero),"Teleport creates no throw "+side);
   Check(Near(GraspMotionPolicy.ReleaseVelocity(zero,new Vector3(.24f*side,0,0),.001f,.15f,1.2f,.25f),new Vector3(1.2f*side,0,0)),"Fast motion speed capped "+side);
  }
  Check(float.IsInfinity(GraspMotionPolicy.AimScore(zero,forward,new Vector3(0,0,2),1.6f,9)),"Beyond surface reach rejected");
  Check(float.IsInfinity(GraspMotionPolicy.AimScore(zero,forward,new Vector3(0,0,-1),1.6f,9)),"Behind camera rejected");
  Check(float.IsInfinity(GraspMotionPolicy.AimScore(zero,zero,forward,1.6f,9)),"Degenerate ray rejected");
  Check(float.IsInfinity(GraspMotionPolicy.AimScore(zero,forward,forward,float.NaN,9)),"Invalid reach rejected");
  Check(float.IsInfinity(GraspMotionPolicy.AimScore(zero,forward,forward,1.6f,0)),"Invalid cone rejected");
  Check(GraspMotionPolicy.AimScore(zero,forward,forward,1.6f,9)<GraspMotionPolicy.AimScore(zero,forward,new Vector3(.1f,0,.5f),1.6f,15),"Angular closeness wins over mere proximity");
  foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){
   Check(Near(GraspMotionPolicy.SafeVelocity(new Vector3(invalid,1,0),2),zero),"Invalid velocity zeroed "+invalid);
   Check(Near(GraspMotionPolicy.ReleaseVelocity(zero,forward,invalid,.15f,1.2f,.25f),zero),"Invalid timestep zeroed "+invalid);
   Check(Near(GraspMotionPolicy.ReleaseVelocity(zero,new Vector3(.1f,0,0),.02f,invalid,1.2f,.25f),zero),"Invalid gain zeroed "+invalid);
   Check(Near(GraspMotionPolicy.ReleaseVelocity(zero,new Vector3(.1f,0,0),.02f,.15f,1.2f,invalid),zero),"Invalid jump threshold zeroed "+invalid);
  }
  Check(Near(GraspMotionPolicy.SafeVelocity(new Vector3(float.MaxValue,0,0),2),new Vector3(2,0,0)),"Large finite velocity safely capped");
  Check(Near(GraspMotionPolicy.ReleaseVelocity(zero,forward,0,.15f,1.2f,.25f),zero),"Zero timestep zeroed");
  Check(Near(GraspMotionPolicy.ReleaseVelocity(zero,forward,-1,.15f,1.2f,.25f),zero),"Negative timestep zeroed");
  Check(Near(GraspMotionPolicy.ReleaseVelocity(zero,new Vector3(.1f,0,0),.02f,0,1.2f,.25f),zero),"No gain means ordinary drop");
  Check(Near(GraspMotionPolicy.SafeVelocity(forward,-1),zero),"Invalid speed rejected");
  var min=new Vector3(-1,-1,-1);var max=new Vector3(1,1,1);
  Check(!GraspMotionPolicy.Separated(min,max,min,max,.015f),"Intersecting boxes remain excluded");
  Check(!GraspMotionPolicy.Separated(min,max,new Vector3(1.01f,-1,-1),new Vector3(2,1,1),.015f),"Clearance margin prevents early restore");
  foreach(var axis in new[]{Vector3.right,Vector3.up,Vector3.forward}){
   Check(GraspMotionPolicy.Separated(min,max,min+axis*3,max+axis*3,.015f),"Positive axis separation");
   Check(GraspMotionPolicy.Separated(min,max,min-axis*3,max-axis*3,.015f),"Negative axis separation");
  }
  Check(!GraspMotionPolicy.Separated(max,min,min,max,.015f),"Malformed bounds fail closed");
  var body=Box(0);var player=Box(0);var floor=Box(10);var trigger=Box(0);trigger.isTrigger=true;
  var shield=new GraspCollisionShield();
  shield.Protect(new[]{body,trigger},new[]{player});
  Check(Physics.GetIgnoreCollision(body,player),"Held player pair excluded");
  Check(!Physics.GetIgnoreCollision(trigger,player),"Finger trigger pairs untouched");
  Check(!Physics.GetIgnoreCollision(body,floor),"Floor pairs untouched");
  shield.Step(false);Check(Physics.GetIgnoreCollision(body,player),"Released overlapping body stays excluded");
  for(int i=0;i<1000;i++)shield.Step(false);
  Check(Physics.GetIgnoreCollision(body,player),"No timeout restores overlap");
  player.enabled=false;shield.Step(false);
  Check(Physics.GetIgnoreCollision(body,player),"Temporarily disabled controller keeps release guard");
  player.enabled=true;Physics.IgnoreCollision(body,player,false);shield.Step(false);
  Check(Physics.GetIgnoreCollision(body,player),"Re-enabled overlapping controller regains guard");
  shield.Protect(new[]{body},new[]{player});shield.Step(false);
  Check(Physics.GetIgnoreCollision(body,player),"Regrabbing drop retains original pair bookkeeping");
  body.bounds=Box(1).bounds;shield.Step(true);
  Check(Physics.GetIgnoreCollision(body,player),"Held separation does not restore prematurely");
  shield.Step(false);Check(!Physics.GetIgnoreCollision(body,player),"Separated release restores original collision");
  Physics.IgnoreCollision(body,player,true);shield.Protect(new[]{body},new[]{player});shield.Step(false);
  Check(Physics.GetIgnoreCollision(body,player),"Originally ignored pair preserved after release");
  shield.Clear();Check(Physics.GetIgnoreCollision(body,player),"Clear also preserves original authoring");
  Physics.IgnoreCollision(body,player,false);body.bounds=Box(0).bounds;
  var child=Box(0);var anotherPlayer=Box(0);
  shield.Protect(new[]{body,child},new[]{player,anotherPlayer});
  Check(Physics.GetIgnoreCollision(child,player)&&Physics.GetIgnoreCollision(child,anotherPlayer),"Child and multiple player colliders protected");
  Physics.IgnoreCollision(body,player,false);shield.Step(true);
  Check(Physics.GetIgnoreCollision(body,player),"Native pair reset reapplied while held");
  body.enabled=false;shield.Step(false);
  Check(!Physics.GetIgnoreCollision(body,player)&&Physics.GetIgnoreCollision(child,player),"Disabled pair restored independently");
  child.gameObject.activeInHierarchy=false;shield.Step(false);
  Check(!Physics.GetIgnoreCollision(child,player),"Inactive object restores pair");
  body.enabled=true;shield.Protect(new[]{body},new[]{player});shield.Clear();
  Check(!Physics.GetIgnoreCollision(body,player),"Cleanup restores active pairs");
  shield.Step(false);Check(!Physics.GetIgnoreCollision(body,player),"Cleanup leaves no lingering suppression");
  shield.Protect(null,new[]{player});shield.Protect(new Collider[]{null},new[]{player});
  Check(!Physics.GetIgnoreCollision(body,player),"Missing collider lists are safe");
  Console.WriteLine("Grasp checks passed: "+passed);
 }
}
