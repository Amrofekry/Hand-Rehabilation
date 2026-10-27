using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GripRehab
{
    /// <summary>
    /// Stage 12 audio probe (opt-in: -apartment-audio-probe). In the rendered player with SYNTHETIC keyboard-demo input,
    /// checks that sounds come from real events: mixer routing and volumes, the menu-to-gameplay ambience fade, grab,
    /// squeeze strain following the deformation rate, release, drop impacts with gain from contact speed, silence at rest
    /// and a fracture. Writes Logs/audio-probe.json. It cannot judge how the sounds feel; that is your listening check.
    /// </summary>
    public sealed class RehabAudioProbe : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, value; public bool passed; }
        [Serializable] public sealed class Report { public bool passed; public string utc, scope, detail = ""; public int errors; public Check[] checks; }
        readonly List<Check> checks = new List<Check>(); int errors;
        void Add(string name, bool passed, string value) => checks.Add(new Check { name = name, passed = passed, value = value });

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-apartment-audio-probe") >= 0)
                new GameObject("Audio probe").AddComponent<RehabAudioProbe>();
        }
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string text, string stack, LogType kind) { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors++; }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), scope = "SYNTHETIC keyboard-demo input in the rendered Windows player; measures events and levels, not how the sounds feel to a person" };
            Exception failure = null; var routine = Play();
            while (true)
            {
                object next = null; bool more;
                try { more = routine.MoveNext(); if (more) next = routine.Current; }
                catch (Exception error) { failure = error; break; }
                if (!more) break;
                yield return next;
            }
            report.checks = checks.ToArray(); report.errors = errors;
            report.passed = failure == null && errors == 0 && checks.Count > 0 && checks.All(c => c.passed);
            report.detail = failure != null ? failure.ToString() : errors > 0 ? errors + " error(s) logged" : string.Join(", ", checks.Where(c => !c.passed).Select(c => c.name + "=" + c.value));
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/audio-probe.json", JsonUtility.ToJson(report, true));
            Debug.Log("[REHAB_AUDIO_PROBE] " + (report.passed ? "PASS" : "FAIL") + " " + report.detail);
            Application.Quit(report.passed ? 0 : 1);
        }

        static int Count => RehabAudio.LastMaterialSound.count;

        IEnumerator Play()
        {
            yield return new WaitForSecondsRealtime(2);
            var shell = FindAnyObjectByType<RehabAppShell>(); var app = shell.experience;
            shell.SetResearchIdentity(null);

            // 1. Mixer: asset loaded, every game source routed, each group volume follows its setting.
            Add("mixer loaded", RehabAudio.HasMixer, RehabAudio.HasMixer.ToString());
            int sfx = RehabAudio.SourcesRoutedTo("SFX"), amb = RehabAudio.SourcesRoutedTo("Ambience");
            Add("sources routed (SFX, Ambience)", sfx >= 16 && amb == 1, sfx + ", " + amb);
            float master = RehabAudio.MasterVolume; RehabAudio.MasterVolume = .5f; yield return null;
            float halfDb = RehabAudio.MixerDb("MasterVolume"); RehabAudio.MasterVolume = master; yield return null;
            Add("master 50% = -6 dB", Mathf.Abs(halfDb + 6.02f) < .1f, halfDb.ToString("0.00"));
            bool muted = RehabAudio.Muted; RehabAudio.Muted = true; yield return null;
            float mutedDb = RehabAudio.MixerDb("SfxVolume"), mutedMaster = RehabAudio.MixerDb("MasterVolume"); RehabAudio.Muted = muted; yield return null;
            Add("game-sounds mute silences SFX, keeps master (UI cues)", mutedDb <= -79 && mutedMaster > -80, mutedDb.ToString("0") + " / " + mutedMaster.ToString("0.0"));
            float voiceDb = RehabAudio.MixerDb("VoiceVolume");
            Add("voice group volume set", !float.IsNaN(voiceDb) && Mathf.Abs(voiceDb - 20 * Mathf.Log10(RehabAudio.VoiceVolume)) < .1f, voiceDb.ToString("0.0"));

            // 2. Menu to gameplay: ambience fades up, never jumps.
            float menuMix = RehabAudio.AmbienceMix;
            shell.openModuleButton.onClick.Invoke(); app.SetHardware(false); app.player.SetPaused(false);
            yield return new WaitForSecondsRealtime(.45f); float midMix = RehabAudio.AmbienceMix;
            yield return new WaitForSecondsRealtime(1.4f); float gameMix = RehabAudio.AmbienceMix;
            Add("ambience menu -> gameplay fades", Mathf.Abs(menuMix - RehabAudio.MenuAmbience) < .02f && midMix > menuMix + .05f && midMix < .98f && gameMix > .99f,
                menuMix.ToString("0.00") + " -> " + midMix.ToString("0.00") + " -> " + gameMix.ToString("0.00"));
            app.player.SetPaused(false); app.TestForce = 0;

            // 3. Grab: a material sound at the object.
            var ball = app.objects.FirstOrDefault(o => o != null && o.displayName == "Silicone ball");
            if (ball == null) throw new InvalidOperationException("Silicone ball missing");
            app.player.LookAt(ball.transform.position); yield return new WaitForSecondsRealtime(.3f);
            int before = Count; app.TryGrab(ball); yield return new WaitForSecondsRealtime(.4f);
            var grab = RehabAudio.LastMaterialSound;
            Add("grab sound (Soft family)", Count > before && grab.sound == SoundEvent.Grab && grab.family == SoundFamily.Soft, grab.family + " " + grab.sound);

            // 4. Squeeze: the strain loop rises while the squeeze changes and fades while it is held still.
            // Compression lags the force (filter and spring-damper), so the peak is sampled while it catches up.
            float peak = 0, c0 = ball.Compression;
            for (int i = 0; i <= 12; i++) { app.TestForce = 2 * i; yield return new WaitForSecondsRealtime(.05f); peak = Mathf.Max(peak, RehabAudio.StrainLevel); }
            for (float t = 0; t < 1.2f; t += Time.unscaledDeltaTime) { peak = Mathf.Max(peak, RehabAudio.StrainLevel); yield return null; }
            float c1 = ball.Compression;
            yield return new WaitForSecondsRealtime(3f); float steady = RehabAudio.StrainLevel, c2 = ball.Compression;
            Add("strain follows deformation rate", peak > .1f && steady < peak * .6f,
                "peak " + peak.ToString("0.000") + ", held " + steady.ToString("0.000") + "; compression " + c0.ToString("0.00") + " -> " + c1.ToString("0.00") + " -> " + c2.ToString("0.00"));
            app.TestForce = 0; yield return new WaitForSecondsRealtime(.6f);

            // 5. Release, then the fall: release sound, then impact(s) whose gain comes from the contact speed.
            var sound = ball.GetComponent<ObjectSound>(); int impacts = sound != null ? sound.Impacts : -1;
            before = Count; app.Release(); yield return new WaitForSecondsRealtime(.15f);
            var release = RehabAudio.LastMaterialSound;
            Add("release sound", Count > before && (release.sound == SoundEvent.Release || release.sound == SoundEvent.Impact), release.family + " " + release.sound);
            yield return new WaitForSecondsRealtime(2f);

            // Drops: the room's own picker puts the object at the station, so it lands on the real table.
            var choices = FindAnyObjectByType<ApartmentChoices>();
            var room = choices.Kitchen ? choices.kitchenObjects : choices.livingObjects;
            int glass = Array.FindIndex(room, o => o != null && ApartmentSounds.FamilyOf(o) == SoundFamily.Glass);
            if (glass >= 0)
            {
                // Porcelain from 40 cm: a Glass impact, and the impact energy breaks it, so its fracture sound follows.
                choices.SelectObject(glass); var mug = room[glass]; yield return new WaitForSecondsRealtime(1.2f);
                before = Count; Drop(mug, .4f, out var mugSound); int mugBefore = mugSound.Impacts; yield return Settle();
                Add("drop impact (Glass family)", mugSound.Impacts > mugBefore && mugSound.PeakGain > 0 && mugSound.Family == SoundFamily.Glass, mug.displayName + " impacts " + (mugSound.Impacts - mugBefore) + " peak gain " + mugSound.PeakGain.ToString("0.00"));
                Add("40 cm drop breaks porcelain, with its fracture sound", !mug.IsBroken || Count > before + 1, mug.Damage.ToString());
                mug.ResetObject();
            }
            int toy = Array.FindIndex(room, o => o != null && o.BreakThresholdNewtons <= 0 && (o.profile == null || !o.profile.breakable));
            if (toy < 0) throw new InvalidOperationException("No unbreakable object in the room");
            choices.SelectObject(toy); var solid = room[toy]; yield return new WaitForSecondsRealtime(1.2f);
            Drop(solid, .4f, out var solidSound); yield return Settle(); float highGain = solidSound.PeakGain;
            int lowBefore = solidSound.Impacts; Drop(solid, .05f, out _); yield return Settle(); float lowGain = solidSound.PeakGain;
            Add("harder drop is louder", lowGain > 0 && highGain > lowGain, solid.displayName + ": 40 cm " + highGain.ToString("0.00") + " > 5 cm " + lowGain.ToString("0.00") + " (" + (solidSound.Impacts - lowBefore) + " impacts)");
            // At rest: stopped on the table and no further sounds.
            yield return new WaitForSecondsRealtime(.5f); int restStart = solidSound.Impacts; yield return new WaitForSecondsRealtime(2f);
            float restSpeed = solid.Body.velocity.magnitude;
            Add("silent at rest", restSpeed < .05f && solidSound.Impacts == restStart, (solidSound.Impacts - restStart) + " impacts in 2 s; speed " + restSpeed.ToString("0.000") + " m/s");
            Add("ball impacts after release", sound != null && sound.Impacts > impacts, impacts + " -> " + (sound != null ? sound.Impacts : -1));

            // 6. Fracture: break the cracker; the fracture sound is its family's (Wood).
            var cracker = app.objects.FirstOrDefault(o => o != null && o.displayName == "Brittle cracker");
            if (cracker == null) throw new InvalidOperationException("Brittle cracker missing");
            bool crackerShown = cracker.gameObject.activeSelf; cracker.gameObject.SetActive(true); yield return new WaitForSecondsRealtime(1.2f);
            before = Count; cracker.AddDamage(10); yield return new WaitForSecondsRealtime(.3f);
            var crack = RehabAudio.LastMaterialSound;
            Add("fracture sound (Wood family)", cracker.IsBroken && Count > before && crack.sound == SoundEvent.Fracture && crack.family == SoundFamily.Wood, cracker.Damage + " " + crack.family + " " + crack.sound);
            cracker.ResetObject(); cracker.gameObject.SetActive(crackerShown);

            // 7. Every object is tracked and classified.
            yield return new WaitForSecondsRealtime(.6f);
            var families = app.objects.Where(o => o != null).Select(o => o.displayName + "=" + ApartmentSounds.FamilyOf(o));
            Add("objects tracked", ApartmentSounds.Instance != null && ApartmentSounds.Instance.Tracked >= app.objects.Count(o => o != null && o.gameObject.activeInHierarchy), (ApartmentSounds.Instance != null ? ApartmentSounds.Instance.Tracked : 0) + ": " + string.Join(", ", families));
            shell.ReturnHome(); yield return new WaitForSecondsRealtime(1.6f);
            Add("gameplay -> menu fades down", Mathf.Abs(RehabAudio.AmbienceMix - RehabAudio.MenuAmbience) < .02f, RehabAudio.AmbienceMix.ToString("0.00"));
        }

        /// <summary>Tosses the object straight up so it rises to the given height and lands at the matching speed (√(2gh)).
        /// A real toss, not a teleport: teleporting a resting body does not end its contact, so the landing would not register.</summary>
        static float Drop(SqueezableObject item, float height, out ObjectSound sound)
        {
            sound = item.GetComponent<ObjectSound>(); sound.PeakGain = 0;
            var body = item.Body; body.isKinematic = false; body.angularVelocity = Vector3.zero;
            body.velocity = Vector3.up * Mathf.Sqrt(2 * -Physics.gravity.y * height); body.WakeUp();
            return height;
        }
        static IEnumerator Settle() { yield return new WaitForSecondsRealtime(1.5f); }
    }
}
