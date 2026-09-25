using UnityEngine;

namespace GripRehab
{
    // Drives the existing skinned hand's 15 finger joints from MediaPipe's 21 points.
    // The camera supplies pose; the single load cell supplies total right-grip force only.
    public sealed class FingerPoseDriver : MonoBehaviour
    {
        [Tooltip("Index, middle, ring, little, thumb; proximal/intermediate/distal for each.")]
        public Transform[] joints;
        [Header("Rig axes — tune against the imported hand mesh")]
        public Vector3 curlAxis = Vector3.right;
        public float curlSign = 1;
        public Vector3 spreadAxis = Vector3.up;
        [Range(0, 1)] public float palmRotationWeight = .75f;
        [Range(0, 90)] public float maxPalmRotationDegrees = 55;
        [Header("Tracking loss")]
        [Tooltip("If false, blend to the visible demo/rest pose when this hand disappears.")]
        public bool holdLastTrackedPoseOnLoss = false;

        public Vector3 PalmDirection { get; private set; }
        public Vector3 PalmNormal { get; private set; }
        public float[] JointAngles => _smoothed;
        public bool TrackingUsed { get; private set; }
        public float Closure { get; private set; }

        private Quaternion[] _rest;
        private Quaternion _rootRest, _modelPalmBasis;
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
            _rootRest = transform.localRotation;
            _rest = new Quaternion[joints.Length];
            for (int i = 0; i < joints.Length; i++)
                if (joints[i] != null) _rest[i] = joints[i].localRotation;

            // This is the imported mesh's own palm basis, so the same mapping works
            // for either hand without assuming the FBX has Unity-standard axes.
            if (joints.Length >= 10 && joints[0] && joints[3] && joints[9])
            {
                var index = transform.InverseTransformPoint(joints[0].position);
                var middle = transform.InverseTransformPoint(joints[3].position);
                var little = transform.InverseTransformPoint(joints[9].position);
                var wrist = Vector3.zero;
                var palm = joints[3].parent;
                if (palm != null && palm.parent != null)
                    wrist = transform.InverseTransformPoint(palm.parent.position);
                _modelPalmBasis = Basis(index - little, middle - wrist);
            }
            else _modelPalmBasis = Quaternion.identity;
        }

        public void UseDemoPose() { _everTracked = false; _anchored = false; }

        public static float Bend(Vector3 a, Vector3 b, Vector3 c)
        {
            var first = b - a;
            var second = c - b;
            if (first.sqrMagnitude < 1e-8f || second.sqrMagnitude < 1e-8f) return 0;
            return Mathf.Clamp(Vector3.Angle(first, second), 0, 100);
        }

        private static Quaternion Basis(Vector3 lateral, Vector3 towardFingers)
        {
            if (lateral.sqrMagnitude < 1e-7f || towardFingers.sqrMagnitude < 1e-7f)
                return Quaternion.identity;
            var up = towardFingers.normalized;
            var side = Vector3.ProjectOnPlane(lateral, up).normalized;
            if (side.sqrMagnitude < 1e-7f) return Quaternion.identity;
            return Quaternion.LookRotation(Vector3.Cross(side, up).normalized, up);
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
                         float smoothing, Vector3 restingPosition, float travelMeters)
        {
            TrackingUsed = tracking && HandKinematics.IsUsable(frame);
            var alpha = 1f - Mathf.Exp(-Mathf.Max(0, dt) / Mathf.Max(.02f, smoothing));
            var palmUp = Vector3.up;
            var palmSide = Vector3.right;
            Vector3[] points = null;
            if (TrackingUsed)
            {
                _everTracked = true;
                points = _points;
                for (int i = 0; i < 21; i++) points[i] = HandKinematics.ToCameraSpace(frame.landmarks[i]);
                palmUp = (points[9] - points[0]).normalized;
                palmSide = (points[5] - points[17]).normalized;
                PalmDirection = palmUp;
                PalmNormal = Vector3.Cross(palmSide, palmUp).normalized;

                var width = Vector3.Distance(points[5], points[17]);
                if (!_anchored)
                {
                    _wristReference = points[0];
                    _palmWidthReference = Mathf.Max(.025f, width);
                    _anchored = true;
                }
                var delta = points[0] - _wristReference;
                var requested = restingPosition + new Vector3(
                    Mathf.Clamp(delta.x * travelMeters, -.23f, .23f),
                    Mathf.Clamp(delta.y * travelMeters, -.20f, .20f),
                    Mathf.Clamp((width / _palmWidthReference - 1f) * .08f, -.07f, .07f));
                transform.localPosition = Vector3.Lerp(transform.localPosition, requested, alpha);
                var measured = Basis(palmSide, palmUp) * Quaternion.Inverse(_modelPalmBasis);
                var limited = Quaternion.RotateTowards(_rootRest, measured, maxPalmRotationDegrees);
                transform.localRotation = Quaternion.Slerp(transform.localRotation,
                    Quaternion.Slerp(_rootRest, limited, palmRotationWeight), alpha);
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
                    var closure = Mathf.Clamp01(demoClosure);
                    _angles[finger * 3] = closure * (finger == 4 ? 45 : 65);
                    _angles[finger * 3 + 1] = closure * 85;
                    _angles[finger * 3 + 2] = closure * 55;
                    _spread[finger] = 0;
                }
                _spreadSmoothed[finger] = Mathf.Lerp(_spreadSmoothed[finger], _spread[finger], alpha);
                for (int joint = 0; joint < 3; joint++)
                {
                    var n = finger * 3 + joint;
                    _smoothed[n] = Mathf.Lerp(_smoothed[n], _angles[n], alpha);
                    if (n < joints.Length && joints[n] != null)
                    {
                        var curl = Quaternion.AngleAxis(_smoothed[n] * curlSign, curlAxis.normalized);
                        var splay = joint == 0
                            ? Quaternion.AngleAxis(_spreadSmoothed[finger] * (finger == 4 ? .6f : 1f), spreadAxis.normalized)
                            : Quaternion.identity;
                        joints[n].localRotation = _rest[n] * splay * curl;
                    }
                    total += _smoothed[n] / 90f;
                }
            }
            Closure = total / 15f;
        }
    }
}
