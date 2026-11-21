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
 /// <summary>Tea activity: when HeldOriented, the held object takes this absolute rotation, turned about a point in its own
 /// space (the grip on a teapot handle, the rim of a cup) so that point stays where the grip put it: pouring and drinking
 /// turn it the way a wrist does. Set fresh each frame by the activity; no read-back of the (interpolated) transform.</summary>
 public bool HeldOriented;public Quaternion HeldOrientation=Quaternion.identity;public Vector3 HeldTiltPivotLocal;
 /// <summary>Tea, camera hand: while drinking, the tipping point (cup rim) is drawn to this world point (the lips) by
 /// HeldPivotBlend 0..1; the activity adds the hand's own motion to it, so tremor still shakes the cup.</summary>
 public Vector3 HeldPivotWorld;public float HeldPivotBlend;
 /// <summary>How far (m) the presentation may carry the hand from rest; the tea activity reaches further to hold a teapot over a low cup.</summary>
 public float PresentationReach=.35f;
 /// <summary>How fast (m/s) the presentation carries the hand (keyboard mode); a full tea cup is carried slowly.</summary>
 public float PresentationSpeed=.8f;
 /// <summary>When above 0, the presentation eases the hand with this acceleration limit (m/s²) instead of a constant speed.</summary>
 public float PresentationAccel;Vector3 _presentVelRight,_presentVelLeft;
 /// <summary>When set, the presentation brings the held object's centre to this world point (tea: the teapot over the cup)
 /// instead of the middle of the view.</summary>
 public bool HasPresentationTarget;public Vector3 PresentationTarget;
 /// <summary>Stage 10: raised after a pickup succeeds and after a held object is let go (activity scoring and assist).</summary>
 public event Action<SqueezableObject> ObjectGrabbed,ObjectReleased;
 public GripDefinition HeldGrip=>Held!=null&&GraspContactSolver.Enabled?GraspContactSolver.PropOf(Held)?.Definition:null;
 public float TestForce=-1; // Used only when an explicit acceptance-test command-line flag is present.
 public string Status{get;private set;}="Ready";
 private readonly Collider[] _clearanceOverlaps=new Collider[64];
 private readonly RaycastHit[] _clearanceHits=new RaycastHit[64];
 // ---- Camera hand, automatic depth (2026-11-19; Hand Lab, real hand: palm-size depth was noise-led). The active hand keeps
 // its on-screen place from the camera image; its distance is eased to just short of what lies under it (the line from the
 // eye through the hand), and the object under the hand is the one a closing hand picks up.
 private readonly RaycastHit[] _reachHits=new RaycastHit[16];private float _reach=float.NaN,_heldReach,_reachGoal=float.NaN,_reachPending,_reachPendingFor;private SqueezableObject _reachHeldFor;
 /// <summary>Camera hand screen mapping, measured on a real hand with a laptop webcam (Hand Lab 2026-11-19): a comfortable
 /// sweep used wrist x 0.12-0.88 and y 0.50-0.95 of the image (y down), and the resting wrist sat at y 0.73, not 0.5.
 /// HandCentreY is the image row (y down) that maps to the view centre; HandRange* is the offset from it that reaches the edge.
 /// The resting row differs per sitting (0.73 and 0.78 in two sessions), so HandCentreY follows where the hand works: a slow
 /// average (20 s) of the tracked wrist row, kept within 0.55-0.85.</summary>
 public static float HandRangeX=.36f,HandRangeY=.25f,HandCentreY=.75f;
 /// <summary>The distance (m) from the eye at which the camera hand sits now (NaN outside camera mode).</summary>
 public float CameraReach=>_reach;
 // ---- Camera-hand depth (2026-11-21): the hand nearer the camera moves the virtual hand forward (HandDepth). A setting;
 // AUTOMATIC keeps the hand on whatever is under it (the previous behaviour).
 public readonly HandDepth Depth=new HandDepth();float _depthAtPickup;
 const string DepthKey="GripRehab.HandDepthCamera";static int _depthSetting=-1;
 public static bool DepthFromCamera{get{if(_depthSetting<0)_depthSetting=PlayerPrefs.GetInt(DepthKey,1)==1?1:0;return _depthSetting==1;}
  set{_depthSetting=value?1:0;PlayerPrefs.SetInt(DepthKey,_depthSetting);}}
 // ---- Intent assist (2026-11-21, plan Phase 4): which object or place the hand is going for, and bounded help to get there.
 /// <summary>The assist core (Unity-free, tested offline); its level is the saved setting (Medium by default).</summary>
 public readonly IntentAssist Intent=new IntentAssist();
 const string AssistKey="GripRehab.IntentAssist";static int _assistSetting=-1;
 // -assist-level=0..3 (probes) sets the level for this run only, without saving it.
 public static AssistLevel AssistSetting{get{if(_assistSetting<0){_assistSetting=Mathf.Clamp(PlayerPrefs.GetInt(AssistKey,(int)AssistLevel.Medium),0,3);
   foreach(var arg in System.Environment.GetCommandLineArgs())if(arg.StartsWith("-assist-level=")&&int.TryParse(arg.Substring(14),out var lv))_assistSetting=Mathf.Clamp(lv,0,3);}return (AssistLevel)_assistSetting;}
  set{_assistSetting=Mathf.Clamp((int)value,0,3);PlayerPrefs.SetInt(AssistKey,_assistSetting);}}
 readonly SqueezableObject[] _intentItems=new SqueezableObject[IntentAssist.MaxCandidates];readonly SnapTarget[] _intentPlaces=new SnapTarget[IntentAssist.MaxCandidates];readonly Vector3[] _intentPlacePoints=new Vector3[IntentAssist.MaxCandidates];
 // The held object's offset from the hand, in the hand's frame, averaged over 0.6 s from 0.3 s after the pickup: measured
 // instantly, the object's physics lag grew the offset as the hand moved and the hand chased a receding point; fixed at
 // the pickup, it missed how the object settles in the hand (5-11 cm). The average follows the settled offset only.
 SqueezableObject _gripFor;float _gripSince;Vector3 _gripLocal;bool _gripKnown;
 // A storage box (PlacementVolume) is offered as a place too: its opening, as a place with id "volume" (the activity then
 // finds a free spot inside instead of snapping to the middle). Listed once per activity, never per frame.
 Activity _volumesFor;PlacementVolume[] _volumes=new PlacementVolume[0];SnapTarget[] _volumePlaces=new SnapTarget[0];
 readonly SnapTarget _pourPlace=new SnapTarget{id="pour",label="cup",capacity=int.MaxValue};
 // A held tool's work (Activity.ToolWork: the pan to stir, the next cut) is offered as a place too, so the assist brings the
 // tool there and the activity's AutoTool cuts or stirs (activity probe 2026-11-21: with camera depth the tool stopped short).
 readonly SnapTarget _workPlace=new SnapTarget{id="work",label="work",capacity=int.MaxValue};
 // Place assist on the object itself (2026-11-21): while the assist is set on a place (Medium, High), the held object is
 // moved by its remaining error to it (eased over 0.15 s, at most 50 cm), since a held object goes exactly where it is
 // requested each physics step. Steering it through the hand fell 8-25 cm short (depth lag, how it hangs in the hand).
 Vector3 _placeOffset;SqueezableObject _placeOffsetFor;
 // The held object's remaining error to the intended place (the teapot: its spout's error to the cup).
 Vector3 PlaceError(SnapTarget place){
  if(place==_workPlace)return _workPlace.position-Held.transform.position;
  var teaRun=place==_pourPlace&&_intentRunner!=null&&_intentRunner.Current is TeaActivity ta?ta.tea:null;
  return teaRun!=null?teaRun.AimWant-teaRun.SpoutTipWorld:place.WorldPosition+Vector3.up*(place.id=="volume"?.05f:.02f)-Held.transform.position;}
 /// <summary>The place the movement assist was set on when the object was let go (read by the activity on release).</summary>
 public SnapTarget ReleasedIntentPlace{get;private set;}
 /// <summary>How near (m, horizontal) the object must be for the assist to finish a placement: Low 5, Medium 12, High 20 cm.</summary>
 public static float AssistPlaceRadius(AssistLevel level)=>level==AssistLevel.High?.20f:level==AssistLevel.Medium?.12f:level==AssistLevel.Low?.05f:0f;
 /// <summary>The held object's horizontal distance (m) to the intended place (diagnostics; +inf without one).</summary>
 public float IntentPlaceError{get;private set;}=float.PositiveInfinity;ActivityRunner _intentRunner;bool _intentRunnerLooked;
 /// <summary>The object the assist believes the hand is going for (null if none or not confident).</summary>
 public SqueezableObject IntentItem=>Intent.Engaged&&Held==null&&Intent.TargetId>=0&&Intent.TargetId<IntentAssist.MaxCandidates?_intentItems[Intent.TargetId]:null;
 /// <summary>The place the assist believes the held object is going to (null if none or not confident).</summary>
 public SnapTarget IntentPlace=>Intent.Engaged&&Held!=null&&Intent.TargetId>=IntentAssist.MaxCandidates?_intentPlaces[Intent.TargetId-IntentAssist.MaxCandidates]:null;
 /// <summary>Where the assist steers the hand for the intended place (diagnostics; valid when IntentPlace is set).</summary>
 public Vector3 IntentPlacePoint=>IntentPlace!=null?_intentPlacePoints[Intent.TargetId-IntentAssist.MaxCandidates]:Vector3.zero;
 /// <summary>Grabs and releases the assist helped with (counted apart from the patient's own reach).</summary>
 public int IntentAssistedGrabs{get;private set;}public int IntentAssistedPlacements{get;private set;}
 public void CountAssistedPlacement(){IntentAssistedPlacements++;}
 // Candidate screen point in the hand mapping's units (u, v: -1..1 at the view edges); false when behind or far off screen.
 bool ScreenPoint(Transform view,Vector3 world,float tanH,float tanV,out float u,out float v){var l=view.InverseTransformPoint(world);u=v=0;if(l.z<.08f)return false;u=l.x/(l.z*tanH);v=(l.y/l.z+.05f)/tanV;return Mathf.Abs(u)<1.4f&&Mathf.Abs(v)<1.4f;}
 // What the activity needs now weighs the guess: an item with a place the rules accept now is likely next (3), one with
 // places that are not open yet is unlikely (.3), a tool with no place is neutral (1), one already on its place rare (.25).
 // group: the open place it goes to (items for the same place are equal choices; -1 none).
 static float ItemPrior(Activity act,ActivityItem a,out int group){group=-1;
  if(act.ToolWork(a,out _))return 3f; // the tool the activity needs now (spoon to stir; intent probe: the plate won at 70 % reach)
  bool any=false; // an open place the rules accept now comes first, even for an item already on a place (plate to serve)
  for(int i=0;i<act.targets.Count;i++){var t=act.targets[i];if(t==null||t==a.snappedTo||t.accepts!=null&&!t.accepts(a))continue;any=true;if(!t.Full&&act.Allows(a,t)){group=i;return 3f;}}
  return a.snappedTo!=null?.25f:any?.3f:1f;}
 /// <summary>The assist's best guess of the object wanted, even below the confidence that engages it (null if none).</summary>
 public SqueezableObject IntentBestItem=>Held==null&&Intent.Target>=0&&Intent.TargetId>=0&&Intent.TargetId<IntentAssist.MaxCandidates?_intentItems[Intent.TargetId]:null;
 void FeedIntent(Transform view,float tanH,float tanV){
  // Placing: every listed place accepts the held object, so the nearest is never a wrong choice (bookshelf slots).
  // Placing is judged to a few centimetres, so near a confident place the pull completes from farther (snap radius).
  Intent.Level=AssistSetting;Intent.confidentAt=Held!=null?.3f:.5f;Intent.snapRadius=Held!=null?.5f:.10f;Intent.captureRadius=Held!=null?.75f:.45f;Intent.BeginFrame();if(Intent.Level==AssistLevel.Off)return;
  if(!_intentRunnerLooked){_intentRunner=GetComponent<ActivityRunner>();_intentRunnerLooked=true;}
  var act=_intentRunner!=null&&_intentRunner.Active?_intentRunner.Current:null;var eye=view.position;
  if(Held==null){
   // Objects to pick up: an activity's items (unplaced ones first), else the room's objects within reach.
   int n=0;
   if(act!=null){for(int i=0;i<act.items.Count&&n<IntentAssist.MaxCandidates;i++){var a=act.items[i];if(a==null)continue;var it=a.Item;if(it==null||!it.gameObject.activeInHierarchy||it.IsBroken)continue;
     if(ScreenPoint(view,it.transform.position,tanH,tanV,out float cu,out float cv)){_intentItems[n]=it;float pr=ItemPrior(act,a,out int eq);Intent.AddCandidate(n,cu,cv,pr,eq);n++;}}}
   else{for(int i=0;i<objects.Length&&n<IntentAssist.MaxCandidates;i++){var it=objects[i];if(it==null||!it.gameObject.activeInHierarchy||it.IsBroken||Vector3.Distance(eye,it.transform.position)>1.3f)continue;
     if(ScreenPoint(view,it.transform.position,tanH,tanV,out float cu,out float cv)){_intentItems[n]=it;Intent.AddCandidate(n,cu,cv,1f);n++;}}}
  } else if(act is TeaActivity tea&&tea.tea!=null&&Held.GetComponent<PourVessel>() is PourVessel pot&&pot.teapot&&(tea.tea.CupMl<tea.tea.TargetMl*.9f||tea.tea.CurrentTilt>8f)){ // held over the cup while still tipped (probe L8: the spout drifted 10 cm off at 90 %)
   // Teapot: the place is "spout over the cup". The tea activity knows where the spout should be (AimWant); the hand goes
   // by the spout's remaining error (intent probe 2026-11-21: palm-size depth could not bring the spout over the cup).
   if(float.IsFinite(tea.tea.AimError)&&ActiveHand!=null){var point=ActiveHand.transform.position+(tea.tea.AimWant-tea.tea.SpoutTipWorld);
    if(ScreenPoint(view,point,tanH,tanV,out float pu,out float pv)){_intentPlaces[0]=_pourPlace;_pourPlace.position=tea.tea.AimWant;_intentPlacePoints[0]=point;Intent.AddCandidate(IntentAssist.MaxCandidates,pu,pv,2f);}}
  } else if(act!=null&&ActiveHand!=null&&Held.GetComponent<ActivityItem>() is ActivityItem tool&&act.ToolWork(tool,out var work)){
   var point=ActiveHand.transform.position+(work-Held.transform.position);
   if(ScreenPoint(view,point,tanH,tanV,out float wu,out float wv)){_intentPlaces[0]=_workPlace;_workPlace.position=work;_intentPlacePoints[0]=point;Intent.AddCandidate(IntentAssist.MaxCandidates,wu,wv,2f);}
  } else if(act!=null&&(Held.GetComponent<PourVessel>() is var vessel&&(vessel==null||vessel.teapot||act is TeaActivity drunk&&drunk.tea!=null&&drunk.tea.CupMl<5f))){
   // A teapot done pouring goes back to its mat; the cup gets place help only once it is empty (never while drinking).
   // Places for the held object: the activity's targets that accept it and still have room.
   // The hand goes where the held object (hanging off the hand) lands on the place, a little above it: the placement is
   // judged by where the object comes to rest (activity probe 2026-11-21: the hand on the place left the object 40 cm off).
   var hand=ActiveHand!=null?ActiveHand.transform:null;
   if(_gripFor!=Held){_gripFor=Held;_gripSince=Time.unscaledTime;_gripKnown=false;}
   if(hand!=null&&Time.unscaledTime-_gripSince>=.3f){var nowLocal=hand.InverseTransformVector(hand.position-Held.transform.position);
    _gripLocal=_gripKnown?_gripLocal+(nowLocal-_gripLocal)*(1-Mathf.Exp(-Time.deltaTime/.6f)):nowLocal;_gripKnown=true;}
   if(!_gripKnown)return; // just picked up: no place help until the hold has settled (0.3 s)
   var held=Held.GetComponent<ActivityItem>();int n=0;var grip=hand.TransformVector(_gripLocal);
   if(_volumesFor!=act){_volumesFor=act;_volumes=_intentRunner.Root!=null?_intentRunner.Root.GetComponentsInChildren<PlacementVolume>():new PlacementVolume[0];
    _volumePlaces=new SnapTarget[_volumes.Length];for(int i=0;i<_volumes.Length;i++)_volumePlaces[i]=new SnapTarget{id="volume",label="box",capacity=int.MaxValue};}
   for(int i=0;i<_volumes.Length&&n<IntentAssist.MaxCandidates;i++){var vol=_volumes[i];if(vol==null)continue;var b=vol.interior;
    _volumePlaces[i].position=vol.transform.TransformPoint(b.center+Vector3.up*b.extents.y);var point=_volumePlaces[i].position+grip+Vector3.up*.05f;
    if(ScreenPoint(view,point,tanH,tanV,out float vu,out float vv)){_intentPlaces[n]=_volumePlaces[i];_intentPlacePoints[n]=point;Intent.AddCandidate(IntentAssist.MaxCandidates+n,vu,vv,2f);n++;}}
   if(held!=null)for(int i=0;i<act.targets.Count&&n<IntentAssist.MaxCandidates;i++){var t=act.targets[i];if(t==null||t.Full||t.accepts==null||!t.accepts(held)||!act.Allows(held,t))continue; // only places the rules accept now
    var point=t.WorldPosition+grip+Vector3.up*.02f;
    if(ScreenPoint(view,point,tanH,tanV,out float cu,out float cv)){_intentPlaces[n]=t;_intentPlacePoints[n]=point;Intent.AddCandidate(IntentAssist.MaxCandidates+n,cu,cv,2f);n++;}}
  }
 }
 /// <summary>The object under the camera hand (on the eye-to-hand line or within 10 cm of where it meets a surface); null if none.</summary>
 public SqueezableObject HandRayTarget{get;private set;}
 SqueezableObject _underPending;float _underSince,_underSeen;
 /// <summary>Where the eye-to-hand line meets the first surface (valid when HandRayHit).</summary>
 public Vector3 HandRayPoint{get;private set;}public bool HandRayHit{get;private set;}
 private void UpdateReach(float dt){
  var hand=ActiveHand;var other=LeftSelected?rightHand:leftHand;other.HasOverrideTarget=false;
  if(!CameraMode||hand==null){if(hand!=null)hand.HasOverrideTarget=false;_reach=_reachGoal=float.NaN;HandRayTarget=null;HandRayHit=false;return;}
  var parent=hand.transform.parent;var view=player.view.transform;var eye=view.position;
  // Screen mapping: the wrist's comfortable image range covers the view (image centre a little below the view centre).
  var cam=player.view;float tanV=Mathf.Tan(cam.fieldOfView*.5f*Mathf.Deg2Rad)*.92f,tanH=tanV*cam.aspect;
  var imageDelta=hand.LastImageDelta;
  if(hand.TrackingUsed&&float.IsFinite(imageDelta.y))HandCentreY=Mathf.Clamp(HandCentreY+(.5f-imageDelta.y-HandCentreY)*(1-Mathf.Exp(-dt/20f)),.55f,.85f);float scale=Intent.RangeScale;float u=imageDelta.x/(HandRangeX*scale),v=(imageDelta.y+HandCentreY-.5f)/(HandRangeY*scale);
  // Intent assist: the patient's own hand point picks the intent; a confident, nearby target pulls the hand to it.
  FeedIntent(view,tanH,tanV);Intent.Step(u,v,dt,hand.TrackingUsed&&!player.Paused);
  u=Mathf.Clamp(u+Intent.OffsetU,-1.05f,1.05f);v=Mathf.Clamp(v+Intent.OffsetV,-1.05f,1.05f);
  var dir=view.TransformDirection(new Vector3(u*tanH,v*tanV-.05f,1)).normalized;
  var restLocal=LeftSelected?_leftRest:_rightRest;float rest=Mathf.Clamp(Vector3.Distance(eye,parent!=null?parent.TransformPoint(restLocal):restLocal),.25f,.6f);
  // The camera hand reaches as far as the game lets any hand pick up (settings.reachMeters, 1.6 m): the 1.1 m line and
  // 1.0 m reach left a bookshelf or stove 1.1-1.4 m away out of reach (intent probe 2026-11-21).
  float maxReach=Mathf.Max(.5f,settings.reachMeters-.1f);
  int count=Physics.RaycastNonAlloc(eye,dir,_reachHits,maxReach+.1f,~(1<<8),QueryTriggerInteraction.Ignore);float nearest=float.MaxValue;Collider hit=null;
  for(int i=0;i<count;i++){var c=_reachHits[i].collider;if(c==null||c.transform.IsChildOf(player.transform)||Held!=null&&c.transform.IsChildOf(Held.transform))continue;
   if(_reachHits[i].distance<nearest){nearest=_reachHits[i].distance;hit=c;}}
  HandRayHit=hit!=null;HandRayPoint=HandRayHit?eye+dir*nearest:eye+dir*rest;
  // The object under the hand: the one the line meets, else the nearest within 10 cm of where it meets the table.
  SqueezableObject under=hit!=null?hit.GetComponentInParent<SqueezableObject>():null;
  if(under!=null&&System.Array.IndexOf(objects,under)<0)under=null;
  if(under==null&&HandRayHit){float best=.10f;foreach(var item in objects){if(item==null||item==Held||!item.gameObject.activeInHierarchy)continue;float d=Vector3.Distance(HandRayPoint,item.ClosestSurface(HandRayPoint));if(d<best){best=d;under=item;}}}
  // Hysteresis: tremor flicks the line between neighbours (a tomato and a mushroom); a new object counts as under the
  // hand once it stays there 0.2 s, or at once when nothing was (intent probe 2026-11-21: the mushroom was picked up).
  if(Held!=null){HandRayTarget=null;_underPending=null;}
  else if(under==HandRayTarget){_underPending=null;}
  else if(HandRayTarget==null||under==null&&Time.unscaledTime-_underSeen>.25f){HandRayTarget=under;_underPending=null;}
  else if(under!=null){if(under!=_underPending){_underPending=under;_underSince=Time.unscaledTime;}else if(Time.unscaledTime-_underSince>=.2f){HandRayTarget=under;_underPending=null;}}
  if(under!=null&&under==HandRayTarget)_underSeen=Time.unscaledTime;
  // Depth from the camera: how big the hand looks, steadied and held while the hand travels across the image.
  var depthFrame=LeftSelected?vision.LatestLeft:vision.LatestRight;
  if(hand.TrackingUsed&&depthFrame!=null&&depthFrame.landmarks!=null){float aspect=depthFrame.imageWidth>0&&depthFrame.imageHeight>0?depthFrame.imageWidth/(float)depthFrame.imageHeight:4f/3f;Depth.Step(depthFrame.landmarks,depthFrame.worldLandmarks,aspect,dt);}
  else if(!hand.TrackingUsed)Depth.Reset();
  bool camDepth=DepthFromCamera&&Depth.Valid;float userDepth=camDepth?Depth.Offset:0f;
  if(Held!=_reachHeldFor){_reachHeldFor=Held;_heldReach=float.IsFinite(_reach)?_reach:rest;_depthAtPickup=userDepth;}
  // Holding: the distance stays where the pickup happened (the palm-size push still moves it a little).
  var place=IntentPlace;
  // Holding: toward the intended place the depth follows it (a little above), so front/back needs no reach of its own.
  if(place!=null)_heldReach+=(Mathf.Clamp(Vector3.Distance(eye,_intentPlacePoints[Intent.TargetId-IntentAssist.MaxCandidates]),.2f,maxReach)-_heldReach)*(1-Mathf.Exp(-dt/.12f));
  float want;
  if(Held!=null)want=Mathf.Clamp(_heldReach+(camDepth?userDepth-_depthAtPickup:0f),.2f,maxReach);
  else if(camDepth){
   // Rest distance plus the hand's own depth; a surface under the hand stops it 3 cm short; an object the assist is sure
   // of draws the depth most of the way (small toward/away moves are enough).
   want=Mathf.Clamp(rest+userDepth,.2f,maxReach);
   if(HandRayHit)want=Mathf.Min(want,Mathf.Max(.2f,nearest-.03f));
   if(IntentItem!=null&&HandRayHit)want=Mathf.Lerp(want,Mathf.Clamp(nearest-.03f,.2f,maxReach),.7f);
  }
  else want=HandRayHit?Mathf.Clamp(nearest-.03f,.2f,maxReach):Mathf.Clamp(rest,.2f,maxReach);
  // Nothing under the hand (the line slips past an edge): keep the last depth and drift to rest slowly, not a 20 cm jump.
  if(Held==null&&!HandRayHit&&!camDepth&&float.IsFinite(_reachGoal))want=_reachGoal+(want-_reachGoal)*(1-Mathf.Exp(-dt/.8f));
  // A depth jump (the line crossing a table edge or an object's rim) is followed only once it lasts (0.35 s away from the
  // eye, 0.12 s toward it so the hand never sinks into what is in front): a still hand whose line grazed an edge swung
  // 20 cm (Hand Lab replay 2026-11-19). Small changes follow at once; the reach moves at most 0.8 m/s.
  if(!float.IsFinite(_reachGoal)||Held!=null||Mathf.Abs(want-_reachGoal)<.05f){_reachGoal=want;_reachPendingFor=0;}
  else{if(Mathf.Abs(want-_reachPending)>.05f){_reachPending=want;_reachPendingFor=0;}_reachPendingFor+=dt;if(_reachPendingFor>=(want>_reachGoal?.35f:.12f)){_reachGoal=want;_reachPendingFor=0;}}
  _reach=float.IsFinite(_reach)?_reach+Mathf.Clamp((_reachGoal-_reach)*(1-Mathf.Exp(-dt/.1f)),-.8f*dt,.8f*dt):_reachGoal;
  // Palm size only adds a small push (6 % dead band, at most 6 cm): reaching in a little is still possible.
  float grow=camDepth?0f:hand.LastPalmSize-1f;grow=Mathf.Sign(grow)*Mathf.Max(0,Mathf.Abs(grow)-.06f); // camera depth replaces the old push
  var world=eye+dir*(_reach+Mathf.Clamp(grow*.3f,-.06f,.06f));
  // (Not for the teapot: its place point already carries the spout's error, so the correction would count it twice.)
  if(place!=null&&Held!=null){var error=PlaceError(place);IntentPlaceError=new Vector2(error.x,error.z).magnitude;}else IntentPlaceError=float.PositiveInfinity;
  hand.OverrideTargetLocal=parent!=null?parent.InverseTransformPoint(world):world;hand.HasOverrideTarget=true;
 }
 private Quaternion _heldRotationOffset;private Collider[] _playerColliders;
 private Vector3 _lastHeld;private Vector3 _velocity;private Vector3 _manualRight,_manualLeft;private bool _leftDemoSelected;private float _summonGraceUntil,_lastMotionTime,_cameraValidAt,_cameraClosedFor,_cameraPowerScore,_nextCameraGrabTry;private bool _cameraReleaseBlocked;
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
   // Intent assist: a partial close grabs the intended object, and a partial opening releases at the intended place.
   float closeAt=settings.cameraCloseAt*(Held==null&&IntentItem!=null?Intent.CloseFactor:1f),openAt=settings.cameraOpenAt;
   if(Held!=null&&IntentPlace!=null)openAt+=(settings.cameraCloseAt-settings.cameraOpenAt)*Intent.OpenFactor;
   closeAt=Mathf.Max(closeAt,openAt+.05f);
   if(!cameraGrace)_cameraGrip.Step(CameraGripScore,cameraValid,Time.deltaTime,closeAt,openAt,settings.cameraGestureDwellSeconds);
   _cameraClosedFor=_cameraGrip.Closed?_cameraClosedFor+Time.deltaTime:0;
   if(cameraValid&&!_cameraGrip.Closed&&CameraGripScore<=settings.cameraOpenAt)_cameraReleaseBlocked=false;
   if(!cameraValid&&Held!=null&&!cameraGrace){Release();Status="Camera tracking lost - object released";}
   // Hysteresis: hold until the gate opens (score at or below the open threshold for the dwell), not on the first dip under the grab threshold.
   else if(_cameraGrip.Closed||cameraGrace)_summonGraceUntil=0;
   else if(Held!=null&&Time.unscaledTime>=_summonGraceUntil){Release();Status="Hand opened - object released";}
   _cameraPowerScore=cameraTracked?HandKinematics.GripScore(selectedFrame):cameraGrace?_cameraPowerScore:0;
   // During the loss grace the hold is kept but the unseen hand squeezes nothing: force eases to 0.
   ForceNewtons=cameraValid?Mathf.MoveTowards(ForceNewtons,HandGrasp.CameraForce(_cameraPowerScore,settings.cameraVisualMaxNewtons),90*Time.deltaTime):cameraGrace?Mathf.MoveTowards(ForceNewtons,0,90*Time.deltaTime):0;
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
  if(Held!=null&&Held.IsBroken){Held=null;if(CameraMode)_cameraReleaseBlocked=true;Status="Virtual brittle object fractured — R to reset";}
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
  UpdateReach(Time.deltaTime);
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
  if(CameraMode&&cameraValid&&!_cameraReleaseBlocked&&_cameraGrip.Closed&&_cameraClosedFor>=.15f&&Held==null&&Time.unscaledTime>=_nextCameraGrabTry)TryCameraGrab();
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
  var wantedWorld=HasPresentationTarget?PresentationTarget:player.view.transform.TransformPoint(new Vector3(0,-.03f,distance)+PresentationOffset);
  // Middle of the view, but never down into the table or counter under it (seated or standing).
  if(!HasPresentationTarget&&Physics.Raycast(wantedWorld+Vector3.up*.6f,Vector3.down,out var support,1.2f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore))
   wantedWorld.y=Mathf.Max(wantedWorld.y,support.point.y+bounds.extents.y+.05f);
  var wanted=parent.InverseTransformPoint(wantedWorld);
  // Where the grip would put the object with the hand at rest: the requested pose (not the safety-clamped
  // object, so a blocked placement can never push the hand further away) minus the hand's actual displacement
  // from rest (exact even while the smoothed hand is still moving).
  var rest=(LeftSelected?_leftRest+Vector3.back*settings.leftWristSetbackMeters:_rightRest)+Vector3.up*handLift;
  var unpresented=parent.InverseTransformPoint(_requestedGrip+(bounds.center-Held.transform.position))-(ActiveHand.transform.localPosition-rest);
  var target=Vector3.ClampMagnitude(wanted-unpresented,PresentationReach);
  // A small dead band: the hand settles instead of hunting by a millimetre or two.
  if(PresentationAccel>0){
   // Eased carry: speed rises and falls at most PresentationAccel (m/s²), stopping on the target (no jerk at either end).
   ref Vector3 vel=ref (LeftSelected?ref _presentVelLeft:ref _presentVelRight);var gap=target-offset;float dist=gap.magnitude;
   var desired=dist<.001f?Vector3.zero:gap/dist*Mathf.Min(PresentationSpeed,Mathf.Sqrt(2*PresentationAccel*dist));
   vel=Vector3.MoveTowards(vel,desired,PresentationAccel*Time.deltaTime);offset+=vel*Time.deltaTime;
  } else if((target-offset).sqrMagnitude>.003f*.003f)offset=Vector3.MoveTowards(offset,target,PresentationSpeed*Time.deltaTime);
 }
 // Follow the only actively observed physical hand. When both are visible,
 // retain the selected side until its stream has stopped refreshing.
 private void AutoSelectCameraHand(){
  if(!CameraMode||vision==null||Held!=null)return; // never swap hands (which releases) while holding
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
  // Ended elsewhere (placed by an activity, deactivated): a still-closed camera hand must open before it grabs again (live
  // Hand Lab 2026-11-19: the cup was placed and the closed hand at once picked up the tray under it).
  if(!Held.gameObject.activeInHierarchy||!Held.IsHeld){Held=null;if(CameraMode)_cameraReleaseBlocked=true;_velocity=Vector3.zero;_task.Reset();return;}

  if(_placeOffsetFor!=Held){_placeOffsetFor=Held;_placeOffset=Vector3.zero;} // a new hold starts with no offset (no jump)
  var placing=IntentPlace;
  bool assisting=placing!=null&&Intent.Level>=AssistLevel.Medium&&(placing!=_pourPlace||_intentRunner!=null&&_intentRunner.Current is TeaActivity pr&&pr.tea!=null&&float.IsFinite(pr.tea.AimError));
  bool direct=false;
  if(!TrySafeGripTarget(Held,out var requested)){
   // The hand's own target is unsafe (inside the stove at full reach): while placing with the assist, the object moves
   // from where it is toward the place instead of stopping (intent probe 2026-11-21: the pan froze 26 cm off the burner).
   if(!assisting){_velocity=Vector3.zero;return;}
   direct=true;_placeOffset=Vector3.zero;requested=Held.transform.position+Vector3.ClampMagnitude(PlaceError(placing)*(1-Mathf.Exp(-Time.fixedDeltaTime/.15f)),.6f*Time.fixedDeltaTime);
  }
  // Stage 8: a fast hand cannot make the held object jump, snap or tunnel.
  else if(GraspContactSolver.Enabled)requested=ContactGeometry.Follow(_lastHeld,requested,HeldFollowSpeed,Time.fixedDeltaTime);
  _velocity=GraspMotionPolicy.ReleaseVelocity(_lastHeld,requested,Time.fixedDeltaTime,settings.releaseVelocityGain,settings.maximumReleaseSpeed,settings.maximumReleaseStep);
  _lastHeld=requested;_lastMotionTime=Time.unscaledTime;
  if(direct){} // already moved toward the place above
  else if(assisting){
   _placeOffset=Vector3.ClampMagnitude(_placeOffset+PlaceError(placing)*(1-Mathf.Exp(-Time.fixedDeltaTime/.15f)),.5f);requested+=_placeOffset;
  } else if(_placeOffset!=Vector3.zero){_placeOffset=Vector3.MoveTowards(_placeOffset,Vector3.zero,.6f*Time.fixedDeltaTime);requested+=_placeOffset;}
  var heldRotation=_hasGripRotation?_gripRotation:GraspMotionPolicy.HeldRotation(ActiveHand.transform.rotation,_heldRotationOffset);
  if(HeldOriented){var pivot=requested+heldRotation*HeldTiltPivotLocal;if(HeldPivotBlend>0)pivot=Vector3.Lerp(pivot,HeldPivotWorld,HeldPivotBlend);heldRotation=HeldOrientation;requested=pivot-heldRotation*HeldTiltPivotLocal;}
  Held.MoveHeld(requested,heldRotation);
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
 // The assist's intent needs only distance: its candidates are already on screen, and a tomato half behind the knife
 // failed the eye's visibility test, so the knife under the hand was picked up instead (intent probe 2026-11-21).
 bool InReach(SqueezableObject item)=>item!=null&&item.gameObject.activeInHierarchy&&!item.IsBroken&&item.HasPickupCollider&&(item.ClosestSurface(player.view.transform.position)-player.view.transform.position).magnitude<=settings.reachMeters;
 private bool CanReach(SqueezableObject item){
  if(item==null||!item.gameObject.activeInHierarchy||item.IsBroken||!item.HasPickupCollider)return false;
  var eye=player.view.transform.position;var point=item.ClosestSurface(eye);
  if((point-eye).magnitude>settings.reachMeters)return false;
  // 2026-11-02: visible if the closest point or the top of the object can be seen. The closest point alone was hidden
  // behind a cutting board's edge while the bread on it was plainly visible (camera activity probe).
  return Visible(item,eye,point)||Visible(item,eye,item.ClosestSurface(item.transform.position+Vector3.up));
 }
 bool Visible(SqueezableObject item,Vector3 eye,Vector3 point){
  var direction=point-eye;
  // Stage 10: something the object rests on or in (a pan, a plate) touching its surface does not block it.
  return direction.sqrMagnitude<.000001f||!Physics.Raycast(eye,direction.normalized,out var hit,direction.magnitude+.01f,~(1<<8),QueryTriggerInteraction.Ignore)||hit.collider.GetComponentInParent<SqueezableObject>()==item||hit.distance>=direction.magnitude-.03f;
 }
 // Check endpoint overlap as well as the camera-to-hand sweep.
 private bool TrySafeGripTarget(SqueezableObject item,out Vector3 target){
  var origin=player.view.transform.position;target=GripTarget(item);
  // Placing with the movement assist: sweep from where the object is now (it still cannot tunnel), not from the eye,
  // whose line to a place on a table grazes the table or the plate and held the object about 10 cm short (2026-11-21).
  if(item==Held&&IntentPlace!=null&&GraspMotionPolicy.Finite(_lastHeld)&&_lastHeld!=Vector3.zero)origin=_lastHeld;
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
  door=null;var ray=player.view.ViewportPointToRay(new Vector3(.5f,.5f,0));SqueezableObject direct=null;
  if(Physics.Raycast(ray,out var hit,settings.reachMeters,~(1<<8),QueryTriggerInteraction.Ignore)){
   direct=hit.collider.GetComponentInParent<SqueezableObject>();
   if(!CanReach(direct)){direct=null;door=hit.collider.GetComponentInParent<ApartmentDoor>();if(door!=null)return null;}
  }
  // 2026-11-02: among reachable objects inside the aim cone, the one whose centre is nearest the view direction wins, so a
  // knife lying on a plate or bread just behind a tomato is picked when you look at it, not the first collider the ray meets.
  // Centres are rigidbody centres of mass, so an off-centre pivot (a pan's handle) does not lose the object being looked at.
  SqueezableObject nearest=null;float best=float.PositiveInfinity,bestCentre=float.PositiveInfinity;
  foreach(var item in objects){
   if(!CanReach(item))continue;
   float along=Vector3.Dot(item.transform.position-ray.origin,ray.direction);
   var point=item.ClosestSurface(ray.GetPoint(Mathf.Clamp(along,0,settings.reachMeters)));
   float score=GraspMotionPolicy.AimScore(ray.origin,ray.direction,point,settings.reachMeters,settings.pickupAssistDegrees);
   if(float.IsInfinity(score)&&item!=direct)continue;
   float centre=Vector3.Angle(ray.direction,(item.Body!=null?item.Body.worldCenterOfMass:item.transform.position)-ray.origin); // the body's centre, not the pivot
   if(centre<bestCentre||centre==bestCentre&&score<best){nearest=item;best=score;bestCentre=centre;}
  }
  return nearest!=null?nearest:direct;
 }
 // Closing fingers near a visible object grabs it without E. A distant fist cannot pick it up.
 private void TryCameraGrab(){
  if(contactRig==null||contactRig.contactBones==null)return;
  // Aim assist (default, a setting): the object you look at and close your hand on is the intent. A fingertip brushing a
  // neighbour in a cluster (books, table settings) must not win over it (2026-11-02, camera activity probe: 9 of 12 runs
  // grabbed the neighbour). Off: only a fingertip at an object grabs (physical reach), as before.
  // Camera hand: the object under the hand is the intent (2026-11-19: a real-hand session reaching for the plate on the
  // left picked up the cup in the middle of the view). The looked-at object helps only when the hand is within 12 cm of it.
  var looked=HandLocomotion.AimAssist?KeyboardTarget(out _):null;
  // Live Hand Lab 2026-11-19: with nothing under the hand, the looked-at tray was picked up from across the table; the
  // looked-at object now needs the virtual hand itself within 12 cm.
  var palmAt=ActiveHand!=null?ActiveHand.transform.position:HandRayPoint;
  var intent=IntentItem!=null&&InReach(IntentItem)?IntentItem:null;
  // A confident intent (from the whole reach) wins over whatever the hand stopped above; otherwise the object under the hand.
  // An object already set on its place (a plate on its mat) is rarely the next one wanted: a weaker intent wins over it.
  var underPlaced=HandRayTarget!=null&&HandRayTarget.GetComponent<ActivityItem>() is ActivityItem placedItem&&placedItem.snappedTo!=null;
  var guess=IntentBestItem!=null&&IntentBestItem!=HandRayTarget&&InReach(IntentBestItem)&&Intent.TargetDistance<=Intent.captureRadius?IntentBestItem:null;
  if(intent==null&&underPlaced&&guess!=null&&Intent.Confidence>=.35f)intent=guess;
  var aimed=intent!=null&&(Intent.Confidence>=.6f||underPlaced&&Intent.Confidence>=.35f)?intent:HandRayTarget!=null?HandRayTarget:intent!=null?intent:looked!=null&&Vector3.Distance(palmAt,looked.ClosestSurface(palmAt))<.12f?looked:null;
  bool intentHelped=aimed!=null&&aimed==intent&&(Intent.Pulling||Intent.CloseFactor<1f||HandRayTarget==null);
  SqueezableObject nearest=null;float best=settings.cameraGrabRadiusMeters;bool aimedTouched=false;
  foreach(var item in objects){
   if(!CanReach(item))continue;
   foreach(var bone in contactRig.contactBones){
    if(bone==null)continue;
    var tip=bone.childCount>0?bone.GetChild(0).position:bone.position;
    float distance=Vector3.Distance(tip,item.ClosestSurface(tip));
    if(distance<best){best=distance;nearest=item;}
    if(item==aimed&&distance<settings.cameraGrabRadiusMeters)aimedTouched=true;
   }
  }
  if(aimed!=null&&TryGrab(aimed,aimed==intent||aimed==HandRayTarget)){
   // Counted separately when no fingertip reached it, so assisted pickups are never mistaken for physical reach.
   if(intentHelped)IntentAssistedGrabs++;
   if(!aimedTouched){AssistedGrabs++;Status="Camera grip (aim assist): holding "+aimed.displayName;}else Status="Camera grip: holding "+aimed.displayName;
   return;
  }
  if(nearest!=null&&TryGrab(nearest)){Status="Camera grip: holding "+nearest.displayName;return;}
  // Nothing grabbed: a closed empty hand retries at 10 Hz, not every frame (the look-target scan visits every object).
  _nextCameraGrabTry=Time.unscaledTime+.1f;
 }
 /// <summary>Camera pickups made by aim assist rather than a fingertip at the object (2026-10-29).</summary>
 public int AssistedGrabs{get;private set;}
 /// <summary>The camera closed-hand gate with hysteresis (what grabbing and holding use), for the HUD state.</summary>
 public bool CameraGateClosed=>_cameraGrip.Closed;
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
 public bool TryGrab(SqueezableObject item)=>TryGrab(item,false);
 /// <summary>onScreen: the object is the assist's choice or under the camera hand, so distance alone decides reach (the
 /// eye's visibility test refused a half-hidden tomato and the fingertip fallback took its neighbour).</summary>
 public bool TryGrab(SqueezableObject item,bool onScreen){
  if(item==null||Held!=null||item.IsBroken||player.Paused||Hardware&&!forceReceiver.Latest.Valid||CameraMode&&!ForceValid)return false;
  if(!(onScreen?InReach(item):CanReach(item))||!BeginGrab(item,false))return false;
  var grip=HeldGrip;
  Status="Holding "+item.displayName+(grip!=null?"  -  "+grip.number+". "+grip.name+" ("+grip.fingers+")":"")+
   (item.Material!=null?"  -  "+item.Material.name+", "+ApartmentChoices.ClassName(item.Material.materialClass)+", "+item.Material.result.ToString().ToLowerInvariant():"");return true;
 }
 public void Release(){
  if(Held!=null){
   var velocity=!player.Paused&&Time.unscaledTime-_lastMotionTime<=.1f?_velocity:Vector3.zero;
   // An assisted placement sets the object down on its place instead of throwing it with the hand's speed.
   ReleasedIntentPlace=CameraMode?IntentPlace:null;
   if(ReleasedIntentPlace!=null){velocity=Vector3.zero;}
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
   string cameraState=cameraLauncher==null?"NOT CONFIGURED":cameraLauncher.State+(cameraLauncher.LastError.Length>0&&(cameraLauncher.State=="FAILED"||cameraLauncher.State=="camera_error")?" - "+cameraLauncher.LastError.TrimEnd('.')+". Keyboard and mouse still work":"");
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
