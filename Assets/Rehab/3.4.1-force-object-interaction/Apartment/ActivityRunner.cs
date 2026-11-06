using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GripRehab.Product;
using UnityEngine;
using UnityEngine.UI;
namespace GripRehab {
/// <summary>
/// Stage 10: runs one multi-step activity at a time in the apartment. Look-and-act model: pick an item up, look
/// where it should go and release (placement assist puts it upright on the aimed surface, then it is scored
/// against the nearest target); tools act on what you look at while you squeeze (stir, cut). Works the same with
/// keyboard, mouse look, the camera hand and Phase 2 force. Results are Module 1 tasks 2-7 in the dashboards.
/// </summary>
public sealed class ActivityRunner : MonoBehaviour {
 public ApartmentExperience app;public ApartmentChoices choices;
 public Activity Current{get;private set;}
 public bool Active=>Current!=null&&!finished;
 public int Level{get;private set;}=1;
 public ActivityKind Selected{get;private set;}=ActivityKind.Bookshelf;
 /// <summary>Last finished activity (probe and tests).</summary>
 public ActivityResult LastResult{get;private set;}
 int assistedAtStart;
 /// <summary>What the last release did (diagnostics for the probes).</summary>
 public string LastPlacement{get;private set;}="";Collider lastAimCollider;bool lastAimVolume;SqueezableObject armedTool;
 public string Feedback{get;private set;}="";
 public float PlacementYaw{get;private set;}
 bool finished;float startedAt;Transform root;Material source;SqueezableObject[] savedObjects;
 readonly RaycastHit[] hits=new RaycastHit[24];
 Text title,body;Image fill;GameObject panel;Button[] activityButtons;bool chopArmed=true;

 public static ActivityRunner Ensure(ApartmentExperience app,ApartmentChoices choices,Material source){
  var runner=app.GetComponent<ActivityRunner>();if(runner==null)runner=app.gameObject.AddComponent<ActivityRunner>();
  runner.app=app;runner.choices=choices;runner.source=source;return runner;
 }
 void OnEnable(){if(app!=null){app.ObjectGrabbed+=Grabbed;app.ObjectReleased+=Released;}}
 void OnDisable(){if(app!=null){app.ObjectGrabbed-=Grabbed;app.ObjectReleased-=Released;}}
 void Start(){
  if(app!=null){app.ObjectGrabbed-=Grabbed;app.ObjectReleased-=Released;app.ObjectGrabbed+=Grabbed;app.ObjectReleased+=Released;}
  BuildPanel();BuildOptionButtons();
 }

 // ---------------- Starting and stopping
 /// <summary>Starts (or restarts) an activity at a level; 0 = the level the research runtime chooses for it.</summary>
 public bool Begin(ActivityKind kind,int level=0){
  End(false);
  var shell=FindAnyObjectByType<RehabAppShell>();var research=shell!=null?shell.Research:null;
  Level=level>0?level:research!=null?ExerciseTuning.Clamp(research.LevelFor(1,ActivityRules.Task(kind))):1;
  if(research!=null&&!research.ModuleStarted(1,ActivityRules.Task(kind))){Feedback="This activity could not start: "+research.LastSaveProblem;return false;}
  Selected=kind;
  bool kitchen=ActivityRules.Kitchen(kind);
  choices.activityStarting=true;try{choices.SelectStation(kitchen);}finally{choices.activityStarting=false;}
  foreach(var item in kitchen?choices.kitchenObjects:choices.livingObjects)item.gameObject.SetActive(false);
  baseForward=Vector3.zero;root=null;var facing=app.player.transform.forward;facing.y=0;baseForward=facing.normalized;baseYaw=app.player.transform.eulerAngles.y;
  root=new GameObject("Activity: "+ActivityRules.Title(kind)).transform;
  Current=Activity.Create(kind,ActivityTuning.ForLevel(Level));finished=false;Feedback="";PlacementYaw=0;LastResult=null;assistedAtStart=app.AssistedGrabs;
  Current.Build(this);
  savedObjects=app.objects;app.objects=savedObjects.Concat(Current.items.Select(i=>i.Item).Where(i=>i!=null)).ToArray();
  app.PresentationOffset=new Vector3(.14f,-.12f,0); // keep the held item low-right so the aim point stays visible
  app.player.SetPaused(false);
  startedAt=Time.unscaledTime;Refresh();
  RehabAudio.Play(RehabSound.Chime);
  return true;
 }
 /// <summary>Removes the activity's props and restores the station (also called on replay and when leaving).</summary>
 public void End(bool keepResult=true){
  if(Current==null&&root==null)return;
  app.Release();
  if(savedObjects!=null){app.objects=savedObjects;savedObjects=null;}
  if(root!=null)Destroy(root.gameObject);root=null;Current=null;finished=false;
  app.PresentationOffset=Vector3.zero;
  if(choices!=null&&!choices.Lab)choices.SelectObject(choices.Kitchen?choices.KitchenSelection:choices.LivingSelection);
  if(!keepResult)LastResult=null;Refresh();
 }
 void OnDestroy(){if(root!=null)Destroy(root.gameObject);}

