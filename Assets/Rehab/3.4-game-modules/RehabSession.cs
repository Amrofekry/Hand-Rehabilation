using System;
using UnityEngine;

namespace GripRehab
{
    public enum TrainingMode { Force, Memory, Assembly, Tracing, Maze }
    public enum GripShape { Sphere = 1, Cylinder = 2, Pencil = 3 }
    public enum SessionState { Ready, Running, Paused, Complete }

    public readonly struct ForceReading
    {
        public readonly float Newtons;
        public readonly double Timestamp;
        public readonly bool Valid;
        public readonly bool Simulated;

        public ForceReading(float newtons, double timestamp, bool valid, bool simulated)
        {
            Newtons = newtons;
            Timestamp = timestamp;
            Valid = valid && !float.IsNaN(newtons) && !float.IsInfinity(newtons) && newtons >= 0;
            Simulated = simulated;
        }
    }

    public interface IForceSource
    {
        ForceReading Latest { get; }
    }

    // Exercise targets are demonstration settings. No clinical force limits are implied.
    public sealed class ForceHoldTask
    {
        public readonly float TargetNewtons;
        public readonly float ToleranceNewtons;
        public readonly float HoldSeconds;
        public float ProgressSeconds { get; private set; }
        public float PeakNewtons { get; private set; }
        public float ValidSeconds { get; private set; }
        public bool Complete => ProgressSeconds >= HoldSeconds;

        public ForceHoldTask(float targetNewtons, float toleranceNewtons, float holdSeconds)
        {
            if (targetNewtons <= 0 || toleranceNewtons <= 0 || holdSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetNewtons));
            TargetNewtons = targetNewtons;
            ToleranceNewtons = toleranceNewtons;
            HoldSeconds = holdSeconds;
        }

        public void Step(ForceReading reading, float deltaSeconds)
        {
            if (Complete || !reading.Valid || deltaSeconds <= 0) return;
            var step = Mathf.Min(deltaSeconds, 0.1f); // A stalled frame cannot earn a long hold.
            ValidSeconds += step;
            PeakNewtons = Mathf.Max(PeakNewtons, reading.Newtons);
            if (Mathf.Abs(reading.Newtons - TargetNewtons) <= ToleranceNewtons)
                ProgressSeconds = Mathf.Min(HoldSeconds, ProgressSeconds + step);
            else
                ProgressSeconds = Mathf.Max(0, ProgressSeconds - step * 0.5f);
        }

        public void Reset()
        {
            ProgressSeconds = 0;
            PeakNewtons = 0;
            ValidSeconds = 0;
        }
    }

    public sealed class SessionClock
    {
        public SessionState State { get; private set; } = SessionState.Ready;
        public float ElapsedSeconds { get; private set; }

        public void Reset() { ElapsedSeconds = 0; State = SessionState.Ready; }
        public void Start() { ElapsedSeconds = 0; State = SessionState.Running; }
        public void Pause() { if (State == SessionState.Running) State = SessionState.Paused; }
        public void Resume() { if (State == SessionState.Paused) State = SessionState.Running; }
        public void Complete() { State = SessionState.Complete; }
        public void Step(float deltaSeconds)
        {
            if (State == SessionState.Running && deltaSeconds > 0)
                ElapsedSeconds += Mathf.Min(deltaSeconds, 0.1f);
        }
    }
}

