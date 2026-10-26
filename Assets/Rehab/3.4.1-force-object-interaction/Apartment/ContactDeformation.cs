using System;
using UnityEngine;
namespace GripRehab {
// Bounded contact-local visual approximation; not a FEM or clinical material model.
public static class ContactDeformation {
 public static float SafeDt(float dt)=>float.IsFinite(dt)&&dt>0?Math.Min(dt,.05f):0;
 public static float Depth(float force,float full,float maximum,int contacts){
  if(!float.IsFinite(force)||force<=0||!float.IsFinite(full)||full<=0||
     !float.IsFinite(maximum)||maximum<=0||contacts<=0)return 0;
  // Full force belongs to the grasp, not independently to all 15 phalanges.
  return Math.Min(.5f,maximum)*Math.Min(1,force/full)*Math.Min(1,5f/contacts);
 }
 public static Vector3 Indentation(Vector3 vertex,Vector3 surface,Vector3 outward,float depth,float radius,Vector3 seed,float bulge){
  if(!GraspMotionPolicy.Finite(vertex)||!GraspMotionPolicy.Finite(surface)||!GraspMotionPolicy.Finite(outward)||
     !GraspMotionPolicy.Finite(seed)||!float.IsFinite(depth)||depth<=0||!float.IsFinite(radius)||radius<=0||
     outward.sqrMagnitude<1e-10f||!float.IsFinite(bulge))return new Vector3();
  float distance=Vector3.Distance(vertex,surface);
  if(distance>=radius)return new Vector3();
  float t=1-distance/radius,falloff=t*t*(3-2*t);
  float irregular=.89f+.11f*(float)Math.Sin(seed.x*29+seed.y*17+seed.z*23);
  Vector3 normal=outward.normalized;
  Vector3 tangent=Vector3.ProjectOnPlane(vertex-surface,normal)/radius;
  Vector3 delta=(normal*(-depth)+tangent*(depth*Math.Max(0,Math.Min(.3f,bulge))))*(falloff*irregular);
  return GraspMotionPolicy.SafeVelocity(delta,depth);
 }
 public static float GripDamage(ref float dwell,bool eligible,float force,float dt,float threshold,float gate,float capacity){
  dt=SafeDt(dt);
  if(!eligible||!float.IsFinite(force)||force<=0||!float.IsFinite(threshold)||threshold<=0||
     !float.IsFinite(gate)||gate<=0||!float.IsFinite(capacity)||capacity<=0||force<threshold){dwell=0;return 0;}
  if(dt==0)return 0;
  float previous=float.IsFinite(dwell)&&dwell>=0?dwell:0;
  dwell=previous+dt;
  float active=Math.Max(0,dwell-gate)-Math.Max(0,previous-gate);
  return active/Math.Max(.01f,gate*.5f*capacity);
 }
 public static float ImpactDamage(float mass,float speedSquared,float threshold){
  if(!float.IsFinite(mass)||mass<=0||!float.IsFinite(speedSquared)||speedSquared<0||
     !float.IsFinite(threshold)||threshold<=0)return 0;
  double energy=.5*(double)mass*speedSquared;
  return energy<threshold?0:(float)Math.Min(1,energy/threshold-.5);
 }
}
public sealed class LocalDeformationState {
 public readonly Vector3[] Rest,Vertices;
 readonly Vector3[] targets,permanent;readonly bool[] touched;
 public LocalDeformationState(Vector3[] rest){
  if(rest==null||Array.Exists(rest,v=>!GraspMotionPolicy.Finite(v)))throw new ArgumentException("Finite rest vertices required");
  Rest=(Vector3[])rest.Clone();Vertices=(Vector3[])rest.Clone();
  targets=new Vector3[rest.Length];permanent=new Vector3[rest.Length];touched=new bool[rest.Length];
 }
 public void BeginStep(){Array.Clear(targets,0,targets.Length);Array.Clear(touched,0,touched.Length);}
 public void SetTarget(int index,Vector3 displacement){
  if(!GraspMotionPolicy.Finite(displacement))return;
  if(displacement.sqrMagnitude>targets[index].sqrMagnitude)targets[index]=displacement;
  touched[index]|=displacement.sqrMagnitude>0;
 }
 public bool Step(float dt,float recovery,float plasticity){
  dt=ContactDeformation.SafeDt(dt);if(dt==0)return false;
  recovery=float.IsFinite(recovery)?Math.Max(.01f,recovery):8;
  plasticity=float.IsFinite(plasticity)?Math.Max(0,Math.Min(1,plasticity)):0;
  float attack=1-(float)Math.Exp(-32*dt),relax=1-(float)Math.Exp(-recovery*dt);
  bool changed=false;
  for(int i=0;i<Rest.Length;i++){
   if(touched[i]&&plasticity>0)permanent[i]=Vector3.Lerp(permanent[i],targets[i]*(plasticity*.6f),attack);
   Vector3 offset=touched[i]?targets[i]:permanent[i];
   if(touched[i]&&offset.sqrMagnitude<permanent[i].sqrMagnitude)offset=permanent[i];
   Vector3 goal=Rest[i]+offset;
   Vector3 next=Vector3.Lerp(Vertices[i],goal,touched[i]?attack:relax);
   // Snap AND upload the final recovery step. Never stop short of the goal.
   if((next-goal).sqrMagnitude<1e-12f)next=goal;
   if((next-Vertices[i]).sqrMagnitude>0){Vertices[i]=next;changed=true;}
  }
  return changed;
 }
 public void Reset(){Array.Copy(Rest,Vertices,Rest.Length);Array.Clear(permanent,0,permanent.Length);if(velocity!=null){Array.Clear(velocity,0,velocity.Length);Array.Clear(goal,0,goal.Length);}BeginStep();}

