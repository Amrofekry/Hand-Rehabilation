using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
namespace GripRehab {
// Opt-in software fixtures only. Never starts the real webcam or saves patient data.
[DefaultExecutionOrder(2000)]
public sealed class SoftBodyAcceptance:MonoBehaviour {
 [Serializable] class Row {public string material,hand,mode;public int contacts,nodes,fragments;public float lowMm,mediumMm,highMm,recoveredMm,damage,peakPenetrationMm;public int limitedJoints;public bool broken;}
 [Serializable] class Result {public bool passed;public string utc,error,scope,device;public Row[] rows;public int checks;public double contactMeanMs,contactMaxMs;public float measuredFps;public long nativePeakBytes,monoUsedPeakBytes;public int meshesAtStart,meshesAtEnd;}
 readonly List<Row> rows=new List<Row>();readonly List<string> failures=new List<string>();ApartmentExperience app;UdpClient socket;long seq=10000;int count;float started;int frames;int errors;
 readonly object replayLock=new object();System.Threading.Thread replayThread;volatile bool stopReplay;string replayMode="",replayError="";float replayForce;bool replayLeft;
 bool capturedCracks;long nativePeak,monoPeak;int meshesAtStart;
 SqueezableObject pendingSkin;bool samplePending;float sampledSkinDepth;
 void LateUpdate(){
  // Measure the displayed pose AFTER the SDK and the contact guard finish.
  // Coroutine checks after Update otherwise see the SDK's unconstrained pose
  // that is subsequently replaced before rendering.
  if(!samplePending)return;
  sampledSkinDepth=pendingSkin.IsBroken?0:app.surfaceGuard.Skin.StrictDepth(pendingSkin);samplePending=false;
 }
 IEnumerator MeasureDisplayedSkin(SqueezableObject item){pendingSkin=item;samplePending=true;while(samplePending)yield return null;}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-apartment-deformation")>=0)new GameObject("Deformation acceptance").AddComponent<SoftBodyAcceptance>();
 }
 void Update(){frames++;if(frames%120==0){nativePeak=Math.Max(nativePeak,UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());monoPeak=Math.Max(monoPeak,UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong());}}
 void Error(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)errors++;}
 IEnumerator Start(){
  Application.runInBackground=true;Application.logMessageReceived+=Error;started=Time.realtimeSinceStartup;
  socket=new UdpClient();replayThread=new System.Threading.Thread(Replay){IsBackground=true,Name="Synthetic deformation input"};replayThread.Start();var run=Run();
  while(true){bool more=false;object value=null;try{more=run.MoveNext();if(more)value=run.Current;}catch(Exception e){Finish(false,e.ToString());yield break;}if(!more)break;yield return value;}
  Finish(errors==0,errors==0?"":string.Join("\n",failures)+" / Runtime errors="+errors);
 }
 void Check(bool value,string message){count++;if(!value){failures.Add(message);Debug.LogError("[DEFORMATION_ASSERT] "+message);}}
 void Send(int port,string text){var bytes=Encoding.UTF8.GetBytes(text);socket.Send(bytes,bytes.Length,"127.0.0.1",port);}
 void Replay(){
  try{while(!stopReplay){string mode;float force;bool left;lock(replayLock){mode=replayMode;force=replayForce;left=replayLeft;}
   if(mode=="Phase2Replay")Force(force);else if(mode=="CameraReplay")Vision(left,force);
   System.Threading.Thread.Sleep(20);
  }}catch(Exception error){replayError=error.ToString();}
 }
 void ReplayCommand(string mode,bool left,float force){lock(replayLock){replayMode=mode;replayLeft=left;replayForce=force;}}
 // Camera stages use genuine partial curls above the existing hold threshold;
 // their percentages are visual closure, not 8/30/80 N measurements.
 void Vision(bool left,float requested){
  float desired=requested<=0?0:requested<20?.58f:requested<50?.68f:1;
  var frame=new LandmarkFrame{schemaVersion=1,source="synthetic deformation acceptance",handedness=left?"Left":"Right",sequence=++seq,captureSeconds=seq*.02,confidence=.96f,landmarks=new Vector3[21]};
  frame.landmarks[0]=new Vector3(.5f,.5f,0);
  float lo=0,hi=130;
  for(int pass=0;pass<15;pass++){
   float angle=(lo+hi)*.5f*Mathf.Deg2Rad;
   for(int finger=0;finger<4;finger++){int n=5+finger*4;float x=.5f+(left?-1:1)*(finger-1.5f)*.045f;
    frame.landmarks[n]=new Vector3(x,.4f,0);frame.landmarks[n+1]=new Vector3(x,.32f,0);
    float sign=left?-1:1;
    frame.landmarks[n+2]=frame.landmarks[n+1]+new Vector3(0,-Mathf.Cos(angle),sign*Mathf.Sin(angle))*.08f;
    frame.landmarks[n+3]=frame.landmarks[n+2]+new Vector3(0,-Mathf.Cos(angle*2),sign*Mathf.Sin(angle*2))*.08f;
   }
   if(HandKinematics.GripScore(frame)<desired)lo=(lo+hi)*.5f;else hi=(lo+hi)*.5f;
  }
  for(int i=1;i<5;i++)frame.landmarks[i]=new Vector3(.5f+(left?1:-1)*(.12f-i*.015f),.44f-i*.03f,0);
  Send(27817,JsonUtility.ToJson(frame));
 }
 void Force(float force){Send(27818,JsonUtility.ToJson(new ForceBridgeFrame{schemaVersion=1,kind="sample",source="synthetic",sessionId="deformation-qa",connectionId="fixture",calibrationRevision=1,calibrationCrc=1234,shapeId=1,sequence=++seq,sampleMs=seq*20,forceMilliNewtons=Mathf.RoundToInt(force*1000),flags=3,ageMs=0}));}
 IEnumerator Drive(string mode,bool left,float force,float seconds){
  ReplayCommand(mode,left,force);
  var pressed=force>0?app.Held:null;bool high=force>=60;
  float initial=pressed!=null?(high?pressed.HighPressureSeconds:pressed.ValidPressureSeconds):0;
  float end=Time.fixedTime+seconds;float deadline=Time.realtimeSinceStartup+Mathf.Max(15,seconds*12);
  // Physics time can advance through catch-up ticks while production safely
  // caps deformation dt. Test sustained pressure using the actual accepted
  // contact integration time, not elapsed engine ticks or a frame hitch.
  while((pressed!=null?!pressed.IsBroken&&(high?pressed.HighPressureSeconds:pressed.ValidPressureSeconds)-initial<seconds:Time.fixedTime<end)&&Time.realtimeSinceStartup<deadline){
   if(mode=="Keyboard"&&!left&&!capturedCracks&&app.Held!=null&&app.Held.DamageValue>=.25f&&!app.Held.IsBroken){
    int id=app.choices.Kitchen?app.choices.KitchenSelection+3:app.choices.LivingSelection;
    if(app.Held.profile.breakable)Check(app.Held.DamageSeamsVisible,"Damage seams are missing in the player");
    Capture(app.Held,id,"cracks");capturedCracks=true;
   }
   if(mode=="Keyboard")app.TestForce=force;

   if(rows.Count>0&&app.surfaceGuard!=null){var row=rows[rows.Count-1];row.peakPenetrationMm=Mathf.Max(row.peakPenetrationMm,app.surfaceGuard.ResidualPenetration*1000);row.limitedJoints=Mathf.Max(row.limitedJoints,app.surfaceGuard.LimitedJoints);}
   yield return new WaitForSeconds(.04f);
  }
  Check(pressed!=null?(pressed.IsBroken||(high?pressed.HighPressureSeconds:pressed.ValidPressureSeconds)-initial>=seconds):Time.fixedTime>=end,"Accepted pressure/physics duration did not reach the requested interval");
 }
 IEnumerator Run(){
  yield return new WaitForSeconds(1.5f);app=FindAnyObjectByType<ApartmentExperience>();
  meshesAtStart=Resources.FindObjectsOfTypeAll<Mesh>().Length;
  Check(app!=null&&app.virtualGrasp.Ready,"Missing scene or VirtualGrasp");app.player.enabled=false;app.player.SetPaused(false);
  var args=Environment.GetCommandLineArgs();int modeIndex=Array.IndexOf(args,"-deformation-mode");string requestedMode=modeIndex>=0&&modeIndex+1<args.Length?args[modeIndex+1]:null;
  foreach(string mode in new[]{"Keyboard","Phase2Replay","CameraReplay"})foreach(bool left in new[]{false,true}){
   if(requestedMode!=null&&mode!=requestedMode)continue;
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"-deformation-left-only")>=0&&!left)continue;
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"-deformation-quick")>=0&&(left||mode!=(requestedMode??"Keyboard")))continue;
   for(int station=0;station<2;station++)for(int selection=0;selection<3;selection++){
    capturedCracks=false;
    app.SetHardware(false);app.TestForce=0;app.ResetObjects();app.choices.SelectStation(station==1);app.choices.SelectObject(selection);app.SelectHand(left);app.player.SetPaused(false);
    var item=app.choices.SelectedObject;Check(item.MaxDisplacementMeters==0&&!item.IsBroken,"Reset does not restore rest immediately");yield return new WaitForSeconds(.15f);
    if(mode=="Phase2Replay"){app.SetHardware(true);yield return Drive(mode,left,0,.25f);}
    if(mode=="CameraReplay"){app.SetCameraMode();yield return Drive(mode,left,0,.25f);}
    // The fixture uses the original direct SDK hand and the shipped contact guard.
    var row=new Row{material=item.displayName,hand=left?"Left":"Right",mode=mode,nodes=item.SimulationNodeCount};rows.Add(row);
    bool grabbed=mode=="CameraReplay"?app.TryCameraSummonSelectedObject():app.TryGrab(item);
    Check(grabbed,"Cannot grab "+row.material+" "+row.hand+" "+mode+" / "+app.Status);
    if(mode=="Keyboard"&&!left)Capture(item,selection+station*3,"before");
    yield return Drive(mode,left,8,.45f);row.lowMm=item.MaxDisplacementMeters*1000;
    yield return Drive(mode,left,30,.7f);row.mediumMm=item.MaxDisplacementMeters*1000;
    yield return MeasureDisplayedSkin(item);
    row.contacts=item.ContactCount;
    Debug.Log("[DEFORMATION_CASE] "+row.material+" "+row.hand+" "+mode+" medium held="+(app.Held==item)+" valid="+app.ForceValid+" contacts="+item.ContactCount+" force="+app.ContactForceNewtons+" gap="+item.ProbeSurfaceGap(app.contactRig.probes[0].GetComponent<CapsuleCollider>(),out _,out _)+" overlap="+app.surfaceGuard.ResidualPenetration);
    Check((app.Held==item&&app.ForceValid)||item.IsBroken,"Unexpected release/stale input during pressure: "+row.material+" "+row.hand+" "+mode);
    Check(item.ContactCount>0||item.IsBroken,"No finger contact: "+row.material+" "+row.hand+" "+mode+" held="+item.IsHeld+" position="+item.Body.position+" hand="+app.ActiveHand.transform.position+" anchors="+app.holdAnchor.position+" probes="+string.Join(";",Array.ConvertAll(app.contactRig.probes,p=>p.transform.position.ToString())));
    Check(app.surfaceGuard.ResidualPenetration<.002f,"Finger surface overlap exceeds 2 mm: "+row.material+" "+row.hand+" "+mode);
    Check(sampledSkinDepth<.002f,"Independent parity skin overlap at medium pressure: "+row.material+" "+row.hand+" "+mode+" depth="+sampledSkinDepth);
    if(item.profile.deformable&&!item.IsBroken)Check(row.mediumMm>.05f,"No visible mesh response: "+row.material+" "+row.hand+" "+mode);
    if(mode=="Keyboard"&&!left)Capture(item,selection+station*3,"squeeze");
    yield return Drive(mode,left,80,2f);row.highMm=item.MaxDisplacementMeters*1000;row.damage=item.DamageValue;row.broken=item.IsBroken;row.fragments=item.FragmentCount;
    yield return MeasureDisplayedSkin(item);
    Debug.Log("[DEFORMATION_CASE] "+row.material+" "+row.hand+" "+mode+" maximum held="+(app.Held==item)+" valid="+app.ForceValid+" input="+app.ForceNewtons+" applied="+app.ContactForceNewtons+" contacts="+item.ContactCount+" displacement="+item.MaxDisplacementMeters+" damage="+item.DamageValue+" dwell="+item.GripDwellSeconds+" threshold="+item.profile.sustainedForceNewtons+" lastApplied="+item.LastDamageForce+" eligible="+item.LastDamageEligible+" limited="+app.surfaceGuard.LimitedJoints+" residual="+app.surfaceGuard.ResidualPenetration+" gaps="+string.Join(";",Array.ConvertAll(app.contactRig.probes,p=>item.ProbeSurfaceGap(p.GetComponent<CapsuleCollider>(),out _,out _).ToString("F5")))+" steps="+item.PressureUpdates+" high="+item.HighPressureUpdates+" highContacts="+item.HighContactUpdates);
    if(mode=="Keyboard"&&!left&&!item.IsBroken)Capture(item,selection+station*3,"maximum");
    Check(row.peakPenetrationMm<2,"Transient finger overlap exceeds 2 mm: "+row.material+" "+row.hand+" "+mode);
    Check(sampledSkinDepth<.002f,"Independent parity skin overlap at maximum pressure: "+row.material+" "+row.hand+" "+mode+" depth="+sampledSkinDepth);
    Check((app.Held==item&&app.ForceValid)||item.IsBroken,"Unexpected release/stale input at maximum pressure: "+row.material+" "+row.hand+" "+mode);
    if(item.profile.deformable&&!item.IsBroken)Check(row.highMm>.05f,"No high-pressure deformation: "+row.material+" "+row.hand+" "+mode);
    if(item.profile.breakable)Check(item.IsBroken&&item.FragmentsVisible&&item.FragmentCount>1,"Brittle object did not fracture: "+row.material+" "+row.hand+" "+mode+" input="+app.ForceNewtons+" contactForce="+app.ContactForceNewtons+" contacts="+item.ContactCount+" damage="+item.DamageValue+" dwell="+item.GripDwellSeconds+" threshold="+item.profile.sustainedForceNewtons+" lastApplied="+item.LastDamageForce+" eligible="+item.LastDamageEligible+" limited="+app.surfaceGuard.LimitedJoints+" residual="+app.surfaceGuard.ResidualPenetration+" gaps="+string.Join(";",Array.ConvertAll(app.contactRig.probes,p=>item.ProbeSurfaceGap(p.GetComponent<CapsuleCollider>(),out _,out _).ToString("F5")))+" steps="+item.PressureUpdates+" high="+item.HighPressureUpdates+" highContacts="+item.HighContactUpdates);
    if(mode=="Keyboard"&&!left&&item.IsBroken)Capture(item,selection+station*3,"fracture");
    app.TestForce=0;app.Release();yield return Drive(mode,left,0,.7f);row.recoveredMm=item.MaxDisplacementMeters*1000;
    if(mode=="Keyboard"&&!left)Capture(item,selection+station*3,"released");
    Check(!item.IsHeld,"Release did not clear grip");item.ResetObject();
    Check(item.MaxDisplacementMeters==0&&item.DamageValue==0&&!item.IsBroken&&!item.FragmentsVisible,"Incomplete full object reset");
   }
  }
  app.SetHardware(false);app.TestForce=0;app.ResetObjects();
  foreach(var item in app.objects){item.ApplyForce(80,.02f);Check(item.MaxDisplacementMeters==0&&item.DamageValue==0,"No-contact force damaged object");}
 }
 void Capture(SqueezableObject item,int id,string phase){
  if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
  // Render the actual object in isolation for readable before/after evidence.
  var renderers=item.IsBroken?GameObject.Find(item.name+" surface fragments")?.GetComponentsInChildren<Renderer>():item.visual.GetComponentsInChildren<Renderer>();
  if(phase=="cracks"){
   var seam=item.transform.Find("Damage seams");
   if(seam!=null&&seam.gameObject.activeInHierarchy){var all=new List<Renderer>(renderers);all.AddRange(seam.GetComponentsInChildren<Renderer>());renderers=all.ToArray();}
  }
  if(renderers==null||renderers.Length==0)return;
  Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
  var old=new int[renderers.Length];for(int i=0;i<old.Length;i++){old[i]=renderers[i].gameObject.layer;renderers[i].gameObject.layer=31;}
  var go=new GameObject("Evidence camera",typeof(Camera));var camera=go.GetComponent<Camera>();
  camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.14f,.18f);
  camera.orthographic=true;camera.orthographicSize=Mathf.Max(.09f,bounds.extents.magnitude*1.2f);
  camera.nearClipPlane=.01f;camera.farClipPlane=5;
  camera.transform.position=bounds.center+new Vector3(.25f,.22f,-.35f);camera.transform.LookAt(bounds.center);
  var rt=new RenderTexture(640,640,24);camera.targetTexture=rt;var prior=RenderTexture.active;
  try{camera.Render();RenderTexture.active=rt;var image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();
   string directory="Logs/deformation-images";Directory.CreateDirectory(directory);File.WriteAllBytes(directory+"/"+id+"-"+phase+".png",image.EncodeToPNG());Destroy(image);
  }finally{RenderTexture.active=prior;camera.targetTexture=null;rt.Release();Destroy(rt);Destroy(go);for(int i=0;i<old.Length;i++)renderers[i].gameObject.layer=old[i];}
 }
 void Finish(bool pass,string error){
  stopReplay=true;if(replayThread!=null)replayThread.Join(1000);
  if(!string.IsNullOrEmpty(replayError)){pass=false;error+=" / Replay sender: "+replayError;}
  Application.logMessageReceived-=Error;socket?.Dispose();Directory.CreateDirectory("Logs");
  File.WriteAllText("Logs/deformation-player.json",JsonUtility.ToJson(new Result{passed=pass,utc=DateTime.UtcNow.ToString("O"),error=error,scope="Explicit synthetic keyboard, camera UDP and Phase 2 UDP replay; no real webcam or BLE hardware",device=SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName,rows=rows.ToArray(),checks=count,contactMeanMs=app!=null?app.ContactMeanMs:0,contactMaxMs=app!=null?app.ContactMaxMs:0,measuredFps=frames/Mathf.Max(.1f,Time.realtimeSinceStartup-started),nativePeakBytes=nativePeak,monoUsedPeakBytes=monoPeak,meshesAtStart=meshesAtStart,meshesAtEnd=Resources.FindObjectsOfTypeAll<Mesh>().Length},true));
  if(pass)Debug.Log("[DEFORMATION_PLAYER] PASS "+count);else Debug.LogError("[DEFORMATION_PLAYER] FAIL "+error);
  Application.Quit(pass?0:1);
 }
}
}
