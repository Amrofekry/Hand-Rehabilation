using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace GripRehab.Editor {
public static class ApartmentReview {
 const string Source="Assets/ThirdParty/Brick Project Studio/Apartment Kit/Scenes/Scene_02.unity";
 public static void Inspect() {
  var scene=EditorSceneManager.OpenScene(Source);
  var roots=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).Where(x=>x.name.StartsWith("Apartment_")).ToArray();
  var report=new List<object>();
  Directory.CreateDirectory("Docs/images");
  foreach(var candidate in roots) {
   var rs=candidate.GetComponentsInChildren<Renderer>(true); if(rs.Length==0) continue;
   Bounds bounds=rs[0].bounds;foreach(var r in rs) bounds.Encapsulate(r.bounds);
   string info=candidate.name+" position="+candidate.position+" bounds="+bounds+" renderers="+rs.Length+" meshes="+candidate.GetComponentsInChildren<MeshFilter>(true).Sum(x=>x.sharedMesh==null?0:x.sharedMesh.triangles.Length/3)+"\n";
   foreach(var t in candidate.GetComponentsInChildren<Transform>(true))
    if(t.name.Contains("Kitchen")||t.name.Contains("Bathroom")||t.name.Contains("Table")||t.name.Contains("Chair")||t.name.Contains("Sofa")||t.name.Contains("Counter")) info+=t.name+" world="+t.position+" local="+t.localPosition+"\n";
   File.WriteAllText("Logs/"+candidate.name+"-review.txt",info);Debug.Log(info.Split('\n')[0]);
  }
  foreach(var handFile in new[]{"Assets/ThirdParty/UnityHands/RightHand.fbx","Assets/ThirdParty/UnityHands/LeftHand.fbx"}) {
   var obj=AssetDatabase.LoadAssetAtPath<GameObject>(handFile);var info="";
   foreach(var t in obj.GetComponentsInChildren<Transform>(true)) info+=t.name+" pos="+t.localPosition+" rot="+t.localEulerAngles+"\n";
   File.WriteAllText("Logs/"+Path.GetFileNameWithoutExtension(handFile)+"-bones.txt",info);
  }
 }
}}
