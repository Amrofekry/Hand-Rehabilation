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
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-apartment-smoke")>=0)new GameObject("Acceptance runner").AddComponent<ApartmentAcceptance>();
 }
 private IEnumerator Start(){
  Finished=false;Passed=false;
  var routine=Run();
  while(true){object current=null;bool more=false;try{more=routine.MoveNext();if(more)current=routine.Current;}catch(Exception error){End(false,error.ToString());yield break;}if(!more)break;yield return current;}
  End(true,"");
 }
 private void Check(bool condition,string text){if(!condition)throw new Exception(text);checks.Add(text);Debug.Log("[APARTMENT_CHECK] PASS: "+text);}
 private IEnumerator Run(){
  yield return new WaitForSeconds(1.5f);
  app=FindAnyObjectByType<ApartmentExperience>();
  Check(app!=null&&app.objects.All(x=>x!=null)&&app.statusText!=null&&app.rightHand!=null,"Serialized scene bindings survive reload");
  Check(app.virtualGrasp!=null&&app.virtualGrasp.Ready,"VirtualGrasp native SDK and external controllers initialize");
  Check(app.virtualGrasp.outputMesh.sharedMesh==app.virtualGrasp.rightOnlyMesh,"Only selected right hand is rendered");
  app.SelectHand(true);Check(app.virtualGrasp.outputMesh.sharedMesh==app.virtualGrasp.leftOnlyMesh&&app.contactRig.pose==app.leftHand,"Left selection swaps visible mesh and contact hand");app.SelectHand(false);
  app.player.SetPaused(false);yield return null;
  var forceFilter=new GripForceFilter();forceFilter.Push(10,.05f,.06f,30,250);
  Check(Mathf.Abs(forceFilter.Push(100,.05f,.06f,30,250)-10)<.001f,"Single isolated force spike is rejected");
  Check(forceFilter.Push(100,.05f,.06f,30,250)>10,"Repeated high force is accepted with smoothing");
  Check(forceFilter.Push(float.NaN,.05f,.06f,30,250)==0,"Impossible force resets the contact filter");
  Check(!app.Hardware&&app.ForceValid,"Keyboard/mouse demo is the default");
  Check(FindObjectsByType<Camera>(FindObjectsSortMode.None).Length==1&&FindObjectsByType<LiveForceReceiver>(FindObjectsSortMode.None).Length==1&&FindObjectsByType<VisionUdpReceiver>(FindObjectsSortMode.None).Length==1&&FindObjectsByType<VisionPreviewReceiver>(FindObjectsSortMode.None).Length==1,"Exactly one camera and each input receiver");
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
  Check(player.Sit(app.seats[0]),"Living room chair can be used from the approach position");
  player.view.transform.LookAt(ball.transform.position);
  yield return new WaitForSeconds(.2f);
  Check(app.TryGrab(ball),"Therapy ball can be reached and grabbed from the chair");
  // In Editor the test drives the same force application explicitly; the executable uses its test flag.
  var ballMesh=ball.visual.GetComponent<MeshFilter>().sharedMesh;
  var ballRest=ballMesh.vertices;
  yield return Screenshot("vg-ball-before");
  yield return DriveForce(18,3.7f);
  yield return Screenshot("vg-ball-deformed");
  Check(ball.IsHeld&&ball.Body.isKinematic&&ball.Compression>.08f,"Held ball is kinematic and deforms under grip force; held="+ball.IsHeld+" kinematic="+ball.Body.isKinematic+" compression="+ball.Compression+" contacts="+ball.ContactCount+" ball="+ball.transform.position+" anchor="+app.holdAnchor.position+" hand="+app.rightHand.transform.position+" eye="+player.view.transform.position+" probes="+string.Join(";",app.contactRig.probes.Select(p=>p.finger+":"+p.segment+"="+(p.Contact==ball)+"@"+Vector3.Distance(p.transform.position,ball.transform.position).ToString("F3"))));
  var touching=app.contactRig.probes.Where(p=>p.Contact==ball).ToArray();
  Check(touching.Length==ball.ContactCount&&touching.Length>0&&touching.Length<app.contactRig.ProbeCount,"Independent finger capsules contribute only when touching the ball ("+touching.Length+"/15)");
  Check(touching.All(p=>p.ResolvedForceNewtons>0&&p.ContactAreaSquareMeters>0)&&app.contactRig.probes.Where(p=>p.Contact!=ball).All(p=>p.ResolvedForceNewtons==0),"Noncontacting fingers resolve exactly zero force");
  var moved=ballMesh.vertices;int changed=0,far=0,farMoved=0;
  for(int vertex=0;vertex<moved.Length;vertex++){
   float displacement=Vector3.Distance(moved[vertex],ballRest[vertex])*ball.visual.lossyScale.x;
   if(displacement>.00005f)changed++;
   var world=ball.visual.TransformPoint(ballRest[vertex]);
   if(touching.All(p=>Vector3.Distance(world,p.Point)>ball.profile.deformationRadiusMeters+.015f)){far++;if(displacement>.0005f)farMoved++;}
  }
  Check(changed>0&&changed<moved.Length&&far>0&&farMoved==0&&ball.MaxDisplacementMeters>.003f,"Mesh vertices deform near finger contacts while distant regions remain stable ("+changed+" moved, "+far+" distant, "+farMoved+" distant moved)");
  Check(app.ProgressSeconds>=3,"A valid 18 N hold completes the three-second exercise");
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
  Check(mug.IsBroken,"Porcelain mug fractures under sustained excessive demo force");yield return Screenshot("vg-mug-fractured");
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
  Check(!cracker.IsBroken&&cracker.visual.gameObject.activeSelf,"Reset restores broken objects and removes fragments");
  Check(player.Stand(),"Seated player can stand into a clear area");
  var kitchen=GameObject.Find("Kitchen standing station").transform;
  menu.kitchenButton.onClick.Invoke();
  Check(menu.Kitchen&&!player.IsSeated,"Kitchen station button takes player to the kitchen");
  menu.objectButtons[1].onClick.Invoke();yield return null;
  var cup=menu.kitchenObjects[1];Check(cup.gameObject.activeSelf&&app.TryGrab(cup),"Kitchen silicone cup is selectable and can be gripped");
  yield return DriveForce(30,.6f);Check(cup.Compression>.2f&&cup.MaxDisplacementMeters>.003f,"Silicone cup visibly yields to demo grip force; compression="+cup.Compression+" contacts="+cup.ContactCount+" force="+app.ContactForceNewtons+" hand="+app.rightHand.transform.position+" cup="+cup.transform.position+" probes="+string.Join(";",app.contactRig.probes.Select(p=>p.finger+":"+p.segment+"="+Vector3.Distance(p.transform.position,cup.transform.position).ToString("F3"))));
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
   frame.sequence++;frame.sampleMs+=1000;Send(udp,27818,JsonUtility.ToJson(frame));yield return new WaitForSeconds(.08f);
   Check(app.ForceValid,"New data recovers after interruption");
   app.ResetObjects();yield return null;Check(app.TryGrab(sponge),"Object can be grabbed after recovery");
   frame.sequence++;frame.sampleMs+=100;frame.calibrationRevision++;frame.calibrationCrc++;Send(udp,27818,JsonUtility.ToJson(frame));yield return new WaitForSeconds(.08f);
   Check(app.Held==null,"Calibration change ends the previous grip");
   Send(udp,27818,"not json");yield return null;
   var left=HandFixture("Left",.34f,1,1);
   var right=HandFixture("Right",.66f,1,1);
   Send(udp,27817,JsonUtility.ToJson(left));Send(udp,27817,JsonUtility.ToJson(right));
   yield return new WaitForSeconds(.15f);
   Check(app.vision.LeftValid&&app.vision.RightValid&&app.leftHand.TrackingUsed&&app.rightHand.TrackingUsed,"Both independent MediaPipe-style 21-point hands drive the SDK input rigs");
   Check(app.statusText.text.Contains("SYNTHETIC"),"HUD identifies synthetic vision frames instead of claiming a live camera");
   Check(app.leftHand.JointAngles[4]>5&&app.rightHand.JointAngles[4]>5,"Measured middle finger PIP bending reaches both input rigs");
   yield return Screenshot("apartment-two-hands-tracked");
   var leftBefore=app.leftHand.transform.localPosition;
   left.sequence=2;left.captureSeconds=2;
   for(int i=0;i<left.landmarks.Length;i++)left.landmarks[i].x+=.08f;
   Send(udp,27817,JsonUtility.ToJson(left));yield return new WaitForSeconds(.26f);
   Check(app.leftHand.transform.localPosition.x>leftBefore.x+.005f,"Tracked wrist displacement moves the left hand");
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
   Send(udp,27817,JsonUtility.ToJson(right));
   yield return new WaitForSeconds(.33f);
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
   for(int i=0;i<6;i++){
    Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,false,++seq,captured+=.06)));
    yield return new WaitForSeconds(.06f);
   }
   Check(app.CameraMode&&app.ForceValid&&HandKinematics.GripScore(app.vision.LatestRight)<.2f,"Open camera hand drives valid pose without squeezing");
   var wristBefore=app.rightHand.transform.localPosition;
   for(int i=0;i<5;i++){
    Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.32f,.5f,false,++seq,captured+=.06)));
    yield return new WaitForSeconds(.06f);
   }
   Check(app.rightHand.transform.localPosition.x<wristBefore.x-.05f,"Absolute camera wrist motion moves the selected vendor input toward objects");
   foreach(var imageY in new[]{.45f,.5f,.55f}){
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
   for(int i=0;i<5;i++){
    Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,true,++seq,captured+=.06)));
    yield return new WaitForSeconds(.06f);
   }
   yield return new WaitForSeconds(.6f);
   Check(!app.ForceValid&&app.ForceNewtons==0&&app.Held==null,"Stale camera landmarks clear visual pressure and cannot keep holding");
   app.SelectHand(true);menu.SelectStation(true);menu.SelectObject(0);player.SetPaused(false);
   Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,true,++seq,captured+=.06)));
   yield return new WaitForSeconds(.08f);
   Check(!app.ForceValid&&app.Held==null,"Right-hand camera data cannot squeeze the selected left hand");
   long leftSeq=1000;double leftCaptured=1000;
   for(int i=0;i<6;i++){
    Send(cameraUdp,27817,JsonUtility.ToJson(CameraFixture(.5f,.5f,false,++leftSeq,leftCaptured+=.06,"Left")));
    yield return new WaitForSeconds(.06f);
   }
   Check(app.ForceValid&&app.virtualGrasp.outputMesh.sharedMesh==app.virtualGrasp.leftOnlyMesh,"Selected left camera stream drives only the left vendor mesh");
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
  }
  app.SetHardware(false);app.ResetObjects();player.enabled=true;player.SetPaused(true);
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
  if(!Application.isBatchMode&&SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null){
   yield return new WaitForEndOfFrame();Directory.CreateDirectory("Docs/images");ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/images/"+name+".png"));yield return null;
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
