using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
namespace GripRehab
{
    /// <summary>Local derived measurements only. Small bounded queue, no raw frames or face identity.</summary>
    public sealed class ResearchAuxiliaryReceiver : IDisposable
    {
        [Serializable] public sealed class Packet
        {
            public double sentUnix; public string version,sessionId; public long sequence; public float ageMs;
            public bool faceValid, physiologyValid, hrvValid;
            public int emotion; public float emotionConfidence, strain, hr, rmssd, quality;
        }
        UdpClient socket; string sessionId; long sequence=-1; double measured=-1e30;
        public Packet Latest { get; private set; }
        public ResearchAuxiliaryReceiver()
        {
            try { socket=new UdpClient(new IPEndPoint(IPAddress.Loopback,27820)); socket.Client.Blocking=false; }
            catch (SocketException) { socket?.Close(); socket=null; }
        }
        public void Poll(double now)
        {
            if(socket==null)return;
            var endpoint=new IPEndPoint(IPAddress.Loopback,0);
            for(int i=0;i<8 && socket.Available>0;i++)
            {
                try {
                    var bytes=socket.Receive(ref endpoint);
                    if(bytes.Length>2048 || !endpoint.Address.Equals(IPAddress.Loopback))continue;
                    var p=JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(bytes));
                    if(p==null || p.version!="aux-1" || p.sequence<0 || (p.sessionId==sessionId&&p.sequence<=sequence) || string.IsNullOrEmpty(p.sessionId) || p.sessionId.Length>64 || !Valid(p.ageMs,0,499) ||
                        !Valid(p.emotionConfidence,0,1) || !Valid(p.strain,0,1) || !Valid(p.quality,0,1) || !Valid(p.hr,0,240) || !Valid(p.rmssd,0,1000) || p.emotion < -1 || p.emotion>6)continue;
                    // Validate flag/value coherence before any UI indexes the
                    // expression label or guidance uses an optional modality.
                    if((p.faceValid && (p.emotion<0 || p.emotionConfidence<.65f)) ||
                        (p.physiologyValid && (p.hr<=0 || p.quality<.55f)) ||
                        (p.hrvValid && !p.physiologyValid))continue;
                    double queued=(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds-p.sentUnix;
                    if(double.IsNaN(queued)||double.IsInfinity(queued)||queued<-.05||queued+p.ageMs/1000.0>=.5)continue;
                    sessionId=p.sessionId;sequence=p.sequence;measured=now-Math.Max(0,queued)-p.ageMs/1000.0;Latest=p;
                } catch (Exception e) when(e is SocketException || e is ArgumentException) { }
            }
        }
        static bool Valid(float f,float min,float max)=>!float.IsNaN(f)&&!float.IsInfinity(f)&&f>=min&&f<=max;
        public bool Fresh(double now)=>Latest!=null && now>=measured && now-measured<.5;
        public void Dispose(){socket?.Close();socket=null;Latest=null;}
    }
}
