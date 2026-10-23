using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Profiling;
namespace GripRehab {
// Explicit, isolated software acceptance. No camera, microphone or user data.
public sealed class PhysicsHandAcceptance:MonoBehaviour {
 [Serializable] sealed class Result {public bool passed;public int checks;public string utc,error,scope,device;public float peakPenetrationMm,peakPreSolverSeparationMm,peakSkinGapMm,maxVelocity,maxAngularVelocity,measuredFps;public int contacts;public bool physicsRecorderAvailable;public double meanPhysicsMs,maxPhysicsMs;public long nativePeakBytes;}
 Result result=new Result{scope="Windows player, actual PhysX hand and imported VirtualGrasp mesh; controlled target poses, no live camera/hardware"};
 ApartmentExperience app;PhysicsHandRig hand;int errors;Vector3[] initial;Quaternion[] rotations;
 float started;int frames,physicsSamples;ProfilerRecorder physicsTime;
 void Update(){frames++;if(frames%120==0)result.nativePeakBytes=Math.Max(result.nativePeakBytes,UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());if(physicsTime.Valid){result.physicsRecorderAvailable=true;double value=physicsTime.LastValue/1000000.0;result.meanPhysicsMs+=(value-result.meanPhysicsMs)/++physicsSamples;result.maxPhysicsMs=Math.Max(result.maxPhysicsMs,value);}}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-apartment-physics-hand")>=0)new GameObject("Physics hand acceptance").AddComponent<PhysicsHandAcceptance>();}
 void Check(bool ok,string text){result.checks++;if(!ok)throw new Exception(text);}
 void Error(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)errors++;}
 IEnumerator Start(){started=Time.realtimeSinceStartup;physicsTime=ProfilerRecorder.StartNew(ProfilerCategory.Physics,"Physics.Simulate");Application.logMessageReceived+=Error;Application.runInBackground=true;var stack=new Stack<IEnumerator>();stack.Push(Run());while(stack.Count>0){bool more;object next=null;try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){Finish(false,e.ToString());yield break;}if(!more){stack.Pop();continue;}if(next is IEnumerator nested){stack.Push(nested);continue;}yield return next;}Finish(errors==0,errors==0?"":"Runtime errors="+errors);}
 IEnumerator Run(){
  yield return new WaitForSeconds(1.5f);app=FindAnyObjectByType<ApartmentExperience>();hand=app.GetComponent<PhysicsHandRig>();app.SetHardware(false);app.TestForce=0;app.player.SetPaused(false);app.player.enabled=false;
  yield return new WaitForSeconds(.5f);Check(hand!=null&&hand.Ready,"Physics hand not ready");Check(hand.AllProbes.Length==16&&hand.Bones.Length==16,"16 palm/segment probes required");
  // Freeze only the fixture input source, never move an active physics body.
  app.enabled=false;app.virtualGrasp.enabled=false;
  foreach(var item in app.objects)item.gameObject.SetActive(false);
  for(int side=0;side<2;side++){
   app.SelectHand(side==1);yield return new WaitForSeconds(.2f);
   initial=new Vector3[16];rotations=new Quaternion[16];var targets=Targets(side==1);for(int i=0;i<16;i++){initial[i]=targets[i].position;rotations[i]=targets[i].rotation;}
   foreach(var bone in hand.Bones){Check(!bone.shape.isTrigger,"Physical hand contains a trigger");Check(bone.shape.gameObject.layer==hand.settings.handLayer,"Hand layer incorrect");Check(bone.finger==5?bone.shape is BoxCollider:bone.shape is CapsuleCollider,"Segment geometry incorrect");}
   Check(!Physics.GetIgnoreLayerCollision(hand.settings.handLayer,hand.settings.surfaceLayer),"Hand surface collision disabled");Check(Physics.GetIgnoreLayerCollision(hand.settings.handLayer,0),"Hand must not hit scenery/player");
   var palm=hand.Bones[0];var axis=palm.body.rotation*Vector3.up;var start=palm.shape.bounds.center;float extent=Vector3.Distance(palm.shape.ClosestPoint(start+axis*10),start);
   var item=Fixture(start+axis*(extent+.055f),.035f,.08f);var before=item.Body.position;
   yield return Motion(targets,axis*.11f,1.2f);
   Check(Vector3.Distance(item.Body.position,before)>.005f,"Light object did not move under physical contact: "+side);
   Destroy(item.gameObject);yield return Motion(targets,Vector3.zero,1.2f);
   item=Fixture(start+axis*(extent+.055f),.035f,1f);item.Body.constraints=RigidbodyConstraints.FreezeAll;
   int contacts=result.contacts;yield return Motion(targets,axis*.12f,1.5f);
   Check(result.contacts>contacts,"Heavy object produced no contact events: "+side);
   foreach(var bone in hand.Bones)Check(bone.ActualPenetration<.003f,"Heavy-object resting penetration exceeds 3mm: "+bone.ActualPenetration);
   Capture(item,side+"-press");
   Check(Vector3.Distance(palm.body.position,targets[0].position)>.02f,"Visual physics hand followed target through fixed surface");
   // Curved surface slide: keep the normal press, shift tangentially.
   yield return Motion(targets,axis*.12f+palm.body.rotation*Vector3.right*.03f,.8f);
   Capture(item,side+"-curved-slide");
   Check(HandRigRules.Finite(palm.body.position),"Curved slide became non-finite");
   // Sudden tracking target jump remains bounded; input freeze disables contacts.
   yield return Motion(targets,axis*.25f,.25f);app.player.SetPaused(true);yield return new WaitForSeconds(.1f);
   foreach(var bone in hand.Bones)Check(!bone.shape.enabled&&bone.body.isKinematic,"Lost/paused input kept a collider driven");
   app.player.SetPaused(false);Destroy(item.gameObject);yield return Motion(targets,Vector3.zero,1.5f);
   item=Fixture(hand.Palm.position+hand.Palm.rotation*Vector3.right*.04f,.015f,.03f);
   Check(item.GrabPhysics(hand,item.Body.position),"Small-object compliant grasp rejected");Check(!item.Body.isKinematic&&item.PhysicsGripActive,"Grip made object kinematic");
   yield return new WaitForSeconds(.2f);item.Release(Vector3.zero);Check(!item.PhysicsGripActive&&!item.IsHeld&&item.Body.useGravity,"Small-object release sticky");yield return new WaitForSeconds(.2f);Destroy(item.gameObject);
  }
  Check(errors==0,"Runtime errors: "+errors);
 }
 Transform[] Targets(bool left){var result=new Transform[16];result[0]=app.virtualGrasp.TargetWrist(left);Array.Copy(app.virtualGrasp.TargetFingerBones(left),0,result,1,15);return result;}
 SqueezableObject Fixture(Vector3 position,float radius,float mass){var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Physical contact fixture";go.layer=9;go.transform.position=position;go.transform.localScale=Vector3.one*(radius*2);var body=go.AddComponent<Rigidbody>();body.mass=mass;body.useGravity=false;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.solverIterations=12;body.solverVelocityIterations=4;var item=go.AddComponent<SqueezableObject>();item.radius=radius;var proxy=go.AddComponent<HandSurfaceProxy>();proxy.hand=hand;return item;}
 IEnumerator Motion(Transform[] targets,Vector3 offset,float seconds){float end=Time.realtimeSinceStartup+seconds;while(Time.realtimeSinceStartup<end){for(int i=0;i<16;i++){targets[i].SetPositionAndRotation(initial[i]+offset,rotations[i]);}yield return new WaitForFixedUpdate();foreach(var bone in hand.Bones){Check(HandRigRules.Finite(bone.body.position)&&HandRigRules.Finite(bone.body.linearVelocity),"Non-finite physics state");float v=bone.body.linearVelocity.magnitude,w=bone.body.angularVelocity.magnitude;result.maxVelocity=Mathf.Max(result.maxVelocity,v);result.maxAngularVelocity=Mathf.Max(result.maxAngularVelocity,w);Check(v<hand.settings.maxVelocity+.3f,"Hand velocity exceeded bounded drive: "+v);if(bone.Contact.item!=null){result.contacts++;result.peakPreSolverSeparationMm=Mathf.Max(result.peakPreSolverSeparationMm,bone.Contact.penetration*1000);result.peakPenetrationMm=Mathf.Max(result.peakPenetrationMm,bone.ActualPenetration*1000);}}}}
 void Capture(SqueezableObject item,string name){
  if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
  var baked=new Mesh();hand.RenderedHand.BakeMesh(baked,true);var vertices=baked.vertices;var used=new bool[vertices.Length];foreach(int vertex in baked.triangles)used[vertex]=true;float skinGap=0;
  for(int i=0;i<vertices.Length;i++)if(used[i]){var point=hand.RenderedHand.transform.TransformPoint(vertices[i]);float closest=float.PositiveInfinity;foreach(var bone in hand.Bones)closest=Mathf.Min(closest,Vector3.Distance(point,bone.shape.ClosestPoint(point)));skinGap=Mathf.Max(skinGap,closest);}
  Destroy(baked);result.peakSkinGapMm=Mathf.Max(result.peakSkinGapMm,skinGap*1000);Debug.Log("[HAND_RENDER_FIT] "+name+" maximum skin/collider gap="+skinGap+" rootScale="+hand.RenderedHand.rootBone.lossyScale);Check(skinGap<.02f,"Rendered skeleton diverged from physics colliders: "+skinGap);
  var bounds=item.GetComponent<Renderer>().bounds;foreach(var bone in hand.Bones)bounds.Encapsulate(bone.shape.bounds);
  var go=new GameObject("Hand contact evidence camera",typeof(Camera));var camera=go.GetComponent<Camera>();camera.cullingMask=(1<<hand.settings.handLayer)|(1<<9);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.12f,.16f);camera.orthographic=true;camera.orthographicSize=bounds.extents.magnitude*1.3f;camera.nearClipPlane=.01f;camera.farClipPlane=5;
  camera.transform.position=bounds.center+hand.Palm.rotation*new Vector3(.3f,.35f,-.25f);camera.transform.LookAt(bounds.center);var rt=new RenderTexture(640,640,24);camera.targetTexture=rt;var prior=RenderTexture.active;
  var renderer=item.GetComponent<Renderer>();var priorMaterial=renderer.sharedMaterial;var material=new Material(app.objects[0].visual.GetComponent<Renderer>().sharedMaterial);
  Debug.Log("[HAND_FIXTURE_RENDER] "+name+" object="+item.transform.position+" renderer="+renderer.enabled+" active="+renderer.gameObject.activeInHierarchy+" bounds="+renderer.bounds+" hand="+hand.Palm.position);
  if(material!=null){material.color=new Color(1,.55f,.08f);if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",material.color);renderer.sharedMaterial=material;}
  try{camera.Render();RenderTexture.active=rt;var image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();Directory.CreateDirectory("Logs/physics-hand-images");File.WriteAllBytes("Logs/physics-hand-images/"+name+".png",image.EncodeToPNG());Destroy(image);}finally{renderer.sharedMaterial=priorMaterial;if(material!=null)Destroy(material);RenderTexture.active=prior;camera.targetTexture=null;rt.Release();Destroy(rt);Destroy(go);}
 }
 void Finish(bool ok,string error){Application.logMessageReceived-=Error;physicsTime.Dispose();result.passed=ok;result.error=error;result.utc=DateTime.UtcNow.ToString("O");result.device=SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName;result.measuredFps=frames/Mathf.Max(.1f,Time.realtimeSinceStartup-started);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/physics-hand-player.json",JsonUtility.ToJson(result,true));if(ok)Debug.Log("[PHYSICS_HAND] PASS "+result.checks);else Debug.LogError("[PHYSICS_HAND] FAIL "+error);Application.Quit(ok?0:1);}
}
}
