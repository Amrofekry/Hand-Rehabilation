using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace GripRehab.Editor {
// Compare the compiled job to an independent managed closest/parity query.
public static class BurstSurfaceVerification {
 [Serializable] sealed class Result{public bool passed;public int assertions,skinningAssertions;public string utc,scope;public float maxGapErrorMm,maxSkinningErrorMm;}
 public static void Run(){
  var app=UnityEngine.Object.FindAnyObjectByType<ApartmentExperience>();if(app==null)throw new Exception("Open GripRehab scene first");
  int count=0,initial=BurstSurfaceBatch.LiveBatches;float maximum=0;var random=new System.Random(3412026);var points=new Vector3[384];
  var data=app.surfaceGuard.skinData;var pose=new Matrix4x4[app.virtualGrasp.outputMesh.bones.Length];int skinCount=0;float skinError=0;
  foreach(var scale in new[]{Vector3.one,new Vector3(.7f,1.1f,1.2f),new Vector3(-1,1,1)}){
   var transform=Matrix4x4.TRS(new Vector3(.03f,-.01f,.02f),Quaternion.Euler(17,33,-21),scale);
   for(int i=0;i<pose.Length;i++)pose[i]=transform*app.virtualGrasp.outputMesh.bones[i].localToWorldMatrix*data.bindPoses[i];
   for(int i=0;i<data.vertices.Length;i++){var w=data.weights[i];var p=data.vertices[i];var expected=pose[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+pose[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+pose[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+pose[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;var actual=HandSkinSurface.SkinVertex(p,w,pose);float difference=Vector3.Distance(expected,actual);skinError=Mathf.Max(skinError,difference*1000);skinCount++;if(!GraspMotionPolicy.Finite(actual)||difference>.00001f)throw new Exception("Optimized skin differs from original four-weight skinning: "+difference);}
  }
  foreach(var item in app.objects){
   var source=item.visual.GetComponent<MeshFilter>().sharedMesh;
   foreach(bool pressed in new[]{false,true}){
    var vertices=source.vertices;
    if(pressed&&item.profile.deformable){var solver=new NodeSpringDeformation(item.profile.softBodyTopology,vertices);var topology=item.profile.softBodyTopology;var location=item.visual.TransformPoint(topology.nodes[0]);var normal=item.visual.TransformDirection(topology.normals[0]);for(int step=0;step<80;step++){solver.BeginStep(item.visual);solver.AddContact(item.visual,location,normal,60,item.profile);solver.Step(item.visual,item.profile,.02f);}vertices=solver.Vertices;}
    var surface=new MeshContactSurface(source,item.visual);
    try{
     var bounds=source.bounds;for(int q=0;q<points.Length;q++){var p=new Vector3((float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1);points[q]=item.visual.TransformPoint(bounds.center+Vector3.Scale(p,bounds.extents)*1.15f);}
     surface.Prepare(vertices);var samples=surface.QueryPrepared(points,points.Length,true);
     for(int q=0;q<points.Length;q++){
      if(!surface.Closest(points[q],vertices,out var hit,out _))throw new Exception("Reference query failed");float distance=Vector3.Distance(points[q],hit);float signed=surface.Inside(points[q])?-distance:distance;float difference=Mathf.Abs(samples[q].gap-signed);maximum=Mathf.Max(maximum,difference*1000);count++;
      if(!float.IsFinite(samples[q].gap)||difference>.00001f)throw new Exception(item.displayName+" compiled/reference mismatch: "+difference+" pressed="+pressed+" query="+q);
     }
    }finally{surface.Dispose();}
   }
  }
  if(BurstSurfaceBatch.LiveBatches!=initial)throw new Exception("Native surface buffers leaked");count++;
  Directory.CreateDirectory("Docs/evidence/interaction-2026-10-09");File.WriteAllText("Docs/evidence/interaction-2026-10-09/burst-reference.json",JsonUtility.ToJson(new Result{passed=true,assertions=count,skinningAssertions=skinCount,utc=DateTime.UtcNow.ToString("O"),scope="Exact compiled job versus managed BVH/parity on six original and pressed meshes, plus zero-weight skinning optimization against the original four-weight sum; deterministic fabricated points",maxGapErrorMm=maximum,maxSkinningErrorMm=skinError},true));
  Debug.Log("[BURST_SURFACE_TEST] PASS: "+count+" reference comparisons and native buffer disposal");
 }
}
}
