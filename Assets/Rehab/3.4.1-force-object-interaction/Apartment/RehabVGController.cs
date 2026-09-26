using System.Collections.Generic;
using UnityEngine;
namespace VirtualGrasp.Controllers {
// Keyboard/MediaPipe input is separate from the visible SDK-controlled skeleton.
[LIBVIRTUALGRASP_UNITY_SCRIPT]
public sealed class RehabVGController : VG_EC_Generic {
 public static int Computes,Matches;
 readonly Dictionary<string,Transform> input=new Dictionary<string,Transform>();
 public RehabVGController(int avatar,VG_HandSide side,Transform origin):this(avatar,side,origin,UnityEngine.Object.FindAnyObjectByType<GripRehab.RehabVirtualGrasp>().inputSkeleton){}
 public RehabVGController(int avatar,VG_HandSide side,Transform origin,Transform inputRoot):base(avatar,side,origin){foreach(var t in inputRoot.GetComponentsInChildren<Transform>(true))input[t.name]=t;m_initialized=true;}
 public override bool Compute(){Computes++;for(int i=0;i<m_mapping.GetNumBones();i++){if(m_mapping.GetTransform(i,out var output)&&input.TryGetValue(output.name,out var source)){Matches++;SetPose(i,Matrix4x4.TRS(source.position,source.rotation,Vector3.one));}}return true;}
}
}
