using System;
using UnityEngine;
namespace GripRehab {
[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(-80)]
public sealed class ApartmentPlayer:MonoBehaviour {
 public ApartmentSettings settings;
 public Camera view;
 public bool IsSeated{get;private set;}
 // Automated runs (-apartment-* probes) drive the player themselves: the real mouse and movement keys of whoever is at the
 // PC must not turn or move a focused test window (2026-11-06; normal play is unchanged).
 static readonly bool Automated=Array.Exists(Environment.GetCommandLineArgs(),a=>a.StartsWith("-apartment-",StringComparison.Ordinal));
 static float Axis(string name)=>Automated?0:Input.GetAxisRaw(name);
 public bool Paused{get;private set;}
 public CharacterController Controller{get;private set;}
 public Vector3 Spawn{get;private set;}
 private float _pitch,_vertical;private SeatPoint _seat;private Vector3 _returnPosition;private Quaternion _returnRotation;private RehabAppShell _shell;
 private readonly RehabKeyboardGate _keyboard=new RehabKeyboardGate();private bool _acceptanceInput;
 private bool InputFocused=>Application.isFocused||_acceptanceInput;
 public bool GameplayInputAllowed{get{ObserveKeyboard();return _keyboard.Allowed;}}
 public bool DemoSqueezing{get{ObserveKeyboard();return _keyboard.Squeezing;}}
 private void ObserveKeyboard(){_keyboard.Sample(Time.frameCount,!Paused&&(isActiveAndEnabled||_acceptanceInput)&&InputFocused&&(_shell==null||!_shell.BlocksGameplay)&&!RehabAppShell.TextEntryHasFocus(),Input.GetKey(KeyCode.Space),Input.GetMouseButton(0));}
 public void ResetGameplayInput()=>_keyboard.Block(Time.frameCount);
 private void OnApplicationFocus(bool focused){if(!focused)ResetGameplayInput();}
 private void Awake(){Controller=GetComponent<CharacterController>();Spawn=transform.position;
  _acceptanceInput=Array.Exists(Environment.GetCommandLineArgs(),a=>a.StartsWith("-apartment-"));
#if UNITY_EDITOR
  _acceptanceInput|=UnityEditor.SessionState.GetBool("ApartmentQA.Active",false);
#endif
 }
 private void Start(){_shell=FindAnyObjectByType<RehabAppShell>();SetPaused(true);}
 private void OnDisable(){_keyboard.Block(Time.frameCount);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
 public void SetPaused(bool paused){if(Paused!=paused)_keyboard.Block(Time.frameCount);Paused=paused;Cursor.lockState=paused?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=paused;}
 private void Update(){
  if(_shell!=null&&_shell.BlocksGameplay){if(!Paused)SetPaused(true);return;}
  if(RehabKeyboardGate.TogglePause(Input.GetKeyDown(KeyCode.Escape),Input.GetKeyDown(KeyCode.Tab),_shell!=null&&_shell.BlocksGameplay,RehabAppShell.TextEntryHasFocus(),InputFocused))SetPaused(!Paused);
  if(!GameplayInputAllowed)return;
  transform.Rotate(0,Axis("Mouse X")*settings.mouseSensitivity,0);
  _pitch=Mathf.Clamp(_pitch-Axis("Mouse Y")*settings.mouseSensitivity,-75,75);
  view.transform.localRotation=Quaternion.Euler(_pitch,0,0);
  if(!IsSeated) {
   var motion=Vector3.ClampMagnitude(new Vector3(Axis("Horizontal"),0,Axis("Vertical")),1);
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
 // Changing stations is a teleport, so a blocked stand-up area at the old
 // chair must not trap the player in the wrong room or keep seated eye height.
 public void TeleportToStanding(Vector3 position,Quaternion rotation){
  Controller.enabled=false;transform.SetPositionAndRotation(position,rotation);
  view.transform.localPosition=new Vector3(0,1.62f,0);
  _seat=null;_vertical=0;IsSeated=false;Controller.enabled=true;
 }
}
}
