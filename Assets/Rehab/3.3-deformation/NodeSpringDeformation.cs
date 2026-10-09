// Adapted from ehsanwwe/Unity-SoftBody-physics, commit d9f6d89e (Apache-2.0).
// See Docs/ThirdParty/UnitySoftBody/NOTICE.md for the original and modifications.
using System;
using UnityEngine;
namespace GripRehab {
// Overdamped surface springs: rest tethers replace upstream center-of-mass
// SpringJoints, unique edges couple neighboring nodes. Implicit Jacobi
// integration avoids per-vertex PhysX objects. Not volumetric FEM or XPBD.
public sealed class NodeSpringDeformation {
 readonly SoftBodyTopology topology;
 readonly Vector3[] rest,offset,previous,iterate,scratch,load,permanent,worldNodes;
 readonly float[] weights,degree;
 public readonly Vector3[] Vertices;
 public int NodeCount=>offset.Length;
 public Vector3 NodeOffset(int index)=>offset[index];
 public bool Sleeping{get;private set;}=true;
 public float AppliedForceNewtons{get;private set;}
 public float Damage{get;private set;}
 public float MaxDisplacementMeters{get;private set;}
 public int MovedVertices{get;private set;}
 public NodeSpringDeformation(SoftBodyTopology topology,Vector3[] rest){
  this.topology=topology;this.rest=rest;Vertices=(Vector3[])rest.Clone();
  int n=topology.nodes.Length;
  offset=new Vector3[n];previous=new Vector3[n];iterate=new Vector3[n];scratch=new Vector3[n];
  load=new Vector3[n];permanent=new Vector3[n];worldNodes=new Vector3[n];weights=new float[n];degree=new float[n];
  foreach(var e in topology.edges){degree[e.x]++;degree[e.y]++;}
 }
 public void BeginStep(Transform visual){
  Array.Clear(load,0,load.Length);AppliedForceNewtons=0;
  for(int i=0;i<NodeCount;i++)worldNodes[i]=visual.TransformPoint(topology.nodes[i]+offset[i]);
 }
 public void AddContact(Transform visual,Vector3 point,Vector3 outward,float force,PhysicalMaterialProfile material){
  if(!GraspMotionPolicy.Finite(point)||!GraspMotionPolicy.Finite(outward)||!float.IsFinite(force)||force<=0)return;
  int nearest=0;float best=float.PositiveInfinity;
  for(int i=0;i<NodeCount;i++){float d=(worldNodes[i]-point).sqrMagnitude;if(d<best){best=d;nearest=i;}}
  // Use the nearest surface normal rather than the center direction: a cup's
  // inner wall has the opposite normal to its outer wall.
  Vector3 fallback=visual.TransformDirection(topology.normals[nearest]).normalized;
  Vector3 normal=outward.sqrMagnitude>.000001f?outward.normalized:fallback;
  // Use the actual contact, not a quantized nearest-node centre. The latter
  // jumped the dent between nodes as a fingertip slid over the surface.
  Vector3 surface=point;float total=0,radius=material.deformationRadiusMeters;
  for(int i=0;i<NodeCount;i++){
   float d=Vector3.Distance(worldNodes[i],surface)/radius;
   float face=Mathf.Clamp01(Vector3.Dot(normal,visual.TransformDirection(topology.normals[i]).normalized)*2+.15f);
   weights[i]=d<1?(1-d*d)*(1-d*d)*face:0;total+=weights[i];
  }
  if(total<=0)return;
  AppliedForceNewtons+=force;
  // One budget across the patch, not the full grip force at each node/finger.
  float compliance=material.maxDepthMeters*6/Mathf.Max(1,material.fullCompressionNewtons);
  float bulge=material.kind==MaterialKind.Rubber?.14f:material.kind==MaterialKind.Foam?.015f:0;
  for(int i=0;i<NodeCount;i++)if(weights[i]>0){
   Vector3 radial=worldNodes[i]-surface;Vector3 tangent=radial-normal*Vector3.Dot(radial,normal);
   Vector3 delta=(-normal+tangent.normalized*bulge)*(force*weights[i]/total*compliance);
   load[i]+=visual.InverseTransformVector(delta);
  }
  Sleeping=false;
 }
 public bool Step(Transform visual,PhysicalMaterialProfile material,float dt){
  dt=ContactDeformation.SafeDt(dt);if(dt<=0||Sleeping)return false;
  bool pressure=AppliedForceNewtons>0;
  float coupling=material.springCoupling;
  float rate=pressure?material.pressureResponsePerSecond:material.recoveryPerSecond;
  float a=dt*rate,max=material.maxDepthMeters;
  Array.Copy(offset,previous,NodeCount);Array.Copy(offset,iterate,NodeCount);
  for(int pass=0;pass<6;pass++){
   for(int i=0;i<NodeCount;i++)scratch[i]=previous[i]+a*(load[i]+permanent[i]);
   foreach(var e in topology.edges){scratch[e.x]+=a*coupling*iterate[e.y];scratch[e.y]+=a*coupling*iterate[e.x];}
   for(int i=0;i<NodeCount;i++)iterate[i]=scratch[i]/(1+a*(1+coupling*degree[i]));
   // Thin inner/outer cup walls move together, rather than crossing each other.
   if(topology.wallPairs!=null)foreach(var pair in topology.wallPairs){
    Vector3 common=(iterate[pair.x]+iterate[pair.y])*.5f;iterate[pair.x]=iterate[pair.y]=common;
   }
  }
  float motion=0;
  for(int i=0;i<NodeCount;i++){
   offset[i]=visual.InverseTransformVector(Vector3.ClampMagnitude(visual.TransformVector(iterate[i]),max));
   if(!GraspMotionPolicy.Finite(offset[i]))offset[i]=Vector3.zero;
   if(pressure&&material.plasticity>0){
    Vector3 desired=load[i]*material.plasticity;
    if(desired.sqrMagnitude>permanent[i].sqrMagnitude)
     permanent[i]=Vector3.Lerp(permanent[i],desired,1-Mathf.Exp(-dt*1.5f));
    permanent[i]=visual.InverseTransformVector(Vector3.ClampMagnitude(visual.TransformVector(permanent[i]),max*material.plasticity));
   }
   if(!pressure&&(offset[i]-permanent[i]).sqrMagnitude<1e-10f)offset[i]=permanent[i];
   motion=Mathf.Max(motion,(offset[i]-previous[i]).sqrMagnitude);
  }
  if(pressure&&material.kind==MaterialKind.Food)Damage=Mathf.Clamp01(Damage+dt*AppliedForceNewtons/Mathf.Max(1,material.fullCompressionNewtons)*.06f);
  bool changed=false;MaxDisplacementMeters=0;MovedVertices=0;
  for(int v=0;v<Vertices.Length;v++){
   Vector3 delta=Vector3.zero;int k=v*4;
   for(int j=0;j<4;j++)delta+=offset[topology.vertexNodes[k+j]]*topology.vertexWeights[k+j];
   Vector3 value=rest[v]+delta;
   if((Vertices[v]-value).sqrMagnitude>1e-14f)changed=true;
   Vertices[v]=value;float displacement=visual.TransformVector(delta).magnitude;
   MaxDisplacementMeters=Mathf.Max(MaxDisplacementMeters,displacement);if(displacement>.00001f)MovedVertices++;
  }
  if(!pressure&&motion<1e-12f)Sleeping=true;
  return changed;
 }
 public void Reset(){
  Array.Clear(offset,0,NodeCount);Array.Clear(permanent,0,NodeCount);Array.Clear(load,0,NodeCount);
  Array.Copy(rest,Vertices,rest.Length);Damage=AppliedForceNewtons=MaxDisplacementMeters=0;MovedVertices=0;Sleeping=true;
 }
}
}
