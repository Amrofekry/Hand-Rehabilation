using System;
using System.Collections.Generic;
using UnityEngine;

namespace GripRehab
{
    public enum RehabSound { Success, Error, Crack, Bump, Chime, Squish, Step }

    /// <summary>
    /// Stage 4.3: game feedback sounds (UI hover/click tones stay in GripUiAccessibility). Every sound is synthesised in code at start-up, so the game ships no third-party
    /// audio and needs no licence. Effects and ambience have separate volumes plus a master mute,
    /// stored per computer (not in patient records). Sounds are feedback only.
    /// </summary>
    public sealed class RehabAudio : MonoBehaviour
    {
        const int Rate = 44100;
        const string MuteKey = "GripRehab.Audio.Muted", EffectsKey = "GripRehab.Audio.Effects", AmbienceKey = "GripRehab.Audio.Ambience";
        static RehabAudio instance;
        readonly Dictionary<RehabSound, AudioClip> clips = new Dictionary<RehabSound, AudioClip>();
        AudioSource[] voices; int nextVoice;
        AudioSource creak, ambience;
        float creakTarget;

        public static bool Muted { get => PlayerPrefs.GetInt(MuteKey, 0) == 1; set { PlayerPrefs.SetInt(MuteKey, value ? 1 : 0); Apply(); } }
        public static float EffectsVolume { get => PlayerPrefs.GetFloat(EffectsKey, .7f); set { PlayerPrefs.SetFloat(EffectsKey, Mathf.Clamp01(value)); Apply(); } }
        public static float AmbienceVolume { get => PlayerPrefs.GetFloat(AmbienceKey, .35f); set { PlayerPrefs.SetFloat(AmbienceKey, Mathf.Clamp01(value)); Apply(); } }
        public static int ClipCount => instance != null ? instance.clips.Count : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("Rehab audio"); DontDestroyOnLoad(go);
            instance = go.AddComponent<RehabAudio>();
        }

        void Awake()
        {
            instance = this;
            var synthesised = Synthesize(out var creakClip, out var ambienceClip);
            foreach (var pair in synthesised) clips[pair.Key] = pair.Value;
            voices = new AudioSource[6];
            for (int i = 0; i < voices.Length; i++) voices[i] = Voice("Voice " + i, false);
            creak = Voice("Squeeze creak", true); creak.clip = creakClip; creak.volume = 0; creak.Play();
            ambience = Voice("Room tone", true); ambience.clip = ambienceClip; ambience.Play();
            Apply();
        }

        /// <summary>Builds every one-shot plus the two loops (squeeze creak, room tone). Testable in edit mode.</summary>
        public static Dictionary<RehabSound, AudioClip> Synthesize(out AudioClip creakLoop, out AudioClip roomLoop)
        {
            var result = new Dictionary<RehabSound, AudioClip>
            {
                [RehabSound.Step] = Tone("step", .07f, t => Sine(520, t) * Env(t, .004f, .06f) * .4f),
                [RehabSound.Success] = Tone("success", .55f, t => (Note(523.3f, t, 0) + Note(659.3f, t, .11f) + Note(784f, t, .22f)) * .45f),
                [RehabSound.Chime] = Tone("chime", .9f, t => (Sine(880, t) * .6f + Sine(1320, t) * .25f + Sine(1760, t) * .1f) * Env(t, .005f, .85f) * .5f),
                [RehabSound.Error] = Tone("error", .32f, t => (Sine(220, t) + Sine(233, t)) * Env(t, .005f, .3f) * .35f),
                [RehabSound.Bump] = Tone("bump", .16f, t => (Sine(95 - 200 * t, t) + Noise() * .25f) * Env(t, .002f, .15f) * .7f),
                [RehabSound.Crack] = Tone("crack", .38f, t => (Noise() * Env(t, .001f, .09f) + Noise() * .4f * Env(t - .11f, .001f, .07f) + Sine(140, t) * Env(t, .002f, .2f) * .5f) * .7f)
            };
            var squishFilter = 0f;
            result[RehabSound.Squish] = Tone("squish", .3f, t => { squishFilter += (Noise() - squishFilter) * .08f; return squishFilter * 3f * Env(t, .02f, .26f) * (1 + .6f * Sine(18, t)); });
            var creakFilter = 0f;
            creakLoop = Tone("creak", 1.2f, t => { creakFilter += (Noise() - creakFilter) * .03f; return creakFilter * 2.2f * (.6f + .4f * Sine(7, t)) * Seam(t, 1.2f); });
            float b0 = 0, b1 = 0, b2 = 0;
            roomLoop = Tone("room tone", 4f, t => { float w = Noise(); b0 = .99765f * b0 + w * .099046f; b1 = .963f * b1 + w * .2965164f; b2 = .57f * b2 + w * 1.0526913f;
                return (b0 + b1 + b2 + w * .1848f) * .045f * Seam(t, 4f) + Sine(60, t) * .004f; });
            return result;
        }

        AudioSource Voice(string name, bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0; source.priority = loop ? 200 : 64;
            return source;
        }

        static void Apply()
        {
            if (instance == null) return;
            float effects = Muted ? 0 : EffectsVolume;
            foreach (var voice in instance.voices) voice.volume = effects;
            if (instance.ambience != null) instance.ambience.volume = Muted ? 0 : AmbienceVolume * .6f;
        }

        public static void Play(RehabSound sound, float pitch = 1)
        {
            if (instance == null || Muted || !instance.clips.TryGetValue(sound, out var clip)) return;
            var voice = instance.voices[instance.nextVoice]; instance.nextVoice = (instance.nextVoice + 1) % instance.voices.Length;
            voice.pitch = Mathf.Clamp(pitch, .5f, 2f); voice.PlayOneShot(clip);
        }

        /// <summary>0..1 squeeze level drives the creak loop while an object is held.</summary>
        public static void SetSqueeze(float level) { if (instance != null) instance.creakTarget = Mathf.Clamp01(level); }

        void Update()
        {
            if (creak == null) return;
            float target = Muted ? 0 : creakTarget * EffectsVolume * .5f;
            creak.volume = Mathf.MoveTowards(creak.volume, target, Time.unscaledDeltaTime * 2.5f);
            creak.pitch = .8f + creakTarget * .5f;
        }

        // ---------- synthesis helpers ----------
        static readonly System.Random random = new System.Random(20261024);
        static float Noise() => (float)(random.NextDouble() * 2 - 1);
        static float Sine(float hz, float t) => Mathf.Sin(2 * Mathf.PI * hz * t);
        static float Env(float t, float attack, float decay) => t < 0 ? 0 : t < attack ? t / attack : Mathf.Exp(-(t - attack) / Mathf.Max(1e-3f, decay / 4));
        static float Note(float hz, float t, float start) => t < start ? 0 : Sine(hz, t - start) * Env(t - start, .004f, .3f);
        static float Seam(float t, float length) => Mathf.Clamp01(Mathf.Min(t, length - t) / .05f); // click-free loop ends

        static AudioClip Tone(string name, float seconds, Func<float, float> wave)
        {
            int count = Mathf.CeilToInt(seconds * Rate); var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(wave(i / (float)Rate), -1, 1);
            var clip = AudioClip.Create("Rehab " + name, count, 1, Rate, false); clip.SetData(data, 0);
            return clip;
        }
    }
}
