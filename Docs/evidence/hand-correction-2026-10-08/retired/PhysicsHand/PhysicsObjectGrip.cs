using UnityEngine;
namespace GripRehab {
// A bounded, compliant grasp connection. Neither body is teleported or made
// kinematic while held; gravity and object/furniture collisions remain active.
public sealed class PhysicsObjectGrip {
 readonly Rigidbody body,palm;readonly PhysicsHandRig hand;readonly PhysicsHandSettings settings;ConfigurableJoint joint;
 float created;public bool Active=>joint!=null;
 public PhysicsObjectGrip(Rigidbody body,PhysicsHandRig hand,Vector3 target){
  created=Time.unscaledTime;this.body=body;this.hand=hand;palm=hand.Palm;settings=hand.settings;
  joint=body.gameObject.AddComponent<ConfigurableJoint>();joint.connectedBody=palm;joint.autoConfigureConnectedAnchor=false;joint.anchor=Vector3.zero;
  joint.connectedAnchor=palm.transform.InverseTransformPoint(target);
  joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Free;
  joint.angularXMotion=joint.angularYMotion=joint.angularZMotion=ConfigurableJointMotion.Free;
  // Linear grasp transport is an explicit bounded world-space force pair.
  // Keep the joint's translational axes free; changing connectedAnchor on an
  // XYZ drive can retain its creation-frame displacement instead of pulling
  // a distant assisted pickup into the intended current grasp aperture.
  joint.xDrive=joint.yDrive=joint.zDrive=new JointDrive();
  joint.rotationDriveMode=RotationDriveMode.Slerp;joint.slerpDrive=new JointDrive{positionSpring=.2f,positionDamper=.04f,maximumForce=settings.maxGripTorque};
  joint.targetRotation=Quaternion.identity;joint.enableCollision=true;joint.enablePreprocessing=false;
  body.isKinematic=false;body.useGravity=true;body.WakeUp();
 }
 public bool Step(Vector3 target){if(joint==null||palm==null||!hand.InputValid||!HandRigRules.Finite(target)||!HandRigRules.Finite(body.position)||!HandRigRules.Finite(body.linearVelocity))return false;
  joint.connectedAnchor=palm.transform.InverseTransformPoint(target);
  var force=(target-body.position)*settings.gripSpring-(body.linearVelocity-palm.linearVelocity)*settings.gripDamping;
  if(body.useGravity)force-=Physics.gravity*body.mass;
  force=Vector3.ClampMagnitude(force,settings.maxGripForce);
  body.AddForce(force,ForceMode.Force);if(!palm.isKinematic)palm.AddForce(-force,ForceMode.Force);
  body.linearVelocity=Vector3.ClampMagnitude(body.linearVelocity,settings.maxVelocity);
  body.angularVelocity=Vector3.ClampMagnitude(body.angularVelocity,settings.maxAngularVelocity);
  return Time.unscaledTime-created<4f||Vector3.Distance(body.position,target)<settings.maximumGripSeparation;
 }
 public void Release(){if(joint==null)return;
  joint.xDrive=joint.yDrive=joint.zDrive=joint.slerpDrive=new JointDrive();joint.xMotion=joint.yMotion=joint.zMotion=ConfigurableJointMotion.Free;
  joint.connectedBody=null;Object.Destroy(joint);joint=null;body.WakeUp();
 }
}
}
