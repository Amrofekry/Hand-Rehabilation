// Test doubles only. No vendor library, native Unity Transform or renderer executes.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
 public sealed class DefaultExecutionOrderAttribute:Attribute {public DefaultExecutionOrderAttribute(int i){}}
 public static class Object {
  public static GripRehab.RehabVirtualGrasp Main;
  public static GripRehab.ApartmentExperience App;
  public static T FindAnyObjectByType<T>() where T:class => typeof(T)==typeof(GripRehab.RehabVirtualGrasp)?Main as T:App as T;
 }
 public class Mesh {}
 public class SkinnedMeshRenderer {public Mesh sharedMesh;public Transform[] bones=new Transform[0];}
 public struct Matrix4x4 {
  public Vector3 position,scale;public Quaternion rotation;
  public static Matrix4x4 TRS(Vector3 p,Quaternion r,Vector3 s)=>new Matrix4x4{position=p,rotation=r,scale=s};
 }
 public static class Time {public static double realtimeSinceStartupAsDouble;}
 public static class Debug {
  public static readonly List<string> Errors=new List<string>();
  public static void LogError(string s){Errors.Add(s);}public static void Log(string s){}
 }
}
namespace VirtualGrasp {
 public sealed class LIBVIRTUALGRASP_UNITY_SCRIPT:Attribute{}
 public enum VG_HandSide {LEFT,RIGHT}
 public enum VG_ReturnCode {SUCCESS,FAILED}
 public class VG_MainScript:MonoBehaviour {
  public int BaseAwakes,BaseUpdates,BaseFixedUpdates;
  public virtual void Awake(){BaseAwakes++;}public virtual void Update(){BaseUpdates++;}public virtual void FixedUpdate(){BaseFixedUpdates++;}
 }
 public static class VG_Controller {
  public static int InitializeCalls,AvatarCalls,ContactCalls;
  public static VG_ReturnCode AvatarResult=VG_ReturnCode.SUCCESS;
  public static readonly Transform[][] Contacts={new Transform[15],new Transform[15]};
  public static int BadContact=-1;public static bool DuplicateContact;
  public static void Initialize(){InitializeCalls++;}
  public static VG_ReturnCode GetAvatarID(SkinnedMeshRenderer mesh,out int id){AvatarCalls++;id=123;return AvatarResult;}
  public static VG_ReturnCode GetFingerBone(int id,VG_HandSide side,int finger,int joint,out Transform bone){
   ContactCalls++;int n=((finger+4)%5)*3+joint;bone=Contacts[(int)side][n];
   if(n==BadContact)return VG_ReturnCode.FAILED;
   if(DuplicateContact&&n==14)bone=Contacts[(int)side][13];
   return VG_ReturnCode.SUCCESS;
  }
 }
}
namespace VirtualGrasp.Controllers {
 public sealed class FakeBoneMapping {
  public static bool WrongSide,MissingOutput;
  public Transform[] bones=new Transform[16];
  public FakeBoneMapping(VirtualGrasp.VG_HandSide side){for(int i=0;i<16;i++)bones[i]=new Transform{name=GripRehab.HandRigRules.BoneName(i,WrongSide?side!=VirtualGrasp.VG_HandSide.LEFT:side==VirtualGrasp.VG_HandSide.LEFT)};}
  public int GetNumBones()=>16;
  public bool GetTransform(int i,out Transform bone){bone=MissingOutput&&i==15?null:bones[i];return bone!=null;}
 }
 public class VG_EC_Generic {
  protected FakeBoneMapping m_mapping;protected bool m_initialized,m_enabled=true;
  public readonly List<Matrix4x4> Written=new List<Matrix4x4>();
  public VG_EC_Generic(int avatar,VirtualGrasp.VG_HandSide side,Transform origin){m_mapping=new FakeBoneMapping(side);}
  protected void SetPose(int index,Matrix4x4 pose){Written.Add(pose);}
  public virtual bool Compute()=>false;
 }
}
namespace GripRehab {
 public sealed class FingerContactRig {public Transform[] contactBones;}
 public sealed class ApartmentExperience {
  public bool LeftSelected;public RehabVirtualGrasp sdk;public FingerContactRig rig=new FingerContactRig();public int Selections;
  public void SelectHand(bool left){LeftSelected=left;Selections++;sdk.ShowLeft(left);sdk.BindContacts(rig,left);}
 }
}
