using System;
using System.Collections.Generic;

namespace GripRehab
{
    [Serializable]
    public sealed class ForceBridgeFrame
    {
        public int schemaVersion;
        public string kind;
        public string source;
        public string sessionId;
        public string connectionId;
        public string reason;
        public long calibrationRevision;
        public long calibrationCrc;
        public int shapeId;
        public long sequence;
        public long sampleMs;
        public int rawCounts;
        public int forceMilliNewtons;
        public int flags;
        public int ageMs;
    }

    // A connection/calibration change ends the current force session. Unity never
    // substitutes keyboard force when this source becomes invalid.
    public sealed class LiveForceState : IForceSource
    {
        public ForceReading Latest { get; private set; }
        public string Status { get; private set; } = "Waiting for BLE bridge";
        public GripShape Shape { get; private set; }
        public int Generation { get; private set; }
        public int SampleCount { get; private set; }
        public long SampleMs { get; private set; }
        public bool IsSynthetic { get; private set; }
        public long CalibrationRevision => _revision;
        public int LastAgeMs { get; private set; }

        private string _connectionId;
        private string _sessionId;
        private readonly Queue<string> _retiredConnections = new Queue<string>();
        private readonly HashSet<string> _retiredLookup = new HashSet<string>();
        private long _revision;
        private long _crc;
        private long _lastSequence = -1;
        private long _lastSampleMs = -1;
        private double _expiresAt;
        private double _lastPacketAt = -1;

        public void Invalidate(string reason)
        {
            Latest = new ForceReading(0, 0, false, IsSynthetic);
            Status = reason;
            _expiresAt = 0;
        }

        public bool Accept(ForceBridgeFrame frame, double now, bool allowSynthetic = false, double queueDelaySeconds = 0)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || double.IsNaN(queueDelaySeconds) ||
                double.IsInfinity(queueDelaySeconds) || queueDelaySeconds < 0 ||
                frame == null || frame.schemaVersion != 1 ||
                (frame.source != "hardware" && !(allowSynthetic && frame.source == "synthetic")) ||
                string.IsNullOrEmpty(frame.connectionId) || string.IsNullOrEmpty(frame.sessionId) ||
                _retiredLookup.Contains(frame.connectionId))
                return false;
            if (frame.kind == "status")
            {
                if (_connectionId != frame.connectionId || _sessionId != frame.sessionId)
                    ChangeConnection(frame.connectionId, frame.sessionId);
                IsSynthetic = frame.source == "synthetic";
                Invalidate(string.IsNullOrEmpty(frame.reason) ? "BLE unavailable" : frame.reason);
                Generation++;
                return true;
            }
            if (frame.kind != "sample" || frame.shapeId < 1 || frame.shapeId > 3 ||
                frame.calibrationRevision <= 0 || frame.calibrationCrc < 0 || frame.calibrationCrc > uint.MaxValue ||
                frame.sequence < 0 || frame.sequence > uint.MaxValue ||
                frame.sampleMs < 0 || frame.sampleMs > uint.MaxValue ||
                frame.rawCounts < -8388608 || frame.rawCounts > 8388607 ||
                frame.forceMilliNewtons < 0 || frame.forceMilliNewtons > 980665 ||
                frame.flags != 3 || frame.ageMs < 0 || frame.ageMs >= 500 || frame.ageMs + queueDelaySeconds * 1000 >= 500)
                return false;

            var changed = _connectionId != frame.connectionId || _sessionId != frame.sessionId ||
                          _revision != frame.calibrationRevision ||
                          _crc != frame.calibrationCrc || (int)Shape != frame.shapeId;
            if (changed)
            {
                if (_connectionId != frame.connectionId || _sessionId != frame.sessionId)
                    ChangeConnection(frame.connectionId, frame.sessionId);
                _revision = frame.calibrationRevision;
                _crc = frame.calibrationCrc;
                Shape = (GripShape)frame.shapeId;
                _lastSequence = -1;
                _lastSampleMs = -1;
                Generation++;
                Invalidate("BLE connection/calibration changed; start a new session");
            }
            if (_lastSequence >= 0)
            {
                var advance = (uint)((uint)frame.sequence - (uint)_lastSequence);
                if (advance == 0 || advance >= 0x80000000u) return false;
            }
            if (_lastSampleMs >= 0)
            {
                var advance = (uint)((uint)frame.sampleMs - (uint)_lastSampleMs);
                if (advance == 0 || advance >= 0x80000000u) return false;
            }
            if (_lastPacketAt >= 0 && now < _lastPacketAt) return false;
            if (!changed && _lastPacketAt >= 0 && now - _lastPacketAt >= .5)
            {
                Generation++;
                Invalidate("BLE resumed after a stale gap; start a new session");
            }
            _lastSequence = frame.sequence;
            _lastSampleMs = frame.sampleMs;
            _lastPacketAt = now;
            _expiresAt = now + (500 - frame.ageMs) / 1000.0 - queueDelaySeconds;
            SampleMs = frame.sampleMs;
            LastAgeMs = frame.ageMs + (int)(queueDelaySeconds * 1000);
            SampleCount++;
            IsSynthetic = frame.source == "synthetic";
            Latest = new ForceReading(frame.forceMilliNewtons / 1000f, now - frame.ageMs / 1000.0 - queueDelaySeconds, true, IsSynthetic);
            Status = IsSynthetic ? "Synthetic BLE replay" : "BLE calibrated / live";
            return true;
        }

        private void ChangeConnection(string connectionId, string sessionId)
        {
            if (_connectionId != null && _connectionId != connectionId)
            {
                _retiredConnections.Enqueue(_connectionId);
                _retiredLookup.Add(_connectionId);
                if (_retiredConnections.Count > 16)
                    _retiredLookup.Remove(_retiredConnections.Dequeue());
            }
            _connectionId = connectionId;
            _sessionId = sessionId;
            _lastSequence = -1;
            _lastSampleMs = -1;
            _lastPacketAt = -1;
        }

        public void Tick(double now)
        {
            if (Latest.Valid && now >= _expiresAt) Invalidate("BLE sample stale (>500 ms)");
        }
    }
}