 // ---------------- Geometry helpers for activities
 public Transform Root=>root;
 public Material Source=>source;
 Vector3 baseForward;
 Vector3 Fwd{get{if(root!=null&&baseForward!=Vector3.zero)return baseForward;var f=app.player.transform.forward;f.y=0;return f.normalized;}}
 Vector3 Right=>Vector3.Cross(Vector3.up,Fwd);
 /// <summary>The room's facing when the activity started: targets and placements use it, not the current look.</summary>
 public float Yaw=>baseYaw;float baseYaw;
 /// <summary>The support surface f metres ahead of and r metres right of the player (table, counter, stove).</summary>
 public Vector3 Surface(float f,float r){
  var p=app.player.transform.position+Fwd*f+Right*r;p.y=app.player.view.transform.position.y+.3f;
  return Physics.Raycast(p,Vector3.down,out var hit,3,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore)?hit.point:new Vector3(p.x,0,p.z);
 }
 public Quaternion Facing=>Quaternion.LookRotation(Fwd);
 public Material Paint(Color c,float smooth=.4f,float metal=0)=>ActivityProps.Paint(source,c,smooth,metal);

 // ---------------- Grab / release / placement assist
 void Grabbed(SqueezableObject item){
  if(!Active||item==null)return;var a=item.GetComponent<ActivityItem>();if(a==null)return;
  if(a.snappedTo!=null){var t=a.snappedTo;t.occupants.Remove(a);a.snappedTo=null;Current.Removed(a,t);}
  if(a.transform.parent!=root){a.transform.SetParent(root,true);SetColliders(a,true);}
  Current.Grabbed(a);Refresh();
 }
 void Released(SqueezableObject item){
  if(!Active||item==null)return;var a=item.GetComponent<ActivityItem>();if(a==null)return;
  bool aimed=Aim(item,out var point);if(aimed)Put(a,point);
  LastPlacement=a.name+" half "+PlacedHalfWidth(item).ToString("F3")+(aimed?" put at "+point.ToString("F3")+" via "+(lastAimCollider!=null?lastAimCollider.name+"<"+(lastAimCollider.transform.parent!=null?lastAimCollider.transform.parent.name:"")+">":"?")+(lastAimVolume?" into volume":""):" not aimed");
  StartCoroutine(EvaluateLater(a));
 }
 /// <summary>Where the player is looking, ignoring the item itself; volumes (the box) redirect inside.</summary>
 bool Aim(SqueezableObject item,out Vector3 point){
  point=default;var view=app.player.view.transform;
  int n=Physics.RaycastNonAlloc(view.position,view.forward,hits,1.4f,~(1<<8),QueryTriggerInteraction.Ignore);
  float best=float.MaxValue;Collider found=null;
  for(int i=0;i<n;i++){var c=hits[i].collider;if(c==null||item!=null&&c.attachedRigidbody==item.Body)continue;if(hits[i].distance<best){best=hits[i].distance;point=hits[i].point;found=c;}}
  lastAimCollider=found;lastAimVolume=false;
  if(found==null)return false;
  var volume=found.GetComponentInParent<PlacementVolume>();
  // Aiming at something already inside a box counts as aiming into the box.
  if(volume==null&&root!=null)foreach(var v in root.GetComponentsInChildren<PlacementVolume>()){var l=v.transform.InverseTransformPoint(point);var e=v.interior;e.Expand(.02f);if(e.Contains(l)){volume=v;break;}}
  if(volume!=null){point=IntoVolume(volume,item,point);lastAimVolume=true;}
  return true;
 }
 /// <summary>The free floor spot of a volume nearest the aim (a sphere the item's width sweeps down to the floor);
 /// if the floor is full, the top of whatever is below the aim.</summary>
 Vector3 IntoVolume(PlacementVolume volume,SqueezableObject item,Vector3 aim){
  var b=volume.interior;var tr=volume.transform;var aimLocal=tr.InverseTransformPoint(aim);
  float half=Mathf.Max(.02f,PlacedHalfWidth(item));
  half=Mathf.Min(half*.9f,Mathf.Min(b.extents.x,b.extents.z)-.005f);
  Vector3 best=Vector3.zero;float bestDistance=float.MaxValue;
  for(float x=b.min.x+half;x<=b.max.x-half+1e-4f;x+=.015f)for(float z=b.min.z+half;z<=b.max.z-half+1e-4f;z+=.015f){
   var top=tr.TransformPoint(new Vector3(x,b.max.y+.02f,z));
   int n=Physics.SphereCastNonAlloc(top,half,Vector3.down,hits,b.size.y+.05f,~(1<<8),QueryTriggerInteraction.Ignore);
   float nearest=float.MaxValue;Collider first=null;
   for(int i=0;i<n;i++){var c=hits[i].collider;if(c==null||item!=null&&c.attachedRigidbody==item.Body)continue;if(hits[i].distance<nearest){nearest=hits[i].distance;first=c;}}
   if(first==null||first.GetComponentInParent<PlacementVolume>()!=volume)continue; // something already there
   float d=new Vector2(x-aimLocal.x,z-aimLocal.z).sqrMagnitude;if(d<bestDistance){bestDistance=d;best=new Vector3(x,b.min.y,z);}
  }
  if(bestDistance<float.MaxValue)return tr.TransformPoint(best);
  // Floor full: stack, but keep the whole item inside the walls.
  var over=tr.TransformPoint(new Vector3(Mathf.Clamp(aimLocal.x,b.min.x+half,b.max.x-half),b.max.y+.02f,Mathf.Clamp(aimLocal.z,b.min.z+half,b.max.z-half)));
  return Physics.Raycast(over,Vector3.down,out var support,b.size.y+.05f,~(1<<8),QueryTriggerInteraction.Ignore)?support.point:tr.TransformPoint(new Vector3(aimLocal.x,b.min.y,aimLocal.z));
 }
 /// <summary>
 /// Half the item's horizontal footprint as it will be placed (rest pose turned by the placement yaw), from its mesh bounds.
 /// 2026-11-02: this used the colliders' world bounds in the held pose, which change with how the hand holds the item; a
 /// camera-held book measured narrow and was put 1.5 cm over the box wall.
 /// </summary>
 float PlacedHalfWidth(SqueezableObject item){
  if(item==null)return 0;var a=item.GetComponent<ActivityItem>();var filter=item.GetComponentInChildren<MeshFilter>();
  if(a==null||filter==null||filter.sharedMesh==null){float h=0;foreach(var c in item.GetComponentsInChildren<Collider>())if(c.enabled&&!c.isTrigger)h=Mathf.Max(h,Mathf.Max(c.bounds.extents.x,c.bounds.extents.z));return h;}
  // Mesh corners in the item's own frame (the mesh may sit on a child), then turned to the placed rotation.
  var b=filter.sharedMesh.bounds;var s=item.transform.lossyScale;var rotation=Quaternion.Euler(0,Yaw+PlacementYaw,0)*a.restRotation;float half=0;
  for(int i=0;i<8;i++){
   var corner=new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z);
   var local=item.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
   var p=rotation*Vector3.Scale(local,s);half=Mathf.Max(half,Mathf.Max(Mathf.Abs(p.x),Mathf.Abs(p.z)));
  }
  return half;
 }
 /// <summary>Placement assist: upright, turned by the player's chosen yaw, resting on the aimed point.</summary>
 void Put(ActivityItem a,Vector3 point){
  var item=a.Item;var body=item.Body;
  var rotation=Quaternion.Euler(0,Yaw+PlacementYaw,0)*a.restRotation;
  body.isKinematic=true;item.transform.SetPositionAndRotation(point,rotation);Physics.SyncTransforms();
  float bottom=float.MaxValue;foreach(var c in item.GetComponentsInChildren<Collider>())if(c.enabled&&!c.isTrigger)bottom=Mathf.Min(bottom,c.bounds.min.y);
  if(bottom<float.MaxValue)item.transform.position+=Vector3.up*(point.y-bottom+.002f);
  // Assisted placement rests the item exactly on the aimed surface and keeps it there (no settling physics, which
  // let a pan sink through the stove's open grate collider). Items let go into empty space still fall.
  body.position=item.transform.position;body.rotation=rotation;body.velocity=Vector3.zero;body.angularVelocity=Vector3.zero;
 }
 IEnumerator EvaluateLater(ActivityItem a){
  yield return new WaitForSeconds(.35f);
  if(!Active||a==null||a.Item==null||a.Item.IsHeld)yield break;
  Evaluate(a);
 }
 void Evaluate(ActivityItem a){
  SnapTarget best=null;float bestError=float.MaxValue;
  var position=a.Item.Body.position;
  foreach(var t in Current.targets){
   if(t.Full||t.accepts!=null&&!t.accepts(a))continue;
   var w=t.WorldPosition;float e=new Vector2(position.x-w.x,position.z-w.z).magnitude;
   if(e<bestError){bestError=e;best=t;}
  }
  // Tools and items with no place anywhere (a knife put down) are simply set down: not a miss.
  if(!Current.targets.Any(t=>t.accepts==null||t.accepts(a))&&!Current.AllowsFreePlacement(a)){Refresh();return;}
  float tolerance=Current.tuning.positionTolerance;
  if(best==null||bestError>(best.carrier!=null?best.carrierRadius:tolerance)){
   if(Current.AllowsFreePlacement(a)){Refresh();return;}
   Current.misses++;Feedback=best==null?"Nothing here takes the "+a.kind+".":"Not quite on the "+best.label+" (off by "+(bestError*1000).ToString("0")+" mm). Pick it up and try again.";
   RehabAudio.Play(RehabSound.Error);Refresh();return;
  }
  float yawNow=a.transform.eulerAngles.y-a.restRotation.eulerAngles.y;
  float angle=best.carrier!=null?0:ActivityRules.YawError(yawNow,best.yaw,Mathf.Min(a.yawSymmetry,best.yawSymmetry));
  var result=best.carrier!=null?new PlacementResult{accepted=true,positionError=bestError,score=1}:ActivityRules.Placement(bestError,angle,tolerance,Current.tuning.angleTolerance);
  if(!result.accepted){Current.misses++;Feedback="Turn it to match the "+best.label+" (off by "+angle.ToString("0")+"°): , and . turn it before you place.";RehabAudio.Play(RehabSound.Error);Refresh();return;}
  if(!Current.Decide(a,best,out string reason)){Current.errors++;Feedback=reason;RehabAudio.Play(RehabSound.Error);Refresh();return;}
  Snap(a,best);Current.Placed(a,best,result);Feedback=Current.PraiseFor(a,best);RehabAudio.Play(RehabSound.Placed);
  Refresh();CheckDone();
 }
 public void Snap(ActivityItem a,SnapTarget t){
  t.occupants.Add(a);a.snappedTo=t;var item=a.Item;item.Body.isKinematic=true;item.Body.velocity=Vector3.zero;
  if(t.carrier!=null){a.transform.SetParent(t.carrier,true);SetColliders(a,false);}
  else{
   var w=t.WorldPosition;var p=item.transform.position;
   float yawNow=a.transform.eulerAngles.y;float s=Mathf.Min(a.yawSymmetry,t.yawSymmetry);
   float turn=Mathf.DeltaAngle(yawNow,t.yaw);if(s<360){turn=((turn%s)+s)%s;if(turn>s*.5f)turn-=s;}
   item.transform.SetPositionAndRotation(new Vector3(w.x,p.y,w.z),Quaternion.Euler(0,yawNow+turn,0)*a.restRotation);
   item.Body.position=item.transform.position;item.Body.rotation=item.transform.rotation;
  }
 }
 /// <summary>Lets items sit in a carrier without colliding (they travel with it).</summary>
 public static void SetColliders(ActivityItem a,bool on){foreach(var c in a.GetComponentsInChildren<Collider>(true))if(!c.isTrigger)c.enabled=on;}

