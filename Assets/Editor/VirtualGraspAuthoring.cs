using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using VirtualGrasp;
namespace GripRehab.Editor {
public static class VirtualGraspAuthoring {
 const string Vendor="Assets/com.gleechi.unity.virtualgrasp/Runtime/Resources/";
 const string Folder="Assets/Rehab/3.2-vision/VirtualGrasp";
 public static void Install(){
  var scene=EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
  if(app.virtualGrasp!=null)throw new Exception("Already installed; use targeted edits.");
  Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  var playerRoot=PrefabUtility.GetOutermostPrefabInstanceRoot(app.player.gameObject);if(playerRoot!=null)PrefabUtility.UnpackPrefabInstance(playerRoot,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
  var oldRight=app.rightHand;var oldLeft=app.leftHand;var parent=oldRight.transform.parent;
  var model=AssetDatabase.LoadAssetAtPath<GameObject>(Vendor+"GleechiHands/GleechiRig.fbx");
  var input=UnityEngine.Object.Instantiate(model,parent);input.name="VirtualGrasp input skeleton (keyboard and MediaPipe)";input.transform.localPosition=Vector3.zero;input.transform.localRotation=Quaternion.identity;
  foreach(var r in input.GetComponentsInChildren<Renderer>())r.enabled=false;
  app.rightHand=Hand(input.transform,parent,false);app.leftHand=Hand(input.transform,parent,true);
  // Both wrist input groups remain under one input root for name-based SDK mapping.
  app.rightHand.transform.SetParent(input.transform,true);app.leftHand.transform.SetParent(input.transform,true);
  UnityEngine.Object.DestroyImmediate(oldRight.gameObject);UnityEngine.Object.DestroyImmediate(oldLeft.gameObject);
  var rig=app.rightHand.gameObject.AddComponent<FingerContactRig>();rig.pose=app.rightHand;rig.Configure();app.contactRig=rig;
  var output=UnityEngine.Object.Instantiate(model);output.name="VirtualGrasp visible hands - native SDK output";
  var renderer=output.GetComponentInChildren<SkinnedMeshRenderer>();var original=renderer.sharedMesh;
  var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name="VirtualGrasp blue glove";mat.SetColor("_BaseColor",new Color(.035f,.34f,.50f));mat.SetFloat("_Smoothness",.37f);
  AssetDatabase.CreateAsset(mat,Folder+"/VirtualGraspBlue.mat");renderer.sharedMaterial=mat;renderer.updateWhenOffscreen=true;
  var right=Half(original,renderer.bones,false);var left=Half(original,renderer.bones,true);AssetDatabase.CreateAsset(right,Folder+"/RightHandMesh.asset");AssetDatabase.CreateAsset(left,Folder+"/LeftHandMesh.asset");
  var sdk=output.AddComponent<RehabVirtualGrasp>();sdk.inputSkeleton=input.transform;sdk.outputMesh=renderer;sdk.registrationMesh=original;sdk.rightOnlyMesh=right;sdk.leftOnlyMesh=left;
  var cp=AssetDatabase.LoadAssetAtPath<VG_ControllerProfile>(Vendor+"VG_ControllerProfiles/VG_CP_Common.Generic.asset");var hp=AssetDatabase.LoadAssetAtPath<VG_HandProfile>(Vendor+"VG_HandProfiles/VG_HP_GleechiHands.asset");
  sdk.m_avatars=new List<VG_Avatar>{new VG_Avatar{m_skeletalMesh=renderer,m_handProfile=hp,m_primarySensorSetup=new VG_SensorSetup{m_profile=cp,m_origin=output.transform},m_secondarySensorSetup=new VG_SensorSetup()}};
  sdk.m_globalGraspInteractionSettings=new VG_GraspInteractionSettings();sdk.m_debugSettings=new VG_DebugSettings();app.virtualGrasp=sdk;
  // Preview matches the input rest pose even outside Play.
  var source=input.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());foreach(var bone in renderer.bones)if(source.TryGetValue(bone.name,out var src))bone.SetPositionAndRotation(src.position,src.rotation);
  renderer.sharedMesh=right;
  var choices=UnityEngine.Object.FindAnyObjectByType<ApartmentChoices>();
  var button=UnityEngine.Object.Instantiate(choices.inputButton,choices.inputButton.transform.parent);button.name="Choose rehabilitation hand";button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();
  var rect=button.GetComponent<RectTransform>();rect.anchoredPosition+=new Vector2(0,-56);choices.handButton=button;
  button.GetComponentInChildren<UnityEngine.UI.Text>().text="RIGHT HAND (SWITCH)";
  EditorUtility.SetDirty(app);EditorUtility.SetDirty(choices);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("[VG_AUTHORING] Installed original GleechiRig, SDK and hand selector");
 }
 public static void Polish(){
 var scene=EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);var menu=UnityEngine.Object.FindAnyObjectByType<ApartmentChoices>();var panel=menu.controls.GetComponent<RectTransform>();panel.sizeDelta=new Vector2(panel.sizeDelta.x,254);var r=menu.handButton.GetComponent<RectTransform>();r.anchoredPosition=new Vector2(0,-218);r.sizeDelta=new Vector2(404,37);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
 }
 static FingerPoseDriver Hand(Transform model,Transform parent,bool left){
  string suffix=left?"_L":"_R";var bones=model.GetComponentsInChildren<Transform>(true);var wrist=bones.First(t=>t.name=="Wrist"+suffix);
  var go=new GameObject(left?"Left hand input":"Right hand input");go.transform.SetParent(parent,false);go.transform.position=wrist.position;wrist.SetParent(go.transform,true);
  var driver=go.AddComponent<FingerPoseDriver>();driver.curlAxis=Vector3.forward;driver.curlSign=left?1:-1;driver.spreadAxis=Vector3.up;
  var names=new[]{"Index","Middle","Ring","Pinky","Thumb"};driver.joints=names.SelectMany(n=>new[]{"A","B","C"}.Select(j=>bones.First(t=>t.name==n+j+suffix))).ToArray();
  go.transform.localPosition=new Vector3(left?-.13f:.13f,-.08f,.47f);return driver;
 }
 static Mesh Half(Mesh original,Transform[] bones,bool left){
  var mesh=UnityEngine.Object.Instantiate(original);mesh.name=left?"Gleechi left hand":"Gleechi right hand";var weights=mesh.boneWeights;var tris=mesh.triangles;var kept=new List<int>();
  for(int i=0;i<tris.Length;i+=3){bool match=true;for(int j=0;j<3;j++){var w=weights[tris[i+j]];if(!bones[w.boneIndex0].name.EndsWith(left?"_L":"_R"))match=false;}if(match){kept.Add(tris[i]);kept.Add(tris[i+1]);kept.Add(tris[i+2]);}}
  mesh.subMeshCount=1;mesh.SetTriangles(kept,0);mesh.RecalculateBounds();if(kept.Count<100)throw new Exception("Hand triangle extraction failed");return mesh;
 }
}}
