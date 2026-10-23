# 2.1 Interchangeable grip fit prototype

**Latest design: [R2 common-core bench package](rev2/README.md)** adds a selected compact cell, dimensioned metal mounts, component layout, 13 checked meshes and four DXF templates. The files described below are the preserved R1 concept. R2 remains oversized and requires physical/mechanical validation.

`grip_core.scad` is editable parametric CAD in millimetres. It creates a shared electronics tray/lid, slotted load-transfer platen, and split Sphere/Cylinder/Pencil-like grip shells with the same core cavity, guide holes, sensor bay and spring pockets. `exports/` contains generated meshes when the OpenSCAD export check passes.

**Release status: fit/concept prototype only.** The PDF has no dimensional drawing or exact sensor part. Its small sphere/pencil render does not demonstrate that the same battery, Nano and 100 kg cell physically fit. This design therefore uses explicit configurable envelopes: core 86 x 32 x 24 mm; provisional cell 80 x 13 x 13 mm; sphere diameter 110 mm; cylinder 108 x diameter 62 mm; hexagonal pencil-like grip 124 mm long, 62 mm across corners. These large shapes are a feasibility finding, not clinically selected ergonomics. A slimmer pencil will require a remote electronics core or substantially different packaging, which changes the architecture and must be reviewed.

## Export

Run `python hardware/2.1-interchangeable-grips/export_parts.py --openscad <path-to-openscad.exe>`. The script checks subprocess failures, renders each of nine parts, and writes SHA-256 hashes. OpenSCAD is available from its [official download page](https://openscad.org/downloads.html).

Run `python hardware/2.1-interchangeable-grips/export_parts.py --verify-only` to check the supplied meshes without OpenSCAD. `render_preview.py` uses Pillow to recreate `assembly-preview.png` directly from the exported geometry; its exploded offsets illustrate the parts rather than a working load path.

## Assembly / load-path design

1. Start with a tray/lid and shell clearance print, no battery or patient loading. Measure the actual selected boards/cell with calipers and revise envelope parameters.
2. Populate the serviceable tray with electrically insulated mounts. Confirm USB/cable routing, antenna clearance, battery restraint and no contact between solder joints and force-bearing hardware.
3. Select the exact load cell and design its fixed-end and active-end mounting adapter from the manufacturer's drawing. The current sensor bay and slotted platen reserve space; they are NOT a completed cell-specific mount. Do not clamp the sensing flexure.
4. Intended force chain: bottom shell reaction support -> fixed cell mount -> sensing element -> active mount/platen -> moving top shell. Electronics must not carry the force. The current tray must be isolated from this load path; verify this with an adapter before force testing.
5. Use guide rods/retainers so halves move without detaching. Fit return springs in the four pockets; spring rates, preload, travel and mechanical overload stops must be selected and measured. Springs can bypass sensor force, so calibrate the entire assembled grip, not just a bare sensor.
6. Test each shape with the same core, confirming clearance through motion and no sharp/pinch contact at the split. Any protective flexible cover changes the load path and requires recalibration.

## Initial fit-print settings and acceptance

Starting print proposal: 0.2 mm layers, four walls, 30% infill, PLA for fit prototypes only. Orient shell split faces on the bed where practical; inspect support needs for sensor bays. These settings do not establish strength. A load-bearing material/process, fasteners and design factor require mechanical review before loaded use.

Record measured dimensions, printer/material, CAD revision, shell-to-core clearance, guide binding, spring travel and repeat assembly in `fit_report.csv`. No real measurements have been supplied. W2 CAD artifacts are delivered; printing, cell-specific adapter, strength/fit verification and final ergonomics remain open under 2.1's W2-W3 window.
