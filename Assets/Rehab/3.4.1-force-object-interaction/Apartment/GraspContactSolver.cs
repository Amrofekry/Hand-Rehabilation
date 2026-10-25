using System;
using UnityEngine;
namespace GripRehab {
public enum HandGrip { Power, Pinch, Cylindrical }

/// <summary>One frame of hand-object contact measurements (metres).</summary>
public struct ContactReport {
 public bool valid;
 /// <summary>Deepest finger-segment overlap beyond the skin tolerance and dent allowance (0 = none).</summary>
 public float maxPenetration;
 /// <summary>Mean gap from the grip's pad segments to the surface (0 = all pads touching).</summary>
 public float padGap;
 /// <summary>How far the object reaches into the palm (0 = clear).</summary>
 public float palmDepth;
 public int touchingPads, limitedJoints;
 public Vector3 towardPads, outOfPalm;
 public HandGrip shape;
 /// <summary>True when the object has a chart grip (GripProp); grip names it.</summary>
 public bool hasGrip;public GripType grip;
}

/// <summary>
/// Stage 8 contact solver. After the hand pose is set each frame, every finger joint (proximal
/// to distal) curls only as far as the held object's surface plus a skin tolerance and the
/// visible dent, so fingers rest on the object instead of passing into it or closing beside it.
/// Mouse, keyboard, camera and device inputs are unchanged: only the final curl is limited.
/// </summary>
public static class GraspContactSolver {
 /// <summary>Off with the -contact-solver-off flag (audit baseline).</summary>
 public static bool Enabled = Array.IndexOf(Environment.GetCommandLineArgs(), "-contact-solver-off") < 0;
 public const float PalmRadius = .02f;
 static readonly int[] PowerPads = { 2, 5, 8, 14 }, PinchPads = { 2, 14 }, CylinderPads = { 2, 5, 8, 11, 14 };

 /// <summary>Grip preset from the object's shape: small items are pinched, tall ones held cylindrically.</summary>
 /// <remarks>Uses the undeformed colliders in the object's own frame, so a visible dent or the hand
 /// turning the object can never switch the grip (and the hold placement) in the middle of a hold.</remarks>
 public static HandGrip ShapeFor(SqueezableObject item){
  if(item==null)return HandGrip.Power;
  var size=LocalSize(item);
  float width=Mathf.Max(size.x,size.z), height=size.y;
  if(Mathf.Max(width,height)<.075f)return HandGrip.Pinch;
  return height>width*1.4f?HandGrip.Cylindrical:HandGrip.Power;
 }

 static Vector3 LocalSize(SqueezableObject item){
  var size=Vector3.zero;var root=item.transform;
  foreach(var c in CollidersOf(item)){
   Vector3 s;var scale=c.transform.lossyScale;
   switch(c){
    case BoxCollider box:s=Vector3.Scale(box.size,scale);break;
    case SphereCollider sphere:s=Vector3.one*sphere.radius*2*Mathf.Max(scale.x,Mathf.Max(scale.y,scale.z));break;
    case CapsuleCollider capsule:{float r=capsule.radius*2*Mathf.Max(scale.x,scale.z);s=new Vector3(r,Mathf.Max(r,capsule.height*scale.y),r);break;}
    case MeshCollider mesh when mesh.sharedMesh!=null:s=Vector3.Scale(mesh.sharedMesh.bounds.size,scale);break;
    default:s=Vector3.one*item.radius*2;break;
   }
   size=Vector3.Max(size,s);
  }
  return size==Vector3.zero?Vector3.one*item.radius*2:size;
 }

 static void Segment(Transform[] joints,int n,out Vector3 a,out Vector3 b){
  var bone=joints[n];a=bone.position;
  var end=bone.childCount>0?bone.GetChild(0):null;
  b=end!=null?end.position:a+bone.forward*.025f;
 }

