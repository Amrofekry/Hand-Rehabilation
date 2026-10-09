using System;
using System.Collections.Generic;
using UnityEngine;
namespace GripRehab {
// Cached samples of the imported skin, not a second hand or a deforming hand.
// Evaluate the same bone * bind-pose weights as the visible SDK renderer.
public sealed class HandSkinSurface {
 readonly Vector3[] rest;readonly BoneWeight[] weights;readonly Matrix4x4[] bind,matrices;
 readonly Transform[] bones;readonly int[][][] groups=new int[2][][],fullGroups=new int[2][][],fingerVertices=new int[2][][];
 readonly Vector3[] points;readonly int[] groupForVertex;readonly int[][] visibleVertices=new int[2][];bool left;
 readonly int[] boneGroups;readonly Vector3[] searchPoints;readonly float[] searchMobility;readonly bool[] searchBones;
 public Bounds Bounds{get;private set;}
 public HandSkinSurface(RehabVirtualGrasp hand,int samplesPerSegment,HandSkinData data){
  if(data==null||data.vertices==null||data.weights==null||data.vertices.Length!=data.weights.Length)throw new ArgumentException("Missing baked project-owned hand surface data");
  rest=data.vertices;weights=data.weights;bind=data.bindPoses;bones=hand.outputMesh.bones;
  matrices=new Matrix4x4[bones.Length];points=new Vector3[rest.Length];groupForVertex=new int[rest.Length];
  boneGroups=new int[bones.Length];for(int i=0;i<bones.Length;i++)boneGroups[i]=Group(bones[i].name);
  searchPoints=new Vector3[rest.Length];searchMobility=new float[rest.Length];searchBones=new bool[bones.Length];
  for(int v=0;v<rest.Length;v++)groupForVertex[v]=Group(bones[weights[v].boneIndex0].name);
  for(int side=0;side<2;side++){
   var visible=new bool[rest.Length];foreach(int v in side==1?data.leftVertices:data.rightVertices)visible[v]=true;
   var active=new List<int>();for(int v=0;v<rest.Length;v++)if(visible[v])active.Add(v);visibleVertices[side]=active.ToArray();
   groups[side]=new int[16][];fullGroups[side]=new int[16][];fingerVertices[side]=new int[5][];
   for(int f=0;f<5;f++){
    var affected=new List<int>();foreach(int v in active){var w=weights[v];
     bool touch=w.weight0>.01f&&Group(bones[w.boneIndex0].name)/3==f||w.weight1>.01f&&Group(bones[w.boneIndex1].name)/3==f||w.weight2>.01f&&Group(bones[w.boneIndex2].name)/3==f||w.weight3>.01f&&Group(bones[w.boneIndex3].name)/3==f;
     if(touch)affected.Add(v);
    }fingerVertices[side][f]=affected.ToArray();
   }
   for(int g=0;g<16;g++){
    var candidates=new List<int>();for(int v=0;v<rest.Length;v++)if(visible[v]&&groupForVertex[v]==g)candidates.Add(v);
    fullGroups[side][g]=candidates.ToArray();int count=Math.Min(candidates.Count,g==15?samplesPerSegment*3:samplesPerSegment);var selected=new List<int>();
    if(count>0){selected.Add(candidates[0]);var distance=new float[candidates.Count];for(int v=0;v<distance.Length;v++)distance[v]=float.PositiveInfinity;
     while(selected.Count<count){int previous=selected[selected.Count-1],best=0;float largest=-1;
      for(int v=0;v<candidates.Count;v++){distance[v]=Mathf.Min(distance[v],(rest[candidates[v]]-rest[previous]).sqrMagnitude);if(distance[v]>largest){largest=distance[v];best=v;}}
      if(largest<1e-12f)break;selected.Add(candidates[best]);
     }
    }groups[side][g]=selected.ToArray();
   }
  }
 }
 static int Group(string name){
  string[] names={"Index","Middle","Ring","Pinky","Thumb"};
  for(int f=0;f<5;f++)if(name.StartsWith(names[f],StringComparison.Ordinal)){char segment=name.Length>names[f].Length?name[names[f].Length]:'A';return f*3+(segment=='A'?0:segment=='B'?1:2);}
  return 15;
 }
 public void Refresh(bool selectedLeft){
  RefreshPose(selectedLeft);bool first=true;var bounds=new Bounds();
  foreach(var indices in groups[left?1:0])foreach(int v in indices){var w=weights[v];var x=rest[v];
   points[v]=matrices[w.boneIndex0].MultiplyPoint3x4(x)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(x)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(x)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(x)*w.weight3;
   if(first){bounds=new Bounds(points[v],Vector3.zero);first=false;}else bounds.Encapsulate(points[v]);
  }Bounds=bounds;
 }
 public void RefreshPose(bool selectedLeft){left=selectedLeft;for(int i=0;i<bones.Length;i++)matrices[i]=bones[i].localToWorldMatrix*bind[i];}
 public void RefreshFingerPose(bool selectedLeft,int finger){left=selectedLeft;for(int i=0;i<bones.Length;i++)if(boneGroups[i]/3==finger)matrices[i]=bones[i].localToWorldMatrix*bind[i];}
 public void CaptureWristSearch(Transform wrist){
  for(int i=0;i<bones.Length;i++)searchBones[i]=bones[i]==wrist||bones[i].IsChildOf(wrist);
  foreach(int v in visibleVertices[left?1:0]){searchPoints[v]=World(v);var w=weights[v];searchMobility[v]=(searchBones[w.boneIndex0]?w.weight0:0)+(searchBones[w.boneIndex1]?w.weight1:0)+(searchBones[w.boneIndex2]?w.weight2:0)+(searchBones[w.boneIndex3]?w.weight3:0);}
 }
 public bool BlockedAtTranslation(SqueezableObject item,Vector3 delta,float padding){
  var bounds=item.VisibleSurfaceBounds;foreach(int v in visibleVertices[left?1:0]){var q=searchPoints[v]+delta*searchMobility[v];if(bounds.Contains(q)&&item.SkinBlocked(q,padding))return true;}return false;
 }
 public bool Blocked(SqueezableObject item,int finger,float padding){
  var bounds=item.VisibleSurfaceBounds;var vertices=finger<0?visibleVertices[left?1:0]:fingerVertices[left?1:0][finger];
  // Sweeps need a yes/no predicate, not the deepest point. Stop at the first
  // unsafe vertex; final acceptance still measures every skin vertex.
  foreach(int v in vertices){var q=World(v);if(bounds.Contains(q)&&item.SkinBlocked(q,padding))return true;}return false;
 }
 public float Gap(SqueezableObject item,int group,out Vector3 point,out Vector3 normal){
  point=normal=Vector3.zero;float gap=float.PositiveInfinity;
  var bounds=item.VisibleSurfaceBounds;float tolerance=item.handContactTolerance;
  foreach(int v in fullGroups[left?1:0][group]){var q=World(v);if(bounds.SqrDistance(q)>tolerance*tolerance)continue;float d=item.SkinSurfaceGap(q,out var p,out var n);if(d<gap){gap=d;point=p;normal=n;}}
  return gap;
 }
 public float Depth(SqueezableObject item,out Vector3 normal){
  return FullDepth(item,out normal);
 }
 Vector3 World(int v){var w=weights[v];var x=rest[v];return matrices[w.boneIndex0].MultiplyPoint3x4(x)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(x)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(x)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(x)*w.weight3;}
 public float FingerDepth(SqueezableObject item,int finger){
  float deepest=0;var bounds=item.VisibleSurfaceBounds;
  foreach(int v in fingerVertices[left?1:0][finger]){var q=World(v);if(!bounds.Contains(q))continue;float gap=item.SkinSurfaceGap(q,out _,out _);deepest=Mathf.Max(deepest,-gap);}return deepest;
 }
 public float FullDepth(SqueezableObject item,out Vector3 normal){
  return Deepest(item,out _,out normal,out _);
 }
 public float StrictDepth(SqueezableObject item)=>Deepest(item,out _,out _,out _,true);
 public float Deepest(SqueezableObject item,out Vector3 skinPoint,out Vector3 normal,out int group,bool forceParity=false){
  // A final all-vertex clearance closes gaps left by the angular search's
  // sparse skin samples. Broad-phase bounds reject most vertices cheaply.
  for(int i=0;i<bones.Length;i++)matrices[i]=bones[i].localToWorldMatrix*bind[i];
  var bounds=item.VisibleSurfaceBounds;float deepest=0;normal=skinPoint=Vector3.zero;group=-1;
  foreach(int v in visibleVertices[left?1:0]){var w=weights[v];var x=rest[v];
   var q=matrices[w.boneIndex0].MultiplyPoint3x4(x)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(x)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(x)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(x)*w.weight3;
   if(!bounds.Contains(q))continue;float gap=item.SkinSurfaceGap(q,out _,out var n,forceParity);if(-gap>deepest){deepest=-gap;normal=n;skinPoint=q;group=groupForVertex[v];}
  }return deepest;
 }
 public void Draw(){Gizmos.color=Color.cyan;foreach(var group in groups[left?1:0])foreach(int v in group)Gizmos.DrawSphere(points[v],.001f);}
}
}
