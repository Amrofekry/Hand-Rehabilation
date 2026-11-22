using System;
using UnityEngine;
using UnityEngine.UI;
namespace GripRehab {
/// <summary>
/// Camera-hand mode: the view turns when the palm reaches the edge band of the camera image and the player walks while
/// pointing (<see cref="HandLocomotionPolicy"/>). Runs after ApartmentPlayer, so mouse look and WASD still add on top; they
/// are options, not required. Thin bars at the screen edges show which way the view is turning. Off in keyboard and
/// sensor modes; a setting turns it off for people who prefer the mouse.
/// </summary>
[DefaultExecutionOrder(50)]
public sealed class HandLocomotion : MonoBehaviour {
 const string Key="GripRehab.HandLocomotion";
 public static bool Enabled{get=>PlayerPrefs.GetInt(Key,1)==1;set=>PlayerPrefs.SetInt(Key,value?1:0);}
 /// <summary>True while CALIBRATE HAND runs: the view does not turn and hand actions wait (2026-11-22).</summary>
 public static bool Suspended;
 const string AimKey="GripRehab.CameraAimAssist";
 /// <summary>On (default): a closed hand also picks up what you look at within reach, counted as assisted. Off: only a fingertip at the object grabs (physical reach).</summary>
 public static bool AimAssist{get=>PlayerPrefs.GetInt(AimKey,1)==1;set=>PlayerPrefs.SetInt(AimKey,value?1:0);}
 /// <summary>Camera hand sensitivity (reach) and steadiness (filter), 0..2 each; see HandControlProfile.</summary>
 public static int Sensitivity{get=>Mathf.Clamp(PlayerPrefs.GetInt("GripRehab.HandSensitivity",1),0,2);set{PlayerPrefs.SetInt("GripRehab.HandSensitivity",Mathf.Clamp(value,0,2));profileVersion++;}}
 public static int Steadiness{get=>Mathf.Clamp(PlayerPrefs.GetInt("GripRehab.HandSteadiness",1),0,2);set{PlayerPrefs.SetInt("GripRehab.HandSteadiness",Mathf.Clamp(value,0,2));profileVersion++;}}
 static int profileVersion;int appliedVersion=-1;float nextProfileCheck; // the setters bump it, so prefs are read only when a setting changes
 public readonly HandInteractionMachine Interaction=new HandInteractionMachine();
 HandInteraction lastState=HandInteraction.Idle;bool stateShown;int lastSens=-1,lastSteady=-1;
 public const float MaxPitch=60;
 ApartmentExperience app;RehabAppShell shell;bool acceptance;
 public readonly HandLocomotionPolicy Policy=new HandLocomotionPolicy();
 public readonly HandActionPolicy Actions=new HandActionPolicy();
 ActivityRunner runner;float twistBase;bool twistStarted,pinchGrabbed;
 public int OptionsOpened{get;private set;}
 public static HandLocomotion Instance{get;private set;}
 public float WalkedMeters{get;private set;}
 public float TurnedDegrees{get;private set;}
 public bool Active{get;private set;}
 Image left,right,up,down;CanvasGroup overlay;Text controls;string controlsText;bool handHint;
 public const string HandHint="EDGE LOOK  POINT WALK  FIST GRAB  PINCH MENU";

 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-no-hand-locomotion")>=0)return; // diagnostics: run without it
  var e=FindAnyObjectByType<ApartmentExperience>();if(e!=null&&e.GetComponent<HandLocomotion>()==null)e.gameObject.AddComponent<HandLocomotion>();}
 void Awake(){Instance=this;app=GetComponent<ApartmentExperience>();acceptance=Array.Exists(Environment.GetCommandLineArgs(),a=>a.StartsWith("-apartment-"));BuildOverlay();}
 void Start(){
  shell=FindAnyObjectByType<RehabAppShell>();
  // The QUICK CONTROLS card is authored in the scene; in camera mode its first line says how to look and walk by hand.
  foreach(var t in FindObjectsByType<Text>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
   if(t.text.Trim()!="QUICK CONTROLS"||t.transform.parent==null)continue;
   foreach(var b in t.transform.parent.GetComponentsInChildren<Text>(true))if(b!=t&&b.text.IndexOf('\n')>=0){controls=b;controlsText=b.text;break;}
   break;
  }
 }
 void Hint(bool hand){
  if(controls==null||hand==handHint)return;handHint=hand;
  int cut=controlsText.IndexOf('\n');controls.text=hand&&cut>0?HandHint+controlsText.Substring(cut):controlsText;
 }

