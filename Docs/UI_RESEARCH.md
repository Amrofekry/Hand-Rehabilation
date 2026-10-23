# Visual research, scoped to the existing Unity interface

Sources were reviewed on 2026-09-29. These are design ideas, not imported HTML/CSS or a claim that every component is implemented. `Docs/ui_references/` did not exist, so the saved Unity screenshots and [earlier Figma board](https://www.figma.com/design/2xyEji05scuske6RwWG8g4/GripRehab-Phase-3-Menu-and-HUD) remain the primary local reference.

| # | Source | Concrete idea extracted | Unity application / status |
| --- | --- | --- | --- |
| 1 | [shadcn Button](https://ui.shadcn.com/docs/components/aria/button) | Give primary, secondary and disabled actions distinct fills | Home buttons; existing, theme tokens added |
| 2 | [shadcn Button](https://ui.shadcn.com/docs/components/aria/button) | Reserve a spinner state for a pending action | Future loading component; pending |
| 3 | [shadcn Button](https://ui.shadcn.com/docs/components/aria/button) | Put icon on action side with consistent gap | Module cards; proposed, no icon imported |
| 4 | [daisyUI Themes](https://daisyui.com/docs/themes/) | Semantic color roles instead of per-control hex values | `GripUiTheme` ScriptableObjects; added |
| 5 | [daisyUI Themes](https://daisyui.com/docs/themes/) | Separate foreground tokens from each fill | Theme text-on-primary/surface; added |
| 6 | [Flowbite Alerts](https://flowbite.com/docs/components/alerts/) | Distinguish info, warning, error, success with text and icon | Future feedback cards; pending |
| 7 | [Flowbite Alerts](https://flowbite.com/docs/components/alerts/) | Dismiss noncritical alerts; keep crucial message visible | Future toasts; pending |
| 8 | [Material 3 Foundations](https://m3.material.io/foundations/) | Use containment to group related controls | Home action card and module cards; existing |
| 9 | [Material 3 Foundations](https://m3.material.io/foundations/) | Express interaction states consistently | Button color states; existing |
| 10 | [Material 3 Foundations](https://m3.material.io/foundations/) | Keep type and spacing values as design tokens | `GripUiTheme` scale/grid; added |
| 11 | [Google expressive research](https://design.google/library/expressive-material-design-google-research) | One visually dominant next action per patient page | Home Start action; existing |
| 12 | [Google expressive research](https://design.google/library/expressive-material-design-google-research) | Use shape/size/containment to aid scanning | Profile grouping; proposed |
| 13 | [Google accessible research](https://design.google/library/designers-guide-accessibility-research) | Test with limited dexterity rather than relying on small gestures | Patient target-size review; pending live usability |
| 14 | [Google accessible research](https://design.google/library/designers-guide-accessibility-research) | Do not encode status only by color | Force source chip text; existing |
| 15 | [WCAG 2.2](https://www.w3.org/TR/wcag/) | Body text/background contrast >=4.5:1 | Theme palette table; calculated |
| 16 | [WCAG 2.2](https://www.w3.org/TR/wcag/) | Focus indication must remain visible | Planned focus ring; pending |
| 17 | [WCAG 2.2](https://www.w3.org/TR/wcag/) | Avoid flashing more than 3 times/second | Avoided flashing UI; current theme fade only |
| 18 | [WCAG 2.2](https://www.w3.org/TR/wcag/) | Generous target spacing helps users with motor impairment | Appearance theme controls, 68px; added |
| 19 | [Kenney UI Pack](https://kenney.nl/assets/ui-pack) | Nine-slice panels scale without stretched corners | Existing local `Rounded96.png`; no Kenney asset copied |
| 20 | [Lucide design language](https://lucide.dev/guide/design/icon-design-guide) | Use one consistent icon grid/stroke | Future icon set; no icons copied |
| 21 | [Cairo font metadata](https://github.com/google/fonts/blob/main/ofl/cairo/METADATA.pb) | Choose one Arabic+Latin family if proper shaping is added | Arabic typography candidate; not imported |
| 22 | [Easings.net](https://easings.net/) | Ease out on entering controls rather than linear drift | Theme color interpolation uses SmoothStep; added |
| 23 | [Google motion](https://design.google/library/making-motion-meaningful) | Motion should communicate transition, not obstruct it | Theme crossfade 280ms; added |
| 24 | [Unity Canvas Scaler](https://docs.unity3d.com/Manual/script-CanvasScaler.html) | Scale from a reference resolution | Existing 1600×900 Canvas Scaler; verified |
| 25 | [Unity multi-resolution UI](https://docs.unity3d.com/Manual/HOWTO-UIMultiResolution.html) | Anchor panels to screen edges | HUD layout review; pending aspect-ratio checks |
| 26 | [Game UI Database](https://www.gameuidatabase.com/) | Compare menu information density with in-game HUD | Reference site blocked by robots; local screenshots used instead |

## Reference to Unity mapping

| Reference concept | Unity location |
| --- | --- |
| Semantic color tokens | `Assets/UI/GripUiTheme.cs`, `Assets/UI/Themes/*.asset` when Editor authoring completes |
| Accessible large action | `Assets/Scenes/GripRehab.unity` / Home / Appearance settings |
| Theme switch | `Assets/UI/GripUiThemeController.cs` |
| Nine-slice card | `Assets/Rehab/3.4-game-modules/UI/Rounded96.png` |
| Existing patient menu/HUD | `Assets/Scenes/GripRehab.unity` |

## Rejected for this pass

1. Imported web component code: DOM/Tailwind does not run in uGUI, and replacing current navigation would violate the protected-logic rule.
2. Live particle/aurora background: GPU cost is unknown on the target laptop; keep the saved static backdrop until profiling.
3. Full-screen blur: adds render cost and hurts low-vision readability; use opaque surfaces until a measured fallback exists.
4. Icon-only patient actions: hand-cursor users need large labeled targets and no ambiguity.
5. Fast spring/bounce on warnings: unnecessary motion for a rehabilitation task; use a calm, optional fade.

The research is intentionally brief. Game UI Database could not be fetched due to robots restrictions, and the Material 3 site rendered JavaScript-only content in the search tool; those entries are high-level ideas from accessible search descriptions, not detailed copied layouts.
