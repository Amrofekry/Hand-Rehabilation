using UnityEngine;

namespace GripRehab
{
    // Drives the existing skinned hand's 15 finger joints from MediaPipe's 21 points.
    // The camera supplies pose; the single load cell supplies total right-grip force only.
    public sealed class FingerPoseDriver : MonoBehaviour
    {
        [Tooltip("Index, middle, ring, little, thumb; proximal/intermediate/distal for each.")]
        public Transform[] joints;
        public bool independentKeyboard;
        public readonly float[] ManualCurl=new float[5];
        public int SelectedFinger;
        public int SelectedJoint=-1;
        public readonly float[] ManualJoint=new float[15];
        public void AdjustFinger(int finger,float delta){
            if(!float.IsFinite(delta))return;
            finger=Mathf.Clamp(finger,0,4);SelectedJoint=Mathf.Clamp(SelectedJoint,-1,2);
            if(SelectedJoint<0)ManualCurl[finger]=HandRigRules.Unit(HandRigRules.Unit(ManualCurl[finger])+delta);
            else{int n=finger*3+SelectedJoint;ManualJoint[n]=HandRigRules.Unit(HandRigRules.Unit(ManualJoint[n])+delta);}
        }
        public void ResetFingers(){System.Array.Clear(ManualCurl,0,5);System.Array.Clear(ManualJoint,0,15);}
        [Header("Rig axes — tune against the imported hand mesh")]
        public Vector3 curlAxis = Vector3.right;
        public float curlSign = 1;
        public Vector3 spreadAxis = Vector3.up;
        [Range(0, 1)] public float palmRotationWeight = 1f;
        [Range(0, 180)] public float maxPalmRotationDegrees = 160;
        [Header("Tracking loss")]
        [Tooltip("If false, blend to the visible demo/rest pose when this hand disappears.")]
        public bool holdLastTrackedPoseOnLoss = false;

        public Vector3 PalmDirection { get; private set; }
        public Vector3 PalmNormal { get; private set; }
        public float[] JointAngles => _smoothed;
        public bool TrackingUsed { get; private set; }
        public float Closure { get; private set; }
        public bool UsingWorldLandmarks { get; private set; }
        public long LastPoseSequence { get; private set; }

        private readonly Quaternion[] _rest = new Quaternion[15];
        private readonly Transform[] _cachedJoints = new Transform[15];
        private Vector3 _safeCurlAxis = Vector3.right, _safeSpreadAxis = Vector3.up;
        private float _safeCurlSign = 1;
        public bool RigReady { get; private set; }
        private Quaternion _rootRest, _cameraPalmReference;
        private Vector3 _wristReference;
        private float _palmWidthReference;
        private bool _anchored, _everTracked;
        private readonly float[] _angles = new float[15];
        private readonly float[] _smoothed = new float[15];
        private readonly float[] _spread = new float[5];
        private readonly float[] _spreadSmoothed = new float[5];
        private readonly Vector3[] _points = new Vector3[21];

        private void Awake()
        {
            HandRigRules.TryRotation(transform.localRotation, out _rootRest);
            _safeCurlAxis = HandRigRules.Axis(curlAxis, Vector3.right);
            _safeSpreadAxis = HandRigRules.Axis(spreadAxis, Vector3.up);
            _safeCurlSign = float.IsFinite(curlSign) ? Mathf.Clamp(curlSign,-1,1) : 1;
            CacheJoints();
        }
        private void CacheJoints()
        {
            RigReady = joints != null && joints.Length == 15;
            for(int i=0;i<15;i++)
            {
                var bone=joints!=null&&i<joints.Length?joints[i]:null;
                if(bone!=_cachedJoints[i])
                {
                    _cachedJoints[i]=bone;
                    _rest[i]=Quaternion.identity;
                    if(bone!=null)HandRigRules.TryRotation(bone.localRotation,out _rest[i]);
                }
                if(bone==null)RigReady=false;
                else for(int j=0;j<i;j++)if(bone==_cachedJoints[j])RigReady=false;
            }
        }
        public void UseDemoPose() { _everTracked = false; _anchored = false; TrackingUsed = UsingWorldLandmarks = false; LastPoseSequence=0; }
        public void RecenterTracking() { _anchored = false; }

