# Voice commands and spelling: Stage 13 (2026-10-27)

**Code:**
- `Assets/Rehab/3.4-game-modules/VoiceCommandRouter.cs`: grammar, state rules, confirmation and the spelling buffer. It is Unity-free and tested offline.
- `RehabVoiceInput.cs`: the Windows recognizer, push-to-talk, the actions and the feedback.

**Checks:**
- `Tools/Offline/VoiceTests.cs`
- `Tools/run_voice_probe.py`

## 1. Investigation on this computer (V-01, Task 13.1)

| Item | Found | How |
|---|---|---|
| Speech recognizer | Microsoft Speech Recognizer 8.0, en-US, on the device | `System.Speech` installed recognizers |
| Text-to-speech voices | Microsoft David and Zira, en-US | `System.Speech` installed voices |
| Microphone | Realtek(R) Audio, status OK | Windows audio endpoints |
| Microphone permission | Allowed for the user, desktop apps and the machine | Windows privacy consent store |
| Online speech recognition | On | Windows speech privacy setting |
| Unity keyword recognizer in the built player | Starts (`IsRunning`) on the Realtek microphone | Voice probe, platform check (no speech judged) |

**Finding:**
- Windows free **dictation** is "performed by a remote web service" and needs a network connection, so it is not offline. The source is Microsoft Learn, Speech recognition, Predefined grammars.
- Unity's `DictationRecognizer` uses that service: it fails without the speech privacy consent (0x80045509).
- Keyword lists are recognised on the device.
- On 2026-10-27 you chose **offline spelling only**. Free dictation is not offered, and V-05 is recorded as Blocked: online-only.

## 2. Control (V-10, V-11)

| Setting (Research portal, then Settings) | Behaviour |
|---|---|
| VOICE COMMANDS: OFF (default) | No recognizer runs. The strip shows VOICE OFF |
| HOLD CTRL TO TALK | Speech counts only while Ctrl is held, or for 0.8 s after release |
| ALWAYS ON | Always listening. "stop listening" puts it to sleep, and only "start listening" wakes it |

- If there is no microphone, or speech is not supported or blocked, the strip shows VOICE UNAVAILABLE and the reason, and the setting is marked unavailable. Every other input keeps working.
- Ctrl was chosen because no game, menu or text key uses it.
- Voice runs alongside mouse, keyboard, hand pointer and camera; none of them changed. Voice is off by default, and the existing suites pass.
- Voice never produces grip force. There is no "squeeze" command, so no measured value can come from speech.

## 3. Command coverage (Gate 13)

Each command has its phrases, where it works, what it does (the same action as the mouse or keyboard), and how it was checked.

| Group | Say | Valid when | Does | Offline router test | Player probe |
|---|---|---|---|---|---|
| UI | next / down, previous / up | menu or paused | moves the selection (uGUI navigation) | ✅ | ✅ selection moved |
| UI | select / press | menu or paused | submits the selected control | ✅ | — |
| UI | open settings / settings | a settings or appearance button is visible | presses it | ✅ | — |
| UI | back / go back | a sub-page, or paused | presses Back or Home, or resumes | ✅ (refused on home) | ✅ refused on home |
| UI | confirm / yes, cancel / no | something is waiting | confirms or cancels | ✅ | ✅ |
| UI | help / what can I say | always | lists the phrases valid right now | ✅ | — |
| In game | move forward, move back, move left, move right (walk forward / back) | apartment, not paused, standing | walks through the player's controller for up to 4 s | ✅ | ✅ 1.64 m |
| In game | stop | always | stops walking (acts even at low confidence) | ✅ | ✅ 0.000 m drift |
| In game | look at object / look at it | apartment, object present | turns to the selected object | ✅ | ✅ 0.0° |
| In game | grab / pick up / select object | apartment, empty hand, object in reach | the same path as E | ✅ | ✅ |
| In game | release / let go / put down | holding | releases | ✅ | ✅ |
| In game | open inventory / inventory / options | apartment, not paused | opens the session options panel | ✅ | ✅ |
| In game | start bookshelf, start coffee table, start storage box, start simple meal, start cutting, start prepare and serve | apartment, no activity running | starts the activity | ✅ | ✅ bookshelf |
| Session | pause; resume / continue | apartment | pauses or resumes | ✅ | ✅ |
| Session | instructions | apartment | reads the current activity step, or replays onboarding | ✅ | ✅ "Step 1 of 1…" |
| Session | restart | during an activity | **asks to confirm**, then restarts | ✅ | ✅ |
| Session | main menu | not on the home page | **asks to confirm**, then goes home | ✅ | ✅ |
| Control | stop listening, start listening | always on | sleep and wake | ✅ | ✅ |
| Spelling | alpha … zulu, or a … z; space; delete / back space; clear; done / finished; confirm; cancel | a text field is selected | builds the text in a review buffer | ✅ | ✅ "Amro" |

## 4. Safety of the input (V-07, V-08, V-09)

- **Review before submit:**
  - Spelled letters go to a buffer, never straight into the field.
  - "done" shows `Is "Amro" right?`, and only "confirm" writes it.
  - "delete" and "clear" correct it, and the keyboard still works.
  - Names are capitalised, and length is capped at 40.
- **Passwords:** voice is refused in password and PIN fields.
- **Not a command:** refused with "Say help for the list".
- **Not valid now** (for example grab while holding, release with empty hands, move while paused, a second activity): refused with the reason.
- **Low confidence:**
  - The command is not acted on; the player is asked "Did you say …? Say confirm or cancel".
  - A confirmation expires after 6 s.
  - On confirm, the state is re-checked, so a stale request is refused.
- **Losing progress:** restart and main menu are always confirmed.
- **Rejected audio and silence:** ignored.
- **Feedback for every outcome:**
  - **Strip:** VOICE HEARD, NOT NOW, CONFIRM?, NOT CLEAR, ASLEEP or UNAVAILABLE.
  - **Message panel:** in the apartment, above the strip in the free band between the bottom cards; in menus, in the bottom margin. A check measures that it covers no visible text.
  - **Sound:** a short tone when heard, the error tone when refused, a chime when confirmation is needed.

## 5. What is not verified

- **Recognition accuracy with your voice and your room noise.** The probe feeds SYNTHETIC transcripts; it only checks that the real recognizer starts on this computer. Your microphone session is a human check, recorded only when you do it.
- **Push-to-talk timing.** The probe bypasses the Ctrl gate for its transcripts, so hold-to-talk needs your hands-on check.
- **Language.** The recognizer is en-US only. Arabic commands are not available: no ar-EG recognizer is installed.
