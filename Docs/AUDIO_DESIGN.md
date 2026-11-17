# Audio and sound design: Stage 12 (2026-10-27)

**Scope:** game feedback sounds, material sounds of grabbable objects, ambience, the mixer and the audio settings.
**Code:**
- `Assets/Rehab/3.4-game-modules/RehabAudio.cs` (player, mixer, loops)
- `RehabSoundBank.cs` (material synthesis)
- `Assets/Rehab/3.4.1-force-object-interaction/Apartment/ApartmentSounds.cs` and `ObjectSound.cs` (event triggers)
- `Assets/Editor/RehabAudioMixerBuilder.cs` (mixer asset)
- `Assets/Resources/Audio/GripRehabMixer.mixer`

Every sound is synthesised in code. No recording or third-party sound is shipped (S-10).

## 1. Audit before Stage 12 (S-01)

| # | Finding | Kind | Fix |
|---|---|---|---|
| AU-1 | Grabbable objects made no sound when they hit something; dropping a mug and dropping a sponge were both silent | Missing | `ObjectSound`: impacts from the contact's normal speed and the object's mass |
| AU-2 | No grab or release sound | Missing | Grab and release sounds by material, at the object |
| AU-3 | One creak loop for every squeezed object, set by compression level only. It kept creaking at a constant squeeze, and it creaked on rigid porcelain | Wrongly triggered | Strain loop per material family, set mostly by the deformation **rate**, so it fades when the squeeze stops changing. Rigid families (metal, glass) are silent |
| AU-4 | One generic crack for every break, and the same crack played twice when a Module 1 round counted the break | Repetitive, duplicated | Fracture sound per family, played once from the object's damage state; stress cracks as damage rises |
| AU-5 | Every activity placement and the activity finish played the same "Success" arpeggio | Misleading | Placement uses a short two-note "Placed"; finishing uses a longer "Complete" |
| AU-6 | The room tone faded to silence at its loop point, an audible dip every 4 s; the creak loop did the same every 1.2 s | Badly timed | Loops are crossfaded tail-to-head; a check fails if the loop point dips |
| AU-7 | All sounds were flat (2D) | Missing | Material sounds are 3D, with logarithmic distance attenuation (0.6 m to 18 m) |
| AU-8 | Volumes were code-only per source; no mixer, no master or voice volume | Missing | GripRehabMixer: Master, SFX, Ambience and Voice, one setting each |
| AU-9 | Ambience was at the same level in menus and in play, and jumped when you entered or left | Badly timed | Ambience is at 45 % in menus, the portal and pause, and fades over 1.2 s |
| AU-10 | No cutting sound | Missing | A slice sound when the knife cuts food |

## 2. Material families (S-03)

The family comes from the object's material name first, then the object's name, then its material class.

| Family | Objects (examples) | Character |
|---|---|---|
| Wood | books, tray, wooden spoon, block, cracker, paper, concrete | Short low knock (modes 190 to 1180 Hz, 25 to 70 ms) |
| Metal | knife, pan, can, coin, screw, hammer, steel block | Bright, longer ring (620 to 4420 Hz, up to 0.55 s), quieter at the same loudness target |
| Glass | porcelain plate, cup, bowl and mug; glass tumbler | High clink (1.85 to 6.6 kHz) |
| Plastic | ABS box, bottle, card, pen, phone | Hollow tock |
| Fabric | napkin, cloth | Soft brushed thump |
| Soft | sponge, foam, food, rubber, silicone, gel, clay | Low damped thud with a filtered-noise body |

## 3. Event-to-sound map (Gate 12)