 // ---------------- Per-frame: keys, tools, completion
 void Update(){
  if(app==null)return;
  if(activityButtons!=null&&buttonsState!=ButtonsState)RefreshButtons();
  bool input=app.player.GameplayInputAllowed;
  if(input&&Input.GetKeyDown(KeyCode.K))Begin(Active?Current.kind:Selected,Active?Level:0);
  if(input&&Input.GetKeyDown(KeyCode.N))NextActivity();
  if(!Active){Show(false);return;}
  if(input&&Input.GetKeyDown(KeyCode.Comma))TurnPlacement(-15);
  if(input&&Input.GetKeyDown(KeyCode.Period))TurnPlacement(15);
  // Tools act on what the player looks at while squeezing.
  var held=app.Held!=null?app.Held.GetComponent<ActivityItem>():null;
  if(held!=null&&app.ForceValid){
   bool aimed=Aim(app.Held,out var point);
   var target=aimed?AimedCollider():null;
   if(held.kind=="knife"){
    float chop=Current.tuning.chopNewtons;
    // Picking up the knife arms the first cut: a camera hand already holds it with a normal grip, so waiting for a
    // relaxed grip first left the first cut impossible (2026-11-02, camera activity probe). Later cuts re-arm on relaxing.
    if(app.Held!=armedTool){armedTool=app.Held;chopArmed=true;}
    if(app.ForceNewtons<chop*.5f)chopArmed=true;
    // A squeeze only uses the cut when aimed at food (2026-11-02): gripping the knife while looking at the board spent it.
    var food=chopArmed&&app.ForceNewtons>=chop&&target!=null?target.GetComponentInParent<CuttableFood>():null;
    if(food!=null){chopArmed=false;Current.Chop(this,food,point);Refresh();}
   } else if(app.ForceNewtons>=Current.tuning.stirNewtons&&target!=null)Current.Squeeze(this,held,target,Time.deltaTime);
  }
  Current.Tick(this,Time.deltaTime);
  CheckDone();
  if(Time.frameCount%10==0)Refresh();
 }
 Collider AimedCollider(){
  var view=app.player.view.transform;int n=Physics.RaycastNonAlloc(view.position,view.forward,hits,1.4f,~(1<<8),QueryTriggerInteraction.Ignore);
  float best=float.MaxValue;Collider found=null;
  for(int i=0;i<n;i++){var c=hits[i].collider;if(c==null||app.Held!=null&&c.attachedRigidbody==app.Held.Body)continue;if(hits[i].distance<best){best=hits[i].distance;found=c;}}
  return found;
 }
 public void Say(string text){Feedback=text;Refresh();}
 /// <summary>Turns the held item's placement (keys , and ., camera wrist roll, voice "rotate").</summary>
 public void TurnPlacement(float degrees){if(float.IsNaN(degrees)||float.IsInfinity(degrees))return;PlacementYaw=Mathf.Repeat(PlacementYaw+degrees+180,360)-180;Refresh();}
 /// <summary>Sets the placement turn directly (camera wrist roll follows the hand).</summary>
 public void SetPlacementYaw(float degrees){if(float.IsNaN(degrees)||float.IsInfinity(degrees)||Mathf.Approximately(degrees,PlacementYaw))return;PlacementYaw=Mathf.Repeat(degrees+180,360)-180;Refresh();}
 /// <summary>Next activity in this room (key N, voice "next activity").</summary>
 public void NextActivity(){Selected=NextInRoom(Active?Current.kind:Selected);if(Active)Begin(Selected);else Refresh();}
 ActivityKind NextInRoom(ActivityKind kind){int first=ActivityRules.Kitchen(kind)?3:0;return (ActivityKind)(first+((int)kind-first+1)%3);}

