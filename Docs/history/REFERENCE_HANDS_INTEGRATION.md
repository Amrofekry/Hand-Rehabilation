> 12 October 2026: restored the earlier VirtualGrasp prototype for Unity 2022.3.0f1 at the user's request. Later hand/contact/soft-body evidence below is historical. See [restoration report](../evidence/rollback-2026-10-12/) and [current 23 tasks](SOFTWARE_TASKS_23.md).

# Original PhysicsHandsPlayground hand integration — WBS 3.4.1

The active apartment scene now uses Ultraleap's original **GenericHand_Arm** prefab, **PhysicalHandsManager / HardContactParent / HardContactHand / HardContactBone**, and **GrabHelper / GrabHelperObject**. The visible hand is bound to the original engine's physical output. The previous VirtualGrasp renderer, custom procedural forearms, skin constraint solver and legacy contact probes are disabled in the saved scene; their source remains available for recovery.

## What was imported

The supplied ZIP matches PhysicsHandsPlayground commit `3a6009926a12957a6c9642e9f172e02fba16c067`. Its missing Ultraleap submodule was recovered at the exact referenced UnityPlugin commit `9fbbe8d664d7671309a06e544a4b25a6823e3452`. Git LFS pointers were resolved to the real original FBX models. Importing the ZIP alone would not supply a working hand engine.

- `Packages/com.ultraleap.tracking`: original 7.1.0 source, hand/arm mesh, original hand binder, native contact/grip logic and license. The source manifest records all installed file hashes.
- Five SDK source compatibility edits only: Unity 6 PhysicsMaterial naming in three files; an invalid SerializeField attribute moved from a property to its backing field in CapsuleHand. The old settings panel excludes properties removed in Unity 6 and its unsupported rational timestep; our native installer configures that timestep directly, preventing repeated Editor NullReferenceExceptions. No ContactBone, GrabHelper, GrabHelperObject or hand-drive equations were replaced.
- Original SDK and demo licenses: Apache-2.0. Embedded Input System 1.7.0: Unity Companion License, separately included. Input System was downloaded from Unity's official package endpoint with certificate verification; no TLS checks were disabled.
- Gray hand material has the original color and a compatible URP shader. The original hand and forearm geometry are retained.

Upstream: https://github.com/ultraleap/PhysicsHandsPlayground and https://github.com/ultraleap/UnityPlugin

## Necessary glue, not a replacement physics solver

The reference consumes Ultraleap/OpenXR hand frames, not MediaPipe UDP messages or our keyboard controls. `UltraleapRehabProvider` converts the existing validated pose input to Leap frames using the original SDK hand dimensions and VectorHand decoder. `UltraleapRehabInteraction` reads original ContactBone contacts and GrabHelper ownership into the existing game's HUD and deformation backend.

Native hand-drive collision impulses are excluded from the world-impact damage route: they are not calibrated sensor measurements. Existing validated force/contact pressure and floor/table drop damage retain their equations and thresholds.

This conversion is project code. It is **not** an original Ultraleap sensor driver, and no claim is made that webcam-derived input has the same quality as the reference's sensor data. MediaPipe, its protocol, the deformation equations, UI, profiles and modules have not been replaced by the demo project.

The prefab initially generated unused Leap sensor components before its input was wired. These components are removed by the installer: only the validated project adapter provides input, and no extra native sensor polling thread is started.

Original GrabHelper requires a thumb OR palm contact plus qualifying opposing finger contact. It does not guarantee every arbitrary two-finger combination. The adapter does not force a successful grab, mark a relocated object as held, or parent a held object to the hand.

## Opening and testing

Open `E:\GP\codex\GripRehab-Unity`; main scene: `Assets/Scenes/GripRehab.unity`. There is still one main apartment scene. Existing Start/Continue and input choices remain.

1. Select Keyboard Demo and the desired hand/object. Space applies demo closure; existing per-finger controls remain available.
2. Open the selected hand first. G explicitly presents the selected object to the hand; after presentation, close your fingers or hold Space. Assistance is refused for a closed hand or a blocked world placement. E uses the existing assisted action. A successful presentation is not a successful grip: the original engine must acquire contact.
3. Q releases, R resets; original physics handles gravity and collisions. Pause/invalid input stops pressure and withdraws the input frame.
4. Camera and calibrated Phase 2 feed the same native engine through the adapter. No silent keyboard fallback is introduced. Camera closure remains a visual force estimate, not measured Newtons.

The original models are under `Ultraleap reference hand interaction / Original GenericHand_Arm (PhysicsHandsPlayground)`. Do **not** run the older hand-correction/first-person authoring installers over this setup. The idempotent original-hand installer is `GripRehab → Hand interaction → Install original Ultraleap reference hands`.

## Inspector settings and layers

- Provider: seated wrist lift (0.1 m keeps the aperture above the existing table), selected-hand wrist/elbow positions, palm rest orientation, lockWristPosition, boneWidths and release suppression. Wrist anchoring is retained as the previous requested first-person presentation; it is not a claim of unrestricted real-world wrist translation.
- Original PhysicalHandsManager: HardContact mode and contact distance 0.002 m. Original HardContactParent bone mass 0.1 kg; original drive and ghost/reset settings remain available.
- G/E assistance: aperture direction/clearance fields. Cup axis aligns to the hand direction for explicit assistance. These are placement settings, not a contact/grip solver.
- Layer 8 Player and 9 Grippable were reserved before installing the original engine, which uses 10 PhysicalHands and 11 PhysicalHandsReset. Hands contact Grippable; reset hands ignore collisions. Unnamed pre-existing layers were not treated as free.
- Six existing rigidbodies: ContinuousDynamic, 24 position / 16 velocity solver iterations, maximum depenetration velocity 1 m/s and per-collider contact offset 0.001 m. Original hand solver: 30 / 20. The supplied demo physics settings are applied natively: 90 Hz fixed step, 0.03 s maximum timestep, 0.001 m default contact offset, 15 / 5 default solver iterations, auto sync disabled. Earth gravity is preserved instead of the reduced gravity in the demo.

Tune input geometry/scale and assistance placement first, then the original contact distance and native drives. Raising contact distance is not proof of eliminating visible clipping. Inspect inner cup surfaces as well as the exterior.

## Evidence and current acceptance

See `evidence/reference-hands-2026-10-10/REPORT.md` and the machine-readable results. The native and executable fixtures intentionally use synthetic closures, artificial aperture placement and disabled gravity during the grip measurement; they are not natural reach, webcam or physical-hardware trials. Each native case stores before/contact/released screenshots. A contact image does not imply that the sustained-grip assertion passed.

Import/build success is separate from interaction acceptance. Failed grips, measured penetration and unverified live-camera behavior stay open; this integration must not be described as a zero-clipping, fully validated final interaction release.

## Recovery

Pre-import source/settings/scene snapshot: `Logs/recovery/2026-10-10-reference-import/source-before.zip`, with the pre-import binary Git diff and status alongside it. These contain the existing uncommitted work. Do not reset the repository or run the scene generator to recover only this change. Use the native scene installer and original source snapshot for a scoped recovery.
