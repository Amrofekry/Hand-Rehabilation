using System;
using UnityEngine;
namespace GripRehab {
/// <summary>Material classes of the user's 2026-10-26 deformation chart.</summary>
public enum MaterialClass { Rigid, SemiRigid, Flexible, Soft }
/// <summary>What remains after the force is removed: the chart's "result of deformation".</summary>
public enum MaterialResult { Elastic, Plastic, Brittle }
/// <summary>The chart's forms of deformation that grip force can drive. (Volume change by heating is thermal and out of scope.)</summary>
public enum DeformationForm { Compression, Tension, Bending, Shear, Torsion, Buckling }

/// <summary>
/// Stage 9 material model. A game-scale visual approximation tuned to read like the chart's materials; these are
/// not measured engineering constants and the deformation is not a finite-element simulation.
/// Forces are the grip force on the held object (newtons); distances are metres.
/// </summary>
public sealed class DeformationMaterial {
 public string name;public MaterialClass materialClass;public MaterialResult result;
 /// <summary>Grip force that produces the full local dent.</summary>
 public float stiffnessNewtons=35;
 /// <summary>Deepest local dent under full force (≈0 for rigid materials).</summary>
 public float maxDepthMeters=.015f;
 /// <summary>Spring-damper of the surface: natural frequency (Hz) and damping ratio (&lt; 1 wobbles, ≥ 1 creeps back).</summary>
 public float naturalHz=8,dampingRatio=.9f;
 /// <summary>Fraction of the full dent (or full form) the material takes elastically; beyond it, plasticity of the excess stays.</summary>
 public float yieldFraction=1,plasticity=0;
 /// <summary>0 = volume is lost when squeezed (sponge), 0.5 = volume preserved (rubber, gel, clay): drives the outward bulge.</summary>
 public float poisson=.3f;
 /// <summary>Neighbour (Laplacian) smoothing of the dent field per step, 0..0.8.</summary>
 public float smoothing=.35f;
 /// <summary>Sustained grip force that cracks then breaks a brittle material (0 = cannot be broken by a hand).</summary>
 public float fractureNewtons;
 /// <summary>Whole-object form driven by grip force; Compression means local dents only unless globalCompression.</summary>
 public DeformationForm form=DeformationForm.Compression;public bool globalCompression;
 /// <summary>Form at full force: strain (compression, tension), radians (bending, torsion) or metres (shear, buckling bow).</summary>
 public float formMax;
 /// <summary>Grip force for the full form; for buckling, formCritical is the load fraction where the column gives way.</summary>
 public float formNewtons=35,formCritical=.45f;

 public bool Rigid=>maxDepthMeters<.0005f&&formMax<=0;

 public DeformationMaterial Copy()=>(DeformationMaterial)MemberwiseClone();

 static DeformationMaterial M(string name,MaterialClass c,MaterialResult r,float stiff,float depth,float hz,float zeta,float yield,float plastic,float poisson,float smooth=.35f,float fracture=0)=>
  new DeformationMaterial{name=name,materialClass=c,result=r,stiffnessNewtons=stiff,maxDepthMeters=depth,naturalHz=hz,dampingRatio=zeta,yieldFraction=yield,plasticity=plastic,poisson=poisson,smoothing=smooth,fractureNewtons=fracture};
 DeformationMaterial Form(DeformationForm f,float max,float newtons,float critical=.45f,bool global=false){form=f;formMax=max;formNewtons=newtons;formCritical=critical;globalCompression=global;return this;}