 void Update(){
  var player=app!=null?app.player:null;if(player==null||app.vision==null||Suspended){Show(0,0);return;}
  var frame=app.LeftSelected?app.vision.LatestLeft:app.vision.LatestRight;
  bool tracked=Enabled&&app.CameraMode&&(app.LeftSelected?app.vision.LeftValid:app.vision.RightValid)&&frame!=null&&
   (app.cameraLauncher==null||app.cameraLauncher.CameraOpened||acceptance)&&HandKinematics.HasPoseGeometry(frame);
  bool usable=tracked&&!player.Paused&&(shell==null||!shell.BlocksGameplay)&&player.GameplayInputAllowed;
  float px=0,py=0,roll=float.NaN;var gesture=HandGesture.None;
  if(tracked){var p=frame.landmarks;px=(p[0].x+p[5].x+p[9].x+p[13].x+p[17].x)/5;py=(p[0].y+p[5].y+p[9].y+p[13].y+p[17].y)/5;gesture=HandGrasp.Gesture(frame);roll=HandActionPolicy.Roll(p[0].x,p[0].y,p[9].x,p[9].y);}
  bool pointing=gesture==HandGesture.Point;
  Hint(Enabled&&app.CameraMode);
  ApplyProfile();
  // Interaction state on the input strip (camera mode only): TRACKING / READY / HOLDING / LOST.
  if(app.CameraMode){
   var st=Interaction.Step(tracked&&!player.Paused,app.CameraGateClosed,app.Held!=null); // the real hysteresis gate, as the grab path uses
   if(st!=lastState||!stateShown){lastState=st;stateShown=true;ApartmentGuide.Set(ApartmentGuide.Channel.Input,st!=HandInteraction.Idle,StateText(st));}
  } else if(stateShown){stateShown=false;lastState=HandInteraction.Idle;ApartmentGuide.Clear(ApartmentGuide.Channel.Input);}
  float dt=Time.deltaTime;
  // The resting hand sits low in a laptop webcam image (ApartmentExperience.HandCentreY, a wrist row; the palm centre is
  // about 0.08 above the wrist): edge look is measured from the resting palm, so up and down are both within reach.
  Policy.Step(px,py-(ApartmentExperience.HandCentreY-.58f),pointing,usable,tracked?frame.confidence:0,dt);Active=usable;
  // The options pinch is an OK sign: thumb on the index tip with at least two of the other fingers out. A closed fist often
  // has the thumb on the index tip too (Hand Lab 2026-11-19: a missed grab opened the options), and it never opens them.
  bool okPinch=false;if(gesture==HandGesture.Pinch){var lm=frame.landmarks;int outN=0;for(int k=1;k<4;k++){int pip=6+k*4,tip=8+k*4;if(((Vector2)(lm[tip]-lm[0])).sqrMagnitude>((Vector2)(lm[pip]-lm[0])).sqrMagnitude*1.21f)outN++;}okPinch=outN>=2;}
  Act(gesture,okPinch,roll,usable,dt);
  if(Policy.YawRate!=0){player.transform.Rotate(0,Policy.YawRate*dt,0);TurnedDegrees+=Mathf.Abs(Policy.YawRate*dt);}
  // Holding the cup, a raised hand is the drink gesture (TeaPouring), not "look up": only looking up is held back.
  var vessel=app.Held!=null?app.Held.GetComponent<PourVessel>():null;bool drinkPose=vessel!=null&&!vessel.teapot;
  if(Policy.PitchRate!=0&&!(drinkPose&&Policy.PitchRate<0)){
   var view=player.view.transform;float pitch=view.localEulerAngles.x;if(pitch>180)pitch-=360;
   pitch=Mathf.Clamp(pitch+Policy.PitchRate*dt,-MaxPitch,MaxPitch);
   player.LookAt(view.position+Quaternion.Euler(pitch,player.transform.eulerAngles.y,0)*Vector3.forward);
  }
  if(Policy.Walking&&!player.IsSeated&&player.Controller!=null&&player.Controller.enabled){
   var before=player.transform.position;
   player.Controller.Move(player.transform.forward*player.settings.walkSpeed*.8f*Policy.Walk*dt);
   var moved=player.transform.position-before;moved.y=0;WalkedMeters+=moved.magnitude;
  }
  Show(Policy.YawRate/Policy.maxYawDegPerSec,Policy.PitchRate/Policy.maxPitchDegPerSec);
 }

