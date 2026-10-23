using System;
using System.IO;
using GripRehab.Product;
using UnityEngine;
namespace GripRehab
{
    public sealed partial class ResearchPortalHost
    {
        float physiologicalReference; int pulseReferenceSamples;
        void SensingConsent()
        {
            Show("Optional local sensing", "Face-expression and camera pulse estimates are experimental. They are not diagnoses, pain measurements or validated heart-rate readings. No raw camera images are saved.",()=>Prepare(guest));
            view.Text("Each option needs a separate opt-in for this app session. You can revoke it here at any time. Low-quality signals are excluded. Eye/head tracking and voice commands are not enabled.",120);
            view.Button(faceConsent ? "FACE EXPRESSION: ON - REVOKE" : "I CONSENT TO LOCAL FACE EXPRESSION",()=>{faceConsent=!faceConsent;WriteSensingConsent();SensingConsent();});
            view.Button(physiologyConsent ? "CAMERA PULSE: ON - REVOKE" : "I CONSENT TO LOCAL CAMERA PULSE",()=>{physiologyConsent=!physiologyConsent;physiologicalReference=0;pulseReferenceSamples=0;WriteSensingConsent();SensingConsent();});
            view.Text("Pulse needs at least 15 seconds of a well-lit, steady face; HRV requires 60 seconds and high signal quality. Expressions do not establish fatigue or pain.",112);
            view.Status(AuxStatus());view.Button("BACK TO PREPARATION",()=>Prepare(guest),true);
        }
        string AuxStatus()
        {
            double now=Time.realtimeSinceStartupAsDouble;
            if(auxiliary==null || !auxiliary.Fresh(now))return "Face / pulse: unavailable or waiting for consented fresh data.";
            var p=auxiliary.Latest;var names=new[]{"Angry","Disgust","Fearful","Happy","Neutral","Sad","Surprised"};
            return "Expression: "+(faceConsent && p.faceValid?names[p.emotion]+" (estimate)":"OFF / low quality")+
                " | Pulse: "+(physiologyConsent && p.physiologyValid?p.hr.ToString("0")+" bpm (estimate)":"OFF / low quality");
        }
        [Serializable] sealed class LocalSensingConsent { public bool face, physiology; public double updatedUnix; }
        static string ResearchProjectRoot()
        {
            var dir = new DirectoryInfo(Application.dataPath);
            for(int i=0;i<5 && dir!=null;i++,dir=dir.Parent)
                if(File.Exists(Path.Combine(dir.FullName,"Tools","Vision","mediapipe_bridge.py")))return dir.FullName;
            return null;
        }
        void WriteSensingConsent()
        {
            string root=ResearchProjectRoot();if(root==null)return;
            string path=Path.Combine(root,"Logs","ai-camera-consent.json");Directory.CreateDirectory(Path.GetDirectoryName(path));
            var value=new LocalSensingConsent { face=faceConsent,physiology=physiologyConsent,updatedUnix=(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds };
            try { File.WriteAllText(path,JsonUtility.ToJson(value)); } catch(IOException) { faceConsent=physiologyConsent=false; }
        }
        void RevokeSensing(){faceConsent=physiologyConsent=false;WriteSensingConsent();auxiliary?.Dispose();auxiliary=null;}
        void TickSensing(double now)
        {
            if(now>=nextConsentWrite){nextConsentWrite=now+1;WriteSensingConsent();if(ScreenTitle=="Optional local sensing")view.SetStatus(AuxStatus());}
            if(!faceConsent&&!physiologyConsent){auxiliary?.Dispose();auxiliary=null;return;}
            auxiliary=auxiliary??new ResearchAuxiliaryReceiver();auxiliary.Poll(now);
        }
        void EnrichMetric(MetricRecord m, TrialSample input, double now)
        {
            m.sequence=input.sequence;m.measurementAgeMs=(float)Math.Min(60000,Math.Max(0,(now-input.capturedAt)*1000));
            m.romValid=input.romValid;m.romDegrees=input.romValid?input.romDegrees:0;
            m.faceConsented=faceConsent;m.pulseConsented=physiologyConsent;
            m.modelValid=lastModelValid&&input.Fresh&&input.synthetic&&proxyDemo;m.modelProbability=m.modelValid?lastModelProbability:0;
            m.modelProvenance=m.modelValid?"PROTOTYPE-PROXY / SYNTHETIC demo":"statistical fallback";
            if(auxiliary!=null&&auxiliary.Fresh(now))
            {
                var p=auxiliary.Latest;m.faceValid=faceConsent&&p.faceValid;m.emotionClass=m.faceValid?p.emotion:-1;
                m.emotionConfidence=m.faceValid?p.emotionConfidence:0;
                m.pulseValid=physiologyConsent&&p.physiologyValid;m.hrvValid=m.pulseValid&&p.hrvValid;
                m.heartRateBpm=m.pulseValid?p.hr:0;m.pulseQuality=m.pulseValid?p.quality:0;m.hrvRmssdMs=m.hrvValid?p.rmssd:0;
            }
        }
        void ApplyFatigueSafety(double now,TrialSample input,bool practising,bool comparable)
        {
            if(fatigueSafety==null || !guidance || session.module!=1 || now<nextAiWindow)return;
            nextAiWindow=now+1;
            var stats=features.Read(now,5);var signs=comparable?StatisticalPredictor.Evaluate(stats,capability,limits):ResearchSigns.None;
            float? force=input.unit==InputUnit.Newtons && comparable && capability.hasForce && capability.meanNewtons>0 && stats.forceValid ? ((signs&ResearchSigns.Force)!=0?.9f:.1f) : (float?)null;
            float? motion=input.MotionValid && comparable && stats.motionValid ? ((signs&ResearchSigns.Motion)!=0?.9f:.1f) : (float?)null;
            float? face=null,pulse=null;
            if(auxiliary!=null && auxiliary.Fresh(now))
            {
                var p=auxiliary.Latest;
                if(faceConsent && p.faceValid && p.emotion>=0 && p.emotionConfidence >= (guidanceSettings!=null?guidanceSettings.facialConfidenceMinimum:.65f))face=p.strain;
                if(physiologyConsent && p.physiologyValid && p.quality>=(guidanceSettings!=null?guidanceSettings.pulseQualityMinimum:.8f))
                {
                    if(pulseReferenceSamples<(guidanceSettings!=null?guidanceSettings.pulseReferenceWindows:15)){physiologicalReference+=(p.hr-physiologicalReference)/++pulseReferenceSamples;}
                    else pulse=p.hr>physiologicalReference*(guidanceSettings!=null?guidanceSettings.physiologicalRiseRatio:1.25f)?.9f:.1f;
                }
            }
            bool modelValid=true; lastModelValid=false;
            if(proxyDemo && input.synthetic && input.unit==InputUnit.Newtons)
            {
                if(aiWindow.CopyTo(aiFeatures,now))
                {
                    float probability=0; modelValid=fatigueAi!=null && fatigueAi.TryPredict(aiFeatures,true,true,out probability);
                    lastModelValid=modelValid;lastModelProbability=modelValid?probability:0;
                    if(modelValid)force=probability;
                }
                // Warm-up freezes increases. A configured but failed model cannot silently fall back.
                else comparable=false;
            }
            bool requiredValid=input.Fresh && !savingFailed && modelValid && !sourceFault;
            var decision=fatigueSafety.Step(now,requiredValid,force,motion,face,pulse,practising&&comparable&&(force.HasValue||motion.HasValue)&&shell.experience.Held!=null);
            if(session.policyEvents.Count<500)session.policyEvents.Add(new PolicyEvent {seconds=now-started,status=decision.state,reason=decision.reason+(lastModelValid?" / PROTOTYPE-PROXY / SYNTHETIC demo / ":" / statistical fallback / ")+AuxStatus(),settingsVersion="research-ai-1",level=decision.level,signs=decision.signs});
            shell.experience.SetResearchDifficulty(decision.level,limits.maximumLevel,guidanceSettings!=null?guidanceSettings.maximumHoldSeconds:5);
            if(decision.state=="stopped"){EndSession(RecordState.SafetyStopped,0,true);return;}
            if(decision.state=="warning" && ScreenTitle!="Research safety pause")
            {
                shell.experience.Release();
                Show("Research safety pause",(sourceFault?"Input source, shape or calibration changed. Finish this attempt and collect a matching baseline":decision.reason)+". Unvalidated research estimates. The session is paused; it will not resume automatically.",()=>EndSession(RecordState.Interrupted,0,true));
                view.Status(AuxStatus());view.Button("RESUME AFTER SIGNALS CLEAR",()=>{
                    try{fatigueSafety.Resume();view.Hide();shell.ResumeResearchGame();}
                    catch(InvalidOperationException e){view.SetStatus(e.Message);}
                },true,enabled:!sourceFault);
                view.Button("FINISH SESSION",()=>EndSession(RecordState.Interrupted,0,true));
            }
        }
    }
}
