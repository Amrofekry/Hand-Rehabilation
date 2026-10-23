using System.Collections.Generic;
using UnityEngine;
using GripRehab;
namespace VirtualGrasp.Controllers {
// Project-owned adapter; vendor code and its output skeleton stay separate.
[LIBVIRTUALGRASP_UNITY_SCRIPT]
public sealed class RehabVGController : VG_EC_Generic {
 public static int Computes,Matches;
 static readonly Dictionary<int,RehabVGController[]> controllers=new Dictionary<int,RehabVGController[]>();
 public static void ResetSession(){Computes=Matches=0;controllers.Clear();}
 public static bool HasBindings(int avatar)=>controllers.TryGetValue(avatar,out var hands)&&
  hands[0]!=null&&hands[1]!=null&&hands[0].BindingsReady&&hands[1].BindingsReady;
 readonly Transform[] sources=new Transform[HandRigRules.SdkBoneCount];
 readonly Transform[] outputs=new Transform[HandRigRules.SdkBoneCount];
 readonly Matrix4x4[] poses=new Matrix4x4[HandRigRules.SdkBoneCount];
 public bool BindingsReady{get;private set;}
 public int WrittenThisFrame{get;private set;}
 public string BindingError{get;private set;}="";
 static Transform InputRoot(){var main=Object.FindAnyObjectByType<RehabVirtualGrasp>();return main!=null?main.inputSkeleton:null;}
 public RehabVGController(int avatar,VG_HandSide side,Transform origin):this(avatar,side,origin,InputRoot()){}
 public RehabVGController(int avatar,VG_HandSide side,Transform origin,Transform inputRoot):base(avatar,side,origin){
  if(side==VG_HandSide.LEFT||side==VG_HandSide.RIGHT){
   if(!controllers.TryGetValue(avatar,out var hands)){hands=new RehabVGController[2];controllers.Add(avatar,hands);}
   hands[side==VG_HandSide.LEFT?0:1]=this;
  }
  if(inputRoot==null){Fail("Input skeleton missing");return;}
  if(m_mapping.GetNumBones()!=HandRigRules.SdkBoneCount){Fail("Expected wrist and 15 finger bones");return;}
  bool left=side==VG_HandSide.LEFT;
  if(side!=VG_HandSide.LEFT&&side!=VG_HandSide.RIGHT){Fail("Unsupported hand side");return;}
  var input=new Dictionary<string,Transform>();var ambiguous=new HashSet<string>();
  foreach(var t in inputRoot.GetComponentsInChildren<Transform>(true)){
   if(input.ContainsKey(t.name))ambiguous.Add(t.name);else input.Add(t.name,t);
  }
  for(int i=0;i<sources.Length;i++){
   string expected=HandRigRules.BoneName(i,left);
   if(ambiguous.Contains(expected)||!input.TryGetValue(expected,out sources[i])||
      !m_mapping.GetTransform(i,out outputs[i])||outputs[i]==null||outputs[i].name!=expected||sources[i]==outputs[i]){
    Fail("Missing, ambiguous or wrong-side bone: "+expected);return;
   }
  }
  BindingsReady=true;m_initialized=true;
 }
 void Fail(string error){BindingError=error;BindingsReady=false;m_initialized=false;m_enabled=false;}
 public override bool Compute(){
  Computes++;WrittenThisFrame=0;
  if(!BindingsReady)return false;
  // Validate the entire frame before SetPose; never send a partially bad hand.
  for(int i=0;i<sources.Length;i++){
   var source=sources[i];
   if(source==null||outputs[i]==null||!HandRigRules.Finite(source.position)||
      !HandRigRules.TryRotation(source.rotation,out var rotation))return false;
   poses[i]=Matrix4x4.TRS(source.position,rotation,Vector3.one);
  }
  for(int i=0;i<poses.Length;i++){SetPose(i,poses[i]);WrittenThisFrame++;Matches++;}
  return true;
 }
}
}
