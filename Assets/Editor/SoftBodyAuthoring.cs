using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace GripRehab.Editor {
public static class SoftBodyAuthoring {
 const string Folder="Assets/Rehab/3.3-deformation/Topologies";
 [MenuItem("Grip Rehab/Deformation/Bake Six Material Networks")]
 public static void Bake(){
  EditorSceneManager.OpenScene("Assets/Scenes/GripRehab.unity");
  if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Rehab/3.3-deformation","Topologies");
  var objects=UnityEngine.Object.FindObjectsByType<SqueezableObject>(FindObjectsInactive.Include);
  var report=new List<string>();
  foreach(var item in objects){
   if(item.profile==null)continue;
   var filter=item.visual.GetComponent<MeshFilter>();var mesh=filter.sharedMesh;
   if(item.displayName.Contains("cup")||item.displayName.Contains("mug")){
    string meshPath=Folder+"/"+item.profile.name+"_dense.asset";
    var dense=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
    if(dense==null){dense=RefineLongEdges(mesh,.24f);AssetDatabase.CreateAsset(dense,meshPath);}
    else if(dense.vertexCount>4096){var replacement=RefineLongEdges(mesh,.24f);EditorUtility.CopySerialized(replacement,dense);UnityEngine.Object.DestroyImmediate(replacement);EditorUtility.SetDirty(dense);}
    if(dense.vertexCount>4096)throw new Exception("Cup refinement exceeded vertex budget: "+dense.vertexCount);
    filter.sharedMesh=dense;EditorUtility.SetDirty(filter);mesh=dense;
   }
   if(mesh.vertexCount==24&&item.profile.deformable){
    string meshPath=Folder+"/"+item.profile.name+"_surface.asset";
    var refined=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
    if(refined==null){refined=Refine(mesh,3);AssetDatabase.CreateAsset(refined,meshPath);}
    filter.sharedMesh=refined;EditorUtility.SetDirty(filter);mesh=refined;
   }
   item.profile.hollowCup=item.displayName.Contains("cup")||item.displayName.Contains("mug");
   if(item.profile.deformable){
    string path=Folder+"/"+item.profile.name+".asset";
    var topology=AssetDatabase.LoadAssetAtPath<SoftBodyTopology>(path);
    if(topology==null){topology=ScriptableObject.CreateInstance<SoftBodyTopology>();AssetDatabase.CreateAsset(topology,path);}
    BakeMesh(mesh,topology);item.profile.softBodyTopology=topology;
    if(item.profile.hollowCup){
     var pairs=new HashSet<long>();float limit=mesh.bounds.size.magnitude*.15f;
     for(int n=0;n<topology.nodes.Length;n++){
      int best=-1;float distance=limit*limit;
      for(int m=0;m<topology.nodes.Length;m++)if(Vector3.Dot(topology.normals[n],topology.normals[m])<-.6f){
       float d=(topology.nodes[n]-topology.nodes[m]).sqrMagnitude;if(d<distance){distance=d;best=m;}}
      if(best>=0)pairs.Add(((long)Math.Min(n,best)<<32)|(uint)Math.Max(n,best));
     }
     topology.wallPairs=pairs.OrderBy(k=>k).Select(k=>new Vector2Int((int)(k>>32),(int)(k&0xffffffff))).ToArray();
    }else topology.wallPairs=Array.Empty<Vector2Int>();
    item.profile.springCoupling=item.profile.kind==MaterialKind.Foam?.008f:.025f;
    item.profile.pressureResponsePerSecond=32;EditorUtility.SetDirty(topology);
   }
   EditorUtility.SetDirty(item.profile);
   report.Add(item.displayName+": "+mesh.vertexCount+" visual vertices; "+(item.profile.softBodyTopology!=null?item.profile.softBodyTopology.nodes.Length:0)+" spring nodes; colliders="+
    string.Join(",",item.GetComponentsInChildren<Collider>(true).Select(c=>c.GetType().Name)));
  }
  AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  System.IO.Directory.CreateDirectory("Docs/evidence/deformation-2026-10-08");
  System.IO.File.WriteAllLines("Docs/evidence/deformation-2026-10-08/bake.txt",report);
  Debug.Log("[SOFTBODY_BAKE] PASS: "+string.Join("; ",report));
 }
 static Mesh RefineLongEdges(Mesh input,float maximum){
  var result=UnityEngine.Object.Instantiate(input);result.name=input.name+" locally refined surface";
  for(int pass=0;pass<5;pass++){
   var v=new List<Vector3>(result.vertices);var uv=new List<Vector2>(result.uv);var normals=new List<Vector3>(result.normals);
   var marked=new HashSet<long>();long Key(int a,int b)=>((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);
   for(int s=0;s<result.subMeshCount;s++){var ts=result.GetTriangles(s);for(int t=0;t<ts.Length;t+=3)for(int k=0;k<3;k++){
    int a=ts[t+k],b=ts[t+(k+1)%3];if((v[a]-v[b]).sqrMagnitude>maximum*maximum)marked.Add(Key(a,b));}}
   if(marked.Count==0)break;
   var mids=new Dictionary<long,int>();int Mid(int a,int b){long key=Key(a,b);if(mids.TryGetValue(key,out int n))return n;
    n=v.Count;mids[key]=n;v.Add((v[a]+v[b])*.5f);uv.Add((uv[a]+uv[b])*.5f);normals.Add((normals[a]+normals[b]).normalized);return n;}
   var parts=new List<int[]>();
   for(int s=0;s<result.subMeshCount;s++){
    var ts=result.GetTriangles(s);var output=new List<int>();
    for(int t=0;t<ts.Length;t+=3){int a=ts[t],b=ts[t+1],c=ts[t+2];
     bool ab=marked.Contains(Key(a,b)),bc=marked.Contains(Key(b,c)),ca=marked.Contains(Key(c,a));int count=(ab?1:0)+(bc?1:0)+(ca?1:0);
     if(count==0)output.AddRange(new[]{a,b,c});
     else if(count==3){int x=Mid(a,b),y=Mid(b,c),z=Mid(c,a);output.AddRange(new[]{a,x,z,x,b,y,z,y,c,x,y,z});}
     else{
      // Rotate triangle so the first edge is split (one edge), or the first
      // two edges are split (two edges). Shared midpoints prevent T-junctions.
      for(int rotate=0;rotate<3;rotate++){
       if(marked.Contains(Key(a,b))&&(count==1||marked.Contains(Key(b,c))))break;
       int save=a;a=b;b=c;c=save;
      }
      int x=Mid(a,b);
      if(count==1)output.AddRange(new[]{a,x,c,x,b,c});
      else{int y=Mid(b,c);output.AddRange(new[]{x,b,y,a,x,c,x,y,c});}
     }
    }parts.Add(output.ToArray());
   }
   result.Clear();result.SetVertices(v);result.SetUVs(0,uv);result.SetNormals(normals);result.subMeshCount=parts.Count;
   for(int s=0;s<parts.Count;s++)result.SetTriangles(parts[s],s);result.RecalculateBounds();
  }
  return result;
 }
 static Mesh Refine(Mesh input,int rounds){
  var result=UnityEngine.Object.Instantiate(input);result.name=input.name+" refined contact surface";
  for(int pass=0;pass<rounds;pass++){
   var vertices=new List<Vector3>(result.vertices);var normals=new List<Vector3>(result.normals);var uv=new List<Vector2>(result.uv);
   var edges=new Dictionary<long,int>();var parts=new List<int[]>();
   int Mid(int a,int b){long key=((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);
    if(edges.TryGetValue(key,out int n))return n;n=vertices.Count;edges[key]=n;
    vertices.Add((vertices[a]+vertices[b])*.5f);normals.Add((normals[a]+normals[b]).normalized);
    if(uv.Count>0)uv.Add((uv[a]+uv[b])*.5f);return n;}
   for(int sub=0;sub<result.subMeshCount;sub++){
    var original=result.GetTriangles(sub);var tris=new List<int>();
    for(int t=0;t<original.Length;t+=3){int a=original[t],b=original[t+1],c=original[t+2],ab=Mid(a,b),bc=Mid(b,c),ca=Mid(c,a);
     tris.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});}parts.Add(tris.ToArray());
   }
   result.Clear();result.SetVertices(vertices);result.SetNormals(normals);if(uv.Count>0)result.SetUVs(0,uv);
   result.subMeshCount=parts.Count;for(int sub=0;sub<parts.Count;sub++)result.SetTriangles(parts[sub],sub);result.RecalculateBounds();
  }
  return result;
 }
 public static void BakeMesh(Mesh mesh,SoftBodyTopology t){
  if(!mesh.isReadable)throw new Exception("Unreadable deformation mesh: "+mesh.name);
  var v=mesh.vertices;var norms=mesh.normals;
  for(int i=0;i<norms.Length;i++)if(norms[i].sqrMagnitude<.1f)norms[i]=(v[i]-mesh.bounds.center).normalized;
  var unique=new List<int>();
  for(int i=0;i<v.Length;i++){
   bool found=false;
   foreach(int index in unique)if((v[index]-v[i]).sqrMagnitude<1e-12f&&Vector3.Dot(norms[index],norms[i])>.8f){found=true;break;}
   if(!found)unique.Add(i);
  }
  var samples=new List<int>{unique[0]};var minimum=Enumerable.Repeat(float.PositiveInfinity,unique.Count).ToArray();
  while(samples.Count<Math.Min(96,unique.Count)){
   int last=samples[samples.Count-1],best=0;float largest=-1;
   for(int j=0;j<unique.Count;j++){
    int candidate=unique[j];float d=(v[candidate]-v[last]).sqrMagnitude;
    d+=mesh.bounds.size.sqrMagnitude*.002f*(1-Vector3.Dot(norms[candidate],norms[last]));
    minimum[j]=Mathf.Min(minimum[j],d);if(minimum[j]>largest){largest=minimum[j];best=j;}
   }
   samples.Add(unique[best]);
  }
  t.sourceMesh=mesh;t.nodes=samples.Select(i=>v[i]).ToArray();t.normals=samples.Select(i=>norms[i]).ToArray();
  t.vertexNodes=new int[v.Length*4];t.vertexWeights=new float[v.Length*4];var owners=new int[v.Length];
  for(int i=0;i<v.Length;i++){
   // UV seams and hard normals duplicate a position without cutting the
   // material. Position-only skinning keeps those copies welded under load;
   // normal-biased ownership used to tear cup handles and cuboid edges.
   var nearest=Enumerable.Range(0,samples.Count).OrderBy(n=>(v[i]-t.nodes[n]).sqrMagnitude).Take(4).ToArray();
   float total=0;owners[i]=nearest[0];
   for(int j=0;j<4;j++){int n=nearest[Math.Min(j,nearest.Length-1)];t.vertexNodes[i*4+j]=n;
    float weight=1/Mathf.Max(1e-8f,(v[i]-t.nodes[n]).sqrMagnitude);t.vertexWeights[i*4+j]=weight;total+=weight;}
   for(int j=0;j<4;j++)t.vertexWeights[i*4+j]/=total;
  }
  // Unique edges from real triangles, not the upstream paired-quad assumption.
  var edges=new HashSet<long>();var triangles=mesh.triangles;
  for(int i=0;i<triangles.Length;i+=3)for(int j=0;j<3;j++){
   int a=owners[triangles[i+j]],b=owners[triangles[i+(j+1)%3]];
   if(a==b||Vector3.Dot(t.normals[a],t.normals[b])<-.1f)continue;
   edges.Add(((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b));
  }
  t.edges=edges.OrderBy(k=>k).Select(k=>new Vector2Int((int)(k>>32),(int)(k&0xffffffff))).ToArray();
 }
}
}