        public static float Bend(Vector3 a, Vector3 b, Vector3 c)
        {
            var first = b - a;
            var second = c - b;
            if (first.sqrMagnitude < 1e-8f || second.sqrMagnitude < 1e-8f) return 0;
            return Mathf.Clamp(Vector3.Angle(first, second), 0, 100);
        }

        private static float ProximalFlexion(Vector3 palmUp, Vector3 palmSide, Vector3 finger)
        {
            var reference = Vector3.ProjectOnPlane(palmUp, palmSide);
            var projected = Vector3.ProjectOnPlane(finger, palmSide);
            if (reference.sqrMagnitude < 1e-8f || projected.sqrMagnitude < 1e-8f) return 0;
            return Mathf.Clamp(Vector3.Angle(reference, projected), 0, 85);
        }

        public void Pose(LandmarkFrame frame, bool tracking, float demoClosure, float dt, float smoothing)
        {
            Pose(frame, tracking, demoClosure, dt, smoothing, transform.localPosition, 0);
        }

        public void Pose(LandmarkFrame frame, bool tracking, float demoClosure, float dt,
                         float smoothing, Vector3 restingPosition, float travelMeters, bool absoluteImageSpace = false)
        {
            CacheJoints();SelectedFinger=Mathf.Clamp(SelectedFinger,0,4);SelectedJoint=Mathf.Clamp(SelectedJoint,-1,2);
            dt=HandRigRules.SafeDt(dt);
            TrackingUsed = RigReady && dt>0 && tracking && HandKinematics.HasPoseGeometry(frame);
            UsingWorldLandmarks = TrackingUsed && HandKinematics.HasWorldPose(frame);
            LastPoseSequence=TrackingUsed?frame.sequence:0;
            if(!RigReady){Closure=0;return;}
            if(dt==0)return; // Invalid time must never poison any Transform.
            if(!HandRigRules.Finite(restingPosition))restingPosition=transform.localPosition;
            travelMeters=float.IsFinite(travelMeters)?Mathf.Clamp(travelMeters,0,2):0;
            smoothing=float.IsFinite(smoothing)?Mathf.Clamp(smoothing,.02f,1):.045f;
            var alpha = 1f - Mathf.Exp(-dt / smoothing);
            var liveCurlAxis=HandRigRules.Axis(curlAxis,_safeCurlAxis);
            var liveSpreadAxis=HandRigRules.Axis(spreadAxis,_safeSpreadAxis);
            float liveCurlSign=float.IsFinite(curlSign)?Mathf.Clamp(curlSign,-1,1):_safeCurlSign;
            var palmUp = Vector3.up;
            var palmSide = Vector3.right;
            Vector3[] points = null;
            if (TrackingUsed)
            {
                _everTracked = true;
                points = _points;
                // The worker already mirrors the image into the patient-facing view.
                // Use that same horizontal convention for position and rotation;
                // a second X reflection reverses travel and signed wrist turns.
                for (int i = 0; i < 21; i++) points[i] = HandKinematics.PosePoint(frame, i);
                palmUp = (points[9] - points[0]).normalized;
                palmSide = (points[5] - points[17]).normalized;
                PalmDirection = palmUp;
                PalmNormal = Vector3.Cross(palmSide, palmUp).normalized;

                var wristImage = HandKinematics.ToCameraSpace(frame.landmarks[0]);
                var width = Vector3.Distance(frame.landmarks[5], frame.landmarks[17]);
                HandKinematics.TryPalmBasis(palmSide, palmUp, out var currentBasis);
                if (!_anchored)
                {
                    _wristReference = wristImage;
                    _palmWidthReference = Mathf.Max(.025f, width);
                    _cameraPalmReference = currentBasis;
                    _anchored = true;
                }
                // Camera mode maps the image center to the resting hand, so moving
                // the real wrist toward an on-screen object moves the virtual wrist.
                // Relative mode stays available for the older synthetic/demo contract.
                var delta = absoluteImageSpace ? wristImage - new Vector3(.5f, .5f, 0) : wristImage - _wristReference;
                // Keep image X as received. Y conversion and palm-size depth
                // mapping retain their existing signs and limits.
                var requested = restingPosition + new Vector3(
                    Mathf.Clamp(delta.x * travelMeters, -.23f, .23f),
                    Mathf.Clamp(delta.y * travelMeters, -.20f, .20f),
                    Mathf.Clamp((width / _palmWidthReference - 1f) * (absoluteImageSpace ? .75f : .08f), absoluteImageSpace ? -.20f : -.12f, absoluteImageSpace ? .65f : .12f));
                transform.localPosition = Vector3.Lerp(transform.localPosition, requested, alpha);
                // Preserve the upright fingers, then turn the palm over around
                // the upright vertical axis: the camera sees the opposite side
                // of the hand from the patient. This changes the rest facing,
                // not the signs of subsequent measured turns or finger bends.
                var poseRest = absoluteImageSpace
                    ? Quaternion.AngleAxis(180f, Vector3.up) *
                      Quaternion.AngleAxis(180f, Vector3.forward) * _rootRest
                    : _rootRest;
                var measured = currentBasis * Quaternion.Inverse(_cameraPalmReference) * poseRest;
                var limited = Quaternion.RotateTowards(poseRest, measured, float.IsFinite(maxPalmRotationDegrees)?Mathf.Clamp(maxPalmRotationDegrees,0,180):160);
                transform.localRotation = Quaternion.Slerp(transform.localRotation,
                    Quaternion.Slerp(poseRest, limited, float.IsFinite(palmRotationWeight)?Mathf.Clamp01(palmRotationWeight):1), alpha);
            }
            else
            {
                _anchored = false;
                if (_everTracked && holdLastTrackedPoseOnLoss) return;
                transform.localPosition = Vector3.Lerp(transform.localPosition, restingPosition, alpha);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, _rootRest, alpha);
            }

