using GripRehab.Product;
using UnityEngine;

namespace GripRehab
{
    [CreateAssetMenu(menuName = "Grip Rehab/Research Guidance Settings")]
    public sealed class ResearchGuidanceSettings : ScriptableObject
    {
        [Tooltip("Unvalidated research settings. This asset is not a clinician prescription.")]
        public GuidanceLimits limits = new GuidanceLimits();
        [Tooltip("Experimental, unvalidated research thresholds. Each sign is an independent modality.")]
        public FatigueSafetyConfig fatigue = new FatigueSafetyConfig();
        [Range(1.1f,2)] public float physiologicalRiseRatio = 1.25f;
        [Range(.65f,1)] public float facialConfidenceMinimum = .65f;
        [Range(.55f,1)] public float pulseQualityMinimum = .8f;
        [Range(10,60)] public int pulseReferenceWindows = 15;
        public FatigueSafetyConfig FatigueSnapshot()
        {
            var copy=JsonUtility.FromJson<FatigueSafetyConfig>(JsonUtility.ToJson(fatigue));
            copy.maximumLevel=limits.maximumLevel;return copy;
        }
        [Range(3, 5)] public float maximumHoldSeconds = 5;
        public GuidanceLimits Snapshot()
        {
            limits.Validate();
            return new GuidanceLimits { version = limits.version, minimumLevel = limits.minimumLevel, maximumLevel = limits.maximumLevel,
                persistence = limits.persistence, stableSeconds = limits.stableSeconds, cooldown = limits.cooldown,
                forceDecline = limits.forceDecline, jitterRatio = limits.jitterRatio, minimumJitter = limits.minimumJitter };
        }
    }
}
