using System;

namespace GripRehab.Product
{
    /// <summary>Receiver-monotonic input; closure is dimensionless and never converted to N.</summary>
    public struct TrialSample
    {
        public double now, capturedAt;
        public long sequence;
        public string source, shape, calibrationRevision;
        public InputUnit unit;
        public float value, closure, speed, jitter;
        public bool valid, handValid, synthetic;
        public float romDegrees; public bool romValid;
        public bool Fresh => valid && Finite(now) && Finite(capturedAt) && now >= capturedAt &&
            now - capturedAt < .5 && Finite(value) && value >= 0 &&
            (unit == InputUnit.Newtons || (unit == InputUnit.ClosureRatio && value <= 1));
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public bool MotionValid => handValid && Finite(closure) && closure >= 0 && closure <= 1 &&
            Finite(speed) && Finite(jitter) && speed >= 0 && jitter >= 0;
        // A receiver's missing/default sample is not evidence of a changed
        // device or calibration. It pauses input but must not latch that fault.
        public bool ChangesIdentity(TrialSample previous) => Fresh &&
            (previous.source != source || previous.unit != unit || previous.shape != shape ||
             previous.calibrationRevision != calibrationRevision || previous.synthetic != synthetic);
    }
    /// <summary>Consented, source-specific research baseline. No inferred MVC from camera data.</summary>
    public sealed class CapabilityTrial
    {
        readonly float duration;
        readonly CapabilityProfile result;
        long previousSequence = long.MinValue;
        double sum, squares, closureSum, closureSquares, speedSum, jitterSum;
        int motionSamples;
        bool initialized;
        public string Problem { get; private set; }
        public bool Complete => Problem == null && result.validSeconds >= duration;
        public float Progress => Math.Min(1, result.validSeconds / duration);
        public CapabilityTrial(string accountId, string profileId, bool consent, float durationSeconds = 12)
        {
            if (!consent) throw new InvalidOperationException("Baseline consent is required.");
            if (!TrialSample.Finite(durationSeconds) || durationSeconds < 1 || durationSeconds > 120) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            duration = durationSeconds;
            result = new CapabilityProfile { accountId = accountId, profileId = profileId, consent = true };
        }
        public void Step(TrialSample input, float dt)
        {
            if (Complete || Problem != null) return;
            if (!input.Fresh || input.sequence <= previousSequence || !TrialSample.Finite(dt) || dt <= 0)
            { result.rejectedSamples++; return; }
            if (initialized && (input.source != result.source || input.shape != result.shape ||
                input.calibrationRevision != result.calibrationRevision || input.unit != result.unit || input.synthetic != result.synthetic))
            { Problem = "Input or calibration changed. Restart the baseline."; return; }
            if (!initialized)
            {
                result.source = input.source; result.shape = input.shape; result.unit = input.unit;
                result.calibrationRevision = input.calibrationRevision; result.synthetic = input.synthetic; initialized = true;
            }
            previousSequence = input.sequence; result.acceptedSamples++;
            result.validSeconds += Math.Min(.1f, dt);
            if (input.unit == InputUnit.Newtons)
            {
                result.hasForce = true; result.peakNewtons = Math.Max(result.peakNewtons, input.value);
                sum += input.value; squares += input.value * input.value;
            }
            if (input.MotionValid)
            {
                motionSamples++; closureSum += input.closure; closureSquares += input.closure * input.closure;
                speedSum += input.speed; jitterSum += input.jitter; result.hasMotion = true;
            }
        }
        public CapabilityProfile Finish()
        {
            if (!Complete) throw new InvalidOperationException(Problem ?? "Keep collecting valid baseline samples.");
            return Snapshot();
        }
        public CapabilityProfile Snapshot()
        {
            if (result.acceptedSamples < 2) throw new InvalidOperationException("Insufficient valid samples.");
            if (result.hasForce)
            {
                result.meanNewtons = (float)(sum / result.acceptedSamples);
                result.forceCv = (float)(Math.Sqrt(Math.Max(0, squares / result.acceptedSamples - result.meanNewtons * result.meanNewtons)) /
                    Math.Max(.001, result.meanNewtons));
            }
            if (motionSamples > 0)
            {
                result.meanClosure = (float)(closureSum / motionSamples);
                result.closureStability = (float)Math.Sqrt(Math.Max(0, closureSquares / motionSamples - result.meanClosure * result.meanClosure));
                result.motionSpeed = (float)(speedSum / motionSamples); result.motionJitter = (float)(jitterSum / motionSamples);
            }
            return result;
        }
    }
    /// <summary>Fixed-capacity temporal statistics; Add/Read allocate no managed objects.</summary>
    public sealed class FeatureWindow
    {
        readonly TrialSample[] samples;
        int next, count;
        TrialSample previous;
        bool initialized;
        public FeatureWindow(int capacity = 100)
        { if (capacity < 2 || capacity > 1000) throw new ArgumentOutOfRangeException(nameof(capacity)); samples = new TrialSample[capacity]; }
        public void Clear() { next = count = 0; initialized = false; }
        public void Add(TrialSample sample)
        {
            if (!sample.Fresh) return;
            if (initialized && (previous.source != sample.source || previous.unit != sample.unit ||
                previous.shape != sample.shape || previous.calibrationRevision != sample.calibrationRevision ||
                previous.synthetic != sample.synthetic)) Clear();
            if (initialized && (sample.sequence <= previous.sequence || sample.capturedAt < previous.capturedAt)) return;
            previous = sample; initialized = true;
            samples[next] = sample; next = (next + 1) % samples.Length; count = Math.Min(count + 1, samples.Length);
        }
        public WindowFeatures Read(double now, double seconds = 10)
        {
            var result = new WindowFeatures();
            if (!initialized || now < previous.capturedAt || now - previous.capturedAt >= .5) return result;
            double total = 0, square = 0, jitter = 0; int force = 0, hand = 0;
            if (!TrialSample.Finite(now) || !TrialSample.Finite(seconds) || seconds <= 0) return result;
            for (int i = 0; i < count; i++)
            {
                var s = samples[i]; if (now < s.capturedAt || now - s.capturedAt > seconds) continue;
                result.count++;
                if (s.unit == InputUnit.Newtons) { total += s.value; square += s.value * s.value; force++; }
                if (s.MotionValid) { jitter += s.jitter; hand++; }
            }
            result.forceValid = force >= 10; result.motionValid = hand >= 10;
            if (force > 0) { result.forceMean = (float)(total / force); result.forceCv = (float)(Math.Sqrt(Math.Max(0, square / force - result.forceMean * result.forceMean)) / Math.Max(.001, result.forceMean)); }
            if (hand > 0) result.jitter = (float)(jitter / hand);
            return result;
        }
    }
    public struct WindowFeatures { public int count; public bool forceValid, motionValid; public float forceMean, forceCv, jitter; }
}
