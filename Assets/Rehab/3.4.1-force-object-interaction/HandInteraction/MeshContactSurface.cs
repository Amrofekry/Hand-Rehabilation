using System;
using System.Collections.Generic;
using UnityEngine;
namespace GripRehab {
// Exact closest points on the full live triangle surface, accelerated by a BVH.
// Uses cached topology and the deformation backend's live vertices; no cooking,
// convex-hull conversion, or per-frame mesh.vertices allocation.
public sealed class MeshContactSurface {
 readonly int[] triangles;readonly float[] normalSigns;readonly int[][] adjacent;readonly Transform visual;
 readonly bool[] closedFaces;bool lastFaceInterior;int lastFace=-1;
 public bool FaceSignReliable=>lastFace>=0&&lastFaceInterior&&closedFaces[lastFace/3];
 public struct Branch {public int start,count,left,right;public Vector3 min,max;}
 readonly int[] order;readonly Branch[] tree;int treeCount;
 readonly Vector3[] rayOrigins,rayU,rayV,rayT;readonly bool[] rayValid;
 readonly Vector3[] worldVertices;Matrix4x4 cachedMatrix;bool dirty=true;float orientationSign=1;
 BurstSurfaceBatch batch;bool batchDirty=true;
 int preparedFrame=-1;
 public void Invalidate(){dirty=true;preparedFrame=-1;}
 public MeshContactSurface(Mesh mesh,Transform visual){
  this.visual=visual;worldVertices=new Vector3[mesh.vertexCount];triangles=mesh.triangles;int faces=triangles.Length/3;rayOrigins=new Vector3[faces];rayU=new Vector3[faces];rayV=new Vector3[faces];rayT=new Vector3[faces];rayValid=new bool[faces];var lists=new List<int>[mesh.vertexCount];for(int i=0;i<lists.Length;i++)lists[i]=new List<int>();
  for(int t=0;t<triangles.Length;t+=3)for(int k=0;k<3;k++)lists[triangles[t+k]].Add(t);
  // UV/normal seams duplicate a vertex. A nearest-vertex tie must still see
  // every adjacent face, otherwise a box face can resolve to its neighbour.
  var positions=mesh.vertices;order=new int[triangles.Length/3];for(int i=0;i<order.Length;i++)order[i]=i*3;tree=new Branch[order.Length*2];Build(positions,0,order.Length);var welded=new Dictionary<Vector3,List<int>>();
  for(int i=0;i<positions.Length;i++){if(!welded.TryGetValue(positions[i],out var all)){all=new List<int>();welded.Add(positions[i],all);}foreach(int t in lists[i])if(!all.Contains(t))all.Add(t);}
  adjacent=new int[lists.Length][];for(int i=0;i<lists.Length;i++)adjacent[i]=welded[positions[i]].ToArray();
  // Closed components can have reversed winding (the existing cup handle).
  // Correct query normals per connected component, keeping source geometry,
  // triangle order, UVs and material assignments unchanged.
  normalSigns=new float[triangles.Length/3];closedFaces=new bool[normalSigns.Length];var seen=new bool[normalSigns.Length];var pending=new Stack<int>();var component=new List<int>();
  var ids=new int[positions.Length];var unique=new Dictionary<Vector3Int,int>();for(int i=0;i<ids.Length;i++){var p=positions[i];var key=new Vector3Int(Mathf.RoundToInt(p.x*1e6f),Mathf.RoundToInt(p.y*1e6f),Mathf.RoundToInt(p.z*1e6f));if(!unique.TryGetValue(key,out int id)){id=unique.Count;unique.Add(key,id);}ids[i]=id;}
  for(int first=0;first<triangles.Length;first+=3){if(seen[first/3])continue;pending.Push(first);seen[first/3]=true;component.Clear();double volume=0;
   while(pending.Count>0){int t=pending.Pop();component.Add(t);volume+=Vector3.Dot(positions[triangles[t]],Vector3.Cross(positions[triangles[t+1]],positions[triangles[t+2]]));
    for(int k=0;k<3;k++)foreach(int other in adjacent[triangles[t+k]])if(!seen[other/3]){seen[other/3]=true;pending.Push(other);}}
   var edges=new Dictionary<(int,int),int>();foreach(int t in component){if(ids[triangles[t]]==ids[triangles[t+1]]||ids[triangles[t+1]]==ids[triangles[t+2]]||ids[triangles[t+2]]==ids[triangles[t]])continue;for(int k=0;k<3;k++){int a=ids[triangles[t+k]],b=ids[triangles[t+(k+1)%3]];var key=a<b?(a,b):(b,a);edges.TryGetValue(key,out int count);edges[key]=count+1;}}
   bool closed=Math.Abs(volume)>1e-12;foreach(var edge in edges)if(edge.Value!=2)closed=false;
   foreach(int t in component){normalSigns[t/3]=volume<0?-1:1;closedFaces[t/3]=closed;}
  }
 }
 public bool Closest(Vector3 world,Vector3[] vertices,out Vector3 point,out Vector3 normal){
  point=normal=Vector3.zero;if(vertices==null||vertices.Length!=adjacent.Length||!HandRigRules.Finite(world))return false;
  Prepare(vertices);return ClosestPrepared(world,out point,out normal);
 }
 public bool ClosestPrepared(Vector3 world,out Vector3 point,out Vector3 normal){
  point=normal=Vector3.zero;
  float best=float.PositiveInfinity;int seed=lastFace;lastFace=-1;lastFaceInterior=false;
  // Adjacent skin queries are spatially coherent. Seed with the last exact
  // triangle, then still traverse the complete BVH against that upper bound.
  // This never restricts the result to a vertex neighbourhood or convex hull.
  if(seed>=0)EvaluateFace(seed,world,ref best,ref point,ref normal);
  ClosestTriangle(0,world,ref best,ref point,ref normal);
  return float.IsFinite(best)&&HandRigRules.Finite(normal)&&normal.sqrMagnitude>.5f;
 }
 public void Prepare(Vector3[] vertices){
  // Unity's Transform matrix getter crosses into native code. A surface
  // search can call it thousands of times without moving the object.
  // Runtime mutations explicitly invalidate; Editor geometry tests retain
  // immediate matrix checks even within a single editor frame.
  if(Application.isPlaying&&!dirty&&preparedFrame==Time.frameCount)return;
  preparedFrame=Time.frameCount;
  var matrix=visual.localToWorldMatrix;
  if(dirty||matrix!=cachedMatrix){for(int i=0;i<vertices.Length;i++)worldVertices[i]=matrix.MultiplyPoint3x4(vertices[i]);cachedMatrix=matrix;orientationSign=matrix.determinant<0?-1:1;dirty=false;Refit(0);PrepareParity();batchDirty=true;}
 }
 public Unity.Collections.NativeArray<SurfaceQuerySample> QueryPrepared(Vector3[] points,int count,bool outside){
  if(batch==null)batch=new BurstSurfaceBatch(worldVertices.Length,triangles.Length,tree.Length);
  if(batchDirty){batch.UpdateGeometry(worldVertices,triangles,order,normalSigns,tree,rayOrigins,rayU,rayV,rayT,rayValid);batchDirty=false;}
  return batch.Query(points,count,outside,orientationSign);
 }
 public void Dispose(){batch?.Dispose();batch=null;}
 static Vector3 Centre(Vector3[] p,int[] t,int i)=>(p[t[i]]+p[t[i+1]]+p[t[i+2]])/3;
 int Build(Vector3[] positions,int start,int count){
  int index=treeCount++;var branch=new Branch{start=start,count=count,left=-1,right=-1};
  if(count>8){var bounds=new Bounds(Centre(positions,triangles,order[start]),Vector3.zero);
   for(int i=start+1;i<start+count;i++)bounds.Encapsulate(Centre(positions,triangles,order[i]));
   int axis=bounds.size.x>=bounds.size.y&&bounds.size.x>=bounds.size.z?0:bounds.size.y>=bounds.size.z?1:2;
   Array.Sort(order,start,count,Comparer<int>.Create((a,b)=>Centre(positions,triangles,a)[axis].CompareTo(Centre(positions,triangles,b)[axis])));
   int half=count/2;branch.left=Build(positions,start,half);branch.right=Build(positions,start+half,count-half);
  }tree[index]=branch;return index;
 }
 void Refit(int index){var b=tree[index];
  if(b.left<0){b.min=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);b.max=-b.min;
   for(int i=b.start;i<b.start+b.count;i++)for(int j=0;j<3;j++){var p=worldVertices[triangles[order[i]+j]];b.min=Vector3.Min(b.min,p);b.max=Vector3.Max(b.max,p);}
  }else{Refit(b.left);Refit(b.right);b.min=Vector3.Min(tree[b.left].min,tree[b.right].min);b.max=Vector3.Max(tree[b.left].max,tree[b.right].max);}tree[index]=b;
 }
 static float Distance(Branch b,Vector3 p){float x=Mathf.Max(0,Mathf.Max(b.min.x-p.x,p.x-b.max.x)),y=Mathf.Max(0,Mathf.Max(b.min.y-p.y,p.y-b.max.y)),z=Mathf.Max(0,Mathf.Max(b.min.z-p.z,p.z-b.max.z));return x*x+y*y+z*z;}
 void ClosestTriangle(int index,Vector3 world,ref float best,ref Vector3 point,ref Vector3 normal){var branch=tree[index];if(Distance(branch,world)>best)return;
  if(branch.left<0){for(int i=branch.start;i<branch.start+branch.count;i++){
   EvaluateFace(order[i],world,ref best,ref point,ref normal);
  }}else{int a=branch.left,b=branch.right;if(Distance(tree[b],world)<Distance(tree[a],world)){a=branch.right;b=branch.left;}
   ClosestTriangle(a,world,ref best,ref point,ref normal);ClosestTriangle(b,world,ref best,ref point,ref normal);
  }
 }
 void EvaluateFace(int t,Vector3 world,ref float best,ref Vector3 point,ref Vector3 normal){
   var a=worldVertices[triangles[t]];var b=worldVertices[triangles[t+1]];var c=worldVertices[triangles[t+2]];
   var n=Vector3.Cross(b-a,c-a);if(n.sqrMagnitude<1e-24f)return;
   var p=Triangle(world,a,b,c);float d=(p-world).sqrMagnitude;
   if(d<best){best=d;point=p;normal=n*(normalSigns[t/3]*orientationSign/Mathf.Sqrt(n.sqrMagnitude));lastFace=t;
    var ab=b-a;var ac=c-a;var ap=p-a;float aa=Vector3.Dot(ab,ab),cc=Vector3.Dot(ac,ac),bc=Vector3.Dot(ab,ac),pa=Vector3.Dot(ap,ab),pc=Vector3.Dot(ap,ac),denominator=aa*cc-bc*bc;
    float u=(cc*pa-bc*pc)/denominator,v=(aa*pc-bc*pa)/denominator;lastFaceInterior=u>1e-5f&&v>1e-5f&&u+v<1-1e-5f;
   }
 }
 // A closest triangle normal alone cannot classify a point near a folded
 // or concave edge. Use ray parity only for candidate interior points.
 public bool Inside(Vector3 p){
  if(treeCount==0||!HandRigRules.Finite(p))return false;
  var b=tree[0];if(p.x<b.min.x||p.x>b.max.x||p.y<b.min.y||p.y>b.max.y||p.z<b.min.z||p.z>b.max.z)return false;
  return (Crossings(0,p,new Vector3(.923f,.331f,.193f))&1)!=0;
 }
 static bool RayBounds(Branch b,Vector3 p,Vector3 d){
  // The parity ray has fixed positive components. Scalar slab tests avoid
  // Vector3 indexers and repeated divisions at every BVH branch.
  float lo=Mathf.Max(0,(b.min.x-p.x)*(1/.923f)),hi=(b.max.x-p.x)*(1/.923f);
  lo=Mathf.Max(lo,(b.min.y-p.y)*(1/.331f));hi=Mathf.Min(hi,(b.max.y-p.y)*(1/.331f));
  lo=Mathf.Max(lo,(b.min.z-p.z)*(1/.193f));hi=Mathf.Min(hi,(b.max.z-p.z)*(1/.193f));return hi>=lo;
 }
 // Precompute the fixed parity ray's triangle coefficients whenever the
 // actual mesh/pose changes. Query results remain exact Moller-Trumbore
 // intersections; only redundant cross products/divisions are removed.
 void PrepareParity(){
  var d=new Vector3(.923f,.331f,.193f);
  for(int t=0;t<triangles.Length;t+=3){int f=t/3;var a=worldVertices[triangles[t]];var e1=worldVertices[triangles[t+1]]-a;var e2=worldVertices[triangles[t+2]]-a;var h=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,h);rayOrigins[f]=a;rayValid[f]=Mathf.Abs(det)>=1e-16f;
   if(!rayValid[f])continue;rayU[f]=h/det;rayV[f]=Vector3.Cross(e1,d)/det;rayT[f]=Vector3.Cross(e1,e2)/det;
  }
 }
 int Crossings(int index,Vector3 p,Vector3 d){var b=tree[index];if(!RayBounds(b,p,d))return 0;
  if(b.left>=0)return Crossings(b.left,p,d)+Crossings(b.right,p,d);
  int hits=0;for(int i=b.start;i<b.start+b.count;i++){int f=order[i]/3;if(!rayValid[f])continue;var q=p-rayOrigins[f];float u=Vector3.Dot(q,rayU[f]);if(u<0||u>1)continue;float w=Vector3.Dot(q,rayV[f]);if(w<0||u+w>1)continue;if(Vector3.Dot(q,rayT[f])>1e-7f)hits++;}return hits;
 }
 // Ericson closest-point regions, implemented locally; degenerate edges safe.
 static Vector3 Triangle(Vector3 p,Vector3 a,Vector3 b,Vector3 c){
  var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
  var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
  float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/Mathf.Max(1e-12f,d1-d3));
  var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
  float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/Mathf.Max(1e-12f,d2-d6));
  float va=d3*d6-d5*d4;if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0)return b+(c-b)*((d4-d3)/Mathf.Max(1e-12f,(d4-d3)+(d5-d6)));
  float sum=va+vb+vc;if(Mathf.Abs(sum)<1e-24f)return a;return a+ab*(vb/sum)+ac*(vc/sum);
 }
}
}
