using GripRehab.Product;
using UnityEngine;

namespace GripRehab
{
    [CreateAssetMenu(menuName = "Grip Rehab/Research Guidance Settings")]
    public sealed class ResearchGuidanceSettings : ScriptableObject
    {
        [Tooltip("Unvalidated research settings. This asset is not a clinician prescription.")]
        public GuidanceLimits limits = new GuidanceLimits();
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
