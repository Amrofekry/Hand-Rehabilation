using System;
using UnityEngine;
namespace GripRehab {
// Each probe is a real capsule trigger with its own kinematic body. No whole-hand overlap is used.
public sealed class FingerContactRig : MonoBehaviour {
 [Header("Hand Tracking")][Tooltip("Existing animated 15-bone hand rig.")]
 public FingerPoseDriver pose;
 [Header("Contact")][Min(.003f)] public float radiusMeters=.009f;
 [Tooltip("Layer 9 = grippable objects only.")] public LayerMask objectLayers=1<<9;
 [SerializeField] public FingerContactProbe[] probes;
 [Header("Debug")] public bool showGizmos;
 public int ProbeCount => probes==null?0:probes.Length;
 public void ClearForces(){if(probes!=null)foreach(var probe in probes)if(probe!=null)probe.SetForce(0);}
 public void Configure() {
  if(pose==null)pose=GetComponent<FingerPoseDriver>();
  if(pose==null||pose.joints==null)return;
  if(probes!=null&&probes.Length==15&&Array.TrueForAll(probes,p=>p!=null))return;
  probes=new FingerContactProbe[15];
  for(int i=0;i<15;i++) {
   var go=new GameObject($"Contact {i/3}:{i%3}");go.layer=8;go.transform.SetParent(transform,false);
   var body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
   var capsule=go.AddComponent<CapsuleCollider>();capsule.isTrigger=true;capsule.direction=1;capsule.radius=radiusMeters;
   var probe=go.AddComponent<FingerContactProbe>();probe.finger=i/3;probe.segment=i%3;probe.rig=this;probes[i]=probe;
  }
 }
 void Awake(){Configure();}
 void FixedUpdate(){
  if(probes==null||pose==null||pose.joints==null)return;
  for(int i=0;i<Mathf.Min(15,probes.Length);i++) {
   var probe=probes[i];var bone=i<pose.joints.Length?pose.joints[i]:null;
   if(probe==null||bone==null){if(probe!=null)probe.gameObject.SetActive(false);continue;}
   Transform end=bone.childCount>0?bone.GetChild(0):null;
   Vector3 a=bone.position,b=end!=null?end.position:a+bone.forward*.025f;
   float length=Vector3.Distance(a,b);
   if(length<.005f){probe.Clear();probe.gameObject.SetActive(false);continue;}
   if(!probe.gameObject.activeSelf)probe.gameObject.SetActive(true);
   var capsule=probe.GetComponent<CapsuleCollider>();capsule.radius=radiusMeters/capsule.transform.lossyScale.x;
   capsule.height=Mathf.Max(capsule.radius*2,length/capsule.transform.lossyScale.y+capsule.radius*2);
   var body=probe.GetComponent<Rigidbody>();var midpoint=(a+b)*.5f;
   body.MovePosition(midpoint);body.MoveRotation(Quaternion.FromToRotation(Vector3.up,b-a));
  }
 }
 void OnDrawGizmos(){if(!showGizmos||probes==null)return;Gizmos.color=Color.cyan;foreach(var p in probes)if(p!=null&&p.Contact!=null)Gizmos.DrawWireSphere(p.Point,radiusMeters);}
}
}
