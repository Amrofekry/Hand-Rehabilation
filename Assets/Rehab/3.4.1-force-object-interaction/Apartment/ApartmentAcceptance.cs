using System;
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
  Check(!app.Hardware&&app.ForceValid,"Keyboard/mouse demo is the default");
  Check(FindObjectsByType<Camera>(FindObjectsSortMode.None).Length==1&&FindObjectsByType<LiveForceReceiver>(FindObjectsSortMode.None).Length==1&&FindObjectsByType<VisionUdpReceiver>(FindObjectsSortMode.None).Length==1,"Exactly one camera and each input receiver");
  var player=app.player;player.enabled=false;player.SetPaused(false);
  Check(player.transform.position.y>-.2f&&Physics.Raycast(player.transform.position+Vector3.up*.5f,Vector3.down,1f,~(1<<8)),"Player spawn is above the apartment floor");
  var ball=app.objects[0];var cracker=app.objects[2];var sponge=app.objects[1];
  var menu=FindAnyObjectByType<ApartmentChoices>();Check(menu!=null&&menu.objectButtons.Length==3&&menu.livingObjects.Length==3&&menu.kitchenObjects.Length==3,"Saved station and object UI references survive reload");
  player.SetPaused(true);yield return new WaitForSeconds(.3f);Check(menu.controls.interactable&&menu.controls.alpha>.9f,"Object menu appears when input is paused");yield return Screenshot("apartment-options");yield return new WaitForSeconds(.3f);player.SetPaused(false);
  Check(player.Sit(app.seats[0]),"Living room chair can be used from the approach position");
  player.view.transform.LookAt(ball.transform.position);
  yield return new WaitForSeconds(.2f);
  Check(app.TryGrab(ball),"Therapy ball can be reached and grabbed from the chair");
  // In Editor the test drives the same force application explicitly; the executable uses its test flag.
  yield return DriveForce(18,3.7f);
  Check(ball.IsHeld&&ball.Body.isKinematic&&ball.Compression>.08f,"Held ball is kinematic and deforms under grip force");
  Check(app.ProgressSeconds>=3,"A valid 18 N hold completes the three-second exercise");
  Check(app.rightHand.Closure>.4f,"Rigged fingers close around a held object");
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
  Check(mug.IsBroken,"Porcelain mug fractures under sustained excessive demo force");
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
  yield return DriveForce(30,.6f);Check(cup.Compression>.2f,"Silicone cup visibly yields to demo grip force");
  app.TestForce=0;menu.objectButtons[2].onClick.Invoke();yield return null;
  var tomato=menu.kitchenObjects[2];Check(tomato.gameObject.activeSelf&&app.TryGrab(tomato),"Kitchen tomato is selectable and can be gripped");
  yield return DriveForce(30,.6f);Check(tomato.Compression>.18f,"Tomato deforms under demo grip force");
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
   yield return new WaitForSeconds(.65f);
   Check(!app.ForceValid&&app.Held==null&&app.ForceNewtons==0,"Stale hardware input releases object and stops force within 500 ms plus a frame");
   frame.sequence++;frame.sampleMs+=1000;Send(udp,27818,JsonUtility.ToJson(frame));yield return new WaitForSeconds(.08f);
   Check(app.ForceValid,"New data recovers after interruption");
   app.ResetObjects();yield return null;Check(app.TryGrab(sponge),"Object can be grabbed after recovery");
   frame.sequence++;frame.sampleMs+=100;frame.calibrationRevision++;frame.calibrationCrc++;Send(udp,27818,JsonUtility.ToJson(frame));yield return new WaitForSeconds(.08f);
   Check(app.Held==null,"Calibration change ends the previous grip");
   Send(udp,27818,"not json");yield return null;
   var landmarks=new Vector3[21];for(int i=0;i<21;i++)landmarks[i]=new Vector3(.4f+i*.004f,.5f-i*.004f,0);
   var vision=new LandmarkFrame{schemaVersion=1,source="synthetic acceptance",confidence=.95f,landmarks=landmarks};
   Send(udp,27817,JsonUtility.ToJson(vision));yield return new WaitForSeconds(.08f);
   Check(app.vision.TrackingValid,"Vision UDP accepts a valid 21-landmark frame");
   yield return new WaitForSeconds(.6f);Check(!app.vision.TrackingValid,"Vision pose expires after input loss");
  }
  app.SetHardware(false);app.ResetObjects();player.Teleport(player.Spawn,Quaternion.Euler(0,90,0));player.view.transform.localRotation=Quaternion.Euler(12,0,0);
  yield return Screenshot("apartment-overview");
  player.enabled=true;player.SetPaused(true);
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
 private static void Send(UdpClient socket,int port,string json){var bytes=Encoding.UTF8.GetBytes(json);socket.Send(bytes,bytes.Length,"127.0.0.1",port);}
 private void End(bool passed,string error){
  Finished=true;Passed=passed;Directory.CreateDirectory("Logs");
  File.WriteAllText("Logs/apartment-runtime-checks.json",JsonUtility.ToJson(new Result{passed=passed,utc=DateTime.UtcNow.ToString("O"),checks=checks.ToArray(),error=error},true));
  if(passed)Debug.Log("[APARTMENT_TEST] PASS: "+checks.Count+" runtime checks");else Debug.LogError("[APARTMENT_TEST] FAIL: "+error);
  if(!Application.isEditor)Application.Quit(passed?0:1);
 }
 [Serializable]private class Result{public bool passed;public string utc,error;public string[] checks;}
}
}
