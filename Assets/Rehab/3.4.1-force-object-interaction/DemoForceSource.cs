using UnityEngine;

namespace GripRehab
{
    // Explicitly simulated source until Phase 6 BLE hardware integration is available.
    public sealed class DemoForceSource : IForceSource, IRehabForceSource
    {
        public float SetpointNewtons { get; private set; } = 18f;
        public ForceReading Latest { get; private set; }
        public GripShape Shape { get; set; } = GripShape.Sphere;
        public ForceSourceKind Kind => ForceSourceKind.KeyboardDemo;
        private uint _sequence;

        public bool TryGetSample(double now, out ForceSample sample)
        {
            sample = new ForceSample(Kind, Latest.Timestamp, Latest.Timestamp,
                _sequence, Shape, 0, Latest.Newtons, Latest.Valid, true);
            return sample.IsFresh(now);
        }

        public void Step(double now, float deltaSeconds)
        {
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
                SetpointNewtons += deltaSeconds * 16f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
                SetpointNewtons -= deltaSeconds * 16f;
            SetpointNewtons = Mathf.Clamp(SetpointNewtons, 0, 50);
            var squeezing = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return);
            Latest = new ForceReading(squeezing ? SetpointNewtons : 0, now, true, true);
            _sequence++;
        }

        public void SetSetpoint(float newtons)
        {
            SetpointNewtons = Mathf.Clamp(newtons, 0, 50);
        }
    }
}
