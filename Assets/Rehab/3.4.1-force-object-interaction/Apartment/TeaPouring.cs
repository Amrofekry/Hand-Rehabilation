using System;
using UnityEngine;
namespace GripRehab {
/// <summary>Marks a teapot or cup: the wrist tilts it (pour, drink) instead of turning its placement.</summary>
[DisallowMultipleComponent]
public sealed class PourVessel : MonoBehaviour { public bool teapot; }

/// <summary>
/// The tea activity's liquid, built on LiquidCore: tea leaves the teapot through its spout (Torricelli flow from the real
/// head of tea), falls as a stream that narrows as it speeds up, lands in the cup (the level rises by the volume) or on
/// the table (a puddle that spreads with what was spilled), sloshes with the cup's real motion and tremor, spills over the
/// rim when tipped or shaken too far, and goes down when the cup is tipped at the mouth. Tilt comes from the wrist roll
/// (camera hand), T (keyboard) or the probe. A game-scale simulation of a real pour (volume, flow and slosh physics), not
/// CFD; measurements are game measures and camera estimates, not clinical ones.
/// </summary>
public sealed class TeaPouring : MonoBehaviour {
 // ---- Geometry (metres, item space): teapot spout outlet and grip, cup cavity. Must match ActivityProps.Teapot / TeaCup.
 public static readonly Vector3 SpoutTip=new Vector3(.135f,.095f,0),SpoutDirection=new Vector3(.8f,.6f,0).normalized,TeapotGrip=new Vector3(-.105f,.062f,0);
 public const float PotRadius=.06f,PotHeight=.10f,SpoutRadius=.004f,StartMl=500;
 public const float CupR=.04f,CupH=.085f;
 public static float CupBottom=>CupH*.15f;public static float CupInnerBottom=>CupR*.84f-.0008f;public static float CupInnerTop=>CupR*.92f-.0008f;public static float CupDepth=>CupH*.85f;

 public ApartmentExperience app;public ActivityRunner runner;public ActivityItem teapot,cup;public int level=1;
 public readonly SpoutVessel pot=new SpoutVessel(PotRadius,PotHeight,.095f,.135f,SpoutRadius);
 public VesselLiquid cupLiquid;
 // ---- Measurements (millilitres, seconds).
 public float PouredMl{get;private set;}public float IntoCupMl{get;private set;}public float SpilledMl{get;private set;}
 public float CarrySpillMl{get;private set;}public float DrankMl{get;private set;}public float PourSeconds{get;private set;}public float DrinkSeconds{get;private set;}
 public float FilledMl{get;private set;}public bool Overflowed{get;private set;}
 public readonly RunningStats flow=new RunningStats(),tilt=new RunningStats();
 public readonly TremorSummary tremor=new TremorSummary();
 public float TargetMl=>LiquidMath.Ml(cupLiquid.Capacity)*TeaRules.TargetFraction;
 public float CupMl=>LiquidMath.Ml(cupLiquid.Volume);
 public float PotMl=>LiquidMath.Ml(pot.Volume);
 public float FlowMlPerSecond{get;private set;}
 public bool Pouring=>FlowMlPerSecond>.5f;
 public float CurrentTilt{get;private set;}
 public bool AtMouth{get;private set;}
 /// <summary>Probe override of the tilt in degrees (NaN = off).</summary>
 public float TestTilt=float.NaN;
 /// <summary>Probe override of the drink pose (null = from the hand or M).</summary>
 public bool? TestMouth;
 public float LevelSeconds{get;private set;} // how long the teapot has been level (no flow) since it last poured

 readonly TremorEstimator estimator=new TremorEstimator();double lastFrameTime=-1;float nextTremor;
 OneEuroFilter rollFilter=new OneEuroFilter(1.2f,.02f);float rollBase=float.NaN;SqueezableObject rollFor;bool raised;float keyTilt;bool keyMouth;
 Quaternion appliedTilt=Quaternion.identity;
 Vector3 cupPrevPos,cupPrevVel,cupAccel;bool cupPrimed;

