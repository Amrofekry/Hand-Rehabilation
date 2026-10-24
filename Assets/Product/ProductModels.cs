using System;
using System.Collections.Generic;

namespace GripRehab.Product
{
    /// <summary>A role is assigned to an account, never granted by a UI toggle.</summary>
    public enum LocalRole { Patient, Doctor }
    public enum InputUnit { Newtons, ClosureRatio }
    public enum RecordState { InProgress, Completed, Interrupted, SafetyStopped }
    [Serializable] public sealed class PasswordVerifier
    { public int iterations; public string saltBase64, hashBase64; }
    [Serializable] public sealed class LocalAccount
    {
        public string id, username, displayName, profileId, createdUtc;
        public LocalRole role;
        public PasswordVerifier password;
    }
    [Serializable] public sealed class CapabilityProfile
    {
        public string id, accountId, profileId, createdUtc, source, shape, calibrationRevision;
        public string settingsVersion = "research-demo-2";
        public InputUnit unit;
        public bool synthetic, hasForce, hasMotion, consent;
        public float peakNewtons, meanNewtons, forceCv, validSeconds;
        public float meanClosure, closureStability, motionSpeed, motionJitter;
        public int acceptedSamples, rejectedSamples;
        public float mvcTrial1, mvcTrial2, mvcTrial3, mvcNewtons, enduranceSeconds, closureRange, romDegrees;
        public bool hasRom, hasStability; public string baselineProtocol;
    }
    [Serializable] public sealed class MetricRecord
    {
        public double seconds;
        public float value, handClosure, motionSpeed, motionJitter;
        public InputUnit unit;
        public bool valid, synthetic;
        public string source, shape, calibrationRevision;
        public float romDegrees, emotionConfidence, heartRateBpm, hrvRmssdMs, pulseQuality, modelProbability;
        public float measurementAgeMs;
        public long sequence;
        public bool romValid, faceConsented, faceValid, pulseConsented, pulseValid, hrvValid, modelValid;
        public int emotionClass = -1;
        public string modelProvenance;
    }
    [Serializable] public sealed class OutcomeMetric
    { public string key, label, unit; public float value; }
    [Serializable] public sealed class PolicyEvent
    { public double seconds; public string status, reason, settingsVersion; public int level, signs; }
    [Serializable] public sealed class SessionRecord
    {
        public string id, accountId, profileId, startedUtc, endedUtc, baselineId;
        public int module, task, score;
        // validSeconds = fresh input was available; activeSeconds = the player was gripping (audit P-007).
        public float durationSeconds, validSeconds, activeSeconds, peakNewtons, meanClosure;
        public bool synthetic, hasForce;
        public RecordState state;
        public List<MetricRecord> metrics = new List<MetricRecord>();
        public List<PolicyEvent> policyEvents = new List<PolicyEvent>();
        // Stage 3: difficulty level played (0 = not recorded by older versions) and named
        // per-exercise outcomes such as reaction time or accuracy. Game metrics, not clinical ones.
        public int level = 1;
        /// <summary>Stage 5.2: level the adaptive loop ended on (0 = it never moved).</summary>
        public int adaptedLevel;
        public List<OutcomeMetric> outcomes = new List<OutcomeMetric>();
        public const float ActiveNewtons = 1f, ActiveClosure = .6f;
        public static bool IsActiveGrip(TrialSample input) => input.Fresh &&
            (input.unit == InputUnit.Newtons ? input.value >= ActiveNewtons : input.value >= ActiveClosure);
    }
    [Serializable] public sealed class ClinicalNote
    { public string id, doctorId, patientId, createdUtc, text, supersedesId; }
    [Serializable] public sealed class LocalProductCatalog
    {
        public int schemaVersion = 1;
        public List<LocalAccount> accounts = new List<LocalAccount>();
        public List<CapabilityProfile> baselines = new List<CapabilityProfile>();
        public List<SessionRecord> sessions = new List<SessionRecord>();
        public List<ClinicalNote> notes = new List<ClinicalNote>();
    }
    /// <summary>Encrypted serialization is supplied by an adapter, never by UI.</summary>
    public interface IProductPersistence
    { LocalProductCatalog Load(); LocalProductCatalog Clone(LocalProductCatalog source); void Save(LocalProductCatalog catalog); }
    public interface IProductClock { double Seconds { get; } string Utc { get; } }
    public sealed class SystemProductClock : IProductClock
    {
        readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        public double Seconds => clock.Elapsed.TotalSeconds;
        public string Utc => DateTime.UtcNow.ToString("O");
    }
}
