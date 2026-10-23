# Reproducible environment

Checked 2026-09-28 on the user's Windows machine. Delivery path: `E:\GP\codex\GripRehab-Unity`. Unity project version: `6000.4.4f1` (`ProjectSettings/ProjectVersion.txt`). URP `17.0.1`, uGUI `2.0.0`, Test Framework `1.4.2`, XR Management `4.7.0` (`Packages/manifest.json`). Legacy input is currently active; migrate deliberately rather than accidentally changing gameplay. The imported VirtualGrasp 1.7.0 is a third-party asset and is not edited by this product work.

Python bridge dependencies are listed in `requirements-ble.txt` and `requirements-vision.txt`. To run the existing Python checks from the Unity root:

```powershell
python -m unittest discover -s Tools/Vision -p 'test_*.py'
python -m unittest discover -s Tools/Phase2Bridge -p 'test_*.py'
```

The existing rendered-player runner is `python Tools/run_player_checks.py` and requires a current Windows build under the documented `Builds/` location. `Docs/QA.md` records its prior output. Logs and generated Builds are ignored by Git; reproducible evidence should be summarized in tracked docs and include command, date, exit status, Unity version and actual hardware used.

Source document copies and hashes: `Source/Hand_Rehab_Pipeline.pdf` `D23B3B8ED6FA476ACDB9DA0F0308589037AE82F3460566A2ADE883D45408199E`; `Source/ganttchart.html` `53456905BAA976E8D3C3CC54B4710073BDDCA71061B18E43EB8A9573CA90CC1C`; `Source/Blockdiagram.pdf` `9DFC8CB44A41D71B35517AB20CC6D64BCE49E7557C20A3FE39F4DA3100EECF80`. Originals remain at their source paths.