 // One hidden trigger sphere gives true penetration depth (also for centres inside the object).
 static SphereCollider probe;
 // Per-object cache: alternating between objects (held one, reach candidates) must not re-query every frame.
 static readonly System.Collections.Generic.Dictionary<SqueezableObject,Collider[]> colliderCache=new System.Collections.Generic.Dictionary<SqueezableObject,Collider[]>();
 static SqueezableObject lastItem;static Collider[] lastColliders;
 static Collider[] CollidersOf(SqueezableObject item){
  // Runs ~500 times a frame. The lookup lives in its own method: a lambda capturing 'item' here would
  // allocate its closure on every call (measured 32 B per call, 16 KB a frame), even on the fast path.
  if(ReferenceEquals(item,lastItem)&&lastColliders!=null)return lastColliders;
  if(!colliderCache.TryGetValue(item,out var cached)||cached==null){cached=FindColliders(item);colliderCache[item]=cached;}
  lastItem=item;lastColliders=cached;
  return cached;
 }
 /// <summary>The object's own solid colliders (cached).</summary>
 public static Collider[] SolidColliders(SqueezableObject item)=>CollidersOf(item);
 static Collider[] FindColliders(SqueezableObject item)=>Array.FindAll(item.GetComponentsInChildren<Collider>(true),c=>!c.isTrigger&&c.attachedRigidbody==item.Body);
 static SphereCollider Probe(){
  if(probe==null){var go=new GameObject("Contact solver probe"){hideFlags=HideFlags.HideAndDontSave};go.layer=2;go.transform.position=new Vector3(0,-1000,0);
   probe=go.AddComponent<SphereCollider>();probe.isTrigger=true;}
  return probe;
 }
 /// <summary>Overlap (positive) or gap (negative) of a sphere with the item, using PhysX penetration.</summary>
 public static float SphereDepth(SqueezableObject item,Vector3 centre,float radius){
  // Primitive colliders use exact signed distances (no PhysX query, no allocation; ~900 queries a frame).
  float best=float.NegativeInfinity;bool mesh=false;
  foreach(var c in CollidersOf(item)){
   if(c==null||!c.enabled||!c.gameObject.activeInHierarchy)continue;
   if(TrySignedDistance(c,centre,out float distance))best=Mathf.Max(best,radius-distance);else mesh=true;
  }
  if(!mesh)return best;
  var sphere=Probe();sphere.radius=radius;float deepest=0;bool any=false;
  foreach(var c in CollidersOf(item)){
   if(c==null||!c.enabled||!c.gameObject.activeInHierarchy||c is SphereCollider||c is BoxCollider||c is CapsuleCollider)continue;
   if(Physics.ComputePenetration(sphere,centre,Quaternion.identity,c,c.transform.position,c.transform.rotation,out _,out float depth)&&depth>deepest){deepest=depth;any=true;}
  }
  if(any)return Mathf.Max(best,deepest);
  return Mathf.Max(best,-(Vector3.Distance(centre,item.ClosestSurface(centre))-radius));
 }

