using UnityEngine;
namespace GripRehab {
[CreateAssetMenu(menuName="Grip Rehab/Physics hand settings")]
public sealed class PhysicsHandSettings:ScriptableObject {
 [HideInInspector] public int schemaVersion;
 [Header("Layers — installed by PhysicsHandAuthoring")][Range(8,31)] public int handLayer=10,surfaceLayer=11;
 [Header("Contact — metres")][Min(.0001f)] public float contactOffset=.001f;
 [Min(.0001f)] public float maxPenetration=.002f;
 [Min(.001f)] public float surfacePatchRadius=.025f;
 [Min(.0005f)] public float surfaceThickness=.003f;
 [Range(.8f,1.15f)] public float colliderScale=1.02f;
 public float[] fingerRadiusScale=new float[]{1,1,1,1,1};
 [Header("Mesh-fit capsule bounds — metres")][Min(.001f)] public float minimumFingerRadius=.003f,maximumFingerRadius=.018f;
 [Header("Body masses — kilograms")][Min(.005f)] public float palmMass=.20f,segmentMass=.015f;
 [Header("Wrist drive")][Min(0)] public float positionSpring=100,positionDamping=14,maxWristForce=6;
 [Min(0)] public float rotationSpring=.8f,rotationDamping=.08f,maxWristTorque=.15f;
 [Header("Finger joint drives")][Min(0)] public float fingerSpring=.10f,fingerDamping=.015f,maxFingerTorque=.008f;
 [Range(1,170)] public float flexionLimit=120,swingLimit=85;
 [Header("Motion safety")][Min(.01f)] public float maxVelocity=.8f,maxAngularVelocity=5,maxDepenetrationVelocity=.20f;
 [Min(.01f)] public float nearSurfaceAngularVelocity=1;
 [Header("Residual overlap correction — bounded force, no pose projection")][Min(0)] public float penetrationSpring=100,penetrationDamping=1,maximumCorrectionForce=.5f;
 [Min(.1f)] public float worldOriginResetDistance=.5f;
 [Range(6,30)] public int solverIterations=12;
 [Range(1,12)] public int solverVelocityIterations=4;
 [Header("Stable grasp")][Range(-1,0)] public float opposingNormalDot=-.3f;
 [Tooltip("Allow palm support opposing two or more contacting fingers, as in a power grip.")] public bool allowPalmSupport=true;
 [Min(.02f)] public float gripDwellSeconds=.12f;
 [Min(0)] public float gripSpring=55,gripDamping=7,maxGripForce=8,maxGripTorque=.05f;
 [Min(.01f)] public float maximumGripSeparation=.35f;
 [Header("Debug")] public bool showGizmos;
 static bool Positive(float value)=>float.IsFinite(value)&&value>0;
 static bool Nonnegative(float value)=>float.IsFinite(value)&&value>=0;
 public bool Valid {
  get {
   if(handLayer<8||surfaceLayer<8||handLayer>31||surfaceLayer>31||handLayer==surfaceLayer)return false;
   if(!Positive(contactOffset)||!Positive(maxPenetration)||!Positive(surfacePatchRadius)||!Positive(surfaceThickness)||!Positive(colliderScale)||!Positive(palmMass)||!Positive(segmentMass))return false;
   if(!Nonnegative(positionSpring)||!Nonnegative(positionDamping)||!Positive(maxWristForce)||!Nonnegative(rotationSpring)||!Nonnegative(rotationDamping)||!Nonnegative(maxWristTorque))return false;
   if(!Nonnegative(fingerSpring)||!Nonnegative(fingerDamping)||!Nonnegative(maxFingerTorque)||!Positive(flexionLimit)||flexionLimit>=180||!Positive(swingLimit)||swingLimit>=180)return false;
   if(!Positive(maxVelocity)||!Positive(maxAngularVelocity)||!Positive(maxDepenetrationVelocity)||!Positive(nearSurfaceAngularVelocity)||!Positive(worldOriginResetDistance)||solverIterations<1||solverVelocityIterations<1)return false;
   if(!Nonnegative(penetrationSpring)||!Nonnegative(penetrationDamping)||!Nonnegative(maximumCorrectionForce))return false;
   if(!float.IsFinite(opposingNormalDot)||opposingNormalDot<-1||opposingNormalDot>0||!Positive(gripDwellSeconds)||!Nonnegative(gripSpring)||!Nonnegative(gripDamping)||!Positive(maxGripForce)||!Nonnegative(maxGripTorque)||!Positive(maximumGripSeparation))return false;
   if(!Positive(minimumFingerRadius)||!Positive(maximumFingerRadius)||maximumFingerRadius<minimumFingerRadius)return false;
   if(fingerRadiusScale==null||fingerRadiusScale.Length!=5)return false;foreach(float value in fingerRadiusScale)if(!Positive(value))return false;
   return true;
  }
 }
}
}