| Event | Trigger (real value) | Sound | Gain | Variation | Where |
|---|---|---|---|---|---|
| Object hits something | `OnCollisionEnter` (normal speed ≥ 0.12 m/s) or `OnCollisionStay` (≥ 0.25 m/s, for landings on a contact the physics engine kept alive); fastest of all contact points | Family **Impact** | `ImpactGain(speed, mass)`: rises with speed up to 2.5 m/s and with √mass; 0 below 0.12 m/s | 4 variants, no repeat, ±5 % pitch, smaller is higher | 3D at the contact point; at most 1 per object per 80 ms; quiet for 0.6 s after spawn |
| Grab | `ApartmentExperience.ObjectGrabbed` | Family **Grab** (short strike plus skin rustle) | 0.8 | 3 variants, ±5 % pitch | 3D at the object |
| Release | `ObjectReleased` | Family **Release** (lift swish plus light tick) | 0.7 | 3 variants | 3D at the object |
| Squeeze (deformation) | Held object's compression and its rate, every frame | Family **strain loop** (Soft, Wood, Plastic, Fabric) | `StrainGain = 0.2·compression + 3.5·|rate|`, peak-held (compression moves on physics steps) and decaying 1.2/s | Pitch 0.85 to 1.2 with compression | 3D at the held object; none for Metal and Glass |
| Damage rises | `SqueezableObject.Damage` goes up (not Broken) | Family **StressCrack** | 0.5 + 0.15 × level | 3 variants | 3D at the object |
| Break | `Damage` becomes Broken | Family **Fracture**: glass shatter, wood snap, plastic crack, food squelch, fabric tear, metal clank | 1 | 3 variants | 3D at the object; once |
| Knife cuts food | `CuttableFood.Cuts` goes up | **Cut** (slice plus board tap) | 0.9 | 3 variants | 3D at the food |
| Liquid squeezed out | Sponge compression above its liquid start | Squish (Stage 4) | 1 | ±10 % pitch | 2D |
| Activity item placed | Placement accepted | **Placed** (two notes) | 1 | none | 2D feedback |
| Activity finished, module finished, Module 1 rounds finished | Result recorded | **Complete** (rising arpeggio) | 1 | none | 2D feedback |
| Wrong placement, wrong answer | Rejected | Error (Stage 4) | 1 | none | 2D feedback |
| Round done, onboarding step | Step advances | Chime (Stage 4) | 1 | none | 2D feedback |
| UI hover and click | `GripUiControlFeedback` | Original tones (`GripUiAccessibility`) | UI volume | 150 ms hover cooldown | 2D, Master group |
| Room ambience | Always | Pink-noise room tone with a 60 Hz hum, 4 s seamless loop | 45 % in menus, 100 % in play, 1.2 s fade | none | 2D, Ambience group |

## 4. Mixer and settings (S-06 to S-08)

| Group | Sources | Setting (Research portal, then Settings) | Default |
|---|---|---|---|
| Master | everything, including UI cues | MASTER VOLUME | 100 % |
| SFX | feedback one-shots, material sounds, strain and creak loops | EFFECTS VOLUME, and GAME SOUNDS on or off | 70 % |
| Ambience | room tone | AMBIENCE VOLUME | 35 % |
| Voice | empty until Stages 13 and 15 (speech) | VOICE VOLUME | 85 % |

- Volumes map to the exposed mixer parameters in dB: 20·log10(v), and 0 is −80 dB.
- GAME SOUNDS off silences SFX and Ambience, but not the UI cues, which keep their own volume and mute.
- Without the mixer asset, the code falls back to per-source volumes.

## 5. Loudness (Gate 12)

How every clip is levelled:
- It is scaled to its loudness target: the RMS of the audible part, meaning samples within 40 dB of the peak.
- The peak is then limited to −1 dBFS.
- Targets: impact −20, grab −27, release −30, stress crack −26, cut −22, fracture −18, strain loops −28 dBFS.
- Every clip reaches its target, so a metal ring is no louder than a wood knock at the same event. The exception is the Soft fracture, which the peak ceiling holds 0.8 to 1.3 dB under its target.
- The full table (129 rows, each family, event and variant) is in `Docs/evidence/stage12-2026-10-27/audio-loudness.json`.

Mean RMS / highest peak (dBFS) per family and event, over all variants:

