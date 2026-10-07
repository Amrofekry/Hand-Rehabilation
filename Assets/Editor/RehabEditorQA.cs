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
 const string Active="ApartmentQA.Active",Cycle="ApartmentQA.Cycle",Pending="ApartmentQA.PendingNext";static double deadline, nextStatus;
 static RehabEditorQA(){EditorApplication.playModeStateChanged+=State;EditorApplication.update+=Tick;
  Application.logMessageReceived+=(message,stack,type)=>{
   if(UnityEditor.SessionState.GetBool(Active,false)&&EditorApplication.isPlaying&&
      (type==LogType.Error||type==LogType.Exception||type==LogType.Assert))
    UnityEditor.SessionState.SetInt("ApartmentQA.RuntimeErrors",UnityEditor.SessionState.GetInt("ApartmentQA.RuntimeErrors",0)+1);
  };
  if(UnityEditor.SessionState.GetBool(Active,false)){
   deadline=EditorApplication.timeSinceStartup+240;
   if(!EditorApplication.isPlayingOrWillChangePlaymode&&UnityEditor.SessionState.GetInt(Cycle,0)<3)
    UnityEditor.SessionState.SetBool(Pending,true);
  }
 }
 public static void BeginBatch(){
  if(!Application.isBatchMode)throw new InvalidOperationException("Batch QA is only for an isolated command-line Editor");
  EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);Begin();
 }
 [MenuItem("Grip Rehab/Run Apartment Play Tests (3 cycles)")]
 public static void Begin(){
  UnityEditor.SessionState.SetInt("ApartmentQA.RuntimeErrors",0);
  ApartmentAuthoring.VerifyScene();RehabVerification.VerifyLogic();RehabIntegrationChecks.Run();
  File.WriteAllText("Logs/editor-qa.txt","Apartment QA "+DateTime.UtcNow.ToString("O")+"\n");
  UnityEditor.SessionState.SetBool(Active,true);UnityEditor.SessionState.SetInt(Cycle,0);
  UnityEditor.SessionState.SetBool(Pending,false);
  UnityEditor.SessionState.SetBool("ApartmentQA.LastPassed",false);
  FocusGameView();EditorApplication.EnterPlaymode();
 }
 static void FocusGameView(){
  // These tests exercise real runtime focus guards; focus Game rather than disabling them.
  if(Application.isBatchMode)return;
  EditorApplication.ExecuteMenuItem("Window/General/Game");
  foreach(var view in Resources.FindObjectsOfTypeAll<EditorWindow>())if(view.GetType().Name=="GameView"){view.Focus();break;}
 }
 static void State(PlayModeStateChange state){
  if(!UnityEditor.SessionState.GetBool(Active,false))return;
  if(state==PlayModeStateChange.EnteredPlayMode){
   UnityEditor.SessionState.SetBool(Pending,false);
   UnityEditor.SessionState.SetBool("ApartmentQA.LastPassed",false);
   deadline=EditorApplication.timeSinceStartup+240;
   EditorApplication.delayCall+=()=>{
    FocusGameView();
    if(EditorApplication.isPlaying&&UnityEditor.SessionState.GetBool(Active,false))
     new GameObject("Editor acceptance").AddComponent<ApartmentAcceptance>();
   };
  }
  if(state==PlayModeStateChange.EnteredEditMode){
   try {
    if(UnityEditor.SessionState.GetInt("ApartmentQA.RuntimeErrors",0)!=0)throw new Exception("Runtime logged errors/exceptions; see Editor.log");
    if(!UnityEditor.SessionState.GetBool("ApartmentQA.LastPassed",false))throw new Exception("Runtime suite failed; see apartment-runtime-checks.json");
    foreach(int port in new[]{27817,27818,27819})using(var socket=new UdpClient(new IPEndPoint(IPAddress.Loopback,port))){}
    int cycle=UnityEditor.SessionState.GetInt(Cycle,0)+1;UnityEditor.SessionState.SetInt(Cycle,cycle);
    File.AppendAllText("Logs/editor-qa.txt",$"PASS: cycle {cycle}, runtime suite and all three UDP sockets released\n");
    // Entering Play directly inside the Edit-mode transition can be ignored
    // by a busy Editor. Retry from update only after the transition settles.
    if(cycle<3){UnityEditor.SessionState.SetBool(Pending,true);deadline=EditorApplication.timeSinceStartup+240;}
    else{UnityEditor.SessionState.SetBool(Active,false);ApartmentAuthoring.VerifyScene();File.AppendAllText("Logs/editor-qa.txt","PASS: THREE PLAY CYCLES\n");RehabGateRunner.CompleteQa(true,"Three Play/Stop cycles, runtime suite, and UDP cleanup passed");if(Application.isBatchMode||Array.IndexOf(Environment.GetCommandLineArgs(),"-rehab-qa-exit")>=0)EditorApplication.Exit(0);else OpenWorkspace();}
   }catch(Exception e){Fail(e.ToString());}
  }
 }
 static void Tick(){
  if(!UnityEditor.SessionState.GetBool(Active,false))return;
  if(EditorApplication.timeSinceStartup>=nextStatus){nextStatus=EditorApplication.timeSinceStartup+5;
   File.WriteAllText("Logs/editor-qa-state.json",JsonUtility.ToJson(new QaState{
    cycle=UnityEditor.SessionState.GetInt(Cycle,0),playing=EditorApplication.isPlaying,
    changing=EditorApplication.isPlayingOrWillChangePlaymode,paused=EditorApplication.isPaused,
    compiling=EditorApplication.isCompiling,pending=UnityEditor.SessionState.GetBool(Pending,false),utc=DateTime.UtcNow.ToString("O")},true));
  }
  if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
  if(!EditorApplication.isPlayingOrWillChangePlaymode&&UnityEditor.SessionState.GetBool(Pending,false)){
   UnityEditor.SessionState.SetBool(Pending,false);FocusGameView();EditorApplication.EnterPlaymode();return;
  }
  if(!EditorApplication.isPlaying){if(deadline>0&&EditorApplication.timeSinceStartup>deadline)Fail("Editor Play transition timeout");return;}
  if(ApartmentAcceptance.Finished){UnityEditor.SessionState.SetBool("ApartmentQA.LastPassed",ApartmentAcceptance.Passed);EditorApplication.ExitPlaymode();}
  else if(deadline>0&&EditorApplication.timeSinceStartup>deadline)Fail("Runtime timeout");
 }
 static void Fail(string message){UnityEditor.SessionState.SetBool(Active,false);UnityEditor.SessionState.SetBool(Pending,false);File.AppendAllText("Logs/editor-qa.txt","FAIL: "+message+"\n");RehabGateRunner.CompleteQa(false,message);if(Application.isBatchMode||Array.IndexOf(Environment.GetCommandLineArgs(),"-rehab-qa-exit")>=0)EditorApplication.Exit(1);else if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();}
 public static void VerifyEditPersistence(){
  if(GripRehab.Composition.SceneComposition.Current!=null){SceneOrganization.VerifyEditPersistence();return;}
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
  SceneOrganization.EnsureWorkspace();
  if(SceneManager.GetActiveScene().path!=ApartmentAuthoring.ScenePath){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);}
  var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
  EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");EditorApplication.ExecuteMenuItem("Window/General/Inspector");EditorApplication.ExecuteMenuItem("Window/General/Project");EditorApplication.ExecuteMenuItem("Window/General/Console");EditorApplication.ExecuteMenuItem("Window/General/Game");
  var view=EditorWindow.GetWindow<SceneView>();view.in2DMode=false;view.LookAt(new Vector3(2.2f,1,-4.4f),Quaternion.Euler(15,90,0),1.6f,false,true);Selection.activeGameObject=app.gameObject;view.Focus();
  File.WriteAllText("Logs/editor-ready.json",JsonUtility.ToJson(new Ready{project=Directory.GetCurrentDirectory(),scene=SceneManager.GetActiveScene().path,playing=EditorApplication.isPlaying,utc=DateTime.UtcNow.ToString("O")},true));
 }
 [Serializable]class Ready{public string project,scene,utc;public bool playing;}
 [Serializable]class QaState{public string utc;public int cycle;public bool playing,changing,paused,compiling,pending;}
}
}
