using UnityEngine;
namespace GripRehab {
[CreateAssetMenu(menuName="Grip Rehab/Apartment Settings")]
public sealed class ApartmentSettings:ScriptableObject {
 [Header("Movement")][Range(.5f,3)] public float walkSpeed=1.7f;
 [Range(.2f,4)] public float mouseSensitivity=1.2f;
 [Header("Demo only — not treatment prescriptions")][Range(1,80)] public float demoForceNewtons=18;
 [Range(.5f,2)] public float reachMeters=1.6f;
 [Range(.02f,.3f)] public float trackingSmoothing=.08f;
 [Tooltip("Camera image displacement to approximate hand travel. Monocular depth is not metric.")] [Range(.2f,1.5f)] public float trackingTravelMeters=.85f;
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
