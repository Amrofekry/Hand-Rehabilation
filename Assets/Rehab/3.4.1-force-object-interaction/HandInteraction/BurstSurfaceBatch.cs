using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
namespace GripRehab {
public struct SurfaceQuerySample {public float gap;public Vector3 point,normal;}

// Exactly the same live triangles/BVH as MeshContactSurface, compiled with
// the project's existing Burst dependency. A synchronous job finishes before
// any SDK bone, object pose or deformation buffer can change. No worker ever
// accesses UnityEngine.Objects. Persistent buffers are owned by one object.
public sealed class BurstSurfaceBatch:IDisposable {
 NativeArray<MeshContactSurface.Branch> branches;
 NativeArray<int> order,indices;
 NativeArray<float> signs;
 NativeArray<Vector3> vertices,points,rayOrigins,rayU,rayV,rayT;
 NativeArray<byte> rayValid;
 NativeArray<SurfaceQuerySample> results;
 public static int LiveBatches{get;private set;}
 public BurstSurfaceBatch(int vertexCount,int triangleCount,int branchCount){
  vertices=new NativeArray<Vector3>(vertexCount,Allocator.Persistent);
  indices=new NativeArray<int>(triangleCount,Allocator.Persistent);order=new NativeArray<int>(triangleCount/3,Allocator.Persistent);signs=new NativeArray<float>(triangleCount/3,Allocator.Persistent);
  branches=new NativeArray<MeshContactSurface.Branch>(branchCount,Allocator.Persistent);
  points=new NativeArray<Vector3>(8192,Allocator.Persistent);results=new NativeArray<SurfaceQuerySample>(8192,Allocator.Persistent);
  int faces=triangleCount/3;rayOrigins=new NativeArray<Vector3>(faces,Allocator.Persistent);rayU=new NativeArray<Vector3>(faces,Allocator.Persistent);rayV=new NativeArray<Vector3>(faces,Allocator.Persistent);rayT=new NativeArray<Vector3>(faces,Allocator.Persistent);rayValid=new NativeArray<byte>(faces,Allocator.Persistent);LiveBatches++;
 }
 public void UpdateGeometry(Vector3[] v,int[] t,int[] o,float[] s,MeshContactSurface.Branch[] b,Vector3[] origins,Vector3[] u,Vector3[] w,Vector3[] distances,bool[] valid){
  vertices.CopyFrom(v);indices.CopyFrom(t);order.CopyFrom(o);signs.CopyFrom(s);branches.CopyFrom(b);rayOrigins.CopyFrom(origins);rayU.CopyFrom(u);rayV.CopyFrom(w);rayT.CopyFrom(distances);for(int i=0;i<valid.Length;i++)rayValid[i]=(byte)(valid[i]?1:0);
 }
 public NativeArray<SurfaceQuerySample> Query(Vector3[] query,int count,bool outside,float orientation){
  if(count<0||count>points.Length)throw new ArgumentOutOfRangeException(nameof(count));
  NativeArray<Vector3>.Copy(query,0,points,0,count);
  new QueryJob{branches=branches,order=order,indices=indices,signs=signs,vertices=vertices,points=points,rayOrigins=rayOrigins,rayU=rayU,rayV=rayV,rayT=rayT,rayValid=rayValid,results=results,count=count,outside=outside,orientation=orientation}.Run();
  return results;
 }
 public void Dispose(){
  if(!vertices.IsCreated)return;
  vertices.Dispose();indices.Dispose();order.Dispose();signs.Dispose();branches.Dispose();points.Dispose();results.Dispose();rayOrigins.Dispose();rayU.Dispose();rayV.Dispose();rayT.Dispose();rayValid.Dispose();LiveBatches--;
 }
 [BurstCompile(CompileSynchronously=true,FloatMode=FloatMode.Default,FloatPrecision=FloatPrecision.Standard)]
 struct QueryJob:IJob {
  [ReadOnly] public NativeArray<MeshContactSurface.Branch> branches;
  [ReadOnly] public NativeArray<int> order,indices;
  [ReadOnly] public NativeArray<float> signs;
  [ReadOnly] public NativeArray<Vector3> vertices,points,rayOrigins,rayU,rayV,rayT;
  [ReadOnly] public NativeArray<byte> rayValid;
  public NativeArray<SurfaceQuerySample> results;public int count;public bool outside;public float orientation;
  public void Execute(){
   int seed=-1;
   for(int q=0;q<count;q++){
    var p=points[q];bool inside=Inside(p);var sample=new SurfaceQuerySample{gap=float.PositiveInfinity};
    if(outside||inside){float best=float.PositiveInfinity;int face=-1;if(seed>=0)Face(seed,p,ref best,ref sample,ref face);
     var stack=new FixedList128Bytes<int>();stack.Add(0);
     while(stack.Length>0){int index=stack[stack.Length-1];stack.Length--;var b=branches[index];if(Distance(b,p)>best)continue;
      if(b.left<0){for(int i=b.start;i<b.start+b.count;i++)Face(order[i],p,ref best,ref sample,ref face);}
      else{int a=b.left,c=b.right;if(Distance(branches[c],p)<Distance(branches[a],p)){a=b.right;c=b.left;}stack.Add(c);stack.Add(a);}
     }
     if(face>=0){float distance=Mathf.Sqrt(best);sample.gap=inside?-distance:distance;if(distance>1e-7f)sample.normal=(p-sample.point)*(inside?-1:1)/distance;seed=face;}
    }results[q]=sample;
   }
  }
  void Face(int t,Vector3 p,ref float best,ref SurfaceQuerySample sample,ref int face){
   var a=vertices[indices[t]];var b=vertices[indices[t+1]];var c=vertices[indices[t+2]];var normal=Vector3.Cross(b-a,c-a);float squared=normal.sqrMagnitude;if(squared<1e-24f)return;
   var point=Triangle(p,a,b,c);float d=(p-point).sqrMagnitude;if(d<best){best=d;sample.point=point;sample.normal=normal*(signs[t/3]*orientation/Mathf.Sqrt(squared));face=t;}
  }
  bool Inside(Vector3 p){
   var root=branches[0];if(p.x<root.min.x||p.x>root.max.x||p.y<root.min.y||p.y>root.max.y||p.z<root.min.z||p.z>root.max.z)return false;
   int hits=0;var stack=new FixedList128Bytes<int>();stack.Add(0);
   while(stack.Length>0){int index=stack[stack.Length-1];stack.Length--;var b=branches[index];if(!RayBounds(b,p))continue;
    if(b.left>=0){stack.Add(b.left);stack.Add(b.right);continue;}
    for(int i=b.start;i<b.start+b.count;i++){int f=order[i]/3;if(rayValid[f]==0)continue;var q=p-rayOrigins[f];float u=Vector3.Dot(q,rayU[f]);if(u<0||u>1)continue;float w=Vector3.Dot(q,rayV[f]);if(w<0||u+w>1)continue;if(Vector3.Dot(q,rayT[f])>1e-7f)hits++;}
   }return (hits&1)!=0;
  }
  static bool RayBounds(MeshContactSurface.Branch b,Vector3 p){float lo=Mathf.Max(0,(b.min.x-p.x)*(1/.923f)),hi=(b.max.x-p.x)*(1/.923f);lo=Mathf.Max(lo,(b.min.y-p.y)*(1/.331f));hi=Mathf.Min(hi,(b.max.y-p.y)*(1/.331f));lo=Mathf.Max(lo,(b.min.z-p.z)*(1/.193f));hi=Mathf.Min(hi,(b.max.z-p.z)*(1/.193f));return hi>=lo;}
  static float Distance(MeshContactSurface.Branch b,Vector3 p){float x=Mathf.Max(0,Mathf.Max(b.min.x-p.x,p.x-b.max.x)),y=Mathf.Max(0,Mathf.Max(b.min.y-p.y,p.y-b.max.y)),z=Mathf.Max(0,Mathf.Max(b.min.z-p.z,p.z-b.max.z));return x*x+y*y+z*z;}
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
}
