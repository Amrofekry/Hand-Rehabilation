using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace GripRehab.Editor {
[InitializeOnLoad]
public static class SoftBodyVerification {
 static SoftBodyVerification(){EditorApplication.playModeStateChanged+=state=>{
  if(state!=PlayModeStateChange.EnteredEditMode||!UnityEditor.SessionState.GetBool("GripSoftBodyQA.Active",false))return;
  int count=0;foreach(var mesh in Resources.FindObjectsOfTypeAll<Mesh>())if(mesh.name.Contains("contact mesh")||mesh.name.Contains("clipped surface shard")||mesh.name.Contains("damage seams"))count++;
  File.AppendAllText("Logs/deformation-mesh-cleanup.txt","Owned runtime deformation meshes after Stop: "+count+"\n");
  File.AppendAllText("Logs/deformation-mesh-cleanup.txt","Owned surface query batches after Stop: "+BurstSurfaceBatch.LiveBatches+"\n");
  if(BurstSurfaceBatch.LiveBatches!=0)UnityEngine.Debug.LogError("[DEFORMATION_CLEANUP] Native surface buffers survived Stop");
  if(count!=0)UnityEngine.Debug.LogError("[DEFORMATION_CLEANUP] Owned meshes survived Stop: "+count);
 };}

 public static void VerifyAndCycles(){Run();File.WriteAllText("Logs/deformation-mesh-cleanup.txt","");UnityEditor.SessionState.SetBool("GripSoftBodyQA.Active",true);if(Array.IndexOf(Environment.GetCommandLineArgs(),"-apartment-deformation-cycles")<0)throw new InvalidOperationException("Use -apartment-deformation-cycles to isolate webcam and profile data");UnityEditor.SessionState.SetBool("ApartmentQA.Active",false);SceneCompositionQA.Begin();}
 public static void BakeAndBuild(){SoftBodyAuthoring.Bake();Run();RehabVerification.BuildWindows();}
 [Serializable] class Row {public string name;public int nodes,vertices;public float lowMm,mediumMm,highMm,recoveredMm,retainedDamage;public double solverMeanMs,legacyMeanMs;public long allocationBytes;}
 [Serializable] class Result {public bool passed;public string utc,scope;public Row[] materials;public int assertions;}
 static int assertions;
 static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
 static double Area(Vector3[] vertices,int[] triangles){double sum=0;for(int t=0;t<triangles.Length;t+=3)sum+=Vector3.Cross(vertices[triangles[t+1]]-vertices[triangles[t]],vertices[triangles[t+2]]-vertices[triangles[t]]).magnitude*.5;return sum;}
 static void VerifyFractureSurface(MeshFilter filter){
  var mesh=filter.sharedMesh;var points=mesh.vertices;for(int i=0;i<points.Length;i++)points[i]=filter.transform.TransformVector(points[i]);
  var triangles=mesh.triangles;var layout=FracturePartitioner.Split(points,triangles,12);var seams=new List<Vector3>();var seamIndices=new List<int>();
  var method=typeof(ObjectFractureVisuals).GetMethod("ClipSurface",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
  var patches=(List<Mesh>)method.Invoke(null,new object[]{points,triangles,mesh.uv,mesh.normals,filter.transform,Quaternion.identity,layout,seams,seamIndices});
  try{
   double source=Area(points,triangles),result=0;bool finite=true;foreach(var patch in patches){result+=Area(patch.vertices,patch.triangles);foreach(var v in patch.vertices)finite&=GraspMotionPolicy.Finite(v);finite&=patch.uv.Length==patch.vertexCount&&patch.normals.Length==patch.vertexCount;}
   Check(finite&&patches.Count>1&&patches.Count<=12,"Invalid clipped fragment vertices/UV/normals");
   Check(Math.Abs(result-source)<Math.Max(1e-8,source*.001),"Fracture clipping lost or duplicated source surface area");
   Check(seams.Count>0&&seamIndices.Count>0,"Clipped fracture has no visible damage seams");
  }finally{foreach(var patch in patches)UnityEngine.Object.DestroyImmediate(patch);}
 }
 public static void Run(){
  EditorSceneManager.OpenScene("Assets/Scenes/GripRehab.unity");assertions=0;var rows=new List<Row>();
  foreach(var item in UnityEngine.Object.FindObjectsByType<SqueezableObject>(FindObjectsInactive.Include)){
   var p=item.profile;var filter=item.visual.GetComponent<MeshFilter>();var mesh=filter.sharedMesh;
   if(p.breakable)VerifyFractureSurface(filter);
   if(!p.deformable){Check(p.kind==MaterialKind.Ceramic&&p.breakable&&p.maxDepthMeters==0,"Mug must stay rigid and brittle");rows.Add(new Row{name=item.displayName,vertices=mesh.vertexCount});continue;}
   Check(p.softBodyTopology!=null&&p.softBodyTopology.Matches(mesh),"Missing or stale baked topology: "+item.displayName);
   var t=p.softBodyTopology;Check(t.edges.Length>0,"No structural springs");
   var welded=new Dictionary<Vector3,int>();var sourcePositions=mesh.vertices;bool seamWeightsMatch=true;
   for(int v=0;v<sourcePositions.Length;v++){
    if(welded.TryGetValue(sourcePositions[v],out int prior)){
     for(int k=0;k<4;k++)seamWeightsMatch &= t.vertexNodes[v*4+k]==t.vertexNodes[prior*4+k]&&Mathf.Abs(t.vertexWeights[v*4+k]-t.vertexWeights[prior*4+k])<1e-7f;
    }else welded.Add(sourcePositions[v],v);
   }
   Check(seamWeightsMatch,"UV/hard-normal seam can split under deformation: "+item.displayName);
   var solver=new NodeSpringDeformation(t,mesh.vertices);
   var row=new Row{name=item.displayName,nodes=t.nodes.Length,vertices=mesh.vertexCount};
   Vector3 point=item.visual.TransformPoint(t.nodes[0]);Vector3 normal=item.visual.TransformDirection(t.normals[0]);
   for(int i=0;i<10;i++){solver.BeginStep(item.visual);solver.Step(item.visual,p,.02f);}
   Check(solver.MaxDisplacementMeters==0&&solver.Sleeping,"Unloaded object must not deform");
   foreach(float f in new[]{8f,24f,80f}){
    solver.Reset();for(int i=0;i<100;i++){solver.BeginStep(item.visual);solver.AddContact(item.visual,point,normal,f,p);solver.Step(item.visual,p,.02f);}
    float mm=solver.MaxDisplacementMeters*1000;
    if(f==8)row.lowMm=mm;else if(f==24)row.mediumMm=mm;else row.highMm=mm;
    Check(mm>0&&mm<=p.maxDepthMeters*1000+.001f,"Displacement must be finite and bounded: "+item.displayName+" force="+f+" mm="+mm+" normal="+normal+" scale="+item.visual.lossyScale);
   }
   Check(row.lowMm<=row.mediumMm+.01f&&row.mediumMm<=row.highMm+.01f,"Force response not monotonic");
   for(int i=0;i<400;i++){solver.BeginStep(item.visual);solver.Step(item.visual,p,.02f);}
   row.recoveredMm=solver.MaxDisplacementMeters*1000;row.retainedDamage=solver.Damage;
   Check(row.recoveredMm<row.highMm,"Object did not recover");
   if(p.plasticity==0)Check(row.recoveredMm<.1f,"Elastic object did not return to rest");
   else Check(row.recoveredMm>0,"Plastic set was lost");
   var one=new NodeSpringDeformation(t,mesh.vertices);var split=new NodeSpringDeformation(t,mesh.vertices);
   for(int i=0;i<50;i++){
    one.BeginStep(item.visual);one.AddContact(item.visual,point,normal,24,p);one.Step(item.visual,p,.02f);
    split.BeginStep(item.visual);for(int c=0;c<5;c++)split.AddContact(item.visual,point,normal,24/5f,p);split.Step(item.visual,p,.02f);
   }
   Check(Mathf.Abs(one.MaxDisplacementMeters-split.MaxDisplacementMeters)<1e-6f,"Force was multiplied by contact count");
   solver.Reset();solver.BeginStep(item.visual);solver.AddContact(item.visual,point,normal,float.NaN,p);solver.Step(item.visual,p,.02f);
   Check(solver.AppliedForceNewtons==0&&solver.MaxDisplacementMeters==0,"Invalid force produced pressure");
   for(int i=0;i<100;i++){solver.BeginStep(item.visual);solver.AddContact(item.visual,point,normal,24,p);solver.Step(item.visual,p,.02f);}
   var clock=new Stopwatch();long before=GC.GetAllocatedBytesForCurrentThread();clock.Start();
   for(int i=0;i<1000;i++){solver.BeginStep(item.visual);solver.AddContact(item.visual,point,normal,24,p);solver.Step(item.visual,p,.02f);}
   clock.Stop();row.allocationBytes=GC.GetAllocatedBytesForCurrentThread()-before;row.solverMeanMs=clock.Elapsed.TotalMilliseconds/1000;
   Check(row.allocationBytes==0,"Managed allocation in warm solver loop");
   // Compare just the two deformation backends with equal output density,
   // one contact, no rendering/upload/normal recomputation in either loop.
   var legacyRest=mesh.vertices;var world=new Vector3[legacyRest.Length];var legacy=new LocalDeformationState(legacyRest);
   for(int v=0;v<world.Length;v++)world[v]=item.visual.TransformPoint(legacyRest[v]);
   clock.Restart();
   for(int repeat=0;repeat<1000;repeat++){
    legacy.BeginStep();for(int v=0;v<world.Length;v++){
     var delta=ContactDeformation.Indentation(world[v],point,normal,ContactDeformation.Depth(24,p.fullCompressionNewtons,p.maxDepthMeters,1),p.deformationRadiusMeters,legacyRest[v],p.kind==MaterialKind.Rubber?.18f:0);
     legacy.SetTarget(v,item.visual.InverseTransformVector(delta));}
    legacy.Step(.02f,p.recoveryPerSecond,p.plasticity);
   }
   clock.Stop();row.legacyMeanMs=clock.Elapsed.TotalMilliseconds/1000;
   solver.Reset();Check(solver.MaxDisplacementMeters==0&&solver.Damage==0&&solver.Sleeping,"Incomplete solver reset");
   rows.Add(row);
  }
  Check(rows.Count==6,"Expected six materials");
  Directory.CreateDirectory("Docs/evidence/deformation-2026-10-08");
  File.WriteAllText("Docs/evidence/deformation-2026-10-08/solver-tests.json",JsonUtility.ToJson(new Result{passed=true,utc=DateTime.UtcNow.ToString("O"),scope="Native Unity math and production solver; not a live hand/hardware test or game FPS measurement",materials=rows.ToArray(),assertions=assertions},true));
  UnityEngine.Debug.Log("[SOFTBODY_TEST] PASS: "+assertions+" assertions");
 }
}
}
