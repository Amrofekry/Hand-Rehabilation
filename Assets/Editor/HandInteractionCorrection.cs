using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace GripRehab.Editor {
public static class HandInteractionCorrection {
 public static void BakeAndVerify(){SoftBodyAuthoring.Bake();Verify();}
 public static void Install(){
  EditorSceneManager.OpenScene("Assets/Scenes/GripRehab.unity");var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
  var guard=app.GetComponent<HandSurfaceGuard>();if(guard==null)guard=app.gameObject.AddComponent<HandSurfaceGuard>();guard.app=app;app.surfaceGuard=guard;
  const string path="Assets/Rehab/3.4.1-force-object-interaction/HandInteraction/HandSkinData.asset";
  var data=AssetDatabase.LoadAssetAtPath<HandSkinData>(path);if(data==null){data=ScriptableObject.CreateInstance<HandSkinData>();AssetDatabase.CreateAsset(data,path);}
  var mesh=app.virtualGrasp.registrationMesh;data.vertices=mesh.vertices;data.weights=mesh.boneWeights;data.bindPoses=mesh.bindposes;
  data.rightVertices=UniqueVertices(app.virtualGrasp.rightOnlyMesh);data.leftVertices=UniqueVertices(app.virtualGrasp.leftOnlyMesh);
  EditorUtility.SetDirty(data);guard.skinData=data;AssetDatabase.SaveAssets();
  EditorUtility.SetDirty(app);EditorUtility.SetDirty(guard);EditorSceneManager.MarkSceneDirty(app.gameObject.scene);EditorSceneManager.SaveScene(app.gameObject.scene);Verify();
 }
 static int[] UniqueVertices(Mesh mesh){var indices=new System.Collections.Generic.SortedSet<int>(mesh.triangles);var result=new int[indices.Count];indices.CopyTo(result);return result;}
 public static void Verify(){
  EditorSceneManager.OpenScene("Assets/Scenes/GripRehab.unity");
  var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
  if(app==null||app.virtualGrasp==null||app.contactRig.probes.Length!=15)throw new Exception("Direct hand references missing");
  foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing scripts: "+t.name);
  HandSurfaceVerification.Run();RehabIntegrationChecks.Run();SoftBodyVerification.Run();RehabVerification.BuildWindows();
 }
}
}
