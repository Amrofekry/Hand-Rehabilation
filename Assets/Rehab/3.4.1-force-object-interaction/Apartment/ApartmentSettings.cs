using UnityEngine;
namespace GripRehab {
[CreateAssetMenu(menuName="Grip Rehab/Apartment Settings")]
public sealed class ApartmentSettings:ScriptableObject {
 [Header("Movement")][Range(.5f,3)] public float walkSpeed=1.7f;
 [Range(.2f,4)] public float mouseSensitivity=1.2f;
 [Header("Demo only — not treatment prescriptions")][Range(1,80)] public float demoForceNewtons=18;
 [Range(.5f,2)] public float reachMeters=1.6f;
 [Tooltip("Small E pickup cone; surface range and visibility still apply.")][Range(1,15)] public float pickupAssistDegrees=9;
 [Tooltip("Visual-hand release gain, not measured throw speed.")][Range(0,.5f)] public float releaseVelocityGain=.15f;
 [Range(.1f,2)] public float maximumReleaseSpeed=1.2f;
 [Tooltip("Larger pose jumps do not generate release velocity.")][Range(.05f,.5f)] public float maximumReleaseStep=.25f;
 [Range(.02f,.3f)] public float trackingSmoothing=.045f;
 [Tooltip("Camera image displacement to approximate hand travel. Monocular depth is not metric.")] [Range(.2f,1.5f)] public float trackingTravelMeters=.85f;
 [Header("Camera interaction - visual estimate, not measured force")]
 [Tooltip("Open the local webcam and select camera control when Play starts. Acceptance smoke runs remain demo-only.")] public bool startWithCamera=true;
 [Range(.25f,.9f)] public float cameraCloseAt=.55f;
 [Range(.05f,.5f)] public float cameraOpenAt=.28f;
 [Range(.05f,.4f)] public float cameraGestureDwellSeconds=.12f;
 [Range(.03f,.2f)] public float cameraGrabRadiusMeters=.09f;
 [Range(5,80)] public float cameraVisualMaxNewtons=80f;
 [Header("Vendor hand calibration")]
 [Tooltip("Pull the left Gleechi wrist toward the camera so its fingertips align with the carried object.")] [Range(0,.3f)] public float leftWristSetbackMeters=.18f;
 [Header("Seated grip clearance")]
 [Range(0,.5f)] public float seatedHoldLiftMeters=.28f;
 [Range(0,.5f)] public float seatedHandsLiftMeters=.22f;
 [Header("Contact-force filtering")]
 [Min(.005f)] public float forceSmoothingSeconds=.06f;
 [Min(1)] public float isolatedSpikeNewtons=30;
 [Min(1)] public float maxForceSlewNewtonsPerSecond=250;
 [Tooltip("Use calibrated Phase 2 samples. Default is keyboard/mouse demo.")] public bool startWithHardware=false;
}
}
