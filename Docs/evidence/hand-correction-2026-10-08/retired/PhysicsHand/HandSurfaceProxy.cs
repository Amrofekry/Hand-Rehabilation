using UnityEngine;
namespace GripRehab {
// Hand-only contact sites on the live visible surface. The old body collider
// still handles furniture/gravity. Patches share that object's Rigidbody and
// transfer real collision impulse; there is no second object body or mass.
[DefaultExecutionOrder(-130)]
[RequireComponent(typeof(SqueezableObject))]
public sealed class HandSurfaceProxy:MonoBehaviour {
 public PhysicsHandRig hand;SqueezableObject item;Renderer visual;SphereCollider[] patches;PhysicsHandBone[] bound;
 public SqueezableObject Item=>item;
 readonly Vector3[] surfacePoints=new Vector3[16],surfaceNormals=new Vector3[16],localSurface=new Vector3[16];readonly bool[] near=new bool[16];bool registered;
 public bool Boundary(int index,Collider shape,out Vector3 normal,out float gap){normal=surfaceNormals[index];gap=0;if(!near[index]||!isActiveAndEnabled||item.IsBroken)return false;gap=Vector3.Dot(shape.ClosestPoint(surfacePoints[index])-surfacePoints[index],normal);return gap<.025f;}
 void Awake(){item=GetComponent<SqueezableObject>();visual=item.visual!=null?item.visual.GetComponent<Renderer>():GetComponent<Renderer>();
  // Per-body solver iteration overrides are runtime properties in Unity;
  // setting them only during authoring does not persist them into a player.
  if(hand!=null&&hand.settings!=null){var body=GetComponent<Rigidbody>();body.solverIterations=hand.settings.solverIterations;body.solverVelocityIterations=hand.settings.solverVelocityIterations;body.maxDepenetrationVelocity=hand.settings.maxDepenetrationVelocity;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;}
 }
 void FixedUpdate(){
  if(hand==null||!hand.Ready||item.Body==null)return;
  if(!registered){hand.RegisterSurface(this);registered=true;}
  if(patches==null){PhysicsMaterial material=null;foreach(var collider in GetComponentsInChildren<Collider>())if(collider.enabled&&!collider.isTrigger&&collider.attachedRigidbody==item.Body){material=collider.sharedMaterial;break;}
   patches=new SphereCollider[16];for(int i=0;i<16;i++){var go=new GameObject("Hand surface "+i,typeof(SphereCollider));go.layer=hand.settings.surfaceLayer;go.transform.SetParent(transform,false);patches[i]=go.GetComponent<SphereCollider>();patches[i].enabled=false;patches[i].contactOffset=hand.settings.contactOffset;patches[i].sharedMaterial=material;}}
  if(bound!=hand.Bones){bound=hand.Bones;foreach(var patch in patches)foreach(var bone in bound)Physics.IgnoreCollision(patch,bone.shape,true);for(int i=0;i<16;i++)Physics.IgnoreCollision(patches[i],bound[i].shape,false);}
  for(int i=0;i<16;i++){
   var patch=patches[i];var bone=bound[i];bool active=!item.IsBroken&&bone.shape.enabled&&bone.gameObject.activeInHierarchy;
   bool prior=near[i];near[i]=false;if(!active){patch.enabled=false;continue;}
   Vector3 query=bone.shape.bounds.center;
   if(prior&&bone.Contact.item==item){var retained=item.visual.TransformPoint(localSurface[i]);var tangent=Vector3.ProjectOnPlane(query-retained,surfaceNormals[i]);if(tangent.magnitude<hand.settings.surfacePatchRadius*.7f)query=retained+surfaceNormals[i]*hand.settings.contactOffset*2;}
   if(visual!=null&&visual.bounds.SqrDistance(query)>.13f*.13f){patch.enabled=false;continue;}
   if(!item.TrySurface(query,out var point,out var normal)||Vector3.Distance(query,point)>.13f){patch.enabled=false;continue;}
   // Refine against the capsule's outer surface, including its endcaps. A
   // wide flat box tangent can block an adjacent finger on a curved object
   // before it actually touches that object, preventing an opposing grasp.
   var closest=bone.shape.ClosestPoint(point);if(item.TrySurface(closest,out var refined,out var refinedNormal)){point=refined;normal=refinedNormal;}
   near[i]=true;surfacePoints[i]=point;surfaceNormals[i]=normal;localSurface[i]=item.visual.InverseTransformPoint(point);
   var scale=transform.lossyScale;float radius=hand.settings.surfaceThickness*.5f;
   patch.transform.SetPositionAndRotation(point-normal*radius,Quaternion.identity);
   patch.radius=radius/Mathf.Max(Mathf.Abs(scale.x),Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z)));patch.enabled=true;
  }
 }
 void OnDisable(){if(patches!=null)foreach(var patch in patches)if(patch!=null)patch.enabled=false;}
}
}
