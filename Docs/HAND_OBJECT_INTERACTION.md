> Historical custom implementation. The active scene now uses original Ultraleap Physical Hands; see [REFERENCE_HANDS_INTEGRATION.md](history/REFERENCE_HANDS_INTEGRATION.md). Do not run this older installer over the native reference setup.

# Direct VirtualGrasp hand and object contact — WBS 3.4.1

The user rejected the separate dynamic hand. The shipped scene now uses the
original imported VirtualGrasp 1.7.0-free mesh, skeleton and direct input again.
There are no spring-driven hand bodies, cloned hand renderer or hand joints.
The retired implementation is outside Assets in local recovery evidence.
The game remains one saved scene: `Assets/Scenes/GripRehab.unity`.

## Changed files and responsibilities

| File | Responsibility |
|---|---|
| `Assets/Rehab/3.4.1-force-object-interaction/HandInteraction/HandSurfaceGuard.cs` | Constrain the original visible wrist/finger pose at contacted surfaces; free-space input stays direct. Preserve measured joint ratios along each finger's safe bending arc. |
| `HandInteraction/MeshContactSurface.cs` | Closest point/normal on the complete current triangle surface, accelerated with a cached triangle BVH. Preserve mesh topology, UVs and materials. |
| `HandInteraction/HandSkinSurface.cs` | Evaluate the original visible skin from its bone weights and bind poses; test all selected-hand vertices, including the palm. No duplicate mesh or soft hand. |
| `HandInteraction/HandSkinData.cs` and `.asset` | Native-baked project-owned CPU vertices, weights, bind poses and visible-hand indices. Allows the Windows player to test contact without changing the vendor mesh's Read/Write setting. Re-run the installer after replacing the SDK mesh. |
| `Apartment/FingerContactRig.cs` | Refresh the original 15 kinematic capsule triggers immediately after pose constraints. |
| `Apartment/SqueezableObject.cs` | Resolve the same live contacts for pressure, damage and deformation; retain pickup, gravity release, player exclusion and reset. |
| `Apartment/ApartmentExperience.cs` | Place the object in the original hand's palm-facing aperture; constrain contacts before pressure is applied. |
| `Assets/Editor/HandInteractionCorrection.cs` | Native, idempotent saved-scene installation and build verification. Does not regenerate rooms or UI. |
| `Assets/Editor/HandSurfaceVerification.cs` | Native surface/scale/triangle regression and direct-hand scene checks. |
| `Assets/Editor/HandSkinContactVerification.cs` | Independently evaluate every visible skin vertex against each held object's current surface, on both hands. Check that contact bones are the actual renderer bones. |

The input hand is never changed by surface limits. The visible SDK output gets
the constraints, so a blocked pose is not fed back into camera tracking. Both
hands retain their existing keyboard and 21-landmark inputs. Force source selection,
protocol and 500 ms validity remain unchanged; camera closure is a visual estimate,
not measured load-cell force.

Pressure and pose searches run at most once per rendered frame, with bounded
accumulated physics time. They recheck input validity before applying pressure.
The last visual pose is checked again after mesh deformation and body movement,
so a newly bulging surface cannot pass through the cached finger pose. Safe poses
are reused only when input/object motion and live contact gaps still qualify.
The force receiver now calculates connection gaps from actual packet arrival,
rather than main-thread processing time: a rendering hitch no longer falsely
releases an object while fresh packets are still arriving.

Closed components are identified from welded edge incidence. For rigid objects
only, a closest point strictly inside a closed triangle can use its oriented
plane sign. All deforming meshes, open surfaces, edges and vertices retain
full ray parity. Boolean finger sweeps test parity before computing closest
distance, and wrist translations reuse exact cached skin weights. Independent
full-parity checks at medium/maximum pressure run after the SDK/contact guard's
LateUpdate, so they measure the displayed pose rather than the unconstrained
intermediate SDK pose. Negative object scale also has a
native normal/parity regression. Surface points still come from the complete
live mesh; there is no simplified hull.

## Inspector and setup

The shipped saved scene already references the guard on ApartmentExperience.
No manual layer, package or joint setup is needed. Original layers 8 (hand
probes) and 9 (objects) remain; introduced dynamic-hand layers were removed.

Tune first on the guard: surface padding (0.5 mm), maximum angular sweep step
(4 degrees), binary search iterations (8), maximum wrist correction (150 mm),
and maximum extra opening (15 degrees). Cup pad bias moves the grasp toward the PIP finger pads;
solids use the MCP aperture. Probe radius (9 mm) on FingerContactRig retains legacy trigger compatibility; pressure uses the actual skin. Tune near-
surface pressure tolerance (1.5 mm) on each object. These are geometric tuning
values, not clinical calibration. Debug Gizmos expose the contact probes and cached skin samples;
object contact Gizmos show pressure points. A held body is kinematic without
interpolation so pose queries and pressure use the same surface. Drop restores
its saved interpolation and Continuous Dynamic collision mode.

## Editor checklist

1. Open the saved scene and Play. Confirm one visible selected hand, immediate
   movement in empty space, and unchanged mouse/keyboard/CV controls.
2. Select each of the three living-room and kitchen objects. Pick up normally
   or use the existing camera summon shortcut. Check it lies in the grasp.
3. Press gently, moderately and strongly. Inspect local pressure response;
   the porcelain mug stays rigid and cracks/breaks, the cracker bends slightly
   then breaks, and the four soft objects respond according to their material.
4. Repeat with the other hand. Slide the hand along a curved surface, rotate
   while held, open to release, drop to the floor and reset repeatedly.
5. Confirm pressing does not move the player/camera and reset removes damage,
   fragments and dents. Stop/Play repeatedly to check owned-resource cleanup.

Automated results, measurements and remaining verification are recorded in
`Docs/evidence/deformation-2026-10-08/REPORT.md`; synthetic fixtures are not a
substitute for a live hand or physical Nano/HX711 test.

## Limits

Capsule triggers remain for compatibility, but they do not define the blocking
surface when the skin guard is active. The guard evaluates the original SDK
skin against current object triangles; pressure uses those same skin contacts.
An already intersecting neutral hand is first moved to a bounded safe open
position; finger chains then sweep along their input angle ratios to contact.
The safe pose is rebuilt if the object's surface changes. No dynamic hand bodies are
installed. Vertex tests are not continuous triangle/triangle collision
detection: very thin surfaces, intersections between vertices and extreme
input jumps may still clip. The correction cap can be exceeded by an extreme
input jump or crowded geometry requiring more correction than allowed. Therefore this implementation does not promise
zero clipping for every pose, object or camera calibration. Held rigid-body
movement remains the game's existing kinematic pickup, not a physics-force
grasp or a virtual tactile device. The solid floor colliders are conservative;
cup walls use a compound wall/base proxy rather than recooking the mesh each
frame. See the deformation report for material and performance limitations.
