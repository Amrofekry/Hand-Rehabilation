using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GripRehab
{
    public static class RehabVisualQuality
    {
        public static void Configure(Camera camera)
        {
            camera.allowHDR = true;
            camera.farClipPlane = 40f;
            camera.fieldOfView = 56f;
            var data = camera.GetComponent<UniversalAdditionalCameraData>() ??
                       camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.volumeLayerMask = 1 << 0;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Grip Rehab restrained filmic grade";
            var tone = profile.Add<Tonemapping>();
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.ACES;

            var grade = profile.Add<ColorAdjustments>();
            grade.contrast.overrideState = true;
            grade.contrast.value = 7f;
            grade.saturation.overrideState = true;
            grade.saturation.value = -3f;

            var bloom = profile.Add<Bloom>();
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.15f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = .08f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = .55f;

            var vignette = profile.Add<Vignette>();
            vignette.intensity.overrideState = true;
            vignette.intensity.value = .11f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = .5f;

            var volume = new GameObject("3.1 Global Visual Grade").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }
    }
}
