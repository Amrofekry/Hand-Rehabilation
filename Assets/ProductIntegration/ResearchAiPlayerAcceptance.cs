using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace GripRehab
{
    /// <summary>Opt-in packaged-model check. Uses fixtures, never a camera or a patient record.</summary>
    public sealed class ResearchAiPlayerAcceptance : MonoBehaviour
    {
        [Serializable] sealed class Fixture { public float[] features; public float expected; }
        [Serializable] sealed class Result
        {
            public bool passed;
            public int assertions;
            public string unity, utc, error;
            public float onnxDifference;
            public double inferenceP50Ms, inferenceP95Ms;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-research-ai-check") >= 0)
                new GameObject("Packaged research AI acceptance").AddComponent<ResearchAiPlayerAcceptance>();
        }
        IEnumerator Start()
        {
            yield return null;
            var result = new Result { unity = Application.unityVersion, utc = DateTime.UtcNow.ToString("O") };
            try
            {
                var resource = Resources.Load<TextAsset>("ResearchAi/parity-fixture");
                Check(resource != null, "Packaged fixture", result);
                var fixture = JsonUtility.FromJson<Fixture>(resource.text);
                using (var model = new ResearchAiRuntime())
                {
                    Check(model.Available, "Packaged Sentis CNN: " + model.Status, result);
                    result.onnxDifference = Math.Abs(model.PredictNormalizedFixture(fixture.features) - fixture.expected);
                    Check(result.onnxDifference < 1e-4f, "Packaged ONNX parity", result);
                    Check(!model.TryPredict(fixture.features, false, true, out _), "Real-force proxy guard", result);
                    Check(!model.TryPredict(fixture.features, true, false, out _), "Synthetic opt-in guard", result);
                    var durations = new double[30]; var watch = new Stopwatch();
                    for (int i = 0; i < durations.Length; i++)
                    {
                        watch.Restart(); model.PredictNormalizedFixture(fixture.features); watch.Stop();
                        durations[i] = watch.Elapsed.TotalMilliseconds;
                    }
                    Array.Sort(durations);
                    result.inferenceP50Ms = durations[15]; result.inferenceP95Ms = durations[28];
                }
                CheckAuxiliary(result);
                result.passed = true;
            }
            catch (Exception error) { result.error = error.ToString(); }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/ai-player-checks.json", JsonUtility.ToJson(result, true));
            if (result.passed) UnityEngine.Debug.Log("[REHAB_AI_PLAYER] PASS: packaged CNN parity, deployment guards and auxiliary transport");
            else UnityEngine.Debug.LogError("[REHAB_AI_PLAYER] FAIL: " + result.error);
            // The existing apartment acceptance owns quitting the player. A
            // failure is logged so its normal runtime-error check also fails.
            Destroy(gameObject);
        }
        static void Check(bool passed, string name, Result result)
        { result.assertions++; if (!passed) throw new InvalidOperationException(name); }
        static void CheckAuxiliary(Result result)
        {
            using(var sender=new UdpClient())
            {
                using(var receiver=new ResearchAuxiliaryReceiver())
                {
                    var packet=new ResearchAuxiliaryReceiver.Packet {version="aux-1",sessionId="SYNTHETIC_player_aux",sequence=1,
                        faceValid=true,emotion=4,emotionConfidence=.9f,strain=.1f,physiologyValid=true,hr=72,quality=.9f};
                    SendAux(sender,receiver,packet);
                    Check(receiver.Fresh(Time.realtimeSinceStartupAsDouble)&&receiver.Latest.emotion==4,"Packaged auxiliary loopback",result);
                    packet.sequence=2;packet.emotion=-1;SendAux(sender,receiver,packet);
                    Check(receiver.Latest.sequence==1,"Packaged expression validity guard",result);
                    packet.faceValid=false;packet.hr=0;SendAux(sender,receiver,packet);
                    Check(receiver.Latest.sequence==1,"Packaged pulse validity guard",result);
                    Check(!receiver.Fresh(Time.realtimeSinceStartupAsDouble+.51),"Packaged auxiliary age",result);
                }
                using(var receiver=new ResearchAuxiliaryReceiver())
                {
                    SendAux(sender,receiver,new ResearchAuxiliaryReceiver.Packet {version="aux-1",sessionId="SYNTHETIC_player_aux_reopen",emotion=-1});
                    Check(receiver.Fresh(Time.realtimeSinceStartupAsDouble),"Packaged auxiliary UDP disposal",result);
                }
            }
        }
        static void SendAux(UdpClient sender,ResearchAuxiliaryReceiver receiver,ResearchAuxiliaryReceiver.Packet packet)
        {
            packet.sentUnix=(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds;
            byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));sender.Send(bytes,bytes.Length,"127.0.0.1",27820);
            System.Threading.Thread.Sleep(5);receiver.Poll(Time.realtimeSinceStartupAsDouble);
        }
    }
}
