using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace GripRehab
{
    // Optional localhost adapter. An external MediaPipe/YOLO process must provide actual inference.
    // UDP is limited to loopback so frames do not leave the machine. Never use timestamps as
    // synchronized clock truth until Phase 4 alignment is implemented.
    public sealed class VisionUdpReceiver : MonoBehaviour
    {
        public int port = 27817;
        public LandmarkFrame Latest { get; private set; }
        public bool TrackingValid => Latest != null && HandKinematics.IsUsable(Latest) &&
                                     Time.realtimeSinceStartupAsDouble - _receivedAt < 0.5;
        private readonly ConcurrentQueue<ReceivedDatagram> _pending = new ConcurrentQueue<ReceivedDatagram>();
        private UdpClient _socket;
        private Thread _thread;
        private volatile bool _running;
        private double _receivedAt;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            while (_pending.TryDequeue(out _)) { }
            try
            {
                _socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                _socket.Client.ReceiveTimeout = 200;
                _running = true;
                _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "Rehab vision loopback" };
                _thread.Start();
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"Vision loopback unavailable: {e.Message}");
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
                    if (remote.Address.Equals(IPAddress.Loopback) && bytes.Length <= 8192 && _pending.Count < 4)
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
                try
                {
                    var parsed = JsonUtility.FromJson<LandmarkFrame>(received.Json);
                    if (!HandKinematics.IsUsable(parsed)) continue;
                    Latest = parsed;
                    _receivedAt = Time.realtimeSinceStartupAsDouble - received.ElapsedSeconds;
                }
                catch (ArgumentException) { /* malformed frame is ignored */ }
            }
            if (!TrackingValid) Latest = null;
        }

        private void OnDisable()
        {
            _running = false;
            _socket?.Close();
            _thread?.Join(500);
            _socket = null;
            _thread = null;
            while (_pending.TryDequeue(out _)) { }
            Latest = null;
        }
    }
}

