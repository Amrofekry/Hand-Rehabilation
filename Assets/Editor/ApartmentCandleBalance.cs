using System;using System.Linq;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
namespace GripRehab.Editor {public static class ApartmentCandleBalance {
 public static void Apply(){var scene=EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);int changed=0;
  foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(light.name=="Point light"&&Vector3.Distance(light.transform.position,new Vector3(2.85f,.92f,-4.48f))<.55f){light.intensity=.25f;light.range=.9f;light.shadows=LightShadows.None;PrefabUtility.RecordPrefabInstancePropertyModifications(light);changed++;}
  if(changed!=2)throw new Exception("Expected exactly two table-candle point lights, found "+changed);
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);ApartmentAuthoring.VerifyScene();Debug.Log("[CANDLE_BALANCE] PASS: two table candles, subtle illumination");
 }
}}
