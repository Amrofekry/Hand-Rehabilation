using System;
using UnityEngine;

namespace GripRehab.Editor
{
    // Stage 4.3: every synthesised sound exists, is audible, never clips or holds NaN, and the
    // two loops start and end near silence so they repeat without a click.
    public static class RehabAudioVerification
    {
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

        static void Check(AudioClip clip, string name, bool loop)
        {
            if (clip == null || clip.samples < 100) throw new Exception(name + " is empty");
            var data = new float[clip.samples]; clip.GetData(data, 0);
            float peak = 0; foreach (var v in data) { if (float.IsNaN(v) || float.IsInfinity(v)) throw new Exception(name + " has invalid samples"); peak = Mathf.Max(peak, Mathf.Abs(v)); }
            if (peak < .02f || peak > 1f) throw new Exception(name + " peak " + peak + " is silent or clipping");
            if (loop && (Mathf.Abs(data[0]) > .02f || Mathf.Abs(data[data.Length - 1]) > .02f)) throw new Exception(name + " loop ends are not silent");
        }
    }
}
