# Editing the single game scene

The working scene is **Assets/Scenes/GripRehab.unity** in
E:\GP\codex\GripRehab-Unity. Assets/Scenes contains this scene and its .meta
only. The apartment, lighting, player and sensors, VirtualGrasp hands, interaction
objects, Canvas, menus and HUD are saved together in this scene.

Double-click GripRehab in Project, or choose **Grip Rehab > Scenes > Open Complete
Workspace**. That command now opens just the main scene. Expand the existing
named groups in Hierarchy, select an object, edit in Inspector, and save Ctrl+S
outside Play. Normal Play edits are temporary. Build Windows Demo uses the saved
scene; it does not regenerate the environment or UI.

References are ordinary serialized references inside the same scene. No additive
bootstrap, composition manifest, reference markers or cross-scene save hooks are
active for this scene. The imported VirtualGrasp package was not modified.

Assets/UI/GripUiGallery.unity remains an optional UI presentation gallery outside
Scenes. Assets/SceneBackups contains the older pre-split recovery copy, excluded
from builds; it is not the current working scene. The pre-merge snapshot is kept
outside Assets in Docs/evidence/single-scene-2026-10-07/before-merge.zip.

See Docs/evidence/single-scene-2026-10-07/REPORT.md for measured checks.
