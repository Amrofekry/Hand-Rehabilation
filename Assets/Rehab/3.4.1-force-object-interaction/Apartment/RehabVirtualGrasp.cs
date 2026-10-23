using UnityEngine;
using VirtualGrasp;
using VirtualGrasp.Controllers;
namespace GripRehab {
[LIBVIRTUALGRASP_UNITY_SCRIPT]
[DefaultExecutionOrder(100)]
public sealed class RehabVirtualGrasp : VG_MainScript {
 public Transform inputSkeleton;public SkinnedMeshRenderer outputMesh;public Mesh registrationMesh,rightOnlyMesh,leftOnlyMesh;

 public bool Ready{get;private set;}public int AvatarId{get;private set;}
 public string LastError{get;private set;}="";
 readonly Transform[] rightContacts=new Transform[15],leftContacts=new Transform[15],unavailableContacts=new Transform[15];
 bool initialized,selectedLeft;double nextRegistration,startedAt;string reportedError="";
 public override void Awake(){
  Ready=false;
  if(inputSkeleton==null||outputMesh==null||registrationMesh==null||rightOnlyMesh==null||leftOnlyMesh==null){
   Error("Required input skeleton or hand mesh is missing");enabled=false;return;
  }
  outputMesh.sharedMesh=registrationMesh;
  // The SDK discovers external-controller profiles with Resources.LoadAll.
  // Changing an instantiated profile here does not make that profile discoverable
  // when the first sensor frame is processed. Keep the matching project-owned
  // VG_CP_Rehab.Input asset in Resources/VG_ControllerProfiles instead.
  // Its class name includes Assembly-CSharp so the SDK's Type.GetType lookup
  // can resolve this project-owned controller outside the vendor assembly.
  base.Awake();
  if(this!=null){RehabVGController.ResetSession();startedAt=Time.realtimeSinceStartupAsDouble;VG_Controller.Initialize();initialized=true;}
 }
 public override void Update(){if(initialized)base.Update();}
 public override void FixedUpdate(){
  if(!initialized)return;
  base.FixedUpdate(); // Registration can finish after the first native tick.
  if(!Ready&&Time.realtimeSinceStartupAsDouble>=nextRegistration){nextRegistration=Time.realtimeSinceStartupAsDouble+.5;TryReady();}
 }
 void Start(){if(initialized){ShowLeft(selectedLeft);TryReady();}}
 void Error(string message){if(reportedError!=message)Debug.LogError("[REHAB_VG] "+message);reportedError=message;LastError=message;}
 void Pending(string message){
  if(Time.realtimeSinceStartupAsDouble-startedAt>=2)Error(message);else LastError=message;
 }
 void TryReady(){
  if(VG_Controller.GetAvatarID(outputMesh,out var id)!=VG_ReturnCode.SUCCESS){Pending("Avatar registration pending or failed");return;}
  if(!RehabVGController.HasBindings(id)){Pending("Both project input controller bindings are required; verify VG_CP_Rehab.Input");return;}
  if(!ReadContacts(id,false,rightContacts)||!ReadContacts(id,true,leftContacts)){
   Pending("SDK must expose all 15 distinct finger bones for each hand");return;
  }
  AvatarId=id;Ready=true;LastError=reportedError="";
  var app=FindAnyObjectByType<ApartmentExperience>();if(app!=null)app.SelectHand(app.LeftSelected);
  Debug.Log("[REHAB_VG] Native registration and both 15-joint contact bindings available");
 }
 static bool ReadContacts(int id,bool left,Transform[] bones){
  for(int i=0;i<bones.Length;i++){
   if(VG_Controller.GetFingerBone(id,left?VG_HandSide.LEFT:VG_HandSide.RIGHT,
      HandRigRules.SdkFinger(i/3),i%3,out bones[i])!=VG_ReturnCode.SUCCESS||bones[i]==null)return false;
   for(int j=0;j<i;j++)if(bones[i]==bones[j])return false;
  }
  return true;
 }
 public void BindContacts(FingerContactRig rig,bool left){
  if(rig!=null)rig.contactBones=Ready?(left?leftContacts:rightContacts):unavailableContacts;
 }
 public void ShowLeft(bool left){selectedLeft=left;if(outputMesh!=null)outputMesh.sharedMesh=left?leftOnlyMesh:rightOnlyMesh;}
}
}
