# Unused code and asset candidates (2026-11-06, release QA)

Found by a read-only scan (`.local/scratch/unused.py`, `unused_assets.py`): a type nothing else names and no scene, prefab or asset uses by GUID, or a file whose GUID nothing references. Self-booting scripts (`RuntimeInitializeOnLoadMethod`), Editor `-executeMethod` tools, build hooks, tests and `Resources/` files were excluded. **Nothing was deleted.** Each removal waits for your OK.

## Code (dead at runtime)
| File | Why it is unused | Note before removing |
|---|---|---|
| `Assets/Rehab/3.4-game-modules/RehabApp.cs` (668 lines) | Legacy controller. Not in any scene (audit A-017); only `RehabAction` names it | On the protected list in `Tools/test_handmate_design.py:53`; update that list and record it with `update_review.py` |
| `Assets/Rehab/3.4-game-modules/RehabAction.cs` | Button helper for `RehabApp` only | Goes with `RehabApp.cs` |
| `Assets/Rehab/3.4-game-modules/LocalPasswordHash.cs` | Superseded by `Assets/Product/PasswordKdf.cs`; nothing calls it | None |

## Assets (unreferenced, 1.6 MB)
| File | Size |
|---|---|
| `Assets/UI/HandMate/Fonts/Montserrat-VariableFont_wght.ttf` | 672 KB (the UI uses `HandMate-Montserrat-Regular.ttf`) |
| `Assets/UI/HandMate/Fonts/Montserrat-Italic-VariableFont_wght.ttf` | 684 KB |
| `Assets/UI/HandMate/Images/module2-locked.jpeg` | 143 KB (Modules 2 and 3 are unlocked now) |
| `Assets/UI/HandMate/Images/module3-locked.jpeg` | 144 KB |
| `Assets/UI/HandMate/Images/back-arrow.png` | 10 KB |

Keep `OFL.txt` (font licence) while any Montserrat or Orbitron font remains.