 /// <summary>Signed distance from p to a primitive collider's surface (negative inside). False for mesh colliders.</summary>
 public static bool TrySignedDistance(Collider c,Vector3 p,out float distance){
  distance=0;var t=c.transform;var scale=t.lossyScale;
  switch(c){
   case SphereCollider sphere:{
    float r=sphere.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
    distance=Vector3.Distance(p,t.TransformPoint(sphere.center))-r;return true;}
   case BoxCollider box:{
    var local=t.InverseTransformPoint(p)-box.center;
    var q=new Vector3(Mathf.Abs(local.x*scale.x),Mathf.Abs(local.y*scale.y),Mathf.Abs(local.z*scale.z))-Vector3.Scale(box.size*.5f,new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
    distance=Vector3.Max(q,Vector3.zero).magnitude+Mathf.Min(Mathf.Max(q.x,Mathf.Max(q.y,q.z)),0);return true;}
   case CapsuleCollider capsule:{
    var axis=capsule.direction==0?t.right:capsule.direction==1?t.up:t.forward;
    float axisScale=Mathf.Abs(capsule.direction==0?scale.x:capsule.direction==1?scale.y:scale.z);
    float r=capsule.radius*(capsule.direction==0?Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z)):capsule.direction==1?Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z)):Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y)));
    float half=Mathf.Max(0,capsule.height*axisScale*.5f-r);var centre=t.TransformPoint(capsule.center);
    float along=Mathf.Clamp(Vector3.Dot(p-centre,axis),-half,half);
    distance=Vector3.Distance(p,centre+axis*along)-r;return true;}
   default:return false;
  }
 }

 // Struct callbacks: Func delegates here cost ~32 B per query in Unity's Mono (~16 KB a frame), breaking the Stage 7 budget.
 struct Surface:ISphereDepth{public SqueezableObject item;public float Depth(Vector3 centre,float radius)=>SphereDepth(item,centre,radius);}
 struct JointDepth:IAngleDepth{
  public FingerPoseDriver hand;public Surface surface;public int n,finger,joint;public float radius;
  public float Depth(float angle){hand.SetJointCurl(n,angle);return FingerDepth(hand.joints,finger,joint,radius,ref surface);}
 }

 public static ContactReport Solve(FingerPoseDriver hand,SqueezableObject item,float radius,bool apply){
  var report=new ContactReport();
  if(hand==null||item==null||hand.joints==null||hand.joints.Length!=15||!item.gameObject.activeInHierarchy||item.IsBroken)return report;
  var joints=hand.joints;
  for(int i=0;i<15;i++)if(joints[i]==null)return report;
  radius=float.IsFinite(radius)&&radius>0?radius:.009f;
  // Cached delegates over static state: no closures, so the solver allocates nothing per frame (Stage 7 budget).
  var surface=new Surface{item=item};
  // A finger may sit as deep as the visible dent: the collider stays round while the mesh yields.
  float tolerance=ContactGeometry.SkinTolerance+Mathf.Clamp(item.MaxDisplacementMeters,0,.03f);
  var prop=PropOf(item);var definition=prop!=null?prop.Definition:null;
  report.shape=definition!=null?(definition.anchor==GripAnchor.Palm?HandGrip.Power:HandGrip.Pinch):ShapeFor(item);
  report.hasGrip=definition!=null;if(definition!=null)report.grip=definition.type;
  var pads=definition!=null?definition.pads:report.shape==HandGrip.Pinch?PinchPads:report.shape==HandGrip.Cylindrical?CylinderPads:PowerPads;
  if(apply)
   for(int finger=0;finger<5;finger++){
    // Fingers that make this grip wrap until they touch; the others keep their own curl.
    bool wraps=definition!=null?definition.Wraps(finger):Array.IndexOf(pads,finger*3+2)>=0;
    for(int j=0;j<3;j++){
     // Some chart grips may close past their photographed shape (CloseFactor); contact stops the finger at the surface.
     int n=finger*3+j;float full=definition!=null?Mathf.Min(110,definition.curl[n]*definition.CloseFactor):HandRigRules.DemoLimit(finger,j);
     float target=wraps?Mathf.Max(hand.JointCurl(n),full):hand.JointCurl(n);
     var depthAt=new JointDepth{hand=hand,surface=surface,n=n,finger=finger,joint=j,radius=radius};
     float allowed=ContactGeometry.LimitAngle(target,ref depthAt,tolerance,7,MinimumCurl(finger,j));
     hand.SetJointCurl(n,allowed);
     if(allowed<target-.5f)report.limitedJoints++;
    }
   }
  float deepest=float.NegativeInfinity;Vector3 deepestPoint=Vector3.zero;
  for(int n=0;n<15;n++){
   Segment(joints,n,out var a,out var b);float d=ContactGeometry.CapsuleDepth(a,b,radius,ref surface);
   if(d>deepest){deepest=d;deepestPoint=(a+b)*.5f;}
  }
  report.maxPenetration=Mathf.Max(0,deepest-tolerance);
  float gap=0;Vector3 padCentre=Vector3.zero;
  foreach(int n in pads){
   Segment(joints,n,out var a,out var b);
   float depth=ContactGeometry.CapsuleDepth(a,b,radius,ref surface);
   if(depth>=-ContactGeometry.SkinTolerance)report.touchingPads++;else gap+=-depth;
   padCentre+=(a+b)*.5f;
  }
  report.padGap=gap/pads.Length;padCentre/=pads.Length;
  var centre=item.Body!=null?item.Body.position:item.transform.position;
  report.towardPads=padCentre-centre;
  var palm=(hand.transform.position+joints[0].position+joints[3].position+joints[6].position+joints[9].position)/5f;
  report.palmDepth=Mathf.Max(0,SphereDepth(item,palm,PalmRadius));
  // A finger segment the curl limit cannot clear (already inside while open) is pushed out like the palm.
  bool fingerDeeper=report.maxPenetration>report.palmDepth;
  report.outOfPalm=centre-(fingerDeeper?deepestPoint:palm);
  if(fingerDeeper)report.palmDepth=report.maxPenetration;
  report.valid=true;
  return report;
 }

 /// <summary>How far a joint may extend (negative curl) to back out of an object; the thumb swings wider.</summary>
 public static float MinimumCurl(int finger,int joint)=>finger==4?(joint==0?-35:-20):(joint==0?-15:-5);

 /// <summary>
 /// Palm-anchored hold: the object rests on the palm along its grasp direction, at its own
 /// half-extent plus a small skin gap, so it can neither sink into the hand nor float off it.
 /// Pinch-sized objects keep the fingertip placement (returns false).
 /// </summary>
 public static bool TryPalmHold(FingerPoseDriver hand,SqueezableObject item,out Vector3 target,float extraOffset=0){
  target=Vector3.zero;
  if(hand==null||item==null||hand.joints==null||hand.joints.Length!=15||ShapeFor(item)==HandGrip.Pinch)return false;
  var j=hand.joints;for(int i=0;i<15;i++)if(j[i]==null)return false;
  var wrist=hand.transform.position;
  var palm=(wrist+j[0].position+j[3].position+j[6].position+j[9].position)/5f;
  var along=j[3].position-wrist;var across=j[0].position-j[9].position;
  var normal=Vector3.Cross(along,across);
  if(normal.sqrMagnitude<1e-10f||along.sqrMagnitude<1e-10f)return false;
  normal.Normalize();
  // The grasp side is the way the middle fingertip moves when the straightened finger first curls (0 to 25 degrees).
  // The whole finger is straightened for the test: with its other joints bent, the tip can move the other way.
  float c3=hand.JointCurl(3),c4=hand.JointCurl(4),c5=hand.JointCurl(5);
  hand.SetJointCurl(3,0);hand.SetJointCurl(4,0);hand.SetJointCurl(5,0);Segment(j,5,out _,out var tipBefore);
  hand.SetJointCurl(3,25);Segment(j,5,out _,out var tipAfter);
  hand.SetJointCurl(3,c3);hand.SetJointCurl(4,c4);hand.SetJointCurl(5,c5);
  if(Vector3.Dot(tipAfter-tipBefore,normal)<0)normal=-normal;
  var cols=CollidersOf(item);if(cols.Length==0)return false;
  var centre=item.Body!=null?item.Body.position:item.transform.position;
  // Exact support distance from the object's centre to its surface toward the palm, per collider shape.
  float extent=0;foreach(var c in cols)extent=Mathf.Max(extent,Support(c,centre,-normal));
  // Shift toward the fingers so the object sits under the finger roots, not the wrist.
  target=palm+along.normalized*(along.magnitude*.25f)+normal*(Mathf.Max(.01f,extent)+PalmRadius*.5f+Mathf.Clamp(extraOffset,0,.04f));
  return GraspMotionPolicy.Finite(target);
 }

 /// <summary>How far collider c reaches from point 'from' in direction d (sphere and oriented box exact; others by bounds).</summary>
 public static float Support(Collider c,Vector3 from,Vector3 d){
  var t=c.transform;var scale=t.lossyScale;
  switch(c){
   case SphereCollider sphere:{var centre=t.TransformPoint(sphere.center);return Vector3.Dot(centre-from,d)+sphere.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z)));}
   case BoxCollider box:{
    var centre=t.TransformPoint(box.center);var h=Vector3.Scale(box.size*.5f,scale);
    return Vector3.Dot(centre-from,d)+Mathf.Abs(Vector3.Dot(t.right,d))*Mathf.Abs(h.x)+Mathf.Abs(Vector3.Dot(t.up,d))*Mathf.Abs(h.y)+Mathf.Abs(Vector3.Dot(t.forward,d))*Mathf.Abs(h.z);}
   case CapsuleCollider capsule:{
    var centre=t.TransformPoint(capsule.center);var axis=capsule.direction==0?t.right:capsule.direction==1?t.up:t.forward;
    float r=capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));float half=Mathf.Max(0,capsule.height*.5f*Mathf.Abs(scale.y)-r);
    return Vector3.Dot(centre-from,d)+Mathf.Abs(Vector3.Dot(axis,d))*half+r;}
   default:{var b=c.bounds;var e=b.extents;return Vector3.Dot(b.center-from,d)+Mathf.Abs(d.x)*e.x+Mathf.Abs(d.y)*e.y+Mathf.Abs(d.z)*e.z;}
  }
 }

 // ---- Chart grips (GripProp): hand frame, anchors, hold pose, presentation ----
 static GripProp lastPropItemProp;static SqueezableObject lastPropItem;
 public static GripProp PropOf(SqueezableObject item){
  if(item==null)return null;
  if(!ReferenceEquals(item,lastPropItem)){lastPropItem=item;lastPropItemProp=item.GetComponent<GripProp>();}
  return lastPropItemProp;
 }
 /// <summary>Hand frame in the hand's own space. Knuckle origins do not move when fingers curl, so it is constant.</summary>
 sealed class HandFrame {
  public FingerPoseDriver hand;public Vector3 palm,alongVector,along,normal,thumb;
  public readonly Vector3[] anchors=new Vector3[GripCatalog.All.Length],pinch=new Vector3[GripCatalog.All.Length];public readonly bool[] hasAnchor=new bool[GripCatalog.All.Length];
 }
 static readonly HandFrame[] frames={new HandFrame(),new HandFrame()};
 static HandFrame FrameOf(FingerPoseDriver hand){
  if(hand==null||hand.joints==null||hand.joints.Length!=15)return null;
  foreach(var f in frames)if(ReferenceEquals(f.hand,hand))return f;
  for(int i=0;i<15;i++)if(hand.joints[i]==null)return null;
  var frame=ReferenceEquals(frames[0].hand,null)?frames[0]:frames[1];
  var j=hand.joints;var t=hand.transform;var wrist=t.position;
  var palm=(wrist+j[0].position+j[3].position+j[6].position+j[9].position)/5f;
  var along=j[3].position-wrist;var across=j[0].position-j[9].position;var normal=Vector3.Cross(along,across);
  if(normal.sqrMagnitude<1e-10f||along.sqrMagnitude<1e-10f)return null;normal.Normalize();
  if(Vector3.Dot(CurlDirection(hand),normal)<0)normal=-normal;
  var thumb=Vector3.Cross(normal,along.normalized);if(Vector3.Dot(thumb,across)<0)thumb=-thumb;
  frame.hand=hand;frame.palm=t.InverseTransformPoint(palm);frame.alongVector=t.InverseTransformVector(along);
  frame.along=t.InverseTransformDirection(along.normalized);frame.normal=t.InverseTransformDirection(normal);frame.thumb=t.InverseTransformDirection(thumb.normalized);
  Array.Clear(frame.hasAnchor,0,frame.hasAnchor.Length);
  return frame;
 }
 // The grasp side is the way the middle fingertip moves when the straightened finger first curls.
 static Vector3 CurlDirection(FingerPoseDriver hand){
  var j=hand.joints;float c3=hand.JointCurl(3),c4=hand.JointCurl(4),c5=hand.JointCurl(5);
  hand.SetJointCurl(3,0);hand.SetJointCurl(4,0);hand.SetJointCurl(5,0);Segment(j,5,out _,out var before);
  hand.SetJointCurl(3,25);Segment(j,5,out _,out var after);
  hand.SetJointCurl(3,c3);hand.SetJointCurl(4,c4);hand.SetJointCurl(5,c5);
  return after-before;
 }
 static readonly float[] savedCurls=new float[15];
 /// <summary>Hand-local anchor for a grip, measured once with the fingers at the grip's full closure.</summary>
 static Vector3 AnchorLocal(FingerPoseDriver hand,HandFrame frame,GripDefinition grip){
  int k=(int)grip.type;if(frame.hasAnchor[k])return frame.anchors[k];
  var j=hand.joints;var t=hand.transform;
  for(int n=0;n<15;n++)savedCurls[n]=hand.JointCurl(n);
  // Pinch axis (thumb toward the opposing finger pads) from the half-closed hand: thin objects lie across it.
  for(int n=0;n<15;n++)hand.SetJointCurl(n,grip.curl[n]*.5f);
  var pinchAxis=t.TransformDirection(frame.normal);
  if(grip.anchor==GripAnchor.Tips||grip.anchor==GripAnchor.Lateral){
   Segment(j,14,out var thumbBase,out var thumbTip);var fingers=Vector3.zero;int count=0;
   foreach(int n in grip.pads)if(n!=14&&n!=13&&n!=12){Segment(j,n,out var a,out var b);fingers+=grip.anchor==GripAnchor.Lateral?(a+b)*.5f:b;count++;}
   var thumb=grip.anchor==GripAnchor.Lateral?(thumbBase+thumbTip)*.5f:thumbTip;
   if(count>0&&(fingers/count-thumb).sqrMagnitude>1e-8f)pinchAxis=(fingers/count-thumb).normalized;
  }
  frame.pinch[k]=t.InverseTransformDirection(pinchAxis);
  for(int n=0;n<15;n++)hand.SetJointCurl(n,grip.curl[n]);
  Vector3 point=t.TransformPoint(frame.palm)+t.TransformVector(frame.alongVector)*.25f;
  switch(grip.anchor){
   case GripAnchor.Tips:{var sum=Vector3.zero;int count=0;foreach(int n in grip.pads)if(n%3==2){Segment(j,n,out _,out var tip);sum+=tip;count++;}if(count>0)point=sum/count;break;}
   case GripAnchor.Lateral:{Segment(j,14,out var a,out var b);Segment(j,1,out var c,out var d);point=((a+b)*.5f+(c+d)*.5f)*.5f;break;}
   case GripAnchor.Hook:{var sum=Vector3.zero;int count=0;foreach(int n in grip.pads){sum+=j[n].position;count++;}if(count>0)point=sum/count;break;}
  }
  for(int n=0;n<15;n++)hand.SetJointCurl(n,savedCurls[n]);
  frame.anchors[k]=t.InverseTransformPoint(point);frame.hasAnchor[k]=true;return frame.anchors[k];
 }
 /// <summary>World pose of a held object for its chart grip: the grip point at the hand's anchor, the
 /// object turned into the hand basis. extraOffset moves a palm hold off the palm while it overlaps.</summary>
 public static bool TryGripPose(FingerPoseDriver hand,SqueezableObject item,float extraOffset,out Vector3 position,out Quaternion rotation){
  position=Vector3.zero;rotation=Quaternion.identity;
  var prop=PropOf(item);var frame=FrameOf(hand);if(prop==null||frame==null||item.IsBroken)return false;
  var grip=prop.Definition;var t=hand.transform;
  var along=t.TransformDirection(frame.along);var normal=t.TransformDirection(frame.normal);var thumb=t.TransformDirection(frame.thumb);
  var anchor=t.TransformPoint(AnchorLocal(hand,frame,grip));
  // Pinches orient the object across the thumb-to-finger axis, so a coin or card is held by its faces.
  var holdNormal=normal;
  if(grip.AlignsToPinch){
   var pinch=Vector3.ProjectOnPlane(t.TransformDirection(frame.pinch[(int)grip.type]),along);
   if(pinch.sqrMagnitude>1e-6f)holdNormal=-pinch.normalized;
  }
  var holdThumb=Vector3.Cross(holdNormal,along);if(Vector3.Dot(holdThumb,thumb)<0)holdThumb=-holdThumb;
  rotation=prop.HoldRotation(holdThumb.normalized,holdNormal,along);
  float extent=Mathf.Max(.003f,ExtentToward(item,prop,rotation,-normal));
  Vector3 held;
  switch(grip.anchor){
   case GripAnchor.Palm:held=anchor+normal*(extent+PalmRadius*.5f+Mathf.Clamp(extraOffset,0,.04f));break;
   case GripAnchor.Hook:held=anchor+normal*(extent+.009f)-along*(extent+.009f);break;
   default:held=anchor;break;
  }
  position=held-rotation*Vector3.Scale(prop.gripLocal,item.transform.lossyScale);
  return GraspMotionPolicy.Finite(position);
 }
 /// <summary>How far the object reaches from its grip point in world direction dir, once turned to rotation.</summary>
 static float ExtentToward(SqueezableObject item,GripProp prop,Quaternion rotation,Vector3 dir){
  var local=Quaternion.Inverse(rotation)*dir;var now=item.transform.rotation*local;var from=item.transform.TransformPoint(prop.gripLocal);
  // Only the parts at the grip count (a hammer head 15 cm up the handle must not push the handle off the palm).
  float extent=0,any=0;
  foreach(var c in CollidersOf(item)){if((c.ClosestPoint(from)-from).sqrMagnitude>.03f*.03f)continue;extent=Mathf.Max(extent,Support(c,from,now));any=1;}
  if(any==0)foreach(var c in CollidersOf(item))extent=Mathf.Max(extent,Support(c,from,now));
  return extent;
 }
 /// <summary>Hand rotation that shows the grip like its chart photograph to the given camera.</summary>
 public static bool TryViewRotation(FingerPoseDriver hand,Transform camera,GripView view,bool left,out Quaternion rotation){
  rotation=Quaternion.identity;var frame=FrameOf(hand);if(frame==null||camera==null)return false;
  GripCatalog.ViewBasis(view,left,out var along,out var palm);
  var a=camera.TransformDirection(along).normalized;var n=Vector3.ProjectOnPlane(camera.TransformDirection(palm),a);
  if(n.sqrMagnitude<1e-6f)return false;
  rotation=Quaternion.LookRotation(a,n.normalized)*Quaternion.Inverse(Quaternion.LookRotation(frame.along,frame.normal));
  return true;
 }
 /// <summary>Two-handed grips: where the second hand's wrist goes so its palm rests on the far side of the object.</summary>
 public static bool TrySecondHandPosition(FingerPoseDriver hand,SqueezableObject item,out Vector3 wrist){
  wrist=Vector3.zero;var prop=PropOf(item);var frame=FrameOf(hand);if(prop==null||frame==null)return false;
  var t=hand.transform;var normal=t.TransformDirection(frame.normal);var centre=item.transform.TransformPoint(prop.gripLocal);
  float extent=0;foreach(var c in CollidersOf(item))extent=Mathf.Max(extent,Support(c,centre,-normal));
  var palm=t.TransformPoint(frame.palm)+t.TransformVector(frame.alongVector)*.25f;
  var wanted=centre-normal*(extent+PalmRadius*.5f);
  wrist=t.position+(wanted-palm);return GraspMotionPolicy.Finite(wrist);
 }

 static float FingerDepth(Transform[] joints,int finger,int fromJoint,float radius,ref Surface surface){
  float deepest=float.NegativeInfinity;
  for(int j=fromJoint;j<3;j++){Segment(joints,finger*3+j,out var a,out var b);deepest=Mathf.Max(deepest,ContactGeometry.CapsuleDepth(a,b,radius,ref surface));}
  return deepest;
 }
}
}
