using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace GripRehab
{
    // The OS BLE stack stays in the Phase 2 Python bridge. Only loopback data is accepted.
    public sealed class LiveForceReceiver : MonoBehaviour, IForceSource, IRehabForceSource
    {
        private readonly ConcurrentQueue<ReceivedDatagram> _pending = new ConcurrentQueue<ReceivedDatagram>();
        private UdpClient _socket;
        private Thread _thread;
        private volatile bool _running;
        private bool _allowSynthetic;
#if UNITY_EDITOR
        [Tooltip("Editor-only test input. Off by default; hardware is never substituted.")] public bool allowSyntheticInEditor;
#endif
        public LiveForceState State { get; } = new LiveForceState();
        public ForceReading Latest => State.Latest;
        public ForceSourceKind Kind => State.Kind;
        public bool TryGetSample(double now, out ForceSample sample) => State.TryGetSample(now, out sample);

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            while (_pending.TryDequeue(out _)) { }
            _allowSynthetic = Array.IndexOf(Environment.GetCommandLineArgs(), "-rehab-force-replay") >= 0;
#if UNITY_EDITOR
            _allowSynthetic |= allowSyntheticInEditor;
#endif
            try
            {
                _socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 27818));
                _socket.Client.ReceiveTimeout = 200;
                _running = true;
                _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "Grip force loopback" };
                _thread.Start();
            }
            catch (SocketException error)
            {
                State.Invalidate("Force bridge port unavailable: " + error.SocketErrorCode);
                Debug.LogWarning(State.Status);
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
                    if (remote.Address.Equals(IPAddress.Loopback) && bytes.Length <= 2048 && _pending.Count < 32)
                        _pending.Enqueue(new ReceivedDatagram(Encoding.UTF8.GetString(bytes)));
                }
                catch (SocketException) { /* receive timeout or closed socket */ }
                catch (ObjectDisposedException) { break; }
            }
        }

        private void Update()
        {
            var now = Time.realtimeSinceStartupAsDouble;
            while (_pending.TryDequeue(out var received))
            {
                try
                {
                    var frame = JsonUtility.FromJson<ForceBridgeFrame>(received.Json);
                    State.Accept(frame, now, _allowSynthetic, received.ElapsedSeconds);
                }
                catch (ArgumentException) { /* malformed JSON is rejected */ }
            }
            State.Tick(now);
        }

        private void OnDisable()
        {
            _running = false;
            _socket?.Close();
            _thread?.Join(500);
            _socket = null;
            _thread = null;
            while (_pending.TryDequeue(out _)) { }
            State.Invalidate("Force bridge stopped");
        }
    }
}
