# Hand-first controls (2026-10-28)

**The rule:**
- The **camera hand** is the main input.
- **Voice** covers special or occasional actions.
- **Mouse and keyboard** stay as options and keep working alongside.

**Code:**
- `Assets/Rehab/3.2-vision/HandLocomotionPolicy.cs`: look, walk and actions. It is Unity-free and tested offline.
- `Assets/Rehab/3.4.1-force-object-interaction/Apartment/HandLocomotion.cs`: wiring, edge indicators and the controls hint.
- `VoiceCommandRouter.cs` and `RehabVoiceInput.cs`: voice.

**Settings (Research portal → Settings):**
- HAND LOOK & WALK (CAMERA): on by default.
- HAND REACH: LOW / NORMAL / HIGH.
- HAND STEADINESS: RESPONSIVE / BALANCED / STEADY.
- AIM ASSIST: ON (default) / OFF.
- RECALIBRATE CAMERA HAND: also the C key or voice "recenter hand".

The input strip shows the camera state: TRACKING / READY / HOLDING / LOST. Measurements are in `Docs/HAND_CONTROL_REPORT.md`.

## Gestures (camera mode)

| Hand | Does | Notes |
|---|---|---|
| Move the hand left, right, up or down | The virtual hand moves to the same place on the screen and sits just above whatever is under it (automatic depth) | 2026-11-19, measured in the Hand Lab: a comfortable sweep (wrist x 0.12-0.88, y 0.50-0.95 of a laptop webcam image) covers the view; the resting hand (low in the image) maps to the view centre. Moving the hand toward or away from the camera adds at most 6 cm. A depth jump at an edge is followed only once it lasts (0.35 s away, 0.12 s toward) |
| Palm in the middle of the camera view | Nothing: a free work zone | Reach and grab without the view moving |
| Palm held at the far left, right, top or bottom edge for 0.5 s | The view turns that way; the further out, the faster | Eases in and out; capped at 75°/s turning and 45°/s up/down; thin bars at the screen edge show the turn. The 0.5 s hold (2026-11-19) stops a reach toward an object from turning the view |
| Point with the index finger | Walk forward where you look | Starts after 0.25 s and stops quickly when the hand changes |
| Close the hand (fist or pinch) | Grab: the object under the hand, else the object at your fingertips, else (aim assist) the looked-at object if the hand is within 12 cm of it | One grab path (2026-11-01; object under the hand first since 2026-11-19); the gate closes at score .55 and opens at .28; aim-assisted grabs are counted separately. A pinch over an object, or a closed fist, never opens the options |
| Open the hand | Release (place) the held object | Hysteresis: a wobbling fist does not drop the object; a tracking gap under 0.6 s keeps it |
| Close the hand harder | Squeeze (force from grip score) | Existing camera behaviour |
| Twist the hand like a dial while holding an activity item | Turn the placement | 10° dead zone, 5° steps, at most ±180° |
| Roll the wrist while holding the teapot or the cup | Tip it: pour, or drink at the mouth | Tea activity (2026-11-18): the tilt follows the knuckle line's roll from its angle at the pickup (6° dead band); a teapot or cup never turns the placement |
| Raise the hand high while holding the cup | Bring the cup to the lips (the drink pose) | Then roll the wrist to drink; while holding the cup, a raised hand does not look up |
| OK sign (thumb on the index tip, other fingers out), held 0.8 s, with no object under the hand | Open the options panel | Since 2026-11-19 a fist with the thumb on the index tip no longer counts (a missed grab paused the game). Then use the hand pointer (dwell) to choose a room, object or activity, or RESUME |

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
| T tip the teapot or cup (tea) | Roll the wrist while holding it | "stop pouring" levels it (voice never pours) | ✅ |
| M cup to the mouth (tea) | Raise the hand while holding the cup | — | ✅ |
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

- **Offline `HandLocomotionTests` (53, 2026-11-01):** adds the hold rule (old vs new), the interaction state machine, One Euro and the profile; earlier checks:
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

## Tea activity (living room, 2026-11-18)
- **Pick up the teapot** by its handle (close the hand). It hangs upright from your hand, spout toward where you look.
- **Pour:** bring the spout over the cup with your hand, then roll the wrist. The more you tip, the faster it pours
  (Torricelli flow from the tea left in the pot). Tea that misses the cup splashes and spreads on the table.
- **Level the pot** to stop; fill up to the gold line. Put the teapot back on its mat (open the hand over it).
- **Pick up the cup** and raise your hand: the cup comes to your lips. Roll the wrist to sip; tipping faster than you
  can drink (about 40 ml/s) spills down. A shaking hand shakes the tea at any time, and can spill it over the rim.
- **Keyboard:** E pick up, look at the cup, hold T to tip, M cup to mouth, Q place. Voice: "start pouring tea", "stop pouring".
- **Measured** (game measures; tremor is a camera estimate, never clinical): fill and its error, tea kept in the cup,
  spills, pour time, flow and angle steadiness, drunk volume, and hand tremor amplitude and frequency while the hand
  holds still or pours.
