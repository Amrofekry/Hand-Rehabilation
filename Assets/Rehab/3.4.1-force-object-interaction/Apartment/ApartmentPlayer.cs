using System;
using UnityEngine;
namespace GripRehab {
[RequireComponent(typeof(CharacterController))]
public sealed class ApartmentPlayer:MonoBehaviour {
 public ApartmentSettings settings;
 public Camera view;
 public bool IsSeated{get;private set;}
 public bool Paused{get;private set;}
 public CharacterController Controller{get;private set;}
 public Vector3 Spawn{get;private set;}
 private float _pitch,_vertical;private SeatPoint _seat;private Vector3 _returnPosition;private Quaternion _returnRotation;
 private void Awake(){Controller=GetComponent<CharacterController>();Spawn=transform.position;}
 private void Start(){SetPaused(true);}
 private void OnDisable(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
 public void SetPaused(bool paused){Paused=paused;Cursor.lockState=paused?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=paused;}
 private void Update(){
  if(Input.GetKeyDown(KeyCode.Escape))SetPaused(!Paused);
  if(Paused)return;
  transform.Rotate(0,Input.GetAxisRaw("Mouse X")*settings.mouseSensitivity,0);
  _pitch=Mathf.Clamp(_pitch-Input.GetAxisRaw("Mouse Y")*settings.mouseSensitivity,-75,75);
  view.transform.localRotation=Quaternion.Euler(_pitch,0,0);
  if(!IsSeated) {
   var motion=Vector3.ClampMagnitude(new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical")),1);
   if(Controller.isGrounded&&_vertical<0)_vertical=-2;
   _vertical=Mathf.Max(-15,_vertical-9.81f*Time.deltaTime);
   Controller.Move((transform.TransformDirection(motion)*settings.walkSpeed+Vector3.up*_vertical)*Time.deltaTime);
  }
  if(transform.position.y<-3)Teleport(Spawn,Quaternion.identity);
 }
 public bool Sit(SeatPoint seat){
  if(IsSeated||seat==null||seat.seatedFeet==null||Vector3.Distance(transform.position,seat.seatedFeet.position)>2.2f)return false;
  _seat=seat;_returnPosition=seat.standFeet!=null?seat.standFeet.position:transform.position;_returnRotation=transform.rotation;
  Controller.enabled=false;transform.SetPositionAndRotation(seat.seatedFeet.position,seat.seatedFeet.rotation);
  view.transform.localPosition=new Vector3(0,seat.seatedEyeHeight,0);_pitch=0;IsSeated=true;
  return true;
 }
 public bool Stand(){
  if(!IsSeated)return true;
  if(Physics.CheckCapsule(_returnPosition+Vector3.up*.34f,_returnPosition+Vector3.up*1.36f,.25f,~(1<<8),QueryTriggerInteraction.Ignore))return false;
  transform.SetPositionAndRotation(_returnPosition,_returnRotation);view.transform.localPosition=new Vector3(0,1.62f,0);
  Controller.enabled=true;_vertical=0;IsSeated=false;
  return true;
 }
 public void LookAt(Vector3 target){
  var direction=target-view.transform.position;var flat=new Vector3(direction.x,0,direction.z);
  if(flat.sqrMagnitude>0.001f)transform.rotation=Quaternion.LookRotation(flat);
  _pitch=Mathf.Clamp(-Mathf.Atan2(direction.y,flat.magnitude)*Mathf.Rad2Deg,-75,75);
  view.transform.localRotation=Quaternion.Euler(_pitch,0,0);
 }
 public void Teleport(Vector3 position,Quaternion rotation){Controller.enabled=false;transform.SetPositionAndRotation(position,rotation);Controller.enabled=true;_vertical=0;}
}
}
