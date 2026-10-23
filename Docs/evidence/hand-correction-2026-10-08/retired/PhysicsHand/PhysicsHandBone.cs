using System;
using UnityEngine;
namespace GripRehab {
public struct PhysicsContact {
 public SqueezableObject item;public Vector3 point,normal;public float penetration,forceNewtons;public int finger,segment;
}
// Solid-collider reactions. Force is a simulated impulse/dt, not a sensor reading.
public sealed class PhysicsHandBone:MonoBehaviour {
 public int finger,segment;public Rigidbody body;public Collider shape;public PhysicsHandRig rig;
 public event Action<PhysicsContact> ContactEnter,ContactStay,ContactExit;
 public PhysicsContact Contact{get;private set;}
 public float EstimatedForceNewtons=>Time.fixedTime-seen<=Time.fixedDeltaTime*1.5f?Contact.forceNewtons:0;
 readonly ContactPoint[] points=new ContactPoint[16];float seen;
 Collider otherShape;
 public float ActualPenetration {
  get {
   if(shape==null||otherShape==null||!shape.enabled||!otherShape.enabled)return 0;
   var position=body.position;var rotation=body.rotation;
   if(shape.transform!=body.transform){position+=rotation*shape.transform.localPosition;rotation*=shape.transform.localRotation;}
   Vector3 otherPosition=otherShape.transform.position;Quaternion otherRotation=otherShape.transform.rotation;var otherBody=otherShape.attachedRigidbody;
   if(otherBody!=null&&otherShape.transform.parent==otherBody.transform){otherPosition=otherBody.position+otherBody.rotation*Vector3.Scale(otherShape.transform.localPosition,otherBody.transform.lossyScale);otherRotation=otherBody.rotation*otherShape.transform.localRotation;}
   return Physics.ComputePenetration(shape,position,rotation,otherShape,otherPosition,otherRotation,out _,out float depth)?Mathf.Max(0,depth):0;
  }
 }
 void OnCollisionEnter(Collision hit){Read(hit,true);}
 void OnCollisionStay(Collision hit){Read(hit,false);}
 void Read(Collision hit,bool entering){
  var item=hit.collider.GetComponentInParent<SqueezableObject>();if(item==null||item.IsBroken)return;
  int count=hit.GetContacts(points);if(count==0)return;
  Vector3 point=Vector3.zero,normal=Vector3.zero;float depth=0;
  for(int i=0;i<count;i++){point+=points[i].point;normal+=points[i].normal;depth=Mathf.Max(depth,-points[i].separation);}
  point/=count;normal.Normalize();
  if(item.TrySurface(point,out var surface,out var outward)){point=surface;normal=outward;}
  var c=new PhysicsContact{item=item,point=point,normal=normal,penetration=depth,forceNewtons=hit.impulse.magnitude/Mathf.Max(.001f,Time.fixedDeltaTime),finger=finger,segment=segment};
  Contact=c;seen=Time.fixedTime;otherShape=hit.collider;
  if(entering)ContactEnter?.Invoke(c);else ContactStay?.Invoke(c);
 }
 void OnCollisionExit(Collision hit){if(hit.collider.GetComponentInParent<SqueezableObject>()==Contact.item)Clear();}
 public bool TryContact(SqueezableObject item,out Vector3 point,out Vector3 normal){
  point=Contact.point;normal=Contact.normal;
  if(Contact.item!=item||item==null||item.IsBroken||!item.gameObject.activeInHierarchy||!shape.enabled)return false;
  if(Time.fixedTime-seen<=Time.fixedDeltaTime*2.5f)return true;
  // Sleeping bodies need not emit Stay. Revalidate geometry instead of keeping
  // an indefinitely stale contact; do not retain its old impulse as force.
  if(item.TrySurface(shape.bounds.center,out var surface,out var outward)&&Vector3.Distance(shape.ClosestPoint(surface),surface)<=rig.settings.contactOffset*2){point=surface;normal=outward;return true;}
  return false;
 }
 public void Clear(){if(Contact.item!=null)ContactExit?.Invoke(Contact);Contact=default;seen=-100;otherShape=null;}
 void OnDisable(){Clear();}
 void OnDrawGizmos(){if(rig==null||rig.settings==null||!rig.settings.showGizmos||shape==null)return;
  Gizmos.color=Color.cyan;
  if(shape is CapsuleCollider capsule){
   var transform=capsule.transform;var direction=capsule.direction==0?Vector3.right:capsule.direction==1?Vector3.up:Vector3.forward;
   var a=transform.TransformPoint(capsule.center+direction*Mathf.Max(0,capsule.height*.5f-capsule.radius));var b=transform.TransformPoint(capsule.center-direction*Mathf.Max(0,capsule.height*.5f-capsule.radius));
   float radius=capsule.radius*Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.z));Gizmos.DrawWireSphere(a,radius);Gizmos.DrawWireSphere(b,radius);
   var tangent=Vector3.Cross((a-b).normalized,Vector3.right);if(tangent.sqrMagnitude<.1f)tangent=Vector3.Cross((a-b).normalized,Vector3.up);tangent.Normalize();var other=Vector3.Cross((a-b).normalized,tangent);
   foreach(var offset in new[]{tangent*radius,-tangent*radius,other*radius,-other*radius})Gizmos.DrawLine(a+offset,b+offset);
  }else if(shape is BoxCollider box){var prior=Gizmos.matrix;Gizmos.matrix=box.transform.localToWorldMatrix;Gizmos.DrawWireCube(box.center,box.size);Gizmos.matrix=prior;}
  if(Contact.item!=null){Gizmos.color=Color.yellow;Gizmos.DrawSphere(Contact.point,.002f);Gizmos.DrawRay(Contact.point,Contact.normal*.04f);Gizmos.color=Color.red;Gizmos.DrawRay(Contact.point,-Contact.normal*ActualPenetration);}
 }
}
}
