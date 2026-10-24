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
 Collider[] objectColliders,playerColliders;
 readonly GraspCollisionShield collisionShield=new GraspCollisionShield();
 bool hasFreeBodySettings;
 CollisionDetectionMode freeCollisionMode;RigidbodyInterpolation freeInterpolation;
 public Vector3 ClosestSurface(Vector3 point){
  Vector3 nearest=transform.position;float best=float.PositiveInfinity;
  foreach(var collider in objectColliders){
   if(collider==null||!collider.enabled||collider.isTrigger||!collider.gameObject.activeInHierarchy)continue;
   Vector3 candidate=collider.ClosestPoint(point);float distance=(candidate-point).sqrMagnitude;
   if(distance<best){best=distance;nearest=candidate;}
  }
  return nearest;
 }
 public bool HasPickupCollider {
  get {foreach(var c in objectColliders)if(c!=null&&c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy)return true;return false;}
 }
 public float ClearanceRadius {
  get {float result=Mathf.Max(.01f,radius);foreach(var c in objectColliders)if(c!=null&&c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy)
   result=Mathf.Max(result,Vector3.Distance(Body.position,c.bounds.center)+c.bounds.extents.magnitude);return result;}
 }
 public void ProtectFromPlayer(Collider[] colliders){playerColliders=colliders;collisionShield.Protect(objectColliders,colliders);}
 void FixedUpdate(){collisionShield.Step(IsHeld);}

 Mesh mesh;Vector3[] rest,worldRest;
 LocalDeformationState deformation;ObjectFractureVisuals fractureVisuals;
 Collider[] shardColliders;bool[] colliderEnabled;
 FingerContactProbe[] boundProbes;CapsuleCollider[] boundCapsules;
 bool[] confirmed;Vector3[] contactPoints,contactNormals;
 public bool HasProbeContact(FingerContactProbe probe,out Vector3 point,out Vector3 normal){
  point=normal=Vector3.zero;
  return probe!=null&&probe.isActiveAndEnabled&&TryProbeContact(probe.GetComponent<CapsuleCollider>(),out point,out normal);
 }
 void BindContactRig(FingerContactRig rig){
  if(ReferenceEquals(boundProbes,rig.probes))return;
  boundProbes=rig.probes;boundCapsules=new CapsuleCollider[boundProbes.Length];confirmed=new bool[boundProbes.Length];
  contactPoints=new Vector3[boundProbes.Length];contactNormals=new Vector3[boundProbes.Length];
  for(int i=0;i<boundProbes.Length;i++)if(boundProbes[i]!=null)boundCapsules[i]=boundProbes[i].GetComponent<CapsuleCollider>();
 }
 Renderer[] visualRenderers;bool[] rendererEnabled;
 public SqueezeLiquid Liquid{get;private set;}
 public bool HasLocalizedDeformation=>deformation!=null;
 public int FragmentCount=>shards==null?0:shards.Length;
 public bool FragmentsVisible=>fragments!=null&&fragments.activeInHierarchy;
 public bool DamageSeamsVisible=>fractureVisuals!=null&&fractureVisuals.DamageVisible;
 public bool TryProbeContact(CapsuleCollider probe,out Vector3 point,out Vector3 normal){
  point=normal=Vector3.zero;if(probe==null||IsBroken||!gameObject.activeInHierarchy)return false;
  float deepest=0;
  foreach(var collider in objectColliders){
   if(collider==null||!collider.enabled||collider.isTrigger||!collider.gameObject.activeInHierarchy)continue;
   if(Physics.ComputePenetration(probe,probe.transform.position,probe.transform.rotation,collider,collider.transform.position,collider.transform.rotation,out var direction,out float depth)&&depth>deepest){
    deepest=depth;normal=direction;point=collider.ClosestPoint(probe.transform.position);
   }
  }
  return deepest>0;
 }
 PhysicMaterial physicsMaterial;GameObject fragments;Rigidbody[] shards;Vector3[] shardPositions;Quaternion[] shardRotations;
 float dwell;Vector3 lastPoint;float lastRadius;
 void Awake(){
  Body=GetComponent<Rigidbody>();origin=transform.position;rotation=transform.rotation;
  objectColliders=System.Array.FindAll(GetComponentsInChildren<Collider>(true),c=>c.attachedRigidbody==Body);
  if(visual==null)visual=transform;restScale=visual.localScale;
  colliderEnabled=System.Array.ConvertAll(objectColliders,c=>c.enabled);
  visualRenderers=visual.GetComponentsInChildren<Renderer>(true);rendererEnabled=System.Array.ConvertAll(visualRenderers,r=>r.enabled);
  if(profile!=null){
   Body.mass=profile.massKg;
   physicsMaterial=new PhysicMaterial(displayName+" physics"){dynamicFriction=profile.friction,staticFriction=profile.friction,bounciness=profile.restitution};
   foreach(var c in objectColliders)c.material=physicsMaterial;
  }
  var filter=visual.GetComponent<MeshFilter>();
  if(profile!=null&&profile.breakable&&filter!=null&&filter.sharedMesh!=null&&filter.sharedMesh.vertexCount<=profile.maxVertices)
   fractureVisuals=ObjectFractureVisuals.Create(transform,filter,visual.GetComponent<Renderer>(),Body.mass);
  if(fractureVisuals!=null||fragmentsPrefab!=null)PrepareFragments();
  if(filter!=null&&filter.sharedMesh!=null&&filter.sharedMesh.isReadable&&filter.sharedMesh.vertexCount<=(profile!=null?profile.maxVertices:4096)&&(profile==null||profile.deformable)){
   mesh=Instantiate(filter.sharedMesh);mesh.name=displayName+" contact mesh";mesh.MarkDynamic();filter.sharedMesh=mesh;
   rest=mesh.vertices;worldRest=new Vector3[rest.Length];deformation=new LocalDeformationState(rest);
  }
  Liquid=SqueezeLiquid.Attach(this); // Stage 3.4: null unless the profile releases liquid.
 }
 public bool Grab(){
  if(IsBroken||IsHeld)return false;
  hasFreeBodySettings=true;freeCollisionMode=Body.collisionDetectionMode;freeInterpolation=Body.interpolation;
  if(!Body.isKinematic){Body.velocity=Vector3.zero;Body.angularVelocity=Vector3.zero;}
  IsHeld=true;Body.isKinematic=true;Body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
  Body.interpolation=RigidbodyInterpolation.Interpolate;return true;
 }
 public void MoveHeld(Vector3 position,Quaternion heldRotation){if(IsHeld){Body.MovePosition(position);Body.MoveRotation(heldRotation);}}
 public void Release(Vector3 velocity){
  if(!IsHeld)return;IsHeld=false;Body.isKinematic=false;Body.useGravity=true;
  RestoreFreeBodySettings();
  Body.velocity=GraspMotionPolicy.SafeVelocity(velocity,2);Body.angularVelocity=Vector3.zero;Body.WakeUp();
  // Keep player exclusion until separation, without disabling floor collisions.
  collisionShield.Step(false);
 }
 void RestoreFreeBodySettings(){if(!hasFreeBodySettings)return;Body.collisionDetectionMode=freeCollisionMode;Body.interpolation=freeInterpolation;hasFreeBodySettings=false;}
 // Physics-step contact resolution. No contact means no applied pressure or damage.
 public void ApplyContacts(FingerContactRig rig,float newtons,float dt){
  if(IsBroken||!gameObject.activeInHierarchy)return;
  dt=ContactDeformation.SafeDt(dt);
  float force=float.IsFinite(newtons)?Mathf.Max(0,newtons):0;
  ContactCount=0;deformation?.BeginStep();
  float radiusWorld=profile!=null?profile.deformationRadiusMeters:Mathf.Max(.015f,radius*.55f);
  float full=profile!=null?profile.fullCompressionNewtons:fullCompressionNewtons;
  float maximum=profile!=null?profile.maxDepthMeters:maximumCompression*radius;
  if(IsHeld&&rig!=null&&rig.probes!=null){
   BindContactRig(rig);
   foreach(var probe in boundProbes)probe?.SetForce(0);
   // Confirm current geometry, independent of callback ordering or cached probe.Contact.
   for(int i=0;i<boundProbes.Length;i++){
    confirmed[i]=boundProbes[i]!=null&&boundProbes[i].isActiveAndEnabled&&TryProbeContact(boundCapsules[i],out contactPoints[i],out contactNormals[i]);
    if(confirmed[i])ContactCount++;
   }
   if(ContactCount>0&&force>0&&dt>0){
    float depth=ContactDeformation.Depth(force,full,maximum,ContactCount);
    if(deformation!=null)for(int i=0;i<rest.Length;i++)worldRest[i]=visual.TransformPoint(rest[i]);
    for(int i=0;i<boundProbes.Length;i++)if(confirmed[i]){
     boundProbes[i].SetForce(force/ContactCount);
     if(deformation!=null){lastPoint=contactPoints[i];lastRadius=radiusWorld;DeformAt(contactPoints[i],contactNormals[i],depth,radiusWorld);}
    }
   }
  }
  Compression=deformation!=null&&ContactCount>0&&force>0&&dt>0?Mathf.Clamp01(force/Mathf.Max(1,full)):0;
  if(deformation!=null)UpdateMesh(dt);
  GripDamage(ContactCount>0?force:0,dt);
 }
 // Compatibility entry point: force without contact must never produce dents.
 public void ApplyForce(float newtons,float dt){ApplyContacts(null,0,dt);}
 void DeformAt(Vector3 point,Vector3 normal,float depth,float worldRadius){
  if(!GraspMotionPolicy.Finite(point)||!GraspMotionPolicy.Finite(normal)||depth<=0)return;
  int nearest=0;float best=float.PositiveInfinity;
  for(int i=0;i<worldRest.Length;i++){float d=(worldRest[i]-point).sqrMagnitude;if(d<best){best=d;nearest=i;}}
  Vector3 surface=worldRest[nearest];
  if(normal.sqrMagnitude<.000001f)normal=surface-visual.TransformPoint(mesh.bounds.center);
  // Resolve occasional interior/ambiguous penetration directions inward.
  if(Vector3.Dot(-normal,visual.TransformPoint(mesh.bounds.center)-surface)<0)normal=-normal;
  float bulge=profile!=null&&profile.kind==MaterialKind.Rubber?.18f:profile!=null&&profile.kind==MaterialKind.Foam?.03f:0;
  for(int i=0;i<rest.Length;i++){
   var delta=ContactDeformation.Indentation(worldRest[i],surface,normal,depth,worldRadius,rest[i],bulge);
   deformation.SetTarget(i,visual.InverseTransformVector(delta));
  }
 }
 void UpdateMesh(float dt){
  bool changed=deformation.Step(dt,profile!=null?profile.recoveryPerSecond:12,profile!=null?profile.plasticity:0);
  DeformedVertices=0;MaxDisplacementMeters=0;
  for(int i=0;i<rest.Length;i++){
   float distance=visual.TransformVector(deformation.Vertices[i]-rest[i]).magnitude;
   MaxDisplacementMeters=Mathf.Max(MaxDisplacementMeters,distance);
   if(distance>.00001f)DeformedVertices++;
  }
  if(changed){mesh.SetVertices(deformation.Vertices);mesh.RecalculateNormals();mesh.RecalculateBounds();}
 }
 void GripDamage(float force,float dt){
  bool eligible=(profile!=null?profile.breakable:breakable)&&IsHeld;
  float increment=ContactDeformation.GripDamage(ref dwell,eligible,force,dt,
   profile!=null?profile.sustainedForceNewtons:fractureNewtons,profile!=null?profile.forceDwellSeconds:.35f,profile!=null?profile.damageCapacity:1);
  if(increment>0)AddDamage(increment);
 }
 void OnCollisionEnter(Collision collision){
  if(IsHeld||IsBroken||!(profile!=null?profile.breakable:breakable))return;
  AddDamage(ContactDeformation.ImpactDamage(Body.mass,collision.relativeVelocity.sqrMagnitude,profile!=null?profile.impactEnergyJoules:.4f));
 }
 public void AddDamage(float amount){
  if(!(profile!=null?profile.breakable:breakable)||IsBroken||!float.IsFinite(amount)||amount<=0)return;
  DamageValue=Mathf.Clamp01(DamageValue+amount);
  Damage=DamageValue>=1?DamageState.Broken:DamageValue>=.75f?DamageState.CriticallyDamaged:DamageValue>=.5f?DamageState.Damaged:DamageValue>=.25f?DamageState.SlightlyDamaged:DamageState.Intact;
  fractureVisuals?.SetDamage(DamageValue,IsBroken);
  if(IsBroken)Fracture();
 }
 public void Break(){AddDamage(1);}
 void PrepareFragments(){
  if(fragments!=null)return;
  if(fractureVisuals!=null)fragments=fractureVisuals.Root;
  else if(fragmentsPrefab!=null)fragments=Instantiate(fragmentsPrefab,transform.position,transform.rotation);
  if(fragments==null)return;
  shards=fragments.GetComponentsInChildren<Rigidbody>(true);shardColliders=fragments.GetComponentsInChildren<Collider>(true);
  shardPositions=new Vector3[shards.Length];shardRotations=new Quaternion[shards.Length];
  for(int i=0;i<shards.Length;i++){shardPositions[i]=shards[i].transform.localPosition;shardRotations[i]=shards[i].transform.localRotation;}
  fragments.SetActive(false);
 }
 void Fracture(){
  Vector3 inherited=Body.isKinematic?Vector3.zero:GraspMotionPolicy.SafeVelocity(Body.velocity,1.2f);
  IsHeld=false;Body.isKinematic=true;
  if(visual!=transform)visual.gameObject.SetActive(false);else foreach(var renderer in visualRenderers)renderer.enabled=false;
  foreach(var collider in objectColliders)collider.enabled=false;
  if(fractureVisuals==null&&fragmentsPrefab==null)return;
  PrepareFragments();

  fragments.SetActive(false);fragments.transform.SetPositionAndRotation(transform.position,transform.rotation);
  for(int i=0;i<shards.Length;i++){
   var r=shards[i];r.isKinematic=true;r.transform.localPosition=shardPositions[i];r.transform.localRotation=shardRotations[i];
  }
  fragments.SetActive(true);
  collisionShield.Protect(shardColliders,playerColliders);
  for(int i=0;i<shardColliders.Length;i++)for(int j=i+1;j<shardColliders.Length;j++)Physics.IgnoreCollision(shardColliders[i],shardColliders[j],true);
  for(int i=0;i<shards.Length;i++){
   var r=shards[i];r.isKinematic=false;
   Vector3 away=(r.worldCenterOfMass-transform.position).normalized;
   r.velocity=GraspMotionPolicy.SafeVelocity(inherited+away*.12f+Vector3.up*.03f,1.5f);
   r.angularVelocity=new Vector3((i%3-1)*1.5f,(i%2==0?1:-1),.5f);r.WakeUp();
  }
 }
 public void ResetObject(){
  if(Body==null)return;
  Liquid?.Clear();
  if(fragments!=null)fragments.SetActive(false);
  if(IsHeld)Release(Vector3.zero);
  IsHeld=false;Damage=DamageState.Intact;DamageValue=dwell=Compression=0;ContactCount=DeformedVertices=0;
  Body.isKinematic=false;Body.useGravity=true;Body.velocity=Vector3.zero;Body.angularVelocity=Vector3.zero;
  RestoreFreeBodySettings();
  transform.SetPositionAndRotation(origin,rotation);Body.position=origin;Body.rotation=rotation;
  visual.gameObject.SetActive(true);visual.localScale=restScale;MaxDisplacementMeters=0;fractureVisuals?.SetDamage(0,false);
  for(int i=0;i<objectColliders.Length;i++)objectColliders[i].enabled=colliderEnabled[i];
  for(int i=0;i<visualRenderers.Length;i++)visualRenderers[i].enabled=rendererEnabled[i];
  if(deformation!=null){deformation.Reset();mesh.SetVertices(deformation.Vertices);mesh.RecalculateNormals();mesh.RecalculateBounds();}
  Body.WakeUp();Physics.SyncTransforms();collisionShield.Step(false);
 }
 void OnDisable(){if(Body!=null&&IsHeld)Release(Vector3.zero);collisionShield.Clear();}
 void OnDrawGizmos(){if(!showContactGizmos||lastRadius<=0)return;Gizmos.color=Color.yellow;Gizmos.DrawWireSphere(lastPoint,lastRadius);}
 void OnDestroy(){collisionShield.Clear();fractureVisuals?.Dispose();if(mesh!=null)Destroy(mesh);if(fractureVisuals==null&&fragments!=null)Destroy(fragments);if(physicsMaterial!=null)Destroy(physicsMaterial);}
}
}
