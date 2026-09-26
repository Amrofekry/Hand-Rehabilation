using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace GripRehab.Editor {
[InitializeOnLoad] public static class RehabEditorQA {
 const string Active="ApartmentQA.Active",Cycle="ApartmentQA.Cycle";static double deadline;
 static RehabEditorQA(){EditorApplication.playModeStateChanged+=State;EditorApplication.update+=Tick;}
 [MenuItem("Grip Rehab/Run Apartment Play Tests (3 cycles)")]
 public static void Begin(){
  ApartmentAuthoring.VerifyScene();RehabVerification.VerifyLogic();RehabIntegrationChecks.Run();
  File.WriteAllText("Logs/editor-qa.txt","Apartment QA "+DateTime.UtcNow.ToString("O")+"\n");
  UnityEditor.SessionState.SetBool(Active,true);UnityEditor.SessionState.SetInt(Cycle,0);EditorApplication.EnterPlaymode();
 }
 static void State(PlayModeStateChange state){
  if(!UnityEditor.SessionState.GetBool(Active,false))return;
  if(state==PlayModeStateChange.EnteredPlayMode){deadline=EditorApplication.timeSinceStartup+80;EditorApplication.delayCall+=()=>new GameObject("Editor acceptance").AddComponent<ApartmentAcceptance>();}
  if(state==PlayModeStateChange.EnteredEditMode){
   try {
    if(!UnityEditor.SessionState.GetBool("ApartmentQA.LastPassed",false))throw new Exception("Runtime suite failed; see apartment-runtime-checks.json");
    foreach(int port in new[]{27817,27818})using(var socket=new UdpClient(new IPEndPoint(IPAddress.Loopback,port))){}
    int cycle=UnityEditor.SessionState.GetInt(Cycle,0)+1;UnityEditor.SessionState.SetInt(Cycle,cycle);
    File.AppendAllText("Logs/editor-qa.txt",$"PASS: cycle {cycle}, runtime suite and both UDP sockets released\n");
    if(cycle<3)EditorApplication.delayCall+=()=>EditorApplication.EnterPlaymode();
    else{UnityEditor.SessionState.SetBool(Active,false);ApartmentAuthoring.VerifyScene();File.AppendAllText("Logs/editor-qa.txt","PASS: THREE PLAY CYCLES\n");if(Application.isBatchMode||Array.IndexOf(Environment.GetCommandLineArgs(),"-rehab-qa-exit")>=0)EditorApplication.Exit(0);else OpenWorkspace();}
   }catch(Exception e){Fail(e.ToString());}
  }
 }
 static void Tick(){
  if(!UnityEditor.SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  if(ApartmentAcceptance.Finished){UnityEditor.SessionState.SetBool("ApartmentQA.LastPassed",ApartmentAcceptance.Passed);EditorApplication.ExitPlaymode();}
  else if(deadline>0&&EditorApplication.timeSinceStartup>deadline)Fail("Runtime timeout");
 }
 static void Fail(string message){UnityEditor.SessionState.SetBool(Active,false);File.AppendAllText("Logs/editor-qa.txt","FAIL: "+message+"\n");if(Application.isBatchMode||Array.IndexOf(Environment.GetCommandLineArgs(),"-rehab-qa-exit")>=0)EditorApplication.Exit(1);else if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();}
 public static void VerifyEditPersistence(){
  var scene=EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);var item=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>().holdAnchor;
  var original=item.localPosition;item.localPosition=original+Vector3.right*.01f;PrefabUtility.RecordPrefabInstancePropertyModifications(item);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  scene=EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);item=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>().holdAnchor;
  if(Vector3.Distance(item.localPosition,original+Vector3.right*.01f)>.0001f)throw new Exception("Saved visual edit lost on reopen");
  item.localPosition=original;PrefabUtility.RecordPrefabInstancePropertyModifications(item);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  var before=File.ReadAllBytes(ApartmentAuthoring.ScenePath);RehabVerification.BuildWindows();
  if(!before.SequenceEqual(File.ReadAllBytes(ApartmentAuthoring.ScenePath)))throw new Exception("Build changed authored scene");
  Debug.Log("[REHAB_PERSISTENCE] PASS: edit/save/reopen and byte-identical scene after Windows build");
 }
 [MenuItem("Grip Rehab/Read Arabic Beginner Guide")]public static void OpenGuide()=>Application.OpenURL(new Uri(Path.GetFullPath("START_HERE_AR.html")).AbsoluteUri);
 [MenuItem("Grip Rehab/Open Beginner Workspace")]public static void OpenWorkspace(){
  if(EditorApplication.isPlaying)return;
  if(SceneManager.GetActiveScene().path!=ApartmentAuthoring.ScenePath){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);}
  var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
  EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");EditorApplication.ExecuteMenuItem("Window/General/Inspector");EditorApplication.ExecuteMenuItem("Window/General/Project");EditorApplication.ExecuteMenuItem("Window/General/Console");EditorApplication.ExecuteMenuItem("Window/General/Game");
  var view=EditorWindow.GetWindow<SceneView>();view.in2DMode=false;view.LookAt(new Vector3(2.2f,1,-4.4f),Quaternion.Euler(15,90,0),1.6f,false,true);Selection.activeGameObject=app.gameObject;view.Focus();
  File.WriteAllText("Logs/editor-ready.json",JsonUtility.ToJson(new Ready{project=Directory.GetCurrentDirectory(),scene=SceneManager.GetActiveScene().path,playing=EditorApplication.isPlaying,utc=DateTime.UtcNow.ToString("O")},true));
 }
 [Serializable]class Ready{public string project,scene,utc;public bool playing;}
}
}
