using System;
using System.Collections.Generic;
using UnityEngine;
namespace GripRehab {
// Smooth round materials across duplicated UV-seam vertices without welding
// the visible mesh or changing its triangles/UVs. All buffers are cached once.
public sealed class DeformationNormals {
 readonly int[] groups,triangles;readonly Vector3[] sums,normals;
 public DeformationNormals(Mesh mesh,Vector3[] rest){
  groups=new int[rest.Length];normals=new Vector3[rest.Length];triangles=mesh.triangles;
  var map=new Dictionary<Vector3Int,int>();
  for(int i=0;i<rest.Length;i++){
   var p=rest[i];var key=new Vector3Int(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.y*100000),Mathf.RoundToInt(p.z*100000));
   if(!map.TryGetValue(key,out int group)){group=map.Count;map.Add(key,group);}groups[i]=group;
  }
  sums=new Vector3[map.Count];
 }
 public void Update(Mesh mesh,Vector3[] positions){
  Array.Clear(sums,0,sums.Length);
  for(int t=0;t<triangles.Length;t+=3){int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
   var n=Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]);
   sums[groups[a]]+=n;sums[groups[b]]+=n;sums[groups[c]]+=n;
  }
  for(int i=0;i<normals.Length;i++)normals[i]=sums[groups[i]].normalized;
  mesh.SetNormals(normals);
 }
}
}
