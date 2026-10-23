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