 // ---- Visuals: cup liquid mesh, stream tube, droplets, puddles; audio.
 const int Around=32,StreamRings=18,StreamSides=8,PuddleCount=8;
 Mesh liquidMesh,streamMesh;MeshRenderer liquidRenderer,streamRenderer;Transform liquidTransform;
 readonly Vector3[] liquidVerts=new Vector3[Around*2+2];readonly Vector3[] streamVerts=new Vector3[StreamRings*StreamSides];readonly Vector3[] path=new Vector3[StreamRings];readonly float[] pathRadius=new float[StreamRings];
 readonly RaycastHit[] hits=new RaycastHit[8];
 ParticleSystem drops;ParticleSystem.EmitParams emit;
 readonly Transform[] puddles=new Transform[PuddleCount];readonly float[] puddleMl=new float[PuddleCount],puddleShown=new float[PuddleCount];int nextPuddle;
 Material tea;float ripple;AudioSource pourSource,sipSource;

 public static TeaPouring Create(ActivityRunner r,ActivityItem teapot,ActivityItem cup,int level){
  var go=new GameObject("Tea");go.transform.SetParent(r.Root,false);
  var t=go.AddComponent<TeaPouring>();t.app=r.app;t.runner=r;t.teapot=teapot;t.cup=cup;t.level=level;
  t.cupLiquid=new VesselLiquid(CupInnerBottom,CupInnerTop,CupDepth);t.pot.Fill(LiquidMath.M3(StartMl));
  t.Build();return t;
 }

