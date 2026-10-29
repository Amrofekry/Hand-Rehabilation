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
 public float RequiredHoldSeconds => _task.HoldSeconds;
 public void SetResearchDifficulty(int level, int maximumLevel, float maximumHoldSeconds) {
  float hold = Mathf.Lerp(3, Mathf.Clamp(maximumHoldSeconds,3,5), maximumLevel > 1 ? Mathf.Clamp01((level-1f)/(maximumLevel-1f)) : 0);
  if(Mathf.Abs(hold-_task.HoldSeconds)>.001f)_task=new ForceHoldTask(18,3,hold);
 }
 // Stage 8: last hand-object contact measurement and the hand-relative hold correction.
 public ContactReport LastContact{get;private set;}

 Vector3 _holdCorrectionLocal;float _palmExtra;const float HeldFollowSpeed=3.5f;
 // Chart grips: the held object's rotation from its grip, and the second hand's approach for two-handed grips.
 Quaternion _gripRotation;bool _hasGripRotation;float _secondHand;
 // Keyboard/demo presentation: the holding hand moves so the held object sits in the middle of the view, at a
 // distance that suits its size (like the grip chart photographs). Independent of the arrow-key hand offset.
 Vector3 _presentRight,_presentLeft;Vector3 _requestedGrip;bool _hasRequestedGrip;
 /// <summary>Stage 10: view-space shift of the presented object (activities keep it low-right so the aim point shows).</summary>
 public Vector3 PresentationOffset;
 /// <summary>Stage 10: raised after a pickup succeeds and after a held object is let go (activity scoring and assist).</summary>
 public event Action<SqueezableObject> ObjectGrabbed,ObjectReleased;
 public GripDefinition HeldGrip=>Held!=null&&GraspContactSolver.Enabled?GraspContactSolver.PropOf(Held)?.Definition:null;
 public float TestForce=-1; // Used only when an explicit acceptance-test command-line flag is present.
 public string Status{get;private set;}="Ready";
 private readonly Collider[] _clearanceOverlaps=new Collider[64];
 private readonly RaycastHit[] _clearanceHits=new RaycastHit[64];
 private Quaternion _heldRotationOffset;private Collider[] _playerColliders;
 private Vector3 _lastHeld;private Vector3 _velocity;private Vector3 _manualRight,_manualLeft;private bool _leftDemoSelected;private float _summonGraceUntil,_lastMotionTime,_cameraValidAt,_cameraClosedFor;private bool _cameraReleaseBlocked;
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
 _testing|=RehabAppShell.JourneyAcceptance;
 if(!_testing&&FindAnyObjectByType<RehabAppShell>()!=null)SetHardware(false);
 else if(settings.startWithHardware)SetHardware(true);else if(settings.startWithCamera&&!_testing)SetCameraMode();else SetHardware(false);}
 public void SetCameraMode(){SetHardware(false);Application.runInBackground=true;vision?.BeginNewSession();CameraMode=true;_cameraGrip.Reset();if(!_testing)cameraLauncher?.EnsureStarted();Status="Starting local MediaPipe camera";}
 public void CycleInputMode(){if(CameraMode)SetHardware(false);else if(Hardware)SetCameraMode();else SetHardware(true);}
 public void SetHardware(bool hardware){Release();player?.ResetGameplayInput();CameraMode=false;_cameraReleaseBlocked=false;CameraGripScore=0;_cameraGrip.Reset();cameraLauncher?.StopWorker();Hardware=hardware;_manualRight=_manualLeft=Vector3.zero;if(!hardware){rightHand.UseDemoPose();leftHand.UseDemoPose();}ForceNewtons=ContactForceNewtons=0;_filter.Reset();_filteredSamples=forceReceiver.State.SampleCount;_sessionValidSamples=0;_task.Reset();_generation=forceReceiver.State.Generation;Status=hardware?"Waiting for calibrated Phase 2 input":"Keyboard & mouse demo";}
 private void Update(){
  AutoSelectCameraHand();
  bool gameplayInput=player.GameplayInputAllowed;
  if(gameplayInput&&Input.GetKeyDown(KeyCode.B))CycleInputMode();
  gameplayInput=player.GameplayInputAllowed;
  if(gameplayInput){
   if(!Hardware&&!CameraMode){DemoSetpoint=RehabKeyboardGate.AdjustSetpoint(DemoSetpoint,Input.mouseScrollDelta.y,Input.GetKey(KeyCode.Equals)||Input.GetKey(KeyCode.KeypadPlus),Input.GetKey(KeyCode.Minus)||Input.GetKey(KeyCode.KeypadMinus),Time.deltaTime);UpdateDemoHandMovement();}
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
   if(CameraMode&&Input.GetKeyDown(KeyCode.C)){Release();ActiveHand.RecenterTracking();Status="Camera hand orientation recentered";}
  }
  gameplayInput=player.GameplayInputAllowed;
  var selectedFrame=LeftSelected?vision.LatestLeft:vision.LatestRight;
  bool cameraTracked=CameraMode&&(LeftSelected?vision.LeftValid:vision.RightValid)&&
   (cameraLauncher!=null&&cameraLauncher.CameraOpened||_testing)&&selectedFrame!=null&&
   ((selectedFrame.source??"").StartsWith("mediapipe-",StringComparison.OrdinalIgnoreCase)||_testing)&&HandKinematics.HasPoseGeometry(selectedFrame);
  bool cameraValid=cameraTracked&&!player.Paused;
  if(CameraMode){
   ForceValid=cameraValid;
   // 2026-10-29 hybrid control: a short tracking gap keeps the hold and the gate state (GraspHoldRule.LossGraceSeconds);
   // the thumb-aware grasp score lets pinches grab and keeps pointing (walk) from grabbing.
   if(cameraValid)_cameraValidAt=Time.unscaledTime;
   bool cameraGrace=!cameraValid&&Held!=null&&!GraspHoldRule.ShouldRelease(true,false,Time.unscaledTime-_cameraValidAt,player.Paused);
   CameraGripScore=cameraTracked?HandGrasp.Score(selectedFrame):cameraGrace?CameraGripScore:0;
   if(!cameraGrace)_cameraGrip.Step(CameraGripScore,cameraValid,Time.deltaTime,settings.cameraCloseAt,settings.cameraOpenAt,settings.cameraGestureDwellSeconds);
   _cameraClosedFor=_cameraGrip.Closed?_cameraClosedFor+Time.deltaTime:0;
   if(cameraValid&&!_cameraGrip.Closed&&CameraGripScore<=settings.cameraOpenAt)_cameraReleaseBlocked=false;
   if(!cameraValid&&Held!=null&&!cameraGrace){Release();Status="Camera tracking lost - object released";}
   // Hysteresis: hold until the gate opens (score at or below the open threshold for the dwell), not on the first dip under the grab threshold.
   else if(_cameraGrip.Closed||cameraGrace)_summonGraceUntil=0;
   else if(Held!=null&&Time.unscaledTime>=_summonGraceUntil){Release();Status="Hand opened - object released";}
   ForceNewtons=cameraValid||cameraGrace?Mathf.MoveTowards(ForceNewtons,CameraGripScore*settings.cameraVisualMaxNewtons,90*Time.deltaTime):0;
  } else if(Hardware){
   ForceValid=forceReceiver.Latest.Valid&&!player.Paused;ForceNewtons=ForceValid?forceReceiver.Latest.Newtons:0;
   if(_generation!=forceReceiver.State.Generation){_generation=forceReceiver.State.Generation;Release();_filter.Reset();_filter.Push(0,Time.deltaTime,settings.forceSmoothingSeconds,settings.isolatedSpikeNewtons,settings.maxForceSlewNewtonsPerSecond);_sessionValidSamples=1;_filteredSamples=forceReceiver.State.SampleCount;ContactForceNewtons=0;_task.Reset();Status="Connection/calibration changed";}
   if(!ForceValid&&Held!=null){Release();Status="Input stale: object released";}
  } else {
   ForceValid=gameplayInput;float requested=RehabKeyboardGate.DemoForce(true,gameplayInput,player.DemoSqueezing,Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift),DemoSetpoint);
   if(_testing&&TestForce>=0&&ForceValid)requested=TestForce;
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
  var rightTracked=CameraMode?cameraTracked&&!LeftSelected:Hardware&&vision!=null&&vision.RightValid;
  var leftTracked=CameraMode?cameraTracked&&LeftSelected:Hardware&&vision!=null&&vision.LeftValid;
  float closure=Held!=null?Mathf.Lerp(.45f,.95f,Mathf.Clamp01(ForceNewtons/45)):Mathf.Clamp01(ForceNewtons/55);
  float handLift=player.IsSeated?settings.seatedHandsLiftMeters:0;
  // Stage 8 grip chart: shape the hand into the object's grip and present it like the chart photograph.
  var grip=HeldGrip;bool twoHands=grip!=null&&grip.twoHands;var otherHand=LeftSelected?rightHand:leftHand;
  ActiveHand.DemoGrip=grip;otherHand.DemoGrip=twoHands?grip:null;
  Quaternion presented=Quaternion.identity,otherPresented=Quaternion.identity;
  ActiveHand.HasDemoRotation=grip!=null&&GraspContactSolver.TryViewRotation(ActiveHand,player.view.transform,grip.view,LeftSelected,out presented);
  ActiveHand.DemoRotation=presented;
  otherHand.HasDemoRotation=twoHands&&GraspContactSolver.TryViewRotation(otherHand,player.view.transform,grip.view,!LeftSelected,out otherPresented);
  otherHand.DemoRotation=otherPresented;
  UpdatePresentation(grip!=null&&!(LeftSelected?leftTracked:rightTracked),handLift);
  rightHand.Pose(rightTracked?vision.LatestRight:null,rightTracked,LeftSelected?(twoHands?closure:0):closure,Time.deltaTime,settings.trackingSmoothing,_rightRest+_manualRight+_presentRight+Vector3.up*handLift,settings.trackingTravelMeters,CameraMode);
  float leftDemo=LeftSelected||twoHands?closure:0;
  leftHand.Pose(leftTracked?vision.LatestLeft:null,leftTracked,leftDemo,Time.deltaTime,settings.trackingSmoothing,_leftRest+_manualLeft+_presentLeft+Vector3.back*settings.leftWristSetbackMeters+Vector3.up*handLift,settings.trackingTravelMeters,CameraMode);
  // Stage 8: fingers stop at the held object's surface; the object settles into the pads, out of the palm.
  bool otherTracked=LeftSelected?rightTracked:leftTracked;
  if(virtualGrasp!=null)virtualGrasp.ShowBoth(twoHands);
  if(twoHands&&!otherTracked&&GraspContactSolver.TrySecondHandPosition(otherHand,Held,out var secondWrist)){
   // The second hand comes in over a third of a second, then rests on the far side of the object.
   _secondHand=Mathf.MoveTowards(_secondHand,1,Time.deltaTime*3);
   otherHand.transform.position=Vector3.Lerp(otherHand.transform.position,secondWrist,_secondHand);
   GraspContactSolver.Solve(otherHand,Held,contactRig!=null?contactRig.radiusMeters:.009f,true);
  } else _secondHand=0;
  if(Held!=null){
   LastContact=GraspContactSolver.Solve(ActiveHand,Held,contactRig!=null?contactRig.radiusMeters:.009f,GraspContactSolver.Enabled);
   // Palm grips: a one-way extra offset grows only while a segment still overlaps, so it settles without hunting.
   if(GraspContactSolver.Enabled&&LastContact.valid&&LastContact.shape!=HandGrip.Pinch){
    if(LastContact.maxPenetration>.001f)_palmExtra=Mathf.Min(.04f,_palmExtra+Mathf.Min(LastContact.maxPenetration-.001f,.06f*Time.deltaTime));
    // Clear of the hand but not reaching the pads (the hand has finished turning, say): settle back slowly.
    else if(LastContact.maxPenetration<=0&&LastContact.padGap>.003f)_palmExtra=Mathf.Max(0,_palmExtra-.02f*Time.deltaTime);
   }
   // Pinches (shape and chart grips alike) ease the object toward pads that have not reached it, bounded and rate-limited.
   if(GraspContactSolver.Enabled&&LastContact.valid&&LastContact.shape==HandGrip.Pinch&&(!LastContact.hasGrip||grip!=null&&grip.PinchPair)){
    var correction=ContactGeometry.HoldCorrection(ActiveHand.transform.TransformVector(_holdCorrectionLocal),LastContact.padGap,LastContact.towardPads,LastContact.palmDepth,LastContact.outOfPalm,Time.deltaTime);
    _holdCorrectionLocal=ActiveHand.transform.InverseTransformVector(correction);
   }
  } else {LastContact=default;_holdCorrectionLocal=Vector3.zero;_palmExtra=0;}
  if(CameraMode&&cameraValid&&!_cameraReleaseBlocked&&_cameraGrip.Closed&&_cameraClosedFor>=.15f&&Held==null)TryCameraGrab();
  if(Time.unscaledTime>=_nextUi){_nextUi=Time.unscaledTime+.08f;RefreshHUD();}
 }
 void UpdatePresentation(bool present,float handLift){
  ref Vector3 offset=ref (LeftSelected?ref _presentLeft:ref _presentRight);
  ref Vector3 idle=ref (LeftSelected?ref _presentRight:ref _presentLeft);
  idle=Vector3.MoveTowards(idle,Vector3.zero,.8f*Time.deltaTime);
  var parent=ActiveHand.transform.parent;
  if(!present||Held==null||parent==null||!_hasRequestedGrip){offset=Vector3.MoveTowards(offset,Vector3.zero,.8f*Time.deltaTime);return;}
  var renderer=Held.visual!=null?Held.visual.GetComponentInChildren<Renderer>():null;
  var bounds=renderer!=null?renderer.bounds:new Bounds(Held.transform.position,Vector3.one*Held.radius);
  float distance=Mathf.Clamp(.3f+bounds.extents.magnitude*2.2f,.32f,.7f);
  var wantedWorld=player.view.transform.TransformPoint(new Vector3(0,-.03f,distance)+PresentationOffset);
  // Middle of the view, but never down into the table or counter under it (seated or standing).
  if(Physics.Raycast(wantedWorld+Vector3.up*.6f,Vector3.down,out var support,1.2f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore))
   wantedWorld.y=Mathf.Max(wantedWorld.y,support.point.y+bounds.extents.y+.05f);
  var wanted=parent.InverseTransformPoint(wantedWorld);
  // Where the grip would put the object with the hand at rest: the requested pose (not the safety-clamped
  // object, so a blocked placement can never push the hand further away) minus the hand's actual displacement
  // from rest (exact even while the smoothed hand is still moving).
  var rest=(LeftSelected?_leftRest+Vector3.back*settings.leftWristSetbackMeters:_rightRest)+Vector3.up*handLift;
  var unpresented=parent.InverseTransformPoint(_requestedGrip+(bounds.center-Held.transform.position))-(ActiveHand.transform.localPosition-rest);
  var target=Vector3.ClampMagnitude(wanted-unpresented,.35f);
  // A small dead band: the hand settles instead of hunting by a millimetre or two.
  if((target-offset).sqrMagnitude>.003f*.003f)offset=Vector3.MoveTowards(offset,target,.8f*Time.deltaTime);
 }
 // Follow the only actively observed physical hand. When both are visible,
 // retain the selected side until its stream has stopped refreshing.
 private void AutoSelectCameraHand(){
  if(!CameraMode||vision==null||Held!=null)return; // never swap hands (which releases) while holding
  double selectedAge=LeftSelected?vision.LeftAgeSeconds:vision.RightAgeSeconds;
  double otherAge=LeftSelected?vision.RightAgeSeconds:vision.LeftAgeSeconds;
  if(otherAge<.15&&(double.IsPositiveInfinity(selectedAge)||selectedAge>1.0)){
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
  if(!Held.gameObject.activeInHierarchy||!Held.IsHeld){Held=null;_velocity=Vector3.zero;_task.Reset();return;}

  if(!TrySafeGripTarget(Held,out var requested)){_velocity=Vector3.zero;return;}
  // Stage 8: a fast hand cannot make the held object jump, snap or tunnel.
  if(GraspContactSolver.Enabled)requested=ContactGeometry.Follow(_lastHeld,requested,HeldFollowSpeed,Time.fixedDeltaTime);
  _velocity=GraspMotionPolicy.ReleaseVelocity(_lastHeld,requested,Time.fixedDeltaTime,settings.releaseVelocityGain,settings.maximumReleaseSpeed,settings.maximumReleaseStep);
  _lastHeld=requested;_lastMotionTime=Time.unscaledTime;
  Held.MoveHeld(requested,_hasGripRotation?_gripRotation:GraspMotionPolicy.HeldRotation(ActiveHand.transform.rotation,_heldRotationOffset));
 }
 private Vector3 GripTarget(SqueezableObject item){
  // Stage 8 grip chart: the object's grip point sits at the grip's anchor (palm, finger tips, thumb and
  // index side, or hooked fingers), turned into the hand.
  _hasGripRotation=false;
  if(GraspContactSolver.Enabled&&GraspContactSolver.TryGripPose(ActiveHand,item,item==Held?_palmExtra:0,out var gripPosition,out var gripRotation)){
   _gripRotation=gripRotation;_hasGripRotation=true;
   if(item==Held){gripPosition+=ActiveHand.transform.TransformVector(_holdCorrectionLocal);_requestedGrip=gripPosition;_hasRequestedGrip=true;}
   return gripPosition;
  }
  // Power and cylindrical grips rest on the palm; only pinch-sized objects use the fingertip placement below.
  if(GraspContactSolver.Enabled&&GraspContactSolver.TryPalmHold(ActiveHand,item,out var palmHold,item==Held?_palmExtra:0))return palmHold;
  var lift=player.IsSeated?settings.seatedHandsLiftMeters:0;
  var handOffset=ActiveHand.transform.localPosition-(LeftSelected?_leftRest:_rightRest)-Vector3.up*lift;
  var anchor=holdAnchor.localPosition;if(LeftSelected)anchor.x=-Mathf.Abs(anchor.x);
  var target=holdAnchor.parent.TransformPoint(anchor)+player.view.transform.TransformVector(handOffset)+Vector3.up*(player.IsSeated?settings.seatedHoldLiftMeters:0);
  // The hand prefab's palm-to-fingertip reach differs from the HUD anchor on
  // both sides. Keep the held object's centre at the animated finger pads so
  // physical probe contacts drive deformation even after camera/seat motion.
  if(contactRig!=null&&contactRig.contactBones!=null&&contactRig.contactBones.Length==15){
   var bones=contactRig.contactBones;
   if(bones[1]!=null&&bones[4]!=null&&bones[7]!=null&&bones[10]!=null){
    Vector3 indexMiddle=(bones[1].position+bones[4].position)*.5f;
    Vector3 ringLittle=(bones[7].position+bones[10].position)*.5f;
    Vector3 across=indexMiddle-ringLittle;
    // A ball centred on every finger penetrates the whole hand. Bias its
    // centre toward the index side of the grasp aperture so contact remains
    // local and the other finger probes are free to report zero force.
    Vector3 pads=indexMiddle+(across.sqrMagnitude>.000001f?across.normalized:ActiveHand.transform.right)*Mathf.Min(.11f,item!=null?item.radius*.75f:.09f);
    target+=Vector3.ClampMagnitude(pads-target,.23f);
   }
  }
  if(item!=null){var toward=ActiveHand.transform.position-target;float correction=Mathf.Clamp(.12f-item.radius,0,.05f);if(toward.sqrMagnitude>.0001f)target+=toward.normalized*correction;}
  if(item!=null&&item==Held)target+=ActiveHand.transform.TransformVector(_holdCorrectionLocal);
  return target;
 }
 // Asset pivots need not lie on their usable surface; include child colliders.
 private bool CanReach(SqueezableObject item){
  if(item==null||!item.gameObject.activeInHierarchy||item.IsBroken||!item.HasPickupCollider)return false;
  var eye=player.view.transform.position;var point=item.ClosestSurface(eye);var direction=point-eye;
  if(direction.magnitude>settings.reachMeters)return false;
  // Stage 10: something the object rests on or in (a pan, a plate) touching its surface does not block it.
  return direction.sqrMagnitude<.000001f||!Physics.Raycast(eye,direction.normalized,out var hit,direction.magnitude+.01f,~(1<<8),QueryTriggerInteraction.Ignore)||hit.collider.GetComponentInParent<SqueezableObject>()==item||hit.distance>=direction.magnitude-.03f;
 }
 // Check endpoint overlap as well as the camera-to-hand sweep.
 private bool TrySafeGripTarget(SqueezableObject item,out Vector3 target){
  var origin=player.view.transform.position;target=GripTarget(item);
  if(!GraspMotionPolicy.Finite(target))return false;
  float clearance=item.ClearanceRadius+.01f;int mask=~((1<<8)|(1<<9));var delta=target-origin;
  // Stage 8: the path is swept with at most 5 cm (enough to stop tunnelling through walls); the full object
  // size is checked where it lands. A full-size sweep from the eye grazed the table and held large objects short.
  bool stage8=GraspContactSolver.Enabled; // -contact-solver-off keeps the pre-Stage-8 checks for the audit baseline
  float sweep=stage8?Mathf.Min(clearance,.05f):clearance;
  if(delta.sqrMagnitude>.000001f){
   int count=Physics.SphereCastNonAlloc(origin,sweep,delta.normalized,_clearanceHits,delta.magnitude,mask,QueryTriggerInteraction.Ignore);
   if(count==_clearanceHits.Length)return false; // saturated query: no unsafe placement
   float distance=delta.magnitude;
   for(int i=0;i<count;i++)if(IsClearanceObstacle(_clearanceHits[i].collider))distance=Mathf.Min(distance,Mathf.Max(0,_clearanceHits[i].distance-.01f));
   target=origin+delta.normalized*distance;
  }
  // Stage 8: test the object's own colliders where they would land (a bounding sphere around a long hammer or a
  // kettlebell is several times larger than the object and pulled it back toward the eye).
  var landing=_hasGripRotation?_gripRotation:item.Body.rotation;
  if(!Blocked(item,target,landing,clearance,mask,stage8))return true;
  if(Blocked(item,origin,landing,clearance,mask,stage8))return false;
  Vector3 blocked=target,safe=origin;
  for(int i=0;i<10;i++){var middle=(safe+blocked)*.5f;if(Blocked(item,middle,landing,clearance,mask,stage8))blocked=middle;else safe=middle;}
  target=safe;return true;
 }
 private bool IsClearanceObstacle(Collider collider)=>collider!=null&&!collider.transform.IsChildOf(player.transform)&&collider.GetComponentInParent<SqueezableObject>()==null;
 private bool Blocked(SqueezableObject item,Vector3 point,Quaternion rotation,float clearance,int mask,bool exact)=>exact?ObjectBlockedAt(item,point,rotation,mask):SphereBlocked(point,clearance,mask);
 private bool SphereBlocked(Vector3 point,float clearance,int mask){
  int count=Physics.OverlapSphereNonAlloc(point,clearance,_clearanceOverlaps,mask,QueryTriggerInteraction.Ignore);
  if(count==_clearanceOverlaps.Length)return true;
  for(int i=0;i<count;i++)if(IsClearanceObstacle(_clearanceOverlaps[i]))return true;
  return false;
 }
 private bool ObjectBlockedAt(SqueezableObject item,Vector3 position,Quaternion rotation,int mask){
  var body=item.transform;var inverse=Quaternion.Inverse(body.rotation);
  foreach(var c in GraspContactSolver.SolidColliders(item)){
   if(c==null||!c.enabled)continue;
   var p=position+rotation*(inverse*(c.transform.position-body.position));var r=rotation*inverse*c.transform.rotation;
   float reach=Vector3.Distance(c.transform.position,c.bounds.center)+c.bounds.extents.magnitude+.01f;
   int count=Physics.OverlapSphereNonAlloc(p,reach,_clearanceOverlaps,mask,QueryTriggerInteraction.Ignore);
   if(count==_clearanceOverlaps.Length)return true;
   for(int i=0;i<count;i++){
    var other=_clearanceOverlaps[i];if(!IsClearanceObstacle(other))continue;
    if(Physics.ComputePenetration(c,p,r,other,other.transform.position,other.transform.rotation,out _,out float depth)&&depth>.002f)return true;
   }
  }
  return false;
 }
 private bool BeginGrab(SqueezableObject item,bool summon){
  if(!TrySafeGripTarget(item,out var target)){Status="Not enough space for this object; move away from the wall";return false;}
  if(!item.Grab())return false;
  Held=item;
  if(_playerColliders==null)_playerColliders=player.GetComponentsInChildren<Collider>(true);
  item.ProtectFromPlayer(_playerColliders);
  Quaternion initial=_hasGripRotation?_gripRotation:summon?holdAnchor.rotation:item.Body.rotation;_secondHand=0;
  _requestedGrip=target;_hasRequestedGrip=_hasGripRotation;
  _heldRotationOffset=GraspMotionPolicy.RelativeRotation(ActiveHand.transform.rotation,initial);
  // Pickup is explicit relocation, never a source of release momentum.
  item.Body.position=target;item.Body.rotation=initial;Physics.SyncTransforms();_holdCorrectionLocal=Vector3.zero;_palmExtra=0;
  _lastHeld=target;_lastMotionTime=Time.unscaledTime;_velocity=Vector3.zero;_task.Reset();
  _lastSamples=forceReceiver.State.SampleCount;_lastMs=forceReceiver.State.SampleMs;
  ObjectGrabbed?.Invoke(item);
  return true;
 }
 private SqueezableObject KeyboardTarget(out ApartmentDoor door){
  door=null;var ray=player.view.ViewportPointToRay(new Vector3(.5f,.5f,0));
  if(Physics.Raycast(ray,out var hit,settings.reachMeters,~(1<<8),QueryTriggerInteraction.Ignore)){
   var direct=hit.collider.GetComponentInParent<SqueezableObject>();
   if(CanReach(direct))return direct;
   door=hit.collider.GetComponentInParent<ApartmentDoor>();if(door!=null)return null;
  }
  SqueezableObject nearest=null;float best=float.PositiveInfinity;
  foreach(var item in objects){
   if(!CanReach(item))continue;
   float along=Vector3.Dot(item.transform.position-ray.origin,ray.direction);
   var point=item.ClosestSurface(ray.GetPoint(Mathf.Clamp(along,0,settings.reachMeters)));
   float score=GraspMotionPolicy.AimScore(ray.origin,ray.direction,point,settings.reachMeters,settings.pickupAssistDegrees);
   if(score<best){nearest=item;best=score;}
  }
  return nearest;
 }
 // Closing fingers near a visible object grabs it without E. A distant fist cannot pick it up.
 private void TryCameraGrab(){
  if(contactRig==null||contactRig.contactBones==null)return;
  SqueezableObject nearest=null;float best=settings.cameraGrabRadiusMeters;
  foreach(var item in objects){
   if(!CanReach(item))continue;
   foreach(var bone in contactRig.contactBones){
    if(bone==null)continue;
    var tip=bone.childCount>0?bone.GetChild(0).position:bone.position;
    float distance=Vector3.Distance(tip,item.ClosestSurface(tip));
    if(distance<best){best=distance;nearest=item;}
   }
  }
  if(nearest!=null&&TryGrab(nearest)){Status="Camera grip: holding "+nearest.displayName;return;}
  // Aim assist (default, a setting): with no fingertip at an object, a closed hand takes what you look at within reach,
  // like the E key. Counted separately so assisted pickups are never mistaken for physical reach.
  if(!HandLocomotion.AimAssist)return;
  var aimed=KeyboardTarget(out _);
  if(aimed!=null&&TryGrab(aimed)){AssistedGrabs++;Status="Camera grip (aim assist): holding "+aimed.displayName;}
 }
 /// <summary>Camera pickups made by aim assist rather than a fingertip at the object (2026-10-29).</summary>
 public int AssistedGrabs{get;private set;}
 // G brings the station's selected object into the tracked hand without
 // changing input mode. The patient must close the hand within a short grace
 // period; tracking loss always releases immediately.
 public bool TryCameraSummonSelectedObject(){
  if(!CameraMode||player.Paused||Held!=null||choices==null||vision==null)return false;
  var frame=LeftSelected?vision.LatestLeft:vision.LatestRight;
  if(!(LeftSelected?vision.LeftValid:vision.RightValid)||frame==null||
     !HandKinematics.HasPoseGeometry(frame)||
     !(cameraLauncher!=null&&cameraLauncher.CameraOpened||_testing)||
     !((frame.source??"").StartsWith("mediapipe-",StringComparison.OrdinalIgnoreCase)||_testing)){
   Status="Show the selected hand to the camera before pressing G";return false;
  }
  var item=choices.SelectedObject;
  if(item==null||!item.gameObject.activeInHierarchy||item.IsBroken||!BeginGrab(item,true))return false;
  _cameraReleaseBlocked=false;_summonGraceUntil=Time.unscaledTime+1.25f;
  Status="Camera hand: close fingers to keep "+item.displayName;
  return true;
 }
 // E is an explicit request for keyboard pickup. It switches camera mode to
 // demo before grasping, so missing camera frames cannot imply measured force.
 public bool TryKeyboardAction(){
  if(player.Paused)return false;
  if(Held!=null){Release();return true;}
  var item=KeyboardTarget(out var door);
  if(item!=null){
   if(CameraMode)SetHardware(false);
   return TryGrab(item);
  }
  if(door==null)return false;
  door.Toggle();return true;
 }
 public bool TryGrab(SqueezableObject item){
  if(item==null||Held!=null||item.IsBroken||player.Paused||Hardware&&!forceReceiver.Latest.Valid||CameraMode&&!ForceValid)return false;
  if(!CanReach(item)||!BeginGrab(item,false))return false;
  var grip=HeldGrip;
  Status="Holding "+item.displayName+(grip!=null?"  -  "+grip.number+". "+grip.name+" ("+grip.fingers+")":"")+
   (item.Material!=null?"  -  "+item.Material.name+", "+ApartmentChoices.ClassName(item.Material.materialClass)+", "+item.Material.result.ToString().ToLowerInvariant():"");return true;
 }
 public void Release(){
  if(Held!=null){
   var velocity=!player.Paused&&Time.unscaledTime-_lastMotionTime<=.1f?_velocity:Vector3.zero;
   var released=Held;Held.Release(velocity);Held=null;ObjectReleased?.Invoke(released);
   if(CameraMode)_cameraReleaseBlocked=true;
   Status=CameraMode?"Released - open hand before gripping again, or use G":"Object released";
  }
  _velocity=Vector3.zero;_summonGraceUntil=0;_task.Reset();
 }
 private void OnDisable(){Release();}
 public void ResetObjects(){Release();foreach(var item in objects)item.ResetObject();Status="Objects restored to their stations";}
 private static string VisionLabel(LandmarkFrame frame)=>frame!=null&&(frame.source??"").StartsWith("synthetic",StringComparison.OrdinalIgnoreCase)?"SYNTHETIC":"CAMERA";
 private void RefreshHUD(){
  string source=CameraMode?"MEDIAPIPE CAMERA":Hardware?(forceReceiver.State.IsSynthetic?"SYNTHETIC REPLAY":"PHASE 2 / BLE"):"KEYBOARD + MOUSE DEMO";
  string handPose=CameraMode?((LeftSelected?leftHand:rightHand).TrackingUsed?"LIVE":"SEARCHING"):LeftSelected?(vision.LeftValid?VisionLabel(vision.LatestLeft):Hardware?(ForceValid?"FORCE":"REST"):"DEMO"):(vision.RightValid?VisionLabel(vision.LatestRight):Hardware?(ForceValid?"FORCE":"REST"):"DEMO");
  statusText.text="GRIP / REHAB  /  MODULE 01\n"+source+"  |  "+(player.IsSeated?"SEATED":"WALKING")+"\nSELECTED HAND: "+(LeftSelected?"LEFT ":"RIGHT ")+handPose;
  if(CameraMode){
   string cameraState=cameraLauncher==null?"NOT CONFIGURED":cameraLauncher.State;
   forceText.text=(ForceValid?(CameraGripScore*100).ToString("F0"):"--")+"% VISUAL GRIP\nCAMERA: "+cameraState+"  "+(cameraLauncher==null?"":cameraLauncher.FramesPerSecond.ToString("F0")+" FPS")+"  R:"+(vision.RightValid?"YES":"NO")+" L:"+(vision.LeftValid?"YES":"NO")+"  OK:"+vision.AcceptedPackets+" REJ:"+vision.RejectedPackets+"\n"+
    (Held!=null?Held.displayName+" | "+(Held.MaxDisplacementMeters*1000).ToString("F1")+" mm indentation":"Close near object, or press G to bring selected object to hand")+"\nNo measured force; open hand to release";
  } else forceText.text=(ForceValid?ForceNewtons.ToString("F1"):"--")+" N "+(Hardware?(ForceValid?(forceReceiver.State.IsSynthetic?"REPLAY":"MEASURED"):"NO FRESH SAMPLE"):"SIMULATED")+"\n"+(Hardware?forceReceiver.State.Status:"Demo setpoint "+DemoSetpoint.ToString("F0")+" N")+"\n"+(Held!=null?Held.displayName+"  |  "+(Held.MaxDisplacementMeters*1000).ToString("F1")+" mm deformation | "+Held.Damage+(Held.BreakThresholdNewtons>0?" | break > "+Held.BreakThresholdNewtons+" N":""):"Aim at an object and press E")+"\nHOLD  "+_task.ProgressSeconds.ToString("F1")+" / "+_task.HoldSeconds.ToString("F1")+" s  |  research target 18 +/- 3 N";
  progressFill.rectTransform.localScale=new Vector3(CameraMode?CameraGripScore:Mathf.Clamp01(_task.ProgressSeconds/_task.HoldSeconds),1,1);
  string prompt=Status;
  if(Held==null){
   var item=KeyboardTarget(out var door);
   if(item!=null)prompt=CameraMode?"Close tracked fingers, or G to summon selected object; E uses keyboard: "+item.displayName:"E  •  Pick up "+item.displayName;
   else if(door!=null)prompt="E  •  Open / close door";
  }
  if(CameraMode&&!ActiveHand.TrackingUsed&&vision.TrackingValid)
   prompt="Camera sees "+(vision.LeftValid?"LEFT":"RIGHT")+"; choose matching HAND in menu";
  else if(CameraMode&&!ActiveHand.TrackingUsed&&vision.RejectedPackets>0)
   prompt="Camera frames rejected: "+vision.LastRejection;
  if(CameraMode&&cameraLauncher!=null&&!string.IsNullOrEmpty(cameraLauncher.LastError))prompt=cameraLauncher.LastError;
  string gripHelp=CameraMode?"CAMERA: close/open tracked hand  |  G summon  |  E switch to keyboard pickup":
   Hardware?"PHASE 2: measured grip  |  E pick up  |  Q release":
   "KEYBOARD DEMO: E pick up  |  hold Space/click to squeeze  |  Q release";
  contextText.text="FINGER "+new[]{"INDEX","MIDDLE","RING","LITTLE","THUMB"}[ActiveHand.SelectedFinger]+" / "+(ActiveHand.SelectedJoint<0?"ALL JOINTS":new[]{"BASE","MIDDLE","TIP"}[ActiveHand.SelectedJoint])+"  |  "+(player.Paused?"PAUSED - ESC to resume":prompt)+"\n"+gripHelp+"  |  F sit / stand\nWASD / mouse move  |  1-5 finger  |  J joint  |  X/Z bend/open  |  C recenter  |  TAB options  |  B input";
 }
}
}
