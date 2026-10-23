using System;
using UnityEngine;

namespace GripRehab
{
    [Flags]
    public enum FatigueSign { None = 0, Force = 1, Kinematic = 2, Facial = 4, Physiological = 8 }
    public enum ResearchSessionStatus { Active, WarningResumeOrQuit, SafetyStopped }

    [CreateAssetMenu(fileName = "ResearchPolicySettings", menuName = "Grip Rehab/Research policy settings")]
    public sealed class ResearchPolicySettings : ScriptableObject
    {
        [Tooltip("Research-demo thresholds only. Not a clinical prescription.")]
        public string version = "research-demo-1";
        public int minimumLevel = 1, maximumLevel = 5, initialLevel = 1;
        public float signPersistenceSeconds = 1.5f;
        public float stableWindowSeconds = 8f;
        public float cooldownSeconds = 10f;
        public float inputMaximumAgeSeconds = .5f;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(version) || minimumLevel < 1 ||
                initialLevel < minimumLevel || maximumLevel < initialLevel ||
                !Positive(signPersistenceSeconds) || !Positive(stableWindowSeconds) ||
                !Positive(cooldownSeconds) || !Positive(inputMaximumAgeSeconds))
                throw new ArgumentException("Invalid research policy settings");
        }

        static bool Positive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
    }

    public readonly struct ResearchObservation
    {
        public readonly double Now, CapturedAt;
        public readonly FatigueSign Signs;
        public readonly bool ForceValid, HandValid, ModelValid, StablePotential;

        public ResearchObservation(double now, double capturedAt, FatigueSign signs,
            bool forceValid, bool handValid, bool modelValid, bool stablePotential)
        {
            Now = now; CapturedAt = capturedAt; Signs = signs;
            ForceValid = forceValid; HandValid = handValid;
            ModelValid = modelValid; StablePotential = stablePotential;
        }
    }

    public readonly struct ResearchDecision
    {
        public readonly ResearchSessionStatus Status;
        public readonly int Level;
        public readonly FatigueSign ConfirmedSigns;
        public readonly string Reason, SettingsVersion;

        public ResearchDecision(ResearchSessionStatus status, int level,
            FatigueSign signs, string reason, string version)
        {
            Status = status; Level = level; ConfirmedSigns = signs;
            Reason = reason; SettingsVersion = version;
        }
    }

    // Deterministic, allocation-free hot path. Each bit is one independent
    // sensing category; multiple features within a category remain one sign.
    public sealed class ResearchAdaptivePolicy
    {
        readonly ResearchPolicySettings settings;
        readonly double[] signSince = { double.NaN, double.NaN, double.NaN, double.NaN };
        double lastAt = double.NegativeInfinity;
        double lastChangeAt = double.NegativeInfinity;
        double stableSince = double.NaN;
        bool lastInputValid;
        FatigueSign confirmed;

        public ResearchSessionStatus Status { get; private set; } = ResearchSessionStatus.Active;
        public int Level { get; private set; }

        public ResearchAdaptivePolicy(ResearchPolicySettings settings)
        {
            this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            Level = settings.initialLevel;
        }

        public ResearchDecision Step(ResearchObservation input)
        {
            if (double.IsNaN(input.Now) || double.IsInfinity(input.Now) || input.Now <= lastAt ||
                double.IsNaN(input.CapturedAt) || double.IsInfinity(input.CapturedAt) ||
                input.CapturedAt > input.Now || (input.Signs & ~(FatigueSign.Force |
                    FatigueSign.Kinematic | FatigueSign.Facial | FatigueSign.Physiological)) != 0)
                throw new ArgumentException("Invalid observation or clock");
            lastAt = input.Now;
            if (Status == ResearchSessionStatus.SafetyStopped) return Result("Safety stop latched");

            lastInputValid = input.ForceValid && input.HandValid && input.ModelValid &&
                input.Now - input.CapturedAt < settings.inputMaximumAgeSeconds;
            if (!lastInputValid)
            {
                stableSince = double.NaN;
                confirmed = FatigueSign.None;
                for (var i = 0; i < 4; i++) signSince[i] = double.NaN;
                return Result("Missing, invalid or stale input; no escalation");
            }

            confirmed = FatigueSign.None;
            var count = 0;
            for (var i = 0; i < 4; i++)
            {
                var sign = (FatigueSign)(1 << i);
                if ((input.Signs & sign) == 0) { signSince[i] = double.NaN; continue; }
                if (double.IsNaN(signSince[i])) signSince[i] = input.Now;
                if (input.Now - signSince[i] < settings.signPersistenceSeconds) continue;
                confirmed |= sign;
                count++;
            }
            if (count >= 2)
            {
                Status = ResearchSessionStatus.SafetyStopped;
                stableSince = double.NaN;
                return Result("Two independent persistent signs");
            }
            if (Status == ResearchSessionStatus.WarningResumeOrQuit)
                return Result("Waiting for explicit Resume or Quit");
            if (count == 1)
            {
                Status = ResearchSessionStatus.WarningResumeOrQuit;
                stableSince = double.NaN;
                return Result("One persistent sign");
            }
            if (!input.StablePotential || input.Signs != FatigueSign.None)
            {
                stableSince = double.NaN;
                return Result("Difficulty held: potential not stable");
            }
            if (double.IsNaN(stableSince)) stableSince = input.Now;
            if (input.Now - stableSince >= settings.stableWindowSeconds &&
                input.Now - lastChangeAt >= settings.cooldownSeconds &&
                Level < settings.maximumLevel)
            {
                Level++;
                stableSince = input.Now;
                lastChangeAt = input.Now;
                return Result("Difficulty increased after stable window");
            }
            return Result("Difficulty held");
        }

        public ResearchDecision Resume()
        {
            if (Status != ResearchSessionStatus.WarningResumeOrQuit || !lastInputValid ||
                confirmed != FatigueSign.None)
                throw new InvalidOperationException("Valid cleared inputs required to resume");
            Status = ResearchSessionStatus.Active;
            stableSince = double.NaN;
            lastChangeAt = lastAt;
            return Result("Resumed explicitly; cooldown restarted");
        }

        public ResearchDecision Quit()
        {
            Status = ResearchSessionStatus.SafetyStopped;
            stableSince = double.NaN;
            return Result("User quit; stop latched");
        }

        ResearchDecision Result(string reason) =>
            new ResearchDecision(Status, Level, confirmed, reason, settings.version);
    }
}