 void Build(){
  tea=runner.Paint(new Color(.55f,.27f,.08f,.78f),.92f);
  if(tea.HasProperty("_Surface")){tea.SetFloat("_Surface",1);tea.SetFloat("_Blend",0);tea.SetOverrideTag("RenderType","Transparent");
   tea.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);tea.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);tea.SetInt("_ZWrite",0);
   tea.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");tea.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;}
  // Liquid in the cup: a child of the cup so it moves with it; rebuilt in cup space each frame.
  var liquid=new GameObject("Tea in cup"){layer=cup.gameObject.layer};liquidTransform=liquid.transform;liquidTransform.SetParent(cup.Item.visual!=null?cup.Item.visual:cup.transform,false);
  liquidMesh=new Mesh{name="Tea surface"};liquidMesh.MarkDynamic();liquid.AddComponent<MeshFilter>().sharedMesh=liquidMesh;
  liquidRenderer=liquid.AddComponent<MeshRenderer>();liquidRenderer.sharedMaterial=tea;liquidRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
  var tris=new int[Around*12];int k=0;
  for(int i=0;i<Around;i++){int j=(i+1)%Around;
   tris[k++]=0;tris[k++]=1+j;tris[k++]=1+i;                                   // bottom (center 0, ring 1..)
   tris[k++]=1+i;tris[k++]=1+j;tris[k++]=1+Around+i;tris[k++]=1+j;tris[k++]=1+Around+j;tris[k++]=1+Around+i; // side
   tris[k++]=Around*2+1;tris[k++]=1+Around+i;tris[k++]=1+Around+j;}            // surface (center last)
  liquidMesh.vertices=liquidVerts;liquidMesh.triangles=tris;liquidRenderer.enabled=false;
  // Stream: a tube along the falling path, in world space (this object stays at the origin).
  transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
  var stream=new GameObject("Tea stream");stream.transform.SetParent(transform,false);
  streamMesh=new Mesh{name="Tea stream"};streamMesh.MarkDynamic();stream.AddComponent<MeshFilter>().sharedMesh=streamMesh;
  streamRenderer=stream.AddComponent<MeshRenderer>();streamRenderer.sharedMaterial=tea;streamRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
  var st=new int[(StreamRings-1)*StreamSides*6];k=0;
  for(int r=0;r<StreamRings-1;r++)for(int s=0;s<StreamSides;s++){int a=r*StreamSides+s,b=r*StreamSides+(s+1)%StreamSides,c=a+StreamSides,d=b+StreamSides;st[k++]=a;st[k++]=c;st[k++]=b;st[k++]=b;st[k++]=c;st[k++]=d;}
  streamMesh.vertices=streamVerts;streamMesh.triangles=st;streamRenderer.enabled=false;
  // Droplets and splashes.
  var dropsGo=new GameObject("Tea drops");dropsGo.transform.SetParent(transform,false);drops=dropsGo.AddComponent<ParticleSystem>();
  drops.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
  var main=drops.main;main.loop=false;main.playOnAwake=false;main.maxParticles=400;main.simulationSpace=ParticleSystemSimulationSpace.World;
  main.startLifetime=new ParticleSystem.MinMaxCurve(.5f,1f);main.startSize=new ParticleSystem.MinMaxCurve(.003f,.007f);main.gravityModifier=1;main.startColor=new Color(.55f,.27f,.08f,.85f);
  var em=drops.emission;em.enabled=false;var shape=drops.shape;shape.enabled=false;
  var col=drops.collision;col.enabled=true;col.type=ParticleSystemCollisionType.World;col.quality=ParticleSystemCollisionQuality.Low;col.bounce=.05f;col.dampen=.85f;col.lifetimeLoss=.5f;col.collidesWith=~(1<<8);
  var pr=dropsGo.GetComponent<ParticleSystemRenderer>();pr.sharedMaterial=tea;pr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
  drops.Play();
  // Puddles: thin discs, scaled by what was spilled there.
  var disc=new MeshKit().Cylinder(0,MeshKit.At(Vector3.zero),1,.0016f,28,1).Build("Puddle");
  for(int i=0;i<PuddleCount;i++){var p=new GameObject("Tea puddle "+i);p.transform.SetParent(transform,false);p.AddComponent<MeshFilter>().sharedMesh=disc;
   var pm=p.AddComponent<MeshRenderer>();pm.sharedMaterial=tea;pm.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;p.SetActive(false);puddles[i]=p.transform;}
  // Sounds, synthesised (ADR-AU-1): a filtered-noise pour whose pitch rises as the cup fills, and a sip.
  pourSource=Source("Pour",TeaSounds.Pour(),true);sipSource=Source("Sip",TeaSounds.Sip(),true);
 }
 AudioSource Source(string name,AudioClip clip,bool loop){
  var s=gameObject.AddComponent<AudioSource>();s.clip=clip;s.loop=loop;s.playOnAwake=false;s.spatialBlend=1;s.minDistance=.3f;s.maxDistance=6;s.volume=0;s.dopplerLevel=0;
  s.outputAudioMixerGroup=RehabAudio.SfxGroup;return s;
 }

 // ---------------- Input: tilt and the drink pose
 /// <summary>Wrist roll in degrees from the knuckle line (index to little finger base) in the camera image.</summary>
 static float KnuckleRoll(LandmarkFrame f){
  var p=f.landmarks;float w=f.imageWidth>0?f.imageWidth:640,h=f.imageHeight>0?f.imageHeight:480;
  return Mathf.Atan2((p[17].y-p[5].y)*h,(p[17].x-p[5].x)*w)*Mathf.Rad2Deg;
 }
 LandmarkFrame Frame()=>app.vision==null?null:app.LeftSelected?(app.vision.LeftValid?app.vision.LatestLeft:null):(app.vision.RightValid?app.vision.LatestRight:null);