 void CheckDone(){
  if(!Active||!Current.steps.Complete)return;
  finished=true;float seconds=Time.unscaledTime-startedAt;
  var result=Current.Result(Level,seconds);LastResult=result;
  // Camera grabs made by aim assist (not a fingertip at the object) are recorded, so analysis can tell physical reach from help.
  if(app.CameraMode)result.outcomes.Add(new OutcomeMetric{key="handling.assistedGrabs",label="Camera grabs with aim assist",unit="",value=app.AssistedGrabs-assistedAtStart});
  Feedback="Activity complete: "+result.score+"/100, "+ActivityRules.Stars(result.score)+" of 3 stars. K replays it, N picks the next one.";
  RehabAudio.Play(RehabSound.Complete);Refresh();
  StartCoroutine(Report(result));
 }
 IEnumerator Report(ActivityResult result){
  yield return new WaitForSecondsRealtime(2.5f);
  var research=FindAnyObjectByType<RehabAppShell>()?.Research;
  End(true);
  if(research!=null&&research.LiveSession)research.ExerciseCompleted(result.score,result.seconds,result.outcomes);
 }

 // ---------------- Panel and option buttons (uGUI, like the station selector)
 void BuildPanel(){
  if(choices==null)return;var canvas=choices.GetComponentInParent<Canvas>();if(canvas==null)return;
  panel=new GameObject("Activity panel",typeof(RectTransform),typeof(Image));panel.transform.SetParent(canvas.transform,false);
  var rect=(RectTransform)panel.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-82);rect.sizeDelta=new Vector2(640,124);
  panel.GetComponent<Image>().color=new Color(.025f,.055f,.105f,.9f);
  title=Label(panel.transform,new Vector2(0,-8),new Vector2(620,26),17,FontStyle.Bold);
  body=Label(panel.transform,new Vector2(0,-36),new Vector2(620,64),14,FontStyle.Normal);
  var bar=new GameObject("Progress",typeof(RectTransform),typeof(Image));bar.transform.SetParent(panel.transform,false);
  var br=(RectTransform)bar.transform;br.anchorMin=br.anchorMax=new Vector2(.5f,1);br.pivot=new Vector2(.5f,1);br.anchoredPosition=new Vector2(0,-106);br.sizeDelta=new Vector2(600,8);bar.GetComponent<Image>().color=new Color(1,1,1,.12f);
  var f=new GameObject("Fill",typeof(RectTransform),typeof(Image));f.transform.SetParent(bar.transform,false);
  var fr=(RectTransform)f.transform;fr.anchorMin=new Vector2(0,0);fr.anchorMax=new Vector2(0,1);fr.pivot=new Vector2(0,.5f);fr.sizeDelta=new Vector2(0,0);fill=f.GetComponent<Image>();fill.color=new Color(.1f,.85f,.95f);
  panel.SetActive(false);
  // Stage 11: the activity panel follows the theme (colours, font, text size) like every other card.
  var presentation=FindAnyObjectByType<GripRehab.UI.GripUiThemeController>();if(presentation!=null)presentation.RegisterPresentation(panel.transform);
 }
 static Text Label(Transform parent,Vector2 pos,Vector2 size,int font,FontStyle style){
  var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
  var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.pivot=new Vector2(.5f,1);r.anchoredPosition=pos;r.sizeDelta=size;
  var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=font;t.fontStyle=style;t.alignment=TextAnchor.UpperLeft;t.color=new Color(.9f,.96f,1);t.raycastTarget=false;
  return t;
 }
 // Stage 11: section 04 of the options panel holds the three activities of the current room;
 // pressing one starts it (fewer steps than choose-then-start). K restarts, N picks the next.
 void BuildOptionButtons(){
  if(choices==null||choices.inputButton==null)return;
  var parent=(RectTransform)choices.inputButton.transform.parent;
  activityButtons=new Button[3];
  for(int i=0;i<3;i++){
   int slot=i;var b=Instantiate(choices.inputButton,parent);b.name="ACTIVITY "+(i+1);b.onClick=new Button.ButtonClickedEvent();
   b.onClick.AddListener(()=>{var kind=RoomKinds()[slot];Selected=kind;if(Begin(kind))choices.experience.player.SetPaused(false);});
   activityButtons[i]=b;
  }
  var presentation=FindAnyObjectByType<GripRehab.UI.GripUiThemeController>();
  if(presentation!=null){foreach(var b in activityButtons)presentation.RegisterPresentation(b.transform);presentation.PresentationApplied+=RefreshButtons;}
  ApartmentOptionsLayout.Apply(choices,activityButtons,presentation);
  RefreshButtons();
 }
 static readonly ActivityKind[] livingKinds={ActivityKind.Bookshelf,ActivityKind.CoffeeTable,ActivityKind.StorageBox};
 static readonly ActivityKind[] kitchenKinds={ActivityKind.SimpleMeal,ActivityKind.Cutting,ActivityKind.PrepareAndServe};
 public static string ShortName(ActivityKind k)=>k==ActivityKind.PrepareAndServe?"PREPARE & SERVE":System.Text.RegularExpressions.Regex.Replace(k.ToString(),"(?<=[a-z])(?=[A-Z])"," ").ToUpperInvariant();
 ActivityKind[] RoomKinds()=>choices!=null&&choices.Kitchen?kitchenKinds:livingKinds;
 int buttonsState=-1;
 int ButtonsState=>(choices.Kitchen?1:0)+(choices.Lab?2:0)+(Active?4+8*(int)Current.kind:0);
 void RefreshButtons(){
  if(activityButtons==null)return;
  bool lab=choices.Lab;var kinds=RoomKinds();
  buttonsState=ButtonsState;
  for(int i=0;i<3;i++){
   var b=activityButtons[i];b.interactable=!lab;
   b.GetComponentInChildren<Text>().text=lab?"-":ShortName(kinds[i]);
   choices.PaintChoice(b,!lab&&Active&&Current.kind==kinds[i]);
  }
 }
 void Show(bool on){if(panel!=null&&panel.activeSelf!=on)panel.SetActive(on);}
 void Refresh(){
  if(activityButtons!=null&&buttonsState!=ButtonsState)RefreshButtons();
  if(panel==null)return;
  bool show=Current!=null&&!app.player.Paused;Show(show);if(!show)return;
  var s=Current.steps;
  title.text="ACTIVITY  -  "+ActivityRules.Title(Current.kind).ToUpperInvariant()+"  (level "+Level+")";
  body.text=(finished?"":"Step "+(s.Current+1)+" of "+s.Count+": "+s.Instruction+(s.CurrentNeeded>1?"  ("+s.CurrentDone+"/"+s.CurrentNeeded+")":"")+"\n")+
   (string.IsNullOrEmpty(Feedback)?"":Feedback+"\n")+
   "E pick up  -  look where it goes  -  Q place  -  , . turn ("+PlacementYaw.ToString("0")+"°)  -  K restart  -  N next";
  ((RectTransform)fill.transform).sizeDelta=new Vector2(600*s.Progress,0);
 }
}

/// <summary>What a finished activity reports (score, time and named outcomes for the dashboards).</summary>
public sealed class ActivityResult { public ActivityKind kind;public int score,level;public float seconds;public List<OutcomeMetric> outcomes=new List<OutcomeMetric>(); }
}