            var total = 0f;
            for (int finger = 0; finger < 5; finger++)
            {
                var start = finger < 4 ? 5 + finger * 4 : 1;
                if (TrackingUsed)
                {
                    var proximal = points[start + 1] - points[start];
                    _angles[finger * 3] = finger == 4
                        ? Bend(points[0], points[1], points[2])
                        : ProximalFlexion(palmUp, palmSide, proximal);
                    _angles[finger * 3 + 1] = Bend(points[start], points[start + 1], points[start + 2]);
                    _angles[finger * 3 + 2] = Bend(points[start + 1], points[start + 2], points[start + 3]);
                    var neutral = finger == 4 ? 35f : (finger - 1.5f) * 8f;
                    _spread[finger] = Mathf.Clamp(
                        Vector3.SignedAngle(palmUp, proximal, PalmNormal) - neutral, -22f, 22f);
                }
                else
                {
                    var closure = Mathf.Max(HandRigRules.Unit(demoClosure), independentKeyboard ? HandRigRules.Unit(ManualCurl[finger]) : 0);
                    closure=Mathf.SmoothStep(0,1,Mathf.Clamp01((closure-finger*.025f)/(1-finger*.025f)));
                    for(int joint=0;joint<3;joint++){
                        int n=finger*3+joint;float limit=HandRigRules.DemoLimit(finger,joint);
                        _angles[n]=Mathf.Max(closure*limit,independentKeyboard?HandRigRules.Unit(ManualJoint[n])*limit:0);
                    }
                    _spread[finger] = 0;
                }
                _spreadSmoothed[finger] = Mathf.Lerp(float.IsFinite(_spreadSmoothed[finger])?_spreadSmoothed[finger]:0, _spread[finger], alpha);
                for (int joint = 0; joint < 3; joint++)
                {
                    var n = finger * 3 + joint;
                    _smoothed[n] = Mathf.Lerp(float.IsFinite(_smoothed[n])?_smoothed[n]:0, _angles[n], alpha);
                    if (n < joints.Length && joints[n] != null)
                    {
                        var curl = Quaternion.AngleAxis(_smoothed[n] * liveCurlSign, liveCurlAxis);
                        var splay = joint == 0
                            ? Quaternion.AngleAxis(_spreadSmoothed[finger] * (finger == 4 ? .6f : 1f), liveSpreadAxis)
                            : Quaternion.identity;
                        joints[n].localRotation = _rest[n] * splay * curl;
                    }
                    total += _smoothed[n] / 90f;
                }
            }
            Closure = total / 15f;
            LastPoseSequence = TrackingUsed ? frame.sequence : 0;
        }
    }
}
