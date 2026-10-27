# GripRehab visual system — 2026-09-30 checkpoint

The UI remains uGUI. `GripUiTheme` stores semantic colors, type sizes, spacing and motion tokens. `GripUiThemeController` recolors existing saved graphics and a small home Appearance panel offers Dark, Light and High Contrast. It does not change gameplay input, tracking, force or patient data. The controller keeps references to its graphics after startup and animates only when a theme changes using unscaled time.

| Token | Dark | Light | High contrast |
| --- | --- | --- | --- |
| Backdrop | `#071820` | `#E7F1EE` | `#000000` |
| Surface | `#142E36` | `#FFFFFF` | `#000000` |
| Dark surface | `#102A32` | `#FFFFFF` | `#000000` |
| Primary | `#167E6F` | `#075C56` | `#FFFF00` |
| Secondary | `#DDEBE7` | `#D9EAE5` | `#FFFFFF` |
| Accent | `#36C9A4` | `#006A5C` | `#FFFF00` |

## Calculated text contrast

| Pair | Ratio | Target |
| --- | ---: | --- |
| Dark-theme light text `#F5FBFA` on surface `#142E36` | 13.61:1 | AA/AAA body |
| Light text `#F5FBFA` on dark surface `#102A32` | 14.34:1 | AA/AAA body |
| Light text `#F5FBFA` on primary `#167E6F` | 4.72:1 | AA body |
| Light theme text `#102A32` on white | 15.01:1 | AA/AAA body |
| Light theme white on primary `#075C56` | 7.85:1 | AA/AAA body |
| Contrast black on yellow | 19.56:1 | AA/AAA body |

Ratios use WCAG sRGB relative luminance. Alpha overlays, anti-aliasing, rendered sprites and all legacy label/background pairs still require in-Editor visual checks before claiming whole-app WCAG compliance.

## Tokens and motion

- Base spacing grid: 8 px. Patient target token: 64 px; doctor target token: 44 px. The existing scene has smaller controls and has **not** passed a full target-size audit.
- Display 64, H1 34, H2 26, body 18, caption 14 at reference 1600×900. These tokens are available for subsequent prefab authorship; old scene labels retain their saved sizes.
- Theme crossfade: 280 ms SmoothStep using unscaled time. `reduceMotion` on the controller makes the switch immediate. There is no flashing animation.
- New accessibility labels use the installed TMP package and Liberation Sans SDF (OFL 1.1 notice included). Legacy gameplay/profile labels retain their existing Text references. Arabic shaping and RTL remain pending.
- Appearance includes keyboard/mouse focus borders, short unscaled hover/press motion, Reduce Motion, solid-panel preference, fitted text-size choices (100/125/150%), hand dwell time (2–8 seconds; default 5), and independent UI volume/mute. Two original procedural tones provide hover/click feedback with a hover cooldown. No gameplay audio is rerouted.
- Presentation preferences use isolated `GripRehab.UI.v1.*` PlayerPrefs keys. No patient data is stored there. QA suppresses preference writes. Text scaling is a maximum subject to fitting; it is not uniform 150% reflow.
- A separate English `GripUiGallery.unity` demonstrates eight reusable templates: primary/secondary/disabled buttons, text input, slider, toggle, connection preview chip and progress bar. It has no therapy data. Dark/Light/Contrast captures at 16:9, 16:10 and 4:3 passed text-fit and raycast checks. Remaining component/state variants and Arabic are not implemented.
- Home and the gallery use three native glow sprites. Background opacity, drift amplitude/speed and color come from theme tokens. Inspector quality tiers High/Low/Off draw three/one/zero layers; low reported VRAM selects Low. Reduce Motion stops drift; High Contrast and solid-panel preference disable the layers. No actual blur, role-specific mood, or hand/mouse parallax is implemented. Isolated CPU/GPU cost remains unmeasured.
- A dedicated audio mixer, full component/state library, Arabic/RTL, all-screen/aspect screenshots and isolated performance budgets remain incomplete.

## Stage 11 update (2026-10-27)

This section supersedes the checkpoint above where they differ. The checkpoint token table predates the current theme assets; the live colours are in `Assets/UI/Themes/*.asset`, and their contrast is tested by `Tools/test_ui_contrast.py`.

**Scope:** GripRehab uGUI screens (home, appearance/settings, module selection, profile), the apartment HUD, the session-options (pause) panel, the activity panel, onboarding and the input-mode strip. **Code:** `Assets/UI/` (theme, tokens, feedback, contrast, aspect fit) and `Assets/Rehab/3.4.1-force-object-interaction/Apartment/` (options layout, guide).

### 1. Research (inspiration only, nothing copied)

