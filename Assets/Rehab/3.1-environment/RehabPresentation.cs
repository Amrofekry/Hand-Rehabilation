using GripRehab.Product;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GripRehab
{
    /// <summary>
    /// Stage 4.1/4.2: runtime presentation layer, so the protected saved scene stays untouched.
    /// Adds realtime reflection probes at the stations, warms the existing grade, applies the
    /// chosen hand tone and drives liquid and round sounds and starts the Stage 12 material sounds. Visual and audio
    /// feedback only: nothing here changes physics, input or any recorded value.
    /// </summary>
    public sealed class RehabPresentation : MonoBehaviour
    {
        const string HandToneKey = "GripRehab.HandTone";
        public static readonly string[] HandToneNames = { "GLOVE", "LIGHT", "MEDIUM", "DEEP" };
        static readonly Color[] HandTones = { new Color(.035f, .34f, .5f), new Color(.93f, .76f, .64f), new Color(.76f, .55f, .41f), new Color(.42f, .28f, .2f) };
        static RehabPresentation instance;

        ApartmentExperience app;
        RehabAppShell shell;
        Material handMaterial;
        float nextSquish;
        ForceRoundSession lastRounds; int lastRound, lastBreaks;
        public int ProbeCount { get; private set; }
        public static RehabPresentation Instance => instance;

        public static int HandTone
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(HandToneKey, 2), 0, HandTones.Length - 1);
            set { PlayerPrefs.SetInt(HandToneKey, Mathf.Clamp(value, 0, HandTones.Length - 1)); if (instance != null) instance.ApplyHandTone(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var experience = FindAnyObjectByType<ApartmentExperience>();
            if (experience == null || instance != null) return;
            instance = new GameObject("Rehab presentation").AddComponent<RehabPresentation>();
            instance.app = experience; instance.shell = FindAnyObjectByType<RehabAppShell>();
            ApartmentSounds.Ensure(experience); // Stage 12: material, impact, strain and fracture sounds
        }

        void Start()
        {
            AddReflectionProbes();
            WarmGrade();
            SoftenCandles();
            ApplyHandTone();
        }

        // Stage 15A (2026-11-01): each table candle's point light sits about 5 cm inside its glass holder, so the glass blows
        // out past the bloom threshold and lights hot spots right where the living-room activities are placed. Dimming did not
        // remove it (the distance is the cause), so these two tiny lights are off; the room's bounce lights light the table.
        void SoftenCandles()
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Point && light.range < 1f && InCandle(light.transform))
                    light.enabled = false;
        }

        // Any ancestor named like a candle (the kit spells its group "CandelSetting"), so renaming the flame object does not
        // bring the glare back.
        static bool InCandle(Transform t)
        {
            for (int depth = 0; t != null && depth < 4; t = t.parent, depth++)
            {
                var n = t.name.ToLowerInvariant();
                if (n.Contains("candle") || n.Contains("candel")) return true;
            }
            return false;
        }

        void AddReflectionProbes()
        {
            if (app.seats == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return; // headless runs have no renderer
            foreach (var seat in app.seats)
            {
                if (seat == null) continue;
                var probe = new GameObject("Station reflections").AddComponent<ReflectionProbe>();
                probe.transform.position = seat.transform.position + Vector3.up * 1.1f;
                probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                probe.size = new Vector3(7, 3.4f, 7); probe.resolution = 128; probe.hdr = true; probe.boxProjection = true;
                probe.importance = 1; probe.RenderProbe(); ProbeCount++;
            }
        }

        void WarmGrade()
        {
            foreach (var volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (!volume.isGlobal || volume.sharedProfile == null) continue;
                var profile = volume.profile; // runtime copy; the authored asset is unchanged
                if (!profile.TryGet(out WhiteBalance white)) white = profile.Add<WhiteBalance>(true);
                white.temperature.Override(6); white.active = true;
                if (profile.TryGet(out ColorAdjustments colour)) { colour.saturation.Override(6); colour.postExposure.Override(.1f); }
                if (profile.TryGet(out Bloom bloom)) bloom.threshold.Override(1.3f); // calmer candle glow
            }
        }

        public void ApplyHandTone()
        {
            var renderer = app != null && app.virtualGrasp != null ? app.virtualGrasp.outputMesh : null;
            if (renderer == null) return;
            if (handMaterial == null) { handMaterial = new Material(renderer.sharedMaterial) { name = "Hand tone (runtime)" }; renderer.sharedMaterial = handMaterial; }
            var tone = HandTones[HandTone];
            if (handMaterial.HasProperty("_BaseColor")) handMaterial.SetColor("_BaseColor", tone);
            if (handMaterial.HasProperty("_Color")) handMaterial.SetColor("_Color", tone);
            if (handMaterial.HasProperty("_Smoothness")) handMaterial.SetFloat("_Smoothness", HandTone == 0 ? .55f : .32f);
        }

        void Update()
        {
            if (app == null) return;
            var held = app.Held;
            // Stage 12: the held object's material strain loop (ApartmentSounds) replaces the single creak; rigid objects stay silent.
            RehabAudio.SetSqueeze(0);
            RehabAudio.SetGameplay((shell == null || !shell.BlocksGameplay) && (app.player == null || !app.player.Paused)); // menus, portal and pause are calmer
            if (held != null && held.Liquid != null && held.Compression > held.profile.liquidStartCompression && Time.unscaledTime >= nextSquish)
            { RehabAudio.Play(RehabSound.Squish, Random.Range(.9f, 1.1f)); nextSquish = Time.unscaledTime + .45f; }
            // Module 1 rounds: chime per finished round, fanfare at the end. Over-squeeze breaks sound from the object itself (ObjectSound).
            var rounds = shell != null && shell.Research != null ? shell.Research.ForceRounds : null;
            if (rounds != lastRounds)
            {
                if (lastRounds != null && lastRounds.Complete) RehabAudio.Play(RehabSound.Complete);
                lastRounds = rounds; lastRound = rounds != null ? rounds.RoundIndex : 0; lastBreaks = rounds != null ? rounds.Breaks : 0;
            }
            else if (rounds != null)
            {
                if (rounds.RoundIndex > lastRound) RehabAudio.Play(RehabSound.Chime);
                lastBreaks = rounds.Breaks; lastRound = rounds.RoundIndex;
            }
        }
    }
}
