using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace GripRehab.Editor {
public static class CoreInteractionAuthoring {
 const string Folder="Assets/Rehab/3.4.1-force-object-interaction/Apartment/Profiles";
 public static void Install(){
  Directory.CreateDirectory(Folder);
  var scene=EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);
  var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
  if(app==null||app.objects==null||app.objects.Length!=6)throw new Exception("Open the existing six-object apartment scene first.");
  var rig=app.rightHand.GetComponent<FingerContactRig>();
  if(rig==null)rig=app.rightHand.gameObject.AddComponent<FingerContactRig>();
  rig.pose=app.rightHand;rig.Configure();app.contactRig=rig;
  var profiles=new[]{
   Make("01 Silicone ball",MaterialKind.Rubber,.20f,.58f,.58f,true,35,.018f,.07f,0,9,false,55,.4f,1,1),
   Make("02 Kitchen sponge",MaterialKind.Foam,.09f,.85f,.03f,true,30,.030f,.08f,.05f,4,false,55,.4f,1,1),
   Make("03 Brittle cracker",MaterialKind.Paper,.045f,.7f,.02f,true,100,.003f,.025f,.3f,1,true,55,.25f,.4f,.08f),
   Make("04 Porcelain mug",MaterialKind.Ceramic,.18f,.38f,.12f,false,200,0,.02f,0,1,true,60,.35f,.35f,.13f),
   Make("05 Silicone cup",MaterialKind.Rubber,.14f,.7f,.25f,true,30,.028f,.06f,0,7,false,55,.4f,1,1),
   Make("06 Ripe tomato",MaterialKind.Food,.13f,.65f,.05f,true,28,.025f,.06f,.25f,3,false,55,.4f,1,1)
  };
  for(int i=0;i<app.objects.Length;i++){
   app.objects[i].profile=profiles[i];
   EditorUtility.SetDirty(app.objects[i]);
   PrefabUtility.RecordPrefabInstancePropertyModifications(app.objects[i]);
  }
  EditorUtility.SetDirty(rig);EditorUtility.SetDirty(app);PrefabUtility.RecordPrefabInstancePropertyModifications(app);
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  Verify();
 }
 static PhysicalMaterialProfile Make(string name,MaterialKind kind,float mass,float friction,float bounce,bool deform,float full,float depth,float radius,float plastic,float recovery,bool brittle,float breakForce,float dwell,float capacity,float impact){
  string path=Folder+"/"+name.Replace(' ','_')+".asset";
  var p=AssetDatabase.LoadAssetAtPath<PhysicalMaterialProfile>(path);
  if(p!=null){if(p.maxVertices<128){p.maxVertices=4096;EditorUtility.SetDirty(p);}return p;} // preserve later Inspector edits
  p=ScriptableObject.CreateInstance<PhysicalMaterialProfile>();p.name=name;p.kind=kind;p.massKg=mass;p.friction=friction;p.restitution=bounce;
  p.deformable=deform;p.fullCompressionNewtons=full;p.maxDepthMeters=depth;p.deformationRadiusMeters=radius;p.plasticity=plastic;p.recoveryPerSecond=recovery;
  p.breakable=brittle;p.sustainedForceNewtons=breakForce;p.forceDwellSeconds=dwell;p.damageCapacity=capacity;p.impactEnergyJoules=impact;
  AssetDatabase.CreateAsset(p,path);return p;
 }
 public static void Verify(){
  var scene=EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);
  var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
  if(app.contactRig==null||app.contactRig.ProbeCount!=15)throw new Exception("15 saved right-hand phalanx probes required.");
  foreach(var probe in app.contactRig.probes)
   if(probe==null||probe.GetComponent<CapsuleCollider>()==null||!probe.GetComponent<CapsuleCollider>().isTrigger||probe.GetComponent<Rigidbody>()==null)
    throw new Exception("Missing capsule or kinematic body on a finger probe.");
  foreach(var item in app.objects){if(item.profile==null)throw new Exception("Physical profile missing: "+item.displayName);if(item.GetComponent<Collider>()==null||item.profile.maxVertices<128)throw new Exception("Core collider/performance setup incomplete: "+item.displayName);}
  ApartmentAuthoring.VerifyScene();
  Debug.Log("[CORE_VERIFY] PASS: 15 capsule probes, six physical profiles, saved scene and existing apartment bindings");
 }
}
}