 // The chart's twelve example materials, plus the forms row and the station objects. Values are chosen so
 // a 0-45 N grip shows each material's character; they are not laboratory data.
 public static DeformationMaterial Steel()=>M("Steel",MaterialClass.Rigid,MaterialResult.Elastic,400,.0001f,30,1,1,0,.3f,.2f);
 public static DeformationMaterial Glass()=>M("Glass",MaterialClass.Rigid,MaterialResult.Brittle,400,.0001f,30,1,1,0,.2f,.2f,40);
 public static DeformationMaterial Concrete()=>M("Concrete",MaterialClass.Rigid,MaterialResult.Elastic,400,.0001f,30,1,1,0,.2f,.2f);
 public static DeformationMaterial Wood()=>M("Wood",MaterialClass.SemiRigid,MaterialResult.Plastic,120,.0006f,14,.6f,.55f,.45f,.3f).Form(DeformationForm.Bending,.45f,45);
 public static DeformationMaterial AbsPlastic()=>M("ABS plastic",MaterialClass.SemiRigid,MaterialResult.Plastic,45,.012f,12,.8f,.45f,.7f,.35f,.45f);
 public static DeformationMaterial DenseFoam()=>M("Dense foam",MaterialClass.SemiRigid,MaterialResult.Elastic,40,.012f,2.5f,1.3f,1,0,.15f,.55f);
 public static DeformationMaterial Rubber()=>M("Rubber",MaterialClass.Flexible,MaterialResult.Elastic,30,.016f,9,.35f,1,0,.48f,.4f);
 public static DeformationMaterial Silicone()=>M("Silicone",MaterialClass.Flexible,MaterialResult.Elastic,28,.014f,8,.3f,1,0,.48f,.4f).Form(DeformationForm.Tension,.6f,30);
 public static DeformationMaterial Fabric()=>M("Fabric",MaterialClass.Flexible,MaterialResult.Plastic,8,.022f,4,.9f,.3f,.25f,.1f,.6f);
 public static DeformationMaterial Gel()=>M("Gel",MaterialClass.Soft,MaterialResult.Elastic,10,.025f,4,.12f,1,0,.5f,.6f);
 public static DeformationMaterial Sponge()=>M("Sponge",MaterialClass.Soft,MaterialResult.Elastic,15,.03f,5,.9f,1,0,.05f,.5f).Form(DeformationForm.Compression,.45f,30,.45f,true);
 public static DeformationMaterial Clay()=>M("Clay",MaterialClass.Soft,MaterialResult.Plastic,12,.022f,6,1,.05f,.9f,.45f,.55f);
 public static DeformationMaterial SteelWire()=>M("Steel strip",MaterialClass.SemiRigid,MaterialResult.Plastic,400,.0001f,12,.7f,.35f,.8f,.3f,.2f).Form(DeformationForm.Torsion,2.2f,35);
 public static DeformationMaterial PaperStack()=>M("Paper stack",MaterialClass.SemiRigid,MaterialResult.Plastic,60,.002f,10,.9f,.4f,.6f,.1f,.3f).Form(DeformationForm.Shear,.025f,25);
 public static DeformationMaterial PlasticStraw()=>M("Plastic straw",MaterialClass.SemiRigid,MaterialResult.Plastic,200,.0005f,12,.7f,.6f,.5f,.3f,.3f).Form(DeformationForm.Buckling,.03f,30,.45f);
 public static DeformationMaterial Porcelain()=>M("Porcelain",MaterialClass.Rigid,MaterialResult.Brittle,400,.0001f,30,1,1,0,.2f,.2f,60);
 public static DeformationMaterial Food()=>M("Ripe food",MaterialClass.Soft,MaterialResult.Plastic,28,.02f,6,.7f,.5f,.3f,.45f,.5f);
 public static DeformationMaterial DryCracker()=>M("Dry cracker",MaterialClass.SemiRigid,MaterialResult.Brittle,60,.001f,20,1,1,0,.1f,.3f,55);

