# Editing the scene workspace

The game uses seven scene files directly inside `Assets/Scenes`, with no subfolders: a small startup scene and six additive content scenes. Open
the same Unity project: `E:\GP\codex\GripRehab-Unity`.

| Scene under `Assets/Scenes` | What to edit |
| --- | --- |
| `3.1-Bootstrap.unity` | Startup/loading and global render settings; entry point for Play and builds |
| `3.1-Apartment.unity` | Furnished apartment, walls, windows, kitchen and furniture |
| `3.1-Lighting.unity` | Daylight and room bounce lights |
| `3.2-PlayerAndSensors.unity` | Player, camera, input skeleton, camera/Phase 2 receivers |
| `3.3-VirtualHands.unity` | Visible VirtualGrasp hand output and SDK controller |
| `3.4.1-Interaction.unity` | Objects, material/deformation assignments, training stations and seat anchors |
| `3.4-Interface.unity` | Saved Canvas, menus, HUD, buttons and EventSystem |

`Assets/UI/GripUiGallery.unity` remains a separate presentation gallery. The
pre-split scene in `Assets/SceneBackups` is a recovery copy, excluded from
builds. It is not the working scene.

## Normal workflow

1. Choose **Grip Rehab > Scenes > Open Complete Workspace**. All six content
   scenes open additively with the startup scene, and references are restored.
2. Expand the relevant scene's `/ content` group in Hierarchy. Collapse the
   others. Select an element to edit its Transform, material or script settings.
3. Save the scene you edited with Ctrl+S; use **File > Save All** for several
   scenes. Objects belong to their original scene even when another scene is
   marked active. Check the scene title in Hierarchy before creating new objects.
4. Press Play from this complete workspace or from `3.1-Bootstrap.unity` alone.
   The latter loads the content automatically. Stop Play before editing.

You can also double-click one content scene to edit a smaller Hierarchy. Its
content becomes visible in Edit mode. For linked Inspector references, lighting
and a full gameplay preview, open the complete workspace. Starting Play from a
content-only scene is prevented; the workspace opens instead. Press Play again.

## References and safe editing

Unity does not persist ordinary object references across scene files. The
project-owned `Assets/Resources/SceneComposition.asset` records the existing
links. Stable `SceneReferenceId` components identify their objects. Startup
loads the content inactive, restores all links, then activates gameplay. Save
hooks remove temporary links before serialization and restore the Edit preview.
This prevents missing references, duplicate cameras or premature `Awake` calls.

Do not remove reference IDs or copy marked objects without assigning new IDs.
Do not drag an object between content scenes or change a reference to an object
in another scene without updating the composition manifest. Those structural
changes need a code review. Normal movement, material edits and settings within
a scene use the usual Inspector workflow. Imported VirtualGrasp code is intact.

Choose **Grip Rehab > Scenes > Verify Composition** after structural edits.
**Configure Build Scenes** restores the startup/content entries in Unity's
global and active scene lists. **Grip Rehab > Build Windows Demo** uses the
manifest rather than recreating the apartment or menus.

The account portal and some exercise UI are created by their existing runtime
scripts. Splitting the saved scene does not turn these generated panels into
new authored scenes or change the patient journey.

## Verification

See `Docs/evidence/flat-scene-folder-2026-10-07/REPORT.md` for the actual
compilation, scene-link, save/reopen, lifecycle and build results and limits.
This is a scene organization change, separate from the graduation task list.
