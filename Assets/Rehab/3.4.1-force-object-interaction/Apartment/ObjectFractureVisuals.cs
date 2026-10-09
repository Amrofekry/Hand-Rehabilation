using System;
using System.Collections.Generic;
using UnityEngine;
namespace GripRehab {
// Allocated once in Awake; generated piece meshes belong to this object only.
public sealed class ObjectFractureVisuals:IDisposable {
 public GameObject Root{get;private set;}
 public bool DamageVisible=>cracks!=null&&cracks.activeInHierarchy;
 readonly List<Mesh> meshes=new List<Mesh>();
 GameObject cracks;Material crackMaterial;
 public static ObjectFractureVisuals Create(Transform owner,MeshFilter source,Renderer renderer,float mass){
  if(source==null||renderer==null||source.sharedMesh==null||!source.sharedMesh.isReadable||source.sharedMesh.subMeshCount!=1)return null;
  var original=source.sharedMesh;var vertices=original.vertices;var triangles=original.triangles;
  if(vertices.Length==0||triangles.Length==0)return null;
  var basis=Quaternion.Inverse(owner.rotation);
  for(int i=0;i<vertices.Length;i++)vertices[i]=basis*(source.transform.TransformPoint(vertices[i])-owner.position);
  FractureLayout layout;
  try{layout=FracturePartitioner.Split(vertices,triangles,12);}catch(ArgumentException){return null;}

  var output=new ObjectFractureVisuals();
  output.Root=new GameObject(owner.name+" surface fragments");output.Root.SetActive(false);output.Root.transform.SetPositionAndRotation(owner.position,owner.rotation);
  float pieceMass=Mathf.Max(.001f,mass/Mathf.Max(1,layout.chunks.Length));
  var uv=original.uv;var sourceNormals=original.normals;
  var seams=new List<Vector3>();var seamIndices=new List<int>();
  var patches=ClipSurface(vertices,triangles,uv,sourceNormals,source.transform,basis,layout,seams,seamIndices);
  foreach(var mesh in patches){
   var go=new GameObject("Surface shard",typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider),typeof(Rigidbody));
   go.transform.SetParent(output.Root.transform,false);go.transform.localPosition=mesh.bounds.center;
   var points=mesh.vertices;var center=mesh.bounds.center;for(int i=0;i<points.Length;i++)points[i]-=center;
   mesh.vertices=points;mesh.RecalculateBounds();output.meshes.Add(mesh);
   // Cups already contain both wall surfaces. Extruding a duplicate layer
   // through their thin wall introduces overlapping surfaces.
   if(owner.GetComponent<SqueezableObject>()?.profile?.hollowCup!=true)CloseSurfaceChip(mesh,.0015f);
   go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
   var collider=go.GetComponent<BoxCollider>();collider.center=mesh.bounds.center;
   var size=mesh.bounds.size;collider.size=new Vector3(Mathf.Max(.003f,size.x),Mathf.Max(.003f,size.y),Mathf.Max(.003f,size.z));
   var body=go.GetComponent<Rigidbody>();body.isKinematic=true;body.useGravity=true;body.mass=pieceMass;
   body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
  }
  // Touching debris must not explode because neighboring chip bounds overlap.
  var colliders=output.Root.GetComponentsInChildren<Collider>(true);
  for(int i=0;i<colliders.Length;i++)for(int j=i+1;j<colliders.Length;j++)Physics.IgnoreCollision(colliders[i],colliders[j],true);
  output.BuildCracks(owner,seams,seamIndices,renderer.sharedMaterial.shader);
  output.Root.SetActive(false);return output;
 }
 // The old fragments were zero-thickness open sheets. Give each source-shaped
 // surface patch a back face and boundary walls so rotating chips stay visible.
 static void CloseSurfaceChip(Mesh mesh,float thickness){
  var original=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;var triangles=mesh.triangles;
  var weld=WeldPositions(original);var smooth=new Vector3[original.Length];
  for(int i=0;i<original.Length;i++)smooth[weld[i]]+=normals[i];
  for(int i=0;i<original.Length;i++)normals[i]=smooth[weld[i]].normalized;
  var vertices=new Vector3[original.Length*2];var tex=new Vector2[vertices.Length];
  Array.Copy(original,vertices,original.Length);
  for(int i=0;i<original.Length;i++){
   vertices[i+original.Length]=original[i]-normals[i]*thickness;
   if(uv.Length==original.Length)tex[i]=tex[i+original.Length]=uv[i];
  }
  var indices=new List<int>(triangles);var counts=new Dictionary<long,int>();var direction=new Dictionary<long,Vector2Int>();
  for(int t=0;t<triangles.Length;t+=3){
   int a=triangles[t],b=triangles[t+1],c=triangles[t+2],n=original.Length;
   indices.AddRange(new[]{c+n,b+n,a+n});
   for(int k=0;k<3;k++){int x=triangles[t+k],y=triangles[t+(k+1)%3],gx=weld[x],gy=weld[y];long key=((long)Math.Min(gx,gy)<<32)|(uint)Math.Max(gx,gy);
    counts[key]=counts.TryGetValue(key,out int count)?count+1:1;direction[key]=new Vector2Int(x,y);}
  }
  foreach(var edge in counts)if(edge.Value==1){var p=direction[edge.Key];int n=original.Length;
   indices.AddRange(new[]{p.x,p.x+n,p.y,p.y,p.x+n,p.y+n});}
  mesh.Clear();mesh.vertices=vertices;mesh.uv=tex;mesh.SetTriangles(indices,0);mesh.RecalculateNormals();
  var finalNormals=mesh.normals;
  for(int i=0;i<original.Length;i++){finalNormals[i]=normals[i];finalNormals[i+original.Length]=-normals[i];}
  mesh.normals=finalNormals;mesh.RecalculateBounds();
 }
 static int[] WeldPositions(Vector3[] points){
  var groups=new int[points.Length];var map=new Dictionary<Vector3Int,int>();
  for(int i=0;i<points.Length;i++){var p=points[i];var key=new Vector3Int(Mathf.RoundToInt(p.x*1000000),Mathf.RoundToInt(p.y*1000000),Mathf.RoundToInt(p.z*1000000));
   if(!map.TryGetValue(key,out int group)){group=map.Count;map.Add(key,group);}groups[i]=group;}
  return groups;
 }
 struct CutVertex {
  public Vector3 point,normal;public Vector2 uv;
  public static CutVertex Mix(CutVertex a,CutVertex b,float t)=>new CutVertex{point=Vector3.LerpUnclamped(a.point,b.point,t),normal=Vector3.LerpUnclamped(a.normal,b.normal,t).normalized,uv=Vector2.LerpUnclamped(a.uv,b.uv,t)};
 }
 // Clip every source triangle against the same Voronoi half-spaces. Whole-
 // triangle assignment left comb-like cracks on long, narrow cup triangles.
 // Shared cut planes retain continuous source geometry and interpolated UVs.
 // All meshes and cuts are precomputed once, never during pressure/fracture.
 static List<Mesh> ClipSurface(Vector3[] vertices,int[] triangles,Vector2[] uv,Vector3[] normals,Transform source,Quaternion basis,FractureLayout layout,List<Vector3> seams,List<int> seamIndices){
  var output=new List<Mesh>();var polygon=new List<CutVertex>(16);var scratch=new List<CutVertex>(16);
  for(int cell=0;cell<layout.chunks.Length;cell++){
   var points=new List<Vector3>();var tex=new List<Vector2>();var smooth=new List<Vector3>();var indices=new List<int>();
   var seed=layout.chunks[cell].center;
   for(int t=0;t<triangles.Length;t+=3){
    polygon.Clear();for(int j=0;j<3;j++){int v=triangles[t+j];polygon.Add(new CutVertex{point=vertices[v],uv=uv.Length==vertices.Length?uv[v]:Vector2.zero,normal=normals.Length==vertices.Length?(basis*source.TransformDirection(normals[v])).normalized:Vector3.up});}
    for(int other=0;other<layout.chunks.Length&&polygon.Count>=3;other++){
     if(other==cell)continue;var n=layout.chunks[other].center-seed;if(n.sqrMagnitude<1e-12f)continue;
     n.Normalize();var middle=(seed+layout.chunks[other].center)*.5f;scratch.Clear();
     var prior=polygon[polygon.Count-1];float d0=Vector3.Dot(prior.point-middle,n);
     foreach(var current in polygon){float d1=Vector3.Dot(current.point-middle,n);
      if((d0<=0)!=(d1<=0))scratch.Add(CutVertex.Mix(prior,current,d0/(d0-d1)));
      if(d1<=0)scratch.Add(current);prior=current;d0=d1;
     }
     var swap=polygon;polygon=scratch;scratch=swap;
    }
    if(polygon.Count<3)continue;
    int first=points.Count;foreach(var v in polygon){points.Add(v.point);tex.Add(v.uv);smooth.Add(v.normal);}
    for(int j=1;j<polygon.Count-1;j++){indices.Add(first);indices.Add(first+j);indices.Add(first+j+1);}
    for(int j=0;j<polygon.Count;j++){
     var a=polygon[j];var b=polygon[(j+1)%polygon.Count];bool cut=false;
     for(int other=0;other<layout.chunks.Length&&!cut;other++)if(other!=cell){var n=layout.chunks[other].center-seed;if(n.sqrMagnitude<1e-12f)continue;n.Normalize();var middle=(seed+layout.chunks[other].center)*.5f;
      cut=Mathf.Abs(Vector3.Dot(a.point-middle,n))<1e-6f&&Mathf.Abs(Vector3.Dot(b.point-middle,n))<1e-6f;
     }
     if(!cut||(b.point-a.point).sqrMagnitude<1e-12f)continue;
     var normal=(a.normal+b.normal).normalized;var side=Vector3.Cross(normal,b.point-a.point).normalized*.0004f;var lift=normal*.0003f;
     int k=seams.Count;seams.Add(a.point-side+lift);seams.Add(a.point+side+lift);seams.Add(b.point-side+lift);seams.Add(b.point+side+lift);
     seamIndices.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3,k+1,k+2,k,k+3,k+2,k+1});
    }
   }
   if(indices.Count==0)continue;
   var mesh=new Mesh{name="Precomputed clipped surface shard"};mesh.SetVertices(points);mesh.SetUVs(0,tex);mesh.SetNormals(smooth);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();output.Add(mesh);
  }
  return output;
 }
 void BuildCracks(Transform owner,List<Vector3> crackPoints,List<int> crackTriangles,Shader fallbackShader){
  if(crackPoints.Count==0)return;
  var shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null)shader=fallbackShader;if(shader==null)return;
  crackMaterial=new Material(shader){name="Research damage seams"};crackMaterial.SetColor("_BaseColor",new Color(.025f,.035f,.045f,1));
  cracks=new GameObject("Damage seams",typeof(MeshFilter),typeof(MeshRenderer));cracks.transform.SetParent(owner,false);
  // Points are physical metres in owner's rotation basis, not scaled mesh coordinates.
  var scale=owner.lossyScale;
  for(int i=0;i<crackPoints.Count;i++){var p=crackPoints[i];crackPoints[i]=new Vector3(p.x/Mathf.Max(.0001f,Mathf.Abs(scale.x)),p.y/Mathf.Max(.0001f,Mathf.Abs(scale.y)),p.z/Mathf.Max(.0001f,Mathf.Abs(scale.z)));}
  var mesh=new Mesh{name=owner.name+" damage seams"};mesh.SetVertices(crackPoints);mesh.SetTriangles(crackTriangles,0);mesh.RecalculateBounds();meshes.Add(mesh);
  cracks.GetComponent<MeshFilter>().sharedMesh=mesh;cracks.GetComponent<MeshRenderer>().sharedMaterial=crackMaterial;cracks.SetActive(false);
 }
 public void SetDamage(float value,bool broken){if(cracks!=null)cracks.SetActive(!broken&&value>=.25f);}
 public void Dispose(){
  if(Root!=null)UnityEngine.Object.Destroy(Root);
  if(cracks!=null)UnityEngine.Object.Destroy(cracks);
  foreach(var mesh in meshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
  if(crackMaterial!=null)UnityEngine.Object.Destroy(crackMaterial);
 }
}
}