 void ReadInput(SqueezableObject held,float dt){
  bool vessel=held!=null&&held.GetComponent<PourVessel>()!=null;
  if(!vessel){rollBase=float.NaN;rollFor=null;keyTilt=0;keyMouth=false;raised=false;CurrentTilt=Mathf.MoveTowards(CurrentTilt,0,240*dt);return;}
  float want;bool mouth;var frame=app.CameraMode?Frame():null;
  if(!float.IsNaN(TestTilt))want=TestTilt;
  else if(frame!=null&&frame.landmarks!=null&&frame.landmarks.Length>=21){
   float roll=KnuckleRoll(frame);
   if(rollFor!=held||float.IsNaN(rollBase)){rollFor=held;rollBase=roll;rollFilter.Reset();}
   float delta=Mathf.Abs(Mathf.DeltaAngle(rollBase,roll));
   want=Mathf.Clamp((rollFilter.Step(delta,Mathf.Max(dt,1e-3f))-6)*1.25f,0,115); // 6° dead band, then a little gain
  } else {
   bool key=app.player.GameplayInputAllowed&&!app.player.Paused&&Input.GetKey(KeyCode.T);
   keyTilt=Mathf.MoveTowards(keyTilt,key?115:0,(key?45:80)*dt);want=keyTilt;
  }
  // Drink pose: hand raised high in the camera image (hysteresis), M on the keyboard, or the probe.
  if(TestMouth.HasValue)mouth=TestMouth.Value;
  else if(frame!=null&&frame.landmarks!=null&&frame.landmarks.Length>=21){float y=(frame.landmarks[0].y+frame.landmarks[9].y)*.5f;raised=raised?y<.42f:y<.32f;mouth=raised;}
  else {if(app.player.GameplayInputAllowed&&!app.player.Paused&&Input.GetKeyDown(KeyCode.M))keyMouth=!keyMouth;mouth=keyMouth;}
  mouth&=held==cup.Item;
  CurrentTilt=Mathf.MoveTowards(CurrentTilt,want,300*dt);
  // The drink pose brings the cup to the mouth, just under the eyes; otherwise the activity's low-right presentation.
  app.PresentationOffset=mouth?new Vector3(0,-.07f,-.26f):new Vector3(.14f,-.12f,0);
 }

 // ---------------- Simulation
 void Update(){
  if(app==null||teapot==null||cup==null)return;
  float dt=Mathf.Min(Time.deltaTime,.05f);if(dt<=0)return;
  var held=app.Held;ReadInput(held,dt);
  ApplyTilt(held);
  SampleTremor(held);
  Pour(dt);
  StepCup(dt,held);
  RenderCup();Puddles(dt);
 }

 void ApplyTilt(SqueezableObject held){
  bool vessel=held!=null&&held.GetComponent<PourVessel>()!=null;
  if(!vessel||CurrentTilt<.05f){if(app.HeldTilt!=Quaternion.identity)app.HeldTilt=Quaternion.identity;appliedTilt=Quaternion.identity;return;}
  bool isPot=held==teapot.Item;
  // The vessel's own facing without the tilt this script added: its spout (teapot) or the side toward the player (cup).
  var baseRotation=Quaternion.Inverse(appliedTilt)*held.transform.rotation;
  Vector3 dir;
  if(isPot)dir=baseRotation*Vector3.right;
  else {dir=app.player.view.transform.position-held.transform.position;}
  dir.y=0;if(dir.sqrMagnitude<1e-6f)dir=Vector3.forward;dir.Normalize();
  var axis=Vector3.Cross(Vector3.up,dir);
  appliedTilt=Quaternion.AngleAxis(CurrentTilt,axis);app.HeldTilt=appliedTilt;
  // Pivot: the grip on the teapot handle; the near rim of the cup (it tips about the lips).
  app.HeldTiltPivotLocal=isPot?TeapotGrip:Quaternion.Inverse(baseRotation)*(dir*CupInnerTop)+Vector3.up*(CupBottom+CupDepth);
 }

 void SampleTremor(SqueezableObject held){
  if(!app.CameraMode||held==null||held.GetComponent<PourVessel>()==null){if(estimator.Count>0&&held==null)estimator.Clear();return;}
  var f=Frame();if(f==null||f.landmarks==null||f.landmarks.Length<21||f.captureSeconds<=lastFrameTime)return;lastFrameTime=f.captureSeconds;
  float w=f.imageWidth>0?f.imageWidth:640,h=f.imageHeight>0?f.imageHeight:480;var p=f.landmarks;
  // Millimetres per pixel from the knuckle breadth (index to little finger base), about 80 mm in an adult hand: an estimate.
  float px=new Vector2((p[17].x-p[5].x)*w,(p[17].y-p[5].y)*h).magnitude;if(px<8)return;float mm=80f/px;
  estimator.Add(f.captureSeconds,p[0].x*w*mm,p[0].y*h*mm);
  if(Time.unscaledTime>=nextTremor){nextTremor=Time.unscaledTime+.5f;if(estimator.Measure(out float rms,out float hz))tremor.Add(rms,hz);}
 }

