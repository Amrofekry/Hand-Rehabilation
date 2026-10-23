using System;

namespace GripRehab.Product
{
    internal static class CatalogValidation
    {
        public static void Credential(PasswordVerifier password)
        {
            try
            {
                if (password.iterations < 100000 || password.iterations > 1000000 ||
                    Convert.FromBase64String(password.saltBase64).Length != 16 ||
                    Convert.FromBase64String(password.hashBase64).Length != 32) throw new ArgumentException("Invalid password verifier.");
            }
            catch (FormatException) { throw new ArgumentException("Invalid password verifier."); }
        }
        static bool Positive(double value) => TrialSample.Finite(value) && value >= 0;
        static bool Ratio(double value) => Positive(value) && value <= 1;
        static bool Unit(InputUnit unit) => unit == InputUnit.Newtons || unit == InputUnit.ClosureRatio;
        public static void Baseline(CapabilityProfile b)
        {
            if (!b.consent || b.acceptedSamples < 2 || b.rejectedSamples < 0 || !Positive(b.validSeconds) || b.validSeconds <= 0 ||
                string.IsNullOrWhiteSpace(b.source) || string.IsNullOrWhiteSpace(b.settingsVersion) || !Unit(b.unit) ||
                b.hasForce != (b.unit == InputUnit.Newtons) || !Positive(b.peakNewtons) || !Positive(b.meanNewtons) ||
                !Positive(b.forceCv) || !Ratio(b.meanClosure) || !Positive(b.closureStability) ||
                !Positive(b.motionSpeed) || !Positive(b.motionJitter) || (!b.hasForce && (b.peakNewtons != 0 || b.meanNewtons != 0)))
                throw new ArgumentException("Invalid baseline values or force provenance.");
            if(b.baselineProtocol!=null && (!Positive(b.mvcTrial1)||!Positive(b.mvcTrial2)||!Positive(b.mvcTrial3)||!Positive(b.mvcNewtons)||
                !Positive(b.enduranceSeconds)||b.enduranceSeconds>b.validSeconds||!Ratio(b.closureRange)||!Positive(b.romDegrees)||b.romDegrees>180||
                (!b.hasForce&&(b.mvcNewtons!=0||b.enduranceSeconds!=0))))throw new ArgumentException("Invalid staged baseline values.");
        }
        public static void Session(SessionRecord s)
        {
            if (s.task < 0 || s.task > 1 || !Enum.IsDefined(typeof(RecordState), s.state) || s.score < 0 || s.score > 100 ||
                !Positive(s.durationSeconds) || !Positive(s.validSeconds) || s.validSeconds > s.durationSeconds + .2 ||
                !Positive(s.peakNewtons) || (!s.hasForce && s.peakNewtons != 0) || !Ratio(s.meanClosure) ||
                s.metrics == null || s.metrics.Count > 1800 || s.policyEvents == null || s.policyEvents.Count > 500)
                throw new ArgumentException("Invalid or excessive session metrics.");
            double prior = -1;
            foreach (var m in s.metrics)
            {
                if (m == null || !Positive(m.seconds) || m.seconds < prior || m.seconds > s.durationSeconds + .2 || !Unit(m.unit) ||
                    !Positive(m.value) || (m.unit == InputUnit.ClosureRatio && !Ratio(m.value)) || !Ratio(m.handClosure) ||
                    !Positive(m.motionSpeed) || !Positive(m.motionJitter) || string.IsNullOrWhiteSpace(m.source) || !Positive(m.romDegrees)||m.romDegrees>180||
                    !Ratio(m.emotionConfidence)||!Positive(m.heartRateBpm)||m.heartRateBpm>240||!Positive(m.hrvRmssdMs)||
                    !Ratio(m.pulseQuality)||!Ratio(m.modelProbability)||!Positive(m.measurementAgeMs)||m.emotionClass < -1||m.emotionClass>6)
                    throw new ArgumentException("Invalid session sample.");
                prior = m.seconds;
            }
            foreach (var e in s.policyEvents)
                if (e == null || !Positive(e.seconds) || e.seconds > s.durationSeconds + .2 || e.level < 1 || e.level > 100 ||
                    e.signs < 0 || e.signs > 15 || string.IsNullOrWhiteSpace(e.settingsVersion)) throw new ArgumentException("Invalid guidance event.");
        }
    }
}
