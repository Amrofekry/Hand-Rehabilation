# Unity Soft Body attribution and adaptation

Source: https://github.com/ehsanwwe/Unity-SoftBody-physics
Revision: `d9f6d89edcc52517f65d50149e3265b1e30db9e3`.
License: Apache-2.0; full license and unmodified generator are included here.

The project adapts its welded surface nodes, mesh-to-node mapping, central rest
springs and connected surface network. The original is a Unity 2021.3 demo;
the original runtime generator is NOT compiled into the Unity 6 player.

Project modifications: separate Editor baking/runtime; at most 96 nodes;
normal-aware sampling; unique triangle-derived edges; local coordinates;
four normalized skinning weights; preserve source UVs/materials/submeshes and
cup cavity; replace per-node native joints with bounded, overdamped implicit
spring integration. The object Rigidbody carries total mass and gravity once;
internal nodes have no independent collision bodies or duplicate mass.
This intentionally models a surface spring network, not volumetric FEM or XPBD.
Actual visible finger-surface contacts distribute a single force budget;
legacy capsules remain trigger probes only. Owned mesh clones are cleaned
up with the object. No convex-hull conversion is used.

This is a substantially rewritten adaptation, not an unmodified plugin install.
Material coefficients and break thresholds are experimental presentation
parameters, not measured constitutive properties or clinical limits.
