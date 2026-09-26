using System.Collections.Generic;
using UnityEngine;
using VirtualGrasp;
using VirtualGrasp.Controllers;
namespace GripRehab {
[LIBVIRTUALGRASP_UNITY_SCRIPT]
[DefaultExecutionOrder(100)]
public sealed class RehabVirtualGrasp : VG_MainScript {
 public Transform inputSkeleton;public SkinnedMeshRenderer outputMesh;public Mesh registrationMesh,rightOnlyMesh,leftOnlyMesh;

 public bool Ready{get;private set;}public int AvatarId{get;private set;}
 public override void Awake(){outputMesh.sharedMesh=registrationMesh;var setup=m_avatars[0].m_primarySensorSetup;setup.m_profile=Instantiate(setup.m_profile);setup.m_profile.m_controllerClasses=typeof(RehabVGController).AssemblyQualifiedName;base.Awake();if(this!=null)VG_Controller.Initialize();}
 public override void Update(){base.Update();}
 public override void FixedUpdate(){if(Ready)base.FixedUpdate();}
 void Start(){
  if(VG_Controller.GetAvatarID(outputMesh,out var id)!=VG_ReturnCode.SUCCESS){Debug.LogError("[REHAB_VG] Avatar registration failed");return;}AvatarId=id;
  Ready=true;ShowLeft(false);var app=FindAnyObjectByType<ApartmentExperience>();if(app!=null)app.SelectHand(app.LeftSelected);Debug.Log("[REHAB_VG] Native SDK active; two tracked hand controllers registered");
 }
 public void BindContacts(FingerContactRig rig,bool left){if(!Ready||rig==null)return;rig.contactBones=new Transform[15];for(int i=0;i<15;i++){int finger=(i/3+1)%5;VG_Controller.GetFingerBone(AvatarId,left?VG_HandSide.LEFT:VG_HandSide.RIGHT,finger,i%3,out rig.contactBones[i]);}}
 public void ShowLeft(bool left){if(outputMesh!=null)outputMesh.sharedMesh=left?leftOnlyMesh:rightOnlyMesh;}
}
}
