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
  ContactChecks();
  GripChartChecks();
  Console.WriteLine("Grasp checks passed: "+passed);
 }
 // Stage 8 grip chart: the production catalog (18 grips) is complete, consistent and safe to drive a hand with.
 static void GripChartChecks(){
  var all=GripCatalog.All;
  Check(all.Length==18&&Enum.GetValues(typeof(GripType)).Length==18,"Grip chart has all 18 grips");
  for(int i=0;i<all.Length;i++){
   var g=all[i];
   Check(g.number==i+1&&GripCatalog.Get(g.type)==g&&!string.IsNullOrEmpty(g.name)&&!string.IsNullOrEmpty(g.fingers)&&!string.IsNullOrEmpty(g.purpose),"Grip "+g.number+" ("+g.name+") is in chart order with its labels");
   bool joints=g.curl.Length==15&&g.spread.Length==5;
   foreach(var c in g.curl)joints&=float.IsFinite(c)&&c>=0&&c<=110;foreach(var s in g.spread)joints&=float.IsFinite(s)&&Math.Abs(s)<=40;
   Check(joints,g.name+" has 15 bounded joint curls and 5 bounded spreads");
   bool pads=g.pads.Length>0;for(int a=0;a<g.pads.Length;a++){pads&=g.pads[a]>=0&&g.pads[a]<15;for(int b=0;b<a;b++)pads&=g.pads[a]!=g.pads[b];}
   Check(pads,g.name+" names distinct, valid contact segments");
   // The chart's hook ("thumb on top") and cupped ("fingers curved + palm") grips do not press with the thumb.
   bool thumbFree=g.type==GripType.Hook||g.type==GripType.Cupped;
   Check(thumbFree?!g.IsPad(14)&&!g.IsPad(13):g.IsPad(14),g.name+(thumbFree?" keeps the thumb off the object":" uses the thumb"));
   if(g.anchor==GripAnchor.Tips){bool finger=false;foreach(int p in g.pads)finger|=p<12&&p%3==2;Check(finger,g.name+" opposes the thumb with at least one fingertip");}
   Check(Math.Abs(g.CurlAt(4,0)-g.curl[4]*.3f)<1e-4f&&Math.Abs(g.CurlAt(4,1)-g.curl[4])<1e-4f&&Math.Abs(g.CurlAt(4,float.NaN)-g.curl[4]*.3f)<1e-4f&&g.CurlAt(-1,1)==0&&g.CurlAt(15,1)==0,g.name+" blends from a relaxed pre-shape to the full grip, rejecting bad input");
   Check(g.twoHands==(g.type==GripType.Interlocking),g.name+(g.twoHands?" uses both hands":" is one-handed"));
  }
  var power=GripCatalog.Get(GripType.Power);var precision=GripCatalog.Get(GripType.Precision);
  bool allWrap=true;for(int f=0;f<5;f++)allWrap&=power.Wraps(f);
  Check(allWrap&&precision.Wraps(0)&&precision.Wraps(4)&&!precision.Wraps(1)&&!precision.Wraps(2)&&!precision.Wraps(3),"Power wraps all five fingers; precision only index and thumb");
  Check(precision.PinchPair&&GripCatalog.Get(GripType.FingerTip).PinchPair&&GripCatalog.Get(GripType.TwoFinger).PinchPair&&GripCatalog.Get(GripType.LateralPinch).PinchPair&&!GripCatalog.Get(GripType.ThreeFinger).PinchPair&&!power.PinchPair,"Only thumb-against-one-finger pinches ease the hold");
  Check(!GripCatalog.Get(GripType.Side).AlignsToPinch&&GripCatalog.Get(GripType.FingerTip).AlignsToPinch&&!power.AlignsToPinch,"The flat-hand side grip keeps the palm axis; small pinches align to the pinch");
  int wider=0;foreach(var g in all)if(g.CloseFactor!=1f){wider++;Check(g.CloseFactor>1&&g.CloseFactor<=1.35f,g.name+" closes at most 35 % past its shape");}
  Check(wider==3,"Only the card, phone and cloth grips may close past their shape");
  foreach(GripView view in Enum.GetValues(typeof(GripView))){
   GripCatalog.ViewBasis(view,false,out var ra,out var rp);GripCatalog.ViewBasis(view,true,out var la,out var lp);
   Check(Math.Abs(la.x+ra.x)<1e-6f&&la.y==ra.y&&la.z==ra.z&&Math.Abs(lp.x+rp.x)<1e-6f&&lp.y==rp.y&&lp.z==rp.z,view+" view mirrors the left hand");
   Check(Vector3.Cross(ra.normalized,rp.normalized).sqrMagnitude>.25f,view+" view has a usable finger and palm direction");
  }
 }
 // Stage 8: production contact geometry against an analytic 4 cm ball.
 // Analytic 4 cm ball: sphere overlap is r - (distance to centre - 4 cm), true depth also inside.
 static float Ball(Vector3 p,float r)=>r-(Vector3.Distance(p,new Vector3())-.04f);
 static void ContactChecks(){
  float r=.009f;
  float gap=ContactGeometry.CapsuleDepth(new Vector3(.05f,-.01f,0),new Vector3(.05f,.01f,0),r,Ball);
  Check(Math.Abs(gap-(-.001f))<.0002f,"Capsule 1 mm clear of the surface reports a 1 mm gap ("+gap+")");
  float overlap=ContactGeometry.CapsuleDepth(new Vector3(.046f,-.01f,0),new Vector3(.046f,.01f,0),r,Ball);
  Check(overlap>.002f&&overlap<.004f,"Capsule pressed 3 mm in reports about 3 mm of overlap ("+overlap+")");
  Check(Math.Abs(ContactGeometry.CapsuleDepth(new Vector3(0,-.01f,0),new Vector3(0,.01f,0),r,Ball)-(r+.04f))<.0002f,"Capsule through the centre reports its true depth, not a capped value");
  Check(float.IsNegativeInfinity(ContactGeometry.CapsuleDepth(new Vector3(float.NaN,0,0),Vector3.up,r,Ball)),"Bad capsule input is rejected");
  // A finger curling toward the ball: depth grows 1 mm per degree past 40 degrees.
  Func<float,float> depth=a=>(a-40)*.001f;
  float allowed=ContactGeometry.LimitAngle(80,depth,ContactGeometry.SkinTolerance);
  Check(allowed>41.4f&&allowed<=42.01f,"Curl stops at the surface plus skin tolerance ("+allowed+" deg)");
  Check(ContactGeometry.LimitAngle(30,depth,ContactGeometry.SkinTolerance)==30,"A curl that does not reach the object is untouched");
  Check(ContactGeometry.LimitAngle(80,a=>.01f,ContactGeometry.SkinTolerance)==0,"A finger already inside stays open rather than closing deeper");
  Check(ContactGeometry.LimitAngle(float.NaN,depth,ContactGeometry.SkinTolerance)==0,"Bad curl target is rejected");
  float backed=ContactGeometry.LimitAngle(30,a=>(a+10)*.001f,ContactGeometry.SkinTolerance,8,-20);
  Check(backed>-8.2f&&backed<=-7.9f,"A finger resting inside the object extends just enough to back out ("+backed+" deg)");
  Check(ContactGeometry.LimitAngle(30,a=>(a+10)*.001f,ContactGeometry.SkinTolerance,8,5)==0,"A positive minimum is treated as 0 (no forced curl)");
  // Hold correction: an 8 mm pad gap closes smoothly, never faster than 12 cm/s and never beyond 5 cm.
  var correction=Vector3.zero;var toward=new Vector3(1,0,0);
  for(int i=0;i<20;i++){var next=ContactGeometry.HoldCorrection(correction,.008f-correction.x,toward,0,Vector3.zero,.02f);Check(Vector3.Distance(next,correction)<=.08f*.02f+1e-6f,"Hold correction step "+i+" is rate limited");correction=next;}
  float finalGap=.008f-correction.x;
  Check(finalGap<=ContactGeometry.SkinTolerance+ContactGeometry.DeadBand+1e-5f&&finalGap>=-1e-5f,"Gap closes to within skin tolerance plus dead band, without overshooting ("+finalGap+")");
  var settled=ContactGeometry.HoldCorrection(correction,finalGap,toward,0,Vector3.zero,.02f);
  Check(Near(settled,correction),"Inside the dead band the hold stays still (no wobble)");
  var out_=ContactGeometry.HoldCorrection(Vector3.zero,0,Vector3.zero,.01f,new Vector3(0,1,0),.02f);
  Check(out_.y>0&&out_.x==0,"Palm overlap pushes the object out of the palm");
  var far=Vector3.zero;for(int i=0;i<500;i++)far=ContactGeometry.HoldCorrection(far,.5f,toward,0,Vector3.zero,.02f);
  Check(far.magnitude<=ContactGeometry.MaxHoldCorrection+1e-6f,"Hold correction is bounded");
  Check(Near(ContactGeometry.HoldCorrection(correction,.008f,toward,0,Vector3.zero,float.NaN),correction),"Bad time leaves the correction unchanged");
  // Follow: a 4 m/s hand cannot move the held object more than 5 cm per 50 Hz step (no tunnelling).
  var held=Vector3.zero;var hand=Vector3.zero;float maxStep=0;
  for(int i=0;i<50;i++){hand+=new Vector3(4*.02f,0,0);var next=ContactGeometry.Follow(held,hand,2.5f,.02f);maxStep=Math.Max(maxStep,Vector3.Distance(next,held));held=next;}
  Check(maxStep<=.05f+1e-5f,"Held object moves at most 5 cm per physics step ("+maxStep+")");
  Check(Near(ContactGeometry.Follow(held,new Vector3(float.NaN,0,0),2.5f,.02f),held),"Bad target keeps the held object in place");
  Check(Near(ContactGeometry.Follow(held,held+new Vector3(.01f,0,0),2.5f,.02f),held+new Vector3(.01f,0,0)),"Small hand motion is followed exactly");
 }
}
