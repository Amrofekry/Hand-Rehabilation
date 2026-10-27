using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GripRehab.Editor
{
    // Stage 4.3 / Stage 12: every synthesised sound exists, is audible, never clips or holds NaN, and the loops join
    // without a click. Stage 12 adds the material bank (every family x event, variant pools that differ, loudness
    // targets, balance between families), the gain rules and the mixer asset, and writes the loudness table.
    public static class RehabAudioVerification
    {
        public const string LoudnessPath = "Logs/audio-loudness.json";

        public static void Run()
        {
            var clips = RehabAudio.Synthesize(out var creak, out var room);
            foreach (RehabSound sound in Enum.GetValues(typeof(RehabSound)))
            {
                if (!clips.TryGetValue(sound, out var clip)) throw new Exception("Missing sound " + sound);
                Check(clip, sound.ToString(), false);
            }
            Check(creak, "creak loop", true);
            Check(room, "room loop", true);
            Debug.Log("[REHAB_AUDIO] PASS: " + clips.Count + " effects and 2 seamless loops synthesised, audible and unclipped");
        }

        public static void RunMaterialBank()
        {
            var bank = RehabSoundBank.Build();
            var rows = new List<string>();
            var impactDb = new Dictionary<SoundFamily, float>();
            foreach (SoundFamily f in Enum.GetValues(typeof(SoundFamily)))
                foreach (SoundEvent e in Enum.GetValues(typeof(SoundEvent)))
                {
                    if (!bank.clips.TryGetValue((f, e), out var set) || set.Length < 3) throw new Exception("Variant pool too small: " + f + " " + e);
                    float sum = 0;
                    for (int v = 0; v < set.Length; v++)
                    {
                        var data = Check(set[v], set[v].name, false);
                        for (int w = 0; w < v; w++) if (Similar(data, Samples(set[w]))) throw new Exception(set[v].name + " is identical to variant " + (w + 1));
                        var (rms, peak, seconds) = RehabSoundBank.Measure(set[v]);
                        if (peak > RehabSoundBank.Db(RehabSoundBank.PeakCeiling) + .05f) throw new Exception(set[v].name + " peak " + peak + " dBFS is over -1 dBFS");
                        if (rms > RehabSoundBank.TargetDb(e) + .5f) throw new Exception(set[v].name + " is louder than its target");
                        sum += rms;
                        rows.Add(Row(f.ToString(), e.ToString(), v + 1, rms, peak, seconds));
                    }
                    if (e == SoundEvent.Impact) impactDb[f] = sum / set.Length;
                }
            // Balance: no family's impacts are more than 9 dB apart (metal rings longer, so it is allowed to read quieter).
            float spread = impactDb.Values.Max() - impactDb.Values.Min();
            if (spread > 9) throw new Exception("Impact loudness spread " + spread.ToString("0.0") + " dB between families");
            foreach (var pair in bank.strain)
            {
                var data = Check(pair.Value, pair.Value.name, true);
                var (rms, peak, seconds) = RehabSoundBank.Measure(pair.Value);
                rows.Add(Row(pair.Key.ToString(), "StrainLoop", 1, rms, peak, seconds));
            }
            if (bank.strain.ContainsKey(SoundFamily.Metal) || bank.strain.ContainsKey(SoundFamily.Glass)) throw new Exception("Rigid families must not have a strain loop");
            var feedback = RehabAudio.Synthesize(out var creak, out var room);
            foreach (var pair in feedback) { var m = RehabSoundBank.Measure(pair.Value); rows.Add(Row("Feedback", pair.Key.ToString(), 1, m.rmsDb, m.peakDb, m.seconds)); }
            foreach (var loop in new[] { ("Creak", creak), ("RoomTone", room) }) { var m = RehabSoundBank.Measure(loop.Item2); rows.Add(Row("Loop", loop.Item1, 1, m.rmsDb, m.peakDb, m.seconds)); }

            // Gain rules: resting contacts are silent, harder hits are louder, a still squeeze fades, no immediate repeats.
            if (RehabSoundBank.ImpactGain(.05f, .3f) != 0) throw new Exception("Resting contact makes a sound");
            if (!(RehabSoundBank.ImpactGain(.4f, .3f) < RehabSoundBank.ImpactGain(1.5f, .3f))) throw new Exception("Impact gain does not rise with speed");
            if (!(RehabSoundBank.ImpactGain(1f, .02f) < RehabSoundBank.ImpactGain(1f, .8f))) throw new Exception("Impact gain does not rise with mass");
            if (RehabSoundBank.ImpactGain(20f, 5f) > 1) throw new Exception("Impact gain over 1");
            if (!(RehabSoundBank.StrainGain(.5f, 0) < RehabSoundBank.StrainGain(.5f, .3f))) throw new Exception("Strain does not follow the deformation rate");
            var random = new System.Random(1); int last = -1;
            for (int i = 0; i < 200; i++) { int pick = RehabSoundBank.PickVariant(4, last, random); if (pick == last || pick < 0 || pick > 3) throw new Exception("Variant repeated"); last = pick; }
            var expected = new Dictionary<(string, string), SoundFamily>
            {
                [("Steel", "Knife")] = SoundFamily.Metal, [("Porcelain", "Plate")] = SoundFamily.Glass, [("Wood", "Tray")] = SoundFamily.Wood,
                [("Paper and board", "Book 1")] = SoundFamily.Wood, [("Fabric", "Napkin")] = SoundFamily.Fabric, [("Sponge", "Sponge")] = SoundFamily.Soft,
                [("Ripe food", "Tomato")] = SoundFamily.Soft, [("ABS plastic", "Lid")] = SoundFamily.Plastic, [("Silicone", "Silicone cup")] = SoundFamily.Soft,
                [("Rubber", "Ball")] = SoundFamily.Soft, [("Glass", "Glass")] = SoundFamily.Glass,
                [(null, "Ballpoint pen")] = SoundFamily.Plastic, [(null, "Kettlebell")] = SoundFamily.Metal, [(null, "Coin")] = SoundFamily.Metal,
                [("Dense foam", "Dense foam block")] = SoundFamily.Soft, [("Concrete", "Concrete block")] = SoundFamily.Wood, [(null, "Paper and board")] = SoundFamily.Wood
            };
            foreach (var pair in expected)
                if (RehabSoundBank.Classify(pair.Key.Item1, pair.Key.Item2) != pair.Value) throw new Exception("Family of " + pair.Key + " should be " + pair.Value);

            System.IO.Directory.CreateDirectory("Logs");
            var json = new StringBuilder("{\n  \"generated\": \"" + DateTime.Now.ToString("s") + "\",\n  \"ceilingDbfs\": -1.0,\n  \"impactSpreadDb\": " + F(spread) + ",\n  \"rows\": [\n");
            json.Append(string.Join(",\n", rows)).Append("\n  ]\n}\n");
            System.IO.File.WriteAllText(LoudnessPath, json.ToString());
            Debug.Log("[REHAB_AUDIO] PASS: material bank " + bank.ClipCount + " clips, " + rows.Count + " rows, impact spread " + spread.ToString("0.0") + " dB -> " + LoudnessPath);
        }

        static string F(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string Row(string family, string sound, int variant, float rms, float peak, float seconds) =>
            "    {\"family\": \"" + family + "\", \"event\": \"" + sound + "\", \"variant\": " + variant + ", \"rmsDbfs\": " + F(rms) + ", \"peakDbfs\": " + F(peak) + ", \"seconds\": " + seconds.ToString("0.00", CultureInfo.InvariantCulture) + "}";

        static float[] Samples(AudioClip clip) { var d = new float[clip.samples]; clip.GetData(d, 0); return d; }
        static bool Similar(float[] a, float[] b)
        {
            int n = Mathf.Min(a.Length, b.Length); if (a.Length != b.Length) return false;
            double diff = 0, energy = 0; for (int i = 0; i < n; i++) { diff += (a[i] - b[i]) * (a[i] - b[i]); energy += a[i] * a[i]; }
            return diff < energy * .01;
        }

        static float[] Check(AudioClip clip, string name, bool loop)
        {
            if (clip == null || clip.samples < 100) throw new Exception(name + " is empty");
            var data = new float[clip.samples]; clip.GetData(data, 0);
            float peak = 0; foreach (var v in data) { if (float.IsNaN(v) || float.IsInfinity(v)) throw new Exception(name + " has invalid samples"); peak = Mathf.Max(peak, Mathf.Abs(v)); }
            if (peak < .02f || peak > 1f) throw new Exception(name + " peak " + peak + " is silent or clipping");
            if (loop)
            {
                // Seamless: the jump from the last sample back to the first is no bigger than ordinary steps inside the loop.
                float maxStep = 0; for (int i = 1; i < data.Length; i++) maxStep = Mathf.Max(maxStep, Mathf.Abs(data[i] - data[i - 1]));
                float seam = Mathf.Abs(data[0] - data[data.Length - 1]);
                if (seam > maxStep + 1e-4f) throw new Exception(name + " loop seam jump " + seam + " exceeds the largest in-loop step " + maxStep);
                // No silent dip at the loop point: the RMS around the seam stays within 6 dB of the whole loop.
                int w = Mathf.Min(data.Length / 8, 2205); double edge = 0, all = 0;
                for (int i = 0; i < w; i++) edge += data[i] * data[i] + data[data.Length - 1 - i] * data[data.Length - 1 - i];
                foreach (var v in data) all += v * v;
                double edgeRms = Math.Sqrt(edge / (2 * w)), allRms = Math.Sqrt(all / data.Length);
                if (edgeRms < allRms * .5) throw new Exception(name + " dips at the loop point (" + (edgeRms / allRms).ToString("0.00") + " of its level)");
            }
            return data;
        }
    }
}
