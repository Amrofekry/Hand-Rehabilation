using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace GripRehab
{
    public enum RehabSound { Success, Error, Crack, Bump, Chime, Squish, Step, Placed, Complete }

    /// <summary>
    /// Stage 4.3 / Stage 12: game feedback sounds (UI hover/click tones stay in GripUiAccessibility). Every sound is synthesised in code at
    /// start-up, so the game ships no third-party audio and needs no licence. Feedback one-shots play flat (2D); material sounds
    /// from <see cref="RehabSoundBank"/> play as 3D sources at the object, with distance attenuation. All sources go through the
    /// GripRehabMixer asset (Master, SFX, Ambience, Voice) with a separate volume per group, stored per computer (not in patient
    /// records). Without the mixer asset it falls back to per-source volumes. Sounds are feedback only.
    /// </summary>
    public sealed class RehabAudio : MonoBehaviour
    {
        const int Rate = 44100;
        const string MuteKey = "GripRehab.Audio.Muted", EffectsKey = "GripRehab.Audio.Effects", AmbienceKey = "GripRehab.Audio.Ambience",
            MasterKey = "GripRehab.Audio.Master", VoiceKey = "GripRehab.Audio.Voice";
        public const string MixerResource = "Audio/GripRehabMixer";
        /// <summary>Ambience level in menus relative to gameplay (S-07: calmer menus, smooth transitions).</summary>
        public const float MenuAmbience = .45f, TransitionSeconds = 1.2f;
        static RehabAudio instance;
        readonly Dictionary<RehabSound, AudioClip> clips = new Dictionary<RehabSound, AudioClip>();
        RehabSoundBank.Bank bank;
        AudioSource[] voices; int nextVoice;
        AudioSource[] spatial; int nextSpatial;
        AudioSource creak, ambience, strain;
        AudioMixer mixer; AudioMixerGroup sfxGroup, ambienceGroup, voiceGroup;
        float creakTarget, strainTarget, ambienceMix = MenuAmbience, ambienceMixTarget = MenuAmbience;
        SoundFamily? strainFamily;
        readonly Dictionary<(SoundFamily, SoundEvent), int> lastVariant = new Dictionary<(SoundFamily, SoundEvent), int>();
        readonly System.Random variety = new System.Random(20261027);

        public static bool Muted { get => PlayerPrefs.GetInt(MuteKey, 0) == 1; set { PlayerPrefs.SetInt(MuteKey, value ? 1 : 0); Apply(); } }
        public static float MasterVolume { get => PlayerPrefs.GetFloat(MasterKey, 1f); set { PlayerPrefs.SetFloat(MasterKey, Mathf.Clamp01(value)); Apply(); } }
        public static float EffectsVolume { get => PlayerPrefs.GetFloat(EffectsKey, .7f); set { PlayerPrefs.SetFloat(EffectsKey, Mathf.Clamp01(value)); Apply(); } }
        public static float AmbienceVolume { get => PlayerPrefs.GetFloat(AmbienceKey, .35f); set { PlayerPrefs.SetFloat(AmbienceKey, Mathf.Clamp01(value)); Apply(); } }
        /// <summary>Spoken voice (assistant, Stages 13 and 15) volume; its group exists now so speech arrives already balanced.</summary>
        public static float VoiceVolume { get => PlayerPrefs.GetFloat(VoiceKey, .85f); set { PlayerPrefs.SetFloat(VoiceKey, Mathf.Clamp01(value)); Apply(); } }
        public static int ClipCount => instance != null ? instance.clips.Count + (instance.bank != null ? instance.bank.ClipCount : 0) : 0;
        public static bool HasMixer => instance != null && instance.mixer != null;
        public static AudioMixerGroup VoiceGroup => instance != null ? instance.voiceGroup : null;
        /// <summary>Last material sound for checks: family, event, gain and position.</summary>
        public static (SoundFamily family, SoundEvent sound, float gain, Vector3 at, int count) LastMaterialSound { get; private set; }
        public static float StrainLevel => instance != null && instance.strain != null ? instance.strain.volume : 0;
        public static float AmbienceMix => instance != null ? instance.ambienceMix : 0;
        /// <summary>An exposed mixer volume in dB, for checks; NaN without the mixer.</summary>
        public static float MixerDb(string parameter) => instance != null && instance.mixer != null && instance.mixer.GetFloat(parameter, out var db) ? db : float.NaN;
        /// <summary>Sources by output group name, for routing checks.</summary>
        public static int SourcesRoutedTo(string group) { int n = 0; if (instance != null) foreach (var s in instance.GetComponentsInChildren<AudioSource>()) if (s.outputAudioMixerGroup != null && s.outputAudioMixerGroup.name == group) n++; return n; }

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
            mixer = Resources.Load<AudioMixer>(MixerResource);
            if (mixer != null) { sfxGroup = Group("SFX"); ambienceGroup = Group("Ambience"); voiceGroup = Group("Voice"); }
            var synthesised = Synthesize(out var creakClip, out var ambienceClip);
            foreach (var pair in synthesised) clips[pair.Key] = pair.Value;
            bank = RehabSoundBank.Build();
            voices = new AudioSource[6];
            for (int i = 0; i < voices.Length; i++) voices[i] = Voice("Voice " + i, false, sfxGroup);
            creak = Voice("Squeeze creak", true, sfxGroup); creak.clip = creakClip; creak.volume = 0; creak.Play();
            ambience = Voice("Room tone", true, ambienceGroup); ambience.clip = ambienceClip; ambience.Play();
            spatial = new AudioSource[8];
            for (int i = 0; i < spatial.Length; i++) spatial[i] = Spatial("Object sound " + i, false);
            strain = Spatial("Strain loop", true); strain.volume = 0;
            Apply();
        }

        void Start() => Apply(); // AudioMixer.SetFloat is ignored during Awake

        AudioMixerGroup Group(string name) { var found = mixer.FindMatchingGroups(name); return found.Length > 0 ? found[0] : null; }

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
                [RehabSound.Crack] = Tone("crack", .38f, t => (Noise() * Env(t, .001f, .09f) + Noise() * .4f * Env(t - .11f, .001f, .07f) + Sine(140, t) * Env(t, .002f, .2f) * .5f) * .7f),
                // Stage 12: a placement is a short two-note confirmation; finishing an activity is a longer rising arpeggio, so the two are not confused.
                [RehabSound.Placed] = Tone("placed", .32f, t => (Note(659.3f, t, 0) + Note(987.8f, t, .07f)) * .38f),
                [RehabSound.Complete] = Tone("complete", 1.4f, t => (Note(523.3f, t, 0) + Note(659.3f, t, .12f) + Note(784f, t, .24f) + Note(1046.5f, t, .38f) * 1.1f
                    + (Sine(1046.5f, t) * .3f + Sine(1568f, t) * .12f) * Env(t - .38f, .01f, 1f)) * .34f)
            };
            var squishFilter = 0f;
            result[RehabSound.Squish] = Tone("squish", .3f, t => { squishFilter += (Noise() - squishFilter) * .08f; return squishFilter * 3f * Env(t, .02f, .26f) * (1 + .6f * Sine(18, t)); });
            var creakFilter = 0f;
            creakLoop = Loop("creak", 1.2f, t => { creakFilter += (Noise() - creakFilter) * .03f; return creakFilter * 2.2f * (.6f + .4f * Sine(7.5f, t)); });
            float b0 = 0, b1 = 0, b2 = 0;
            // Stage 12 fix: the room tone used to fade to silence at its loop point, an audible dip every 4 s. It is now crossfaded.
            roomLoop = Loop("room tone", 4f, t => { float w = Noise(); b0 = .99765f * b0 + w * .099046f; b1 = .963f * b1 + w * .2965164f; b2 = .57f * b2 + w * 1.0526913f;
                return (b0 + b1 + b2 + w * .1848f) * .045f + Sine(60, t) * .004f; });
            return result;
        }

        AudioSource Voice(string name, bool loop, AudioMixerGroup group)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0; source.priority = loop ? 200 : 64;
            source.outputAudioMixerGroup = group;
            return source;
        }

        AudioSource Spatial(string name, bool loop)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = 1; source.priority = 96;
            source.rolloffMode = AudioRolloffMode.Logarithmic; source.minDistance = .6f; source.maxDistance = 18f;
            source.dopplerLevel = 0; source.spread = 0; source.outputAudioMixerGroup = sfxGroup;
            return source;
        }

        static float ToDb(float linear) => linear <= .0001f ? -80f : 20f * Mathf.Log10(linear);

        static void Apply()
        {
            if (instance == null) return;
            float effects = Muted ? 0 : EffectsVolume, room = Muted ? 0 : AmbienceVolume * .6f * instance.ambienceMix;
            if (instance.mixer != null)
            {
                // Mixer path: groups carry the volumes, sources stay at full level (one-shots still scale per event).
                instance.mixer.SetFloat("MasterVolume", ToDb(MasterVolume));
                instance.mixer.SetFloat("SfxVolume", ToDb(effects));
                instance.mixer.SetFloat("AmbienceVolume", ToDb(Muted ? 0 : AmbienceVolume * .6f));
                instance.mixer.SetFloat("VoiceVolume", ToDb(VoiceVolume));
                effects = 1; room = instance.ambienceMix;
            }
            else { effects *= MasterVolume; room *= MasterVolume; }
            foreach (var voice in instance.voices) voice.volume = effects;
            foreach (var voice in instance.spatial) voice.volume = effects;
            if (instance.ambience != null) instance.ambience.volume = room;
        }

        static float SfxScale => instance != null && instance.mixer != null ? 1 : (Muted ? 0 : EffectsVolume * MasterVolume);

        public static void Play(RehabSound sound, float pitch = 1)
        {
            if (instance == null || Muted || !instance.clips.TryGetValue(sound, out var clip)) return;
            var voice = instance.voices[instance.nextVoice]; instance.nextVoice = (instance.nextVoice + 1) % instance.voices.Length;
            voice.pitch = Mathf.Clamp(pitch, .5f, 2f); voice.PlayOneShot(clip);
        }

        /// <summary>
        /// Plays a material sound at a world position (3D, distance-attenuated). Gain 0..1 comes from the physical event
        /// (contact speed, force, deformation rate); pitch varies ±5% and smaller objects sound higher.
        /// </summary>
        public static void PlayAt(SoundFamily family, SoundEvent sound, Vector3 position, float gain, float sizeMeters = .06f)
        {
            if (instance == null || instance.bank == null || Muted || gain <= .01f || !instance.bank.clips.TryGetValue((family, sound), out var set)) return;
            instance.lastVariant.TryGetValue((family, sound), out int last);
            int pick = RehabSoundBank.PickVariant(set.Length, instance.lastVariant.ContainsKey((family, sound)) ? last : -1, instance.variety);
            instance.lastVariant[(family, sound)] = pick;
            var source = instance.spatial[instance.nextSpatial]; instance.nextSpatial = (instance.nextSpatial + 1) % instance.spatial.Length;
            source.transform.position = position;
            float size = Mathf.Clamp(Mathf.Sqrt(.06f / Mathf.Max(.01f, sizeMeters)), .85f, 1.25f);
            source.pitch = size * (1 + ((float)instance.variety.NextDouble() - .5f) * .1f);
            source.PlayOneShot(set[pick], Mathf.Clamp01(gain));
            var previous = LastMaterialSound;
            LastMaterialSound = (family, sound, gain, position, previous.count + 1);
        }

        /// <summary>0..1 squeeze level drives the creak loop while an object is held (Module 1 stations).</summary>
        public static void SetSqueeze(float level) { if (instance != null) instance.creakTarget = Mathf.Clamp01(level); }

        /// <summary>Stage 12 (S-05): the held object's deformation loop, by family, from compression and its rate. Null family stops it.</summary>
        public static void SetStrain(SoundFamily? family, float compression, float compressionRate, Vector3 position)
        {
            if (instance == null || instance.strain == null || instance.bank == null) return;
            AudioClip clip = null;
            if (family.HasValue) instance.bank.strain.TryGetValue(family.Value, out clip);
            // Compression changes on physics steps, so the per-frame rate flickers to 0 between them: hold the peak and let Update decay it.
            instance.strainTarget = clip == null ? 0 : Mathf.Max(RehabSoundBank.StrainGain(compression, compressionRate), instance.strainTarget);
            instance.strain.transform.position = position;
            if (clip != null && instance.strain.clip != clip)
            {
                instance.strain.clip = clip; instance.strain.volume = 0; instance.strain.time = 0; instance.strain.Play();
            }
            instance.strainFamily = clip == null ? null : family;
            instance.strain.pitch = .85f + .35f * Mathf.Clamp01(compression);
        }

        /// <summary>Menus play a calmer ambience; gameplay brings it up. The change is a fade, never a cut (S-07).</summary>
        public static void SetGameplay(bool playing) { if (instance != null) instance.ambienceMixTarget = playing ? 1 : MenuAmbience; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (creak != null)
            {
                float target = Muted ? 0 : creakTarget * .5f * SfxScale;
                creak.volume = Mathf.MoveTowards(creak.volume, target, dt * 2.5f);
                creak.pitch = .8f + creakTarget * .5f;
            }
            if (strain != null)
            {
                float target = Muted || !strainFamily.HasValue ? 0 : strainTarget * .7f * SfxScale;
                strain.volume = Mathf.MoveTowards(strain.volume, target, dt * (target > strain.volume ? 8f : 3f));
                strainTarget = Mathf.MoveTowards(strainTarget, 0, dt * 1.2f); // decays unless the squeeze keeps changing
            }
            if (!Mathf.Approximately(ambienceMix, ambienceMixTarget))
            {
                ambienceMix = Mathf.MoveTowards(ambienceMix, ambienceMixTarget, dt * (1 - MenuAmbience) / TransitionSeconds);
                Apply();
            }
        }

        // ---------- synthesis helpers ----------
        static readonly System.Random random = new System.Random(20261024);
        static float Noise() => (float)(random.NextDouble() * 2 - 1);
        static float Sine(float hz, float t) => Mathf.Sin(2 * Mathf.PI * hz * t);
        static float Env(float t, float attack, float decay) => t < 0 ? 0 : t < attack ? t / attack : Mathf.Exp(-(t - attack) / Mathf.Max(1e-3f, decay / 4));
        static float Note(float hz, float t, float start) => t < start ? 0 : Sine(hz, t - start) * Env(t - start, .004f, .3f);

        static AudioClip Tone(string name, float seconds, Func<float, float> wave)
        {
            int count = Mathf.CeilToInt(seconds * Rate); var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(wave(i / (float)Rate), -1, 1);
            var clip = AudioClip.Create("Rehab " + name, count, 1, Rate, false); clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A seamless loop: rendered 0.25 s long and the tail crossfaded into the head, so the loop point has no gap or click.
        /// Loop frequencies are whole cycles of the length so the modulation also wraps.</summary>
        static AudioClip Loop(string name, float seconds, Func<float, float> wave)
        {
            int count = Mathf.CeilToInt(seconds * Rate), extra = Mathf.CeilToInt(.25f * Rate);
            var raw = new float[count + extra];
            for (int i = 0; i < raw.Length; i++) raw[i] = wave(i / (float)Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float mix = i < extra ? (float)i / extra : 1;
                data[i] = Mathf.Clamp(raw[i] * mix + (i < extra ? raw[count + i] * (1 - mix) : 0), -1, 1);
            }
            var clip = AudioClip.Create("Rehab " + name, count, 1, Rate, false); clip.SetData(data, 0);
            return clip;
        }
    }
}