 void Pour(float dt){
  var t=teapot.transform;var s=t.TransformDirection(Vector3.right);
  float pitch=Mathf.Asin(Mathf.Clamp(-s.y,-1,1));
  float out_=pot.Pour(pitch,dt);float ml=LiquidMath.Ml(out_);FlowMlPerSecond=ml/dt;
  if(ml<=0){streamRenderer.enabled=false;LevelSeconds+=dt;SetLoop(pourSource,0,1);return;}
  LevelSeconds=0;PouredMl+=ml;PourSeconds+=dt;flow.Add(FlowMlPerSecond);tilt.Add(CurrentTilt);
  // The stream: leaves the spout tip along the spout at the Torricelli exit speed, then falls.
  var tip=t.TransformPoint(SpoutTip);var v0=t.TransformDirection(SpoutDirection)*Mathf.Max(.15f,pot.ExitSpeed(pitch));
  float q=FlowMlPerSecond/LiquidMath.MlPerM3;var g=Physics.gravity;
  int n=1;path[0]=tip;pathRadius[0]=Mathf.Sqrt(q/(Mathf.PI*v0.magnitude));
  int where=0;Vector3 hitPoint=tip,hitNormal=Vector3.up;Collider hitCollider=null;const float step=.018f;
  for(int i=1;i<StreamRings&&where==0;i++){
   float tt=i*step;var p=tip+v0*tt+.5f*g*tt*tt;var a=path[i-1];
   if(CrossesCupMouth(a,p,out var mouthPoint)){where=1;p=mouthPoint;}
   else {
    var d=p-a;int hc=Physics.RaycastNonAlloc(a,d.normalized,hits,d.magnitude,~((1<<8)),QueryTriggerInteraction.Ignore);float best=float.MaxValue;
    for(int h=0;h<hc;h++){var c=hits[h].collider;if(c==null||c.transform.IsChildOf(teapot.transform))continue;if(hits[h].distance<best){best=hits[h].distance;hitPoint=hits[h].point;hitNormal=hits[h].normal;hitCollider=c;}}
    if(hitCollider!=null){where=2;p=hitPoint;}
   }
   path[i]=p;var vel=v0+g*tt;pathRadius[i]=Mathf.Sqrt(q/(Mathf.PI*Mathf.Max(.2f,vel.magnitude)));n=i+1;
  }
  if(where==0){where=2;hitPoint=path[n-1];hitNormal=Vector3.up;}
  if(where==1){
   float rest=cupLiquid.Add(out_);IntoCupMl+=ml-LiquidMath.Ml(rest);if(rest>0){Overflowed=true;Spill(cup.transform.position,LiquidMath.Ml(rest));}
   var kick=UnityEngine.Random.insideUnitCircle*Mathf.Min(.25f,ml*.02f);cupLiquid.Kick(kick.x,kick.y);ripple=Mathf.Min(1,ripple+ml*.08f);
  } else {
   // Off the cup: splashes, and the tea runs down to the surface under it (a cup wall, the table, the floor).
   if(hitCollider!=null&&hitCollider.transform.IsChildOf(cup.transform)&&Physics.Raycast(hitPoint+hitNormal*.01f,Vector3.down,out var under,1.5f,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore))hitPoint=under.point;
   Spill(hitPoint,ml);Splash(hitPoint,hitNormal,ml);
  }
  // Thin streams break into drops (low flow); a fuller stream is a continuous tube.
  if(FlowMlPerSecond<5){streamRenderer.enabled=false;Drip(tip,v0,ml);}
  else BuildStream(n);
  SetLoop(pourSource,Mathf.Clamp01(FlowMlPerSecond/35f)*.55f,.85f+.55f*cupLiquid.FillFraction);
  pourSource.transform.position=where==1?cup.transform.position:hitPoint;
 }

