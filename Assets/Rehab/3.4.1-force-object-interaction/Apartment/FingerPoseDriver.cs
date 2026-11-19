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
        [Header("Camera hand (2026-10-29)")]
        [Tooltip("Seconds the last tracked pose is held through a tracking gap before easing to rest.")]
        public float lossGraceSeconds = .6f;
        [Tooltip("A gap longer than this re-anchors position, depth and palm references on reacquisition.")]
        public float reanchorAfterSeconds = 1.5f;
        [Tooltip("Largest sideways / vertical reach from rest in camera mode (metres).")]
        public float reachX = .30f, reachY = .26f;
        [Tooltip("One Euro filter on the camera hand position: min cutoff (Hz) and speed coefficient. Lower cutoff = steadier, higher beta = less lag.")]
        public float positionMinCutoff = 1.2f, positionBeta = 8f;
        [Tooltip("One Euro filter on the palm-size depth estimate (noisier than position, so steadier settings).")]
        public float depthMinCutoff = .3f, depthBeta = .3f;

        public Vector3 PalmDirection { get; private set; }
        public Vector3 PalmNormal { get; private set; }
        public float[] JointAngles => _smoothed;
        public bool TrackingUsed { get; private set; }
        public float Closure { get; private set; }
        public bool UsingWorldLandmarks { get; private set; }
        // Stage 8 grip chart: keyboard/demo input shapes the hand into the held object's grip and turns it to
        // the chart's viewing angle. A tracked camera hand ignores both and keeps its measured pose.
        public GripDefinition DemoGrip;
        public bool HasDemoRotation;
        public Quaternion DemoRotation = Quaternion.identity;
        public long LastPoseSequence { get; private set; }
        /// <summary>Hand Lab diagnostics: the camera-mode hand target before filtering, and the palm size relative to its reference.</summary>
        public Vector3 LastTargetLocal { get; private set; }
        public float LastPalmSize { get; private set; }

        private readonly Quaternion[] _rest = new Quaternion[15];
        private readonly Transform[] _cachedJoints = new Transform[15];
        private Vector3 _safeCurlAxis = Vector3.right, _safeSpreadAxis = Vector3.up;
        private float _safeCurlSign = 1;
        public bool RigReady { get; private set; }
        private Quaternion _rootRest, _cameraPalmReference;
        private Vector3 _wristReference;
        /// <summary>Distance between two normalised image points with x scaled by the image aspect (equal pixels per unit).</summary>
        internal static float ImageDistance(Vector3 a, Vector3 b, float aspect) { float dx = (a.x - b.x) * aspect, dy = a.y - b.y; return Mathf.Sqrt(dx * dx + dy * dy); }
        private float _palmWidthReference, _palmLengthReference, _clock, _lastTrackedAt = -10;
        private bool _referencesValid;
        private OneEuroFilter _fx, _fy, _fz;
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
        public void UseDemoPose() { _everTracked = false; _anchored = false; _referencesValid = false; TrackingUsed = UsingWorldLandmarks = false; LastPoseSequence=0; }
        public void RecenterTracking() { _anchored = false; _referencesValid = false; }

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
            _clock += dt;
            var liveCurlAxis=HandRigRules.Axis(curlAxis,_safeCurlAxis);
            var liveSpreadAxis=HandRigRules.Axis(spreadAxis,_safeSpreadAxis);
            float liveCurlSign=float.IsFinite(curlSign)?Mathf.Clamp(curlSign,-1,1):_safeCurlSign;
            _liveCurlAxis=liveCurlAxis;_liveSpreadAxis=liveSpreadAxis;_liveCurlSign=liveCurlSign;
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
                // Palm size in image units with x scaled by the aspect (x is normalised by width, y by height): a hand that
                // only rotates in the image (a wrist roll to pour) keeps its size, so the depth estimate does not jump.
                float aspect = frame.imageWidth > 0 && frame.imageHeight > 0 ? frame.imageWidth / (float)frame.imageHeight : 4f / 3f;
                var width = ImageDistance(frame.landmarks[5], frame.landmarks[17], aspect);
                // Two palm-size measures (palm width and wrist-to-middle-knuckle length) averaged: half the noise of either alone.
                var length = ImageDistance(frame.landmarks[0], frame.landmarks[9], aspect);
                HandKinematics.TryPalmBasis(palmSide, palmUp, out var currentBasis);
                if (!_anchored)
                {
                    // Camera mode: references survive short gaps (no re-anchoring jump); a long gap or an explicit recenter takes new ones. Relative mode re-anchors as before.
                    if (!_referencesValid || !absoluteImageSpace || _clock - _lastTrackedAt > reanchorAfterSeconds)
                    {
                        _wristReference = wristImage;
                        _palmWidthReference = Mathf.Max(.025f, width);
                        _palmLengthReference = Mathf.Max(.025f, length);
                        _cameraPalmReference = currentBasis;
                        _referencesValid = true;
                        _fx.Reset(); _fy.Reset(); _fz.Reset();
                    }
                    _anchored = true;
                }
                _lastTrackedAt = _clock;
                // Camera mode maps the image center to the resting hand, so moving
                // the real wrist toward an on-screen object moves the virtual wrist.
                // Relative mode stays available for the older synthetic/demo contract.
                var delta = absoluteImageSpace ? wristImage - new Vector3(.5f, .5f, 0) : wristImage - _wristReference;
                // Keep image X as received. Y conversion and palm-size depth
                // mapping retain their existing signs and limits.
                if (absoluteImageSpace)
                {
                    // Camera mode: One Euro filtered position (steady at rest, little lag when moving) and a filtered depth.
                    float size = .5f * (width / _palmWidthReference + (_palmLengthReference > 0 ? length / _palmLengthReference : width / _palmWidthReference));
                    _fx.minCutoff = _fy.minCutoff = positionMinCutoff; _fx.beta = _fy.beta = positionBeta; _fx.dCutoff = _fy.dCutoff = 1f;
                    _fz.minCutoff = depthMinCutoff; _fz.beta = depthBeta; _fz.dCutoff = 1f;
                    float rx = float.IsFinite(reachX) ? Mathf.Clamp(reachX, .05f, .6f) : .30f, ry = float.IsFinite(reachY) ? Mathf.Clamp(reachY, .05f, .6f) : .26f;
                    var target = restingPosition + new Vector3(
                        Mathf.Clamp(delta.x * travelMeters, -rx, rx),
                        Mathf.Clamp(delta.y * travelMeters, -ry, ry),
                        Mathf.Clamp((size - 1f) * .75f, -.20f, .65f));
                    LastTargetLocal = target; LastPalmSize = size; // Hand Lab: the mapping before the filters
                    if (!_fx.Primed) { _fx.Step(transform.localPosition.x, dt); _fy.Step(transform.localPosition.y, dt); _fz.Step(transform.localPosition.z, dt); }
                    transform.localPosition = new Vector3(_fx.Step(target.x, dt), _fy.Step(target.y, dt), _fz.Step(target.z, dt));
                }
                else
                {
                    var requested = restingPosition + new Vector3(
                        Mathf.Clamp(delta.x * travelMeters, -.23f, .23f),
                        Mathf.Clamp(delta.y * travelMeters, -.20f, .20f),
                        Mathf.Clamp((width / _palmWidthReference - 1f) * .08f, -.12f, .12f));
                    transform.localPosition = Vector3.Lerp(transform.localPosition, requested, alpha);
                }
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
                if (_everTracked && (holdLastTrackedPoseOnLoss || _clock - _lastTrackedAt < lossGraceSeconds)) return;
                transform.localPosition = Vector3.Lerp(transform.localPosition, restingPosition, alpha);
                var restRotation = _rootRest;
                if (HasDemoRotation && HandRigRules.TryRotation(DemoRotation, out var presented))
                    restRotation = transform.parent != null ? Quaternion.Inverse(transform.parent.rotation) * presented : presented;
                transform.localRotation = Quaternion.Slerp(transform.localRotation, restRotation, alpha);
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
                    var grip = DemoGrip;
                    for(int joint=0;joint<3;joint++){
                        int n=finger*3+joint;float limit=HandRigRules.DemoLimit(finger,joint);
                        float shaped=grip!=null?grip.CurlAt(n,closure):closure*limit;
                        _angles[n]=Mathf.Max(shaped,independentKeyboard?HandRigRules.Unit(ManualJoint[n])*limit:0);
                    }
                    _spread[finger] = grip!=null?grip.spread[finger]:0;
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

        // Stage 8 contact solver: read a joint's applied curl and re-apply a limited value.
        // Writing back into the smoothed angle means the next frame starts from the contact
        // pose, so a finger resting on an object does not flicker between open and closed.
        private Vector3 _liveCurlAxis = Vector3.right, _liveSpreadAxis = Vector3.up;
        private float _liveCurlSign = 1;
        public float JointCurl(int joint) => RigReady && joint >= 0 && joint < 15 && float.IsFinite(_smoothed[joint]) ? _smoothed[joint] : 0;
        public void SetJointCurl(int joint, float degrees)
        {
            if (!RigReady || joint < 0 || joint >= 15 || joints[joint] == null || !float.IsFinite(degrees)) return;
            _smoothed[joint] = degrees;
            int finger = joint / 3;
            var curl = Quaternion.AngleAxis(degrees * _liveCurlSign, _liveCurlAxis);
            var splay = joint % 3 == 0 ? Quaternion.AngleAxis(_spreadSmoothed[finger] * (finger == 4 ? .6f : 1f), _liveSpreadAxis) : Quaternion.identity;
            joints[joint].localRotation = _rest[joint] * splay * curl;
        }
    }
}
