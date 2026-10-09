using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace GripRehab.Editor {
// Opt-in actual skinned surface diagnostics. No webcam, profiles or vendor edits.
[InitializeOnLoad]
public static class HandSkinContactVerification {
 const string Active="GripRehab.SkinQA.Active";
 [Serializable] sealed class Row {public string material,hand,deepestBone;public float nearestSkinGapMm,deepestSkinInsideMm,wristCorrectionMm,guardDepthMm;public int insideVertices;}
 [Serializable] sealed class Result {public bool passed;public string utc,scope,error;public Row[] rows;}
 static readonly List<Row> rows=new List<Row>();static int index,phase;static double until,deadline;
 static HandSkinContactVerification(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&UnityEditor.SessionState.GetBool(Active,false)){until=EditorApplication.timeSinceStartup+3;deadline=until+90;}};}
 public static void Begin(){UnityEditor.SessionState.SetBool(Active,true);EditorSceneManager.OpenScene("Assets/Scenes/GripRehab.unity");EditorApplication.EnterPlaymode();}
 static void Tick(){
  if(!UnityEditor.SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<until)return;
  try{
   if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Skin diagnostic timeout");
   var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();if(app==null||!app.virtualGrasp.Ready)return;
   foreach(var contact in app.contactRig.contactBones){bool found=false;foreach(var rendered in app.virtualGrasp.outputMesh.bones)if(ReferenceEquals(contact,rendered))found=true;if(!found)throw new Exception("Contact bone is not the rendered skin bone: "+contact.name);}
   if(phase==0){
    app.EnableAcceptanceInput();app.player.enabled=false;app.player.SetPaused(false);app.SetHardware(false);app.TestForce=0;app.ResetObjects();
    int selected=index%6;app.choices.SelectStation(selected>=3);app.choices.SelectObject(selected%3);app.SelectHand(index>=6);app.player.SetPaused(false);
    if(!app.TryGrab(app.choices.SelectedObject))throw new Exception("Diagnostic pickup failed: "+app.Status);
    app.TestForce=30;phase=1;until=EditorApplication.timeSinceStartup+1.5;return;
   }
   var item=app.Held;if(item==null)throw new Exception("Diagnostic object released");
   {
    var renderer=app.virtualGrasp.outputMesh;var mesh=renderer.sharedMesh;
    // Evaluate only vertices referenced by the selected hand's triangles.
    // The extracted half meshes retain hidden vertices of the other hand.
    // World-space CPU skinning mirrors bone * bindPose, avoiding BakeMesh's
    // renderer-scale option on the imported FBX under headless rendering.
    var visible=new bool[mesh.vertexCount];foreach(int v in mesh.triangles)visible[v]=true;
    var positions=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes;var bones=renderer.bones;
    var matrices=new Matrix4x4[bones.Length];for(int i=0;i<bones.Length;i++)matrices[i]=bones[i].localToWorldMatrix*bind[i];
    var current=item.visual.GetComponent<MeshFilter>().sharedMesh;var surface=new MeshContactSurface(current,item.visual);var vertices=current.vertices;
    var row=new Row{material=item.displayName,hand=index>=6?"Left":"Right",nearestSkinGapMm=float.PositiveInfinity,wristCorrectionMm=app.surfaceGuard.WristCorrectionMeters*1000,guardDepthMm=app.surfaceGuard.ResidualPenetration*1000};
    for(int v=0;v<positions.Length;v++){if(!visible[v])continue;var w=weights[v];var x=positions[v];
     var q=matrices[w.boneIndex0].MultiplyPoint3x4(x)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(x)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(x)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(x)*w.weight3;
     if(!surface.Closest(q,vertices,out var p,out _))continue;
     float distance=Vector3.Distance(q,p)*1000;row.nearestSkinGapMm=Mathf.Min(row.nearestSkinGapMm,distance);
     if(surface.Inside(q)){row.insideVertices++;if(distance>row.deepestSkinInsideMm){row.deepestSkinInsideMm=distance;row.deepestBone=bones[w.boneIndex0].name;}}
    }
    rows.Add(row);
   }
   app.Release();if(++index>=12){string failure="";foreach(var row in rows)if(row.deepestSkinInsideMm>=2||row.nearestSkinGapMm>=2||!float.IsFinite(row.nearestSkinGapMm))failure+=row.material+" "+row.hand+" skin gap/overlap out of bounds; ";Finish(failure);return;}phase=0;until=EditorApplication.timeSinceStartup+.2;
  }catch(Exception error){Finish(error.ToString());}
 }
 static void Finish(string error){UnityEditor.SessionState.SetBool(Active,false);Directory.CreateDirectory("Docs/evidence/hand-correction-2026-10-08");
  File.WriteAllText("Docs/evidence/hand-correction-2026-10-08/native-skin-diagnostics.json",JsonUtility.ToJson(new Result{passed=error==""&&rows.Count==12,utc=DateTime.UtcNow.ToString("O"),scope="Every selected SDK skin vertex at 30 N synthetic grip, six materials and both hands; contact gap and penetration below 2 mm. No live camera or clinical acceptance.",error=error,rows=rows.ToArray()},true));
  EditorApplication.ExitPlaymode();EditorApplication.delayCall+=()=>EditorApplication.Exit(error==""?0:1);
 }
}
}