 bool CrossesCupMouth(Vector3 a,Vector3 b,out Vector3 point){
  point=b;var ct=cup.transform;var up=ct.up;if(up.y<.4f)return false; // a cup on its side has no mouth to fill
  var rim=ct.TransformPoint(0,CupBottom+CupDepth,0);float da=Vector3.Dot(a-rim,up),db=Vector3.Dot(b-rim,up);
  if(!(da>=0&&db<0))return false;
  point=a+(b-a)*(da/(da-db));var radial=point-rim;radial-=up*Vector3.Dot(radial,up);
  return radial.magnitude<=CupInnerTop*.97f;
 }

 void StepCup(float dt,SqueezableObject held){
  var ct=cup.transform;var pos=ct.position;
  if(!cupPrimed){cupPrevPos=pos;cupPrevVel=Vector3.zero;cupPrimed=true;}
  var vel=(pos-cupPrevPos)/dt;var acc=(vel-cupPrevVel)/dt;cupPrevPos=pos;cupPrevVel=vel;
  cupAccel=Vector3.Lerp(cupAccel,Vector3.ClampMagnitude(acc,30),1-Mathf.Exp(-dt/.025f));
  var up=ct.up;float tiltX=up.y>.05f?up.x/up.y:Mathf.Sign(up.x)*20,tiltZ=up.y>.05f?up.z/up.y:Mathf.Sign(up.z)*20;
  bool heldCup=held!=null&&held==cup.Item;
  // At the mouth, what the tipped cup cannot hold goes into the mouth (a gulp is capped, the rest runs down the chin).
  AtMouth=heldCup&&app.PresentationOffset.z<-.1f&&Vector3.Distance(ct.TransformPoint(0,CupBottom+CupDepth,0),app.player.view.transform.position)<.26f;
  if(AtMouth&&cupLiquid.Volume>0){
   float keep=LiquidMath.RetainedVolume(cupLiquid.MeanRadius,cupLiquid.height,cupLiquid.SurfaceSlope(tiltX,tiltZ));
   float drink=Mathf.Min(Mathf.Max(0,cupLiquid.Volume-keep),LiquidMath.M3(40)*dt);
   if(drink>0){DrankMl+=LiquidMath.Ml(cupLiquid.Remove(drink));DrinkSeconds+=dt;}
   SetLoop(sipSource,drink>0?.45f:0,1);
  } else SetLoop(sipSource,0,1);
  float lost=cupLiquid.Step(dt,tiltX,tiltZ,cupAccel.x,cupAccel.z);
  if(lost>0){
   float ml=LiquidMath.Ml(lost);if(heldCup&&!AtMouth)CarrySpillMl+=ml;
   // It leaves over the lowest point of the rim.
   var low=new Vector3(up.x,0,up.z);var rimDir=low.sqrMagnitude>1e-6f?low.normalized:new Vector3(cupLiquid.SloshX,0,cupLiquid.SloshZ).normalized;
   var lip=ct.TransformPoint(0,CupBottom+CupDepth,0)+rimDir*CupInnerTop;
   Drip(lip,rimDir*.15f,ml);
   if(Physics.Raycast(lip+Vector3.up*.01f,Vector3.down,out var under,2,~((1<<8)|(1<<9)),QueryTriggerInteraction.Ignore))Spill(under.point,ml);else SpilledMl+=ml;
  }
 }

