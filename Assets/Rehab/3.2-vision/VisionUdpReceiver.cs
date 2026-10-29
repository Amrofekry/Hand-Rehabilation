using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace GripRehab
{
    // Two independent 21-point streams on loopback. A legacy packet without handedness is Right.
    // Receive age includes time spent queued for Unity's main thread.
    [DefaultExecutionOrder(-30)] // frames land before ApartmentExperience reads them in the same frame (2026-10-29)
    public sealed class VisionUdpReceiver : MonoBehaviour, IHandTracker
    {
        public int port = 27817;
        public LandmarkFrame LatestRight { get; private set; }
        public LandmarkFrame LatestLeft { get; private set; }
        public bool RightValid => Valid(LatestRight, _rightReceivedAt);
        public bool LeftValid => Valid(LatestLeft, _leftReceivedAt);
        public double RightAgeSeconds => RightValid ? Time.realtimeSinceStartupAsDouble - _rightReceivedAt : double.PositiveInfinity;
        public double LeftAgeSeconds => LeftValid ? Time.realtimeSinceStartupAsDouble - _leftReceivedAt : double.PositiveInfinity;
        // Backward compatibility for the other training modules.
        public LandmarkFrame Latest => RightValid ? LatestRight : LeftValid ? LatestLeft : null;
        public bool TrackingValid => RightValid || LeftValid;
        public int ReceivedPackets { get; private set; }
        public int AcceptedPackets { get; private set; }
        public int RejectedPackets { get; private set; }
        public string LastRejection { get; private set; } = "";
        /// <summary>Stage 7.2: why the loopback port could not open (empty when it is listening).</summary>
        public string PortProblem { get; private set; } = "";
        void Reject(string reason) { RejectedPackets++; LastRejection = reason; }

        private readonly ConcurrentQueue<ReceivedDatagram> _pending = new ConcurrentQueue<ReceivedDatagram>();
        private UdpClient _socket;
        private Thread _thread;
        private volatile bool _running;
        private double _rightReceivedAt, _leftReceivedAt;
        private double _rightArrivedAt, _leftArrivedAt;
        private double _rightCapture, _leftCapture;
        private long _rightSequence, _leftSequence;

        public bool TryGetHand(HandSide side, double now, out HandSample sample)
        {
            var left = side == HandSide.Left;
            var frame = left ? LatestLeft : LatestRight;
            var capturedAt = left ? _leftReceivedAt : _rightReceivedAt;
            var arrivedAt = left ? _leftArrivedAt : _rightArrivedAt;
            sample = new HandSample(side, frame?.landmarks, capturedAt, arrivedAt,
                frame?.sequence ?? 0, frame?.confidence ?? 0,
                HandKinematics.IsUsable(frame));
            return sample.IsFresh(now);
        }

        private static bool Valid(LandmarkFrame frame, double at) =>
            frame != null && HandKinematics.IsUsable(frame) &&
            Time.realtimeSinceStartupAsDouble - at < 0.5;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            ResetFrames();
            while (_pending.TryDequeue(out _)) { }
            try
            {
                _socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                _socket.Client.ReceiveTimeout = 200;
                _running = true; PortProblem = "";
                _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "Rehab bimanual vision loopback" };
                _thread.Start();
            }
            catch (SocketException error)
            {
                PortProblem = error.SocketErrorCode == SocketError.AddressAlreadyInUse ?
                    "Hand-tracking port " + port + " is already in use. Close any other GripRehab window and restart." :
                    "Hand-tracking port " + port + " could not open (" + error.SocketErrorCode + ").";
                Debug.LogWarning("Vision loopback unavailable: " + error.Message);
                _running = false;
                _socket?.Close();
                _socket = null;
            }
        }

        private void ReceiveLoop()
        {
            var remote = new IPEndPoint(IPAddress.Loopback, 0);
            while (_running)
            {
                try
                {
                    var bytes = _socket.Receive(ref remote);
                    if (remote.Address.Equals(IPAddress.Loopback) && bytes.Length <= 8192 && _pending.Count < 32)
                        _pending.Enqueue(new ReceivedDatagram(Encoding.UTF8.GetString(bytes)));
                }
                catch (SocketException) { /* timeout or closed socket */ }
                catch (ObjectDisposedException) { break; }
            }
        }

        private void Update()
        {
            while (_pending.TryDequeue(out var received))
            {
                ReceivedPackets++;
                if (received.ElapsedSeconds >= 0.5) { Reject("queued >500 ms"); continue; }
                try
                {
                    var frame = JsonUtility.FromJson<LandmarkFrame>(received.Json);
                    if (!HandKinematics.IsUsable(frame)) { Reject("invalid landmarks/handedness/confidence"); continue; }
                    if (received.ElapsedSeconds + frame.ageMs / 1000.0 >= 0.5) { Reject("capture >500 ms"); continue; }
                    var left = HandKinematics.IsLeft(frame);
                    var oldCapture = left ? _leftCapture : _rightCapture;
                    var oldSequence = left ? _leftSequence : _rightSequence;
                    // The Python bridge supplies both. Legacy packets may omit either value.
                    // A stale side may be a restarted worker whose sequence began again (audit A-001).
                    var stale = Time.realtimeSinceStartupAsDouble - (left ? _leftReceivedAt : _rightReceivedAt) >= 0.5;
                    if (!HandStreamOrder.Accepts(frame.sequence, frame.captureSeconds, oldSequence, oldCapture, stale)) { Reject("old or duplicate packet"); continue; }
                    var receivedAt = Time.realtimeSinceStartupAsDouble - received.ElapsedSeconds - frame.ageMs / 1000.0;
                    if (left)
                    {
                        LatestLeft = frame; _leftReceivedAt = receivedAt;
                        _leftArrivedAt = Time.realtimeSinceStartupAsDouble - received.ElapsedSeconds;
                        _leftCapture = frame.captureSeconds; _leftSequence = frame.sequence;
                    }
                    else
                    {
                        LatestRight = frame; _rightReceivedAt = receivedAt;
                        _rightArrivedAt = Time.realtimeSinceStartupAsDouble - received.ElapsedSeconds;
                        _rightCapture = frame.captureSeconds; _rightSequence = frame.sequence;
                    }
                    AcceptedPackets++;
                }
                catch (ArgumentException) { Reject("malformed JSON"); }
            }
            if (!RightValid) LatestRight = null;
            if (!LeftValid) LatestLeft = null;
        }

        public void BeginNewSession()
        {
            ResetFrames();
            while (_pending.TryDequeue(out _)) { }
        }

        private void ResetFrames()
        {
            LatestRight = LatestLeft = null;
            _rightReceivedAt = _leftReceivedAt = 0;
            _rightArrivedAt = _leftArrivedAt = 0;
            _rightCapture = _leftCapture = 0;
            _rightSequence = _leftSequence = 0;
            ReceivedPackets = AcceptedPackets = RejectedPackets = 0;
            LastRejection = "";
        }

        private void OnDisable()
        {
            _running = false;
            _socket?.Close();
            _thread?.Join(500);
            _socket = null;
            _thread = null;
            while (_pending.TryDequeue(out _)) { }
            ResetFrames();
        }
    }
}
