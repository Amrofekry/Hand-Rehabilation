using UnityEngine;
namespace GripRehab {
[RequireComponent(typeof(Rigidbody))]
public sealed class SqueezableObject : MonoBehaviour {
 [Header("Material")] public string displayName="Therapy ball";
 public PhysicalMaterialProfile profile;
 public Transform visual;
 [Header("Legacy scene settings (used only without a profile)")]
 public float fullCompressionNewtons=35, maximumCompression=.28f, fractureNewtons=55, radius=.1f;
 public bool breakable;
 public GameObject fragmentsPrefab;
 [Header("Debug")] public bool showContactGizmos;
 public bool IsHeld{get;private set;}
 public bool IsBroken=>Damage==DamageState.Broken;
 public DamageState Damage{get;private set;}
 public float DamageValue{get;private set;}
 public float Compression{get;private set;}
 public int ContactCount{get;private set;}
 public int DeformedVertices{get;private set;}
 public float MaxDisplacementMeters{get;private set;}
 public float BreakThresholdNewtons=>profile!=null&&profile.breakable?profile.sustainedForceNewtons:0;
 public Rigidbody Body{get;private set;}
 Vector3 origin,restScale;Quaternion rotation;
 Mesh mesh;Vector3[] rest,vertices,permanent,displacements;bool[] touched;
 PhysicsMaterial physicsMaterial;GameObject fragments;Rigidbody[] shards;Vector3[] shardPositions;Quaternion[] shardRotations;
 float dwell;Vector3 lastPoint;float lastRadius;
 void Awake(){
  Body=GetComponent<Rigidbody>();origin=transform.position;rotation=transform.rotation;
  if(visual==null)visual=transform;restScale=visual.localScale;
  if(profile!=null){
   Body.mass=profile.massKg;
   physicsMaterial=new PhysicsMaterial(displayName+" physics"){dynamicFriction=profile.friction,staticFriction=profile.friction,bounciness=profile.restitution};
   foreach(var c in GetComponents<Collider>())c.material=physicsMaterial;
  }
  if(fragmentsPrefab!=null)PrepareFragments();
  var filter=visual.GetComponent<MeshFilter>();
  if(filter!=null&&filter.sharedMesh!=null&&filter.sharedMesh.isReadable&&filter.sharedMesh.vertexCount<=(profile!=null?profile.maxVertices:4096)&&(profile==null||profile.deformable)){
   mesh=Instantiate(filter.sharedMesh);mesh.name=displayName+" contact mesh";filter.sharedMesh=mesh;
   rest=mesh.vertices;vertices=(Vector3[])rest.Clone();permanent=new Vector3[rest.Length];touched=new bool[rest.Length];displacements=new Vector3[rest.Length];
  }
 }
 public bool Grab(){
  if(IsBroken||IsHeld)return false;
  IsHeld=true;Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;Body.isKinematic=true;return true;
 }
 public void MoveHeld(Vector3 position,Quaternion heldRotation){if(IsHeld){Body.MovePosition(position);Body.MoveRotation(heldRotation);}}
 public void Release(Vector3 velocity){
  if(!IsHeld)return;IsHeld=false;Body.isKinematic=false;Body.useGravity=true;
  Body.linearVelocity=Vector3.ClampMagnitude(velocity,2);Body.WakeUp();
 }
 // Physics-step contact resolution. A noncontacting probe contributes exactly zero.
 public void ApplyContacts(FingerContactRig rig,float newtons,float dt){
  if(IsBroken||!gameObject.activeInHierarchy)return;
  float force=float.IsFinite(newtons)?Mathf.Max(0,newtons):0;
  ContactCount=0;
  float radiusWorld=profile!=null?profile.deformationRadiusMeters:Mathf.Max(.015f,radius*.55f);
  float full=profile!=null?profile.fullCompressionNewtons:fullCompressionNewtons;
  float maxMeters=profile!=null?profile.maxDepthMeters:maximumCompression*radius;
  if(touched!=null){System.Array.Clear(touched,0,touched.Length);System.Array.Clear(displacements,0,displacements.Length);}
  if(IsHeld&&rig!=null&&rig.probes!=null){
   foreach(var p in rig.probes)if(p!=null&&p.Contact==this)ContactCount++;
   if(ContactCount>0&&force>0){foreach(var p in rig.probes)if(p!=null&&p.Contact==this)p.SetForce(force/ContactCount);}
   if(ContactCount>0&&force>0&&mesh!=null){
    float depth=Mathf.Min(maxMeters,force/Mathf.Max(1,full)*maxMeters);
    foreach(var p in rig.probes)if(p!=null&&p.Contact==this){
     lastPoint=p.Point;lastRadius=radiusWorld;DeformAt(p.Point,p.Normal,depth,radiusWorld,dt);
    }
   }
  }
  Compression=(profile==null||profile.deformable)&&ContactCount>0&&force>0?Mathf.Clamp01(force/Mathf.Max(1,full)):0;
  if(mesh!=null)UpdateMesh(dt);
  // Meshless legacy objects have no localized solution, so never globally scale them.
  GripDamage(ContactCount>0?force:0,dt);
 }
 // Retained for compatibility; force alone cannot deform an object.
 public void ApplyForce(float newtons,float dt){ApplyContacts(null,0,dt);}
 void DeformAt(Vector3 point,Vector3 normal,float depth,float worldRadius,float dt){
  // Project onto the undeformed surface. Collider.ClosestPoint returns an interior
  // point when a phalanx penetrates deeply; that must not erase the indentation.
  int nearest=0;float best=float.PositiveInfinity;
  for(int i=0;i<rest.Length;i++){float d=(visual.TransformPoint(rest[i])-point).sqrMagnitude;if(d<best){best=d;nearest=i;}}
  Vector3 surface=visual.TransformPoint(rest[nearest]);
  Vector3 inward=-normal.normalized;
  if(inward.sqrMagnitude<.5f)inward=(visual.TransformPoint(mesh.bounds.center)-surface).normalized;
  for(int i=0;i<rest.Length;i++){
   Vector3 world=visual.TransformPoint(rest[i]);float distance=Vector3.Distance(world,surface);
   if(distance>=worldRadius)continue;
   float t=1-distance/worldRadius,falloff=t*t*(3-2*t);
   // Deterministic small asymmetry; no frame-to-frame random jitter.
   float irregular=.88f+.12f*Mathf.Sin(rest[i].x*29+rest[i].y*17+rest[i].z*23);
   Vector3 target=visual.InverseTransformVector(inward*(depth*falloff*irregular));
   if(target.sqrMagnitude>displacements[i].sqrMagnitude)displacements[i]=target;
   touched[i]=true;
  }
 }