 /// <summary>A hard object that is not part of the chart's examples (pen, card, phone): rigid and elastic.</summary>
 public static DeformationMaterial RigidSolid(string name)=>M(name,MaterialClass.Rigid,MaterialResult.Elastic,400,.0001f,30,1,1,0,.3f,.2f);
 /// <summary>The chart's twelve example materials, in chart order.</summary>
 public static DeformationMaterial[] ChartMaterials()=>new[]{Steel(),Glass(),Concrete(),Wood(),AbsPlastic(),DenseFoam(),Rubber(),Silicone(),Fabric(),Gel(),Sponge(),Clay()};
}

/// <summary>Pure deformation dynamics shared by the game and the offline tests (no Unity objects).</summary>
public static class DeformationDynamics {
 /// <summary>Damped spring toward a goal, semi-implicit Euler in sub-steps so stiff materials stay stable at 50 Hz.</summary>
 public static void Spring(ref float x,ref float v,float goal,float hz,float zeta,float dt){
  if(!float.IsFinite(x))x=0;if(!float.IsFinite(v))v=0;
  if(!float.IsFinite(goal)||!float.IsFinite(dt)||dt<=0)return;
  float w=2*(float)Math.PI*Mathf.Clamp(float.IsFinite(hz)?hz:8,.2f,60);float z=Mathf.Clamp(float.IsFinite(zeta)?zeta:1,0,4);
  int steps=Math.Max(1,Math.Min(32,(int)Math.Ceiling(w*dt/.3f)));float h=dt/steps;
  for(int i=0;i<steps;i++){v+=(w*w*(goal-x)-2*z*w*v)*h;x+=v*h;}
 }
 public static void Spring(ref Vector3 x,ref Vector3 v,Vector3 goal,float hz,float zeta,float dt){
  if(!GraspMotionPolicy.Finite(x))x=goal;if(!GraspMotionPolicy.Finite(v))v=Vector3.zero;
  if(!GraspMotionPolicy.Finite(goal)||!float.IsFinite(dt)||dt<=0)return;
  float w=2*(float)Math.PI*Mathf.Clamp(float.IsFinite(hz)?hz:8,.2f,60);float z=Mathf.Clamp(float.IsFinite(zeta)?zeta:1,0,4);
  int steps=Math.Max(1,Math.Min(32,(int)Math.Ceiling(w*dt/.3f)));float h=dt/steps;
  for(int i=0;i<steps;i++){v+=(goal-x)*(w*w*h)-v*(2*z*w*h);x+=v*h;}
 }
 /// <summary>Permanent set after loading to load (same units as limit): plasticity of the part beyond yield; never shrinks.</summary>
 public static float PlasticSet(float current,float load,float yieldLimit,float plasticity){
  if(!float.IsFinite(current)||current<0)current=0;
  if(!float.IsFinite(load)||!float.IsFinite(yieldLimit)||!float.IsFinite(plasticity))return current;
  float excess=Math.Max(0,Math.Abs(load)-Math.Max(0,yieldLimit));
  return Math.Max(current,excess*Mathf.Clamp01(plasticity));
 }
 /// <summary>For each vertex, the first vertex at the same position (UV seams and poles duplicate positions).</summary>
 public static int[] WeldGroups(Vector3[] vertices,float tolerance=1e-6f){
  var first=new System.Collections.Generic.Dictionary<(long,long,long),int>();var weld=new int[vertices.Length];
  double q=1.0/Math.Max(1e-9f,tolerance);
  for(int i=0;i<vertices.Length;i++){
   var key=((long)Math.Round(vertices[i].x*q),(long)Math.Round(vertices[i].y*q),(long)Math.Round(vertices[i].z*q));
   if(first.TryGetValue(key,out int f))weld[i]=f;else{first[key]=i;weld[i]=i;}
  }
  return weld;
 }
 /// <summary>
 /// Compressed CSR vertex adjacency from a triangle list. With weld groups, coincident vertices share one neighbour
 /// set (listed by group representative), so smoothing and bulge cross seams and poles without tearing them open.
 /// </summary>
 public static void Neighbours(int[] triangles,int vertexCount,out int[] start,out int[] list,int[] weld=null){
  if(triangles==null||vertexCount<=0||triangles.Length%3!=0||weld!=null&&weld.Length!=vertexCount)throw new ArgumentException("Valid triangle list required");
  int R(int i)=>weld!=null?weld[i]:i;
  var sets=new System.Collections.Generic.HashSet<int>[vertexCount];
  for(int i=0;i<vertexCount;i++)if(R(i)==i)sets[i]=new System.Collections.Generic.HashSet<int>();
  for(int t=0;t<triangles.Length;t+=3)for(int e=0;e<3;e++){
   int a=triangles[t+e],b=triangles[t+(e+1)%3];
   if(a<0||b<0||a>=vertexCount||b>=vertexCount)throw new ArgumentException("Triangle index out of bounds");
   int ra=R(a),rb=R(b);if(ra!=rb){sets[ra].Add(rb);sets[rb].Add(ra);}
  }
  start=new int[vertexCount+1];int total=0;for(int i=0;i<vertexCount;i++){start[i]=total;total+=sets[R(i)].Count;}start[vertexCount]=total;
  list=new int[total];for(int i=0;i<vertexCount;i++){int k=start[i];foreach(int n in sets[R(i)])list[k++]=n;}
 }
 /// <summary>Gives coincident vertices the same (averaged) normal so a bulge cannot open a seam.</summary>
 public static void WeldNormals(Vector3[] normals,int[] weld){
  if(weld==null)return;
  for(int i=0;i<normals.Length;i++)if(weld[i]!=i)normals[weld[i]]+=normals[i];
  for(int i=0;i<normals.Length;i++)if(weld[i]==i&&normals[i].sqrMagnitude>1e-20f)normals[i]=normals[i].normalized;
  for(int i=0;i<normals.Length;i++)if(weld[i]!=i)normals[i]=normals[weld[i]];
 }
 /// <summary>Area-weighted vertex normals and areas of the rest mesh.</summary>
 public static void RestNormals(Vector3[] vertices,int[] triangles,Vector3[] normals,float[] areas){
  Array.Clear(normals,0,normals.Length);Array.Clear(areas,0,areas.Length);
  for(int t=0;t<triangles.Length;t+=3){
   int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
   var n=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);float area=n.magnitude*.5f;
   normals[a]+=n;normals[b]+=n;normals[c]+=n;areas[a]+=area/3;areas[b]+=area/3;areas[c]+=area/3;
  }
  for(int i=0;i<normals.Length;i++)normals[i]=normals[i].sqrMagnitude>1e-20f?normals[i].normalized:Vector3.zero;
 }
 /// <summary>Enclosed volume of a closed triangle mesh (divergence theorem); open meshes give an approximate value.</summary>
 public static float Volume(Vector3[] vertices,int[] triangles){
  double sum=0;
  for(int t=0;t+2<triangles.Length;t+=3){var a=vertices[triangles[t]];var b=vertices[triangles[t+1]];var c=vertices[triangles[t+2]];sum+=Vector3.Dot(a,Vector3.Cross(b,c));}
  return (float)(sum/6);
 }