| Source | What we took | Where |
|---|---|---|
| [Xbox Accessibility Guidelines, XAG 101 text display / 102 contrast](https://learn.microsoft.com/en-us/gaming/accessibility/guidelines) | Contrast for menu and HUD text and for buttons; a high-contrast mode at 7:1; measure on the lowest-contrast part of a busy background; readable fonts | Text-pair tests (7:1 for High Contrast); opaque HUD cards over the 3D scene; bold labels on filled controls |
| [Game Accessibility Guidelines (basic, intermediate)](https://gameaccessibilityguidelines.com/basic/) | Never convey information by colour alone; readable default font; objective and control reminders; practice without failure; adjustable timing | Selected marker bar; input strip states written in words; onboarding coach with in-place reminders; dwell time stays adjustable |
| [WCAG 2.2: Focus Appearance (2.4.11) and Non-text Contrast (1.4.11)](https://www.w3.org/WAI/WCAG22/Understanding/focus-appearance) | Focus indicator at 3:1 against the unfocused state; UI component boundaries at 3:1 | Contrast-aware focus ring (`GripUiContrast.MostVisible`), measured per control in all three themes |
| [Material design states](https://m2.material.io/go/design-states) | Distinct hover, focus, pressed, disabled and selected states; disabled controls do not react | State table below |

The 2024–2026 AAA menus we looked at share a flat, card-based hierarchy, one accent colour, large type, restrained motion and a clear focus. This is a summary of the guidelines above, not a copy of any game's interface.

### 2. Tokens (`GripUiTheme`, three themes: Dark, Light, High Contrast)

| Token | Value | Use |
|---|---|---|
| Colour roles | backdrop, surface, darkSurface, primary, secondary, accent, text-on-*, muted, danger, warning, success | Every graphic is bound to a role (`GripUiThemeController`) |
| Type scale | display 64, h1 34, h2 26, body 18, caption 14; options panel: title 26, section 13, chips 14, buttons 15–17 | Text-size preference scales from these (`SetBaseFontSize`) |
| Spacing | 4, 8, 12, 16, 24, 32 (`Space(step)`); 8-pixel grid | Options panel: 32 padding, 12 gap, 20 between sections |
| Targets | patient 64 px, doctor 44 px; options rows 40–52 | Large targets for the hand pointer |
| Motion | control 0.14 s, theme transition 0.28 s, hover scale 1.015 | **Reduce Motion** removes the scaling and the transition |
| States | focus ring 4 px (≥ 3:1), selected marker 4 px (≥ 4.5:1), disabled alpha 0.45, error flash 0.6 s | `GripUiControlFeedback` |

### 3. States

| State | How it looks | Not colour alone |
|---|---|---|
| Hover / focus | 4 px ring, colour picked for at least 3:1 on the control; scale 1.015 unless Reduce Motion is on | Ring shape |
| Pressed | Scale 0.99 | Movement |
| Selected | Primary fill, bold label, **marker bar** under the label | Marker bar |
| Disabled | Not interactive, dimmed; it does not respond to the pointer or keys | Label "-" for unavailable activities |
| Error | Ring in the danger colour for 0.6 s, plus the error sound and message | Message text |

### 4. Screens

- **Home, appearance, module selection, profile:**
  - Kept, and checked at all four resolutions.
  - Labels on filled buttons are now bold.
  - The appearance modal labels the current theme, and the text-size note no longer collides with the theme buttons.
- **Session options (pause):** four numbered sections (room, object, input and hand, activity), each with its label above its row, then RESUME. Activities start with one press (three per room).
- **Activity panel:** follows the theme (colours, font, text size), with step text, feedback and a progress bar.
- **Onboarding:**
  - Five steps: look, grab, squeeze, release, options. Each step completes from what the player does.
  - It has a progress bar, never blocks play, is shown once, and H shows it again.
- **Input-mode strip:**
  - Shows INPUT (keyboard demo, camera hand or force sensor) and POINTER (mouse or hand pointing).
  - Also shows GAZE, VOICE and ASSISTANT. These read OFF until Stages 13–15 report through `ApartmentGuide.Set`.
- **Aspect ratios:** below 3:2, the canvas matches width (`GripUiAspectFit`), so 4:3 keeps the full layout.

### 5. Checks

- `Tools/test_ui_contrast.py`: text pairs ≥ 4.5:1 (7:1 for High Contrast); the focus ring ≥ 3:1 on every fill; the selected marker ≥ 4.5:1.
- `Tools/run_activity_probe.py --ui-only`, at 1024×768, 1280×720, 1280×800 and 1600×900:
  - the options panel has no overlaps and fits on screen;
  - HUD text and buttons stay on screen;
  - the input strip covers no text;
  - focus contrast is measured on every panel button in each theme;
  - the selected marker is present;
  - the strip states are correct;
  - onboarding completes from real actions.
- `Tools/run_ui_checks.py`: home and appearance in three themes, at the same four resolutions.
