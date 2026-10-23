using System;using System.IO;using System.Linq;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
namespace GripRehab.Editor {public static class ApartmentVisualPolish {
 public static void Apply(){var scene=EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);
  var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();
  MoveHand(app.rightHand,new Vector3(.13f,-.10f,.53f));MoveHand(app.leftHand,new Vector3(-.18f,-.14f,.48f));
  app.holdAnchor.localPosition=new Vector3(.12f,-.22f,.57f);PrefabUtility.RecordPrefabInstancePropertyModifications(app.holdAnchor);
  var table=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(x=>x.name=="Table_Dining_Apt_01");
  foreach(var r in table.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)if(m!=null&&m.HasProperty("_Smoothness")){m.SetFloat("_Smoothness",Mathf.Min(.33f,m.GetFloat("_Smoothness")));if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",0);EditorUtility.SetDirty(m);}
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  var cam=app.player.view;foreach(var hand in new[]{app.rightHand,app.leftHand}){var r=hand.GetComponentInChildren<SkinnedMeshRenderer>();var point=cam.WorldToViewportPoint(r.bounds.center);if(point.y<.08f||point.y>.82f||point.x<0||point.x>1)throw new Exception("Hand is outside camera framing: "+hand.name+" "+point);Debug.Log("[HAND_FRAMING] PASS "+hand.name+" "+point);}
  ApartmentAuthoring.VerifyScene();
 }
 static void MoveHand(FingerPoseDriver hand,Vector3 position){hand.transform.localPosition=position;PrefabUtility.RecordPrefabInstancePropertyModifications(hand.transform);
  var forearm=hand.transform.Find("Forearm");if(forearm!=null){forearm.localPosition=new Vector3(0,-.015f,-.14f);forearm.localScale=new Vector3(.085f,.12f,.085f);PrefabUtility.RecordPrefabInstancePropertyModifications(forearm);}
  var cuff=hand.transform.Find("Navy sleeve cuff");if(cuff!=null){cuff.localPosition=new Vector3(0,-.015f,-.27f);PrefabUtility.RecordPrefabInstancePropertyModifications(cuff);}
 }
}}
