using System;

namespace GripRehab
{
    /// <summary>
    /// Hand look and hand walking for camera-hand mode (2026-10-28, your design). Unity-free so it is tested offline.
    ///
    /// Look: the centre of the camera image is a free work zone (reaching and grabbing do not turn the view). When the palm
    /// moves into the outer band, the view turns that way; the further out, the faster, up to a cap. Turning eases in and
    /// out (no jerks) so it is comfortable to look at.
    /// Walk: pointing with the index finger, held for a moment, walks forward where you look; any other hand shape stops.
    /// A fist stays for grabbing, so the two never mix.
    /// Lost or doubtful tracking, a menu or pause stops everything at once. Mouse and keyboard keep working alongside.
    /// Image coordinates are MediaPipe's normalised ones (x right, y down) from the mirrored (selfie) camera, so moving your
    /// hand to your right moves it right in the image and turns the view right.
    /// </summary>
    public sealed class HandLocomotionPolicy
    {
        // Work zone half-sizes (fraction of the image from its centre) and the outer edge where speed is full.
        public float deadZoneX = .30f, deadZoneY = .26f, fullAt = .46f; // 2026-10-29: wider work zone, so reaching for an object does not turn the view
        public float maxYawDegPerSec = 75f, maxPitchDegPerSec = 45f;
        /// <summary>Seconds for the turn rate to follow the hand (eases in and out).</summary>
        public float turnSmoothing = .18f;
        /// <summary>Seconds the point must be held before walking, and the walking speed ramp (fraction per second).</summary>
        public float pointHoldSeconds = .25f, walkRamp = 2.5f;
        public float minConfidence = .5f;

        public float YawRate { get; private set; }
        public float PitchRate { get; private set; }
        /// <summary>0..1 share of walking speed.</summary>
        public float Walk { get; private set; }
        public bool Walking => Walk > 0.001f;
        float pointHeld;

        public void Reset() { YawRate = PitchRate = Walk = 0; pointHeld = 0; }

        /// <summary>Target turn rate for one axis: 0 inside the work zone, rising smoothly to max at fullAt.</summary>
        public static float Rate(float offset, float deadZone, float fullAt, float max)
        {
            float a = Math.Abs(offset);
            if (!(a > deadZone) || !(fullAt > deadZone)) return 0;
            float t = Math.Min(1f, (a - deadZone) / (fullAt - deadZone));
            t = t * t * (3 - 2 * t); // smoothstep: gentle at the zone edge
            return Math.Sign(offset) * t * max;
        }

        /// <summary>
        /// One frame. palmX/palmY: palm centre in normalised image coordinates; pointing: the hand shape is a point;
        /// usable: tracking is valid, confident and gameplay is running. Returns nothing; read YawRate, PitchRate, Walk.
        /// YawRate &gt; 0 turns right, PitchRate &gt; 0 looks down.
        /// </summary>
        public void Step(float palmX, float palmY, bool pointing, bool usable, float confidence, float dt)
        {
            if (!(dt > 0) || dt > .5f) dt = 0;
            bool ok = usable && confidence >= minConfidence && IsFinite(palmX) && IsFinite(palmY) && palmX >= -.1f && palmX <= 1.1f && palmY >= -.1f && palmY <= 1.1f;
            if (!ok) { Reset(); return; } // stop at once: never keep turning or walking on stale data
            float yaw = Rate(palmX - .5f, deadZoneX, fullAt, maxYawDegPerSec);
            float pitch = Rate(palmY - .5f, deadZoneY, fullAt, maxPitchDegPerSec);
            float k = turnSmoothing > 0 ? 1 - (float)Math.Exp(-dt / turnSmoothing) : 1;
            YawRate += (yaw - YawRate) * k; PitchRate += (pitch - PitchRate) * k;
            if (Math.Abs(YawRate) < .05f && yaw == 0) YawRate = 0;
            if (Math.Abs(PitchRate) < .05f && pitch == 0) PitchRate = 0;
            pointHeld = pointing ? pointHeld + dt : 0;
            float target = pointHeld >= pointHoldSeconds ? 1 : 0;
            // Starting ramps up; stopping is twice as quick, so letting go of the point stops promptly.
            float step = walkRamp * dt * (target < Walk ? 2 : 1);
            Walk = target > Walk ? Math.Min(target, Walk + step) : Math.Max(target, Walk - step);
        }

        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }

    /// <summary>
    /// Camera-hand actions that replace keys (2026-10-28): a fist held on an object grabs it (was G/E); a pinch held opens the
    /// options panel (was TAB); twisting the hand like a dial while holding turns the placement (was , and .). Each fires
    /// once and needs the hand shape to change before it fires again. Unity-free; tested offline.
    /// </summary>
    public sealed class HandActionPolicy
    {
        public float grabHoldSeconds = .35f, optionsHoldSeconds = .8f;
        /// <summary>Twist (degrees) ignored around the angle the object was grabbed at; then placement follows the twist, in 5° steps.</summary>
        public float twistDeadZone = 10f, twistStep = 5f, twistMax = 180f;
        float fistHeld, pinchHeld; bool grabArmed = true, optionsArmed = true, twisting; float twistBase;
        public bool GrabRequested { get; private set; }
        public bool OptionsRequested { get; private set; }
        /// <summary>Placement turn from the twist, degrees; NaN when not holding.</summary>
        public float PlacementYaw { get; private set; } = float.NaN;

        public void Reset() { fistHeld = pinchHeld = 0; grabArmed = optionsArmed = true; twisting = false; GrabRequested = OptionsRequested = false; PlacementYaw = float.NaN; }

