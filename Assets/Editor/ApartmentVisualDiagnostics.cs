using System.IO;using System.Linq;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
namespace GripRehab.Editor {public static class ApartmentVisualDiagnostics {
 public static void Inspect(){EditorSceneManager.OpenScene(ApartmentAuthoring.ScenePath);
 var app=Object.FindAnyObjectByType<ApartmentExperience>();var cam=app.player.view;
 var list=new System.Collections.Generic.List<string>();
 foreach(var hand in new[]{app.rightHand,app.leftHand}){
  list.Add(hand.name+" transform="+hand.transform.position+" scale="+hand.transform.lossyScale+" active="+hand.gameObject.activeInHierarchy);
  foreach(var r in hand.GetComponentsInChildren<Renderer>(true))list.Add("  "+r.name+" "+r.GetType().Name+" enabled="+r.enabled+" bounds="+r.bounds+" mesh="+(r is SkinnedMeshRenderer s?s.sharedMesh?.name:r.GetComponent<MeshFilter>()?.sharedMesh?.name)+" viewport="+cam.WorldToViewportPoint(r.bounds.center));
 }
 foreach(var o in app.objects)list.Add(o.displayName+" at "+o.transform.position+" active="+o.gameObject.activeSelf);
  foreach(var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))list.Add("LIGHT "+l.name+" pos="+l.transform.position+" type="+l.type+" intensity="+l.intensity+" enabled="+l.enabled);
 foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))if(Vector3.Distance(r.bounds.center,new Vector3(2.83f,.83f,-4.41f))<.7f)list.Add("TABLE PROP "+r.name+" center="+r.bounds.center+" mats="+string.Join(",",r.sharedMaterials.Select(m=>m?.name+" smooth="+(m!=null&&m.HasProperty("_Smoothness")?m.GetFloat("_Smoothness"):0)+" emission="+(m!=null&&m.HasProperty("_EmissionColor")?m.GetColor("_EmissionColor").ToString():"none"))));
 File.WriteAllLines("Logs/visual-diagnostics.txt",list);
 Debug.Log(string.Join("\n",list));}
}}
