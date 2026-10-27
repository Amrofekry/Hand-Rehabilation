using System;
using System.Collections.Generic;
using UnityEngine;

namespace GripRehab
{
    /// <summary>Material sound families (Stage 12, S-03). Ceramic and porcelain use Glass; food, foam, sponge, rubber and gel use Soft.</summary>
    public enum SoundFamily { Wood, Metal, Glass, Plastic, Fabric, Soft }
    /// <summary>Object events with a sound. Strain is the loop driven by deformation rate while an object is squeezed.</summary>
    public enum SoundEvent { Impact, Grab, Release, Fracture, StressCrack, Cut }

    /// <summary>
    /// Stage 12: material sound bank. Every clip is synthesised in code (modal resonances plus filtered noise), so the
    /// game ships no third-party audio (S-10). Each family has a variant pool per event (S-04); each clip is
    /// normalised to a loudness target with a peak ceiling, so families stay balanced and nothing clips (S-07, S-09).
    /// Testable in edit mode; the loudness table comes from <see cref="Measure"/>.
    /// </summary>
    public static class RehabSoundBank
    {
        public const int Rate = 44100;
        /// <summary>Peak ceiling for every clip: -1 dBFS.</summary>
        public const float PeakCeiling = .891f;
        public const int ImpactVariants = 4, OtherVariants = 3;

        /// <summary>Loudness target (RMS of the audible part, dBFS) per event.</summary>
        public static float TargetDb(SoundEvent e) => e switch
        {
            SoundEvent.Impact => -20f, SoundEvent.Grab => -27f, SoundEvent.Release => -30f,
            SoundEvent.Fracture => -18f, SoundEvent.StressCrack => -26f, _ => -22f
        };
        public const float StrainLoopDb = -28f;

        public sealed class Bank
        {
            public readonly Dictionary<(SoundFamily, SoundEvent), AudioClip[]> clips = new Dictionary<(SoundFamily, SoundEvent), AudioClip[]>();
            /// <summary>Deformation loops; Metal and Glass have none because they do not visibly deform under a hand.</summary>
            public readonly Dictionary<SoundFamily, AudioClip> strain = new Dictionary<SoundFamily, AudioClip>();
            public int ClipCount { get { int n = strain.Count; foreach (var v in clips.Values) n += v.Length; return n; } }
        }

        /// <summary>Maps an object to its family: its material name first, then its own name, then its material class.</summary>
        public static SoundFamily Classify(string materialName, string objectName, MaterialClass? materialClass = null)
        {
            var family = ByWords(materialName) ?? ByWords(objectName);
            if (family.HasValue) return family.Value;
            return materialClass == MaterialClass.Flexible || materialClass == MaterialClass.Soft ? SoundFamily.Soft : SoundFamily.Plastic;
        }

        static SoundFamily? ByWords(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var text = name.ToLowerInvariant();
            if (Has(text, "silicone", "rubber")) return SoundFamily.Soft; // a silicone cup or rubber ball is soft whatever its shape
            if (Has(text, "steel", "metal", "iron", "knife", "pan", "can", "coin", "screw", "hammer", "kettlebell")) return SoundFamily.Metal;
            if (Has(text, "glass", "porcelain", "ceramic", "tumbler", "mug", "plate", "bowl", "cup")) return SoundFamily.Glass;
            if (Has(text, "fabric", "napkin", "cloth", "towel")) return SoundFamily.Fabric;
            if (Has(text, "foam", "sponge", "food", "tomato", "bread", "cucumber", "gel", "clay", "stress ball")) return SoundFamily.Soft;
            if (Has(text, "plastic", "abs", "straw", "pen", "phone", "usb", "card", "bottle")) return SoundFamily.Plastic;
            if (Has(text, "wood", "plank", "paper", "board", "book", "cracker", "spoon", "block", "tray", "shelf", "concrete", "stone", "brick")) return SoundFamily.Wood;
            if (Has(text, "ball")) return SoundFamily.Soft;
            return null;
        }
        static bool Has(string text, params string[] words) { foreach (var w in words) if (text.Contains(w)) return true; return false; }

