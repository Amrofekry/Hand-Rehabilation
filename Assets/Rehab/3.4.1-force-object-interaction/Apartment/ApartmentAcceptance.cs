using System;
using VirtualGrasp.Controllers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
namespace GripRehab {
// Opt-in executable acceptance suite, also invoked by the Editor QA menu.
public sealed class ApartmentAcceptance:MonoBehaviour {
 public static bool Finished,Passed;
 private readonly List<string> checks=new List<string>();private ApartmentExperience app;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){
  Finished=false;Passed=false;
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-apartment-smoke")>=0){
   Application.runInBackground=true;
   new GameObject("Acceptance runner").AddComponent<ApartmentAcceptance>();
  }
 }
 private IEnumerator Start(){
  Application.runInBackground=true;
  Finished=false;Passed=false;
  var routine=Run();
  while(true){object current=null;bool more=false;try{more=routine.MoveNext();if(more)current=routine.Current;}catch(Exception error){End(false,error.ToString());yield break;}if(!more)break;yield return current;}
  End(true,"");
 }
 private void Check(bool condition,string text){if(!condition)throw new Exception(text);checks.Add(text);}
 private IEnumerator Run(){
  yield return new WaitForSeconds(1.5f);
  app=FindAnyObjectByType<ApartmentExperience>();
  Check(app!=null&&app.objects.All(x=>x!=null)&&app.statusText!=null&&app.rightHand!=null,"Serialized scene bindings survive reload");
  Check(app.virtualGrasp!=null&&app.virtualGrasp.Ready,"VirtualGrasp native SDK and external controllers initialize");
  Check(app.cameraLauncher.swapHandedness,"Saved laptop camera calibration reverses the observed right/left labels");
  Check(app.virtualGrasp.outputMesh.sharedMesh==app.virtualGrasp.rightOnlyMesh,"Only selected right hand is rendered");
  app.SelectHand(true);Check(app.virtualGrasp.outputMesh.sharedMesh==app.virtualGrasp.leftOnlyMesh&&app.contactRig.pose==app.leftHand,"Left selection swaps visible mesh and contact hand");app.SelectHand(false);
  app.player.enabled=false;app.player.SetPaused(false);yield return null;
  var forceFilter=new GripForceFilter();forceFilter.Push(10,.05f,.06f,30,250);
  Check(Mathf.Abs(forceFilter.Push(100,.05f,.06f,30,250)-10)<.001f,"Single isolated force spike is rejected");
  Check(forceFilter.Push(100,.05f,.06f,30,250)>10,"Repeated high force is accepted with smoothing");
  Check(forceFilter.Push(float.NaN,.05f,.06f,30,250)==0,"Impossible force resets the contact filter");
  Check(!app.Hardware&&app.ForceValid,"Keyboard/mouse demo is the default");
  Check(FindObjectsByType<Camera>().Length==1&&FindObjectsByType<LiveForceReceiver>().Length==1&&FindObjectsByType<VisionUdpReceiver>().Length==1&&FindObjectsByType<VisionPreviewReceiver>().Length==1,"Exactly one camera and each input receiver");
  var player=app.player;player.enabled=false;player.SetPaused(false);
  var sdk=app.virtualGrasp;
  VirtualGrasp.VG_Controller.GetFingerBone(sdk.AvatarId,VirtualGrasp.VG_HandSide.RIGHT,1,1,out Transform outputIndex);
  VirtualGrasp.VG_Controller.GetFingerBone(sdk.AvatarId,VirtualGrasp.VG_HandSide.RIGHT,2,1,out Transform outputMiddle);
  Check(outputIndex!=null&&outputMiddle!=null,"SDK exposes the rendered finger joints");
  var indexBefore=outputIndex.localRotation;var middleBefore=outputMiddle.localRotation;
  app.rightHand.independentKeyboard=true;app.rightHand.AdjustFinger(0,1);yield return new WaitForSeconds(.45f);
  Check(Quaternion.Angle(indexBefore,outputIndex.localRotation)>20&&Quaternion.Angle(middleBefore,outputMiddle.localRotation)<5,"Independent index input moves the SDK output without curling middle finger");
  app.rightHand.ResetFingers();yield return new WaitForSeconds(.4f);
  app.SelectHand(true);
  VirtualGrasp.VG_Controller.GetFingerBone(sdk.AvatarId,VirtualGrasp.VG_HandSide.LEFT,0,1,out Transform leftThumb);
  VirtualGrasp.VG_Controller.GetFingerBone(sdk.AvatarId,VirtualGrasp.VG_HandSide.LEFT,1,1,out Transform leftIndex);
  Check(leftThumb!=null&&leftIndex!=null&&sdk.outputMesh.sharedMesh==sdk.leftOnlyMesh,"Left hand selection uses the vendor mesh and joints");
  var leftThumbBefore=leftThumb.localRotation;var leftIndexBefore=leftIndex.localRotation;
  app.leftHand.independentKeyboard=true;app.leftHand.AdjustFinger(4,1);yield return new WaitForSeconds(.45f);
  Check(Quaternion.Angle(leftThumbBefore,leftThumb.localRotation)>20&&Quaternion.Angle(leftIndexBefore,leftIndex.localRotation)<5,"Left thumb bends independently on the SDK output");
  Check(app.statusText.text.Contains("SELECTED HAND: LEFT")&&FindAnyObjectByType<ApartmentChoices>().handButton.GetComponentInChildren<UnityEngine.UI.Text>().text=="LEFT HAND (SWITCH)","HUD and button identify the selected left hand");
  var neutral=CameraFixture(.5f,.5f,false,1,1);
  var leftNeutral=CameraFixture(.5f,.5f,false,1,1,"Left");
  for(int i=0;i<16;i++){app.rightHand.Pose(neutral,true,0,.05f,.045f);app.leftHand.Pose(leftNeutral,true,0,.05f,.045f);}
  var rightPalmRest=app.rightHand.transform.localRotation;
  var leftPalmRest=app.leftHand.transform.localRotation;
  foreach(var turn in new[]{new Vector3(75,0,0),new Vector3(0,75,0),new Vector3(0,0,75)}){
   var rotated=RotateCameraFixture(neutral,Quaternion.Euler(turn));
   var rotatedLeft=RotateCameraFixture(leftNeutral,Quaternion.Euler(turn));
   for(int i=0;i<16;i++){app.rightHand.Pose(rotated,true,0,.05f,.045f);app.leftHand.Pose(rotatedLeft,true,0,.05f,.045f);}
   Check(Quaternion.Angle(rightPalmRest,app.rightHand.transform.localRotation)>55&&
         Quaternion.Angle(leftPalmRest,app.leftHand.transform.localRotation)>55,
    "Both vendor hands follow a 75-degree physical palm turn around "+turn+" without the old 55-degree cap");
  }
  app.rightHand.RecenterTracking();app.leftHand.RecenterTracking();
  var recenterRight=RotateCameraFixture(neutral,Quaternion.Euler(0,0,75));
  var recenterLeft=RotateCameraFixture(leftNeutral,Quaternion.Euler(0,0,75));
  for(int i=0;i<16;i++){app.rightHand.Pose(recenterRight,true,0,.05f,.045f);app.leftHand.Pose(recenterLeft,true,0,.05f,.045f);}
  Check(Quaternion.Angle(rightPalmRest,app.rightHand.transform.localRotation)<5&&
        Quaternion.Angle(leftPalmRest,app.leftHand.transform.localRotation)<5,
   "Camera C recenter aligns either tracked hand with its vendor rest pose");
  // Upright first-person facing turns the old camera palm over. The worker
  // already mirrors X; a second reflection must not reverse wrist travel.
  app.rightHand.UseDemoPose();app.leftHand.UseDemoPose();
  var cameraRightRest=app.rightHand.transform.localPosition;
  var cameraLeftRest=app.leftHand.transform.localPosition;
  for(int i=0;i<16;i++){
   app.rightHand.Pose(neutral,true,0,.05f,.045f,cameraRightRest,.85f,true);
   app.leftHand.Pose(leftNeutral,true,0,.05f,.045f,cameraLeftRest,.85f,true);
  }
  var cameraFacingTurn=Quaternion.AngleAxis(180,Vector3.up)*Quaternion.AngleAxis(180,Vector3.forward);
  Check(Vector3.Dot(app.rightHand.transform.localRotation*Vector3.forward,cameraFacingTurn*rightPalmRest*Vector3.forward)>.99f&&
        Vector3.Dot(app.leftHand.transform.localRotation*Vector3.forward,cameraFacingTurn*leftPalmRest*Vector3.forward)>.99f&&
        Vector3.Dot(app.rightHand.transform.localRotation*Vector3.up,cameraFacingTurn*rightPalmRest*Vector3.up)>.99f&&
        Vector3.Dot(app.leftHand.transform.localRotation*Vector3.up,cameraFacingTurn*leftPalmRest*Vector3.up)>.99f,
   "Camera rest turns both vendor palms to the patient-facing side");
  var cameraRightCenter=app.rightHand.transform.localPosition;
  var cameraLeftCenter=app.leftHand.transform.localPosition;
  var upperLeftRight=CameraFixture(.35f,.35f,false,2,2);
  var upperLeftLeft=CameraFixture(.35f,.35f,false,2,2,"Left");
  for(int i=0;i<16;i++){
   app.rightHand.Pose(upperLeftRight,true,0,.05f,.045f,cameraRightRest,.85f,true);
   app.leftHand.Pose(upperLeftLeft,true,0,.05f,.045f,cameraLeftRest,.85f,true);
  }
  Check(app.rightHand.transform.localPosition.x<cameraRightCenter.x-.08f&&
        app.leftHand.transform.localPosition.x<cameraLeftCenter.x-.08f&&
        app.rightHand.transform.localPosition.y>cameraRightCenter.y+.08f&&
        app.leftHand.transform.localPosition.y>cameraLeftCenter.y+.08f,
   "Both wrists follow received image X while vertical travel retains its sign");
  VerifyWorldHandPoses(neutral,leftNeutral);
  app.rightHand.UseDemoPose();app.leftHand.UseDemoPose();
  for(int i=0;i<16;i++){app.rightHand.Pose(null,false,0,.05f,.045f);app.leftHand.Pose(null,false,0,.05f,.045f);}
  app.SelectHand(false);yield return new WaitForSeconds(.3f);
  var rightRest=app.rightHand.transform.localPosition;var leftRest=app.leftHand.transform.localPosition;
  app.SetDemoHandOffset(false,new Vector3(.08f,0,0));app.SetDemoHandOffset(true,new Vector3(-.08f,0,0));
  yield return new WaitForSeconds(.25f);
  Check(app.rightHand.transform.localPosition.x>rightRest.x+.04f&&app.leftHand.transform.localPosition.x<leftRest.x-.04f,"Keyboard/demo workspace moves each hand independently");
  app.SetDemoHandOffset(false,Vector3.zero);app.SetDemoHandOffset(true,Vector3.zero);yield return new WaitForSeconds(.25f);
  Check(app.statusText.text.Contains("SELECTED HAND: RIGHT")&&FindAnyObjectByType<ApartmentChoices>().handButton.GetComponentInChildren<UnityEngine.UI.Text>().text=="RIGHT HAND (SWITCH)","HUD and button identify the selected right hand");
  Check(player.transform.position.y>-.2f&&Physics.Raycast(player.transform.position+Vector3.up*.5f,Vector3.down,1f,~(1<<8)),"Player spawn is above the apartment floor");
  var ball=app.objects[0];var cracker=app.objects[2];var sponge=app.objects[1];
  var menu=FindAnyObjectByType<ApartmentChoices>();Check(menu!=null&&menu.objectButtons.Length==3&&menu.livingObjects.Length==3&&menu.kitchenObjects.Length==3,"Saved station and object UI references survive reload");
  player.SetPaused(true);yield return new WaitForSeconds(.3f);Check(menu.controls.interactable&&menu.controls.alpha>.9f,"Object menu appears when input is paused");yield return Screenshot("apartment-options");yield return new WaitForSeconds(.3f);player.SetPaused(false);
  player.Teleport(app.seats[0].standFeet.position,app.seats[0].standFeet.rotation);
  Check(player.Sit(app.seats[0]),"Living room chair can be used from the approach position");
  player.view.transform.LookAt(ball.transform.position);
  yield return new WaitForSeconds(.2f);
  Check(app.TryGrab(ball),"Therapy ball can be reached and grabbed from the chair");
  // In Editor the test drives the same force application explicitly; the executable uses its test flag.
  var ballMesh=ball.visual.GetComponent<MeshFilter>().sharedMesh;
  var ballRest=ballMesh.vertices;
  yield return Screenshot("vg-ball-before");
  yield return DriveForce(18,4.5f);
  yield return Screenshot("vg-ball-deformed");
  Check(ball.IsHeld&&ball.Body.isKinematic&&ball.Compression>.08f,"Held ball is kinematic and deforms under grip force; held="+ball.IsHeld+" kinematic="+ball.Body.isKinematic+" compression="+ball.Compression+" contacts="+ball.ContactCount+" ball="+ball.transform.position+" anchor="+app.holdAnchor.position+" hand="+app.rightHand.transform.position+" eye="+player.view.transform.position+" probes="+string.Join(";",app.contactRig.probes.Select(p=>p.finger+":"+p.segment+"="+(p.Contact==ball)+"@"+Vector3.Distance(p.transform.position,ball.transform.position).ToString("F3"))));
  var touching=app.contactRig.probes.Where(p=>ball.HasProbeContact(p,out _,out _)).ToArray();
  Check(touching.Length==ball.ContactCount&&touching.Length>0&&touching.Length<app.contactRig.ProbeCount,"Independent finger capsules contribute only when touching the ball ("+touching.Length+"/15); ball="+ball.transform.position+" colliderRadius="+ball.GetComponent<SphereCollider>().radius+" probes="+string.Join(";",app.contactRig.probes.Select(p=>p.finger+":"+p.segment+"="+Vector3.Distance(p.transform.position,ball.transform.position).ToString("F3"))));
  Check(touching.All(p=>p.ResolvedForceNewtons>0&&p.ContactAreaSquareMeters>0)&&app.contactRig.probes.Where(p=>!ball.HasProbeContact(p,out _,out _)).All(p=>p.ResolvedForceNewtons==0),"Noncontacting fingers resolve exactly zero force");
  var moved=ballMesh.vertices;int changed=0,far=0,farMoved=0;
  for(int vertex=0;vertex<moved.Length;vertex++){
   float displacement=Vector3.Distance(moved[vertex],ballRest[vertex])*ball.visual.lossyScale.x;
   if(displacement>.00005f)changed++;
   var world=ball.visual.TransformPoint(ballRest[vertex]);
   if(touching.All(p=>Vector3.Distance(world,p.Point)>ball.profile.deformationRadiusMeters+.015f)){far++;if(displacement>.0005f)farMoved++;}
  }
  Check(changed>0&&changed<moved.Length&&far>0&&farMoved==0&&ball.MaxDisplacementMeters>.003f,"Mesh vertices deform near finger contacts while distant regions remain stable ("+changed+" moved, "+far+" distant, "+farMoved+" distant moved)");
  Check(app.ProgressSeconds>=3,"A valid 18 N hold completes the three-second exercise; progress="+app.ProgressSeconds.ToString("F2"));
  Check(app.rightHand.Closure>.4f,"Rigged fingers close around a held object");
  var beforeHandMove=ball.transform.position;app.SetDemoHandOffset(false,new Vector3(.07f,0,0));yield return new WaitForSeconds(.35f);
  Check(Vector3.Dot(ball.transform.position-beforeHandMove,player.view.transform.right)>.025f,"Held object follows right hand workspace movement");
  app.SetDemoHandOffset(false,Vector3.zero);yield return new WaitForSeconds(.25f);
  yield return Screenshot("apartment-seated-grip");
  var height=ball.transform.position.y;app.TestForce=0;app.Release();
  yield return new WaitForSeconds(.9f);
  Check(!ball.Body.isKinematic&&ball.transform.position.y<height-.04f&&ball.transform.position.y>-.3f,"Released ball falls and collides with the furnished apartment");
  app.ResetObjects();yield return new WaitForSeconds(.2f);
  menu.objectButtons[1].onClick.Invoke();yield return null;
  var mug=menu.livingObjects[1];
  Check(mug.gameObject.activeSelf&&!ball.gameObject.activeSelf,"Living room button selects the hollow porcelain mug");
  Check(app.TryGrab(mug),"Porcelain mug can be picked up from the chair");
  yield return DriveForce(70,1.5f);
  Check(mug.IsBroken&&mug.FragmentsVisible&&mug.FragmentCount>1,"Porcelain mug fractures into visible surface pieces under sustained excessive demo force");yield return Screenshot("vg-mug-fractured");
  mug.ResetObject();mug.transform.position+=Vector3.up*.85f;mug.Body.position=mug.transform.position;
  yield return new WaitForSeconds(.85f);
  Check(mug.IsBroken,"A sufficiently energetic porcelain-mug drop causes impact fracture");
  app.TestForce=0;menu.objectButtons[2].onClick.Invoke();yield return null;
  Check(cracker.gameObject.activeSelf&&!mug.gameObject.activeSelf,"Living room button selects the brittle cracker");
  player.view.transform.LookAt(cracker.transform.position);
  Check(app.TryGrab(cracker),"Brittle object can be picked up");
  yield return DriveForce(65,1.4f);
  Check(cracker.IsBroken&&!cracker.visual.gameObject.activeSelf&&app.Held==null,"Sustained high force fractures the virtual cracker");
  app.TestForce=0;app.ResetObjects();yield return new WaitForSeconds(.2f);
  Check(!cracker.IsBroken&&cracker.visual.gameObject.activeSelf&&!cracker.FragmentsVisible&&cracker.DamageValue==0&&cracker.MaxDisplacementMeters==0,"Reset restores broken objects, damage, mesh and removes fragments");
  Check(player.Stand(),"Seated player can stand into a clear area");
  Check(player.Sit(app.seats[0]),"Player can sit again before changing stations");
  var blockedStand=GameObject.CreatePrimitive(PrimitiveType.Cube);
  blockedStand.transform.SetPositionAndRotation(app.seats[0].standFeet.position+Vector3.up*.8f,Quaternion.identity);
  blockedStand.transform.localScale=new Vector3(.6f,1.6f,.6f);Physics.SyncTransforms();
  Check(!player.Stand(),"Obstructed chair prevents an unsafe local stand-up");
  var kitchen=GameObject.Find("Kitchen standing station").transform;
  menu.kitchenButton.onClick.Invoke();
  Check(menu.Kitchen&&!player.IsSeated&&Vector3.Distance(player.transform.position,kitchen.position)<.05f,
   "Kitchen station teleports a seated player even when the old stand-up area is blocked");
  Destroy(blockedStand);
  menu.objectButtons[1].onClick.Invoke();yield return null;
  var cup=menu.kitchenObjects[1];Check(cup.gameObject.activeSelf&&app.TryGrab(cup),"Kitchen silicone cup is selectable and can be gripped");
  var cupCollider=cup.GetComponent<Collider>();
  Check(Physics.GetIgnoreCollision(cupCollider,player.Controller),"A held object cannot push the standing player's CharacterController backwards");
  var cupInitialRotation=cup.Body.rotation;var palmInitialRotation=app.ActiveHand.transform.rotation;
  var cupOffset=GraspMotionPolicy.RelativeRotation(palmInitialRotation,cupInitialRotation);
  Check(Quaternion.Angle(GraspMotionPolicy.HeldRotation(palmInitialRotation,cupOffset),cupInitialRotation)<.1f,"Pickup preserves cup orientation");

  yield return DriveForce(30,.6f);Check(cup.Compression>.2f&&cup.MaxDisplacementMeters>.003f,"Silicone cup visibly yields to demo grip force; compression="+cup.Compression+" contacts="+cup.ContactCount+" force="+app.ContactForceNewtons+" hand="+app.rightHand.transform.position+" cup="+cup.transform.position+" probes="+string.Join(";",app.contactRig.probes.Select(p=>p.finger+":"+p.segment+"="+Vector3.Distance(p.transform.position,cup.transform.position).ToString("F3"))));
  var playerBeforeCupOverlap=player.transform.position;
  cup.Body.position=player.Controller.bounds.center;Physics.SyncTransforms();
  player.Controller.Move(Vector3.down*.01f);
  var drift=Vector2.Distance(new Vector2(playerBeforeCupOverlap.x,playerBeforeCupOverlap.z),
                             new Vector2(player.transform.position.x,player.transform.position.z));
  Check(drift<.015f,"Overlapping held cup cannot displace the standing player horizontally; drift="+drift.ToString("F4")+" m");
  app.Release();Check(Physics.GetIgnoreCollision(cupCollider,player.Controller),"Overlapping release retains player exclusion until separation");
  Check(!cup.Body.isKinematic&&cup.Body.useGravity,"Dropped object remains dynamic with gravity");
  var releasePlayer=player.transform.position;player.Controller.Move(Vector3.down*.01f);
  Check(Vector2.Distance(new Vector2(releasePlayer.x,releasePlayer.z),new Vector2(player.transform.position.x,player.transform.position.z))<.015f,"Dropped overlapping cup does not recoil the player");
  cup.Body.position=player.Controller.bounds.center+Vector3.up*3;Physics.SyncTransforms();yield return new WaitForFixedUpdate();yield return null;
  Check(!Physics.GetIgnoreCollision(cupCollider,player.Controller),"Separated drop restores its original player collision pair");
  app.TestForce=0;menu.objectButtons[2].onClick.Invoke();yield return null;
  var tomato=menu.kitchenObjects[2];Check(tomato.gameObject.activeSelf&&app.TryGrab(tomato),"Kitchen tomato is selectable and can be gripped");
  yield return DriveForce(30,.6f);Check(tomato.Compression>.18f&&tomato.MaxDisplacementMeters>.003f,"Tomato deforms under demo grip force");
  app.TestForce=0;menu.objectButtons[0].onClick.Invoke();yield return null;
  Check(sponge.gameObject.activeSelf&&!tomato.gameObject.activeSelf,"Kitchen object button restores the sponge");
  player.Teleport(kitchen.position,kitchen.rotation);player.view.transform.LookAt(sponge.transform.position);
  yield return new WaitForSeconds(.2f);
  Check(Physics.Raycast(kitchen.position+Vector3.up*.5f,Vector3.down,1f,~(1<<8)),"Kitchen station has a supporting floor");
  Check(app.TryGrab(sponge),"Kitchen sponge can be reached without passing through furniture");
  yield return DriveForce(30,.6f);
  Check(sponge.Compression>.25f,"Kitchen sponge has its own softer deformation response");
  yield return Screenshot("apartment-kitchen-grip");
  app.TestForce=0;app.ResetObjects();
  app.SetHardware(true);yield return null;
  Check(!app.ForceValid&&app.ForceNewtons==0,"Hardware mode waits for data and never substitutes demo force");
  using(var udp=new UdpClient()) {
   var frame=new ForceBridgeFrame{schemaVersion=1,kind="sample",source="synthetic",sessionId="apartment-test",connectionId="a",calibrationRevision=1,calibrationCrc=1234,shapeId=1,sequence=1,sampleMs=100,forceMilliNewtons=18000,flags=3,ageMs=0};
   Send(udp,27818,JsonUtility.ToJson(frame));yield return new WaitForSeconds(.12f);
   bool replay=Array.IndexOf(Environment.GetCommandLineArgs(),"-rehab-force-replay")>=0;
   Check(app.forceReceiver.Latest.Valid==replay,replay?"Explicit synthetic replay is accepted":"Synthetic replay is rejected by default");
   if(replay){
    yield return new WaitForSeconds(.26f);
    Check(FindAnyObjectByType<RehabAppShell>().hudSource.text=="SYNTHETIC REPLAY",
     "Premium HUD labels accepted Phase 2 test input as synthetic replay");
   }
   frame.source="hardware";frame.sessionId="envelope-simulation";frame.connectionId="b";
   for(int i=0;i<4;i++){frame.sequence++;frame.sampleMs+=50;Send(udp,27818,JsonUtility.ToJson(frame));yield return new WaitForSeconds(.05f);}
   Check(app.ForceValid&&Mathf.Abs(app.ForceNewtons-18)<.01f,"Phase 2 UDP envelope converts 18000 milliNewtons to 18 N");
   int samples=app.forceReceiver.State.SampleCount;Send(udp,27818,JsonUtility.ToJson(frame));yield return new WaitForSeconds(.05f);
   Check(app.forceReceiver.State.SampleCount==samples,"Duplicate UDP sample does not advance force input");
   Check(app.TryGrab(sponge),"Validated Phase 2 envelope enables gripping");
   frame.sequence++;frame.sampleMs+=50;frame.forceMilliNewtons=500000;Send(udp,27818,JsonUtility.ToJson(frame));yield return new WaitForSeconds(.07f);
   Check(app.ContactForceNewtons<30,"One isolated calibrated-force spike does not reach contact damage");frame.forceMilliNewtons=18000;

   yield return new WaitForSeconds(.65f);
   Check(!app.ForceValid&&app.Held==null&&app.ForceNewtons==0,"Stale hardware input releases object and stops force within 500 ms plus a frame");
   var staleHud=FindAnyObjectByType<RehabAppShell>();
   Check(staleHud.hudSource.text=="PHASE 2 · WAITING"&&staleHud.hudValue.text=="--",
    "Premium HUD clears stale Phase 2 values and shows a waiting source");
   frame.sequence++;frame.sampleMs+=1000;Send(udp,27818,JsonUtility.ToJson(frame));yield return new WaitForSeconds(.08f);
   Check(app.ForceValid,"New data recovers after interruption");
   app.ResetObjects();yield return null;Check(app.TryGrab(sponge),"Object can be grabbed after recovery");
   frame.sequence++;frame.sampleMs+=100;frame.calibrationRevision++;frame.calibrationCrc++;Send(udp,27818,JsonUtility.ToJson(frame));
   float calibrationDeadline=Time.realtimeSinceStartup+.45f;
   while(app.Held!=null&&Time.realtimeSinceStartup<calibrationDeadline)yield return null;
   Check(app.Held==null,"Calibration change ends the previous grip within 450 ms; generation="+
    app.forceReceiver.State.Generation+" samples="+app.forceReceiver.State.SampleCount);
   Send(udp,27818,"not json");yield return null;
   var left=HandFixture("Left",.34f,1,1);
   var right=HandFixture("Right",.66f,1,1);
   Send(udp,27817,JsonUtility.ToJson(left));Send(udp,27817,JsonUtility.ToJson(right));
   yield return new WaitForSeconds(.15f);
   Check(app.vision.LeftValid&&app.vision.RightValid&&app.leftHand.TrackingUsed&&app.rightHand.TrackingUsed,"Both independent MediaPipe-style 21-point hands drive the SDK input rigs");
   Check(app.statusText.text.Contains("SYNTHETIC"),"HUD identifies synthetic vision frames instead of claiming a live camera");
    Check(app.leftHand.JointAngles[4]>5&&app.rightHand.JointAngles[4]>5,"Measured middle finger PIP bending reaches both input rigs");
    yield return Screenshot("apartment-two-hands-tracked");
    // PNG encoding can outlast the 500 ms vision validity window. Re-establish
    // a fresh wrist reference before testing displacement from it.
    for(int i=0;i<3;i++){
     left.sequence++;left.captureSeconds+=.06;
     Send(udp,27817,JsonUtility.ToJson(left));yield return new WaitForSeconds(.06f);
    }
    var leftBefore=app.leftHand.transform.localPosition;
    left.sequence++;left.captureSeconds+=.06;
   for(int i=0;i<left.landmarks.Length;i++)left.landmarks[i].x+=.08f;
    for(int i=0;i<4;i++){
     if(i>0){left.sequence++;left.captureSeconds+=.06;}
     Send(udp,27817,JsonUtility.ToJson(left));yield return new WaitForSeconds(.06f);
    }
    Check(app.leftHand.transform.localPosition.x>leftBefore.x+.005f,
     "Tracked wrist displacement moves the left hand; before="+leftBefore.x.ToString("F3")+
     " after="+app.leftHand.transform.localPosition.x.ToString("F3"));
   var acceptedLeft=app.vision.LatestLeft;
   Send(udp,27817,JsonUtility.ToJson(left));yield return new WaitForSeconds(.04f);
   Check(ReferenceEquals(app.vision.LatestLeft,acceptedLeft),"Duplicate vision frame is rejected");
   var aged=HandFixture("Left",.34f,2.5,3);aged.ageMs=600;Send(udp,27817,JsonUtility.ToJson(aged));yield return new WaitForSeconds(.04f);
   Check(ReferenceEquals(app.vision.LatestLeft,acceptedLeft),"Already stale captured frame is rejected before queueing");
   left.captureSeconds=1.5;left.sequence=3;
   Send(udp,27817,JsonUtility.ToJson(left));yield return new WaitForSeconds(.04f);
   Check(ReferenceEquals(app.vision.LatestLeft,acceptedLeft),"Out-of-order vision frame is rejected");
   left.handedness="Unknown";Send(udp,27817,JsonUtility.ToJson(left));yield return null;
   Check(ReferenceEquals(app.vision.LatestLeft,acceptedLeft),"Invalid handedness cannot overwrite a valid pose");
   right.sequence=2;right.captureSeconds=3;
   for(int i=0;i<8;i++){
    right.sequence++;right.captureSeconds+=.08;
    Send(udp,27817,JsonUtility.ToJson(right));
    yield return new WaitForSeconds(.08f);
   }
   Check(!app.vision.LeftValid&&app.vision.RightValid,"One lost hand expires independently while the other remains tracked");
   yield return new WaitForSeconds(.6f);
   Check(!app.vision.TrackingValid&&!app.leftHand.TrackingUsed&&!app.rightHand.TrackingUsed,"Both input hands leave tracking mode after the 500 ms timeout");
  }
  app.SetHardware(false);app.ResetObjects();
  menu.SelectStation(true);menu.SelectObject(0);app.SelectHand(true);yield return new WaitForSeconds(.3f);
  Check(app.TryGrab(sponge),"Left hand can pick up the kitchen sponge");
  yield return DriveForce(35,.8f);
  Check(sponge.ContactCount>0&&sponge.MaxDisplacementMeters>.003f,"Left hand vendor joints make visible, localized sponge deformation: contacts="+sponge.ContactCount+" depth="+sponge.MaxDisplacementMeters);
  yield return Screenshot("vg-left-kitchen-grip");app.TestForce=0;app.Release();app.SelectHand(false);
  player.Teleport(player.Spawn,Quaternion.Euler(0,90,0));player.view.transform.localRotation=Quaternion.Euler(12,0,0);
  yield return Screenshot("apartment-overview");
  // Camera interaction contract uses explicit synthetic 21-point frames; the
  // physical webcam stays closed in this suite so the replay is reproducible.
  app.SetCameraMode();
  Check(app.CameraMode&&!app.Hardware&&!app.cameraLauncher.IsRunning,"Camera test mode does not silently open the physical webcam");
  menu.SelectStation(false);menu.SelectObject(0);app.SelectHand(false);player.SetPaused(false);
  using(var cameraUdp=new UdpClient()){
   long seq=100;double captured=100;
   player.SetPaused(true);
   foreach(bool left in new[]{false,true}){
    app.SelectHand(left);
    var worldOpen=WorldCameraFixture(CameraFixture(.5f,.5f,false,++seq,captured+=.06,left?"Left":"Right"),Quaternion.identity);
    for(int i=0;i<6;i++){worldOpen.sequence=++seq;worldOpen.captureSeconds=captured+=.06;Send(cameraUdp,27817,JsonUtility.ToJson(worldOpen));yield return new WaitForSeconds(.06f);}
    VirtualGrasp.VG_Controller.GetFingerBone(sdk.AvatarId,left?VirtualGrasp.VG_HandSide.LEFT:VirtualGrasp.VG_HandSide.RIGHT,1,1,out Transform trackedIndex);
    VirtualGrasp.VG_Controller.GetFingerBone(sdk.AvatarId,left?VirtualGrasp.VG_HandSide.LEFT:VirtualGrasp.VG_HandSide.RIGHT,2,1,out Transform trackedMiddle);
    var worldIndexBefore=trackedIndex.localRotation;var worldMiddleBefore=trackedMiddle.localRotation;
    worldOpen.worldLandmarks[7]=worldOpen.worldLandmarks[6]+new Vector3(0,0,.04f);
    worldOpen.worldLandmarks[8]=worldOpen.worldLandmarks[7]+new Vector3(0,0,.02f);
    for(int i=0;i<6;i++){worldOpen.sequence=++seq;worldOpen.captureSeconds=captured+=.06;Send(cameraUdp,27817,JsonUtility.ToJson(worldOpen));yield return new WaitForSeconds(.06f);}
    Check(app.ActiveHand.UsingWorldLandmarks&&app.ActiveHand.LastPoseSequence==worldOpen.sequence&&
          Quaternion.Angle(worldIndexBefore,trackedIndex.localRotation)>40&&Quaternion.Angle(worldMiddleBefore,trackedMiddle.localRotation)<5,
          (left?"Left":"Right")+" synthetic v1 world-pose extension traverses UDP and drives the imported SDK index independently");
   }
   app.SelectHand(false);player.SetPaused(false);
   for(int i=0;i<6;i++){
    Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,false,++seq,captured+=.06)));
    yield return new WaitForSeconds(.06f);
   }
   Check(app.CameraMode&&!app.LeftSelected&&app.ForceValid&&HandKinematics.GripScore(app.vision.LatestRight)<.2f,"Detected right hand automatically selects and drives the right vendor hand");
   player.SetPaused(true);
   for(int i=0;i<3;i++){
    Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,false,++seq,captured+=.06)));
    yield return new WaitForSeconds(.06f);
   }
   Check(app.rightHand.TrackingUsed&&!app.ForceValid,"Live camera fingers keep moving while the start menu is open without enabling grasp");
   player.SetPaused(false);

   var wristBefore=app.rightHand.transform.localPosition;
   for(int i=0;i<5;i++){
    Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.32f,.5f,false,++seq,captured+=.06)));
    yield return new WaitForSeconds(.06f);
   }
   Check(app.rightHand.transform.localPosition.x<wristBefore.x-.05f,"Camera wrist follows received horizontal movement without a second reflection");
   foreach(var imageY in new[]{.45f,.5f,.55f,.65f,.75f,.85f}){
    foreach(var imageX in new[]{.5f,.4f,.3f,.2f,.6f,.7f}){
     for(int i=0;i<4;i++){
      Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(imageX,imageY,true,++seq,captured+=.06)));
      yield return new WaitForSeconds(.06f);
     }
     if(app.Held==ball)break;
    }
    if(app.Held==ball)break;
   }
   float nearest=float.PositiveInfinity;var ballCollider=ball.GetComponent<Collider>();
   foreach(var bone in app.contactRig.contactBones)if(bone!=null)nearest=Mathf.Min(nearest,Vector3.Distance(bone.position,ballCollider.ClosestPoint(bone.position)));
   Check(app.Held==ball,"Closing the camera hand near the ball grabs without E; nearest finger="+nearest.ToString("F3")+"m hand="+app.rightHand.transform.position+" ball="+ball.transform.position+" score="+app.CameraGripScore.ToString("F2"));
   for(int i=0;i<6;i++){
    Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,false,++seq,captured+=.06)));
    yield return new WaitForSeconds(.06f);
   }
   Check(app.Held==null&&app.CameraMode,"Opening the tracked hand releases the ball");
   Check(app.TryCameraSummonSelectedObject()&&app.CameraMode&&app.Held==ball,
    "G brings the selected living-room object into the tracked right hand without changing camera mode");
   yield return new WaitForFixedUpdate();
   Check(Vector3.Distance(ball.transform.position,app.rightHand.transform.position)<.5f,
    "Summoned object is positioned at the right hand without requiring camera depth alignment");
   for(int i=0;i<24;i++){
    Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,false,++seq,captured+=.06)));
    yield return new WaitForSeconds(.06f);
   }
   Check(app.Held==null&&app.CameraMode,"Summoned object drops if the tracked hand stays open beyond the grace period");
   for(int i=0;i<5;i++){
    Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,true,++seq,captured+=.06)));
    yield return new WaitForSeconds(.06f);
   }
   yield return new WaitForSeconds(.6f);
   Check(!app.ForceValid&&app.ForceNewtons==0&&app.Held==null,"Stale camera landmarks clear visual pressure and cannot keep holding");
    var staleShell = FindAnyObjectByType<RehabAppShell>();
    var staleHud = staleShell.hudValue.text;
    var refreshField=typeof(RehabAppShell).GetField("nextRefresh",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
    Check(staleHud=="--",
     "Premium HUD does not show zero as a live camera closure after tracking loss; actual="+staleHud+
     " page="+staleShell.CurrentPage+" blocks="+staleShell.BlocksGameplay+
     " enabled="+staleShell.enabled+" active="+staleShell.gameObject.activeInHierarchy+
     " sameExperience="+(staleShell.experience==app)+" shellValid="+staleShell.experience.ForceValid+
     " now="+Time.realtimeSinceStartup.ToString("F2")+" next="+refreshField.GetValue(staleShell));
   Check(!app.TryCameraSummonSelectedObject(),"G cannot summon an object without a fresh tracked hand");
   app.SelectHand(true);menu.SelectStation(true);menu.SelectObject(0);player.SetPaused(false);
   Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,true,++seq,captured+=.06)));
   yield return new WaitForSeconds(.08f);
   Check(!app.LeftSelected&&app.ForceValid&&app.Held==null,"Right camera data automatically switches the selected hand from left to right");
   long leftSeq=1000;double leftCaptured=1000;
   for(int i=0;i<6;i++){
    Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,false,++leftSeq,leftCaptured+=.06,"Left")));
    yield return new WaitForSeconds(.06f);
   }
   Check(app.LeftSelected&&app.ForceValid&&app.leftHand.TrackingUsed&&app.virtualGrasp.outputMesh.sharedMesh==app.virtualGrasp.leftOnlyMesh,
    "Left camera data automatically selects and drives the left vendor mesh");
   Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,false,++seq,captured+=.06)));
   Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,false,++leftSeq,leftCaptured+=.06,"Left")));
   yield return new WaitForSeconds(.07f);
   Check(app.LeftSelected,"Seeing both hands briefly does not switch away from the actively tracked left hand");
   foreach(var imageY in new[]{.5f,.7f,.85f}){
    foreach(var imageX in new[]{.5f,.4f,.3f,.2f,.6f,.7f,.8f}){
     for(int i=0;i<4;i++){
      Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(imageX,imageY,true,++leftSeq,leftCaptured+=.06,"Left",2.2f)));
      yield return new WaitForSeconds(.06f);
     }
     if(app.Held==sponge)break;
    }
    if(app.Held==sponge)break;
   }
   float leftNearest=float.PositiveInfinity;var spongeCollider=sponge.GetComponent<Collider>(); foreach(var bone in app.contactRig.contactBones)if(bone!=null)leftNearest=Mathf.Min(leftNearest,Vector3.Distance(bone.position,spongeCollider.ClosestPoint(bone.position)));
   Check(app.Held==sponge,"Selected left camera fist can pick up the kitchen sponge without E; nearest="+leftNearest.ToString("F3")+" hand="+app.leftHand.transform.position+" sponge="+sponge.transform.position+" score="+app.CameraGripScore.ToString("F2"));
   for(int i=0;i<6;i++){
    Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,false,++leftSeq,leftCaptured+=.06,"Left")));
    yield return new WaitForSeconds(.06f);
   }
   Check(app.Held==null,"Opening the left camera hand drops the sponge");
   Check(app.TryCameraSummonSelectedObject()&&app.CameraMode&&app.Held==sponge,
    "G brings the selected kitchen object into the tracked left hand without changing camera mode");
   yield return new WaitForFixedUpdate();
   Check(Vector3.Distance(sponge.transform.position,app.leftHand.transform.position)<.5f,
    "Summoned kitchen object is positioned at the left hand");
   for(int i=0;i<24;i++){
    Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,true,++leftSeq,leftCaptured+=.06,"Left")));
    yield return new WaitForSeconds(.06f);
   }
   Check(app.Held==sponge&&app.CameraMode,"Closing the left hand within the G grace period keeps the summoned object held");
   for(int i=0;i<6;i++){
    Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,false,++leftSeq,leftCaptured+=.06,"Left")));
    yield return new WaitForSeconds(.06f);
   }
   Check(app.Held==null,"Opening the left hand after a G grip releases the object");
   app.SelectHand(false);menu.SelectStation(false);menu.SelectObject(0);player.SetPaused(false);
   player.view.transform.LookAt(ball.transform.position);
   Check(app.TryKeyboardAction()&&!app.CameraMode&&app.Held==ball,
    "E keyboard grip works immediately from camera mode and explicitly selects demo");
   app.Release();
   app.SetCameraMode();
   Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,false,1,1)));
   yield return new WaitForSeconds(.08f);
   Check(app.vision.RightValid&&app.rightHand.TrackingUsed,
    "Camera restart accepts a new MediaPipe sequence starting at one");
  }
  app.SetHardware(false);app.ResetObjects();player.enabled=true;player.SetPaused(true);
  var shell=FindAnyObjectByType<RehabAppShell>();
  Check(shell!=null&&shell.root!=null&&shell.homePanel!=null&&shell.modulePanel!=null&&shell.profilePanel!=null&&
   shell.patientPanel!=null&&shell.doctorPanel!=null&&shell.gameplayLayers.Length>=4,
   "Saved main menu, profile, module, dashboard and gameplay HUD references are complete");
  Check(shell.root.GetComponent<UnityEngine.UI.Image>().color.a>=.999f&&
   shell.continueButton.GetComponentInChildren<UnityEngine.UI.Text>().text=="CONTINUE PRACTICE",
   "Main menu backdrop hides gameplay text and Continue is labelled as practice recovery");
  Check(new[]{shell.profilePicker,shell.genderPicker,shell.affectedPicker,shell.dominantPicker}
   .All(d=>d.transform.Find("Arrow/Chevron")!=null),
   "Saved profile dropdowns have visible chevrons instead of missing sprite squares");
  Check(shell.root.GetComponent<UnityEngine.UI.Image>().sprite!=null&&
   shell.startButton.image.sprite!=null&&shell.openModuleButton.image.sprite!=null,
   "Premium menu background and rounded controls retain imported sprite references");
  Check(shell.hudRoom!=null&&shell.hudValue!=null&&shell.hudUnit!=null&&
   shell.hudPrompt!=null&&shell.hudProgressFill!=null,
   "Saved in-game HUD has readable station, source, unit, guidance and progress bindings");
  var blockedProfileDirectory=Path.Combine(Application.temporaryCachePath,"grip-profile-blocked-"+Guid.NewGuid().ToString("N"));
  File.WriteAllText(blockedProfileDirectory,"not a directory");
  var blockedStore=new RehabProfileStore(blockedProfileDirectory);
  bool saveFailed=false;
  try{blockedStore.Create("Unsaved Patient",29,"","Right","Right","","",-1);}
  catch(IOException){saveFailed=true;}
  Check(saveFailed&&blockedStore.Catalog.profiles.Count==0&&blockedStore.Active==null,
   "Failed profile persistence leaves no unsaved patient selectable in memory");
  File.Delete(blockedProfileDirectory);
  var testProfileDirectory=Path.Combine(Application.temporaryCachePath,"grip-profile-qa-"+Guid.NewGuid().ToString("N"));
  shell.SetStoreForAcceptance(new RehabProfileStore(testProfileDirectory));
  shell.ShowHome();
  Check(shell.BlocksGameplay&&shell.CurrentPage=="Home"&&player.Paused&&!shell.continueButton.interactable,
   "First launch opens a paused home menu and disables Continue without a saved session");
  Check(shell.startButton.IsInteractable()&&shell.newProfileButton.IsInteractable(),
   "First-launch Start and New Profile buttons are actually clickable");
  yield return new WaitForSecondsRealtime(.2f);
  Canvas.ForceUpdateCanvases();
  yield return Screenshot("ui-main-menu");
  shell.startButton.onClick.Invoke();
  Check(shell.CurrentPage=="Modules"&&shell.Store.Active==null&&
   shell.moduleGreeting.text.Contains("GUEST"),
   "Start opens hands-free guest practice without inventing patient data");
  shell.cognitiveLockedButton.onClick.Invoke();
  Check(shell.CurrentPage=="Activity"&&shell.Store.Active==null,
   "Guest can launch playable cognitive exercises without creating a profile");
  shell.ShowHome();shell.newProfileButton.onClick.Invoke();
  Check(shell.CurrentPage=="Profile","New Profile opens the assisted registration form");
  Check(shell.saveProfileButton.IsInteractable(),"Profile form Save button is actually clickable");
  yield return new WaitForSecondsRealtime(.2f);
  yield return Screenshot("ui-new-profile");
  shell.saveProfileButton.onClick.Invoke();
  Check(shell.Store.Active==null&&shell.formError.text.Contains("age"),
   "Incomplete profile stays on the form with a visible validation message");
  shell.nameField.text="Acceptance Patient";shell.ageField.text="29";shell.concernField.text="Reduced grip comfort";
  shell.goalField.text="Hold a cup";shell.painField.text="4";
  shell.affectedPicker.value=1;shell.dominantPicker.value=0;
  shell.saveProfileButton.onClick.Invoke();
  Check(shell.CurrentPage=="Modules"&&shell.Store.Active!=null&&shell.Store.Active.affectedHand=="Left"&&
   shell.Store.Active.painToday==4&&shell.Store.Active.goal=="Hold a cup",
   "New profile saves self-reported hand, goal and optional pain accurately; "+
   $"page={shell.CurrentPage}, error={shell.formError.text}, active={shell.Store.Active?.name}, "+
   $"hand={shell.Store.Active?.affectedHand}, pain={shell.Store.Active?.painToday}, goal={shell.Store.Active?.goal}");
  Check(shell.cognitiveLockedButton.IsInteractable()&&shell.visuomotorLockedButton.IsInteractable()&&
   shell.openModuleButton.IsInteractable(),"All three research modules are selectable");
  shell.cognitiveLockedButton.onClick.Invoke();
  Check(shell.CurrentPage=="Activity"&&shell.Store.Active.lastModule==2,
   "Module 02 opens its exercises and saves a resumable module checkpoint");
  shell.ShowModules();shell.visuomotorLockedButton.onClick.Invoke();
  Check(shell.CurrentPage=="Activity"&&shell.Store.Active.lastModule==3,
   "Module 03 opens its exercises and saves a resumable module checkpoint");
  shell.ShowModules();
  yield return new WaitForSecondsRealtime(.2f);
  yield return Screenshot("ui-module-selection");
  shell.openModuleButton.onClick.Invoke();
  Check(!shell.BlocksGameplay&&!player.Paused&&app.CameraMode&&shell.Store.Active.hasSession,
   "Open Module 01 begins camera mode and creates a resumable checkpoint");
  Check(UnityEngine.EventSystems.EventSystem.current!=null&&
   !UnityEngine.EventSystems.EventSystem.current.sendNavigationEvents&&
   UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject==null,
   "Space grip cannot submit a selected uGUI menu or quit button during active play");
  menu.SelectStation(true);menu.SelectObject(1);app.SelectHand(true);app.SetHardware(false);
  shell.gameplayHomeButton.onClick.Invoke();
  Check(shell.CurrentPage=="Home"&&shell.BlocksGameplay&&shell.continueButton.interactable&&
   shell.Store.Active.kitchen&&shell.Store.Active.kitchenObject==1&&shell.Store.Active.leftHand&&
   shell.Store.Active.inputMode==1,"Main Menu saves kitchen, object, hand and demo mode for Continue");
  var reloaded=new RehabProfileStore(testProfileDirectory);
  Check(reloaded.Active!=null&&reloaded.Active.id==shell.Store.Active.id&&reloaded.Active.kitchenObject==1,
   "Saved profile and session checkpoint survive a fresh disk load");
  var protectedPath=Path.Combine(testProfileDirectory,"profiles.dat");
  Check(File.Exists(protectedPath)&&File.Exists(protectedPath+".bak")&&
   !File.Exists(Path.Combine(testProfileDirectory,"profiles.json"))&&
   !Encoding.UTF8.GetString(File.ReadAllBytes(protectedPath)).Contains("Acceptance Patient"),
   "Profiles persist as encrypted primary/backup without plaintext names");
  var legacyDirectory=Path.Combine(Application.temporaryCachePath,"grip-profile-legacy-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(legacyDirectory);
  var legacy=new RehabProfileCatalog{activeId="fixture-legacy"};
  legacy.profiles.Add(new RehabProfile{id="fixture-legacy",name="Synthetic Legacy Patient",age=42,
   affectedHand="Right",dominantHand="Left",createdUtc="2026-09-01T00:00:00Z"});
  File.WriteAllText(Path.Combine(legacyDirectory,"profiles.json"),JsonUtility.ToJson(legacy,true));
  var migrated=new RehabProfileStore(legacyDirectory);
  var migratedPath=Path.Combine(legacyDirectory,"profiles.dat");
  Check(migrated.Active!=null&&migrated.Active.name=="Synthetic Legacy Patient"&&
   File.Exists(migratedPath)&&File.Exists(migratedPath+".bak")&&
   !File.Exists(Path.Combine(legacyDirectory,"profiles.json"))&&!migrated.MigrationCleanupRequired,
   "Legacy profile migration verifies encrypted primary/backup before plaintext removal");
  var damaged=File.ReadAllBytes(migratedPath);damaged[damaged.Length/2]^=0x55;
  File.WriteAllBytes(migratedPath,damaged);
  var recovered=new RehabProfileStore(legacyDirectory);
  Check(recovered.Active!=null&&recovered.Active.name=="Synthetic Legacy Patient"&&
   WindowsProfileProtection.Unprotect(File.ReadAllBytes(migratedPath)).Length>0,
   "Tampered encrypted primary recovers from encrypted backup");
  Directory.Delete(legacyDirectory,true);
  shell.continueButton.onClick.Invoke();
  Check(!shell.BlocksGameplay&&!player.Paused&&!app.CameraMode&&!app.Hardware&&menu.Kitchen&&
   menu.KitchenSelection==1&&app.LeftSelected,"Continue restores Module 01 location, object, hand and input mode");
  shell.gameplayHomeButton.onClick.Invoke();
  shell.patientButton.onClick.Invoke();
  Check(shell.CurrentPage=="Patient"&&shell.patientSummary.text.Contains("Acceptance Patient")&&
   shell.patientSummary.text.Contains("No saved session")==false,
   "Patient preview shows the selected profile and a real saved-session state");
  shell.homeFromPatientButton.onClick.Invoke();shell.doctorButton.onClick.Invoke();
  Check(shell.CurrentPage=="Doctor"&&shell.doctorSummary.text.Contains("RESEARCH DASHBOARD"),
   "Doctor dashboard labels local game scores as research data");
  shell.homeFromDoctorButton.onClick.Invoke();
  Check(shell.CurrentPage=="Home"&&player.Paused,"Dashboard navigation returns to the home menu");
  shell.newProfileButton.onClick.Invoke();
  shell.nameField.text="Second Patient";shell.ageField.text="44";
  shell.saveProfileButton.onClick.Invoke();shell.homeFromModulesButton.onClick.Invoke();
  Check(shell.Store.Active.name=="Second Patient"&&!shell.continueButton.interactable,
   "A new patient cannot continue another patient's checkpoint");
  shell.profilePicker.value=0;shell.chooseProfileButton.onClick.Invoke();
  Check(shell.Store.Active.name=="Acceptance Patient"&&shell.continueButton.interactable,
   "Profile picker signs back into the original patient's saved session");
  // Exercise native uGUI palm dwell using local 21-landmark fixtures.
  shell.ShowHome();
  Canvas.ForceUpdateCanvases();
  var startCorners=new Vector3[4];
  shell.startButton.GetComponent<RectTransform>().GetWorldCorners(startCorners);
  var startCentre=(startCorners[0]+startCorners[2])*.5f;
  var pointer=shell.gameObject.AddComponent<RehabDwellPointer>();
  pointer.shell=shell;pointer.vision=app.vision;pointer.cameraLauncher=app.cameraLauncher;
  pointer.testNoCameraLaunch=true;pointer.dwellSeconds=.6f;pointer.smoothingSeconds=0;
  // Reproduce the reported reload fault: cached Unity EventSystem survives,
  // managed PointerEventData does not. The old code crashes every tracked frame.
  var pointerType=typeof(RehabDwellPointer);
  var cacheFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
  pointerType.GetField("eventSystem",cacheFlags).SetValue(pointer,UnityEngine.EventSystems.EventSystem.current);
  pointerType.GetField("pointerEvent",cacheFlags).SetValue(pointer,null);
  var pointing=PointingMenuFixture(startCentre.x/Screen.width,1f-startCentre.y/Screen.height);
  using(var pointerUdp=new UdpClient()){
   for(int i=0;i<20&&shell.CurrentPage=="Home";i++){
    pointing.sequence=10000+i;pointing.captureSeconds=10000+i*.07;
    Send(pointerUdp,27817,JsonUtility.ToJson(pointing));
    yield return new WaitForSecondsRealtime(.07f);
   }
  }
  Check(shell.CurrentPage=="Modules"&&!app.cameraLauncher.IsRunning,
   "21-landmark palm dwell raycasts the Start button and opens Modules without a webcam; hover="+pointer.HoveredTargetName+
   " vision="+app.vision.LastRejection+" screen="+Screen.width+"x"+Screen.height+
   " focus="+Application.isFocused+" eventSystem="+(UnityEngine.EventSystems.EventSystem.current!=null)+
   " centre="+startCentre+" scale="+shell.startButton.transform.lossyScale);
  Check(pointerType.GetField("pointerEvent",cacheFlags).GetValue(pointer)!=null,
   "Lost managed pointer event is rebuilt when the cached EventSystem survives reload");
  // Production five-second timing, with changing finger poses and both
  // visible hands. The older ideal .6s fixture could not expose this failure.
  shell.ShowHome();pointer.enabled=false;pointer.enabled=true;
  pointer.dwellSeconds=5;Canvas.ForceUpdateCanvases();
  pointerType.GetField("pointerEvent",cacheFlags).SetValue(pointer,null);
  var leftPoint=PointingMenuFixture(startCentre.x/Screen.width,1f-startCentre.y/Screen.height);
  leftPoint.handedness="Left";
  var relaxed=PointingMenuFixture(startCentre.x/Screen.width,1f-startCentre.y/Screen.height);
  relaxed.confidence=.1f; // A low-quality second hand cannot mask the valid left hand.
  relaxed.landmarks[8]=relaxed.landmarks[5];
  int activations=pointer.ActivationCount;
  using(var pointerUdp=new UdpClient()){
   for(int i=0;i<135&&shell.CurrentPage=="Home";i++){
    var outgoing=PointingMenuFixture(startCentre.x/Screen.width,1f-startCentre.y/Screen.height);
    outgoing.handedness="Left";outgoing.sequence=20000+i;outgoing.captureSeconds=20000+i*.06;
    if(i%5==4)outgoing.landmarks[8]=outgoing.landmarks[5];
    relaxed.sequence=20000+i;relaxed.captureSeconds=20000+i*.06;
    Send(pointerUdp,27817,JsonUtility.ToJson(relaxed));
    Send(pointerUdp,27817,JsonUtility.ToJson(outgoing));
    yield return new WaitForSecondsRealtime(.06f);
   }
  }
  Check(shell.CurrentPage=="Modules"&&pointer.ActivationCount==activations+1,
   "Production five-second left palm dwell survives finger bending beside a low-confidence right hand; hover="+pointer.HoveredTargetName);
  Destroy(pointer);yield return null;
  Directory.Delete(testProfileDirectory,true);
 }
 private static LandmarkFrame PointingMenuFixture(float x,float y){
  var frame=new LandmarkFrame{schemaVersion=1,handedness="Right",source="mediapipe-acceptance",
   confidence=.95f,ageMs=1,landmarks=new Vector3[21]};
  for(int i=0;i<21;i++)frame.landmarks[i]=new Vector3(x,y,0);
  frame.landmarks[5]=new Vector3(x-.02f,y+.08f,0);
  frame.landmarks[6]=new Vector3(x-.015f,y+.055f,0);
  frame.landmarks[7]=new Vector3(x-.008f,y+.025f,0);
  frame.landmarks[8]=new Vector3(x,y,0);
  foreach(int joint in new[]{9,13,17}){
   frame.landmarks[joint]=new Vector3(x+.02f,y+.06f,0);
   frame.landmarks[joint+1]=new Vector3(x+.02f,y+.025f,0);
   frame.landmarks[joint+2]=new Vector3(x+.02f,y+.04f,0);
   frame.landmarks[joint+3]=new Vector3(x+.02f,y+.05f,0);
  }
  var palm=(frame.landmarks[0]+frame.landmarks[5]+frame.landmarks[9]+frame.landmarks[13]+frame.landmarks[17])*.2f;
  var shift=new Vector3(x,y,0)-palm;
  for(int i=0;i<21;i++)frame.landmarks[i]+=shift;
  return frame;
 }
 private IEnumerator DriveForce(float force,float seconds){
  app.TestForce=force;
#if UNITY_EDITOR
  // Editor testing is explicit and never persists as a scene setting.
  app.EnableAcceptanceInput();
#endif
  yield return new WaitForSeconds(seconds);
 }
 private IEnumerator Screenshot(string name){
  #if UNITY_EDITOR
   // Repeated Play/Stop cycles exercise lifetime and input contracts; capture
   // visual evidence once to avoid headless Game-view stalls on later cycles.
   if(UnityEditor.SessionState.GetBool("ApartmentQA.Active",false)&&
      UnityEditor.SessionState.GetInt("ApartmentQA.Cycle",0)>0)yield break;
  #endif
  if(!Application.isBatchMode&&SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null){
   yield return new WaitForEndOfFrame();
   // The sandboxed standalone player can write Logs but cannot overwrite
   // repository-owned Docs images. The Python runner validates and copies them.
   string folder=Application.isEditor?"Docs/images":"Logs/player-screenshots";
   Directory.CreateDirectory(folder);
   // ScreenCapture queues an asynchronous write which may be cancelled when
   // the smoke-test player quits. Capture synchronously for reliable evidence.
   var screenshot=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
   try{
    screenshot.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);
    screenshot.Apply(false,false);
    File.WriteAllBytes(Path.GetFullPath(folder+"/"+name+".png"),screenshot.EncodeToPNG());
   }finally{Destroy(screenshot);}
   yield return null;
  }
 }
 private static LandmarkFrame CameraFixture(float center,float wristY,bool closed,long sequence,double captured,string side="Right",float palmScale=1){
  var frame=new LandmarkFrame{schemaVersion=1,source="mediapipe-test",handedness=side,confidence=.96f,sequence=sequence,captureSeconds=captured,landmarks=new Vector3[21]};
  frame.landmarks[0]=new Vector3(center,wristY,0);
  for(int finger=0;finger<4;finger++){
   int n=5+finger*4;float x=center+(side=="Left"?-1:1)*(finger-1.5f)*.045f*palmScale;
   frame.landmarks[n]=new Vector3(x,wristY-.10f,0);
   frame.landmarks[n+1]=new Vector3(x,wristY-.18f,0);
   frame.landmarks[n+2]=new Vector3(x,wristY+(closed?-.12f:-.26f),0);
   frame.landmarks[n+3]=new Vector3(x,wristY+(closed?-.04f:-.34f),0);
  }
  for(int i=1;i<5;i++)frame.landmarks[i]=new Vector3(center+(side=="Left"?1:-1)*(.12f-i*.015f),wristY-.06f-i*.03f,0);
  return frame;
 }
 private static LandmarkFrame RotateCameraFixture(LandmarkFrame source,Quaternion rotation){
  var frame=new LandmarkFrame{schemaVersion=source.schemaVersion,source=source.source,
   handedness=source.handedness,confidence=source.confidence,sequence=source.sequence,
   captureSeconds=source.captureSeconds,landmarks=new Vector3[21]};
  var wrist=HandKinematics.ToCameraSpace(source.landmarks[0]);
  for(int i=0;i<21;i++){
   var point=wrist+rotation*(HandKinematics.ToCameraSpace(source.landmarks[i])-wrist);
   frame.landmarks[i]=new Vector3(point.x,1-point.y,-point.z);
  }
  return frame;
 }
 private static LandmarkFrame HandFixture(string side,float center,double captured,long sequence){
  var points=new Vector3[21];points[0]=new Vector3(center,.82f,0);
  for(int finger=0;finger<5;finger++)for(int joint=0;joint<4;joint++){
   float x=center+(finger-2)*.025f;
   float y=.65f-joint*.07f+(joint>=2?(joint-1)*.025f:0);
   float z=joint>=2?-(joint-1)*.065f:0;
   points[1+finger*4+joint]=new Vector3(x,y,z);
  }
  return new LandmarkFrame{schemaVersion=1,source="synthetic acceptance",handedness=side,captureSeconds=captured,sequence=sequence,confidence=.95f,landmarks=points};
 }
 private static LandmarkFrame WorldCameraFixture(LandmarkFrame source,Quaternion rotation){
  var frame=CameraFixture(source.landmarks[0].x,source.landmarks[0].y,false,source.sequence,source.captureSeconds,source.handedness);
  frame.imageWidth=640;frame.imageHeight=480;frame.worldLandmarks=new Vector3[21];
  for(int i=0;i<21;i++){
   // Fabricated isotropic hand shape, not a recorded camera hand.
   var p=rotation*HandKinematics.PosePoint(source,i)*.4f;
   frame.worldLandmarks[i]=new Vector3(p.x,-p.y,-p.z);
  }
  return frame;
 }
 private void VerifyWorldHandPoses(LandmarkFrame right,LandmarkFrame left){
  foreach(bool isLeft in new[]{false,true}){
   var driver=isLeft?app.leftHand:app.rightHand;var source=isLeft?left:right;
   driver.UseDemoPose();var rest=driver.transform.localPosition;
   var world=WorldCameraFixture(source,Quaternion.identity);
   for(int i=0;i<16;i++)driver.Pose(world,true,0,.05f,.045f,rest,.85f,true);
   var neutralRotation=driver.transform.localRotation;
   foreach(var axis in new[]{Vector3.right,Vector3.up,Vector3.forward})foreach(var angle in new[]{-75f,75f}){
    var rotation=Quaternion.AngleAxis(angle,axis);var rotated=WorldCameraFixture(source,rotation);
    for(int i=0;i<16;i++)driver.Pose(rotated,true,0,.05f,.045f,rest,.85f,true);
    Check(driver.UsingWorldLandmarks&&Quaternion.Angle(driver.transform.localRotation,rotation*neutralRotation)<1,
      (isLeft?"Left":"Right")+" world palm follows signed "+angle+" degree turn on "+axis+" without a second horizontal reflection");
   }
   for(int i=0;i<16;i++)driver.Pose(world,true,0,.05f,.045f,rest,.85f,true);
   var baseline=(float[])driver.JointAngles.Clone();
   for(int finger=0;finger<5;finger++){
    var bent=WorldCameraFixture(source,Quaternion.identity);int n=finger<4?5+finger*4:1;
    bent.worldLandmarks[n+2]=bent.worldLandmarks[n+1]+new Vector3(0,0,.04f);
    bent.worldLandmarks[n+3]=bent.worldLandmarks[n+2]+new Vector3(0,0,.02f);
    for(int i=0;i<16;i++)driver.Pose(bent,true,0,.05f,.045f,rest,.85f,true);
    float otherChange=0;for(int j=0;j<15;j++)if(j/3!=finger)otherChange=Mathf.Max(otherChange,Mathf.Abs(driver.JointAngles[j]-baseline[j]));
    Check(driver.JointAngles[finger*3+1]>60&&otherChange<.1f,
      (isLeft?"Left":"Right")+" world finger "+finger+" bends independently of all other fingers and the unchanged image points");
   }
   var collapsed=WorldCameraFixture(source,Quaternion.identity);System.Array.Clear(collapsed.worldLandmarks,0,21);
   driver.Pose(collapsed,true,0,.05f,.045f,rest,.85f,true);
   Check(!driver.TrackingUsed,"Collapsed "+(isLeft?"left":"right")+" palm cannot reset the rig to an identity basis");
   driver.UseDemoPose();
  }
 }
 private static void Send(UdpClient socket,int port,string json){var bytes=Encoding.UTF8.GetBytes(json);socket.Send(bytes,bytes.Length,"127.0.0.1",port);}
 private void End(bool passed,string error){
  Finished=true;Passed=passed;Directory.CreateDirectory("Logs");
  File.WriteAllText("Logs/apartment-runtime-checks.json",JsonUtility.ToJson(new Result{passed=passed,utc=DateTime.UtcNow.ToString("O"),checks=checks.ToArray(),error=error,contactMeanMs=app!=null?app.ContactMeanMs:0,contactMaxMs=app!=null?app.ContactMaxMs:0},true));
  if(passed)Debug.Log("[APARTMENT_TEST] PASS: "+checks.Count+" runtime checks");else Debug.LogError("[APARTMENT_TEST] FAIL: "+error);
  if(!Application.isEditor)Application.Quit(passed?0:1);
 }
 [Serializable]private class Result{public bool passed;public string utc,error;public string[] checks;public double contactMeanMs,contactMaxMs;}
}
}
