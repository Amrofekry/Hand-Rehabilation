using System;
using UnityEngine;
namespace GripRehab {
[RequireComponent(typeof(Rigidbody))]
public sealed class SqueezableObject:MonoBehaviour {
 public string displayName="Therapy ball";
 public Transform visual;
 [Min(1)] public float fullCompressionNewtons=35;
 [Range(.05f,.5f)] public float maximumCompression=.28f;
 public bool breakable;
 [Min(1)] public float fractureNewtons=55;
 public GameObject fragmentsPrefab;
 public bool IsHeld {get;private set;}
 public bool IsBroken {get;private set;}
 public float Compression {get;private set;}
 public Rigidbody Body {get;private set;}
 public float radius=.1f;
 private Vector3 _origin,_restScale; private Quaternion _rotation; private Mesh _mesh; private Vector3[] _vertices,_deformed;
 private float _stress; private GameObject _fragments;
 private void Awake() {
  Body=GetComponent<Rigidbody>();_origin=transform.position;_rotation=transform.rotation;
  if(visual==null)visual=transform;_restScale=visual.localScale;
  var filter=visual.GetComponent<MeshFilter>();
  if(filter!=null && filter.sharedMesh!=null && filter.sharedMesh.isReadable) {
   _mesh=Instantiate(filter.sharedMesh);_mesh.name=displayName+" runtime deformation";filter.sharedMesh=_mesh;
   _vertices=_mesh.vertices;_deformed=new Vector3[_vertices.Length];
  }
 }
 public bool Grab() {
  if(IsBroken||IsHeld)return false;
  IsHeld=true;Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;Body.isKinematic=true;return true;
 }
 public void MoveHeld(Vector3 position,Quaternion rotation) { if(IsHeld){Body.MovePosition(position);Body.MoveRotation(rotation);} }
 public void Release(Vector3 velocity) {
  if(!IsHeld)return;IsHeld=false;Body.isKinematic=false;Body.useGravity=true;Body.linearVelocity=Vector3.ClampMagnitude(velocity,2);Body.WakeUp();
 }
 public void ApplyForce(float newtons,float dt) {
  if(IsBroken||!gameObject.activeInHierarchy)return;
  if(!float.IsFinite(newtons)||newtons<0)newtons=0;
  float target=IsHeld?Mathf.Clamp01(newtons/fullCompressionNewtons)*maximumCompression:0;
  Compression=Mathf.Lerp(Compression,target,1-Mathf.Exp(-12*Mathf.Max(0,dt)));
  if(_mesh!=null) {
   for(int i=0;i<_vertices.Length;i++) {
    var v=_vertices[i];float contact=Mathf.SmoothStep(.35f,1,Mathf.Clamp01(Mathf.Abs(v.x)*2));
    v.x*=1-Compression*contact;
    float bulge=1+Compression*.24f*(1-Mathf.Clamp01(v.x*v.x*4));
    v.y*=bulge;v.z*=bulge;_deformed[i]=v;
   }
   _mesh.vertices=_deformed;_mesh.RecalculateNormals();_mesh.RecalculateBounds();
  } else visual.localScale=Vector3.Scale(_restScale,new Vector3(1-Compression,1+Compression*.2f,1+Compression*.2f));
  _stress=IsHeld&&breakable&&newtons>=fractureNewtons?_stress+Mathf.Min(dt,.1f):0;
  if(_stress>.35f)Break();
 }
 public void Break() {
  if(IsBroken||!breakable||fragmentsPrefab==null)return;
  IsBroken=true;IsHeld=false;Body.isKinematic=true;visual.gameObject.SetActive(false);
  foreach(var c in GetComponents<Collider>())c.enabled=false;
  _fragments=Instantiate(fragmentsPrefab,transform.position,transform.rotation);
  _fragments.SetActive(true);
  foreach(var r in _fragments.GetComponentsInChildren<Rigidbody>())r.AddExplosionForce(.5f,transform.position,.2f,.05f,ForceMode.Impulse);
 }
 public void ResetObject() {
  if(Body==null)return;
  if(_fragments!=null)Destroy(_fragments); IsHeld=false;IsBroken=false;_stress=0;Compression=0;
  Body.isKinematic=false;Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;
  transform.SetPositionAndRotation(_origin,_rotation);Body.position=_origin;Body.rotation=_rotation;
  visual.gameObject.SetActive(true);visual.localScale=_restScale;
  foreach(var c in GetComponents<Collider>())c.enabled=true;
  if(_mesh!=null){_mesh.vertices=_vertices;_mesh.RecalculateNormals();_mesh.RecalculateBounds();}
  Body.WakeUp();
 }
 private void OnDestroy(){if(_mesh!=null)Destroy(_mesh);if(_fragments!=null)Destroy(_fragments);}
}
}