 // ---- Stage 9 material dynamics (used when the object has a DeformationMaterial; Step above is the older,
 // first-order recovery kept for compatibility). Arrays are allocated once, so a step allocates nothing.
 Vector3[] velocity,goal,smoothed,normals;float[] areas,ring,ringNext;int[] neighbourStart,neighbours;
 const int BulgePasses=16;
 /// <summary>How far (mesh units, along the surface) the volume bulge reaches beyond the deep part of a dent.</summary>
 public float BulgeReach;
 public bool HasTopology=>neighbours!=null;
 int[] triangles;
 float GoalVolume(){for(int i=0;i<Rest.Length;i++)smoothed[i]=Rest[i]+goal[i];return Math.Abs(DeformationDynamics.Volume(smoothed,triangles));}
 /// <summary>Rest-mesh adjacency, normals and areas for smoothing and the volume bulge. Call once.</summary>
 public void SetTopology(int[] triangles){
  this.triangles=(int[])triangles.Clone();
  var weld=DeformationDynamics.WeldGroups(Rest);
  DeformationDynamics.Neighbours(triangles,Rest.Length,out neighbourStart,out neighbours,weld);
  normals=new Vector3[Rest.Length];areas=new float[Rest.Length];DeformationDynamics.RestNormals(Rest,triangles,normals,areas);
  // Either winding: the signed volume says which way the triangle normals face; make them point outward.
  if(DeformationDynamics.Volume(Rest,triangles)<0)for(int i=0;i<normals.Length;i++)normals[i]=-normals[i];
  DeformationDynamics.WeldNormals(normals,weld);
  velocity=new Vector3[Rest.Length];goal=new Vector3[Rest.Length];smoothed=new Vector3[Rest.Length];
  ring=new float[Rest.Length];ringNext=new float[Rest.Length];
  float edges=0;int count=0;for(int i=0;i<Rest.Length;i++)for(int k=neighbourStart[i];k<neighbourStart[i+1];k++){edges+=(Rest[i]-Rest[neighbours[k]]).magnitude;count++;}
  if(BulgeReach<=0)BulgeReach=count>0?4*edges/count:0; // about four rings unless the owner sets a physical reach
 }
 /// <summary>Signed volume change of the current goal shape, as a fraction of the rest volume estimate (diagnostic).</summary>
 public float LastVolumeChange{get;private set;}
 /// <summary>
 /// One physics step: dents (targets) are smoothed over neighbours, given a volume-preserving outward bulge
 /// (material Poisson ratio), kept partly as permanent set beyond yield, and reached by a damped spring per vertex.
 /// maxDepth is the material's full dent, used for the yield limit.
 /// </summary>
 public bool StepDynamic(float dt,DeformationMaterial material,float restVolume){
  dt=ContactDeformation.SafeDt(dt);if(dt==0||material==null||!HasTopology)return false;
  float yieldLimit=Math.Max(0,material.yieldFraction)*Math.Max(1e-5f,material.maxDepthMeters);
  // 1. Permanent set and the wanted displacement per vertex.
  for(int i=0;i<Rest.Length;i++){
   var t=targets[i];
   if(touched[i]&&material.plasticity>0){
    float set=DeformationDynamics.PlasticSet(permanent[i].magnitude,t.magnitude,yieldLimit,material.plasticity);
    if(set>permanent[i].magnitude+1e-7f&&t.sqrMagnitude>0)permanent[i]=t.normalized*set;
   }
   goal[i]=touched[i]&&t.sqrMagnitude>=permanent[i].sqrMagnitude?t:permanent[i];
  }
  // 2. Neighbour smoothing keeps the dent continuous (no single-vertex spikes).
  float s=Mathf.Clamp(material.smoothing,0,.8f);
  if(s>0){
   for(int i=0;i<Rest.Length;i++){
    int a=neighbourStart[i],b=neighbourStart[i+1];if(b<=a){smoothed[i]=goal[i];continue;}
    var sum=Vector3.zero;for(int k=a;k<b;k++)sum+=goal[neighbours[k]];
    smoothed[i]=goal[i]*(1-s)+sum*(s/(b-a));
   }
   Array.Copy(smoothed,goal,goal.Length);
  }
  // 3. Volume: the dented volume reappears as an outward bulge in the rings around each dent (as rubber bulges
  // beside a finger), scaled by 2*poisson (0 = sponge loses volume, 0.5 = rubber/gel/clay keep it). The far side
  // of the object does not move.
  float keep=Mathf.Clamp(material.poisson,0,.5f)*2;
  // Dented volume of the smoothed field (inward only), and the dent itself: vertices pushed in by more than 5 % of the deepest.
  float dentVolume=0,deepest=0;
  for(int i=0;i<Rest.Length;i++){float along=Vector3.Dot(goal[i],normals[i]);if(along<0){dentVolume+=along*areas[i];deepest=Math.Max(deepest,-along);}}
  // Seeds are the deep half of each dent; the rings around them (also between neighbouring fingers) take the bulge,
  // weighted by how little each vertex is already pushed in. Several fingers at once still leave room for it.
  float half=deepest*.5f;
  // Surface distance from the deep part (a few relaxation passes over the mesh edges), limited to BulgeReach.
  float reach=BulgeReach>0?BulgeReach:float.PositiveInfinity;
  for(int i=0;i<Rest.Length;i++)ring[i]=deepest>0&&-Vector3.Dot(goal[i],normals[i])>half?0:float.PositiveInfinity;
  for(int pass=0;pass<BulgePasses;pass++){
   bool moved=false;
   for(int i=0;i<Rest.Length;i++){
    float d=ring[i];
    for(int k=neighbourStart[i];k<neighbourStart[i+1];k++){int j=neighbours[k];if(ring[j]<reach){float via=ring[j]+(Rest[i]-Rest[j]).magnitude;if(via<d)d=via;}}
    if(d<ring[i]){ring[i]=d;moved=true;}
   }
   if(!moved)break;
  }
  float weighted=0;
  for(int i=0;i<Rest.Length;i++){
   float pushed=half>0?Mathf.Clamp01(-Vector3.Dot(goal[i],normals[i])/half):0;
   ringNext[i]=ring[i]>0&&ring[i]<reach?(1-ring[i]/reach)*(1-pushed):0;weighted+=ringNext[i]*areas[i];
  }
  // Exact enclosed volume of the dented shape, then two bulge passes toward the volume the material keeps
  // (first-order estimates undercount deep dents from several fingers).
  if(dentVolume<0&&weighted>1e-12f&&restVolume>1e-12f){
   float dented=GoalVolume(),wanted=restVolume+(dented-restVolume)*(1-keep);
   for(int pass=0;pass<2;pass++){
    float now=pass==0?dented:GoalVolume();float bulge=(wanted-now)/weighted;if(!(bulge>0))break;
    for(int i=0;i<Rest.Length;i++)if(ringNext[i]>0)goal[i]+=normals[i]*(bulge*ringNext[i]);
   }
   LastVolumeChange=(GoalVolume()-restVolume)/restVolume;
  } else LastVolumeChange=0;
  // 4. Damped spring per vertex toward the goal: wobble for gel, slow creep for dense foam, snap for stiff.
  bool changed=false;
  for(int i=0;i<Rest.Length;i++){
   var x=Vertices[i]-Rest[i];var v=velocity[i];
   if((x-goal[i]).sqrMagnitude<1e-14f&&v.sqrMagnitude<1e-10f){if(x!=goal[i]){Vertices[i]=Rest[i]+goal[i];velocity[i]=Vector3.zero;changed=true;}continue;}
   DeformationDynamics.Spring(ref x,ref v,goal[i],material.naturalHz,material.dampingRatio,dt);
   if(!GraspMotionPolicy.Finite(x)){x=goal[i];v=Vector3.zero;}
   Vertices[i]=Rest[i]+x;velocity[i]=v;changed=true;
  }
  return changed;
 }
}
}