 /// <summary>
 /// Displacement of rest point p for a whole-object form of size amount (strain, radians or metres). The axis
 /// runs from the held end (origin, s = 0) to the free end (s = length); side is the bending/shear/bow direction.
 /// </summary>
 public static Vector3 Form(Vector3 p,DeformationForm form,float amount,Vector3 origin,Vector3 axis,Vector3 side,float length,float poisson){
  if(!GraspMotionPolicy.Finite(p)||!float.IsFinite(amount)||amount==0||!float.IsFinite(length)||length<=1e-5f||axis.sqrMagnitude<1e-8f)return Vector3.zero;
  axis=axis.normalized;side=Vector3.ProjectOnPlane(side,axis);if(side.sqrMagnitude<1e-8f)side=Vector3.Cross(axis,Mathf.Abs(axis.y)<.9f?Vector3.up:Vector3.right);side=side.normalized;
  var rel=p-origin;float s=Vector3.Dot(rel,axis);var radial=rel-axis*s;float u=Mathf.Clamp01(s/length);
  poisson=Mathf.Clamp(float.IsFinite(poisson)?poisson:.3f,0,.5f);
  switch(form){
   case DeformationForm.Compression:{
    // Squash along the axis about its middle; a Poisson bulge sideways.
    // Cross-section scales by (1 - strain)^-poisson: poisson 0.5 keeps the volume exactly (sponge ~0 loses it).
    float strain=Mathf.Clamp(amount,-.95f,.95f);float mid=length*.5f;
    return -axis*((s-mid)*strain)+radial*((float)Math.Pow(1-strain,-poisson)-1);}
   case DeformationForm.Tension:{
    // Cross-section thins by (1 + strain)^-poisson, exact for large stretch (0.5 = volume kept).
    float strain=Mathf.Clamp(amount,-.9f,3f);
    return axis*(s*strain)+radial*((float)Math.Pow(1+strain,-poisson)-1);}
   case DeformationForm.Bending:{
    // Uniform curvature from the held end: the centreline becomes an arc of total angle amount.
    float kappa=amount/length;if(Math.Abs(kappa)<1e-6f)return Vector3.zero;
    float off=Vector3.Dot(radial,side);float r=1/kappa-off;float theta=s*kappa;
    var bent=side*(1/kappa-r*(float)Math.Cos(theta))+axis*(r*(float)Math.Sin(theta));
    return bent-(axis*s+side*off);}
   case DeformationForm.Shear:{
    // Layers slide: the far end moves sideways by amount, the held end stays.
    return side*(amount*u);}
   case DeformationForm.Torsion:{
    // Each cross-section turns about the axis by an angle growing toward the free end.
    var turned=Quaternion.AngleAxis(amount*u*Mathf.Rad2Deg,axis)*radial;return turned-radial;}
   case DeformationForm.Buckling:{
    // Both ends held: the column shortens slightly and bows sideways in its first mode.
    float bow=amount*(float)Math.Sin(Math.PI*u);float shorten=-(float)(Math.PI*Math.PI*amount*amount/(4*length))*u;
    return side*bow+axis*shorten;}
  }
  return Vector3.zero;
 }
 /// <summary>Form size for a force: proportional below the critical load; a buckling column gives way past it.</summary>
 public static float FormTarget(DeformationMaterial m,float force){
  if(m==null||m.formMax<=0||!float.IsFinite(force)||force<=0||m.formNewtons<=0)return 0;
  float load=Math.Min(1.5f,force/m.formNewtons);
  if(m.form!=DeformationForm.Buckling)return m.formMax*load;
  float critical=Mathf.Clamp(m.formCritical,.05f,.95f);
  return load<=critical?0:m.formMax*(float)Math.Sqrt((load-critical)/(1-critical));
 }
}
}
