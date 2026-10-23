using UnityEngine;
namespace GripRehab {
public sealed class FingerContactProbe : MonoBehaviour {
 public int finger,segment;public FingerContactRig rig;
 public SqueezableObject Contact{get;private set;}
 public Vector3 Point{get;private set;} public Vector3 Normal{get;private set;} public Vector3 Velocity{get;private set;}
 public float Duration{get;private set;}
 public float ResolvedForceNewtons{get;private set;}
 public float ContactAreaSquareMeters=>rig==null?0:Mathf.PI*rig.radiusMeters*rig.radiusMeters;
 internal void SetForce(float newtons){ResolvedForceNewtons=Mathf.Max(0,newtons);}
 Vector3 previous;float lastSeen;
 void OnEnable(){previous=transform.position;}
 void FixedUpdate(){
  Velocity=(transform.position-previous)/Mathf.Max(Time.fixedDeltaTime,.001f);previous=transform.position;
  if(Contact!=null&&Time.fixedTime-lastSeen>Time.fixedDeltaTime*2.5f)Clear();
  else if(Contact!=null)Duration+=Time.fixedDeltaTime;
 }
 void OnTriggerEnter(Collider other){Resolve(other);}
 void OnTriggerStay(Collider other){Resolve(other);}
 void OnTriggerExit(Collider other){if(other.GetComponentInParent<SqueezableObject>()==Contact)Clear();}
 void Resolve(Collider other){
  if(rig==null||(rig.objectLayers.value&(1<<other.gameObject.layer))==0)return;
  var item=other.GetComponentInParent<SqueezableObject>();if(item==null||item.IsBroken||!item.gameObject.activeInHierarchy)return;
  var capsule=GetComponent<CapsuleCollider>();
  if(!Physics.ComputePenetration(capsule,transform.position,transform.rotation,other,other.transform.position,other.transform.rotation,out var direction,out var depth)||depth<=0)return;
  if(Contact!=item){Contact=item;Duration=0;}
  Normal=direction;Point=other.ClosestPoint(transform.position);lastSeen=Time.fixedTime;
 }
 public void Clear(){Contact=null;Duration=0;ResolvedForceNewtons=0;Point=Normal=Velocity=Vector3.zero;}
 void OnDisable(){Clear();}
}
}
