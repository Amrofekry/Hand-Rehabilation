using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace GripRehab
{
    // Preview stays on 127.0.0.1 and is never written to disk by this component.
    public sealed class VisionPreviewReceiver : MonoBehaviour
    {
        public int port = 27819;
        public ApartmentExperience experience;
        public RawImage image;
        public CanvasGroup panel;
        public bool visible = true;
        public int FrameCount { get; private set; }
        public int ReceivedPackets { get; private set; }

        readonly ConcurrentQueue<byte[]> pending = new ConcurrentQueue<byte[]>();
        UdpClient socket;
        Thread thread;
        volatile bool running;
        Texture2D texture;
        float lastFrameAt;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            try
            {
                socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                socket.Client.ReceiveTimeout = 200;
                running = true;
                thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "Rehab local camera preview" };
                thread.Start();
            }
            catch (SocketException error)
            {
                Debug.LogWarning("[VISION_PREVIEW] Loopback unavailable: " + error.Message);
                running = false; socket?.Close(); socket = null;
            }
        }

        void ReceiveLoop()
        {
            var remote = new IPEndPoint(IPAddress.Loopback, 0);
            while (running)
            {
                try
                {
                    byte[] jpeg = socket.Receive(ref remote);
                    if (!remote.Address.Equals(IPAddress.Loopback) || jpeg.Length < 4 || jpeg.Length > 60000 ||
                        jpeg[0] != 0xff || jpeg[1] != 0xd8) continue;
                    while (pending.TryDequeue(out _)) { } // only the newest frame matters
                    pending.Enqueue(jpeg);
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { break; }
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.V)) visible = !visible;
            if (pending.TryDequeue(out var data))
            {
                ReceivedPackets++;
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    lastFrameAt = Time.unscaledTime;
                }
                else if (visible && experience != null && experience.CameraMode)
                {
                    if (texture == null) texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    if (ImageConversion.LoadImage(texture, data, false))
                    {
                        if (image != null) image.texture = texture;
                        lastFrameAt = Time.unscaledTime;
                        FrameCount++;
                    }
                }
            }
            if (panel != null)
            {
                panel.alpha = visible && experience != null && experience.CameraMode &&
                              Time.unscaledTime - lastFrameAt < 1f ? 1f : 0f;
                panel.blocksRaycasts = false;
            }
        }

        void OnDisable()
        {
            running = false;
            socket?.Close();
            thread?.Join(500);
            socket = null; thread = null;
            while (pending.TryDequeue(out _)) { }
            if (image != null) image.texture = null;
            if (texture != null) Destroy(texture);
            texture = null; FrameCount = ReceivedPackets = 0;
            if (panel != null) panel.alpha = 0;
        }
    }
}
