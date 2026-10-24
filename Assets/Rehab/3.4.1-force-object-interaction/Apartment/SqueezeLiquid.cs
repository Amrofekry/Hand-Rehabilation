using UnityEngine;
namespace GripRehab {
// Stage 3.4: liquid squeezed out of a held object (sponge water, tomato juice) and a burst
// when it breaks. A lightweight in-house stand-in for the PDF's Obi Fluid role: visual
// feedback only, it never pushes rigidbodies or changes any measured value.
[DisallowMultipleComponent]
public sealed class SqueezeLiquid:MonoBehaviour {
 public SqueezableObject source;
 ParticleSystem particles;ParticleSystem.EmitParams emit;float carry;DamageState lastDamage;
 public int EmittedCount{get;private set;}
 public bool Active=>particles!=null;

 public static SqueezeLiquid Attach(SqueezableObject item){
  var p=item!=null?item.profile:null;
  if(p==null||!p.releasesLiquid||p.liquidMaterial==null)return null;
  var go=new GameObject("Squeeze liquid");go.transform.SetParent(item.transform,false);
  var liquid=go.AddComponent<SqueezeLiquid>();liquid.source=item;liquid.Build(p);return liquid;
 }

 void Build(PhysicalMaterialProfile p){
  particles=gameObject.AddComponent<ParticleSystem>();
  particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
  var main=particles.main;main.loop=false;main.playOnAwake=false;main.maxParticles=240;
  main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=new ParticleSystem.MinMaxCurve(.6f,1.1f);
  main.startSpeed=new ParticleSystem.MinMaxCurve(.05f,.25f);main.startSize=new ParticleSystem.MinMaxCurve(.005f,.011f);
  main.gravityModifier=1;main.startColor=p.liquidColor;
  var emission=particles.emission;emission.enabled=false; // Emitted by hand from compression.
  var shape=particles.shape;shape.enabled=false;
  var collision=particles.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.World;
  collision.quality=ParticleSystemCollisionQuality.Low;collision.bounce=.05f;collision.dampen=.8f;collision.lifetimeLoss=.4f;collision.sendCollisionMessages=false;
  var fade=particles.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();
  gradient.SetKeys(new[]{new GradientColorKey(p.liquidColor,0),new GradientColorKey(p.liquidColor,1)},new[]{new GradientAlphaKey(p.liquidColor.a,0),new GradientAlphaKey(0,1)});fade.color=gradient;
  var renderer=GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=p.liquidMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;
  renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
  particles.Play();lastDamage=source.Damage;
 }

 void LateUpdate(){
  if(particles==null||source==null)return;
  var p=source.profile;float dt=Time.deltaTime;
  // Squeeze: drips grow with compression above the threshold while the object is held.
  float over=source.IsHeld?Mathf.Clamp01((source.Compression-p.liquidStartCompression)/Mathf.Max(.01f,1-p.liquidStartCompression)):0;
  carry+=over*p.liquidRate*dt;
  int count=Mathf.Min(12,Mathf.FloorToInt(carry));carry-=count;
  if(count>0)Emit(count,.25f);
  if(source.Damage==DamageState.Broken&&lastDamage!=DamageState.Broken&&p.liquidBurst>0)Emit(p.liquidBurst,1.2f);
  lastDamage=source.Damage;
 }

 void Emit(int count,float spread){
  float radius=source.ClearanceRadius>0?source.ClearanceRadius*.6f:.04f;
  for(int i=0;i<count;i++){
   var dir=Random.onUnitSphere;dir.y=-Mathf.Abs(dir.y)*.5f-.2f;dir.Normalize();
   emit.position=source.transform.position+dir*radius;
   emit.velocity=dir*Random.Range(.05f,.25f)*(1+spread)+Vector3.down*.05f;
   particles.Emit(emit,1);EmittedCount++;
  }
 }

 public void Clear(){if(particles!=null)particles.Clear();carry=0;lastDamage=source!=null?source.Damage:DamageState.Intact;}
}
}
