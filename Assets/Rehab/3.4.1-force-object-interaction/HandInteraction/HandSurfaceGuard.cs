using UnityEngine;
using VirtualGrasp.Controllers;
namespace GripRehab {
// Hard pose constraints on the original SDK output. No simulated hand, springs,
// cloned renderer or forces: free-space input remains the original direct pose.
[DefaultExecutionOrder(1000)]
public sealed class HandSurfaceGuard:MonoBehaviour {
 public ApartmentExperience app;
 public HandSkinData skinData;
 [Header("Surface limits")][Range(.0001f,.005f)] public float surfacePadding=.0005f;
 [Tooltip("Sub-millimetre correction hysteresis prevents repeated opening/closing from numerical surface noise.")][Range(0,.001f)] public float correctionSlop=.0003f;
 [Range(4,12)] public int jointSearchSteps=8;
 [Tooltip("Test the closing arc, so a finger cannot pass through an object and finish on its other side.")][Range(1,8)] public float maximumSweepAngle=4;
 [Tooltip("Thin hollow walls need smaller articulated sweep steps; otherwise three joints can cross a wall between samples.")][Range(.1f,2)] public float hollowSweepAngle=.5f;
 [Range(1,6)] public int passes=3;
 [Tooltip("Allow a blocked finger to open slightly beyond its neutral pose before closing against a surface.")][Range(0,25)] public float maximumContactOpeningDegrees=15;
 [Range(.01f,.25f)] public float maximumWristCorrection=.15f;
 [Tooltip("Move a cup's grasp from knuckles toward the finger pads, keeping the body clear of the palm.")][Range(0,1)] public float cupPadBias=1;
 [Tooltip("Cached visible skin samples per bone; palm uses three times this count. Re-enter Play after changing.")][Range(8,24)] public int skinSamplesPerSegment=24;
 public HandSkinSurface Skin{get;private set;}
 [Header("Held grip fit")]
 [Tooltip("Maximum object adjustment inside a held grasp after a soft contact patch retreats; never moves the player.")][Range(0,.04f)] public float maximumGripFit=.025f;
 [Tooltip("Bounded settling speed of the held object; no teleport to a fixed palm anchor.")][Range(.005f,.1f)] public float gripFitSpeed=.04f;
 [Header("Debug")] public bool showGizmos;
 public Vector3 ResolvedWristOffset=>localWristCorrection;
 public float WristCorrectionMeters{get;private set;} public int LimitedJoints{get;private set;}public float ResidualPenetration{get;private set;}
 readonly Quaternion[][] rest={new Quaternion[15],new Quaternion[15]};
 readonly bool[] initialized=new bool[2];readonly Quaternion[] target=new Quaternion[15];
 readonly Transform[] inputWrists=new Transform[2],outputWrists=new Transform[2];
 readonly Vector3[] padReach=new Vector3[2];readonly Vector3[] openPads=new Vector3[2],inward=new Vector3[2],across=new Vector3[2];
 readonly Vector3[] rawPads=new Vector3[15],resolvedPoints=new Vector3[15];readonly Quaternion[] resolvedRotations=new Quaternion[15];Vector3 resolvedWrist;Quaternion resolvedWristRotation;bool resolved;bool resolvedLeft;SqueezableObject resolvedHeld;
 readonly Vector3[] cachedInputPoints=new Vector3[15];readonly Quaternion[] cachedInputRotations=new Quaternion[15];readonly bool[] blockedFingers=new bool[5];
 Vector3 cachedInputWrist,cachedObjectPosition;Vector3 localWristCorrection;readonly Quaternion[] solvedLocal=new Quaternion[15];Quaternion cachedInputWristRotation,cachedObjectRotation;Transform wrist;bool lastLeft;
 public bool TryRawPad(int index,out Vector3 point){point=Vector3.zero;if(wrist==null||index<0||index>=15||lastLeft!=app.LeftSelected)return false;point=rawPads[index];return true;}
 void Awake(){
  if(app==null)return;
  Skin=new HandSkinSurface(app.virtualGrasp,skinSamplesPerSegment,skinData);app.contactRig.skin=Skin;
  foreach(var b in app.virtualGrasp.inputSkeleton.GetComponentsInChildren<Transform>(true)){if(b.name=="Wrist_R")inputWrists[0]=b;else if(b.name=="Wrist_L")inputWrists[1]=b;}
  foreach(var b in app.virtualGrasp.outputMesh.bones){if(b==null)continue;if(b.name=="Wrist_R")outputWrists[0]=b;else if(b.name=="Wrist_L")outputWrists[1]=b;}
  for(int side=0;side<2;side++){
   var hand=side==1?app.leftHand:app.rightHand;if(hand==null||hand.joints==null||hand.joints.Length!=15)continue;
   var bones=hand.joints;for(int i=0;i<15;i++)rest[side][i]=bones[i].localRotation;
   var pads=(bones[0].position+bones[3].position)*.5f;
   var width=pads-(bones[6].position+bones[9].position)*.5f;
   var direction=bones[1].position-bones[0].position;
   var curl=Vector3.Cross(bones[0].TransformDirection(hand.curlAxis)*hand.curlSign,direction).normalized;
   padReach[side]=hand.transform.InverseTransformVector((bones[1].position+bones[4].position)*.5f-pads);openPads[side]=hand.transform.InverseTransformPoint(pads);inward[side]=hand.transform.InverseTransformDirection(curl);across[side]=hand.transform.InverseTransformDirection(width.normalized);initialized[side]=true;
  }
 }
 public bool TryGripFrame(SqueezableObject item,out Vector3 centre){
  centre=Vector3.zero;int side=app.LeftSelected?1:0;if(!initialized[side])return false;
  var hand=app.ActiveHand;
  // The object occupies the palm-facing aperture. Finger bending cannot move
  // its anchor out of the grasp or chase the constrained visual skeleton.
  float radius=item.GraspSurfaceRadius;
  float depth=item.SurfaceExtent(hand.transform.TransformDirection(inward[side]))+app.contactRig.radiusMeters+surfacePadding;
  centre=hand.transform.TransformPoint(openPads[side]+(item.profile!=null&&item.profile.hollowCup?padReach[side]*cupPadBias:Vector3.zero))+hand.transform.TransformDirection(inward[side])*depth+hand.transform.TransformDirection(across[side])*radius*.2f;
  return true;
 }
 void LateUpdate(){if(app!=null)app.UpdateHeldSurface();}
 public void FinishSurfaceStep(){
  if(!resolved||app==null||resolvedHeld!=app.Held||wrist==null)return;
  RefineCurrentSurface(app.Held,app.contactRig);
  SaveResolvedPose(app.contactRig.contactBones);
  app.contactRig.RefreshGeometry();
 }
 public void ResolveContacts(){
  if(app==null||app.virtualGrasp==null||!app.virtualGrasp.Ready||app.contactRig==null)return;
  var rig=app.contactRig;var bones=rig.contactBones;if(bones==null||bones.Length!=15||bones[0]==null)return;
  // Always read fresh unconstrained input; never feed a limited pose back.

  lastLeft=app.LeftSelected;int side=lastLeft?1:0;
  wrist=outputWrists[side];
  if(wrist==null)return;
  // Restore the unconstrained project input before applying hard contact
  // limits. Native SDK output may be refreshed between physics ticks, so a
  // Match counter cannot safely gate the visual surface constraint.
  Transform inputWrist=inputWrists[side];
  if(inputWrist==null)return;if(TryCachedContact(inputWrist,bones,rig))return;wrist.SetPositionAndRotation(inputWrist.position,inputWrist.rotation);
  for(int i=0;i<15;i++)bones[i].SetPositionAndRotation(app.ActiveHand.joints[i].position,app.ActiveHand.joints[i].rotation);
  LimitedJoints=0;ResidualPenetration=0;WristCorrectionMeters=0;System.Array.Clear(blockedFingers,0,5);
  for(int i=0;i<15;i++){target[i]=bones[i].localRotation;rawPads[i]=bones[i].position;if(!initialized[side])rest[side][i]=target[i];}
  Skin.Refresh(lastLeft);
  initialized[side]=true;

  var held=app.Held;
  if(held!=null){
   // Start the whole chain at its safe open pose. Testing one open joint with
   // the remaining joints still curled incorrectly rejects valid solutions.
   for(int i=0;i<15;i++)bones[i].localRotation=rest[side][i];
   if(resolved&&held==resolvedHeld&&resolvedLeft==lastLeft)wrist.position=inputWrist.position+inputWrist.TransformVector(localWristCorrection);
   var start=inputWrist.position;
   ClearWrist(held,inputWrist);
   WristCorrectionMeters=Vector3.Distance(start,wrist.position);
   // Sweep each articulated finger as one chain. A proximal-only sweep can
   // stop against its straight distal segment, then lose contact when that
   // segment bends. Keep the three measured joint ratios along the safe arc.
   SweepFingers(held,rig,bones,side);
  }else{
   // Move only the SDK wrist away from touched objects, never the player/camera.
   var start=wrist.position;
   for(int pass=0;pass<passes;pass++){
    rig.RefreshGeometry(false);Skin.Refresh(lastLeft);float deepest=0;Vector3 direction=Vector3.zero;
    var handBounds=Skin.Bounds;handBounds.Expand(surfacePadding*2);
    foreach(var item in app.objects)if(item!=null&&!item.IsBroken&&item.gameObject.activeInHierarchy&&item.visual.GetComponent<Renderer>().bounds.Intersects(handBounds)){
     float d=Skin.Depth(item,out var n);if(d>deepest){deepest=d;direction=n;}
    }
    if(deepest<=surfacePadding)break;
    wrist.position=start+Vector3.ClampMagnitude(wrist.position-start+direction*(deepest+surfacePadding),maximumWristCorrection);
   }
  }
  if(held!=null)RefineCurrentSurface(held,rig);
  if(held==null)WristCorrectionMeters=Vector3.Distance(inputWrist.position,wrist.position);
  resolvedWrist=wrist.position;resolvedWristRotation=wrist.rotation;resolvedLeft=app.LeftSelected;resolved=true;
  for(int i=0;i<15;i++){resolvedPoints[i]=bones[i].position;resolvedRotations[i]=bones[i].rotation;cachedInputPoints[i]=inputWrist.InverseTransformPoint(app.ActiveHand.joints[i].position);cachedInputRotations[i]=app.ActiveHand.joints[i].localRotation;}
  resolvedHeld=held;cachedInputWrist=inputWrist.position;cachedInputWristRotation=inputWrist.rotation;
  if(held!=null){cachedObjectPosition=inputWrist.InverseTransformPoint(held.transform.position);cachedObjectRotation=Quaternion.Inverse(inputWrist.rotation)*held.transform.rotation;}
  SaveResolvedPose(bones);
  rig.RefreshGeometry();
  Skin.Refresh(lastLeft);if(held!=null)ResidualPenetration=Skin.FullDepth(held,out _);

 }
 void SweepFingers(SqueezableObject held,FingerContactRig rig,Transform[] bones,int side){
   Skin.RefreshPose(lastLeft);float sweep=held.profile!=null&&held.profile.hollowCup?Mathf.Min(maximumSweepAngle,hollowSweepAngle):maximumSweepAngle;
   for(int f=0;f<5;f++){
    float angle=0;for(int j=0;j<3;j++)angle=Mathf.Max(angle,Quaternion.Angle(rest[side][f*3+j],target[f*3+j]));
    if(angle<.01f)continue;
    float open=0;
    if(FingerBlocked(held,f)){
     float limit=-Mathf.Min(.6f,maximumContactOpeningDegrees/angle);int opening=Mathf.Max(1,Mathf.CeilToInt(-limit*angle/sweep));
     for(int step=1;step<=opening;step++){open=limit*step/opening;SetFinger(bones,side,f,open);if(!FingerBlocked(held,f))break;}
    }
    int samples=Mathf.Clamp(Mathf.CeilToInt((1-open)*angle/sweep),1,720);float safe=open,blocked=-1;
    for(int step=1;step<=samples;step++){
     float t=Mathf.Lerp(open,1,step/(float)samples);SetFinger(bones,side,f,t);
     if(FingerBlocked(held,f)){blocked=t;break;}safe=t;
    }
    if(blocked<0)continue;
    for(int n=0;n<jointSearchSteps;n++){float t=(safe+blocked)*.5f;SetFinger(bones,side,f,t);if(FingerBlocked(held,f))blocked=t;else safe=t;}
    SetFinger(bones,side,f,safe);if(!blockedFingers[f])LimitedJoints+=3;blockedFingers[f]=true;
   }
 }
 float CurrentDepth(SqueezableObject held,FingerContactRig rig){return Skin.FullDepth(held,out _);}
 void ClearWrist(SqueezableObject held,Transform input){
  Skin.RefreshPose(lastLeft);var original=wrist.position;Skin.CaptureWristSearch(wrist);
  var correction=original-input.position;
  // Recover the smallest safe clearance along the previous direction as a
  // soft object compresses. Retaining the old open-hand offset can otherwise
  // leave every closed finger in empty space, with zero pressure forever.
  if(!Skin.BlockedAtTranslation(held,-correction,surfacePadding)){wrist.position=input.position;return;}
  if(correction.sqrMagnitude>1e-10f&&!Skin.BlockedAtTranslation(held,Vector3.zero,surfacePadding)){
   float blocked=0,safe=1;for(int n=0;n<jointSearchSteps;n++){float mid=(safe+blocked)*.5f;if(Skin.BlockedAtTranslation(held,correction*(mid-1),surfacePadding))blocked=mid;else safe=mid;}
   wrist.position=input.position+correction*safe;return;
  }
  var away=(original-held.VisibleSurfaceBounds.center).normalized;if(away.sqrMagnitude<.5f)away=-input.forward;
  // Find a safe OPEN hand before closing. Local normal projection can get
  // trapped between opposing contacts; it must not start the closing sweep
  // from an already intersecting palm or neutral thumb.
  for(int axis=0;axis<7;axis++){
   var direction=axis==0?away:axis==1?input.right:axis==2?-input.right:axis==3?input.up:axis==4?-input.up:axis==5?input.forward:-input.forward;
   float previous=0;
   for(float distance=.005f;distance<=maximumWristCorrection+.001f;distance+=.005f){
    var candidate=input.position+Vector3.ClampMagnitude(original-input.position+direction*distance,maximumWristCorrection);
    if(!Skin.BlockedAtTranslation(held,candidate-original,surfacePadding)){
     float safe=distance,blocked=previous;
     for(int search=0;search<jointSearchSteps;search++){float mid=(safe+blocked)*.5f;candidate=input.position+Vector3.ClampMagnitude(original-input.position+direction*mid,maximumWristCorrection);if(!Skin.BlockedAtTranslation(held,candidate-original,surfacePadding))safe=mid;else blocked=mid;}
     wrist.position=input.position+Vector3.ClampMagnitude(original-input.position+direction*safe,maximumWristCorrection);return;
    }previous=distance;
   }
  }wrist.position=original;
 }
 void SaveResolvedPose(Transform[] bones){
  resolvedWrist=wrist.position;resolvedWristRotation=wrist.rotation;
  var input=inputWrists[app.LeftSelected?1:0];
  localWristCorrection=input.InverseTransformVector(wrist.position-input.position);
  for(int i=0;i<15;i++){resolvedPoints[i]=bones[i].position;resolvedRotations[i]=bones[i].rotation;solvedLocal[i]=bones[i].localRotation;}
 }
 void RefineCurrentSurface(SqueezableObject held,FingerContactRig rig){
  if(held==null||held.IsBroken)return;
  rig.RefreshGeometry(false);ResidualPenetration=CurrentDepth(held,rig);if(ResidualPenetration<=surfacePadding+correctionSlop)return;
  int side=app.LeftSelected?1:0;var input=inputWrists[side];if(input==null)return;
  // Rebuild a safe articulated contact pose when the object's shape changes.
  // Never stretch or translate individual bones and never deform the hand.
  var bones=rig.contactBones;for(int i=0;i<15;i++)bones[i].localRotation=rest[side][i];
  ClearWrist(held,input);SweepFingers(held,rig,bones,side);
  WristCorrectionMeters=Vector3.Distance(input.position,wrist.position);ResidualPenetration=CurrentDepth(held,rig);
 }
 bool TryCachedContact(Transform input,Transform[] bones,FingerContactRig rig){
  var held=app.Held;if(!resolved||held==null||held!=resolvedHeld||resolvedLeft!=app.LeftSelected)return false;
  var relativePosition=input.InverseTransformPoint(held.transform.position);
  var relativeRotation=Quaternion.Inverse(input.rotation)*held.transform.rotation;
  if((relativePosition-cachedObjectPosition).sqrMagnitude>4e-8f||Quaternion.Angle(relativeRotation,cachedObjectRotation)>.1f)return false;
  for(int i=0;i<15;i++)if((input.InverseTransformPoint(app.ActiveHand.joints[i].position)-cachedInputPoints[i]).sqrMagnitude>4e-8f||Quaternion.Angle(app.ActiveHand.joints[i].localRotation,cachedInputRotations[i])>.25f)return false;
  wrist.SetPositionAndRotation(input.position+input.TransformVector(localWristCorrection),input.rotation);
  var delta=input.rotation*Quaternion.Inverse(cachedInputWristRotation);for(int i=0;i<15;i++){bones[i].SetPositionAndRotation(input.position+delta*(resolvedPoints[i]-cachedInputWrist),delta*resolvedRotations[i]);rawPads[i]=app.ActiveHand.joints[i].position;}
  rig.RefreshGeometry(false);
  Skin.Refresh(lastLeft);float worst=0;
  for(int f=0;f<5;f++){float nearest=float.PositiveInfinity;
   for(int j=0;j<3;j++){var probe=rig.probes[f*3+j];if(probe==null||!probe.gameObject.activeInHierarchy)continue;float gap=Skin.Gap(held,f*3+j,out _,out _);nearest=Mathf.Min(nearest,gap);worst=Mathf.Max(worst,-gap);}
   if(nearest < -surfacePadding-.0001f || (blockedFingers[f]&&nearest>held.handContactTolerance))return false;
  }
  worst=Mathf.Max(worst,Skin.FullDepth(held,out _));if(worst>surfacePadding+.0001f)return false;
  ResidualPenetration=worst;rig.RefreshGeometry();return true;
 }
 void SetFinger(Transform[] bones,int side,int finger,float t){for(int j=0;j<3;j++){int i=finger*3+j;bones[i].localRotation=Quaternion.SlerpUnclamped(rest[side][i],target[i],t);}}
 bool FingerBlocked(SqueezableObject item,int finger){Skin.RefreshFingerPose(lastLeft,finger);return Skin.Blocked(item,finger,surfacePadding);}
 void OnDrawGizmos(){if(!showGizmos||app==null||app.contactRig==null)return;Skin?.Draw();Gizmos.color=Color.cyan;foreach(var p in app.contactRig.probes)if(p!=null&&p.gameObject.activeInHierarchy){var c=p.GetComponent<CapsuleCollider>();Gizmos.matrix=c.transform.localToWorldMatrix;Gizmos.DrawWireCube(c.center,new Vector3(c.radius*2,c.height,c.radius*2));}Gizmos.matrix=Matrix4x4.identity;}
}
}
