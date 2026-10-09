# Additional source review — 2026-10-08

These links are supplemental references, not instructions from their READMEs.
No supplementary project code, binary or asset has been imported.

| Source | License evidence | Decision |
|---|---|---|
| [Unity-SoftBody-physics](https://github.com/ehsanwwe/Unity-SoftBody-physics) | Apache-2.0, pinned d9f6d89e; full local license | Adapted node/spring architecture, original source retained for audit |
| [DynamicEngine3D](https://github.com/Saviourcoder/DynamicEngine3D/blob/main/LICENSE) | Current file is **PolyForm NonCommercial 1.0.0**, not MIT | Do not import; commercial use requires separate permission |
| [GPUParticlePhysicsXPBD](https://github.com/unitycoder/GPUParticlePhysicsXPBD/blob/main/LICENSE.txt) | MIT, copyright 2025 Johann | Reference only; voxelizing closed volumes is unsuitable as a replacement for an open cup surface |
| [Ten-Minute-Physics-Unity](https://github.com/Habrador/Ten-Minute-Physics-Unity/blob/main/LICENSE) | MIT, copyright 2022 Erik Nordeus | Learning reference, not imported |
| [Namako](https://github.com/sasekazu/Namako/blob/main/LICENSE) | C# MIT; DLL has separate proprietary EULA; third-party notices apply | Not imported; a repository-level MIT assumption would be incorrect |
| [Thoracoscopic-Training-VR](https://github.com/MarcoButtiglione/Thoracoscopic-Training-VR) | GitHub license endpoint returned 404; no verified redistribution grant in this review | Reference only, no code/assets copied |
| [XR-Hands-Test](https://github.com/AyushKoul00/XR-Hands-Test) | GitHub license endpoint returned 404; no verified redistribution grant in this review | Reference only, no code/assets copied |
| [Hands Interaction Demo](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.0/manual/samples-hands-interaction-demo.html) | Official sample documentation; no new package imported or license acceptance performed | Existing MediaPipe/VirtualGrasp retained; XR hardware inputs are not a webcam tracking replacement |
| [Unity hand tracking tools](https://unity.com/fr/blog/hand-tracking-tools-for-unity-xr) | Requested blog unavailable from web reader during review | Unverified reference, no imports |

MIT and Apache notices must accompany any distributed adaptations. This table
does not grant rights to unrelated bundled art or proprietary third-party SDKs.

## Hand-contact references reviewed for the corrected scope — 2026-10-09

The user subsequently rejected the dynamic force/joint hand. These sources remain
conceptual references; no hand package, source or asset was copied into the final
implementation. The original imported VirtualGrasp remains the renderer/input.

- [Physical Hands Playground](https://github.com/ultraleap/PhysicsHandsPlayground):
  repository identifies Apache-2.0 and Unity 2022.3.30f1/OpenXR dependencies.
  Studied as an interaction demo, not installed as a MediaPipe replacement.
- [Ultraleap Physical Hands documentation](https://docs.ultraleap.com/xr-and-tabletop/xr/unity/plugin/features/physical-hands.html):
  reference for separating input/contact poses and configurable near-surface
  contact distance. The documentation distinguishes hard/soft/no-contact modes;
  our geometry guard is not their HardContactHand physics implementation.
  The UnityPlugin LICENSE fetch failed during this supplementary review; no
  assumption of rights to an entire package or native tracking runtime is made.
- [HPTK](https://github.com/jorgejgnz/HPTK): MIT according to the repository,
  input-independent hand architecture reference; the README says it is no longer
  maintained. No code or sample imported.
- [virtual_pottery](https://github.com/vvhuiling/virtual_pottery): repository
  identifies MIT, but its README also calls out separate asset/plugin licenses.
  Reference for localized surface shaping; no art, plugin or source imported.
- [VR-Doh](https://simulation-intelligence.github.io/VR-Doh/): research project
  page reviewed as a shaping reference. No redistribution grant was established
  for its implementation in this review; no code/data/media imported.