        /// <summary>Volume 0..1 from the contact's normal speed (m/s) and the object's mass: silent below 0.12 m/s so resting and rolling contacts make no noise.</summary>
        public static float ImpactGain(float normalSpeed, float massKg)
        {
            if (normalSpeed < .12f) return 0;
            float speed = Mathf.Pow(Mathf.Clamp01(Mathf.InverseLerp(.12f, 2.5f, normalSpeed)), .6f);
            float mass = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(.005f, massKg) / .25f), .45f, 1.15f);
            return Mathf.Clamp01((.18f + .82f * speed) * mass);
        }

        /// <summary>Strain-loop level from compression (0..1) and its rate (per second): mostly the rate, so a still squeeze goes quiet.</summary>
        public static float StrainGain(float compression, float compressionRate) =>
            Mathf.Clamp01(.2f * Mathf.Clamp01(compression) + 3.5f * Mathf.Abs(compressionRate));

        /// <summary>Picks a variant that differs from the last one (no immediate repeats).</summary>
        public static int PickVariant(int count, int last, System.Random random)
        {
            if (count <= 1) return 0;
            int pick = random.Next(count - 1);
            return pick >= last && last >= 0 ? pick + 1 : pick;
        }

        public static Bank Build()
        {
            var bank = new Bank();
            foreach (SoundFamily f in Enum.GetValues(typeof(SoundFamily)))
                foreach (SoundEvent e in Enum.GetValues(typeof(SoundEvent)))
                {
                    int n = e == SoundEvent.Impact ? ImpactVariants : OtherVariants;
                    var set = new AudioClip[n];
                    for (int v = 0; v < n; v++)
                    {
                        var random = new System.Random(1000 * (int)f + 100 * (int)e + v + 20261027);
                        var data = Synth(f, e, v, random);
                        Normalise(data, TargetDb(e));
                        set[v] = Clip(f + " " + e + " " + (v + 1), data);
                    }
                    bank.clips[(f, e)] = set;
                }
            foreach (SoundFamily f in Enum.GetValues(typeof(SoundFamily)))
            {
                if (f == SoundFamily.Metal || f == SoundFamily.Glass) continue;
                var data = StrainLoop(f, new System.Random(7000 + (int)f));
                Normalise(data, StrainLoopDb);
                bank.strain[f] = Clip(f + " strain loop", data);
            }
            return bank;
        }

        // ---------- synthesis ----------
        // Modal synthesis: a struck object rings at a few resonant frequencies, each decaying exponentially.
        struct Mode { public float hz, decay, gain; public Mode(float h, float d, float g) { hz = h; decay = d; gain = g; } }

        static Mode[] Modes(SoundFamily f) => f switch
        {
            SoundFamily.Wood => new[] { new Mode(190, .07f, 1), new Mode(440, .05f, .7f), new Mode(760, .035f, .45f), new Mode(1180, .025f, .25f) },
            SoundFamily.Metal => new[] { new Mode(620, .55f, .8f), new Mode(1490, .42f, 1), new Mode(2810, .3f, .55f), new Mode(4420, .2f, .3f) },
            SoundFamily.Glass => new[] { new Mode(1850, .22f, 1), new Mode(3240, .16f, .65f), new Mode(5010, .1f, .4f), new Mode(6600, .07f, .2f) },
            SoundFamily.Plastic => new[] { new Mode(330, .035f, 1), new Mode(910, .028f, .6f), new Mode(1720, .02f, .35f) },
            SoundFamily.Fabric => new[] { new Mode(120, .03f, .4f) },
            _ => new[] { new Mode(110, .06f, 1), new Mode(240, .04f, .4f) }
        };

        static float NoiseShare(SoundFamily f) => f switch
        {
            SoundFamily.Fabric => .95f, SoundFamily.Soft => .45f, SoundFamily.Wood => .3f, SoundFamily.Plastic => .25f, _ => .12f
        };
        static float NoiseCutoff(SoundFamily f) => f switch
        {
            SoundFamily.Soft => 600, SoundFamily.Fabric => 2200, SoundFamily.Wood => 2500, SoundFamily.Plastic => 4000, _ => 7000
        };

        static float[] Synth(SoundFamily f, SoundEvent e, int variant, System.Random r)
        {
            switch (e)
            {
                case SoundEvent.Impact: return Strike(f, r, 1f, 1f);
                case SoundEvent.Grab: return Mix(Strike(f, r, .45f, .55f), Rustle(r, .07f, 1800, .5f));
                case SoundEvent.Release: return Mix(Rustle(r, .14f, 3000, .7f), Strike(f, r, .25f, .35f));
                case SoundEvent.StressCrack: return Crackle(f, r, .12f, 3);
                case SoundEvent.Cut: return Mix(Rustle(r, .16f, 5000, .6f), Strike(SoundFamily.Wood, r, .5f, .6f));
                default: return Fracture(f, r);
            }
        }

        /// <summary>A strike: modes detuned ±4% per variant, each with random phase, plus a filtered noise transient.</summary>
        static float[] Strike(SoundFamily f, System.Random r, float decayScale, float level)
        {
            var modes = Modes(f);
            float longest = 0; foreach (var m in modes) longest = Mathf.Max(longest, m.decay);
            float seconds = Mathf.Clamp(longest * decayScale * 5f, .08f, 1.8f);
            var data = new float[Mathf.CeilToInt(seconds * Rate)];
            float noise = NoiseShare(f), cutoff = NoiseCutoff(f);
            float a = Alpha(cutoff), lp = 0;
            var detune = new float[modes.Length]; var phase = new float[modes.Length];
            for (int k = 0; k < modes.Length; k++) { detune[k] = 1 + ((float)r.NextDouble() - .5f) * .08f; phase[k] = (float)r.NextDouble() * 6.283f; }
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate, s = 0;
                for (int k = 0; k < modes.Length; k++)
                    s += modes[k].gain * Mathf.Sin(6.2832f * modes[k].hz * detune[k] * t + phase[k]) * Mathf.Exp(-t / (modes[k].decay * decayScale));
                lp += a * (Noise(r) - lp);
                s = s * (1 - noise) + lp * 3f * noise * Mathf.Exp(-t / (.012f + .02f * decayScale));
                data[i] = s * Attack(t, .0015f) * level;
            }
            Fade(data, .01f);
            return data;
        }

        /// <summary>Filtered noise burst: skin on surface, a lift or a slice.</summary>
        static float[] Rustle(System.Random r, float seconds, float cutoff, float level)
        {
            var data = new float[Mathf.CeilToInt(seconds * Rate)];
            float a = Alpha(cutoff), lp = 0, hp = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate, x = (float)i / data.Length;
                lp += a * (Noise(r) - lp); hp = lp - hp * .2f;
                data[i] = hp * Mathf.Sin(Mathf.PI * x) * level;
            }
            return data;
        }

        /// <summary>A short train of clicks: a stress crack before something breaks.</summary>
        static float[] Crackle(SoundFamily f, System.Random r, float seconds, int clicks)
        {
            var data = new float[Mathf.CeilToInt(seconds * Rate)];
            for (int c = 0; c < clicks; c++)
            {
                var hit = Strike(f, r, .18f, .7f + .3f * (float)r.NextDouble());
                int at = (int)(r.NextDouble() * (data.Length * .6f));
                for (int i = 0; i < hit.Length && at + i < data.Length; i++) data[at + i] += hit[i];
            }
            Fade(data, .01f);
            return data;
        }

        static float[] Fracture(SoundFamily f, System.Random r)
        {
            switch (f)
            {
                case SoundFamily.Glass: // shatter: a hard strike, then many small high fragments
                {
                    var data = Pad(Strike(f, r, .8f, 1), .5f);
                    for (int k = 0; k < 14; k++)
                    {
                        var bit = Strike(f, r, .15f + .2f * (float)r.NextDouble(), .25f + .35f * (float)r.NextDouble());
                        int at = (int)(Rate * (.02f + .4f * (float)r.NextDouble() * (float)r.NextDouble()));
                        for (int i = 0; i < bit.Length && at + i < data.Length; i++) data[at + i] += bit[i] * (k % 2 == 0 ? 1.15f : .85f);
                    }
                    Fade(data, .02f); return data;
                }
                case SoundFamily.Wood: // snap: sharp noise, a low crack and a short splinter
                    return Mix(Strike(f, r, 1.4f, 1), Pad(Crackle(f, r, .2f, 4), .25f));
                case SoundFamily.Soft: // squelch / tear of food
                {
                    var data = new float[(int)(.32f * Rate)];
                    float a = Alpha(900), lp = 0;
                    for (int i = 0; i < data.Length; i++) { float t = i / (float)Rate; lp += a * (Noise(r) - lp); data[i] = lp * 3 * Attack(t, .01f) * Mathf.Exp(-t / .12f) * (1 + .7f * Mathf.Sin(6.2832f * 22 * t)); }
                    return Mix(data, Strike(f, r, .8f, .6f));
                }
                case SoundFamily.Fabric: return Mix(Rustle(r, .3f, 3500, 1), Crackle(f, r, .3f, 6));
                case SoundFamily.Metal: return Mix(Strike(f, r, 1.2f, 1), Pad(Crackle(SoundFamily.Plastic, r, .1f, 2), .2f));
                default: return Mix(Crackle(f, r, .18f, 6), Strike(f, r, 1, .8f)); // plastic crack
            }
        }

        /// <summary>A seamless deformation loop: the tail is crossfaded into the head so the loop point has no gap or click.</summary>
        static float[] StrainLoop(SoundFamily f, System.Random r)
        {
            float seconds = 1.6f, fade = .2f;
            int n = (int)(seconds * Rate), extra = (int)(fade * Rate);
            var raw = new float[n + extra];
            float cutoff = f switch { SoundFamily.Soft => 450, SoundFamily.Fabric => 2600, SoundFamily.Wood => 900, _ => 1400 };
            float wobbleHz = f switch { SoundFamily.Soft => 5f, SoundFamily.Fabric => 10f, SoundFamily.Wood => 3.75f, _ => 7.5f };
            float a = Alpha(cutoff), lp = 0, lp2 = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)Rate;
                lp += a * (Noise(r) - lp); lp2 += a * (lp - lp2);
                float grain = f == SoundFamily.Wood || f == SoundFamily.Plastic ? (r.NextDouble() < .0015 ? (float)(r.NextDouble() - .5) * 3 : 0) : 0;
                raw[i] = (lp2 * 4 + grain) * (.6f + .4f * Mathf.Sin(6.2832f * wobbleHz * t));
            }
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = i < extra ? raw[i] * ((float)i / extra) + raw[n + i] * (1 - (float)i / extra) : raw[i];
            return data;
        }

        // ---------- helpers ----------
        static float Noise(System.Random r) => (float)(r.NextDouble() * 2 - 1);
        static float Alpha(float cutoffHz) { float dt = 1f / Rate, rc = 1 / (6.2832f * cutoffHz); return dt / (rc + dt); }
        static float Attack(float t, float seconds) => t < seconds ? t / seconds : 1;
        static void Fade(float[] data, float seconds) { int n = Mathf.Min(data.Length, (int)(seconds * Rate)); for (int i = 0; i < n; i++) data[data.Length - 1 - i] *= (float)i / n; }
        static float[] Pad(float[] data, float seconds) { var o = new float[Mathf.Max(data.Length, (int)(seconds * Rate))]; Array.Copy(data, o, data.Length); return o; }
        static float[] Mix(float[] a, float[] b) { var o = new float[Mathf.Max(a.Length, b.Length)]; for (int i = 0; i < o.Length; i++) o[i] = (i < a.Length ? a[i] : 0) + (i < b.Length ? b[i] : 0); return o; }

        /// <summary>Scales to the RMS target, then limits the gain so the peak stays at or under -1 dBFS.</summary>
        public static void Normalise(float[] data, float targetDb)
        {
            var (rms, peak) = Levels(data);
            if (rms <= 0 || peak <= 0) return;
            float gain = Mathf.Min(Mathf.Pow(10, targetDb / 20) / rms, PeakCeiling / peak);
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }

        /// <summary>RMS of the audible part (samples within 40 dB of the peak, so tails do not dilute it) and the peak, both linear.</summary>
        public static (float rms, float peak) Levels(float[] data)
        {
            float peak = 0; foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak <= 0) return (0, 0);
            float floor = peak * .01f; double sum = 0; int count = 0;
            foreach (var v in data) if (Mathf.Abs(v) >= floor) { sum += v * v; count++; }
            return ((float)Math.Sqrt(sum / Math.Max(1, count)), peak);
        }

        public static float Db(float linear) => 20 * Mathf.Log10(Mathf.Max(1e-6f, linear));

        /// <summary>Peak and RMS (dBFS) and length of a clip, for the loudness table.</summary>
        public static (float rmsDb, float peakDb, float seconds) Measure(AudioClip clip)
        {
            var data = new float[clip.samples * clip.channels]; clip.GetData(data, 0);
            var (rms, peak) = Levels(data);
            return (Db(rms), Db(peak), clip.length);
        }

        static AudioClip Clip(string name, float[] data)
        {
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1, 1);
            var clip = AudioClip.Create("Rehab " + name, data.Length, 1, Rate, false); clip.SetData(data, 0);
            return clip;
        }
    }
}
