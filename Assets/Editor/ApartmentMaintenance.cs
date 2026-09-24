using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace GripRehab.Editor {
 public static class ApartmentMaintenance {
  public static void PrepareAndBuild(){
   Inspect();
   var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
   var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
   if(app.player.view.GetComponent<AudioListener>()==null)app.player.view.gameObject.AddComponent<AudioListener>();
   foreach(var door in UnityEngine.Object.FindObjectsByType<ApartmentDoor>()) {
    if(door.transform.parent==null||!door.transform.parent.name.StartsWith("DoorFrame"))UnityEngine.Object.DestroyImmediate(door);
    else GameObjectUtility.SetStaticEditorFlags(door.gameObject,0);
   }
   app.progressFill.type=UnityEngine.UI.Image.Type.Simple;
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   foreach(var path in new[]{"Assets/Rehab/3.1-environment/Authored","Assets/Resources/Surfaces","Assets/Resources/RehabBase.mat","Assets/Editor/RehabAuthoring.cs"}) {
    var needed=AssetDatabase.GetDependencies(ApartmentAuthoring.ScenePath,true);
    if(needed.Any(x=>x==path||x.StartsWith(path+"/")))throw new Exception("Old environment is still referenced: "+path);
    AssetDatabase.DeleteAsset(path);
   }
   PruneUnusedVendorAssets();
   foreach(var path in AssetDatabase.GetDependencies(ApartmentAuthoring.ScenePath,true)) {
    if(AssetImporter.GetAtPath(path) is TextureImporter texture&&texture.maxTextureSize>2048){texture.maxTextureSize=2048;texture.SaveAndReimport();}
   }
   RehabEditorQA.VerifyEditPersistence();
  }
  public static void Inspect(){
   EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);
   var all=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
   File.WriteAllLines("Logs/apartment-doors.txt",all.Where(x=>x.name.ToLowerInvariant().Contains("door")).Select(x=>x.name+" pos="+x.position+" mesh="+(x.GetComponent<MeshFilter>()!=null)+" parent="+x.parent?.name));
   File.WriteAllLines("Logs/apartment-bounds.txt",UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include).OrderByDescending(x=>x.bounds.size.sqrMagnitude).Take(35).Select(x=>x.name+" "+x.bounds+" active="+x.gameObject.activeInHierarchy));
   var exp=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
   if(exp.objects.Any(x=>x==null)||exp.seats.Any(x=>x==null)||exp.statusText==null||exp.forceText==null||exp.holdAnchor==null)throw new Exception("Missing scene references");
   Debug.Log("[APARTMENT_BINDINGS] PASS");
  }
  public static void PruneUnusedVendorAssets(){
   EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);
   var roots=AssetDatabase.FindAssets("",new[]{"Assets/Rehab/3.1-environment/Apartment","Assets/Settings","Assets/Scenes"}).Select(AssetDatabase.GUIDToAssetPath).Where(x=>!AssetDatabase.IsValidFolder(x)).ToArray();
   var dependencies=AssetDatabase.GetDependencies(roots,true).ToHashSet(StringComparer.OrdinalIgnoreCase);
   const string prefix="Assets/ThirdParty/Brick Project Studio/";
   var files=AssetDatabase.GetAllAssetPaths().Where(x=>x.StartsWith(prefix,StringComparison.Ordinal)&&!AssetDatabase.IsValidFolder(x)).ToArray();
   long before=files.Sum(x=>File.Exists(x)?new FileInfo(x).Length:0),removed=0;var deleted=new System.Collections.Generic.List<string>();
   foreach(var path in files){if(dependencies.Contains(path)||path.EndsWith(".txt",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".md",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase))continue;
    var full=Path.GetFullPath(path);if(!full.StartsWith(Path.GetFullPath(prefix),StringComparison.OrdinalIgnoreCase))throw new Exception("Deletion escaped vendor directory");
    if(File.Exists(path))removed+=new FileInfo(path).Length;deleted.Add(path);AssetDatabase.DeleteAsset(path);
   }
   File.WriteAllText("Docs/ASSET_SELECTION.md",$"# Selected apartment\n\nApartment_02 from the user-provided Brick Project Studio archive. 125,618 source mesh triangles, approximately half the geometry of Apartment_01. The scene instantiates this furnished apartment only, normalized to 0.72 scale. Original archive remains unchanged in Downloads.\n\nImported vendor source bytes before dependency pruning: {before:N0}. Removed unused bytes: {removed:N0}. Retained vendor source bytes: {before-removed:N0}. Shared FBX files may include unused submeshes; retaining them preserves the selected apartment's dependencies.\n\nMaterials are local URP copies. No vendor controllers run. Unity XR Hands 1.5.0 sample hand meshes are supplied with their original license under Assets/ThirdParty/UnityHands.\n");
   File.WriteAllLines("Logs/removed-unused-vendor-assets.txt",deleted);AssetDatabase.SaveAssets();ApartmentAuthoring.VerifyScene();
  }
 }
}
