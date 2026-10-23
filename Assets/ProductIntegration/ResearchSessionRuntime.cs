using System;
using GripRehab.Product;
using UnityEngine;

namespace GripRehab
{
    public sealed partial class ResearchPortalHost
    {
        static readonly int[] ResearchFingerStarts = {1,5,9,13,17};
        public bool ModuleStarted(int module, int task)
        {
            if (!LeavingGame()) return false;
            if (!AuthorizeTraining()) return false;
            started = Time.realtimeSinceStartupAsDouble; nextSample = started; nextSave = started + 5;
            visualHold = 0; hadWrist = false; hadInput = false; features.Clear(); savingFailed = false; LastSaveProblem="";
            capability = !guest && HasAccount ? repository.LatestBaseline(repository.Current.id) : null;

            fatigueSafety = new FatigueSafetyPolicy(guidanceSettings != null ? guidanceSettings.FatigueSnapshot() : new FatigueSafetyConfig { maximumLevel = limits.maximumLevel });
            nextAiWindow = started; aiWindow.Clear(); sourceFault=false;
            fatigueAi?.Dispose(); fatigueAi = proxyDemo ? new ResearchAiRuntime() : null; lastModelValid=false;

            shell.experience.SetResearchDifficulty(guidance ? limits.minimumLevel : 1, limits.maximumLevel, guidanceSettings != null ? guidanceSettings.maximumHoldSeconds : 5);
            bool synthetic = !shell.experience.CameraMode && !shell.experience.Hardware;
            if (!guest && HasAccount)
            {
                try { session = repository.Begin(module, task, synthetic); }
                catch (Exception error) { Show("Practice could not start", "Protected storage: " + error.Message, Welcome); view.Button("BACK TO MENU", Welcome, true); return false; }
            }
            else session = new SessionRecord { id = Guid.NewGuid().ToString("N"), module = module, task = task, synthetic = synthetic, startedUtc = DateTime.UtcNow.ToString("O") };
            liveSession = true; view.Hide();
            return true;
        }
        public void ExerciseCompleted(int score, float seconds)
        {
            if (!liveSession) return;
            session.durationSeconds = seconds; EndSession(RecordState.Completed, score, true);
        }
        public bool LeavingGame()
        {
            if (liveSession) EndSession(RecordState.Interrupted, 0, false);
            if (savingFailed) { SavingIssue(); return false; }
            return true;
        }
        void SavingIssue()
        {
            Show("Saving needs attention", "Your previous saved copy is retained. Retry before switching accounts or starting another session.", null);
            view.Status("The current result has not been saved.");
            view.Button("RETRY SAVE", () => {
                try { repository.SaveSession(session); savingFailed = false; Welcome(); }
                catch (Exception error) { view.SetStatus("Save still unavailable: " + error.Message); }
            }, true);
        }
        void EndSession(RecordState state, int score, bool show)
        {
            if (!liveSession) return;
            session.state = state; session.score = Mathf.Clamp(score, 0, 100);
            session.durationSeconds = (float)(Time.realtimeSinceStartupAsDouble - started);
            if (!guest && HasAccount)
            {
                try { repository.SaveSession(session); }
                catch (Exception error) { savingFailed = true; LastSaveProblem=error.Message; }
            }
            liveSession = false; fatigueAi?.Dispose(); fatigueAi=null; lastModelValid=false;
            if (shell != null && shell.experience != null) shell.experience.Release();
            if (show)
            {
                Show(state == RecordState.Completed ? "Practice complete" : "Take a break", "Your game results are a record of practice, not a clinical assessment.", Welcome);
                view.Text("Module " + session.module + " · " + state + "\nScore: " + session.score + "/100\nValid input: " + session.validSeconds.ToString("0.0") + " s", 112);
                view.Text(guest ? "Guest practice was not saved." : savingFailed ? "Saving failed: " + LastSaveProblem + ". Keep this screen open and retry." : "Saved locally in protected storage.");
                if (savingFailed) view.Button("RETRY SAVE", () => Guard(() => { repository.SaveSession(session); savingFailed = false; Dashboard(); }), true);
                view.Button("BACK TO MENU", Welcome, !savingFailed);
            }
        }
        void Update()
        {
            TickSensing(Time.realtimeSinceStartupAsDouble);
            if (!collecting && !liveSession) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextSample) return; nextSample = now + .1;
            var input = ReadSample(now);
            if (collecting)
            {
                baseline.Step(input, .1f); view.SetProgress(baseline.Progress);
                view.SetStatus(baseline.Problem ?? (input.Fresh ? baseline.Instruction + " - " + "Collecting valid samples. Move comfortably; stop if uncomfortable." : "Waiting for fresh input. Keyboard demo: hold Space. Hardware: check calibration/bridge."));
                if (baseline.Complete)
                {
                    collecting = false;
                    Guard(() => { repository.SaveBaseline(baseline.Finish()); BaselineSummary(); });
                }
                return;
            }
            if (session == null) return;
            session.durationSeconds = (float)(now - started);
            bool changedSource = hadInput && input.ChangesIdentity(previousInput);
            if (changedSource) { sourceFault=true; lastModelValid=false; aiWindow.Clear(); features.Clear(); visualHold = 0; hadWrist = false; }
            bool unique = input.Fresh && (!hadInput || changedSource || input.sequence > previousInput.sequence);
            if (unique) { previousInput = input; hadInput = true; }
            bool practising = shell.CurrentPage == "Activity" || (shell.CurrentPage == "Game" && !shell.experience.player.Paused);
            if (practising && unique)
            {
                session.validSeconds += .1f; session.synthetic |= input.synthetic;
                if (input.unit == InputUnit.Newtons) { session.hasForce = true; session.peakNewtons = Mathf.Max(session.peakNewtons, input.value); }
                session.meanClosure += (input.closure - session.meanClosure) / Mathf.Max(1, session.validSeconds * 10);
                features.Add(input);
                if(input.unit==InputUnit.Newtons) aiWindow.Add(input.capturedAt,input.value,capability!=null && capability.mvcNewtons>0?capability.mvcNewtons:50);
            }
            else if ((fatigueSafety?.State == "warning") && unique) {
                features.Add(input); if(input.unit==InputUnit.Newtons)aiWindow.Add(input.capturedAt,input.value,capability!=null&&capability.mvcNewtons>0?capability.mvcNewtons:50);
            }
            if (session.metrics.Count < 1800 && (session.metrics.Count == 0 || now - started - session.metrics[session.metrics.Count - 1].seconds >= 1))
            {
                var metric=new MetricRecord { seconds = now - started, value = input.value, valid = practising && input.Fresh,
                    unit = input.unit, source = input.source, synthetic = input.synthetic, shape = input.shape,
                    calibrationRevision = input.calibrationRevision, handClosure = input.closure, motionSpeed = input.speed, motionJitter = input.jitter };
                EnrichMetric(metric,input,now);session.metrics.Add(metric);
            }
            bool comparable = capability != null && capability.source == input.source && capability.shape == input.shape &&
                capability.calibrationRevision == input.calibrationRevision && capability.unit == input.unit && capability.synthetic == input.synthetic;
            ApplyFatigueSafety(now, input, practising, comparable);
            if (!liveSession) return;
            if (guidance && fatigueSafety != null && fatigueSafety.State != "active") practising = false;
            if (session.module == 1 && practising)
            {
                if (shell.experience.CameraMode)
                    visualHold = input.Fresh && shell.experience.Held != null && input.closure >= .6f ? visualHold + (unique ? .1f : 0) : 0;
                if ((!shell.experience.CameraMode && shell.experience.ProgressSeconds >= shell.experience.RequiredHoldSeconds) || visualHold >= shell.experience.RequiredHoldSeconds)
                { EndSession(RecordState.Completed, 100, true); return; }
            }
            if (!guest && HasAccount && now >= nextSave)
            {
                nextSave = now + 5;
                try { repository.SaveSession(session); savingFailed = false; }
                catch (Exception)
                {
                    savingFailed = true; Show("Saving needs attention", "Practice is paused. Your previous saved copy is retained.", Welcome);
                    view.Button("RETRY SAVE", () => Guard(() => { repository.SaveSession(session); savingFailed = false; view.Hide(); shell.ResumeResearchGame(); }), true);
                    view.Button("END SESSION", () => EndSession(RecordState.Interrupted, 0, true));
                }
            }
        }
        TrialSample ReadSample(double now)
        {
            var app = shell.experience;
            var sample = new TrialSample { now = now, capturedAt = now, sequence = ++demoSequence,
                source = "Keyboard demo", shape = "Demo grip", calibrationRevision = "demo-1", unit = InputUnit.Newtons,
                value = collecting || (liveSession && fatigueSafety?.State == "warning") ?
                    (Input.GetKey(KeyCode.Space) ? app.settings.demoForceNewtons : 0) : app.ForceNewtons,
                valid = true, synthetic = true };
            bool handValid = app.vision.TryGetHand(app.LeftSelected ? HandSide.Left : HandSide.Right, now, out var hand);
            sample.handValid = handValid;
            if (handValid)
            {
                var frame = app.LeftSelected ? app.vision.LatestLeft : app.vision.LatestRight;
                sample.closure = HandKinematics.GripScore(frame);
                var wrist = hand.Landmarks[0]; var velocity = hadWrist ? (wrist - previousWrist) / .1f : Vector3.zero;
                sample.speed = velocity.magnitude; sample.jitter = hadWrist ? (velocity - previousVelocity).magnitude : 0;
                var points = frame.landmarks;
                float flexion = 0; int joints = 0;
                foreach (int first in ResearchFingerStarts)
                    for (int j = first + 1; j <= first + 2; j++) {
                        var a = points[j-1]-points[j]; var b = points[j+1]-points[j];
                        if(a.sqrMagnitude>1e-8f && b.sqrMagnitude>1e-8f){flexion += 180-Vector3.Angle(a,b);joints++;}
                    }
                sample.romValid=joints==10; sample.romDegrees=joints>0?flexion/joints:0;
                previousWrist = wrist; previousVelocity = velocity; hadWrist = true;
            }
            else hadWrist = false;
            if (app.CameraMode)
            {
                sample.source = app.LeftSelected ? "Camera / left" : "Camera / right"; sample.unit = InputUnit.ClosureRatio;
                sample.synthetic = false; sample.value = sample.closure; sample.valid = handValid;
                sample.capturedAt = hand.CapturedAt; sample.sequence = hand.Sequence; sample.shape = app.LeftSelected ? "Left hand" : "Right hand";
                sample.calibrationRevision = "camera-1";
            }
            else if (app.Hardware)
            {
                sample.valid = app.forceReceiver.State.TryGetSample(now, out var force);
                sample.source = force.Synthetic ? "Synthetic replay" : "Phase 2"; sample.synthetic = force.Synthetic;
                sample.value = force.Newtons; sample.capturedAt = force.MeasuredAt; sample.sequence = force.Sequence;
                sample.shape = force.Shape.ToString(); sample.calibrationRevision = force.CalibrationRevision.ToString();
            }
            return sample;
        }
        void OnApplicationQuit()
        { RevokeSensing(); if (liveSession) EndSession(RecordState.Interrupted, 0, false); }
    }
}