 void UpdateMesh(float dt){
  float alpha=1-Mathf.Exp(-(profile!=null?profile.recoveryPerSecond:12)*Mathf.Max(0,dt));
  bool changed=false;DeformedVertices=0;MaxDisplacementMeters=0;
  for(int i=0;i<rest.Length;i++){
   if(touched[i]){
    vertices[i]=Vector3.Lerp(vertices[i],rest[i]+permanent[i]+displacements[i],1-Mathf.Exp(-20*dt));
    if(profile!=null&&profile.plasticity>0)permanent[i]=Vector3.Lerp(permanent[i],displacements[i]*.45f,profile.plasticity*dt);
   }
   Vector3 target=rest[i]+permanent[i];
   MaxDisplacementMeters=Mathf.Max(MaxDisplacementMeters,visual.TransformVector(vertices[i]-rest[i]).magnitude);
   if((vertices[i]-target).sqrMagnitude>1e-10f){
    if(!touched[i])vertices[i]=Vector3.Lerp(vertices[i],target,alpha);
    changed=true;DeformedVertices++;
   }
  }
  if(changed){mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();}
 }
 void GripDamage(float force,float dt){
  if(!(profile!=null?profile.breakable:breakable)||!IsHeld||force<=0){dwell=0;return;}
  float threshold=profile!=null?profile.sustainedForceNewtons:fractureNewtons;
  float gate=profile!=null?profile.forceDwellSeconds:.35f;
  if(force<threshold){dwell=0;return;}
  dwell+=Mathf.Max(0,dt);if(dwell<gate)return;
  float capacity=profile!=null?profile.damageCapacity:1;
  AddDamage(dt/Mathf.Max(.1f,gate*.5f)/capacity);
 }
 void OnCollisionEnter(Collision collision){
  if(IsHeld||IsBroken||!(profile!=null?profile.breakable:breakable))return;
  // Real-time energy proxy, not a finite-element stress simulation.
  float energy=.5f*Body.mass*collision.relativeVelocity.sqrMagnitude;
  float threshold=profile!=null?profile.impactEnergyJoules:.4f;
  if(energy>=threshold)AddDamage(Mathf.Clamp01(energy/threshold-.5f));
 }
 public void AddDamage(float amount){
  if(!(profile!=null?profile.breakable:breakable)||IsBroken||!float.IsFinite(amount)||amount<=0)return;
  DamageValue=Mathf.Clamp01(DamageValue+amount);
  Damage=DamageValue>=1?DamageState.Broken:DamageValue>=.75f?DamageState.CriticallyDamaged:DamageValue>=.5f?DamageState.Damaged:DamageValue>=.25f?DamageState.SlightlyDamaged:DamageState.Intact;
  if(IsBroken)Fracture();
 }
 public void Break(){AddDamage(1);}
 void PrepareFragments(){
  if(fragments!=null||fragmentsPrefab==null)return;
  fragments=Instantiate(fragmentsPrefab,transform.position,transform.rotation);
  shards=fragments.GetComponentsInChildren<Rigidbody>();
  shardPositions=new Vector3[shards.Length];shardRotations=new Quaternion[shards.Length];
  for(int i=0;i<shards.Length;i++){shardPositions[i]=shards[i].transform.localPosition;shardRotations[i]=shards[i].transform.localRotation;}
  fragments.SetActive(false);
 }
 void Fracture(){
  IsHeld=false;Body.isKinematic=true;visual.gameObject.SetActive(false);
  foreach(var c in GetComponents<Collider>())c.enabled=false;
  if(fragmentsPrefab==null)return;
  PrepareFragments();

  fragments.SetActive(false);fragments.transform.SetPositionAndRotation(transform.position,transform.rotation);
  for(int i=0;i<shards.Length;i++){
   var r=shards[i];r.isKinematic=true;r.transform.localPosition=shardPositions[i];r.transform.localRotation=shardRotations[i];
   r.linearVelocity=Vector3.zero;r.angularVelocity=Vector3.zero;
  }
  fragments.SetActive(true);
  for(int i=0;i<shards.Length;i++){
   var r=shards[i];r.isKinematic=false;r.WakeUp();
   Vector3 away=(r.worldCenterOfMass-transform.position).normalized;
   r.AddForce((away+Vector3.up*.3f)*.08f,ForceMode.Impulse);
  }
 }
 public void ResetObject(){
  if(Body==null)return;
  if(fragments!=null)fragments.SetActive(false);
  IsHeld=false;Damage=DamageState.Intact;DamageValue=dwell=Compression=0;ContactCount=DeformedVertices=0;
  Body.isKinematic=false;Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;
  transform.SetPositionAndRotation(origin,rotation);Body.position=origin;Body.rotation=rotation;
  visual.gameObject.SetActive(true);visual.localScale=restScale;
  foreach(var c in GetComponents<Collider>())c.enabled=true;
  if(mesh!=null){System.Array.Clear(permanent,0,permanent.Length);System.Array.Copy(rest,vertices,rest.Length);mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();}
  Body.WakeUp();
 }
 void OnDrawGizmos(){if(!showContactGizmos||lastRadius<=0)return;Gizmos.color=Color.yellow;Gizmos.DrawWireSphere(lastPoint,lastRadius);}
 void OnDestroy(){if(mesh!=null)Destroy(mesh);if(fragments!=null)Destroy(fragments);if(physicsMaterial!=null)Destroy(physicsMaterial);}
}
}