| Family | Impact | Grab | Release | StressCrack | Fracture | Cut | StrainLoop |
|---|---|---|---|---|---|---|---|
| Wood | -20.0 / -2.6 | -27.0 / -11.6 | -30.0 / -18.7 | -26.0 / -12.5 | -18.0 / -1.7 | -22.0 / -7.8 | -28.0 / -10.2 |
| Metal | -20.0 / -3.4 | -27.0 / -10.8 | -30.0 / -14.7 | -26.0 / -14.2 | -18.0 / -1.1 | -22.0 / -7.5 | — |
| Glass | -20.0 / -3.4 | -27.0 / -11.4 | -30.0 / -17.6 | -26.0 / -12.4 | -18.0 / -3.2 | -22.0 / -8.3 | — |
| Plastic | -20.0 / -3.9 | -27.0 / -12.5 | -30.0 / -18.3 | -26.0 / -11.0 | -18.0 / -4.0 | -22.0 / -8.5 | -28.0 / -13.3 |
| Fabric | -20.0 / -3.0 | -27.0 / -11.1 | -30.0 / -17.4 | -26.0 / -10.6 | -18.0 / -1.6 | -22.0 / -8.3 | -28.0 / -14.1 |
| Soft | -20.0 / -5.2 | -27.0 / -14.0 | -30.0 / -18.9 | -26.0 / -12.2 | -19.1 / -1.0 | -22.0 / -6.4 | -28.0 / -13.6 |

Feedback sounds and loops (Stage 4, unchanged levels): Step -19.4 / -8.2, Success -16.2 / -4.8, Chime -21.1 / -7.3, Error -17.1 / -3.4, Bump -14.3 / -1.5, Crack -16.0 / -0.4, Placed -17.3 / -6.0, Complete -20.5 / -8.0, Squish -15.6 / 0.0, Creak -19.5 / -5.0, RoomTone -22.2 / -10.6.

Impact spread between families: 0.0 dB.

These are digital levels of the clips, not a measured sound pressure level in a room. How loud it feels depends on your speakers and your settings.

## 6. Licensing (S-10)

- Everything is synthesised in code: modal resonances plus filtered noise for materials, sine notes for feedback, and pink noise for the room.
- No recording, sample library or download is used.
- New dependencies: none. The mixer asset is generated by the project's own editor script.
- The builder calls Unity's editor `AudioMixerController` by reflection. The source is UnityCsReference 2022.3, `Editor/Mono/Audio/Mixer/Bindings/AudioMixerController.cs`.

## 7. Checks

- **EditMode `MaterialSoundBank`:**
  - every family × event has a pool of at least 3, and the variants differ;
  - no NaN, no clipping, peak ≤ −1 dBFS, at or under the target;
  - impact balance across families is within 9 dB;
  - no strain loop for rigid families;
  - the gain rules hold (silent at rest, louder with speed and mass, strain follows rate), there are no immediate repeats, and the family map is right;
  - it writes the loudness table.
- **EditMode `SynthesisedAudio`:** feedback sounds, plus loops with no seam jump and no dip at the loop point.
- **EditMode `AudioMixerAsset`:** 4 groups and 4 exposed volumes.
- **`Tools/run_audio_probe.py`** (rendered player, SYNTHETIC input):
  - mixer routing and dB values;
  - mute behaviour;
  - the menu-to-gameplay fade and back;
  - grab, strain peak versus a held squeeze, and release;
  - a 40 cm drop of the porcelain mug gives a Glass impact and breaks it, with its fracture sound;
  - on the unbreakable ball, a 40 cm drop is louder than 5 cm, and once it has stopped it is silent;
  - fracture (Wood);
  - every active object tracked.
- **Not done:** listening by a person (S-09). How the sounds feel, whether anything is annoying, and the balance on real speakers are yours to judge. They are recorded as human-verified only after you listen.

## Tea sounds (2026-11-18)
Synthesised like every other game sound (ADR-AU-1), routed to the SFX group, 3D at the cup:
- **Pour:** a seamless loop of band-passed noise with a slow gurgle; its volume follows the flow and its pitch rises
  as the cup fills (the air column above the tea shortens, as with a real cup).
- **Sip:** a soft low swallow loop while tea goes to the lips.
Listening review (S-07, S-09) is still yours.
