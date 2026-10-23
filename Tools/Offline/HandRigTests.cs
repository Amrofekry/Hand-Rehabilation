using System;
using System.Reflection;
using GripRehab;
using VirtualGrasp;
using VirtualGrasp.Controllers;
using UnityEngine;

// Executes project-owned adapters against test doubles, never the native SDK.
static class HandRigTests {
 static int passed,failed;
 static void Check(bool value,string label){Console.WriteLine((value?"PASS ":"FAIL ")+label);if(value)passed++;else failed++;}
 static void Group(string label,Action run){try{run();}catch(Exception e){Check(false,label+": "+e.GetBaseException().Message);}}
 static void Invoke(object target,string method){target.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,null);}
 static FingerPoseDriver Driver(bool left=false){
  var driver=new FingerPoseDriver{joints=new Transform[15],independentKeyboard=true,curlAxis=Vector3.forward,curlSign=left?1:-1};
  for(int i=0;i<15;i++)driver.joints[i]=new Transform();Invoke(driver,"Awake");return driver;
 }
 static void Settle(FingerPoseDriver driver,LandmarkFrame frame=null,float closure=0){for(int i=0;i<30;i++)driver.Pose(frame,frame!=null,closure,.05f,.045f);}
 static bool Finite(Quaternion q)=>float.IsFinite(q.x)&&float.IsFinite(q.y)&&float.IsFinite(q.z)&&float.IsFinite(q.w);
 static LandmarkFrame Frame(){
  var f=new LandmarkFrame{schemaVersion=1,confidence=.95f,sequence=7,captureSeconds=1,landmarks=new Vector3[21],worldLandmarks=new Vector3[21],handedness="Right"};
  for(int finger=0;finger<5;finger++)for(int joint=0;joint<4;joint++){
   int i=1+finger*4+joint;var point=new Vector3((finger-2)*.025f,.1f+joint*.04f,0);
   f.worldLandmarks[i]=new Vector3(point.x,-point.y,-point.z);f.landmarks[i]=new Vector3(.5f+point.x,.5f-point.y,0);
  }
  f.landmarks[0]=new Vector3(.5f,.5f,0);return f;
 }
 static void PoseTests(){
  foreach(bool left in new[]{false,true})for(int finger=0;finger<5;finger++)for(int joint=0;joint<3;joint++){
   var d=Driver(left);d.SelectedJoint=joint;d.AdjustFinger(finger,1);Settle(d);
   int n=finger*3+joint;float expected=joint==0?(finger==4?45:65):joint==1?85:55;
   bool other=true;for(int i=0;i<15;i++)if(i!=n&&d.JointAngles[i]>.001f)other=false;
   Check(Math.Abs(d.JointAngles[n]-expected)<.01f&&other,(left?"Left":"Right")+" independent channel "+finger+":"+joint);
   var rotation=Quaternion.AngleAxis(expected*(left?1:-1),Vector3.forward);
   Check(Quaternion.Angle(d.joints[n].localRotation,rotation)<.1f,"Authored curl axis/sign for "+left+" "+n);
  }
  var driver=Driver();driver.SelectedJoint=99;driver.AdjustFinger(4,1);Settle(driver);
  Check(driver.SelectedJoint==2&&driver.JointAngles[14]>54,"Invalid selected joint stays within the thumb chain");
  driver=Driver();driver.AdjustFinger(0,float.NaN);Settle(driver);
  Check(driver.ManualCurl[0]==0&&driver.JointAngles[0]==0,"Malformed keyboard delta cannot poison pose");
  driver.ManualCurl[0]=float.NaN;driver.ManualJoint[1]=float.PositiveInfinity;Settle(driver,null,float.NaN);
  Check(Finite(driver.joints[0].localRotation)&&driver.JointAngles[0]==0,"Malformed manual/demo channels safely open");
  driver=Driver();driver.AdjustFinger(2,1);Settle(driver);driver.ResetFingers();Settle(driver);
  Check(driver.JointAngles[7]<.001f,"Reset recovers finger joints to captured rest");
  foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1f}){
   driver=Driver();driver.Pose(null,false,1,bad,.045f);
   Check(Finite(driver.joints[0].localRotation)&&driver.JointAngles[0]==0,"Invalid delta cannot move/poison joints "+bad);
  }
  driver=Driver();driver.curlAxis=new Vector3(float.NaN,0,0);driver.spreadAxis=new Vector3();driver.curlSign=float.NaN;
  driver.maxPalmRotationDegrees=float.NaN;driver.palmRotationWeight=float.NaN;
  Settle(driver,Frame());
  Check(Finite(driver.transform.localRotation)&&Finite(driver.joints[0].localRotation),"Malformed rig settings retain finite rotations");
  driver=Driver();driver.Pose(Frame(),true,0,.05f,float.NaN);
  Check(Finite(driver.transform.localRotation)&&Finite(driver.joints[0].localRotation),"Malformed smoothing cannot poison tracked pose");
  driver=Driver();driver.JointAngles[0]=float.NaN;Settle(driver,null,1);
  Check(float.IsFinite(driver.JointAngles[0])&&Finite(driver.joints[0].localRotation),"Corrupted angle buffer recovers on a valid sample");
  driver=Driver();driver.holdLastTrackedPoseOnLoss=true;Settle(driver,Frame());var held=driver.transform.localRotation;
  driver.Pose(null,false,0,.05f,.045f);
  Check(!driver.TrackingUsed&&driver.LastPoseSequence==0&&!driver.UsingWorldLandmarks&&Quaternion.Angle(held,driver.transform.localRotation)<.01f,"Held loss pose carries no live sample metadata");
  driver=Driver();Settle(driver,Frame());driver.UseDemoPose();
  Check(!driver.TrackingUsed&&driver.LastPoseSequence==0&&!driver.UsingWorldLandmarks,"Demo switch immediately clears live pose metadata");
  driver=new FingerPoseDriver{joints=null};Invoke(driver,"Awake");driver.Pose(Frame(),true,0,.05f,.045f);
  Check(!driver.TrackingUsed&&driver.Closure==0,"Missing rig rejects tracking without a null exception");
  driver=Driver();driver.joints=new Transform[1];driver.Pose(Frame(),true,0,.05f,.045f);
  Check(!driver.TrackingUsed,"Incomplete replacement rig cannot masquerade as tracking");
  driver=Driver();driver.joints[2]=driver.joints[1];driver.Pose(Frame(),true,0,.05f,.045f);
  Check(!driver.TrackingUsed,"Duplicate joint cannot control two finger channels");
  driver=Driver();var replacement=new Transform{localRotation=Quaternion.AngleAxis(20,Vector3.up)};
  driver.joints[0]=replacement;Settle(driver);
  Check(Quaternion.Angle(replacement.localRotation,Quaternion.AngleAxis(20,Vector3.up))<.1f,"Replacement joint gets its own rest rather than stale rest");
 }
 static Transform Skeleton(){
  var root=new Transform{name="Input"};
  foreach(bool left in new[]{false,true})for(int bone=0;bone<16;bone++)root.children.Add(new Transform{
   name=HandRigRules.BoneName(bone,left),position=new Vector3(bone*.01f,left?1:-1,.5f),rotation=Quaternion.AngleAxis(bone,Vector3.forward)});
  return root;
 }
 static void ControllerTests(){
  FakeBoneMapping.WrongSide=FakeBoneMapping.MissingOutput=false;
  foreach(VG_HandSide side in new[]{VG_HandSide.LEFT,VG_HandSide.RIGHT}){
   var root=Skeleton();var controller=new RehabVGController(1,side,new Transform(),root);
   bool success=controller.Compute();bool match=controller.Written.Count==16;
   for(int bone=0;bone<controller.Written.Count;bone++){
    var source=root.children[(side==VG_HandSide.LEFT?16:0)+bone];
    match&=Vector3.Distance(controller.Written[bone].position,source.position)<.001f&&Quaternion.Angle(controller.Written[bone].rotation,source.rotation)<.1f;
   }
   Check(success&&match,side+" SDK adapter writes wrist and all 15 correct-side bones in SDK order");
   controller.Written.Clear();var bad=root.children[(side==VG_HandSide.LEFT?16:0)+15];bad.position=new Vector3(float.NaN,0,0);
   Check(!controller.Compute()&&controller.Written.Count==0,side+" invalid final bone prevents the entire frame being written");
   bad.position=Vector3.one;bad.rotation=new Quaternion(0,0,0,0);
   Check(!controller.Compute()&&controller.Written.Count==0,side+" zero quaternion rejected before any SDK write");
   bad.rotation=Quaternion.identity;
   Check(controller.Compute()&&controller.Written.Count==16,side+" valid poses recover after transient bad input");
  }
  var skeleton=Skeleton();skeleton.children.RemoveAt(15);var missing=new RehabVGController(1,VG_HandSide.RIGHT,new Transform(),skeleton);
  Check(!missing.Compute()&&missing.Written.Count==0,"Missing source joint cannot report SDK success");
  skeleton=Skeleton();skeleton.children.Add(new Transform{name="ThumbA_R"});var duplicate=new RehabVGController(1,VG_HandSide.RIGHT,new Transform(),skeleton);
  Check(!duplicate.Compute()&&duplicate.Written.Count==0,"Ambiguous same-side bone cannot overwrite a source silently");
  var noRoot=new RehabVGController(1,VG_HandSide.RIGHT,new Transform(),null);
  Check(!noRoot.Compute(),"Missing input root fails cleanly");
  FakeBoneMapping.WrongSide=true;
  var wrong=new RehabVGController(1,VG_HandSide.RIGHT,new Transform(),Skeleton());
  Check(!wrong.Compute(),"Wrong-side SDK output mapping rejected");FakeBoneMapping.WrongSide=false;
  FakeBoneMapping.MissingOutput=true;
  var noOutput=new RehabVGController(1,VG_HandSide.RIGHT,new Transform(),Skeleton());
  Check(!noOutput.Compute(),"Incomplete SDK output rejected");FakeBoneMapping.MissingOutput=false;
 }
 static RehabVirtualGrasp MainRig(bool bindings=true){
  Debug.Errors.Clear();VG_Controller.BadContact=-1;VG_Controller.DuplicateContact=false;VG_Controller.AvatarResult=VG_ReturnCode.SUCCESS;
  for(int side=0;side<2;side++)for(int i=0;i<15;i++)VG_Controller.Contacts[side][i]=new Transform{name="SDK bone "+side+":"+i};
  var sdk=new RehabVirtualGrasp{inputSkeleton=Skeleton(),outputMesh=new SkinnedMeshRenderer(),registrationMesh=new Mesh(),rightOnlyMesh=new Mesh(),leftOnlyMesh=new Mesh()};
  UnityEngine.Object.Main=sdk;UnityEngine.Object.App=null;Time.realtimeSinceStartupAsDouble=0;
  sdk.Awake();
  if(bindings){new RehabVGController(123,VG_HandSide.LEFT,new Transform(),sdk.inputSkeleton);new RehabVGController(123,VG_HandSide.RIGHT,new Transform(),sdk.inputSkeleton);}
  return sdk;
 }
 static void MainTests(){
  var sdk=MainRig();Invoke(sdk,"Start");
  Check(sdk.Ready&&sdk.AvatarId==123,"Ready means registration plus both complete contact hands");
  var noBindings=MainRig(false);Invoke(noBindings,"Start");
  Check(!noBindings.Ready,"Registered mesh alone is not a working project input controller");
  Check(Debug.Errors.Count==0,"Ordinary startup grace does not emit a false SDK error");
  Time.realtimeSinceStartupAsDouble=2.1;noBindings.FixedUpdate();
  Check(Debug.Errors.Count==1,"Persistent missing controllers produce one actionable SDK error");
  Time.realtimeSinceStartupAsDouble=3;noBindings.FixedUpdate();
  Check(Debug.Errors.Count==1,"Persistent SDK binding error is not logged each physics tick");
  sdk=MainRig();Invoke(sdk,"Start");
  var rig=new FingerContactRig();sdk.BindContacts(rig,false);var first=rig.contactBones;
  bool match=true;for(int i=0;i<15;i++)match&=rig.contactBones[i]==VG_Controller.Contacts[(int)VG_HandSide.RIGHT][i];
  Check(match,"Contact channels convert index-first to SDK thumb-first correctly");
  sdk.BindContacts(rig,true);match=true;for(int i=0;i<15;i++)match&=rig.contactBones[i]==VG_Controller.Contacts[(int)VG_HandSide.LEFT][i];
  Check(match,"Left contacts never reuse right bones");
  sdk.BindContacts(rig,false);Check(ReferenceEquals(first,rig.contactBones),"Hand switches reuse precomputed contact arrays");
  sdk.ShowLeft(true);Check(sdk.outputMesh.sharedMesh==sdk.leftOnlyMesh,"Only selected left mesh is visible");
  sdk.ShowLeft(false);Check(sdk.outputMesh.sharedMesh==sdk.rightOnlyMesh,"Only selected right mesh is visible");
  sdk.ShowLeft(true);sdk.ShowLeft(true);Check(sdk.outputMesh.sharedMesh==sdk.leftOnlyMesh,"Repeated same-hand selection is idempotent");
  sdk=MainRig();VG_Controller.BadContact=14;Invoke(sdk,"Start");
  Check(!sdk.Ready,"A failed finger lookup does not report Ready");
  sdk.BindContacts(rig,true);match=true;foreach(var bone in rig.contactBones)match&=bone==null;
  Check(match,"Unavailable SDK cannot retain the previous hand contact bindings");
  VG_Controller.BadContact=-1;sdk.FixedUpdate();
  Check(sdk.Ready&&sdk.BaseFixedUpdates>0,"Delayed registration retries while the native heartbeat still advances");
  sdk=MainRig();VG_Controller.DuplicateContact=true;Invoke(sdk,"Start");
  Check(!sdk.Ready,"One SDK bone cannot stand in for two joints");
  sdk=MainRig();VG_Controller.AvatarResult=VG_ReturnCode.FAILED;Invoke(sdk,"Start");sdk.FixedUpdate();int prior=VG_Controller.AvatarCalls;
  sdk.FixedUpdate();Check(!sdk.Ready&&VG_Controller.AvatarCalls==prior,"Failed startup retries are rate limited");
  var missing=new RehabVirtualGrasp();int init=VG_Controller.InitializeCalls;missing.Awake();Invoke(missing,"Start");missing.Update();missing.FixedUpdate();
  Check(!missing.Ready&&VG_Controller.InitializeCalls==init&&missing.BaseUpdates==0,"Missing assets never enter uninitialized SDK lifecycle");
 }
 static int Main(){Group("Pose",PoseTests);Group("Controller",ControllerTests);Group("Main",MainTests);
  Console.WriteLine("Hand rig checks: "+passed+" passed, "+failed+" failed; managed math/SDK doubles, no native Unity or VirtualGrasp");return failed==0?0:1;}
}
