using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace GripRehab.Editor {
public static class PhysicsHandAuthoring {
 const string Folder="Assets/Rehab/3.4.1-force-object-interaction/PhysicsHand";
 [MenuItem("Grip Rehab/Physics Hand/Install In Saved Scene")]
 public static void Install(){
  if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before installing the physics hand");
  EditorSceneManager.OpenScene("Assets/Scenes/GripRehab.unity");
  var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
  if(app==null||app.virtualGrasp==null||app.contactRig==null)throw new InvalidOperationException("Authored apartment and VirtualGrasp references required");
  string path=Folder+"/HandPhysics.asset";var settings=AssetDatabase.LoadAssetAtPath<PhysicsHandSettings>(path);
  if(settings==null){settings=ScriptableObject.CreateInstance<PhysicsHandSettings>();AssetDatabase.CreateAsset(settings,path);}
  if(!settings.Valid)throw new InvalidOperationException("Invalid physics hand settings");
  // One-time migration from the initial 4N XYZ-drive prototype. The paired
  // force controller needs gravity support plus transport headroom for mugs.
  if(settings.schemaVersion<1){if(settings.maxGripForce==4)settings.maxGripForce=8;settings.schemaVersion=1;EditorUtility.SetDirty(settings);}
  if(settings.schemaVersion<2){if(Mathf.Approximately(settings.maximumFingerRadius,.026f))settings.maximumFingerRadius=.018f;settings.schemaVersion=2;EditorUtility.SetDirty(settings);}
  // The original 140mm ball was larger than the imported hand's grasp span.
  // A 70mm stress ball keeps the existing mesh/topology and material response;
  // only its world size changes. Preserve user-authored non-default scales.
  if(settings.schemaVersion<3){foreach(var item in app.objects)if(item.profile!=null&&item.profile.kind==MaterialKind.Rubber&&item.displayName=="Silicone ball"&&item.transform.localScale==Vector3.one){item.transform.localScale=Vector3.one*.5f;EditorUtility.SetDirty(item.transform);}settings.schemaVersion=3;EditorUtility.SetDirty(settings);}
  if(settings.schemaVersion<4){foreach(var item in app.objects)if(item.profile!=null&&item.displayName=="Silicone ball"&&Mathf.Approximately(item.profile.deformationRadiusMeters,.07f)){item.profile.deformationRadiusMeters=.035f;EditorUtility.SetDirty(item.profile);}settings.schemaVersion=4;EditorUtility.SetDirty(settings);}
  if(settings.schemaVersion<5){if(Mathf.Approximately(settings.maxFingerTorque,.08f))settings.maxFingerTorque=.008f;settings.schemaVersion=5;EditorUtility.SetDirty(settings);}
  var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=tags.FindProperty("layers");
  SetLayer(layers,settings.handLayer,"RehabPhysicsHand");SetLayer(layers,settings.surfaceLayer,"RehabHandSurface");tags.ApplyModifiedPropertiesWithoutUndo();
  for(int i=0;i<32;i++){Physics.IgnoreLayerCollision(settings.handLayer,i,i!=settings.surfaceLayer);Physics.IgnoreLayerCollision(settings.surfaceLayer,i,i!=settings.handLayer);}
  var hand=app.GetComponent<PhysicsHandRig>();if(hand==null)hand=app.gameObject.AddComponent<PhysicsHandRig>();
  hand.app=app;hand.target=app.virtualGrasp;hand.contacts=app.contactRig;hand.settings=settings;app.contactRig.physicsHand=hand;
  foreach(var item in UnityEngine.Object.FindObjectsByType<SqueezableObject>(FindObjectsInactive.Include)){
   var proxy=item.GetComponent<HandSurfaceProxy>();if(proxy==null)proxy=item.gameObject.AddComponent<HandSurfaceProxy>();proxy.hand=hand;
   var body=item.GetComponent<Rigidbody>();body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.solverIterations=settings.solverIterations;body.solverVelocityIterations=settings.solverVelocityIterations;body.maxDepenetrationVelocity=settings.maxDepenetrationVelocity;
   EditorUtility.SetDirty(body);EditorUtility.SetDirty(proxy);
  }
  EditorUtility.SetDirty(settings);EditorUtility.SetDirty(hand);EditorUtility.SetDirty(app.contactRig);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  Debug.Log("[PHYSICS_HAND_INSTALL] Saved one scene, dedicated layer matrix, settings and six surface adapters. Vendor assets unchanged. Fixed timestep preserved: "+Time.fixedDeltaTime);
 }
 static void SetLayer(SerializedProperty layers,int index,string name){var entry=layers.GetArrayElementAtIndex(index);if(!string.IsNullOrEmpty(entry.stringValue)&&entry.stringValue!=name)throw new InvalidOperationException("Layer already occupied: "+index);entry.stringValue=name;}
}
}
