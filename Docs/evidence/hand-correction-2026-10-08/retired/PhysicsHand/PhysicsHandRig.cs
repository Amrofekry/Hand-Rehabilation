using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
namespace GripRehab {
[DefaultExecutionOrder(150)]
public sealed class PhysicsHandRig:MonoBehaviour {
 public ApartmentExperience app;public RehabVirtualGrasp target;public FingerContactRig contacts;public PhysicsHandSettings settings;
 public bool Ready{get;private set;}public bool Left=>app.LeftSelected;
 public Rigidbody Palm=>active?.palm.body;
 public PhysicsHandBone[] Bones=>active?.bones;
 public FingerContactProbe[] AllProbes=>active?.allProbes;
 public Transform[] FingerBones=>active?.fingerTransforms;
 public SkinnedMeshRenderer RenderedHand=>visible;
 public bool InputValid{
  get{
   if(settings==null||!settings.Valid||app==null||!app.ForceValid||app.player.Paused)return false;
   if(active!=null)foreach(var pose in active.targets)if(pose==null||!HandRigRules.Finite(pose.position)||!HandRigRules.TryRotation(pose.rotation,out _))return false;
   return true;
  }
 }
 public float MaximumPenetration{get;private set;}
 public float[] FingerForceNewtons=new float[5];
 GameObject runtime;SkinnedMeshRenderer visible;Transform skeletonRoot;readonly Dictionary<Transform,Transform> clones=new Dictionary<Transform,Transform>();
 Side right,left,active;bool builtLeft,inputActive;SqueezableObject candidate;float stableTime;
 Vector3 previousOrigin;int readyAfterMatches;
 readonly List<HandSurfaceProxy> surfaces=new List<HandSurfaceProxy>();readonly bool[] blocked=new bool[16];
 public void RegisterSurface(HandSurfaceProxy surface){if(!surfaces.Contains(surface))surfaces.Add(surface);}
 sealed class Side {
  public GameObject root;public PhysicsHandBone palm;public PhysicsHandBone[] bones=new PhysicsHandBone[16];
  public Transform[] targets=new Transform[16],fingerTransforms=new Transform[15];
  public FingerContactProbe[] allProbes=new FingerContactProbe[16],fingerProbes=new FingerContactProbe[15];
  public ConfigurableJoint[] joints=new ConfigurableJoint[15];public Quaternion[] initial=new Quaternion[15];
 }
 void Awake(){if(contacts==null)contacts=app.contactRig;contacts.physicsHand=this;foreach(var probe in contacts.probes)probe.gameObject.SetActive(false);}
 IEnumerator Start(){var step=new WaitForFixedUpdate();while(true){yield return step;if(Ready&&inputActive)foreach(var bone in active.bones){bone.body.linearVelocity=Vector3.ClampMagnitude(bone.body.linearVelocity,settings.maxVelocity);bone.body.angularVelocity=Vector3.ClampMagnitude(bone.body.angularVelocity,settings.maxAngularVelocity);}}}
 void LateUpdate(){
  if(Ready&&Vector3.Distance(previousOrigin,app.player.transform.position)>settings.worldOriginResetDistance){
   // A room/seat teleport changes the tracking origin discontinuously. Retire
   // the old bodies and initialize a new rig after fresh SDK target frames;
   // no active Rigidbody is moved through a surface by a transform assignment.
   app.Release();runtime.SetActive(false);Destroy(runtime);runtime=null;visible=null;clones.Clear();right=left=active=null;Ready=false;readyAfterMatches=VirtualGrasp.Controllers.RehabVGController.Matches+64;
  }
  previousOrigin=app.player.transform.position;
  if(!Ready){if(target==null||!target.Ready||settings==null||!settings.Valid||!InputValid||VirtualGrasp.Controllers.RehabVGController.Matches<Mathf.Max(64,readyAfterMatches))return;Build();}
  if(Left!=builtLeft)SelectHand(Left);
  var renderer=target.outputMesh;visible.sharedMesh=renderer.sharedMesh;
  visible.transform.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation);visible.transform.localScale=renderer.transform.lossyScale;
  // SDK retains its own target hierarchy. Only this separate draw skeleton is
  // written from physics; no vendor output bone or physics body is teleported.
  foreach(var pair in clones){pair.Value.localPosition=pair.Key.localPosition;pair.Value.localRotation=pair.Key.localRotation;pair.Value.localScale=pair.Key.localScale;}
  // The common skeleton root may itself be under an imported scale/rotation
  // parent. Cloning only its local TRS loses that parent's world transform.
  var root=clones[skeletonRoot];root.SetPositionAndRotation(skeletonRoot.position,skeletonRoot.rotation);root.localScale=skeletonRoot.lossyScale;
  foreach(var bone in active.bones){var visual=clones[active.targets[bone.finger==5?0:bone.finger*3+bone.segment+1]];
   visual.SetPositionAndRotation(bone.body.position,bone.body.rotation);}
  var drawBounds=active.palm.shape.bounds;foreach(var bone in active.bones)drawBounds.Encapsulate(bone.shape.bounds);drawBounds.Expand(.04f);visible.bounds=drawBounds;
  target.outputMesh.forceRenderingOff=true;visible.enabled=inputActive;
 }
 void Build(){
  runtime=new GameObject("Physics hand runtime");var renderer=target.outputMesh;
  Transform common=renderer.bones[0];foreach(var bone in renderer.bones)while(common!=null&&!bone.IsChildOf(common)&&bone!=common)common=common.parent;
  if(common==null)throw new InvalidOperationException("VirtualGrasp bones must share a skeleton root");
  skeletonRoot=common;
  Clone(common,runtime.transform);var go=new GameObject("Physics VirtualGrasp visible hand",typeof(SkinnedMeshRenderer));go.layer=settings.handLayer;go.transform.SetParent(runtime.transform,false);visible=go.GetComponent<SkinnedMeshRenderer>();
  visible.sharedMesh=renderer.sharedMesh;visible.sharedMaterials=renderer.sharedMaterials;visible.bones=Array.ConvertAll(renderer.bones,b=>clones[b]);visible.rootBone=clones[common];visible.updateWhenOffscreen=true;visible.localBounds=renderer.localBounds;
  right=BuildSide(false);left=BuildSide(true);Ready=true;SelectHand(Left);
 }
 void Clone(Transform source,Transform parent){var go=new GameObject(source.name);go.transform.SetParent(parent,false);go.transform.localPosition=source.localPosition;go.transform.localRotation=source.localRotation;go.transform.localScale=source.localScale;clones.Add(source,go.transform);foreach(Transform child in source)Clone(child,go.transform);}
 Side BuildSide(bool isLeft){
  var side=new Side{root=new GameObject(isLeft?"Left physics bodies":"Right physics bodies")};side.root.transform.SetParent(runtime.transform,false);
  side.targets[0]=target.TargetWrist(isLeft);var fingers=target.TargetFingerBones(isLeft);for(int i=0;i<15;i++)side.targets[i+1]=fingers[i];
  var renderer=target.outputMesh;var prior=renderer.sharedMesh;renderer.sharedMesh=isLeft?target.leftOnlyMesh:target.rightOnlyMesh;
  // Compensate the imported renderer scale before transforming into world
  // space. The Gleechi renderer carries a scale of 100; the default overload
  // already includes that scale and would inflate colliders a second time.
  var baked=new Mesh();renderer.BakeMesh(baked,true);var vertices=baked.vertices;var weights=renderer.sharedMesh.boneWeights;var used=new bool[vertices.Length];foreach(int v in baked.triangles)used[v]=true;
  for(int i=0;i<vertices.Length;i++)vertices[i]=renderer.transform.TransformPoint(vertices[i]);
  for(int n=0;n<16;n++){
   var source=side.targets[n];var go=new GameObject(source.name);go.layer=settings.handLayer;go.transform.SetParent(side.root.transform,false);go.transform.SetPositionAndRotation(source.position,source.rotation);
   var body=go.AddComponent<Rigidbody>();body.useGravity=false;body.mass=n==0?settings.palmMass:settings.segmentMass;body.interpolation=RigidbodyInterpolation.Interpolate;
   body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.maxAngularVelocity=settings.maxAngularVelocity;body.maxDepenetrationVelocity=settings.maxDepenetrationVelocity;body.solverIterations=settings.solverIterations;body.solverVelocityIterations=settings.solverVelocityIterations;
   var bone=go.AddComponent<PhysicsHandBone>();bone.finger=n==0?5:(n-1)/3;bone.segment=n==0?0:(n-1)%3;bone.body=body;bone.rig=this;side.bones[n]=bone;
   var probe=go.AddComponent<FingerContactProbe>();probe.rig=contacts;probe.finger=bone.finger;probe.segment=bone.segment;probe.physicalBone=bone;side.allProbes[n]=probe;
   int index=Array.IndexOf(renderer.bones,source);bool found=false;var bounds=new Bounds();
   for(int v=0;v<vertices.Length;v++)if(used[v]){var w=weights[v];float influence=(w.boneIndex0==index?w.weight0:0)+(w.boneIndex1==index?w.weight1:0)+(w.boneIndex2==index?w.weight2:0)+(w.boneIndex3==index?w.weight3:0);
    if(influence<.4f)continue;var point=go.transform.InverseTransformPoint(vertices[v]);if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);}
   if(n==0){side.palm=bone;var box=go.AddComponent<BoxCollider>();box.center=found?bounds.center:Vector3.zero;box.size=found?Vector3.Max(bounds.size*settings.colliderScale,Vector3.one*.006f):new Vector3(.07f,.03f,.07f);bone.shape=box;}
   else{
    Vector3 end=source.childCount>0?source.GetChild(0).position:source.position+source.right*.025f;var direction=go.transform.InverseTransformVector(end-source.position);
    var geometry=new GameObject("Segment surface",typeof(CapsuleCollider));geometry.layer=settings.handLayer;geometry.transform.SetParent(go.transform,false);geometry.transform.localRotation=Quaternion.FromToRotation(Vector3.up,direction);
    var capsule=geometry.GetComponent<CapsuleCollider>();capsule.direction=1;float radius=.007f,min=0,max=direction.magnitude;
    if(found){radius=0;min=float.PositiveInfinity;max=float.NegativeInfinity;
     for(int v=0;v<vertices.Length;v++)if(used[v]){var w=weights[v];if((w.boneIndex0==index?w.weight0:0)+(w.boneIndex1==index?w.weight1:0)+(w.boneIndex2==index?w.weight2:0)+(w.boneIndex3==index?w.weight3:0)<.4f)continue;
      var p=geometry.transform.InverseTransformPoint(vertices[v]);radius=Mathf.Max(radius,new Vector2(p.x,p.z).magnitude);min=Mathf.Min(min,p.y);max=Mathf.Max(max,p.y);}}
    float multiplier=settings.fingerRadiusScale.Length==5?settings.fingerRadiusScale[bone.finger]:1;
    capsule.radius=Mathf.Clamp(radius*settings.colliderScale*multiplier,settings.minimumFingerRadius,settings.maximumFingerRadius);
    capsule.center=Vector3.up*((min+max)*.5f);capsule.height=Mathf.Max(capsule.radius*2,max-min);
    bone.shape=capsule;side.fingerTransforms[n-1]=go.transform;side.fingerProbes[n-1]=probe;
    var parent=n%3==1?side.palm.body:side.bones[n-1].body;var joint=go.AddComponent<ConfigurableJoint>();joint.connectedBody=parent;joint.autoConfigureConnectedAnchor=false;joint.anchor=Vector3.zero;joint.connectedAnchor=parent.transform.InverseTransformPoint(go.transform.position);
    joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Locked;joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Limited;
    joint.lowAngularXLimit=new SoftJointLimit{limit=-settings.flexionLimit};joint.highAngularXLimit=new SoftJointLimit{limit=settings.flexionLimit};joint.angularYLimit=joint.angularZLimit=new SoftJointLimit{limit=settings.swingLimit};joint.rotationDriveMode=RotationDriveMode.Slerp;
    joint.enableCollision=false;joint.enablePreprocessing=false;side.joints[n-1]=joint;side.initial[n-1]=Quaternion.Inverse(parent.rotation)*body.rotation;
   }
   bone.shape.contactOffset=settings.contactOffset;
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"-apartment-physics-hand")>=0)Debug.Log("[HAND_FIT] "+source.name+" body="+body.position+" scale="+renderer.transform.lossyScale+" baked="+baked.bounds+" found="+found+" local="+bounds+" capsule="+(bone.shape is CapsuleCollider cap?cap.height+"/"+cap.radius:((BoxCollider)bone.shape).size.ToString()));
  }
  renderer.sharedMesh=prior;Destroy(baked);side.root.SetActive(false);return side;
 }
 public void SelectHand(bool isLeft){if(!Ready)return;candidate=null;stableTime=0;active=isLeft?left:right;builtLeft=isLeft;right.root.SetActive(!isLeft);left.root.SetActive(isLeft);
  contacts.probes=active.fingerProbes;contacts.contactBones=active.fingerTransforms;foreach(var bone in active.bones)bone.Clear();
 }
 void FixedUpdate(){
  if(!Ready)return;inputActive=InputValid;
  MaximumPenetration=0;Array.Clear(FingerForceNewtons,0,5);
  foreach(var bone in active.bones){bone.shape.enabled=inputActive;bone.body.isKinematic=!inputActive;
   if(!inputActive){bone.Clear();continue;}
   bone.body.linearVelocity=Vector3.ClampMagnitude(bone.body.linearVelocity,settings.maxVelocity);bone.body.angularVelocity=Vector3.ClampMagnitude(bone.body.angularVelocity,settings.maxAngularVelocity);
   float depth=bone.ActualPenetration;MaximumPenetration=Mathf.Max(MaximumPenetration,depth);if(bone.finger<5)FingerForceNewtons[bone.finger]+=bone.EstimatedForceNewtons;
   if(depth>settings.maxPenetration&&bone.Contact.item!=null){var other=bone.Contact.item.Body;var normal=bone.Contact.normal;
    float approach=-Vector3.Dot(bone.body.linearVelocity-other.linearVelocity,normal);
    float correction=Mathf.Clamp((depth-settings.maxPenetration)*settings.penetrationSpring+approach*settings.penetrationDamping,0,settings.maximumCorrectionForce);
    var reaction=normal*correction;bone.body.AddForce(reaction,ForceMode.Force);if(!other.isKinematic)other.AddForce(-reaction,ForceMode.Force);
   }
  }
  if(!inputActive)return;
  for(int i=surfaces.Count-1;i>=0;i--)if(surfaces[i]==null)surfaces.RemoveAt(i);
  Array.Clear(blocked,0,16);
  // Predictive normal-speed limits keep a newly arriving segment from crossing
  // a surface in one tick. These change velocities, never body transforms.
  for(int i=0;i<16;i++)foreach(var surface in surfaces)if(surface.Boundary(i,active.bones[i].shape,out var normal,out var gap)){
   var body=active.bones[i].body;float allowance=Mathf.Max(0,gap-settings.contactOffset)/Time.fixedDeltaTime;
   float closing=Vector3.Dot(body.linearVelocity-surface.Item.Body.linearVelocity,normal);if(closing<-allowance)body.linearVelocity+=normal*(-allowance-closing);
   if(gap<=settings.contactOffset*3){blocked[i]=true;body.angularVelocity=Vector3.ClampMagnitude(body.angularVelocity,settings.nearSurfaceAngularVelocity);}
  }
  var wrist=active.palm.body;var source=active.targets[0];var force=Vector3.ClampMagnitude((source.position-wrist.position)*settings.positionSpring-wrist.linearVelocity*settings.positionDamping,settings.maxWristForce);
  // A held body moves with the palm; it must not become an immovable wall
  // that cancels wrist transport. Finger closure and overlap guards remain.
  for(int i=0;i<16;i++)if(blocked[i])foreach(var surface in surfaces)if(!surface.Item.IsHeld&&surface.Boundary(i,active.bones[i].shape,out var normal,out _)){float into=Vector3.Dot(force,normal);if(into<0)force-=normal*into;}
  foreach(var bone in active.bones)if(bone.Contact.item!=null&&!bone.Contact.item.IsHeld&&bone.Contact.penetration>settings.maxPenetration){float into=Vector3.Dot(force,bone.Contact.normal);if(into<0)force-=bone.Contact.normal*into;}
  wrist.AddForce(force,ForceMode.Force);
  var rotation=source.rotation*Quaternion.Inverse(wrist.rotation);rotation.ToAngleAxis(out float angle,out var axis);if(angle>180)angle-=360;
  if((!blocked[0]||app.Held!=null)&&HandRigRules.Finite(axis))wrist.AddTorque(Vector3.ClampMagnitude(axis*(angle*Mathf.Deg2Rad*settings.rotationSpring)-wrist.angularVelocity*settings.rotationDamping,settings.maxWristTorque),ForceMode.Force);
  for(int i=0;i<15;i++){var joint=active.joints[i];var parent=i%3==0?active.targets[0]:active.targets[i];var desired=Quaternion.Inverse(parent.rotation)*active.targets[i+1].rotation;
   if(HandRigRules.TryRotation(Quaternion.Inverse(desired)*active.initial[i],out var jointRotation))joint.targetRotation=jointRotation;
   else{joint.slerpDrive=default;continue;}
   var bone=active.bones[i+1];var center=bone.Contact.item!=null?bone.Contact.point:bone.shape.bounds.center;var localCenter=Quaternion.Inverse(bone.body.rotation)*(center-bone.body.position);
   var wantedCenter=active.targets[i+1].position+active.targets[i+1].rotation*localCenter;bool pressingFurther=false;
   if(blocked[i+1])foreach(var surface in surfaces)if(surface.Boundary(i+1,bone.shape,out var normal,out _)&&Vector3.Dot(wantedCenter-center,normal)<-.0001f){pressingFurther=true;break;}
   if(bone.Contact.item!=null&&bone.Contact.penetration>settings.maxPenetration&&Vector3.Dot(wantedCenter-center,bone.Contact.normal)<-.0001f)pressingFurther=true;
   // Contact guards stop further inward closing, not an opening/retraction
   // drive. Disabling both directions would leave fingers stuck at release.
   joint.slerpDrive=new JointDrive{positionSpring=settings.fingerSpring,positionDamper=settings.fingerDamping,maximumForce=pressingFurther?0:settings.maxFingerTorque};}
 }
 public bool StableGrip(SqueezableObject item){
  if(!Ready||item==null||!inputActive){candidate=null;stableTime=0;return false;}bool opposed=false;
  int fingers=0;for(int i=1;i<16;i++)if(active.bones[i].TryContact(item,out _,out _))fingers|=1<<active.bones[i].finger;
  bool multipleFingers=fingers!=0&&(fingers&(fingers-1))!=0;
  if(multipleFingers)for(int a=settings.allowPalmSupport?0:1;a<16&&!opposed;a++)if(active.bones[a].TryContact(item,out _,out var n))for(int b=a+1;b<16;b++)if(active.bones[b].finger!=active.bones[a].finger&&active.bones[b].TryContact(item,out _,out var m)&&Vector3.Dot(n,m)<=settings.opposingNormalDot){opposed=true;break;}
  if(candidate!=item){candidate=item;stableTime=0;}stableTime=opposed?stableTime+Time.deltaTime:0;return stableTime>=settings.gripDwellSeconds;
 }
 void OnDestroy(){if(target!=null&&target.outputMesh!=null)target.outputMesh.forceRenderingOff=false;if(runtime!=null)Destroy(runtime);}
}
}