 /// <summary>Camera actions that replace keys: fist grabs (G/E), pinch opens options (TAB), a twist turns the placement (, .).</summary>
 void Act(HandGesture gesture,bool okPinch,float roll,bool usable,float dt){
  bool holding=app.Held!=null;
  Actions.Step(gesture==HandGesture.Fist,okPinch,holding,roll,usable,dt);
  // Grabbing is the single camera path in ApartmentExperience (closed-hand gate + reach or aim assist), so it is not repeated here.
  // A pinch that picked something up is a grab, never the options gesture (both use the pinch).
  pinchGrabbed=gesture==HandGesture.Pinch&&(pinchGrabbed||app.Held!=null);
  // A grab that catches nothing often looks like a pinch: the options pinch counts only with no object under the hand
  // (2026-11-19, Hand Lab: a missed grab opened the options and paused the game, so the hand seemed dead). The grip gate is
  // not a guard: a pinch closes it too.
  if(Actions.OptionsRequested&&!app.player.Paused&&app.Held==null&&!pinchGrabbed&&app.HandRayTarget==null){app.player.SetPaused(true);OptionsOpened++;}
  if(runner==null)runner=app.GetComponent<ActivityRunner>();
  // A teapot or cup turns with the wrist to pour and drink (TeaPouring reads the same roll), never to the placement yaw.
  bool turning=holding&&runner!=null&&runner.Active&&!float.IsNaN(Actions.PlacementYaw)&&app.Held.GetComponent<ActivityItem>()!=null&&app.Held.GetComponent<PourVessel>()==null;
  if(!turning){twistStarted=false;return;}
  if(!twistStarted){twistStarted=true;twistBase=runner.PlacementYaw;}
  runner.SetPlacementYaw(twistBase+Actions.PlacementYaw);
 }

 static string StateText(HandInteraction s)=>s==HandInteraction.Tracking?"CAMERA  TRACKING":s==HandInteraction.ReadyToGrab?"CAMERA  READY":s==HandInteraction.Holding?"CAMERA  HOLDING":s==HandInteraction.Releasing?"CAMERA  RELEASING":s==HandInteraction.TrackingLost?"CAMERA  LOST":"CAMERA  IDLE";
 // Settings change rarely: apply only when they do (no per-frame work or allocation).
 void ApplyProfile(){
  // Re-read at most once a second as well, so a reset that writes the prefs directly is picked up too.
  if(appliedVersion==profileVersion&&Time.unscaledTime<nextProfileCheck)return;appliedVersion=profileVersion;nextProfileCheck=Time.unscaledTime+1;
  int sens=Sensitivity,steady=Steadiness;if(sens==lastSens&&steady==lastSteady)return;lastSens=sens;lastSteady=steady;
  Apply(app.rightHand,sens,steady);Apply(app.leftHand,sens,steady);
 }
 static void Apply(FingerPoseDriver h,int sens,int steady){
  if(h==null)return;
  h.reachX=HandControlProfile.Reach(sens);h.reachY=HandControlProfile.Reach(sens)*.87f;
  h.positionMinCutoff=HandControlProfile.PositionCutoff(steady);h.depthMinCutoff=HandControlProfile.DepthCutoff(steady);
 }

 void Show(float yaw,float pitch){
  if(overlay==null)return;
  bool on=app!=null&&app.CameraMode&&Enabled;overlay.alpha=on?1:0;
  SetAlpha(right,Mathf.Max(0,yaw));SetAlpha(left,Mathf.Max(0,-yaw));SetAlpha(down,Mathf.Max(0,pitch));SetAlpha(up,Mathf.Max(0,-pitch));
 }
 static void SetAlpha(Image i,float a){var c=i.color;c.a=.55f*Mathf.Clamp01(a);i.color=c;}

 void BuildOverlay(){
  var go=new GameObject("Hand look indicators",typeof(Canvas),typeof(CanvasGroup));go.transform.SetParent(transform,false);
  var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=850;
  overlay=go.GetComponent<CanvasGroup>();overlay.alpha=0;overlay.blocksRaycasts=false;overlay.interactable=false;
  left=Bar(go,"Turning left",new Vector2(0,0),new Vector2(0,1),new Vector2(16,0));right=Bar(go,"Turning right",new Vector2(1,0),new Vector2(1,1),new Vector2(16,0));
  up=Bar(go,"Looking up",new Vector2(0,1),new Vector2(1,1),new Vector2(0,16));down=Bar(go,"Looking down",new Vector2(0,0),new Vector2(1,0),new Vector2(0,16));
 }
 static Image Bar(GameObject parent,string name,Vector2 min,Vector2 max,Vector2 size){
  var g=new GameObject(name,typeof(Image));g.transform.SetParent(parent.transform,false);
  var r=g.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.pivot=new Vector2(min.x==max.x?min.x:.5f,min.y==max.y?min.y:.5f);r.sizeDelta=size;r.anchoredPosition=Vector2.zero;
  var i=g.GetComponent<Image>();i.color=new Color(.21f,.79f,.64f,0);i.raycastTarget=false;return i;
 }
}
}
