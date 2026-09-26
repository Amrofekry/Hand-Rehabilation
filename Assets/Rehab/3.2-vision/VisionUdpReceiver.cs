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
    public sealed class VisionUdpReceiver : MonoBehaviour
    {
        public int port = 27817;
        public LandmarkFrame LatestRight { get; private set; }
        public LandmarkFrame LatestLeft { get; private set; }
        public bool RightValid => Valid(LatestRight, _rightReceivedAt);
        public bool LeftValid => Valid(LatestLeft, _leftReceivedAt);
        // Backward compatibility for the other training modules.
        public LandmarkFrame Latest => RightValid ? LatestRight : LeftValid ? LatestLeft : null;
        public bool TrackingValid => RightValid || LeftValid;
        public int ReceivedPackets { get; private set; }
        public int AcceptedPackets { get; private set; }
        public int RejectedPackets { get; private set; }
        public string LastRejection { get; private set; } = "";
        void Reject(string reason) { RejectedPackets++; LastRejection = reason; }

        private readonly ConcurrentQueue<ReceivedDatagram> _pending = new ConcurrentQueue<ReceivedDatagram>();
        private UdpClient _socket;
        private Thread _thread;
        private volatile bool _running;
        private double _rightReceivedAt, _leftReceivedAt;
        private double _rightCapture, _leftCapture;
        private long _rightSequence, _leftSequence;

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
                _running = true;
                _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "Rehab bimanual vision loopback" };
                _thread.Start();
            }
            catch (SocketException error)
            {
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
                    if (frame.captureSeconds > 0 && oldCapture > 0 && frame.captureSeconds <= oldCapture) { Reject("old capture"); continue; }
                    if (frame.sequence > 0 && oldSequence > 0 && frame.sequence <= oldSequence) { Reject("duplicate sequence"); continue; }
                    var receivedAt = Time.realtimeSinceStartupAsDouble - received.ElapsedSeconds - frame.ageMs / 1000.0;
                    if (left)
                    {
                        LatestLeft = frame; _leftReceivedAt = receivedAt;
                        _leftCapture = frame.captureSeconds; _leftSequence = frame.sequence;
                    }
                    else
                    {
                        LatestRight = frame; _rightReceivedAt = receivedAt;
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
