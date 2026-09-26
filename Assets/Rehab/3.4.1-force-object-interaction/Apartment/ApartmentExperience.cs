using System;
using UnityEngine;
using UnityEngine.UI;
namespace GripRehab {
public sealed class ApartmentExperience:MonoBehaviour {
 public ApartmentPlayer player;public ApartmentSettings settings;public LiveForceReceiver forceReceiver;public VisionUdpReceiver vision;
 public RehabVirtualGrasp virtualGrasp;public VisionCameraLauncher cameraLauncher;
 public bool LeftSelected{get;private set;}
 public bool CameraMode{get;private set;}
 public float CameraGripScore{get;private set;}
 readonly CameraGripGate _cameraGrip=new CameraGripGate();
 public FingerPoseDriver ActiveHand=>LeftSelected?leftHand:rightHand;
 public void SelectHand(bool left){Release();_cameraGrip.Reset();CameraGripScore=0;LeftSelected=left;_leftDemoSelected=left;if(contactRig!=null){contactRig.pose=ActiveHand;foreach(var p in contactRig.probes){p.Clear();p.transform.SetParent(ActiveHand.transform,true);}Physics.SyncTransforms();}if(virtualGrasp!=null){virtualGrasp.ShowLeft(left);virtualGrasp.BindContacts(contactRig,left);}rightHand.ResetFingers();leftHand.ResetFingers();}
 public Transform holdAnchor;public FingerPoseDriver rightHand,leftHand;public FingerContactRig contactRig;
 public ApartmentChoices choices;
 public Text statusText,contextText,forceText;public Image progressFill;
 public SqueezableObject[] objects;public SeatPoint[] seats;
 public SqueezableObject Held{get;private set;}
 public bool Hardware{get;private set;}
 public float ForceNewtons{get;private set;}
 public float ContactForceNewtons{get;private set;}
 public bool IsSensorLive=>Hardware&&ForceValid&&!forceReceiver.State.IsSynthetic;
 public double ContactMeanMs{get;private set;}
 public double ContactMaxMs{get;private set;}
 public bool ForceValid{get;private set;}
 public float DemoSetpoint{get;private set;}
 public float ProgressSeconds => _task.ProgressSeconds;
 public float TestForce=-1; // Used only when an explicit acceptance-test command-line flag is present.
 public string Status{get;private set;}="Ready";
 private Vector3 _lastHeld;private Vector3 _velocity;private Vector3 _manualRight,_manualLeft;private bool _leftDemoSelected;private float _summonGraceUntil;
 private int _generation,_lastSamples;private long _lastMs;private Vector3 _rightRest,_leftRest;
 private ForceHoldTask _task=new ForceHoldTask(18,3,3);
 private long _contactTicks;
 private static readonly Unity.Profiling.ProfilerMarker ContactMarker=new Unity.Profiling.ProfilerMarker("GripRehab.FingerContacts");
 private bool _testing;private float _nextUi;private readonly GripForceFilter _filter=new GripForceFilter();private int _filteredSamples,_sessionValidSamples;
 #if UNITY_EDITOR
 public void EnableAcceptanceInput(){_testing=true;}
#endif
 private void Start(){_rightRest=rightHand.transform.localPosition;_leftRest=leftHand.transform.localPosition;choices=choices!=null?choices:FindAnyObjectByType<ApartmentChoices>();DemoSetpoint=settings.demoForceNewtons;_testing=Array.Exists(Environment.GetCommandLineArgs(),x=>x.StartsWith("-apartment-"));
#if UNITY_EDITOR
 _testing|=UnityEditor.SessionState.GetBool("ApartmentQA.Active",false);
#endif
 if(settings.startWithHardware)SetHardware(true);else if(settings.startWithCamera&&!_testing)SetCameraMode();else SetHardware(false);}
 public void SetCameraMode(){SetHardware(false);Application.runInBackground=true;vision?.BeginNewSession();CameraMode=true;_cameraGrip.Reset();if(!_testing)cameraLauncher?.EnsureStarted();Status="Starting local MediaPipe camera";}
 public void CycleInputMode(){if(CameraMode)SetHardware(false);else if(Hardware)SetCameraMode();else SetHardware(true);}
 public void SetHardware(bool hardware){Release();CameraMode=false;CameraGripScore=0;_cameraGrip.Reset();cameraLauncher?.StopWorker();Hardware=hardware;_manualRight=_manualLeft=Vector3.zero;if(!hardware){rightHand.UseDemoPose();leftHand.UseDemoPose();}ForceNewtons=ContactForceNewtons=0;_filter.Reset();_filteredSamples=forceReceiver.State.SampleCount;_sessionValidSamples=0;_task.Reset();_generation=forceReceiver.State.Generation;Status=hardware?"Waiting for calibrated Phase 2 input":"Keyboard & mouse demo";}
 private void Update(){
  AutoSelectCameraHand();
  if(Input.GetKeyDown(KeyCode.B))CycleInputMode();
  if(!player.Paused){
   if(!Hardware&&!CameraMode){DemoSetpoint=Mathf.Clamp(DemoSetpoint+Input.mouseScrollDelta.y*3+(Input.GetKey(KeyCode.Equals)?25*Time.deltaTime:Input.GetKey(KeyCode.Minus)?-25*Time.deltaTime:0),1,80);UpdateDemoHandMovement();}
   if(Input.GetKeyDown(KeyCode.Q))Release();
   if(Input.GetKeyDown(KeyCode.R))ResetObjects();
   if(Input.GetKeyDown(KeyCode.F)){
    if(player.IsSeated){if(!player.Stand())Status="Stand-up area blocked";}
    else {SeatPoint nearest=null;float distance=2.2f;foreach(var s in seats){float d=Vector3.Distance(player.transform.position,s.seatedFeet.position);if(d<distance){nearest=s;distance=d;}}if(nearest!=null){Release();player.Sit(nearest);}else Status="Move closer to a marked chair";}
   }
   if(Input.GetKeyDown(KeyCode.E)){
    if(Held!=null)Release();
    else TryKeyboardAction();
   }
   if(Input.GetKeyDown(KeyCode.G))TryCameraSummonSelectedObject();
  }
  var selectedFrame=LeftSelected?vision.LatestLeft:vision.LatestRight;
  bool cameraTracked=CameraMode&&(LeftSelected?vision.LeftValid:vision.RightValid)&&
   (cameraLauncher!=null&&cameraLauncher.CameraOpened||_testing)&&selectedFrame!=null&&
   ((selectedFrame.source??"").StartsWith("mediapipe-",StringComparison.OrdinalIgnoreCase)||_testing);
  bool cameraValid=cameraTracked&&!player.Paused;
  if(CameraMode){
   ForceValid=cameraValid;
   CameraGripScore=cameraTracked?HandKinematics.GripScore(selectedFrame):0;
   _cameraGrip.Step(CameraGripScore,cameraValid,Time.deltaTime,settings.cameraCloseAt,settings.cameraOpenAt,settings.cameraGestureDwellSeconds);
   if(!cameraValid&&Held!=null){Release();Status="Camera tracking lost - object released";}
   else if(_cameraGrip.Closed&&CameraGripScore>=settings.cameraCloseAt)_summonGraceUntil=0;
   else if(Held!=null&&Time.unscaledTime>=_summonGraceUntil){Release();Status="Hand opened - object released";}
   ForceNewtons=cameraValid?Mathf.MoveTowards(ForceNewtons,CameraGripScore*settings.cameraVisualMaxNewtons,90*Time.deltaTime):0;
  } else if(Hardware){
   ForceValid=forceReceiver.Latest.Valid&&!player.Paused;ForceNewtons=ForceValid?forceReceiver.Latest.Newtons:0;
   if(_generation!=forceReceiver.State.Generation){_generation=forceReceiver.State.Generation;Release();_filter.Reset();_filter.Push(0,Time.deltaTime,settings.forceSmoothingSeconds,settings.isolatedSpikeNewtons,settings.maxForceSlewNewtonsPerSecond);_sessionValidSamples=1;_filteredSamples=forceReceiver.State.SampleCount;ContactForceNewtons=0;_task.Reset();Status="Connection/calibration changed";}
   if(!ForceValid&&Held!=null){Release();Status="Input stale: object released";}
  } else {
   ForceValid=!player.Paused;float requested=ForceValid&&(Input.GetMouseButton(0)||Input.GetKey(KeyCode.Space))?(Input.GetKey(KeyCode.LeftShift)?80:DemoSetpoint):0;
   if(_testing&&TestForce>=0)requested=TestForce;
   ForceNewtons=Mathf.MoveTowards(ForceNewtons,requested,90*Time.deltaTime);
  }

  if(!ForceValid){_filter.Reset();ContactForceNewtons=0;}
  else if(!Hardware)ContactForceNewtons=_filter.Push(ForceNewtons,Time.deltaTime,settings.forceSmoothingSeconds,settings.isolatedSpikeNewtons,settings.maxForceSlewNewtonsPerSecond);
  else if(_filteredSamples!=forceReceiver.State.SampleCount){_filteredSamples=forceReceiver.State.SampleCount;_sessionValidSamples++;ContactForceNewtons=_sessionValidSamples<2?0:_filter.Push(ForceNewtons,Time.deltaTime,settings.forceSmoothingSeconds,settings.isolatedSpikeNewtons,settings.maxForceSlewNewtonsPerSecond);}
  if(Held!=null&&Held.IsBroken){Held=null;Status="Virtual brittle object fractured — R to reset";}
  if(Held!=null&&ForceValid){
   if(!Hardware&&!CameraMode)_task.Step(new ForceReading(ForceNewtons,Time.realtimeSinceStartupAsDouble,true,true),Time.deltaTime);
   else if(Hardware&&_lastSamples!=forceReceiver.State.SampleCount){_task.Step(forceReceiver.Latest,Mathf.Min(.1f,(uint)((uint)forceReceiver.State.SampleMs-(uint)_lastMs)/1000f));_lastSamples=forceReceiver.State.SampleCount;_lastMs=forceReceiver.State.SampleMs;}
  }
  var rightTracked=CameraMode?cameraTracked&&!LeftSelected:vision!=null&&vision.RightValid;
  var leftTracked=CameraMode?cameraTracked&&LeftSelected:vision!=null&&vision.LeftValid;
  float closure=Held!=null?Mathf.Lerp(.45f,.95f,Mathf.Clamp01(ForceNewtons/45)):Mathf.Clamp01(ForceNewtons/55);
  float handLift=player.IsSeated?settings.seatedHandsLiftMeters:0;
  rightHand.Pose(rightTracked?vision.LatestRight:null,rightTracked,LeftSelected?0:closure,Time.deltaTime,settings.trackingSmoothing,_rightRest+_manualRight+Vector3.up*handLift,settings.trackingTravelMeters,CameraMode);
  float leftDemo=LeftSelected?closure:0;
  leftHand.Pose(leftTracked?vision.LatestLeft:null,leftTracked,leftDemo,Time.deltaTime,settings.trackingSmoothing,_leftRest+_manualLeft+Vector3.back*settings.leftWristSetbackMeters+Vector3.up*handLift,settings.trackingTravelMeters,CameraMode);
  if(CameraMode&&cameraValid&&_cameraGrip.Closed&&Held==null)TryCameraGrab();
  if(Time.unscaledTime>=_nextUi){_nextUi=Time.unscaledTime+.08f;RefreshHUD();}
 }
 // Follow the only actively observed physical hand. When both are visible,
 // retain the selected side until its stream has stopped refreshing.
 private void AutoSelectCameraHand(){
  if(!CameraMode||vision==null)return;
  double selectedAge=LeftSelected?vision.LeftAgeSeconds:vision.RightAgeSeconds;
  double otherAge=LeftSelected?vision.RightAgeSeconds:vision.LeftAgeSeconds;
  if(otherAge<.15&&(double.IsPositiveInfinity(selectedAge)||selectedAge>.25)){
   SelectHand(!LeftSelected);
   Status=LeftSelected?"Camera selected LEFT hand":"Camera selected RIGHT hand";
  }
 }
 public void SetDemoHandOffset(bool left,Vector3 offset){
  if(Hardware||CameraMode)return;
  offset=new Vector3(Mathf.Clamp(offset.x,-.2f,.2f),Mathf.Clamp(offset.y,-.18f,.18f),Mathf.Clamp(offset.z,-.15f,.15f));
  if(left)_manualLeft=offset;else _manualRight=offset;
 }
 private void UpdateDemoHandMovement(){
  for(int f=0;f<5;f++)if(Input.GetKeyDown(KeyCode.Alpha1+f))ActiveHand.SelectedFinger=f;
  ActiveHand.independentKeyboard=true;
  if(Input.GetKeyDown(KeyCode.J))ActiveHand.SelectedJoint=ActiveHand.SelectedJoint==2?-1:ActiveHand.SelectedJoint+1;
  float bend=(Input.GetKey(KeyCode.X)?1:0)-(Input.GetKey(KeyCode.Z)?1:0);
  if(bend!=0)ActiveHand.AdjustFinger(ActiveHand.SelectedFinger,bend*Time.deltaTime);
  if(Input.GetKeyDown(KeyCode.C))ActiveHand.ResetFingers();
  if(Input.GetKeyDown(KeyCode.Alpha0))SetDemoHandOffset(_leftDemoSelected,Vector3.zero);
  var motion=new Vector3((Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.LeftArrow)?1:0),
    (Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.DownArrow)?1:0),
    (Input.GetKey(KeyCode.PageUp)?1:0)-(Input.GetKey(KeyCode.PageDown)?1:0));
  if(motion.sqrMagnitude>0)SetDemoHandOffset(_leftDemoSelected,(_leftDemoSelected?_manualLeft:_manualRight)+Vector3.ClampMagnitude(motion,1)*.32f*Time.deltaTime);
 }
 private void FixedUpdate(){
  long before=System.Diagnostics.Stopwatch.GetTimestamp();
  if(contactRig!=null)contactRig.ClearForces();
  using(ContactMarker.Auto())foreach(var item in objects)if(item!=null)item.ApplyContacts(contactRig,item==Held&&ForceValid?ContactForceNewtons:0,Time.fixedDeltaTime);
  double elapsed=(System.Diagnostics.Stopwatch.GetTimestamp()-before)*1000.0/System.Diagnostics.Stopwatch.Frequency;
  _contactTicks++;ContactMeanMs+= (elapsed-ContactMeanMs)/_contactTicks;ContactMaxMs=Math.Max(ContactMaxMs,elapsed);
  if(Held==null)return;
  var origin=player.view.transform.position;
  var requested=GripTarget(Held);var direction=requested-origin;
  if(Physics.SphereCast(origin,Held.radius,direction.normalized,out var hit,direction.magnitude,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore))requested=origin+direction.normalized*Mathf.Max(.15f,hit.distance-.015f);
  _velocity=(requested-_lastHeld)/Mathf.Max(Time.fixedDeltaTime,.001f);_lastHeld=requested;
  Held.MoveHeld(requested,holdAnchor.rotation);
 }
 private Vector3 GripTarget(SqueezableObject item){
  var lift=player.IsSeated?settings.seatedHandsLiftMeters:0;
  var handOffset=ActiveHand.transform.localPosition-(LeftSelected?_leftRest:_rightRest)-Vector3.up*lift;
  var anchor=holdAnchor.localPosition;if(LeftSelected)anchor.x=-Mathf.Abs(anchor.x);
  var target=holdAnchor.parent.TransformPoint(anchor)+player.view.transform.TransformVector(handOffset)+Vector3.up*(player.IsSeated?settings.seatedHoldLiftMeters:0);
  // The mirrored Gleechi hand has a different palm-to-fingertip reach. Place a
  // left-hand held object at the actual finger pad cluster, not a mirrored HUD anchor.
  if(LeftSelected&&contactRig!=null&&contactRig.contactBones!=null&&contactRig.contactBones.Length==15){
   var bones=contactRig.contactBones;
   if(bones[1]!=null&&bones[4]!=null&&bones[7]!=null&&bones[10]!=null){
    Vector3 pads=(bones[1].position+bones[4].position+bones[7].position+bones[10].position)*.25f;
    target+=Vector3.ClampMagnitude(pads-target,.23f);
   }
  }
  if(item!=null){var toward=ActiveHand.transform.position-target;float correction=Mathf.Clamp(.12f-item.radius,0,.05f);if(toward.sqrMagnitude>.0001f)target+=toward.normalized*correction;}
  return target;
 }
 // Closing fingers near a visible object grabs it without E. A distant fist cannot pick it up.
 private void TryCameraGrab(){
  if(contactRig==null||contactRig.contactBones==null)return;
  SqueezableObject nearest=null;float best=settings.cameraGrabRadiusMeters;
  foreach(var item in objects){
   if(item==null||!item.gameObject.activeInHierarchy||item.IsBroken)continue;
   var collider=item.GetComponent<Collider>();if(collider==null)continue;
   if(Vector3.Distance(player.view.transform.position,item.transform.position)>settings.reachMeters)continue;
   foreach(var bone in contactRig.contactBones){
    if(bone==null)continue;
    var tip=bone.childCount>0?bone.GetChild(0).position:bone.position;
    float distance=Vector3.Distance(tip,collider.ClosestPoint(tip));
    if(distance<best){best=distance;nearest=item;}
   }
  }
  if(nearest!=null&&TryGrab(nearest))Status="Camera grip: holding "+nearest.displayName;
 }
 // G brings the station's selected object into the tracked hand without
 // changing input mode. The patient must close the hand within a short grace
 // period; tracking loss always releases immediately.
 public bool TryCameraSummonSelectedObject(){
  if(!CameraMode||player.Paused||Held!=null||choices==null||vision==null)return false;
  var frame=LeftSelected?vision.LatestLeft:vision.LatestRight;
  if(!(LeftSelected?vision.LeftValid:vision.RightValid)||frame==null||
     !(cameraLauncher!=null&&cameraLauncher.CameraOpened||_testing)||
     !((frame.source??"").StartsWith("mediapipe-",StringComparison.OrdinalIgnoreCase)||_testing)){
   Status="Show the selected hand to the camera before pressing G";return false;
  }
  var item=choices.SelectedObject;
  if(item==null||!item.gameObject.activeInHierarchy||item.IsBroken||!item.Grab())return false;
  Held=item;_summonGraceUntil=Time.unscaledTime+1.25f;
  var target=GripTarget(item);item.Body.position=target;item.Body.rotation=holdAnchor.rotation;
  Physics.SyncTransforms();_lastHeld=target;_velocity=Vector3.zero;_task.Reset();
  Status="Camera hand: close fingers to keep "+item.displayName;
  return true;
 }
 // E is an explicit request for keyboard pickup. It switches camera mode to
 // demo before grasping, so missing camera frames cannot imply measured force.
 public bool TryKeyboardAction(){
  if(Held!=null){Release();return true;}
  if(player.Paused)return false;
  if(!Physics.Raycast(player.view.ViewportPointToRay(new Vector3(.5f,.5f,0)),out var hit,
     settings.reachMeters,~(1<<8),QueryTriggerInteraction.Ignore))return false;
  var item=hit.collider.GetComponentInParent<SqueezableObject>();
  if(item!=null){
   if(CameraMode)SetHardware(false);
   return TryGrab(item);
  }
  var door=hit.collider.GetComponentInParent<ApartmentDoor>();
  if(door==null)return false;
  door.Toggle();return true;
 }
 public bool TryGrab(SqueezableObject item){
  if(item==null||Held!=null||item.IsBroken||player.Paused||Hardware&&!forceReceiver.Latest.Valid||CameraMode&&!ForceValid)return false;
  var eye=player.view.transform.position;var direction=item.transform.position-eye;
  if(direction.magnitude>settings.reachMeters)return false;
  if(Physics.Raycast(eye,direction.normalized,out var hit,direction.magnitude+.03f,~(1<<8),QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<SqueezableObject>()!=item)return false;
  if(!item.Grab())return false;Held=item;_lastHeld=GripTarget(item);_velocity=Vector3.zero;_task.Reset();_lastSamples=forceReceiver.State.SampleCount;_lastMs=forceReceiver.State.SampleMs;Status="Holding "+item.displayName;return true;
 }
 public void Release(){if(Held!=null){Held.Release(_velocity*.15f);Held=null;}_summonGraceUntil=0;_task.Reset();}
 public void ResetObjects(){Release();foreach(var item in objects)item.ResetObject();Status="Objects restored to their stations";}
 private static string VisionLabel(LandmarkFrame frame)=>frame!=null&&(frame.source??"").StartsWith("synthetic",StringComparison.OrdinalIgnoreCase)?"SYNTHETIC":"CAMERA";
 private void RefreshHUD(){
  string source=CameraMode?"MEDIAPIPE CAMERA":Hardware?(forceReceiver.State.IsSynthetic?"SYNTHETIC REPLAY":"PHASE 2 / BLE"):"KEYBOARD + MOUSE DEMO";
  string handPose=CameraMode?((LeftSelected?leftHand:rightHand).TrackingUsed?"LIVE":"SEARCHING"):LeftSelected?(vision.LeftValid?VisionLabel(vision.LatestLeft):Hardware?(ForceValid?"FORCE":"REST"):"DEMO"):(vision.RightValid?VisionLabel(vision.LatestRight):Hardware?(ForceValid?"FORCE":"REST"):"DEMO");
  statusText.text="GRIP / REHAB     �     APARTMENT LAB\n"+source+"   |   "+(player.IsSeated?"SEATED":"WALKING")+"\nSELECTED HAND: "+(LeftSelected?"LEFT ":"RIGHT ")+handPose+" � UNVALIDATED TRACKING";
  if(CameraMode){
   string cameraState=cameraLauncher==null?"NOT CONFIGURED":cameraLauncher.State;
   forceText.text=(CameraGripScore*100).ToString("F0")+"% VISUAL GRIP\nCAMERA: "+cameraState+"  "+(cameraLauncher==null?"":cameraLauncher.FramesPerSecond.ToString("F0")+" FPS")+"  R:"+(vision.RightValid?"YES":"NO")+" L:"+(vision.LeftValid?"YES":"NO")+"  OK:"+vision.AcceptedPackets+" REJ:"+vision.RejectedPackets+"\n"+
    (Held!=null?Held.displayName+" | "+(Held.MaxDisplacementMeters*1000).ToString("F1")+" mm indentation":"Close near object, or press G to bring selected object to hand")+"\nNo measured force; open hand to release";
  } else forceText.text=(ForceValid?ForceNewtons.ToString("F1"):"--")+" N\n"+(Hardware?forceReceiver.State.Status:"Demo setpoint "+DemoSetpoint.ToString("F0")+" N")+"\n"+(Held!=null?Held.displayName+"  ·  "+(Held.MaxDisplacementMeters*1000).ToString("F1")+" mm deformation | "+Held.Damage+(Held.BreakThresholdNewtons>0?" | break > "+Held.BreakThresholdNewtons+" N":""):"Aim at an object and press E")+"\nHOLD  "+_task.ProgressSeconds.ToString("F1")+" / 3 s  ·  target 18 ± 3 N";
  progressFill.rectTransform.localScale=new Vector3(CameraMode?CameraGripScore:Mathf.Clamp01(_task.ProgressSeconds/3),1,1);
  string prompt=Status;
  if(Held==null&&Physics.Raycast(player.view.ViewportPointToRay(new Vector3(.5f,.5f,0)),out var hit,settings.reachMeters,~(1<<8),QueryTriggerInteraction.Ignore)){
   var item=hit.collider.GetComponentInParent<SqueezableObject>();if(item!=null)prompt=CameraMode?"Close tracked fingers, or G to summon selected object; E uses keyboard: "+item.displayName:"E  •  Pick up "+item.displayName;
   else if(hit.collider.GetComponentInParent<ApartmentDoor>()!=null)prompt="E  •  Open / close door";
  }
  if(CameraMode&&!ActiveHand.TrackingUsed&&vision.TrackingValid)
   prompt="Camera sees "+(vision.LeftValid?"LEFT":"RIGHT")+"; choose matching HAND in menu";
  else if(CameraMode&&!ActiveHand.TrackingUsed&&vision.RejectedPackets>0)
   prompt="Camera frames rejected: "+vision.LastRejection;
  if(CameraMode&&cameraLauncher!=null&&!string.IsNullOrEmpty(cameraLauncher.LastError))prompt=cameraLauncher.LastError;
  contextText.text="FINGER "+new[]{"INDEX","MIDDLE","RING","LITTLE","THUMB"}[ActiveHand.SelectedFinger]+" / "+(ActiveHand.SelectedJoint<0?"ALL JOINTS":new[]{"BASE","MIDDLE","TIP"}[ActiveHand.SelectedJoint])+"\n"+(player.Paused?"PAUSED � ESC to resume":prompt)+"\nWASD move  �  Mouse look  �  G camera summon  �  E keyboard grab  �  Q release  �  F sit / stand\nSpace/click squeeze  �  1�5 finger  �  J joint  �  X/Z bend/open  �  C reset  �  arrows/PgUp/PgDn move  �  TAB hand/object menu";
 }
}
}
