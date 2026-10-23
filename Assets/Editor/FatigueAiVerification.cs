using System;
using System.IO;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using GripRehab.Product;
using UnityEngine;
namespace GripRehab.Editor
{
    public static partial class FatigueAiVerification
    {
        [Serializable] class Step {public double now;public bool valid;public float[] signals;public string state;public int level, signs;}
        [Serializable] class Case {public string name;public Step[] steps;}
        [Serializable] class Replay {public Case[] cases;}
        [Serializable] class Features {public float[] input,expected;public float mvc;}
        [Serializable] class OnnxFixture {public float[] features;public float expected;}
        [Serializable] class Report {public bool passed;public string unity,utc;public int assertions;public float maxFeatureError,onnxDifference;public double inferenceP50Ms,inferenceP95Ms;}
        static int checks;
        static void Check(bool pass,string name){checks++;if(!pass)throw new Exception("AI acceptance: "+name);}
        static partial void RunChecks()
        {
            checks=0;
            var settings=Resources.Load<ResearchGuidanceSettings>("ResearchGuidance");
            Check(settings!=null && settings.FatigueSnapshot().consecutive>=3,"versioned research settings asset");
            UnityEditor.EditorUtility.SetDirty(settings);UnityEditor.AssetDatabase.SaveAssetIfDirty(settings);
            var replay=JsonUtility.FromJson<Replay>(Resources.Load<TextAsset>("ResearchAi/SYNTHETIC_policy-fixture").text);
            foreach(var c in replay.cases){var policy=new FatigueSafetyPolicy();foreach(var row in c.steps){
                float? a=row.signals[0]<0?(float?)null:row.signals[0],b=row.signals[1]<0?(float?)null:row.signals[1],
                    d=row.signals[2]<0?(float?)null:row.signals[2],e=row.signals[3]<0?(float?)null:row.signals[3];
                var decision=policy.Step(row.now,row.valid,a,b,d,e,true);
                Check(decision.state==row.state&&decision.level==row.level&&decision.signs==row.signs,"Python/C# replay "+c.name+" at "+row.now);
            }}
            var p=new FatigueSafetyPolicy();for(int i=0;i<3;i++)p.Step(i,true,.9f,null,null,null,false);
            bool refused=false;try{p.Resume();}catch(InvalidOperationException){refused=true;}Check(refused,"warning cannot resume early");
            for(int i=3;i<6;i++)p.Step(i,true,.1f,null,null,null,false);Check(p.Resume().state=="active","explicit resume after clear");
            Check(p.Step(6,true,float.NaN,null,null,null,true).state=="warning","NaN pauses safely");
            var fixture=JsonUtility.FromJson<Features>(Resources.Load<TextAsset>("ResearchAi/SYNTHETIC_feature-fixture").text);
            var window=new TemporalForceWindow();for(int i=0;i<fixture.input.Length;i++)Check(window.Add(i*.1,fixture.input[i],fixture.mvc),"feature accepted");
            var output=new float[400];Check(window.CopyTo(output,9.9),"feature window complete");float delta=0;
            for(int i=0;i<400;i++)delta=Math.Max(delta,Math.Abs(output[i]-fixture.expected[i]));
            Check(delta<1e-4f,"Python/C# causal feature parity");Check(!window.CopyTo(output,10.4),"stale feature refused");
            Check(!window.Add(10,float.NaN,50),"NaN force refused");
            var prior=new TrialSample {now=1,capturedAt=1,sequence=1,valid=true,source="Phase 2",shape="sphere",calibrationRevision="1",unit=InputUnit.Newtons,value=20};
            Check(!new TrialSample().ChangesIdentity(prior),"missing force sample does not fabricate a calibration change");
            var recovered=prior;recovered.now=recovered.capturedAt=2;recovered.sequence=2;
            Check(!recovered.ChangesIdentity(prior),"same-source recovery preserves the baseline identity");
            recovered.calibrationRevision="2";Check(recovered.ChangesIdentity(prior),"fresh changed calibration still latches a source fault");
            CheckAuxiliaryTransport();
            var trial=new StagedCapabilityTrial("SYNTHETIC_account","SYNTHETIC_profile",true);
            for(int i=0;i<600&&!trial.Complete;i++)trial.Step(new TrialSample{now=i*.1,capturedAt=i*.1,sequence=i,source="SYNTHETIC_demo",shape="sphere",calibrationRevision="test",unit=InputUnit.Newtons,value=20,closure=(i%20)/20f,speed=.1f,jitter=.01f,valid=true,synthetic=true,handValid=true,romValid=true,romDegrees=i%90},.1f);
            Check(trial.Complete,"all staged baseline phases finish");var baseline=trial.Finish();
            Check(baseline.mvcNewtons==20&&baseline.mvcTrial3==20&&baseline.enduranceSeconds>9&&baseline.hasRom&&baseline.hasStability,"MVC/endurance/ROM persisted");
            var invalid=new StagedCapabilityTrial("SYNTHETIC_account","SYNTHETIC_profile",true);invalid.Step(new TrialSample(),.1f);Check(invalid.Progress==0,"invalid input earns no baseline time");
            var modelFixture=JsonUtility.FromJson<OnnxFixture>(Resources.Load<TextAsset>("ResearchAi/parity-fixture").text);
            float modelDifference;double[] latency=new double[30];
            using(var model=new ResearchAiRuntime()){
                Check(model.Available,"native Sentis CPU model loaded: "+model.Status);
                var sw=new Stopwatch();float predicted=model.PredictNormalizedFixture(modelFixture.features);modelDifference=Math.Abs(predicted-modelFixture.expected);
                Check(modelDifference<1e-4f,"ONNX Runtime/Sentis CPU parity");
                Check(!model.TryPredict(modelFixture.features,false,true,out _),"public EMG model blocked on real force");
                Check(!model.TryPredict(modelFixture.features,true,false,out _),"synthetic model opt-in required");
                for(int i=0;i<30;i++){sw.Restart();model.PredictNormalizedFixture(modelFixture.features);sw.Stop();latency[i]=sw.Elapsed.TotalMilliseconds;}
            }
            Array.Sort(latency);File.WriteAllText("Logs/ai-native-checks.json",JsonUtility.ToJson(new Report{passed=true,assertions=checks,maxFeatureError=delta,onnxDifference=modelDifference,inferenceP50Ms=latency[15],inferenceP95Ms=latency[28],unity=Application.unityVersion,utc=DateTime.UtcNow.ToString("O")},true));
            UnityEngine.Debug.Log("[REHAB_AI] PASS "+checks+" checks including native Sentis inference and replay parity");
        }
        static void CheckAuxiliaryTransport()
        {
            using(var sender=new UdpClient())
            {
                using(var receiver=new ResearchAuxiliaryReceiver())
                {
                    var p=new ResearchAuxiliaryReceiver.Packet { version="aux-1",sessionId="SYNTHETIC_aux_a",sequence=1,
                        faceValid=true,emotion=4,emotionConfidence=.9f,strain=.1f,physiologyValid=true,hr=72,quality=.9f };
                    SendAux(sender,receiver,p);
                    Check(receiver.Fresh(Time.realtimeSinceStartupAsDouble)&&receiver.Latest.emotion==4,"live loopback auxiliary packet accepted");
                    p.strain=.9f;SendAux(sender,receiver,p);
                    Check(receiver.Latest.strain==.1f,"duplicate auxiliary sequence rejected");
                    p.sequence=-1;SendAux(sender,receiver,p);
                    Check(receiver.Latest.sequence==1,"negative auxiliary sequence rejected");
                    p.sequence=2;p.ageMs=500;SendAux(sender,receiver,p);
                    Check(receiver.Latest.sequence==1,"old auxiliary sample rejected");
                    p.ageMs=490;SendAux(sender,receiver,p,-.1);
                    Check(receiver.Latest.sequence==1,"auxiliary transport delay included in500ms limit");
                    p.ageMs=0;SendAux(sender,receiver,p,.2);
                    Check(receiver.Latest.sequence==1,"future auxiliary timestamp rejected");
                    p.emotion=-1;SendAux(sender,receiver,p);
                    Check(receiver.Latest.sequence==1,"face-valid packet cannot index a missing expression");
                    p.emotion=4;p.hr=0;SendAux(sender,receiver,p);
                    Check(receiver.Latest.sequence==1,"pulse-valid packet needs a positive heart rate");
                    p.hr=72;p.physiologyValid=false;p.hrvValid=true;SendAux(sender,receiver,p);
                    Check(receiver.Latest.sequence==1,"HRV cannot be valid without pulse");
                    p.hrvValid=false;p.physiologyValid=true;p.quality=.1f;SendAux(sender,receiver,p);
                    Check(receiver.Latest.sequence==1,"low-quality pulse cannot claim validity");
                    var malformed=Encoding.UTF8.GetBytes("{broken");sender.Send(malformed,malformed.Length,"127.0.0.1",27820);
                    System.Threading.Thread.Sleep(5);receiver.Poll(Time.realtimeSinceStartupAsDouble);
                    Check(receiver.Latest.sequence==1,"malformed auxiliary JSON leaves valid state intact");
                    p.quality=.9f;p.sessionId="SYNTHETIC_aux_b";p.sequence=0;SendAux(sender,receiver,p);
                    Check(receiver.Latest.sessionId==p.sessionId&&receiver.Latest.sequence==0,"auxiliary worker restart accepted");
                    Check(!receiver.Fresh(Time.realtimeSinceStartupAsDouble+.51),"auxiliary frame expires after500ms");
                }
                using(var reopened=new ResearchAuxiliaryReceiver())
                {
                    SendAux(sender,reopened,new ResearchAuxiliaryReceiver.Packet {version="aux-1",sessionId="SYNTHETIC_aux_c",sequence=0,emotion=-1});
                    Check(reopened.Fresh(Time.realtimeSinceStartupAsDouble),"auxiliary disposal releases UDP27820");
                }
            }
        }
        static void SendAux(UdpClient sender,ResearchAuxiliaryReceiver receiver,ResearchAuxiliaryReceiver.Packet p,double sentOffset=0)
        {
            p.sentUnix=(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds+sentOffset;
            byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(p));sender.Send(bytes,bytes.Length,"127.0.0.1",27820);
            System.Threading.Thread.Sleep(5);receiver.Poll(Time.realtimeSinceStartupAsDouble);
        }
    }
}