 void Spill(Vector3 at,float ml){
  if(!(ml>0))return;SpilledMl+=ml;
  int best=-1;float bestD=float.MaxValue;
  for(int i=0;i<PuddleCount;i++){if(!puddles[i].gameObject.activeSelf)continue;float r=PuddleRadius(puddleMl[i])+.02f;var d=puddles[i].position-at;if(Mathf.Abs(d.y)>.02f)continue;d.y=0;if(d.magnitude<r&&d.magnitude<bestD){bestD=d.magnitude;best=i;}}
  if(best<0){best=nextPuddle;nextPuddle=(nextPuddle+1)%PuddleCount;puddleMl[best]=0;puddleShown[best]=0;puddles[best].position=at+Vector3.up*.0012f;puddles[best].localScale=new Vector3(.001f,1,.001f);puddles[best].gameObject.SetActive(true);}
  else {float w=ml/(puddleMl[best]+ml);puddles[best].position=Vector3.Lerp(puddles[best].position,at+Vector3.up*.0012f,w*.3f);}
  puddleMl[best]+=ml;
 }
 static float PuddleRadius(float ml)=>Mathf.Min(.22f,Mathf.Sqrt(ml*1e-6f/(Mathf.PI*.0012f))); // a 1.2 mm film
 void Puddles(float dt){
  for(int i=0;i<PuddleCount;i++){if(!puddles[i].gameObject.activeSelf)continue;float want=PuddleRadius(puddleMl[i]);
   if(Mathf.Abs(puddleShown[i]-want)<1e-4f)continue;puddleShown[i]=Mathf.MoveTowards(puddleShown[i],want,.12f*dt+(want-puddleShown[i])*2*dt);
   puddles[i].localScale=new Vector3(puddleShown[i],1,puddleShown[i]);}
 }
 void Splash(Vector3 at,Vector3 normal,float ml){
  int count=Mathf.Clamp(Mathf.RoundToInt(ml*1.5f),1,6);
  for(int i=0;i<count;i++){var d=(normal+UnityEngine.Random.insideUnitSphere*.8f).normalized;emit.position=at+normal*.004f;emit.velocity=d*UnityEngine.Random.Range(.15f,.6f);drops.Emit(emit,1);}
 }
 void Drip(Vector3 at,Vector3 velocity,float ml){
  int count=Mathf.Clamp(Mathf.RoundToInt(ml*3),1,8);
  for(int i=0;i<count;i++){emit.position=at;emit.velocity=velocity+UnityEngine.Random.insideUnitSphere*.05f;drops.Emit(emit,1);}
 }

 void BuildStream(int n){
  // Tube through the path; rings past the end collapse onto the last point (world-space mesh).
  float wobble=Time.time*31;
  for(int r=0;r<StreamRings;r++){
   int i=Mathf.Min(r,n-1);var c=path[i];var fwd=(i<n-1?path[i+1]-path[i]:path[i]-path[Mathf.Max(0,i-1)]);if(fwd.sqrMagnitude<1e-10f)fwd=Vector3.down;fwd.Normalize();
   var side=Vector3.Cross(fwd,Vector3.right);if(side.sqrMagnitude<1e-6f)side=Vector3.Cross(fwd,Vector3.forward);side.Normalize();var up=Vector3.Cross(fwd,side);
   float radius=r<n?Mathf.Clamp(pathRadius[i],.0008f,.006f)*(1+.07f*Mathf.Sin(wobble+r*1.7f)):0;
   for(int s=0;s<StreamSides;s++){float a=s*Mathf.PI*2/StreamSides;streamVerts[r*StreamSides+s]=c+(side*Mathf.Cos(a)+up*Mathf.Sin(a))*radius;}
  }
  streamMesh.vertices=streamVerts;streamMesh.RecalculateNormals();streamMesh.RecalculateBounds();streamRenderer.enabled=true;
 }

