using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace GripRehab.Editor {
[InitializeOnLoad]
public static class PhysicsHandVerification {
 const string Active="GripPhysicsHandQA.Active", Cycle="GripPhysicsHandQA.Cycle";static double deadline,settle;
 static bool checkedRuntime{get=>UnityEditor.SessionState.GetBool("GripPhysicsHandQA.Checked",false);set=>UnityEditor.SessionState.SetBool("GripPhysicsHandQA.Checked",value);}
 static PhysicsHandVerification(){if(UnityEditor.SessionState.GetBool(Active,false)){deadline=EditorApplication.timeSinceStartup+60;settle=EditorApplication.timeSinceStartup+2;}EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;Application.logMessageReceived+=(text,stack,type)=>{if(UnityEditor.SessionState.GetBool(Active,false)&&(type==LogType.Error||type==LogType.Exception))UnityEditor.SessionState.SetInt("GripPhysicsHandQA.Errors",UnityEditor.SessionState.GetInt("GripPhysicsHandQA.Errors",0)+1);};}
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Verify(){
  EditorSceneManager.OpenScene("Assets/Scenes/GripRehab.unity");var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();var hand=app.GetComponent<PhysicsHandRig>();Check(hand!=null&&hand.target==app.virtualGrasp&&hand.contacts==app.contactRig&&app.contactRig.physicsHand==hand,"Missing native rig references");Check(hand.settings.Valid,"Invalid saved settings");
  foreach(var item in app.objects){Check(item.GetComponent<HandSurfaceProxy>().hand==hand,"Missing object adapter");var body=item.GetComponent<Rigidbody>();Check(body.collisionDetectionMode==CollisionDetectionMode.ContinuousDynamic,"Object CCD setup wrong");}
  for(int i=0;i<32;i++){Check(Physics.GetIgnoreLayerCollision(hand.settings.handLayer,i)==(i!=hand.settings.surfaceLayer),"Hand collision matrix wrong");Check(Physics.GetIgnoreLayerCollision(hand.settings.surfaceLayer,i)==(i!=hand.settings.handLayer),"Surface collision matrix wrong");}
  foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script: "+t.name);
  var invalid=ScriptableObject.CreateInstance<PhysicsHandSettings>();Check(invalid.Valid,"Default settings invalid");invalid.maxVelocity=float.NaN;Check(!invalid.Valid,"NaN velocity accepted");invalid.maxVelocity=.8f;invalid.palmMass=-1;Check(!invalid.Valid,"Negative mass accepted");UnityEngine.Object.DestroyImmediate(invalid);
  var go=new GameObject("Nonuniform surface verification");go.transform.localScale=new Vector3(2,3,4);var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up},triangles=new[]{0,1,2}};var surface=new MeshContactSurface(mesh,go.transform);Check(surface.Closest(new Vector3(.3f,.4f,1),mesh.vertices,out var point,out var normal)&&Vector3.Distance(point,new Vector3(.3f,.4f,0))<.00001f&&Vector3.Dot(normal,Vector3.forward)>.999f,"Nonuniform surface metric wrong");UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(mesh);
  File.WriteAllText("Logs/physics-hand-editor-contracts.txt","PASS: six native adapters, 64 layer relations, missing scripts, settings faults and world-space surface metric\n");
 }
 public static void BuildVerified(){PhysicsHandAuthoring.Install();Verify();SoftBodyVerification.Run();RehabVerification.BuildWindows();}
 public static void BuildAndCycles(){BuildVerified();Cycles();}
 public static void BuildProfiled(){
  Verify();Directory.CreateDirectory("Builds/PhysicsHandProfile");
  var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/GripRehab.unity"},locationPathName="Builds/PhysicsHandProfile/GripRehab.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
  if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Profile build failed: "+report.summary.result);
 }
 public static void Cycles(){Verify();Check(Array.IndexOf(Environment.GetCommandLineArgs(),"-apartment-physics-cycles")>=0,"Use explicit synthetic cycle flag");UnityEditor.SessionState.SetBool(Active,true);UnityEditor.SessionState.SetInt(Cycle,0);UnityEditor.SessionState.SetInt("GripPhysicsHandQA.Errors",0);File.WriteAllText("Logs/physics-hand-cycles.txt","");Begin();}
 static void Begin(){checkedRuntime=false;deadline=EditorApplication.timeSinceStartup+60;settle=EditorApplication.timeSinceStartup+2;EditorApplication.EnterPlaymode();}
 static void Tick(){if(!UnityEditor.SessionState.GetBool(Active,false)||EditorApplication.isCompiling||!EditorApplication.isPlaying)return;if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}if(EditorApplication.timeSinceStartup<settle)return;try{var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();app.SetHardware(false);app.player.SetPaused(false);var hand=app.GetComponent<PhysicsHandRig>();if(!hand.Ready)return;Check(UnityEngine.Object.FindObjectsByType<PhysicsHandBone>(FindObjectsInactive.Include).Length==32,"Expected two 16-bone runtime skeletons");Check(UnityEngine.Object.FindObjectsByType<PhysicsHandBone>().Length==16,"Only selected hand must be active");Check(hand.AllProbes.Length==16&&app.virtualGrasp.outputMesh.forceRenderingOff,"Rendered/physics ownership incorrect");foreach(var bone in hand.Bones)Check(!bone.shape.isTrigger,"Runtime trigger instead of solid contact");app.SelectHand(true);Check(hand.Left&&hand.Bones.Length==16,"Left physics hand failed");app.SelectHand(false);Check(!hand.Left,"Right physics hand failed");checkedRuntime=true;EditorApplication.ExitPlaymode();}catch(Exception e){Finish(false,e.ToString());}}
 static void State(PlayModeStateChange state){if(!UnityEditor.SessionState.GetBool(Active,false)||state!=PlayModeStateChange.EnteredEditMode)return;try{Check(checkedRuntime,"Play exited before runtime checks");Check(UnityEngine.Object.FindObjectsByType<PhysicsHandBone>(FindObjectsInactive.Include).Length==0&&GameObject.Find("Physics hand runtime")==null,"Runtime bodies survived Stop");int cycle=UnityEditor.SessionState.GetInt(Cycle,0)+1;UnityEditor.SessionState.SetInt(Cycle,cycle);File.AppendAllText("Logs/physics-hand-cycles.txt","Cycle "+cycle+": PASS; 32 total / 16 selected bodies; 0 after Stop\n");if(cycle<3)EditorApplication.delayCall+=Begin;else Finish(UnityEditor.SessionState.GetInt("GripPhysicsHandQA.Errors",0)==0,"Completed three native Play/Stop cycles");}catch(Exception e){Finish(false,e.ToString());}}
 static void Finish(bool ok,string text){UnityEditor.SessionState.SetBool(Active,false);File.WriteAllText("Logs/physics-hand-cycles.json",JsonUtility.ToJson(new Result{passed=ok,cycles=UnityEditor.SessionState.GetInt(Cycle,0),detail=text,utc=DateTime.UtcNow.ToString("O")},true));if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(ok?0:1);}
 [Serializable]sealed class Result{public bool passed;public int cycles;public string detail,utc;}
}
}
