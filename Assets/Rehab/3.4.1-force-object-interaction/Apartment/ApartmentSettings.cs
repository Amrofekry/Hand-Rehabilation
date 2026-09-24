using UnityEngine;
namespace GripRehab {
[CreateAssetMenu(menuName="Grip Rehab/Apartment Settings")]
public sealed class ApartmentSettings:ScriptableObject {
 [Header("Movement")][Range(.5f,3)] public float walkSpeed=1.7f;
 [Range(.2f,4)] public float mouseSensitivity=1.2f;
 [Header("Demo only — not treatment prescriptions")][Range(1,80)] public float demoForceNewtons=18;
 [Range(.5f,2)] public float reachMeters=1.6f;
 [Range(.02f,.3f)] public float trackingSmoothing=.08f;
 [Tooltip("Use calibrated Phase 2 samples. Default is keyboard/mouse demo.")] public bool startWithHardware=false;
}
}
