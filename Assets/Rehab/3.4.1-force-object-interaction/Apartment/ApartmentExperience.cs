using System;
using UnityEngine;
using UnityEngine.UI;
namespace GripRehab {
public sealed class ApartmentExperience:MonoBehaviour {
 public ApartmentPlayer player;public ApartmentSettings settings;public LiveForceReceiver forceReceiver;public VisionUdpReceiver vision;
 public Transform holdAnchor;public FingerPoseDriver rightHand,leftHand;
 public Text statusText,contextText,forceText;public Image progressFill;
 public SqueezableObject[] objects;public SeatPoint[] seats;
 public SqueezableObject Held{get;private set;}
 public bool Hardware{get;private set;}
 public float ForceNewtons{get;private set;}
 public bool ForceValid{get;private set;}
 public float DemoSetpoint{get;private set;}
 public float ProgressSeconds => _task.ProgressSeconds;
 public float TestForce=-1; // Used only when an explicit acceptance-test command-line flag is present.
 public string Status{get;private set;}="Ready";
 private Vector3 _lastHeld;private Vector3 _velocity;
 private int _generation,_lastSamples;private long _lastMs;
 private ForceHoldTask _task=new ForceHoldTask(18,3,3);
 private bool _testing;private float _nextUi;
 #if UNITY_EDITOR
 public void EnableAcceptanceInput(){_testing=true;}
#endif
 private void Start(){Hardware=settings.startWithHardware;DemoSetpoint=settings.demoForceNewtons;_testing=Array.Exists(Environment.GetCommandLineArgs(),x=>x.StartsWith("-apartment-"));}
 public void SetHardware(bool hardware){Release();Hardware=hardware;ForceNewtons=0;_task.Reset();_generation=forceReceiver.State.Generation;Status=hardware?"Waiting for calibrated Phase 2 input":"Keyboard & mouse demo";}
 private void Update(){
  if(Input.GetKeyDown(KeyCode.B))SetHardware(!Hardware);
  if(!player.Paused){
   if(!Hardware)DemoSetpoint=Mathf.Clamp(DemoSetpoint+Input.mouseScrollDelta.y*3+(Input.GetKey(KeyCode.Equals)?25*Time.deltaTime:Input.GetKey(KeyCode.Minus)?-25*Time.deltaTime:0),1,80);
   if(Input.GetKeyDown(KeyCode.Q))Release();
   if(Input.GetKeyDown(KeyCode.R))ResetObjects();
   if(Input.GetKeyDown(KeyCode.F)){
    if(player.IsSeated){if(!player.Stand())Status="Stand-up area blocked";}
    else {SeatPoint nearest=null;float distance=2.2f;foreach(var s in seats){float d=Vector3.Distance(player.transform.position,s.seatedFeet.position);if(d<distance){nearest=s;distance=d;}}if(nearest!=null){Release();player.Sit(nearest);}else Status="Move closer to a marked chair";}
   }
   if(Input.GetKeyDown(KeyCode.E)){
    if(Held!=null)Release();
    else if(Physics.Raycast(player.view.ViewportPointToRay(new Vector3(.5f,.5f,0)),out var hit,settings.reachMeters,~(1<<8),QueryTriggerInteraction.Ignore)){
     var item=hit.collider.GetComponentInParent<SqueezableObject>();if(item!=null)TryGrab(item);
     else {var door=hit.collider.GetComponentInParent<ApartmentDoor>();if(door!=null)door.Toggle();}
    }
   }
  }
  if(Hardware){
   ForceValid=forceReceiver.Latest.Valid&&!player.Paused;ForceNewtons=ForceValid?forceReceiver.Latest.Newtons:0;
   if(_generation!=forceReceiver.State.Generation){_generation=forceReceiver.State.Generation;Release();_task.Reset();Status="Connection/calibration changed";}
   if(!ForceValid&&Held!=null){Release();Status="Input stale: object released";}
  } else {
   ForceValid=!player.Paused;float requested=ForceValid&&(Input.GetMouseButton(0)||Input.GetKey(KeyCode.Space))?DemoSetpoint:0;
   if(_testing&&TestForce>=0)requested=TestForce;
   ForceNewtons=Mathf.MoveTowards(ForceNewtons,requested,90*Time.deltaTime);
  }
  foreach(var item in objects)if(item!=null)item.ApplyForce(item==Held&&ForceValid?ForceNewtons:0,Time.deltaTime);
  if(Held!=null&&Held.IsBroken){Held=null;Status="Virtual brittle object fractured — R to reset";}
  if(Held!=null&&ForceValid){
   if(!Hardware)_task.Step(new ForceReading(ForceNewtons,Time.realtimeSinceStartupAsDouble,true,true),Time.deltaTime);
   else if(_lastSamples!=forceReceiver.State.SampleCount){_task.Step(forceReceiver.Latest,Mathf.Min(.1f,(uint)((uint)forceReceiver.State.SampleMs-(uint)_lastMs)/1000f));_lastSamples=forceReceiver.State.SampleCount;_lastMs=forceReceiver.State.SampleMs;}
  }
  var tracked=vision!=null&&vision.TrackingValid;
  float closure=Held!=null?Mathf.Lerp(.45f,.95f,Mathf.Clamp01(ForceNewtons/45)):Mathf.Clamp01(ForceNewtons/55);
  rightHand.Pose(vision.Latest,tracked,closure,Time.deltaTime,settings.trackingSmoothing);
  leftHand.Pose(null,false,Held!=null?.16f:0,Time.deltaTime,settings.trackingSmoothing);
  if(Time.unscaledTime>=_nextUi){_nextUi=Time.unscaledTime+.08f;RefreshHUD();}
 }
 private void FixedUpdate(){
  if(Held==null)return;
  var origin=player.view.transform.position;
  var requested=holdAnchor.position;var direction=requested-origin;
  if(Physics.SphereCast(origin,Held.radius,direction.normalized,out var hit,direction.magnitude,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore))requested=origin+direction.normalized*Mathf.Max(.15f,hit.distance-.015f);
  _velocity=(requested-_lastHeld)/Mathf.Max(Time.fixedDeltaTime,.001f);_lastHeld=requested;
  Held.MoveHeld(requested,holdAnchor.rotation);
 }
 public bool TryGrab(SqueezableObject item){
  if(item==null||Held!=null||item.IsBroken||player.Paused||Hardware&&!forceReceiver.Latest.Valid)return false;
  var eye=player.view.transform.position;var direction=item.transform.position-eye;
  if(direction.magnitude>settings.reachMeters)return false;
  if(Physics.Raycast(eye,direction.normalized,out var hit,direction.magnitude+.03f,~(1<<8),QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<SqueezableObject>()!=item)return false;
  if(!item.Grab())return false;Held=item;_lastHeld=holdAnchor.position;_velocity=Vector3.zero;_task.Reset();_lastSamples=forceReceiver.State.SampleCount;_lastMs=forceReceiver.State.SampleMs;Status="Holding "+item.displayName;return true;
 }
 public void Release(){if(Held!=null){Held.Release(_velocity*.15f);Held=null;}_task.Reset();}
 public void ResetObjects(){Release();foreach(var item in objects)item.ResetObject();Status="Objects restored to their stations";}
 private void RefreshHUD(){
  string source=Hardware?(forceReceiver.State.IsSynthetic?"SYNTHETIC REPLAY":"PHASE 2 / BLE"):"KEYBOARD + MOUSE DEMO";
  statusText.text="GRIP / REHAB     •     APARTMENT LAB\n"+source+"   |   "+(player.IsSeated?"SEATED":"WALKING")+"\nHAND: "+(vision.TrackingValid?"CAMERA LANDMARKS — UNVALIDATED":"ANIMATED DEMO — NO TRACKING");
  forceText.text=(ForceValid?ForceNewtons.ToString("F1"):"--")+" N\n"+(Hardware?forceReceiver.State.Status:"Demo setpoint "+DemoSetpoint.ToString("F0")+" N")+"\n"+(Held!=null?Held.displayName+"  ·  "+(Held.Compression*100).ToString("F0")+"% compression":"Aim at an object and press E")+"\nHOLD  "+_task.ProgressSeconds.ToString("F1")+" / 3 s  ·  target 18 ± 3 N";
  progressFill.rectTransform.localScale=new Vector3(Mathf.Clamp01(_task.ProgressSeconds/3),1,1);
  string prompt=Status;
  if(Held==null&&Physics.Raycast(player.view.ViewportPointToRay(new Vector3(.5f,.5f,0)),out var hit,settings.reachMeters,~(1<<8),QueryTriggerInteraction.Ignore)){
   var item=hit.collider.GetComponentInParent<SqueezableObject>();if(item!=null)prompt="E  •  Pick up "+item.displayName;
   else if(hit.collider.GetComponentInParent<ApartmentDoor>()!=null)prompt="E  •  Open / close door";
  }
  contextText.text=(player.Paused?"PAUSED — ESC to resume":prompt)+"\nWASD move  •  Mouse look  •  E grab  •  Q release  •  F sit / stand\nHold SPACE / left mouse to squeeze  •  Wheel / +/- force  •  R reset  •  B input mode  •  TAB options  •  ESC pause";
 }
}
}