        /// <summary>Roll of the hand in the image, degrees: 0 with the fingers up, positive turning clockwise (to your right).</summary>
        public static float Roll(float wristX, float wristY, float middleBaseX, float middleBaseY) =>
            (float)(Math.Atan2(middleBaseX - wristX, wristY - middleBaseY) * 180 / Math.PI);

        public void Step(bool fist, bool pinch, bool holding, float rollDegrees, bool usable, float dt)
        {
            GrabRequested = OptionsRequested = false;
            if (!usable || !(dt >= 0) || dt > .5f) { fistHeld = pinchHeld = 0; twisting = false; PlacementYaw = float.NaN; if (!usable) grabArmed = optionsArmed = true; return; }
            if (!fist) grabArmed = true;
            fistHeld = fist && !holding ? fistHeld + dt : 0;
            if (fistHeld >= grabHoldSeconds && grabArmed) { GrabRequested = true; grabArmed = false; fistHeld = 0; }
            if (!pinch) optionsArmed = true;
            pinchHeld = pinch ? pinchHeld + dt : 0;
            if (pinchHeld >= optionsHoldSeconds && optionsArmed) { OptionsRequested = true; optionsArmed = false; pinchHeld = 0; }
            bool rollOk = !float.IsNaN(rollDegrees) && !float.IsInfinity(rollDegrees);
            if (!holding || !rollOk) { twisting = false; PlacementYaw = float.NaN; return; }
            if (!twisting) { twisting = true; twistBase = rollDegrees; }
            float d = Wrap(rollDegrees - twistBase);
            float beyond = Math.Abs(d) <= twistDeadZone ? 0 : Math.Sign(d) * (Math.Abs(d) - twistDeadZone);
            float yaw = Math.Max(-twistMax, Math.Min(twistMax, beyond * 2f)); // a 45° twist turns about 70°: wrists rotate less than objects need
            PlacementYaw = (float)Math.Round(yaw / twistStep) * twistStep;
        }

        static float Wrap(float a) { a %= 360; if (a > 180) a -= 360; if (a < -180) a += 360; return a; }
    }

    /// <summary>
    /// When a camera-held object is released (2026-10-29). Old rule: release as soon as the score dips under the grab
    /// threshold, so one noisy frame dropped the object, and any tracking gap released at once. New rule: hysteresis (hold
    /// while the closed-hand gate stays closed; it only opens after the score stays at or below the open threshold for its
    /// dwell) plus a short grace through tracking gaps.
    /// </summary>
    public static class GraspHoldRule
    {
        public const float LossGraceSeconds = .6f;
        public static bool OldShouldRelease(bool gateClosed, float score, float closeAt, bool trackingValid) =>
            !trackingValid || !(gateClosed && score >= closeAt);
        public static bool ShouldRelease(bool gateClosed, bool trackingValid, float secondsSinceValid, bool paused) =>
            trackingValid ? !gateClosed : paused || secondsSinceValid > LossGraceSeconds;
    }

    public enum HandInteraction { Idle, Tracking, ReadyToGrab, Holding, Releasing, TrackingLost }

    /// <summary>
    /// Explicit camera-hand interaction state (Idle → Tracking → ReadyToGrab → Holding → Releasing, and TrackingLost).
    /// It reads the same facts ApartmentExperience acts on (tracking, closed-hand gate, holding) and reports transitions once,
    /// for the HUD, the session counts and the tests. Unity-free.
    /// </summary>
    public sealed class HandInteractionMachine
    {
        public HandInteraction State { get; private set; } = HandInteraction.Idle;
        public int Grabs { get; private set; }
        public int Releases { get; private set; }
        public int Losses { get; private set; }
        public bool JustGrabbed { get; private set; }
        public bool JustReleased { get; private set; }

        public void Reset() { State = HandInteraction.Idle; Grabs = Releases = Losses = 0; JustGrabbed = JustReleased = false; }

        public HandInteraction Step(bool tracked, bool gateClosed, bool holding)
        {
            var before = State;
            JustGrabbed = JustReleased = false;
            if (holding)
                State = tracked ? HandInteraction.Holding : HandInteraction.TrackingLost;
            else if (before == HandInteraction.Holding || before == HandInteraction.TrackingLost)
                State = HandInteraction.Releasing;
            else if (!tracked)
                State = HandInteraction.Idle;
            else
                State = gateClosed ? HandInteraction.ReadyToGrab : HandInteraction.Tracking;
            if (holding && before != HandInteraction.Holding && before != HandInteraction.TrackingLost) { Grabs++; JustGrabbed = true; }
            if (State == HandInteraction.Releasing) { Releases++; JustReleased = true; }
            if (State == HandInteraction.TrackingLost && before != HandInteraction.TrackingLost) Losses++;
            return State;
        }
    }

    /// <summary>
    /// Player-facing camera hand settings, three steps each (Settings page; no code changes needed).
    /// Sensitivity = how far the virtual hand reaches; steadiness = One Euro cutoff (steadier = slightly more lag).
    /// </summary>
    public static class HandControlProfile
    {
        public static readonly string[] SensitivityNames = { "LOW", "NORMAL", "HIGH" };
        public static readonly string[] SteadinessNames = { "RESPONSIVE", "BALANCED", "STEADY" };
        public static float Reach(int sensitivity) => new[] { .24f, .30f, .36f }[Clamp(sensitivity)];
        public static float TravelScale(int sensitivity) => new[] { .85f, 1f, 1.2f }[Clamp(sensitivity)];
        public static float PositionCutoff(int steadiness) => new[] { 2.0f, 1.2f, .7f }[Clamp(steadiness)];
        public static float DepthCutoff(int steadiness) => new[] { .6f, .35f, .2f }[Clamp(steadiness)];
        static int Clamp(int v) => v < 0 ? 0 : v > 2 ? 2 : v;
    }
}
