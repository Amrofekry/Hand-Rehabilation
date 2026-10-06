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
    }
    [Serializable] public sealed class MetricRecord
    {
        public double seconds;
        public float value, handClosure, motionSpeed, motionJitter;
        public InputUnit unit;
        public bool valid, synthetic;
        public string source, shape, calibrationRevision;
    }
    [Serializable] public sealed class PolicyEvent
    { public double seconds; public string status, reason, settingsVersion; public int level, signs; }
    [Serializable] public sealed class SessionRecord
    {
        public string id, accountId, profileId, startedUtc, endedUtc, baselineId;
        public int module, task, score;
        public float durationSeconds, validSeconds, peakNewtons, meanClosure;
        public bool synthetic, hasForce;
        public RecordState state;
        public List<MetricRecord> metrics = new List<MetricRecord>();
        public List<PolicyEvent> policyEvents = new List<PolicyEvent>();
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
