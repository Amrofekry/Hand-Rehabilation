# Hand-first controls (2026-10-28)

**The rule:**
- The **camera hand** is the main input.
- **Voice** covers special or occasional actions.
- **Mouse and keyboard** stay as options and keep working alongside.

**Code:**
- `Assets/Rehab/3.2-vision/HandLocomotionPolicy.cs`: look, walk and actions. It is Unity-free and tested offline.
- `Assets/Rehab/3.4.1-force-object-interaction/Apartment/HandLocomotion.cs`: wiring, edge indicators and the controls hint.
- `VoiceCommandRouter.cs` and `RehabVoiceInput.cs`: voice.

**Setting:** Research portal → Settings → HAND LOOK & WALK (CAMERA). It is on by default.

## Gestures (camera mode)

| Hand | Does | Notes |
|---|---|---|
| Palm in the middle of the camera view | Nothing: a free work zone | Reach and grab without the view moving |
| Palm toward the left, right, top or bottom edge | The view turns that way; the further out, the faster | Eases in and out; capped at 75°/s turning and 45°/s up/down; thin bars at the screen edge show the turn |
| Point with the index finger | Walk forward where you look | Starts after 0.25 s and stops quickly when the hand changes |
| Fist, held 0.35 s | Grab the object at the centre of the view, or the station's object | Fires once until the hand opens |
| Open the hand | Release (place) the held object | Existing camera behaviour |
| Close the hand harder | Squeeze (force from grip score) | Existing camera behaviour |
| Twist the hand like a dial while holding an activity item | Turn the placement | 10° dead zone, 5° steps, at most ±180° |
| Pinch, held 0.8 s | Open the options panel | Then use the hand pointer (dwell) to choose a room, object or activity, or RESUME |

**Safety:** lost or low-confidence tracking, a menu or pause stops looking and walking at once.

## Every keyboard and mouse action, and its replacement

| Was | Camera hand | Voice | Status |
|---|---|---|---|
| Mouse look | Palm at the edges | "look at object" | ✅ |
| WASD walk | Point | "move forward / back / left / right", "stop" | ✅ |
| E, G grab | Fist held | "grab", "pick up" | ✅ |
| Q, E release | Open hand | "release", "let go" | ✅ |
| Space or click to squeeze | Close the hand (grip score) | — (force never comes from voice) | ✅ |
| TAB, Esc options or pause | Pinch held | "pause", "options", "resume" | ✅ |
| Mouse click in menus | Hand pointer (dwell) | "next", "previous", "select", "back" | ✅ (existing) |
| , and . turn placement | Twist while holding | "rotate left / right" | ✅ |
| K restart activity | Options → activity button (pointer) | "restart" (confirmed) | ✅ |
| N next activity | Options → activity buttons (pointer) | "next activity" | ✅ |
| F sit / stand | — | "sit down", "stand up" | ✅ voice (occasional) |
| R reset objects | — | "reset objects" | ✅ voice (occasional) |
| B input mode | Options panel button (pointer) | "camera mode", "keyboard mode" | ✅ |
| C recenter hand | — | "recenter hand" | ✅ voice (occasional) |
| H guide | — | "instructions" | ✅ voice |
| Modules 2–3 (memory 1–4, maze arrows, assembly, tracing) | Hand pointer play (existing): choices, maze buttons, pick and drop, tracing | "select" | ✅ (existing) |
| 1–5, 0, J, X, Z, arrows, PageUp/Down, + and − | Not needed: in camera mode your own fingers move the hand | — | Keyboard demo only |
| V camera preview | — | — | Debug only |

## Checks

- **Offline `HandLocomotionTests` (32):**
  - the work zone and edges;
  - speed rising and capped;
  - easing;
  - walk dwell, stop and fist;
  - lost tracking, low confidence and bad data;
  - fist grab once and re-arm;
  - pinch;
  - twist dead zone, steps and limits.
- **Offline `VoiceTests` (165):** includes the new special-action commands.
- **`Tools/run_handlook_probe.py`, 21 checks:** rendered player, SYNTHETIC camera frames over the real vision port.
  - Turning: right +133°, left −118° in 2 s, up −44°.
  - Walking: 1.72 m in 2 s, stopping at 0.000 m, no walking with a fist.
  - Fist grab, hold and open-hand release.
  - Pinch opens the options panel.
  - Twisting turns the placement +60° and −60°.
  - Lost tracking, pause, setting off and keyboard mode stop it.
  - The controls hint shows.
- **Not verified:** how it feels with your real hand and camera (comfort, speed, reach). That is your check, recorded separately.
