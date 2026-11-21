"""Static integration checks; they do not run Unity/PhysX."""
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]
APT=ROOT/'Assets/Rehab/3.4.1-force-object-interaction/Apartment'
class GraspBindings(unittest.TestCase):
    def setUp(self):
        self.app=(APT/'ApartmentExperience.cs').read_text(encoding='utf-8-sig')
        self.obj=(APT/'SqueezableObject.cs').read_text(encoding='utf-8-sig')
        self.guard=(APT/'GraspCollisionShield.cs').read_text(encoding='utf-8-sig')
    def test_collision_ownership_survives_release(self):
        self.assertNotIn('IgnoreHeldPlayerCollision',self.app)
        self.assertIn('Held.Release(velocity);Held=null;',self.app)
        self.assertIn('void FixedUpdate(){collisionShield.Step(IsHeld);}',self.obj)
        self.assertIn('GraspMotionPolicy.Separated(',self.guard)
        self.assertIn('pair.originalIgnore',self.guard)
    def test_acquisition_and_hud_share_candidate(self):
        self.assertGreaterEqual(self.app.count('KeyboardTarget(out var door)'),2)
        self.assertIn('if(!CanReach(item))continue;',self.app)
        self.assertIn('GraspMotionPolicy.AimScore(',self.app)
        self.assertIn('hit.collider.GetComponentInParent<SqueezableObject>()==item',self.app)
    def test_child_colliders_surface_and_layer_scope(self):
        self.assertIn('GetComponentsInChildren<Collider>(true)',self.obj)
        self.assertIn('c.attachedRigidbody==Body',self.obj)
        self.assertIn('item.ClosestSurface(tip)',self.app)
        self.assertIn('item.isTrigger||player.isTrigger',self.guard)
    def test_mode_guards_and_shared_grab(self):
        self.assertIn('Hardware&&!forceReceiver.Latest.Valid||CameraMode&&!ForceValid',self.app)
        # 2026-11-21: on-screen targets (the movement assist's choice, the object under the camera hand) use the distance
        # check; every other grab keeps the eye's reach-and-visibility check. Both still need BeginGrab.
        self.assertIn('!(onScreen?InReach(item):CanReach(item))||!BeginGrab(item,false)',self.app)
        self.assertIn('!BeginGrab(item,true)',self.app)
        self.assertIn('if(CameraMode)SetHardware(false);',self.app)
    def test_held_and_release_pose(self):
        self.assertIn('GraspMotionPolicy.HeldRotation(ActiveHand.transform.rotation,_heldRotationOffset)',self.app)
        self.assertIn('GraspMotionPolicy.RelativeRotation(ActiveHand.transform.rotation,initial)',self.app)
        self.assertIn('GraspMotionPolicy.ReleaseVelocity(',self.app)
        self.assertIn('_lastHeld=target;_lastMotionTime=Time.unscaledTime;_velocity=Vector3.zero',self.app)
        self.assertIn('Time.unscaledTime-_lastMotionTime<=.1f',self.app)
    def test_clearance_and_body_lifecycle(self):
        self.assertIn('item.ClearanceRadius+.01f',self.app)
        self.assertIn('Physics.OverlapSphereNonAlloc(',self.app)
        self.assertIn('collider.GetComponentInParent<SqueezableObject>()==null',self.app)
        self.assertIn('if(count==_clearanceHits.Length)return false',self.app)
        self.assertIn('Physics.SphereCastNonAlloc(',self.app)
        # Stage 8: the landing check tests the object's own colliders at the target pose (was a bounding sphere).
        self.assertIn('Blocked(item,target,landing,clearance,mask,stage8)',self.app)
        self.assertIn('exact?ObjectBlockedAt(item,point,rotation,mask):SphereBlocked(point,clearance,mask)',self.app)
        self.assertIn('Physics.ComputePenetration(c,p,r,other',self.app)
        self.assertIn('Body.isKinematic=false;Body.useGravity=true',self.obj)
        self.assertIn('Body.collisionDetectionMode=freeCollisionMode;Body.interpolation=freeInterpolation',self.obj)
        self.assertIn('collisionShield.Clear()',self.obj)
        self.assertIn('private void OnDisable(){Release();}',self.app)
    def test_camera_explicit_release_does_not_immediately_regrab(self):
        self.assertIn('!_cameraReleaseBlocked&&_cameraGrip.Closed',self.app)
        self.assertIn('if(CameraMode)_cameraReleaseBlocked=true;',self.app)
        self.assertIn('if(cameraValid&&!_cameraGrip.Closed&&CameraGripScore<=settings.cameraOpenAt)_cameraReleaseBlocked=false;',self.app)
        self.assertIn('_cameraReleaseBlocked=false;_summonGraceUntil=',self.app)
    def test_native_acceptance_has_updated_separation_expectation(self):
        checks=(APT/'ApartmentAcceptance.cs').read_text(encoding='utf-8-sig')
        self.assertIn('Overlapping release retains player exclusion until separation',checks)
        self.assertIn('Dropped overlapping cup does not recoil the player',checks)
        self.assertIn('Separated drop restores its original player collision pair',checks)
if __name__=='__main__':unittest.main()