 void RenderCup(){
  float ml=CupMl;liquidRenderer.enabled=ml>.5f;if(!liquidRenderer.enabled)return;
  var space=liquidTransform;float yb=CupBottom,H=CupDepth,r0=CupInnerBottom,r1=CupInnerTop,k=(r1-r0)/H;
  var n=space.InverseTransformDirection(new Vector3(-cupLiquid.SloshX,1,-cupLiquid.SloshZ).normalized);
  var centre=new Vector3(0,yb+cupLiquid.Level,0);float c=Vector3.Dot(n,centre);
  liquidVerts[0]=new Vector3(0,yb,0);
  ripple=Mathf.MoveTowards(ripple,0,Time.deltaTime*.8f);float t=Time.time;
  float topSum=0;
  for(int i=0;i<Around;i++){
   float phi=i*Mathf.PI*2/Around,cs=Mathf.Cos(phi),sn=Mathf.Sin(phi),a=n.x*cs+n.z*sn,den=a*k+n.y;
   float y=Mathf.Abs(den)>1e-4f?(c-a*r0+a*k*yb)/den:yb+cupLiquid.Level;
   y=Mathf.Clamp(y+ripple*.0012f*Mathf.Sin(3*phi+t*14),yb,yb+H);float r=r0+k*(y-yb);
   liquidVerts[1+i]=new Vector3(r0*cs,yb,r0*sn);liquidVerts[1+Around+i]=new Vector3(r*cs,y,r*sn);topSum+=y;
  }
  liquidVerts[Around*2+1]=new Vector3(0,Mathf.Clamp(topSum/Around,yb,yb+H),0);
  liquidMesh.vertices=liquidVerts;liquidMesh.RecalculateNormals();liquidMesh.RecalculateBounds();
 }

 static void SetLoop(AudioSource s,float volume,float pitch){
  if(s==null)return;s.volume=Mathf.MoveTowards(s.volume,volume,Time.deltaTime*3);s.pitch=pitch;
  if(s.volume>.001f&&!s.isPlaying)s.Play();else if(s.volume<=.001f&&s.isPlaying)s.Stop();
 }

  /// <summary>Records the cup's fill when the pour step ends (the measured fill for the score).</summary>
 public void MarkFilled(){FilledMl=CupMl;}
 /// <summary>Levels the vessel (voice "stop pouring", the end of the activity).</summary>
 public void Level(){TestTilt=float.NaN;keyTilt=0;rollBase=float.NaN;CurrentTilt=0;app.HeldTilt=Quaternion.identity;appliedTilt=Quaternion.identity;}
 void OnDestroy(){if(app!=null){app.HeldTilt=Quaternion.identity;}}
}

/// <summary>Synthesised tea sounds (ADR-AU-1): no recordings.</summary>
static class TeaSounds {
 const int Rate=44100;
 /// <summary>A seamless 1 s loop of band-passed noise with a gurgle: liquid poured into a cup.</summary>
 public static AudioClip Pour(){
  var data=new float[Rate];var rng=new System.Random(11);float lp=0,bp=0;
  for(int i=0;i<data.Length;i++){
   float white=(float)(rng.NextDouble()*2-1);lp+=.18f*(white-lp);bp+=.5f*(lp-bp);float gurgle=.5f+.5f*Mathf.Sin(i*2*Mathf.PI*7f/Rate)*Mathf.Sin(i*2*Mathf.PI*2f/Rate);
   data[i]=(lp-bp)*.9f*(.6f+.4f*gurgle);
  }
  data=Loop(data,2000);var clip=AudioClip.Create("Tea pour",data.Length,1,Rate,false);clip.SetData(data,0);return clip;
 }
 /// <summary>A 0.6 s loop of soft low swallow pulses.</summary>
 public static AudioClip Sip(){
  int n=(int)(Rate*.6f);var data=new float[n];var rng=new System.Random(5);float lp=0;
  for(int i=0;i<n;i++){float white=(float)(rng.NextDouble()*2-1);lp+=.04f*(white-lp);float env=Mathf.Pow(Mathf.Sin(Mathf.PI*i/n),2);data[i]=lp*3.2f*env;}
  data=Loop(data,800);var clip=AudioClip.Create("Tea sip",data.Length,1,Rate,false);clip.SetData(data,0);return clip;
 }
 /// <summary>Seamless loop: the last m samples are faded into the first m and dropped.</summary>
 static float[] Loop(float[] d,int m){var o=new float[d.Length-m];for(int i=0;i<o.Length;i++){if(i<m){float w=i/(float)m;o[i]=d[i]*w+d[d.Length-m+i]*(1-w);}else o[i]=d[i];}return o;}
}
}
