# Corrected deformation and direct-hand contact delivery — WBS 3.3 / 3.4.1

Project: `E:\GP\codex\GripRehab-Unity`, Unity **6000.4.4f1**.
Final evidence UTC: 2026-10-09T00:02:26.062658+00:00 (client timezone Africa/Cairo).

## What ships

The original imported VirtualGrasp 1.7.0-free renderer/skeleton and direct input
are restored. The rejected spring/joint-driven hand, cloned renderer and added
physics-hand layers are removed from Assets and the saved scene. They remain
only in local recovery evidence. No virtual hand is simulated as a soft body.
The game still loads one `Assets/Scenes/GripRehab.unity` scene. Keyboard,
MediaPipe, UI, modules, accounts and imported vendor code are preserved.

The six objects use a baked surface-spring network, at most 96 nodes per
deformable object, four normalized mesh weights and unique triangle-derived
edges. Geometry/UV/material slots and cup cavities are preserved. Existing
cuboid and cup surfaces have separate native refined mesh assets for local
bending; original assets are not overwritten. Position-identical UV/hard-normal
vertices share deformation weights, preventing visible seam tears. Runtime
nodes are arrays, not extra PhysX bodies, mass, joints or allocations per grip.

This is a substantially rewritten, bounded **overdamped implicit surface-spring
adaptation** of [Unity-SoftBody-physics](https://github.com/ehsanwwe/Unity-SoftBody-physics)
at `d9f6d89edcc52517f65d50149e3265b1e30db9e3`, with Apache-2.0 source, LICENSE and
NOTICE retained in `Docs/ThirdParty/UnitySoftBody`. It is not the untouched plugin,
Obi, FEM, XPBD, a volumetric solver, or measured constitutive material physics.

| Material | Max local displacement | Response |
|---|---:|---|
| Silicone ball | 18 mm | Local dents, limited side bulge, elastic recovery; 70 mm grasp diameter |
| Porcelain mug | 0 mm | Rigid, visible damage seams, then 12 source-shaped physical surface fragments |
| Brittle cracker | 3 mm | Slight bending/set, cracks, then 12 fragments |
| Kitchen sponge | 30 mm | Deep local compression, slower recovery, 5% set |
| Silicone cup | 28 mm | Local wall/rim/handle bending, paired inner/outer nodes, cavity retained |
| Ripe tomato | 25 mm | Local compression, accumulating damage, 25% pressure set |

Coefficients and break thresholds are experimental. The total force budget is
divided across verified near-surface contacts; it is never copied to every
finger. The spring backend and legacy dent engine cannot modify the same mesh
together. No contact or invalid input means no additional pressure/damage.
Reset restores vertices, proxy colliders, damage, fragments and ownership.

## Contact and placement correction

`HandSurfaceGuard` constrains only the original visible SDK output. The input
pose remains direct and cannot be contaminated by its constrained output.
Whole fingers sweep along their original three-joint angle ratios and stop at
the live triangle surface. A complete triangle BVH and ray-parity inside test
replace the inaccurate nearest-vertex shortcut. All selected-hand skin vertices are evaluated from the original renderer
bone weights and bind poses, including the palm. These same skin contacts
feed pressure and deformation. A bounded safe-open wrist search precedes finger closing whenever the
neutral hand intersects the object. Original finger joint ratios close to the
actual skin surface; the safe pose is rebuilt after new bulges or movement. The Windows player uses native-baked project-owned HandSkinData because
the imported SDK meshes are not CPU-readable in a player; the original
assets/importers remain untouched. No spring-driven hand or delayed
force-following wrist is installed.

Pickup targets the source hand's aperture, using MCP geometry for solids and
a serialized PIP bias for cups. It does not chase the constrained hand or use
a handle-inflated bounding radius. Surface correction moves the visible hand,
never the player/camera. Existing held kinematic motion, gravity release,
collision exclusion and reset remain.

Heavy pose/pressure calculations run at most once per rendered frame, recheck
freshness before pressure, and consume bounded accumulated physics time. This
prevents repeated catch-up steps from amplifying frame hitches. Contact sweeps
stop at the first unsafe skin vertex, testing exact parity before closest
distance. Wrist translations reuse exact cached bone-weight translation.
Only rigid closed-face interiors use their oriented plane sign; deforming
meshes and edges/open surfaces retain full parity. Independent full-parity
checks cover medium/maximum pressure after the displayed LateUpdate pose;
final diagnostics still inspect the
complete visible skin. Phase 2
connection gaps now use actual receive timestamps rather than processing time;
continuous fresh packets no longer cause a false disconnect after a hitch.
The 500 ms expiry, source selection, flags, calibration and sequence checks
remain in effect. Camera grip is a visual estimate, never load-cell newtons.

The native scene installer saved the guard reference, six contact tolerances,
four refined mesh bindings, the matching 70 mm ball scale and Continuous Dynamic
mode on free objects. No room/UI is regenerated. Original layers 8/9 and global
physics values remain: fixed timestep 13.333 ms (75 Hz), contact offset 10 mm,
solver iterations 6 / velocity iterations 1. These govern body/floor collision;
the explicit hand geometry padding is 0.5 mm, pressure tolerance 1.5 mm.

## Actual verification

| Check | Result |
|---|---|
| Native saved-scene/direct-hand geometry and BVH regressions | 76 assertions passed |
| Independent actual skin contacts, both hands at 30 N | 12 cases passed |
| Native solver, recovery, shared-force, invalid force, seam weights and reset | 78 assertions passed |
| Native Phase 2 input contracts | Passed: units, flags, ordering, duplicate/stale/queued samples, calibration, shape, true reconnect, continuous arrivals during a hitch, synthetic opt-in and rollover |
| Windows headless material matrix | 36 cases / 639 checks passed |
| Windows rendered 1280×720 material matrix | 36 cases / 639 checks passed |
| Existing Windows gameplay regression | All three runs passed: headless, Phase 2 replay and rendered |
| Native cold-scene Play/Stop | Three cycles passed; zero owned runtime deformation meshes after every Stop |
| Python tests from root | 66 passed |
| Offline editor/player compilation and domain/binding regressions | Passed; metadata and five-assembly dependency graph |
| Native Windows build / missing-script and saved-reference checks | Passed |

The sustained-pressure fixture counts the production integrator's accepted
contact duration, rather than engine catch-up time; it exits early for an
actual fracture. Safety dt caps and material thresholds are unchanged.

Each matrix covers six objects × both hands × keyboard / 21-landmark synthetic
camera / Phase 2 UDP replay. It tests light/medium/maximum pressure, sustained
pressure, fracture, release, reset and no-contact damage. Stale-input safety
is covered separately by the native input-contract and player regression tests.
The measured maximum observed skin-vertex penetration in the rendered run was
**1.144 mm**, below the 2 mm
regression limit. This tests the selected SDK skin vertices; it is not continuous triangle/triangle collision detection.
Fixtures do not open the real camera, collect raw images or save a patient.

Rendered right-hand keyboard observations (8 / 30 / 80 N requested, current
gameplay filter/contact path; not maximum theoretical material compression):

| Object | Low / medium / high displacement, mm | Release / break |
|---|---|---|
| Silicone ball | 1.22 / 4.44 / 9.12 | 0.02 mm after 0.7 s |
| Porcelain mug | 0.00 / 0.00 / 0.00 | 12 fragments |
| Brittle cracker | 0.36 / 1.39 / 3.00 | 12 fragments |
| Kitchen sponge | 2.10 / 8.84 / 17.85 | 1.77 mm after 0.7 s |
| Silicone cup | 1.32 / 7.03 / 28.00 | 0.08 mm after 0.7 s |
| Ripe tomato | 2.04 / 7.41 / 20.63 | 6.05 mm after 0.7 s |

Images in `images/` cover every object before/during/after pressure, and both
brittle objects' visible cracks/fractures. They were visually reviewed, including
the corrected cup handle and preserved cavity. Fresh `hand-*.png` full-scene
regression screenshots show the original SDK hand. Source hashes and test timestamps are
recorded in `DELIVERY_RESULTS.json`.

Device: **11th Gen Intel(R) Core(TM) i7-11800H @ 2.30GHz / NVIDIA GeForce RTX 3060 Laptop GPU**. The uncapped synthetic rendered fixture averaged
**102.9 FPS**; contact/pressure work averaged
**12.40 ms**, worst observed **240.69 ms**.
The 60 FPS target is not verified: this full-pressure fixture measured the
value above and contact-search outliers remain. These aggregates include
startup/capture and do not establish sustained live-
camera 60 FPS. Native allocated peak was 251.4 MiB,
managed used peak 16.4 MiB. Headless contact scans before the early-exit optimization averaged
15.58 ms, with 704.48 ms maximum;
the final headless fixture averaged 12.56 ms,
with 235.07 ms maximum. Both records cover the same 36 cases/device, but pressure-duration scheduling
also changed: this historical comparison does not isolate the optimization
or guarantee live CV performance. Zero allocation
is verified only in the warmed production solver loop. See solver-tests.json
for the equal-density, one-contact comparison with the legacy solver; the new
backend is not faster for every small mesh.

## Reproduce and tune

Run from the project root, one player/Editor fixture at a time (shared localhost
ports 27817 / 27818 / 27819):

```powershell
python -m unittest discover -s Tools -p 'test_*.py'
python Tools/offline_verify.py --compile --tests
python Tools/run_deformation_checks.py
python Tools/run_deformation_checks.py --rendered
python Tools/run_player_checks.py
```

Native Editor methods: `HandInteractionCorrection.Install` saves only guard
bindings and verifies/builds; `HandInteractionCorrection.BakeAndVerify` rebakes
mesh weights/topologies and verifies/builds; `SoftBodyVerification.VerifyAndCycles`
runs native solver checks and three Play/Stop cycles. For cycles pass
`-apartment-deformation-cycles` and omit `-quit`; it exits itself.

First tune surface padding, angular sweep, near-surface tolerance, cup pad bias
and maximum wrist correction in Inspector. Material profiles expose displacement,
patch radius, spring coupling, recovery, plastic set and break thresholds.
See `Docs/HAND_OBJECT_INTERACTION.md` for changed files and the Editor checklist.
No package, layer or joint installation is required on the delivered scene.

## Boundaries and preservation

The guard checks actual selected-hand skin vertices, including the palm.
It does not run a separate simulated hand. Finite vertex and angular sweeps
can miss triangle intersections or very thin geometry between samples. Extreme input jumps/crowded geometry can exceed the correction cap.
This is not a guarantee of zero clipping for every real hand pose/calibration.
The solid floor colliders are conservative; cups use 12 walls and a base, with
no separate physical handle collider. Low-node skinning can miss small features;
Voronoi-clipped patches approximate fracture, without physically propagated
cracks or exact watertight fragment volumes. There is no true volumetric mass
conservation, fluid leakage or topology remeshing. Live human CV and actual
Nano/HX711 trials remain unverified; replay is not substituted for them.

The working tree already contained extensive uncommitted work. Local backups
and protected hashes preserve it. Scoped WBS commits include the current versions
of relevant contact/deformation files, some of which also contain existing
pending prerequisites. Unrelated pending work remains untouched. Tests validate
the complete current working tree, not an older clean checkout. Rejected hand
recovery files and large pre-task patches are local evidence, not active code.
See ADDITIONAL_SOURCES.md for the reference/licensing review.
