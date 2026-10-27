using System;
using System.Collections.Generic;
using UnityEngine;
namespace GripRehab {
/// <summary>
/// Stage 10 cutting: splits a closed mesh with a plane into two closed pieces (the cut face is filled with a fan
/// from the centre of the section, exact for the convex cross-sections of the vegetables used). A geometric
/// approximation of a knife cut, not a material or fracture simulation.
/// </summary>
public static class PlaneCutter {
 public sealed class Piece { public readonly List<Vector3> vertices=new List<Vector3>();public readonly List<int> triangles=new List<int>(); }

 /// <summary>Splits (vertices, triangles) by the plane through point with normal. above gets the side the normal points to.</summary>
 public static bool Split(Vector3[] vertices,int[] triangles,Vector3 point,Vector3 normal,out Piece above,out Piece below){
  above=new Piece();below=new Piece();
  if(vertices==null||triangles==null||triangles.Length%3!=0||!GraspMotionPolicy.Finite(point)||!GraspMotionPolicy.Finite(normal)||normal.sqrMagnitude<1e-12f)return false;
  normal=normal.normalized;
  // Which way do this mesh's triangle cross products face (outward or inward)? Caps must follow the same rule.
  float winding=DeformationDynamics.Volume(vertices,triangles)>=0?1:-1;
  var d=new float[vertices.Length];
  for(int i=0;i<vertices.Length;i++){if(!GraspMotionPolicy.Finite(vertices[i]))return false;d[i]=Vector3.Dot(vertices[i]-point,normal);}
  var mapA=new Dictionary<int,int>();var mapB=new Dictionary<int,int>();
  var edgeA=new Dictionary<long,int>();var edgeB=new Dictionary<long,int>();
  var section=new List<Vector3>();var sectionKeys=new HashSet<long>();
  int V(Piece p,Dictionary<int,int> map,int i){if(!map.TryGetValue(i,out int k)){k=p.vertices.Count;p.vertices.Add(vertices[i]);map[i]=k;}return k;}
  int E(Piece p,Dictionary<long,int> cache,int a,int b){
   long key=a<b?((long)a<<32)|(uint)b:((long)b<<32)|(uint)a;
   if(!cache.TryGetValue(key,out int k)){
    float t=d[a]/(d[a]-d[b]);var x=Vector3.Lerp(vertices[a],vertices[b],t);
    k=p.vertices.Count;p.vertices.Add(x);cache[key]=k;
    if(sectionKeys.Add(key))section.Add(x);
   }
   return k;
  }
  for(int t=0;t<triangles.Length;t+=3){
   int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
   if(a<0||b<0||c<0||a>=vertices.Length||b>=vertices.Length||c>=vertices.Length)return false;
   bool pa=d[a]>=0,pb=d[b]>=0,pc=d[c]>=0;
   if(pa&&pb&&pc){above.triangles.AddRange(new[]{V(above,mapA,a),V(above,mapA,b),V(above,mapA,c)});continue;}
   if(!pa&&!pb&&!pc){below.triangles.AddRange(new[]{V(below,mapB,a),V(below,mapB,b),V(below,mapB,c)});continue;}
   // Rotate so that 'a' is the lone vertex on its side, keeping the winding.
   if(pa==pb){int s=a;a=c;c=b;b=s;}else if(pa==pc){int s=a;a=b;b=c;c=s;}
   bool loneAbove=d[a]>=0;
   var lone=loneAbove?above:below;var pair=loneAbove?below:above;
   var lm=loneAbove?mapA:mapB;var pm=loneAbove?mapB:mapA;var le=loneAbove?edgeA:edgeB;var pe=loneAbove?edgeB:edgeA;
   int ab1=E(lone,le,a,b),ac1=E(lone,le,a,c),ab2=E(pair,pe,a,b),ac2=E(pair,pe,a,c);
   lone.triangles.AddRange(new[]{V(lone,lm,a),ab1,ac1});
   int pb2=V(pair,pm,b),pc2=V(pair,pm,c);
   pair.triangles.AddRange(new[]{ab2,pb2,pc2,ab2,pc2,ac2});
  }
  if(above.triangles.Count==0||below.triangles.Count==0)return false;
  Cap(above,section,-normal,winding);Cap(below,section,normal,winding);
  return true;
 }
 // Fan over the section points sorted by angle around their centre; triangles face 'outward'.
 static void Cap(Piece piece,List<Vector3> section,Vector3 outward,float winding){
  if(section.Count<3)return;
  var centre=Vector3.zero;foreach(var p in section)centre+=p;centre/=section.Count;
  var u=Vector3.Cross(outward,Mathf.Abs(outward.y)<.9f?Vector3.up:Vector3.right).normalized;var w=Vector3.Cross(outward,u);
  var order=new List<Vector3>(section);
  order.Sort((p,q)=>Math.Atan2(Vector3.Dot(p-centre,w),Vector3.Dot(p-centre,u)).CompareTo(Math.Atan2(Vector3.Dot(q-centre,w),Vector3.Dot(q-centre,u))));
  int c=piece.vertices.Count;piece.vertices.Add(centre);int start=piece.vertices.Count;piece.vertices.AddRange(order);
  for(int i=0;i<order.Count;i++){
   int a=start+i,b=start+(i+1)%order.Count;
   var n=Vector3.Cross(piece.vertices[a]-centre,piece.vertices[b]-centre);
   if(Vector3.Dot(n,outward)*winding<0){int s=a;a=b;b=s;}
   piece.triangles.AddRange(new[]{c,a,b});
  }
 }
}
}
