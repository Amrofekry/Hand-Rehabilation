# 2.1-R2 Dimensioned common-core bench prototype

This revision closes the earlier **nominal sensor/battery envelope mismatch** with a documented compact cell and a larger shared structure. It supplies a specific mounting pattern and force-transfer stack. It is a bench fabrication proposal, not a patient-ready or load-rated grip. The original nine fit meshes remain in the parent folder as R1; do not mix revisions.

## Selected configuration

Use the **HT Sensor TAL220, 100 kg alloy-steel configuration** matching the [manufacturer family drawing](https://cdn.sparkfun.com/datasheets/Sensors/ForceFlex/TAL220M4M5Update.pdf), also saved in `references/`. It specifies an 80 × 12.7 × 12.7 mm body. With its drawing oriented as shown, the loaded left end has M4 holes at x=-35/-20 mm; the fixed right end has M5 holes at x=20/35 mm. All four lie at y=0. Do not substitute the common 10 kg aluminium retail version. Obtain seller confirmation of the 100 kg variant and matching drawing before ordering; current stock has not been established.

`layout.json` is the dimension source. Battery and electronics occupy separate layers in a 100 × 54 mm bay. It retains the protected Adafruit 2011 battery and PowerBoost 1000C. Board envelopes include declared allowances; connector/antenna clearance and actual board height require measurement. Low-profile wiring is assumed. The supplied tray does not accept arbitrary tall headers.

The structural plates are 132 × 80 × 4 mm. External shapes are deliberately oversized: sphere Ø190 mm, cylinder 158 × Ø124 mm, and hexagonal pencil-like shell 174 mm long / 144 mm across corners. **These are bench shape demonstrations, not satisfactory hand-grip ergonomics.** Retaining all selected modules, clear load paths and serviceable mounts has a size cost. A handheld product needs a subsequent compact packaging/ergonomic revision; the PDF does not establish its feasibility. This conflict is tracked in PROJECT_PLAN.md.

## Parts and manufacturing

| Export | Qty | Proposed process / role |
| --- | --- | --- |
| `tray.stl`, `deck.stl` | 1 each | Printed insulated electronics carrier and separator; no grip load through the battery or boards |
| `base_plate.dxf`, `top_plate.dxf` | 1 each | Cut/drill 4 mm aluminium plate; verify stock grade, stiffness, edge finish and fastener design before loaded use |
| `fixed_spacer.dxf`, `active_spacer.dxf` | 1 each | Machine 4 mm metal spacers, bearing only on the solid sensor end regions |
| `stop_sleeve.stl` | 4 | Fit gauge for proposed stop height; replace with adjustable metal stop stack for load testing |
| Each shape's `_bottom.stl`, `_top.stl` | 1 pair | Fit/bench print; common attachment pattern across all shapes |

Plate/spacer STLs are geometry previews, **not instructions to print load-bearing replacements**. DXFs are 1:1 XY outlines in millimetres; verify scale against the 132 mm plate width. CAD z positions are assembly coordinates: use slicer “drop to bed” for printed parts. Source STL surfaces do not establish printability without support/orientation review.

## Assembly and force path

1. Fit the battery without compression below the 14 mm deck. Insulate and restrain it; use the deck slots for light cable ties without crossing the cell. Mount boards above the deck with insulated spacers and low-profile wiring. Service openings are at both x ends. Keep the Nano antenna clear of metal and verify radio performance in the assembled core.
2. Attach the lower shell to the base plate at x=±60, y=±33 using four M4 through-fasteners, large washers and retained nuts. The four lower posts connect the shell floor to the base. Measure screw lengths on the actual print; no screw may contact the battery. These joints need mechanical review before carrying force.
3. Attach the sensor's fixed right end to the base through the fixed spacer using the two M5 holes. The base top is z=34, spacer top z=38, and sensor top z=50.7. Screws enter from below the base. Select engagement from the actual threaded depth; do not bottom screws or clamp the flexure.
4. Attach the active left end to the upper plate through its 4 mm spacer, with M4 screws from above. Upper plate bottom is z=54.7. Attach the upper shell to its plate using its own four M4 fasteners. No fastener directly joins the two shells across the moving split.
5. Fit four 4 mm guide rods at x=±48, y=±25, with retained ends and 4.6 mm moving clearances. Guides constrain separation; they must not bind or carry parallel sensing force during ordinary measurement. Stop sleeves surround them. Retainers, threads/collars and real rod lengths must be fitted from measured stock.
6. The proposed compression-stop height is 20.45 mm, leaving a nominal 0.25 mm gap below the moving plate. **That gap is a setup placeholder.** The sensor drawing gives no rated deflection. Measure load/deflection with a reviewed fixture, then set/lock metal stops below damaging travel. A 0.25 mm gap is not a 100 kg overload guarantee. Do not use a printed sleeve to prove overload protection.
7. Initial R2 uses the sensor's elastic return, without auxiliary springs. This is an explicit departure from the PDF's illustrated spring arrangement. Added springs create a parallel force path; their stiffness, preload and assembled calibration would need validation. Repeat calibration after every shape change.

Intended sensing path: upper shell → upper plate → active spacer → cell flexure → fixed spacer → base plate → lower shell. The central flexure remains clear. With no applied force, check the upper plate has no unintended contact with the fixed right end, electronics, guides or lower shell.

## Reproduce and verify

```powershell
python hardware/2.1-interchangeable-grips/rev2/build.py --openscad <openscad.exe>
python hardware/2.1-interchangeable-grips/rev2/build.py --verify-only
```

The script checks nominal electronics bounding boxes for containment and pairwise intersection, the manufacturer's hole pattern, vertical stack interfaces, closed mesh edges and export hashes. These checks do not inspect cable flexibility, manufacturing tolerances, radio clearance, strength or clinical usability. Review `exports/verification.json` for exactly what passed. `assembly.scad` shows coloured component envelope mocks; the sensor mock is not a manufacturing model.

Before live use, complete the existing physical fit and bench reports, obtain mechanical review, validate stop setting and repeatability, and verify the [R2 electrical arrangement](../../2.2-electronics/REV2_CIRCUIT.md). No physical test has been performed remotely.


Review update (2026-09-13): corrected the deck support contact, through-slots and sphere service openings; regenerated all exports. Validation now runs under `python -O`, rejects unsupported carrier-interface changes and nonfinite dimensions, checks the base/spacer contact, and binds exported meshes and DXFs to the source files. Some interfaces remain fixed in `assembly.scad`; changes outside supported parameters require coordinated CAD/validator edits rather than changing JSON alone. Verify-only mode does not write a new mesh report.
