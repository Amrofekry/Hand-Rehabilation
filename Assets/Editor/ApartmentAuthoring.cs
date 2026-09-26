using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace GripRehab.Editor {
public static class ApartmentAuthoring {
 public const string ScenePath="Assets/Scenes/GripRehab.unity";
 public static void VerifyScene(){
  var scene=SceneManager.GetActiveScene();if(scene.path!=ScenePath)scene=EditorSceneManager.OpenScene(ScenePath);
  var exp=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
  if(exp==null||exp.objects.Length<6||exp.objects.Any(x=>x==null)||exp.seats.Length==0||exp.seats.Any(x=>x==null)||exp.player==null||exp.settings==null||exp.rightHand==null||exp.leftHand==null||exp.holdAnchor==null||exp.statusText==null||exp.forceText==null||exp.contextText==null||exp.progressFill==null||exp.cameraLauncher==null||exp.vision.GetComponent<VisionPreviewReceiver>()==null)throw new Exception("Apartment references incomplete");
  foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true)) {
   if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script: "+t.name);
   if(t.TryGetComponent<Renderer>(out var renderer)&&renderer.sharedMaterials.Any(x=>x==null||x.shader==null||x.shader.name=="Hidden/InternalErrorShader"))throw new Exception("Missing material/shader: "+t.name);
  }
  if(exp.settings.startWithHardware||!exp.settings.startWithCamera||exp.forceReceiver.allowSyntheticInEditor)throw new Exception("Delivery must default to camera mode with synthetic force replay disabled");
  if(UnityEngine.Object.FindObjectsByType<Camera>().Length!=1||UnityEngine.Object.FindObjectsByType<LiveForceReceiver>().Length!=1||UnityEngine.Object.FindObjectsByType<VisionUdpReceiver>().Length!=1)throw new Exception("Expected one player camera and each receiver");
  Debug.Log("[APARTMENT_VERIFY] PASS: furnished apartment, scene references/materials/scripts, first-person rig, six selectable objects, seated station, camera default");
 }
 [MenuItem("Grip Rehab/Create Separate Scene Copy")]
 public static void CreateSeparateCopy(){
  if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
  var path=AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/GripRehab-Copy.unity");
  if(!AssetDatabase.CopyAsset(ScenePath,path))throw new Exception("Could not create scene copy");
  EditorSceneManager.OpenScene(path);
 }
}
}
