using System;

namespace GripRehab
{
    // Count only qualified input time. A brief gesture flicker pauses rather
    // than discarding five seconds of effort; it can never activate a control.
    public sealed class RehabDwellGate
    {
        int targetId;
        float elapsed, interruption;
        bool armed;
        public float Elapsed => elapsed;
        public bool Latched { get; private set; }
        public void Reset() { targetId = 0; elapsed = interruption = 0; armed = Latched = false; }
        public bool Step(int id, bool eligible, float dt, float duration, float grace)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration) || float.IsNaN(grace) || float.IsInfinity(grace))
            { Reset(); return false; }
            dt = float.IsNaN(dt) || float.IsInfinity(dt) ? 0 : Math.Min(.1f, Math.Max(0, dt));
            if (id == 0) { Reset(); return false; }
            if (id != targetId) { Reset(); targetId = id; }
            if (!eligible)
            {
                interruption += dt;
                if (interruption > Math.Min(.3f, Math.Max(0, grace))) { elapsed = 0; armed = Latched = false; }
                return false;
            }
            interruption = 0;
            if (Latched) return false;
            if (!armed) { armed = true; return false; }
            elapsed += dt;
            if (elapsed + .00001f < Math.Max(.5f, duration)) return false;
            Latched = true;
            return true;
        }
    }
}
