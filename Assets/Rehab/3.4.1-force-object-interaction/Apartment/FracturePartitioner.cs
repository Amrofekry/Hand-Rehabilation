using System;
using System.Collections.Generic;
using UnityEngine;
namespace GripRehab {
public sealed class FractureChunk {
 public Vector3[] vertices;public int[] triangles,sourceIndices;public Vector3 center;
}
public sealed class FractureLayout {
 public FractureChunk[] chunks;public int[] trianglePieces;
}
// Precomputed surface pieces; preserves all source triangles without modifying the source mesh.
public static class FracturePartitioner {
 public static FractureLayout Split(Vector3[] vertices,int[] triangles,int requestedPieces){
  if(vertices==null||triangles==null||vertices.Length==0||triangles.Length==0||triangles.Length%3!=0||
     Array.Exists(vertices,v=>!GraspMotionPolicy.Finite(v)))throw new ArgumentException("Valid finite indexed mesh required");
  foreach(int index in triangles)if(index<0||index>=vertices.Length)throw new ArgumentException("Triangle index out of bounds");
  int count=Math.Min(Math.Max(1,requestedPieces),Math.Min(24,triangles.Length/3));
  var centroids=new Vector3[triangles.Length/3];
  for(int i=0;i<centroids.Length;i++)centroids[i]=(vertices[triangles[i*3]]+vertices[triangles[i*3+1]]+vertices[triangles[i*3+2]])/3;
  if(Array.Exists(centroids,v=>!GraspMotionPolicy.Finite(v)))throw new ArgumentException("Invalid triangle centroid");
  var seeds=new Vector3[count];seeds[0]=centroids[0];
  for(int s=1;s<count;s++){
   float far=-1;int best=0;
   for(int i=0;i<centroids.Length;i++){
    float closest=float.PositiveInfinity;
    for(int j=0;j<s;j++)closest=Math.Min(closest,(centroids[i]-seeds[j]).sqrMagnitude);
    if(closest>far){far=closest;best=i;}
   }
   seeds[s]=centroids[best];
  }
  var groups=new List<int>[count];for(int i=0;i<count;i++)groups[i]=new List<int>();
  var labels=new int[centroids.Length];
  for(int i=0;i<centroids.Length;i++){
   float best=float.PositiveInfinity;int selected=0;
   for(int j=0;j<count;j++){float distance=(centroids[i]-seeds[j]).sqrMagnitude;if(distance<best){best=distance;selected=j;}}
   labels[i]=selected;
   for(int k=0;k<3;k++)groups[selected].Add(triangles[i*3+k]);
  }
  var chunks=new List<FractureChunk>();var remap=new int[count];
  for(int p=0;p<count;p++){
   if(groups[p].Count==0){remap[p]=-1;continue;}
   remap[p]=chunks.Count;var map=new Dictionary<int,int>();var indices=new List<int>();var points=new List<Vector3>();var local=new List<int>();
   foreach(int source in groups[p]){
    int index;if(!map.TryGetValue(source,out index)){index=points.Count;map.Add(source,index);indices.Add(source);points.Add(vertices[source]);}
    local.Add(index);
   }
   Vector3 center=new Vector3();foreach(var point in points)center+=point;center/=points.Count;
   for(int i=0;i<points.Count;i++)points[i]-=center;
   chunks.Add(new FractureChunk{vertices=points.ToArray(),triangles=local.ToArray(),sourceIndices=indices.ToArray(),center=center});
  }
  for(int i=0;i<labels.Length;i++)labels[i]=remap[labels[i]];
  return new FractureLayout{chunks=chunks.ToArray(),trianglePieces=labels};
 }
}
}
